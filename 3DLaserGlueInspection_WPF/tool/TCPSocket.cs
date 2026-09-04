using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using TCPIP;

namespace BaslerCamera.IO
{
    public class DataExchange : EventArgs
    {
        public EndPoint ip { get; set; }
        public Socket tmpSkt { get; set; }
        public string data { get; set; }
    }

    public class TCP_Client
    {
        public TCP_Client(string ip, int port)
        {
            this.ipAndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        // ======================== 事件 ========================

        public delegate void DelDataArrived(DataExchange tmp);
        /// <summary>收到数据时触发</summary>
        public event DelDataArrived OnDataArrivedEvent;

        /// <summary>收到数据（简化版）</summary>
        public event Action<string> OnDataReceived;

        /// <summary>连接状态变化时触发 (true=已连接, false=已断开)</summary>
        public event Action<bool> OnConnectionStateChanged;

        // ======================== 属性 ========================

        private IPEndPoint _IpAndPoint;
        public IPEndPoint ipAndPoint
        {
            get { return _IpAndPoint; }
            set { _IpAndPoint = value; }
        }

        private Socket _MySocket;
        public Socket mySocket
        {
            get { return _MySocket; }
            private set { _MySocket = value; }
        }

        /// <summary>当前是否处于已连接状态</summary>
        public bool IsConnected
        {
            get
            {
                try
                {
                    return _MySocket != null && _MySocket.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        // ======================== 内部状态 ========================

        private readonly object _lock = new object();
        private bool _isRunning = false;          // 用户是否要求保持连接
        private bool _isReconnecting = false;     // 防止重连线程重复启动

        // 自动重连参数
        private const int RECONNECT_INTERVAL_MS = 3000;   // 重连间隔（毫秒）
        private const int CONNECT_TIMEOUT_MS = 5000;       // 单次连接超时（毫秒）

        private Thread _reconnectThread;
        private CancellationTokenSource _receiveCts;  // 用于取消接收循环

        // ======================== 公开方法 ========================

        /// <summary>
        /// 启动连接，并开启自动重连
        /// </summary>
        public void Connect()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            // 首次尝试连接
            TryConnectOnce();

            // 启动自动重连线程
            StartReconnectThread();
        }

        /// <summary>
        /// 断开连接，并停止自动重连
        /// </summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                _isRunning = false;
            }

            CloseSocket();

            // 重连线程会在下一次循环检测到 _isRunning == false 后自行退出
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        public bool Send(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;

            try
            {
                Socket skt = _MySocket;
                if (skt != null && skt.Connected)
                {
                    byte[] data = Encoding.Default.GetBytes(msg);
                    skt.Send(data, SocketFlags.None);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP_Client] 发送失败: {ex.Message}");
                // 发送失败触发断开处理，让重连线程接管
                HandleDisconnect();
            }
            return false;
        }

        // ======================== 连接逻辑 ========================

        /// <summary>
        /// 尝试连接一次（带超时），成功则启动接收
        /// </summary>
        private bool TryConnectOnce()
        {
            // 先关掉旧的
            CloseSocket();

            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // 使用异步连接 + 超时控制
                IAsyncResult result = socket.BeginConnect(ipAndPoint, null, null);
                bool connected = result.AsyncWaitHandle.WaitOne(CONNECT_TIMEOUT_MS, true);

                if (!connected)
                {
                    socket.Close();
                    Console.WriteLine($"[TCP_Client] 连接超时: {ipAndPoint}");
                    return false;
                }

                // 检查连接是否真正成功
                socket.EndConnect(result);

                _MySocket = socket;
                Console.WriteLine($"[TCP_Client] 已连接到 {ipAndPoint}");

                // 通知状态变化
                OnConnectionStateChanged?.Invoke(true);

                // 启动数据接收
                StartReceiveLoop();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP_Client] 连接失败: {ex.Message}");
                try { socket?.Close(); } catch { }
                return false;
            }
        }

        // ======================== 自动重连线程 ========================

        private void StartReconnectThread()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            _reconnectThread = new Thread(ReconnectLoop)
            {
                IsBackground = true,
                Name = "TCP_Client_Reconnect"
            };
            _reconnectThread.Start();
        }

        private void ReconnectLoop()
        {
            Console.WriteLine("[TCP_Client] 自动重连线程已启动。");

            while (true)
            {
                // 每隔一段时间检查一次
                Thread.Sleep(RECONNECT_INTERVAL_MS);

                // 用户主动断开，退出重连线程
                if (!_isRunning)
                {
                    Console.WriteLine("[TCP_Client] 已停止，重连线程退出。");
                    break;
                }

                // 连接正常，跳过
                if (IsConnected)
                    continue;

                // 连接断开，尝试重连
                Console.WriteLine($"[TCP_Client] 正在尝试重连 {ipAndPoint} ...");
                bool ok = TryConnectOnce();

                if (ok)
                {
                    Console.WriteLine("[TCP_Client] 重连成功。");
                }
                else
                {
                    Console.WriteLine($"[TCP_Client] 重连失败，{RECONNECT_INTERVAL_MS / 1000}秒后重试。");
                }
            }

            _isReconnecting = false;
        }

        // ======================== 接收逻辑 ========================

        private void StartReceiveLoop()
        {
            // 取消上一轮接收（如果有）
            _receiveCts?.Cancel();
            _receiveCts = new CancellationTokenSource();
            var token = _receiveCts.Token;

            Task.Run(() => ReceiveLoop(token), token);
        }

        private void ReceiveLoop(CancellationToken token)
        {
            Socket skt = _MySocket;
            if (skt == null) return;

            byte[] buffer = new byte[1024 * 1024];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!skt.Connected)
                    {
                        Console.WriteLine("[TCP_Client] 检测到连接已断开。");
                        break;
                    }

                    int len = skt.Receive(buffer, 0, buffer.Length, SocketFlags.None);

                    if (len > 0)
                    {
                        string msg = Encoding.Default.GetString(buffer, 0, len);

                        // 触发 DataExchange 事件
                        OnDataArrivedEvent?.Invoke(new DataExchange
                        {
                            ip = skt.RemoteEndPoint,
                            tmpSkt = skt,
                            data = msg
                        });

                        // 触发简化事件
                        //OnDataReceived?.Invoke(msg);
                        if (OnDataReceived != null)
                        {
                            OnDataReceived(msg);
                        }
                    }
                    else
                    {
                        // len == 0，对端正常关闭
                        Console.WriteLine("[TCP_Client] 服务器关闭了连接。");
                        break;
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[TCP_Client] 接收异常: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Socket 已被关闭，正常退出
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP_Client] 未知异常: {ex.Message}");
            }
            finally
            {
                // 接收循环结束，触发断开处理
                if (_isRunning)
                {
                    HandleDisconnect();
                }
            }
        }

        // ======================== 断开处理 ========================

        private void HandleDisconnect()
        {
            CloseSocket();
            Console.WriteLine("[TCP_Client] 连接已断开，等待自动重连...");
            OnConnectionStateChanged?.Invoke(false);
            // 重连线程会自动检测到并尝试重连
        }

        private void CloseSocket()
        {
            // 取消接收任务
            _receiveCts?.Cancel();

            if (_MySocket != null)
            {
                try
                {
                    if (_MySocket.Connected)
                        _MySocket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                finally
                {
                    try { _MySocket.Close(); } catch { }
                    _MySocket = null;
                }
            }
        }
    }
}
