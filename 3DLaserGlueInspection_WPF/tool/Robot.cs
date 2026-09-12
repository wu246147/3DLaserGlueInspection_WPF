//using HalconDotNet;
using _3DLaserGlueInspection.subForm;
using BaslerCamera.IO;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Siemens.S7PlusHelper;
using HslCommunication.Robot.YASKAWA;
using Kitware.VTK;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml.Serialization;
using TCPIP;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using Wpf_Replace_halcon;
using static OpenCvSharp.FileStorage;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace _3DLaserGlueInspection
{
    public enum RobotType
    {
        YRCRobot = 0,
        JAKARobot ,
        FanucRobot ,
        KukaRobot ,
        KawasakiRobot,
    }

    public interface IRobot
    {
        RobotParam Param { get; set; }
        Dictionary<string, IoAddress> IoDict { get; set; }
        bool IsOpen { get; }

        void ShowForm();


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
        bool ReadPose(out PoseParameters hPose);

        /// <summary>
        /// 获取坐标
        /// </summary>
        /// <param name="hPose"></param>
        /// <returns></returns>
        bool ReadPose(out PoseParameters hPose,out double R7);


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
        public bool Read坐标(out PoseParameters hPose)
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
                hPose = new PoseParameters();
                hPose.x = x; hPose.y = y; hPose.z = z;
                hPose.rx = rx; hPose.ry = ry; hPose.rz = rz;
                hPose.PoseType = 2;
            }
            else
            {
                hPose = null;
            }
            _errMsg = read.Message;
            return read.IsSuccess;
        }
        private IByteTransform byteTransform = new RegularByteTransform();
        public bool ReadPose(out PoseParameters hPose)
        {
            OperateResult<byte[]> operateResult = yrc.ReadCommand(117, 101, 0, 1, null);
            if (operateResult.IsSuccess && operateResult.Content.Length >= 44)
            {
                int[] array = new int[6];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = byteTransform.TransInt32(operateResult.Content, 20 + i * 4);
                }
                //hPose = new PoseParameters(array[0] / 1000000, array[1] / 1000000, array[2] / 1000000, array[3] / 10000, array[4] / 10000, array[5] / 10000, "Rp+T", "abg", "point");
                hPose = new PoseParameters();

                hPose.x = array[0] / 1000000; hPose.y = array[1] / 1000000; hPose.z = array[2] / 1000000;
                hPose.rx = array[3] / 10000; hPose.ry = array[4] / 10000; hPose.rz = array[5] / 10000;
                hPose.PoseType = 2;

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

    public class JAKARobot : IRobot
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
            new WindowRobot(this).ShowDialog();
        }

        public bool ReadPose(out PoseParameters hPose)
        {
            var operateResult = modbus.ReadFloat("x=4;406", 6);
            if (operateResult.IsSuccess)
            {
                var array = operateResult.Content;
                //hPose = new PoseParameters(array[0] / 1000, array[1] / 1000, array[2] / 1000, array[3], array[4], array[5], "Rp+T", "abg", "point");

                hPose = new PoseParameters();

                hPose.x = array[0] / 1000; hPose.y = array[1] / 1000; hPose.z = array[2] / 1000;
                hPose.rx = array[3]; hPose.ry = array[4]; hPose.rz = array[5];
                hPose.PoseType = 2;

                return true;
            }
            else
            {
                hPose = null;
                _errMsg = operateResult.Message;
                return false;
            }
        }

        public bool ReadPose(out PoseParameters hPose, out double R7)
        {
            bool rt = ReadPose(out hPose);
            R7 = 0.0;
            return rt;
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
                            _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                        _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                    else
                    {
                        ioDict = ios.ToDictionary(n => { return n.IoName; });
                    }
                }
                else
                {
                    result = false;
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.DoesNotSupportStrings;
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.DoesNotSupportStrings;
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
                    _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressFormatMismatch;
                }
                else
                {
                    value = false;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
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
                    _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressFormatMismatch;
                }
                else
                {
                    value = 0;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
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
                            _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressFormatMismatch;
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
                            _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressFormatMismatch;
                        }
                        else
                        {
                            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
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
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            return false;
        }

    }

    public class FanucRobot : IRobot
    {
        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen = false;

        RobotParam param = new RobotParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        public RobotParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }

        HslCommunication.Robot.FANUC.FanucInterfaceNet robot = new HslCommunication.Robot.FANUC.FanucInterfaceNet();
        public FanucRobot() { }

        public void ShowForm()
        {
            new WindowRobot(this).ShowDialog();
        }

        public bool ReadPose(out PoseParameters hPose)
        {
            OperateResult<float[]> xyzwpr = robot.ReadFloat("D751", 9);
            if (xyzwpr.IsSuccess)
            {
                double x = xyzwpr.Content[0] / 1000;
                double y = xyzwpr.Content[1] / 1000;
                double z = xyzwpr.Content[2] / 1000;
                double rx = xyzwpr.Content[3];
                double ry = xyzwpr.Content[4];
                double rz = xyzwpr.Content[5];
                //hPose = new HPose(x, y, z, rx, ry, rz, "Rp+T", "abg", "point");

                hPose = new PoseParameters();

                hPose.x = x;
                hPose.y = y;
                hPose.z = z;
                hPose.rx = rx;
                hPose.ry = ry;
                hPose.rz = rz;
                hPose.PoseType = 2;
            }
            else
            {
                hPose = null;
            }
            _errMsg = xyzwpr.Message;
            return xyzwpr.IsSuccess;
        }
        public bool ReadPose(out PoseParameters hPose, out double R7)
        {
            bool rt = ReadPose(out hPose);
            R7 = 0.0;
            return rt;
        }
        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "FanucParam.xml";
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
                            _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                string paramPath = basePath + "FanucIoParam.xml";
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
                        _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                    else
                    {
                        ioDict = ios.ToDictionary(n => { return n.IoName; });
                    }
                }
                else
                {
                    result = false;
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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

                string OpcParamPath = basePath + "FanucParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "FanucIoParam.xml";
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
            robot.IpAddress = param.IpAddress;
            robot.Port = param.Port;
            robot.ConnectTimeOut = 5000;     // 连接超时，单位毫秒
            robot.ReceiveTimeOut = 3000;     // 接收超时，单位毫秒
            //robot.Station = 1;
            //robot.AddressStartWithZero = true;
            //robot.IsCheckMessageId = true;
            //robot.IsStringReverse = false;
            //robot.DataFormat = HslCommunication.Core.DataFormat.ABCD;

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

        public bool Read(DI eDI, out string value)
        {
            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.DoesNotSupportStrings;
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.DoesNotSupportStrings;
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
                    if (ioAddress[0] == 'R')
                    {
                        var operateResult = robot.ReadUInt16(ioAddress);
                        value = operateResult.Content > 0;
                        _errMsg = operateResult.Message;
                        return operateResult.IsSuccess;
                    }
                    else
                    {
                        var operateResult = robot.ReadBool(ioAddress);
                        value = operateResult.Content;
                        _errMsg = operateResult.Message;
                        return operateResult.IsSuccess;
                    }
                }
                else
                {
                    value = false;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
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
                    var operateResult = robot.ReadUInt16(ioAddress);
                    value = operateResult.Content;
                    _errMsg = operateResult.Message;
                    return operateResult.IsSuccess;
                }
                else
                {
                    value = 0;
                    return true;
                }
            }
            else
            {
                _errMsg = ioName + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
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
                            if (ioAddress[0] == 'R')
                            {
                                var operateResult = robot.Write(ioAddress, (bool)value ? 1 : 0);
                                _errMsg = operateResult.Message;
                                return operateResult.IsSuccess;
                            }
                            else
                            {
                                var operateResult = robot.Write(ioAddress, (bool)value);
                                _errMsg = operateResult.Message;
                                return operateResult.IsSuccess;
                            }
                        }
                        else if (value is ushort)
                        {
                            var operateResult = robot.Write(ioAddress, (ushort)value);
                            _errMsg = operateResult.Message;
                            return operateResult.IsSuccess;
                        }
                        else
                        {
                            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
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
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            return false;
        }
    }

    public class KukaRobot : IRobot
    {
        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen = false;

        RobotParam param = new RobotParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        public RobotParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }

        HslCommunication.Robot.KUKA.KukaTcpNet robot = new HslCommunication.Robot.KUKA.KukaTcpNet();

        // TCP 通讯
        TCP_Server Server;
        //socket通讯，获取io信号
        Thread threadReconnect = null; // 通讯重连线程

        #region
        /// <summary>
        /// 开始信号
        /// </summary>

        bool isStart = false;


        /// <summary>
        /// 触发信号
        /// </summary>
        bool isPGON = false;


        /// <summary>
        /// 开始信号
        /// </summary>

        bool isStartEnd = false;


        /// <summary>
        /// 触发信号
        /// </summary>
        bool isPGONEnd = false;

        /// <summary>
        /// 中断信号
        /// </summary>
        bool isAbort = false;

        /// <summary>
        /// 结束信号
        /// </summary>
        bool isEND = false;

        //ushort 256

        /// <summary>
        /// 车型号
        /// </summary>
        int CarNumber = 0;

        public void resetSignal()
        {
            isStart = false;
            isPGON = false;
            isAbort = false;
            isEND = false;
            isStartEnd = false;
            isPGONEnd = false;
        }


        /// <summary>
        /// 根据通讯方式初始化连接线程
        /// </summary>
        public void InitCommunicationConnection()
        {

            IniTCPSocketServer();

        }

        /// <summary>
        /// 停止通讯线程
        /// </summary>
        public void StopCommunicationThread()
        {
            if (Server != null)
            {

                // TCP 停止监听并null
                if (Server != null)
                {
                    Server.StopListen();
                    Server = null;

                    //_timer.Stop();
                }
                // 等待线程结束
                if (!threadReconnect.Join(1000))  // 等待最多1秒
                {
                    try
                    {
                        threadReconnect.Abort();  // 强制终止（不推荐，但有时必要）
                    }
                    catch { }
                }
                threadReconnect = null;
            }
            
        }

        private void IniTCPSocketServer()
        {
            threadReconnect = new Thread(IniRobotTCPserver);
            threadReconnect.Start();
        }
        private void IniRobotTCPserver()
        {

            try
            {
                Server = new TCP_Server();

                bool rt = Server.Load();
                if (rt)
                {
                    //绑定委托与事件
                    Server.reserveInfoSignal += ProcessInfo;

                    //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ServerSignalLoadedSuccessfully);

                    //开始监听
                    Server.StartListen();

                    //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.TCPCommunicationServerSuccessfullyEstablished + "!");

                    return;
                }
                else
                {
                    //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ServerSignalLoadingFailed);

                }
            }

            catch (Exception ex)
            {
                //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.TCPCommunicationServerEstablishmentFailed);
                _errMsg = ex.Message;
                return;
            }
        }

        void ProcessInfo(string info)
        {
            int PROGRAM_ID = 0;
            int COMMAND_ID = 0;
            int POINT_ID = 0;
            Thread cameraThread;
            try
            {
                //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReceivedSignal + ":" + info);

                //string[] tempReceivedParts;
                //tempReceivedParts = info.Split(',');

                var doc = XDocument.Parse(info);
                var root = doc.Root;

                //车型
                int programId = int.Parse(root.Element("PROGRAM_ID").Value);
                //指令
                int commandId = int.Parse(root.Element("COMMAND_ID").Value);
                //指令值
                int pointId = int.Parse(root.Element("POINT_ID").Value);


                CarNumber = programId;
                switch (commandId)
                {
                    case 0:
                        //开始信号
                        if (pointId == 1)
                        {
                            isStart = true;
                        }
                        else if (pointId == 0)
                        {
                            isStart = false;
                        }
                        break;
                    case 1:
                        //启动信号
                        if (pointId == 1)
                        {
                            isPGON = true;
                        }
                        else if (pointId == 0)
                        {
                            isPGON = false;
                        }
                        break;

                    case 2:
                        //开始信号结束
                        if (pointId == 1)
                        {
                            isStartEnd = true;
                        }
                        else if (pointId == 0)
                        {
                            isStartEnd = false;
                        }
                        break;
                    case 3:
                        //启动信号结束
                        if (pointId == 1)
                        {
                            isPGONEnd = true;
                        }
                        else if (pointId == 0)
                        {
                            isPGONEnd = false;
                        }
                        break;
                    case 4:
                        //中断信号
                        if (pointId == 1)
                        {
                            isAbort = true;
                        }
                        else if (pointId == 0)
                        {
                            isAbort = false;
                        }
                        break;
                    case 5:
                        //停止信号
                        if (pointId == 1)
                        {
                            isEND = true;
                        }
                        else if (pointId == 0)
                        {
                            isEND = false;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }


        public string serializationInfoKuka(double x, double y, double z, double a, double b, double c, int result, string setflat)
        {

            XElement xml = new XElement("VISION",


                new XElement("ROBOTPOS",
                    new XAttribute("X", x),
                    new XAttribute("Y", y),
                    new XAttribute("Z", z),
                    new XAttribute("A", a),
                    new XAttribute("B", b),
                    new XAttribute("C", c),
                    ""
                ),

                new XElement("RESULT", result),


                new XAttribute("Set_Flag", setflat), ""

            );

            return xml.ToString();
        }


        void SocketSend(int RESULT = 0)
        {
            string msg = serializationInfoKuka(0, 0, 0, 0, 0, 0, RESULT, "11");
            //ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.SendSignal + ":" + msg);
            Server.Send(msg);
            
        }


        #endregion



        public KukaRobot() 
        {
            InitCommunicationConnection();
        }

        
        public void Dispose()
        {
            StopCommunicationThread();
        }

        public void ShowForm()
        {
            new WindowRobot(this).ShowDialog();
        }

        public bool FormatTransform(string info, out PoseParameters hPose)
        {
            hPose = new PoseParameters();
            try
            {
                var pattern = @"\b([XYZABC])\s+([-+]?\d*\.?\d+)";
                var matches = Regex.Matches(info, pattern);

                var dict = new Dictionary<string, double>();
                foreach (Match m in matches)
                {
                    dict[m.Groups[1].Value] = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                }

                if (dict.ContainsKey("X"))
                {
                    double x = dict["X"];
                    hPose.x = x/1000;
                }
                else { return false; }

                if (dict.ContainsKey("Y"))
                {
                    double y = dict["Y"];
                    hPose.y = y/1000;
                }
                else { return false; }
                if (dict.ContainsKey("Z"))
                {
                    double z = dict["Z"];
                    hPose.z = z / 1000;
                }
                else { return false; }
                if (dict.ContainsKey("A"))
                {
                    double a = dict["A"];
                    hPose.rz = a;
                }
                else { return false; }
                if (dict.ContainsKey("B"))
                {
                    double b = dict["B"];
                    hPose.ry = b;
                }
                else { return false; }
                if (dict.ContainsKey("C"))
                {
                    double c = dict["C"];
                    hPose.rx = c;
                }
                else { return false; }

                hPose.PoseType = 2;
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
        public bool ReadPose(out PoseParameters hPose)
        {
            bool rt = Read("$POS_ACT", out string info);
            hPose = new PoseParameters();

            if (rt)
            {
                rt = FormatTransform(info, out hPose); 
            }
            

            return rt;
        }
        public bool ReadPose(out PoseParameters hPose, out double R7)
        {
            bool rt = ReadPose(out hPose);
            R7 = 0.0;
            return rt;
        }
        public bool Read(string key, out string info)
        {
            var value = robot.ReadString(key);

            info = value.Content;
            return value.IsSuccess;
        }


        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "KukaParam.xml";
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
                            _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                string paramPath = basePath + "KukaIoParam.xml";
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
                        _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                    else
                    {
                        ioDict = ios.ToDictionary(n => { return n.IoName; });
                    }
                }
                else
                {
                    result = false;
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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

                string OpcParamPath = basePath + "KukaParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "KukaIoParam.xml";
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
            robot.IpAddress = param.IpAddress;
            robot.Port = param.Port;
            robot.ConnectTimeOut = 5000;     // 连接超时，单位毫秒
            robot.ReceiveTimeOut = 3000;     // 接收超时，单位毫秒
            //robot.Station = 1;
            //robot.AddressStartWithZero = true;
            //robot.IsCheckMessageId = true;
            //robot.IsStringReverse = false;
            //robot.DataFormat = HslCommunication.Core.DataFormat.ABCD;

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

        public bool Read(DI eDI, out string value)
        {
            _errMsg = "不支持字符串";
            value = null;
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            _errMsg = "不支持字符串";
            value = null;
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            value = false;
            switch (eDI.ToString())
            {
                case "Start":
                    value = isStart;
                    return true;
                case "Abort":
                    value = isAbort;
                    return true;
                case "PGON":
                    value = isPGON;
                    return true;
                case "isStartEnd":
                    value = isStartEnd;
                    return true;
                case "isPGONEnd":
                    value = isPGONEnd;
                    return true;
                case "END":
                    value = isEND;
                    return true;

                default:
                    _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
                    break;
            }

            return false;
        }
        public bool Read(DO eDO, out bool value)
        {
            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.UnsupportedBool;
            value = false;

            return false;
        }

        public bool Read(DI eDI, out ushort value)
        {
            value = 0;
            switch (eDI.ToString())
            {
                case "CarNumber":
                    value = (ushort)CarNumber;
                    return true;

                default:
                    _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
                    break;
            }

            return false;
        }
        public bool Read(DO eDO, out ushort value)
        {
            _errMsg = "不支持读取输出";
            value = 0;
            return false;
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            switch (eDO.ToString())
            {
                case "Result":
                    SocketSend((int)value);
                    return true;

                default:
                    _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
                    break;
            }
            return false;
        }
    }


    public class KawasakiRobotOld : IRobot
    {

        public string serializationInfo(double x, double y, double z, double a, double b, double c, int result, string setflat)
        {
            return "";
        }

        private static bool deserializationInfo(string info, out float X, out float Y, out float Z, out float RX, out float RY, out float RZ, out float R1, out float R2, out float R3, out float R4, out float R5, out float R6, out int programID, out int commandID, out int pointID)
        {
            X = 0;
            Y = 0;
            Z = 0;
            RX = 0;
            RY = 0;
            RZ = 0;
            R1 = 0;
            R2 = 0;
            R3 = 0;
            R4 = 0;
            R5 = 0;
            R6 = 0;
            programID = 0;
            commandID = 0;
            pointID = 0;

            try
            {
                // 去掉末尾的 #
                string raw = info.TrimEnd('#').Trim();
                string[] parts = raw.Split(',');

                // 辅助：安全解析 float，空串或解析失败返回默认值
                float SafeFloat(int index, float defaultValue = 0f)
                {
                    if (index < 0 || index >= parts.Length) return defaultValue;
                    string s = parts[index].Trim();
                    if (string.IsNullOrEmpty(s)) return defaultValue;
                    return float.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : defaultValue;
                }

                // 辅助：安全解析 int
                int SafeInt(int index, int defaultValue = 0)
                {
                    if (index < 0 || index >= parts.Length) return defaultValue;
                    string s = parts[index].Trim();
                    if (string.IsNullOrEmpty(s)) return defaultValue;
                    return int.TryParse(s, out int v) ? v : defaultValue;
                }

                // ---- 关节角 J1~J6 (索引 7~12) ----
                R1 = SafeFloat(7);    // J1: -86.6277
                R2 = SafeFloat(8);    // J2: -27.53063
                R3 = SafeFloat(9);    // J3: -18.65816
                R4 = SafeFloat(10);   // J4: -178.16177
                R5 = SafeFloat(11);   // J5: 12.41696
                R6 = SafeFloat(12);   // J6: -5.59366

                // ---- 末端位姿 X,Y,Z,RX,RY,RZ (倒数第 7~2 位) ----
                int total = parts.Length;
                X = SafeFloat(total - 6);   // -2446.65137
                Y = SafeFloat(total - 5);   // 218.11565
                Z = SafeFloat(total - 4);   // 2085.89941
                RX = SafeFloat(total - 3);   // 87.2569
                RY = SafeFloat(total - 2);   // 86.23324
                RZ = SafeFloat(total - 1);   // -3.54589

                //zyz 转 xyz
                double transformRX = 0, transformRY = 0, transformRz = 0;
                int robot_r_type = 2;   //机器人的坐标系类型，0为xyz，1为zyx，2为zyz
                int alg_r_type = 0;      //相机的坐标系类型，默认都是0，0为xyz，1为zyx，2为zyz

                CoordinateTransform.transformCartPose2(RX, RY, RZ, robot_r_type, ref transformRX, ref transformRY, ref transformRz, alg_r_type);

                RX = (float)transformRX;
                RY = (float)transformRY;
                RZ = (float)transformRz;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[deserializationInfo] 解析异常: {ex.Message}");
                return false;
            }
        }


        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen = false;

        string name = null;
        public string robotName { get => name; set => name = value; }

        RobotParam param = new RobotParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        public RobotParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }

        TCP_Client robot;


        //寄存数据
        bool _isExist = false;
        float _X = 0, _Y = 0, _Z = 0, _RX = 0, _RY = 0, _RZ = 0,
            _R1 = 0, _R2 = 0, _R3 = 0, _R4 = 0, _R5 = 0, _R6 = 0;
        int _programID = 0, _commandID = 0, _pointID = 0;

        bool _isConnected = false;

        public KawasakiRobotOld()
        {

        }
        public void ShowForm()
        {
            new WindowRobot(this).ShowDialog();
        }
        public bool ReadPose(out PoseParameters hPose)
        {
            //var read = robot.ReadFanucData();
            if (_isExist)
            {
                double x = _X / 1000;
                double y = _Y / 1000;
                double z = _Z / 1000;
                double rx = _RX;
                double ry = _RY;
                double rz = _RZ;
                hPose = new PoseParameters();


                hPose.x = x;
                hPose.y = y;
                hPose.z = z;
                hPose.rx = rx;
                hPose.ry = ry;
                hPose.rz = rz;
            }
            else
            {
                hPose = null;
            }
            return _isExist;
        }

        public bool ReadPose(out PoseParameters hPose, out double R7)
        {
            bool rt = ReadPose(out hPose);
            R7 = 0.0;
            return rt;
        }


        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + $"Data\\";
            try
            {
                string paramPath = basePath + "KawasakiParam.xml";
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
                            _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                            result = false;
                        }
                    }
                }
                else
                {
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                string paramPath = basePath + "KawasakiIoParam.xml";
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
                        _errMsg = paramPath +_3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                    else
                    {
                        ioDict = ios.ToDictionary(n => { return n.IoName; });
                    }
                }
                else
                {
                    result = false;
                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                string basePath = AppDomain.CurrentDomain.BaseDirectory + $"Data\\";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                string OpcParamPath = basePath + "KawasakiParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "KawasakiIoParam.xml";
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


        void ProcessInfo(string info)
        {
            ///
            /// 信号处理
            ///

            //序列化信息
            float X, Y, Z, RX, RY, RZ, R1, R2, R3, R4, R5, R6;
            int programID, commandID, pointID;

            bool rt = deserializationInfo(info, out X, out Y, out Z, out RX, out RY, out RZ, out R1, out R2, out R3, out R4, out R5, out R6, out programID, out commandID, out pointID);
            // 更新缓存数据
            if (rt)
            {
                _isExist = true;
            }
            else
            {
                _isExist = false;
            }
            _X = X;
            _Y = Y;
            _Z = Z;
            _RX = RX;
            _RY = RY;
            _RZ = RZ;

            _R1 = R1;
            _R2 = R2;
            _R3 = R3;
            _R4 = R4;
            _R5 = R5;
            _R6 = R6;

        }

        public bool isOpen()
        {
            return IsOpen;
        }

        public bool isConnected()
        {
            return true;
        }
        public bool Open()
        {
            robot = new TCP_Client(param.IpAddress, param.Port);
            //绑定委托与事件
            robot.OnDataReceived += ProcessInfo;
            //开始监听
            robot.Connect();

            _isOpen = true;
            return true;
        }
        public bool Open(string ip, int port)
        {
            param.IpAddress = ip;
            param.Port = port;
            return Open();
        }

        public bool Close()
        {
            if (robot != null)
            {
                robot.Disconnect();
            }
            _isOpen = false;
            return true;
        }


        public bool Read(DI eDI, out string value)
        {
            _errMsg = "不支持string";
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            _errMsg = "不支持string";
            value = "";
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            _errMsg = "不支持bool";
            value = false;
            return false;
        }
        public bool Read(DO eDO, out bool value)
        {
            value = true;
            return false;
        }
        private bool Read(string ioName, out bool value)
        {
            value = true;
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
            value = 0;
            return false;
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            return false;
        }
    }


    public class KawasakiRobot : IRobot
    {
        #region 基础属性

        public string ErrMsg => _errMsg;
        private string _errMsg = string.Empty;

        public bool IsOpen => _isOpen;
        private bool _isOpen = false;

        private bool _isConnected = false;

        private string _robotName;
        public string robotName
        {
            get => _robotName;
            set => _robotName = value;
        }

        private RobotParam _param = new RobotParam();
        public RobotParam Param
        {
            get => _param;
            set => _param = value;
        }

        private Dictionary<string, IoAddress> _ioDict =
            new Dictionary<string, IoAddress>();

        public Dictionary<string, IoAddress> IoDict
        {
            get => _ioDict;
            set => _ioDict = value;
        }

        private TCP_Client robot;

        #endregion

        #region TCP 阻塞通讯相关字段

        /// <summary>
        /// 同一时间只允许一个命令发送并等待响应。
        /// 防止多个线程同时发送命令导致响应错位。
        /// </summary>
        private readonly object _ioLock = new object();

        /// <summary>
        /// 保护接收缓存。
        /// </summary>
        private readonly object _receiveLock = new object();

        /// <summary>
        /// TCP 接收缓存。
        /// </summary>
        private readonly StringBuilder _receiveBuffer =
            new StringBuilder();

        /// <summary>
        /// 保护当前等待的响应。
        /// </summary>
        private readonly object _responseLock = new object();

        /// <summary>
        /// 当前等待中的响应任务。
        /// TCP 后台线程收到响应后，会完成此任务。
        /// </summary>
        private TaskCompletionSource<string> _pendingResponse;

        /// <summary>
        /// 判断收到的消息是否属于当前等待的命令。
        /// </summary>
        private Func<string, bool> _pendingResponseMatcher;

        /// <summary>
        /// 保护机器人位姿缓存。
        /// </summary>
        private readonly object _poseLock = new object();

        /// <summary>
        /// 当前通讯协议的消息结束符。
        /// 你原来的代码中使用了 TrimEnd('#')，所以这里使用 #。
        /// </summary>
        private const char FrameEndChar = '#';

        /// <summary>
        /// 阻塞等待响应的超时时间。
        /// </summary>
        public int ResponseTimeoutMilliseconds { get; set; } = 3000;

        #endregion

        #region 机器人数据缓存

        private bool _isExist = false;

        private float _X = 0;
        private float _Y = 0;
        private float _Z = 0;
        private float _RX = 0;
        private float _RY = 0;
        private float _RZ = 0;

        private float _R1 = 0;
        private float _R2 = 0;
        private float _R3 = 0;
        private float _R4 = 0;
        private float _R5 = 0;
        private float _R6 = 0;

        private int _programID = 0;
        private int _commandID = 0;
        private int _pointID = 0;

        #endregion

        public KawasakiRobot()
        {
        }

        #region 数据序列化和反序列化

        public string serializationInfo(
            double x,
            double y,
            double z,
            double a,
            double b,
            double c,
            int result,
            string setflat)
        {
            /*
             * 这里根据 Kawasaki 实际通讯协议生成发送字符串。
             *
             * 例如：
             * return string.Format(
             *     CultureInfo.InvariantCulture,
             *     "CMD,{0},{1},{2}#",
             *     x, y, z);
             */

            return string.Empty;
        }

        private static bool deserializationInfo(
            string info,
            out float X,
            out float Y,
            out float Z,
            out float RX,
            out float RY,
            out float RZ,
            out float R1,
            out float R2,
            out float R3,
            out float R4,
            out float R5,
            out float R6,
            out int programID,
            out int commandID,
            out int pointID)
        {
            X = 0;
            Y = 0;
            Z = 0;
            RX = 0;
            RY = 0;
            RZ = 0;

            R1 = 0;
            R2 = 0;
            R3 = 0;
            R4 = 0;
            R5 = 0;
            R6 = 0;

            programID = 0;
            commandID = 0;
            pointID = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(info))
                    return false;

                // 去除消息末尾的结束符
                string raw = info.TrimEnd('#').Trim();

                string[] parts = raw.Split(',');

                float SafeFloat(
                    int index,
                    float defaultValue = 0f)
                {
                    if (index < 0 || index >= parts.Length)
                        return defaultValue;

                    string text = parts[index].Trim();

                    if (string.IsNullOrEmpty(text))
                        return defaultValue;

                    return float.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float value)
                        ? value
                        : defaultValue;
                }

                int SafeInt(
                    int index,
                    int defaultValue = 0)
                {
                    if (index < 0 || index >= parts.Length)
                        return defaultValue;

                    string text = parts[index].Trim();

                    if (string.IsNullOrEmpty(text))
                        return defaultValue;

                    return int.TryParse(
                        text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value)
                        ? value
                        : defaultValue;
                }

                // 根据原代码，索引 7~12 为 J1~J6
                R1 = SafeFloat(7);
                R2 = SafeFloat(8);
                R3 = SafeFloat(9);
                R4 = SafeFloat(10);
                R5 = SafeFloat(11);
                R6 = SafeFloat(12);

                /*
                 * 位姿数据从消息末尾读取：
                 * X, Y, Z, RX, RY, RZ
                 */
                int total = parts.Length;

                X = SafeFloat(total - 6);
                Y = SafeFloat(total - 5);
                Z = SafeFloat(total - 4);
                RX = SafeFloat(total - 3);
                RY = SafeFloat(total - 2);
                RZ = SafeFloat(total - 1);

                /*
                 * Kawasaki 使用 ZYZ，
                 * 当前算法使用 XYZ，因此进行姿态转换。
                 */
                double transformRX = 0;
                double transformRY = 0;
                double transformRZ = 0;

                int robotPoseType = 2; // Kawasaki：ZYZ
                int algorithmPoseType = 0; // 算法：XYZ

                CoordinateTransform.transformCartPose2(
                    RX,
                    RY,
                    RZ,
                    robotPoseType,
                    ref transformRX,
                    ref transformRY,
                    ref transformRZ,
                    algorithmPoseType);

                RX = (float)transformRX;
                RY = (float)transformRY;
                RZ = (float)transformRZ;

                /*
                 * 以下字段需要根据你的实际协议确定。
                 * 如果协议中存在程序号、命令号、点号，
                 * 可以在这里解析。
                 */
                programID = SafeInt(0);
                commandID = SafeInt(1);
                pointID = SafeInt(2);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[deserializationInfo] 解析异常：" + ex.Message);

                return false;
            }
        }

        #endregion

        #region 位姿读取

        public bool ReadPose(out PoseParameters hPose)
        {
            lock (_poseLock)
            {
                string command = "";
                string response;
                hPose = new PoseParameters();


                //发送信息并阻塞等待
                if (!TrySendAndWait(
                command,
                IsReadResponse,
                out response))
                {
                    return false;
                }

                string value = response
                    .Trim()
                    .TrimEnd(FrameEndChar)
                    .Trim();

                float X, Y, Z, RX, RY, RZ;
                float R1, R2, R3, R4, R5, R6;
                int programID, commandID, pointID;

                bool result = deserializationInfo(
                value,
                out X,
                out Y,
                out Z,
                out RX,
                out RY,
                out RZ,
                out R1,
                out R2,
                out R3,
                out R4,
                out R5,
                out R6,
                out programID,
                out commandID,
                out pointID);


                hPose =new PoseParameters()
                {
                    x = X / 1000.0,
                    y = Y / 1000.0,
                    z = Z / 1000.0,
                    rx = RX,
                    ry = RY,
                    rz = RZ
                };

                

                return true;
            }
        }

        public bool ReadPose(out PoseParameters hPose, out double R7)
        {
            bool rt = ReadPose(out hPose);
            R7 = 0.0;
            return rt;
        }

        #endregion

        #region TCP 接收处理

        /// <summary>
        /// TCP 接收回调。
        ///
        /// 注意：
        /// TCP 一次接收不一定对应一条完整消息，
        /// 也可能一次接收半条消息或多条消息。
        /// 所以这里先进行消息分帧。
        /// </summary>
        private void ProcessInfo(string incomingData)
        {
            if (string.IsNullOrEmpty(incomingData))
                return;

            List<string> frames = ExtractFrames(incomingData);

            foreach (string frame in frames)
            {
                ProcessFrame(frame);
            }
        }

        /// <summary>
        /// 按照 # 拆分完整消息。
        /// </summary>
        private List<string> ExtractFrames(string incomingData)
        {
            List<string> frames = new List<string>();

            lock (_receiveLock)
            {
                /*
                 * 如果 TCP_Client 内部已经完成分帧，
                 * 并且当前消息中没有结束符，
                 * 则直接认为它是一条完整消息。
                 */
                if (_receiveBuffer.Length == 0 &&
                    incomingData.IndexOf(FrameEndChar) < 0)
                {
                    frames.Add(incomingData);
                    return frames;
                }

                _receiveBuffer.Append(incomingData);

                while (true)
                {
                    string currentData = _receiveBuffer.ToString();

                    int endIndex =
                        currentData.IndexOf(FrameEndChar);

                    if (endIndex < 0)
                        break;

                    string frame = currentData.Substring(
                        0,
                        endIndex + 1);

                    frames.Add(frame);

                    _receiveBuffer.Remove(
                        0,
                        endIndex + 1);
                }
            }

            return frames;
        }

        /// <summary>
        /// 处理一条完整消息。
        /// </summary>
        private void ProcessFrame(string frame)
        {

            /*
             * 再判断当前消息是否是某个命令等待的响应。
             */
            CompletePendingResponse(frame);
        }

        /// <summary>
        /// 收到匹配的响应后，唤醒等待线程。
        /// </summary>
        private void CompletePendingResponse(string frame)
        {
            TaskCompletionSource<string> responseSource = null;

            lock (_responseLock)
            {
                if (_pendingResponse == null)
                    return;

                /*
                 * 只接收属于当前命令的响应。
                 * 普通状态消息会被忽略，不会唤醒等待线程。
                 */
                if (_pendingResponseMatcher != null &&
                    !_pendingResponseMatcher(frame))
                {
                    return;
                }

                responseSource = _pendingResponse;

                _pendingResponse = null;
                _pendingResponseMatcher = null;
            }

            /*
             * 不在锁内部唤醒任务，避免锁竞争。
             */
            responseSource.TrySetResult(frame);
        }

        #endregion

        #region 连接和关闭

        public bool isOpen()
        {
            return IsOpen;
        }

        public bool isConnected()
        {
            return _isConnected;
        }

        public void ShowForm()
        {
            new WindowRobot(this).ShowDialog();
        }

        public bool Open()
        {
            try
            {
                if (_isOpen)
                    return true;

                robot = new TCP_Client(
                    _param.IpAddress,
                    _param.Port);

                /*
                 * TCP_Client 的后台接收线程收到数据后，
                 * 会调用 ProcessInfo。
                 */
                robot.OnDataReceived += ProcessInfo;

                robot.Connect();

                _isOpen = true;
                _isConnected = true;
                _errMsg = string.Empty;

                return true;
            }
            catch (Exception ex)
            {
                _isOpen = false;
                _isConnected = false;
                _errMsg = ex.Message;

                return false;
            }
        }

        public bool Open(string ip, int port)
        {
            _param.IpAddress = ip;
            _param.Port = port;

            return Open();
        }

        public bool Close()
        {
            try
            {
                /*
                 * 如果当前有线程正在等待机器人响应，
                 * 连接关闭时需要主动唤醒它。
                 */
                FailPendingResponse(
                    new IOException("机器人连接已关闭"));

                if (robot != null)
                {
                    robot.OnDataReceived -= ProcessInfo;
                    robot.Disconnect();
                    robot = null;
                }

                _isOpen = false;
                _isConnected = false;

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;

                _isOpen = false;
                _isConnected = false;

                return false;
            }
        }

        #endregion

        #region 阻塞式发送和等待响应

        /// <summary>
        /// 发送命令。
        ///
        /// 这里是唯一需要根据 TCP_Client 实际接口修改的地方。
        /// 如果实际方法叫 SendData、Write 或 SendString，
        /// 只需要修改下面这一行。
        /// </summary>
        private void SendCommand(string command)
        {
            // 根据 TCP_Client 实际接口修改
            robot.Send(command);
        }

        /// <summary>
        /// 发送命令并阻塞等待响应。
        ///
        /// 当前线程会在 responseSource.Task.Wait()
        /// 处等待，但 TCP_Client 的后台接收线程仍然可以继续运行。
        /// </summary>
        private bool TrySendAndWait(
            string command,
            Func<string, bool> responseMatcher,
            out string response)
        {
            response = null;

            if (string.IsNullOrWhiteSpace(command))
            {
                _errMsg = "发送命令不能为空";
                return false;
            }

            lock (_ioLock)
            {
                if (!_isOpen || robot == null)
                {
                    _errMsg = "机器人未连接";
                    return false;
                }

                TaskCompletionSource<string> responseSource =
                    new TaskCompletionSource<string>();

                /*
                 * 必须先注册等待对象，再发送命令。
                 * 防止机器人响应速度太快，导致响应消息丢失。
                 */
                lock (_responseLock)
                {
                    if (_pendingResponse != null)
                    {
                        _errMsg = "当前已有命令正在等待响应";
                        return false;
                    }

                    _pendingResponse = responseSource;
                    _pendingResponseMatcher = responseMatcher;
                }

                try
                {
                    SendCommand(command);

                    /*
                     * 阻塞等待响应。
                     * 后台接收线程收到匹配消息后，
                     * 会调用 CompletePendingResponse。
                     */
                    bool received = responseSource.Task.Wait(
                        ResponseTimeoutMilliseconds);

                    if (!received)
                    {
                        _errMsg = "等待机器人响应超时";
                        return false;
                    }

                    response = responseSource.Task.Result;
                    return true;
                }
                catch (AggregateException ex)
                {
                    _errMsg = ex.InnerException == null
                        ? ex.Message
                        : ex.InnerException.Message;

                    return false;
                }
                catch (Exception ex)
                {
                    _errMsg = ex.Message;
                    return false;
                }
                finally
                {
                    /*
                     * 超时、异常或正常完成后，
                     * 都必须清理当前等待对象。
                     */
                    lock (_responseLock)
                    {
                        if (object.ReferenceEquals(
                            _pendingResponse,
                            responseSource))
                        {
                            _pendingResponse = null;
                            _pendingResponseMatcher = null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 连接关闭时，结束正在等待的命令。
        /// </summary>
        private void FailPendingResponse(Exception exception)
        {
            TaskCompletionSource<string> responseSource = null;

            lock (_responseLock)
            {
                responseSource = _pendingResponse;

                _pendingResponse = null;
                _pendingResponseMatcher = null;
            }

            if (responseSource != null)
            {
                responseSource.TrySetException(exception);
            }
        }

        #endregion

        #region IO 命令



        /// <summary>
        /// 判断是否为读取响应。
        ///
        /// 必须根据 Kawasaki 返回协议修改。
        /// 这里假定响应中包含 READ、ACK、OK 或 ERR。
        /// </summary>
        private bool IsReadResponse(string frame)
        {
            return ContainsResponseKeyword(frame);
        }

        /// <summary>
        /// 判断是否为写入响应。
        /// </summary>
        private bool IsWriteResponse(string frame)
        {
            return ContainsResponseKeyword(frame);
        }

        private bool ContainsResponseKeyword(string frame)
        {
            if (string.IsNullOrEmpty(frame))
                return false;

            return frame.IndexOf(
                       "READ",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   ||
                   frame.IndexOf(
                       "ACK",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   ||
                   frame.IndexOf(
                       "OK",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   ||
                   frame.IndexOf(
                       "ERR",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 判断写入是否成功。
        /// </summary>
        private bool IsSuccessResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return false;

            return response.IndexOf(
                       "ERR",
                       StringComparison.OrdinalIgnoreCase) < 0
                   &&
                   response.IndexOf(
                       "NG",
                       StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>
        /// 读取 IO 原始字符串。
        /// </summary>
        private bool ReadIo(
            string ioType,
            string ioName,
            out string value)
        {
            value = string.Empty;

            string command="";

            string response;

            if (!TrySendAndWait(
                command,
                IsReadResponse,
                out response))
            {
                return false;
            }

            value = response
                .Trim()
                .TrimEnd(FrameEndChar)
                .Trim();

            return true;
        }

        public bool Read(
            DI eDI,
            out string value)
        {
            return ReadIo(
                "DI",
                eDI.ToString(),
                out value);
        }

        public bool Read(
            DO eDO,
            out string value)
        {
            return ReadIo(
                "DO",
                eDO.ToString(),
                out value);
        }

        public bool Read(
            DI eDI,
            out bool value)
        {
            value = false;

            if (!Read(eDI, out string text))
                return false;

            return TryParseBool(
                text,
                out value);
        }

        public bool Read(
            DO eDO,
            out bool value)
        {
            value = false;

            if (!Read(eDO, out string text))
                return false;

            return TryParseBool(
                text,
                out value);
        }

        public bool Read(
            DI eDI,
            out ushort value)
        {
            value = 0;

            if (!Read(eDI, out string text))
                return false;

            return TryParseUShort(
                text,
                out value);
        }

        public bool Read(
            DO eDO,
            out ushort value)
        {
            value = 0;

            if (!Read(eDO, out string text))
                return false;

            return TryParseUShort(
                text,
                out value);
        }

        /// <summary>
        /// 写入 DO，并阻塞等待机器人响应。
        /// </summary>
        public bool Write(
            DO eDO,
            object value)
        {
            string command ="";

            string response;

            if (!TrySendAndWait(
                command,
                IsWriteResponse,
                out response))
            {
                return false;
            }

            return IsSuccessResponse(response);
        }

        private bool TryParseBool(
            string text,
            out bool value)
        {
            value = false;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string raw = text
                .Trim()
                .TrimEnd(FrameEndChar)
                .Trim();

            string[] parts = raw.Split(',');

            string lastValue = parts
                .LastOrDefault(n => !string.IsNullOrWhiteSpace(n));

            if (string.IsNullOrEmpty(lastValue))
                return false;

            lastValue = lastValue.Trim();

            if (lastValue == "1" ||
                lastValue.Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase) ||
                lastValue.Equals(
                    "on",
                    StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (lastValue == "0" ||
                lastValue.Equals(
                    "false",
                    StringComparison.OrdinalIgnoreCase) ||
                lastValue.Equals(
                    "off",
                    StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        private bool TryParseUShort(
            string text,
            out ushort value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string raw = text
                .Trim()
                .TrimEnd(FrameEndChar)
                .Trim();

            string[] parts = raw.Split(',');

            string lastValue = parts
                .LastOrDefault(n => !string.IsNullOrWhiteSpace(n));

            if (string.IsNullOrEmpty(lastValue))
                return false;

            return ushort.TryParse(
                lastValue.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        #endregion

        #region 参数保存和加载

        public bool Load()
        {
            bool result = true;

            string basePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data");

            try
            {
                string paramPath = Path.Combine(
                    basePath,
                    "KawasakiParam.xml");

                if (!File.Exists(paramPath))
                {
                    _errMsg = paramPath +
                              " 文件不存在";
                    result = false;
                }
                else
                {
                    XmlSerializer xml =
                        new XmlSerializer(_param.GetType());

                    using (FileStream stream =
                           new FileStream(
                               paramPath,
                               FileMode.Open,
                               FileAccess.Read))
                    {
                        RobotParam loadedParam =
                            (RobotParam)xml.Deserialize(stream);

                        if (loadedParam != null)
                            _param = loadedParam;
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }

            try
            {
                string ioPath = Path.Combine(
                    basePath,
                    "KawasakiIoParam.xml");

                if (!File.Exists(ioPath))
                {
                    _errMsg = ioPath +
                              " 文件不存在";
                    result = false;
                }
                else
                {
                    XmlSerializer xml =
                        new XmlSerializer(
                            typeof(List<IoAddress>));

                    using (FileStream stream =
                           new FileStream(
                               ioPath,
                               FileMode.Open,
                               FileAccess.Read))
                    {
                        List<IoAddress> ios =
                            (List<IoAddress>)xml.Deserialize(stream);

                        if (ios == null)
                        {
                            result = false;
                        }
                        else
                        {
                            _ioDict = ios.ToDictionary(
                                n => n.IoName);
                        }
                    }
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
            try
            {
                string basePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Data");

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                string paramPath = Path.Combine(
                    basePath,
                    "KawasakiParam.xml");

                XmlSerializer paramXml =
                    new XmlSerializer(_param.GetType());

                using (FileStream stream =
                       new FileStream(
                           paramPath,
                           FileMode.Create,
                           FileAccess.Write))
                {
                    paramXml.Serialize(stream, _param);
                }

                string ioPath = Path.Combine(
                    basePath,
                    "KawasakiIoParam.xml");

                XmlSerializer ioXml =
                    new XmlSerializer(
                        typeof(List<IoAddress>));

                using (FileStream stream =
                       new FileStream(
                           ioPath,
                           FileMode.Create,
                           FileAccess.Write))
                {
                    ioXml.Serialize(
                        stream,
                        _ioDict.Values.ToList());
                }

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.ToString();
                return false;
            }
        }
        #endregion
    }

}
