using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using System.Windows.Shapes;
using System.Xml.Serialization;
using OpenCvSharp;
using Wpf_Replace_halcon;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using _3DLaserGlueInspection;
using System.Data.Common;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Windows.Media.Media3D;
using System.Reflection;
namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// WindowVision.xaml 的交互逻辑
    /// </summary>
    public partial class WindowVision : System.Windows.Window
    {

        Vision vision = new Vision();
        CarNameIdSet car = new CarNameIdSet();
        CamParams Params = new CamParams();
        Setting set;
        bool isAlter = false;

        string CamParamName;
        CutSet cutSet = null;

        Mat hImage = null;
        ImageSet imageSet = null;
        string camKey = null;

        object olockShow = new object();
        bool showing = false;

        SynchronizedList<long> robotPoseKeys = new SynchronizedList<long>();

        Mat robotPoseMat = new Mat();
        double robotAndCamAngle = 0;
        Wpf_Replace_halcon.PoseParameters currentRobotPose = new PoseParameters();  //当前图片的机器人位姿
        Wpf_Replace_halcon.PoseParameters lastRobotPose = new PoseParameters();     //上一张图片的机器人位姿

        SynchronizedList<PoseParameters> robotPoseValues = new SynchronizedList<PoseParameters>();
        SynchronizedList<Dictionary<string, SynchronizedList<long>>> ImageKeys = new SynchronizedList<Dictionary<string, SynchronizedList<long>>>();//指示拍照位置
        SynchronizedList<Dictionary<string, Dictionary<long, Mat>>> Images = new SynchronizedList<Dictionary<string, Dictionary<long, Mat>>>();//分段-相机-时间-图片

        Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>> Robot3DPoseDict = new Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>>();//相机-分段-时间-机器位姿
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DXsDict = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();//相机-分段-时间-图片数据
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DYsDict = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DZsDict = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();


        Mat hXLDCont10mm = new Mat();

        Data resultData = new Data();
        BResult bResult = new BResult();
        Mat outMaxRegion = new Mat();
        Mat outRegionRectangle2 = new Mat();
        Mat hXLDCont10mm3D = new Mat();


        List<double[]> pointsSave = new List<double[]>();

        public WindowVision()
        {
            InitializeComponent();

            addButton.IsEnabled = deleteButton.IsEnabled = false;
            camUsedGroupBox.IsEnabled = publicParaGridBox.IsEnabled = false;
            imageSetGrid.IsEnabled = false;

            outlineCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            outlineCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyOutline);
            };

            threNumericUpDown.ContextMenu = new System.Windows.Controls.ContextMenu();
            threNumericUpDown.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyThre);
            };

            laserMinWidthNumericUpDown.ContextMenu = new System.Windows.Controls.ContextMenu();
            laserMinWidthNumericUpDown.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyThre);
            };


            singleFrameCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            singleFrameCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopySingleFrame);
            };
            _3DCloudDetCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            _3DCloudDetCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, Copy3DGlueDet);
            };

            toleranceRangeGrid.ContextMenu = new System.Windows.Controls.ContextMenu();
            toleranceRangeGrid.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyToleranceRange);
            };
            useCroppintCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            useCroppintCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyUseCrop);
            };
            cropGrid.ContextMenu = new System.Windows.Controls.ContextMenu();
            cropGrid.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyCropRange);
            };
            useDiscreteDenoisingCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            useDiscreteDenoisingCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyDiscreteDenoising);
            };
            discreteDenoisingGrid.ContextMenu = new System.Windows.Controls.ContextMenu();
            discreteDenoisingGrid.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyDenoisingPara);
            };

            isUseAngleOptCheck.ContextMenu = new System.Windows.Controls.ContextMenu();
            isUseAngleOptCheck.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyAngle);
            };

            correctionGrid.ContextMenu = new System.Windows.Controls.ContextMenu();
            correctionGrid.ContextMenu.Opened += (s, e) =>
            {
                CreateRightClickMenu((System.Windows.Controls.ContextMenu)s, CopyCorrectionScaleSize);
            };
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
        //                mat = Mat.Zeros((int)(showHeight * Vision.scaleSize), (int)(showWidth  *Vision.scaleSize), MatType.CV_8UC3);

        //                //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
        //                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));

        //                //Console.WriteLine($"mat.Size:{mat.Size()}");

        //                //Console.WriteLine($"Polyline :");

        //                PointCollection points = new PointCollection();
        //                for (int i = 0; i < hXLDCont10mm.Rows; i++)
        //                {
        //                    System.Windows.Point point = new System.Windows.Point();
        //                    point.X = hXLDCont10mm.At<double>(i, 0);
        //                    point.Y = hXLDCont10mm.At<double>(i, 1);
        //                    points.Add(point);

        //                    //Console.WriteLine($"point:{point}");

        //                }
        //                //DispPolylinejHWindowControlEvent(points, Colors.Gray);
        //                hWindowModel.AddPolyline(points, Colors.Gray);

        //                if (!hRegion.Empty())
        //                {
        //                    //Console.WriteLine($"text value :");
        //                    string text = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + $"{data.胶高:0.00}\r\n"
        //                       + _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth+":" + $"{data.胶宽:0.00}\r\n"
        //                       + _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + $"{data.面积:0.00}";

        //                    //Console.WriteLine($"point :({data.column},{data.row})");
        //                    //DispTextInImageHWindowControlEvent(text, Colors.Black, (int)data.column, (int)data.row);
        //                    hWindowModel.AddTextBlock(text, Colors.White, (int)data.column+(int)(data.胶宽/2 * Vision.scaleSize), 
        //                        (int)data.row + (int)(data.胶高/2 * Vision.scaleSize));

        //                    //Console.WriteLine($"text result :");
        //                    //hWindowControl.DispTextInImage(text, data.row, data.column);
        //                    string textWindow1 = _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth+":" + (bResult.胶宽 ? "OK" : "NG");
        //                    string textWindow2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + (bResult.胶高 ? "OK" : "NG");
        //                    string textWindow3 = _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + (bResult.面积 ? "OK" : "NG");
        //                    string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
        //                    //Console.WriteLine($"point :({10},{10})");
        //                    //DispTextInImageHWindowControlEvent(textWindow, Colors.Black, 10, 10);
        //                    hWindowModel.AddTextBlock(textWindow, Colors.White, 10, 10);


        //                    //Console.WriteLine($"region :");

        //                    PointCollection regionPoints = new PointCollection();
        //                    for (int i = 0; i < hRegion.Rows; i++)
        //                    {
        //                        System.Windows.Point point = new System.Windows.Point();
        //                        point.X = hRegion.At<double>(i, 0);
        //                        point.Y = hRegion.At<double>(i, 1);
        //                        regionPoints.Add(point);
        //                        //Console.WriteLine($"point:{point}");
        //                    }

        //                    hWindowModel.AddPolygon(regionPoints, Colors.Red, "fill");

        //                    //DispPolygonjHWindowControlEvent(regionPoints, Colors.Red, "fill");

        //                    //Console.WriteLine($"regionSmallestRectangle :");
        //                    PointCollection regionSmallestRectangle2Points = new PointCollection();
        //                    for (int i = 0; i < hRegionSmallestRectangle2.Rows; i++)
        //                    {
        //                        System.Windows.Point point = new System.Windows.Point();
        //                        point.X = hRegionSmallestRectangle2.At<double>(i, 0);
        //                        point.Y = hRegionSmallestRectangle2.At<double>(i, 1);
        //                        regionSmallestRectangle2Points.Add(point);
        //                        //Console.WriteLine($"point:{point}");
        //                    }

        //                    //DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");
        //                    hWindowModel.AddPolygon(regionSmallestRectangle2Points, Colors.Blue, "margin");
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

        //private void AddCrossContour(int size, double rows, double cols, double angles, System.Windows.Media.Color color)
        //{
        //    PointCollection Points1 = new PointCollection();
        //    PointCollection Points2 = new PointCollection();

        //    System.Windows.Point p1 = new System.Windows.Point(cols + Math.Cos(angles / 180 * Math.PI) * size, rows + Math.Sin(angles / 180 * Math.PI) * size);
        //    System.Windows.Point p2 = new System.Windows.Point(cols + Math.Cos((angles + 180) / 180 * Math.PI) * size, rows + Math.Sin((angles + 180) / 180 * Math.PI) * size);
        //    System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
        //    System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
        //    Points1.Add(p1);
        //    Points1.Add(p2);
        //    Points2.Add(p3);
        //    Points2.Add(p4);

        //    hWindowModel.AddPolyline(Points1, color);
        //    hWindowModel.AddPolyline(Points2, color);
        //}

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //判断输入是否正负号，小数点，或数字。
            e.Handled = new Regex(@"[^0-9+\-.]+").IsMatch(e.Text);

        }
        private void finishCreatePolylineEven(PointCollection drawPoints)
        {
            var XLDData = set.XLDDatas[cutSetListBox.SelectedIndex];
            double[] controlRows = new double[drawPoints.Count];
            double[] controlCols = new double[drawPoints.Count];
            double[] knots = new double[drawPoints.Count];
            double[] rows = new double[drawPoints.Count];
            double[] cols = new double[drawPoints.Count];
            double[] tangents = new double[drawPoints.Count];
            isAlter = true;
            for (int i = 0; i < drawPoints.Count; i++)
            {
                controlRows[i] = drawPoints[i].Y;
                controlCols[i] = drawPoints[i].X;
            }
            if (drawPoints.Count > 0)
            {
                XLDData.ControlRows = controlRows;
                XLDData.ControlCols = controlCols;
                XLDData.Knots = knots;
                XLDData.Rows = rows;
                XLDData.Cols = cols;
                XLDData.Tangents = tangents;
                isAlter = true;
            }
            hWindowModel.finishCreatePolylineEventHandler -= finishCreatePolylineEven;

        }
        private void CreateRightClickMenu(System.Windows.Controls.ContextMenu contextMenuStrip, Action<ImageSet, ImageSet> action)
        {
            if (contextMenuStrip.Items.Count > 0)
            {
                contextMenuStrip.Items.Clear();
            }
            if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0)
            {
                var imageSets = set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex];
                var dstImageSet = set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex][selectPictureListBox.SelectedIndex];
                //复制至全部图片
                System.Windows.Controls.MenuItem toolAll = new System.Windows.Controls.MenuItem();
                toolAll.Header =  _3DLaserGlueInspection.Resources.LanguageDict.CopyToAllImages;
                toolAll.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                toolAll.Click += (s0, e0) =>
                {
                    foreach (var imageSet in imageSets)
                    {
                        action.Invoke(imageSet, dstImageSet);
                    }
                };
                contextMenuStrip.Items.Add(toolAll);
                //复制至范围图片
                System.Windows.Controls.MenuItem toolRange = new System.Windows.Controls.MenuItem();
                toolRange.Header = _3DLaserGlueInspection.Resources.LanguageDict.CopyToRangeImage;
                toolRange.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                toolRange.Click += (s, e) =>
                {
                    WindowCopy formCopy = new WindowCopy(selectPictureListBox.SelectedIndex, imageSets.Count - 1);
                    if ((bool)formCopy.ShowDialog())
                    {
                        int startID, endID;
                        startID = Math.Max(0, formCopy.startID);
                        startID = Math.Min(startID, imageSets.Count - 1);
                        endID = Math.Max(0, formCopy.endID);
                        endID = Math.Min(endID, imageSets.Count - 1);

                        for (int k = startID; k <= endID; k++)
                        {
                            action.Invoke(imageSets[k], dstImageSet);
                        }
                    }
                };
                contextMenuStrip.Items.Add(toolRange);
            }
        }

        private void CopyOutline(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.轮廓检测 = dstImageSet.轮廓检测;
            isAlter = true;
        }

        private void CopyCorrectionScaleSize(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.correctionScaleSizeX = dstImageSet.correctionScaleSizeX;
            srcImageSet.correctionScaleSizeY = dstImageSet.correctionScaleSizeY;

            isAlter = true;

        }
        private void CopyAngle(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.isUseAngleOpt = dstImageSet.isUseAngleOpt;
            isAlter = true;
        }

        private void CopyThre(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.minThreshold = dstImageSet.minThreshold;
            srcImageSet.laserMinWidth = dstImageSet.laserMinWidth;
            isAlter = true;
        }


        private void CopySingleFrame(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.单帧检测 = dstImageSet.单帧检测;

            isAlter = true;
        }

        private void Copy3DGlueDet(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet._3DGlueDet = dstImageSet._3DGlueDet;

            isAlter = true;
        }
        private void CopyToleranceRange(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.widthMin = dstImageSet.widthMin;
            srcImageSet.widthMax = dstImageSet.widthMax;
            srcImageSet.heightMin = dstImageSet.heightMin;
            srcImageSet.heightMax = dstImageSet.heightMax;
            srcImageSet.areaMin = dstImageSet.areaMin;
            srcImageSet.areaMax = dstImageSet.areaMax;
            isAlter = true;
        }
        private void CopyUseCrop(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.启用裁剪 = dstImageSet.启用裁剪;

            isAlter = true;
        }
        private void CopyCropRange(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.LeftX = dstImageSet.LeftX;
            srcImageSet.RightX = dstImageSet.RightX;
            srcImageSet.TopY = dstImageSet.TopY;
            srcImageSet.DownY = dstImageSet.DownY;
            isAlter = true;
        }
        private void CopyDiscreteDenoising(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.离散去噪 = dstImageSet.离散去噪;
            isAlter = true;
        }
        private void CopyDenoisingPara(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.分段距离 = dstImageSet.分段距离;
            srcImageSet.成段点数 = dstImageSet.成段点数;
            isAlter = true;
        }

        private void WindowVision_Loaded(object sender, RoutedEventArgs e)
        {
            // 翻译 ，未启用
            //GeneralFunc.ChangeLanguateFun(typeof(FormVision), this);
            string[] carNames = car.LoadName();

            for (int id = 0; id < carNames.Length; id++)
            {
                carTypeComboBox.Items.Add(carNames[id]);
            }
            Params.Load();

            showImageComboBox.SelectedIndex = 0;

            rubbish.Visibility = Visibility.Hidden;

        }


        private void WindowVision_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isAlter && set != null)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DoYouWantToSaveIt + " " + set.Name + " " + _3DLaserGlueInspection.Resources.LanguageDict.Para, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!set.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SaveFailed + set.ErrMsg, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
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
        }

        private void CopyToAllPicture(object sender, RoutedEventArgs e)
        {

            //if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0)
            //{
            //    var imageSets = set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex];
            //    //复制至全部图片
            //    ToolStripMenuItem toolAll = new ToolStripMenuItem();
            //    toolAll.Text = _3DLaserGlueInspection.Resources.LanguageDict.复制至全部图片");
            //    toolAll.Click += (s0, e0) =>
            //    {
            //        foreach (var imageSet in imageSets)
            //        {
            //            action.Invoke(imageSet);
            //        }
            //        contextMenuStrip.
            //    };
            //    contextMenuStrip.Items.Add(toolAll);
            //    //复制至范围图片
            //    ToolStripMenuItem toolRange = new ToolStripMenuItem();
            //    toolRange.Text = _3DLaserGlueInspection.Resources.LanguageDict.复制至范围图片");
            //    toolRange.Click += (s, e) =>
            //    {
            //        //FormCopy formCopy = new FormCopy(selectPictureListBox.SelectedIndex, imageSets.Count - 1);
            //        //if (formCopy.ShowDialog())
            //        //{
            //        //    for (int k = formCopy.起点; k <= formCopy.终点; k++)
            //        //    {
            //        //        action.Invoke(imageSets[k]);
            //        //    }
            //        //}
            //    };
            //    contextMenuStrip.Items.Add(toolRange);
            //}
        }
        void LoadUpDataSet()
        {
            saveNGImageCheck.Checked += UpDataSet;
            saveOKImageCheck.Checked += UpDataSet;
            saveNGImageCheck.Unchecked += UpDataSet;
            saveOKImageCheck.Unchecked += UpDataSet;

            saveImageDirTextBox.TextChanged += UpDataSet;
        }
        void UnLoadUpDataSet()
        {
            saveNGImageCheck.Checked -= UpDataSet;
            saveOKImageCheck.Checked -= UpDataSet;
            saveNGImageCheck.Unchecked -= UpDataSet;
            saveOKImageCheck.Unchecked -= UpDataSet;

            saveImageDirTextBox.TextChanged -= UpDataSet;
        }
        void UpDataSet(object sender, EventArgs e)
        {
            if (set != null)
            {
                isAlter = true;
                set.OtherSet.SaveNGImage = (bool)saveNGImageCheck.IsChecked;
                set.OtherSet.SaveOKImage = (bool)saveOKImageCheck.IsChecked;
                set.OtherSet.SaveImagePath = saveImageDirTextBox.Text;
            }
        }

        void LoadUpData()
        {
            enableCam1Check.Checked += UpData;
            enableCam2Check.Checked += UpData;
            enableCam3Check.Checked += UpData;
            enableCam4Check.Checked += UpData;
            enableCam1Check.Unchecked += UpData;
            enableCam2Check.Unchecked += UpData;
            enableCam3Check.Unchecked += UpData;
            enableCam4Check.Unchecked += UpData;
            imageCountNumericUpDown.TextChanged += numericUpDownImageNumUpData;
            showWidthNumericUpDown.TextChanged += UpData;
            showHeightNumericUpDown.TextChanged += UpData;
            colorLimitMaxNumericUpDown.TextChanged += UpData;
            colorLimitMinNumericUpDown.TextChanged += UpData;
            identificationSizeNumericUpDown.TextChanged += UpData;
            startImageIndexNumericUpDown.TextChanged += UpData;
            endImageIndexNumericUpDown.TextChanged += UpData;

            outlineCheck.Checked += UpData;
            outlineCheck.Unchecked += UpData;

            threNumericUpDown.TextChanged += UpData;
            laserMinWidthNumericUpDown.TextChanged += UpData;

            singleFrameCheck.Checked += UpData;
            singleFrameCheck.Unchecked += UpData;
            _3DCloudDetCheck.Checked += UpData;
            _3DCloudDetCheck.Unchecked += UpData;

            glueWidthMinNumericUpDown.TextChanged += UpData;
            glueWidthMaxNumericUpDown.TextChanged += UpData;
            glueHeightMinNumericUpDown.TextChanged += UpData;
            glueHeightMaxNumericUpDown.TextChanged += UpData;
            glueAreaMinNumericUpDown.TextChanged += UpData;
            glueAreaMaxNumericUpDown.TextChanged += UpData;
            useCroppintCheck.Checked += UpData;
            useCroppintCheck.Unchecked += UpData;

            leftRangeMinNumericUpDown.TextChanged += UpData;
            leftRangeMaxNumericUpDown.TextChanged += UpData;
            topRangeMinNumericUpDown.TextChanged += UpData;
            topRangeMaxNumericUpDown.TextChanged += UpData;

            //correctionScaleSizeXNumericUpDown.TextChanged += UpData;
            //correctionScaleSizeYNumericUpDown.TextChanged += UpData;


            useDiscreteDenoisingCheck.Checked += UpData;
            useDiscreteDenoisingCheck.Unchecked += UpData;

            discreteDenoisingDistNumericUpDown.TextChanged += UpData;
            discreteDenoisingCountNumericUpDown.TextChanged += UpData;


            scaleSizeNumericUpDown.TextChanged += UpData;
            isUseAngleOptCheck.Checked += UpData;
            isUseAngleOptCheck.Unchecked += UpData;

            coefficientSharingCheck.Checked += UpData;
            coefficientSharingCheck.Unchecked += UpData;


        }
        void UnLoadUpData()
        {
            enableCam1Check.Checked -= UpData;
            enableCam2Check.Checked -= UpData;
            enableCam3Check.Checked -= UpData;
            enableCam4Check.Checked -= UpData;
            enableCam1Check.Unchecked -= UpData;
            enableCam2Check.Unchecked -= UpData;
            enableCam3Check.Unchecked -= UpData;
            enableCam4Check.Unchecked -= UpData;
            imageCountNumericUpDown.TextChanged -= numericUpDownImageNumUpData;
            showWidthNumericUpDown.TextChanged -= UpData;
            showHeightNumericUpDown.TextChanged -= UpData;
            colorLimitMaxNumericUpDown.TextChanged -= UpData;
            colorLimitMinNumericUpDown.TextChanged -= UpData;
            identificationSizeNumericUpDown.TextChanged -= UpData;
            startImageIndexNumericUpDown.TextChanged -= UpData;
            endImageIndexNumericUpDown.TextChanged -= UpData;

            outlineCheck.Checked -= UpData;
            outlineCheck.Unchecked -= UpData;

            threNumericUpDown.TextChanged -= UpData;
            laserMinWidthNumericUpDown.TextChanged -= UpData;

            singleFrameCheck.Checked -= UpData;
            singleFrameCheck.Unchecked -= UpData;
            _3DCloudDetCheck.Checked -= UpData;
            _3DCloudDetCheck.Unchecked -= UpData;

            glueWidthMinNumericUpDown.TextChanged -= UpData;
            glueWidthMaxNumericUpDown.TextChanged -= UpData;
            glueHeightMinNumericUpDown.TextChanged -= UpData;
            glueHeightMaxNumericUpDown.TextChanged -= UpData;
            glueAreaMinNumericUpDown.TextChanged -= UpData;
            glueAreaMaxNumericUpDown.TextChanged -= UpData;
            useCroppintCheck.Checked -= UpData;
            useCroppintCheck.Unchecked -= UpData;

            leftRangeMinNumericUpDown.TextChanged -= UpData;
            leftRangeMaxNumericUpDown.TextChanged -= UpData;
            topRangeMinNumericUpDown.TextChanged -= UpData;
            topRangeMaxNumericUpDown.TextChanged -= UpData;

            //correctionScaleSizeXNumericUpDown.TextChanged -= UpData;
            //correctionScaleSizeYNumericUpDown.TextChanged -= UpData;

            useDiscreteDenoisingCheck.Checked -= UpData;
            useDiscreteDenoisingCheck.Unchecked -= UpData;

            discreteDenoisingDistNumericUpDown.TextChanged -= UpData;
            discreteDenoisingCountNumericUpDown.TextChanged -= UpData;

            scaleSizeNumericUpDown.TextChanged -= UpData;
            isUseAngleOptCheck.Checked -= UpData;
            isUseAngleOptCheck.Unchecked -= UpData;

            coefficientSharingCheck.Checked -= UpData;
            coefficientSharingCheck.Unchecked -= UpData;

        }
        void UpData(object sender, EventArgs e)
        {
            if (cutSetListBox.SelectedIndex >= 0)
            {
                var cutSet = set.CutSets[cutSetListBox.SelectedIndex];
                if (cutSet != null)
                {
                    isAlter = true;
                    cutSet.Cam1Enabled = (bool)enableCam1Check.IsChecked;
                    cutSet.Cam2Enabled = (bool)enableCam2Check.IsChecked;
                    cutSet.Cam3Enabled = (bool)enableCam3Check.IsChecked;
                    cutSet.Cam4Enabled = (bool)enableCam4Check.IsChecked;

                    //cutSet.isUseAngleOpt = (bool)isUseAngleOptCheck.IsChecked;

                    cutSet.isCoefficientSharing = (bool)coefficientSharingCheck.IsChecked;

                    try
                    {
                        cutSet.ShowWidth = Convert.ToInt32(showWidthNumericUpDown.Text);
                        cutSet.ShowHeight = Convert.ToInt32(showHeightNumericUpDown.Text);
                        cutSet.ShowColorMax = Convert.ToDouble(colorLimitMaxNumericUpDown.Text);
                        cutSet.ShowColorMin = Convert.ToDouble(colorLimitMinNumericUpDown.Text);
                        cutSet.Size = Convert.ToInt32(identificationSizeNumericUpDown.Text);
                        cutSet.StartImageIndex = Convert.ToInt32(startImageIndexNumericUpDown.Text);
                        cutSet.EndImageIndex = Convert.ToInt32(endImageIndexNumericUpDown.Text);

                        cutSet.scaleSize = Convert.ToInt32(scaleSizeNumericUpDown.Text);

                        
                    }
                    catch (Exception)
                    {

                    }


                    if (selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0)
                    {
                        var imageSet = cutSet.imageSet[selectCamListBox.SelectedIndex][selectPictureListBox.SelectedIndex];

                        Copy(imageSet);
                    }
                }
            }
        }
        private void Copy(ImageSet imageSet)
        {
            try
            {
                imageSet.轮廓检测 = (bool)outlineCheck.IsChecked;
                imageSet.minThreshold = Convert.ToDouble(threNumericUpDown.Text);
                imageSet.laserMinWidth = Convert.ToInt32(laserMinWidthNumericUpDown.Text);

                imageSet.单帧检测 = (bool)singleFrameCheck.IsChecked;
                imageSet._3DGlueDet = (bool)_3DCloudDetCheck.IsChecked;
                imageSet.widthMin = Convert.ToDouble(glueWidthMinNumericUpDown.Text);
                imageSet.widthMax = Convert.ToDouble(glueWidthMaxNumericUpDown.Text);
                imageSet.heightMin = Convert.ToDouble(glueHeightMinNumericUpDown.Text);
                imageSet.heightMax = Convert.ToDouble(glueHeightMaxNumericUpDown.Text);
                imageSet.areaMin = Convert.ToDouble(glueAreaMinNumericUpDown.Text);
                imageSet.areaMax = Convert.ToDouble(glueAreaMaxNumericUpDown.Text);
                imageSet.启用裁剪 = (bool)useCroppintCheck.IsChecked;
                imageSet.LeftX = Convert.ToDouble(leftRangeMinNumericUpDown.Text);
                imageSet.RightX = Convert.ToDouble(leftRangeMaxNumericUpDown.Text);
                imageSet.TopY = Convert.ToDouble(topRangeMinNumericUpDown.Text);
                imageSet.DownY = Convert.ToDouble(topRangeMaxNumericUpDown.Text);
                imageSet.离散去噪 = (bool)useDiscreteDenoisingCheck.IsChecked;
                imageSet.分段距离 = Convert.ToDouble(discreteDenoisingDistNumericUpDown.Text);
                imageSet.成段点数 = Convert.ToInt32(discreteDenoisingCountNumericUpDown.Text);

                imageSet.isUseAngleOpt = (bool)isUseAngleOptCheck.IsChecked;

                //imageSet.correctionScaleSizeX = Convert.ToDouble(correctionScaleSizeXNumericUpDown.Text);
                //imageSet.correctionScaleSizeY = Convert.ToDouble(correctionScaleSizeYNumericUpDown.Text);

            }
            catch (Exception ex)
            {
            }

        }

        void SelectedCamAndImage()
        {
            camKey = null;
            bool existImage = false;
            bool showPara = false;


            if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0 && robotPoseKeys.Count > 0)
            {
                
                //图片更新
                try
                {
                    camKey = $"Cam{selectCamListBox.SelectedIndex + 1}";
                    if (ImageKeys.Count > cutSetListBox.SelectedIndex && ImageKeys[cutSetListBox.SelectedIndex].ContainsKey(camKey) && ImageKeys[cutSetListBox.SelectedIndex][camKey].Count > selectPictureListBox.SelectedIndex)
                    {
                        long imageKey = ImageKeys[cutSetListBox.SelectedIndex][camKey][selectPictureListBox.SelectedIndex];


                        hImage = Images[cutSetListBox.SelectedIndex][camKey][imageKey];
                        if (hImage != null)
                        {
                            //hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(hImage).Clone());
                            //showImageComboBox.SelectedIndex = -1;
                            //showImageComboBox.SelectedIndex = 0;
                            existImage = true;
                        }

                        robotAndCamAngleNumericUpDown.Text = "NaN";
                        robotAndCamAngle = double.NaN;

                        //如果是第一张图片，不用进行检测
                        if (selectPictureListBox.SelectedIndex > 0)
                        {
                            //先获取上一张图片的机器人位姿
                            long lastImageKey = ImageKeys[cutSetListBox.SelectedIndex][camKey][selectPictureListBox.SelectedIndex - 1];

                            for (int i = 0; i < robotPoseKeys.Count; i++)
                            {
                                if (robotPoseKeys[i] > lastImageKey)
                                {
                                    if (i > 0)
                                    {
                                        HMatrixTransform.mathHPose(robotPoseValues[i - 1],
                                                            robotPoseValues[i], out lastRobotPose,
                                                            (lastImageKey - robotPoseKeys[i - 1]) /
                                                            (double)(robotPoseKeys[i] - robotPoseKeys[i - 1])
                                                            );
                                        break;
                                    }
                                    else
                                    {
                                        lastRobotPose = null;
                                    }
                                }
                            }


                            //获取当前图片的机器人位姿
                            for (int i = 0; i < robotPoseKeys.Count; i++)
                            {
                                if (robotPoseKeys[i] > imageKey)
                                {
                                    if (i > 0)
                                    {
                                        HMatrixTransform.mathHPose(robotPoseValues[i - 1],
                                                            robotPoseValues[i], out currentRobotPose,
                                                            (imageKey - robotPoseKeys[i - 1]) /
                                                            (double)(robotPoseKeys[i] - robotPoseKeys[i - 1])
                                                            );
                                        break;
                                    }
                                    else
                                    {
                                        currentRobotPose = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            currentRobotPose = null;
                            lastRobotPose = null;

                            goto showImageEnd;
                        }
                        

                        //夹角计算

                        Mat Cam1ToTool = new Mat();
                        Mat Cam1ToBase = new Mat();
                        Mat CamToTool = new Mat();
                        Mat CamToBase = new Mat();
                        Mat CenterToCam1 = new Mat();

                        if (CamParamName == null || camKey == null
                           || !Params.CamToCam1.TryGetValue(CamParamName, out var CamToCam1s) || !CamToCam1s.TryGetValue(camKey, out Mat CamToCam1))
                        {
                            System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara);
                            return;
                        }

                        if (Params.CamHandEyeType[CamParamName] == 0)
                        {
                            if (!Params.Cam1ToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out Cam1ToTool))
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara +$":Cam1ToTool");
                                return;
                            }
                            //Cam1ToTool = Params.Cam1ToTool[CamParamName][camKey];
                            CamToTool = Cam1ToTool * CamToCam1;
                        }
                        else
                        {
                            if (!Params.Cam1ToBase.TryGetValue(CamParamName, out var Cam1ToBases) || !Cam1ToBases.TryGetValue(camKey, out Cam1ToBase))
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToBase");
                                return;
                            }
                            if (!Params.CenterToCam1.TryGetValue(CamParamName, out var CenterToCam1s) || !CenterToCam1s.TryGetValue(camKey, out CenterToCam1))
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":CenterToCam1");
                                return;
                            }
                            //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                            //Cam1ToBase = Params.Cam1ToBase[CamParamName][camKey];
                            CamToBase = Cam1ToBase * CamToCam1;

                        }

                        if (Params.CamHandEyeType[CamParamName] == 0)
                        {
                            //如果两次的机器人位姿都不为空，则计算机器人移动与相机的夹角
                            if (currentRobotPose != null && lastRobotPose != null)
                            {

                                robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                                robotPoseMat.At<double>(0, 0) = lastRobotPose.x;
                                robotPoseMat.At<double>(0, 1) = lastRobotPose.y;
                                robotPoseMat.At<double>(0, 2) = lastRobotPose.z;
                                robotPoseMat.At<double>(0, 3) = lastRobotPose.rx;
                                robotPoseMat.At<double>(0, 4) = lastRobotPose.ry;
                                robotPoseMat.At<double>(0, 5) = lastRobotPose.rz;
                                robotPoseMat.At<double>(0, 6) = lastRobotPose.PoseType;

                                robotPoseMat.At<double>(1, 0) = currentRobotPose.x;
                                robotPoseMat.At<double>(1, 1) = currentRobotPose.y;
                                robotPoseMat.At<double>(1, 2) = currentRobotPose.z;
                                robotPoseMat.At<double>(1, 3) = currentRobotPose.rx;
                                robotPoseMat.At<double>(1, 4) = currentRobotPose.ry;
                                robotPoseMat.At<double>(1, 5) = currentRobotPose.rz;
                                robotPoseMat.At<double>(1, 6) = currentRobotPose.PoseType;

                            }
                            Mat ToolToBase = new Mat();
                            Vision.poseToHomMat3d(currentRobotPose.PoseType, currentRobotPose.x, currentRobotPose.y, currentRobotPose.z, currentRobotPose.rx, currentRobotPose.ry, currentRobotPose.rz, ToolToBase.CvPtr);
                            CamToBase = ToolToBase * CamToTool;
                            // 角度计算
                            Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToBase.CvPtr, 2, 0, out robotAndCamAngle);
                            //大于90的，都取缩小后的值
                            if (robotAndCamAngle > 90)
                            {
                                robotAndCamAngle = 180 - robotAndCamAngle;
                            }
                        }
                        else
                        {
                            if (currentRobotPose != null && lastRobotPose != null)
                            {
                                robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);

                                //轨迹的前一个点，要转成center2Tool
                                {
                                    Mat ToolToBase = new Mat();
                                    Mat BaseToTool = new Mat();
                                    Mat CenterToTool = new Mat();
                                    Vision.poseToHomMat3d(lastRobotPose.PoseType, lastRobotPose.x, lastRobotPose.y, lastRobotPose.z,
                                        lastRobotPose.rx, lastRobotPose.ry, lastRobotPose.rz, ToolToBase.CvPtr);

                                    BaseToTool = ToolToBase.Inv();

                                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                    double x, y, z, rx, ry, rz;
                                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                    robotPoseMat.At<double>(0, 0) = x;
                                    robotPoseMat.At<double>(0, 1) = y;
                                    robotPoseMat.At<double>(0, 2) = z;
                                    robotPoseMat.At<double>(0, 3) = rx;
                                    robotPoseMat.At<double>(0, 4) = ry;
                                    robotPoseMat.At<double>(0, 5) = rz;
                                    robotPoseMat.At<double>(0, 6) = 2;

                                }
                                //轨迹的后一个点，要转成center2Tool
                                {
                                    Mat ToolToBase = new Mat();
                                    Mat BaseToTool = new Mat();
                                    Mat CenterToTool = new Mat();
                                    Vision.poseToHomMat3d(currentRobotPose.PoseType, currentRobotPose.x, currentRobotPose.y, currentRobotPose.z,
                                        currentRobotPose.rx, currentRobotPose.ry, currentRobotPose.rz, ToolToBase.CvPtr);

                                    BaseToTool = ToolToBase.Inv();

                                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                    double x, y, z, rx, ry, rz;
                                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                    robotPoseMat.At<double>(1, 0) = x;
                                    robotPoseMat.At<double>(1, 1) = y;
                                    robotPoseMat.At<double>(1, 2) = z;
                                    robotPoseMat.At<double>(1, 3) = rx;
                                    robotPoseMat.At<double>(1, 4) = ry;
                                    robotPoseMat.At<double>(1, 5) = rz;
                                    robotPoseMat.At<double>(1, 6) = 2;

                                }
                            }
                            //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                            //Mat BaseToTool = robotPoseMat.Inv();

                            {

                                Mat ToolToBase = new Mat();
                                Mat BaseToTool = new Mat();
                                Vision.poseToHomMat3d(currentRobotPose.PoseType, currentRobotPose.x, currentRobotPose.y, currentRobotPose.z, currentRobotPose.rx, currentRobotPose.ry, currentRobotPose.rz, ToolToBase.CvPtr);
                                BaseToTool = ToolToBase.Inv();

                                //临时测试，中心点位姿
                                //CamToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                //当前相机的位姿
                                CamToTool = BaseToTool * CamToBase;

                                //Vision.showMatPoint(CamToTool, "CamToTool");

                                //Console.Write($"CamToTool:\r\n[");
                                //for (int i = 0; i < CamToTool.Rows; i++)
                                //{
                                //    Console.Write($"[");
                                //    for (int j = 0; j < CamToTool.Cols; j++)
                                //    {
                                //        Console.Write($"{CamToTool.At<double>(i, j)},");
                                //    }
                                //    Console.Write($"]");
                                //    Console.Write($"\r\n");

                                //}
                                //Console.Write($"]");

                            }
                            // 角度计算
                            Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                            //眼在手外，要减180度
                            robotAndCamAngle = 180 - robotAndCamAngle;
                            //大于90的，都取缩小后的值
                            if (robotAndCamAngle > 90)
                            {
                                robotAndCamAngle = 180 - robotAndCamAngle;
                            }
                        }
                        


                        robotAndCamAngleNumericUpDown.Text = robotAndCamAngle.ToString("F5");
                       

                    }
                }
                catch (Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
            showImageEnd:


                //参数更新
                try
                {
                    var imageSet = set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex][selectPictureListBox.SelectedIndex];
                    UnLoadUpData();
                    outlineCheck.IsChecked = imageSet.轮廓检测;
                    threNumericUpDown.Text = imageSet.minThreshold.ToString();
                    laserMinWidthNumericUpDown.Text = imageSet.laserMinWidth.ToString();
                    singleFrameCheck.IsChecked = imageSet.单帧检测;
                    _3DCloudDetCheck.IsChecked = imageSet._3DGlueDet;
                    glueWidthMinNumericUpDown.Text = imageSet.widthMin.ToString();
                    glueWidthMaxNumericUpDown.Text = imageSet.widthMax.ToString();
                    glueHeightMinNumericUpDown.Text = imageSet.heightMin.ToString();
                    glueHeightMaxNumericUpDown.Text = imageSet.heightMax.ToString();
                    glueAreaMinNumericUpDown.Text = imageSet.areaMin.ToString();
                    glueAreaMaxNumericUpDown.Text = imageSet.areaMax.ToString();
                    useCroppintCheck.IsChecked = imageSet.启用裁剪;
                    leftRangeMinNumericUpDown.Text = imageSet.LeftX.ToString();
                    leftRangeMaxNumericUpDown.Text = imageSet.RightX.ToString();
                    topRangeMinNumericUpDown.Text = imageSet.TopY.ToString();
                    topRangeMaxNumericUpDown.Text = imageSet.DownY.ToString();
                    useDiscreteDenoisingCheck.IsChecked = imageSet.离散去噪;
                    discreteDenoisingDistNumericUpDown.Text = imageSet.分段距离.ToString();
                    discreteDenoisingCountNumericUpDown.Text = imageSet.成段点数.ToString();

                    isUseAngleOptCheck.IsChecked = imageSet.isUseAngleOpt;

                    correctionScaleSizeXNumericUpDown.Text = imageSet.correctionScaleSizeX.ToString();
                    correctionScaleSizeYNumericUpDown.Text = imageSet.correctionScaleSizeY.ToString();


                    LoadUpData();
                    imageSetGrid.IsEnabled = true;
                    this.imageSet = imageSet;
                    showPara = true;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }


                //机器人姿态更新


            }

            if (!existImage)
            {
                hWindowModel.Clear();
                hImage = null;
            }
            if (!showPara)
            {
                imageSetGrid.IsEnabled = false;
                imageSet = null;
            }

            //刷新界面
            int showImageComboboxIndex = showImageComboBox.SelectedIndex;

            if (showImageComboboxIndex == 2)
            {
                showImageComboBox.SelectedIndex = -1;
                showImageComboBox.SelectedIndex = 0;
            }
            else 
            {
                showImageComboBox.SelectedIndex = -1;
                switch (showImageComboboxIndex)
                {
                    case 1:
                        runOutLineButton_Click(null,null);
                        break;
                    case 3:
                        runButton_Click2(null, null);
                        break;

                }
                showImageComboBox.SelectedIndex = showImageComboboxIndex;
            }



        }


        private void carTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isAlter && set != null)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DoYouWantToSaveIt + " " + set.Name + " " + _3DLaserGlueInspection.Resources.LanguageDict.Para, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!set.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SaveFailed + set.ErrMsg, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        //改回原来的
                        carTypeComboBox.SelectionChanged -= new SelectionChangedEventHandler(carTypeComboBox_SelectionChanged);
                        carTypeComboBox.Text = set.Name;
                        carTypeComboBox.SelectionChanged += new SelectionChangedEventHandler(carTypeComboBox_SelectionChanged);
                        return;
                    }
                }
                isAlter = false;
            }

            CamParamName = null;
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                foreach (var item in car.Cars.Values)
                {
                    if (item.Name == carTypeComboBox.Items[carTypeComboBox.SelectedIndex].ToString())
                    {
                        CamParamName = item.CamParamName;
                        break;
                    }
                }

                set = new Setting(carTypeComboBox.Items[carTypeComboBox.SelectedIndex].ToString());
                set.Load();
                cutSetListBox.Items.Clear();
                for (int i = 0; i < set.CutSets.Count; i++)
                {
                    cutSetListBox.Items.Add(set.CutSets[i].Name);
                }

                UnLoadUpDataSet();
                saveNGImageCheck.IsChecked = set.OtherSet.SaveNGImage;
                saveOKImageCheck.IsChecked = set.OtherSet.SaveOKImage;
                saveImageDirTextBox.Text = set.OtherSet.SaveImagePath;
                LoadUpDataSet();

                addButton.IsEnabled = deleteButton.IsEnabled = true;
            }
            else
            {
                set = null;
                addButton.IsEnabled = deleteButton.IsEnabled = false;
            }
        }

        private void cutSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectPictureListBox.SelectedIndex = -1;
            if (cutSetListBox.SelectedIndex >= 0)
            {
                selectCamListBox.Items.Clear();
                for (int i = 0; i < 4; i++)
                {
                    selectCamListBox.Items.Add(_3DLaserGlueInspection.Resources.LanguageDict.Cam1 + (i + 1).ToString());
                }

                cutSet = set.CutSets[cutSetListBox.SelectedIndex];
                if (cutSet != null)
                {
                    UnLoadUpData();

                    enableCam1Check.IsChecked = cutSet.Cam1Enabled;
                    enableCam2Check.IsChecked = cutSet.Cam2Enabled;
                    enableCam3Check.IsChecked = cutSet.Cam3Enabled;
                    enableCam4Check.IsChecked = cutSet.Cam4Enabled;

                    //isUseAngleOptCheck.IsChecked = cutSet.isUseAngleOpt;
                    coefficientSharingCheck.IsChecked = cutSet.isCoefficientSharing;

                    imageCountNumericUpDown.Text = cutSet.ImageNum.ToString();

                    showWidthNumericUpDown.Text = cutSet.ShowWidth.ToString();
                    showHeightNumericUpDown.Text = cutSet.ShowHeight.ToString();
                    colorLimitMaxNumericUpDown.Text = cutSet.ShowColorMax.ToString();
                    colorLimitMinNumericUpDown.Text = cutSet.ShowColorMin.ToString();
                    identificationSizeNumericUpDown.Text = cutSet.Size.ToString();
                    startImageIndexNumericUpDown.Text = cutSet.StartImageIndex.ToString();
                    endImageIndexNumericUpDown.Text = cutSet.EndImageIndex.ToString();
                    scaleSizeNumericUpDown.Text = cutSet.scaleSize.ToString();


                    //SelectedCamAndImage();

                    LoadUpData();

                    camUsedGroupBox.IsEnabled = publicParaGridBox.IsEnabled = true;
                    return;
                }
            }
            cutSet = null;
            camUsedGroupBox.IsEnabled = publicParaGridBox.IsEnabled = false;

            ////更新机器人姿态

            //Robot3DPoseDict.Clear();
            //List<string> camKeyList = new List<string> { "Cam1", "Cam2", "Cam3", "Cam4" };

            ////数据初始化
            //foreach (var camKey in camKeyList)
            //{
            //    var dictRobotPoseList = new SynchronizedList<Dictionary<long, PoseParameters>>();
            //    Robot3DPoseDict.Add(camKey, dictRobotPoseList);
            //    for (int indexTaskCut = 0; indexTaskCut < Images.Count; indexTaskCut++)
            //    {
            //        var dictRobotPose = new Dictionary<long, PoseParameters>();
            //        Robot3DPoseDict[camKey].Add(dictRobotPose);

            //    }
            //}



        }

        void numericUpDownImageNumUpData(object sender, EventArgs e)
        {
            if (cutSetListBox.SelectedIndex >= 0)
            {
                var cutSet = set.CutSets[cutSetListBox.SelectedIndex];
                if (cutSet != null)
                {
                    isAlter = true;
                    cutSet.ImageNum = Convert.ToInt32(imageCountNumericUpDown.Text);
                    for (int i = 0; i < 4; i++)
                    {
                        while (cutSet.imageSet.Count <= i)
                        {
                            cutSet.imageSet.Add(new List<ImageSet>());
                        }
                        while (cutSet.imageSet[i].Count < cutSet.ImageNum)
                        {
                            cutSet.imageSet[i].Add(new ImageSet(cutSet.imageSet[i].Count));
                        }
                    }
                }
            }
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            WindowGetName form = new WindowGetName();
            form.CheckName = new string[set.CutSets.Count];
            for (int i = 0; i < set.CutSets.Count; i++)
            {
                form.CheckName[i] = set.CutSets[i].Name;
            }
            if ((bool)form.ShowDialog())
            {
                CutSet cutSet = new CutSet(form.Value);
                XLDData xLDData = new XLDData(form.Value);
                if (!string.IsNullOrEmpty(form.CopyName))
                {
                    foreach (var c in set.CutSets)
                    {
                        if (c.Name == form.CopyName)
                        {
                            cutSet = c.Clone();
                            cutSet.Name = form.Value;
                            break;
                        }
                    }
                    foreach (var x in set.XLDDatas)
                    {
                        if (x.Name == form.CopyName)
                        {
                            xLDData = x.Clone();
                            xLDData.Name = form.Value;
                            break;
                        }
                    }
                }
                set.CutSets.Add(cutSet);
                set.XLDDatas.Add(xLDData);
                cutSetListBox.Items.Add(form.Value);

                isAlter = true;
            }
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (cutSetListBox.SelectedIndex >= 0)
            {
                set.CutSets.RemoveAt(cutSetListBox.SelectedIndex);
                set.XLDDatas.RemoveAt(cutSetListBox.SelectedIndex);
                cutSetListBox.Items.RemoveAt(cutSetListBox.SelectedIndex);

                isAlter = true;
            }
        }

        private void selectCamListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedCamAndImage();

        }

        private void selectPictureListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedCamAndImage();
        }

        private void openDirButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.SelectedPath = "D:\\image\\";
            if (carTypeComboBox.SelectedIndex >= 0 && set != null)
            {
                folder.SelectedPath = set.OtherSet.SaveImagePath + "\\" + carTypeComboBox.Items[carTypeComboBox.SelectedIndex].ToString();
            }
            if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] dataSources = Directory.GetDirectories(folder.SelectedPath);
                dataScoreComboBox.Items.Clear();
                //dataScoreComboBox.Items.AddRange(strings);
                for (int i = 0; i < dataSources.Length; i++)
                {
                    dataScoreComboBox.Items.Add(dataSources[i]);
                }
                dataScoreComboBox.SelectedIndex = 0;
            }
        }

        private void dataScoreComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //数据清空
            robotPoseKeys.Clear();
            robotPoseValues.Clear();
            ImageKeys.Clear();

            Robot3DPoseDict.Clear();
            Point3DXsDict.Clear();
            Point3DYsDict.Clear();
            Point3DZsDict.Clear();

            foreach (var item in Images)
            {
                foreach (var item2 in item.Values)
                {
                    foreach (var item3 in item2.Values)
                    {
                        item3?.Dispose();
                    }
                }
            }
            Images.Clear();

            if (dataScoreComboBox.SelectedIndex >= 0)
            {
                string basePath = dataScoreComboBox.Items[dataScoreComboBox.SelectedIndex].ToString();
                try
                {
                    string robotPoseKeysPath = $"{basePath}\\robotPoseKeys.xml";
                    if (File.Exists(robotPoseKeysPath))
                    {
                        XmlSerializer xml = new XmlSerializer(typeof(SynchronizedList<long>));
                        using (FileStream stream = new FileStream(robotPoseKeysPath, FileMode.OpenOrCreate))
                        {
                            var paramList = (SynchronizedList<long>)xml.Deserialize(stream);
                            if (paramList != null)
                            {
                                robotPoseKeys = paramList;
                            }
                            else
                            {
                                System.Windows.Forms.MessageBox.Show(robotPoseKeysPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException);
                                return;
                            }
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(robotPoseKeysPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                    return;
                }
                try
                {
                    string robotPoseValuesPath = $"{basePath}\\robotPoseValues.xml";
                    if (File.Exists(robotPoseValuesPath))
                    {
                        XmlSerializer xml = new XmlSerializer(typeof(SynchronizedList<double[]>));
                        using (FileStream stream = new FileStream(robotPoseValuesPath, FileMode.OpenOrCreate))
                        {
                            var paramList = (SynchronizedList<double[]>)xml.Deserialize(stream);
                            if (paramList != null)
                            {
                                robotPoseValues.Clear();
                                //robotPoseValues = paramList;
                                foreach (var pose in paramList)
                                {
                                    PoseParameters posePara = new PoseParameters();
                                    posePara.x = pose[0];
                                    posePara.y = pose[1];
                                    posePara.z = pose[2];
                                    posePara.rx = pose[3];
                                    posePara.ry = pose[4];
                                    posePara.rz = pose[5];
                                    posePara.PoseType = (int)pose[6];
                                    robotPoseValues.Add(posePara);
                                }
                            }
                            else
                            {
                                System.Windows.Forms.MessageBox.Show(robotPoseValuesPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException);
                                return;
                            }
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(robotPoseValuesPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                    return;
                }
                try
                {
                    string[] 段数 = Directory.GetDirectories(basePath);
                    for (int i = 0; i < 段数.Length; i++)
                    {
                        if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(段数[i]), out int i段数))
                        {
                            while (ImageKeys.Count <= i段数) { ImageKeys.Add(new Dictionary<string, SynchronizedList<long>>()); }
                            while (Images.Count <= i段数) { Images.Add(new Dictionary<string, Dictionary<long, Mat>>()); }
                            string[] 相机名 = Directory.GetDirectories(段数[i]);
                            for (int j = 0; j < 相机名.Length; j++)
                            {
                                string camKey = System.IO.Path.GetFileNameWithoutExtension(相机名[j]);
                                var imageDict = new Dictionary<long, Mat>();
                                Images[i段数].Add(camKey, imageDict);
                                string[] imagesPath = Directory.GetFiles(相机名[j], "*.png");
                                for (int k = 0; k < imagesPath.Length; k++)
                                {
                                    if (long.TryParse(System.IO.Path.GetFileNameWithoutExtension(imagesPath[k]), out long imageKey))
                                    {
                                        Mat hImage = null;
                                        try
                                        {
                                            hImage = new Mat(imagesPath[k], ImreadModes.Unchanged);
                                        }
                                        catch (Exception ex)
                                        {

                                        }
                                        if (hImage != null)
                                        {
                                            imageDict.Add(imageKey, hImage);
                                        }
                                        else
                                        {
                                            System.Windows.Forms.MessageBox.Show(imagesPath[k] + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist);
                                        }
                                    }
                                }
                                var keyList = new SynchronizedList<long>(imageDict.Keys.OrderBy(key => key).ToList());
                                ImageKeys[i段数].Add(camKey, keyList);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                    return;
                }
            }
        }

        private void lastSourecButton_Click(object sender, RoutedEventArgs e)
        {
            if (dataScoreComboBox.Items.Count < 1)
            {
                return;
            }
            if (dataScoreComboBox.SelectedIndex <= 0)
            {
                dataScoreComboBox.SelectedIndex = dataScoreComboBox.Items.Count - 1;
            }
            else
            {
                dataScoreComboBox.SelectedIndex -= 1;
            }
            currentSourceIDLabel.Content = dataScoreComboBox.SelectedIndex.ToString();

        }

        private void nextSourecButton_Click(object sender, RoutedEventArgs e)
        {
            if (dataScoreComboBox.Items.Count < 1)
            {
                return;
            }
            if (dataScoreComboBox.SelectedIndex >= dataScoreComboBox.Items.Count - 1)
            {
                dataScoreComboBox.SelectedIndex = 0;
            }
            else
            {
                dataScoreComboBox.SelectedIndex += 1;
            }
            currentSourceIDLabel.Content = dataScoreComboBox.SelectedIndex.ToString();
        }

        private void showRangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (hImage != null)
            {
                int imageWidth, imageHeight;
                imageWidth = hImage.Width;
                imageHeight = hImage.Height;

                double LeftX = imageWidth * imageSet.LeftX;
                double RightX = imageWidth * imageSet.RightX;
                double TopY = imageHeight * imageSet.TopY;
                double DownY = imageHeight * imageSet.DownY;
                hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(hImage).Clone());

                PointCollection points = new PointCollection();
                points.Add(new System.Windows.Point(LeftX, TopY));
                points.Add(new System.Windows.Point(RightX, TopY));
                points.Add(new System.Windows.Point(RightX, DownY));
                points.Add(new System.Windows.Point(LeftX, DownY));
                hWindowModel.AddPolygon(points, System.Windows.Media.Color.FromRgb(255, 0, 0), null);

            }
        }

        private void runOutLineButton_Click(object sender, RoutedEventArgs e)
        {
            if (hImage == null)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImage);
                return;
            }
            if (cutSet == null && imageSet == null)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoDetectionParameters);
                return;
            }

            Mat Cam1ToTool = new Mat();
            Mat Cam1ToBase = new Mat();
            Mat CamToTool = new Mat();
            Mat CamToBase = new Mat();
            if (CamParamName == null || camKey == null
             || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
             || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
             || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
             || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
             || !Params.CamToCam1.TryGetValue(CamParamName, out var CamToCam1s) || !CamToCam1s.TryGetValue(camKey, out var CamToCam1)
             || !Params.CenterToCam1.TryGetValue(CamParamName, out var CenterToCam1s) || !CenterToCam1s.TryGetValue(camKey, out var CenterToCam1))

            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoCameraParameters);
                return;
            }

            //根据情况提取手眼标定参数
            if (Params.CamHandEyeType[CamParamName] == 0)
            {
                if (!Params.Cam1ToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out Cam1ToTool))
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToTool");
                    return;
                }
                Cam1ToTool = Params.Cam1ToTool[CamParamName][camKey];
                CamToTool = Cam1ToTool * CamToCam1;
            }
            else
            {
                if (!Params.Cam1ToBase.TryGetValue(CamParamName, out var Cam1ToBases) || !Cam1ToBases.TryGetValue(camKey, out Cam1ToBase))
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToBase");
                    return;
                }
                //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                Cam1ToBase = Params.Cam1ToBase[CamParamName][camKey];
                CamToBase = Cam1ToBase * CamToCam1;

            }


            // 结果保存变量
            bool getOutlineResult = false;
            hXLDCont10mm = new Mat();

            //清空3d界面
            _3DShowControl.ClearPointCloud();


            //开始检测
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //for (int i = 0; i < 100; i++)
            //{
            if (imageSet.轮廓检测)
            {
                //激光轮廓提取
                //Mat xy = new Mat();
                //Vision.getLaserPosition(hImage, imageSet.minThreshold, out xy, camParam.OffsetX, camParam.OffsetY);


                Mat xy = new Mat();
                Mat imgCut = new Mat();
                int LeftX = 0;
                int TopY = 0;
                if (imageSet.启用裁剪)
                {
                    //Vision.cutLight(xy, camParam, hImage, imageSet, out xyCut);
                    int imageWidth, imageHeight;
                    imageWidth = hImage.Cols;
                    imageHeight = hImage.Rows;

                    LeftX = (int)(imageWidth * imageSet.LeftX);
                    TopY = (int)(imageHeight * imageSet.TopY);
                    int cutWidth = (int)((imageSet.RightX - imageSet.LeftX) * imageWidth);
                    int cutHeight = (int)((imageSet.DownY - imageSet.TopY) * imageHeight);

                    imgCut = new Mat(hImage, new OpenCvSharp.Rect(LeftX, TopY, cutWidth, cutHeight));
                }
                else
                {
                    imgCut = hImage.Clone();
                }
                Vision.getLaserPosition(imgCut, imageSet.minThreshold, imageSet.laserMinWidth, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);


                if (xy.Rows > 0)
                {
                    getOutlineResult = true;
                    //坐标转换
                    Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                    List<double> robotX, robotY, robotZ;
                    Mat lightXY = new Mat();
                    //Mat xyCut = new Mat();
                    //if (imageSet.启用裁剪)
                    //{
                    //    Vision.cutLight(xy, camParam, hImage, imageSet, out xyCut);
                    //}
                    //else
                    //{
                    //    xyCut = xy;
                    //}
                    //if (xyCut.Height > 0)
                    //{


                    if (Params.CamHandEyeType[CamParamName] == 0)
                    {
                        Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                    robotPose, out lightXY, out robotX, out robotY, out robotZ);
                    }
                    else
                    {
                        //搞个robot的逆pose，后面再专门打包个算法搞逆pose
                        PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                        Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToBase,
                            BaseInTool, out lightXY, out robotX, out robotY, out robotZ);
                    }

                    Vision.scalePoint(lightXY, cutSet, 90 - LightInCam.rx, out hXLDCont10mm);

                    //这里不做尺寸检测，因此，先不管它的x尺寸矫正
                    ////对x方向进行矫正
                    //double scaleX = 1;
                    //scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                    //Mat correctionPoints = new Mat();
                    //correctionPoints = hXLDCont10mm.Clone();

                    //for (int id = 0; id < correctionPoints.Rows; id++)
                    //{
                    //    correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                    //}

                    //hXLDCont10mm = correctionPoints.Clone();

                }
            }
            //}
            stopwatch.Stop();
            //结束检测
            TimeSpan elapsedTime = stopwatch.Elapsed;
            double useTime = elapsedTime.TotalMilliseconds;
            runTimeLabel.Content = $"{elapsedTime.TotalMilliseconds:F3} ms";

            if (imageSet.轮廓检测)
            {
                if (getOutlineResult)
                {
                    //GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm, ref hWindowModel, ref showing, ref olockShow);

                    //showImageComboBox.SelectedIndex = 1;
                    //showImageComboBox_SelectionChanged(null, null);
                    showImageComboBox.SelectedIndex = -1;
                    showImageComboBox.SelectedIndex = 1;


                }
            }
        }

        private void setImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Multiselect = true;
                ofd.Title = _3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectAFile;
                ofd.Filter = _3DLaserGlueInspection.Resources.LanguageDict.ImageBmpJpgJpegPngAllFiles;
                if (System.Windows.Forms.DialogResult.OK == ofd.ShowDialog())
                {
                    try
                    {
                        Mat hImage = new Mat(ofd.FileName, ImreadModes.Unchanged);
                        set.image?.Dispose();
                        set.image = hImage;
                        isAlter = true;
                        hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.SettingFailed + ex.ToString());
                    }
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheVehicleModelFirst);
            }
        }

        private void showImage_Click(object sender, RoutedEventArgs e)
        {
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                if (!set.image.Empty())
                {
                    hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImageSet);
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheVehicleModelFirst);
            }
        }

        private void setTrajectoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                if (cutSetListBox.SelectedIndex >= 0)
                {
                    if (!set.image.Empty())
                    {
                        hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                        while (cutSetListBox.SelectedIndex >= set.XLDDatas.Count)
                        {
                            set.XLDDatas.Add(new XLDData(set.CutSets[set.XLDDatas.Count].Name));
                        }
                        //画线
                        hWindowModel.startDraw();
                        hWindowModel.finishCreatePolylineEventHandler += finishCreatePolylineEven;
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImageSet);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheNumberOfSegmentsFirst);
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheVehicleModelFirst);
            }

        }

        private void showTrajectoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                if (cutSetListBox.SelectedIndex >= 0)
                {
                    if (!set.image.Empty())
                    {
                        hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                        //绘制轨迹
                        while (cutSetListBox.SelectedIndex >= set.XLDDatas.Count)
                        {
                            set.XLDDatas.Add(new XLDData(set.CutSets[set.XLDDatas.Count].Name));
                        }
                        var XLDData = set.XLDDatas[cutSetListBox.SelectedIndex];



                        if (XLDData.ControlRows.Length > 0)
                        {
                            PointCollection points = new PointCollection();
                            for (int i = 0; i < XLDData.ControlRows.Length; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = XLDData.ControlCols[i];
                                point.Y = XLDData.ControlRows[i];
                                points.Add(point);
                            }
                            hWindowModel.AddPolyline(points, Colors.Red);
                        }
                        else
                        {
                            System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoTrajectorySet);
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImageSet);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheNumberOfSegmentsFirst);
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheVehicleModelFirst);
            }
        }

        private void runTrajectoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (carTypeComboBox.SelectedIndex >= 0)
            {
                if (cutSetListBox.SelectedIndex >= 0)
                {
                    //初始显示
                    if (set.image.Empty())
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImageSet);
                        return;
                    }
                    hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                    while (cutSetListBox.SelectedIndex >= set.XLDDatas.Count)
                    {
                        set.XLDDatas.Add(new XLDData(set.CutSets[set.XLDDatas.Count].Name));
                    }
                    var XLDData = set.XLDDatas[cutSetListBox.SelectedIndex];
                    if (XLDData.ControlRows.Length <= 0)
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoTrajectorySet);
                        return;
                    }
                    if (set.CutSets[cutSetListBox.SelectedIndex].StartImageIndex > set.CutSets[cutSetListBox.SelectedIndex].EndImageIndex)
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.StartIndexIsGreaterThanEndIndex);
                        return;
                    }
                    int size = set.CutSets[cutSetListBox.SelectedIndex].Size;
                    int step = set.CutSets[cutSetListBox.SelectedIndex].EndImageIndex - set.CutSets[cutSetListBox.SelectedIndex].StartImageIndex + 1;
                    //显示折线
                    Vision.XLDDataDivide(XLDData, step, out var rows, out var cols, out var angles);

                    PointCollection points = new PointCollection();
                    for (int i = 0; i < step; i++)
                    {
                        System.Windows.Point point = new System.Windows.Point();
                        point.X = cols[i];
                        point.Y = rows[i];
                        points.Add(point);
                    }
                    hWindowModel.AddPolyline(points, Colors.Blue);

                    //结果颜色显示，显示交叉
                    Task.Run(() =>
                    {
                        for (int i = 0; i < rows.Count; i++)
                        {
                            //Thread.Sleep(20);
                            //if (i % 500 == 450 || i % 500 == 451 || i % 500 == 452 || i % 500 == 453)
                            if (i % 10 == 0)

                            {
                                //HXLDCont hXLDCont = new HXLDCont();
                                //hXLDCont.GenCrossContourXld(rows[i].D, cols[i].D, size, angles[i].D);
                                ////hXLDCont.GenRectangle2ContourXld(rows[i], cols[i], 0, 1, 1);
                                //hWindowControl.DispObj(hXLDCont, null, "red");
                                //hXLDCont.Dispose();
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    GlobalVarAndFunc.AddCrossContour(size, rows[i], cols[i], angles[i], Colors.Red, ref hWindowModel);
                                });
                            }
                            else
                            {
                                //HXLDCont hXLDCont = new HXLDCont();
                                //hXLDCont.GenCrossContourXld(rows[i].D, cols[i].D, size, angles[i].D);
                                //hWindowControl.DispObj(hXLDCont, null, "green");
                                //hXLDCont.Dispose();
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    GlobalVarAndFunc.AddCrossContour(size, rows[i], cols[i], angles[i], Colors.Green, ref hWindowModel);
                                });
                            }
                        }
                    });
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheNumberOfSegmentsFirst);
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.PleaseSelectTheVehicleModelFirst);
            }

        }


        private void imageCountNumericUpDown_TextChanged(object sender, TextChangedEventArgs e)
        {
            int ImageNum = Convert.ToInt32(imageCountNumericUpDown.Text);
            if (selectPictureListBox != null)
            {
                while (selectPictureListBox.Items.Count < ImageNum)
                {
                    selectPictureListBox.Items.Add(selectPictureListBox.Items.Count);
                }
                while (selectPictureListBox.Items.Count > ImageNum)
                {
                    selectPictureListBox.Items.RemoveAt(selectPictureListBox.Items.Count - 1);
                }
            }

        }

        private void Show2DWindow()
        {
            hWindowModel.Visibility = Visibility.Visible;
            _3DShowControl.Visibility = Visibility.Hidden;

        }
        private void Show3DWindow()
        {
            hWindowModel.Visibility = Visibility.Hidden;
            _3DShowControl.Visibility = Visibility.Visible;

        }

        private void showImageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cutSetListBox.SelectedIndex < 0 || selectCamListBox.SelectedIndex < 0 || selectPictureListBox.SelectedIndex < 0 || robotPoseKeys.Count < 0)
            {
                return;
            }
                
            if (showImageComboBox.SelectedIndex < 0)
            {
                return;
            }

            if (showImageComboBox.SelectedIndex != 2 && showImageComboBox.SelectedIndex != 4)
            {
                Show2DWindow();
            }
            else
            {
                Show3DWindow();
            }
            switch (showImageComboBox.SelectedIndex)
            {
                case 0:
                    if (hImage != null)
                    {

                        hWindowModel.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(hImage).Clone());
                    }
                    break;
                case 1:
                    if (hXLDCont10mm != null && !hXLDCont10mm.Empty())
                    {

                        GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, cutSet, hXLDCont10mm, ref hWindowModel, ref showing, ref olockShow);
                    }
                    else
                    {
                        hWindowModel.SetImageSource(null);
                    }
                    break;
                case 3:
                    if (hXLDCont10mm3D != null && !hXLDCont10mm3D.Empty())
                    {
                        if (!outMaxRegion.Empty() && !outRegionRectangle2.Empty())
                        {
                            GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, cutSet, cutSet.imageSet[selectCamListBox.SelectedIndex][selectPictureListBox.SelectedIndex], hXLDCont10mm3D, outMaxRegion, outRegionRectangle2, resultData, bResult, ref hWindowModel, ref showing, ref olockShow, 0, 0);

                        }
                        else
                        {
                            GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, cutSet, hXLDCont10mm3D, ref hWindowModel, ref showing, ref olockShow, 0, 0);
                        }
                    }
                    else
                    {
                        hWindowModel.SetImageSource(null);
                    }
                    break;

                case 2:
                    _3DShowControl.ClearPointCloud();
                    Thread.Sleep(100);
                    if (Point3DXsDict.Count > 0)
                    {
                        _3DShowControl.RefreshOn(100, true);

                        //显示机器人轨迹
                        foreach (var camKey in Robot3DPoseDict.Keys) //相机
                        {
                            var dictRobotPoses = Robot3DPoseDict[camKey];

                            foreach (var dictRobotPose in dictRobotPoses) //分段
                            {
                                foreach (var dictRobotPoseKey in dictRobotPose.Keys)
                                {
                                    var dictRobotPoseVal = dictRobotPose[dictRobotPoseKey];
                                    _3DShowControl.AddPoint(dictRobotPoseVal.x, dictRobotPoseVal.y, dictRobotPoseVal.z, 4);
                                }
                            }
                        }

                        //显示检测点云
                        foreach (var camKey in Point3DXsDict.Keys) //相机
                        {
                            var dictRobotX = Point3DXsDict[camKey];
                            var dictRobotY = Point3DYsDict[camKey];
                            var dictRobotZ = Point3DZsDict[camKey];


                            for (int i = 0; i < dictRobotX.Count; i++)
                            {
                                List<double> colorScale;

                                foreach (var dictRobotPointKey in dictRobotX[i].Keys)
                                {
                                    var x = dictRobotX[i][dictRobotPointKey];
                                    var y = dictRobotY[i][dictRobotPointKey];
                                    var z = dictRobotZ[i][dictRobotPointKey];

                                    colorScale = new List<double>();
                                    //计算显示颜色
                                    for (int j = 0; j < z.Count; j++)
                                    {
                                        double color = ((z[j] - cutSet.ShowColorMin / 1000) / ((cutSet.ShowColorMax - cutSet.ShowColorMin) / 1000));

                                        colorScale.Add(color);
                                    }
                                    _3DShowControl.AddPointCloud(x, y, z, colorScale);
                                }
                            }
                        }


                        _3DShowControl.RefreshOFF();
                        _3DShowControl.RefreshPoints();

                    }



                    break;

                case 4:

                    _3DShowControl.ClearPointCloud();
                    Thread.Sleep(100);
                    if (robotPoseValues.Count > 0)
                    {
                        _3DShowControl.RefreshOn(100, true);

                        //显示机器人轨迹
                        Mat Cam1ToTool = new Mat();
                        Mat Cam1ToBase = new Mat();
                        Mat CamToTool = new Mat();
                        Mat CamToBase = new Mat();
                        if (CamParamName == null || camKey == null
                         || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
                         || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
                         || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
                         || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
                         || !Params.CamToCam1.TryGetValue(CamParamName, out var CamToCam1s) || !CamToCam1s.TryGetValue(camKey, out var CamToCam1)
                         || !Params.CenterToCam1.TryGetValue(CamParamName, out var CenterToCam1s) || !CenterToCam1s.TryGetValue(camKey, out var CenterToCam1))

                        {
                            System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoCameraParameters);
                            goto showCamPoseFinish;
                        }

                        //根据情况提取手眼标定参数
                        if (Params.CamHandEyeType[CamParamName] == 0)
                        {
                            if (!Params.Cam1ToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out Cam1ToTool))
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToTool");
                                goto showCamPoseFinish;
                            }
                            Cam1ToTool = Params.Cam1ToTool[CamParamName][camKey];
                            CamToTool = Cam1ToTool * CamToCam1;
                        }
                        else
                        {
                            if (!Params.Cam1ToBase.TryGetValue(CamParamName, out var Cam1ToBases) || !Cam1ToBases.TryGetValue(camKey, out Cam1ToBase))
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToBase");
                                goto showCamPoseFinish;
                            }
                            //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                            Cam1ToBase = Params.Cam1ToBase[CamParamName][camKey];
                            CamToBase = Cam1ToBase * CamToCam1;

                        }

                        for (int i = 0; i < robotPoseValues.Count; i++)
                        {
                            var dictRobotPoseVal = robotPoseValues[i];

                            if (Params.CamHandEyeType[CamParamName] == 0)
                            {
                                //改为添加相机中心的坐标
                                Mat ToolToBase = new Mat();
                                Mat CenterToBase = new Mat();
                                PoseParameters centerInBase = new PoseParameters();
                                Vision.poseToHomMat3d(dictRobotPoseVal.PoseType, dictRobotPoseVal.x, dictRobotPoseVal.y, dictRobotPoseVal.z, dictRobotPoseVal.rx, dictRobotPoseVal.ry, dictRobotPoseVal.rz, ToolToBase.CvPtr);
                                CenterToBase = ToolToBase * Cam1ToTool * CenterToCam1;
                                centerInBase.PoseType = 2;
                                Vision.HomMat3dToPose(centerInBase.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToBase.CvPtr);
                                centerInBase.x = centerX;
                                centerInBase.y = centerY;
                                centerInBase.z = centerZ;
                                centerInBase.rx = centerRX;
                                centerInBase.ry = centerRY;
                                centerInBase.rz = centerRZ;

                                _3DShowControl.AddPoint(centerInBase.x, centerInBase.y, centerInBase.z, 4);
                            }
                            else 
                            {
                                //也改为添加相机中心的坐标，但是这里是法兰盘的坐标系
                                PoseParameters BaseInTool = Vision.PoseInv(dictRobotPoseVal);
                                Mat BaseToTool = new Mat();
                                PoseParameters centerInTool = new PoseParameters();
                                Vision.poseToHomMat3d(BaseInTool.PoseType, BaseInTool.x, BaseInTool.y, BaseInTool.z, BaseInTool.rx, BaseInTool.ry, BaseInTool.rz, BaseToTool.CvPtr);
                                Mat CenterToTool = new Mat();
                                CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;
                                centerInTool.PoseType = 2;
                                Vision.HomMat3dToPose(centerInTool.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToTool.CvPtr);
                                centerInTool.x = centerX;
                                centerInTool.y = centerY;
                                centerInTool.z = centerZ;
                                centerInTool.rx = centerRX;
                                centerInTool.ry = centerRY;
                                centerInTool.rz = centerRZ;

                                _3DShowControl.AddPoint(centerInTool.x, centerInTool.y, centerInTool.z, 4);
                            }
                        }


                        //显示相机位姿
                        if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0 && robotPoseKeys.Count > 0)
                        {
                            int indexRobotPose = 1;

                            //获取机器人位姿
                            long imageKey = ImageKeys[cutSetListBox.SelectedIndex][camKey][selectPictureListBox.SelectedIndex];

                            while (robotPoseKeys[indexRobotPose] < imageKey)//循环到的姿态晚于等于图片，处理
                            {

                                indexRobotPose++;
                            }
                            Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                            HMatrixTransform.mathHPose(robotPoseValues[indexRobotPose - 1],
                                                                   robotPoseValues[indexRobotPose], out robotPose,
                                                                   (imageKey - robotPoseKeys[indexRobotPose - 1]) /
                                                                   (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1])
                                                                   );

                            //计算相机位姿
                            if (Params.CamHandEyeType[CamParamName] == 0)
                            {
                                Mat ToolToBase = new Mat();
                                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);

                                CamToBase = ToolToBase * Cam1ToTool * CamToCam1;

                                double camInBaseX, camInBaseY, camInBaseZ, camInBaseRX, camInBaseRY, camInBaseRZ;

                                Vision.HomMat3dToPose(2, out camInBaseX, out camInBaseY, out camInBaseZ, out camInBaseRX, out camInBaseRY, out camInBaseRZ, CamToBase.CvPtr);

                                //显示相机 暂时先显示点
                                _3DShowControl.AddCoord(camInBaseX, camInBaseY, camInBaseZ, camInBaseRX, camInBaseRY, camInBaseRZ, 0.1);
                            }
                            else
                            {
                                //眼在手外时，显示法兰盘坐标系下的位姿
                                PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                                Mat BaseToTool = new Mat();
                                PoseParameters centerInTool = new PoseParameters();
                                Vision.poseToHomMat3d(BaseInTool.PoseType, BaseInTool.x, BaseInTool.y, BaseInTool.z, BaseInTool.rx, BaseInTool.ry, BaseInTool.rz, BaseToTool.CvPtr);

                                // 临时测试
                                //CamToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                CamToTool = BaseToTool * Cam1ToBase * CamToCam1;

                                double camInToolX, camInToolY, camInToolZ, camInToolRX, camInToolRY, camInToolRZ;

                                Vision.HomMat3dToPose(2, out camInToolX, out camInToolY, out camInToolZ, out camInToolRX, out camInToolRY, out camInToolRZ, CamToTool.CvPtr);

                                //显示相机 暂时先显示点
                                _3DShowControl.AddCoord(camInToolX, camInToolY, camInToolZ, camInToolRX, camInToolRY, camInToolRZ, 0.1);

                            }

                            

                        }

                    showCamPoseFinish:
                        _3DShowControl.RefreshOFF();
                        _3DShowControl.RefreshPoints();

                    }


                    break;


                default:
                    break;
            }
        }

        Dictionary<string, Task> tasks = new Dictionary<string, Task>();//相机-处理任务

        private void get3DDataButton_Click(object sender, RoutedEventArgs e)
        {
            _3DShowControl.ClearPointCloud();
            Thread.Sleep(100);

            //数据清空
            List<string> camKeyList = new List<string> { "Cam1", "Cam2", "Cam3", "Cam4" };
            tasks.Clear();
            Robot3DPoseDict.Clear();
            Point3DXsDict.Clear();
            Point3DYsDict.Clear();
            Point3DZsDict.Clear();

            //画面切换
            showImageComboBox.SelectedIndex = -1;
            showImageComboBox.SelectedIndex = 2;



            // 3D 每隔100毫秒再刷新一下结果
            _3DShowControl.RefreshOn(100, true);

            var cutSet = set.CutSets[cutSetListBox.SelectedIndex];

            int camID = -1;

            //相机循环
            foreach (var camKey in camKeyList)
            {
                camID += 1;
                int currentCamID = camID;

                //判断是否启用相机
                bool CamEnabled = camKey == "Cam1" ? cutSet.Cam1Enabled :
                        camKey == "Cam2" ? cutSet.Cam2Enabled :
                        camKey == "Cam3" ? cutSet.Cam3Enabled :
                        cutSet.Cam4Enabled;

                if (CamEnabled)
                {
                    //多线程循环
                    tasks.Add(camKey, Task.Run((Action)(() =>
                {
                    string camKeyCopy = camKey.ToString();
                    int indexRobotPose = 1;
                    //int indexTaskCut = 0;//指示正在图像处理段数

                    //数据初始化
                    var dictRobotPoseList = new SynchronizedList<Dictionary<long, PoseParameters>>();
                    Robot3DPoseDict.Add(camKeyCopy, dictRobotPoseList);

                    var dictXList = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DXsDict.Add(camKeyCopy, dictXList);
                    var dictYList = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DYsDict.Add(camKeyCopy, dictYList);
                    var dictZList = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DZsDict.Add(camKeyCopy, dictZList);


                    if (robotPoseKeys.Count() < 1)
                    {
                        return;
                    }
                    // 段循环
                    pointsSave = new List<double[]>();

                    for (int indexTaskCut = 0; indexTaskCut < Images.Count; indexTaskCut++)
                    {
                        var imageDict = Images[indexTaskCut][camKeyCopy];

                        var dictRobotPose = new Dictionary<long, PoseParameters>();

                        Robot3DPoseDict[camKeyCopy].Add(dictRobotPose);
                        var dictX = new Dictionary<long, List<double>>();
                        Point3DXsDict[camKeyCopy].Add(dictX);
                        var dictY = new Dictionary<long, List<double>>();
                        Point3DYsDict[camKeyCopy].Add(dictY);
                        var dictZ = new Dictionary<long, List<double>>();
                        Point3DZsDict[camKeyCopy].Add(dictZ);
                        // 图片循环
                        int indexImage = -1;
                        foreach (var camTimeKey in imageDict.Keys)
                        {
                            indexImage += 1;

                            var imageSet = cutSet.imageSet[currentCamID][indexImage];


                            if (robotPoseKeys[indexRobotPose] < camTimeKey)//循环到的姿态晚于等于图片，处理
                            {

                                indexRobotPose++;
                            }

                            Mat hImage_tmp = imageDict[camTimeKey].Clone();
                            if (hImage_tmp == null)
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImage);
                                return;
                            }
                            if (cutSet == null && imageSet == null)
                            {
                                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoDetectionParameters);
                                return;
                            }

                            Mat Cam1ToTool = new Mat();
                            Mat Cam1ToBase = new Mat();
                            Mat CamToTool = new Mat();
                            Mat CamToBase = new Mat();
                            if (CamParamName == null || camKey == null
                             || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
                             || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
                             || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
                             || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
                             || !Params.CamToCam1.TryGetValue(CamParamName, out var CamToCam1s) || !CamToCam1s.TryGetValue(camKey, out var CamToCam1)
                             || !Params.CenterToCam1.TryGetValue(CamParamName, out var CenterToCam1s) || !CenterToCam1s.TryGetValue(camKey, out var CenterToCam1))

                            {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoCameraParameters);
                                return;
                            }

                            //根据情况提取手眼标定参数
                            if (Params.CamHandEyeType[CamParamName] == 0)
                            {
                                if (!Params.Cam1ToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out Cam1ToTool))
                                {
                                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToTool");
                                    return;
                                }
                                Cam1ToTool = Params.Cam1ToTool[CamParamName][camKey];
                                CamToTool = Cam1ToTool * CamToCam1;
                            }
                            else
                            {
                                if (!Params.Cam1ToBase.TryGetValue(CamParamName, out var Cam1ToBases) || !Cam1ToBases.TryGetValue(camKey, out Cam1ToBase))
                                {
                                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToBase");
                                    return;
                                }
                                //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                                Cam1ToBase = Params.Cam1ToBase[CamParamName][camKey];
                                CamToBase = Cam1ToBase * CamToCam1;

                            }

                            


                            // 结果保存变量
                            bool getOutlineResult = false;
                            bool singleFrameExisOutline = false;
                            bool singleFrameExistGlue = false;
                            Data resultData = new Data();
                            BResult bResult = new BResult();
                            Mat outMaxRegion = new Mat();
                            Mat outRegionRectangle2 = new Mat();
                            Mat hXLDCont10mm = new Mat();
                            double PoseD = 0;
                            double V = 0;

                            double robotAndCamAngle = int.MaxValue;


                            //开始检测
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            //for (int i = 0; i < 100; i++)
                            {
                                if (imageSet.轮廓检测)
                                {
                                    Mat xy = new Mat();
                                    Mat imgCut = new Mat();
                                    int LeftX = 0;
                                    int TopY = 0;
                                    if (imageSet.启用裁剪)
                                    {
                                        //Vision.cutLight(xy, camParam, hImage, imageSet, out xyCut);
                                        int imageWidth, imageHeight;
                                        imageWidth = hImage_tmp.Cols;
                                        imageHeight = hImage_tmp.Rows;

                                        LeftX = (int)(imageWidth * imageSet.LeftX);
                                        TopY = (int)(imageHeight * imageSet.TopY);
                                        int cutWidth = (int)((imageSet.RightX - imageSet.LeftX) * imageWidth);
                                        int cutHeight = (int)((imageSet.DownY - imageSet.TopY) * imageHeight);

                                        imgCut = new Mat(hImage_tmp, new OpenCvSharp.Rect(LeftX, TopY, cutWidth, cutHeight));

                                    }
                                    else
                                    {
                                        imgCut = hImage_tmp.Clone();
                                    }
                                    Vision.getLaserPosition(imgCut, imageSet.minThreshold, imageSet.laserMinWidth, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);

                                    //坐标转换
                                    Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                                    HMatrixTransform.mathHPose(robotPoseValues[indexRobotPose - 1],
                                                                           robotPoseValues[indexRobotPose], out robotPose,
                                                                           (camTimeKey - robotPoseKeys[indexRobotPose - 1]) /
                                                                           (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1])
                                                                           );
                                    //三维数据添加(机器人坐标)
                                    if (Params.CamHandEyeType[CamParamName] == 0)
                                    {
                                        //改为添加相机中心的坐标
                                        Mat ToolToBase = new Mat();
                                        Mat CenterToBase = new Mat();
                                        PoseParameters centerInBase = new PoseParameters();
                                        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                        CenterToBase = ToolToBase * Cam1ToTool * CenterToCam1;
                                        centerInBase.PoseType = 2;
                                        Vision.HomMat3dToPose(centerInBase.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToBase.CvPtr);
                                        centerInBase.x = centerX;
                                        centerInBase.y = centerY;
                                        centerInBase.z = centerZ;
                                        centerInBase.rx = centerRX;
                                        centerInBase.ry = centerRY;
                                        centerInBase.rz = centerRZ;
                                        dictRobotPose.Add(camTimeKey, centerInBase);

                                        ////测试
                                        _3DShowControl.AddPoint(centerX, centerY, centerZ, 4);

                                    }
                                    else 
                                    {
                                        //也改为添加相机中心的坐标，但是这里是法兰盘的坐标系
                                        PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                                        Mat BaseToTool = new Mat();
                                        PoseParameters centerInTool = new PoseParameters();
                                        Vision.poseToHomMat3d(BaseInTool.PoseType, BaseInTool.x, BaseInTool.y, BaseInTool.z, BaseInTool.rx, BaseInTool.ry, BaseInTool.rz, BaseToTool.CvPtr);
                                        Mat CenterToTool = new Mat();
                                        Mat centerToBase = Cam1ToBase * CenterToCam1;
                                        Vision.HomMat3dToPose(centerInTool.PoseType, out double centerX2, out double centerY2, out double centerZ2, out double centerRX2, out double centerRY2, out double centerRZ2, centerToBase.CvPtr);


                                        CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;
                                        centerInTool.PoseType = 2;
                                        Vision.HomMat3dToPose(centerInTool.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToTool.CvPtr);
                                        centerInTool.x = centerX;
                                        centerInTool.y = centerY;
                                        centerInTool.z = centerZ;
                                        centerInTool.rx = centerRX;
                                        centerInTool.ry = centerRY;
                                        centerInTool.rz = centerRZ;
                                        dictRobotPose.Add(camTimeKey, centerInTool);

                                        ////测试
                                        _3DShowControl.AddPoint(centerX, centerY, centerZ, 4);

                                    }

                                    // 计算机器人移动距离
                                    if (dictRobotPose.Count > 0)
                                    {
                                        var last = dictRobotPose.Last();
                                        var lastRobotPose = last.Value;

                                        PoseD = Math.Sqrt(Math.Pow((robotPose.x - lastRobotPose.x), 2) +
                                            Math.Pow((robotPose.y - lastRobotPose.y), 2) +
                                            Math.Pow((robotPose.z - lastRobotPose.z), 2));
                                    }
                                    // 计算机器人与相机的夹角,必须要机器人有移动
                                    if (dictRobotPose.Count > 0 && PoseD > 0)
                                    {
                                        //计算CamToTool的矩阵

                                        //打包前后机器人pose
                                        var last = dictRobotPose.Last();
                                        var lastRobotPose = last.Value;

                                        Mat robotPoseMat = new Mat();
                                        //robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                                        //robotPoseMat.At<double>(0, 0) = lastRobotPose.x;
                                        //robotPoseMat.At<double>(0, 1) = lastRobotPose.y;
                                        //robotPoseMat.At<double>(0, 2) = lastRobotPose.z;
                                        //robotPoseMat.At<double>(0, 3) = lastRobotPose.rx;
                                        //robotPoseMat.At<double>(0, 4) = lastRobotPose.ry;
                                        //robotPoseMat.At<double>(0, 5) = lastRobotPose.rz;
                                        //robotPoseMat.At<double>(0, 6) = lastRobotPose.PoseType;

                                        //robotPoseMat.At<double>(1, 0) = robotPose.x;
                                        //robotPoseMat.At<double>(1, 1) = robotPose.y;
                                        //robotPoseMat.At<double>(1, 2) = robotPose.z;
                                        //robotPoseMat.At<double>(1, 3) = robotPose.rx;
                                        //robotPoseMat.At<double>(1, 4) = robotPose.ry;
                                        //robotPoseMat.At<double>(1, 5) = robotPose.rz;
                                        //robotPoseMat.At<double>(1, 6) = robotPose.PoseType;

                                        //if (Params.CamHandEyeType[CamParamName] == 1)
                                        //{
                                        //    //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                                        //    //Mat BaseToTool = robotPoseMat.Inv();
                                        //    Mat ToolToBase = new Mat();
                                        //    Mat BaseToTool = new Mat();
                                        //    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                        //    BaseToTool = ToolToBase.Inv();

                                        //    CamToTool = BaseToTool * Cam1ToBase * CamToCam1;
                                        //}

                                        //Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                                        if (Params.CamHandEyeType[CamParamName] == 0)
                                        {
                                            //如果两次的机器人位姿都不为空，则计算机器人移动与相机的夹角
                                            if (currentRobotPose != null && lastRobotPose != null)
                                            {

                                                robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                                                robotPoseMat.At<double>(0, 0) = lastRobotPose.x;
                                                robotPoseMat.At<double>(0, 1) = lastRobotPose.y;
                                                robotPoseMat.At<double>(0, 2) = lastRobotPose.z;
                                                robotPoseMat.At<double>(0, 3) = lastRobotPose.rx;
                                                robotPoseMat.At<double>(0, 4) = lastRobotPose.ry;
                                                robotPoseMat.At<double>(0, 5) = lastRobotPose.rz;
                                                robotPoseMat.At<double>(0, 6) = lastRobotPose.PoseType;

                                                robotPoseMat.At<double>(1, 0) = robotPose.x;
                                                robotPoseMat.At<double>(1, 1) = robotPose.y;
                                                robotPoseMat.At<double>(1, 2) = robotPose.z;
                                                robotPoseMat.At<double>(1, 3) = robotPose.rx;
                                                robotPoseMat.At<double>(1, 4) = robotPose.ry;
                                                robotPoseMat.At<double>(1, 5) = robotPose.rz;
                                                robotPoseMat.At<double>(1, 6) = robotPose.PoseType;

                                            }
                                            Mat ToolToBase = new Mat();
                                            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                            CamToBase = ToolToBase * CamToTool;
                                            // 角度计算
                                            Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToBase.CvPtr, 2, 0, out robotAndCamAngle);
                                            //大于90的，都取缩小后的值
                                            if (robotAndCamAngle > 90)
                                            {
                                                robotAndCamAngle = 180 - robotAndCamAngle;
                                            }
                                        }
                                        else
                                        {
                                            if (currentRobotPose != null && lastRobotPose != null)
                                            {
                                                robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);

                                                //轨迹的前一个点，要转成center2Tool
                                                {
                                                    Mat ToolToBase = new Mat();
                                                    Mat BaseToTool = new Mat();
                                                    Mat CenterToTool = new Mat();
                                                    Vision.poseToHomMat3d(lastRobotPose.PoseType, lastRobotPose.x, lastRobotPose.y, lastRobotPose.z,
                                                        lastRobotPose.rx, lastRobotPose.ry, lastRobotPose.rz, ToolToBase.CvPtr);

                                                    BaseToTool = ToolToBase.Inv();

                                                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                                    double x, y, z, rx, ry, rz;
                                                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                                    robotPoseMat.At<double>(0, 0) = x;
                                                    robotPoseMat.At<double>(0, 1) = y;
                                                    robotPoseMat.At<double>(0, 2) = z;
                                                    robotPoseMat.At<double>(0, 3) = rx;
                                                    robotPoseMat.At<double>(0, 4) = ry;
                                                    robotPoseMat.At<double>(0, 5) = rz;
                                                    robotPoseMat.At<double>(0, 6) = 2;

                                                }
                                                //轨迹的后一个点，要转成center2Tool
                                                {
                                                    Mat ToolToBase = new Mat();
                                                    Mat BaseToTool = new Mat();
                                                    Mat CenterToTool = new Mat();
                                                    Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                                                        robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);

                                                    BaseToTool = ToolToBase.Inv();

                                                    CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                                    double x, y, z, rx, ry, rz;
                                                    Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                                    robotPoseMat.At<double>(1, 0) = x;
                                                    robotPoseMat.At<double>(1, 1) = y;
                                                    robotPoseMat.At<double>(1, 2) = z;
                                                    robotPoseMat.At<double>(1, 3) = rx;
                                                    robotPoseMat.At<double>(1, 4) = ry;
                                                    robotPoseMat.At<double>(1, 5) = rz;
                                                    robotPoseMat.At<double>(1, 6) = 2;

                                                }
                                            }
                                            //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                                            //Mat BaseToTool = robotPoseMat.Inv();
                                            {

                                                Mat ToolToBase = new Mat();
                                                Mat BaseToTool = new Mat();
                                                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                BaseToTool = ToolToBase.Inv();

                                                CamToTool = BaseToTool * CamToBase;
                                            }
                                            // 角度计算
                                            Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                                            //眼在手外，要减180度
                                            robotAndCamAngle = 180 - robotAndCamAngle;
                                            //大于90的，都取缩小后的值
                                            if (robotAndCamAngle > 90)
                                            {
                                                robotAndCamAngle = 180 - robotAndCamAngle;
                                            }
                                        }

                                    }

                                    Mat lightXY = new Mat();
                                    bool singleFrameExistOutline = false;

                                    if (xy.Rows > 0)
                                    {
                                        getOutlineResult = true;

                                        List<double> robotX, robotY, robotZ, colorScale;

                                        ////测试
                                        if (Params.CamHandEyeType[CamParamName] == 0)
                                        {
                                            Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                                        robotPose, out lightXY, out robotX, out robotY, out robotZ);
                                        }
                                        else
                                        {
                                            //搞个robot的逆pose，后面再专门打包个算法搞逆pose
                                            PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                                            Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToBase,
                                                BaseInTool, out lightXY, out robotX, out robotY, out robotZ);
                                        }

                                        int displayIntervalID = 2;

                                        if (indexImage % displayIntervalID == 0)
                                        {
                                            colorScale = new List<double>();
                                            //计算显示颜色
                                            for (int i = 0; i < robotZ.Count; i++)
                                            {
                                                double color = ((robotZ[i] - cutSet.ShowColorMin / 1000) / ((cutSet.ShowColorMax - cutSet.ShowColorMin) / 1000));

                                                colorScale.Add(color);
                                            }


                                            _3DShowControl.AddPointCloud(robotX, robotY, robotZ, colorScale);
                                        }


                                        //三维数据添加
                                        dictX.Add(camTimeKey, robotX);
                                        dictY.Add(camTimeKey, robotY);
                                        dictZ.Add(camTimeKey, robotZ);
                                        //}
                                    }

                                    //单帧检测速度测试
                                    if (getOutlineResult)
                                    {
                                        if (lightXY.Rows > 0)
                                        {
                                            singleFrameExistOutline = true;
                                            //单帧检测(使用激光坐标系)
                                            Vision.scalePoint(lightXY, cutSet, 90 - LightInCam.rx, out hXLDCont10mm);

                                            if (imageSet.isUseAngleOpt)
                                            {
                                                //对x方向进行矫正
                                                double scaleX = 1;
                                                scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                                                Mat correctionPoints = new Mat();
                                                correctionPoints = hXLDCont10mm.Clone();

                                                for (int id = 0; id < correctionPoints.Rows; id++)
                                                {
                                                    correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                                }

                                                hXLDCont10mm = correctionPoints.Clone();
                                            }
                                            {
                                                //对两个方向进行矫正
                                                double scaleX = imageSet.correctionScaleSizeX;
                                                double scaleY = imageSet.correctionScaleSizeY;

                                                Mat correctionPoints = new Mat();
                                                correctionPoints = hXLDCont10mm.Clone();

                                                for (int id = 0; id < correctionPoints.Rows; id++)
                                                {
                                                    correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                                    correctionPoints.At<double>(id, 1) = correctionPoints.At<double>(id, 1) * scaleY;

                                                }

                                                hXLDCont10mm = correctionPoints.Clone();
                                            }

                                            //如果存在
                                            if (!hXLDCont10mm.Empty())
                                            {
                                                //Vision.singleFrameDetAndResult(hXLDCont10mm, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);

                                                //离散滤波
                                                if (imageSet.离散去噪)
                                                {
                                                    Vision.TrajectoryDiscreteFilter(hXLDCont10mm, out hXLDCont10mm3D, imageSet.分段距离 * cutSet.scaleSize, imageSet.成段点数);
                                                }
                                                else
                                                {
                                                    hXLDCont10mm3D = hXLDCont10mm.Clone();
                                                }

                                                //测试
                                                Vision.singleFrameDetAndResult(hXLDCont10mm3D, imageSet, cutSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
                                                //计算涂胶体积
                                                V = resultData.glueArea * PoseD;
                                            }
                                        }
                                    }


                                }



                            }
                            stopwatch.Stop();
                            //结束检测
                            TimeSpan elapsedTime = stopwatch.Elapsed;
                            double useTime = elapsedTime.TotalMilliseconds;



                        }
                    }



                })));

                    Thread.Sleep(10);
                }
            }

            Task.Run((Action)(() =>
            {
                foreach (var item in tasks.Values)
                {
                    while (!item.IsCompleted)
                    {
                        Thread.Sleep(10);
                    }
                }
                _3DShowControl.RefreshOFF();
                _3DShowControl.RefreshPoints();

                if (isSavePointCloud)
                {
                    //保存机器人每张图的位姿数据
                    foreach (var camKey_tmp in Robot3DPoseDict.Keys)
                    {
                        for (int i = 0; i < Robot3DPoseDict[camKey_tmp].Count; i++)
                        {
                            using (FileStream stream = new FileStream($"{camKey_tmp}_{(i + 1).ToString()}_robotPoseValues.xml", FileMode.Create))
                            {
                                //转化
                                List<double[]> pose = new List<double[]>();
                                foreach (var poseKey in Robot3DPoseDict[camKey_tmp][i].Values.ToArray())
                                {
                                    pose.Add(new double[] { poseKey.x, poseKey.y, poseKey.z, poseKey.rx, poseKey.ry, poseKey.rz, poseKey.PoseType });
                                }
                                new XmlSerializer(pose.GetType()).Serialize(stream, pose);
                            }
                        }
                    }
                    //保存测试点云数据
                    foreach (var camKey_tmp in Point3DXsDict.Keys)
                    {
                        for (int i = 0; i < Point3DXsDict[camKey_tmp].Count; i++)
                        {
                            foreach (var imageKey in Point3DXsDict[camKey_tmp][i].Keys)
                            {
                                for (int j = 0; j < Point3DXsDict[camKey_tmp][i][imageKey].Count(); j++)
                                {
                                    pointsSave.Add(new double[] { Point3DXsDict[camKey_tmp][i][imageKey][j], Point3DYsDict[camKey_tmp][i][imageKey][j], Point3DZsDict[camKey_tmp][i][imageKey][j] });
                                }

                            }


                        }
                    }

                    string fPath = "pointCloud.xml";
                    XmlSerializer xml = new XmlSerializer(pointsSave.GetType());
                    using (FileStream stream = new FileStream(fPath, FileMode.Create))
                    {
                        xml.Serialize(stream, pointsSave);
                    }
                }


            }));


        }

        private void runButton_Click(object sender, RoutedEventArgs e)
        {
            camKey = null;

            if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0)
            {
                try
                {
                    outMaxRegion = new Mat();
                    outRegionRectangle2 = new Mat();
                    hXLDCont10mm3D = new Mat();
                    resultData = new Data();
                    bResult = new BResult();


                    camKey = $"Cam{selectCamListBox.SelectedIndex + 1}";
                    if (ImageKeys.Count > cutSetListBox.SelectedIndex && ImageKeys[cutSetListBox.SelectedIndex].ContainsKey(camKey) && ImageKeys[cutSetListBox.SelectedIndex][camKey].Count > selectPictureListBox.SelectedIndex)
                    {
                        List<string> camKeyList = new List<string> { "Cam1", "Cam2", "Cam3", "Cam4" };

                        string camKey_tmp = "Cam1";
                        int indexImageCut = cutSetListBox.SelectedIndex;
                        int indexImage = selectPictureListBox.SelectedIndex;
                        var cutSet = set.CutSets[indexImageCut];
                        List<bool> camEnableList = new List<bool> { cutSet.Cam1Enabled, cutSet.Cam2Enabled, cutSet.Cam3Enabled, cutSet.Cam4Enabled };
                        //相机循环
                        foreach (var item in camKeyList)
                        {
                            //判断是否启用相机
                            bool CamEnabled = item == "Cam1" ? cutSet.Cam1Enabled :
                                    item == "Cam2" ? cutSet.Cam2Enabled :
                                    item == "Cam3" ? cutSet.Cam3Enabled :
                                    cutSet.Cam4Enabled;

                            if (CamEnabled)
                            {
                                camKey_tmp = item;
                                break;
                            }
                        }
                        long[] imageKeyList = Robot3DPoseDict[camKey_tmp][indexImageCut].Keys.ToArray();
                        Mat cloudList = new Mat(), poseList = new Mat();
                        poseList = Mat.Zeros(Robot3DPoseDict[camKey_tmp][indexImageCut].Values.Count, 6, MatType.CV_64FC1);
                        int id = 0;
                        foreach (var poseKey in Robot3DPoseDict[camKey_tmp][indexImageCut].Values)
                        {
                            poseList.At<Double>(id, 0) = poseKey.x;
                            poseList.At<Double>(id, 1) = poseKey.y;
                            poseList.At<Double>(id, 2) = poseKey.z;
                            poseList.At<Double>(id, 3) = poseKey.rx;
                            poseList.At<Double>(id, 4) = poseKey.ry;
                            poseList.At<Double>(id, 5) = poseKey.rz;
                            id++;
                        }
                        //3d 点云格式转换
                        int pointCount = 0;
                        int camId = 0;
                        foreach (var item in camEnableList)
                        {
                            if (item)
                            {

                                foreach (var imageKey_tmp in Point3DXsDict[camKeyList[camId]][indexImageCut].Keys)
                                {
                                    pointCount += Point3DXsDict[camKeyList[camId]][indexImageCut][imageKey_tmp].Count();
                                }
                            }
                            camId++;
                        }
                        cloudList = Mat.Zeros(pointCount, 3, MatType.CV_64FC1);
                        id = 0;
                        camId = 0;
                        foreach (var item in camEnableList)
                        {
                            if (item)
                            {
                                foreach (var imageKey_tmp in Point3DXsDict[camKeyList[camId]][indexImageCut].Keys)
                                {
                                    for (int j = 0; j < Point3DXsDict[camKeyList[camId]][indexImageCut][imageKey_tmp].Count; j++)
                                    {
                                        double x = Point3DXsDict[camKeyList[camId]][indexImageCut][imageKey_tmp][j];
                                        double y = Point3DYsDict[camKeyList[camId]][indexImageCut][imageKey_tmp][j];
                                        double z = Point3DZsDict[camKeyList[camId]][indexImageCut][imageKey_tmp][j];

                                        cloudList.At<Double>(id, 0) = x;
                                        cloudList.At<Double>(id, 1) = y;
                                        cloudList.At<Double>(id, 2) = z;

                                        id++;
                                    }

                                }
                            }
                            camId++;
                        }



                        //Mat[] imgList = new Mat[poseList.Rows];
                        //IntPtr[] imgsPtr = new IntPtr[imgList.Length];
                        //for (int i = 0; i < imgList.Length; i++)
                        //{
                        //    imgList[i] = new Mat();
                        //    imgsPtr[i] = imgList[i].CvPtr;
                        //}
                        Mat imgCut = new Mat();
                        Vision.pointCloudCutSingle(cloudList.CvPtr, poseList.CvPtr, indexImage, Vision.xSize, Vision.ySize, Vision.zSize, cutSet.scaleSize * 1000, Vision.offset_z, imgCut.CvPtr);

                        //Mat[] imgList = new Mat[poseList.Rows];
                        //IntPtr[] imgsPtr = new IntPtr[imgList.Length];
                        //for (int i = 0; i < imgList.Length; i++)
                        //{
                        //    imgList[i] = new Mat();
                        //    imgsPtr[i] = imgList[i].CvPtr;
                        //}
                        //Vision.pointCloudCutAll(cloudList.CvPtr, poseList.CvPtr, Vision.xSize, Vision.ySize, Vision.zSize, Vision.scaleSize * 1000, Vision.offset_z, imgsPtr);
                        //Mat imgCut = imgList[indexImage];

                        var imageKey = imageKeyList[indexImage];
                        //需要判断图片是否为空，来判断是否有结果
                        if (!imgCut.Empty())
                        {
                            Mat thinn = new Mat();
                            Mat points = new Mat();
                            Vision.thinning3d(imgCut.CvPtr, thinn.CvPtr, points.CvPtr);

                            //Cv2.NamedWindow("thinn", WindowFlags.Normal);
                            //Cv2.ImShow("thinn", thinn);
                            //Cv2.WaitKey(0);
                            //Cv2.DestroyAllWindows();
                            Console.WriteLine($"imgList[indexImage] count:{imgCut.Rows}");
                            Console.WriteLine($"points count:{points.Rows}");

                            //Vision.showMatPoint(imgList[indexImage], "origin");
                            //Vision.showMatPoint(points, "thinn");

                            //需要判断图片是否为空，来判断是否有结果
                            if (!thinn.Empty())
                            {
                                //检测
                                bool singleFrameExistGlue = false;


                                //离散滤波
                                if (imageSet.离散去噪)
                                {
                                    Vision.TrajectoryDiscreteFilter(points, out hXLDCont10mm3D, imageSet.分段距离 * cutSet.scaleSize, imageSet.成段点数);
                                }
                                else
                                {
                                    hXLDCont10mm3D = points.Clone();
                                }

                                Vision.singleFrameDetAndResult(points, imageSet, cutSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);


                            }
                        }

                        //结果显示

                        Dispatcher.Invoke(() =>
                        {
                            showImageComboBox.SelectedIndex = -1;
                            showImageComboBox.SelectedIndex = 3;
                        });

                    }
                }
                catch (Exception ex)
                {

                }
            }

        }
        bool isSavePointCloud = false;
        private void saveCloudPointCheck_Checked(object sender, RoutedEventArgs e)
        {
            isSavePointCloud = (bool)saveCloudPointCheck.IsChecked;
        }

        private void runButton_Click2(object sender, RoutedEventArgs e)
        {
            if (hImage == null)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoImage);
                return;
            }
            if (cutSet == null && imageSet == null)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoDetectionParameters);
                return;
            }
            if (currentRobotPose == null || lastRobotPose == null)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessRobotPose);
                return;
            }

            outMaxRegion = new Mat();
            outRegionRectangle2 = new Mat();
            hXLDCont10mm3D = new Mat();

            // 计算机器人移动距离
            double PoseD = Math.Sqrt(Math.Pow((currentRobotPose.x - lastRobotPose.x), 2) +
                Math.Pow((currentRobotPose.y - lastRobotPose.y), 2) +
                Math.Pow((currentRobotPose.z - lastRobotPose.z), 2));

            if (PoseD == 0)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DistIsZero);
                return;
            }

            Mat Cam1ToTool = new Mat();
            Mat Cam1ToBase = new Mat();
            Mat CamToTool = new Mat();
            Mat CamToBase = new Mat();
            if (CamParamName == null || camKey == null
             || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
             || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
             || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
             || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
             || !Params.CamToCam1.TryGetValue(CamParamName, out var CamToCam1s) || !CamToCam1s.TryGetValue(camKey, out var CamToCam1)
             || !Params.CenterToCam1.TryGetValue(CamParamName, out var CenterToCam1s) || !CenterToCam1s.TryGetValue(camKey, out var CenterToCam1))

            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.NoCameraParameters);
                return;
            }

            //根据情况提取手眼标定参数
            if (Params.CamHandEyeType[CamParamName] == 0)
            {
                if (!Params.Cam1ToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out Cam1ToTool))
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToTool");
                    return;
                }
                Cam1ToTool = Params.Cam1ToTool[CamParamName][camKey];
                CamToTool = Cam1ToTool * CamToCam1;
            }
            else
            {
                if (!Params.Cam1ToBase.TryGetValue(CamParamName, out var Cam1ToBases) || !Cam1ToBases.TryGetValue(camKey, out Cam1ToBase))
                {
                    System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.LessCamPara + $":Cam1ToBase");
                    return;
                }
                //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                Cam1ToBase = Params.Cam1ToBase[CamParamName][camKey];
                CamToBase = Cam1ToBase * CamToCam1;

            }

            // 结果保存变量
            bool getOutlineResult = false;

            //清空3d界面
            _3DShowControl.ClearPointCloud();

            //Dictionary<string, Task> tasks = new Dictionary<string, Task>();
            //开始检测
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //for (int i = 0; i < 1000; i++)
            {
                if (imageSet.轮廓检测)
                {
                    //激光轮廓提取
                    Mat xy = new Mat();
                    Mat lightXY = new Mat();

                    bool singleFrameExistOutline = false;
                    bool singleFrameExistGlue = false;

                    Mat imgCut = new Mat();
                    int LeftX = 0;
                    int TopY = 0;
                    if (imageSet.启用裁剪)
                    {
                        //Vision.cutLight(xy, camParam, hImage, imageSet, out xyCut);
                        int imageWidth, imageHeight;
                        imageWidth = hImage.Cols;
                        imageHeight = hImage.Rows;

                        LeftX = (int)(imageWidth * imageSet.LeftX);
                        TopY = (int)(imageHeight * imageSet.TopY);
                        int cutWidth = (int)((imageSet.RightX - imageSet.LeftX) * imageWidth);
                        int cutHeight = (int)((imageSet.DownY - imageSet.TopY) * imageHeight);

                        imgCut = new Mat(hImage, new OpenCvSharp.Rect(LeftX, TopY, cutWidth, cutHeight));
                    }
                    else
                    {
                        imgCut = hImage.Clone();
                    }


                    //修改，让相机拍照偏移值为0，测试用
                    //Vision.getLaserPosition(imgCut, imageSet.minThreshold, out xy,  LeftX,  TopY);
                    Vision.getLaserPosition(imgCut, imageSet.minThreshold, imageSet.laserMinWidth, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);


                    ////添加
                    //Vision.printPoint(xy, "xy");

                    if (xy.Rows > 0)
                    {
                        getOutlineResult = true;
                        //坐标转换
                        Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                        List<double> robotX, robotY, robotZ;



                        if (Params.CamHandEyeType[CamParamName] == 0)
                        {
                            Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                        robotPose, out lightXY, out robotX, out robotY, out robotZ);
                        }
                        else
                        {
                            //搞个robot的逆pose，后面再专门打包个算法搞逆pose
                            PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                            Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToBase,
                                BaseInTool, out lightXY, out robotX, out robotY, out robotZ);
                        }



                    }

                    //单帧检测速度测试
                    if (getOutlineResult)
                    {
                        if (lightXY.Rows > 0)
                        {
                            singleFrameExistOutline = true;
                            //单帧检测(使用激光坐标系)
                            Vision.scalePoint(lightXY, cutSet, 90 - LightInCam.rx, out hXLDCont10mm);
                            //Vision.showMatPoint(lightXY, "lightXY");

                            ////添加

                            //Console.WriteLine($"lightXY:\r\n");

                            //for (int i = 0; i < lightXY.Rows; i++)
                            //{
                            //    Console.WriteLine($"x:{lightXY.At<double>(i,0)}");
                            //    Console.WriteLine($"y:{lightXY.At<double>(i, 1)},\r\n");

                            //}



                            if (imageSet.isUseAngleOpt)
                            {
                                //涂胶角度优化
                                //对x方向进行矫正
                                double scaleX = 1;
                                scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                                Mat correctionPoints = new Mat();
                                correctionPoints = hXLDCont10mm.Clone();

                                for (int id = 0; id < correctionPoints.Rows; id++)
                                {
                                    correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                }

                                hXLDCont10mm = correctionPoints.Clone();
                            }


                            {
                                //对两个方向进行矫正
                                double scaleX = imageSet.correctionScaleSizeX;
                                double scaleY = imageSet.correctionScaleSizeY;

                                Mat correctionPoints = new Mat();
                                correctionPoints = hXLDCont10mm.Clone();

                                for (int id = 0; id < correctionPoints.Rows; id++)
                                {
                                    correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                    correctionPoints.At<double>(id, 1) = correctionPoints.At<double>(id, 1) * scaleY;

                                }

                                hXLDCont10mm = correctionPoints.Clone();
                            }

                            //Vision.showMatPoint(hXLDCont10mm, "hXLDCont10mm");
                            ////添加
                            //Vision.printPoint(hXLDCont10mm, "hXLDCont10mm");

                            //Console.WriteLine($"hXLDCont10mm:\r\n");

                            //for (int i = 0; i < hXLDCont10mm.Rows; i++)
                            //{
                            //    Console.WriteLine($"x:{hXLDCont10mm.At<double>(i, 0)}");
                            //    Console.WriteLine($"y:{hXLDCont10mm.At<double>(i, 1)},\r\n");

                            //}

                            //如果存在
                            if (!hXLDCont10mm.Empty())
                            {
                                //Vision.singleFrameDetAndResult(hXLDCont10mm, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);

                                //离散滤波
                                if (imageSet.离散去噪)
                                {
                                    Vision.TrajectoryDiscreteFilter(hXLDCont10mm, out hXLDCont10mm3D, imageSet.分段距离 * cutSet.scaleSize, imageSet.成段点数);
                                }
                                else
                                {
                                    hXLDCont10mm3D = hXLDCont10mm.Clone();
                                }

                                Vision.singleFrameDetAndResult(hXLDCont10mm3D, imageSet, cutSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
                            }
                        }
                    }
                }

            }



            stopwatch.Stop();
            //结束检测
            TimeSpan elapsedTime = stopwatch.Elapsed;
            double useTime = elapsedTime.TotalMilliseconds;
            runTimeLabel.Content = $"{elapsedTime.TotalMilliseconds:F3} ms";

            if (imageSet.轮廓检测)
            {
                if (getOutlineResult)
                {
                    showImageComboBox.SelectedIndex = -1;
                    showImageComboBox.SelectedIndex = 3;


                }
            }

        }

       

        private void correctionButton_Click(object sender, RoutedEventArgs e)
        {
            double actualGlueSizeX = 0;
            double actualGlueSizeY = 0;
            try
            {
                actualGlueSizeX = Convert.ToDouble(actualGlueWidthNumericUpDown.Text);
                actualGlueSizeY = Convert.ToDouble(actualGlueHeightNumericUpDown.Text);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"输入数据格式错误：{ex.Message}！");
                return;
            }
            if (actualGlueSizeX == 0 || actualGlueSizeY == 0)
            {
                System.Windows.Forms.MessageBox.Show($"输入实际数据不能为0！");
                return;
            }
            if (resultData.glueWidth == 0 || resultData.glueHeight == 0)
            {
                System.Windows.Forms.MessageBox.Show($"检测结果不能为0！");
                return;
            }

            if (cutSet.isCoefficientSharing)
            {
                double oldScaleSizeX = imageSet.correctionScaleSizeX;
                double oldScaleSizeY = imageSet.correctionScaleSizeY;

                //遍历修改
                for (int i = 0; i < set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex].Count; i++)
                {
                    set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex][i].correctionScaleSizeX = actualGlueSizeX / (resultData.glueWidth / oldScaleSizeX);
                    set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex][i].correctionScaleSizeY = actualGlueSizeY / (resultData.glueHeight / oldScaleSizeY);

                }
            }
            else 
            {
                imageSet.correctionScaleSizeX = actualGlueSizeX / (resultData.glueWidth / imageSet.correctionScaleSizeX);
                imageSet.correctionScaleSizeY = actualGlueSizeY / (resultData.glueHeight / imageSet.correctionScaleSizeY);
            }

            correctionScaleSizeXNumericUpDown.Text = imageSet.correctionScaleSizeX.ToString();
            correctionScaleSizeYNumericUpDown.Text = imageSet.correctionScaleSizeY.ToString();

            System.Windows.Forms.MessageBox.Show($"矫正成功。");

        }

        private void coefficientSharingCheck_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
