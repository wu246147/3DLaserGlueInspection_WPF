using System;
using System.Windows;
using System.Windows.Controls;
using OpenCvSharp;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// 在参数设置窗口的既定车型、检测段、相机和数据集内，独立选择参数起点与实际图片起点。
    /// 预览 B 和实际序列中的前一张 A，确认后返回基准值，磁盘保存由父窗口统一完成。
    /// </summary>
    /// <remarks>
    /// 必须作为 UI 线程模态窗口使用，避免浏览期间父窗口替换数据。
    /// 图像由父窗口持有；本窗口只生成显示位图，不释放借用的 Mat，也不修改检测参数。
    /// </remarks>
    public partial class WindowImageStart : System.Windows.Window
    {
        /// <summary>当前相机有效参数数量，决定参数 ID 的独立输入范围。</summary>
        private readonly int parameterCount;
        /// <summary>当前数据集实际图片数量，决定图片 ID 的独立输入范围。</summary>
        private readonly int imageCount;
        /// <summary>借用父窗口图像的只读提供器，返回值的所有权不转移。</summary>
        private readonly Func<int, Mat> imageProvider;
        /// <summary>按实际图片 ID 计算中心位姿的提供器，失败时抛出具有说明的异常。</summary>
        private readonly Func<int, PoseParameters> poseProvider;
        /// <summary>初始化输入框期间阻止提前访问尚未创建的预览控件。</summary>
        private bool initializing = true;

        /// <summary>当前有效选择生成的 A/B 基准；输入无效或坐标不可用时为 null。</summary>
        public ImageStartReference Reference { get; private set; }
        /// <summary>当前选择的实际图片 B 的 ID；确认成功后供父窗口直接建立本批配对。</summary>
        public int ActualImageId { get; private set; }

        /// <summary>创建具有两个独立 ID 输入和 A/B 双图预览的起点设置窗口。</summary>
        /// <param name="scope">父窗口提供的车型、检测段和相机说明，不允许为空。</param>
        /// <param name="parameterCount">可用参数数量，必须大于零。</param>
        /// <param name="imageCount">实际图片数量，至少为二，才能取得前一张参考图。</param>
        /// <param name="parameterId">初始参数 ID，将限制在当前有效参数范围内。</param>
        /// <param name="imageId">初始实际图片 ID，将限制在当前实际序列范围内。</param>
        /// <param name="imageProvider">按实际 ID 借用图像的提供器，不允许为空；调用期间图像须保持有效。</param>
        /// <param name="poseProvider">按实际 ID 计算中心位姿的提供器，不允许为空；坐标单位为米。</param>
        /// <exception cref="ArgumentException">说明或提供器为空，或参数、图片数量不足。</exception>
        /// <remarks>不读写文件，不持有相机；所有提供器均同步在 UI 线程调用。</remarks>
        public WindowImageStart(string scope, int parameterCount, int imageCount, int parameterId, int imageId,
            Func<int, Mat> imageProvider, Func<int, PoseParameters> poseProvider)
        {
            if (scope == null || parameterCount <= 0 || imageCount < 2 || imageProvider == null || poseProvider == null)
                throw new ArgumentException("起点设置需要有效的车型范围、参数及至少两张实际图片。");
            this.parameterCount = parameterCount;
            this.imageCount = imageCount;
            this.imageProvider = imageProvider;
            this.poseProvider = poseProvider;
            InitializeComponent();
            scopeText.Text = scope;
            parameterIdTextBox.Text = Math.Max(0, Math.Min(parameterId, parameterCount - 1)).ToString();
            actualImageIdTextBox.Text = Math.Max(0, Math.Min(imageId, imageCount - 1)).ToString();
            initializing = false;
            RefreshPreview();
        }

        /// <summary>输入任一 ID 后重新检查两个独立范围，并更新图像和待应用的对应关系。</summary>
        /// <param name="sender">发生文本变化的输入框，不依赖其具体实例。</param>
        /// <param name="e">文本变化事件，不修改事件标记。</param>
        private void selectionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initializing) RefreshPreview();
        }

        /// <summary>仅将实际图片选择向前移动一张，不改变起点参数 ID。</summary>
        /// <param name="sender">上一张按钮，不读取其状态。</param>
        /// <param name="e">点击事件，不修改事件标记。</param>
        private void previousImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(actualImageIdTextBox.Text, out int id) && id > 0)
                actualImageIdTextBox.Text = (id - 1).ToString();
        }

        /// <summary>仅将实际图片选择向后移动一张，不改变起点参数 ID。</summary>
        /// <param name="sender">下一张按钮，不读取其状态。</param>
        /// <param name="e">点击事件，不修改事件标记。</param>
        private void nextImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(actualImageIdTextBox.Text, out int id) && id >= 0 && id < imageCount - 1)
                actualImageIdTextBox.Text = (id + 1).ToString();
        }

        /// <summary>
        /// 校验参数和实际图片选择，借用 A/B 原图生成预览并读取位姿，成功后构造可应用的基准。
        /// 实际图片零没有前一张，只允许查看，不允许用作双点起点。
        /// </summary>
        /// <remarks>仅更新弹窗；提供器异常在此显示实际原因，不沿用上一组有效基准或图像。</remarks>
        private void RefreshPreview()
        {
            Reference = null;
            applyButton.IsEnabled = false;
            previousImagePreview.SetImageSource(null);
            startImagePreview.SetImageSource(null);
            previousPoseText.Text = startPoseText.Text = rangeText.Text = string.Empty;
            if (!int.TryParse(parameterIdTextBox.Text, out int parameterId) || parameterId < 0 || parameterId >= parameterCount)
            {
                bindingText.Text = $"参数 ID 必须为 0～{parameterCount - 1} 的整数。";
                return;
            }
            if (!int.TryParse(actualImageIdTextBox.Text, out int imageId) || imageId < 0 || imageId >= imageCount)
            {
                bindingText.Text = $"实际图片 ID 必须为 0～{imageCount - 1} 的整数。";
                return;
            }
            try
            {
                Mat startImage = imageProvider(imageId);
                if (startImage == null || startImage.IsDisposed || startImage.Empty())
                    throw new InvalidOperationException("起点图片为空，无法应用。");
                startImagePreview.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(startImage));
                if (imageId == 0)
                {
                    bindingText.Text = "实际图片 0 没有前一张图片，无法定义 A/B 起点；请选后续图片或加载更早开始的采集数据。";
                    return;
                }
                Mat previousImage = imageProvider(imageId - 1);
                if (previousImage == null || previousImage.IsDisposed || previousImage.Empty())
                    throw new InvalidOperationException("前一张实际图片为空，无法应用。");
                previousImagePreview.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(previousImage));
                PoseParameters a = poseProvider(imageId - 1), b = poseProvider(imageId);
                if (a == null || b == null) throw new InvalidOperationException("A/B 图片没有有效中心位姿。");
                var reference = new ImageStartReference
                {
                    ParameterId = parameterId, PreviousX = a.x, PreviousY = a.y, PreviousZ = a.z,
                    StartX = b.x, StartY = b.y, StartZ = b.z
                };
                previousPoseText.Text = $"图片 {imageId - 1}　X={a.x * 1000:F3}，Y={a.y * 1000:F3}，Z={a.z * 1000:F3} mm";
                startPoseText.Text = $"图片 {imageId}　X={b.x * 1000:F3}，Y={b.y * 1000:F3}，Z={b.z * 1000:F3} mm";
                if (!reference.IsValid) throw new InvalidOperationException("A/B 坐标无效或几乎重合，无法确定起点方向。");
                double distance = Math.Sqrt(Math.Pow(b.x - a.x, 2) + Math.Pow(b.y - a.y, 2) + Math.Pow(b.z - a.z, 2));
                rangeText.Text = $"A → B 间距：{distance * 1000:F3} mm；横向允许偏差等于此间距；越过 B 后不限制纵向距离。";
                bindingText.Text = $"将参数 {parameterId} 绑定到实际图片 {imageId} 所在位置；应用后立即重新对齐当前数据集。";
                Reference = reference;
                ActualImageId = imageId;
                applyButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                bindingText.Text = "无法设置起点：" + ex.Message;
            }
        }

        /// <summary>重新验证当前输入，成功后返回对应关系并关闭模态窗口。</summary>
        /// <param name="sender">应用按钮，不读取其状态。</param>
        /// <param name="e">点击事件，不修改事件标记。</param>
        /// <remarks>不保存文件；父窗口负责应用基准并标记未保存。</remarks>
        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPreview();
            if (Reference != null) DialogResult = true;
        }
    }
}
