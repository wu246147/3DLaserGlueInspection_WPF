using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml.Serialization;
using System.Drawing;
//using System.Windows.Forms;
using System.Xml.Linq;
using System.Globalization;
//using HalconDotNet;
using System.Threading;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography;
using OpenCvSharp;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection
{

    public class Cam : MvCamera
    {
        public string ErrMsg => _errMsg;
        string _errMsg;
        public bool IsOpen => _isOpen;
        bool _isOpen;
        public bool IsGrabbing => _isGrabbing;
        bool _isGrabbing;
        public string Name => _name;
        string _name;
        public string SN => _SN;
        string _SN;

        public string ManufacturerName => _manufacturerName;
        string _manufacturerName;

        bool _ReverseX;


        /// <summary>
        /// 搜素相机及其信息
        /// </summary>
        /// <param name="names"> 相机名称 </param>
        /// <param name="SNs"> 相机ID </param>
        /// <param name="ManufacturerNames"> 相机厂家 </param>
        /// <param name="DeviceList"> 相机ip信息 </param>
        /// <returns></returns>
        public bool Find(out string[] names, out string[] SNs, out string[] ManufacturerNames, out MV_CC_DEVICE_INFO[] DeviceList)
        {
            MV_CC_DEVICE_INFO_LIST m_pDeviceList = new MV_CC_DEVICE_INFO_LIST();
            int nRet = MvCamera.MV_CC_EnumDevices_NET(MvCamera.MV_GIGE_DEVICE | MvCamera.MV_USB_DEVICE, ref m_pDeviceList);
            if (MvCamera.MV_OK != nRet)
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("寻找相机失败：") + Convert.ToString(nRet, 16);
                names = null;
                SNs = null;
                DeviceList = null;
                ManufacturerNames = null;
                return false;
            }
            names = new string[m_pDeviceList.nDeviceNum];
            SNs = new string[m_pDeviceList.nDeviceNum];
            DeviceList = new MV_CC_DEVICE_INFO[m_pDeviceList.nDeviceNum];
            ManufacturerNames = new string[m_pDeviceList.nDeviceNum];
            for (int i = 0; i < m_pDeviceList.nDeviceNum; i++)
            {
                MvCamera.MV_CC_DEVICE_INFO device = (MvCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_pDeviceList.pDeviceInfo[i], typeof(MvCamera.MV_CC_DEVICE_INFO));
                if (device.nTLayerType == MvCamera.MV_GIGE_DEVICE)
                {
                    IntPtr buffer = Marshal.UnsafeAddrOfPinnedArrayElement(device.SpecialInfo.stGigEInfo, 0);
                    MvCamera.MV_GIGE_DEVICE_INFO gigeInfo = (MvCamera.MV_GIGE_DEVICE_INFO)Marshal.PtrToStructure(buffer, typeof(MvCamera.MV_GIGE_DEVICE_INFO));
                    names[i] = gigeInfo.chUserDefinedName;
                    SNs[i] = gigeInfo.chSerialNumber;
                    ManufacturerNames[i] = gigeInfo.chManufacturerName;
                    DeviceList[i] = device;
                }
                else if (device.nTLayerType == MvCamera.MV_USB_DEVICE)
                {
                    IntPtr buffer = Marshal.UnsafeAddrOfPinnedArrayElement(device.SpecialInfo.stUsb3VInfo, 0);
                    MvCamera.MV_USB3_DEVICE_INFO usbInfo = (MvCamera.MV_USB3_DEVICE_INFO)Marshal.PtrToStructure(buffer, typeof(MvCamera.MV_USB3_DEVICE_INFO));
                    names[i] = usbInfo.chUserDefinedName;
                    SNs[i] = usbInfo.chSerialNumber;
                    ManufacturerNames[i] = usbInfo.chManufacturerName;
                    DeviceList[i] = device;
                }
            }
            return true;
        }

        /// <summary>
        /// 打开相机，打开第一个
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            if (Find(out string[] names, out string[] SNs, out string[] ManufacturerNames, out MV_CC_DEVICE_INFO[] DeviceList))
            {
                if (DeviceList.Length > 0)
                {
                    _name = names[0];
                    _SN = SNs[0];
                    _manufacturerName = ManufacturerNames[0];
                    return Open(DeviceList[0]);
                }
                else
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("未找到任何相机");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 打开相机，根据相机名称打开
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool OpenByName(string name)
        {
            if (Find(out string[] names, out string[] SNs, out string[] ManufacturerNames, out MV_CC_DEVICE_INFO[] DeviceList))
            {
                if (DeviceList.Length > 0)
                {
                    for (int i = 0; i < DeviceList.Length; i++)
                    {
                        if (names[i] == name)
                        {
                            _name = names[i];
                            _SN = SNs[i];
                            _manufacturerName = ManufacturerNames[i];
                            return Open(DeviceList[i]);
                        }
                    }
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("未找到相机") + name;
                    return false;
                }
                else
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("未找到任何相机");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 打开相机，根据相机ID打开
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        public bool OpenBySN(string sn)
        {
            if (Find(out string[] names, out string[] SNs, out string[] ManufacturerNames, out MV_CC_DEVICE_INFO[] DeviceList))
            {
                if (DeviceList.Length > 0)
                {
                    for (int i = 0; i < DeviceList.Length; i++)
                    {
                        if (SNs[i] == sn)
                        {
                            _name = names[i];
                            _SN = SNs[i];
                            _manufacturerName = ManufacturerNames[i];
                            return Open(DeviceList[i]);
                        }
                    }
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("未找到相机") + sn;
                    return false;
                }
                else
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("未找到任何相机");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 打开相机，根据相机IP打开
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private bool Open(MV_CC_DEVICE_INFO device)
        {
            int nRet = MV_CC_CreateDevice_NET(ref device);
            if (MvCamera.MV_OK != nRet)
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机创建失败:") + Convert.ToString(nRet, 16);
                return false;
            }

            // ch:打开设备 | en:Open device
            nRet = MV_CC_OpenDevice_NET();
            if (MvCamera.MV_OK != nRet)
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机打开失败:") + Convert.ToString(nRet, 16);
                return false;
            }

            //// ch:注册回调函数 | en:Register image callback
            //var ImageCallback = new MvCamera.cbOutputExdelegate(ImageCallbackFunc);
            //nRet = MV_CC_RegisterImageCallBackEx_NET(ImageCallback, IntPtr.Zero);
            //if (MvCamera.MV_OK != nRet)
            //{
            //    _errMsg = "注册回调失败:" + Convert.ToString(nRet, 16);
            //    return false;
            //}
            _isOpen = true;
            return true;
        }

        /// <summary>
        /// 关闭相机
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            // ch:停止抓图 || en:Stop grab image
            StopGrabbing();
            // ch:关闭设备 || en: Close device
            int nRet = MV_CC_CloseDevice_NET();
            if (MvCamera.MV_OK == nRet)
            {
                _isOpen = false;
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("关闭相机失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }

        /// <summary>
        /// 相机开始取像
        /// </summary>
        /// <returns></returns>
        public bool StartGrabbing()
        {
            int nRet = MV_CC_StartGrabbing_NET();
            if (MvCamera.MV_OK == nRet)
            {
                _isGrabbing = true;
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("开始采集失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }

        /// <summary>
        /// 相机停止取像
        /// </summary>
        /// <returns></returns>
        public bool StopGrabbing()
        {
            int nRet = MV_CC_StopGrabbing_NET();
            if (MvCamera.MV_OK == nRet)
            {
                _isGrabbing = false;
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("停止采集失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }

        /// <summary>
        /// 召回函数
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pFrameInfo"></param>
        /// <param name="pUser"></param>
        void ImageCallbackFunc(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
        {
            ToMImage(pData, pFrameInfo, out Mat mImage);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pFrameInfo"></param>
        /// <param name="mImage"></param>
        /// <returns></returns>
        private bool ToMImage(IntPtr pData, MV_FRAME_OUT_INFO_EX pFrameInfo, out Mat mImage)
        {
            if (IsColorPixelFormat(pFrameInfo.enPixelType))
            {
                IntPtr pTemp = IntPtr.Zero;
                if (pFrameInfo.enPixelType == MvGvspPixelType.PixelType_Gvsp_RGB8_Packed)
                {
                    pTemp = pData;
                }
                else
                {
                    int nImageBufSize = pFrameInfo.nWidth * pFrameInfo.nHeight * 3;
                    IntPtr pImageBuf = Marshal.AllocHGlobal(nImageBufSize);

                    MV_PIXEL_CONVERT_PARAM stPixelConvertParam = new MV_PIXEL_CONVERT_PARAM();

                    stPixelConvertParam.pSrcData = pData;//源数据
                    stPixelConvertParam.nWidth = pFrameInfo.nWidth;//图像宽度
                    stPixelConvertParam.nHeight = pFrameInfo.nHeight;//图像高度
                    stPixelConvertParam.enSrcPixelType = pFrameInfo.enPixelType;//源数据的格式
                    stPixelConvertParam.nSrcDataLen = pFrameInfo.nFrameLen;

                    stPixelConvertParam.nDstBufferSize = (uint)nImageBufSize;
                    stPixelConvertParam.pDstBuffer = pImageBuf;//转换后的数据
                    stPixelConvertParam.enDstPixelType = MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
                    int nRet = MV_CC_ConvertPixelType_NET(ref stPixelConvertParam);//格式转换
                    if (MvCamera.MV_OK != nRet)
                    {
                        _errMsg = GlobalVarAndFunc.LanguageTranslate("格式转换失败");
                        mImage = null;
                        return false;
                    }
                    pTemp = pImageBuf;
                }

                try
                {
                    mImage = Mat.FromPixelData(pFrameInfo.nHeight, pFrameInfo.nWidth, MatType.CV_8UC3, pTemp);

                    return true;
                }
                catch (Exception ex)
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("格式转换创建失败:") + ex.ToString();
                    mImage = null;
                    return false;
                }
            }
            else if (IsMonoPixelFormat(pFrameInfo.enPixelType))
            {
                IntPtr pTemp = IntPtr.Zero;
                if (pFrameInfo.enPixelType == MvGvspPixelType.PixelType_Gvsp_Mono8)
                {
                    pTemp = pData;
                }
                else
                {
                    int nImageBufSize = pFrameInfo.nWidth * pFrameInfo.nHeight;
                    IntPtr pImageBuf = Marshal.AllocHGlobal(nImageBufSize);

                    MV_PIXEL_CONVERT_PARAM stPixelConvertParam = new MV_PIXEL_CONVERT_PARAM();

                    stPixelConvertParam.pSrcData = pData;//源数据
                    stPixelConvertParam.nWidth = pFrameInfo.nWidth;//图像宽度
                    stPixelConvertParam.nHeight = pFrameInfo.nHeight;//图像高度
                    stPixelConvertParam.enSrcPixelType = pFrameInfo.enPixelType;//源数据的格式
                    stPixelConvertParam.nSrcDataLen = pFrameInfo.nFrameLen;

                    stPixelConvertParam.nDstBufferSize = (uint)nImageBufSize;
                    stPixelConvertParam.pDstBuffer = pImageBuf;//转换后的数据
                    stPixelConvertParam.enDstPixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;
                    int nRet = MV_CC_ConvertPixelType_NET(ref stPixelConvertParam);//格式转换
                    if (MvCamera.MV_OK != nRet)
                    {
                        _errMsg = GlobalVarAndFunc.LanguageTranslate("格式转换失败");
                        mImage = null;
                        return false;
                    }
                    pTemp = pImageBuf;
                }
                try
                {
                    //mImage = new Mat("byte", pFrameInfo.nWidth, pFrameInfo.nHeight, pTemp);
                    mImage = Mat.FromPixelData(pFrameInfo.nHeight, pFrameInfo.nWidth, MatType.CV_8UC1, pTemp);

                    //Mat mImage = new Mat();
                    //mImage.GenImage1Extern("byte", pFrameInfo.nWidth, pFrameInfo.nHeight, pTemp, IntPtr.Zero);
                    return true;
                }
                catch (Exception ex)
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("格式转换创建失败:") + ex.ToString();
                    mImage = null;
                    return false;
                }
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("未知格式:") + pFrameInfo.enPixelType;
                mImage = null;
                return false;
            }

        }


        /// <summary>
        /// 单帧取像
        /// </summary>
        /// <param name="mImage"></param>
        /// <returns></returns>
        public bool OneShot(out Mat mImage)
        {
            mImage = null;
            if (IsOpen)
            {
                if (!IsGrabbing)
                {
                    if (!StartGrabbing())
                    {
                        return false;
                    }
                }
                //while (IsGrabbing)
                {
                    MV_FRAME_OUT stFrameOut = new MV_FRAME_OUT();
                    int nRet = MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);
                    StopGrabbing();
                    if (MvCamera.MV_OK == nRet)
                    {
                        bool bflag = ToMImage(stFrameOut.pBufAddr, stFrameOut.stFrameInfo, out mImage);
                        if (_manufacturerName == "ChinaVision" && _ReverseX)
                        {
                            //HObject mImage_mirror;
                            //HalconDotNet.HOperatorSet.MirrorImage(mImage, out mImage_mirror, "column");
                            //mImage = new Mat(mImage_mirror);
                            Mat mImageFlip = new Mat();
                            Cv2.Flip(mImage, mImageFlip, 0);
                            mImage = mImageFlip;
                        }

                        MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                        return bflag;
                    }
                    else
                    {
                        _errMsg = GlobalVarAndFunc.LanguageTranslate("采集失败：") + Convert.ToString(nRet, 16);
                        return false;
                    }
                }
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机未打开");
                return false;
            }
        }

        /// <summary>
        /// 单帧取像
        /// </summary>
        /// <param name="mImage"></param>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="right"></param>
        /// <param name="down"></param>
        /// <param name="outGray"></param>
        /// <returns></returns>
        public bool OneShot(out Mat mImage, double left, double top, double right, double down, out double outGray)
        {
            mImage = null;
            outGray = -1;
            if (IsOpen)
            {
                if (!IsGrabbing)
                {
                    if (!StartGrabbing())
                    {
                        return false;
                    }
                }
                //while (IsGrabbing)
                {
                    MV_FRAME_OUT stFrameOut = new MV_FRAME_OUT();
                    int nRet = MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);
                    StopGrabbing();
                    if (MvCamera.MV_OK == nRet)
                    {
                        bool bflag = ToMImage(stFrameOut.pBufAddr, stFrameOut.stFrameInfo, out mImage);
                        MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                        if (bflag)
                        {
                            double col1 = stFrameOut.stFrameInfo.nWidth * left;
                            double col2 = stFrameOut.stFrameInfo.nWidth * right;
                            double row1 = stFrameOut.stFrameInfo.nHeight * top;
                            double row2 = stFrameOut.stFrameInfo.nHeight * down;
                            //outGray = mImage.Intensity(new HRegion(row1, col1, row2, col2), out double _);
                            Rect rect = new Rect((int)col1, (int)row1, (int)(col2 - col1), (int)(row2 - row1));
                            Mat cutImage = new Mat(mImage, rect);
                            Scalar mean = Cv2.Mean(cutImage);
                            outGray = mean.Val0;


                        }
                        return bflag;
                    }
                    else
                    {
                        _errMsg = GlobalVarAndFunc.LanguageTranslate("采集失败：") + Convert.ToString(nRet, 16);
                        return false;
                    }
                }
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机未打开");
                return false;
            }
        }
        /// <summary>
        /// 单帧取像
        /// </summary>
        /// <param name="mImage"></param>
        /// <param name="countMax"></param>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="right"></param>
        /// <param name="down"></param>
        /// <param name="grayMin"></param>
        /// <param name="grayMax"></param>
        /// <param name="outGray"></param>
        /// <returns></returns>
        public bool OneShotByGray(out Mat mImage, int countMax, double left, double top, double right, double down, byte grayMin, byte grayMax, out double outGray)
        {
            mImage = null;
            outGray = -1;
            if (IsOpen)
            {
                bool bRun = true;
                int count = 0;
                MVCC_FLOATVALUE pstValue = new MVCC_FLOATVALUE();
                int nRet = MV_CC_GetFloatValue_NET("ExposureTime", ref pstValue);
                if (MvCamera.MV_OK != nRet)
                {
                    _errMsg = GlobalVarAndFunc.LanguageTranslate("获取ExposureTime值失败：") + Convert.ToString(nRet, 16);
                    return false;
                }
                float 最小曝光 = pstValue.fMin;
                float 最大曝光 = pstValue.fMax;
                float 曝光间隔 = 1;

                float exposureMin = 最小曝光;
                float exposureMax = 最大曝光;
                if (!GetWidth(out long Width))
                {
                    return false;
                }
                if (!GetHeight(out long Height))
                {
                    return false;
                }
                double col1 = Width * left;
                double col2 = Width * right;
                double row1 = Height * top;
                double row2 = Height * down;
                //HRegion hRegion = new HRegion(row1, col1, row2, col2);
                Rect rect = new Rect((int)col1, (int)row1, (int)(col2 - col1), (int)(row2 - row1));

                bool bflag = false;
                while (bRun)
                {
                    count++;
                    if (!IsGrabbing)
                    {
                        if (!StartGrabbing())
                        {
                            return false;
                        }
                    }
                    while (IsOpen && IsGrabbing)
                    {
                        MV_FRAME_OUT stFrameOut = new MV_FRAME_OUT();
                        nRet = MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);
                        StopGrabbing();
                        if (MvCamera.MV_OK == nRet)
                        {
                            bflag = ToMImage(stFrameOut.pBufAddr, stFrameOut.stFrameInfo, out mImage);
                            MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                            if (bflag)
                            {
                                //double gray = mImage.Intensity(hRegion, out double _);
                                Mat cutImage = new Mat(mImage, rect);
                                Scalar mean = Cv2.Mean(cutImage);
                                double gray = mean.Val0;

                                if (gray == 0)
                                {
                                    rect = new Rect((int)col1, (int)row1, (int)(col2 - col1), (int)(row2 - row1));
                                    cutImage = new Mat(mImage, rect);
                                    mean = Cv2.Mean(cutImage);
                                    gray = mean.Val0;
                                }
                                if (gray < grayMin)
                                {
                                    if (!GetExposure(out exposureMin))
                                    {
                                        mImage?.Dispose();
                                        return false;
                                    }
                                    Console.WriteLine(exposureMin + " " + gray + " " + count);
                                    if (exposureMax == 最大曝光)
                                    {
                                        if (!SetExposure(Math.Min(exposureMin * 2, 最大曝光)))
                                        {
                                            mImage?.Dispose();
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if (exposureMax - exposureMin <= 曝光间隔)
                                        {
                                            bRun = false;
                                        }
                                        else
                                        {
                                            if (!SetExposure((exposureMin + exposureMax) / 2))
                                            {
                                                mImage?.Dispose();
                                                return false;
                                            }
                                        }
                                    }
                                }
                                else if (gray > grayMax)
                                {
                                    if (!GetExposure(out exposureMax))
                                    {
                                        mImage?.Dispose();
                                        return false;
                                    }
                                    Console.WriteLine(exposureMax + " " + gray + " " + count);
                                    if (exposureMin == 最小曝光)
                                    {
                                        if (!SetExposure(Math.Max(exposureMax / 2, 最小曝光)))
                                        {
                                            mImage?.Dispose();
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if (exposureMax - exposureMin <= 曝光间隔)
                                        {
                                            bRun = false;
                                        }
                                        else
                                        {
                                            if (!SetExposure((exposureMin + exposureMax) / 2))
                                            {
                                                mImage?.Dispose();
                                                return false;
                                            }
                                        }
                                    }
                                }
                                else { bRun = false; }

                                if (exposureMin == exposureMax)
                                {
                                    bRun = false;
                                }
                                if (countMax > 0 && count >= countMax)
                                {
                                    bRun = false;
                                }

                                outGray = gray;
                            }
                        }
                        //else
                        //{
                        //    _errMsg = "采集失败：" + Convert.ToString(nRet, 16);
                        //    return false;
                        //}
                    }
                }
                return bflag;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机未打开");
                return false;
            }
        }

        /// <summary>
        /// 连续取像
        /// </summary>
        /// <param name="UseImages"></param>
        /// <returns></returns>
        public bool KeepShot(Action<Mat> UseImages)
        {
            if (IsOpen)
            {
                if (!IsGrabbing)
                {
                    if (!StartGrabbing())
                    {
                        return false;
                    }
                }
                Thread th = new Thread(() =>
                {
                    while (IsOpen && IsGrabbing)
                    {
                        MV_FRAME_OUT stFrameOut = new MV_FRAME_OUT();
                        int nRet = MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);
                        if (MvCamera.MV_OK == nRet)
                        {
                            bool bflag = ToMImage(stFrameOut.pBufAddr, stFrameOut.stFrameInfo, out Mat mImage);
                            if (_manufacturerName == "ChinaVision" && _ReverseX)
                            {
                                //HObject mImage_mirror;
                                //HalconDotNet.HOperatorSet.MirrorImage(mImage, out mImage_mirror, "column");
                                //mImage = new Mat(mImage_mirror);
                                Mat mImageFlip = new Mat();
                                Cv2.Flip(mImage, mImageFlip, 0);
                                mImage = mImageFlip;
                            }
                            if (bflag)
                            {
                                UseImages(mImage);
                            }
                            MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                        }
                    };
                });
                th.Start();
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("相机未打开");
                return false;
            }
        }

        /// <summary>
        /// 相机初始化设置
        /// </summary>
        /// <returns></returns>
        public bool InitSet()
        {
            try
            {
                if (!SetAcquisitionMode(AcquisitionMode.Continuous))
                {
                    return false;
                }
                if (!SetTriggerMode(TriggerMode.Off))
                {
                    return false;
                }
                if (!SetTriggerCacheEnable(true))
                {
                    return false;
                }
                if (!SetExposureAuto(ExposureAuto.Off))
                {
                    return false;
                }
                //if (!SetTimestamp(true))
                //{
                //    return false;
                //}
                //if (!SetFramecounter(true))
                //{
                //    return false;
                //}

                if (!SetLine2Mode(LineMode.Strobe))
                {
                    return false;
                }
                if (!SetLine1Inverter(false))
                {
                    return false;
                }
                if (!SetLine2Inverter(false))
                {
                    return false;
                }
                if (!SetLine1StrobeEnable(false))
                {
                    return false;
                }
                if (!SetLine2StrobeEnable(false))
                {
                    return false;
                }

                if (!SetAcquisitionFrameRateEnable(true))
                {
                    return false;
                }

                if (!SetImageNodeNum(10))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 相机初始化设置
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public bool InitSet(CamParam param)
        {
            try
            {
                if (!SetAcquisitionMode(AcquisitionMode.Continuous))
                {
                    return false;
                }
                //if (!SetTriggerMode(TriggerMode.Off))
                //{
                //    return false;
                //}
                if (!SetTriggerCacheEnable(true))
                {
                    return false;
                }
                if (!SetExposureAuto(ExposureAuto.Off))
                {
                    return false;
                }

                if (!SetLine2Mode(LineMode.Strobe))
                {
                    return false;
                }
                if (!SetLine1Inverter(false))
                {
                    return false;
                }

                if (!SetLine1StrobeEnable(true))
                {
                    return false;
                }
                //if (!SetLine2StrobeEnable(false))
                //{
                //    return false;
                //}

                if (!SetExposure(param.Exposure))
                {
                    return false;
                }
                if (!SetAcquisitionFrameRate(param.Hz))
                {
                    return false;
                }
                if (!SetAcquisitionFrameRateEnable(true))
                {
                    return false;
                }
                if (!SetReverseX(param.ReverseX))
                {
                    return false;
                }
                if (!SetReverseY(param.ReverseY))
                {
                    return false;
                }
                if (!SetOffsetX(0))
                {
                    return false;
                }
                if (!SetOffsetY(0))
                {
                    return false;
                }
                if (!SetWidth(param.SizeWidth))
                {
                    return false;
                }
                if (!SetHeight(param.SizeHeight))
                {
                    return false;
                }
                if (!SetOffsetX(param.ReverseX ? param.WidthMax - param.SizeWidth - param.OffsetX : param.OffsetX))
                {
                    return false;
                }
                if (!SetOffsetY(param.ReverseY ? param.HeightMax - param.SizeHeight - param.OffsetY : param.OffsetY))
                {
                    return false;
                }
                if (!SetImageNodeNum(10))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
            return true;
        }

        #region 参数获取与设置函数
        /// <summary>
        /// 设置设备的采集模式
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetAcquisitionMode(AcquisitionMode value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetEnumValue("AcquisitionMode", (uint)value);

            }
            else if (_manufacturerName == "ChinaVision")
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// 是否启用触发器缓存
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetTriggerCacheEnable(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetBoolValue("TriggerCacheEnable", value);

            }
            else if (_manufacturerName == "ChinaVision")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 设置设备的采集模式
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetTriggerMode(TriggerMode value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetEnumValue("TriggerMode", (uint)value);

            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetEnumValue("TriggerMode", (uint)value);
            }
            else
            {
                return false;
            }

        }

        public bool SetExposureAuto(ExposureAuto value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetEnumValue("ExposureAuto", (uint)value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetEnumValue("ExposureAuto", (uint)value);
            }
            else
            {
                return false;
            }

        }

        public bool SetExposure(float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (value < 15)
                {
                    if (!SetEnumValue("ExposureTimeMode", (uint)ExposureTimeMode.UltraShort))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!SetEnumValue("ExposureTimeMode", (uint)ExposureTimeMode.Standard))
                    {
                        return false;
                    }
                }
                return SetFloatValue("ExposureTime", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetFloatValue("ExposureTime", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetExposure(out float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetFloatValue("ExposureTime", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetFloatValue("ExposureTime", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// 设置帧率值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetAcquisitionFrameRate(float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetFloatValue("AcquisitionFrameRate", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetIntValue("AcquisitionFrameRateAbs", (int)value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 获取设置的帧率值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool GetAcquisitionFrameRate(out float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetFloatValue("AcquisitionFrameRate", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                long int_value = 0;
                bool rt = GetIntValue("AcquisitionFrameRateAbs", out int_value);
                value = (float)int_value;
                return rt;
            }
            else
            {
                value = 0;
                return false;
            }
        }
        /// <summary>
        /// 设置帧率控制开关
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetAcquisitionFrameRateEnable(bool value)
        {

            if (_manufacturerName == "Hikrobot")
            {
                return SetBoolValue("AcquisitionFrameRateEnable", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetBoolValue("AcquisitionFrameRateEnable", value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 获取设置的帧率控制开关值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool GetAcquisitionFrameRateEnable(out bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetBoolValue("AcquisitionFrameRateEnable", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetBoolValue("AcquisitionFrameRateEnable", out value);
            }
            else
            {
                value = false;
                return false;
            }

        }
        /// <summary>
        /// 获取实际帧率
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool GetResultingFrameRate(out float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetFloatValue("ResultingFrameRate", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                value = 0;
                return false;
            }
            else
            {
                value = 0;
                return false;
            }

        }
        public bool SetWidth(long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetIntValue("Width", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetIntValue("Width", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetWidth(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("Width", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("Width", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        public bool GetWidthMax(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("WidthMax", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("WidthMax", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        public bool SetHeight(long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetIntValue("Height", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetIntValue("Height", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetHeight(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("Height", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("Height", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        public bool GetHeightMax(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("HeightMax", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("HeightMax", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        public bool SetOffsetX(long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetIntValue("OffsetX", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetIntValue("OffsetX", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetOffsetX(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("OffsetX", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("OffsetX", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        public bool SetOffsetY(long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetIntValue("OffsetY", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetIntValue("OffsetY", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetOffsetY(out long value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetIntValue("OffsetY", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetIntValue("OffsetY", out value);
            }
            else
            {
                value = 0;
                return false;
            }
        }
        /// <summary>
        /// 设置水平镜像
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetReverseX(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetBoolValue("ReverseX", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                _ReverseX = value;
                return SetBoolValue("ReverseX", value);
            }
            else
            {
                return false;
            }

        }
        /// <summary>
        /// 设置垂直镜像
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetReverseY(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetBoolValue("ReverseY", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetBoolValue("ReverseY", value);
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// 图像嵌入信息选择器
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool SetFrameSpecInfoSelector(FrameSpecInfoSelector value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetEnumValue("FrameSpecInfoSelector", (uint)value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return false;
            }
            else
            {
                return false;
            }

        }
        /// <summary>
        /// 图像嵌入信息使能
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool SetFrameSpecInfo(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetBoolValue("FrameSpecInfo", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return false;
            }
            else
            {
                return false;
            }

        }
        /// <summary>
        /// 时间戳
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetTimestamp(bool value)
        {
            if (SetFrameSpecInfoSelector(FrameSpecInfoSelector.Timestamp))
            {
                return SetFrameSpecInfo(value);
            }
            else
            {
                value = false;
                return false;
            }
        }
        /// <summary>
        /// 帧计数器
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetFramecounter(bool value)
        {
            if (SetFrameSpecInfoSelector(FrameSpecInfoSelector.Framecounter))
            {
                return SetFrameSpecInfo(value);
            }
            else
            {
                value = false;
                return false;
            }
        }
        /// <summary>
        /// 设置SDK内部图像缓存节点个数，大于等于1，在抓图前调用 
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public bool SetImageNodeNum(uint num)
        {
            int nRet = MV_CC_SetImageNodeNum_NET(num);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("设置设置SDK内部图像缓存节点个数为") + num + GlobalVarAndFunc.LanguageTranslate("失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        #endregion

        #region 参数获取与设置封装功能函数
        private bool SetBoolValue(string strKey, bool value)
        {
            int nRet = MV_CC_SetBoolValue_NET(strKey, value);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("设置") + strKey + GlobalVarAndFunc.LanguageTranslate("为") + value.ToString() + GlobalVarAndFunc.LanguageTranslate("失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool SetEnumValue(string strKey, uint value)
        {
            int nRet = MV_CC_SetEnumValue_NET(strKey, value);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("设置") + strKey + GlobalVarAndFunc.LanguageTranslate("为") + value.ToString() + GlobalVarAndFunc.LanguageTranslate("失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool SetIntValue(string strKey, long value)
        {
            int nRet = MV_CC_SetIntValueEx_NET(strKey, value);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("设置") + strKey + GlobalVarAndFunc.LanguageTranslate("为") + value.ToString() + GlobalVarAndFunc.LanguageTranslate("失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool SetFloatValue(string strKey, float value)
        {
            int nRet = MV_CC_SetFloatValue_NET(strKey, value);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("设置") + strKey + GlobalVarAndFunc.LanguageTranslate("为") + value.ToString() + GlobalVarAndFunc.LanguageTranslate("失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }

        private bool GetBoolValue(string strKey, out bool value)
        {
            bool pstValue = false;
            int nRet = MV_CC_GetBoolValue_NET(strKey, ref pstValue);
            value = pstValue;
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("获取") + strKey + GlobalVarAndFunc.LanguageTranslate("值失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool GetEnumValue(string strKey, out uint value)
        {
            MVCC_ENUMVALUE pstValue = new MVCC_ENUMVALUE();
            int nRet = MV_CC_GetEnumValue_NET(strKey, ref pstValue);
            value = pstValue.nCurValue;
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("获取") + strKey + GlobalVarAndFunc.LanguageTranslate("值失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool GetIntValue(string strKey, out long value)
        {
            MVCC_INTVALUE_EX pstValue = new MVCC_INTVALUE_EX();
            int nRet = MV_CC_GetIntValueEx_NET(strKey, ref pstValue);
            value = pstValue.nCurValue;
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("获取") + strKey + GlobalVarAndFunc.LanguageTranslate("值失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }
        private bool GetFloatValue(string strKey, out float value)
        {
            MVCC_FLOATVALUE pstValue = new MVCC_FLOATVALUE();
            int nRet = MV_CC_GetFloatValue_NET(strKey, ref pstValue);
            value = pstValue.fCurValue;
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = GlobalVarAndFunc.LanguageTranslate("获取") + strKey + GlobalVarAndFunc.LanguageTranslate("值失败：") + Convert.ToString(nRet, 16);
                return false;
            }
        }

        static private bool IsMonoPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        static private bool IsColorPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_RGBA8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BGRA8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                    return true;
                default:
                    return false;
            }
        }
        #endregion

        #region IO
        bool SetLineSelector(LineSelector value)
        {
            return SetEnumValue("LineSelector", (uint)value);
        }
        bool SetLineStatus(bool value)
        {
            return SetBoolValue("LineStatus", value);
        }
        bool GetLineStatus(out bool value)
        {
            return GetBoolValue("LineStatus", out value);
        }
        bool SetLineInverter(bool value)
        {
            return SetBoolValue("LineInverter", value);
        }
        bool GetLineInverter(out bool value)
        {
            return GetBoolValue("LineInverter", out value);
        }
        bool SetLineSource(LineSource value)
        {
            return SetEnumValue("LineSource", (uint)value);
        }
        bool SetStrobeLineDuration(long value)
        {
            return SetIntValue("StrobeLineDuration", value);
        }
        bool GetStrobeLineDuration(out long value)
        {
            return GetIntValue("StrobeLineDuration", out value);
        }
        bool SetLineStrobeEnable(bool value)
        {
            return SetBoolValue("StrobeEnable", value);
        }
        bool GetLineStrobeEnable(out bool value)
        {
            return GetBoolValue("StrobeEnable", out value);
        }

        /// <summary>
        /// 设置line2模式（输入/输出）
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine2Mode(LineMode value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (SetLineSelector(LineSelector.Line2))
                {
                    return SetEnumValue("LineMode", (uint)value);
                }
                else
                {
                    return false;
                }
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        //public bool SetLine1Status(bool value)
        //{
        //    if (SetLineSelector(LineSelector.Line1))
        //    {
        //        return SetLineStatus(value);
        //    }
        //    else
        //    {
        //        value = false;
        //        return false;
        //    }
        //}
        //public bool SetLine2Status(bool value)
        //{
        //    if (SetLineSelector(LineSelector.Line2))
        //    {
        //        return SetLineStatus(value);
        //    }
        //    else
        //    {
        //        value = false;
        //        return false;
        //    }
        //}
        public bool GetLine0Status(out bool value)
        {
            if (SetLineSelector(LineSelector.Line0))
            {
                return GetLineStatus(out value);
            }
            else
            {
                value = false;
                return false;
            }
        }
        public bool GetLine1Status(out bool value)
        {
            if (SetLineSelector(LineSelector.Line1))
            {
                return GetLineStatus(out value);
            }
            else
            {
                value = false;
                return false;
            }
        }
        public bool GetLine2Status(out bool value)
        {
            if (SetLineSelector(LineSelector.Line2))
            {
                return GetLineStatus(out value);
            }
            else
            {
                value = false;
                return false;
            }
        }
        public bool GetLineStatusAll(out bool value0, out bool value1, out bool value2)
        {
            if (GetIntValue("LineStatusAll", out long value))
            {
                value0 = (value & 0b001) > 0;
                value1 = (value & 0b010) > 0;
                value2 = (value & 0b100) > 0;
                return true;
            }
            else
            {
                value0 = false;
                value1 = false;
                value2 = false;
                return false;
            }
        }

        /// <summary>
        /// 设置线路1反转
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine1Inverter(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (SetLineSelector(LineSelector.Line1))
                {
                    return SetLineInverter(value);
                }
                else
                {
                    value = false;
                    return false;
                }
            }
            else if (_manufacturerName == "ChinaVision")
            {
                long int_value = 0;
                if (value)
                {
                    int_value = 0;
                }
                else
                {
                    int_value = 1;
                }
                return SetIntValue("GPOut0", int_value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 设置线路2反转
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine2Inverter(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (SetLineSelector(LineSelector.Line2))
                {
                    return SetLineInverter(value);
                }
                else
                {
                    value = false;
                    return false;
                }
            }
            else if (_manufacturerName == "ChinaVision")
            {
                long int_value = 0;
                if (value)
                {
                    int_value = 0;
                }
                else
                {
                    int_value = 1;
                }
                return SetIntValue("GPOut1", int_value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 选择要在线路1输出的内部采集或I/O源信号
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine1Source(LineSource value)
        {
            if (SetLineSelector(LineSelector.Line1))
            {
                return SetLineSource(value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 选择要在线路2输出的内部采集或I/O源信号
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine2Source(LineSource value)
        {
            if (SetLineSelector(LineSelector.Line2))
            {
                return SetLineSource(value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 以us为单位设置选定输出线路1持续时间的值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool SetStrobeLine1Duration(long value)
        {
            if (SetLineSelector(LineSelector.Line1))
            {
                return SetStrobeLineDuration(value);
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 以us为单位设置选定输出线路2持续时间的值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool SetStrobeLine2Duration(long value)
        {
            if (SetLineSelector(LineSelector.Line2))
            {
                return SetStrobeLineDuration(value);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 设置线路1输出使能开关
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine1StrobeEnable(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (SetLineSelector(LineSelector.Line1))
                {
                    return SetLineStrobeEnable(value);
                }
                else
                {
                    value = false;
                    return false;
                }
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        /// <summary>
        /// 设置线路2输出使能开关
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetLine2StrobeEnable(bool value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                if (SetLineSelector(LineSelector.Line2))
                {
                    return SetLineStrobeEnable(value);
                }
                else
                {
                    value = false;
                    return false;
                }
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region Enum
        public enum AcquisitionMode
        {
            SingleFrame = 0,
            Continuous = 2,
        }
        public enum TriggerMode
        {
            Off = 0,
            On = 1,
        }
        public enum ExposureTimeMode
        {
            /// <summary>
            /// 标准
            /// </summary>
            Standard = 0,
            /// <summary>
            /// 超短
            /// </summary>
            UltraShort = 1,
        }
        public enum ExposureAuto
        {
            Off = 0,
            Once = 1,
            Continuous = 2,
        }
        enum LineSelector
        {
            Line0 = 0,
            Line1 = 1,
            Line2 = 2,
        }
        public enum LineMode
        {
            Input = 0,
            Strobe = 8,
        }
        public enum LineSource
        {
            /// <summary>
            /// 曝光开始有效
            /// </summary>
            ExposureStartActive = 0,
            /// <summary>
            /// 采集开始有效
            /// </summary>
            AcquisitionStartActive,
            /// <summary>
            /// 采集停止有效
            /// </summary>
            AcquisitionStopActive,
            /// <summary>
            /// 帧突发开始有效
            /// </summary>
            FrameBurstStartActive,
            /// <summary>
            /// 帧突发结束有效
            /// </summary>
            FrameBurstEndActive,
            /// <summary>
            /// 软触发有效
            /// </summary>
            SoftTriggerActive,
            /// <summary>
            /// 硬触发有效
            /// </summary>
            HardTriggerActive,
            /// <summary>
            /// 计数器有效
            /// </summary>
            CounterActive,
            /// <summary>
            /// 计时器有效
            /// </summary>
            TimerActive,

        }
        enum FrameSpecInfoSelector
        {
            /// <summary>
            /// 时间戳
            /// </summary>
            Timestamp = 0,
            /// <summary>
            /// 增益
            /// </summary>
            Gain = 1,
            /// <summary>
            /// 曝光
            /// </summary>
            Exposure = 2,
            /// <summary>
            /// 亮度信息
            /// </summary>
            BrightnessInfo = 3,
            /// <summary>
            /// 帧计数器
            /// </summary>
            Framecounter = 5,
            /// <summary>
            /// 外部触发器的计数器
            /// </summary>
            ExtTriggerCount = 6,
            /// <summary>
            /// 线路输入输出
            /// </summary>
            LineInputOutput = 7,
            /// <summary>
            /// 感兴趣区域位置
            /// </summary>
            ROIPosition = 8,
        }
        #endregion

    }
    public class CamParams
    {
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;
        //保存的参数
        public Dictionary<string, Dictionary<string, CamParam>> Param = new Dictionary<string, Dictionary<string, CamParam>>();
        public Dictionary<string, Dictionary<string, CameraParameters>> CamPar = new Dictionary<string, Dictionary<string, CameraParameters>>();
        public Dictionary<string, Dictionary<string, PoseParameters>> LightInCam = new Dictionary<string, Dictionary<string, PoseParameters>>();
        //public Dictionary<string, Dictionary<string, PoseParameters>> SensorInCam = new Dictionary<string, Dictionary<string, PoseParameters>>();
        public Dictionary<string, Dictionary<string, PoseParameters>> ToolInCam = new Dictionary<string, Dictionary<string, PoseParameters>>();
        //计算的参数
        public Dictionary<string, Dictionary<string, Mat>> LightToCam = new Dictionary<string, Dictionary<string, Mat>>();
        //public Dictionary<string, Dictionary<string, Mat>> CamToSensor = new Dictionary<string, Dictionary<string, Mat>>();
        public Dictionary<string, Dictionary<string, Mat>> CamToTool = new Dictionary<string, Dictionary<string, Mat>>();
        ////保存的参数
        //public Dictionary<string, Mat> SensorToTool = new Dictionary<string, Mat>();

        public bool Load()
        {
            bool results = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data";
            Param.Clear();
            CamPar.Clear();
            LightInCam.Clear();
            //SensorInCam.Clear();
            ToolInCam.Clear();
            LightToCam.Clear();
            //CamToSensor.Clear();
            CamToTool.Clear();
            string camSetPath = basePath + "\\CamSet";
            if (Directory.Exists(camSetPath))
            {
                string[] paths = Directory.GetDirectories(camSetPath);
                foreach (var path in paths)
                {
                    string name = Path.GetFileName(path);
                    Param.Add(name, new Dictionary<string, CamParam>());
                    CamPar.Add(name, new Dictionary<string, CameraParameters>());
                    LightInCam.Add(name, new Dictionary<string, PoseParameters>());
                    //SensorInCam.Add(name, new Dictionary<string, PoseParameters>());
                    ToolInCam.Add(name, new Dictionary<string, PoseParameters>());
                    LightToCam.Add(name, new Dictionary<string, Mat>());
                    //CamToSensor.Add(name, new Dictionary<string, Mat>());
                    CamToTool.Add(name, new Dictionary<string, Mat>());

                    string[] camPaths = Directory.GetDirectories(path);
                    foreach (string camPath in camPaths)
                    {
                        string camKey = Path.GetFileName(camPath);

                        bool result = true;
                        try
                        {
                            string paramPath = camPath + "\\Param.xml";
                            if (File.Exists(paramPath))
                            {
                                XmlSerializer xml = new XmlSerializer(typeof(CamParam));
                                using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                                {
                                    CamParam paramList = (CamParam)xml.Deserialize(stream);
                                    if (paramList != null)
                                    {
                                        Param[name].Add(camKey, paramList);
                                    }
                                    else
                                    {
                                        Param[name].Add(camKey, new CamParam());
                                        result = false;
                                        _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                                    }
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
                        if (!result)
                        {
                            result = true;
                            try
                            {
                                string paramPath = camPath + "\\Param_bak.xml";
                                if (File.Exists(paramPath))
                                {
                                    XmlSerializer xml = new XmlSerializer(typeof(CamParam));
                                    using (FileStream stream = new FileStream(paramPath, FileMode.OpenOrCreate))
                                    {
                                        CamParam paramList = (CamParam)xml.Deserialize(stream);
                                        if (paramList != null)
                                        {
                                            Param[name][camKey] = paramList;
                                            File.Copy(paramPath, camPath + "\\Params.xml", true);
                                        }
                                        else
                                        {
                                            result = false;
                                        }
                                    }
                                }
                                else
                                {
                                    result = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result = false;
                            }
                        }

                        bool result2 = true;
                        try
                        {
                            string paramPath = camPath + "\\camparam.cal";
                            if (File.Exists(paramPath))
                            {
                                var hCamPar = new CameraParameters();
                                //hCamPar.ReadCamPar(paramPath);
                                hCamPar = HFileIO.ReadCamPara(paramPath);
                                if (CamPar[name].ContainsKey(camKey))
                                {
                                    CamPar[name][camKey] = hCamPar;
                                }
                                else
                                {
                                    CamPar[name].Add(camKey, hCamPar);
                                }
                            }
                            else
                            {
                                result2 = false;
                                _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                            }
                        }
                        catch (Exception ex)
                        {
                            result2 = false;
                            _errMsg = ex.ToString();
                        }
                        if (!result2)
                        {
                            result2 = true;
                            try
                            {
                                string paramPath = camPath + "\\camparam_bak.xml";
                                if (File.Exists(paramPath))
                                {
                                    var hCamPar = new CameraParameters();
                                    //hCamPar.ReadCamPar(paramPath);
                                    hCamPar = HFileIO.ReadCamPara(paramPath);

                                    if (CamPar[name].ContainsKey(camKey))
                                    {
                                        CamPar[name][camKey] = hCamPar;
                                    }
                                    else
                                    {
                                        CamPar[name].Add(camKey, hCamPar);
                                    }
                                }
                                else
                                {
                                    result2 = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result2 = false;
                            }
                        }

                        bool result3 = true;
                        try
                        {
                            string paramPath = camPath + "\\LightInCam.dat";
                            if (File.Exists(paramPath))
                            {
                                var hWorldPose = new PoseParameters();

                                //hWorldPose.ReadPose(paramPath);
                                hWorldPose = HFileIO.ReadPosePara(paramPath);
                                if (LightInCam[name].ContainsKey(camKey))
                                {
                                    LightInCam[name][camKey] = hWorldPose;
                                }
                                else
                                {
                                    LightInCam[name].Add(camKey, hWorldPose);
                                }
                            }
                            else
                            {
                                result3 = false;
                                _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                            }
                        }
                        catch (Exception ex)
                        {
                            result3 = false;
                            _errMsg = ex.ToString();
                        }
                        if (!result3)
                        {
                            result3 = true;
                            try
                            {
                                string paramPath = camPath + "\\LightInCam_bak.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);
                                    if (LightInCam[name].ContainsKey(camKey))
                                    {
                                        LightInCam[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        LightInCam[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result3 = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result3 = false;
                            }
                        }

                        bool result4 = true;
                        //try
                        //{
                        //    string paramPath = camPath + "\\SensorInCam.dat";
                        //    if (File.Exists(paramPath))
                        //    {
                        //        var hWorldPose = new PoseParameters();
                        //        hWorldPose.ReadPose(paramPath);
                        //        if (SensorInCam[name].ContainsKey(camKey))
                        //        {
                        //            SensorInCam[name][camKey] = hWorldPose;
                        //        }
                        //        else
                        //        {
                        //            SensorInCam[name].Add(camKey, hWorldPose);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        result4 = false;
                        //        _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                        //    }
                        //}
                        //catch (Exception ex)
                        //{
                        //    result4 = false;
                        //    _errMsg = ex.ToString();
                        //}
                        //if (!result4)
                        //{
                        //    result4 = true;
                        //    try
                        //    {
                        //        string paramPath = camPath + "\\SensorInCam_bak.dat";
                        //        if (File.Exists(paramPath))
                        //        {
                        //            var hWorldPose = new PoseParameters();
                        //            hWorldPose.ReadPose(paramPath);
                        //            if (SensorInCam[name].ContainsKey(camKey))
                        //            {
                        //                SensorInCam[name][camKey] = hWorldPose;
                        //            }
                        //            else
                        //            {
                        //                SensorInCam[name].Add(camKey, hWorldPose);
                        //            }
                        //        }
                        //        else
                        //        {
                        //            result4 = false;
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        result4 = false;
                        //    }
                        //}

                        bool result5 = true;
                        try
                        {
                            string paramPath = camPath + "\\ToolInCam.dat";
                            if (File.Exists(paramPath))
                            {
                                var hWorldPose = new PoseParameters();
                                //hWorldPose.ReadPose(paramPath);
                                hWorldPose = HFileIO.ReadPosePara(paramPath);

                                if (ToolInCam[name].ContainsKey(camKey))
                                {
                                    ToolInCam[name][camKey] = hWorldPose;
                                }
                                else
                                {
                                    ToolInCam[name].Add(camKey, hWorldPose);
                                }
                            }
                            else
                            {
                                result5 = false;
                                _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                            }
                        }
                        catch (Exception ex)
                        {
                            result5 = false;
                            _errMsg = ex.ToString();
                        }
                        if (!result5)
                        {
                            result5 = true;
                            try
                            {
                                string paramPath = camPath + "\\ToolInCam_bak.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);

                                    if (ToolInCam[name].ContainsKey(camKey))
                                    {
                                        ToolInCam[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        ToolInCam[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result5 = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result5 = false;
                            }
                        }

                        bool result6 = true;
                        try
                        {

                            if (LightInCam[name].ContainsKey(camKey))
                            {
                                if (LightToCam[name].ContainsKey(camKey))
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(LightInCam[name][camKey].PoseType, LightInCam[name][camKey].x, LightInCam[name][camKey].y, LightInCam[name][camKey].z,
                                        LightInCam[name][camKey].rx, LightInCam[name][camKey].ry, LightInCam[name][camKey].rz, H.CvPtr);
                                    LightToCam[name][camKey] = H;
                                }
                                else
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(LightInCam[name][camKey].PoseType, LightInCam[name][camKey].x, LightInCam[name][camKey].y, LightInCam[name][camKey].z,
                                        LightInCam[name][camKey].rx, LightInCam[name][camKey].ry, LightInCam[name][camKey].rz, H.CvPtr);

                                    LightToCam[name].Add(camKey, H);
                                }
                            }
                            //if (SensorInCam[name].ContainsKey(camKey))
                            //{
                            //    if (CamToSensor[name].ContainsKey(camKey))
                            //    {
                            //        CamToSensor[name][camKey] = SensorInCam[name][camKey].PoseInvert().PoseToHomMat3d();
                            //    }
                            //    else
                            //    {
                            //        CamToSensor[name].Add(camKey, SensorInCam[name][camKey].PoseInvert().PoseToHomMat3d());
                            //    }
                            //}
                            if (ToolInCam[name].ContainsKey(camKey))
                            {
                                if (CamToTool[name].ContainsKey(camKey))
                                {
                                    //CamToTool[name][camKey] = ToolInCam[name][camKey].PoseInvert().PoseToHomMat3d();
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(ToolInCam[name][camKey].PoseType, ToolInCam[name][camKey].x, ToolInCam[name][camKey].y, ToolInCam[name][camKey].z,
                                        ToolInCam[name][camKey].rx, ToolInCam[name][camKey].ry, ToolInCam[name][camKey].rz, H.CvPtr);

                                    CamToTool[name][camKey] = H.Inv();
                                }
                                else
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(ToolInCam[name][camKey].PoseType, ToolInCam[name][camKey].x, ToolInCam[name][camKey].y, ToolInCam[name][camKey].z,
                                        ToolInCam[name][camKey].rx, ToolInCam[name][camKey].ry, ToolInCam[name][camKey].rz, H.CvPtr);

                                    CamToTool[name].Add(camKey, H.Inv());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result6 = false;
                            _errMsg = ex.ToString();
                        }


                        if (!result || !result2 || !result3 || !result4 || !result5 || !result6)
                        {
                            results = false;
                        }
                    }

                    //bool resultTool = true;
                    //try
                    //{
                    //    string paramPath = path + "\\SensorToTool";
                    //    if (File.Exists(paramPath))
                    //    {
                    //        using (FileStream stream = new FileStream(paramPath, FileMode.Open))
                    //        {
                    //            var hHomMat3D = Mat.Deserialize(stream);
                    //            if (SensorToTool.ContainsKey(name))
                    //            {
                    //                SensorToTool[name] = hHomMat3D;
                    //            }
                    //            else
                    //            {
                    //                SensorToTool.Add(name, hHomMat3D);
                    //            }
                    //        }
                    //    }
                    //    else
                    //    {
                    //        resultTool = false;
                    //        _errMsg = paramPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    resultTool = false;
                    //    _errMsg = ex.ToString();
                    //}
                    //if (!resultTool)
                    //{
                    //    resultTool = true;
                    //    try
                    //    {
                    //        string paramPath = path + "\\SensorToTool_bak";
                    //        if (File.Exists(paramPath))
                    //        {
                    //            using (FileStream stream = new FileStream(paramPath, FileMode.Open))
                    //            {
                    //                var hHomMat3D = Mat.Deserialize(stream);
                    //                if (SensorToTool.ContainsKey(name))
                    //                {
                    //                    SensorToTool[name] = hHomMat3D;
                    //                }
                    //                else
                    //                {
                    //                    SensorToTool.Add(name, hHomMat3D);
                    //                }
                    //                File.Copy(paramPath, path + "\\SensorToTool", true);
                    //            }
                    //        }
                    //        else
                    //        {
                    //            resultTool = false;
                    //        }
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        resultTool = false;
                    //    }
                    //}

                    //if (!resultTool)
                    //{
                    //    results = false;
                    //}
                }
            }
            else
            {
                _errMsg = camSetPath + GlobalVarAndFunc.LanguageTranslate("文件夹不存在");
                results = false;
            }

            return results;
        }
        public bool Save()
        {
            bool result = true;
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                foreach (var name in Param.Keys)
                {
                    foreach (var key in Param[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        string BaslerParamPath = $"{path}\\Param.xml";
                        XmlSerializer xml = new XmlSerializer(Param[name][key].GetType());
                        using (FileStream stream = new FileStream(BaslerParamPath, FileMode.Create))
                        {
                            xml.Serialize(stream, Param[name][key]);
                        }
                        File.Copy(BaslerParamPath, path + "\\Param_bak.xml", true);
                    }
                }

                foreach (var name in CamPar.Keys)
                {
                    foreach (var key in CamPar[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        string paramPath = $"{path}\\camparam.cal";
                        //CamPar[name][key].WriteCamPar(paramPath);
                        HFileIO.WriteCamPara(paramPath, CamPar[name][key]);

                        File.Copy(paramPath, path + "\\camparam_bak.cal", true);
                    }
                }

                foreach (var name in LightInCam.Keys)
                {
                    foreach (var key in LightInCam[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        string paramPath = $"{path}\\LightInCam.dat";
                        //LightInCam[name][key].WritePose(paramPath);
                        HFileIO.WritePosePara(paramPath, LightInCam[name][key]);

                        File.Copy(paramPath, path + "\\LightInCam_bak.dat", true);
                    }
                }

                //foreach (var name in SensorInCam.Keys)
                //{
                //    foreach (var key in SensorInCam[name].Keys)
                //    {
                //        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                //        if (!Directory.Exists(path))
                //        {
                //            Directory.CreateDirectory(path);
                //        }

                //        string paramPath = $"{path}\\SensorInCam.dat";
                //        SensorInCam[name][key].WritePose(paramPath);
                //        File.Copy(paramPath, path + "\\SensorInCam_bak.dat", true);
                //    }
                //}

                foreach (var name in ToolInCam.Keys)
                {
                    foreach (var key in ToolInCam[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        string paramPath = $"{path}\\ToolInCam.dat";
                        //ToolInCam[name][key].WritePose(paramPath);
                        HFileIO.WritePosePara(paramPath, ToolInCam[name][key]);
                        File.Copy(paramPath, path + "\\ToolInCam_bak.dat", true);
                    }
                }

                //foreach (var name in SensorToTool.Keys)
                //{
                //    string path = $"{basePath}\\CamSet\\{name}";
                //    if (!Directory.Exists(path))
                //    {
                //        Directory.CreateDirectory(path);
                //    }

                //    string paramPath = $"{path}\\SensorToTool";
                //    using (FileStream stream = new FileStream(paramPath, FileMode.Create))
                //    {
                //        SensorToTool[name].Serialize(stream);
                //    }
                //    File.Copy(paramPath, path + "\\SensorToTool_bak", true);
                //}
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }
            return result;
        }

        static public string[] GetParamNames()
        {
            List<string> names = new List<string>();
            string camSetPath = AppDomain.CurrentDomain.BaseDirectory + "Data\\CamSet";
            if (Directory.Exists(camSetPath))
            {
                string[] paths = Directory.GetDirectories(camSetPath);
                foreach (var path in paths)
                {
                    names.Add(Path.GetFileName(path));
                }
            }
            return names.ToArray();
        }

        public bool IsExistParamName(string name)
        {
            return Param.ContainsKey(name);
        }

        public void CopyParam(string source, string target)
        {
            if (Param.ContainsKey(source) && Param[source] != null)
            {
                Dictionary<string, CamParam> pairs = new Dictionary<string, CamParam>();
                foreach (var key in Param[source].Keys)
                {
                    pairs.Add(key, Param[source][key].Clone());
                }
                if (Param.ContainsKey(target))
                {
                    Param[target] = pairs;
                }
                else
                {
                    Param.Add(target, pairs);
                }
            }
            if (CamPar.ContainsKey(source) && CamPar[source] != null)
            {
                Dictionary<string, CameraParameters> pairs = new Dictionary<string, CameraParameters>();
                foreach (var key in CamPar[source].Keys)
                {
                    pairs.Add(key, CamPar[source][key].Clone());
                }
                if (CamPar.ContainsKey(target))
                {
                    CamPar[target] = pairs;
                }
                else
                {
                    CamPar.Add(target, pairs);
                }
            }
            if (LightInCam.ContainsKey(source) && LightInCam[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in LightInCam[source].Keys)
                {
                    pairs.Add(key, LightInCam[source][key].Clone());
                }
                if (LightInCam.ContainsKey(target))
                {
                    LightInCam[target] = pairs;
                }
                else
                {
                    LightInCam.Add(target, pairs);
                }
            }
            //if (SensorInCam.ContainsKey(source) && SensorInCam[source] != null)
            //{
            //    Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
            //    foreach (var key in SensorInCam[source].Keys)
            //    {
            //        pairs.Add(key, SensorInCam[source][key].Clone());
            //    }
            //    if (SensorInCam.ContainsKey(target))
            //    {
            //        SensorInCam[target] = pairs;
            //    }
            //    else
            //    {
            //        SensorInCam.Add(target, pairs);
            //    }
            //}
            if (ToolInCam.ContainsKey(source) && ToolInCam[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in ToolInCam[source].Keys)
                {
                    pairs.Add(key, ToolInCam[source][key].Clone());
                }
                if (ToolInCam.ContainsKey(target))
                {
                    ToolInCam[target] = pairs;
                }
                else
                {
                    ToolInCam.Add(target, pairs);
                }
            }
            if (LightToCam.ContainsKey(source) && LightToCam[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in LightToCam[source].Keys)
                {
                    pairs.Add(key, LightToCam[source][key].Clone());
                }
                if (LightToCam.ContainsKey(target))
                {
                    LightToCam[target] = pairs;
                }
                else
                {
                    LightToCam.Add(target, pairs);
                }
            }
            //if (CamToSensor.ContainsKey(source) && CamToSensor[source] != null)
            //{
            //    Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
            //    foreach (var key in CamToSensor[source].Keys)
            //    {
            //        pairs.Add(key, CamToSensor[source][key].Clone());
            //    }
            //    if (CamToSensor.ContainsKey(target))
            //    {
            //        CamToSensor[target] = pairs;
            //    }
            //    else
            //    {
            //        CamToSensor.Add(target, pairs);
            //    }
            //}
            if (CamToTool.ContainsKey(source) && CamToTool[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in CamToTool[source].Keys)
                {
                    pairs.Add(key, CamToTool[source][key].Clone());
                }
                if (CamToTool.ContainsKey(target))
                {
                    CamToTool[target] = pairs;
                }
                else
                {
                    CamToTool.Add(target, pairs);
                }
            }
            //if (SensorToTool.ContainsKey(source) && SensorToTool[source] != null)
            //{
            //    if (SensorToTool.ContainsKey(target))
            //    {
            //        SensorToTool[target] = SensorToTool[source].Clone();
            //    }
            //    else
            //    {
            //        SensorToTool.Add(target, SensorToTool[source].Clone());
            //    }
            //}
        }
    }

    [Serializable]
    public class CamParam
    {
        public CamParam Clone() { return (CamParam)this.MemberwiseClone(); }

        public string Key = string.Empty;
        public string CamName = string.Empty;
        public bool Enable = true;
        public float Exposure = 5000;
        public float Hz = 10;
        public bool HzEnable = true;
        public bool ReverseX = false;
        public bool ReverseY = false;
        public int WidthMax = 640, HeightMax = 480;
        public int SizeWidth = 640, SizeHeight = 480;
        public int OffsetX = 0, OffsetY = 0;
        public int Count = 0;
        public double LeftX = 0.25, TopY = 0.25, RightX = 0.75, DownY = 0.75;
        public byte GrayMin = 0, GrayMax = 255;
        public string ImageFormat = ".jpg";
    }
}
