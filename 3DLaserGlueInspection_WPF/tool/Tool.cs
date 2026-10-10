using _3DLaserGlueInspection.subForm;
//using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace _3DLaserGlueInspection
{
    public static class GeneralFunc
    {

        //private static void ChangeControlLanguateFun(ComponentResourceManager resources, Control control)
        //{
        //    //将资源与控件对应
        //    resources.ApplyResources(control, control.Name);
        //    if (control.HasChildren)//子控件，比如组合框GroupBox里的控件
        //    {
        //        foreach (Control controls in control.Controls)
        //            ChangeControlLanguateFun(resources, controls);
        //    }
        //    if (control is MenuStrip)//菜单栏控件
        //    {
        //        MenuStrip ms = (MenuStrip)control;
        //        if (ms.Items.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (ToolStripMenuItem ts in ms.Items)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //                if (ts.DropDownItems.Count > 0)
        //                {
        //                    foreach (ToolStripMenuItem tts in ts.DropDownItems)//子菜单
        //                    {
        //                        resources.ApplyResources(tts, tts.Name);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    if (control is DataGridView)//菜单栏控件
        //    {
        //        DataGridView dgv = (DataGridView)control;
        //        if (dgv.Columns.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (DataGridViewTextBoxColumn ts in dgv.Columns)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //            }
        //        }
        //    }
        //}

        #region  界面翻译
        //public static void ChangeLanguateFun(Type t, Form f)
        //{
        //    int currentLcid = 2052; //1033代表英文，2052代表中文
        //    if (GlobalVarAndFunc.LANGUAGE_ID == 1)
        //    {
        //        currentLcid = 1033;
        //    }
        //    Thread.CurrentThread.CurrentUICulture = new CultureInfo(currentLcid);
        //    ComponentResourceManager resources = new ComponentResourceManager(t);
        //    resources.ApplyResources(f, "$this");//窗体标题


        //    foreach (Control control in f.Controls)//循环当前界面所有的控件
        //    {
        //        ChangeControlLanguateFun(resources, control);
        //    }
        //    //刷新窗体，有时窗体标题无法切换成功，需要刷新一下
        //    f.Refresh();
        //}
        #endregion
    }

    /// <summary>
    /// Euler-angle conversion used by the former HALCON implementation.
    /// All public angles are in degrees.
    /// PoseType 0 = XYZ/abg, 1 = ZYX/gba, 2 = ZYZ.
    /// </summary>
    public static class CoordinateTransform
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private const double SingularityEpsilon = 1e-12;

        /// <summary>Convert XYZ/abg to ZYX/gba.</summary>
        public static void xyz2zyx(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToGba(CreateAbg(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Convert XYZ/abg to ZYZ.</summary>
        public static void xyz2zyz(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToZyz(CreateAbg(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Convert ZYX/gba to XYZ/abg.</summary>
        public static void zyx2xyz(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToAbg(CreateGba(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Convert ZYX/gba to ZYZ.</summary>
        public static void zyx2zyz(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToZyz(CreateGba(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Convert ZYZ to XYZ/abg.</summary>
        public static void zyz2xyz(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToAbg(CreateZyz(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Convert ZYZ to ZYX/gba.</summary>
        public static void zyz2zyx(double r1, double r2, double r3, out double R1_new, out double R2_new, out double R3_new)
        {
            MatrixToGba(CreateZyz(r1, r2, r3), out R1_new, out R2_new, out R3_new);
        }

        /// <summary>Equivalent to the original transformCartPose2 function.</summary>
        public static int transformCartPose2(double RX, double RY, double RZ, int originCarPoseType, ref double transformRX, ref double transformRY, ref double transformRZ, int dstCarPoseType)
        {
            ValidatePoseType(originCarPoseType, "originCarPoseType");
            ValidatePoseType(dstCarPoseType, "dstCarPoseType");

            if (originCarPoseType == dstCarPoseType)
            {
                transformRX = RX;
                transformRY = RY;
                transformRZ = RZ;
                return 0;
            }

            RotationMatrix rotation = CreateRotation(RX, RY, RZ, originCarPoseType);
            switch (dstCarPoseType)
            {
                case 0:
                    MatrixToAbg(rotation, out transformRX, out transformRY, out transformRZ);
                    break;
                case 1:
                    MatrixToGba(rotation, out transformRX, out transformRY, out transformRZ);
                    break;
                default:
                    MatrixToZyz(rotation, out transformRX, out transformRY, out transformRZ);
                    break;
            }
            return 0;
        }

        /// <summary>PascalCase alias for new call sites.</summary>
        public static int TransformCartPose2(double RX, double RY, double RZ, int originCarPoseType, ref double transformRX, ref double transformRY, ref double transformRZ, int dstCarPoseType)
        {
            return transformCartPose2(RX, RY, RZ, originCarPoseType, ref transformRX, ref transformRY, ref transformRZ, dstCarPoseType);
        }

        private static RotationMatrix CreateRotation(double r1, double r2, double r3, int poseType)
        {
            switch (poseType)
            {
                case 0: return CreateAbg(r1, r2, r3);
                case 1: return CreateGba(r1, r2, r3);
                default: return CreateZyz(r1, r2, r3);
            }
        }

        // HALCON "abg": Rz(r3) * Ry(r2) * Rx(r1).
        private static RotationMatrix CreateAbg(double r1, double r2, double r3)
        {
            double x = r1 * DegToRad;
            double y = r2 * DegToRad;
            double z = r3 * DegToRad;
            double sx = Math.Sin(x), cx = Math.Cos(x);
            double sy = Math.Sin(y), cy = Math.Cos(y);
            double sz = Math.Sin(z), cz = Math.Cos(z);

            return new RotationMatrix
            {
                M00 = cz * cy,
                M01 = cz * sy * sx - sz * cx,
                M02 = cz * sy * cx + sz * sx,
                M10 = sz * cy,
                M11 = sz * sy * sx + cz * cx,
                M12 = sz * sy * cx - cz * sx,
                M20 = -sy,
                M21 = cy * sx,
                M22 = cy * cx
            };
        }

        // HALCON "gba": Rx(r1) * Ry(r2) * Rz(r3).
        private static RotationMatrix CreateGba(double r1, double r2, double r3)
        {
            double x = r1 * DegToRad;
            double y = r2 * DegToRad;
            double z = r3 * DegToRad;
            double sx = Math.Sin(x), cx = Math.Cos(x);
            double sy = Math.Sin(y), cy = Math.Cos(y);
            double sz = Math.Sin(z), cz = Math.Cos(z);

            return new RotationMatrix
            {
                M00 = cy * cz,
                M01 = -cy * sz,
                M02 = sy,
                M10 = sx * sy * cz + cx * sz,
                M11 = -sx * sy * sz + cx * cz,
                M12 = -sx * cy,
                M20 = -cx * sy * cz + sx * sz,
                M21 = cx * sy * sz + sx * cz,
                M22 = cx * cy
            };
        }

        // ZYZ: Rz(r1) * Ry(r2) * Rz(r3).
        private static RotationMatrix CreateZyz(double r1, double r2, double r3)
        {
            double z1 = r1 * DegToRad;
            double y = r2 * DegToRad;
            double z2 = r3 * DegToRad;
            double sz1 = Math.Sin(z1), cz1 = Math.Cos(z1);
            double sy = Math.Sin(y), cy = Math.Cos(y);
            double sz2 = Math.Sin(z2), cz2 = Math.Cos(z2);

            return new RotationMatrix
            {
                M00 = cz1 * cy * cz2 - sz1 * sz2,
                M01 = -cz1 * cy * sz2 - sz1 * cz2,
                M02 = cz1 * sy,
                M10 = sz1 * cy * cz2 + cz1 * sz2,
                M11 = -sz1 * cy * sz2 + cz1 * cz2,
                M12 = sz1 * sy,
                M20 = -sy * cz2,
                M21 = sy * sz2,
                M22 = cy
            };
        }

        // Inverse of Rz(r3) * Ry(r2) * Rx(r1).
        private static void MatrixToAbg(RotationMatrix m, out double r1, out double r2, out double r3)
        {
            double y = Math.Atan2(-m.M20, Hypot(m.M00, m.M10));
            double cy = Hypot(m.M00, m.M10);
            if (cy > SingularityEpsilon)
            {
                r1 = Math.Atan2(m.M21, m.M22);
                r3 = Math.Atan2(m.M10, m.M00);
            }
            else
            {
                // At |ry| = 90 degrees, rx and rz are not unique. Select rz = 0.
                r3 = 0.0;
                r1 = m.M20 < 0.0 ? Math.Atan2(m.M01, m.M02) : Math.Atan2(-m.M01, -m.M02);
            }
            r1 *= RadToDeg;
            r2 = y * RadToDeg;
            r3 *= RadToDeg;
        }

        // Inverse of Rx(r1) * Ry(r2) * Rz(r3).
        private static void MatrixToGba(RotationMatrix m, out double r1, out double r2, out double r3)
        {
            double y = Math.Atan2(m.M02, Hypot(m.M00, m.M01));
            double cy = Hypot(m.M00, m.M01);
            if (cy > SingularityEpsilon)
            {
                r1 = Math.Atan2(-m.M12, m.M22);
                r3 = Math.Atan2(-m.M01, m.M00);
            }
            else
            {
                // At |ry| = 90 degrees, rx and rz are not unique. Select rz = 0.
                r3 = 0.0;
                r1 = m.M02 > 0.0 ? Math.Atan2(m.M10, m.M11) : Math.Atan2(-m.M10, m.M11);
            }
            r1 *= RadToDeg;
            r2 = y * RadToDeg;
            r3 *= RadToDeg;
        }

        // Inverse of Rz(r1) * Ry(r2) * Rz(r3).
        // This matches the original HALCON indices H[6], H[2], H[10], H[9], H[8].
        private static void MatrixToZyz(RotationMatrix m, out double r1, out double r2, out double r3)
        {
            double c = Clamp(m.M22, -1.0, 1.0);
            double s = Hypot(m.M02, m.M12);
            double y = Math.Atan2(s, c);
            if (s > SingularityEpsilon)
            {
                r1 = Math.Atan2(m.M12, m.M02);
                r3 = Math.Atan2(m.M21, -m.M20);
            }
            else
            {
                // At ry = 0 or 180 degrees, the two z rotations are not unique.
                r1 = 0.0;
                r3 = c >= 0.0 ? Math.Atan2(m.M10, m.M00) : Math.Atan2(m.M10, -m.M00);
            }
            r1 *= RadToDeg;
            r2 = y * RadToDeg;
            r3 *= RadToDeg;
        }

        private static double Hypot(double a, double b)
        {
            return Math.Sqrt(a * a + b * b);
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static void ValidatePoseType(int poseType, string parameterName)
        {
            if (poseType < 0 || poseType > 2)
                throw new ArgumentOutOfRangeException(parameterName, poseType, "PoseType must be 0 (XYZ), 1 (ZYX), or 2 (ZYZ).");
        }

        private struct RotationMatrix
        {
            public double M00, M01, M02;
            public double M10, M11, M12;
            public double M20, M21, M22;
        }
    }

    public static class GlobalVarAndFunc
    {

//        #region
//        //坐标系转换
//        #region
//        public static void xyz2zyx(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {



//            // Local iconic variables 

//            // Local control variables 

//            HTuple hv_Pose = new HTuple(), hv_PoseOut = new HTuple();
//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_Pose.Dispose();
//            HOperatorSet.CreatePose(0, 0, 0, hv_r1, hv_r2, hv_r3, "Rp+T", "abg", "point",
//                out hv_Pose);
//            hv_PoseOut.Dispose();
//            HOperatorSet.ConvertPoseType(hv_Pose, "Rp+T", "gba", "point", out hv_PoseOut);
//            hv_R1_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R1_new = hv_PoseOut.TupleSelect(
//                    3);
//            }
//            hv_R2_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R2_new = hv_PoseOut.TupleSelect(
//                    4);
//            }
//            hv_R3_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R3_new = hv_PoseOut.TupleSelect(
//                    5);
//            }

//            hv_Pose.Dispose();
//            hv_PoseOut.Dispose();

//            return;
//        }

//        public static void xyz2zyz(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//            out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {



//            // Local iconic variables 

//            // Local control variables 

//            HTuple hv_Pose = new HTuple(), hv_HomMat3DXYZ = new HTuple();
//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_Pose.Dispose();
//            HOperatorSet.CreatePose(0, 0, 0, hv_r1, hv_r2, hv_r3, "Rp+T", "abg", "point",
//                out hv_Pose);
//            hv_HomMat3DXYZ.Dispose();
//            HOperatorSet.PoseToHomMat3d(hv_Pose, out hv_HomMat3DXYZ);
//            //ZYZ
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R1_new.Dispose();
//                HOperatorSet.TupleAtan2(hv_HomMat3DXYZ.TupleSelect(6), hv_HomMat3DXYZ.TupleSelect(
//                    2), out hv_R1_new);
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R2_new.Dispose();
//                HOperatorSet.TupleAtan2(((1 - (((hv_HomMat3DXYZ.TupleSelect(10))).TuplePow(2)))).TupleSqrt()
//                    , hv_HomMat3DXYZ.TupleSelect(10), out hv_R2_new);
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R3_new.Dispose();
//                HOperatorSet.TupleAtan2(hv_HomMat3DXYZ.TupleSelect(9), -(hv_HomMat3DXYZ.TupleSelect(
//                    8)), out hv_R3_new);
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                {
//                    HTuple
//                      ExpTmpLocalVar_R1_new = hv_R1_new.TupleDeg()
//                        ;
//                    hv_R1_new.Dispose();
//                    hv_R1_new = ExpTmpLocalVar_R1_new;
//                }
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                {
//                    HTuple
//                      ExpTmpLocalVar_R2_new = hv_R2_new.TupleDeg()
//                        ;
//                    hv_R2_new.Dispose();
//                    hv_R2_new = ExpTmpLocalVar_R2_new;
//                }
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                {
//                    HTuple
//                      ExpTmpLocalVar_R3_new = hv_R3_new.TupleDeg()
//                        ;
//                    hv_R3_new.Dispose();
//                    hv_R3_new = ExpTmpLocalVar_R3_new;
//                }
//            }

//            hv_Pose.Dispose();
//            hv_HomMat3DXYZ.Dispose();

//            return;
//        }

//        public static void zyx2xyz(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//            out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {



//            // Local iconic variables 

//            // Local control variables 

//            HTuple hv_Pose = new HTuple(), hv_PoseOut = new HTuple();
//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_Pose.Dispose();
//            HOperatorSet.CreatePose(0, 0, 0, hv_r1, hv_r2, hv_r3, "Rp+T", "gba", "point",
//                out hv_Pose);
//            hv_PoseOut.Dispose();
//            HOperatorSet.ConvertPoseType(hv_Pose, "Rp+T", "abg", "point", out hv_PoseOut);
//            hv_R1_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R1_new = hv_PoseOut.TupleSelect(
//                    3);
//            }
//            hv_R2_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R2_new = hv_PoseOut.TupleSelect(
//                    4);
//            }
//            hv_R3_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R3_new = hv_PoseOut.TupleSelect(
//                    5);
//            }

//            hv_Pose.Dispose();
//            hv_PoseOut.Dispose();

//            return;
//        }

//        public static void zyx2zyz(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//            out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {


//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_R1_new.Dispose(); hv_R2_new.Dispose(); hv_R3_new.Dispose();
//            zyx2xyz(hv_r1, hv_r2, hv_r3, out hv_R1_new, out hv_R2_new, out hv_R3_new);
//            {
//                HTuple ExpTmpOutVar_0; HTuple ExpTmpOutVar_1; HTuple ExpTmpOutVar_2;
//                xyz2zyz(hv_R1_new, hv_R2_new, hv_R3_new, out ExpTmpOutVar_0, out ExpTmpOutVar_1,
//                    out ExpTmpOutVar_2);
//                hv_R1_new.Dispose();
//                hv_R1_new = ExpTmpOutVar_0;
//                hv_R2_new.Dispose();
//                hv_R2_new = ExpTmpOutVar_1;
//                hv_R3_new.Dispose();
//                hv_R3_new = ExpTmpOutVar_2;
//            }


//            return;
//        }

//        public static void zyz2xyz(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//            out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {



//            // Local iconic variables 

//            // Local control variables 

//            HTuple hv_HomMat3DIdentity = new HTuple();
//            HTuple hv_HomMat3DRotate = new HTuple(), hv_HomMat3DRotate1 = new HTuple();
//            HTuple hv_HomMat3DRotate2 = new HTuple(), hv_Pose5 = new HTuple();
//            HTuple hv_PoseXYZ = new HTuple();
//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_HomMat3DIdentity.Dispose();
//            HOperatorSet.HomMat3dIdentity(out hv_HomMat3DIdentity);
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_HomMat3DRotate.Dispose();
//                HOperatorSet.HomMat3dRotateLocal(hv_HomMat3DIdentity, hv_r1.TupleRad(), "z",
//                    out hv_HomMat3DRotate);
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_HomMat3DRotate1.Dispose();
//                HOperatorSet.HomMat3dRotateLocal(hv_HomMat3DRotate, hv_r2.TupleRad(), "y", out hv_HomMat3DRotate1);
//            }
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_HomMat3DRotate2.Dispose();
//                HOperatorSet.HomMat3dRotateLocal(hv_HomMat3DRotate1, hv_r3.TupleRad(), "z", out hv_HomMat3DRotate2);
//            }
//            hv_Pose5.Dispose();
//            HOperatorSet.HomMat3dToPose(hv_HomMat3DRotate2, out hv_Pose5);
//            hv_PoseXYZ.Dispose();
//            HOperatorSet.ConvertPoseType(hv_Pose5, "Rp+T", "abg", "point", out hv_PoseXYZ);

//            hv_R1_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R1_new = hv_PoseXYZ.TupleSelect(
//                    3);
//            }
//            hv_R2_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R2_new = hv_PoseXYZ.TupleSelect(
//                    4);
//            }
//            hv_R3_new.Dispose();
//            using (HDevDisposeHelper dh = new HDevDisposeHelper())
//            {
//                hv_R3_new = hv_PoseXYZ.TupleSelect(
//                    5);
//            }

//            hv_HomMat3DIdentity.Dispose();
//            hv_HomMat3DRotate.Dispose();
//            hv_HomMat3DRotate1.Dispose();
//            hv_HomMat3DRotate2.Dispose();
//            hv_Pose5.Dispose();
//            hv_PoseXYZ.Dispose();

//            return;
//        }

//        public static void zyz2zyx(HTuple hv_r1, HTuple hv_r2, HTuple hv_r3, out HTuple hv_R1_new,
//            out HTuple hv_R2_new, out HTuple hv_R3_new)
//        {


//            // Initialize local and output iconic variables 
//            hv_R1_new = new HTuple();
//            hv_R2_new = new HTuple();
//            hv_R3_new = new HTuple();
//            hv_R1_new.Dispose(); hv_R2_new.Dispose(); hv_R3_new.Dispose();
//            zyz2xyz(hv_r1, hv_r2, hv_r3, out hv_R1_new, out hv_R2_new, out hv_R3_new);
//            {
//                HTuple ExpTmpOutVar_0; HTuple ExpTmpOutVar_1; HTuple ExpTmpOutVar_2;
//                xyz2zyx(hv_R1_new, hv_R2_new, hv_R3_new, out ExpTmpOutVar_0, out ExpTmpOutVar_1,
//                    out ExpTmpOutVar_2);
//                hv_R1_new.Dispose();
//                hv_R1_new = ExpTmpOutVar_0;
//                hv_R2_new.Dispose();
//                hv_R2_new = ExpTmpOutVar_1;
//                hv_R3_new.Dispose();
//                hv_R3_new = ExpTmpOutVar_2;
//            }


//            return;
//        }


//        #endregion


//        ///PoseType 0是xyz，1是zyx，2是zyz,这里是用halcon实现
//        public static int transformCartPose2(double RX, double RY, double RZ, int originCarPoseType,
//              ref double transformRX, ref double transformRY, ref double transformRZ, int dstCarPoseType)
//        {
//            if (originCarPoseType == 0)
//            {
//                if (dstCarPoseType == 0)
//                {
//                    transformRX = RX; transformRY = RY; transformRZ = RZ;
//                }
//                else if (dstCarPoseType == 1)
//                {
//                    xyz2zyx(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;

//                }
//                else
//                {
//                    xyz2zyz(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;
//                }
//            }
//            else if (originCarPoseType == 1)
//            {
//                if (dstCarPoseType == 0)
//                {
//                    zyx2xyz(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;
//                }
//                else if (dstCarPoseType == 1)
//                {
//                    transformRX = RX; transformRY = RY; transformRZ = RZ;

//                }
//                else
//                {
//                    zyx2zyz(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;
//                }
//            }
//            else
//            {
//                if (dstCarPoseType == 0)
//                {
//                    zyz2xyz(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;
//                }
//                else if (dstCarPoseType == 1)
//                {
//                    zyz2zyx(RX, RY, RZ, out HTuple hvR1, out HTuple hvR2, out HTuple hvR3);

//                    transformRX = hvR1.D;
//                    transformRY = hvR2.D;
//                    transformRZ = hvR3.D;
//                }
//                else
//                {
//                    transformRX = RX; transformRY = RY; transformRZ = RZ;

//                }
//            }


//            return 0;
//        }


//        #endregion


        public static void SwitchLanguage(string cultureName)
        {
            // 保存选择到本地
            //这里要现在项目里添加这个变量。项目 → 右键 → 属性 → 左侧找到"设置"
            GlobalVarAndFunc.LANGUAGE_ID = cultureName;
            WriteLanguageID();

            // 语言切换
            InitLanguage();
            // 弹窗提醒重启后，切换语言
            var result = MessageBox.Show(
                Resources.LanguageDict.LanguageChangeSuccess,
                Resources.LanguageDict.Prompt,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // // 重启应用，使 x:Static 重新按新语言加载
            //Process.Start(Application.ResourceAssembly.Location);
            //Application.Current.Shutdown();
            //
        }
        public static string LANGUAGE_ID = "zh-CN"; //zh-CN默认中文，en-US英文

        public static void ReadLanguageID()
        {
            string fPath = "Data\\LanguageID";
            if (File.Exists(fPath))
            {
                LANGUAGE_ID = File.ReadAllText(fPath);
            }
        }

        public static void WriteLanguageID()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }
            File.WriteAllText("Data\\LanguageID", GlobalVarAndFunc.LANGUAGE_ID);
        }


        /// <summary>
        /// 启动时恢复上次的语言设置
        /// </summary>
        public static void InitLanguage()
        {
            ReadLanguageID();
            if (!string.IsNullOrEmpty(LANGUAGE_ID))
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(LANGUAGE_ID);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(LANGUAGE_ID);
            }
        }

        /// <summary>
        /// 将旧版以中文文本为参数的翻译调用转换为当前 LanguageDict 资源。
        /// 支持资源中带或不带中英文冒号的文本；未找到资源时原样返回，避免日志内容丢失。
        /// </summary>
        /// <param name="info">待翻译的中文文本。</param>
        /// <returns>当前语言对应的文本；找不到匹配资源时返回原文本。</returns>
        public static string LanguageTranslate(string info)
        {
            if (string.IsNullOrEmpty(info))
            {
                return info;
            }

            string normalizedInfo = info.TrimEnd(' ', ':', '：');
            System.Resources.ResourceSet resourceSet =
                Resources.LanguageDict.ResourceManager.GetResourceSet(
                    CultureInfo.InvariantCulture, true, true);
            if (resourceSet != null)
            {
                foreach (System.Collections.DictionaryEntry entry in resourceSet)
                {
                    string resourceValue = entry.Value as string;
                    if (string.IsNullOrEmpty(resourceValue))
                    {
                        continue;
                    }

                    string normalizedResourceValue = resourceValue.TrimEnd(' ', ':', '：');
                    if (!string.Equals(normalizedResourceValue, normalizedInfo, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string translated = Resources.LanguageDict.ResourceManager.GetString(
                        entry.Key.ToString(), CultureInfo.CurrentUICulture);
                    if (string.IsNullOrEmpty(translated))
                    {
                        return info;
                    }

                    string suffix = info.Substring(normalizedInfo.Length);
                    if (suffix.Length > 0 && !translated.EndsWith(":", StringComparison.Ordinal)
                        && !translated.EndsWith("：", StringComparison.Ordinal))
                    {
                        translated += suffix.IndexOf("：", StringComparison.Ordinal) >= 0 ? ":" : suffix;
                    }
                    return translated;
                }
            }

            return info;
        }


        public static BitmapImage ConvertMatToBitmapImage(Mat mat)
        {            
            var bitmapImage = new BitmapImage();
            try
            {
                if (!mat.Empty())
                {
                    // 方法1：编码为JPEG流 
                    Cv2.ImEncode(".jpg", mat, out byte[] jpegData);

                    using (var stream = new MemoryStream(jpegData))
                    {
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
                    }
                }
            }
            catch (Exception ex)
            {
                
            }
           
            return bitmapImage;
        }



        public static void ShowImageData(int showWidth, int showHeight, CutSet cutSet, Mat hXLDCont10mm, ref ImageControl2 imageControl,ref bool showing, ref object olockShow, double offsetX = 0, double offsetY = 0)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        using (Mat mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3))
                        {
                            imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
                        }
                        //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0) + offsetX;
                            point.Y = hXLDCont10mm.At<double>(i, 1) + offsetY;
                            points.Add(point);
                        }
                        //DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        //imageControl.AddPolyline(points, Colors.Gray);

                        // 改成用点来显示
                        imageControl.AddPoints(points, Colors.Gray);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }
        public static string GetGlueWidthLabel(int side)
        {
            return side == 1 ? Resources.LanguageDict.LeftGlueWidth
                : side == 2 ? Resources.LanguageDict.RightGlueWidth : Resources.LanguageDict.GlueWidth;
        }

        public static void ShowImageData(int showWidth, int showHeight, CutSet cutSet,ImageSet set, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, BResult bResult,
             ref ImageControl2 imageControl, ref bool showing, ref object olockShow, double offsetX = 0, double offsetY = 0)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        using (Mat mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3))
                        {
                            //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                            imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
                        }

                        //Console.WriteLine($"mat.Size:{mat.Size()}");

                        //Console.WriteLine($"Polyline :");

                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0) + offsetX;
                            point.Y = hXLDCont10mm.At<double>(i, 1) + offsetY;
                            points.Add(point);

                            //Console.WriteLine($"point:{point}");

                        }
                        //DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        //imageControl.AddPolyline(points, Colors.Gray);

                        // 改成用点来显示
                        imageControl.AddPoints(points, Colors.Gray);


                      


                        if (!hRegion.Empty())
                        {
                            //Console.WriteLine($"text value :");
                            string text1 = GetGlueWidthLabel(data.glueWidthSide) + ":" + $"{data.glueWidth:0.00}mm";
                            string text2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + $"{data.glueHeight:0.00}mm";
                            string text3 = _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + $"{data.glueArea:0.00}mm²";

                            imageControl.AddTextBlock(text1, (bResult.glueWidth ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY));
                            imageControl.AddTextBlock(text2, (bResult.glueHeight ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY + 24));
                            imageControl.AddTextBlock(text3, (bResult.glueArea ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY + 48));

                            if (set.isUseBubbleDet)
                            {
                                double scale = cutSet.scaleSize > 0 ? cutSet.scaleSize : 1;
                                string bubbleText = $"{Resources.LanguageDict.BubbleDetection}: {data.bubblePointDistance / scale:0.00}mm";
                                imageControl.AddTextBlock(bubbleText, (bResult.bubbleResult ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY + 72));
                            }

                            //Console.WriteLine($"text result :");
                            //hWindowControl.DispTextInImage(text, data.row, data.column);


                            //Console.WriteLine($"region :");

                            PointCollection regionPoints = new PointCollection();
                            for (int i = 0; i < hRegion.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegion.At<double>(i, 0)+ offsetX;
                                point.Y = hRegion.At<double>(i, 1) + offsetY;
                                regionPoints.Add(point);
                                //Console.WriteLine($"point:{point}");
                            }

                            imageControl.AddPolygon(regionPoints, Color.FromArgb(128, 255, 0, 0), "fill");

                            //DispPolygonjHWindowControlEvent(regionPoints, Colors.Red, "fill");

                            //Console.WriteLine($"regionSmallestRectangle :");
                            PointCollection regionSmallestRectangle2Points = new PointCollection();
                            for (int i = 0; i < hRegionSmallestRectangle2.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegionSmallestRectangle2.At<double>(i, 0)+ offsetX;
                                point.Y = hRegionSmallestRectangle2.At<double>(i, 1) + offsetY;
                                regionSmallestRectangle2Points.Add(point);
                                //Console.WriteLine($"point:{point}");
                            }

                            //DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");
                            imageControl.AddPolygon(regionSmallestRectangle2Points, Colors.Blue, "margin");
                        }


                        if (set.isUseBaseLine)
                        {
                            // 直接使用检测阶段保存的基准线参数，不重复计算基准线。
                            double baselineOffset = set.distBaseLineThre * cutSet.scaleSize;
                            PointCollection baselinePoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY, 0);
                            PointCollection ignoredHeightPoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY, baselineOffset);
                            PointCollection rangeUpperPoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY,
                                set.distBaseLineRangeUpper * cutSet.scaleSize);
                            PointCollection rangeLowerPoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY,
                                set.distBaseLineRangeLower * cutSet.scaleSize);
                            
                            // 屏蔽区间上限为红色，下限为绿色，距离与原生过滤算法一致。
                            if (rangeUpperPoints.Count >= 2)
                            {
                                imageControl.AddPolyline(rangeUpperPoints, Colors.Red);
                            }
                            if (rangeLowerPoints.Count >= 2)
                            {
                                imageControl.AddPolyline(rangeLowerPoints, Colors.Green);
                            }

                            // 基准线
                            if (baselinePoints.Count >= 2)
                            {
                                imageControl.AddPolyline(baselinePoints, Colors.Blue);
                            }
                            // 忽略线
                            if (ignoredHeightPoints.Count >= 2)
                            {
                                imageControl.AddPolyline(ignoredHeightPoints, Colors.Yellow);


                            }

                           
                        }



                        // 字体结果显示最后

                        Vision.GetGlueWidthRange(set, data.glueWidthSide, out double widthMinimum, out double widthMaximum);
                        string textWindow1 = GetGlueWidthLabel(data.glueWidthSide) + ":" + (bResult.glueWidth ? Resources.LanguageDict.OK : Resources.LanguageDict.NG) + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                             $": {widthMinimum}~{widthMaximum}mm";
                        string textWindow2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight + ":" + (bResult.glueHeight ? Resources.LanguageDict.OK : Resources.LanguageDict.NG) + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                            $": {set.heightMin}~{set.heightMax}mm";
                        string textWindow3 = _3DLaserGlueInspection.Resources.LanguageDict.Area + ":" + (bResult.glueArea ? Resources.LanguageDict.OK : Resources.LanguageDict.NG) + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                            $": {set.areaMin}~{set.areaMax}mm²";
                        string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
                        //Console.WriteLine($"point :({10},{10})");
                        //DispTextInImageHWindowControlEvent(textWindow, Colors.Black, 10, 10);
                        imageControl.AddTextBlock(textWindow1, (bResult.glueWidth ? Colors.Green : Colors.Red), 10, 10);
                        imageControl.AddTextBlock(textWindow2, (bResult.glueHeight ? Colors.Green : Colors.Red), 10, 10 + 24);
                        imageControl.AddTextBlock(textWindow3, (bResult.glueArea ? Colors.Green : Colors.Red), 10, 10 + 48);

                        // 气泡检测启用时，在结果区显示检测结果、最大点间隔和阈值。
                        if (set.isUseBubbleDet)
                        {
                            double scale = cutSet.scaleSize > 0 ? cutSet.scaleSize : 1;
                            string bubbleText = $"{Resources.LanguageDict.BubbleDetection}: {(bResult.bubbleResult ? Resources.LanguageDict.OK : Resources.LanguageDict.NG)} {Resources.LanguageDict.BubbleMaximumPointSpacing}: {data.bubblePointDistance / scale:0.00}mm {Resources.LanguageDict.Threshold}: {set.bubblePointMinDistance:0.00}mm";
                            imageControl.AddTextBlock(bubbleText,
                                (bResult.bubbleResult ? Colors.Green : Colors.Red), 10, 10 + 72);
                            // 气泡检测 NG 时，连接检测出的超限间隔两个点并显示提示。
                            if (!bResult.bubbleResult && data.bubbleGapPointValid)
                            {
                                PointCollection bubbleGapPoints = new PointCollection();
                                bubbleGapPoints.Add(new System.Windows.Point(data.bubbleGapStartX + offsetX, data.bubbleGapStartY + offsetY));
                                bubbleGapPoints.Add(new System.Windows.Point(data.bubbleGapEndX + offsetX, data.bubbleGapEndY + offsetY));
                                imageControl.AddPolyline(bubbleGapPoints, Colors.Orange, 3);
                                int promptX = (int)((data.bubbleGapStartX + data.bubbleGapEndX) / 2 + offsetX);
                                int promptY = (int)((data.bubbleGapStartY + data.bubbleGapEndY) / 2 + offsetY);
                                imageControl.AddTextBlock(Resources.LanguageDict.BubbleOutOfRangeGap, Colors.Orange, promptX, promptY);
                            }
                        }

                        //// 气泡检测启用时，在结果区显示检测结果、最大点间隔和阈值。
                        //if (set.isUseBubbleDet && data != null && bResult != null)
                        //{
                        //    double scale = cutSet.scaleSize > 0 ? cutSet.scaleSize : 1;
                        //    string bubbleText = $"气泡检测: {(bResult.bubbleResult ? "OK" : "NG")}  最大点间隔: {data.bubblePointDistance / scale:0.00}mm  阈值: {set.bubblePointMinDistance:0.00}mm";
                        //    imageControl.AddTextBlock(bubbleText,
                        //        (bResult.bubbleResult ? Colors.Green : Colors.Red), 10, 10 + 72);
                        //}

                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

        /// <summary>
        /// 根据 cv::fitLine 返回的方向向量和线上一点，生成与画布边界相交的直线。
        /// offsetDistance 为正时，沿图像上方（Y 减小方向）偏移。
        /// </summary>
        private static PointCollection CreateLinePoints(double vx, double vy, double x0, double y0,
            double imageWidth, double imageHeight, double offsetX, double offsetY, double offsetDistance)
        {
            PointCollection points = new PointCollection();
            double vectorLength = Math.Sqrt(vx * vx + vy * vy);
            if (vectorLength < 1e-12 || imageWidth <= 0 || imageHeight <= 0)
            {
                return points;
            }

            vx /= vectorLength;
            vy /= vectorLength;

            // 取 Y 分量为负的法向量，确保“上方”对应图像坐标的 Y 减小方向。
            double normalX = -vy;
            double normalY = vx;
            if (normalY > 0)
            {
                normalX = -normalX;
                normalY = -normalY;
            }

            double lineX = x0 + offsetX + normalX * offsetDistance;
            double lineY = y0 + offsetY + normalY * offsetDistance;
            double epsilon = 1e-8;
            List<double> intersections = new List<double>();

            Action<double> addIntersection = t =>
            {
                double x = lineX + t * vx;
                double y = lineY + t * vy;
                if (x < -epsilon || x > imageWidth + epsilon || y < -epsilon || y > imageHeight + epsilon)
                {
                    return;
                }

                foreach (double oldT in intersections)
                {
                    if (Math.Abs(oldT - t) < epsilon)
                    {
                        return;
                    }
                }
                intersections.Add(t);
            };

            if (Math.Abs(vx) > epsilon)
            {
                addIntersection(-lineX / vx);
                addIntersection((imageWidth - lineX) / vx);
            }
            if (Math.Abs(vy) > epsilon)
            {
                addIntersection(-lineY / vy);
                addIntersection((imageHeight - lineY) / vy);
            }

            if (intersections.Count >= 2)
            {
                intersections.Sort();
                points.Add(new System.Windows.Point(lineX + intersections[0] * vx, lineY + intersections[0] * vy));
                points.Add(new System.Windows.Point(lineX + intersections[intersections.Count - 1] * vx,
                    lineY + intersections[intersections.Count - 1] * vy));
            }

            return points;
        }
        public static void AddCrossContour(int size, double rows, double cols, double angles, System.Windows.Media.Color color, ref ImageControl2 imageControl)
        {
            //PointCollection Points1 = new PointCollection();
            //PointCollection Points2 = new PointCollection();

            //System.Windows.Point p1 = new System.Windows.Point(cols + Math.Cos(angles / 180 * Math.PI) * size, rows + Math.Sin(angles / 180 * Math.PI) * size);
            //System.Windows.Point p2 = new System.Windows.Point(cols + Math.Cos((angles + 180) / 180 * Math.PI) * size, rows + Math.Sin((angles + 180) / 180 * Math.PI) * size);
            //System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
            //System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
            //Points1.Add(p1);
            //Points1.Add(p2);
            //Points2.Add(p3);
            //Points2.Add(p4);

            //imageControl.AddPolyline(Points1, color, 2);
            //imageControl.AddPolyline(Points2, color, 2);

            PointCollection Points1 = new PointCollection();

            System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
            System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
     
            Points1.Add(p3);
            Points1.Add(p4);

            imageControl.AddPolyline(Points1, color, 2);

        }
    }

}
