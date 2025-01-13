using _3DLaserGlueInspection.subForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static _3DLaserGlueInspection.MainWindowModel;

namespace _3DLaserGlueInspection
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowModel model;
        bool stop = true;
        Thread mainThread = null;

        //readonly UserHWindowControl hWindow数模图 = new UserHWindowControl();
        //readonly UserHWindowControl hWindowControl = new UserHWindowControl();



        public MainWindow()
        {
            InitializeComponent();
            model = new MainWindowModel();
            this.DataContext = model.mainModel;
            //mess
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {


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
            if ((string)ButtonRun.Content == GlobalVarAndFunc.LanguageTranslate("启动"))
            {
                if (model.simulation)
                {
                    if (model.simulationPath == "")
                    {
                        model.ShowMessage(GlobalVarAndFunc.LanguageTranslate("仿真路径未填写"), LogType.warn);
                        return;
                    }
                    else if (!Directory.Exists(model.simulationPath))
                    {
                        model.ShowMessage(GlobalVarAndFunc.LanguageTranslate("仿真路径不存在"), LogType.warn);
                        return;
                    }
                }
                if (mainThread == null || !mainThread.IsAlive)
                {
                    model.stop = false;
                    mainThread = new Thread(model.MainRun);
                    mainThread.Start();
                }
                else
                {
                    model.ShowMessage(GlobalVarAndFunc.LanguageTranslate("主线程已经运行中"), LogType.warn);
                }
                //ButtonRun.Content = GlobalVarAndFunc.LanguageTranslate("停止");
                //button启停.Image = Resources._3;

                model.mainModel.buttonRunContentControl = GlobalVarAndFunc.LanguageTranslate("停止");
                model.mainModel.buttonRunTagControl = "\uE67A";

            }
            else
            {
                if (mainThread != null && mainThread.IsAlive)
                {

                }
                model.stop = true;
                model.mainModel.buttonRunContentControl = GlobalVarAndFunc.LanguageTranslate("启动");
                model.mainModel.buttonRunTagControl = "\uE658";
            }
        }


        private void simulationPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            model.simulationPath = simulationPath.Text;
        }

        private void simulationCheck_Click(object sender, RoutedEventArgs e)
        {
            model.simulation = (bool)simulationCheck.IsChecked;
            //测试
            //model.ShowMessage(GlobalVarAndFunc.LanguageTranslate("开启仿真模式"), LogType.warn);
            //model.mainModel.OKCountControl = 100;

            // 保存car文件
            bool result = true;
            Dictionary<Guid, Car> cars = new Dictionary<Guid, Car>();
            Car car = new Car();
            car.CamParamName = "beifen";
            car.IDs = new List<int>{1};
            car.Name = "节卡(lin3D涂胶)";

            cars.Add(Guid.NewGuid(), car);
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                string fPath = basePath + "CarID";
                using (FileStream stream = new FileStream(fPath, FileMode.Create))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(stream, cars);
                }
                File.Copy(fPath, basePath + "CarID_bak", true);
            }
            catch (Exception ex)
            {
                result = false;
            }
        }

    }
}
