using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPIP;

namespace BaslerCamera.IO
{
    /// <summary>
    /// UDP 通信端点。
    ///
    /// 该类的公共调用方式尽量与 TCP_Client 保持一致，方便现有机器人通信类切换通信协议。
    /// 需要注意：UDP 没有 TCP 意义上的连接，Connect() 这里只负责创建 Socket、绑定本地端口并启动接收循环。
    /// </summary>
    public class UDP
    {
        /// <summary>
        /// 创建一个 UDP 通信端点。
        /// port 只表示远端端口。本地监听端口通过 LocalPort 单独设置。
        /// </summary>
        public UDP(string ip, int port)
        {
            ipAndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            // 0 表示由操作系统自动分配本地端口，避免默认与远端端口产生混淆或冲突。
            //LocalPort = port;
        }

        // ======================== 事件 ========================

        /// <summary>
        /// 收到数据时触发的完整数据事件。
        /// </summary>
        public delegate void DelDataArrived(DataExchange tmp);

        public event DelDataArrived OnDataArrivedEvent;

        /// <summary>
        /// 收到数据时触发的字符串事件。
        /// KawasakiRobot 目前使用该事件处理机器人位姿数据。
        /// </summary>
        public event Action<string> OnDataReceived;

        /// <summary>
        /// UDP Socket 状态发生变化时触发。
        /// true 表示本地 Socket 已成功创建并绑定，false 表示 Socket 已关闭或发生通信异常。
        /// 该事件不代表远端机器人一定在线。
        /// </summary>
        public event Action<bool> OnConnectionStateChanged;

        // ======================== 属性 ========================

        private IPEndPoint _ipAndPoint;

        /// <summary>
        /// 默认发送目标端点。
        /// 如果已经收到过数据，Send() 会优先回复到最近一次收到数据的发送端点。
        /// </summary>
        public IPEndPoint ipAndPoint
        {
            get { return _ipAndPoint; }
            set { _ipAndPoint = value; }
        }

        /// <summary>
        /// 远端 UDP 端口。
        /// 构造函数中的 port 参数就是该端口；该属性用于运行过程中读取或修改远端端口。
        /// </summary>
        public int RemotePort
        {
            get
            {
                return ipAndPoint == null ? 0 : ipAndPoint.Port;
            }
            set
            {
                if (ipAndPoint == null)
                {
                    throw new InvalidOperationException("UDP 远端端点未设置。");
                }

                ipAndPoint.Port = value;
            }
        }

        private Socket _mySocket;

        /// <summary>
        /// 当前 UDP Socket。
        /// 为了与 TCP_Client 保持一致，保留 mySocket 属性。
        /// </summary>
        public Socket mySocket
        {
            get { return _mySocket; }
            private set { _mySocket = value; }
        }

        /// <summary>
        /// 本地 UDP 监听端口。
        /// 必须在 Connect() 之前设置；设置为 0 时由操作系统自动分配端口。
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// 当前是否已经创建并绑定本地 UDP Socket。
        /// UDP 没有 TCP 意义上的连接握手，因此该状态不能证明远端机器人在线。
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isRunning && _mySocket != null;
                }
            }
        }

        /// <summary>
        /// 实际绑定成功后的本地端点。
        /// 当 LocalPort 为 0 时，可通过该属性获取系统分配的端口。
        /// </summary>
        public IPEndPoint LocalEndPoint { get; private set; }

        // ======================== 内部状态 ========================

        private readonly object _syncRoot = new object();
        private bool _isRunning;
        private CancellationTokenSource _receiveCts;
        private IPEndPoint _lastRemoteEndPoint;

        // UDP 数据报一般较小，64 KB 是 Socket 支持的最大 IPv4 UDP 数据报缓冲区上限。
        private const int RECEIVE_BUFFER_SIZE = 64 * 1024;
        private const int RECEIVE_TIMEOUT_MS = 500;

        // 当前项目 TCP_Client 使用系统默认编码，UDP 为保持行为一致也使用系统默认编码。
        private static readonly Encoding MessageEncoding = Encoding.Default;

        // ======================== 公共方法 ========================

        /// <summary>
        /// 创建并绑定 UDP Socket，启动后台接收循环。
        /// UDP 不需要向远端建立握手连接，因此这里不会验证机器人是否在线。
        /// </summary>
        public void Connect()
        {
            lock (_syncRoot)
            {
                if (_isRunning)
                {
                    return;
                }
            }

            Socket socket = null;
            CancellationTokenSource receiveCts = null;

            try
            {
                if (ipAndPoint == null)
                {
                    throw new InvalidOperationException("UDP 远端端点未设置。");
                }

                if (LocalPort < 0 || LocalPort > 65535)
                {
                    throw new ArgumentOutOfRangeException(nameof(LocalPort), "UDP 本地端口必须在 0 到 65535 之间。");
                }

                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.ReceiveTimeout = RECEIVE_TIMEOUT_MS;
                socket.Bind(new IPEndPoint(IPAddress.Any, LocalPort));

                receiveCts = new CancellationTokenSource();

                lock (_syncRoot)
                {
                    // 防止多个线程同时调用 Connect() 时重复接管 Socket。
                    if (_isRunning)
                    {
                        socket.Close();
                        receiveCts.Dispose();
                        return;
                    }

                    mySocket = socket;
                    LocalEndPoint = (IPEndPoint)socket.LocalEndPoint;
                    _receiveCts = receiveCts;
                    _lastRemoteEndPoint = null;
                    _isRunning = true;
                }

                Task.Run(() => ReceiveLoop(socket, receiveCts.Token), receiveCts.Token);
                NotifyConnectionStateChanged(true);
            }
            catch (Exception ex)
            {
                try { receiveCts?.Dispose(); } catch { }
                try { socket?.Close(); } catch { }

                Console.WriteLine("[UDP] 创建或绑定 Socket 失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 停止接收循环并关闭 UDP Socket。
        /// </summary>
        public void Disconnect()
        {
            Socket socket;
            CancellationTokenSource receiveCts;
            bool shouldNotify;

            lock (_syncRoot)
            {
                shouldNotify = _isRunning || _mySocket != null;
                _isRunning = false;

                socket = _mySocket;
                receiveCts = _receiveCts;

                mySocket = null;
                _receiveCts = null;
                _lastRemoteEndPoint = null;
                LocalEndPoint = null;
            }

            try { receiveCts?.Cancel(); } catch { }
            try { receiveCts?.Dispose(); } catch { }
            try { socket?.Close(); } catch { }

            if (shouldNotify)
            {
                NotifyConnectionStateChanged(false);
            }
        }

        /// <summary>
        /// 向最近一次收到数据的端点发送字符串；如果还没有收到过数据，则发送到 ipAndPoint。
        /// UDP 每次 Send() 都对应一个完整的数据报，不会产生 TCP 的粘包问题。
        /// </summary>
        public bool Send(string msg)
        {
            if (string.IsNullOrEmpty(msg))
            {
                return false;
            }

            Socket socket;
            IPEndPoint targetEndPoint;

            lock (_syncRoot)
            {
                socket = _mySocket;
                targetEndPoint = _lastRemoteEndPoint ?? ipAndPoint;

                if (!_isRunning || socket == null || targetEndPoint == null)
                {
                    return false;
                }
            }

            try
            {
                byte[] data = MessageEncoding.GetBytes(msg);
                int sentLength = socket.SendTo(data, targetEndPoint);
                return sentLength == data.Length;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                Console.WriteLine("[UDP] 发送失败: " + ex.Message);
                HandleSocketFailure(socket);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UDP] 发送异常: " + ex.Message);
                return false;
            }
        }

        // ======================== 接收逻辑 ========================

        /// <summary>
        /// 后台接收 UDP 数据报。
        /// 每次 ReceiveFrom() 得到一个完整数据报，因此不需要 TCP 的拆包和粘包处理。
        /// </summary>
        private void ReceiveLoop(Socket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[RECEIVE_BUFFER_SIZE];

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsCurrentSocketRunning(socket))
                {
                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    try
                    {
                        int length = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                        if (length <= 0)
                        {
                            continue;
                        }

                        IPEndPoint sourceEndPoint = remoteEndPoint as IPEndPoint;
                        if (sourceEndPoint == null)
                        {
                            continue;
                        }

                        // 记录最近一次发送端点。回复报文优先发送到这个端点，避免源端口与配置端口不同。
                        lock (_syncRoot)
                        {
                            if (ReferenceEquals(_mySocket, socket))
                            {
                                _lastRemoteEndPoint = sourceEndPoint;
                            }
                        }

                        string msg = MessageEncoding.GetString(buffer, 0, length);

                        try
                        {
                            OnDataArrivedEvent?.Invoke(new DataExchange
                            {
                                ip = sourceEndPoint,
                                tmpSkt = socket,
                                data = msg
                            });

                            OnDataReceived?.Invoke(msg);
                        }
                        catch (Exception callbackException)
                        {
                            // 回调内部异常不应终止 UDP 接收循环。
                            Console.WriteLine("[UDP] 数据回调异常: " + callbackException.Message);
                        }
                    }
                    catch (SocketException ex)
                    {
                        // ReceiveTimeout 用于让 Disconnect() 能够及时停止阻塞接收。
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            continue;
                        }

                        if (cancellationToken.IsCancellationRequested || !IsCurrentSocketRunning(socket))
                        {
                            break;
                        }

                        Console.WriteLine("[UDP] 接收异常: " + ex.Message);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[UDP] 接收循环异常: " + ex.Message);
                }
            }
            finally
            {
                // 只有当前 Socket 仍由该接收线程管理时，才处理异常断开。
                HandleSocketFailure(socket);
            }
        }

        /// <summary>
        /// 判断指定 Socket 是否仍是当前运行中的 Socket。
        /// 用于避免关闭后立即重连时，旧接收线程误关闭新 Socket。
        /// </summary>
        private bool IsCurrentSocketRunning(Socket socket)
        {
            lock (_syncRoot)
            {
                return _isRunning && ReferenceEquals(_mySocket, socket);
            }
        }

        /// <summary>
        /// 处理 Socket 异常关闭。
        /// </summary>
        private void HandleSocketFailure(Socket socket)
        {
            CancellationTokenSource receiveCts = null;
            bool shouldNotify = false;

            lock (_syncRoot)
            {
                if (!ReferenceEquals(_mySocket, socket))
                {
                    return;
                }

                shouldNotify = _isRunning;
                _isRunning = false;
                receiveCts = _receiveCts;
                _receiveCts = null;
                mySocket = null;
                _lastRemoteEndPoint = null;
                LocalEndPoint = null;
            }

            try { receiveCts?.Cancel(); } catch { }
            try { receiveCts?.Dispose(); } catch { }
            try { socket?.Close(); } catch { }

            if (shouldNotify)
            {
                NotifyConnectionStateChanged(false);
            }
        }

        /// <summary>
        /// 通知通信状态变化。
        /// 外部回调即使抛出异常，也不能影响 UDP 通信线程本身。
        /// </summary>
        private void NotifyConnectionStateChanged(bool connected)
        {
            try
            {
                OnConnectionStateChanged?.Invoke(connected);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UDP] 状态回调异常: " + ex.Message);
            }
        }
    }
}
