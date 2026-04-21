using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using OpenCvSharp;

namespace Wpf_Replace_halcon
{
    public class CameraParameters
    {
        public CameraParameters Clone() { return (CameraParameters)this.MemberwiseClone(); }

        public string CameraModel { get; set; } = "area_scan_division";  // 如"area_scan_division"
        public double Focus { get; set; } = 0.008;        // 焦距（单位：米）
        public double Kappa { get; set; } = 0;          // 径向畸变系数 (单位：m^-2)
        public double Sx { get; set; } = 5.2e-06;            // 传感器上两个相邻单元之间的水平距离（m/像素）
        public double Sy { get; set; } = 5.2e-06;          // 传感器上两个相邻单元之间的垂直距离（m/像素）
        public double Cx { get; set; } = 640;          // 主点X坐标（像素）
        public double Cy { get; set; } = 512;    // 主点Y坐标（像素）
        public int ImageWidth { get; set; } = 1280;     // 图像宽度 
        public int ImageHeight { get; set; } = 1024;    // 图像高度 
    }

    public class PoseParameters
    {
        public PoseParameters Clone() { return (PoseParameters)this.MemberwiseClone(); }

        public int PoseType { get; set; } = 2;
        public double rx { get; set; } = 0;
        public double ry { get; set; } = 0;
        public double rz { get; set; } = 0;
        public double x { get; set; } = 0;
        public double y { get; set; } = 0;
        public double z { get; set; } = 0;
    }
    static class HMatrixTransform
    {

        static public int getInternalReferenceMat(CameraParameters cameraParameters, out Mat internalReferenceMatrix, out Mat distCoeffs)
        {
            internalReferenceMatrix = Mat.Eye(new Size(3, 3), MatType.CV_64F);
            distCoeffs = Mat.Zeros(new Size(5, 1), MatType.CV_64F);

            internalReferenceMatrix.At<double>(0, 0) = cameraParameters.Focus / cameraParameters.Sx;
            internalReferenceMatrix.At<double>(0, 2) = cameraParameters.Cx;
            internalReferenceMatrix.At<double>(1, 1) = cameraParameters.Focus / cameraParameters.Sy;
            internalReferenceMatrix.At<double>(1, 2) = cameraParameters.Cy;

            distCoeffs.At<double>(0, 0) = -cameraParameters.Kappa;
            return 0;
        }

        //static public int imagePointsToWorldPlane(CameraParameters cameraParameters, PoseParameters poseParameters, double[] xs, double[] ys, out double[] hx, out double[] hy)
        //{
        //    hx = new double[xs.Length];
        //    hy = new double[ys.Length];

        //    getInternalReferenceMat(cameraParameters, out Mat internalReferenceMatrix, out Mat distCoeffs);
        //    poseToHomMat3d(poseParameters, out Mat extrinsicMatrix);

        //    Mat extrinsicMatrix_3_4 = Mat.Zeros(new Size(3, 4), MatType.CV_64F);
        //    extrinsicMatrix_3_4.At<double>(0, 0) = extrinsicMatrix.At<double>(0, 0);
        //    extrinsicMatrix_3_4.At<double>(0, 1) = extrinsicMatrix.At<double>(0, 1);
        //    extrinsicMatrix_3_4.At<double>(0, 2) = extrinsicMatrix.At<double>(0, 2);
        //    extrinsicMatrix_3_4.At<double>(0, 3) = extrinsicMatrix.At<double>(0, 3);
        //    extrinsicMatrix_3_4.At<double>(1, 0) = extrinsicMatrix.At<double>(1, 0);
        //    extrinsicMatrix_3_4.At<double>(1, 1) = extrinsicMatrix.At<double>(1, 1);
        //    extrinsicMatrix_3_4.At<double>(1, 2) = extrinsicMatrix.At<double>(1, 2);
        //    extrinsicMatrix_3_4.At<double>(1, 3) = extrinsicMatrix.At<double>(1, 3);
        //    extrinsicMatrix_3_4.At<double>(2, 0) = extrinsicMatrix.At<double>(2, 0);
        //    extrinsicMatrix_3_4.At<double>(2, 1) = extrinsicMatrix.At<double>(2, 1);
        //    extrinsicMatrix_3_4.At<double>(2, 2) = extrinsicMatrix.At<double>(2, 2);
        //    extrinsicMatrix_3_4.At<double>(2, 3) = extrinsicMatrix.At<double>(2, 3);


        //    Mat srcMat = Mat.Zeros(new Size(xs.Length, 2), MatType.CV_64F);
        //    Mat dstMat = new Mat();
        //    Mat worldMat = new Mat();

        //    for (int i = 0; i < xs.Length; i++)
        //    {
        //        srcMat.At<double>(i, 0) = xs[i];
        //        srcMat.At<double>(i, 1) = ys[i];
        //    }

        //    Cv2.UndistortPoints(srcMat, dstMat, internalReferenceMatrix, distCoeffs);

        //    Cv2.PerspectiveTransform(dstMat, worldMat, extrinsicMatrix_3_4);

        //    for (int i = 0; i < xs.Length; i++)
        //    {
        //        hx[i] = worldMat.At<double>(i, 0);
        //        hy[i] = worldMat.At<double>(i, 1);
        //    }

        //    return 0;
        //}

        static public int mathHPose(PoseParameters inputPoseParameters1, PoseParameters inputPoseParameters2, out PoseParameters outputPoseParameters, double s)
        {
            outputPoseParameters = new PoseParameters();

            outputPoseParameters.x = inputPoseParameters1.x + (inputPoseParameters2.x - inputPoseParameters1.x) * s;
            outputPoseParameters.y = inputPoseParameters1.y + (inputPoseParameters2.y - inputPoseParameters1.y) * s;
            outputPoseParameters.z = inputPoseParameters1.z + (inputPoseParameters2.z - inputPoseParameters1.z) * s;

            if (inputPoseParameters1.rx > 0 && inputPoseParameters2.rx < 0)
            {
                outputPoseParameters.rx = inputPoseParameters1.rx + (inputPoseParameters2.rx + 360 - inputPoseParameters1.rx) * s;
            }
            else if (inputPoseParameters1.rx < 0 && inputPoseParameters2.rx > 0)
            {
                outputPoseParameters.rx = inputPoseParameters1.rx + (inputPoseParameters2.rx - 360 - inputPoseParameters1.rx) * s;
            }
            else
            {
                outputPoseParameters.rx = inputPoseParameters1.rx + (inputPoseParameters2.rx - inputPoseParameters1.rx) * s;
            }


            if (inputPoseParameters1.ry > 0 && inputPoseParameters2.ry < 0)
            {
                outputPoseParameters.ry = inputPoseParameters1.ry + (inputPoseParameters2.ry + 360 - inputPoseParameters1.ry) * s;
            }
            else if (inputPoseParameters1.ry < 0 && inputPoseParameters2.ry > 0)
            {
                outputPoseParameters.ry = inputPoseParameters1.ry + (inputPoseParameters2.ry - 360 - inputPoseParameters1.ry) * s;
            }
            else
            {
                outputPoseParameters.ry = inputPoseParameters1.ry + (inputPoseParameters2.ry - inputPoseParameters1.ry) * s;
            }


            if (inputPoseParameters1.rz > 0 && inputPoseParameters2.rz < 0)
            {
                outputPoseParameters.rz = inputPoseParameters1.rz + (inputPoseParameters2.rz + 360 - inputPoseParameters1.rz) * s;
            }
            else if (inputPoseParameters1.rz < 0 && inputPoseParameters2.rz > 0)
            {
                outputPoseParameters.rz = inputPoseParameters1.rz + (inputPoseParameters2.rz - 360 - inputPoseParameters1.rz) * s;
            }
            else
            {
                outputPoseParameters.rz = inputPoseParameters1.rz + (inputPoseParameters2.rz - inputPoseParameters1.rz) * s;
            }


            return 0;
        }

        ///// <summary>
        ///// 位姿转转换矩阵
        ///// </summary>
        ///// <param name="poseParameters">位姿参数</param>
        ///// <param name="mat">返回4*3的外参矩阵</param>
        ///// <returns></returns>
        //static public int poseToHomMat3d(PoseParameters poseParameters, out Mat mat)
        //{
        //    mat = Mat.Zeros(new Size(4, 4), MatType.CV_64F);

        //    double value = 0;
        //    value = Math.Cos(poseParameters.ry * 3.14 / 180) * Math.Cos(poseParameters.rz * 3.14 / 180);
        //    Console.WriteLine(value);

        //    mat.At<double>(0, 0) = value;
        //    value = Math.Sin(poseParameters.rx * 3.14 / 180) * Math.Sin(poseParameters.ry * 3.14 / 180) * Math.Cos(poseParameters.rz * 3.14 / 180) - Math.Cos(poseParameters.rx * 3.14 / 180) * Math.Sin(poseParameters.rz * 3.14 / 180);
        //    Console.WriteLine(value);

        //    mat.At<double>(0, 1) = value;
        //    mat.At<double>(0, 2) = Math.Sin(poseParameters.ry * 3.14 / 180) * Math.Cos(poseParameters.rx * 3.14 / 180) * Math.Cos(poseParameters.rz * 3.14 / 180) + Math.Sin(poseParameters.rx * 3.14 / 180) * Math.Sin(poseParameters.rz * 3.14 / 180);
        //    mat.At<double>(1, 0) = Math.Cos(poseParameters.ry * 3.14 / 180) * Math.Sin(poseParameters.rz * 3.14 / 180);
        //    mat.At<double>(1, 1) = Math.Sin(poseParameters.rx * 3.14 / 180) * Math.Sin(poseParameters.ry * 3.14 / 180) * Math.Sin(poseParameters.rz * 3.14 / 180) + Math.Cos(poseParameters.rx * 3.14 / 180) * Math.Cos(poseParameters.rz * 3.14 / 180);
        //    mat.At<double>(1, 2) = Math.Sin(poseParameters.ry * 3.14 / 180) * Math.Cos(poseParameters.rx * 3.14 / 180) * Math.Sin(poseParameters.rz * 3.14 / 180) - Math.Sin(poseParameters.rx * 3.14 / 180) * Math.Cos(poseParameters.rz * 3.14 / 180);
        //    value = -Math.Sin(poseParameters.ry * 3.14 / 180);
        //    mat.At<double>(2, 0) = value;
        //    mat.At<double>(2, 1) = Math.Sin(poseParameters.rx * 3.14 / 180) * Math.Cos(poseParameters.ry * 3.14 / 180);
        //    mat.At<double>(2, 2) = Math.Cos(poseParameters.rx * 3.14 / 180) * Math.Cos(poseParameters.ry * 3.14 / 180);

        //    mat.At<double>(0, 3) = poseParameters.x;
        //    mat.At<double>(1, 3) = poseParameters.y;
        //    mat.At<double>(2, 3) = poseParameters.z;

        //    mat.At<double>(3, 0) = 0;
        //    mat.At<double>(3, 1) = 0;
        //    mat.At<double>(3, 2) = 0;

        //    mat.At<double>(3, 3) = 1;

        //    //string info = $"posePara Mat:(({mat.At<double>(0, 0)}, " +
        //    //    $"{mat.At<double>(0, 1)}, " +
        //    //    $"{mat.At<double>(0, 2)}, " +
        //    //    $"{mat.At<double>(0, 3)}), " +
        //    //    $"({mat.At<double>(1, 0)}, " +
        //    //    $"{mat.At<double>(1, 1)}, " +
        //    //    $"{mat.At<double>(1, 2)}, " +
        //    //    $"{mat.At<double>(1, 3)}), " +
        //    //    $"({mat.At<double>(2, 0)}, " +
        //    //    $"{mat.At<double>(2, 1)}, " +
        //    //    $"{mat.At<double>(2, 2)}, " +
        //    //    $"{mat.At<double>(2, 3)}), " +
        //    //    $"({mat.At<double>(3, 0)}, " +
        //    //    $"{mat.At<double>(3, 1)}, " +
        //    //    $"{mat.At<double>(3, 2)}, " +
        //    //    $"{mat.At<double>(3, 3)}), " +
        //    //    $")\r--------------------------------------\r";

        //    //Console.WriteLine(info);

        //    return 0;
        //}

        //static public int affineTransPoint3d(Mat mat, double[] xs, double[] ys, double[] zs, out double[] transformXs, out double[] transformYs, out double[] transformZs)
        //{

        //    //Point3f[] points = { new Point3f(1, 2, 3), new Point3f(4, 5, 6) };

        //    //// 执行变换 
        //    //Mat pointsMat2 = new Mat(2, 3, MatType.CV_32FC1);

        //    transformXs = new double[xs.Length];
        //    transformYs = new double[ys.Length];
        //    transformZs = new double[zs.Length];


        //    Mat pointsMat = Mat.Zeros(new Size(xs.Length, 1), MatType.CV_64FC3);

        //    for (int i = 0; i < xs.Length; i++)
        //    {
        //        pointsMat.At<double>(i, 0) = xs[i];
        //        pointsMat.At<double>(i, 1) = ys[i];
        //        pointsMat.At<double>(i, 2) = zs[i];

        //    }
        //    Mat homogeneous = new Mat();
        //    Cv2.HConcat(pointsMat, Mat.Ones(xs.Length, 1, MatType.CV_64FC1), homogeneous);

        //    Console.WriteLine(mat.Size());
        //    Console.WriteLine(mat.Channels());

        //    Console.WriteLine(homogeneous.Size());
        //    Console.WriteLine(homogeneous.Channels());

        //    Mat result = homogeneous.Transform(mat);
            
            

        //    for (int i = 0; i < xs.Length; i++)
        //    {
        //        transformXs[i] = result.At<double>(i, 0);
        //        transformYs[i] = result.At<double>(i, 1);
        //        transformZs[i] = result.At<double>(i, 2);

        //    }

        //    return 0;
        //}
    }
}
