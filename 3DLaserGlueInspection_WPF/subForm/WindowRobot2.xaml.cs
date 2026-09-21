using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TCPIP;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection.subForm
{


    /// <summary>
    /// WindowRobot2.xaml 的交互逻辑
    /// </summary>
    public partial class WindowRobot2 : Window
    {

        IRobot signal = null;
        //TCP_Server server = null;
        
        RobotParam param => signal.Param;

        //IPEndPoint param2 => server.ipAndPoint;

        bool isAlter = false;
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //判断输入是否正负号，小数点，或数字。
            e.Handled = new Regex(@"[^0-9+\-.]+").IsMatch(e.Text);

        }

        void ShowMessage(string message)
        {
            logTextBox.Dispatcher.Invoke(() =>
            {
                logTextBox.Text += DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss") + "  " + message + "\r\n";
            }); 
        }

        private void UpData(object sender, EventArgs e)
        {
            param.IpAddress = IpNumericUpDown.Text;
            param.Port = Convert.ToInt32(PortNumericUpDown.Text);

            if (signal is KukaRobot kuka)
            {
                ((KukaRobot)this.signal).Server.ipAndPoint.Address = IPAddress.Parse(IpServerNumericUpDown.Text);
                ((KukaRobot)this.signal).Server.ipAndPoint.Port = Convert.ToInt32(PortServerNumericUpDown.Text);
            }
            else if (signal is KawasakiRobot kawasaki)
            {
                ((KawasakiRobot)this.signal).Server.ipAndPoint.Address = IPAddress.Parse(IpServerNumericUpDown.Text);
                ((KawasakiRobot)this.signal).Server.ipAndPoint.Port = Convert.ToInt32(PortServerNumericUpDown.Text);
            }
            else
            {
                Console.WriteLine("未知的 IRobot 类型");
            }

            isAlter = true;
        }


        public WindowRobot2(IRobot robot)
        {
            InitializeComponent();

            this.signal = robot;

            
        }


        private void WindowRobot2_Load(object sender, RoutedEventArgs e)
        {
            //翻译，未启用
            //GeneralFunc.ChangeLanguateFun(typeof(RobotForm), this);

            signal.Load();


            IpNumericUpDown.Text = param.IpAddress;
            PortNumericUpDown.Text = Convert.ToString(param.Port);

            IpNumericUpDown.TextChanged += UpData;
            PortNumericUpDown.TextChanged += UpData;


            if (signal is KukaRobot kuka)
            {
                ((KukaRobot)this.signal).Server = new TCP_Server();
                ((KukaRobot)this.signal).Server.Load();

                IpServerNumericUpDown.Text = ((KukaRobot)this.signal).Server.ipAndPoint.Address.ToString();
                PortServerNumericUpDown.Text = Convert.ToString(((KukaRobot)this.signal).Server.ipAndPoint.Port);
            }
            else if (signal is KawasakiRobot kawasaki)
            {
                ((KawasakiRobot)this.signal).Server = new TCP_Server();
                ((KawasakiRobot)this.signal).Server.Load();

                IpServerNumericUpDown.Text = ((KawasakiRobot)this.signal).Server.ipAndPoint.Address.ToString();
                PortServerNumericUpDown.Text = Convert.ToString(((KawasakiRobot)this.signal).Server.ipAndPoint.Port);
            }
            else
            {
                Console.WriteLine("未知的 IRobot 类型");
            }

            

            IpServerNumericUpDown.TextChanged += UpData;
            PortServerNumericUpDown.TextChanged += UpData;

        }

        private void WindowRobot2_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isAlter)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DoYouWantToSaveTheParameters, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!signal.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SaveFailed + signal.ErrMsg, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        e.Cancel = true;
                        return;
                    }
                    else
                    {
                        isAlter = false;
                    }


                    if (signal is KukaRobot kuka)
                    {
                        if (!((KukaRobot)this.signal).Server.Save())
                        {
                            System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SaveFailed + ((KukaRobot)this.signal).Server.ErrMsg, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            e.Cancel = true;
                            return;
                        }
                        else
                        {
                            isAlter = false;
                        }
                    }
                    else if (signal is KawasakiRobot kawasaki)
                    {
                        if (!((KawasakiRobot)this.signal).Server.Save())
                        {
                            System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SaveFailed + ((KawasakiRobot)this.signal).Server.ErrMsg, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            e.Cancel = true;
                            return;
                        }
                        else
                        {
                            isAlter = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("未知的 IRobot 类型");
                    }
                   

                }
                else if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            signal.Close();

            if (signal is KukaRobot kuka2)
            {
                ((KukaRobot)this.signal).StopCommunicationThread();
            }
            else if (signal is KawasakiRobot kawasaki)
            {
                ((KawasakiRobot)this.signal).StopCommunicationThread();
            }
            else
            {
                Console.WriteLine("未知的 IRobot 类型");
            }
        }



        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (connectButton.IsEnabled)//没有效果
            {
                connectButton.IsEnabled = false;

                getPoseButton.IsEnabled = false;

                if (signal.Open())
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionSuccessful);
                    disconnectButton.IsEnabled = true;

                    //
                    if (signal is KukaRobot kuka)
                    {
                        getPoseButton.IsEnabled = true;


                    }
                    else if (signal is KawasakiRobot kawasaki)
                    {
                        ((KawasakiRobot)this.signal).robot.OnDataReceived += ProcessInfo;
                    }

                }
                else
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionFailed + signal.ErrMsg);
                    connectButton.IsEnabled = true;
                }
            }
        }

        private void ProcessInfo(string info)
        {
            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReceivedSignal + ":" + info);
        }

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            signal.Close();
            getPoseButton.IsEnabled = false;

            disconnectButton.IsEnabled = false;
            connectButton.IsEnabled = true;
            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.CloseSuccessfully);
        }

        private void connectServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (connectServerButton.IsEnabled)//没有效果
            {
                connectServerButton.IsEnabled = false;

                bool isSuccess = true;

                if (signal is KukaRobot kuka)
                {
                    ((KukaRobot)this.signal).StopCommunicationThread();
                    ((KukaRobot)this.signal).InitCommunicationConnection();
                    while (((KukaRobot)this.signal).Server == null)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    ((KukaRobot)this.signal).Server.reserveInfoSignal += ProcessInfo;

                }
                else if (signal is KawasakiRobot kawasaki)
                {
                    ((KawasakiRobot)this.signal).StopCommunicationThread();
                    ((KawasakiRobot)this.signal).InitCommunicationConnection();

                    while (((KawasakiRobot)this.signal).Server == null)
                    {
                        System.Threading.Thread.Sleep(10);
                    }

                    ((KawasakiRobot)this.signal).Server.reserveInfoSignal += ProcessInfo;

                }
                else
                {
                    Console.WriteLine("未知的 IRobot 类型");
                }

                if (isSuccess)
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionSuccessful);
                    disconnectServerButton.IsEnabled = true;
                }
                else
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionFailed + signal.ErrMsg);
                    connectServerButton.IsEnabled = true;
                }
            }
        }

        private void disconnectServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (signal is KukaRobot kuka)
            {
                ((KukaRobot)this.signal).StopCommunicationThread();
            }
            else if (signal is KawasakiRobot kawasaki)
            {
                ((KawasakiRobot)this.signal).StopCommunicationThread();
            }
            else
            {
                Console.WriteLine("未知的 IRobot 类型");
            }
            disconnectServerButton.IsEnabled = false;
            connectServerButton.IsEnabled = true;
            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.CloseSuccessfully);


        }

        private void getPoseButton_Click(object sender, RoutedEventArgs e)
        {
            bool rt = signal.ReadPose(out PoseParameters pose);
            if (rt)
            {
                ShowMessage($"ReadPose:{pose.x},{pose.y},{pose.z},{pose.rx},{pose.ry},{pose.rz},{pose.R7},{pose.PoseType}");

            }
            else
            {
                ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReadFailed + signal.ErrMsg);

            }
        }
    }
}
