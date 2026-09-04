using OpenCvSharp;
using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static _3DLaserGlueInspection.MainWindowModel;
using Wpf_Replace_halcon;

namespace _3DLaserGlueInspection.subForm
{
    public class WindowStandardBlockTestModel : NotifyBase
    {
        private ObservableCollection<LogRecord> _logRecords = new ObservableCollection<LogRecord>();

        public ObservableCollection<LogRecord> LogRecords
        {
            get { return _logRecords; }
            set
            {
                _logRecords = value;
                DoNotify();
            }
        }
    }

    public partial class WindowStandardBlockTest : System.Windows.Window
    {
        private readonly WindowStandardBlockTestModel mainModel;
        private readonly CarNameIdSet cars = new CarNameIdSet();
        private readonly CamParams cameraParams = new CamParams();
        private readonly Cam cam = new Cam();

        private Car selectedCar;
        private Setting setting;
        private CutSet cutSet;
        private ImageSet imageSet;
        private CamParam camParam;
        private string camParamName;
        private string camKey;

        private Mat currentImage;
        private Mat contourPoints;
        private Mat contourPointsForDetection;
        private Mat outMaxRegion;
        private Mat outRegionRectangle2;

        private Data resultData = new Data();
        private BResult bResult = new BResult();

        private object olockShow = new object();
        private bool showing;
        private bool suppressUiEvents;
        private bool isAlter;
        private double originalThreshold;

        private int previousCarIndex = -1;
        private int previousCameraIndex = -1;

        private sealed class LogicalCameraOption
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
        }

        public WindowStandardBlockTest()
        {
            InitializeComponent();

            mainModel = new WindowStandardBlockTestModel();
            DataContext = mainModel;

            UpdateButtons();
        }

        private void ShowMessage(string message, LogType type = LogType.normal)
        {
            LogRecord logRecord = new LogRecord
            {
                LogTime = DateTime.Now.ToString("G"),
                LogInfo = message,
                LogResult = type == LogType.ok ? "ok"
                    : type == LogType.ng ? "ng"
                    : type == LogType.warn ? "warn" : "normal"
            };

            Action addLog = () =>
            {
                while (mainModel.LogRecords.Count > 1000)
                {
                    mainModel.LogRecords.RemoveAt(mainModel.LogRecords.Count - 1);
                }
                mainModel.LogRecords.Insert(0, logRecord);
            };

            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(addLog);
            }
            else
            {
                addLog();
            }
        }

        private void WindowStandardBlockTest_Loaded(object sender, RoutedEventArgs e)
        {
            suppressUiEvents = true;
            try
            {
                if (!cameraParams.Load())
                {
                    ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.ParameterLoadingFailed + cameraParams.ErrMsg, LogType.ng);
                }

                string[] carNames = cars.LoadName();
                foreach (string carName in carNames)
                {
                    carNameComboBox.Items.Add(carName);
                }
            }
            finally
            {
                suppressUiEvents = false;
            }

            if (carNameComboBox.Items.Count > 0)
            {
                carNameComboBox.SelectedIndex = 0;
            }
            else
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady, LogType.warn);
            }

            UpdateButtons();
        }

        private void carNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressUiEvents || carNameComboBox.SelectedIndex < 0)
            {
                return;
            }

            if (!ConfirmPendingChanges())
            {
                RestoreCarSelection();
                return;
            }

            CloseCamera();
            DisposeCurrentData();
            DisposeSetting();

            string carName = carNameComboBox.SelectedItem as string;
            selectedCar = cars.Cars.Values.FirstOrDefault(item => item.Name == carName);
            previousCarIndex = carNameComboBox.SelectedIndex;

            if (selectedCar == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady, LogType.warn);
                UpdateButtons();
                return;
            }

            camParamName = selectedCar.CamParamName;
            setting = new Setting(selectedCar.Name);
            if (!setting.Load())
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.ParameterLoadingFailed + setting.ErrMsg, LogType.warn);
            }

            PopulateLogicalCameras();
            UpdateButtons();
        }

        private void cameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressUiEvents || cameraComboBox.SelectedIndex < 0)
            {
                return;
            }

            if (!ConfirmPendingChanges())
            {
                RestoreCameraSelection();
                return;
            }

            CloseCamera();
            DisposeCurrentData();
            LoadSelectedCamera();
            UpdateButtons();
        }

        private void PopulateLogicalCameras()
        {
            suppressUiEvents = true;
            try
            {
                cameraComboBox.Items.Clear();

                Dictionary<string, CamParam> cameraItems = null;
                if (!string.IsNullOrWhiteSpace(camParamName)
                    && cameraParams.Param.TryGetValue(camParamName, out cameraItems)
                    && cameraItems != null)
                {
                    for (int i = 1; i <= CutSet.CamCount; i++)
                    {
                        string key = "Cam" + i;
                        if (!cameraItems.ContainsKey(key))
                        {
                            continue;
                        }

                        cameraComboBox.Items.Add(new LogicalCameraOption
                        {
                            Key = key,
                            DisplayName = GetLogicalCameraName(i)
                        });
                    }
                }

                if (cameraComboBox.Items.Count > 0)
                {
                    cameraComboBox.SelectedIndex = 0;
                }
            }
            finally
            {
                suppressUiEvents = false;
            }

            if (cameraComboBox.SelectedIndex >= 0)
            {
                LoadSelectedCamera();
            }
            else
            {
                ClearSelectionState();
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockPhysicalCameraMissing, LogType.warn);
            }
        }

        private string GetLogicalCameraName(int cameraIndex)
        {
            string cameraText = global::_3DLaserGlueInspection.Resources.LanguageDict.Cam1;
            switch (cameraIndex)
            {
                case 2:
                    cameraText = global::_3DLaserGlueInspection.Resources.LanguageDict.Cam1;
                    break;
                case 3:
                    cameraText = global::_3DLaserGlueInspection.Resources.LanguageDict.Cam1;
                    break;
                case 4:
                    cameraText = global::_3DLaserGlueInspection.Resources.LanguageDict.Cam1;
                    break;
            }

            return cameraText + cameraIndex;
        }

        private void LoadSelectedCamera()
        {
            ClearSelectionState();

            LogicalCameraOption cameraOption = cameraComboBox.SelectedItem as LogicalCameraOption;
            if (cameraOption == null || selectedCar == null || setting == null)
            {
                UpdateButtons();
                return;
            }

            camKey = cameraOption.Key;
            camParam = null;
            if (cameraParams.Param.TryGetValue(camParamName, out Dictionary<string, CamParam> cameraItems)
                && cameraItems != null)
            {
                cameraItems.TryGetValue(camKey, out camParam);
            }

            if (camParam == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockPhysicalCameraMissing, LogType.warn);
                UpdateButtons();
                return;
            }

            if (setting.CutSets == null || setting.CutSets.Count == 0)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockImageSetMissing, LogType.warn);
                UpdateButtons();
                return;
            }

            int cameraIndex = GetCameraIndex(camKey);
            cutSet = setting.CutSets[0];
            if (cutSet.imageSet == null || cameraIndex < 0 || cameraIndex >= cutSet.imageSet.Count
                || cutSet.imageSet[cameraIndex] == null || cutSet.imageSet[cameraIndex].Count == 0)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockImageSetMissing, LogType.warn);
                UpdateButtons();
                return;
            }

            imageSet = cutSet.imageSet[cameraIndex][0];
            if (imageSet == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockImageSetMissing, LogType.warn);
                UpdateButtons();
                return;
            }

            suppressUiEvents = true;
            try
            {
                thresholdTextBox.Text = imageSet.minThreshold.ToString("0.###");
                widthRangeTextBox.Text = FormatRange(imageSet.widthMin, imageSet.widthMax, "mm");
                heightRangeTextBox.Text = FormatRange(imageSet.heightMin, imageSet.heightMax, "mm");
                areaRangeTextBox.Text = FormatRange(imageSet.areaMin, imageSet.areaMax, "mm²");
                physicalCameraTextBlock.Text = string.IsNullOrWhiteSpace(camParam.CamName)
                    ? "--"
                    : camParam.CamName;
                resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady;
                resultStatusTextBlock.Foreground = Brushes.LightGray;
            }
            finally
            {
                suppressUiEvents = false;
            }

            originalThreshold = imageSet.minThreshold;
            isAlter = false;
            previousCameraIndex = cameraComboBox.SelectedIndex;
            UpdateButtons();
        }

        private int GetCameraIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("Cam", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            if (int.TryParse(key.Substring(3), out int index))
            {
                return index - 1;
            }

            return -1;
        }

        private void thresholdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressUiEvents || imageSet == null)
            {
                return;
            }

            if (double.TryParse(thresholdTextBox.Text, out double threshold)
                && threshold >= 0)
            {
                if (Math.Abs(imageSet.minThreshold - threshold) > 1e-12)
                {
                    imageSet.minThreshold = threshold;
                    isAlter = true;
                }
            }

            UpdateButtons();
        }

        private void openCamButton_Click(object sender, RoutedEventArgs e)
        {
            if (camParam == null || imageSet == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady, LogType.warn);
                return;
            }

            if (cam.IsOpen)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(camParam.CamName))
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockPhysicalCameraMissing, LogType.warn);
                return;
            }

            if (!cam.OpenBySN(camParam.CamName))
            {
                ShowMessage(cam.ErrMsg, LogType.ng);
                return;
            }

            CamParam cameraInitParam = camParam.Clone();
            cameraInitParam.Key = camKey;
            if (!cam.InitSet(cameraInitParam, true))
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.CameraInitializationFailed + ":" + cam.ErrMsg, LogType.ng);
                cam.Close();
                UpdateButtons();
                return;
            }

            ShowMessage(cam.Name + "(" + cam.SN + ")" + global::_3DLaserGlueInspection.Resources.LanguageDict.CameraSuccessfullyOpened, LogType.ok);
            UpdateButtons();
        }

        private void closeCamButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCamera();
            DisposeCurrentData();
            ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.CameraClose, LogType.normal);
            UpdateButtons();
        }

        private void singleCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (!cam.IsOpen)
            {
                return;
            }

            DisposeDetectionData();
            currentImage?.Dispose();
            currentImage = null;

            cam.SetLine1Inverter(true);
            if (cam.OneShot(out currentImage))
            {
                cam.SetLine1Inverter(false);

                ClearImageDisplay();
                resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockAcquisitionCompleted;
                resultStatusTextBlock.Foreground = Brushes.LightGray;
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockAcquisitionCompleted, LogType.ok);

                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(currentImage));

            }
            else
            {
                currentImage?.Dispose();
                currentImage = null;
                ShowMessage(cam.ErrMsg, LogType.ng);
            }

            UpdateButtons();
        }

        private void detectButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentImage == null || currentImage.Empty() || imageSet == null || cutSet == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady, LogType.warn);
                return;
            }

            if (!TryGetCalibration(out CameraParameters hCamPar, out PoseParameters lightInCam))
            {
                return;
            }

            DisposeDetectionData();

            ImageSet detectionImageSet = imageSet.Clone();
            CutSet detectionCutSet = cutSet.Clone();
            if (detectionCutSet.scaleSize <= 0)
            {
                detectionCutSet.scaleSize = 5;
            }
            if (detectionCutSet.ShowWidth <= 0)
            {
                detectionCutSet.ShowWidth = 50;
            }
            if (detectionCutSet.ShowHeight <= 0)
            {
                detectionCutSet.ShowHeight = 50;
            }

            Mat outline = null;
            Mat lightXY = null;
            Mat scaledPoints = null;
            try
            {
                int leftX = 0;
                int topY = 0;
                Mat imageForOutline = currentImage;
                if (detectionImageSet.启用裁剪)
                {
                    leftX = (int)(currentImage.Cols * detectionImageSet.LeftX);
                    topY = (int)(currentImage.Rows * detectionImageSet.TopY);
                    int cropWidth = (int)((detectionImageSet.RightX - detectionImageSet.LeftX) * currentImage.Cols);
                    int cropHeight = (int)((detectionImageSet.DownY - detectionImageSet.TopY) * currentImage.Rows);
                    if (leftX < 0 || topY < 0 || cropWidth <= 0 || cropHeight <= 0
                        || leftX + cropWidth > currentImage.Cols || topY + cropHeight > currentImage.Rows)
                    {
                        ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockInvalidCrop, LogType.ng);
                        return;
                    }

                    imageForOutline = new Mat(currentImage,
                        new OpenCvSharp.Rect(leftX, topY, cropWidth, cropHeight));
                }

                try
                {
                    Vision.getLaserPosition(imageForOutline, detectionImageSet.minThreshold,
                        detectionImageSet.laserMinWidth, out outline,
                        camParam.OffsetX + leftX, camParam.OffsetY + topY);
                }
                finally
                {
                    if (!ReferenceEquals(imageForOutline, currentImage))
                    {
                        imageForOutline.Dispose();
                    }
                }

                if (outline == null || outline.Rows <= 0)
                {
                    ClearImageDisplay();
                    resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline;
                    resultStatusTextBlock.Foreground = Brushes.Orange;
                    ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline, LogType.warn);
                    return;
                }

                Vision.GetXY(hCamPar, lightInCam, outline, out lightXY);
                if (lightXY == null || lightXY.Rows <= 0)
                {
                    ClearImageDisplay();
                    resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline;
                    resultStatusTextBlock.Foreground = Brushes.Orange;
                    ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline, LogType.warn);
                    return;
                }

                Vision.scalePoint(lightXY, detectionCutSet, 90 - lightInCam.rx, out scaledPoints);
                ApplyCorrectionScale(scaledPoints, detectionImageSet);

                contourPoints = scaledPoints;
                scaledPoints = null;

                if (detectionImageSet.离散去噪)
                {
                    Vision.TrajectoryDiscreteFilter(contourPoints, out contourPointsForDetection,
                        detectionImageSet.分段距离 * detectionCutSet.scaleSize,
                        detectionImageSet.成段点数);
                }
                else
                {
                    contourPointsForDetection = contourPoints.Clone();
                }

                if (contourPointsForDetection == null || contourPointsForDetection.Rows <= 0)
                {
                    ClearImageDisplay();
                    resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline;
                    resultStatusTextBlock.Foreground = Brushes.Orange;
                    ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockNoOutline, LogType.warn);
                    return;
                }

                bool existGlue = false;
                outMaxRegion = new Mat();
                outRegionRectangle2 = new Mat();
                Vision.singleFrameDetAndResult(contourPointsForDetection, detectionImageSet,
                    detectionCutSet, ref existGlue, ref resultData, ref bResult,
                    ref outMaxRegion, ref outRegionRectangle2, contourPoints);

                ClearImageDisplay();
                GlobalVarAndFunc.ShowImageData(detectionCutSet.ShowWidth, detectionCutSet.ShowHeight,
                    detectionCutSet, detectionImageSet, contourPointsForDetection,
                    outMaxRegion, outRegionRectangle2, resultData, bResult,
                    ref hWindowModel, ref showing, ref olockShow, 0, 0);

                UpdateResultStatus();
                ShowMessage(BuildResultLog(), bResult.Result ? LogType.ok : LogType.ng);
            }
            catch (Exception ex)
            {
                ClearImageDisplay();
                resultStatusTextBlock.Text = ex.Message;
                resultStatusTextBlock.Foreground = Brushes.Red;
                ShowMessage(ex.ToString(), LogType.ng);
            }
            finally
            {
                outline?.Dispose();
                lightXY?.Dispose();
                scaledPoints?.Dispose();
            }

            UpdateButtons();
        }

        private bool TryGetCalibration(out CameraParameters hCamPar, out PoseParameters lightInCam)
        {
            hCamPar = null;
            lightInCam = null;

            if (string.IsNullOrWhiteSpace(camParamName) || string.IsNullOrWhiteSpace(camKey)
                || !cameraParams.CamPar.TryGetValue(camParamName, out Dictionary<string, CameraParameters> cameraParameters)
                || cameraParameters == null
                || !cameraParameters.TryGetValue(camKey, out hCamPar)
                || hCamPar == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockCalibrationMissing, LogType.ng);
                return false;
            }

            if (!cameraParams.LightInCam.TryGetValue(camParamName, out Dictionary<string, PoseParameters> lightParameters)
                || lightParameters == null
                || !lightParameters.TryGetValue(camKey, out lightInCam)
                || lightInCam == null)
            {
                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockCalibrationMissing, LogType.ng);
                return false;
            }

            return true;
        }

        private void ApplyCorrectionScale(Mat points, ImageSet detectionImageSet)
        {
            if (points == null)
            {
                return;
            }

            for (int i = 0; i < points.Rows; i++)
            {
                points.At<double>(i, 0) *= detectionImageSet.correctionScaleSizeX;
                points.At<double>(i, 1) *= detectionImageSet.correctionScaleSizeY;
            }
        }

        private void UpdateResultStatus()
        {
            string resultText = global::_3DLaserGlueInspection.Resources.LanguageDict.DetResult + ": "
                + (bResult.Result ? global::_3DLaserGlueInspection.Resources.LanguageDict.OK : global::_3DLaserGlueInspection.Resources.LanguageDict.NG)
                + " | " + global::_3DLaserGlueInspection.Resources.LanguageDict.GlueWidth + ":"
                + resultData.glueWidth.ToString("0.00") + "mm"
                + " | " + global::_3DLaserGlueInspection.Resources.LanguageDict.GlueHeight + ":"
                + resultData.glueHeight.ToString("0.00") + "mm"
                + " | " + global::_3DLaserGlueInspection.Resources.LanguageDict.Area + ":"
                + resultData.glueArea.ToString("0.00") + "mm²";
            resultStatusTextBlock.Text = resultText;
            resultStatusTextBlock.Foreground = bResult.Result ? Brushes.LightGreen : Brushes.OrangeRed;
        }

        private string BuildResultLog()
        {
            return global::_3DLaserGlueInspection.Resources.LanguageDict.DetResult + ":"
                + (bResult.Result ? global::_3DLaserGlueInspection.Resources.LanguageDict.OK : global::_3DLaserGlueInspection.Resources.LanguageDict.NG)
                + " " + global::_3DLaserGlueInspection.Resources.LanguageDict.GlueWidth + "=" + resultData.glueWidth.ToString("0.00") + "mm, "
                + global::_3DLaserGlueInspection.Resources.LanguageDict.GlueHeight + "=" + resultData.glueHeight.ToString("0.00") + "mm, "
                + global::_3DLaserGlueInspection.Resources.LanguageDict.Area + "=" + resultData.glueArea.ToString("0.00") + "mm²";
        }

        private string FormatRange(double min, double max, string unit)
        {
            return min.ToString("0.###") + "~" + max.ToString("0.###") + unit;
        }

        private bool ConfirmPendingChanges()
        {
            if (!isAlter || imageSet == null || setting == null)
            {
                return true;
            }

            System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(
                global::_3DLaserGlueInspection.Resources.LanguageDict.DoYouWantToSaveTheParameters,
                global::_3DLaserGlueInspection.Resources.LanguageDict.Prompt,
                System.Windows.Forms.MessageBoxButtons.YesNoCancel,
                System.Windows.Forms.MessageBoxIcon.Question);

            if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
            {
                return false;
            }

            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
            {
                if (!setting.Save())
                {
                    ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.ParameterSaveFailed + ":" + setting.ErrMsg, LogType.ng);
                    return false;
                }

                ShowMessage(global::_3DLaserGlueInspection.Resources.LanguageDict.ParameterSavedSuccessfully, LogType.ok);
            }
            else
            {
                imageSet.minThreshold = originalThreshold;
                suppressUiEvents = true;
                try
                {
                    thresholdTextBox.Text = originalThreshold.ToString("0.###");
                }
                finally
                {
                    suppressUiEvents = false;
                }
            }

            isAlter = false;
            return true;
        }

        private void WindowStandardBlockTest_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ConfirmPendingChanges())
            {
                e.Cancel = true;
                return;
            }

            CloseCamera();
            DisposeCurrentData();
            DisposeSetting();
        }

        private void RestoreCarSelection()
        {
            suppressUiEvents = true;
            try
            {
                carNameComboBox.SelectedIndex = previousCarIndex;
            }
            finally
            {
                suppressUiEvents = false;
            }
        }

        private void RestoreCameraSelection()
        {
            suppressUiEvents = true;
            try
            {
                cameraComboBox.SelectedIndex = previousCameraIndex;
            }
            finally
            {
                suppressUiEvents = false;
            }
        }

        private void ClearSelectionState()
        {
            camParam = null;
            camKey = null;
            cutSet = null;
            imageSet = null;
            suppressUiEvents = true;
            try
            {
                thresholdTextBox.Text = string.Empty;
                widthRangeTextBox.Text = string.Empty;
                heightRangeTextBox.Text = string.Empty;
                areaRangeTextBox.Text = string.Empty;
                physicalCameraTextBlock.Text = string.Empty;
                resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady;
                resultStatusTextBlock.Foreground = Brushes.LightGray;
            }
            finally
            {
                suppressUiEvents = false;
            }
            isAlter = false;
        }

        private void CloseCamera()
        {
            if (cam.IsOpen)
            {
                cam.Close();
            }

            UpdateButtons();
        }

        private void DisposeSetting()
        {
            if (setting != null)
            {
                setting.Dispose();
                setting = null;
            }
        }

        private void DisposeCurrentData()
        {
            currentImage?.Dispose();
            currentImage = null;
            DisposeDetectionData();
            ClearImageDisplay();
            resultData = new Data();
            bResult = new BResult();
            resultStatusTextBlock.Text = global::_3DLaserGlueInspection.Resources.LanguageDict.StandardBlockReady;
            resultStatusTextBlock.Foreground = Brushes.LightGray;
        }

        private void DisposeDetectionData()
        {
            DisposeMat(ref contourPoints);
            DisposeMat(ref contourPointsForDetection);
            DisposeMat(ref outMaxRegion);
            DisposeMat(ref outRegionRectangle2);
        }

        private void ClearImageDisplay()
        {
            hWindowModel.SetImageSource(null);
            hWindowModel.ClearChildren();
        }

        private void DisposeMat(ref Mat mat)
        {
            if (mat != null)
            {
                mat.Dispose();
                mat = null;
            }
        }

        private void UpdateButtons()
        {
            bool hasSelection = imageSet != null && camParam != null;
            bool isOpen = cam.IsOpen;

            carNameComboBox.IsEnabled = !isOpen;
            cameraComboBox.IsEnabled = !isOpen;
            openCamButton.IsEnabled = hasSelection && !isOpen;
            closeCamButton.IsEnabled = isOpen;
            singleCheckButton.IsEnabled = isOpen;
            detectButton.IsEnabled = isOpen && currentImage != null && !currentImage.Empty();
        }
    }
}
