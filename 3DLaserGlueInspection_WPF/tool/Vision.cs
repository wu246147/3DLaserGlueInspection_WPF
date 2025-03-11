//using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using OpenCvSharp;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection
{
    public class Vision
    {
        private const string DllName = "RaivasAlgTransform.dll"; // Replace with the actual DLL 
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int poseToHomMat3d(int PoseType, double x, double y, double z, double rx, double ry, double rz, IntPtr transformMat);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int affineTransPoint3d(IntPtr srcPoints, IntPtr transformPoints, IntPtr transformMat);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]

        public static extern int imagePointsToWorldPlane(double Focus, double Kappa, double CamSx, double CamSy, double CamCx, double CamCy,
        int PoseType, double PoseX, double PoseY, double PoseZ, double PoseRx, double PoseRy, double PoseRz,
        IntPtr srcPoints, IntPtr transformPoints);

        private const string DllName2 = "RaivasAlgGB.dll"; // Replace with the actual DLL 
        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        public static extern int thinning(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat);
        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        public static extern int singleFrameDet(IntPtr inputPointMat, out bool existGlue, out double centerX, out double centerY,
            out double phi, out double width, out double height);
        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]

        public static extern int trajectoryDiscreteFilter(IntPtr inputPointMat, double distThre, double segmentalThre, IntPtr pointsFilterMat);


        /// <summary>
        /// 获取激光位置
        /// </summary>
        /// <param name="Image"></param>
        /// <param name="minThreshold"></param>
        /// <param name="outlinePoints"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void getLaserPosition(Mat Image, double minThreshold,out Mat outlinePoints, int offsetX = 0, int offsetY = 0)
        {
            Mat outImage = new Mat();
            outlinePoints = new Mat();
            thinning(Image.CvPtr, outImage.CvPtr, outlinePoints.CvPtr);

            if (offsetX != 0)
            {
                for (int i = 0; i < outlinePoints.Cols; i++)
                {
                    outlinePoints.At<double>(0, i) += offsetX;
                }
            }
            if (offsetY != 0)
            {
                for (int i = 0; i < outlinePoints.Cols; i++)
                {
                    outlinePoints.At<double>(1, i) += offsetY;
                }
            }

        }



        /// <summary>
        /// 输入像素坐标，输出物理坐标xy
        /// </summary>
        public void GetXY(CameraParameters hCamPar, PoseParameters hWorldPose, Mat srcPoints, out Mat transformPoints, bool 反转X = false, bool 反转Y = false)
        {
            transformPoints = new Mat();

            //hCamPar.ImagePointsToWorldPlane(hWorldPose, new HTuple(ys), new HTuple(xs), "m", out hx, out hy);


            imagePointsToWorldPlane(hCamPar.Focus, hCamPar.Kappa, hCamPar.Sx, hCamPar.Sy, hCamPar.Cx, hCamPar.Cy, hWorldPose.PoseType, hWorldPose.x, hWorldPose.y, hWorldPose.z,
                hWorldPose.rx, hWorldPose.ry, hWorldPose.rz, srcPoints.CvPtr, transformPoints.CvPtr);

            if (反转X)
            {
                for (int i = 0; i < transformPoints.Cols; i++)
                {
                    transformPoints.At<double>(0, i) *= -1;
                }
            }
            if (反转Y)
            {
                for (int i = 0; i < transformPoints.Cols; i++)
                {
                    transformPoints.At<double>(1, i) *= -1;
                }
            }


        }


        public void getOutline(Mat srcPoints, out Mat transformPoints, double distThre, int segmentalThre)
        {
            transformPoints = new Mat();

            trajectoryDiscreteFilter(srcPoints.CvPtr, distThre, segmentalThre, transformPoints.CvPtr);


        }

        public void RunRegion(Mat hRegion, ImageSet imageSet, out RotatedRect hRegionGenRectangle2, out Data data, out bResult bResult)
        {
            data = new Data();
            bResult = new bResult();
            hRegionGenRectangle2 = Cv2.MinAreaRect(hRegion);
            ////hRegion.SmallestRectangle2(out data.row, out data.column, out double phi, out double length1, out double length2);
            //hRegionGenRectangle2 = new Rect();
            //hRegionGenRectangle2.GenRectangle2(data.row, data.column, phi, length1, length2);

            bool heng = Math.Abs(hRegionGenRectangle2.Angle * Math.PI / 180) <= Math.PI / 4;
            data.胶高 = (heng ? hRegionGenRectangle2.Size.Height : hRegionGenRectangle2.Size.Width) / 100d * 2;
            data.胶宽 = (heng ? hRegionGenRectangle2.Size.Width : hRegionGenRectangle2.Size.Height) / 100d * 2;
            data.面积 = hRegion.Width * hRegion.Height / 10000d;
            if (data.胶高 >= imageSet.heightMin && data.胶高 <= imageSet.heightMax)
            {
                bResult.胶高 = true;
            }
            if (data.胶宽 >= imageSet.widthMin && data.胶宽 <= imageSet.widthMax)
            {
                bResult.胶宽 = true;
            }
            if (data.面积 >= imageSet.areaMin && data.面积 <= imageSet.areaMax)
            {
                bResult.面积 = true;
            }
            if (bResult.胶高 && bResult.胶宽 && bResult.面积)
            {
                bResult.Result = true;
            }
        }
    }
    public struct Point3D
    {
        public double X;
        public double Y;
        public double Z;
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class Setting
    {
        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;

        public string Name;
        /// <summary>
        /// 各段参数
        /// </summary>
        public List<CutSet> CutSets = new List<CutSet>();

        //其他参数
        public OtherSet OtherSet = new OtherSet();

        //数模图
        public Mat image;
        public List<XLDData> XLDDatas = new List<XLDData>();

        public Setting(string name)
        {
            this.Name = name;
        }

        public bool Load()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "\\";
            if (!Load(basePath))
            {
                string err = _errMsg;
                string basePath_bak = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "_bak\\";
                if (!Load(basePath_bak))
                {
                    _errMsg = err;
                    return false;
                }
                else
                {
                    CopyDirectory(basePath_bak, basePath);
                }
            }
            return true;
        }

        private bool Load(string basePath)
        {
            _errMsg = string.Empty;
            bool result0 = true;
            try
            {
                string fPath = basePath + "OtherSet.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(OtherSet.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        OtherSet = (OtherSet)xml.Deserialize(stream);
                    }
                    if (OtherSet == null)
                    {
                        OtherSet = new OtherSet();
                        result0 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result0 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result0 = false;
            }

            bool result1 = true;
            try
            {
                CutSets = new List<CutSet>();
                string fPath = basePath + "CutSet.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(CutSets.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        CutSets = (List<CutSet>)xml.Deserialize(stream);
                    }
                    if (CutSets == null)
                    {
                        CutSets = new List<CutSet>();
                        result1 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result1 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result1 = false;
            }

            bool result2 = true;
            try
            {
                string fPath = basePath + "Image.png";
                if (File.Exists(fPath))
                {
                    image = new Mat(fPath, (ImreadModes)(-1));
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result2 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result2 = false;
            }

            bool result3 = true;
            try
            {
                XLDDatas = new List<XLDData>();
                string fPath = basePath + "XLDData.xml";
                if (File.Exists(fPath))
                {
                    XmlSerializer xml = new XmlSerializer(XLDDatas.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Open))
                    {
                        XLDDatas = (List<XLDData>)xml.Deserialize(stream);
                    }
                    if (XLDDatas == null)
                    {
                        XLDDatas = new List<XLDData>();
                        result3 = false;
                        _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常");
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + GlobalVarAndFunc.LanguageTranslate("文件不存在");
                    result3 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result3 = false;
            }

            return result0 && result1 && result2 && result3;
        }

        public bool Save()
        {
            bool result = true;

            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "\\";
            try
            {
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                {
                    string fPath = basePath + "OtherSet.xml";
                    XmlSerializer xml = new XmlSerializer(OtherSet.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, OtherSet);
                    }
                }
                {
                    string fPath = basePath + "CutSet.xml";
                    XmlSerializer xml = new XmlSerializer(CutSets.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, CutSets);
                    }
                }
                {
                    string fPath = basePath + "Image.png";
                    if (!image.Empty())
                    {
                        Cv2.ImWrite(fPath, image);
                    }
                }
                {
                    string fPath = basePath + "XLDData.xml";
                    XmlSerializer xml = new XmlSerializer(XLDDatas.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, XLDDatas);
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }

            if (result)
            {
                string destPath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\" + Name + "_bak";
                if (Directory.Exists(destPath))
                {
                    Directory.Delete(destPath, true);
                }
                CopyDirectory(basePath, destPath);
            }

            return result;
        }

        private void CopyDirectory(string sourcePath, string destPath)
        {
            string floderName = Path.GetFileName(sourcePath);
            DirectoryInfo di = Directory.CreateDirectory(Path.Combine(destPath, floderName));
            string[] files = Directory.GetFileSystemEntries(sourcePath);

            foreach (string file in files)
            {
                if (Directory.Exists(file))
                {
                    CopyDirectory(file, di.FullName);
                }
                else
                {
                    File.Copy(file, Path.Combine(di.FullName, Path.GetFileName(file)), true);
                }
            }
        }
    }

    [Serializable]
    public class OtherSet
    {
        //保存图片
        public bool SaveNGImage = true;
        public bool SaveOKImage = true;
        public string SaveImagePath = "D:\\image";
    }

    [Serializable]
    public class CutSet
    {
        public string Name;
        //图像数量
        public int ImageNum = 0;
        //相机启用情况
        public bool Cam1Enabled = true;
        public bool Cam2Enabled = true;
        public bool Cam3Enabled = true;
        public bool Cam4Enabled = true;
        //显示画布大小
        public int ShowWidth = 50;//mm
        public int ShowHeight = 50;
        //3D颜色范围
        public double ShowColorMax = 100;//mm
        public double ShowColorMin = -100;
        //标识大小
        public int Size = 3;
        public int StartImageIndex = 0;
        public int EndImageIndex = 0;

        /// <summary>
        /// 各相机-图片参数
        /// </summary>
        public List<List<ImageSet>> imageSet = new List<List<ImageSet>>();
        public CutSet(string name)
        {
            this.Name = name;
        }
        CutSet() { }
        public CutSet Clone()
        {
            CutSet clone = (CutSet)this.MemberwiseClone();
            clone.imageSet = new List<List<ImageSet>>();
            for (int i = 0; i < imageSet.Count; i++)
            {
                clone.imageSet.Add(new List<ImageSet>());
                for (int j = 0; j < imageSet[i].Count; j++)
                {
                    clone.imageSet[i].Add(imageSet[i][j].Clone());
                }
            }
            return clone;
        }
    }

    [Serializable]
    public class ImageSet
    {
        public int Index;
        //图像启用情况
        public bool 轮廓检测 = false;
        public double minThreshold = 40;
        public bool 单帧检测 = false;
        public double widthMin = 2, widthMax = 4;
        public double heightMin = 2, heightMax = 4;
        public double areaMin = 4, areaMax = 16;
        public bool 启用裁剪 = false;
        public double LeftX = 0.25, TopY = 0.25, RightX = 0.75, DownY = 0.75;
        public bool 离散去噪 = false;
        public double 分段距离 = 1.5;
        public int 成段点数 = 3;
        public bool 拐点分段 = false;
        public double 分段弧度 = 0.070;
        public double 弧度分段距离 = 2;

        public ImageSet(int index)
        {
            this.Index = index;
        }
        ImageSet() { }
        public ImageSet Clone() { return (ImageSet)this.MemberwiseClone(); }
    }

    [Serializable]
    public class XLDData
    {
        public string Name;
        //public int step = 5, halfLength = 30, halfWidth = 3, threshold = 100;
        public double[] ControlRows, ControlCols, Knots;
        public double[] Rows, Cols, Tangents;

        public XLDData(string name)
        {
            ControlRows = new double[0];
            ControlCols = new double[0];
            Knots = new double[0];
            Rows = new double[0];
            Cols = new double[0];
            Tangents = new double[0];
            this.Name = name;
        }
        XLDData() { }
        public XLDData Clone() { return (XLDData)this.MemberwiseClone(); }
    }

    [Serializable]
    public class Data
    {
        public double row, column;
        public double 胶高;
        public double 胶宽;
        public double 面积;
    }
    [Serializable]
    public class bResult
    {
        public bool 胶高;
        public bool 胶宽;
        public bool 面积;
        /// <summary>
        /// 总结果
        /// </summary>
        public bool Result;
    }
}
