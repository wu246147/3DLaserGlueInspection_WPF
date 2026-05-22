using Microsoft.Win32;
using OpenCvSharp;
using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static _3DLaserGlueInspection.MainModel;
using static _3DLaserGlueInspection.MainWindowModel;
using static _3DLaserGlueInspection.MvCamera;
using static System.Net.Mime.MediaTypeNames;

namespace _3DLaserGlueInspection.subForm
{

    public class WindowCameraModel : NotifyBase
    {
        ObservableCollection<LogRecord> _logRecord = new ObservableCollection<LogRecord>();
        public ObservableCollection<LogRecord> LogRecords { get { return _logRecord; } set { _logRecord = value; this.DoNotify(); } }
    }
    /// <summary>
    /// WindowCamera.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCamera : System.Windows.Window
    {
        public WindowCameraModel mainModel;

        Cam cam = new Cam();
        CamParams Params = new CamParams();
        CamParam camParam = null;

        bool _isAlter = false;

        List<Mat> MImages = new List<Mat>();

        Stopwatch stopWatch = new Stopwatch();

        Mat img = null;
        bool busy = false;

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //判断输入是否正负号，小数点，或数字。
            e.Handled = new Regex(@"[^0-9+\-.]+").IsMatch(e.Text);

        }
        public void ShowMessage(string mesage)
        {
            ShowMessage(mesage, LogType.normal);
        }

        public void ShowMessage(string mesage, LogType type)
        {
            DateTime dateTime = DateTime.Now;

            LogRecord logRecord = new LogRecord();
            logRecord.LogTime = dateTime.ToString("G");
            logRecord.LogInfo = mesage;

            switch (type)
            {
                case LogType.normal:
                    logRecord.LogResult = "normal";
                    break;
                case LogType.ok:
                    logRecord.LogResult = "ok";

                    break;
                case LogType.ng:
                    logRecord.LogResult = "ng";

                    break;
                case LogType.warn:
                    logRecord.LogResult = "warn";

                    break;
                default:
                    break;
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                while (mainModel.LogRecords.Count > 1000)
                {
                    mainModel.LogRecords.RemoveAt(mainModel.LogRecords.Count - 1);
                }
                mainModel.LogRecords.Insert(0, logRecord);
            });

        }
        void ButtonsEnable(bool flag)
        {
            closeCamButton.IsEnabled = flag;
            continuousCheckButton.IsEnabled = flag;
            singleCheckButton.IsEnabled = flag;
            stopCheckButton.IsEnabled = flag;
            getExportTimeButton.IsEnabled = flag;
            setExportTimeButton.IsEnabled = flag;
            getMaxSizeButton.IsEnabled = flag;
            setCurrentSizeButton.IsEnabled = flag;
            getCurrentSizeButton.IsEnabled = flag;
            setCurrentOffsetButton.IsEnabled = flag;
            getCurrentOffsetButton.IsEnabled = flag;
            setCurrentFPSButton.IsEnabled = flag;
            getCurrentFPSButton.IsEnabled = flag;

            CamNameComboBox.IsEnabled = !flag;
            openCamButton.IsEnabled = !flag;
            newCamParaNameComboBox.IsEnabled = !flag;
            selectCamGrid.IsEnabled = !flag;
        }


        void ShowImage(Mat imgMat)
        {
            if (imgMat.Empty())
            {
                return;
            }
            
            Dispatcher.Invoke(new Action(() => { 
                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(imgMat));
                if ((bool)cacheImgCheck.IsChecked)
                {
                    MImages.Add(img.Clone());
                    imageCountLabel.Content = MImages.Count.ToString();
                }

            }));

            
           
        }

        public WindowCamera()
        {
            InitializeComponent();

            mainModel = new WindowCameraModel();
            this.DataContext = mainModel;

            saveImgTypeComboBox.Items.Add(".png");
            saveImgTypeComboBox.Items.Add(".bmp");
            saveImgTypeComboBox.Items.Add(".tif");
            saveImgTypeComboBox.Items.Add(".jpg");

        }
        public void WindowCamera_Loaded(object sender, RoutedEventArgs e)
        {
            //翻译，未启用
            //GeneralFunc.ChangeLanguateFun(typeof(FormCamera), this);
            paraGrip.IsEnabled = false;
            ButtonsEnable(false);

            if (!Params.Load())
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("参数加载失败：") + Params.ErrMsg);
            }

            string[] ParaNames = CamParams.GetParamNames();
            for (int i = 0; i < ParaNames.Length; i++)
            {
                newCamParaNameComboBox.Items.Add(ParaNames[i]);
            }


            newCamParaNameComboBox.SelectionChanged += comboBoxParamName_SelectedIndexChanged;
        }


        private void comboBoxParamName_SelectedIndexChanged(object sender, EventArgs e)
        {
            paraGrip.IsEnabled = newCamParaNameComboBox.SelectedIndex >= 0;
            //RadioButton_Cam_CheckedChanged(null, null);
            radioButtonCam1.IsChecked = true;
            RadioButton_Cam_CheckedChanged(null, null);
        }

        private void RadioButton_Cam_CheckedChanged(object sender, EventArgs e)
        {
            DisenableUpData();
            string camKey = (bool)radioButtonCam1.IsChecked ? "Cam1" : (bool)radioButtonCam2.IsChecked ? "Cam2" : (bool)radioButtonCam3.IsChecked ? "Cam3" : "Cam4";

            string camParaName = newCamParaNameComboBox.Items[newCamParaNameComboBox.SelectedIndex].ToString();
            if (!Params.Param[camParaName].ContainsKey(camKey))
            {
                Params.Param[camParaName].Add(camKey, new CamParam());
            }
            camParam = Params.Param[camParaName][camKey];

            checkBoxEnableCam.IsChecked = camParam.Enable;
            if (!CamNameComboBox.Items.Contains(camParam.CamName))
            {
                CamNameComboBox.Items.Add(camParam.CamName);
            }
            CamNameComboBox.SelectedValue = camParam.CamName;
            exportTimeNumericUpDown.Text = Convert.ToString(camParam.Exposure);
            gainNumericUpDown.Text = Convert.ToString(camParam.Gain);

            currentWidthNumericUpDown.Text = Convert.ToString(camParam.SizeWidth);
            currentHeightNumericUpDown.Text = Convert.ToString(camParam.SizeHeight);
            widthMaxNumericUpDown.Text = Convert.ToString(camParam.WidthMax);
            heightMaxNumericUpDown.Text = Convert.ToString(camParam.HeightMax);
            currentOffsetXNumericUpDown.Text = Convert.ToString(camParam.OffsetX);
            currentOffsetYNumericUpDown.Text = Convert.ToString(camParam.OffsetY);
            currentFPSNumericUpDown.Text = Convert.ToString(camParam.Hz);
            useFPSCheck.IsChecked = camParam.HzEnable;
            verticalMirror.IsChecked = camParam.ReverseX;
            horizontalMirror.IsChecked = camParam.ReverseY;
            saveImgTypeComboBox.SelectedValue = camParam.ImageFormat;

            EnableUpData();
        }
        private void EnableUpData()
        {
            checkBoxEnableCam.Checked += Updata;
            checkBoxEnableCam.Unchecked += Updata;

            CamNameComboBox.SelectionChanged += Updata;
            exportTimeNumericUpDown.TextChanged += Updata;
            gainNumericUpDown.TextChanged += Updata;

            currentWidthNumericUpDown.TextChanged += Updata;
            currentHeightNumericUpDown.TextChanged += Updata;
            widthMaxNumericUpDown.TextChanged += Updata;
            heightMaxNumericUpDown.TextChanged += Updata;
            currentOffsetXNumericUpDown.TextChanged += Updata;
            currentOffsetYNumericUpDown.TextChanged += Updata;
            currentFPSNumericUpDown.TextChanged += Updata;

            useFPSCheck.Checked += Updata;
            useFPSCheck.Unchecked += Updata;
            verticalMirror.Checked += Updata;
            verticalMirror.Unchecked += Updata;
            horizontalMirror.Checked += Updata;
            horizontalMirror.Unchecked += Updata;
            saveImgTypeComboBox.SelectionChanged += Updata;
        }

        private void DisenableUpData()
        {
            checkBoxEnableCam.Checked -= Updata;
            checkBoxEnableCam.Unchecked -= Updata;

            CamNameComboBox.SelectionChanged -= Updata;
            exportTimeNumericUpDown.TextChanged -= Updata;
            gainNumericUpDown.TextChanged -= Updata;

            currentWidthNumericUpDown.TextChanged -= Updata;
            currentHeightNumericUpDown.TextChanged -= Updata;
            widthMaxNumericUpDown.TextChanged -= Updata;
            heightMaxNumericUpDown.TextChanged -= Updata;
            currentOffsetXNumericUpDown.TextChanged -= Updata;
            currentOffsetYNumericUpDown.TextChanged -= Updata;
            currentFPSNumericUpDown.TextChanged -= Updata;

            useFPSCheck.Checked -= Updata;
            useFPSCheck.Unchecked -= Updata;
            verticalMirror.Checked -= Updata;
            verticalMirror.Unchecked -= Updata;
            horizontalMirror.Checked -= Updata;
            horizontalMirror.Unchecked -= Updata;
            saveImgTypeComboBox.SelectionChanged -= Updata;
        }
        private void Updata(object sender, EventArgs e)
        {
            //string camKey = radioButtonCam1.Checked ? "Cam1" : radioButtonCam2.Checked ? "Cam2" : radioButtonCam3.Checked ? "Cam3" : "Cam4";
            //var camParam = Params.param[comboBoxParamName.Text][camKey];
            _isAlter = true;
            if (CamNameComboBox.SelectedIndex >= 0)
            {
                try
                {
                    camParam.Enable = (bool)checkBoxEnableCam.IsChecked;
                    camParam.CamName = CamNameComboBox.Items[CamNameComboBox.SelectedIndex].ToString();
                    camParam.Exposure = Convert.ToInt32(exportTimeNumericUpDown.Text);
                    camParam.Gain = Convert.ToInt32(gainNumericUpDown.Text);

                    camParam.SizeWidth = Convert.ToInt32(currentWidthNumericUpDown.Text);
                    camParam.SizeHeight = Convert.ToInt32(currentHeightNumericUpDown.Text);
                    camParam.WidthMax = Convert.ToInt32(widthMaxNumericUpDown.Text);
                    camParam.HeightMax = Convert.ToInt32(heightMaxNumericUpDown.Text);
                    camParam.OffsetX = Convert.ToInt32(currentOffsetXNumericUpDown.Text);
                    camParam.OffsetY = Convert.ToInt32(currentOffsetYNumericUpDown.Text);
                    camParam.Hz = (float)Convert.ToDouble(currentFPSNumericUpDown.Text);
                    camParam.HzEnable = (bool)useFPSCheck.IsChecked;
                    camParam.ReverseX = (bool)verticalMirror.IsChecked;
                    camParam.ReverseY = (bool)horizontalMirror.IsChecked;
                    camParam.ImageFormat = saveImgTypeComboBox.SelectedValue.ToString();
                }
                catch
                { 
                }
            }
        }
        private void scanCamButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.Find(out string[] names, out string[] SNs, out string[] ManufacturerNames, out MV_CC_DEVICE_INFO[] DeviceList))
            {
                CamNameComboBox.Items.Clear();

                ShowMessage(GlobalVarAndFunc.LanguageTranslate("找到") + DeviceList.Length + GlobalVarAndFunc.LanguageTranslate("个相机"));
                for (int i = 0; i < SNs.Length; i++)
                {
                    CamNameComboBox.Items.Add((SNs[i]).ToString());
                }

                if (!CamNameComboBox.Items.Contains(camParam.CamName))
                {
                    CamNameComboBox.SelectedValue = camParam.CamName;
                }

            }
            else
            {
                ShowMessage(cam.ErrMsg);
            }
        }



        private void openCamButton_Click(object sender, RoutedEventArgs e)
        {
            if ("" == CamNameComboBox.SelectedValue.ToString() ? cam.Open() : cam.OpenBySN(CamNameComboBox.SelectedValue.ToString()))
            {
                //关闭光源
                Line1EnableCheck.IsChecked = false;
                Line2EnableCheck.IsChecked = false;
                
                //控件启用
                ButtonsEnable(true);
                ShowMessage($"{cam.Name}({cam.SN})" + GlobalVarAndFunc.LanguageTranslate("相机打开成功"));
                if (cam.InitSet(camParam, true))
                {
                    ShowMessage($"{cam.Name}({cam.SN})" + GlobalVarAndFunc.LanguageTranslate("相机初始化成功"));
                }
                else
                {
                    ShowMessage($"{cam.Name}({cam.SN})" + GlobalVarAndFunc.LanguageTranslate("相机初始化失败" + "：") + cam.ErrMsg);
                }
            }
            else
            {
                ShowMessage(cam.ErrMsg);
            }
        }

        private void singleCheckButton_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            if (cam.OneShot(out img))
            {
                sw.Stop();
                Dispatcher.Invoke(new Action(() =>
                {
                    hWindowModel.ClearChildren();
                }));
                ShowImage(img);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("单帧采集时间") + "：" + sw.ElapsedMilliseconds.ToString() + "ms");
            }
            else
            {
                sw.Stop();
                ShowMessage(cam.ErrMsg);
            }
        }

        private void continuousCheckButton_Click(object sender, RoutedEventArgs e)
        {
            singleCheckButton.IsEnabled = false;
            continuousCheckButton.IsEnabled = false;
            stopWatch.Restart();
            cam.KeepShot(ShowImage);
            ShowMessage(GlobalVarAndFunc.LanguageTranslate("连续采集开始"));
        }

        private void closeCamButton_Click(object sender, RoutedEventArgs e)
        {
            //关闭光源
            Line1EnableCheck.IsChecked = false;
            Line2EnableCheck.IsChecked = false;

            //关闭相机
            cam.Close();
            //控件复原
            ButtonsEnable(false);
            ShowMessage($"{cam.Name}({cam.SN})" + GlobalVarAndFunc.LanguageTranslate("相机关闭"));
        }

        private void stopCheckButton_Click(object sender, RoutedEventArgs e)
        {
            cam.StopGrabbing();
            stopWatch.Stop();
            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机采集停止,采集时间：") + stopWatch.ElapsedMilliseconds.ToString());
            singleCheckButton.IsEnabled = true;
            continuousCheckButton.IsEnabled = true;
        }

        private void getExportTimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetExposure(out float value))
            {
                exportTimeNumericUpDown.Text = Convert.ToString(value);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("曝光时间：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("曝光时间获取失败:") + cam.ErrMsg);
            }
        }

        private void setExportTimeButton_Click(object sender, RoutedEventArgs e)
        {
            double value = Convert.ToDouble(exportTimeNumericUpDown.Text);
            if (cam.SetExposure((float)value))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置曝光：") + exportTimeNumericUpDown.Text);
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("曝光设置失败:") + cam.ErrMsg);
            }
        }

        private void horizontalMirror_Checked(object sender, RoutedEventArgs e)
        {
            if (!cam.IsOpen)
            {
                return;
            }
            if (!busy)
            {
                busy = true;
                bool bValue = (bool)horizontalMirror.IsChecked;
                if (cam.SetReverseX(bValue))
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置水平镜像使能：") + bValue);
                }
                else
                {
                    horizontalMirror.IsChecked = !bValue;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("水平镜像使能设置失败:") + cam.ErrMsg);
                }
                busy = false;
            }
        }

        private void verticalMirror_Checked(object sender, RoutedEventArgs e)
        {
            if (!cam.IsOpen)
            {
                return;
            }
            if (!busy)
            {
                busy = true;
                bool bValue = (bool)horizontalMirror.IsChecked;
                if (cam.SetReverseY(bValue))
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置垂直镜像使能：") + bValue);
                }
                else
                {
                    horizontalMirror.IsChecked = !bValue;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("垂直镜像使能设置失败:") + cam.ErrMsg);
                }
                busy = false;
            }
        }

        private void getMaxSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetWidthMax(out long value))
            {
                widthMaxNumericUpDown.Text = Convert.ToString(value);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像最大宽度：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像最大宽度获取失败:") + cam.ErrMsg);
            }
            if (cam.GetHeightMax(out long value2))
            {
                heightMaxNumericUpDown.Text = Convert.ToString(value2);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像最大高度：") + value2.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像最大高度获取失败:") + cam.ErrMsg);
            }
        }

        private void getCurrentSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetWidth(out long value))
            {
                currentWidthNumericUpDown.Text = Convert.ToString(value);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像宽度：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像宽度获取失败:") + cam.ErrMsg);
            }
            if (cam.GetHeight(out long value2))
            {
                currentHeightNumericUpDown.Text = Convert.ToString(value2);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像高度：") + value2.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像高度获取失败:") + cam.ErrMsg);
            }
        }

        private void setCurrentSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.SetWidth((long)Convert.ToInt64(currentWidthNumericUpDown.Text)))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置图像宽度：") + Convert.ToInt64(currentWidthNumericUpDown.Text));
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像宽度设置失败:") + cam.ErrMsg);
            }
            if (cam.SetHeight((long)Convert.ToInt64(currentHeightNumericUpDown.Text)))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置图像高度：") + Convert.ToInt64(currentHeightNumericUpDown.Text));
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像高度设置失败:") + cam.ErrMsg);
            }
        }

        private void getCurrentOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetOffsetX(out long value))
            {
                long 参数值 = (bool)verticalMirror.IsChecked ? (long)(Convert.ToInt64(widthMaxNumericUpDown.Text)
                    - Convert.ToInt64(currentWidthNumericUpDown.Text) - value) : value;
                currentOffsetXNumericUpDown.Text = Convert.ToString(参数值);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("水平偏移参数值：") + 参数值.ToString() + GlobalVarAndFunc.LanguageTranslate("，相机值：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("水平偏移获取失败:") + cam.ErrMsg);
            }
            if (cam.GetOffsetY(out long value2))
            {
                long 参数值 = (bool)horizontalMirror.IsChecked ? (long)(Convert.ToInt64(heightMaxNumericUpDown.Text)
                    - Convert.ToInt64(currentHeightNumericUpDown.Text) - value2) : value2;
                currentOffsetYNumericUpDown.Text = Convert.ToString(参数值);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("垂直偏移参数值：") + 参数值.ToString() + GlobalVarAndFunc.LanguageTranslate("，相机值：") + value2.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("垂直偏移获取失败:") + cam.ErrMsg);
            }
        }

        private void setCurrentOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            long 参数值offsetX = Convert.ToInt32(currentOffsetXNumericUpDown.Text);
            long 相机值offsetX = (bool)verticalMirror.IsChecked ? (long)(Convert.ToInt32(widthMaxNumericUpDown.Text) - Convert.ToInt32(currentWidthNumericUpDown.Text) -
                参数值offsetX) : 参数值offsetX;
            if (cam.SetOffsetX(相机值offsetX))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置水平偏移参数值：") + 参数值offsetX.ToString() + GlobalVarAndFunc.LanguageTranslate("，相机值：") + 相机值offsetX.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("水平偏移设置失败:") + cam.ErrMsg);
            }
            long 参数值offsetY = Convert.ToInt32(currentOffsetYNumericUpDown.Text);
            long 相机值offsetY = (bool)horizontalMirror.IsChecked ? (long)(Convert.ToInt32(heightMaxNumericUpDown.Text) - Convert.ToInt32(currentHeightNumericUpDown.Text) -
                参数值offsetY) : 参数值offsetY;
            if (cam.SetOffsetY(相机值offsetY))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置垂直偏移参数值：") + 参数值offsetY.ToString() + GlobalVarAndFunc.LanguageTranslate("，相机值：") + 相机值offsetY.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("垂直偏移设置失败:") + cam.ErrMsg);
            }
        }

        private void useFPSCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!cam.IsOpen)
            {
                return;
            }
            if (!busy)
            {
                busy = true;
                bool bValue = (bool)useFPSCheck.IsChecked;
                if (cam.SetAcquisitionFrameRateEnable(bValue))
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置帧率使能：") + bValue.ToString());
                }
                else
                {
                    useFPSCheck.IsChecked = !bValue;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("帧率使能设置失败:") + cam.ErrMsg);
                }
                busy = false;
            }
        }

        private void getCurrentFPSButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetAcquisitionFrameRate(out float value))
            {
                currentFPSNumericUpDown.Text = Convert.ToString( value);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置的帧率：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置的帧率获取失败:") + cam.ErrMsg);
            }
            if (cam.GetResultingFrameRate(out float value2))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("实际帧率：") + value2.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("实际帧率获取失败:") + cam.ErrMsg);
            }
        }

        private void setCurrentFPSButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.SetAcquisitionFrameRate((float)Convert.ToDouble( currentFPSNumericUpDown.Text)))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置帧率：") + currentFPSNumericUpDown.Text);
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("帧率设置失败:") + cam.ErrMsg);
            }
        }

        private void saveCurrentImgButton_Click(object sender, RoutedEventArgs e)
        {
            if (img != null)
            {
                System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                sfd.Title = GlobalVarAndFunc.LanguageTranslate("选择要保存的位置");
                sfd.Filter = "PNG(*.png)|*.png|Bitmap(*.bmp)|*.bmp|Tiff(*.tif)|*.tif|JPEG(*.jpg)|*.jpg|所有文件(*.*)|*.*";
                if (System.Windows.Forms.DialogResult.OK == sfd.ShowDialog())
                {
                    string path = sfd.FileName;
                    string format = System.IO.Path.GetExtension(path);
                    Cv2.ImWrite(path, img);
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图片保存成功"));
                }
                //sfd.Dispose();
            }
        }

        private void saveParaButton_Click(object sender, RoutedEventArgs e)
        {
            if (Params.Save())
            {
                _isAlter = false;
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("参数保存成功"));
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("参数保存失败：") + Params.ErrMsg);
            }
        }


        private void newCamParaButton_Click(object sender, RoutedEventArgs e)
        {
            WindowGetName form = new WindowGetName();
            form.CheckName = CamParams.GetParamNames();
            if (true == form.ShowDialog())
            {
                Params.Param.Add(form.Value, new Dictionary<string, CamParam>());
                for (int i = 1; i <= 4; i++)
                {
                    Params.Param[form.Value].Add("Cam" + i, new CamParam() { Key = "Cam" + i });
                }
                Params.CopyParam(form.CopyName, form.Value);
                newCamParaNameComboBox.Items.Add(form.Value);
                //newCamParaNameComboBox.Text = form.Value;
                newCamParaNameComboBox.SelectedValue = form.Value;
                _isAlter = true;
            }
        }

        private void saveCacheButton_Click(object sender, RoutedEventArgs e)
        {
            string path = "images";
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
            for (int i = 0; i < MImages.Count; i++)
            {
                //HImages[i].WriteImage("png", 0, $"{path}\\{i:0000}.png");//=png 6
                Cv2.ImWrite($"{path}\\{i:0000}.png", MImages[i]);
            }
        }

        private void clearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            MImages.ForEach(img => { img.Dispose(); });
            MImages.Clear();
            imageCountLabel.Content= MImages.Count.ToString();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isAlter)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("是否保存参数？"), GlobalVarAndFunc.LanguageTranslate("提示"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!Params.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("保存失败：") + Params.ErrMsg, GlobalVarAndFunc.LanguageTranslate("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        e.Cancel = true;
                        return;
                    }
                    else
                    {
                        _isAlter = false;
                    }
                }
                else if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            try
            {
                img?.Dispose();
                cam.Close();
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("关闭相机"));
            }
            catch { }
        }

        private void Line1EnableCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!cam.SetLine1Inverter((bool)Line1EnableCheck.IsChecked))
            {
                ShowMessage("Line1" + GlobalVarAndFunc.LanguageTranslate("打开设置失败:") + cam.ErrMsg);
            }
        }

        private void Line2EnableCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!cam.SetLine2Inverter((bool)Line2EnableCheck.IsChecked))
            {
                ShowMessage("Line2" + GlobalVarAndFunc.LanguageTranslate("打开设置失败:") + cam.ErrMsg);
            }
        }

        private void getGainTimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam.GetGain(out float value))
            {
                gainNumericUpDown.Text = Convert.ToString(value);
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("增益：") + value.ToString());
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("增益获取失败:") + cam.ErrMsg);
            }
        }

        private void setGainTimeButton_Click(object sender, RoutedEventArgs e)
        {
            double value = Convert.ToDouble(gainNumericUpDown.Text);
            if (cam.SetGain((float)value))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("设置增益：") + gainNumericUpDown.Text);
            }
            else
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("增益设置失败:") + cam.ErrMsg);
            }
        }
    }
}
