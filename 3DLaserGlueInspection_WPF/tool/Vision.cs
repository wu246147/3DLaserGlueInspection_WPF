//using HalconDotNet;
using _3DLaserGlueInspection.subForm;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection
{
    using Newtonsoft.Json;
    using System.Runtime.Serialization;

    public class ProjectionMapperPersistence
    {
        public static void SaveToFile(double[,] matrix, string filePath)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            double[][] jagged = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                jagged[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                    jagged[i][j] = matrix[i, j];
            }

            string json = JsonConvert.SerializeObject(jagged, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static double[,] LoadFromFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            double[][] jagged = JsonConvert.DeserializeObject<double[][]>(json);

            int rows = jagged.Length;
            int cols = jagged[0].Length;
            double[,] matrix = new double[rows, cols];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    matrix[i, j] = jagged[i][j];

            return matrix;
        }
    }

    /// <summary>
    /// 基于轨迹弧长参数化的 3D→2D 映射。
    /// 不拟合投影矩阵，不需要中间点一一对应，只要求首尾对应。
    /// </summary>
    public class ProjectionMapper
    {
        private List<double[]> _pts3D;   // 3D 轨迹点
        private List<double[]> _pts2D;   // 2D 轨迹点
        private double[] _t3D;           // 3D 弧长参数 [0,1]
        private double[] _t2D;           // 2D 弧长参数 [0,1]
        private bool _ready;

        // ══════════════════════════════════════════════════════
        //  公开方法
        // ══════════════════════════════════════════════════════

        public List<double[]> get3DPoint()
        {
            return _pts3D;
        }

        public List<double[]> getMapping2DPoint()
        {
            if(!_ready)
                throw new InvalidOperationException("请先调用 Calibrate()");
            return To2D(_pts3D);
        }


        public List<double[]> get2DPoint()
        {
            return _pts2D;
        }


        public bool isCalib()
        {
            return _ready;
        }

        /// <summary>
        /// 标定：传入 3D 轨迹和 2D 轨迹（首尾自动对应）。
        /// </summary>
        public void Calibrate(List<double[]> pose3D, double[] controlRows, double[] controlCols)
        {
            if (pose3D == null || pose3D.Count < 2)
                throw new ArgumentException("pose3D 至少需要 2 个点");
            if (controlRows == null || controlCols == null || controlRows.Length < 2)
                throw new ArgumentException("controlRows/controlCols 至少需要 2 个点");
            if (controlRows.Length != controlCols.Length)
                throw new ArgumentException("controlRows 与 controlCols 长度不一致");

            // 构建 2D 点列表 (x, y) = (col, row)
            _pts2D = new List<double[]>(controlRows.Length);
            for (int i = 0; i < controlRows.Length; i++)
                _pts2D.Add(new double[] { controlCols[i], controlRows[i] });

            _pts3D = new List<double[]>(pose3D);

            // 分别做弧长参数化
            _t3D = ComputeArcLength(_pts3D);
            _t2D = ComputeArcLength(_pts2D);

            _ready = true;
        }


        /// <summary>
        /// 将一个 3D 点映射到 2D 坐标。
        /// 通过在 3D 轨迹上找最近点获取弧长参数，再在 2D 轨迹上插值。
        /// </summary>
        public double[] To2D(double[] point3D)
        {
            if (!_ready)
                throw new InvalidOperationException("请先调用 Calibrate()");
            if (point3D == null || point3D.Length < 3)
                throw new ArgumentException("point3D 至少包含 3 个元素");

            // 1. 在 3D 轨迹上找最近点，得到弧长参数 t
            int nearestIdx = FindNearestIndex(point3D, _pts3D);
            double t = _t3D[nearestIdx];

            // 2. 用 t 在 2D 轨迹上插值
            return Interpolate2D(t, _t2D, _pts2D);
        }

        /// <summary>
        /// 批量映射。
        /// </summary>
        public List<double[]> To2D(List<double[]> points3D)
        {
            var result = new List<double[]>(points3D.Count);
            foreach (var p in points3D)
                result.Add(To2D(p));
            return result;
        }

        // ══════════════════════════════════════════════════════
        //  序列化（Newtonsoft.Json）
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 保存标定数据到文件。
        /// </summary>
        public bool SaveToFile(string filePath)
        {
            try
            {
                if (!_ready)
                    return false;

                var data = new MapperData
                {
                    Pts3D = _pts3D,
                    Pts2D = _pts2D,
                    T3D = _t3D,
                    T2D = _t2D
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
                }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从文件恢复标定数据。
        /// </summary>
        public bool LoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<MapperData>(json);

                _pts3D = data.Pts3D;
                _pts2D = data.Pts2D;
                _t3D = data.T3D;
                _t2D = data.T2D;
                _ready = true;
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════
        //  内部方法
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 计算点序列的归一化弧长参数 [0, 1]。
        /// </summary>
        private double[] ComputeArcLength(List<double[]> pts)
        {
            int n = pts.Count;
            double[] cum = new double[n];
            int dim = Math.Min(pts[0].Length, 3);

            for (int i = 1; i < n; i++)
            {
                double sumSq = 0.0;
                for (int d = 0; d < dim; d++)
                {
                    double diff = pts[i][d] - pts[i - 1][d];
                    sumSq += diff * diff;
                }
                cum[i] = cum[i - 1] + Math.Sqrt(sumSq);
            }

            double total = cum[n - 1];
            double[] t = new double[n];

            if (total < 1e-12)
            {
                // 所有点重合，均匀分布
                for (int i = 0; i < n; i++)
                    t[i] = (n > 1) ? (double)i / (n - 1) : 0.0;
            }
            else
            {
                for (int i = 0; i < n; i++)
                    t[i] = cum[i] / total;
            }

            return t;
        }

        /// <summary>
        /// 在点序列上按弧长参数 t 线性插值。
        /// </summary>
        private double[] Interpolate2D(double t, double[] tVals, List<double[]> pts)
        {
            int n = tVals.Length;

            // 边界处理
            if (t <= tVals[0])
                return new double[] { pts[0][0], pts[0][1] };
            if (t >= tVals[n - 1])
                return new double[] { pts[n - 1][0], pts[n - 1][1] };

            // 查找 t 所在的区间 [tVals[i], tVals[i+1]]
            for (int i = 0; i < n - 1; i++)
            {
                if (t >= tVals[i] && t <= tVals[i + 1])
                {
                    double segLen = tVals[i + 1] - tVals[i];
                    double alpha = (segLen < 1e-12) ? 0.0 : (t - tVals[i]) / segLen;

                    return new double[]
                    {
                    pts[i][0] + alpha * (pts[i + 1][0] - pts[i][0]),
                    pts[i][1] + alpha * (pts[i + 1][1] - pts[i][1])
                    };
                }
            }

            return new double[] { pts[n - 1][0], pts[n - 1][1] };
        }

        /// <summary>
        /// 在点列表中找距 query 最近的点，返回索引。
        /// </summary>
        private int FindNearestIndex(double[] query, List<double[]> pts)
        {
            int bestIdx = 0;
            double bestDist = double.MaxValue;

            for (int i = 0; i < pts.Count; i++)
            {
                double dist = DistanceSq(query, pts[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private double DistanceSq(double[] a, double[] b)
        {
            int dim = Math.Min(a.Length, b.Length);
            double sum = 0.0;
            for (int d = 0; d < dim; d++)
            {
                double diff = a[d] - b[d];
                sum += diff * diff;
            }
            return sum;
        }

        // ══════════════════════════════════════════════════════
        //  序列化数据结构
        // ══════════════════════════════════════════════════════

        private class MapperData
        {
            public List<double[]> Pts3D { get; set; }
            public List<double[]> Pts2D { get; set; }
            public double[] T3D { get; set; }
            public double[] T2D { get; set; }
        }
    }

    public class Vision
    {
        private const string DllName = "dll\\RaivasAlgTransform.dll"; // Replace with the actual DLL 
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        ///pose转旋转矩阵
        public static extern int poseToHomMat3d(int PoseType, double x, double y, double z, double rx, double ry, double rz, IntPtr transformMat);


        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        ///旋转矩阵转pose
        public static extern int HomMat3dToPose(int PoseType, out double x, out double y, out double z, out double rx, out double ry, out double rz, IntPtr transformMat);


        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        ///坐标映射

        public static extern int affineTransPoint3d(IntPtr srcPoints, IntPtr transformMat, IntPtr transformPoints,bool isDebug = false);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        ///图像坐标转相机坐标

        public static extern int imagePointsToWorldPlane(double Focus, double Kappa, double CamSx, double CamSy, double CamCx, double CamCy,
        int PoseType, double PoseX, double PoseY, double PoseZ, double PoseRx, double PoseRy, double PoseRz,
        IntPtr srcPoints, IntPtr transformPoints);

        private const string DllName2 = "dll\\RaivasAlgGB.dll"; // Replace with the actual DLL 

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///激光提取函数
        public static extern int thinning(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///激光提取函数,亚像素
        public static extern int thinningD(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat, int min_thre, int min_width = 1);
        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///单帧检测

        public static extern int singleFrameDet(IntPtr inputPointMat, out bool existGlue, out double centerX, out double centerY,
            out double phi, out double width, out double height, out double maxArea, IntPtr outMaxRegion, IntPtr outRegionRectangle2, bool angleProcess = true, bool isDebug = false);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///离散滤波

        public static extern int trajectoryDiscreteFilter(IntPtr inputPointMat, double distThre, int segmentalThre, IntPtr pointsFilterMat, bool isDebug = false);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///将多段线划分成指定点数的线段

        public static extern int dividePolyline(IntPtr polyline, int divideCout, IntPtr dividedPoints);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///3d点云切割成2d图片

        public static extern int pointCloudCutAll(IntPtr pointCloud, IntPtr robotPose, double xSize, double ySize, double zSize, double scale_size, double offset_z, IntPtr[] cutImgsPtr);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///3d点云切割成2d图片

        public static extern int pointCloudCutSingle(IntPtr pointCloud, IntPtr robotPose,int poseID, double xSize, double ySize, double zSize, double scale_size, double offset_z, IntPtr cutImg);


        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///激光提取函数，对3d数据进行处理

        public static extern int thinning3d(IntPtr img, IntPtr thinn, IntPtr pointsMat);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        // 机器人移动方向与相机方向夹角
        ///
        /// \brief robotAndCamVectorAngle 根据机器人移动轨迹和相机的位姿来计算机器人移动向量和相机取像向量的夹角
        /// \param robotPoses 机器人移动前后两个点（2，7），包括x，y，z rx ry rz rt
        /// \param Cam2Tool 相机到法兰盘的旋转矩阵
        /// \param axisType 所计算是哪个相机轴的夹角，0为x，1为y，2为z。正常就是z。
        /// \param planeType 所计算的夹角是基于哪个平面（这个平面是世界坐标的平面），0为xy平面，1为xz平面，2为yz平面。这个需要根据检测产品在机器人坐标系的哪个平面下进行判断,正常是xy平面。
        /// \param angle    返回的夹角。
        /// \return
        ///
        public static extern int robotAndCamVectorAngle(IntPtr robotPose, IntPtr Cam2Tool, int axisType, int planeType, out double angle);

        /// <summary>
        /// 输入机器人前后帧的位姿，以及一些相机参数，算出机器人前后的移动方向与相机姿态的夹角
        /// </summary>
        /// <param name="CamHandEyeType"></param>
        /// <param name="CamToCam1"></param>
        /// <param name="CenterToCam1"></param>
        /// <param name="Cam1ToBase"></param>
        /// <param name="CamToTool"></param>
        /// <param name="robotPose"></param>
        /// <param name="lastRobotPose"></param>
        /// <returns></returns>
        public static double GetRobotAndCamAngle(int CamHandEyeType, Mat CamToCam1, Mat CenterToCam1, Mat Cam1ToBase, Mat CamToTool, PoseParameters robotPose, PoseParameters lastRobotPose, User3DShowControl_V user3DShowControl_V = null)
        {
            double robotAndCamAngle;
            if (CamHandEyeType == 0)
            {
                Mat robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                robotPoseMat.At<double>(0, 0) = lastRobotPose.x;
                robotPoseMat.At<double>(0, 1) = lastRobotPose.y;
                robotPoseMat.At<double>(0, 2) = lastRobotPose.z;
                robotPoseMat.At<double>(0, 3) = lastRobotPose.rx;
                robotPoseMat.At<double>(0, 4) = lastRobotPose.ry;
                robotPoseMat.At<double>(0, 5) = lastRobotPose.rz;
                robotPoseMat.At<double>(0, 6) = lastRobotPose.PoseType;

                robotPoseMat.At<double>(1, 0) = robotPose.x;
                robotPoseMat.At<double>(1, 1) = robotPose.y;
                robotPoseMat.At<double>(1, 2) = robotPose.z;
                robotPoseMat.At<double>(1, 3) = robotPose.rx;
                robotPoseMat.At<double>(1, 4) = robotPose.ry;
                robotPoseMat.At<double>(1, 5) = robotPose.rz;
                robotPoseMat.At<double>(1, 6) = robotPose.PoseType;

                Mat ToolToBase = new Mat();
                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);

                Mat CamToBase = ToolToBase * CamToTool;

                Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToBase.CvPtr, 2, 0, out robotAndCamAngle);
                //大于90的，都取缩小后的值
                if (robotAndCamAngle > 90)
                {
                    robotAndCamAngle = 180 - robotAndCamAngle;
                }
            }
            else
            {
                Mat camInTools = Mat.Zeros(2, 7, MatType.CV_64FC1);
                //这里直接使用Cam2Tool，后面可以使用Center2Tool
                //轨迹的前一个点，要转成center2Tool
                {
                    Mat ToolToBase = new Mat();
                    Mat BaseToTool;
                    Mat CenterToTool;
                    Vision.poseToHomMat3d(lastRobotPose.PoseType, lastRobotPose.x, lastRobotPose.y, lastRobotPose.z,
                        lastRobotPose.rx, lastRobotPose.ry, lastRobotPose.rz, ToolToBase.CvPtr);

                    BaseToTool = ToolToBase.Inv();

                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                    double x, y, z, rx, ry, rz;
                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                    camInTools.At<double>(0, 0) = x;
                    camInTools.At<double>(0, 1) = y;
                    camInTools.At<double>(0, 2) = z;
                    camInTools.At<double>(0, 3) = rx;
                    camInTools.At<double>(0, 4) = ry;
                    camInTools.At<double>(0, 5) = rz;
                    camInTools.At<double>(0, 6) = 2;

                }
                //轨迹的后一个点，要转成center2Tool
                {
                    Mat ToolToBase = new Mat();
                    Mat BaseToTool ;
                    Mat CenterToTool ;
                    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                        robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);

                    BaseToTool = ToolToBase.Inv();

                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                    double x, y, z, rx, ry, rz;
                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                    camInTools.At<double>(1, 0) = x;
                    camInTools.At<double>(1, 1) = y;
                    camInTools.At<double>(1, 2) = z;
                    camInTools.At<double>(1, 3) = rx;
                    camInTools.At<double>(1, 4) = ry;
                    camInTools.At<double>(1, 5) = rz;
                    camInTools.At<double>(1, 6) = 2;

                }

                //检测的相机位姿
                {
                    //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                    //Mat BaseToTool = robotPoseMat.Inv();
                    Mat ToolToBase = new Mat();
                    Mat BaseToTool;
                    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                    BaseToTool = ToolToBase.Inv();

                    CamToTool = BaseToTool * Cam1ToBase * CamToCam1;

                    Vision.robotAndCamVectorAngle(camInTools.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                    //显示结果
                    if (user3DShowControl_V != null)
                    {
                        user3DShowControl_V.ClearPointCloud();
                        user3DShowControl_V.RefreshOn(100, true);

                        //显示中心姿态
                        PoseParameters showRobotPose = new PoseParameters();
                        Vision.HomMat3dToPose(showRobotPose.PoseType, out double x, out double y, out double z, out double rx, out double ry, out double rz, CamToTool.CvPtr);
                        user3DShowControl_V.AddCoord(x, y, z, rx, ry, rz, 0.1);
                        //显示前后移动点
                        user3DShowControl_V.AddPoint(camInTools.At<double>(0,0), camInTools.At<double>(0, 1), camInTools.At<double>(0, 2), 0);
                        user3DShowControl_V.AddPoint(camInTools.At<double>(1, 0), camInTools.At<double>(1, 1), camInTools.At<double>(1, 2), 4);


                        user3DShowControl_V.RefreshPoints();
                        user3DShowControl_V.RefreshOFF();
                    }

                    //眼在手外，要减180度
                    robotAndCamAngle = 180 - robotAndCamAngle;
                    //大于90的，都取缩小后的值
                    if (robotAndCamAngle > 90)
                    {
                        robotAndCamAngle = 180 - robotAndCamAngle;
                    }
                }
            }

            return robotAndCamAngle;
        }



        //public static double scaleSize = 10; //表示计算过程中的点云放缩因子，1时单位为1mm，10时单位为100um，100时单位为10um

        ////后面开放一下这几个参数 这个是用多个相机进行点云合成，然后再用点云投影成平面时，才需要用到的参数，分别指xyz的范围。目前不用多个相机融合，因此不用到
        //public static double xSize = 0.02;
        //public static double ySize = 0.02;
        //public static double offset_z = 0.235;
        //public static double zSize = 0.002;

        public static double xSize = 0.025;
        public static double ySize = 0.04;
        public static double zSize = 0.004;
        public static double offset_z = -0.010;


        /// <summary>
        /// 获取激光位置
        /// </summary>
        /// <param name="Image"></param>
        /// <param name="minThreshold"></param>
        /// <param name="outlinePoints"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public static void getLaserPosition(Mat Image, double minThreshold,int laserMinWidth, out Mat outlinePoints, int offsetX = 0, int offsetY = 0)
        {
            Mat outImage = new Mat();
            outlinePoints = new Mat();

            thinningD(Image.CvPtr, outImage.CvPtr, outlinePoints.CvPtr, (int)minThreshold, laserMinWidth);

            ////添加
            //Vision.printPoint(outlinePoints, "outlinePoints");
            //Console.WriteLine($"outlinePoints:\r\n");

            //for (int i = 0; i < outlinePoints.Rows; i++)
            //{
            //    Console.WriteLine($"x:{outlinePoints.At<double>(i, 0)}");
            //    Console.WriteLine($"y:{outlinePoints.At<double>(i, 1)},\r\n");

            //}
            //thinningD(Image.CvPtr, outImage.CvPtr, outlinePoints.CvPtr);
            //Console.WriteLine($"Image type:{Image.Type()}.");
            //Console.WriteLine($"outImage type:{outImage.Type()}.");

            if (offsetX != 0)
            {
                for (int i = 0; i < outlinePoints.Rows; i++)
                {
                    outlinePoints.At<double>(i, 0) += offsetX;
                    //Console.WriteLine($"outlinePoints .At<double>(i, 0):{outlinePoints.At<double>(i, 0)}.");
                }
            }
            if (offsetY != 0)
            {
                for (int i = 0; i < outlinePoints.Rows; i++)
                {
                    outlinePoints.At<double>(i, 1) += offsetY;
                    //Console.WriteLine($"outlinePoints .At<double>(i, 1):{outlinePoints.At<double>(i, 1)}.");

                }
            }

            //Cv2.NamedWindow("Image", WindowFlags.Normal);
            //Cv2.ImShow("Image", Image);
            //Cv2.NamedWindow("outImage", WindowFlags.Normal);
            //Cv2.ImShow("outImage", outImage);
            //Cv2.WaitKey(0);
            //showMatPoint(outlinePoints, "imagePoints");

        }

        public static void showMatPoint(Mat Points, string windowName)
        {
            //显示
            double imgW = 0;
            double imgH = 0;
            for (int i = 0; i < Points.Rows; i++)
            {
                double x = Points.At<double>(i, 0);
                double y = Points.At<double>(i, 1);
                imgW = Math.Max(x, imgW);
                imgH = Math.Max(y, imgH);
            }
            Mat imgShow = Mat.Zeros((int)imgH + 1, (int)imgW + 1, MatType.CV_8UC1);
            for (int i = 0; i < Points.Rows; i++)
            {
                double x = Points.At<double>(i, 0);
                double y = Points.At<double>(i, 1);
                imgShow.At<byte>((int)y, (int)x) = 255;
            }
            Cv2.NamedWindow(windowName, WindowFlags.Normal);
            Cv2.ImShow(windowName, imgShow);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();

        }



        /// <summary>
        /// 输入像素坐标，输出物理坐标xy
        /// </summary>
        public static void GetXY(CameraParameters hCamPar, PoseParameters hWorldPose, Mat srcPoints, out Mat transformPoints, bool flipX = false, bool flipY = false)
        {
            transformPoints = new Mat();

            //hCamPar.ImagePointsToWorldPlane(hWorldPose, new HTuple(ys), new HTuple(xs), "m", out hx, out hy);


            imagePointsToWorldPlane(hCamPar.Focus, hCamPar.Kappa, hCamPar.Sx, hCamPar.Sy, hCamPar.Cx, hCamPar.Cy, hWorldPose.PoseType, hWorldPose.x, hWorldPose.y, hWorldPose.z,
                hWorldPose.rx, hWorldPose.ry, hWorldPose.rz, srcPoints.CvPtr, transformPoints.CvPtr);

            if (flipX)
            {
                for (int i = 0; i < transformPoints.Cols; i++)
                {
                    transformPoints.At<double>(0, i) *= -1;
                }
            }
            if (flipY)
            {
                for (int i = 0; i < transformPoints.Cols; i++)
                {
                    transformPoints.At<double>(1, i) *= -1;
                }
            }


        }

        /// <summary>
        /// 离散滤波
        /// </summary>
        /// <param name="srcPoints"></param>
        /// <param name="transformPoints"></param>
        /// <param name="distThre"></param>
        /// <param name="segmentalThre"></param>
        public static void TrajectoryDiscreteFilter(Mat srcPoints, out Mat transformPoints, double distThre, int segmentalThre)
        {
            transformPoints = new Mat();

            trajectoryDiscreteFilter(srcPoints.CvPtr, distThre, segmentalThre, transformPoints.CvPtr);


        }

        /// <summary>
        /// 点云切割
        /// </summary>
        /// <param name="item"></param>
        /// <param name="hCamPar"></param>
        /// <param name="LightInCam"></param>
        /// <param name="dictImage"></param>
        /// <param name="imageKey"></param>
        /// <param name="imageSet"></param>
        /// <param name="xy"></param>
        /// <param name="lightXYcut"></param>
        public static void cutLight(Mat xy, CamParam camParam, CameraParameters hCamPar,
            PoseParameters LightInCam, Mat img, ImageSet imageSet, out Mat lightXYcut)
        {
            lightXYcut = new Mat();
            int imageWidth, imageHeight;
            imageWidth = img.Cols;
            imageHeight = img.Rows;

            double LeftX = imageWidth * imageSet.LeftX + camParam.OffsetX;
            double RightX = imageWidth * imageSet.RightX + camParam.OffsetX;
            double TopY = imageHeight * imageSet.TopY + camParam.OffsetY;
            double DownY = imageHeight * imageSet.DownY + camParam.OffsetY;
            List<int> idList = new List<int>();
            for (int i = 0; i < xy.Rows; i++)
            {
                double x = xy.At<double>(i, 0);
                double y = xy.At<double>(i, 1);
                if (x >= LeftX && x <= RightX && y >= TopY && y <= DownY)
                {
                    idList.Add(i);
                }
            }
            Mat selectXY = new Mat(idList.Count, 2, MatType.CV_64FC1);
            for (int i = 0; i < idList.Count; i++)
            {
                selectXY.At<double>(i, 0) = xy.At<double>(idList[i], 0);
                selectXY.At<double>(i, 1) = xy.At<double>(idList[i], 1);
            }
            //转激光坐标系
            Vision.GetXY(hCamPar, LightInCam, selectXY, out lightXYcut);
        }

        public static void cutLight(Mat xy, CamParam camParam, Mat img, ImageSet imageSet, out Mat xyCut)
        {
            int imageWidth, imageHeight;
            imageWidth = img.Cols;
            imageHeight = img.Rows;

            double LeftX = imageWidth * imageSet.LeftX + camParam.OffsetX;
            double RightX = imageWidth * imageSet.RightX + camParam.OffsetX;
            double TopY = imageHeight * imageSet.TopY + camParam.OffsetY;
            double DownY = imageHeight * imageSet.DownY + camParam.OffsetY;
            List<int> idList = new List<int>();
            for (int i = 0; i < xy.Rows; i++)
            {
                double x = xy.At<double>(i, 0);
                double y = xy.At<double>(i, 1);
                if (x >= LeftX && x <= RightX && y >= TopY && y <= DownY)
                {
                    idList.Add(i);
                }
            }
            xyCut = new Mat(idList.Count, 2, MatType.CV_64FC1);
            for (int i = 0; i < idList.Count; i++)
            {
                xyCut.At<double>(i, 0) = xy.At<double>(idList[i], 0);
                xyCut.At<double>(i, 1) = xy.At<double>(idList[i], 1);
            }
            
        }

        /// <summary>
        /// 分析涂胶结果
        /// </summary>
        /// <param name="imageSet"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="phi"></param>
        /// <param name="maxArea"></param>
        /// <param name="resultData"></param>
        /// <param name="bResult"></param>
        public static void judgeGlueResult(ImageSet imageSet,int scaleSize,double centerX,double centerY, double width, double height, double phi, double maxArea, out Data resultData, out BResult bResult)
        {
            resultData = new Data();
            bResult = new BResult();

            resultData.row = centerY;
            resultData.column = centerX;

            bool heng = Math.Abs(phi) <= Math.PI / 4;
            resultData.glueHeight = (heng ? height : width) / scaleSize;
            resultData.glueWidth = (heng ? width : height) / scaleSize;
            //resultData.glueArea = (heng ? width : height) / scaleSize;

            resultData.glueArea = maxArea / (scaleSize * scaleSize);
            if (resultData.glueHeight >= imageSet.heightMin && resultData.glueHeight <= imageSet.heightMax)
            {
                bResult.glueHeight = true;
            }
            if (resultData.glueWidth >= imageSet.widthMin && resultData.glueWidth <= imageSet.widthMax)
            {
                bResult.glueWidth = true;
            }
            if (resultData.glueArea >= imageSet.areaMin && resultData.glueArea <= imageSet.areaMax)
            {
                bResult.glueArea = true;
            }
            if (!bResult.glueHeight || !bResult.glueWidth || !bResult.glueArea)
            {
                bResult.Result = false;
            }
        }

        /// <summary>
        /// 轨迹分段，把多段线划分成指定段数
        /// </summary>
        /// <param name="XLDData"></param>
        /// <param name="divideCount"></param>
        /// <param name="rows"></param>
        /// <param name="cols"></param>
        /// <param name="angles"></param>
        public static void XLDDataDivide(XLDData XLDData, int divideCount, out List<double> rows, out List<double> cols, out List<double> angles)
        {
            rows = new List<double>();
            cols = new List<double>();
            angles = new List<double>();
            if (XLDData.ControlRows.Length >= 2)
            {
                Mat inputPointMat = new Mat();
                Mat dividedPoints = new Mat();
                inputPointMat = Mat.Zeros(XLDData.ControlRows.Length, 2, MatType.CV_64FC1);
                for (int i = 0; i < XLDData.ControlRows.Length; i++)
                {
                    inputPointMat.At<double>(i, 0) = XLDData.ControlCols[i];
                    inputPointMat.At<double>(i, 1) = XLDData.ControlRows[i];

                }

                dividePolyline(inputPointMat.CvPtr, divideCount, dividedPoints.CvPtr);
                for (int i = 0; i < divideCount; i++)
                {
                    cols.Add(dividedPoints.At<double>(i, 0));
                    rows.Add(dividedPoints.At<double>(i, 1));
                    angles.Add(dividedPoints.At<double>(i, 2));
                }
            }
            

        }
        public static void printPoint(Mat Points,string Name)
        { 
            Console.WriteLine(Name+":");
            for (int i = 0; i < Points.Rows; i++)
            {
                string meg = $"point {i}:(";
                for (int j = 0; j < Points.Cols; j++)
                {
                    meg += $"{Points.At<double>(i,j)},";
                }
                meg += ")";
                Console.WriteLine(meg);
            }
        }

        /// <summary>
        /// 点坐标转换，从相机转为激光坐标系和机器人坐标系.眼在手上输出的是机器坐标系下的点云坐标；眼在手外输出的是法兰盘坐标系下的点云位置。
        /// 眼在手上时，输出objToBase
        /// 眼在手上时，输出objToTool
        /// </summary>
        /// <param name="imagePoint"></param>
        /// <param name="hCamPar"></param>
        /// <param name="LightInCam"></param>
        /// <param name="LightToCam"></param>
        /// <param name="CamToTool"></param>
        /// 眼在手上时，应该调用CamToTool
        /// 眼在手外时，应该调用CamToBase
        /// <param name="robotPose"></param>
        /// 眼在手上时，应该调用robotPose
        /// 眼在手外时，应该调用robotPoseInv
        /// <param name="lightXY"></param>
        /// <param name="robotX"></param>
        /// <param name="robotY"></param>
        /// <param name="robotZ"></param>
        public static void pointTransform2CamAndRobot(Mat imagePoint, CameraParameters hCamPar, PoseParameters LightInCam,
            Mat LightToCam, Mat CamToTool, PoseParameters robotPose, out Mat lightXY, out List<double> robotX, out List<double> robotY,
            out List<double> robotZ)
        {
            //printPoint(imagePoint, "imagePoint");

            //转激光坐标系
            GetXY(hCamPar, LightInCam, imagePoint, out lightXY);
            //printPoint(lightXY, "lightXY");

            //printPoint(lightXY, "lightXY");
            Mat lightXY4 = new Mat();
            lightXY4 = Mat.Zeros(lightXY.Rows, 4, MatType.CV_64FC1);
            Mat ones = new Mat();
            ones = Mat.Ones(lightXY.Rows, 1, MatType.CV_64FC1);
            ones.CopyTo(lightXY4.Col(3));

            lightXY.CopyTo(lightXY4[new OpenCvSharp.Rect(0, 0, 2, lightXY4.Rows)]);
            //转相机坐标系
            Mat camXY4 = new Mat();
            //Console.WriteLine("LightToCam:");
            affineTransPoint3d(lightXY4.CvPtr, camXY4.CvPtr, LightToCam.CvPtr, false);
            //printPoint(LightToCam, "LightToCam");
            //printPoint(camXY4, "camXY4");

            ////转传感器坐标系
            Mat toolXY4 = new Mat();
            //转工具
            //Console.WriteLine("CamToTool:");
            affineTransPoint3d(camXY4.CvPtr, toolXY4.CvPtr, CamToTool.CvPtr, false);
            //printPoint(CamToTool, "CamToTool");
            //printPoint(toolXY4, "toolXY4");

            //转机器人坐标
            Mat robotXY4 = new Mat();
            Mat ToolToRobot = new Mat();

            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToRobot.CvPtr);
            //Console.WriteLine("ToolToRobot:");
            Vision.affineTransPoint3d(toolXY4.CvPtr, robotXY4.CvPtr, ToolToRobot.CvPtr, false);
            //printPoint(ToolToRobot, "ToolToRobot");
            //printPoint(robotXY4, "robotXY4");

            //List<Point2d> imagePointsList = new List<Point2d>();
            //List<Point3d> lightPointsList = new List<Point3d>();
            //List<Point3d> camPointsList = new List<Point3d>();
            //List<Point3d> toolPointsList = new List<Point3d>();
            //List<Point3d> robotXY4List = new List<Point3d>();

            //tranformMatToPoint2d(imagePoint, imagePointsList);
            //tranformMatToPoint3d(lightXY4, lightPointsList);
            //tranformMatToPoint3d(camXY4, camPointsList);
            //tranformMatToPoint3d(toolXY4, toolPointsList);
            //tranformMatToPoint3d(robotXY4, robotXY4List);

            robotX = new List<double>();
            robotY = new List<double>();
            robotZ = new List<double>();
            for (int i = 0; i < robotXY4.Rows; i++)
            {
                robotX.Add(robotXY4.At<double>(i, 0));
                robotY.Add(robotXY4.At<double>(i, 1));
                robotZ.Add(robotXY4.At<double>(i, 2));

                //robotX.Add(camXY4.At<double>(i, 0));
                //robotY.Add(camXY4.At<double>(i, 1));
                //robotZ.Add(camXY4.At<double>(i, 2));
            }
        }

        /// <summary>
        /// 点坐标转换，从相机转为激光坐标系和机器人坐标系.眼在手上输出的是机器坐标系下的点云坐标；眼在手外输出的是法兰盘坐标系下的点云位置。
        /// 眼在手上时，输出objToBase
        /// 眼在手上时，输出objToTool
        /// </summary>
        /// <param name="lightXY"></param>
        /// <param name="LightToCam"></param>
        /// <param name="CamToTool"></param>
        /// 眼在手上时，应该调用CamToTool
        /// 眼在手外时，应该调用CamToBase
        /// <param name="robotPose"></param>
        /// 眼在手上时，应该调用robotPose
        /// 眼在手外时，应该调用robotPoseInv
        /// <param name="robotX"></param>
        /// <param name="robotY"></param>
        /// <param name="robotZ"></param>
        public static void pointTransform2LightAndRobot(Mat lightXY, 
            Mat LightToCam, Mat CamToTool, PoseParameters robotPose, out List<double> robotX, out List<double> robotY,
            out List<double> robotZ)
        {
            //printPoint(lightXY, "lightXY");

            //printPoint(lightXY, "lightXY");
            Mat lightXY4 = new Mat();
            lightXY4 = Mat.Zeros(lightXY.Rows, 4, MatType.CV_64FC1);
            Mat ones = new Mat();
            ones = Mat.Ones(lightXY.Rows, 1, MatType.CV_64FC1);
            ones.CopyTo(lightXY4.Col(3));

            lightXY.CopyTo(lightXY4[new OpenCvSharp.Rect(0, 0, 2, lightXY4.Rows)]);
            //转相机坐标系
            Mat camXY4 = new Mat();
            //Console.WriteLine("LightToCam:");
            affineTransPoint3d(lightXY4.CvPtr, camXY4.CvPtr, LightToCam.CvPtr, false);
            //printPoint(LightToCam, "LightToCam");
            //printPoint(camXY4, "camXY4");

            ////转传感器坐标系
            Mat toolXY4 = new Mat();
            //转工具
            //Console.WriteLine("CamToTool:");
            affineTransPoint3d(camXY4.CvPtr, toolXY4.CvPtr, CamToTool.CvPtr, false);
            //printPoint(CamToTool, "CamToTool");
            //printPoint(toolXY4, "toolXY4");

            //转机器人坐标
            Mat robotXY4 = new Mat();
            Mat ToolToRobot = new Mat();

            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToRobot.CvPtr);
            //Console.WriteLine("ToolToRobot:");
            Vision.affineTransPoint3d(toolXY4.CvPtr, robotXY4.CvPtr, ToolToRobot.CvPtr, false);
            //printPoint(ToolToRobot, "ToolToRobot");
            //printPoint(robotXY4, "robotXY4");

            //List<Point2d> imagePointsList = new List<Point2d>();
            //List<Point3d> lightPointsList = new List<Point3d>();
            //List<Point3d> camPointsList = new List<Point3d>();
            //List<Point3d> toolPointsList = new List<Point3d>();
            //List<Point3d> robotXY4List = new List<Point3d>();

            //tranformMatToPoint2d(imagePoint, imagePointsList);
            //tranformMatToPoint3d(lightXY4, lightPointsList);
            //tranformMatToPoint3d(camXY4, camPointsList);
            //tranformMatToPoint3d(toolXY4, toolPointsList);
            //tranformMatToPoint3d(robotXY4, robotXY4List);

            robotX = new List<double>();
            robotY = new List<double>();
            robotZ = new List<double>();
            for (int i = 0; i < robotXY4.Rows; i++)
            {
                robotX.Add(robotXY4.At<double>(i, 0));
                robotY.Add(robotXY4.At<double>(i, 1));
                robotZ.Add(robotXY4.At<double>(i, 2));

                //robotX.Add(camXY4.At<double>(i, 0));
                //robotY.Add(camXY4.At<double>(i, 1));
                //robotZ.Add(camXY4.At<double>(i, 2));
            }
        }



        /// <summary>
        /// 在指定范围内做高斯加权平均
        /// </summary>
        /// <param name="points">轨迹点</param>
        /// <param name="from">可用范围起点索引</param>
        /// <param name="to">可用范围终点索引</param>
        /// <param name="anchor">权重中心（最关注的点）</param>
        /// <param name="sigma">高斯标准差</param>
        public static Point3D GaussianSmoothInRange(
            Point3D[] points,
            int from,
            int to,
            int anchor,
            double sigma)
        {
            double wx = 0, wy = 0, wz = 0, wSum = 0;

            // 确保不越界
            int start = Math.Max(from, 0);
            int end = Math.Min(to, points.Length - 1);

            for (int j = start; j <= end; j++)
            {
                double dist = j - anchor;  // 到权重中心的距离
                double w = Math.Exp(-(dist * dist) / (2.0 * sigma * sigma));

                wx += points[j].X * w;
                wy += points[j].Y * w;
                wz += points[j].Z * w;
                wSum += w;
            }

            return new Point3D(wx / wSum, wy / wSum, wz / wSum);
        }

        private static void tranformMatToPoint2d(Mat pointMat, List<Point2d> pointList)
        {
            for (int i = 0; i < pointMat.Rows; i++)
            {
                pointList.Add(new Point2d(pointMat.At<double>(i, 0),
                    pointMat.At<double>(i, 1)));
            }
        }


        private static void tranformMatToPoint3d(Mat pointMat, List<Point3d> pointList)
        {
            for (int i = 0; i < pointMat.Rows; i++)
            {
                pointList.Add(new Point3d(pointMat.At<double>(i, 0),
                    pointMat.At<double>(i, 1), pointMat.At<double>(i, 2)));
            }
        }

        /// <summary>
        /// 用于显示和矫正计算，要结合激光和相机的夹角，进行
        /// </summary>
        /// <param name="lightXYcut"></param>
        /// <param name="cutSet"></param>
        /// <param name="XY_10um"></param>
        public static void scalePoint(Mat lightXYcut,CutSet cutSet,double lightAngle, out Mat XY_10um)
        {


            XY_10um = new Mat(lightXYcut.Size(), lightXYcut.Type());
            //X
            Cv2.Multiply(lightXYcut.Col(0), new Scalar(1000 * cutSet.scaleSize), XY_10um.Col(0));
            Cv2.Add(XY_10um.Col(0), new Scalar(cutSet.ShowWidth * cutSet.scaleSize / 2), XY_10um.Col(0));
            //Y

            Cv2.Multiply(lightXYcut.Col(1), new Scalar(1000 * cutSet.scaleSize * Math.Cos(lightAngle / 180 * Math.PI)), XY_10um.Col(1));
            Cv2.Add(XY_10um.Col(1), new Scalar(cutSet.ShowHeight * cutSet.scaleSize / 2), XY_10um.Col(1));
        }

        public static void singleFrameDetAndResult(Mat OutLine,ImageSet imageSet,CutSet cutSet, ref bool singleFrameExistGlue, ref Data resultData, ref BResult bResult, ref Mat outMaxRegion, ref Mat outRegionRectangle2)
        {
            bool existGlue = false;
            double centerX = 0;
            double centerY = 0;
            double phi = 0;
            double width = 0;
            double height = 0;
            double maxArea = 0;

            //////开始检测
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();


            Vision.singleFrameDet(OutLine.CvPtr, out existGlue, out centerX, out centerY, out phi, out width, out height, out maxArea,
                outMaxRegion.CvPtr, outRegionRectangle2.CvPtr, imageSet.isUseAngleOpt, false);


            //stopwatch.Stop();
            ////结束检测
            //TimeSpan elapsedTime = stopwatch.Elapsed;
            //double useTime = elapsedTime.TotalMilliseconds;

            //Console.WriteLine($"singleFrameDet use time:{useTime}ms");

            bResult = new BResult();
            resultData = new Data();
            if (maxArea > 0)
            {
                //stopwatch = new Stopwatch();
                //stopwatch.Start();

                singleFrameExistGlue = true;
                Vision.judgeGlueResult(imageSet, cutSet.scaleSize, centerX, centerY, width, height, phi, maxArea, out resultData, out bResult);

                ////结束检测
                //elapsedTime = stopwatch.Elapsed;
                //useTime = elapsedTime.TotalMilliseconds;
                //Console.WriteLine($"judgeGlueResult use time:{useTime}");
            }
            else
            {
                bResult.Result = false;
            }


        }

        public static PoseParameters PoseInv(PoseParameters robotPose)
        {
            PoseParameters BaseInTool = new PoseParameters();
            Mat ToolToBase = new Mat();
            Mat BaseToTool = new Mat();
            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
            BaseToTool = ToolToBase.Inv();

            BaseInTool.PoseType = robotPose.PoseType;
            Vision.HomMat3dToPose(BaseInTool.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, BaseToTool.CvPtr);
            BaseInTool.x = centerX;
            BaseInTool.y = centerY;
            BaseInTool.z = centerZ;
            BaseInTool.rx = centerRX;
            BaseInTool.ry = centerRY;
            BaseInTool.rz = centerRZ;
            return BaseInTool;
        }
    }
    //public struct Point3D
    //{
    //    public double X;
    //    public double Y;
    //    public double Z;
    //    public Point3D(double x, double y, double z)
    //    {
    //        X = x;
    //        Y = y;
    //        Z = z;
    //    }
    //}

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

        //3d映射2d参数
        public ProjectionMapper mapper = new ProjectionMapper();


        //数模图
        public Mat image = new Mat();
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
                        _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                        _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }

                    // 反序列化成功后，逐个检查
                    foreach (var cutSet in CutSets)
                    {
                        cutSet.AfterDeserialize();
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                    _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
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
                        _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                }
                else
                {
                    _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
                    result3 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result3 = false;
            }


            bool result4 = true;
            try
            {
                //XLDDatas = new List<XLDData>();
                string fPath = basePath + "ProjectMapping.json";
                if (File.Exists(fPath))
                {

                    result4 = mapper.LoadFromFile(fPath);
                }
                else
                {
                    _errMsg += "\r\n" + fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
                    result4 = false;
                }
            }
            catch (Exception ex)
            {
                _errMsg += "\r\n" + ex.ToString();
                result4 = false;
            }

            return result0 && result1 && result2 && result3 && result4;
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


                {
                    //XLDDatas = new List<XLDData>();
                    string fPath = basePath + "ProjectMapping.json";
                    result &= mapper.SaveToFile(fPath);
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

        public static int CamCount = 4; //不用变，最多4个相机
        public List<bool> CamEnabled;
        //显示画布大小
        public int ShowWidth = 50;//mm
        public int ShowHeight = 50;
        //3D颜色范围
        public double ShowColorMax = 100;//mm
        public double ShowColorMin = -100;
        //标识大小
        public int Size = 3;

        //记录开始轨迹开始和结束id
        public int StartImageIndex = 0;
        public int EndImageIndex = 0;

        // 是否使用角度优化
        public bool isUseAngleOpt = true;
        // 放大处理倍数
        public int scaleSize = 5;

        // 矫正缩放系数
        public double correctionScaleSizeX = 1;
        public double correctionScaleSizeY = 1;
        // 是否共享矫正系数
        public bool isCoefficientSharing = true;

        // 胶长检测
        public bool glueLenthDetEnable = false;
        public double glueWidthThre = 0;
        public double glueLenthMin = 0;
        public double glueLenthMax = 99999;
        public int glueStartID = 0;
        public int glueEndID = 99999;


        public void AfterDeserialize()
        {
            // 如果 JSON 里没有这个字段（旧存档），给默认值
            if (CamEnabled == null || CamEnabled.Count == 0)
            {
                CamEnabled = Enumerable.Repeat(true, CamCount).ToList();
            }
        }


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
        public int laserMinWidth = 2;
        public bool 单帧检测 = false;
        public bool _3DGlueDet = false;
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

        // 是否使用角度优化
        public bool isUseAngleOpt = true;

        // 矫正缩放系数
        public double correctionScaleSizeX = 1;
        public double correctionScaleSizeY = 1;


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
        public double row = -1, column = -1;
        public double glueHeight = -1;
        public double glueWidth = -1;
        public double glueArea = -1;

        // 深复制
        public Data Clone()
        {
            return new Data 
            { 
                row = this.row,
                column = this.column,
                glueHeight = this.glueHeight,
                glueWidth = this.glueWidth,
                glueArea = this.glueArea,
            };
        }
    }
    [Serializable]
    public class BResult
    {
        public bool glueHeight ;
        public bool glueWidth ;
        public bool glueArea ;
        /// <summary>
        /// 总结果
        /// </summary>
        public bool Result = true;

        public BResult Clone()
        {
            return new BResult
            {
                glueHeight = this.glueHeight,
                glueWidth = this.glueWidth,
                glueArea = this.glueArea,
                Result = this.Result,
            };
        }
    }
}
