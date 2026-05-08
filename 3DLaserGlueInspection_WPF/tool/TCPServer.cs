using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using _3DLaserGlueInspection;
using System.IO;
using System.Xml.Serialization;

namespace TCPIP
{
    public class DataExchange : EventArgs
    {
        private EndPoint _IP;
        public EndPoint ip
        {
            get { return _IP; }
            set { _IP = value; }
        }

        private Socket _TmpSkt;
        public Socket tmpSkt
        {
            get { return _TmpSkt; }
            set { _TmpSkt = value; }
        }

        private string _Data;
        public string data
        {
            get { return _Data; }
            set { _Data = value; }
        }
    }

    public class TCP_Server
    {
        public TCP_Server()
        {
            //this.ipAndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);
            this.mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        public DataExchange tmp = new DataExchange();
        public delegate void DelDataArrived(DataExchange tmp);
        public event DelDataArrived OnDataArrivedEvent;  //收到数据事件
        public event DelDataArrived OnDiscoveredDeviceEvent;  //发现设备事件

        private readonly object _lock = new object(); // 用于线程安全
        private bool _isListening = false;

        public string ErrMsg => _errMsg;
        string _errMsg;

        /// <summary>
        /// IP地址和Port口
        /// </summary>
        private IPEndPoint _IpAndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        public IPEndPoint ipAndPoint
        {
            get { return _IpAndPoint; }
            set { _IpAndPoint = value; }
        }
        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "TCPParam.xml";
                if (File.Exists(paramPath))
                {

                    RobotParam param = new RobotParam();
                    //param.IpAddress = _IpAndPoint.Address.ToString();
                    //param.Port = _IpAndPoint.Port;

                    XmlSerializer xml = new XmlSerializer(param.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        RobotParam _ = (RobotParam)xml.Deserialize(stream);
                        if (_ != null)
                        {
                            param = _;

                            _IpAndPoint.Address = IPAddress.Parse(param.IpAddress);
                            _IpAndPoint.Port = param.Port;

                        }
                        else
                        {
                            _errMsg = paramPath + "文件格式异常";
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + "文件不存在";
                    result = false;
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }

            return result;
        }

        public bool Save()
        {
            bool result = true;
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                RobotParam param = new RobotParam();
                param.IpAddress = _IpAndPoint.Address.ToString();
                param.Port = _IpAndPoint.Port;

                string OpcParamPath = basePath + "TCPParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }
            return result;
        }

        /// <summary>
        /// 负责监听的Socket对象
        /// </summary>
        private Socket _MySocket;
        public Socket mySocket
        {
            get { return _MySocket; }
            set { _MySocket = value; }
        }

        /// <summary>
        /// 负责收发数据的Socket对象
        /// </summary>
        private Socket _ConnectSocket;
        public Socket ConnectSocket
        {
            get { return _ConnectSocket; }
            set { _ConnectSocket = value; }
        }
        /// <summary>
        /// 服务器开始监听
        /// </summary>
        // 私有方法，用于创建一个持续的接受循环
        private void StartAcceptLoop()
        {
            try
            {
                // 开始一个异步接受操作
                mySocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
            }
            catch (ObjectDisposedException)
            {
                // 当 mySocket 被关闭时，BeginAccept 会抛出此异常，这是正常的停止方式
                Console.WriteLine("监听已停止。");
            }
            catch (Exception ex)
            {
                // 处理其他可能的异常
                Console.WriteLine($"开始监听时发生错误: {ex.Message}");
                // 根据需求，可以选择在这里停止或尝试重新开始监听
            }
        }

        // 接受操作的回调函数
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // 1. 完成接受操作，并获取与客户端通信的新Socket
                Socket clientSocket = mySocket.EndAccept(ar);
                Console.WriteLine($"新客户端已连接: {clientSocket.RemoteEndPoint}");

                //// 2. 处理这个新连接的客户端
                ////    注意：这里我们不再将客户端Socket存到类级别的变量(this.ConnectSocket)中，
                ////    而是直接传递给处理任务，这样可以支持多个客户端同时连接。
                //if (OnDiscoveredDeviceEvent != null)
                //{
                //    // 假设 tmp 是一个您定义的对象
                //    var tmp = new YourDeviceType();
                //    tmp.ip = clientSocket.RemoteEndPoint;
                //    OnDiscoveredDeviceEvent(tmp);
                //}

                this.ConnectSocket = clientSocket;
          
                // 启动一个新任务来处理该客户端的数据接收，这样不会阻塞接受循环
                _ = Task.Run(() => ReceiveDataFromClient(clientSocket));

                // 3. 【关键步骤】处理完当前客户端后，立即再次调用 StartAcceptLoop()
                //    以便服务器可以继续接受下一个客户端连接。
                StartAcceptLoop();
            }
            catch (ObjectDisposedException)
            {
                // 监听Socket被关闭，停止接受循环
                Console.WriteLine("监听Socket已关闭，停止接受新连接。");
            }
            catch (Exception ex)
            {
                // 在接受客户端时发生错误
                Console.WriteLine($"接受客户端时发生错误: {ex.Message}");
                // 即使出错，也尝试继续监听下一个客户端
                StartAcceptLoop();
            }
        }


        //委托
        public event Action<string> reserveInfoSignal;

        //开始监听
        public void StartListen()
        {
            // 确保只有一个监听线程在运行
            lock (_lock)
            {
                if (_isListening) return;

                _isListening = true;
                mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                mySocket.Bind(ipAndPoint);
                mySocket.Listen(10);

                // 启动第一个接受操作
                StartAcceptLoop();
            }
        }

        // 您可能还需要一个停止监听的方法
        public void StopListen()
        {
            lock (_lock)
            {
                if (_isListening)
                {
                    _isListening = false;
                    if (mySocket != null)
                    {
                        mySocket.Close(); // 这会导致 BeginAccept 抛出 ObjectDisposedException，从而停止循环
                        mySocket = null;
                    }
                }
            }
        }
        public Task ReceiveDataFromClient(Socket rcvSocket)
        {
            return Task.Run(() =>
            {
                using (rcvSocket)
                {
                    try
                    {
                        while (true)
                        {
                            byte[] byt = new byte[1024];
                            int len = rcvSocket.Receive(byt, 0, byt.Length, SocketFlags.None);
                            if (len > 0)
                            {
                                tmp.data = Encoding.Default.GetString(byt, 0, len);
                                reserveInfoSignal(tmp.data);
                            }
                            Thread.Sleep(100);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                   
                }
            });
        }
        /// <summary>
        /// 发送数据给客户端
        /// </summary>
        /// <param name="Msg"></param>
        public void Send(string Msg)
        {
            try
            {
                byte[] bytStr = Encoding.Default.GetBytes(Msg);

                if (this.ConnectSocket!=null && this.ConnectSocket.Connected)
                {
                    this.ConnectSocket.BeginSend(bytStr, 0, bytStr.Length, SocketFlags.None, new AsyncCallback((iar) =>
                    {
                        Socket Skt = (Socket)iar.AsyncState;
                        int length = this.ConnectSocket.EndSend(iar);
                    }), this.ConnectSocket);
                }

             
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message.ToString());
            }
        }

        public bool Isconnected()
        {
            if (this.ConnectSocket != null && this.ConnectSocket.Connected)
            {
                if (VerifyConnection())
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }


        }

        private bool VerifyConnection()
        {
            try
            {
                // 方法1：设置短时Poll
                bool part1 = ConnectSocket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (ConnectSocket.Available == 0);
                if (part1 && part2)
                    return false;

                //// 方法2：检查Socket选项
                //byte[] outValue = new byte[1];
                //ConnectSocket.GetSocketOption(SocketOptionLevel.Socket,
                //                             SocketOptionName.Error,
                //                             outValue);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

   
}
