using System;
using System.Globalization;
using System.Windows;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// 收集当前检测段、当前相机的图片参数移动范围，并在输入校验通过后提示覆盖及初始化影响。
    /// 仅在用户二次确认后返回范围，本窗口本身不修改图片参数或写入配置文件。
    /// </summary>
    /// <remarks>由设置窗口在 UI 线程以模态方式创建；关闭、取消或拒绝确认均不返回可执行的移动结果。</remarks>
    public partial class WindowMoveImageParameters : Window
    {
        /// <summary>创建弹窗时的当前检测段图片数量，作为所有 ID 的独占上界。</summary>
        private readonly int imageCount;

        /// <summary>显示在弹窗及二次确认中的车型、检测段和相机说明。</summary>
        private readonly string scopeDescription;

        /// <summary>二次确认通过后的源起始图片 ID；仅在 ShowDialog 返回 true 后使用。</summary>
        public int StartId { get; private set; }

        /// <summary>二次确认通过后的源结束图片 ID，包含此位置；仅在 ShowDialog 返回 true 后使用。</summary>
        public int EndId { get; private set; }

        /// <summary>二次确认通过后的目标起始图片 ID；仅在 ShowDialog 返回 true 后使用。</summary>
        public int TargetStartId { get; private set; }

        /// <summary>
        /// 创建图片参数移动窗口，显示当前操作对象及有效 ID 范围，将三个输入初始化为当前图片 ID。
        /// 默认图片 ID 超出有效范围时收敛到最近的有效 ID；窗口需由调用方设置 Owner 并在 UI 线程显示。
        /// </summary>
        /// <param name="defaultImageId">当前选中的图片 ID；未选中时可传 -1，初始值会收敛至 0。</param>
        /// <param name="imageCount">当前检测段配置的图片数量，必须大于 0。</param>
        /// <param name="scopeDescription">当前车型、检测段及相机的可读说明，不允许为空或仅含空白。</param>
        /// <exception cref="ArgumentOutOfRangeException">imageCount 不大于 0。</exception>
        /// <exception cref="ArgumentException">scopeDescription 为空或仅含空白。</exception>
        public WindowMoveImageParameters(int defaultImageId, int imageCount, string scopeDescription)
        {
            if (imageCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(imageCount), "图片数量必须大于 0。");
            if (string.IsNullOrWhiteSpace(scopeDescription))
                throw new ArgumentException("必须指定当前车型、检测段及相机。", nameof(scopeDescription));
            InitializeComponent();
            this.imageCount = imageCount;
            this.scopeDescription = scopeDescription;
            scopeText.Text = scopeDescription;
            rangeHintText.Text = $"有效图片 ID：0～{imageCount - 1}，包含起始和结束图片。";
            string initialId = Math.Min(Math.Max(0, defaultImageId), imageCount - 1).ToString(CultureInfo.InvariantCulture);
            startIdTextBox.Text = endIdTextBox.Text = targetStartIdTextBox.Text = initialId;
        }

        /// <summary>
        /// 读取并校验三个输入框，拒绝空值、非十进制非负整数、倒置区间、越界和原地移动。
        /// 同时验证粘贴内容，不依赖按键过滤；失败时保留窗口和已有输入，且不修改已确认的范围属性。
        /// </summary>
        /// <param name="startId">输出解析后的源起始 ID；返回 false 时不可用于执行移动。</param>
        /// <param name="endId">输出解析后的源结束 ID；返回 false 时不可用于执行移动。</param>
        /// <param name="targetStartId">输出解析后的目标起始 ID；返回 false 时不可用于执行移动。</param>
        /// <param name="errorMessage">失败时输出可显示给用户的中文原因；成功时为空字符串。</param>
        /// <returns>全部输入可执行移动时返回 true，否则返回 false 并提供错误原因。</returns>
        /// <remarks>仅在所属 UI 线程调用；不修改业务参数，也不显示消息窗口。</remarks>
        private bool TryGetRange(out int startId, out int endId, out int targetStartId, out string errorMessage)
        {
            startId = endId = targetStartId = 0;
            errorMessage = "三个图片 ID 均须填写 0～2147483647 之间的十进制整数。";
            if (!int.TryParse(startIdTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out startId)
                || !int.TryParse(endIdTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out endId)
                || !int.TryParse(targetStartIdTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out targetStartId))
                return false;
            try
            {
                ImageParameterMover.GetTargetEndId(imageCount, startId, endId, targetStartId);
            }
            catch (ArgumentException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            if (startId == targetStartId)
            {
                errorMessage = "目标图片起始 ID 与源起始 ID 相同，无需移动。";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 处理确定操作，先验证输入，再展示源范围、目标范围、覆盖和初始化提示。
        /// 用户选择“是”后才提交返回属性并关闭弹窗；选择“否”或输入无效时保留弹窗继续编辑。
        /// </summary>
        /// <param name="sender">触发事件的确定按钮，不读取其状态。</param>
        /// <param name="e">WPF 点击事件参数，不修改事件处理标记。</param>
        /// <remarks>在 UI 线程执行；确认框默认选择“否”，此方法不修改业务参数或配置文件。</remarks>
        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetRange(out int startId, out int endId, out int targetStartId, out string errorMessage))
            {
                MessageBox.Show(this, errorMessage, "图片参数移动", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int targetEndId = ImageParameterMover.GetTargetEndId(imageCount, startId, endId, targetStartId);
            int resetStartId = targetStartId > startId ? startId : Math.Max(startId, targetEndId + 1);
            int resetEndId = targetStartId > startId ? Math.Min(endId, targetStartId - 1) : endId;
            string message = $"是否执行图片参数移动？\n\n{scopeDescription}\n"
                + $"源图片范围：{startId}～{endId}，共 {endId - startId + 1} 张。\n"
                + $"目标图片范围：{targetStartId}～{targetEndId}。\n\n"
                + "目标范围原有图片参数将被覆盖并清除。\n"
                + $"原位置中的图片 {resetStartId}～{resetEndId} 将恢复为默认参数。\n"
                + "移动后需点击“保存参数”保存修改。\n\n是否继续？";
            if (MessageBox.Show(this, message, "确认图片参数移动", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            StartId = startId;
            EndId = endId;
            TargetStartId = targetStartId;
            DialogResult = true;
        }
    }
}
