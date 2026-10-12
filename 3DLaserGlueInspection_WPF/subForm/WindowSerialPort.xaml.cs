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

        private void UpdateConnectionState()
        {
            bool connected = signal.IsOpen;
            parametersPanel.IsEnabled = !busy && !connected;
            connectButton.IsEnabled = !busy && !connected;
            disconnectButton.IsEnabled = !busy && connected;
            saveButton.IsEnabled = !busy;
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (busy)
            {
                ShowMessage("正在连接或断开，请等待操作完成后再关闭窗口。");
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
