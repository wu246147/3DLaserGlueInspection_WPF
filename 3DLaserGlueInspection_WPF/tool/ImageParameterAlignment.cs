using System;

namespace _3DLaserGlueInspection
{
    /// <summary>
    /// 保存一个检测段内单个相机的涂胶起点基准，将固定参数 ID 绑定到参考图片 B 的位置。
    /// A 是同一批参考数据中 B 的前一张实际图片；A→B 定义局部运动方向及横向允许距离。
    /// </summary>
    /// <remarks>
    /// 坐标单位与机器人位姿一致，为米；眼在手上使用基座坐标，眼在手外及七轴使用工具坐标。
    /// 此类型仅保存值，不持有图像或矩阵，不保存任何数据集的临时 ID 偏移。
    /// 配置在 UI 线程修改；后台检测使用独立副本，运行期间不得并发修改同一实例。
    /// </remarks>
    [Serializable]
    public sealed class ImageStartReference
    {
        /// <summary>参考起点对应的参数 ID，从零开始，须小于当前相机的参数数量。</summary>
        public int ParameterId;
        /// <summary>设置基准时的手眼类型数值，用于拒绝不同坐标定义之间的匹配。</summary>
        public int HandEyeType;
        /// <summary>前一张实际图片 A 的中心 X 坐标，单位米。</summary>
        public double PreviousX;
        /// <summary>前一张实际图片 A 的中心 Y 坐标，单位米。</summary>
        public double PreviousY;
        /// <summary>前一张实际图片 A 的中心 Z 坐标，单位米。</summary>
        public double PreviousZ;
        /// <summary>起点图片 B 的中心 X 坐标，单位米。</summary>
        public double StartX;
        /// <summary>起点图片 B 的中心 Y 坐标，单位米。</summary>
        public double StartY;
        /// <summary>起点图片 B 的中心 Z 坐标，单位米。</summary>
        public double StartZ;

        /// <summary>基准是否具有非负参数 ID、有限坐标及可区分的 A/B 位置。</summary>
        public bool IsValid
        {
            get
            {
                double dx = StartX - PreviousX, dy = StartY - PreviousY, dz = StartZ - PreviousZ;
                double squaredLength = dx * dx + dy * dy + dz * dz;
                return ParameterId >= 0 && IsFinite(PreviousX) && IsFinite(PreviousY) && IsFinite(PreviousZ)
                    && IsFinite(StartX) && IsFinite(StartY) && IsFinite(StartZ)
                    && IsFinite(squaredLength) && squaredLength > 1e-24;
            }
        }

        /// <summary>
        /// 判断中心位置是否已沿 A→B 越过 A，且到该运动直线的横向距离不大于 A/B 间距。
        /// A 不属于起点区间，B 属于；越过 B 后不限制纵向距离，按采集顺序取首个有效候选。
        /// </summary>
        /// <param name="x">待匹配中心 X 坐标，单位米，须与参考坐标处于同一坐标系。</param>
        /// <param name="y">待匹配中心 Y 坐标，单位米，须为有限值。</param>
        /// <param name="z">待匹配中心 Z 坐标，单位米，须为有限值。</param>
        /// <returns>基准和输入有效且满足方向、横向范围时为 true；无效值或尚未越过 A 时为 false。</returns>
        /// <remarks>不修改实例、不分配原生资源；允许多个线程只读调用。</remarks>
        public bool IsStartCandidate(double x, double y, double z)
        {
            if (!IsValid || !IsFinite(x) || !IsFinite(y) || !IsFinite(z)) return false;
            double dx = StartX - PreviousX, dy = StartY - PreviousY, dz = StartZ - PreviousZ;
            double px = x - PreviousX, py = y - PreviousY, pz = z - PreviousZ;
            double lengthSquared = dx * dx + dy * dy + dz * dz;
            double projection = px * dx + py * dy + pz * dz;
            // 使用相对数值容差排除 A 本身；不引入额外的业务距离阈值。
            if (!IsFinite(projection) || projection <= lengthSquared * 1e-12) return false;
            double lateralX = px - projection / lengthSquared * dx;
            double lateralY = py - projection / lengthSquared * dy;
            double lateralZ = pz - projection / lengthSquared * dz;
            double lateralSquared = lateralX * lateralX + lateralY * lateralY + lateralZ * lateralZ;
            return IsFinite(lateralSquared) && lateralSquared <= lengthSquared * (1 + 1e-12);
        }

        /// <summary>复制全部参考值，使检测快照或检测段副本不共享可变的起点配置。</summary>
        /// <returns>不持有外部资源、可独立修改的新基准实例。</returns>
        public ImageStartReference Clone()
        {
            return (ImageStartReference)MemberwiseClone();
        }

        /// <summary>检查坐标或计算中间值是否为可参与几何运算的有限数。</summary>
        /// <param name="value">待检查的双精度数值，单位随调用上下文。</param>
        /// <returns>既非 NaN 也非正负无穷时为 true。</returns>
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// 在固定参数 ID 和单次采集图片 ID 之间执行偏移换算，不移动参数、不改写原始图片索引。
    /// 偏移定义为实际起点图片 ID 减去参考起点参数 ID；零偏移也用于未设置起点的旧配置。
    /// </summary>
    /// <remarks>所有方法均无共享可变状态；空偏移表示已定义基准但本批尚未成功匹配。</remarks>
    public static class ImageParameterAlignment
    {
        /// <summary>将固定参数 ID 转为本批实际图片 ID，并检查图片范围。</summary>
        /// <param name="parameterId">从零开始的参数 ID；负值视为无效选择。</param>
        /// <param name="offset">本批临时偏移；null 表示没有可用对应关系。</param>
        /// <param name="imageCount">本批实际图片数量，允许为零。</param>
        /// <returns>有效实际图片 ID；未对齐、越界或无图片时返回 -1，不夹到首尾图片。</returns>
        public static int GetImageId(int parameterId, int? offset, int imageCount)
        {
            if (parameterId < 0 || !offset.HasValue) return -1;
            long id = (long)parameterId + offset.Value;
            return id >= 0 && id < imageCount ? (int)id : -1;
        }

        /// <summary>将本批实际图片 ID 转为参数 ID，并检查参数范围。</summary>
        /// <param name="imageId">从零开始的实际图片 ID；负值视为无效图片。</param>
        /// <param name="offset">本批临时偏移；null 表示没有可用对应关系。</param>
        /// <param name="parameterCount">当前相机可用参数数量，允许为零。</param>
        /// <returns>有效参数 ID；未对齐或超出参数范围时返回 -1。</returns>
        public static int GetParameterId(int imageId, int? offset, int parameterCount)
        {
            if (imageId < 0 || !offset.HasValue) return -1;
            long id = (long)imageId - offset.Value;
            return id >= 0 && id < parameterCount ? (int)id : -1;
        }
    }
}
