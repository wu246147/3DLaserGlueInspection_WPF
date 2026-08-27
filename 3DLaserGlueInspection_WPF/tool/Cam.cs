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
using System.Windows.Ink;

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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.FailedToFindCamera + Convert.ToString(nRet, 16);
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
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CouldFindAnyCam1;
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
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CouldFindCam1 + name;
                    return false;
                }
                else
                {
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CouldFindAnyCam1;
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
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CouldFindCam1 + sn;
                    return false;
                }
                else
                {
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CouldFindAnyCam1;
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraCreationFailed + Convert.ToString(nRet, 16);
                return false;
            }

            // ch:打开设备 | en:Open device
            nRet = MV_CC_OpenDevice_NET();
            if (MvCamera.MV_OK != nRet)
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraFailedToOpen + Convert.ToString(nRet, 16);
                return false;
            }

            //// ch:注册回调函数 | en:Register image callback
            //var ImageCallback = new MvCamera.cbOutputExdelegate(ImageCallbackFunc);
            //nRet = MV_CC_RegisterImageCallBackEx_NET(ImageCallback, IntPtr.Zero);
            //if (MvCamera.MV_OK != nRet)
            //{
            //    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.RegisterCallbackFailed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraShutdownFailed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.FailedToStartCollection + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.StopCollectionFailed + Convert.ToString(nRet, 16);
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
            if (ToMImage(pData, pFrameInfo, out Mat mImage))
            {
                // 该回调只用于兼容相机 SDK 的回调模式，没有消费者接管图像所有权。
                mImage?.Dispose();
            }
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
            mImage = null;
            IntPtr pImageBuf = IntPtr.Zero;
            try
            {
                IntPtr pTemp = IntPtr.Zero;
                MatType matType;
                int nImageBufSize;

                if (IsColorPixelFormat(pFrameInfo.enPixelType))
                {
                    matType = MatType.CV_8UC3;
                    nImageBufSize = pFrameInfo.nWidth * pFrameInfo.nHeight * 3;
                    if (pFrameInfo.enPixelType == MvGvspPixelType.PixelType_Gvsp_RGB8_Packed)
                    {
                        pTemp = pData;
                    }
                    else
                    {
                        pImageBuf = Marshal.AllocHGlobal(nImageBufSize);

                        MV_PIXEL_CONVERT_PARAM stPixelConvertParam = new MV_PIXEL_CONVERT_PARAM();
                        stPixelConvertParam.pSrcData = pData;
                        stPixelConvertParam.nWidth = pFrameInfo.nWidth;
                        stPixelConvertParam.nHeight = pFrameInfo.nHeight;
                        stPixelConvertParam.enSrcPixelType = pFrameInfo.enPixelType;
                        stPixelConvertParam.nSrcDataLen = pFrameInfo.nFrameLen;
                        stPixelConvertParam.nDstBufferSize = (uint)nImageBufSize;
                        stPixelConvertParam.pDstBuffer = pImageBuf;
                        stPixelConvertParam.enDstPixelType = MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;

                        int nRet = MV_CC_ConvertPixelType_NET(ref stPixelConvertParam);
                        if (MvCamera.MV_OK != nRet)
                        {
                            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.FormatConversionFailed;
                            return false;
                        }
                        pTemp = pImageBuf;
                    }
                }
                else if (IsMonoPixelFormat(pFrameInfo.enPixelType))
                {
                    matType = MatType.CV_8UC1;
                    nImageBufSize = pFrameInfo.nWidth * pFrameInfo.nHeight;
                    if (pFrameInfo.enPixelType == MvGvspPixelType.PixelType_Gvsp_Mono8)
                    {
                        pTemp = pData;
                    }
                    else
                    {
                        pImageBuf = Marshal.AllocHGlobal(nImageBufSize);

                        MV_PIXEL_CONVERT_PARAM stPixelConvertParam = new MV_PIXEL_CONVERT_PARAM();
                        stPixelConvertParam.pSrcData = pData;
                        stPixelConvertParam.nWidth = pFrameInfo.nWidth;
                        stPixelConvertParam.nHeight = pFrameInfo.nHeight;
                        stPixelConvertParam.enSrcPixelType = pFrameInfo.enPixelType;
                        stPixelConvertParam.nSrcDataLen = pFrameInfo.nFrameLen;
                        stPixelConvertParam.nDstBufferSize = (uint)nImageBufSize;
                        stPixelConvertParam.pDstBuffer = pImageBuf;
                        stPixelConvertParam.enDstPixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;

                        int nRet = MV_CC_ConvertPixelType_NET(ref stPixelConvertParam);
                        if (MvCamera.MV_OK != nRet)
                        {
                            _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.FormatConversionFailed;
                            return false;
                        }
                        pTemp = pImageBuf;
                    }
                }
                else
                {
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.UnknownFormat + pFrameInfo.enPixelType;
                    return false;
                }

                // FromPixelData 只是一个不拥有底层指针的视图，必须在释放 SDK/临时缓冲区
                // 之前深拷贝，避免回调结束后 Mat 仍指向无效内存。
                using (Mat imageView = Mat.FromPixelData(pFrameInfo.nHeight, pFrameInfo.nWidth, matType, pTemp))
                {
                    mImage = imageView.Clone();
                }

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.FormatConversionCreationFailed + ex.ToString();
                mImage = null;
                return false;
            }
            finally
            {
                if (pImageBuf != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pImageBuf);
                }
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
                        if (bflag && _manufacturerName == "ChinaVision" && _ReverseX)
                        {
                            Mat mImageFlip = null;
                            try
                            {
                                mImageFlip = new Mat();
                                Cv2.Flip(mImage, mImageFlip, 0);
                                mImage.Dispose();
                                mImage = mImageFlip;
                                mImageFlip = null;
                            }
                            finally
                            {
                                mImageFlip?.Dispose();
                            }
                        }

                        MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                        return bflag;
                    }
                    else
                    {
                        _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CollectionFailed + Convert.ToString(nRet, 16);
                        return false;
                    }
                }
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraNotTurnedOn;
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
                            using (Mat cutImage = new Mat(mImage, rect))
                            {
                                Scalar mean = Cv2.Mean(cutImage);
                                outGray = mean.Val0;
                            }


                        }
                        return bflag;
                    }
                    else
                    {
                        _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CollectionFailed + Convert.ToString(nRet, 16);
                        return false;
                    }
                }
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraNotTurnedOn;
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
                    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.GetExportTimeFail + Convert.ToString(nRet, 16);
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
                                double gray;
                                using (Mat cutImage = new Mat(mImage, rect))
                                {
                                    gray = Cv2.Mean(cutImage).Val0;
                                }

                                if (gray == 0)
                                {
                                    rect = new Rect((int)col1, (int)row1, (int)(col2 - col1), (int)(row2 - row1));
                                    using (Mat cutImage = new Mat(mImage, rect))
                                    {
                                        gray = Cv2.Mean(cutImage).Val0;
                                    }
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

                                // 本帧仅用于曝光判断且还要继续重拍时，立即释放本帧图像。
                                if (bRun)
                                {
                                    mImage?.Dispose();
                                    mImage = null;
                                }
                            }
                        }
                        //else
                        //{
                        //    _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CollectionFailed + Convert.ToString(nRet, 16);
                        //    return false;
                        //}
                    }
                }
                return bflag;
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraNotTurnedOn;
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
                            Mat mImage = null;
                            try
                            {
                                bool bflag = ToMImage(stFrameOut.pBufAddr, stFrameOut.stFrameInfo, out mImage);
                                if (bflag && _manufacturerName == "ChinaVision" && _ReverseX)
                                {
                                    Mat mImageFlip = null;
                                    try
                                    {
                                        mImageFlip = new Mat();
                                        Cv2.Flip(mImage, mImageFlip, 0);
                                        mImage.Dispose();
                                        mImage = mImageFlip;
                                        mImageFlip = null;
                                    }
                                    finally
                                    {
                                        mImageFlip?.Dispose();
                                    }
                                }

                                if (bflag && mImage != null)
                                {
                                    // 回调成功接收后，Mat 的所有权转移给回调方。
                                    UseImages(mImage);
                                    mImage = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                _errMsg = ex.ToString();
                            }
                            finally
                            {
                                // 回调未接管的图像（包括异常和无效帧）必须在这里释放。
                                mImage?.Dispose();
                                MV_CC_FreeImageBuffer_NET(ref stFrameOut);
                            }
                        }
                    }
                });
                th.IsBackground = true;
                th.Start();
                return true;
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.CameraNotTurnedOn;
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
        public bool InitSet(CamParam param,bool isDebug)
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
                if (!SetGain(param.Gain))
                {
                    return false;
                }

                if (!SetAcquisitionFrameRate(param.Hz))
                {
                    return false;
                }
                if (param.Key == "Cam1")
                {
                    if (!SetAcquisitionFrameRateEnable(true))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!SetAcquisitionFrameRateEnable(false))
                    {
                        return false;
                    }
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

                if (isDebug)
                {
                    if (_manufacturerName == "Hikrobot")
                    {
                        //触发模式
                        if (!SetTriggerMode(TriggerMode.Off))
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
                    }
                    else if (_manufacturerName == "ChinaVision")
                    {
                        //触发模式
                        if (!SetTriggerMode(TriggerMode.Off))
                        {
                            return false;
                        }

                        if (!SetEnumValue("Out0Mod", (uint)0x8))//IO模式
                        {
                            return false;
                        }
                        if (!SetEnumValue("Out1Mod", (uint)0x8))//IO模式
                        {
                            return false;
                        }
                        if (!SetLine1Inverter(false))
                        {
                            return false;
                        }
                        if (!SetLine2Inverter(true))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (_manufacturerName == "Hikrobot")
                    {
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
                        if (!SetLine1Source(LineSource.ExposureStartActive))
                        {
                            return false;
                        }
                        if (!SetLine1StrobeEnable(true))
                        {
                            return false;
                        }

                        if (param.Key == "Cam1")
                        {
                            if (!SetLine2Source(LineSource.ExposureStartActive))
                            {
                                return false;
                            }
                            if (!SetLine2StrobeEnable(true))
                            {
                                return false;
                            }
                            //触发模式
                            if (!SetTriggerMode(TriggerMode.Off))
                            {
                                return false;
                            }
                        }
                        else if (param.Key == "Cam3")
                        {
                            if (!SetLine2StrobeEnable(false))
                            {
                                return false;
                            }
                            //触发模式
                            if (!SetTriggerMode(TriggerMode.On))
                            {
                                return false;
                            }
                            //触发源:软触发 7,line0 0
                            if (!SetEnumValue("TriggerSource", 0))//line0
                            {
                                return false;
                            }
                            //触发数量
                            if (!SetIntValue("AcquisitionBurstFrameCount", 1))
                            {
                                return false;
                            }

                            //触发极性:上升沿 0,下降沿 1,高电平 2,低电平 3
                            if (!SetEnumValue("TriggerActivation", 0))
                            {
                                return false;
                            }
                            //触发延迟（us）
                            if (!SetIntValue("TriggerDelay", 0))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!SetLine2StrobeEnable(false))
                            {
                                return false;
                            }
                            //触发模式
                            if (!SetTriggerMode(TriggerMode.On))
                            {
                                return false;
                            }
                            //触发源:软触发 7,line0 0
                            if (!SetEnumValue("TriggerSource", 0))//line0
                            {
                                return false;
                            }
                            //触发数量
                            if (!SetIntValue("AcquisitionBurstFrameCount", 1))
                            {
                                return false;
                            }

                            //触发极性:上升沿 0,下降沿 1,高电平 2,低电平 3
                            if (!SetEnumValue("TriggerActivation", 0))
                            {
                                return false;
                            }
                            //触发延迟（us）
                            int time = (int)(1 / param.Hz * 1000 * 1000 / 2);
                            if (!SetIntValue("TriggerDelay", time))
                            {
                                return false;
                            }
                        }
                        if (!SetLine2StrobeEnable(false))
                        {
                            return false;
                        }
                    }
                    else if (_manufacturerName == "ChinaVision")
                    {
                        #region TriggerControl
                        //触发模式
                        if (!SetTriggerMode(TriggerMode.On))
                        {
                            return false;
                        }
                        //触发间隔
                        if (!SetIntValue("TriggerInterval", 0))
                        {
                            return false;
                        }
                        if (!SetEnumValue("StrobeMode", (uint)1))//半自动
                        {
                            return false;
                        }
                        if (!SetEnumValue("StrobeEnable", (uint)1))//输出使能
                        {
                            return false;
                        }
                        if (!SetIntValue("StrobeDelay", 0))//输出延时
                        {
                            return false;
                        }
                        if (!SetIntValue("StrobeWidth", (long)param.Exposure))//输出宽度
                        {
                            return false;
                        }

                        if (param.Key == "Cam1")
                        {
                            //触发源:软触发 1,线路1 2
                            if (!SetEnumValue("TriggerSource", 1))//软触发
                            {
                                return false;
                            }
                            //触发数量
                            if (!SetIntValue("TriggerCount", uint.MaxValue))
                            {
                                return false;
                            }

                            //触发极性:上升沿 0x80000000,下降沿 0x00000000,上升或下降沿 0x00000001,HighLevel 0x80000002,LowLevel 0x00000002
                            if (!SetEnumValue("TriggerActivation", 0x00000000))
                            {
                                return false;
                            }
                            //ExtTrigJitterTime
                            if (!SetIntValue("ExtTrigJitterTime", 0))
                            {
                                return false;
                            }
                            //TriggerStartDelay
                            if (!SetIntValue("TriggerStartDelay", 0))
                            {
                                return false;
                            }
                            //触发延迟（us）
                            if (!SetIntValue("TriggerDelay", 0))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            //触发源:软触发 1,线路1 2
                            if (!SetEnumValue("TriggerSource", 2))//Line1
                            {
                                return false;
                            }
                            //触发数量
                            if (!SetIntValue("TriggerCount", 1))
                            {
                                return false;
                            }

                            //触发极性:上升沿 0x80000000,下降沿 0x00000000,上升或下降沿 0x00000001,HighLevel 0x80000002,LowLevel 0x00000002
                            if (!SetEnumValue("TriggerActivation", 0x00000000))
                            {
                                return false;
                            }
                            //ExtTrigJitterTime
                            if (!SetIntValue("ExtTrigJitterTime", 0))
                            {
                                return false;
                            }

                            // 改为根据曝光时间来设置延迟触发时间
                            //if (param.Key == "Cam3")
                            //{
                            //    //TriggerStartDelay
                            //    if (!SetIntValue("TriggerStartDelay", 0))
                            //    {
                            //        return false;
                            //    }
                            //}
                            //else
                            //{
                            //    //TriggerStartDelay
                            //    //int time = (int)(1 / param.Hz * 1000 * 1000 / 2);

                            //    int time = (int)500;
                            //    if (!SetIntValue("TriggerStartDelay", time))
                            //    {
                            //        return false;
                            //    }
                            //}


                            {
                                int CamID = int.Parse(param.Key.Substring(param.Key.Length - 1, 1));

                                //曝光的两倍加个50ms
                                int time = ((int)param.Exposure * 2 + 50) * (CamID - 1);
                                if (!SetIntValue("TriggerStartDelay", time))
                                {
                                    return false;
                                }
                            }

                            //触发延迟（us）
                            if (!SetIntValue("TriggerDelay", 0))
                            {
                                return false;
                            }
                        }
                        #endregion

                        #region 数字IO控制
                        //输入
                        if (!SetEnumValue("In0Mod", (uint)0x1))//触发模式
                        {
                            return false;
                        }

                        //输出
                        if (!SetEnumValue("Out0Mod", (uint)0x9))//闪光灯模式
                        {
                            return false;
                        }

                        if (param.Key == "Cam1")
                        {
                            if (!SetEnumValue("Out1Mod", (uint)0x9))//闪光灯模式
                            {
                                return false;
                            }
                        }
                        else
                        { 
                            if (!SetEnumValue("Out1Mod", (uint)0x8))//IO模式
                            {
                                return false;
                            }
                            if (!SetIntValue("GPOut1", 1))//板子默认0是关
                            {
                                return false;
                            }
                        }
                        #endregion
                    }
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

        public bool SetGain(float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return SetFloatValue("Gain", value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return SetFloatValue("Gain", value);
            }
            else
            {
                return false;
            }

        }
        public bool GetGain(out float value)
        {
            if (_manufacturerName == "Hikrobot")
            {
                return GetFloatValue("Gain", out value);
            }
            else if (_manufacturerName == "ChinaVision")
            {
                return GetFloatValue("Gain", out value);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.SetTheNumberOfInternalImageCachingNodesInTheSdkTo +
                    num + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
                return false;
            }
        }
        #endregion

        #region 参数获取与设置封装功能函数
        public bool TriggerSoftwareExecute()
        {
            int nRet = MV_CC_TriggerSoftwareExecute_NET();
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.SoftwareTriggerFailure + Convert.ToString(nRet, 16);
                return false;
            }
        }

        private bool SetBoolValue(string strKey, bool value)
        {
            int nRet = MV_CC_SetBoolValue_NET(strKey, value);
            if (MvCamera.MV_OK == nRet)
            {
                return true;
            }
            else
            {
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Set + strKey + _3DLaserGlueInspection.Resources.LanguageDict.To + value.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Set + strKey + _3DLaserGlueInspection.Resources.LanguageDict.To + value.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Set + strKey + _3DLaserGlueInspection.Resources.LanguageDict.To + value.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Set + strKey + _3DLaserGlueInspection.Resources.LanguageDict.To + value.ToString() + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Get + strKey + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Get + strKey + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Get + strKey + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
                _errMsg = _3DLaserGlueInspection.Resources.LanguageDict.Get + strKey + _3DLaserGlueInspection.Resources.LanguageDict.Failed + Convert.ToString(nRet, 16);
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
        public Dictionary<string, Dictionary<string, CamParam>> Param = new Dictionary<string, Dictionary<string, CamParam>>();     //相机调试参数
        public Dictionary<string, Dictionary<string, CameraParameters>> CamPar = new Dictionary<string, Dictionary<string, CameraParameters>>();    //相机内参
        public Dictionary<string, Dictionary<string, PoseParameters>> LightInCam = new Dictionary<string, Dictionary<string, PoseParameters>>();    //光平面内参
        //public Dictionary<string, Dictionary<string, PoseParameters>> SensorInCam = new Dictionary<string, Dictionary<string, PoseParameters>>();
        public Dictionary<string, Dictionary<string, PoseParameters>> ToolInCam1 = new Dictionary<string, Dictionary<string, PoseParameters>>();    //相机外参（手眼标定，其实都是相机1的）

        public Dictionary<string, Dictionary<string, PoseParameters>> CamInCam1 = new Dictionary<string, Dictionary<string, PoseParameters>>();    //转相机坐标系的外参

        public Dictionary<string, Dictionary<string, PoseParameters>> CenterInCam1 = new Dictionary<string, Dictionary<string, PoseParameters>>();    //转涂胶中心坐标系的外参 。根据相机1和3的关系计算的，因为这里主要是要坐标，因此角度是直接用了相机3相对于相机1的角度。如果只有1个相机，可以直接设置位单位矩阵

        //计算的参数
        public Dictionary<string, Dictionary<string, Mat>> LightToCam = new Dictionary<string, Dictionary<string, Mat>>();    //光平面旋转矩阵
        //public Dictionary<string, Dictionary<string, Mat>> CamToSensor = new Dictionary<string, Dictionary<string, Mat>>();
        public Dictionary<string, Dictionary<string, Mat>> Cam1ToTool = new Dictionary<string, Dictionary<string, Mat>>();    //手眼标定的旋转矩阵（其实都是相机1的，要右乘CamToCam1才是自己的）

        public Dictionary<string, Dictionary<string, Mat>> CamToCam1 = new Dictionary<string, Dictionary<string, Mat>>();    //转相机1的旋转矩阵

        public Dictionary<string, Dictionary<string, Mat>> CenterToCam1 = new Dictionary<string, Dictionary<string, Mat>>();    //转涂胶中心的旋转矩阵


        //兼容眼在手外
        public Dictionary<string, Dictionary<string, PoseParameters>> Cam1InBase = new Dictionary<string, Dictionary<string, PoseParameters>>();    //相机外参（手眼标定，眼在手外，其实都是相机1
        public Dictionary<string, Dictionary<string, Mat>> Cam1ToBase = new Dictionary<string, Dictionary<string, Mat>>();    //手眼标定的旋转矩阵（其实都是相机1的，要右乘CamToCam1才是自己的）


        public Dictionary<string, int> CamHandEyeType = new Dictionary<string, int>();    //相机手眼标定类型，0是眼在手上，1是眼在手外

        ////保存的参数
        //public Dictionary<string, Mat> SensorToTool = new Dictionary<string, Mat>();

        private void DisposeMatDictionaries(Dictionary<string, Dictionary<string, Mat>> matrices)
        {
            foreach (var group in matrices.Values)
            {
                foreach (var matrix in group.Values)
                {
                    matrix?.Dispose();
                }
            }
        }

        public bool Load()
        {
            bool results = true;
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data";
            // 这些字典持有 OpenCV 原生内存，清空前必须先释放旧值。
            DisposeMatDictionaries(LightToCam);
            DisposeMatDictionaries(Cam1ToTool);
            DisposeMatDictionaries(CamToCam1);
            DisposeMatDictionaries(CenterToCam1);
            DisposeMatDictionaries(Cam1ToBase);

            Param.Clear();
            CamPar.Clear();
            LightInCam.Clear();
            ToolInCam1.Clear();
            LightToCam.Clear();
            Cam1ToTool.Clear();
            CamInCam1.Clear();
            CamToCam1.Clear();

            CenterToCam1.Clear();
            CenterInCam1.Clear();

            CamHandEyeType.Clear();
            Cam1ToBase.Clear();
            Cam1InBase.Clear();

            //需要改变相机数量，只要修改data文件夹下的CamSet文件夹下的相机文件夹数量即可
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
                    ToolInCam1.Add(name, new Dictionary<string, PoseParameters>());
                    LightToCam.Add(name, new Dictionary<string, Mat>());
                    //CamToSensor.Add(name, new Dictionary<string, Mat>());
                    Cam1ToTool.Add(name, new Dictionary<string, Mat>());

                    CamInCam1.Add(name, new Dictionary<string, PoseParameters>());
                    CamToCam1.Add(name, new Dictionary<string, Mat>());

                    CenterInCam1.Add(name, new Dictionary<string, PoseParameters>());
                    CenterToCam1.Add(name, new Dictionary<string, Mat>());

                    CamHandEyeType.Add(name, 0);
                    Cam1ToBase.Add(name, new Dictionary<string, Mat>());
                    Cam1InBase.Add(name, new Dictionary<string, PoseParameters>());

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
                                        _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                                    }
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
                                _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                                _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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

                        //判断是眼在手上还是眼在手外
                        {
                            string paramPath = camPath + "\\ToolInCam.dat";
                            if (File.Exists(paramPath))
                            {
                                CamHandEyeType[name] = 0;
                            }
                            paramPath = camPath + "\\CamInBase.dat";
                            if (File.Exists(paramPath))
                            {
                                CamHandEyeType[name] = 1;
                            }
                        }

                        
                        bool result5 = true;

                        if (CamHandEyeType[name] == 0)
                        {
                            //眼在手上数据读取
                            try
                            {
                                string paramPath = camPath + "\\ToolInCam.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);

                                    if (ToolInCam1[name].ContainsKey(camKey))
                                    {
                                        ToolInCam1[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        ToolInCam1[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result5 = false;
                                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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

                                        if (ToolInCam1[name].ContainsKey(camKey))
                                        {
                                            ToolInCam1[name][camKey] = hWorldPose;
                                        }
                                        else
                                        {
                                            ToolInCam1[name].Add(camKey, hWorldPose);
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

                        }
                        else
                        {
                            try
                            {
                                //眼在手外数据读取

                                string paramPath = camPath + "\\CamInBase.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);

                                    if (Cam1InBase[name].ContainsKey(camKey))
                                    {
                                        Cam1InBase[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        Cam1InBase[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result5 = false;
                                    _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                                    string paramPath = camPath + "\\CamInBase_bak.dat";
                                    if (File.Exists(paramPath))
                                    {
                                        var hWorldPose = new PoseParameters();
                                        //hWorldPose.ReadPose(paramPath);
                                        hWorldPose = HFileIO.ReadPosePara(paramPath);

                                        if (Cam1InBase[name].ContainsKey(camKey))
                                        {
                                            Cam1InBase[name][camKey] = hWorldPose;
                                        }
                                        else
                                        {
                                            Cam1InBase[name].Add(camKey, hWorldPose);
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

                        }





                        bool result7 = true;
                        try
                        {
                            string paramPath = camPath + "\\CamInCam1.dat";
                            if (File.Exists(paramPath))
                            {
                                var hWorldPose = new PoseParameters();
                                //hWorldPose.ReadPose(paramPath);
                                hWorldPose = HFileIO.ReadPosePara(paramPath);

                                if (CamInCam1[name].ContainsKey(camKey))
                                {
                                    CamInCam1[name][camKey] = hWorldPose;
                                }
                                else
                                {
                                    CamInCam1[name].Add(camKey, hWorldPose);
                                }
                            }
                            else
                            {
                                result7 = false;
                                _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
                            }
                        }
                        catch (Exception ex)
                        {
                            result7 = false;
                            _errMsg = ex.ToString();
                        }
                        if (!result7)
                        {
                            result7 = true;
                            try
                            {
                                string paramPath = camPath + "\\CamInCam1_bak.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);

                                    if (CamInCam1[name].ContainsKey(camKey))
                                    {
                                        CamInCam1[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        CamInCam1[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result7 = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result7 = false;
                            }
                        }

                        bool result8 = true;
                        try
                        {
                            string paramPath = camPath + "\\CenterInCam1.dat";
                            if (File.Exists(paramPath))
                            {
                                var hWorldPose = new PoseParameters();
                                hWorldPose = HFileIO.ReadPosePara(paramPath);

                                if (CenterInCam1[name].ContainsKey(camKey))
                                {
                                    CenterInCam1[name][camKey] = hWorldPose;
                                }
                                else
                                {
                                    CenterInCam1[name].Add(camKey, hWorldPose);
                                }
                            }
                            else
                            {
                                result8 = false;
                                _errMsg = paramPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
                            }
                        }
                        catch (Exception ex)
                        {
                            result8 = false;
                            _errMsg = ex.ToString();
                        }
                        if (!result8)
                        {
                            result8 = true;
                            try
                            {
                                string paramPath = camPath + "\\CenterInCam1_bak.dat";
                                if (File.Exists(paramPath))
                                {
                                    var hWorldPose = new PoseParameters();
                                    //hWorldPose.ReadPose(paramPath);
                                    hWorldPose = HFileIO.ReadPosePara(paramPath);

                                    if (CenterInCam1[name].ContainsKey(camKey))
                                    {
                                        CenterInCam1[name][camKey] = hWorldPose;
                                    }
                                    else
                                    {
                                        CenterInCam1[name].Add(camKey, hWorldPose);
                                    }
                                }
                                else
                                {
                                    result8 = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                result8 = false;
                            }
                        }

                        bool result6 = true;
                        // pose转mat
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

                            if (CamHandEyeType[name] == 0)
                            {
                                if (ToolInCam1[name].ContainsKey(camKey))
                                {
                                    if (Cam1ToTool[name].ContainsKey(camKey))
                                    {
                                        //CamToTool[name][camKey] = ToolInCam[name][camKey].PoseInvert().PoseToHomMat3d();
                                        using (Mat H = new Mat())
                                        {
                                            Vision.poseToHomMat3d(ToolInCam1[name][camKey].PoseType,
                                                ToolInCam1[name][camKey].x, ToolInCam1[name][camKey].y,
                                                ToolInCam1[name][camKey].z, ToolInCam1[name][camKey].rx,
                                                ToolInCam1[name][camKey].ry, ToolInCam1[name][camKey].rz, H.CvPtr);
                                            // 这里必须要求逆才行，因为标定的是相机坐标系下的法兰盘位姿。
                                            Mat oldMatrix = Cam1ToTool[name][camKey];
                                            oldMatrix?.Dispose();
                                            Cam1ToTool[name][camKey] = H.Inv();
                                        }
                                    }
                                    else
                                    {
                                        using (Mat H = new Mat())
                                        {
                                            Vision.poseToHomMat3d(ToolInCam1[name][camKey].PoseType,
                                                ToolInCam1[name][camKey].x, ToolInCam1[name][camKey].y,
                                                ToolInCam1[name][camKey].z, ToolInCam1[name][camKey].rx,
                                                ToolInCam1[name][camKey].ry, ToolInCam1[name][camKey].rz, H.CvPtr);
                                            Cam1ToTool[name].Add(camKey, H.Inv());
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (Cam1InBase[name].ContainsKey(camKey))
                                {
                                    if (Cam1ToBase[name].ContainsKey(camKey))
                                    {
                                        //CamToTool[name][camKey] = ToolInCam[name][camKey].PoseInvert().PoseToHomMat3d();
                                        Mat H = new Mat();
                                        Vision.poseToHomMat3d(Cam1InBase[name][camKey].PoseType, Cam1InBase[name][camKey].x, Cam1InBase[name][camKey].y, Cam1InBase[name][camKey].z,
                                            Cam1InBase[name][camKey].rx, Cam1InBase[name][camKey].ry, Cam1InBase[name][camKey].rz, H.CvPtr);
                                        Cam1ToBase[name][camKey] = H;
                                    }
                                    else
                                    {
                                        Mat H = new Mat();
                                        Vision.poseToHomMat3d(Cam1InBase[name][camKey].PoseType, Cam1InBase[name][camKey].x, Cam1InBase[name][camKey].y, Cam1InBase[name][camKey].z,
                                            Cam1InBase[name][camKey].rx, Cam1InBase[name][camKey].ry, Cam1InBase[name][camKey].rz, H.CvPtr);
                                        Cam1ToBase[name].Add(camKey, H);
                                    }
                                }
                            }
                            

                            if (CenterInCam1[name].ContainsKey(camKey))
                            {
                                if (CenterToCam1[name].ContainsKey(camKey))
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(CenterInCam1[name][camKey].PoseType, CenterInCam1[name][camKey].x, CenterInCam1[name][camKey].y, CenterInCam1[name][camKey].z,
                                        CenterInCam1[name][camKey].rx, CenterInCam1[name][camKey].ry, CenterInCam1[name][camKey].rz, H.CvPtr);
                                    CenterToCam1[name][camKey] = H;
                                }
                                else
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(CenterInCam1[name][camKey].PoseType, CenterInCam1[name][camKey].x, CenterInCam1[name][camKey].y, CenterInCam1[name][camKey].z,
                                        CenterInCam1[name][camKey].rx, CenterInCam1[name][camKey].ry, CenterInCam1[name][camKey].rz, H.CvPtr);
                                    CenterToCam1[name].Add(camKey, H);
                                }
                            }


                            if (CamInCam1[name].ContainsKey(camKey))
                            {
                                if (CamToCam1[name].ContainsKey(camKey))
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(CamInCam1[name][camKey].PoseType, CamInCam1[name][camKey].x, CamInCam1[name][camKey].y, CamInCam1[name][camKey].z,
                                        CamInCam1[name][camKey].rx, CamInCam1[name][camKey].ry, CamInCam1[name][camKey].rz, H.CvPtr);
                                    CamToCam1[name][camKey] = H;
                                }
                                else
                                {
                                    Mat H = new Mat();
                                    Vision.poseToHomMat3d(CamInCam1[name][camKey].PoseType, CamInCam1[name][camKey].x, CamInCam1[name][camKey].y, CamInCam1[name][camKey].z,
                                        CamInCam1[name][camKey].rx, CamInCam1[name][camKey].ry, CamInCam1[name][camKey].rz, H.CvPtr);
                                    CamToCam1[name].Add(camKey, H);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result6 = false;
                            _errMsg = ex.ToString();
                        }


                       

                        if (!result || !result2 || !result3 || !result4 || !result5 || !result6 || !result7)
                        {
                            results = false;
                        }
                    }
                }
            }
            else
            {
                _errMsg = camSetPath + _3DLaserGlueInspection.Resources.LanguageDict.TheFolderDoesNotExist;
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

                foreach (var name in CamInCam1.Keys)
                {
                    foreach (var key in CamInCam1[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        string paramPath = $"{path}\\CamInCam1.dat";
                        HFileIO.WritePosePara(paramPath, CamInCam1[name][key]);
                        File.Copy(paramPath, path + "\\CamInCam1_bak.dat", true);
                    }
                }

                foreach (var name in CenterInCam1.Keys)
                {
                    foreach (var key in CenterInCam1[name].Keys)
                    {
                        string path = $"{basePath}\\CamSet\\{name}\\{key}";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        string paramPath = $"{path}\\CenterInCam1.dat";
                        HFileIO.WritePosePara(paramPath, CenterInCam1[name][key]);
                        File.Copy(paramPath, path + "\\CenterInCam1_bak.dat", true);
                    }
                }

                foreach (var name in CamHandEyeType.Keys)
                {
                    if (CamHandEyeType[name] == 0)
                    {
                        foreach (var key in ToolInCam1[name].Keys)
                        {
                            string path = $"{basePath}\\CamSet\\{name}\\{key}";
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }

                            string paramPath = $"{path}\\ToolInCam.dat";
                            //ToolInCam[name][key].WritePose(paramPath);
                            HFileIO.WritePosePara(paramPath, ToolInCam1[name][key]);
                            File.Copy(paramPath, path + "\\ToolInCam_bak.dat", true);
                        }
                    }
                    else
                    {
                        foreach (var key in Cam1InBase[name].Keys)
                        {
                            string path = $"{basePath}\\CamSet\\{name}\\{key}";
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }
                            string paramPath = $"{path}\\CamInBase.dat";
                            HFileIO.WritePosePara(paramPath, Cam1InBase[name][key]);
                            File.Copy(paramPath, path + "\\CamInBase_bak.dat", true);
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

            if (ToolInCam1.ContainsKey(source) && ToolInCam1[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in ToolInCam1[source].Keys)
                {
                    pairs.Add(key, ToolInCam1[source][key].Clone());
                }
                if (ToolInCam1.ContainsKey(target))
                {
                    ToolInCam1[target] = pairs;
                }
                else
                {
                    ToolInCam1.Add(target, pairs);
                }
            }
            if (Cam1InBase.ContainsKey(source) && Cam1InBase[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in Cam1InBase[source].Keys)
                {
                    pairs.Add(key, Cam1InBase[source][key].Clone());
                }
                if (Cam1InBase.ContainsKey(target))
                {
                    Cam1InBase[target] = pairs;
                }
                else
                {
                    Cam1InBase.Add(target, pairs);
                }
            }

            if (CamHandEyeType.ContainsKey(source))
            {
                if (CamHandEyeType.ContainsKey(target))
                {
                    CamHandEyeType[target] = CamHandEyeType[source];
                }
                else
                {
                    CamHandEyeType.Add(target, CamHandEyeType[source]);
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
            if (Cam1ToTool.ContainsKey(source) && Cam1ToTool[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in Cam1ToTool[source].Keys)
                {
                    pairs.Add(key, Cam1ToTool[source][key].Clone());
                }
                if (Cam1ToTool.ContainsKey(target))
                {
                    Cam1ToTool[target] = pairs;
                }
                else
                {
                    Cam1ToTool.Add(target, pairs);
                }
            }
            if (Cam1ToBase.ContainsKey(source) && Cam1ToBase[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in Cam1ToBase[source].Keys)
                {
                    pairs.Add(key, Cam1ToBase[source][key].Clone());
                }
                if (Cam1ToBase.ContainsKey(target))
                {
                    Cam1ToBase[target] = pairs;
                }
                else
                {
                    Cam1ToBase.Add(target, pairs);
                }
            }


            if (CamInCam1.ContainsKey(source) && CamInCam1[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in CamInCam1[source].Keys)
                {
                    pairs.Add(key, CamInCam1[source][key].Clone());
                }
                if (CamInCam1.ContainsKey(target))
                {
                    CamInCam1[target] = pairs;
                }
                else
                {
                    CamInCam1.Add(target, pairs);
                }
            }

            if (CamToCam1.ContainsKey(source) && CamToCam1[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in CamToCam1[source].Keys)
                {
                    pairs.Add(key, CamToCam1[source][key].Clone());
                }
                if (CamToCam1.ContainsKey(target))
                {
                    CamToCam1[target] = pairs;
                }
                else
                {
                    CamToCam1.Add(target, pairs);
                }
            }


            if (CenterInCam1.ContainsKey(source) && CenterInCam1[source] != null)
            {
                Dictionary<string, PoseParameters> pairs = new Dictionary<string, PoseParameters>();
                foreach (var key in CenterInCam1[source].Keys)
                {
                    pairs.Add(key, CenterInCam1[source][key].Clone());
                }
                if (CenterInCam1.ContainsKey(target))
                {
                    CenterInCam1[target] = pairs;
                }
                else
                {
                    CenterInCam1.Add(target, pairs);
                }
            }

            if (CenterToCam1.ContainsKey(source) && CenterToCam1[source] != null)
            {
                Dictionary<string, Mat> pairs = new Dictionary<string, Mat>();
                foreach (var key in CenterToCam1[source].Keys)
                {
                    pairs.Add(key, CenterToCam1[source][key].Clone());
                }
                if (CenterToCam1.ContainsKey(target))
                {
                    CenterToCam1[target] = pairs;
                }
                else
                {
                    CenterToCam1.Add(target, pairs);
                }
            }
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
        public float Gain = 2.5f;
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