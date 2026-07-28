using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// WindowRobot.xaml 的交互逻辑
    /// </summary>
    public partial class WindowRobot : Window
    {
        List<System.Windows.Controls.Label> diNameLabels = new List<System.Windows.Controls.Label>();
        List<System.Windows.Controls.TextBox> diAddressTextBoxs = new List<System.Windows.Controls.TextBox>();
        List<System.Windows.Controls.TextBox> diValueTextBoxs = new List<System.Windows.Controls.TextBox>();

        List<System.Windows.Controls.Label> doNameLabels = new List<System.Windows.Controls.Label>();
        List<System.Windows.Controls.TextBox> doAddressTextBoxs = new List<System.Windows.Controls.TextBox>();
        List<System.Windows.Controls.TextBox> doValueTextBoxs = new List<System.Windows.Controls.TextBox>();

        IRobot signal = null;
        RobotParam param => signal.Param;
        Dictionary<string, IoAddress> ioDict => signal.IoDict;

        bool isAlter = false;
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //判断输入是否正负号，小数点，或数字。
            e.Handled = new Regex(@"[^0-9+\-.]+").IsMatch(e.Text);

        }

        void ShowMessage(string message)
        {
            logTextBox.Text += DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss") + "  " + message + "\r\n";
        }

        private void UpData(object sender, EventArgs e)
        {
            param.IpAddress = IpNumericUpDown.Text;
            param.Port = Convert.ToInt32(PortNumericUpDown.Text);
            isAlter = true;
        }
        private void UpDataDi(object sender, EventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox)
            {
                int i = (int)((System.Windows.Controls.TextBox)sender).Tag;
                if (ioDict.ContainsKey(diNameLabels[i].Content.ToString()))
                {
                    ioDict[diNameLabels[i].Content.ToString()].Address = diAddressTextBoxs[i].Text;
                }
                else
                {
                    ioDict.Add(diNameLabels[i].Content.ToString(), new IoAddress() { IoName = diNameLabels[i].Content.ToString(), Address = diAddressTextBoxs[i].Text });
                }
                isAlter = true;
            }
        }
        private void UpDataDo(object sender, EventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox)
            {
                int i = (int)((System.Windows.Controls.TextBox)sender).Tag;
                if (ioDict.ContainsKey(doNameLabels[i].Content.ToString()))
                {
                    ioDict[doNameLabels[i].Content.ToString()].Address = doAddressTextBoxs[i].Text;
                }
                else
                {
                    ioDict.Add(doNameLabels[i].Content.ToString(), new IoAddress() { IoName = doNameLabels[i].Content.ToString(), Address = doAddressTextBoxs[i].Text });
                }
                isAlter = true;
            }
        }

        private void LblIn_DoubleClick(object sender, EventArgs e)
        {
            if (signal.IsOpen)
            {
                if (sender is System.Windows.Controls.Label)
                {
                    System.Windows.Controls.Label lbl = (System.Windows.Controls.Label)sender;
                    int i = (int)lbl.Tag;
                    DI eDI = (DI)Enum.Parse(typeof(DI), lbl.Content.ToString());
                    if ((int)eDI < 256)
                    {
                        if (signal.Read(eDI, out bool value))
                        {
                            diValueTextBoxs[i].Text = value.ToString();
                        }
                        else
                        {
                            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReadFailed+ signal.ErrMsg);
                        }
                    }
                    else if ((int)eDI < 1024)
                    {
                        if (signal.Read(eDI, out ushort value))
                        {
                            diValueTextBoxs[i].Text = value.ToString();
                        }
                        else
                        {
                            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReadFailed+ signal.ErrMsg);
                        }
                    }
                    else if ((int)eDI < 2048)
                    {

                    }
                    else if ((int)eDI < 4096)
                    {

                    }
                    else if ((int)eDI < 8192)
                    {

                    }
                    else
                    {
                        if (signal.Read(eDI, out string value))
                        {
                            diValueTextBoxs[i].Text = value.ToString();
                        }
                        else
                        {
                            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ReadFailed+ signal.ErrMsg);
                        }
                    }
                }
            }
        }

        private void LblOut_DoubleClick(object sender, EventArgs e)
        {
            if (signal.IsOpen)
            {
                if (sender is System.Windows.Controls.Label)
                {
                    System.Windows.Controls.Label lbl = (System.Windows.Controls.Label)sender;
                    int i = (int)lbl.Tag;
                    DO eDO = (DO)Enum.Parse(typeof(DO), lbl.Content.ToString());
                    if ((int)eDO < 256)
                    {
                        if (bool.TryParse(doValueTextBoxs[i].Text, out bool value))
                        {
                            if (signal.Write(eDO, value))
                            {

                            }
                            else
                            {
                                ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.WriteFailed+ signal.ErrMsg);
                            }
                        }
                        else
                        {
                            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConversionFailedFormatError);
                        }
                    }
                    else if ((int)eDO < 1024)
                    {

                    }
                    else if ((int)eDO < 2048)
                    {

                    }
                    else if ((int)eDO < 4096)
                    {

                    }
                    else if ((int)eDO < 8192)
                    {

                    }
                    else
                    {

                    }
                }
            }
        }

        public WindowRobot(IRobot robot)
        {
            InitializeComponent();
            string[] diNames = Enum.GetNames(typeof(DI));
            for (int i = 0; i < diNames.Length; i++)
            {
                System.Windows.Controls.Label lbl = new System.Windows.Controls.Label();
                lbl.Content = diNames[i];
                lbl.Foreground = new SolidColorBrush(Colors.White);
                lbl.Tag = i;
                lbl.Width = 60;
                lbl.Height = 28;
                lbl.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                lbl.VerticalAlignment = System.Windows.VerticalAlignment.Top;

                lbl.Margin = new Thickness((int)inputNameLabel.Margin.Left, (int)(inputNameLabel.Margin.Top - 5 + (inputNameLabel.Height * 1.5 * (i + 1))), 0, 0);
                lbl.MouseDoubleClick += LblIn_DoubleClick;
                diNameLabels.Add(lbl);
                this.inputGrip.Children.Add(lbl);

                System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox();
                textBox.Tag = i;
                textBox.Width = 60;
                //textBox.Height = 28;
                textBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                textBox.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                textBox.FontSize = 14;
                textBox.Margin = new Thickness((int)inputDirLabel.Margin.Left, (int)(inputDirLabel.Margin.Top - 5 + (inputDirLabel.Height * 1.5 * (i + 1))), 0,0); 
                diAddressTextBoxs.Add(textBox);
                this.inputGrip.Children.Add(textBox);

                System.Windows.Controls.TextBox textBoxValue = new System.Windows.Controls.TextBox();
                textBoxValue.Tag = i;
                textBoxValue.Width = 60;
                //textBoxValue.Height = 28;
                textBoxValue.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                textBoxValue.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                textBoxValue.FontSize = 14;
                textBoxValue.Margin = new Thickness((int)inputValueLabel.Margin.Left, (int)(inputValueLabel.Margin.Top - 5 + (inputValueLabel.Height * 1.5 * (i + 1))), 0, 0);
                textBoxValue.IsReadOnly = true;
                diValueTextBoxs.Add(textBoxValue);
                this.inputGrip.Children.Add(textBoxValue);
            }

            string[] doNames = Enum.GetNames(typeof(DO));
            for (int i = 0; i < doNames.Length; i++)
            {
                System.Windows.Controls.Label lbl = new System.Windows.Controls.Label();
                lbl.Content = doNames[i];
                lbl.Foreground = new SolidColorBrush(Colors.White);
                lbl.Tag = i;
                lbl.Width = 60;
                lbl.Height = 28;
                lbl.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                lbl.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                lbl.Margin = new Thickness((int)outputNameLabel.Margin.Left, (int)(outputNameLabel.Margin.Top - 5 + (outputNameLabel.Height * 1.5 * (i + 1))), 0, 0);
                lbl.MouseDoubleClick += LblOut_DoubleClick;
                doNameLabels.Add(lbl);
                this.outputGrip.Children.Add(lbl);

                System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox();
                textBox.Tag = i;
                textBox.Width = 60;
                //textBox.Height = 28;
                textBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                textBox.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                textBox.FontSize = 14;
                textBox.Margin = new Thickness((int)outputDirLabel.Margin.Left, (int)(outputDirLabel.Margin.Top - 5 + (outputDirLabel.Height * 1.5 * (i + 1))), 0, 0);
                doAddressTextBoxs.Add(textBox);
                this.outputGrip.Children.Add(textBox);

                System.Windows.Controls.TextBox textBoxValue = new System.Windows.Controls.TextBox();
                textBoxValue.Tag = i;
                textBoxValue.Width = 60;
                //textBoxValue.Height = 28;
                textBoxValue.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                textBoxValue.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                textBoxValue.FontSize = 14;
                textBoxValue.Margin = new Thickness((int)outputValueLabel.Margin.Left, (int)(outputValueLabel.Margin.Top - 5 + (outputValueLabel.Height * 1.5 * (i + 1))), 0, 0);
                textBoxValue.IsReadOnly = true;
                doValueTextBoxs.Add(textBoxValue);
                this.outputGrip.Children.Add(textBoxValue);
            }

            this.signal = robot;
        }


        private void WindowRobot_Load(object sender, RoutedEventArgs e)
        {
            //翻译，未启用
            //GeneralFunc.ChangeLanguateFun(typeof(RobotForm), this);

            signal.Load();

            IpNumericUpDown.Text = param.IpAddress;
            PortNumericUpDown.Text = Convert.ToString(param.Port);

            IpNumericUpDown.TextChanged += UpData;
            PortNumericUpDown.TextChanged += UpData;

            for (int i = 0; i < diNameLabels.Count; i++)
            {
                if (ioDict.ContainsKey(diNameLabels[i].Content.ToString()))
                {
                    diAddressTextBoxs[i].Text = ioDict[diNameLabels[i].Content.ToString()].Address;
                }
                diAddressTextBoxs[i].TextChanged += UpDataDi;
            }
            for (int i = 0; i < doNameLabels.Count; i++)
            {
                if (ioDict.ContainsKey(doNameLabels[i].Content.ToString()))
                {
                    doAddressTextBoxs[i].Text = ioDict[doNameLabels[i].Content.ToString()].Address;
                }
                doAddressTextBoxs[i].TextChanged += UpDataDo;
            }
        }

        private void WindowRobot_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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
                }
                else if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            signal.Close();
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (connectButton.IsEnabled)//没有效果
            {
                connectButton.IsEnabled = false;
                if (signal.Open())
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionSuccessful);
                    disconnectButton.IsEnabled = true;
                }
                else
                {
                    ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.ConnectionFailed + signal.ErrMsg);
                    connectButton.IsEnabled = true;
                }
            }
        }

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            signal.Close();
            disconnectButton.IsEnabled = false;
            connectButton.IsEnabled = true;
            ShowMessage(_3DLaserGlueInspection.Resources.LanguageDict.CloseSuccessfully);
        }
    }
}
