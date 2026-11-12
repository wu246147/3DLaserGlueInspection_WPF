using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// PI 串口参数与通讯调试窗口，提供连接管理、收发监视、连番号/VIN 显示以及手动 ACK 和结果发送。
    /// 复用主流程持有的 PISerialPort，通过队列和 UI 定时器显示后台收发数据。
    /// </summary>
    /// <remarks>必须在 UI 线程创建和操作；关闭窗口会解除事件和定时器，但不会关闭外部传入的串口。后台操作期间禁止关闭窗口和重复发送。</remarks>
    public partial class WindowSerialPort : Window
    {
        private const int MaxPendingLogs = 2000;
        private const int MaxLogCharacters = 200000;
        private readonly PISerialPort signal;
        private readonly DispatcherTimer displayTimer;
        private readonly object logLock = new object();
        private readonly Queue<CommunicationLog> pendingLogs = new Queue<CommunicationLog>();
        private bool updatingControls;
        private bool parametersChanged;
        private bool busy;
        private volatile bool closed;
        private long receiveCount;
        private long sendCount;
        private int droppedLogs;
        private string lastError = string.Empty;

        private sealed class CommunicationLog
        {
            public bool Received;
            public PISerialPortDataEventArgs Data;
        }

        /// <summary>
        /// 初始化串口调试窗口，加载当前串口参数并订阅收发事件，创建每 200 毫秒刷新一次的 UI 定时器。
        /// 初始化连接状态及最近接收的产品信息；定时器在窗口加载后启动。
        /// </summary>
        /// <param name="serialPort">非空 PI 通讯实例；可处于连接或断开状态，窗口只借用实例，不负责其最终释放。</param>
        /// <exception cref="ArgumentNullException">serialPort 为 null 时抛出。</exception>
        /// <remarks>调用方必须在 UI 线程创建；窗口关闭时解除本窗口的订阅并停止定时器。</remarks>
        public WindowSerialPort(PISerialPort serialPort)
        {
            signal = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
            InitializeComponent();
            PopulateParameters();
            parametersPanel.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(ParameterChanged));
            parametersPanel.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(ParameterChanged));
            parametersPanel.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(ParameterChanged));
            parametersPanel.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(ParameterChanged));
            signal.DataReceived += OnDataReceived;
            signal.DataSent += OnDataSent;
            displayTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            displayTimer.Tick += DisplayTimer_Tick;
            UpdateConnectionState();
            UpdateReceivedInfo();
        }

        private void PopulateParameters()
        {
            updatingControls = true;
            try
            {
                var param = signal.SerialParam;
                RefreshPorts();
                portNameComboBox.Text = param.PortName;
                baudRateComboBox.ItemsSource = new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
                baudRateComboBox.Text = param.BaudRate.ToString(CultureInfo.InvariantCulture);
                dataBitsComboBox.ItemsSource = new[] { 5, 6, 7, 8 };
                dataBitsComboBox.SelectedItem = param.DataBits;
                parityComboBox.ItemsSource = Enum.GetValues(typeof(Parity));
                parityComboBox.SelectedItem = param.Parity;
                stopBitsComboBox.ItemsSource = new[] { StopBits.One, StopBits.OnePointFive, StopBits.Two };
                stopBitsComboBox.SelectedItem = param.StopBits;
                handshakeComboBox.ItemsSource = Enum.GetValues(typeof(Handshake));
                handshakeComboBox.SelectedItem = param.Handshake;
                readTimeoutTextBox.Text = param.ReadTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
                writeTimeoutTextBox.Text = param.WriteTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
                dtrEnableCheckBox.IsChecked = param.DtrEnable;
                rtsEnableCheckBox.IsChecked = param.RtsEnable;
                sumIncludesETXCheckBox.IsChecked = param.SumIncludesETX;
            }
            finally { updatingControls = false; }
        }

        private void RefreshPorts()
        {
            string selected = portNameComboBox.Text;
            try
            {
                portNameComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(name => name).ToArray();
                portNameComboBox.Text = selected;
            }
            catch (Exception ex) { ShowMessage("串口列表刷新失败：" + ex.Message); }
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            updatingControls = true;
            try { RefreshPorts(); }
            finally { updatingControls = false; }
        }

        private void ParameterChanged(object sender, RoutedEventArgs e)
        {
            if (updatingControls)
                return;
            parametersChanged = true;
            UpdateRtsControl();
        }

        private void UpdateRtsControl()
        {
            var selected = handshakeComboBox.SelectedItem;
            rtsEnableCheckBox.IsEnabled = !(selected is Handshake
                && ((Handshake)selected == Handshake.RequestToSend || (Handshake)selected == Handshake.RequestToSendXOnXOff));
        }

        private bool TryReadParameters(out PISerialPortParam param)
        {
            param = signal.SerialParam.Copy();
            int baudRate, readTimeout, writeTimeout;
            if (string.IsNullOrWhiteSpace(portNameComboBox.Text)
                || !int.TryParse(baudRateComboBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out baudRate) || baudRate <= 0
                || !(dataBitsComboBox.SelectedItem is int) || !(parityComboBox.SelectedItem is Parity)
                || !(stopBitsComboBox.SelectedItem is StopBits) || !(handshakeComboBox.SelectedItem is Handshake))
            {
                ShowMessage("请填写串口号、有效波特率，并选择数据位、校验位、停止位和流控。");
                return false;
            }
            if (!int.TryParse(readTimeoutTextBox.Text, out readTimeout) || readTimeout <= 0 || readTimeout > 1000
                || !int.TryParse(writeTimeoutTextBox.Text, out writeTimeout) || writeTimeout <= 0 || writeTimeout > 5000)
            {
                ShowMessage("超时参数无效：读超时为 1~1000 ms，写超时为 1~5000 ms。");
                return false;
            }
            param.PortName = portNameComboBox.Text.Trim();
            param.BaudRate = baudRate;
            param.DataBits = (int)dataBitsComboBox.SelectedItem;
            param.Parity = (Parity)parityComboBox.SelectedItem;
            param.StopBits = (StopBits)stopBitsComboBox.SelectedItem;
            param.Handshake = (Handshake)handshakeComboBox.SelectedItem;
            param.ReadTimeoutMilliseconds = readTimeout;
            param.WriteTimeoutMilliseconds = writeTimeout;
            param.DtrEnable = dtrEnableCheckBox.IsChecked == true;
            param.RtsEnable = rtsEnableCheckBox.IsChecked == true;
            param.SumIncludesETX = sumIncludesETXCheckBox.IsChecked == true;
            return true;
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            PISerialPortParam param;
            if (busy || signal.IsOpen || !TryReadParameters(out param))
                return;
            busy = true;
            UpdateConnectionState();
            try
            {
                signal.SerialParam = param;
                bool success = await Task.Run(() => signal.Open());
                ShowMessage(success ? "串口连接成功：" + param.PortName : "串口连接失败：" + signal.ErrMsg);
                lastError = signal.ErrMsg;
            }
            catch (Exception ex) { ShowMessage("串口连接失败：" + ex.Message); }
            finally { busy = false; UpdateConnectionState(); }
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;
            busy = true;
            UpdateConnectionState();
            try
            {
                bool success = await Task.Run(() => signal.Close());
                ShowMessage(success ? "串口已断开。" : "断开失败：" + signal.ErrMsg);
                lastError = signal.ErrMsg;
            }
            catch (Exception ex) { ShowMessage("断开失败：" + ex.Message); }
            finally { busy = false; UpdateConnectionState(); }
        }

        private bool SaveParameters()
        {
            if (!signal.IsOpen)
            {
                PISerialPortParam param;
                if (!TryReadParameters(out param))
                    return false;
                signal.SerialParam = param;
            }
            if (!signal.Save())
            {
                ShowMessage("参数保存失败：" + signal.ErrMsg);
                return false;
            }
            parametersChanged = false;
            lastError = signal.ErrMsg;
            ShowMessage("串口参数保存成功。");
            return true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!busy)
                SaveParameters();
        }

        /// <summary>从按钮 Tag 读取 A/B/D/F 命令，交给统一异步发送流程执行手动 ACK 测试；无效 Tag 不执行发送。</summary>
        /// <param name="sender">触发事件的按钮，Tag 应为单字符命令字符串；不是按钮或 Tag 无效时忽略。</param>
        /// <param name="e">WPF 点击事件参数，本方法不读取其内容。</param>
        /// <remarks>从 UI 线程调用，发送期间禁用操作；异步异常由统一发送流程记录到窗口。</remarks>
        private async void ReplySignal_Click(object sender, RoutedEventArgs e)
        {
            string command = (sender as Button)?.Tag as string;
            if (command == null || command.Length != 1)
                return;
            await SendTestSignalAsync("回复 " + command, () => signal.SendTestReply(command[0]));
        }

        /// <summary>读取界面的 OK/NG 选择并异步发送 E 结果测试帧，不修改生产结果缓存。</summary>
        /// <param name="sender">触发发送的按钮，本方法不读取该参数。</param>
        /// <param name="e">WPF 点击事件参数，本方法不读取其内容。</param>
        /// <remarks>从 UI 线程调用；索引 0 发送 OK，其余选择发送 NG，操作状态和异常由统一发送流程管理。</remarks>
        private async void SendResultSignal_Click(object sender, RoutedEventArgs e)
        {
            bool isOk = testResultComboBox.SelectedIndex == 0;
            await SendTestSignalAsync("发送 E 结果（" + (isOk ? "OK" : "NG") + "）", () => signal.SendTestResult(isOk));
        }

        /// <summary>
        /// 在连接有效且窗口空闲时禁用操作，将阻塞串口发送放到后台任务，完成后在 UI 线程显示成功或错误。
        /// 忙碌时忽略请求，未连接时提示；无论发送成功、失败或异常，均恢复窗口操作状态。
        /// </summary>
        /// <param name="description">用于显示操作结果的非空中文描述，不包含报文内容。</param>
        /// <param name="send">非空同步发送委托；返回 true 表示成功，false 时从 signal.ErrMsg 读取失败原因，不得访问 UI。</param>
        /// <returns>表示发送及 UI 状态恢复完成的任务；发送异常会显示到操作记录。</returns>
        /// <remarks>必须从 UI 线程调用并保留其同步上下文；等待期间窗口禁止关闭，串口实例由外部继续持有。</remarks>
        private async Task SendTestSignalAsync(string description, Func<bool> send)
        {
            if (busy)
                return;
            if (!signal.IsOpen)
            {
                ShowMessage("请先连接串口，再发送测试信号。");
                return;
            }
            busy = true;
            UpdateConnectionState();
            try
            {
                bool success = await Task.Run(send);
                ShowMessage(description + (success ? "成功。" : "失败：" + signal.ErrMsg));
                lastError = signal.ErrMsg;
            }
            catch (Exception ex) { ShowMessage(description + "失败：" + ex.Message); }
            finally { busy = false; UpdateConnectionState(); }
        }

        private void OnDataReceived(object sender, PISerialPortDataEventArgs e) { QueueLog(true, e); }
        private void OnDataSent(object sender, PISerialPortDataEventArgs e) { QueueLog(false, e); }

        private void QueueLog(bool received, PISerialPortDataEventArgs data)
        {
            lock (logLock)
            {
                if (closed)
                    return;
                if (pendingLogs.Count >= MaxPendingLogs)
                {
                    pendingLogs.Dequeue();
                    droppedLogs++;
                }
                pendingLogs.Enqueue(new CommunicationLog { Received = received, Data = data });
            }
        }

        /// <summary>每次最多取出 250 条后台收发记录批量显示，更新计数、缓冲丢弃提示、错误、连接状态及已解析的产品信息。</summary>
        /// <param name="sender">触发刷新的 UI 定时器，本方法不读取该参数。</param>
        /// <param name="e">定时事件参数，本方法不读取其内容。</param>
        /// <remarks>仅在 UI 线程执行；队列通过 logLock 与收发线程同步，显示字段在协议校验后读取。</remarks>
        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            var received = new StringBuilder();
            var sent = new StringBuilder();
            var batch = new List<CommunicationLog>();
            int dropped;
            lock (logLock)
            {
                for (int i = 0; i < 250 && pendingLogs.Count > 0; i++)
                    batch.Add(pendingLogs.Dequeue());
                dropped = droppedLogs;
                droppedLogs = 0;
            }
            foreach (var log in batch)
            {
                if (log.Received) { received.Append(FormatData(log.Data)); receiveCount++; }
                else { sent.Append(FormatData(log.Data)); sendCount++; }
            }
            AppendLog(receiveLogTextBox, received.ToString());
            AppendLog(sendLogTextBox, sent.ToString());
            receiveCountText.Text = "接收（" + receiveCount + " 条）";
            sendCountText.Text = "发送（" + sendCount + " 条）";
            if (dropped > 0)
                ShowMessage("显示缓冲已满，跳过 " + dropped + " 条较早的显示记录。");
            string error = signal.ErrMsg;
            if (!string.IsNullOrEmpty(error) && error != lastError)
                ShowMessage(error);
            lastError = error;
            UpdateConnectionState();
            // 原始接收事件早于 B 帧解析，在 UI 定时刷新时读取已校验的字段。
            UpdateReceivedInfo();
        }

        /// <summary>读取串口最近校验通过的连番号和 VIN 并更新标签；空值显示破折号，不解析未经校验的原始报文。</summary>
        /// <remarks>仅在 UI 线程调用；各字段由串口内部状态锁读取，不修改业务状态。</remarks>
        private void UpdateReceivedInfo()
        {
            string serialNumber = signal.SerialNumber;
            string vin = signal.VIN;
            serialNumberLabel.Content = "连番号：" + (string.IsNullOrEmpty(serialNumber) ? "—" : serialNumber);
            vinLabel.Content = "VIN码：" + (string.IsNullOrEmpty(vin) ? "—" : vin);
        }

        private static string FormatData(PISerialPortDataEventArgs args)
        {
            byte[] data = args.Data;
            var ascii = new StringBuilder();
            foreach (byte value in data)
            {
                switch (value)
                {
                    case 0x02: ascii.Append("<STX>"); break;
                    case 0x03: ascii.Append("<ETX>"); break;
                    case 0x06: ascii.Append("<ACK>"); break;
                    case 0x15: ascii.Append("<NAK>"); break;
                    default:
                        if (value >= 0x20 && value <= 0x7E) ascii.Append((char)value);
                        else ascii.Append('<').Append(value.ToString("X2")).Append('>');
                        break;
                }
            }
            return args.Timestamp.ToString("HH:mm:ss.fff") + "  [" + data.Length + " bytes]\r\n"
                + "HEX   " + BitConverter.ToString(data).Replace('-', ' ') + "\r\n"
                + "ASCII " + ascii + "\r\n\r\n";
        }

        private static void AppendLog(TextBox target, string text)
        {
            if (text.Length == 0)
                return;
            target.AppendText(text);
            if (target.Text.Length > MaxLogCharacters)
            {
                int start = target.Text.IndexOf('\n', target.Text.Length - MaxLogCharacters * 3 / 4);
                target.Text = target.Text.Substring(start >= 0 ? start + 1 : target.Text.Length - MaxLogCharacters / 2);
            }
            target.ScrollToEnd();
        }

        private void ShowMessage(string message)
        {
            AppendLog(statusLogTextBox, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + "\r\n");
        }

        /// <summary>根据连接状态及 busy 标志更新参数区、连接按钮、手动测试区和状态灯，防止连接期间改参数或重复执行串口操作。</summary>
        /// <remarks>仅在 UI 线程调用；只改变控件状态，不建立或关闭串口连接。</remarks>
        private void UpdateConnectionState()
        {
            bool connected = signal.IsOpen;
            parametersPanel.IsEnabled = !busy && !connected;
            connectButton.IsEnabled = !busy && !connected;
            disconnectButton.IsEnabled = !busy && connected;
            saveButton.IsEnabled = !busy;
            manualSignalsPanel.IsEnabled = !busy && connected;
            connectionStatusText.Text = busy ? "操作中…" : connected ? "已连接：" + signal.SerialParam.PortName : "未连接";
            connectionStatusText.Foreground = connected ? Brushes.LightGreen : Brushes.LightGray;
            connectionIndicator.Fill = connected ? Brushes.LightGreen : Brushes.Gray;
            UpdateRtsControl();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            lock (logLock) { pendingLogs.Clear(); droppedLogs = 0; }
            receiveLogTextBox.Clear();
            sendLogTextBox.Clear();
            statusLogTextBox.Clear();
            receiveCount = sendCount = 0;
            receiveCountText.Text = "接收（0 条）";
            sendCountText.Text = "发送（0 条）";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            displayTimer.Start();
            ShowMessage("串口收发监视已开启。连接后自动显示收到和发出的数据。");
        }

        /// <summary>关闭前检查异步串口操作和未保存参数；操作中取消关闭，参数有变化时按用户选择保存或取消。</summary>
        /// <param name="sender">正在关闭的窗口，本方法不读取该参数。</param>
        /// <param name="e">关闭取消参数；操作未结束、用户取消或保存失败时将 Cancel 设为 true。</param>
        /// <remarks>仅在 UI 线程调用；可能弹出保存确认并写入串口配置，不关闭通讯实例。</remarks>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (busy)
            {
                ShowMessage("正在执行串口操作，请等待操作完成后再关闭窗口。");
                e.Cancel = true;
                return;
            }
            if (!parametersChanged)
                return;
            var choice = MessageBox.Show(this, "是否保存串口参数？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel || (choice == MessageBoxResult.Yes && !SaveParameters()))
                e.Cancel = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            closed = true;
            displayTimer.Stop();
            displayTimer.Tick -= DisplayTimer_Tick;
            signal.DataReceived -= OnDataReceived;
            signal.DataSent -= OnDataSent;
            lock (logLock) pendingLogs.Clear();
            // 只关闭显示窗口，保留用户选择的串口连接状态，业务线程可继续通讯。
        }
    }
}
