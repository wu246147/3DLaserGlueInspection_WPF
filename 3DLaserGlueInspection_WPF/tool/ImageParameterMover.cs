using System;
using System.Collections.Generic;

namespace _3DLaserGlueInspection
{
    /// <summary>
    /// 在同一检测段、同一相机的图片参数列表内执行连续区间移动。
    /// 先准备完整的源参数副本和空出位置的默认参数，再提交替换，保证重叠移动读取的是移动前的数据。
    /// </summary>
    /// <remarks>
    /// 本类型不持有资源，不操作图像、段级设置或配置文件；本次替换位置的 Index 按所在位置设置。
    /// 调用方负责在 UI 线程独占访问列表，并在完成后刷新界面、标记未保存状态。
    /// </remarks>
    internal static class ImageParameterMover
    {
        /// <summary>
        /// 校验包含两端的源区间及目标起点，计算平移后的目标结束 ID，禁止超出当前配置的图片数量。
        /// 使用长整数计算目标终点，避免较大的输入发生整数溢出后绕过边界校验。
        /// </summary>
        /// <param name="imageCount">当前检测段配置的图片数量，必须大于 0；有效 ID 为 0 至 imageCount - 1。</param>
        /// <param name="startId">源区间的起始图片 ID，必须在有效范围内且不大于 endId。</param>
        /// <param name="endId">源区间的结束图片 ID，包含此位置，必须在有效范围内。</param>
        /// <param name="targetStartId">目标区间的起始图片 ID，整个目标区间均须位于有效范围内。</param>
        /// <returns>校验通过后的目标结束 ID，包含此位置；源起点和目标起点相同时仍返回合法终点。</returns>
        /// <exception cref="ArgumentOutOfRangeException">图片数量非正数，或任一 ID、计算出的目标区间越界。</exception>
        /// <exception cref="ArgumentException">源区间的起始 ID 大于结束 ID。</exception>
        public static int GetTargetEndId(int imageCount, int startId, int endId, int targetStartId)
        {
            if (imageCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(imageCount), "请先配置大于 0 的图片数量。");
            if (startId < 0 || startId >= imageCount || endId < 0 || endId >= imageCount)
                throw new ArgumentOutOfRangeException(nameof(startId), $"起始和结束图片 ID 必须在 0～{imageCount - 1} 之间。");
            if (startId > endId)
                throw new ArgumentException("起始图片 ID 不能大于结束图片 ID。");
            if (targetStartId < 0 || targetStartId >= imageCount)
                throw new ArgumentOutOfRangeException(nameof(targetStartId), $"目标图片起始 ID 必须在 0～{imageCount - 1} 之间。");
            long targetEndId = (long)targetStartId + endId - startId;
            if (targetEndId >= imageCount)
                throw new ArgumentOutOfRangeException(nameof(targetStartId), $"目标图片范围为 {targetStartId}～{targetEndId}，超出有效范围 0～{imageCount - 1}，禁止执行移动。");
            return (int)targetEndId;
        }

        /// <summary>
        /// 将源区间的全部图片参数按原顺序移动到目标区间，并将源区间中未被目标覆盖的位置恢复为新建参数的默认值。
        /// 克隆及默认对象创建全部完成后才替换列表元素，避免重叠覆盖或准备阶段失败造成部分移动。
        /// </summary>
        /// <param name="frames">当前相机的可修改参数列表，不允许为空引用，长度至少为 imageCount；源区间元素不允许为空。</param>
        /// <param name="imageCount">当前检测段配置的图片数量，必须大于 0；列表中超出此数量的历史元素不参与移动。</param>
        /// <param name="startId">源区间起始 ID，取值为 0 至 endId。</param>
        /// <param name="endId">源区间结束 ID，包含此位置，必须小于 imageCount。</param>
        /// <param name="targetStartId">目标起始 ID，整个目标区间必须位于 0 至 imageCount - 1；等于 startId 时不修改列表。</param>
        /// <exception cref="ArgumentNullException">frames 为空引用。</exception>
        /// <exception cref="ArgumentOutOfRangeException">图片数量非正数，源或目标区间越界。</exception>
        /// <exception cref="ArgumentException">源区间倒置。</exception>
        /// <exception cref="InvalidOperationException">参数列表长度不足或源区间存在空元素。</exception>
        /// <remarks>
        /// 仅替换源和目标位置的对象，不修改其他位置或原对象；基准线框和尺寸矩阵随 ImageSet.Clone 独立复制。
        /// 不写配置文件，不分配原生图像资源。调用期间不得从其他线程修改列表或其中参数。
        /// </remarks>
        public static void Move(List<ImageSet> frames, int imageCount, int startId, int endId, int targetStartId)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));
            int targetEndId = GetTargetEndId(imageCount, startId, endId, targetStartId);
            if (frames.Count < imageCount)
                throw new InvalidOperationException("当前相机的图片参数数量不足，请检查检测段的图片数量配置。");
            if (startId == targetStartId)
                return;

            int count = endId - startId + 1;
            var moved = new ImageSet[count];
            for (int offset = 0; offset < count; offset++)
            {
                ImageSet source = frames[startId + offset];
                if (source == null)
                    throw new InvalidOperationException($"图片 {startId + offset} 的参数为空，未执行移动。");
                moved[offset] = source.Clone();
                moved[offset].Index = targetStartId + offset;
            }

            var initialized = new List<KeyValuePair<int, ImageSet>>();
            for (int index = startId; index <= endId; index++)
            {
                // 重叠区间内的位置接收移动后的参数，只有真正空出的源位置才恢复默认值。
                if (index < targetStartId || index > targetEndId)
                    initialized.Add(new KeyValuePair<int, ImageSet>(index, new ImageSet(index)));
            }
            for (int offset = 0; offset < count; offset++)
                frames[targetStartId + offset] = moved[offset];
            foreach (var item in initialized)
                frames[item.Key] = item.Value;
        }
    }
}
