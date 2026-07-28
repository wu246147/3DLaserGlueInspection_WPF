using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DLaserGlueInspection
{
    public interface ISignal
    {
        string ErrMsg { get; }
        bool IsOpen { get; }
        void ShowForm();
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

    public enum DO
    {
        //bool 0

        /// <summary>
        /// 心跳信号
        /// </summary>
        Alive = 0,

        /// <summary>
        /// 准备好信号
        /// </summary>
        Ready,

        /// <summary>
        /// 运行信号
        /// </summary>
        Running,

        /// <summary>
        /// 拍照中信号
        /// </summary>
        Triggering,

        /// <summary>
        /// 检测结果
        /// </summary>
        Result,

        /// <summary>
        /// 结果可读信号
        /// </summary>
        IsRead,

    }

    public enum DI
    {
        //bool 0

        /// <summary>
        /// 开始信号
        /// </summary>
        Start = 0,

        /// <summary>
        /// 中断信号
        /// </summary>
        Abort,

        /// <summary>
        /// 触发信号
        /// </summary>
        PGON,

        /// <summary>
        /// 结束信号
        /// </summary>
        END,

        //ushort 256

        /// <summary>
        /// 车型号
        /// </summary>
        CarNumber = 256,

        //int 1024

        //float 2048

        //double 4096

        //string 8192

        /// <summary>
        /// 车架号
        /// </summary>
        InVIN = 8192,

    }

    public enum IONameEnum
    {
        OPC,

        S7,

        TCP,

        SharedMemory,

        OmronPlc,

        ToyotaPlc,

        MelsecPlc,
    }


    public class Mmf : ISignal
    {
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        Dictionary<string, MemoryMappedViewAccessor> io = new Dictionary<string, MemoryMappedViewAccessor>();
        public Dictionary<string, MemoryMappedViewAccessor> IO { get { return io; } }

        public bool IsOpen => io.Count > 0;

        Dictionary<string, MemoryMappedFile> memoryMappedFile = new Dictionary<string, MemoryMappedFile>();
        public Mmf()
        {

        }
        bool Ini()
        {
            io.Clear();
            memoryMappedFile.Clear();
            string[] vs = Enum.GetNames(typeof(DI));
            foreach (string item in vs)
            {
                Add(item);
            }
            string[] vs2 = Enum.GetNames(typeof(DO));
            foreach (string item in vs2)
            {
                Add(item);
            }
            return true;
        }

        void Add(string name)
        {
            long capacity = 256;

            //创建或者打开共享内存
            MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen(name, capacity, MemoryMappedFileAccess.ReadWrite);
            memoryMappedFile.Add(name, mmf);

            //通过MemoryMappedFile的CreateViewAccssor方法获得共享内存的访问器
            io.Add(name, mmf.CreateViewAccessor(0, capacity));
        }

        public bool ReadBooolean(DO dO)
        {
            return this.IO[(dO).ToString()].ReadBoolean(0);
        }
        public bool ReadBooolean(DI di)
        {
            return this.IO[(di).ToString()].ReadBoolean(0);
        }
        public void Write(DO dO, bool value)
        {
            this.IO[(dO).ToString()].Write(0, value);
        }
        public void Write(DI di, bool value)
        {
            this.IO[(di).ToString()].Write(0, value);
        }

        public ushort ReadUInt16(DO dO)
        {
            return this.IO[(dO).ToString()].ReadUInt16(0);
        }
        public ushort ReadUInt16(DI di)
        {
            return this.IO[(di).ToString()].ReadUInt16(0);
        }
        public void Write(DO dO, ushort value)
        {
            this.IO[(dO).ToString()].Write(0, value);
        }
        public void Write(DI di, ushort value)
        {
            this.IO[(di).ToString()].Write(0, value);
        }

        public int ReadInt32(DO dO)
        {
            return this.IO[(dO).ToString()].ReadInt32(0);
        }
        public int ReadInt32(DI di)
        {
            return this.IO[(di).ToString()].ReadInt32(0);
        }
        public void Write(DO dO, int value)
        {
            this.IO[(dO).ToString()].Write(0, value);
        }
        public void Write(DI di, int value)
        {
            this.IO[(di).ToString()].Write(0, value);
        }

        public char ReadChar(DO dO)
        {
            return this.IO[(dO).ToString()].ReadChar(0);
        }
        public char ReadChar(DI di)
        {
            return this.IO[(di).ToString()].ReadChar(0);
        }
        public void Write(DO dO, char value)
        {
            this.IO[(dO).ToString()].Write(0, value);
        }
        public void Write(DI di, char value)
        {
            this.IO[(di).ToString()].Write(0, value);
        }

        public string ReadString(DO dO)
        {
            int Length = this.IO[(dO).ToString()].ReadInt32(0);
            char[] array = new char[Length];
            if (Length > 0)
            {
                this.IO[(dO).ToString()].ReadArray<char>(sizeof(int), array, 0, array.Length);
                return new string(array);
            }
            return "";
        }
        public string ReadString(DI di)
        {
            int Length = this.IO[(di).ToString()].ReadInt32(0);
            char[] array = new char[Length];
            if (Length > 0)
            {
                this.IO[(di).ToString()].ReadArray<char>(sizeof(int), array, 0, array.Length);
                return new string(array);
            }
            return "";
        }
        public void Write(DO dO, string value)
        {
            var array = value.ToCharArray();
            this.IO[(dO).ToString()].Write(0, array.Length);
            if (array.Length > 0)
                this.IO[(dO).ToString()].WriteArray<char>(sizeof(int), array, 0, array.Length);
        }
        public void Write(DI di, string value)
        {
            var array = value.ToCharArray();
            this.IO[(di).ToString()].Write(0, array.Length);
            if (array.Length > 0)
                this.IO[(di).ToString()].WriteArray<char>(sizeof(int), array, 0, array.Length);
        }

        public void ShowForm()
        {
            //System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.该通讯方式无设置界面！"));
        }

        public bool Load()
        {
            return true;
        }

        public bool Open()
        {
            Ini();
            return true;
        }

        public bool Close()
        {
            {
                string[] keys = io.Keys.ToArray();
                foreach (string key in keys)
                {
                    io[key].Dispose();
                }
                io.Clear();
            }
            {
                string[] keys = memoryMappedFile.Keys.ToArray();
                foreach (string key in keys)
                {
                    memoryMappedFile[key].Dispose();
                }
                memoryMappedFile.Clear();
            }
            return true;
        }

        public bool Read(DI eDI, out string value)
        {
            value = ReadString(eDI);
            return true;
        }

        public bool Read(DO eDO, out string value)
        {
            value = ReadString(eDO);
            return true;
        }

        public bool Read(DI eDI, out bool value)
        {
            value = ReadBooolean(eDI);
            return true;
        }

        public bool Read(DO eDO, out bool value)
        {
            value = ReadBooolean(eDO);
            return true;
        }

        public bool Read(DI eDI, out ushort value)
        {
            value = ReadUInt16(eDI);
            return true;
        }

        public bool Read(DO eDO, out ushort value)
        {
            value = ReadUInt16(eDO);
            return true;
        }

        public bool Write(DO eDO, object value)
        {
            if (value is bool)
            {
                Write(eDO, (bool)value);
            }
            else if (value is ushort)
            {
                Write(eDO, (ushort)value);
            }
            else if (value is string)
            {
                Write(eDO, (string)value);
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.WritingFormatNotSupported;
                return false;
            }
            return true;
        }

        public bool Save()
        {
            return true;
        }
    }
}
