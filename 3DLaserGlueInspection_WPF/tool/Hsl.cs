using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Melsec;
using HslCommunication.Profinet.Omron;
using HslCommunication.Profinet.Toyota;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Windows;

namespace _3DLaserGlueInspection
{
    public interface IHsl
    {
        PlcParam Param { get; set; }
        Dictionary<string, IoAddress> IoDict { get; set; }
        bool IsOpen { get; }
        string ErrMsg { get; }
        bool Load();
        bool Open();
        bool Close();
        bool Read(DI eDI, out string value);
        bool Read(DO eDO, out string value);
        bool Read(DI eDI, out bool value);
        bool Read(DO eDO, out bool value);
        bool Read(DI eDI, out ushort value);
        bool Read(DO eDO, out ushort value);
        bool Write(DO eDO, object value);
        bool Save();
    }

    [Serializable]
    public class PlcParam
    {
        public string IpAddress = "127.0.0.1";
        public int Port = 2000;
        public byte DA2 = 0;
        public HslCommunication.Core.DataFormat DataFormat = new HslCommunication.Core.DataFormat();
        public bool IsStringReverseByteWord = false;
    }
    [Serializable]
    public class IoAddress
    {
        public string IoName;
        public string Address;
    }


    public class MelsecPlc : ISignal, IHsl
    {
        PlcParam param = new PlcParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        MelsecMcNet plc = new MelsecMcNet();
        bool _isOpen = false;
        public bool IsOpen => _isOpen;
        public PlcParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        public MelsecPlc()
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("0293fde5-6e7c-4c76-bacd-e3bdb0ee6187"))
            {
                System.Windows.MessageBox.Show("active failed");
            }
            param.Port = 6000;
            param.DataFormat = plc.ByteTransform.DataFormat;
            param.IsStringReverseByteWord = plc.ByteTransform.IsStringReverseByteWord;
        }

        public bool Close()
        {
            plc.ConnectClose();
            _isOpen = false;
            return true;
        }

        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "MelsecPlcParam.xml";
                if (File.Exists(paramPath))
                {
                    XmlSerializer xml = new XmlSerializer(param.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        PlcParam _ = (PlcParam)xml.Deserialize(stream);
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
                string paramPath = basePath + "MelsecIoParam.xml";
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

                string OpcParamPath = basePath + "MelsecPlcParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "MelsecIoParam.xml";
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
            plc.ConnectTimeOut = 2000;
            plc.NetworkNumber = 0;
            plc.NetworkStationNumber = 0;
            plc.EnableWriteBitToWordRegister = true;
            plc.ByteTransform.DataFormat = param.DataFormat;
            plc.ByteTransform.IsStringReverseByteWord = param.IsStringReverseByteWord;
            plc.IpAddress = param.IpAddress;
            plc.Port = param.Port;

            var result = plc.ConnectServer();
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

        public bool Read(DI eDI, out string value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadString(ioDict[eDI.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadString(ioDict[eDO.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DO eDO, out bool value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DI eDI, out ushort value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public bool Read(DO eDO, out ushort value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public void ShowForm()
        {
            //new HslForm(this, false).ShowDialog();
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                lock (iolock)
                {
                    OperateResult result;
                    if (value is bool)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (bool)value);
                    }
                    else if (value is ushort)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (ushort)value);
                    }
                    else if (value is string)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (string)value);
                    }
                    else
                    {
                        _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
                        return false;
                    }
                    if (result.IsSuccess)
                    {
                        return true;
                    }
                    else
                    {
                        _errMsg = result.Message;
                    }
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            return false;
        }
    }

    public class OmronPlc : ISignal, IHsl
    {
        PlcParam param = new PlcParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        OmronFinsNet plc = new OmronFinsNet();
        bool _isOpen = false;
        public bool IsOpen => _isOpen;
        public PlcParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        public OmronPlc()
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("0293fde5-6e7c-4c76-bacd-e3bdb0ee6187"))
            {
                System.Windows.MessageBox.Show("active failed");
            }
            param.Port = 9600;
            param.DataFormat = plc.ByteTransform.DataFormat;
            param.IsStringReverseByteWord = plc.ByteTransform.IsStringReverseByteWord;
        }

        public bool Close()
        {
            plc.ConnectClose();
            _isOpen = false;
            return true;
        }

        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "OmronPlcParam.xml";
                if (File.Exists(paramPath))
                {
                    XmlSerializer xml = new XmlSerializer(param.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        PlcParam _ = (PlcParam)xml.Deserialize(stream);
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
                string paramPath = basePath + "OmronIoParam.xml";
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

                string OpcParamPath = basePath + "OmronPlcParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "OmronIoParam.xml";
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
            plc.ConnectTimeOut = 2000;
            plc.ByteTransform.DataFormat = param.DataFormat;
            plc.ByteTransform.IsStringReverseByteWord = param.IsStringReverseByteWord;
            plc.IpAddress = param.IpAddress;
            plc.Port = param.Port;
            plc.DA2 = param.DA2;

            var result = plc.ConnectServer();
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

        public bool Read(DI eDI, out string value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadString(ioDict[eDI.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadString(ioDict[eDO.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DO eDO, out bool value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DI eDI, out ushort value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public bool Read(DO eDO, out ushort value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public void ShowForm()
        {
            //new HslForm(this, true).ShowDialog();
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                lock (iolock)
                {
                    OperateResult result;
                    if (value is bool)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (bool)value);
                    }
                    else if (value is ushort)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (ushort)value);
                    }
                    else if (value is string)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (string)value);
                    }
                    else
                    {
                        _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
                        return false;
                    }
                    if (result.IsSuccess)
                    {
                        return true;
                    }
                    else
                    {
                        _errMsg = result.Message;
                    }
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            return false;
        }
    }

    public class ToyotaPlc : ISignal, IHsl
    {
        PlcParam param = new PlcParam();
        Dictionary<string, IoAddress> ioDict = new Dictionary<string, IoAddress>();
        ToyoPuc plc = new ToyoPuc();
        bool _isOpen = false;

        public bool IsOpen => _isOpen;
        public PlcParam Param { get => param; set => param = value; }
        public Dictionary<string, IoAddress> IoDict { get => ioDict; set => ioDict = value; }
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        public ToyotaPlc()
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("0293fde5-6e7c-4c76-bacd-e3bdb0ee6187"))
            {
                System.Windows.MessageBox.Show("active failed");
                
            }
            param.Port = 6000;
            param.DataFormat = plc.ByteTransform.DataFormat;
            param.IsStringReverseByteWord = plc.ByteTransform.IsStringReverseByteWord;
        }

        public bool Close()
        {
            plc.ConnectClose();
            _isOpen = false;
            return true;
        }

        public bool Load()
        {
            bool result = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
            try
            {
                string paramPath = basePath + "ToyotaPlcParam.xml";
                if (File.Exists(paramPath))
                {
                    XmlSerializer xml = new XmlSerializer(param.GetType());
                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                    {
                        PlcParam _ = (PlcParam)xml.Deserialize(stream);
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
                string paramPath = basePath + "ToyotaIoParam.xml";
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

                string OpcParamPath = basePath + "ToyotaPlcParam.xml";
                XmlSerializer xml = new XmlSerializer(param.GetType());
                using (FileStream stream = new FileStream(OpcParamPath, FileMode.Create))
                {
                    xml.Serialize(stream, param);
                }

                List<IoAddress> ios = ioDict.Values.ToList();
                string ioParamPath = basePath + "ToyotaIoParam.xml";
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
            plc.ConnectTimeOut = 2000;
            plc.ByteTransform.DataFormat = param.DataFormat;
            plc.ByteTransform.IsStringReverseByteWord = param.IsStringReverseByteWord;

            plc.IpAddress = param.IpAddress;
            plc.Port = param.Port;

            var result = plc.ConnectServer();
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

        public bool Read(DI eDI, out string value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadString(ioDict[eDI.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DO eDO, out string value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadString(ioDict[eDO.ToString()].Address, 19);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = "";
            return false;
        }

        public bool Read(DI eDI, out bool value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DO eDO, out bool value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadBool(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = false;
            return false;
        }

        public bool Read(DI eDI, out ushort value)
        {
            if (ioDict.ContainsKey(eDI.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDI.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDI.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public bool Read(DO eDO, out ushort value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                var result = plc.ReadUInt16(ioDict[eDO.ToString()].Address);
                if (result.IsSuccess)
                {
                    value = result.Content;
                    return true;
                }
                else
                {
                    _errMsg = result.Message;
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            value = 0;
            return false;
        }

        public void ShowForm()
        {
            //new HslForm(this, false).ShowDialog();
        }

        object iolock = new object();
        public bool Write(DO eDO, object value)
        {
            if (ioDict.ContainsKey(eDO.ToString()))
            {
                lock (iolock)
                {
                    OperateResult result;
                    if (value is bool)
                    {
                        result = Write(ioDict[eDO.ToString()].Address, (bool)value);
                    }
                    else if (value is ushort)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (ushort)value);
                    }
                    else if (value is string)
                    {
                        result = plc.Write(ioDict[eDO.ToString()].Address, (string)value);
                    }
                    else
                    {
                        _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
                        return false;
                    }
                    if (result.IsSuccess)
                    {
                        return true;
                    }
                    else
                    {
                        _errMsg = result.Message;
                    }
                }
            }
            else
            {
                _errMsg = eDO.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.AddressNotAssigned;
            }
            return false;
        }
        OperateResult Write(string address, bool value)
        {
            //D寄存器不支持位写入
            if (address.Contains('.'))//存在位地址
            {
                if (address.ToUpper().Contains("D"))
                {
                    string[] strings = address.Split('.');
                    if (strings.Length == 2)
                    {
                        string io = strings[0];
                        int index = Convert.ToInt32(strings[1], 16);
                        var read = plc.ReadUInt16(io);
                        if (read.IsSuccess)
                        {
                            ushort num;
                            if (value)
                            {
                                num = (ushort)(read.Content | (1 << index));
                            }
                            else
                            {
                                num = (ushort)(read.Content & (ushort.MaxValue - (1 << index)));
                            }
                            System.Threading.Thread.Sleep(2);
                            return plc.Write(io, num);
                        }
                        else
                        {
                            var re = new OperateResult();
                            re.IsSuccess = false;
                            re.Message = read.Message;
                            return re;
                        }
                    }
                }
            }
            return plc.Write(address, value);
        }
    }
}
