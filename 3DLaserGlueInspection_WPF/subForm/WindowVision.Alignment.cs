using System;
using System.Collections.Generic;
using System.Windows;
using OpenCvSharp;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// 检测参数窗口的起点设置与临时图片映射部分。
    /// 参数列表始终以固定参数 ID 导航；图像、机器人姿态和点云继续使用实际采集时间戳索引。
    /// </summary>
    /// <remarks>映射在 UI 线程重建，后台任务只读；数据、车型或起点改变后重新计算，不持久化偏移。</remarks>
    public partial class WindowVision
    {
        /// <summary>按检测段及相机保存本批偏移；null 表示已配置起点但尚无有效匹配。</summary>
        private readonly Dictionary<string, int?> imageParameterOffsets = new Dictionary<string, int?>();
        /// <summary>与偏移同范围的显示状态，不改变车型参数的未保存标记。</summary>
        private readonly Dictionary<string, string> imageAlignmentMessages = new Dictionary<string, string>();
        /// <summary>更新参数列表显示内容期间阻止选择事件再次加载和写回控件。</summary>
        private bool refreshingParameterImages;

        /// <summary>生成同一窗口内检测段与相机的临时映射键。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <returns>仅用于当前数据集缓存的键。</returns>
        private static string GetAlignmentKey(int cutIndex, int cameraIndex)
        {
            return cutIndex + ":" + cameraIndex;
        }

        /// <summary>读取指定范围的实际图片时间戳序列，不创建或重排采集数据。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <returns>按采集时间排序的原始序列；范围无效或未加载时为 null，所有权仍属于窗口。</returns>
        private SynchronizedList<long> GetActualImageKeys(int cutIndex, int cameraIndex)
        {
            if (cutIndex < 0 || cutIndex >= ImageKeys.Count || cameraIndex < 0) return null;
            ImageKeys[cutIndex].TryGetValue("Cam" + (cameraIndex + 1), out var keys);
            return keys;
        }

        /// <summary>将指定参数 ID 转成当前数据集中的实际图片 ID。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <param name="parameterId">固定参数 ID，负值表示未选择。</param>
        /// <returns>实际图片 ID；未匹配或缺图时为 -1。</returns>
        /// <remarks>后台任务调用前须在 UI 线程完成 RebuildImageParameterAlignment。</remarks>
        private int GetAlignedImageId(int cutIndex, int cameraIndex, int parameterId)
        {
            var keys = GetActualImageKeys(cutIndex, cameraIndex);
            int? offset = GetImageAlignmentOffset(cutIndex, cameraIndex);
            return ImageParameterAlignment.GetImageId(parameterId, offset, keys?.Count ?? 0);
        }

        /// <summary>读取临时偏移；新建且未设置起点的参数范围沿用相同 ID。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <returns>本批偏移；已定义基准但未匹配时为 null。</returns>
        private int? GetImageAlignmentOffset(int cutIndex, int cameraIndex)
        {
            if (imageParameterOffsets.TryGetValue(GetAlignmentKey(cutIndex, cameraIndex), out int? offset)) return offset;
            if (set == null || cutIndex < 0 || cutIndex >= set.CutSets.Count || cameraIndex < 0) return null;
            var references = set.CutSets[cutIndex].ImageStartReferences;
            return references == null || cameraIndex >= references.Length || references[cameraIndex] == null ? (int?)0 : null;
        }

        /// <summary>
        /// 为当前车型的全部检测段和相机重建本批映射；未定义起点时直接使用零偏移。
        /// 已定义时按实际采集顺序找到首个满足 A/B 有向范围的位置，坐标无效的图片不参与匹配。
        /// </summary>
        /// <remarks>仅在 UI 线程且没有后台检测时调用；不修改 ImageSet、原始图片序列或参数保存状态。</remarks>
        private void RebuildImageParameterAlignment()
        {
            imageParameterOffsets.Clear();
            imageAlignmentMessages.Clear();
            if (set == null) return;
            for (int cutIndex = 0; cutIndex < set.CutSets.Count; cutIndex++)
            {
                var currentCut = set.CutSets[cutIndex];
                for (int cameraIndex = 0; cameraIndex < currentCut.imageSet.Count; cameraIndex++)
                {
                    string key = GetAlignmentKey(cutIndex, cameraIndex);
                    var reference = currentCut.ImageStartReferences != null && cameraIndex < currentCut.ImageStartReferences.Length
                        ? currentCut.ImageStartReferences[cameraIndex] : null;
                    if (reference == null)
                    {
                        imageParameterOffsets[key] = 0;
                        imageAlignmentMessages[key] = "未定义起点，按相同 ID 对应";
                        continue;
                    }
                    imageParameterOffsets[key] = null;
                    imageAlignmentMessages[key] = "尚未找到对应起点";
                    if (!reference.IsValid || reference.ParameterId >= currentCut.ImageNum)
                    {
                        imageAlignmentMessages[key] = "起点基准无效，请重新设置";
                        continue;
                    }
                    if (CamParamName == null || !Params.CamHandEyeType.TryGetValue(CamParamName, out var handEyeType)
                        || reference.HandEyeType != (int)handEyeType)
                    {
                        imageAlignmentMessages[key] = "手眼坐标类型改变，请重新设置起点";
                        continue;
                    }
                    var keys = GetActualImageKeys(cutIndex, cameraIndex);
                    if (keys == null || keys.Count == 0)
                    {
                        imageAlignmentMessages[key] = "已定义起点，等待加载图片";
                        continue;
                    }
                    string error = null;
                    for (int imageId = 0; imageId < keys.Count; imageId++)
                    {
                        if (!TryGetImageCenterPose(cutIndex, cameraIndex, imageId, out var center, out string poseError))
                        {
                            error = poseError;
                            continue;
                        }
                        if (!reference.IsStartCandidate(center.x, center.y, center.z)) continue;
                        int offset = imageId - reference.ParameterId;
                        imageParameterOffsets[key] = offset;
                        imageAlignmentMessages[key] = $"已对齐：参数 {reference.ParameterId} ↔ 图片 {imageId}，偏移 {offset:+0;-0;0}";
                        break;
                    }
                    if (!imageParameterOffsets[key].HasValue && error != null)
                        imageAlignmentMessages[key] += "；" + error;
                }
            }
        }

        /// <summary>刷新固定参数列表的双列显示，保持当前选中的参数 ID。</summary>
        /// <remarks>仅改变列表呈现，不改写参数或保存状态；必须在 UI 线程调用。</remarks>
        private void RefreshParameterImageList()
        {
            if (selectPictureListBox == null || refreshingParameterImages) return;
            int selected = selectPictureListBox.SelectedIndex;
            refreshingParameterImages = true;
            try
            {
                for (int parameterId = 0; parameterId < selectPictureListBox.Items.Count; parameterId++)
                {
                    int actualId = GetAlignedImageId(cutSetListBox.SelectedIndex, selectCamListBox.SelectedIndex, parameterId);
                    selectPictureListBox.Items[parameterId] = Tuple.Create(parameterId, actualId >= 0 ? actualId.ToString() : "—");
                }
                selectPictureListBox.SelectedIndex = selected;
            }
            finally
            {
                refreshingParameterImages = false;
            }
        }

        /// <summary>按参数 ID 读取已提取的实际图片中心位姿，供显示范围和轨迹映射使用。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraKey">相机键，例如 Cam1，必须对应当前已提取点云的相机。</param>
        /// <param name="parameterId">显示或轨迹范围中的固定参数 ID。</param>
        /// <returns>原位姿集合持有的中心位姿，只读借用，不转移所有权。</returns>
        /// <exception cref="InvalidOperationException">参数没有对应图片或尚未提取其中心位姿。</exception>
        private PoseParameters GetAlignedCenterPose(int cutIndex, string cameraKey, int parameterId)
        {
            if (string.IsNullOrEmpty(cameraKey) || cameraKey.Length <= 3
                || !int.TryParse(cameraKey.Substring(3), out int cameraNumber))
                throw new InvalidOperationException("相机编号无效。");
            int actualId = GetAlignedImageId(cutIndex, cameraNumber - 1, parameterId);
            var keys = GetActualImageKeys(cutIndex, cameraNumber - 1);
            if (actualId < 0 || !CamCenter3DPoseDict.TryGetValue(cameraKey, out var poses) || cutIndex >= poses.Count
                || !poses[cutIndex].TryGetValue(keys[actualId], out var pose))
                throw new InvalidOperationException($"参数 ID {parameterId} 没有对应的已提取图片位姿。");
            return pose;
        }

        /// <summary>完整读取当前显示参数范围的中心位姿，避免缺图时提交部分轨迹数据。</summary>
        /// <param name="cutIndex">当前车型中从零开始的检测段索引。</param>
        /// <param name="cameraKey">当前相机键，例如 Cam1。</param>
        /// <returns>全部位置均有提取位姿时为 true；失败时显示原因并保留原轨迹。</returns>
        /// <remarks>仅在 UI 线程调用；成功时替换 pose3D，保留既有终点不包含的轨迹范围约定。</remarks>
        private bool TryLoadAlignedPoseRange(int cutIndex, string cameraKey)
        {
            var positions = new List<double[]>();
            var settings = set.CutSets[cutIndex];
            try
            {
                for (int id = settings.StartImageIndex; id < settings.EndImageIndex; id++)
                {
                    var pose = GetAlignedCenterPose(cutIndex, cameraKey, id);
                    positions.Add(new[] { pose.x, pose.y, pose.z, pose.rx, pose.ry, pose.rz, (double)pose.PoseType });
                }
                pose3D = positions;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>检查实际时间戳是否对应显示参数范围内的位置，供点云重绘筛选使用。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <param name="imageKey">本批实际图片时间戳，与原始序列使用相同时钟。</param>
        /// <returns>位于有效显示区间时为 true；未设置有效显示范围时保留全部点云预览。</returns>
        /// <remarks>仅在 UI 线程读取映射；允许显示区间的一部分缺图，实际边界限制在现有序列内。</remarks>
        private bool IsWithinDisplayParameterRange(int cutIndex, int cameraIndex, long imageKey)
        {
            if (set == null || cutIndex < 0 || cutIndex >= set.CutSets.Count) return false;
            var settings = set.CutSets[cutIndex];
            if (settings.EndImageIndex <= settings.StartImageIndex) return true;
            var keys = GetActualImageKeys(cutIndex, cameraIndex);
            int? offset = GetImageAlignmentOffset(cutIndex, cameraIndex);
            if (keys == null || keys.Count == 0 || !offset.HasValue) return false;
            long first = Math.Max(0L, (long)settings.StartImageIndex + offset.Value);
            long last = Math.Min(keys.Count - 1L, (long)settings.EndImageIndex + offset.Value);
            return first <= last && imageKey >= keys[(int)first] && imageKey <= keys[(int)last];
        }

        /// <summary>显示当前编辑参数与实际图片的对应关系，并更新设置起点按钮的可用性。</summary>
        /// <remarks>数据未加载或未定义起点属于正常前期设置状态，不弹出错误提示。</remarks>
        private void UpdateImageAlignmentStatus()
        {
            int cutIndex = cutSetListBox.SelectedIndex, cameraIndex = selectCamListBox.SelectedIndex;
            int parameterId = selectPictureListBox.SelectedIndex;
            var keys = GetActualImageKeys(cutIndex, cameraIndex);
            int imageId = GetAlignedImageId(cutIndex, cameraIndex, parameterId);
            string key = GetAlignmentKey(cutIndex, cameraIndex);
            imageAlignmentMessages.TryGetValue(key, out string message);
            imageAlignmentStatusText.Text = parameterId >= 0
                ? $"正在编辑参数 {parameterId} ｜ 实际图片 {(imageId >= 0 ? imageId.ToString() : "无")} ｜ {message}"
                : message ?? "请选择检测段、相机及参数位置";
            imageAlignmentStatusText.ToolTip = imageAlignmentStatusText.Text;
            setImageStartButton.IsEnabled = !isAuto && !isCorrectingRange && set != null
                && cutIndex >= 0 && cameraIndex >= 0 && keys != null && keys.Count >= 2;
        }

        /// <summary>按图像时间戳二分查找并插值机器人位姿，避免起点搜索反复线性遍历全部姿态。</summary>
        /// <param name="imageKey">与机器人记录使用同一时钟的图像时间戳。</param>
        /// <returns>准确时间点或相邻样本插值位姿；没有包围样本时为 null，不外推或复用上一张位姿。</returns>
        private PoseParameters GetAlignmentRobotPose(long imageKey)
        {
            if (robotPoseKeys.Count == 0 || robotPoseKeys.Count != robotPoseValues.Count) return null;
            int low = 0, high = robotPoseKeys.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (robotPoseKeys[middle] == imageKey) return robotPoseValues[middle];
                if (robotPoseKeys[middle] < imageKey) low = middle + 1;
                else high = middle - 1;
            }
            if (high < 0 || low >= robotPoseKeys.Count || robotPoseKeys[low] <= robotPoseKeys[high]) return null;
            HMatrixTransform.mathHPose(robotPoseValues[high], robotPoseValues[low], out PoseParameters pose,
                (imageKey - robotPoseKeys[high]) / (double)(robotPoseKeys[low] - robotPoseKeys[high]));
            return pose;
        }

        /// <summary>独立于检测参数计算实际图片中心位姿，用于设置及自动匹配起点。</summary>
        /// <param name="cutIndex">从零开始的检测段索引。</param>
        /// <param name="cameraIndex">从零开始的相机索引。</param>
        /// <param name="imageId">本批实际图片 ID，不是参数 ID。</param>
        /// <param name="center">成功时为米制中心位姿；失败时为 null。</param>
        /// <param name="error">失败的中文原因或底层错误信息；成功时为 null。</param>
        /// <returns>位姿与标定完整且转换成功时为 true。</returns>
        /// <remarks>共享标定矩阵仅借用；临时矩阵和七轴预测矩阵均在本次调用内释放。</remarks>
        private bool TryGetImageCenterPose(int cutIndex, int cameraIndex, int imageId, out PoseParameters center, out string error)
        {
            center = null;
            error = null;
            Mat predicted = null;
            try
            {
                var keys = GetActualImageKeys(cutIndex, cameraIndex);
                if (keys == null || imageId < 0 || imageId >= keys.Count)
                    throw new InvalidOperationException("没有对应的实际图片。");
                PoseParameters robotPose = GetAlignmentRobotPose(keys[imageId]);
                if (robotPose == null) throw new InvalidOperationException("图片缺少对应的机器人位姿。");
                if (CamParamName == null || !Params.CamHandEyeType.TryGetValue(CamParamName, out var mode))
                    throw new InvalidOperationException("当前车型缺少手眼类型。");
                string cameraKey = "Cam" + (cameraIndex + 1);
                Mat extrinsic;
                if (mode == CameraHandEyeType.EyeInHand)
                    extrinsic = GetCorrectionCameraParameter(Params.Cam1ToTool, cameraKey, "Cam1ToTool");
                else if (mode == CameraHandEyeType.SevenAxis)
                {
                    if (recordedRobotPoseHasR7 != true || !Params.SevenAxisCalibration.TryGetValue(CamParamName, out var calibrations)
                        || !calibrations.TryGetValue(cameraKey, out var calibration)
                        || !SevenAxisRobotSupport.TryPredictCam1ToBase(calibration, robotPose.R7, out predicted, out string predictionError))
                        throw new InvalidOperationException("无法取得当前图片的七轴标定矩阵。");
                    extrinsic = predicted;
                }
                else
                    extrinsic = GetCorrectionCameraParameter(Params.Cam1ToBase, cameraKey, "Cam1ToBase");
                PoseParameters transformPose = mode == CameraHandEyeType.EyeInHand ? robotPose : Vision.PoseInv(robotPose);
                using (var poseMatrix = new Mat())
                {
                    Vision.poseToHomMat3d(transformPose.PoseType, transformPose.x, transformPose.y, transformPose.z,
                        transformPose.rx, transformPose.ry, transformPose.rz, poseMatrix.CvPtr);
                    using (Mat intermediate = poseMatrix * extrinsic)
                    using (Mat centerMatrix = intermediate * GetCorrectionCameraParameter(Params.CenterToCam1, cameraKey, "CenterToCam1"))
                    {
                        Vision.HomMat3dToPose(2, out double x, out double y, out double z,
                            out double rx, out double ry, out double rz, centerMatrix.CvPtr);
                        if (!IsFiniteCorrectionValue(x) || !IsFiniteCorrectionValue(y) || !IsFiniteCorrectionValue(z))
                            throw new InvalidOperationException("图片中心坐标不是有效数值。");
                        center = new PoseParameters { PoseType = 2, x = x, y = y, z = z, rx = rx, ry = ry, rz = rz };
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // 自动搜索可跳过没有位姿的图片，但必须保留原因供界面或起点设置弹窗展示。
                error = ex.Message;
                return false;
            }
            finally
            {
                predicted?.Dispose();
            }
        }

        /// <summary>打开当前参数设置范围的起点绑定弹窗，允许独立选择参数 ID 和实际图片 ID。</summary>
        /// <param name="sender">设置涂胶对齐起点按钮，不读取其状态。</param>
        /// <param name="e">WPF 点击事件，不修改事件标记。</param>
        /// <remarks>在 UI 线程执行；应用只修改内存基准并标记未保存，偏移始终为本批临时数据。</remarks>
        private void setImageStartButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAuto || isCorrectingRange || set == null) return;
            if (System.Linq.Enumerable.Any(tasks.Values, task => !task.IsCompleted))
            {
                System.Windows.Forms.MessageBox.Show("请等待当前点云提取完成后再设置起点。");
                return;
            }
            int cutIndex = cutSetListBox.SelectedIndex, cameraIndex = selectCamListBox.SelectedIndex;
            var keys = GetActualImageKeys(cutIndex, cameraIndex);
            if (cutIndex < 0 || cutIndex >= set.CutSets.Count || cameraIndex < 0 || keys == null || keys.Count < 2) return;
            var currentCut = set.CutSets[cutIndex];
            if (currentCut.ImageNum <= 0 || cameraIndex >= currentCut.imageSet.Count) return;
            int parameterCount = Math.Min(currentCut.ImageNum, currentCut.imageSet[cameraIndex].Count);
            if (parameterCount == 0) return;
            int parameterId = Math.Max(0, Math.Min(selectPictureListBox.SelectedIndex, parameterCount - 1));
            int imageId = GetAlignedImageId(cutIndex, cameraIndex, parameterId);
            string cameraKey = "Cam" + (cameraIndex + 1);
            SaveOriginalImageRegions(true);
            var dialog = new WindowImageStart($"车型：{set.Name}　检测段：{currentCut.Name}　相机：{cameraKey}",
                parameterCount, keys.Count, parameterId, Math.Max(1, imageId),
                id => Images[cutIndex][cameraKey][keys[id]],
                id =>
                {
                    if (!TryGetImageCenterPose(cutIndex, cameraIndex, id, out var pose, out string error))
                        throw new InvalidOperationException(error);
                    return pose;
                }) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            if (currentCut.ImageStartReferences == null || currentCut.ImageStartReferences.Length < 4)
                Array.Resize(ref currentCut.ImageStartReferences, 4);
            dialog.Reference.HandEyeType = (int)Params.CamHandEyeType[CamParamName];
            currentCut.ImageStartReferences[cameraIndex] = dialog.Reference;
            isAlter = true;
            // 已提取点云使用旧的图参关系，设置新基准后必须重新提取。
            CamCenter3DPoseDict.Clear();
            Robot3DPoseDict.Clear();
            Point3DXsDict.Clear();
            Point3DYsDict.Clear();
            Point3DZsDict.Clear();
            resultData = new Data();
            bResult = new BResult();
            RebuildImageParameterAlignment();
            // 本批以用户明确选择的配对为准，不让重复位置抢走手动指定的图片。
            string key = GetAlignmentKey(cutIndex, cameraIndex);
            imageParameterOffsets[key] = dialog.ActualImageId - dialog.Reference.ParameterId;
            imageAlignmentMessages[key] = $"已对齐：参数 {dialog.Reference.ParameterId} ↔ 图片 {dialog.ActualImageId}";
            RefreshParameterImageList();
            selectPictureListBox.SelectedIndex = dialog.Reference.ParameterId;
            SelectedCamAndImage();
        }
    }
}
