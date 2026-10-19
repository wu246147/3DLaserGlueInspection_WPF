using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace _3DLaserGlueInspection.tool
{
    class CIFX
    {


        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xDriverOpen")]
        private static extern Int32 _xDriverOpen(ref IntPtr hDriver);


        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xSysdeviceOpen")]
        private static extern Int32 _xSysdeviceOpen(IntPtr hDriver, string szBoard, ref IntPtr phSysdevice);

        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xChannelOpen")]
        private static extern Int32 _xChannelOpen(IntPtr hChannel, [MarshalAs(UnmanagedType.LPStr)] string szBoard, UInt32 ulChannel, ref IntPtr phChannel);


        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xDriverClose")]
        public static extern Int32 _xDriverClose(IntPtr hDriver);

        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xSysdeviceClose")]
        public static extern Int32 _xSysdeviceClose(IntPtr hSysdevice);

        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xChannelClose")]
        public static extern Int32 _xChannelClose(IntPtr hChannel);




        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xChannelIOWrite")]
        private static extern Int32 _xChannelIOWrite(IntPtr hChannel, UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pvData, UInt32 ulTimeout);


        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xChannelIORead")]
        private static extern Int32 _xChannelIORead(IntPtr hChannel, UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pvData, UInt32 ulTimeout);

        [DllImport("dll\\cifX32dll64.dll", EntryPoint = "xChannelIOReadSendData")]
        private static extern Int32 _xChannelIOReadSendData(IntPtr hChannel, UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pvData);


        private static IntPtr _hDriver;
        public static IntPtr hDriver
        { get { return _hDriver; } }


        private static IntPtr _hChannel;
        public static IntPtr hChannel
        {
            get { return _hChannel; }
            set { _hChannel = value; }
        }

        private static IntPtr _hSysdevice;
        public static IntPtr hSysDevice
        { get { return _hSysdevice; } }

        public int lReturn = 0;
        public int xDriverOpen()
        {
            try
            {
                lReturn = _xDriverOpen(ref _hDriver);
                return lReturn;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message.ToString());
                return 99;
            }
        }
        public int xChannelOpen(string szBoard, int iChannel)
        {
            try
            {
                lReturn = _xChannelOpen(hDriver,
                                            szBoard,
                                            (UInt32)iChannel,
                                            ref _hChannel);
                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }
        public int xSysdeviceOpen(string szBoard)
        {
            try
            {
                lReturn = _xSysdeviceOpen(_hDriver, szBoard, ref _hSysdevice);
                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }

        public int xDriverClose()
        {
            try
            {
                lReturn = 0;
                if (_hDriver != IntPtr.Zero)
                {
                    lReturn = _xDriverClose(_hDriver);
                    _hDriver = IntPtr.Zero;
                }

                return lReturn;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message.ToString());
                return 99;
            }
        }
        public int xChannelClose(string szBoard, int iChannel)
        {
            try
            {
                lReturn = 0;
                if (_hChannel != IntPtr.Zero)
                {
                    lReturn = _xChannelClose(_hChannel);
                    _hChannel = IntPtr.Zero;
                }


                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }
        public int xSysdeviceClose(string szBoard)
        {
            try
            {
                lReturn = 0;
                if (_hSysdevice != IntPtr.Zero)
                {
                    lReturn = _xSysdeviceClose(_hSysdevice);
                    _hSysdevice = IntPtr.Zero;
                }

                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }


        public int xChannelIOWrite(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            try
            {
                lReturn = _xChannelIOWrite(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData, 0);
                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }
        public int xChannelIORead(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            try
            {
                lReturn = _xChannelIORead(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData, 0);
                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }

        public int xChannelIOReadSendData(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            try
            {
                lReturn = _xChannelIOReadSendData(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData);
                return lReturn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return lReturn;
            }
        }


    }
}
