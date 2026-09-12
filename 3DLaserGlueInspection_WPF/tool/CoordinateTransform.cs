using System;

namespace Wpf_Replace_halcon
{
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
}

