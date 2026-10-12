using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace _3DLaserGlueInspection
{
    [Serializable]
    public class PISerialPortParam : PlcParam
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public Parity Parity { get; set; } = Parity.Odd;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Handshake Handshake { get; set; } = Handshake.None;
        public bool DtrEnable { get; set; }
        public bool RtsEnable { get; set; }
        public int ReadTimeoutMilliseconds { get; set; } = 200;
        public int WriteTimeoutMilliseconds { get; set; } = 1000;
        // SUM 固定包含 STX、通讯连号、命令及数据，ETX 默认不计入。
        public bool SumIncludesETX { get; set; }

        internal PISerialPortParam Copy()
        {
            return (PISerialPortParam)MemberwiseClone();
        }
    }

    public sealed class PISerialPortDataEventArgs : EventArgs
    {
        private readonly byte[] data;
        public DateTime Timestamp { get; }
        public byte[] Data => (byte[])data.Clone();

        internal PISerialPortDataEventArgs(byte[] buffer, int count)
        {
            Timestamp = DateTime.Now;
            data = new byte[count];
            Buffer.BlockCopy(buffer, 0, data, 0, count);
        }
    }

    /// <summary>
    /// PI 工具卡 RS232 通讯。接收线程更新缓存，Read 不主动发送报文。
    /// B 接收并保存后回复 ACK，Write(DO.Result, ...) 使用该 B 的通讯连号发送 E 结果报文。
    /// </summary>
    public class PISerialPort : ISignal, IHsl, IDisposable
    {
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte ACK = 0x06;
        private const byte NAK = 0x15;
        private readonly object lifecycleLock = new object();
        private readonly object sendLock = new object();
        private readonly object stateLock = new object();
        private PISerialPortParam param = new PISerialPortParam();
        private PISerialPortParam activeParam;
        private SerialPort serialPort;
        private Thread receiveThread;
        private volatile bool receiving;
        private volatile bool isOpen;
        private string errMsg = string.Empty;
        private byte[] lastCommand;
        private string serialNumber = string.Empty;
        private string vin = string.Empty;
        private string toolNumber = string.Empty;
        private string programNumber = string.Empty;
        private bool isStart;
        private bool isEnd;
        private bool resultSent;
        private string result = string.Empty;

        public string ErrMsg => errMsg;
        public bool IsOpen => isOpen;
        // 在实际收发线程触发，界面只入队显示，不阻塞串口线程。
        public event EventHandler<PISerialPortDataEventArgs> DataReceived;
        public event EventHandler<PISerialPortDataEventArgs> DataSent;
        public PlcParam Param
        {
            get => param;
            set
            {
                var serialParam = value as PISerialPortParam;
                if (serialParam == null)
                    throw new ArgumentException("PI 通讯参数必须为 PISerialPortParam。", nameof(value));
                param = serialParam;
            }
        }
        public PISerialPortParam SerialParam
        {
            get => param;
            set => Param = value;
        }
        // 保留 IHsl 配置接口；PI 使用固定报文字段，不使用 PLC 寄存器地址。
        public Dictionary<string, IoAddress> IoDict { get; set; } = new Dictionary<string, IoAddress>();
        public string SerialNumber { get { lock (stateLock) return serialNumber; } }
        public string VIN { get { lock (stateLock) return vin; } }
        public string ToolNumber { get { lock (stateLock) return toolNumber; } }
        public string ProgramNumber { get { lock (stateLock) return programNumber; } }

        public bool Open()
        {
            lock (lifecycleLock)
            {
                if (isOpen)
                    return true;
                if (receiveThread != null && receiveThread.IsAlive)
                    return Fail("PI 上一次接收线程尚未退出。请先关闭连接。");
                SerialPort port = null;
                try
                {
                    var settings = param.Copy();
                    if (settings.ReadTimeoutMilliseconds <= 0 || settings.ReadTimeoutMilliseconds > 1000
                        || settings.WriteTimeoutMilliseconds <= 0 || settings.WriteTimeoutMilliseconds > 5000)
                        return Fail("PI 读写超时超出范围（读 1~1000ms，写 1~5000ms）。");
                    port = new SerialPort(settings.PortName, settings.BaudRate, settings.Parity,
                        settings.DataBits, settings.StopBits)
                    {
                        Encoding = Encoding.ASCII,
                        Handshake = settings.Handshake,
                        DtrEnable = settings.DtrEnable,
                        ReadTimeout = settings.ReadTimeoutMilliseconds,
                        WriteTimeout = settings.WriteTimeoutMilliseconds
                    };
                    if (settings.Handshake != Handshake.RequestToSend
                        && settings.Handshake != Handshake.RequestToSendXOnXOff)
                        port.RtsEnable = settings.RtsEnable;
                    port.Open();
                    serialPort = port;
                    activeParam = settings;
                    lock (stateLock)
                    {
                        lastCommand = null;
                        serialNumber = vin = toolNumber = programNumber = string.Empty;
                        ResetSignalCore();
                    }
                    receiving = true;
                    isOpen = true;
                    errMsg = string.Empty;
                    receiveThread = new Thread(() => ReceiveLoop(port))
                    {
                        IsBackground = true,
                        Name = "PI RS232 receive"
                    };
                    receiveThread.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    receiving = isOpen = false;
                    port?.Dispose();
                    serialPort = null;
                    return Fail(ex.Message);
                }
            }
        }

        public bool Close()
        {
            lock (lifecycleLock)
            {
                receiving = isOpen = false;
                // Read 有超时，先等待线程退出，避免关闭串口时与正在读写的线程互锁。
                if (receiveThread != null && receiveThread != Thread.CurrentThread
                    && !receiveThread.Join(7000))
                    return Fail("PI 接收线程未能在超时时间内退出。");
                receiveThread = null;
                lock (sendLock)
                {
                    try { serialPort?.Dispose(); }
                    catch (Exception ex) { return Fail("PI 串口关闭失败：" + ex.Message); }
                    serialPort = null;
                }
                lock (stateLock)
                {
                    lastCommand = null;
                    ResetSignalCore();
                }
                return true;
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void ReceiveLoop(SerialPort port)
        {
            var pending = new List<byte>();
            var bytes = new byte[256];
            DateTime lastReceived = DateTime.UtcNow;
            try
            {
                while (receiving)
                {
                    try
                    {
                        int count = port.Read(bytes, 0, bytes.Length);
                        NotifyData(DataReceived, bytes, count);
                        if ((DateTime.UtcNow - lastReceived).TotalSeconds >= 2)
                            pending.Clear();
                        lastReceived = DateTime.UtcNow;
                        ProcessReceivedBytes(pending, bytes, count);
                    }
                    catch (TimeoutException)
                    {
                        // 丢弃超过两秒的残帧，防止断帧阻塞后续报文。
                        if (pending.Count > 0 && (DateTime.UtcNow - lastReceived).TotalSeconds >= 2)
                        {
                            pending.Clear();
                            Fail("PI 不完整报文接收超时。");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (receiving)
                    Fail("PI 串口接收失败：" + ex.Message);
            }
            finally
            {
                receiving = isOpen = false;
                lock (sendLock)
                {
                    try { port.Dispose(); }
                    catch (Exception ex) { Fail("PI 串口关闭失败：" + ex.Message); }
                }
                lock (stateLock)
                {
                    lastCommand = null;
                    ResetSignalCore();
                }
            }
        }

        private void ProcessReceivedBytes(List<byte> pending, byte[] bytes, int count)
        {
            for (int i = 0; i < count; i++)
                pending.Add(bytes[i]);
            while (pending.Count > 0)
            {
                int start = pending.IndexOf(STX);
                if (start < 0)
                {
                    pending.Clear();
                    return;
                }
                if (start > 0)
                    pending.RemoveRange(0, start);
                if (pending.Count < 5)
                    return;
                // 通讯连号是两个原始字节，可能等于 STX/ETX，不能按结束符直接 Split。
                bool reply = pending[4] == ACK || pending[4] == NAK;
                int length;
                switch ((char)pending[3])
                {
                    case 'A':
                    case 'D': length = reply ? 9 : 7; break;
                    case 'B': length = reply ? 11 : 52; break;
                    case 'E': length = reply ? 9 : 44; break;
                    default:
                        Fail("PI 收到不支持的命令：" + pending[3].ToString("X2"));
                        pending.RemoveAt(0);
                        continue;
                }
                if (pending.Count < length)
                    return;
                if (pending[length - 1] != ETX)
                {
                    Fail("PI 报文长度或 ETX 错误。");
                    pending.RemoveAt(0);
                    continue;
                }
                byte[] frame = pending.GetRange(0, length).ToArray();
                pending.RemoveRange(0, length);
                ProcessFrame(frame, reply);
            }
        }

        private void ProcessFrame(byte[] frame, bool reply)
        {
            byte[] sum = GetChecksum(frame, activeParam);
            if (frame[frame.Length - 3] != sum[0] || frame[frame.Length - 2] != sum[1])
            {
                Fail("PI 报文 SUM 校验失败。");
                return;
            }
            // 现场约定：D/E 收到后都置位并回复，包括 E 结果应答帧。
            if (frame[3] == (byte)'D' || frame[3] == (byte)'E')
            {
                lock (stateLock)
                    isEnd = true;
                if (reply && frame[4] == NAK)
                    Fail("PI 返回 " + (char)frame[3] + " NAK，详细码：" + (char)frame[5]);
                SendReply(frame);
                return;
            }
            if (reply)
                return;
            switch ((char)frame[3])
            {
                case 'A':
                    SendReply(frame);
                    break;
                case 'B':
                    for (int i = 4; i < frame.Length - 3; i++)
                    {
                        if (frame[i] < 0x20 || frame[i] > 0x7E)
                        {
                            Fail("PI B 报文包含非 ASCII 数据。");
                            return;
                        }
                    }
                    string number = Encoding.ASCII.GetString(frame, 4, 5);
                    if (!number.All(c => c >= '0' && c <= '9'))
                    {
                        Fail("PI B 报文连番格式错误。");
                        return;
                    }
                    lock (stateLock)
                    {
                        lastCommand = frame;
                        serialNumber = number;
                        vin = Encoding.ASCII.GetString(frame, 9, 20).TrimEnd(' ');
                        toolNumber = Encoding.ASCII.GetString(frame, 29, 4);
                        programNumber = Encoding.ASCII.GetString(frame, 33, 16);
                        ResetSignalCore();
                        isStart = true;
                    }
                    SendReply(frame);
                    break;
            }
        }

        private static byte[] GetChecksum(byte[] frame, PISerialPortParam settings)
        {
            int sum = STX;
            for (int i = 1; i < frame.Length - 3; i++)
                sum += frame[i];
            if (settings.SumIncludesETX)
                sum += ETX;
            return Encoding.ASCII.GetBytes((sum & 0xFF).ToString("X2", CultureInfo.InvariantCulture));
        }

        private bool SendReply(byte[] command)
        {
            bool isB = command[3] == (byte)'B';
            var frame = new byte[isB ? 11 : 9];
            frame[0] = STX;
            frame[1] = command[1];
            frame[2] = command[2];
            frame[3] = command[3];
            frame[4] = ACK;
            frame[5] = (byte)'0';
            if (isB)
            {
                // B 指令接收回复：厂商名、厂商机种代码按协议固定为 F。
                frame[6] = (byte)'F';
                frame[7] = (byte)'F';
            }
            return SendFrame(frame);
        }

        private bool SendResult(byte[] command, char judgement)
        {
            // E：程序号 1 位、综合判定 1 位、分项结果 5 位、品质数据 5 * 6 位。
            var frame = new byte[44];
            frame[0] = STX;
            frame[1] = command[1];
            frame[2] = command[2];
            frame[3] = (byte)'E';
            for (int i = 4; i < frame.Length - 3; i++)
                frame[i] = (byte)'0';
            frame[4] = (byte)'1';
            frame[5] = (byte)judgement;
            return SendFrame(frame);
        }

        private bool SendFrame(byte[] frame)
        {
            frame[frame.Length - 1] = ETX;
            byte[] sum = GetChecksum(frame, activeParam);
            frame[frame.Length - 3] = sum[0];
            frame[frame.Length - 2] = sum[1];
            lock (sendLock)
            {
                if (!isOpen || serialPort == null)
                    return Fail("PI 串口未打开。");
                try
                {
                    serialPort.Write(frame, 0, frame.Length);
                }
                catch (Exception ex)
                {
                    return Fail("PI 串口发送失败：" + ex.Message);
                }
            }
            NotifyData(DataSent, frame, frame.Length);
            return true;
        }

        private void NotifyData(EventHandler<PISerialPortDataEventArgs> handlers, byte[] data, int count)
        {
            if (handlers == null || count <= 0)
                return;
            var args = new PISerialPortDataEventArgs(data, count);
            foreach (EventHandler<PISerialPortDataEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, args); }
                catch (Exception ex)
                {
                    // 显示端异常不能中断协议处理或改变发送结果。
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }

        public void ResetSignal()
        {
            lock (stateLock)
                ResetSignalCore();
        }

        private void ResetSignalCore()
        {
            isStart = isEnd = resultSent = false;
            result = string.Empty;
        }

        public bool Read(DI eDI, out string value)
        {
            lock (stateLock)
            {
                value = string.Empty;
                switch (eDI)
                {
                    case DI.InVIN: value = vin; return true;
                    case DI.CarNumber: value = serialNumber; return true;
                    case DI.Start: value = isStart.ToString(); return true;
                    case DI.END: value = isEnd.ToString(); return true;
                    default: return Unsupported(eDI);
                }
            }
        }

        public bool Read(DI eDI, out bool value)
        {
            lock (stateLock)
            {
                value = false;
                switch (eDI)
                {
                    case DI.Start: value = isStart; return true;
                    case DI.END: value = isEnd; return true;
                    default: return Unsupported(eDI);
                }
            }
        }

        public bool Read(DI eDI, out ushort value)
        {
            lock (stateLock)
            {
                value = 0;
                if (eDI != DI.CarNumber)
                    return Unsupported(eDI);
                if (lastCommand == null)
                    return Fail("PI 尚未收到 B 指令。");
                if (ushort.TryParse(serialNumber, out value))
                    return true;
                return Fail("PI 连番号超出 ushort 范围，请按 string 读取。");
            }
        }

        public bool Read(DO eDO, out string value)
        {
            lock (stateLock)
            {
                value = string.Empty;
                switch (eDO)
                {
                    case DO.Result: value = result; return true;
                    case DO.IsRead: value = resultSent.ToString(); return true;
                    default: return Unsupported(eDO);
                }
            }
        }

        public bool Read(DO eDO, out bool value)
        {
            lock (stateLock)
            {
                value = false;
                switch (eDO)
                {
                    case DO.Result:
                        if (result != "0" && result != "1")
                            return Fail("PI 当前综合判定不能转换为 bool，请按 string 或 ushort 读取。");
                        value = result == "1";
                        return true;
                    case DO.IsRead: value = resultSent; return true;
                    default: return Unsupported(eDO);
                }
            }
        }

        public bool Read(DO eDO, out ushort value)
        {
            lock (stateLock)
            {
                value = 0;
                switch (eDO)
                {
                    case DO.Result:
                        if (ushort.TryParse(result, out value))
                            return true;
                        return Fail("PI 尚未发送结果。");
                    case DO.IsRead: value = resultSent ? (ushort)1 : (ushort)0; return true;
                    default: return Unsupported(eDO);
                }
            }
        }

        public bool Write(DO eDO, object value)
        {
            if (eDO != DO.Result)
                return Unsupported(eDO);
            string judgement = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (judgement == null || judgement.Length != 1 || judgement[0] < '0' || judgement[0] > '9')
                return Fail("PI 综合判定必须是协议规定的一位数字，直接写入，不做 OK/NG 映射。");
            lock (lifecycleLock)
            {
                byte[] command;
                lock (stateLock)
                    command = lastCommand;
                if (command == null)
                    return Fail("PI 尚未收到 B 指令，无法发送结果。");
                if (!SendResult(command, judgement[0]))
                    return false;
                lock (stateLock)
                {
                    if (ReferenceEquals(command, lastCommand))
                    {
                        result = judgement;
                        resultSent = true;
                    }
                }
                return true;
            }
        }

        public bool Load()
        {
            lock (lifecycleLock)
            {
                if (isOpen)
                    return Fail("请先关闭 PI 串口，再读取参数。");
                try
                {
                    string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    PISerialPortParam loaded;
                    List<IoAddress> ios;
                    using (var stream = File.OpenRead(Path.Combine(basePath, "PISerialPortParam.xml")))
                        loaded = (PISerialPortParam)new XmlSerializer(typeof(PISerialPortParam)).Deserialize(stream);
                    using (var stream = File.OpenRead(Path.Combine(basePath, "PIIoParam.xml")))
                        ios = (List<IoAddress>)new XmlSerializer(typeof(List<IoAddress>)).Deserialize(stream);
                    if (loaded == null || ios == null)
                        return Fail("PI 参数文件格式错误。");
                    var loadedIo = ios.ToDictionary(io => io.IoName);
                    param = loaded;
                    IoDict = loadedIo;
                    errMsg = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    return Fail(ex.Message);
                }
            }
        }

        public bool Save()
        {
            lock (lifecycleLock)
            {
                try
                {
                    string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    Directory.CreateDirectory(basePath);
                    using (var stream = File.Create(Path.Combine(basePath, "PISerialPortParam.xml")))
                        new XmlSerializer(typeof(PISerialPortParam)).Serialize(stream, param);
                    using (var stream = File.Create(Path.Combine(basePath, "PIIoParam.xml")))
                        new XmlSerializer(typeof(List<IoAddress>)).Serialize(stream, IoDict.Values.ToList());
                    errMsg = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    return Fail(ex.Message);
                }
            }
        }

        public void ShowForm()
        {
            if (!IsOpen)
                Load();
            var window = new subForm.WindowSerialPort(this);
            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible)
                window.Owner = owner;
            window.ShowDialog();
        }

        private bool Unsupported(object signal)
        {
            return Fail("PI 不支持该信号或读取类型：" + signal);
        }

        private bool Fail(string message)
        {
            errMsg = message;
            return false;
        }
    }
}
