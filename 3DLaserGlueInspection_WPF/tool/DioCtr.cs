using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace _3DLaserGlueInspection.tool
{
    public class DioCtr
    {

        CIFX user = new CIFX();
        private object _ioLock;

        public DioCtr()
        {

        }

        bool m_bOpen = false;

        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;


        public virtual bool Open()
        {
            if (m_bOpen) return true;
            try
            {
                int lret = 0;

                string szBoard = "cifX0";
                lret = user.xSysdeviceOpen(szBoard);
                lret = user.xChannelOpen(szBoard, 0);

                if (lret == 0)
                {
                    m_bOpen = true;
                    return true;
                }
                else
                {
                    _errMsg = "控制卡打开失败！";
                    m_bOpen = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _errMsg = "控制卡打开失败！" + ex.ToString();
                return false;
            }
        }

        public virtual bool CloseCard()
        {
            m_bOpen = false;

            try
            {
                int lret = 0;
                string szBoard = "cifX0";

                lret = user.xChannelClose(szBoard, 0);
                lret = user.xSysdeviceClose(szBoard);

                if (lret == 0)
                {
                    return true;
                }
                else
                {
                    _errMsg = "控制卡关闭失败！";
                    return false;
                }
            }
            catch (Exception ex)
            {
                _errMsg = "控制卡关闭失败！" + ex.ToString();
                return false;
            }
        }


        public bool ReadDI(UInt16 pvoffset, ref byte[] pvDataOut)
        {
            int index = 0;
            byte[] pvData = new byte[2];
            UInt32 ulDataLen = 2;
            UInt32 ulAreaNumber = 0;
            while (true)
            {
                int rtn = user.xChannelIORead(ulAreaNumber, pvoffset, ulDataLen, ref pvData);
                if (rtn == 0)
                {
                    pvDataOut = pvData;
                    return true;
                }
                index++;
                if (index > 2000) return false;
            }
        }
        public virtual bool ReadDI(UInt16 pvoffset, int ID, out bool bresult)
        {
            byte[] pvData = new byte[2];
            bresult = false;
            UInt32 ulDataLen = 2;
            UInt32 ulAreaNumber = 0;
            int rtn = -1;
            int index = 0;
            while (true)
            {
                rtn = user.xChannelIORead(ulAreaNumber, pvoffset, ulDataLen, ref pvData);
                if (rtn == 0)
                {
                    // Console.WriteLine("aaaa= " + index);
                    //Data.SaveLogFile("####DI读取成功！"+ index.ToString());
                    Thread.Sleep(1);
                    break;

                }
                index++;
                //Data.SaveLogFile("DI读取次数！" + index.ToString());
                if (index > 600)
                {
                    //Data.SaveLogFile("DI读取失败！");
                    return false;
                }
            }
            int pvindex = 0, IDindex = 0;
            if (ID >= 0 && ID < 8)
            {
                pvindex = 0;
                IDindex = ID;
            }
            else if (ID > 7 && ID < 16)
            {
                pvindex = 1;
                IDindex = ID - 8;
            }
            else
            {
                return false;
            }
            int pvData0 = pvData[pvindex];
            string ss = Convert.ToString(pvData0, 2).PadLeft(8, '0');
            if (ss[8 - IDindex - 1] == '1')
            {
                bresult = true;
            }
            else bresult = false;
            return true;
        }

        public virtual bool ReadDI(UInt16 pvoffset, out bool[] sts)
        {
            sts = new bool[16];
            byte[] pvData = new byte[2];
            bool brtn = ReadDI(pvoffset,ref pvData);
            if (brtn == false) return false;
            for (int i = 0; i < 8; i++)
            {
                byte pvData0 = pvData[0];
                string ss = Convert.ToString(pvData0, 2).PadLeft(8, '0');
                if (ss[8 - i - 1] == '1')
                {
                    sts[i] = true;
                }
                else sts[i] = false;
            }
            for (int i = 0; i < 8; i++)
            {
                byte pvData1 = pvData[1];
                string ss = Convert.ToString(pvData1, 2).PadLeft(8, '0');
                if (ss[8 - i - 1] == '1')
                {
                    sts[i + 8] = true;
                }
                else sts[i + 8] = false;
            }
            return true;
        }

        public bool ReadDO(UInt16 pvoffset, ref byte[] pvDataOut)
        {
            int index = 0;
            byte[] pvData = new byte[2];
            UInt32 ulDataLen = 2;
            UInt32 ulAreaNumber = 0;

            while (true)
            {
                int rtn = user.xChannelIOReadSendData(ulAreaNumber, pvoffset, ulDataLen, ref pvData);
                if (rtn == 0)
                {
                    pvDataOut = pvData;
                    return true;
                }
                index++;
                if (index > 2000) return false;
            }
        }

        public virtual bool ReadDOBit(UInt16 pvoffset, int ID, out bool sts)
        {
            sts = false;
            byte[] pvData = new byte[1];
            UInt16 IDIndex = 0;
            if (ID > 7)
            {
                IDIndex = (UInt16)(ID - 8);
            }
            else
                IDIndex = (UInt16)ID;
            bool rtn = ReadDO(pvoffset,ref pvData);
            if (rtn == false) return false;
            string ss = Convert.ToString(pvData[0], 2).PadLeft(8, '0');
            if (ss[8 - IDIndex - 1] == '1')
            {
                sts = true;
            }
            else sts = false;
            return true;
        }

        public virtual bool WriteBit(UInt16 pvoffset, int ID, bool sts)
        {
            if (!m_bOpen) return false;
            byte[] pvData = new byte[2];
            byte pvData2 = new byte();
            int ID2 = 0;
            UInt16 pvoffsetWrite = pvoffset;
            bool rtn = ReadDO(pvoffsetWrite, ref pvData);
            if (rtn == false) return false;
            if (ID < 0) return false;
            else if (ID > 15) return false;
            else if (ID < 8)
            {
                pvData2 = pvData[0];
                ID2 = ID;
            }
            else
            {
                pvData2 = pvData[1];
                ID2 = ID - 8;
                pvoffsetWrite += 1;
            }
            string ss = Convert.ToString(pvData2, 2).PadLeft(8, '0');
            string strSts;
            if (sts) strSts = ss.Remove(8 - ID2 - 1) + '1' + ss.Substring(8 - ID2);
            else strSts = ss.Remove(8 - ID2 - 1) + '0' + ss.Substring(8 - ID2);
            int nsts = Convert.ToInt32(strSts, 2);
            byte[] bySts = new Byte[1] { Convert.ToByte(nsts) };
            int index = 0;
            int ltn = -1;

            lock (_ioLock)
            {
                while (true)
                {
                    ltn = user.xChannelIOWrite(0, pvoffsetWrite, 1, ref bySts);
                    if (ltn == 0) break;
                    index++;
                    if (index > 1500) return false;
                }
            }
            //int ltn = user.xChannelIOWrite(0, pvoffset, 1, ref bySts);
            if (ltn == 0) return true;
            else return false;
        }

    }
}
