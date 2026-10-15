using System;
using System.Xml.Serialization;

namespace _3DLaserGlueInspection
{
    /// <summary>尺寸矫正的仿射矩阵；平移项使矫正可以绕检测中心进行。</summary>
    [Serializable]
    public sealed class SizeCorrectionMatrix
    {
        public double M00 = 1, M01, M10, M11 = 1;
        public double OffsetX, OffsetY;
        // 最近一次校准结果的胶宽方向，单位为弧度。
        public double WidthAngle;

        [XmlIgnore]
        public bool IsValid => IsFinite(M00) && IsFinite(M01) && IsFinite(M10)
            && IsFinite(M11) && IsFinite(OffsetX) && IsFinite(OffsetY)
            && IsFinite(WidthAngle) && IsFinite(Determinant) && Determinant > 1e-12;

        [XmlIgnore]
        public double Determinant => M00 * M11 - M01 * M10;

        // 显示沿校准结果宽/高方向的累计长度比例，不把矩阵对角项冒充比例。
        [XmlIgnore]
        public double WidthRatio => GetDirectionalRatio(WidthAngle);
        [XmlIgnore]
        public double HeightRatio => GetDirectionalRatio(WidthAngle + Math.PI / 2);

        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static SizeCorrectionMatrix FromLegacyScale(double scaleX, double scaleY)
        {
            if (!IsFinite(scaleX) || !IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0)
                throw new InvalidOperationException("原矫正系数无效，请先初始化。");
            return new SizeCorrectionMatrix { M00 = scaleX, M11 = scaleY };
        }

        public static SizeCorrectionMatrix AlongMeasurementAxes(double widthRatio, double heightRatio,
            double angle, double centerX, double centerY)
        {
            if (!IsFinite(widthRatio) || !IsFinite(heightRatio) || widthRatio <= 0 || heightRatio <= 0
                || !IsFinite(angle) || !IsFinite(centerX) || !IsFinite(centerY))
                throw new InvalidOperationException("尺寸矫正比例或检测方向无效。");
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            var matrix = new SizeCorrectionMatrix
            {
                M00 = widthRatio * cos * cos + heightRatio * sin * sin,
                M01 = (widthRatio - heightRatio) * sin * cos,
                M10 = (widthRatio - heightRatio) * sin * cos,
                M11 = widthRatio * sin * sin + heightRatio * cos * cos,
                WidthAngle = angle
            };
            matrix.OffsetX = centerX - matrix.M00 * centerX - matrix.M01 * centerY;
            matrix.OffsetY = centerY - matrix.M10 * centerX - matrix.M11 * centerY;
            matrix.Validate();
            return matrix;
        }

        public void Validate()
        {
            if (!IsValid)
                throw new InvalidOperationException("尺寸矫正矩阵无效，请先初始化。");
        }

        public SizeCorrectionMatrix Clone() => (SizeCorrectionMatrix)MemberwiseClone();

        public void Transform(double x, double y, out double transformedX, out double transformedY)
        {
            transformedX = M00 * x + M01 * y + OffsetX;
            transformedY = M10 * x + M11 * y + OffsetY;
        }

        /// <summary>先应用当前矩阵，再应用 next；每次都与旧矩阵组合，避免重复缩放。</summary>
        public SizeCorrectionMatrix Then(SizeCorrectionMatrix next)
        {
            Validate();
            next.Validate();
            var result = new SizeCorrectionMatrix
            {
                M00 = next.M00 * M00 + next.M01 * M10,
                M01 = next.M00 * M01 + next.M01 * M11,
                M10 = next.M10 * M00 + next.M11 * M10,
                M11 = next.M10 * M01 + next.M11 * M11,
                OffsetX = next.M00 * OffsetX + next.M01 * OffsetY + next.OffsetX,
                OffsetY = next.M10 * OffsetX + next.M11 * OffsetY + next.OffsetY,
                WidthAngle = next.WidthAngle
            };
            result.Validate();
            return result;
        }

        public SizeCorrectionMatrix Inverse()
        {
            Validate();
            double determinant = Determinant;
            var result = new SizeCorrectionMatrix
            {
                M00 = M11 / determinant, M01 = -M01 / determinant,
                M10 = -M10 / determinant, M11 = M00 / determinant,
                WidthAngle = WidthAngle
            };
            result.OffsetX = -result.M00 * OffsetX - result.M01 * OffsetY;
            result.OffsetY = -result.M10 * OffsetX - result.M11 * OffsetY;
            result.Validate();
            return result;
        }

        private double GetDirectionalRatio(double angle)
        {
            Validate();
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double x = (M11 * cos - M01 * sin) / Determinant;
            double y = (-M10 * cos + M00 * sin) / Determinant;
            return 1 / Math.Sqrt(x * x + y * y);
        }

        /// <summary>变换矩形四角，返回覆盖平行四边形的最小面积旋转矩形。角度单位为度。</summary>
        public void TransformRectangle(double centerX, double centerY, double width, double height,
            double angleDegrees, out double newCenterX, out double newCenterY,
            out double newWidth, out double newHeight, out double newAngleDegrees)
        {
            Validate();
            if (!IsFinite(centerX) || !IsFinite(centerY) || !IsFinite(width) || !IsFinite(height)
                || !IsFinite(angleDegrees) || width <= 0 || height <= 0)
                throw new InvalidOperationException("基准线检测框无效。");
            double angle = angleDegrees * Math.PI / 180;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double ux = width * (M00 * cos + M01 * sin);
            double uy = width * (M10 * cos + M11 * sin);
            double vx = height * (-M00 * sin + M01 * cos);
            double vy = height * (-M10 * sin + M11 * cos);
            double firstAngle = Math.Atan2(uy, ux), secondAngle = Math.Atan2(vy, vx);
            GetBounds(ux, uy, vx, vy, firstAngle, out double firstWidth, out double firstHeight);
            GetBounds(ux, uy, vx, vy, secondAngle, out double secondWidth, out double secondHeight);
            bool useFirst = firstWidth * firstHeight <= secondWidth * secondHeight;
            newWidth = useFirst ? firstWidth : secondWidth;
            newHeight = useFirst ? firstHeight : secondHeight;
            newAngleDegrees = (useFirst ? firstAngle : secondAngle) * 180 / Math.PI;
            Transform(centerX, centerY, out newCenterX, out newCenterY);
            if (!IsFinite(newCenterX) || !IsFinite(newCenterY) || !IsFinite(newWidth)
                || !IsFinite(newHeight) || newWidth <= 0 || newHeight <= 0)
                throw new InvalidOperationException("矫正后的基准线检测框无效。");
        }

        private static void GetBounds(double ux, double uy, double vx, double vy, double angle,
            out double width, out double height)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            width = Math.Abs(ux * cos + uy * sin) + Math.Abs(vx * cos + vy * sin);
            height = Math.Abs(-ux * sin + uy * cos) + Math.Abs(-vx * sin + vy * cos);
        }
    }
}
