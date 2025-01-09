using HalconDotNet;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Siemens.S7PlusHelper;
using HslCommunication.Robot.YASKAWA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace _3DLaserGlueInspection
{
    public interface IRobot
    {
        RobotParam Param { get; set; }
        Dictionary<string, IoAddress> IoDict { get; set; }
        bool IsOpen { get; }

        /// <summary>
        /// 获取最后一次错误信息
        /// </summary>
        /// <returns></returns>
        string ErrMsg { get; }
        /// <summary>
        /// 加载参数
        /// </summary>
        /// <returns></returns>
        bool Load();
        /// <summary>
        /// 保存参数
        /// </summary>
        /// <returns></returns>
        bool Save();
        /// <summary>
        /// 打开（连接）
        /// </summary>
        /// <returns></returns>
        bool Open();
        /// <summary>
        /// 关闭（断开）
        /// </summary>
        /// <returns></returns>
        bool Close();
        /// <summary>
        /// 获取坐标
        /// </summary>
        /// <param name="hPose"></param>
        /// <returns></returns>
        bool ReadPose(out HPose hPose);

        bool Read(DI eDI, out string value);
        bool Read(DO eDO, out string value);
        bool Read(DI eDI, out bool value);
        bool Read(DO eDO, out bool value);
        bool Read(DI eDI, out ushort value);
        bool Read(DO eDO, out ushort value);
        bool Write(DO eDO, object value);
    }

    [Serializable]
    public class RobotParam
    {
        public string IpAddress = "127.0.0.1";
        public int Port = 2000;
    }

    public class YRCRobot /*: IRobot*/
    {
        public string ErrMsg => _errMsg;
        string _errMsg;

        string ip = string.Empty;
        int port = 10040;
        YRCHighEthernet yrc = new YRCHighEthernet();
        public YRCRobot() { }

        public bool Read坐标(out string[] value)
        {
            OperateResult<byte[]> operateResult = yrc.ReadCommand(117, 101, 0, 1, null);
            if (operateResult.IsSuccess)
            {
                string[] array = new string[operateResult.Content.Length / 4];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = byteTransform.TransInt32(operateResult.Content, i * 4).ToString();
                }
                value = array;
                return true;
            }
            else
            {
                value = null;
                _errMsg = operateResult.Message;
                return false;
            }
        }
        public bool Read坐标(out HPose hPose)
        {
            OperateResult<string[]> read = yrc.ReadPose();//关节坐标
            if (read.IsSuccess)
            {
                double x = double.Parse(read.Content[0]) / 1000;
                double y = double.Parse(read.Content[1]) / 1000;
                double z = double.Parse(read.Content[2]) / 1000;
                double rx = double.Parse(read.Content[3]) / 10000;
                double ry = double.Parse(read.Content[4]) / 10000;
                double rz = double.Parse(read.Content[5]) / 10000;
                hPose = new HPose(x, y, z, rx, ry, rz, "Rp+T", "abg", "point");
            }
            else
            {
                hPose = null;
            }
            _errMsg = read.Message;
            return read.IsSuccess;
        }
        private IByteTransform byteTransform = new RegularByteTransform();
        public bool ReadPose(out HPose hPose)
        {
            OperateResult<byte[]> operateResult = yrc.ReadCommand(117, 101, 0, 1, null);
            if (operateResult.IsSuccess && operateResult.Content.Length >= 44)
            {
                int[] array = new int[6];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = byteTransform.TransInt32(operateResult.Content, 20 + i * 4);
                }
                hPose = new HPose(array[0] / 1000000, array[1] / 1000000, array[2] / 1000000, array[3] / 10000, array[4] / 10000, array[5] / 10000, "Rp+T", "abg", "point");
                return true;
            }
            else
            {
                hPose = null;
                _errMsg = operateResult.Message;
                return false;
            }
        }

        public bool Load()
        {
            ip = "192.168.255.1";
            port = 10040;
            return true;
        }

        public bool Save()
        {
            return true;
        }

        public bool Open()
        {
            yrc.IpAddress = ip;
            yrc.Port = port;
            return true;
        }

        public bool Close()
        {
            return true;
        }
    }

    public class JAKARobot : IRobot, ISignal
    {
        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen = false;

        HslCommunication.ModBus.ModbusTcpNet modbus = new HslCommunication.ModBus.ModbusTcpNet();

        RobotParam param = new RobotParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        public RobotParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }

        public JAKARobot() { }

        public void ShowForm()
        {
            //new RobotForm(this).ShowDialog();
        }

        public bool ReadPose(out HPose hPose)
        {
            var operateResult = modbus.ReadFloat("x=4;406", 6);
            if (operateResult.IsSuccess)
            {
                var array = operateResult.Content;
                hPose = new HPose(array[0] / 1000, array[1] / 1000, array[2] / 1000, array[3], array[4], array[5], "Rp+T", "abg", "point");
                return true;
            }
            else
            {
                hPose = null;
                _errMsg = operateResult.Message;
                return false;
            }
        }
        /// <summary>
        /// 读取输出信号DO1~DO128
        /// </summary>
        /// <param name="index">1~128</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadDO(int index, out bool value)
        {
            int address = 8 + index - 1;
            var operateResult = modbus.ReadBool($"x=2;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 读取输入信号DI1~DI128
        /// </summary>
        /// <param name="index">1~128</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadDI(int index, out bool value)
        {
            int address = 40 + index - 1;
            var operateResult = modbus.ReadBool($"x=1;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 写入输入信号DI1~DI128
        /// </summary>
        /// <param name="index">1~128</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool WriteDI(int index, bool value)
        {
            int address = 40 + index - 1;
            var operateResult = modbus.Write($"x=1;{address}", value);
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 读取输出信号AO1~AO32
        /// </summary>
        /// <param name="index">1~32</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadAO(int index, out ushort value)
        {
            int address = 96 + index - 1;
            var operateResult = modbus.ReadUInt16($"x=4;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 读取输出信号AI1~AI32
        /// </summary>
        /// <param name="index">1~32</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadAI(int index, out ushort value)
        {
            int address = 100 + index - 1;
            var operateResult = modbus.ReadUInt16($"x=3;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 写入输入信号AI1~AI32
        /// </summary>
        /// <param name="index">1~32</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool WriteAI(int index, ushort value)
        {
            int address = 100 + index - 1;
            var operateResult = modbus.Write($"x=3;{address}", value);
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 读取输出信号AO33~AO64
        /// </summary>
        /// <param name="index">33~64</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadAO(int index, out float value)
        {
            int address = 128 + (index - 33) * 2;
            var operateResult = modbus.ReadUInt16($"x=4;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 读取输出信号AI33~AI64
        /// </summary>
        /// <param name="index">33~64</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadAI(int index, out float value)
        {
            int address = 132 + (index - 33) * 2;
            var operateResult = modbus.ReadUInt16($"x=3;{address}");
            value = operateResult.Content;
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }
        /// <summary>
        /// 写入输入信号AI33~AI64
        /// </summary>
        /// <param name="index">33~64</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool WriteAI(int index, float value)
        {
            int address = 132 + (index - 33) * 2;
            var operateResult = modbus.Write($"x=3;{address}", value);
            _errMsg = operateResult.Message;
            return operateResult.IsSuccess;
        }

        public bool Load()
        {
            //ip = "192.168.100.120";
            //port = 6502;
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "JAKAParam.xml";
                if (File.Exists(paramPath))
                {
                    XmlSerializer xml = new XmlSerializer(param.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        RobotParam _ = (RobotParam)xml.Deserialize(stream);
                        if (_ != null)
                        {
                            param = _;
                        }
                        else
                        {
                            _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result = false;
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }

            try
            {
                string paramPath = basePath + "JAKAIoParam.xml";
                if (File.Exists(paramPath))
                {
                    List<IoAddress> ios = new List<IoAddress>();
                    XmlSerializer xml = new XmlSerializer(ios.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        ios = (List<IoAddress>)xml.Deserialize(stream);
                    }
                    if (ios == null)
                    {
                        result = false;
                        _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                    else
                    {
                        ioDict = ios.ToDictionary(n => { return n.IoName; });
                    }
                }
                else
                {
                    result = false;
                    _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
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

                string OpcParamPath = basePath + "JAKAParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "JAKAIoParam.xml";
                XmlSerializer ioXml = new XmlSerializer(ios.GetType());
                using (FileStream stream = new FileStream(ioParamPath, FileMode.Create))
                {
                    ioXml.Serialize(stream, ios);
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }
            return result;
        }

        public bool Open()
        {
            modbus.IpAddress = param.IpAddress;
            modbus.Port = param.Port;
            modbus.ConnectTimeOut = 5000;     // 连接超时，单位毫秒
            modbus.ReceiveTimeOut = 3000;     // 接收超时，单位毫秒
            modbus.Station = 1;
            modbus.AddressStartWithZero = true;
            modbus.IsCheckMessageId = true;
            modbus.IsStringReverse = false;
            modbus.DataFormat = HslCommunication.Core.DataFormat.ABCD;

            var result = modbus.ConnectServer();
            if (result.IsSuccess)
            {
                _isOpen = true;
                return true;
            }
            else
            {
                _errMsg = result.Message;
                return false;
            }
        }

        public bool Close()
        {
            modbus.ConnectClose();
            _isOpen = false;
            return true;
        }

        public bool Read(DI eDI, out string value)
        {
            _errMsg = GlobalVarAndFunc.LanguageTranslate("不支持字符串");
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            _errMsg = GlobalVarAndFunc.LanguageTranslate("不支持字符串");
            value = "";
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            return Read(eDI.ToString(), out value);
        }
        public bool Read(DO eDO, out bool value)
        {
            return Read(eDO.ToString(), out value);
        }
        private bool Read(string ioName, out bool value)
        {
            if (ioDict.ContainsKey(ioName))
            {
                string ioAddress = ioDict[ioName].Address.Trim();
                if (!string.IsNullOrEmpty(ioAddress))
                {
                    if (ioAddress.Length > 2 && ioAddress[0] == 'D' && int.TryParse(ioAddress.Substring(2, ioAddress.Length - 2), out int index))
                    {
                        if (ioAddress[1] == 'I')
                        {
                            return ReadDI(index, out value);
                        }
                        else if (ioAddress[1] == 'O')
                        {
                            return ReadDO(index, out value);
                        }
                    }
                    _errMsg = ioName + GlobalVarAndFunc.LanguageTranslate("地址格式不匹配");
                }
                else
                {
                    value = false;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + GlobalVarAndFunc.LanguageTranslate("地址未分配");
            }
            value = false;
            return false;
        }

        public bool Read(DI eDI, out ushort value)
        {
            return Read(eDI.ToString(), out value);
        }
        public bool Read(DO eDO, out ushort value)
        {
            return Read(eDO.ToString(), out value);
        }
        private bool Read(string ioName, out ushort value)
        {
            if (ioDict.ContainsKey(ioName))
            {
                string ioAddress = ioDict[ioName].Address.Trim();
                if (!string.IsNullOrEmpty(ioAddress))
                {
                    if (ioAddress.Length > 2 && ioAddress[0] == 'A' && int.TryParse(ioAddress.Substring(2, ioAddress.Length - 2), out int index))
                    {
                        if (ioAddress[1] == 'I')
                        {
                            return ReadAI(index, out value);
                        }
                        else if (ioAddress[1] == 'O')
                        {
                            return ReadAO(index, out value);
                        }
                    }
                    _errMsg = ioName + GlobalVarAndFunc.LanguageTranslate("地址格式不匹配");
                }
                else
                {
                    value = 0;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + GlobalVarAndFunc.LanguageTranslate("地址未分配");
            }
            value = 0;
            return false;
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                string ioAddress = ioDict[eDO.ToString()].Address.Trim();
                if (!string.IsNullOrEmpty(ioAddress))
                {
                    lock (iolock)
                    {
                        if (value is bool)
                        {
                            if (ioAddress.Length > 2 && ioAddress[0] == 'D' && int.TryParse(ioAddress.Substring(2, ioAddress.Length - 2), out int index))
                            {
                                if (ioAddress[1] == 'I')
                                {
                                    return WriteDI(index, (bool)value);
                                }
                            }
                            _errMsg = eDO.ToString() + GlobalVarAndFunc.LanguageTranslate("地址格式不匹配");
                        }
                        else if (value is ushort)
                        {
                            if (ioAddress.Length > 2 && ioAddress[0] == 'A' && int.TryParse(ioAddress.Substring(2, ioAddress.Length - 2), out int index))
                            {
                                if (ioAddress[1] == 'I')
                                {
                                    return WriteAI(index, (ushort)value);
                                }
                            }
                            _errMsg = eDO.ToString() + GlobalVarAndFunc.LanguageTranslate("地址格式不匹配");
                        }
                        else
                        {
                            _errMsg = GlobalVarAndFunc.LanguageTranslate("写入格式不支持");
                            return false;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + GlobalVarAndFunc.LanguageTranslate("地址未分配");
            }
            return false;
        }
    }

    public class FanucRobot /*: IRobot*/
    {
        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen = false;

        string ip = string.Empty;
        int port = 60008;
        HslCommunication.Robot.FANUC.FanucInterfaceNet robot = new HslCommunication.Robot.FANUC.FanucInterfaceNet();
        public FanucRobot() { }

        public bool ReadPose(out HPose hPose)
        {
            var read = robot.ReadFanucData();
            if (read.IsSuccess)
            {
                double x = read.Content.CurrentPose.Xyzwpr[0] / 1000;
                double y = read.Content.CurrentPose.Xyzwpr[1] / 1000;
                double z = read.Content.CurrentPose.Xyzwpr[2] / 1000;
                double rx = read.Content.CurrentPose.Xyzwpr[3];
                double ry = read.Content.CurrentPose.Xyzwpr[4];
                double rz = read.Content.CurrentPose.Xyzwpr[5];
                hPose = new HPose(x, y, z, rx, ry, rz, "Rp+T", "abg", "point");
            }
            else
            {
                hPose = null;
            }
            _errMsg = read.Message;
            return read.IsSuccess;
        }

        public bool Load()
        {
            ip = "192.168.255.1";
            port = 60008;
            return true;
        }

        public bool Save()
        {
            return true;
        }

        public bool Open()
        {
            robot.IpAddress = ip;
            robot.Port = port;
            robot.CommunicationPipe = new HslCommunication.Core.Pipe.PipeTcpNet(ip, port)
            {
                ConnectTimeOut = 2000,    // 连接超时时间，单位毫秒
                ReceiveTimeOut = 5000,    // 接收设备数据反馈的超时时间
                SleepTime = 0,
                SocketKeepAliveTime = -1,
                IsPersistentConnection = true,
            };
            var result = robot.ConnectServer();
            if (result.IsSuccess)
            {
                _isOpen = true;
                return true;
            }
            else
            {
                _errMsg = result.Message;
                return false;
            }
        }

        public bool Close()
        {
            robot.ConnectClose();
            _isOpen = false;
            return true;
        }
    }
}
