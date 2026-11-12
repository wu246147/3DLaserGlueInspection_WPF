using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace _3DLaserGlueInspection.tool
{
    /// <summary>
    /// 封装 cifX 本机驱动的设备、通道生命周期及过程数据读写。
    /// 所有实例共用静态驱动和通道句柄，封装调用通过同一把锁串行访问，
    /// 避免心跳、主流程和关卡操作同时访问这些句柄。
    /// </summary>
    /// <remarks>
    /// 调用方负责按现有顺序打开和关闭设备，且读写期间必须保证缓冲区有效。
    /// 各方法返回本次调用的局部结果；兼容字段 lReturn 仅表示最近一次记录，
    /// 并发调用方不得将其作为自己的调用结果。直接调用本机导入函数不受此锁保护。
    /// </remarks>
    class CIFX
    {
        /// <summary>
        /// 保护所有实例共用的本机句柄；只覆盖单次驱动调用，不覆盖上层重试休眠。
        /// </summary>
        private static readonly object DriverSync = new object();


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


        /// <summary>所有封装实例共用的驱动句柄，由打开与关闭方法在 DriverSync 内维护。</summary>
        private static IntPtr _hDriver;

        /// <summary>当前驱动句柄的兼容只读视图；调用方不得在驱动关闭后继续使用取得的值。</summary>
        public static IntPtr hDriver
        { get { return _hDriver; } }


        /// <summary>所有实例共用的过程数据通道句柄，封装内的本机访问由 DriverSync 保护。</summary>
        private static IntPtr _hChannel;

        /// <summary>
        /// 当前通道句柄的兼容访问入口；外部替换须在停止全部通信访问后执行，
        /// 不得通过此入口绕过通道打开和关闭的生命周期管理。
        /// </summary>
        public static IntPtr hChannel
        {
            get { return _hChannel; }
            set { _hChannel = value; }
        }

        /// <summary>当前系统设备共享句柄，由设备打开与关闭方法负责维护。</summary>
        private static IntPtr _hSysdevice;

        /// <summary>系统设备句柄的兼容只读视图；调用方不得在设备关闭后继续使用取得的值。</summary>
        public static IntPtr hSysDevice
        { get { return _hSysdevice; } }

        /// <summary>
        /// 最近一次驱动调用的兼容状态快照；调用结果必须以各方法的返回值为准。
        /// </summary>
        public int lReturn = 0;

        /// <summary>
        /// 打开驱动并保存共享驱动句柄，与其他封装调用互斥执行。
        /// </summary>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时显示原有提示并返回 99。</returns>
        /// <remarks>成功后由调用方配对调用 xDriverClose；本方法不检查重复打开。</remarks>
        public int xDriverOpen()
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xDriverOpen(ref _hDriver);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                System.Windows.Forms.MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 使用共享驱动句柄打开指定通道，保存供后续过程数据读写使用的共享句柄。
        /// </summary>
        /// <param name="szBoard">驱动识别的板卡名称，例如 cifX0；由驱动校验名称有效性。</param>
        /// <param name="iChannel">从 0 开始的通道号，调用方应传入板卡支持的非负编号。</param>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>与其他封装调用互斥；调用方负责先打开驱动，并在使用结束后关闭通道。</remarks>
        public int xChannelOpen(string szBoard, int iChannel)
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xChannelOpen(hDriver, szBoard, (UInt32)iChannel, ref _hChannel);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 打开板卡的系统设备并保存共享句柄，与通道读写和关闭操作互斥。
        /// </summary>
        /// <param name="szBoard">驱动识别的板卡名称，例如 cifX0；由驱动校验名称有效性。</param>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>调用方须先打开驱动，并在使用结束后配对调用 xSysdeviceClose。</remarks>
        public int xSysdeviceOpen(string szBoard)
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xSysdeviceOpen(_hDriver, szBoard, ref _hSysdevice);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 关闭非空共享驱动句柄，并按原有行为清空句柄，避免与封装读写交错。
        /// </summary>
        /// <returns>句柄为空时返回 0，否则返回本次关闭结果；捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>调用方须先停止上层任务并关闭通道和系统设备；本方法不会代为停止任务。</remarks>
        public int xDriverClose()
        {
            int result = 0;
            try
            {
                lock (DriverSync)
                {
                    if (_hDriver != IntPtr.Zero)
                    {
                        result = _xDriverClose(_hDriver);
                        _hDriver = IntPtr.Zero;
                    }
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                result = 99;
                lReturn = result;
                System.Windows.Forms.MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 关闭当前非空共享通道句柄，并按原有行为清空句柄，与过程数据访问互斥。
        /// </summary>
        /// <param name="szBoard">保留的兼容参数；实际关闭已保存的通道，不按此名称重新查找。</param>
        /// <param name="iChannel">保留的兼容通道号；实际关闭已保存的共享通道句柄。</param>
        /// <returns>句柄为空时返回 0，否则返回本次关闭结果；捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>调用方应先停止心跳等访问任务；本方法只与正在执行的单次封装调用互斥。</remarks>
        public int xChannelClose(string szBoard, int iChannel)
        {
            int result = 0;
            try
            {
                lock (DriverSync)
                {
                    if (_hChannel != IntPtr.Zero)
                    {
                        result = _xChannelClose(_hChannel);
                        _hChannel = IntPtr.Zero;
                    }
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                result = 99;
                lReturn = result;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 关闭非空共享系统设备句柄，并按原有行为清空句柄，与其他封装调用互斥。
        /// </summary>
        /// <param name="szBoard">保留的兼容参数；实际关闭已保存的系统设备句柄。</param>
        /// <returns>句柄为空时返回 0，否则返回本次关闭结果；捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>调用方负责先结束设备访问，并在需要时继续关闭驱动。</remarks>
        public int xSysdeviceClose(string szBoard)
        {
            int result = 0;
            try
            {
                lock (DriverSync)
                {
                    if (_hSysdevice != IntPtr.Zero)
                    {
                        result = _xSysdeviceClose(_hSysdevice);
                        _hSysdevice = IntPtr.Zero;
                    }
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                result = 99;
                lReturn = result;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }


        /// <summary>
        /// 向当前共享通道写入一次输出过程数据，使用局部变量保留本次驱动返回码。
        /// </summary>
        /// <param name="ulAreaNumber">输出过程数据区编号，由板卡配置决定。</param>
        /// <param name="ulOffset">相对于所选数据区起点的字节偏移。</param>
        /// <param name="ulDataLen">写入字节数，不得超过缓冲区长度和所选数据区范围。</param>
        /// <param name="pvData">待写入的非空字节数组；调用方保留所有权，调用期间不得修改。</param>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>保持驱动超时参数为 0；只锁定本次访问，上层重试和休眠不占用此锁。</remarks>
        public int xChannelIOWrite(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xChannelIOWrite(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData, 0);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 从当前共享通道读取一次输入过程数据，避免心跳写入覆盖本次读取返回码。
        /// </summary>
        /// <param name="ulAreaNumber">输入过程数据区编号，由板卡配置决定。</param>
        /// <param name="ulOffset">相对于所选数据区起点的字节偏移。</param>
        /// <param name="ulDataLen">读取字节数，不得超过缓冲区长度和所选数据区范围。</param>
        /// <param name="pvData">接收数据的非空字节数组；调用方保留所有权，调用期间不得并发访问。</param>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>保持驱动超时参数为 0；只锁定本次访问，不在锁内执行上层失败重试。</remarks>
        public int xChannelIORead(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xChannelIORead(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData, 0);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }

        /// <summary>
        /// 回读当前共享通道最近发送的输出数据，与输入读取、输出写入和关卡操作互斥。
        /// </summary>
        /// <param name="ulAreaNumber">输出过程数据区编号，由板卡配置决定。</param>
        /// <param name="ulOffset">相对于所选数据区起点的字节偏移。</param>
        /// <param name="ulDataLen">回读字节数，不得超过缓冲区长度和所选数据区范围。</param>
        /// <param name="pvData">接收输出回读数据的非空数组；调用方保留所有权，调用期间不得并发访问。</param>
        /// <returns>本次驱动返回码；0 表示成功，捕获到托管异常时提示并返回 99。</returns>
        /// <remarks>只执行一次驱动调用；重试策略由上层负责，不读取共享状态快照作为结果。</remarks>
        public int xChannelIOReadSendData(UInt32 ulAreaNumber, UInt32 ulOffset, UInt32 ulDataLen, ref byte[] pvData)
        {
            int result = 99;
            try
            {
                lock (DriverSync)
                {
                    result = _xChannelIOReadSendData(_hChannel, ulAreaNumber, ulOffset, ulDataLen, pvData);
                    lReturn = result;
                }
            }
            catch (Exception ex)
            {
                lReturn = 99;
                MessageBox.Show(ex.Message.ToString());
            }
            return result;
        }


    }
}
