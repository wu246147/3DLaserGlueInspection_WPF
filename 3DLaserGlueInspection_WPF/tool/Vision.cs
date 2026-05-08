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
using OpenCvSharp;
using Wpf_Replace_halcon;
using System.Xml.Serialization;
using System.Diagnostics;


namespace _3DLaserGlueInspection
{
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

            ////临时添加
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
            //临时显示
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
        /// 点坐标转换，从相机转为激光坐标系和机器人坐标系
        /// </summary>
        /// <param name="imagePoint"></param>
        /// <param name="hCamPar"></param>
        /// <param name="LightInCam"></param>
        /// <param name="LightToCam"></param>
        /// <param name="CamToTool"></param>
        /// <param name="robotPose"></param>
        /// <param name="lightXY"></param>
        /// <param name="robotX"></param>
        /// <param name="robotY"></param>
        /// <param name="robotZ"></param>
        public static void pointTransform2CamAndRobot(Mat imagePoint, CameraParameters hCamPar, PoseParameters LightInCam,
            Mat LightToCam, Mat CamToTool, PoseParameters robotPose, out Mat lightXY, out List<double> robotX, out List<double> robotY,
            out List<double> robotZ)
        {
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

        /// <summary>
        /// 单帧检测整体算法
        /// </summary>
        /// <param name="camParam"></param>
        /// <param name="hCamPar"></param>
        /// <param name="LightInCam"></param>
        /// <param name="detImage"></param>
        /// <param name="cutSet"></param>
        /// <param name="imageSet"></param>
        /// <param name="singleFrameExistGlue"></param>
        /// <param name="singleFrameExistOutline"></param>
        /// <param name="resultData"></param>
        /// <param name="bResult"></param>
        /// <param name="outMaxRegion"></param>
        /// <param name="outRegionRectangle2"></param>
        /// <param name="hXLDCont10mm"></param>
        /// <param name="xy"></param>
        /// <param name="lightXY"></param>
        public static void singleFrameDetTotal(CamParam camParam, CameraParameters hCamPar, PoseParameters LightInCam, Mat detImage, CutSet cutSet, ImageSet imageSet, ref bool singleFrameExistGlue, ref bool singleFrameExistOutline, ref Data resultData, ref BResult bResult, ref Mat outMaxRegion, ref Mat outRegionRectangle2, ref Mat hXLDCont10mm, Mat xy, Mat lightXY,double robotAndCamAngle)
        {
            Mat OutLine;
            Mat lightXYcut;
            if (imageSet.启用裁剪)
            {
                Vision.cutLight(xy, camParam, hCamPar, LightInCam, detImage, imageSet, out lightXYcut);
            }
            else
            {
                lightXYcut = lightXY;
            }

            //Vision.showMatPoint(lightXY, "lightXY");
            //Vision.showMatPoint(lightXYcut, "lightXYcut");

            if (lightXYcut.Rows > 0)
            {
                singleFrameExistOutline = true;
                //单帧检测(使用激光坐标系)
                //轮廓只计算整数，所以数据单位放大至0.01mm，并把原点移至画布中心
                Mat XY_10um;
                scalePoint(lightXYcut, cutSet, 90 - LightInCam.rx, out XY_10um);

                if (cutSet.isUseAngleOpt)
                {
                    //对x方向进行矫正
                    double scaleX = 1;
                    scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                    Mat correctionPoints = new Mat();
                    correctionPoints = hXLDCont10mm.Clone();

                    for (int id = 0; id < correctionPoints.Rows; id++)
                    {
                        correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                    }

                    hXLDCont10mm = correctionPoints.Clone();
                }
                {
                    //对两个方向进行矫正
                    double scaleX = cutSet.correctionScaleSizeX;
                    double scaleY = cutSet.correctionScaleSizeY;

                    Mat correctionPoints = new Mat();
                    correctionPoints = hXLDCont10mm.Clone();

                    for (int id = 0; id < correctionPoints.Rows; id++)
                    {
                        correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                        correctionPoints.At<double>(id, 1) = correctionPoints.At<double>(id, 1) * scaleY;

                    }

                    hXLDCont10mm = correctionPoints.Clone();
                }
                //离散滤波
                if (imageSet.离散去噪)
                {
                    Vision.TrajectoryDiscreteFilter(XY_10um, out hXLDCont10mm, imageSet.分段距离 * cutSet.scaleSize, imageSet.成段点数);
                    OutLine = hXLDCont10mm.Clone();
                }
                else
                {
                    hXLDCont10mm = XY_10um.Clone();
                    OutLine = XY_10um.Clone();
                }

                //Vision.showMatPoint(hXLDCont10mm, "hXLDCont10mm");

                //如果存在
                if (!OutLine.Empty())
                {
                    singleFrameDetAndResult(OutLine,imageSet,cutSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
                }
            }
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
                outMaxRegion.CvPtr, outRegionRectangle2.CvPtr, cutSet.isUseAngleOpt, false);


            //stopwatch.Stop();
            ////结束检测
            //TimeSpan elapsedTime = stopwatch.Elapsed;
            //double useTime = elapsedTime.TotalMilliseconds;

            //Console.WriteLine($"singleFrameDet use time:{useTime}ms");


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
        public double glueHeight;
        public double glueWidth;
        public double glueArea;

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
        public bool glueHeight;
        public bool glueWidth;
        public bool glueArea;
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
