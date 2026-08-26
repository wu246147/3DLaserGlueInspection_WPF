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
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        MainWindowModel model;
        bool stop = true;
        Thread mainThread = null;

        JAKARobot robot = new JAKARobot();


        //readonly UserHWindowControl hWindow数模图 = new UserHWindowControl();
        //readonly UserHWindowControl hWindowControl = new UserHWindowControl();
        //readonly Wpf_halcon.ImageControl2 hWindowModel = new Wpf_halcon.ImageControl2();
        //readonly Wpf_halcon.ImageControl2 hWindowControl = new Wpf_halcon.ImageControl2();

        public MainWindow()
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("0293fde5-6e7c-4c76-bacd-e3bdb0ee6187"))
            {
                System.Windows.Forms.MessageBox.Show("active failed");
            }

            InitializeComponent();
            model = new MainWindowModel();
            this.DataContext = model.mainModel;


            //打开通讯

            model.InitCommunicationConnection();

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

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            model.stop = true;
            model.StopCommunicationThread();

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
            this.Close();
        }

        private void TopMain_MouseLeftBtnDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

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
                if (mainThread == null || !mainThread.IsAlive)
                {
                    model.stop = false;
                    mainThread = new Thread(model.MainRun);

                    ////测试取像问题
                    //mainThread = new Thread(model.AcqAndRobotTest);

                    mainThread.Start();
                }
                else
                {
                    model.ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.TheMainThreadIsAlreadyRunning, LogType.warn);
                }
                //ButtonRun.Content = _3DLaserGlueInspection.Resources.LanguageDict.停止");
                //button启停.Image = Resources._3;

                model.mainModel.buttonRunContentControl = _3DLaserGlueInspection.Resources.LanguageDict.Stop;
                model.mainModel.buttonRunTagControl = "\uE67A";

            }
            else
            {
                if (mainThread != null && mainThread.IsAlive)
                {

                }
                model.stop = true;
                model.mainModel.buttonRunContentControl = _3DLaserGlueInspection.Resources.LanguageDict.Start;
                model.mainModel.buttonRunTagControl = "\uE658";
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
                }
                else
                {
                    simulationCheck.Visibility = Visibility.Visible;
                    simulationPath.Visibility = Visibility.Visible;
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

        private void MenuItem_camSetting_Click(object sender, RoutedEventArgs e)
        {
            WindowCamera windowCamera = new WindowCamera();
            windowCamera.ShowDialog();

        }

        private void MenuItem_robotSetting_signalSetting_Click(object sender, RoutedEventArgs e)
        {
            //密码验证，未启用
            WindowPassWord formPassword = new WindowPassWord();
            if ((bool)formPassword.ShowDialog())
                robot.ShowForm();
        }

        private void MenuItem_productSetting_Click(object sender, RoutedEventArgs e)
        {
            WindowPassWord formPassword = new WindowPassWord();
            if ((bool)formPassword.ShowDialog())
                new CarNameIdSet().ShowCarSetForm(CamParams.GetParamNames());
        }

        private void MenuItem_paraSetting_Click(object sender, RoutedEventArgs e)
        {
            WindowPassWord formPassword = new WindowPassWord();
            if ((bool)formPassword.ShowDialog())
                new WindowVision().ShowDialog();
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
