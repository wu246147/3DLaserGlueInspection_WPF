using _3DLaserGlueInspection.subForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenCvSharp;

using static _3DLaserGlueInspection.MainWindowModel;
using LiveCharts;
using System.Globalization;
using _3DLaserGlueInspection.Resources;


namespace _3DLaserGlueInspection
{

    /// <summary>
    /// 主窗口的交互逻辑，创建机器人与 PLC 通讯实例，连接图像显示和主流程，并提供参数入口。
    /// PI 收发记录写入业务日志，主流程运行期间禁用设置菜单，清理结束后再由 UI 线程恢复。
    /// </summary>
    /// <remarks>窗口及其控件由 UI 线程管理；主流程使用独立线程，关闭时请求停止并解除本次新增的事件订阅。</remarks>
    public partial class MainWindow : System.Windows.Window
    {
        MainWindowModel model;
        bool stop = true;
        Thread mainThread = null;

        //JAKARobot robot = new JAKARobot();


        //readonly UserHWindowControl hWindow数模图 = new UserHWindowControl();
        //readonly UserHWindowControl hWindowControl = new UserHWindowControl();
        //readonly Wpf_halcon.ImageControl2 hWindowModel = new Wpf_halcon.ImageControl2();
        //readonly Wpf_halcon.ImageControl2 hWindowControl = new Wpf_halcon.ImageControl2();

        /// <summary>
        /// 初始化主窗口和数据模型，按现有配置创建机器人与 PLC 实例，绑定界面及运行完成通知。
        /// 选择 PI 通讯时订阅收发日志；保留各机器人现有的连接方式初始化和语言选择。
        /// </summary>
        /// <remarks>必须在 UI 线程创建；通讯和运行通知订阅在 Window_Closing 中解除，设备的运行及清理由主流程负责。</remarks>
        public MainWindow()
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("0293fde5-6e7c-4c76-bacd-e3bdb0ee6187"))
            {
                System.Windows.Forms.MessageBox.Show("active failed");
            }

            InitializeComponent();
            model = new MainWindowModel();
            this.DataContext = model.mainModel;
            model.RunFinished += OnRunFinished;


            //打开通讯

            //model.InitCommunicationConnection();
            switch (model.robotType)
            {
                case RobotType.YRCRobot:
                    //model.robot = new YRCRobot();
                    break;
                case RobotType.JAKARobot:
                    model.robot = new JAKARobot();
                    break;
                case RobotType.FanucRobot:
                    model.robot = new FanucRobot();
                    break;
                case RobotType.KukaRobot:
                    model.robot = new KukaRobot();
                    ((KukaRobot)model.robot).robotConnectType = model.robotConnectType;
                    ((KukaRobot) model.robot).InitCommunicationConnection();
                    break;
                case RobotType.KawasakiRobot:
                    model.robot = new KawasakiRobot();
                    ((KawasakiRobot)model.robot).robotConnectType = model.robotConnectType;

                    ((KawasakiRobot)model.robot).InitCommunicationConnection();

                    break;
            }

            switch (model.plcType)
            {
                case PlcType.MelsecPlc:
                    model.plc = new MelsecPlc();
                    break;
                case PlcType.OmronPlc:
                    model.plc = new OmronPlc();
                    break;
                case PlcType.ToyotaPlc:
                    model.plc = new ToyotaPlc();
                    break;
                case PlcType.PISerialPort:
                    model.plc = new PISerialPort();
                    ((PISerialPort)model.plc).DataReceived += OnDataReceived;
                    ((PISerialPort)model.plc).DataSent += OnDataSent;
                    break;
            }

            //语言选择确定
            if (GlobalVarAndFunc.LANGUAGE_ID == "zh-CN")
            {
                MenuItem_chineseLanguage.IsChecked = true;
            }
            else
            {
                MenuItem_englishLanguage.IsChecked = true;
            }
        }
        /// <summary>
        /// 主窗口加载后绑定图像、点云及相机数量通知，完成显示订阅后自动调用启停入口启动主流程。
        /// 自动启动复用按钮入口的仿真路径检查、线程检查和菜单状态控制，与 PI 调试版本保持一致。
        /// </summary>
        /// <param name="sender">触发 Loaded 事件的主窗口，本方法不读取该参数。</param>
        /// <param name="e">WPF 加载事件参数，本方法不读取其内容。</param>
        /// <remarks>仅在 UI 线程执行；自动启动会创建主流程线程，按当前配置加载参数并连接设备，退出清理由主流程负责。</remarks>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            //GlobalVarAndFunc.ReadLanguageID();
            //GlobalVarAndFunc.LanguageDicInit();
            //GeneralFunc.ChangeLanguateFun(typeof(FormVision), this);


            model.DispImageHWindowNumericalModelDiagramEvent += hWindowModel.SetImageSource;
            model.DispPolylineHWindowNumericalModelDiagramEvent += hWindowModel.AddPolyline;

            model.DispClearHWindowControlEvent += hWindowControl.ClearChildren;
            model.DispImageWithoutCloneHWindowControlEvent += hWindowControl.SetImageSource;
            model.DispTextInImageHWindowControlEvent += hWindowControl.AddTextBlock;
            model.DispTextInWindowHWindowControlEvent += hWindowControl.AddTextBlock;
            model.DispPolylinejHWindowControlEvent += hWindowControl.AddPolyline;
            model.DispPolygonjHWindowControlEvent += hWindowControl.AddPolygon;

            model.Disp3DPointControlEvent += _3DShowControl.AddPointCloud;
            model.Clear3DPointControlEvent += _3DShowControl.ClearPointCloud;

            model.RefreshPointsEvent += _3DShowControl.RefreshPoints;
            model.RefreshOFFEvent += _3DShowControl.RefreshOFF;
            model.RefreshOnEvent += _3DShowControl.RefreshOn;

            // 订阅 model 中相机数量变化事件，用于调整 _2DResultDataGrid 列显示
            model.CamCountChanged += (count) =>
            {
                // 确保在 UI 线程执行
                Dispatcher.Invoke(() => Adjust2DResultDataGridColumns(count));
            };

            // 显示订阅完成后复用现有启动入口，保持参数校验和运行状态控制一致。
            ButtonRun_Click(null, null);
        }

        /// <summary>将 PI 接收数据转换为十六进制日志，添加当前 VIN 和调用信息后，按原始收发时间写入日志文件。</summary>
        /// <param name="sender">产生事件的 PI 通讯实例，本方法不读取该参数。</param>
        /// <param name="e">非空接收事件参数，包含本次原始字节快照及接收时间。</param>
        /// <remarks>在串口接收线程同步执行，不访问 UI；日志异常由 PI 的事件通知边界记录到调试输出。</remarks>
        private void OnDataReceived(object sender, PISerialPortDataEventArgs e)
        {
            string message = model.ProcessMessage("[PI 输入/RX] " + BitConverter.ToString(e.Data).Replace('-', ' '), LogType.normal);
            model.WriteLog(message, LogType.normal, e.Timestamp);
        }

        /// <summary>将 PI 已发送数据转换为十六进制日志，添加当前 VIN 和调用信息后，按实际发送时间写入日志文件。</summary>
        /// <param name="sender">产生事件的 PI 通讯实例，本方法不读取该参数。</param>
        /// <param name="e">非空发送事件参数，包含本次完整报文快照及发送时间。</param>
        /// <remarks>在实际发送线程同步执行，不访问 UI；日志异常由 PI 的事件通知边界记录到调试输出。</remarks>
        private void OnDataSent(object sender, PISerialPortDataEventArgs e)
        {
            string message = model.ProcessMessage("[PI 输出/TX] " + BitConverter.ToString(e.Data).Replace('-', ' '), LogType.normal);
            model.WriteLog(message, LogType.normal, e.Timestamp);
        }

        /// <summary>窗口关闭时请求主流程停止，并解除运行完成和 PI 收发日志订阅，避免退出期间继续回调窗口。</summary>
        /// <param name="sender">正在关闭的主窗口，本方法不读取该参数。</param>
        /// <param name="e">WPF 关闭事件参数，本方法不更改取消状态。</param>
        /// <remarks>仅在 UI 线程调用；此处不等待主线程退出，通讯和相机仍由主流程的 finally 负责关闭。</remarks>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            model.stop = true;
            model.RunFinished -= OnRunFinished;
            if (model.plc is PISerialPort piSerialPort)
            {
                piSerialPort.DataReceived -= OnDataReceived;
                piSerialPort.DataSent -= OnDataSent;
            }
            //model.StopCommunicationThread();

        }

        // 根据相机数量调整 _2DResultDataGrid 的列显示和宽度
        private void Adjust2DResultDataGridColumns(int camCount)
        {
            try
            {
                if (_2DResultDataGrid == null || _2DResultDataGrid.Columns == null) return;

                camCount = Math.Max(1, Math.Min(camCount, _2DResultDataGrid.Columns.Count));

                for (int i = 0; i < _2DResultDataGrid.Columns.Count; i++)
                {
                    var col = _2DResultDataGrid.Columns[i];
                    if (i < camCount)
                    {
                        col.Visibility = Visibility.Visible;
                        col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    }
                    else
                    {
                        col.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch { }
        }

        private void min_Btn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }


        private void btnMin_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnMax_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.Administrator
                 || userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                model.ShowMessage($"{userType} 关闭软件");
               this.Close();
            }
           
        }

        private void TopMain_MouseLeftBtnDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        /// <summary>统一设置标准块、通讯、相机和产品设置菜单的可用状态，使运行过程和清理过程共用同一组限制。</summary>
        /// <param name="enabled">true 表示允许进入设置，false 表示禁用这些入口。</param>
        /// <remarks>必须在 UI 线程调用，只更新菜单控件。</remarks>
        private void SetMenusEnabled(bool enabled)
        {
            MenuItem_standardBlockTest.IsEnabled = enabled;
            MenuItem_signalSetting.IsEnabled = enabled;
            MenuItem_camSetting.IsEnabled = enabled;
            MenuItem_productSetting.IsEnabled = enabled;
        }

        /// <summary>接收主流程清理完成通知，将菜单恢复安排到 UI 线程，并校验通知仍属于当前运行线程。</summary>
        /// <remarks>由结束中的主流程线程同步调用；窗口已进入关闭时忽略，通过线程身份避免旧通知影响随后启动的新流程。</remarks>
        private void OnRunFinished()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            Thread finishedThread = Thread.CurrentThread;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 只恢复本次运行的菜单，避免旧通知影响已经启动的新流程。
                if (mainThread == finishedThread)
                    SetMenusEnabled(true);
            }));
        }

        /// <summary>
        /// 按启停按钮状态启动主流程或请求停止；启动前检查仿真目录和已有线程，先禁用设置菜单再启动工作线程。
        /// 停止只设置退出请求，等待主流程清理完成通知恢复菜单，避免关闭设备时进入调试界面。
        /// </summary>
        /// <param name="sender">触发启停的按钮；本方法不读取该参数。</param>
        /// <param name="e">WPF 点击事件参数；本方法不读取该参数。</param>
        /// <remarks>仅在 UI 线程调用；创建主流程线程并修改运行按钮及模型停止状态，不同步等待主流程结束。</remarks>
        private void ButtonRun_Click(object sender, RoutedEventArgs e)
        {
            //正常启动
            if ((string)ButtonRun.Content == _3DLaserGlueInspection.Resources.LanguageDict.Start)
            {
                if (model.simulation)
                {
                    if (model.simulationPath == "")
                    {
                        model.ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.SimulationPathNotFilledIn, LogType.warn);
                        return;
                    }
                    else if (!Directory.Exists(model.simulationPath))
                    {
                        model.ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.SimulationPathDoesNotExist, LogType.warn);
                        return;
                    }
                }
                if (mainThread != null && mainThread.IsAlive)
                {
                    model.ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.TheMainThreadIsAlreadyRunning, LogType.warn);
                    return;
                }

                model.stop = false;
                mainThread = new Thread(model.MainRun);

                model.mainModel.buttonRunContentControl = _3DLaserGlueInspection.Resources.LanguageDict.Stop;
                model.mainModel.buttonRunTagControl = "\uE67A";

                // 在线程启动前禁用菜单，避免 MainRun 快速退出后再次被禁用。
                SetMenusEnabled(false);
                mainThread.Start();
            }
            else
            {
                model.stop = true;
                model.mainModel.buttonRunContentControl = _3DLaserGlueInspection.Resources.LanguageDict.Start;
                model.mainModel.buttonRunTagControl = "\uE658";

                // 菜单由 RunFinished 通知在主流程清理结束后恢复。
            }

            ////测试
            ////PieChartResult.Series[0].Values = new ChartValues<double> { 100 };
            //model.mainModel.OKCountControl += 2;
            //model.mainModel.NGCountControl += 1;

        }
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (simulationCheck.Visibility == Visibility.Visible)
                {
                    simulationCheck.Visibility = Visibility.Hidden;
                    simulationPath.Visibility = Visibility.Hidden;
                    DemoCheck.Visibility = Visibility.Hidden;
                }
                else
                {
                    simulationCheck.Visibility = Visibility.Visible;
                    simulationPath.Visibility = Visibility.Visible;
                    DemoCheck.Visibility = Visibility.Visible;

                }
            }
        }

        private void simulationPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            model.simulationPath = simulationPath.Text;
        }

        private void simulationCheck_Click(object sender, RoutedEventArgs e)
        {
            model.simulation = (bool)simulationCheck.IsChecked;
        }

        private bool TryPasswordLogin(out WindowPassWord.PasswordUserType userType)
        {
            WindowPassWord formPassword = new WindowPassWord();
            bool loginSucceeded = formPassword.ShowDialog() == true;
            userType = formPassword.LoginUserType;
            return loginSucceeded && userType != WindowPassWord.PasswordUserType.None;
        }

        private void ShowAdministratorPermissionDenied()
        {
            System.Windows.Forms.MessageBox.Show(
                _3DLaserGlueInspection.Resources.LanguageDict.AdministratorCannotEnterPage,
                _3DLaserGlueInspection.Resources.LanguageDict.Prompt,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
        private void MenuItem_camSetting_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                WindowCamera windowCamera = new WindowCamera();
                windowCamera.ShowDialog();
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }

        private void MenuItem_databaseQuery_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType != WindowPassWord.PasswordUserType.Administrator
                && userType != WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                ShowAdministratorPermissionDenied();
                return;
            }

            string sqlExecutablePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "sqlForm",
                "sql.exe");

            if (!File.Exists(sqlExecutablePath))
            {
                System.Windows.MessageBox.Show(
                    sqlExecutablePath,
                    _3DLaserGlueInspection.Resources.LanguageDict.Prompt,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = sqlExecutablePath,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(sqlExecutablePath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    _3DLaserGlueInspection.Resources.LanguageDict.Prompt,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MenuItem_PLCSetting_signalSetting_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                model.plc.ShowForm();
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }


        private void MenuItem_robotSetting_signalSetting_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                model.robot.ShowForm();
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }

        private void MenuItem_productSetting_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                new CarNameIdSet().ShowCarSetForm(CamParams.GetParamNames());
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }

        private void MenuItem_paraSetting_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                new WindowVision().ShowDialog();
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }

        private void MenuItem_standardBlockTest_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPasswordLogin(out WindowPassWord.PasswordUserType userType))
            {
                return;
            }

            if (userType == WindowPassWord.PasswordUserType.SuperAdministrator)
            {
                WindowStandardBlockTest window = new WindowStandardBlockTest();
                window.Owner = this;
                window.ShowDialog();
            }
            else
            {
                ShowAdministratorPermissionDenied();
            }
        }

        private void MenuItem_changePasswordSetting_Click(object sender, RoutedEventArgs e)
        {
            new WindowNewPassWord().ShowDialog();
        }


        private void Show2DWindow()
        {
            hWindowControl.Visibility = Visibility.Visible;
            _3DShowControl.Visibility = Visibility.Hidden;

        }
        private void Show3DWindow()
        {
            hWindowControl.Visibility = Visibility.Hidden;
            _3DShowControl.Visibility = Visibility.Visible;

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (resultTabControl.SelectedIndex == 0)
            {
                //Show2DWindow();
            }
            else
            {
                Show3DWindow();
            }
        }

        private void _2DResultDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            try
            {
                Show2DWindow();
                var _cells = _2DResultDataGrid.SelectedCells;
                if (_cells.Any())
                {
                    Dictionary<int, string> CamKeyDict = new Dictionary<int, string> {
                        {0, "Cam1" } ,
                        {1, "Cam2" } ,
                        {2, "Cam3" } ,
                        {3, "Cam4" } 
                    };
                    int rowIndex = _2DResultDataGrid.Items.IndexOf(_cells.First().Item);
                    int columnIndex = _cells.First().Column.DisplayIndex;

                    string camKey = CamKeyDict[columnIndex];
                    string[] strings =new string[0];
                    switch (columnIndex)
                    {
                        case 0:
                            strings = model.mainModel.ImageResultRecords[rowIndex].Cam1.Split(':');
                            break;
                        case 1:
                            strings = model.mainModel.ImageResultRecords[rowIndex].Cam2.Split(':');
                            break;
                        case 2:
                            strings = model.mainModel.ImageResultRecords[rowIndex].Cam3.Split(':');
                            break;
                        case 3:
                            strings = model.mainModel.ImageResultRecords[rowIndex].Cam4.Split(':');
                            break;
                        default:
                            break;
                    }

                    if (strings.Length > 1)
                    {
                        if (int.TryParse(strings[0], out int segmentIndex) 
                            && int.TryParse(strings[1], out int segmentSubIndex))
                        {
                            


                            if (model.outLineDict.ContainsKey(camKey) && 
                                model.outLineDict[camKey].Count > segmentIndex &&
                                model.outLineDict[camKey][segmentIndex].ContainsKey(model.ImageKeys[camKey][segmentIndex][segmentSubIndex]))
                            {
                                var cutSet = model.sets[model.car.Name].CutSets[segmentIndex];

                                // 显示检测结果
                                Mat hXLDCont10mm = model.outLineDict[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];
                                if (model.glueRegionDict.ContainsKey(camKey) && model.glueRegionDict[camKey].Count > segmentIndex && model.glueRegionDict[camKey][segmentIndex].ContainsKey(model.ImageKeys[camKey][segmentIndex][segmentSubIndex]))
                                {
                                    Mat hRegion = model.glueRegionDict[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];
                                    Mat hRegionSmallestRectangle2 = model.glueSmallRectRegionDict[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];
                                    Data data = model.glueDataDict[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];
                                    BResult bResult = model.glueResultDict[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];




                                    GlobalVarAndFunc.ShowImageData((int)model.displaySize[camKey][segmentIndex].Width, 
                                        (int)model.displaySize[camKey][segmentIndex].Height, cutSet, cutSet.imageSet[columnIndex][segmentSubIndex],
                                        hXLDCont10mm, 
                                        hRegion, 
                                        hRegionSmallestRectangle2, 
                                        data, bResult,ref hWindowControl, 
                                        ref showing,ref olockShow);
                                }
                                else
                                {
                                    GlobalVarAndFunc.ShowImageData((int)model.displaySize[camKey][segmentIndex].Width, (int)model.displaySize[camKey][segmentIndex].Height, cutSet, hXLDCont10mm,
                                         ref hWindowControl, ref showing, ref olockShow);
                                }

                                // 显示2d图像位置
                                {
                                    hWindowModel.RemoveChildren(model.showCheckPoint);
                                    System.Windows.Point point = model.glue2DCheckPoint[camKey][segmentIndex][model.ImageKeys[camKey][segmentIndex][segmentSubIndex]];
                                    model.showCheckPoint = hWindowModel.AddCircle(point, 5, Colors.Yellow);
                                }

                                return;
                            }
                        }
                    }
                }

            }
            catch (Exception ex) 
            { 
                System.Windows.Forms.MessageBox.Show(ex.ToString());
            }
        }


        bool showing;
        object olockShow = new object();

        private void MenuItem_chineseLanguage_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_chineseLanguage.IsChecked = true;
            MenuItem_englishLanguage.IsChecked = false;

            GlobalVarAndFunc.SwitchLanguage("zh-CN");
        }

        private void MenuItem_englishLanguage_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_chineseLanguage.IsChecked = false;
            MenuItem_englishLanguage.IsChecked = true;

            GlobalVarAndFunc.SwitchLanguage("en-US");
        }

        private void menuItem_custom_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DemoCheck_Checked(object sender, RoutedEventArgs e)
        {
            model.isDemoMode = (bool)DemoCheck.IsChecked;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            double transformRX =0, transformRY = 0, transformRZ = 0;
            CoordinateTransform.transformCartPose2(
                    -179.779, 178.588, -3.603,
                    2,
                    ref transformRX,
                    ref transformRY,
                    ref transformRZ,
                    0);
        }

        private void handVinCheck_Click(object sender, RoutedEventArgs e)
        {
            model.isHandVIN = (bool)handVinCheck.IsChecked;
            if (model.isHandVIN)
            {
                handVIN.IsEnabled = true;
            }
            else
            {
                handVIN.IsEnabled = false;
            }
        }

        private void handVIN_TextChanged(object sender, TextChangedEventArgs e)
        {
            model.handVIN = handVIN.Text;
        }

        //void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm)
        //{
        //    if (!showing)
        //    {
        //        showing = true;
        //        try
        //        {
        //            lock (olockShow)
        //            {
        //                Mat mat = new Mat();
        //                mat = Mat.Zeros((int)(showHeight * Vision.scaleSize), (int)(showWidth * Vision.scaleSize), MatType.CV_8UC3);
        //                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
        //                //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
        //                PointCollection points = new PointCollection();
        //                for (int i = 0; i < hXLDCont10mm.Rows; i++)
        //                {
        //                    System.Windows.Point point = new System.Windows.Point();
        //                    point.X = hXLDCont10mm.At<double>(i, 0);
        //                    point.Y = hXLDCont10mm.At<double>(i, 1);
        //                    points.Add(point);
        //                }
        //                //DispPolylinejHWindowControlEvent(points, Colors.Gray);
        //                hWindowModel.AddPolyline(points, Colors.Gray);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Windows.Forms.MessageBox.Show(ex.ToString());
        //        }
        //        showing = false;
        //    }
        //}
        //void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, bResult bResult)
        //{
        //    if (!showing)
        //    {
        //        showing = true;
        //        try
        //        {
        //            lock (olockShow)
        //            {
        //                Mat mat = new Mat();
        //                mat = Mat.Zeros((int)(showHeight * Vision.scaleSize), (int)(showWidth * Vision.scaleSize), MatType.CV_8UC3);

        //                //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
        //                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));

        //                PointCollection points = new PointCollection();
        //                for (int i = 0; i < hXLDCont10mm.Rows; i++)
        //                {
        //                    System.Windows.Point point = new System.Windows.Point();
        //                    point.X = hXLDCont10mm.At<double>(i, 0);
        //                    point.Y = hXLDCont10mm.At<double>(i, 1);
        //                    points.Add(point);
        //                }
        //                //DispPolylinejHWindowControlEvent(points, Colors.Gray);
        //                hWindowModel.AddPolyline(points, Colors.Gray);

        //                if (!hRegion.Empty())
        //                {
        //                    PointCollection regionPoints = new PointCollection();
        //                    for (int i = 0; i < hRegion.Rows; i++)
        //                    {
        //                        System.Windows.Point point = new System.Windows.Point();
        //                        point.X = hRegion.At<double>(i, 0);
        //                        point.Y = hRegion.At<double>(i, 1);
        //                        regionPoints.Add(point);
        //                    }

        //                    hWindowModel.AddPolygon(points, Colors.Red, "fill");

        //                    //DispPolygonjHWindowControlEvent(regionPoints, Colors.Red, "fill");
        //                    PointCollection regionSmallestRectangle2Points = new PointCollection();
        //                    for (int i = 0; i < hRegionSmallestRectangle2.Rows; i++)
        //                    {
        //                        System.Windows.Point point = new System.Windows.Point();
        //                        point.X = hRegionSmallestRectangle2.At<double>(i, 0);
        //                        point.Y = hRegionSmallestRectangle2.At<double>(i, 1);
        //                        regionSmallestRectangle2Points.Add(point);
        //                    }

        //                    //DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");
        //                    hWindowModel.AddPolygon(regionSmallestRectangle2Points, Colors.Blue, "margin");

        //                    string text = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + $"{data.胶高:0.00}\r\n"
        //                       + _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth+":" + $"{data.胶宽:0.00}\r\n"
        //                       + _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + $"{data.面积:0.00}";


        //                    //DispTextInImageHWindowControlEvent(text, Colors.Black, (int)data.column, (int)data.row);
        //                    hWindowModel.AddTextBlock(text, Colors.Black, (int)data.column, (int)data.row);

        //                    //hWindowControl.DispTextInImage(text, data.row, data.column);
        //                    string textWindow1 = _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth+":" + (bResult.胶宽 ? "OK" : "NG");
        //                    string textWindow2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + (bResult.胶高 ? "OK" : "NG");
        //                    string textWindow3 = _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + (bResult.面积 ? "OK" : "NG");
        //                    string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;

        //                    //DispTextInImageHWindowControlEvent(textWindow, Colors.Black, 10, 10);
        //                    hWindowModel.AddTextBlock(textWindow, Colors.Black, 10, 10);

        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Windows.Forms.MessageBox.Show(ex.ToString());
        //        }
        //        showing = false;
        //    }
        //}


    }
}
