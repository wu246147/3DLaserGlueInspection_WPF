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
    using System.Windows;
    using System.Windows.Documents;

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
                throw new InvalidOperationException(_3DLaserGlueInspection.Resources.LanguageDict.CalibrateRequired);
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
                throw new ArgumentException(_3DLaserGlueInspection.Resources.LanguageDict.Pose3DAtLeastTwoPoints);
            if (controlRows == null || controlCols == null || controlRows.Length < 2)
                throw new ArgumentException(_3DLaserGlueInspection.Resources.LanguageDict.ControlRowsColsAtLeastTwoPoints);
            if (controlRows.Length != controlCols.Length)
                throw new ArgumentException(_3DLaserGlueInspection.Resources.LanguageDict.ControlRowsColsLengthMismatch);

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
                throw new InvalidOperationException(_3DLaserGlueInspection.Resources.LanguageDict.CalibrateRequired);
            if (point3D == null || point3D.Length < 3)
                throw new ArgumentException(_3DLaserGlueInspection.Resources.LanguageDict.Point3DAtLeastThreeElements);

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


        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "transform7HomMat3dN")]
        /// Seven-parameter pose calibration; poseTypes and r7Poses are contiguous buffers.
        public static extern int transform7HomMat3dN(
            int poseCount,
            IntPtr poseTypes,
            IntPtr r7Poses,
            double axisX,
            double axisY,
            double axisZ,
            IntPtr transformMatBm,
            IntPtr transformMatMc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "predict7HomMat3d")]
        /// Predict a seven-parameter pose from calibration matrices and a rotation axis.
        public static extern int predict7HomMat3d(
            IntPtr transformMatBm,
            IntPtr transformMatMc,
            double rotationAngle,
            double axisX,
            double axisY,
            double axisZ,
            IntPtr r7Mat);
        private const string DllName2 = "dll\\RaivasAlgGB.dll"; // Replace with the actual DLL 

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///激光提取函数
        public static extern int thinning(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///激光提取函数,亚像素
        public static extern int thinningD(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat, int min_thre, int min_width = 1);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]

        ///激光提取函数,稳定版
        public static extern int thinningH(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat, int min_thre, int min_width = 1);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///thinningF：基于 thinningH 的候选提取和轨迹跟踪流程，
        /// 在候选生成阶段增加反射噪声的方向过滤。
        public static extern int thinningF(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat, int min_thre, int min_width = 1);

        [DllImport(DllName2, CallingConvention = CallingConvention.StdCall)]
        ///激光提取函数，默认局部灰度差为1，辅助追踪轮廓可选
        public static extern int thinningL(IntPtr inputMat, IntPtr outImage, IntPtr outPointMat, double minContrast = 1.0, IntPtr trackingProfile = default(IntPtr));

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///获取矩形框内的点
        public static extern int GetPointsInRotatedRect(IntPtr laserPoints,IntPtr selectLaserPoints, double centerX, double centerY, double width, double height, double angle
            );

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///直线拟合
        public static extern int ransacFitLineFast(IntPtr pts, out double item0,
            out double item1,
            out double item2,
            out double item3,
            float distThresh = 10.0f,   // 内点距离阈值（像素）
            int maxIter = 100,        // 最大迭代
            double conf = 0.999);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///基于基准面的点过滤，可以过滤海绵条
        public static extern int FilterPointsByLine(
                IntPtr inputPoints,
                double item0,
                double item1,
                double item2,
                double item3,
                double lineDistThre,
                IntPtr filteredPoints);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///删除相对基准面的距离位于上下限闭区间内的点
        public static extern int FilterPointsByLineRange(
                IntPtr inputPoints,
                double item0,
                double item1,
                double item2,
                double item3,
                double lowerDist,
                double upperDist,
                IntPtr filteredPoints);

        /// <summary>按相对基准线的最高点保留左侧（1）或右侧（2）涂胶点。</summary>
        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        public static extern int FilterGluePointsBySide(
            IntPtr inputPoints, double item0, double item1, double item2, double item3,
            int keepSide, IntPtr filteredPoints);

        [DllImport(DllName2, CallingConvention = CallingConvention.Cdecl)]
        ///基于基准面的涂胶检测
        public static extern int glueDet(
            IntPtr inputPoints,
            double item0,
            double item1,
            double item2,
            double item3,
            out bool existGlue,
            out double centerX,
            out double centerY,
            out double phi,
            out double width,
            out double height,
            out double maxArea,
            IntPtr regionBig,
            IntPtr regionRect);



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
        public static double GetRobotAndCamAngle(int CamHandEyeType, Mat CamToCam1, Mat CenterToCam1,
            Mat Cam1ToBase, Mat CamToTool, PoseParameters robotPose, PoseParameters lastRobotPose,
            User3DShowControl_V user3DShowControl_V = null)
        {
            double robotAndCamAngle;
            if (CamHandEyeType == 0)
            {
                using (Mat robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1))
                using (Mat toolToBase = new Mat())
                {
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

                    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                        robotPose.rx, robotPose.ry, robotPose.rz, toolToBase.CvPtr);
                    using (Mat camToBase = toolToBase * CamToTool)
                    {
                        Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, camToBase.CvPtr, 2, 0, out robotAndCamAngle);
                    }
                }

                if (robotAndCamAngle > 90)
                {
                    robotAndCamAngle = 180 - robotAndCamAngle;
                }
            }
            else
            {
                using (Mat camInTools = Mat.Zeros(2, 7, MatType.CV_64FC1))
                {
                    // 轨迹的前一个点转换到中心坐标系。
                    using (Mat toolToBase = new Mat())
                    {
                        Vision.poseToHomMat3d(lastRobotPose.PoseType, lastRobotPose.x, lastRobotPose.y,
                            lastRobotPose.z, lastRobotPose.rx, lastRobotPose.ry, lastRobotPose.rz, toolToBase.CvPtr);
                        using (Mat inv = toolToBase.Inv())
                        using (Mat centerToTool = inv * Cam1ToBase * CenterToCam1)
                        {
                            Vision.HomMat3dToPose(2, out double x, out double y, out double z,
                                out double rx, out double ry, out double rz, centerToTool.CvPtr);
                            camInTools.At<double>(0, 0) = x;
                            camInTools.At<double>(0, 1) = y;
                            camInTools.At<double>(0, 2) = z;
                            camInTools.At<double>(0, 3) = rx;
                            camInTools.At<double>(0, 4) = ry;
                            camInTools.At<double>(0, 5) = rz;
                            camInTools.At<double>(0, 6) = 2;
                        }
                    }

                    // 轨迹的后一个点转换到中心坐标系。
                    using (Mat toolToBase = new Mat())
                    {
                        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y,
                            robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, toolToBase.CvPtr);
                        using (Mat inv = toolToBase.Inv())
                        using (Mat centerToTool = inv * Cam1ToBase * CenterToCam1)
                        {
                            Vision.HomMat3dToPose(2, out double x, out double y, out double z,
                                out double rx, out double ry, out double rz, centerToTool.CvPtr);
                            camInTools.At<double>(1, 0) = x;
                            camInTools.At<double>(1, 1) = y;
                            camInTools.At<double>(1, 2) = z;
                            camInTools.At<double>(1, 3) = rx;
                            camInTools.At<double>(1, 4) = ry;
                            camInTools.At<double>(1, 5) = rz;
                            camInTools.At<double>(1, 6) = 2;
                        }
                    }

                    // 计算检测时的相机位姿。
                    using (Mat toolToBase = new Mat())
                    {
                        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y,
                            robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, toolToBase.CvPtr);
                        using (Mat inv = toolToBase.Inv())
                        using (Mat camToToolForAngle = inv * Cam1ToBase * CamToCam1)
                        {
                            Vision.robotAndCamVectorAngle(camInTools.CvPtr, camToToolForAngle.CvPtr, 2, 0,
                                out robotAndCamAngle);

                            if (user3DShowControl_V != null)
                            {
                                user3DShowControl_V.ClearPointCloud();
                                user3DShowControl_V.RefreshOn(100, true);

                                Vision.HomMat3dToPose(2, out double x, out double y, out double z,
                                    out double rx, out double ry, out double rz, camToToolForAngle.CvPtr);
                                user3DShowControl_V.AddCoord(x, y, z, rx, ry, rz, 0.1);
                                user3DShowControl_V.AddPoint(camInTools.At<double>(0, 0), camInTools.At<double>(0, 1),
                                    camInTools.At<double>(0, 2), 0);
                                user3DShowControl_V.AddPoint(camInTools.At<double>(1, 0), camInTools.At<double>(1, 1),
                                    camInTools.At<double>(1, 2), 4);
                                user3DShowControl_V.RefreshPoints();
                                user3DShowControl_V.RefreshOFF();
                            }
                        }
                    }

                    // 眼在手外，要减 180 度。
                    robotAndCamAngle = 180 - robotAndCamAngle;
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
            outlinePoints = new Mat();
            using (Mat outImage = new Mat())
            {
                // thinningL 使用默认局部灰度差 1.0，无最小线宽参数；保留旧入参以兼容调用方。
                thinningL(Image.CvPtr, outImage.CvPtr, outlinePoints.CvPtr);
            }

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
            int imageWidth = img.Cols;
            int imageHeight = img.Rows;

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

            using (Mat selectXY = new Mat(idList.Count, 2, MatType.CV_64FC1))
            {
                for (int i = 0; i < idList.Count; i++)
                {
                    selectXY.At<double>(i, 0) = xy.At<double>(idList[i], 0);
                    selectXY.At<double>(i, 1) = xy.At<double>(idList[i], 1);
                }
                Vision.GetXY(hCamPar, LightInCam, selectXY, out lightXYcut);
            }
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

        /// <summary>所有检测入口统一应用尺寸矩阵；旧参数自动使用 X/Y 对角矩阵。</summary>
        public static void ApplySizeCorrection(Mat points, ImageSet imageSet)
        {
            if (points == null)
                return;
            ApplySizeCorrectionMatrix(points, imageSet.GetSizeCorrectionMatrix());
        }

        private static void ApplySizeCorrectionMatrix(Mat points, SizeCorrectionMatrix matrix)
        {
            matrix.Validate();
            for (int index = 0; index < points.Rows; index++)
            {
                matrix.Transform(points.At<double>(index, 0), points.At<double>(index, 1), out double x, out double y);
                //if (!SizeCorrectionMatrix.IsFinite(x) || !SizeCorrectionMatrix.IsFinite(y))
                //    throw new InvalidOperationException("矫正轮廓包含无效坐标。");
                points.At<double>(index, 0) = x;
                points.At<double>(index, 1) = y;
            }
        }

        /// <summary>
        /// 半侧归属在尺寸矫正前的坐标中判断。矩阵交叉项可能把原右侧点移到峰顶的图像左侧，
        /// 因此不能直接用矫正后的 X 分半；只将筛选结果变换回当前检测坐标。
        /// </summary>
        public static int FilterGlueSideBeforeSizeCorrection(Mat correctedPoints, ImageSet imageSet,
            double lineX, double lineY, double anchorX, double anchorY, int keepSide, Mat filteredPoints)
        {
            SizeCorrectionMatrix matrix = imageSet.GetSizeCorrectionMatrix();
            SizeCorrectionMatrix inverse = matrix.Inverse();
            using (Mat originalPoints = correctedPoints.Clone())
            using (Mat originalSidePoints = new Mat())
            {
                // 基准线方向只应用逆矩阵的线性部分，线上一点还需应用逆平移。
                double originalLineX = inverse.M00 * lineX + inverse.M01 * lineY;
                double originalLineY = inverse.M10 * lineX + inverse.M11 * lineY;
                inverse.Transform(anchorX, anchorY, out double originalAnchorX, out double originalAnchorY);
                ApplySizeCorrectionMatrix(originalPoints, inverse);
                filteredPoints.Create(0, 0, MatType.CV_64FC1);
                int status = FilterGluePointsBySide(originalPoints.CvPtr,
                    originalLineX, originalLineY, originalAnchorX, originalAnchorY, keepSide, originalSidePoints.CvPtr);
                if (status == 0 && !originalSidePoints.Empty())
                {
                    ApplySizeCorrectionMatrix(originalSidePoints, matrix);
                    originalSidePoints.CopyTo(filteredPoints);
                }
                return status;
            }
        }

        /// <summary>先在矫正前坐标拟合，再变换整条基准线，避免比例改变 RANSAC 的内点集合。</summary>
        public static int FitBaseLineBeforeSizeCorrection(Mat correctedPoints, ImageSet imageSet, double distanceThreshold,
            out double lineX, out double lineY, out double anchorX, out double anchorY)
        {
            lineX = lineY = anchorX = anchorY = 0;
            SizeCorrectionMatrix matrix = imageSet.GetSizeCorrectionMatrix();
            using (Mat originalPoints = correctedPoints.Clone())
            {
                ApplySizeCorrectionMatrix(originalPoints, matrix.Inverse());
                int status = ransacFitLineFast(originalPoints.CvPtr, out double originalLineX, out double originalLineY,
                    out double originalAnchorX, out double originalAnchorY, (float)distanceThreshold);
                if (status != 0)
                    return status;
                double vx = matrix.M00 * originalLineX + matrix.M01 * originalLineY;
                double vy = matrix.M10 * originalLineX + matrix.M11 * originalLineY;
                double length = Math.Sqrt(vx * vx + vy * vy);
                matrix.Transform(originalAnchorX, originalAnchorY, out double x, out double y);
                if (!SizeCorrectionMatrix.IsFinite(length) || length < 1e-12
                    || !SizeCorrectionMatrix.IsFinite(x) || !SizeCorrectionMatrix.IsFinite(y))
                    return -1;
                lineX = vx / length;
                lineY = vy / length;
                anchorX = x;
                anchorY = y;
                return 0;
            }
        }

        /// <summary>复制框几何和显示属性，避免跨图片共享可变对象。</summary>
        public static RectData CloneBaseLineRegion(RectData region)
        {
            if (region == null) return null;
            return new RectData
            {
                Id = region.Id, X = region.X, Y = region.Y,
                Width = region.Width, Height = region.Height, Angle = region.Angle,
                IsDraggable = region.IsDraggable, IsRotatable = region.IsRotatable,
                StrokeColor = region.StrokeColor, StrokeThickness = region.StrokeThickness
            };
        }

        private static void ValidateBaseLineRegion(RectData region)
        {
            if (region == null || !SizeCorrectionMatrix.IsFinite(region.CenterX)
                || !SizeCorrectionMatrix.IsFinite(region.CenterY) || !SizeCorrectionMatrix.IsFinite(region.Angle)
                || !SizeCorrectionMatrix.IsFinite(region.Width) || !SizeCorrectionMatrix.IsFinite(region.Height)
                || region.Width <= 0 || region.Height <= 0)
                throw new InvalidOperationException("基准线检测框无效。");
        }

        /// <summary>原图框使用当前图像像素坐标；传给标定的点保留相机偏移。</summary>
        public static Mat ExtractImageBaseLinePoints(Mat image, CamParam camera, ImageSet frame, Mat extractedPixels = null)
        {
            if (image == null || image.Empty()) throw new InvalidOperationException("基准线原图为空。");
            if ((frame.baseLineImageWidth > 0 && frame.baseLineImageWidth != image.Cols)
                || (frame.baseLineImageHeight > 0 && frame.baseLineImageHeight != image.Rows))
                throw new InvalidOperationException("原图尺寸已改变，请重新设置基准线框。");
            var points = new List<OpenCvSharp.Point2d>();
            var seen = new HashSet<long>();
            int left = frame.启用裁剪 ? (int)(frame.LeftX * image.Cols) : 0;
            int top = frame.启用裁剪 ? (int)(frame.TopY * image.Rows) : 0;
            int width = frame.启用裁剪 ? (int)((frame.RightX - frame.LeftX) * image.Cols) : image.Cols;
            int height = frame.启用裁剪 ? (int)((frame.DownY - frame.TopY) * image.Rows) : image.Rows;
            if (left < 0 || top < 0 || width <= 0 || height <= 0 || left + width > image.Cols || top + height > image.Rows)
                throw new InvalidOperationException("裁剪范围无效。");
            Mat ownedPixels = null, additionalPixels = null;
            try
            {
                if (extractedPixels == null)
                {
                    using (Mat crop = new Mat(image, new OpenCvSharp.Rect(left, top, width, height)))
                        getLaserPosition(crop, frame.minThreshold, frame.laserMinWidth, out ownedPixels, camera.OffsetX + left, camera.OffsetY + top);
                    extractedPixels = ownedPixels;
                }
                bool needsAdditionalPoints = false;
                foreach (RectData region in new[] { frame.baseLineRegion1, frame.baseLineRegion2 })
                {
                    ValidateBaseLineRegion(region);
                    double angle = region.Angle * Math.PI / 180;
                    double halfX = (Math.Abs(Math.Cos(angle)) * region.Width + Math.Abs(Math.Sin(angle)) * region.Height) / 2;
                    double halfY = (Math.Abs(Math.Sin(angle)) * region.Width + Math.Abs(Math.Cos(angle)) * region.Height) / 2;
                    needsAdditionalPoints |= region.CenterX - halfX < left || region.CenterX + halfX >= left + width
                        || region.CenterY - halfY < top || region.CenterY + halfY >= top + height;
                }
                // ROI 内复用胶轮廓提取结果；仅对 ROI 外的基准区域补充完整原图的点。
                if (frame.启用裁剪 && needsAdditionalPoints)
                    getLaserPosition(image, frame.minThreshold, frame.laserMinWidth, out additionalPixels, camera.OffsetX, camera.OffsetY);
                Mat[] sources = { extractedPixels, additionalPixels };
                foreach (RectData region in new[] { frame.baseLineRegion1, frame.baseLineRegion2 })
                {
                    ValidateBaseLineRegion(region);
                    double angle = region.Angle * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
                    int selectedCount = 0;
                    for (int source = 0; source < sources.Length; source++)
                    {
                        Mat pixels = sources[source];
                        if (pixels == null || pixels.Empty()) continue;
                        if (pixels.Cols != 2 || pixels.Type() != MatType.CV_64FC1)
                            throw new InvalidOperationException("激光点坐标格式无效。");
                        for (int i = 0; i < pixels.Rows; i++)
                        {
                            double x = pixels.At<double>(i, 0), y = pixels.At<double>(i, 1);
                            if (!SizeCorrectionMatrix.IsFinite(x) || !SizeCorrectionMatrix.IsFinite(y)) continue;
                            double rawX = x - camera.OffsetX, rawY = y - camera.OffsetY;
                            if (source == 1 && rawX >= left && rawX < left + width && rawY >= top && rawY < top + height) continue;
                            double dx = rawX - region.CenterX, dy = rawY - region.CenterY;
                            if (Math.Abs(dx * cos + dy * sin) > region.Width / 2
                                || Math.Abs(-dx * sin + dy * cos) > region.Height / 2) continue;
                            selectedCount++;
                            if (seen.Add(((long)source << 32) + i)) points.Add(new OpenCvSharp.Point2d(x, y));
                        }
                    }
                    if (selectedCount < 2) throw new InvalidOperationException("基准线检测框内没有足够的有效激光点。");
                }
            }
            finally { ownedPixels?.Dispose(); additionalPixels?.Dispose(); }
            var result = new Mat(points.Count, 2, MatType.CV_64FC1);
            for (int i = 0; i < points.Count; i++)
            {
                result.At<double>(i, 0) = points[i].X;
                result.At<double>(i, 1) = points[i].Y;
            }
            return result;
        }

        /// <summary>基准点先标定到物理平面；拟合容差与旧流程一致，最终线再同步尺寸矫正。</summary>
        public static BaseLineResult PrepareImageBaseLine(Mat image, CamParam camera, CameraParameters calibration,
            PoseParameters lightPose, ImageSet frame, CutSet cut, double angleScale = 1, bool applySizeCorrection = true, Mat extractedPixels = null)
        {
            if (!frame.isUseBaseLine || !frame.baseLineInImageCoordinates) return null;
            try
            {
                if (cut.scaleSize <= 0 || !SizeCorrectionMatrix.IsFinite(angleScale) || angleScale <= 1e-6)
                    throw new InvalidOperationException("基准线坐标转换比例无效。");
                using (Mat pixels = ExtractImageBaseLinePoints(image, camera, frame, extractedPixels))
                {
                    GetXY(calibration, lightPose, pixels, out Mat physical);
                    using (physical)
                    {
                        scalePoint(physical, cut, 90 - lightPose.rx, out Mat scaled);
                        using (scaled)
                        {
                            for (int i = 0; i < scaled.Rows; i++) scaled.At<double>(i, 0) *= angleScale;
                            int status = ransacFitLineFast(scaled.CvPtr, out double vx, out double vy,
                                out double x, out double y, 0.5f * cut.scaleSize);
                            if (status != 0) return BaseLineResult.Failure("原图基准点拟合失败（状态 " + status + "）。");
                            BaseLineResult line = BaseLineResult.Create(vx, vy, x, y);
                            return applySizeCorrection ? TransformBaseLine(line, frame.GetSizeCorrectionMatrix()) : line;
                        }
                    }
                }
            }
            catch (Exception ex) { return BaseLineResult.Failure(ex.Message); }
        }

        public static BaseLineResult TransformBaseLine(BaseLineResult line, SizeCorrectionMatrix matrix)
        {
            if (line == null || !line.IsValid) return line;
            matrix.Validate();
            // 方向只应用线性部分，线上一点同时应用平移。
            matrix.Transform(line.X, line.Y, out double x, out double y);
            return BaseLineResult.Create(matrix.M00 * line.Vx + matrix.M01 * line.Vy,
                matrix.M10 * line.Vx + matrix.M11 * line.Vy, x, y);
        }

        /// <summary>
        /// 旧轮廓框通过标定映射的反向采样迁移到原图，避免假定像素到物理平面为仿射变换。
        /// 不写磁盘；两个框均转换成功后才由调用方提交。采样外接矩形需要调试人员复核。
        /// </summary>
        public static RectData[] ConvertLegacyBaseLineRegions(Mat image, CamParam camera, CameraParameters calibration,
            PoseParameters lightPose, ImageSet frame, CutSet cut, double angleScale)
        {
            if (image == null || image.Empty() || cut.scaleSize <= 0 || !SizeCorrectionMatrix.IsFinite(angleScale) || angleScale <= 1e-6)
                throw new InvalidOperationException("没有有效的原图或基准线转换参数。");
            List<OpenCvSharp.Point2f>[] support = GetLegacyBaseLineSupport(image, camera, calibration, lightPose, frame, cut, angleScale);
            const int step = 2;
            int columns = (image.Cols + step - 1) / step, rows = (image.Rows + step - 1) / step;
            using (Mat pixels = new Mat(columns * rows, 2, MatType.CV_64FC1))
            {
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        pixels.At<double>(index, 0) = col * step + camera.OffsetX;
                        pixels.At<double>(index, 1) = row * step + camera.OffsetY;
                    }
                GetXY(calibration, lightPose, pixels, out Mat physical);
                using (physical)
                {
                    scalePoint(physical, cut, 90 - lightPose.rx, out Mat contour);
                    using (contour)
                    {
                        for (int i = 0; i < contour.Rows; i++) contour.At<double>(i, 0) *= angleScale;
                        ApplySizeCorrection(contour, frame);
                        var converted = new List<RectData>();
                        foreach (RectData region in new[] { frame.baseLineRegion1, frame.baseLineRegion2 })
                        {
                            ValidateBaseLineRegion(region);
                            double angle = region.Angle * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
                            var selected = new List<OpenCvSharp.Point2f>();
                            for (int i = 0; i < contour.Rows; i++)
                            {
                                double dx = contour.At<double>(i, 0) - region.CenterX, dy = contour.At<double>(i, 1) - region.CenterY;
                                if (Math.Abs(dx * cos + dy * sin) <= region.Width / 2
                                    && Math.Abs(-dx * sin + dy * cos) <= region.Height / 2)
                                    selected.Add(new OpenCvSharp.Point2f((float)(pixels.At<double>(i, 0) - camera.OffsetX),
                                        (float)(pixels.At<double>(i, 1) - camera.OffsetY)));
                            }
                            if (selected.Count < 3) throw new InvalidOperationException("旧基准框无法映射到当前原图，请检查标定、图像和角度补偿参数。");
                            RotatedRect bounds = Cv2.MinAreaRect(selected);
                            RectData mapped = CloneBaseLineRegion(region);
                            mapped.Width = Math.Max(4, bounds.Size.Width + 2 * step);
                            mapped.Height = Math.Max(4, bounds.Size.Height + 2 * step);
                            mapped.X = bounds.Center.X - mapped.Width / 2;
                            mapped.Y = bounds.Center.Y - mapped.Height / 2;
                            mapped.Angle = bounds.Angle;
                            // 反投影外接框会扩大切向选点范围；用旧框实际选中的激光点恢复取点跨度。
                            List<OpenCvSharp.Point2f> laserPoints = support[converted.Count];
                            if (laserPoints.Count >= 2)
                            {
                                double a = mapped.Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                                double minU = double.PositiveInfinity, maxU = double.NegativeInfinity;
                                double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
                                foreach (OpenCvSharp.Point2f point in laserPoints)
                                {
                                    double dx = point.X - bounds.Center.X, dy = point.Y - bounds.Center.Y;
                                    double u = dx * c + dy * s, v = -dx * s + dy * c;
                                    minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
                                    minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
                                }
                                double cx = bounds.Center.X, cy = bounds.Center.Y;
                                if (maxU - minU >= maxV - minV)
                                {
                                    mapped.Width = Math.Max(2, maxU - minU + 0.5);
                                    double mid = (minU + maxU) / 2; cx += mid * c; cy += mid * s;
                                }
                                else
                                {
                                    mapped.Height = Math.Max(2, maxV - minV + 0.5);
                                    double mid = (minV + maxV) / 2; cx -= mid * s; cy += mid * c;
                                }
                                mapped.X = cx - mapped.Width / 2;
                                mapped.Y = cy - mapped.Height / 2;
                            }
                            converted.Add(mapped);
                        }
                        return converted.ToArray();
                    }
                }
            }
        }

        private static List<OpenCvSharp.Point2f>[] GetLegacyBaseLineSupport(Mat image, CamParam camera,
            CameraParameters calibration, PoseParameters lightPose, ImageSet frame, CutSet cut, double angleScale)
        {
            var support = new[] { new List<OpenCvSharp.Point2f>(), new List<OpenCvSharp.Point2f>() };
            int left = frame.启用裁剪 ? (int)(frame.LeftX * image.Cols) : 0;
            int top = frame.启用裁剪 ? (int)(frame.TopY * image.Rows) : 0;
            int width = frame.启用裁剪 ? (int)((frame.RightX - frame.LeftX) * image.Cols) : image.Cols;
            int height = frame.启用裁剪 ? (int)((frame.DownY - frame.TopY) * image.Rows) : image.Rows;
            using (Mat crop = new Mat(image, new OpenCvSharp.Rect(left, top, width, height)))
            {
                getLaserPosition(crop, frame.minThreshold, frame.laserMinWidth, out Mat pixels, camera.OffsetX + left, camera.OffsetY + top);
                using (pixels)
                {
                    if (pixels.Empty()) return support;
                    GetXY(calibration, lightPose, pixels, out Mat physical);
                    using (physical)
                    {
                        scalePoint(physical, cut, 90 - lightPose.rx, out Mat contour);
                        using (contour)
                        {
                            for (int i = 0; i < contour.Rows; i++) contour.At<double>(i, 0) *= angleScale;
                            ApplySizeCorrection(contour, frame);
                            Mat filtered;
                            if (frame.离散去噪)
                                TrajectoryDiscreteFilter(contour, out filtered, frame.分段距离 * cut.scaleSize, frame.成段点数);
                            else filtered = contour.Clone();
                            using (filtered)
                            {
                                var kept = new HashSet<Tuple<double, double>>();
                                for (int i = 0; i < filtered.Rows; i++) kept.Add(Tuple.Create(filtered.At<double>(i, 0), filtered.At<double>(i, 1)));
                                RectData[] regions = { frame.baseLineRegion1, frame.baseLineRegion2 };
                                for (int r = 0; r < regions.Length; r++)
                                {
                                    RectData region = regions[r]; ValidateBaseLineRegion(region);
                                    double a = region.Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                                    for (int i = 0; i < contour.Rows; i++)
                                    {
                                        double x = contour.At<double>(i, 0), y = contour.At<double>(i, 1);
                                        if (!kept.Contains(Tuple.Create(x, y))) continue;
                                        double dx = x - region.CenterX, dy = y - region.CenterY;
                                        if (Math.Abs(dx * c + dy * s) <= region.Width / 2 && Math.Abs(-dx * s + dy * c) <= region.Height / 2)
                                            support[r].Add(new OpenCvSharp.Point2f((float)(pixels.At<double>(i, 0) - camera.OffsetX),
                                                (float)(pixels.At<double>(i, 1) - camera.OffsetY)));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return support;
        }

        /// <summary>当前有效的胶宽检测方式：0 整胶，1 左侧，2 右侧。</summary>
        public static int GetGlueWidthSide(ImageSet imageSet)
        {
            return imageSet.isUseBaseLine && imageSet.isUseHalfGlueWidth ? imageSet.glueWidthSide : 0;
        }

        public static void GetGlueWidthRange(ImageSet imageSet, int side, out double minimum, out double maximum)
        {
            minimum = side == 1 ? imageSet.leftGlueWidthMin : side == 2 ? imageSet.rightGlueWidthMin : imageSet.widthMin;
            maximum = side == 1 ? imageSet.leftGlueWidthMax : side == 2 ? imageSet.rightGlueWidthMax : imageSet.widthMax;
        }

        private static bool IsRectangleHorizontal(double phi)
        {
            double normalizedPhi = phi % Math.PI;
            if (normalizedPhi >= Math.PI / 2)
                normalizedPhi -= Math.PI;
            else if (normalizedPhi < -Math.PI / 2)
                normalizedPhi += Math.PI;
            return Math.Abs(normalizedPhi) <= Math.PI / 4;
        }

        /// <summary>按最终检测区域的胶宽、胶高和面积判定结果。</summary>
        public static void judgeGlueResult(ImageSet imageSet,int scaleSize,double centerX,double centerY, double width, double height, double phi, double maxArea, out Data resultData, out BResult bResult)
        {
            resultData = new Data();
            bResult = new BResult();

            resultData.row = centerY;
            resultData.column = centerX;

            // 最小外接矩形的 width/height 是沿矩形自身方向的尺寸，
            // 转换为图像坐标系尺寸：竖直方向为 glueHeight，横向为 glueWidth。
            bool rectangleIsHorizontal = IsRectangleHorizontal(phi);
            resultData.glueHeight = (rectangleIsHorizontal ? height : width) / scaleSize;
            resultData.glueWidth = (rectangleIsHorizontal ? width : height) / scaleSize;
            resultData.glueWidthAngle = rectangleIsHorizontal ? phi : phi + Math.PI / 2;
            resultData.glueWidthSide = GetGlueWidthSide(imageSet);
            //resultData.glueArea = (heng ? width : height) / scaleSize;

            resultData.glueArea = maxArea / (scaleSize * scaleSize);
            if (resultData.glueHeight >= imageSet.heightMin && resultData.glueHeight <= imageSet.heightMax)
            {
                bResult.glueHeight = true;
            }
            GetGlueWidthRange(imageSet, resultData.glueWidthSide, out double widthMinimum, out double widthMaximum);
            if (resultData.glueWidth >= widthMinimum && resultData.glueWidth <= widthMaximum)
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
        public static void XLDDataDivide(XLDData XLDData, int divideCount, out List<double> rows,
            out List<double> cols, out List<double> angles)
        {
            rows = new List<double>();
            cols = new List<double>();
            angles = new List<double>();
            if (XLDData.ControlRows.Length >= 2)
            {
                using (Mat inputPointMat = Mat.Zeros(XLDData.ControlRows.Length, 2, MatType.CV_64FC1))
                using (Mat dividedPoints = new Mat())
                {
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
            // 转激光坐标系。lightXY 是输出结果，由调用方负责释放。
            GetXY(hCamPar, LightInCam, imagePoint, out lightXY);

            using (Mat lightXY4 = Mat.Zeros(lightXY.Rows, 4, MatType.CV_64FC1))
            using (Mat ones = Mat.Ones(lightXY.Rows, 1, MatType.CV_64FC1))
            using (Mat lightXY4Col3 = lightXY4.Col(3))
            using (Mat lightXY4XY = lightXY4[new OpenCvSharp.Rect(0, 0, 2, lightXY4.Rows)])
            using (Mat camXY4 = new Mat())
            using (Mat toolXY4 = new Mat())
            using (Mat robotXY4 = new Mat())
            using (Mat toolToRobot = new Mat())
            {
                ones.CopyTo(lightXY4Col3);
                lightXY.CopyTo(lightXY4XY);

                affineTransPoint3d(lightXY4.CvPtr, camXY4.CvPtr, LightToCam.CvPtr, false);
                affineTransPoint3d(camXY4.CvPtr, toolXY4.CvPtr, CamToTool.CvPtr, false);


                robotX = new List<double>();
                robotY = new List<double>();
                robotZ = new List<double>();

                if (robotPose != null)
                {
                    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                   robotPose.rx, robotPose.ry, robotPose.rz, toolToRobot.CvPtr);
                    Vision.affineTransPoint3d(toolXY4.CvPtr, robotXY4.CvPtr, toolToRobot.CvPtr, false);

                  
                    for (int i = 0; i < robotXY4.Rows; i++)
                    {
                        robotX.Add(robotXY4.At<double>(i, 0));
                        robotY.Add(robotXY4.At<double>(i, 1));
                        robotZ.Add(robotXY4.At<double>(i, 2));
                    }
                }
               
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
            using (Mat lightXY4 = Mat.Zeros(lightXY.Rows, 4, MatType.CV_64FC1))
            using (Mat ones = Mat.Ones(lightXY.Rows, 1, MatType.CV_64FC1))
            using (Mat lightXY4Col3 = lightXY4.Col(3))
            using (Mat lightXY4XY = lightXY4[new OpenCvSharp.Rect(0, 0, 2, lightXY4.Rows)])
            using (Mat camXY4 = new Mat())
            using (Mat toolXY4 = new Mat())
            using (Mat robotXY4 = new Mat())
            using (Mat toolToRobot = new Mat())
            {
                ones.CopyTo(lightXY4Col3);
                lightXY.CopyTo(lightXY4XY);

                affineTransPoint3d(lightXY4.CvPtr, camXY4.CvPtr, LightToCam.CvPtr, false);
                affineTransPoint3d(camXY4.CvPtr, toolXY4.CvPtr, CamToTool.CvPtr, false);

                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                    robotPose.rx, robotPose.ry, robotPose.rz, toolToRobot.CvPtr);
                Vision.affineTransPoint3d(toolXY4.CvPtr, robotXY4.CvPtr, toolToRobot.CvPtr, false);

                robotX = new List<double>();
                robotY = new List<double>();
                robotZ = new List<double>();
                for (int i = 0; i < robotXY4.Rows; i++)
                {
                    robotX.Add(robotXY4.At<double>(i, 0));
                    robotY.Add(robotXY4.At<double>(i, 1));
                    robotZ.Add(robotXY4.At<double>(i, 2));
                }
            }
        }
        /// <summary>
        /// 用于显示和矫正计算，要结合激光和相机的夹角，进行
        /// </summary>
        /// <param name="lightXYcut"></param>
        /// <param name="cutSet"></param>
        /// <param name="XY_10um"></param>
        public static void scalePoint(Mat lightXYcut, CutSet cutSet, double lightAngle, out Mat XY_10um)
        {
            XY_10um = new Mat(lightXYcut.Size(), lightXYcut.Type());

            using (Mat inputX = lightXYcut.Col(0))
            using (Mat outputX = XY_10um.Col(0))
            {
                Cv2.Multiply(inputX, new Scalar(1000 * cutSet.scaleSize), outputX);
                Cv2.Add(outputX, new Scalar(cutSet.ShowWidth * cutSet.scaleSize / 2), outputX);
            }

            using (Mat inputY = lightXYcut.Col(1))
            using (Mat outputY = XY_10um.Col(1))
            {
                Cv2.Multiply(inputY,
                    new Scalar(1000 * cutSet.scaleSize * Math.Cos(lightAngle / 180 * Math.PI)), outputY);
                Cv2.Add(outputY, new Scalar(cutSet.ShowHeight * cutSet.scaleSize / 2), outputY);
            }
        }
        /// <summary>
        /// 检测轮廓相邻点之间的最大间隔是否超过气泡检测阈值。
        /// 存在任意相邻点间隔大于阈值时返回 false；点数不足两个时视为通过。
        /// </summary>
        /// <param name="points">按轮廓顺序排列的 N×2 点矩阵，列 0 为横向坐标，列 1 为竖直坐标。</param>
        /// <param name="maxAllowedDistance">允许的最大点间隔，单位与 points 一致。</param>
        /// <param name="maxDistance">检测到的相邻点最大间隔。</param>
        /// <param name="startX">最大间隔起点的横向坐标。</param>
        /// <param name="startY">最大间隔起点的竖直坐标。</param>
        /// <param name="endX">最大间隔终点的横向坐标。</param>
        /// <param name="endY">最大间隔终点的竖直坐标。</param>
        /// <param name="gapPointValid">是否成功记录了最大间隔对应的两个点。</param>
        /// <returns>所有相邻点间隔均不超过阈值时返回 true，否则返回 false。</returns>
        public static bool DetectBubble(Mat points, double maxAllowedDistance,
            out double maxDistance, out double startX, out double startY,
            out double endX, out double endY, out bool gapPointValid)
        {
            maxDistance = 0;
            startX = 0;
            startY = 0;
            endX = 0;
            endY = 0;
            gapPointValid = false;
            if (points == null || points.Empty() || points.Rows < 2)
            {
                return true;
            }

            if (points.Cols < 2 || double.IsNaN(maxAllowedDistance) ||
                double.IsInfinity(maxAllowedDistance) || maxAllowedDistance < 0)
            {
                return false;
            }

            for (int index = 1; index < points.Rows; index++)
            {
                double dx = points.At<double>(index, 0) - points.At<double>(index - 1, 0);
                double dy = points.At<double>(index, 1) - points.At<double>(index - 1, 1);
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (double.IsNaN(distance) || double.IsInfinity(distance))
                {
                    return false;
                }

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    startX = points.At<double>(index - 1, 0);
                    startY = points.At<double>(index - 1, 1);
                    endX = points.At<double>(index, 0);
                    endY = points.At<double>(index, 1);
                    gapPointValid = true;
                }
            }

            return maxDistance <= maxAllowedDistance;
        }

        public static void singleFrameDetAndResult(Mat OutLine,ImageSet imageSet,CutSet cutSet, ref bool singleFrameExistGlue, ref Data resultData, ref BResult bResult, ref Mat outMaxRegion, ref Mat outRegionRectangle2, Mat bubblePoints = null, Action<string> detectionTrace = null, BaseLineResult baseLine = null)
        {

            bool existGlue = false;
            double centerX = 0;
            double centerY = 0;
            double phi = 0;
            double width = 0;
            double height = 0;
            double maxArea = 0;
            double item0 = 0;
            double item1 = 0;
            double item2 = 0;
            double item3 = 0;
            int glueWidthSide = GetGlueWidthSide(imageSet);
            singleFrameExistGlue = false;

           
            //////开始检测
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();



            if (imageSet.isUseBaseLine)
            {
                // 拟合点提取和基准线筛选均为本次检测的临时 Mat。
                using (Mat selectBaseLinePoints1 = new Mat())
                using (Mat selectBaseLinePoints2 = new Mat())
                using (Mat selectBaseLinePoints = new Mat())
                using (Mat selectSpongeStripPoints = new Mat())
                using (Mat selectRangePoints = new Mat())
                {
                    int baselineStatus;
                    if (imageSet.baseLineInImageCoordinates)
                    {
                        baselineStatus = baseLine != null && baseLine.IsValid ? 0 : -1;
                        if (baselineStatus == 0)
                        {
                            item0 = baseLine.Vx; item1 = baseLine.Vy; item2 = baseLine.X; item3 = baseLine.Y;
                        }
                        else detectionTrace?.Invoke(baseLine?.Error ?? "本帧缺少由原图基准框计算的基准线。");
                    }
                    else
                    {
                        Vision.GetPointsInRotatedRect(OutLine.CvPtr, selectBaseLinePoints1.CvPtr,
                            imageSet.baseLineRegion1.CenterX, imageSet.baseLineRegion1.CenterY,
                            imageSet.baseLineRegion1.Width, imageSet.baseLineRegion1.Height,
                            imageSet.baseLineRegion1.Angle);

                        Vision.GetPointsInRotatedRect(OutLine.CvPtr, selectBaseLinePoints2.CvPtr,
                            imageSet.baseLineRegion2.CenterX, imageSet.baseLineRegion2.CenterY,
                            imageSet.baseLineRegion2.Width, imageSet.baseLineRegion2.Height,
                            imageSet.baseLineRegion2.Angle);

                        Cv2.VConcat(selectBaseLinePoints1, selectBaseLinePoints2, selectBaseLinePoints);
                        detectionTrace?.Invoke($"基准线框内 {selectBaseLinePoints1.Rows}+{selectBaseLinePoints2.Rows} 点");

                        float disThre = 0.5f;
                        baselineStatus = FitBaseLineBeforeSizeCorrection(selectBaseLinePoints, imageSet,
                            disThre * cutSet.scaleSize, out item0, out item1, out item2, out item3);
                    }
                    if (baselineStatus != 0)
                    {
                        detectionTrace?.Invoke($"基准线拟合失败（状态 {baselineStatus}）");
                        outMaxRegion.Create(0, 0, MatType.CV_64FC1);
                        outRegionRectangle2.Create(0, 0, MatType.CV_64FC1);
                        resultData = new Data { glueWidthSide = glueWidthSide };
                        bResult = new BResult { Result = false };
                        return;
                    }

                    Vision.FilterPointsByLine(OutLine.CvPtr, item0, item1, item2, item3,
                        imageSet.distBaseLineThre * cutSet.scaleSize, selectSpongeStripPoints.CvPtr);
                    detectionTrace?.Invoke($"忽略高度 {imageSet.distBaseLineThre:F3} mm 后剩 {selectSpongeStripPoints.Rows} 点");

                    Vision.FilterPointsByLineRange(selectSpongeStripPoints.CvPtr, item0, item1, item2, item3,
                        imageSet.distBaseLineRangeLower * cutSet.scaleSize,
                        imageSet.distBaseLineRangeUpper * cutSet.scaleSize, selectRangePoints.CvPtr);
                    detectionTrace?.Invoke($"屏蔽区间 [{imageSet.distBaseLineRangeLower:F3}, {imageSet.distBaseLineRangeUpper:F3}] mm 后剩 {selectRangePoints.Rows} 点");

                    if (glueWidthSide == 0)
                    {
                        int status = Vision.glueDet(selectRangePoints.CvPtr, item0, item1, item2, item3,
                            out existGlue, out centerX, out centerY, out phi, out width, out height,
                            out maxArea, outMaxRegion.CvPtr, outRegionRectangle2.CvPtr);
                        if (!existGlue || maxArea <= 0)
                            detectionTrace?.Invoke($"整胶区域构建失败（状态 {status}）");
                    }
                    else
                    {
                        // 半侧筛选后直接检测，全部尺寸、中心和显示区域均使用所选半侧结果。
                        using (Mat sidePoints = new Mat())
                        {
                            outMaxRegion.Create(0, 0, MatType.CV_64FC1);
                            outRegionRectangle2.Create(0, 0, MatType.CV_64FC1);
                            int sideStatus = Vision.FilterGlueSideBeforeSizeCorrection(selectRangePoints, imageSet,
                                item0, item1, item2, item3, glueWidthSide, sidePoints);
                            detectionTrace?.Invoke($"{(glueWidthSide == 1 ? "左" : "右")}侧筛选后 {sidePoints.Rows} 点（状态 {sideStatus}）");
                            if (sideStatus == 0 && !sidePoints.Empty())
                            {
                                int status = Vision.glueDet(sidePoints.CvPtr, item0, item1, item2, item3,
                                    out existGlue, out centerX, out centerY, out phi,
                                    out width, out height, out maxArea,
                                    outMaxRegion.CvPtr, outRegionRectangle2.CvPtr);
                                if (!existGlue || maxArea <= 0)
                                    detectionTrace?.Invoke($"半侧区域构建失败（状态 {status}）");
                                existGlue = existGlue && maxArea > 0;
                            }
                            if (!existGlue)
                            {
                                // 半侧检测失败时清空本次区域，不能退回整胶区域或沿用上一帧结果。
                                outMaxRegion.Create(0, 0, MatType.CV_64FC1);
                                outRegionRectangle2.Create(0, 0, MatType.CV_64FC1);
                            }
                        }
                    }
                }
            }
            else
            {
                int status = Vision.singleFrameDet(OutLine.CvPtr, out existGlue, out centerX, out centerY, out phi, out width, out height, out maxArea,
                outMaxRegion.CvPtr, outRegionRectangle2.CvPtr, imageSet.isUseAngleOpt, false);
                if (!existGlue || maxArea <= 0)
                    detectionTrace?.Invoke($"轮廓 {OutLine.Rows} 点，区域构建失败（状态 {status}）");
            }

            //stopwatch.Stop();
            ////结束检测
            //TimeSpan elapsedTime = stopwatch.Elapsed;
            //double useTime = elapsedTime.TotalMilliseconds;

            //Console.WriteLine($"singleFrameDet use time:{useTime}ms");

            // 气泡检测使用修正后的原始轮廓点，避免离散滤波掩盖点间大间隔。
            bool bubbleResult = true;
            double bubblePointDistance = 0;
            double bubbleGapStartX = 0;
            double bubbleGapStartY = 0;
            double bubbleGapEndX = 0;
            double bubbleGapEndY = 0;
            bool bubbleGapPointValid = false;
            double bubblePointDistanceThreshold = imageSet.bubblePointMinDistance * cutSet.scaleSize;
            if (imageSet.isUseBubbleDet)
            {
                bubbleResult = DetectBubble(bubblePoints ?? OutLine, bubblePointDistanceThreshold,
                    out bubblePointDistance, out bubbleGapStartX, out bubbleGapStartY,
                    out bubbleGapEndX, out bubbleGapEndY, out bubbleGapPointValid);
            }

            bResult = new BResult();
            resultData = new Data();

            if (existGlue && maxArea > 0)
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

            // 未检出涂胶时也保留基准线，供结果界面显示屏蔽区间。
            resultData.item0 = item0;
            resultData.item1 = item1;
            resultData.item2 = item2;
            resultData.item3 = item3;
            resultData.glueWidthSide = glueWidthSide;



            // 保存气泡检测结果和阈值，供主流程、结果记录及 ShowImageData 使用。
            resultData.bubbleResult = bubbleResult;
            resultData.bubblePointDistance = bubblePointDistance;
            resultData.bubblePointDistanceThreshold = bubblePointDistanceThreshold;
            resultData.bubbleGapPointValid = bubbleGapPointValid;
            resultData.bubbleGapStartX = bubbleGapStartX;
            resultData.bubbleGapStartY = bubbleGapStartY;
            resultData.bubbleGapEndX = bubbleGapEndX;
            resultData.bubbleGapEndY = bubbleGapEndY;
            bResult.bubbleResult = bubbleResult;
            if (!bubbleResult)
            {
                bResult.Result = false;
            }
        }

        public static PoseParameters PoseInv(PoseParameters robotPose)
        {
            PoseParameters BaseInTool = new PoseParameters();
            using (Mat ToolToBase = new Mat())
            {
                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                    robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                using (Mat BaseToTool = ToolToBase.Inv())
                {
                    BaseInTool.PoseType = robotPose.PoseType;
                    Vision.HomMat3dToPose(BaseInTool.PoseType, out double centerX, out double centerY,
                        out double centerZ, out double centerRX, out double centerRY, out double centerRZ,
                        BaseToTool.CvPtr);

                    BaseInTool.x = centerX;
                    BaseInTool.y = centerY;
                    BaseInTool.z = centerZ;
                    BaseInTool.rx = centerRX;
                    BaseInTool.ry = centerRY;
                    BaseInTool.rz = centerRZ;
                }
            }
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

    public class Setting : IDisposable
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

        public void Dispose()
        {
            image?.Dispose();
            image = null;
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
            image?.Dispose();
            image = null;
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
                    if (image!= null && !image.Empty())
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
                    if (mapper.isCalib())
                    {
                        //XLDDatas = new List<XLDData>();
                        string fPath = basePath + "ProjectMapping.json";
                        result &= mapper.SaveToFile(fPath);
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

        // 同一相机、同一检测段连续 NG 达到此图片数后，段结果和总结果才置为 NG。
        public int consecutiveNGCount = 5;

        



        public void AfterDeserialize()
        {
            if (consecutiveNGCount < 1)
            {
                consecutiveNGCount = 5;
            }
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

    /// <summary>仅属于本帧的基准线，不存入车型参数；坐标必须与检测轮廓一致。</summary>
    public sealed class BaseLineResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public double Vx { get; private set; }
        public double Vy { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }

        public static BaseLineResult Failure(string error)
        {
            return new BaseLineResult { Error = error };
        }

        public static BaseLineResult Create(double vx, double vy, double x, double y)
        {
            double length = Math.Sqrt(vx * vx + vy * vy);
            if (!SizeCorrectionMatrix.IsFinite(length) || length < 1e-12
                || !SizeCorrectionMatrix.IsFinite(x) || !SizeCorrectionMatrix.IsFinite(y))
                return Failure("基准线拟合结果无效。");
            return new BaseLineResult { IsValid = true, Vx = vx / length, Vy = vy / length, X = x, Y = y };
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
        // 原有黄色范围继续参与检测判断。
        public double widthMin = 2, widthMax = 4;
        public double leftGlueWidthMin = 1, leftGlueWidthMax = 2;
        public double rightGlueWidthMin = 1, rightGlueWidthMax = 2;
        public double heightMin = 2, heightMax = 4;
        public double areaMin = 4, areaMax = 16;
        // 绿色范围仅供设置和保存，不参与检测判断；旧配置未设置时沿用对应黄色范围。
        public double? greenWidthMin, greenWidthMax;
        public double? greenLeftGlueWidthMin, greenLeftGlueWidthMax;
        public double? greenRightGlueWidthMin, greenRightGlueWidthMax;
        public double? greenHeightMin, greenHeightMax;
        public double? greenAreaMin, greenAreaMax;
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

        // 历史 X/Y 系数：旧 XML 没有 sizeCorrectionMatrix 时按对角矩阵使用。
        public double correctionScaleSizeX = 1;
        public double correctionScaleSizeY = 1;

        public SizeCorrectionMatrix sizeCorrectionMatrix;

        public SizeCorrectionMatrix GetSizeCorrectionMatrix()
        {
            return sizeCorrectionMatrix ?? SizeCorrectionMatrix.FromLegacyScale(correctionScaleSizeX, correctionScaleSizeY);
        }

        // 是否使用基线
        public bool isUseBaseLine = false;
        // 每张图片独立启用、选择半侧；需同时启用基准面检测。1 为左侧，2 为右侧。
        public bool isUseHalfGlueWidth = false;
        public int glueWidthSide = 1;
        public double distBaseLineThre = 0.5;
        // 基准面距离屏蔽区间，单位为 mm；旧配置未包含这两个参数时默认均为 0。
        public double distBaseLineRangeLower = 0;
        public double distBaseLineRangeUpper = 0;

        // 是否启用气泡检测
        public bool isUseBubbleDet = false;
        // 气泡检测允许的最大点间隔；界面参数单位为 mm，计算时换算到轨迹坐标单位。
        public double bubblePointMinDistance = 5;
        public RectData baseLineRegion1 = new RectData();
        public RectData baseLineRegion2 = new RectData();
        // 旧 XML 默认为轮廓坐标；原图示教后明确标记，禁止混用两种坐标。
        public bool baseLineInImageCoordinates = false;
        public int baseLineImageWidth = 0;
        public int baseLineImageHeight = 0;

        public ImageSet(int index)
        {
            this.Index = index;
        }
        ImageSet() { }
        public ImageSet Clone()
        {
            var clone = (ImageSet)MemberwiseClone();
            clone.sizeCorrectionMatrix = sizeCorrectionMatrix?.Clone();
            clone.baseLineRegion1 = Vision.CloneBaseLineRegion(baseLineRegion1);
            clone.baseLineRegion2 = Vision.CloneBaseLineRegion(baseLineRegion2);
            return clone;
        }
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
        // 与最终 glueWidth 对应的矩形方向，单位为弧度；无有效检测时禁止用于矫正。
        public double glueWidthAngle = double.NaN;
        // 本次结果的胶宽检测方式：0 整胶，1 左侧，2 右侧。
        public int glueWidthSide = 0;

        // 基准线拟合参数：方向向量 (item0, item1) 和线上一点 (item2, item3)。
        public double item0;
        public double item1;
        public double item2;
        public double item3;

        // 气泡检测结果、实际最大点间隔和检测阈值。
        public bool bubbleResult = true;
        public double bubblePointDistance = 0;
        public double bubblePointDistanceThreshold = 0;
        // 气泡超限间隔对应的两个点坐标。
        public bool bubbleGapPointValid = false;
        public double bubbleGapStartX = 0;
        public double bubbleGapStartY = 0;
        public double bubbleGapEndX = 0;
        public double bubbleGapEndY = 0;

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
                glueWidthAngle = this.glueWidthAngle,
                glueWidthSide = this.glueWidthSide,
                item0 = this.item0,
                item1 = this.item1,
                item2 = this.item2,
                item3 = this.item3,
                bubbleResult = this.bubbleResult,
                bubblePointDistance = this.bubblePointDistance,
                bubblePointDistanceThreshold = this.bubblePointDistanceThreshold,
                bubbleGapPointValid = this.bubbleGapPointValid,
                bubbleGapStartX = this.bubbleGapStartX,
                bubbleGapStartY = this.bubbleGapStartY,
                bubbleGapEndX = this.bubbleGapEndX,
                bubbleGapEndY = this.bubbleGapEndY,
            };
        }
    }
    [Serializable]
    public class BResult
    {
        public bool glueHeight ;
        public bool glueWidth ;
        public bool glueArea ;

        // 气泡检测结果
        public bool bubbleResult = true;

      
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
                bubbleResult = this.bubbleResult,
                
                Result = this.Result,
            };
        }
    }
}
