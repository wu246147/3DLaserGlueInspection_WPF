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
        //                    string text = GlobalVarAndFunc.LanguageTranslate("胶高：") + $"{data.胶高:0.00}\r\n"
        //                       + GlobalVarAndFunc.LanguageTranslate("胶宽：") + $"{data.胶宽:0.00}\r\n"
        //                       + GlobalVarAndFunc.LanguageTranslate("面积：") + $"{data.面积:0.00}";

        //                    //Console.WriteLine($"point :({data.column},{data.row})");
        //                    //DispTextInImageHWindowControlEvent(text, Colors.Black, (int)data.column, (int)data.row);
        //                    hWindowModel.AddTextBlock(text, Colors.White, (int)data.column+(int)(data.胶宽/2 * Vision.scaleSize), 
        //                        (int)data.row + (int)(data.胶高/2 * Vision.scaleSize));

        //                    //Console.WriteLine($"text result :");
        //                    //hWindowControl.DispTextInImage(text, data.row, data.column);
        //                    string textWindow1 = GlobalVarAndFunc.LanguageTranslate("胶宽：") + (bResult.胶宽 ? "OK" : "NG");
        //                    string textWindow2 = GlobalVarAndFunc.LanguageTranslate("胶高：") + (bResult.胶高 ? "OK" : "NG");
        //                    string textWindow3 = GlobalVarAndFunc.LanguageTranslate("面积：") + (bResult.面积 ? "OK" : "NG");
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
                toolAll.Header = GlobalVarAndFunc.LanguageTranslate("复制至全部图片");
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
                toolRange.Header = GlobalVarAndFunc.LanguageTranslate("复制至范围图片");
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

        private void CopyThre(ImageSet srcImageSet, ImageSet dstImageSet)
        {
            srcImageSet.minThreshold = dstImageSet.minThreshold;
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
        }


        private void WindowVision_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isAlter && set != null)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("是否保存") + " " + set.Name + " " + GlobalVarAndFunc.LanguageTranslate("参数？"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!set.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("保存失败：") + set.ErrMsg, GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
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
            //    toolAll.Text = GlobalVarAndFunc.LanguageTranslate("复制至全部图片");
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
            //    toolRange.Text = GlobalVarAndFunc.LanguageTranslate("复制至范围图片");
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

            leafRangeMinNumericUpDown.TextChanged += UpData;
            leafRangeMaxNumericUpDown.TextChanged += UpData;
            topRangeMinNumericUpDown.TextChanged += UpData;
            topRangeMaxNumericUpDown.TextChanged += UpData;

            useDiscreteDenoisingCheck.Checked += UpData;
            useDiscreteDenoisingCheck.Unchecked += UpData;

            discreteDenoisingDistNumericUpDown.TextChanged += UpData;
            discreteDenoisingCountNumericUpDown.TextChanged += UpData;

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

            leafRangeMinNumericUpDown.TextChanged -= UpData;
            leafRangeMaxNumericUpDown.TextChanged -= UpData;
            topRangeMinNumericUpDown.TextChanged -= UpData;
            topRangeMaxNumericUpDown.TextChanged -= UpData;

            useDiscreteDenoisingCheck.Checked -= UpData;
            useDiscreteDenoisingCheck.Unchecked -= UpData;

            discreteDenoisingDistNumericUpDown.TextChanged -= UpData;
            discreteDenoisingCountNumericUpDown.TextChanged -= UpData;

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

                    try
                    {
                        cutSet.ShowWidth = Convert.ToInt32(showWidthNumericUpDown.Text);
                        cutSet.ShowHeight = Convert.ToInt32(showHeightNumericUpDown.Text);
                        cutSet.ShowColorMax = Convert.ToDouble(colorLimitMaxNumericUpDown.Text);
                        cutSet.ShowColorMin = Convert.ToDouble(colorLimitMinNumericUpDown.Text);
                        cutSet.Size = Convert.ToInt32(identificationSizeNumericUpDown.Text);
                        cutSet.StartImageIndex = Convert.ToInt32(startImageIndexNumericUpDown.Text);
                        cutSet.EndImageIndex = Convert.ToInt32(endImageIndexNumericUpDown.Text);
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
                imageSet.单帧检测 = (bool)singleFrameCheck.IsChecked;
                imageSet._3DGlueDet = (bool)_3DCloudDetCheck.IsChecked;
                imageSet.widthMin = Convert.ToDouble(glueWidthMinNumericUpDown.Text);
                imageSet.widthMax = Convert.ToDouble(glueWidthMaxNumericUpDown.Text);
                imageSet.heightMin = Convert.ToDouble(glueHeightMinNumericUpDown.Text);
                imageSet.heightMax = Convert.ToDouble(glueHeightMaxNumericUpDown.Text);
                imageSet.areaMin = Convert.ToDouble(glueAreaMinNumericUpDown.Text);
                imageSet.areaMax = Convert.ToDouble(glueAreaMaxNumericUpDown.Text);
                imageSet.启用裁剪 = (bool)useCroppintCheck.IsChecked;
                imageSet.LeftX = Convert.ToDouble(leafRangeMinNumericUpDown.Text);
                imageSet.RightX = Convert.ToDouble(leafRangeMaxNumericUpDown.Text);
                imageSet.TopY = Convert.ToDouble(topRangeMinNumericUpDown.Text);
                imageSet.DownY = Convert.ToDouble(topRangeMaxNumericUpDown.Text);
                imageSet.离散去噪 = (bool)useDiscreteDenoisingCheck.IsChecked;
                imageSet.分段距离 = Convert.ToDouble(discreteDenoisingDistNumericUpDown.Text);
                imageSet.成段点数 = Convert.ToInt32(discreteDenoisingCountNumericUpDown.Text);
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
            if (cutSetListBox.SelectedIndex >= 0 && selectCamListBox.SelectedIndex >= 0 && selectPictureListBox.SelectedIndex >= 0)
            {
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
                            showImageComboBox.SelectedIndex = -1;
                            showImageComboBox.SelectedIndex = 0;
                            existImage = true;
                        }

                        //showImageComboBox_SelectionChanged(null, null);
                    }
                }
                catch (Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
                try
                {
                    var imageSet = set.CutSets[cutSetListBox.SelectedIndex].imageSet[selectCamListBox.SelectedIndex][selectPictureListBox.SelectedIndex];
                    UnLoadUpData();
                    outlineCheck.IsChecked = imageSet.轮廓检测;
                    threNumericUpDown.Text = imageSet.minThreshold.ToString();
                    singleFrameCheck.IsChecked = imageSet.单帧检测;
                    _3DCloudDetCheck.IsChecked = imageSet._3DGlueDet;
                    glueWidthMinNumericUpDown.Text = imageSet.widthMin.ToString();
                    glueWidthMaxNumericUpDown.Text = imageSet.widthMax.ToString();
                    glueHeightMinNumericUpDown.Text = imageSet.heightMin.ToString();
                    glueHeightMaxNumericUpDown.Text = imageSet.heightMax.ToString();
                    glueAreaMinNumericUpDown.Text = imageSet.areaMin.ToString();
                    glueAreaMaxNumericUpDown.Text = imageSet.areaMax.ToString();
                    useCroppintCheck.IsChecked = imageSet.启用裁剪;
                    leafRangeMinNumericUpDown.Text = imageSet.LeftX.ToString();
                    leafRangeMaxNumericUpDown.Text = imageSet.RightX.ToString();
                    topRangeMinNumericUpDown.Text = imageSet.TopY.ToString();
                    topRangeMaxNumericUpDown.Text = imageSet.DownY.ToString();
                    useDiscreteDenoisingCheck.IsChecked = imageSet.离散去噪;
                    discreteDenoisingDistNumericUpDown.Text = imageSet.分段距离.ToString();
                    discreteDenoisingCountNumericUpDown.Text = imageSet.成段点数.ToString();
                    LoadUpData();
                    imageSetGrid.IsEnabled = true;
                    this.imageSet = imageSet;
                    showPara = true;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
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
        }


        private void carTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isAlter && set != null)
            {
                DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("是否保存") + " " + set.Name + " " + GlobalVarAndFunc.LanguageTranslate("参数？"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    if (!set.Save())
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("保存失败：") + set.ErrMsg, GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
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
                    selectCamListBox.Items.Add(GlobalVarAndFunc.LanguageTranslate("相机") + (i + 1).ToString());
                }

                cutSet = set.CutSets[cutSetListBox.SelectedIndex];
                if (cutSet != null)
                {
                    UnLoadUpData();

                    enableCam1Check.IsChecked = cutSet.Cam1Enabled;
                    enableCam2Check.IsChecked = cutSet.Cam2Enabled;
                    enableCam3Check.IsChecked = cutSet.Cam3Enabled;
                    enableCam4Check.IsChecked = cutSet.Cam4Enabled;
                    imageCountNumericUpDown.Text = cutSet.ImageNum.ToString();

                    showWidthNumericUpDown.Text = cutSet.ShowWidth.ToString();
                    showHeightNumericUpDown.Text = cutSet.ShowHeight.ToString();
                    colorLimitMaxNumericUpDown.Text = cutSet.ShowColorMax.ToString();
                    colorLimitMinNumericUpDown.Text = cutSet.ShowColorMin.ToString();
                    identificationSizeNumericUpDown.Text = cutSet.Size.ToString();
                    startImageIndexNumericUpDown.Text = cutSet.StartImageIndex.ToString();
                    endImageIndexNumericUpDown.Text = cutSet.EndImageIndex.ToString();

                    //SelectedCamAndImage();

                    LoadUpData();

                    camUsedGroupBox.IsEnabled = publicParaGridBox.IsEnabled = true;
                    return;
                }
            }
            cutSet = null;
            camUsedGroupBox.IsEnabled = publicParaGridBox.IsEnabled = false;
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
                                System.Windows.Forms.MessageBox.Show(robotPoseKeysPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常"));
                                return;
                            }
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(robotPoseKeysPath + GlobalVarAndFunc.LanguageTranslate("文件不存在"));
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
                                System.Windows.Forms.MessageBox.Show(robotPoseValuesPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常"));
                                return;
                            }
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(robotPoseValuesPath + GlobalVarAndFunc.LanguageTranslate("文件不存在"));
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
                                            System.Windows.Forms.MessageBox.Show(imagesPath[k] + GlobalVarAndFunc.LanguageTranslate("文件不存在"));
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
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无图片"));
                return;
            }
            if (cutSet == null && imageSet == null)
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无检测参数"));
                return;
            }
            if (CamParamName == null || camKey == null
                || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
                || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
                || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
                || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
                || !Params.CamToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out var CamToTool))
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无相机参数"));
                return;
            }
            // 临时结果保存变量
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
                Vision.getLaserPosition(imgCut, imageSet.minThreshold, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);


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
                    Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                    robotPose, out lightXY, out robotX, out robotY, out robotZ);


                    Vision.scalePoint(lightXY, cutSet, out hXLDCont10mm);
                    //}


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
                ofd.Title = GlobalVarAndFunc.LanguageTranslate("请选择文件");
                ofd.Filter = GlobalVarAndFunc.LanguageTranslate("图片|*.bmp;*.jpg;*.jpeg;*.png;*.tif|所有文件|*.*");
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
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("设置失败：") + ex.ToString());
                    }
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择车型"));
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
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置图片"));
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择车型"));
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
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置图片"));
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择段数"));
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择车型"));
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
                            System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置轨迹"));
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置图片"));
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择段数"));
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择车型"));
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
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置图片"));
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
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未设置轨迹"));
                        return;
                    }
                    if (set.CutSets[cutSetListBox.SelectedIndex].StartImageIndex > set.CutSets[cutSetListBox.SelectedIndex].EndImageIndex)
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("起点大于终点"));
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
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择段数"));
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择车型"));
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
            if (showImageComboBox.SelectedIndex < 0)
            {
                return;
            }

            if (showImageComboBox.SelectedIndex != 2)
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

                        GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm, ref hWindowModel, ref showing, ref olockShow);
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
                            //GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, outMaxRegion, outRegionRectangle2, resultData, bResult, ref hWindowModel, ref showing, ref olockShow, (cutSet.ShowWidth - Vision.xSize * 1000) * Vision.scaleSize / 2, (cutSet.ShowHeight - Vision.ySize * 1000) * Vision.scaleSize / 2);

                            //GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, outMaxRegion, outRegionRectangle2, resultData, bResult, ref hWindowModel, ref showing, ref olockShow, 0, (cutSet.ShowHeight - Vision.ySize * 1000) * Vision.scaleSize / 2);

                            GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, outMaxRegion, outRegionRectangle2, resultData, bResult, ref hWindowModel, ref showing, ref olockShow, 0, 0);

                        }
                        else
                        {
                            //GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, ref hWindowModel, ref showing, ref olockShow, (cutSet.ShowWidth - Vision.xSize * 1000) * Vision.scaleSize / 2, (cutSet.ShowHeight - Vision.ySize * 1000) * Vision.scaleSize / 2);
                            //GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, ref hWindowModel, ref showing, ref olockShow, 0, (cutSet.ShowHeight - Vision.ySize * 1000) * Vision.scaleSize / 2);

                            GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm3D, ref hWindowModel, ref showing, ref olockShow, 0, 0);
                        }
                    }
                    else
                    {
                        hWindowModel.SetImageSource(null);
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

            //画面切换
            showImageComboBox.SelectedIndex = -1;
            showImageComboBox.SelectedIndex = 2;
            //数据清空
            List<string> camKeyList = new List<string> { "Cam1", "Cam2", "Cam3", "Cam4" };
            tasks.Clear();
            Robot3DPoseDict.Clear();
            Point3DXsDict.Clear();
            Point3DYsDict.Clear();
            Point3DZsDict.Clear();

            // 3D 每隔100毫秒再刷新一下结果
            _3DShowControl.RefreshOn(100, true);

            var cutSet = set.CutSets[cutSetListBox.SelectedIndex];

            //相机循环
            foreach (var camKey in camKeyList)
            {
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
                            if (robotPoseKeys[indexRobotPose] < camTimeKey)//循环到的姿态晚于等于图片，处理
                            {

                                indexRobotPose++;
                            }

                            Mat hImage_tmp = imageDict[camTimeKey].Clone();
                            if (hImage_tmp == null)
                            {
                                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无图片"));
                                return;
                            }
                            if (cutSet == null && imageSet == null)
                            {
                                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无检测参数"));
                                return;
                            }
                            if (CamParamName == null || camKeyCopy == null
                            || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKeyCopy, out var camParam)
                            || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKeyCopy, out var hCamPar)
                            || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKeyCopy, out var LightInCam)
                            || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKeyCopy, out var LightToCam)
                            || !Params.CamToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKeyCopy, out var CamToTool))
                            {
                                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无相机参数"));
                                return;
                            }
                            // 临时结果保存变量
                            bool getOutlineResult = false;
                            bool singleFrameExisOutline = false;
                            bool singleFrameExistGlue = false;
                            Data resultData = new Data();
                            BResult bResult = new BResult();
                            Mat outMaxRegion = new Mat();
                            Mat outRegionRectangle2 = new Mat();
                            Mat hXLDCont10mm = new Mat();


                            //开始检测
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            //for (int i = 0; i < 100; i++)
                            //{
                            if (imageSet.轮廓检测)
                            {
                                ////激光轮廓提取
                                //Mat xy = new Mat();
                                //Vision.getLaserPosition(hImage_tmp, imageSet.minThreshold, out xy, camParam.OffsetX, camParam.OffsetY);
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

                                    ////临时保存图片数据
                                    //string saveImagePath = "./cutImage/" + (camTimeKey).ToString() + ".png";
                                    //Cv2.ImWrite(saveImagePath, imgCut);
                                }
                                else
                                {
                                    imgCut = hImage_tmp.Clone();
                                }
                                Vision.getLaserPosition(imgCut, imageSet.minThreshold, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);

                                //坐标转换
                                Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                                HMatrixTransform.mathHPose(robotPoseValues[indexRobotPose - 1],
                                                                       robotPoseValues[indexRobotPose], out robotPose,
                                                                       (camTimeKey - robotPoseKeys[indexRobotPose - 1]) /
                                                                       (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1])
                                                                       );
                                //三维数据添加(机器人坐标)
                                dictRobotPose.Add(camTimeKey, robotPose);

                                _3DShowControl.AddPoint(robotPose.x, robotPose.y, robotPose.z, 4);

                                Mat lightXY = new Mat();
                                bool singleFrameExistOutline = false;

                                if (xy.Rows > 0)
                                {
                                    getOutlineResult = true;

                                    List<double> robotX, robotY, robotZ, colorScale;

                                    Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                                 robotPose, out lightXY, out robotX, out robotY, out robotZ);

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
                                        Vision.scalePoint(lightXY, cutSet, out hXLDCont10mm);
                                        //如果存在
                                        if (!hXLDCont10mm.Empty())
                                        {
                                            //Vision.singleFrameDetAndResult(hXLDCont10mm, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);

                                            //离散滤波
                                            if (imageSet.离散去噪)
                                            {
                                                Vision.TrajectoryDiscreteFilter(hXLDCont10mm, out hXLDCont10mm3D, imageSet.分段距离 * Vision.scaleSize, imageSet.成段点数);
                                            }
                                            else
                                            {
                                                hXLDCont10mm3D = hXLDCont10mm.Clone();
                                            }

                                            Vision.singleFrameDetAndResult(hXLDCont10mm3D, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
                                        }
                                    }
                                }


                            }



                            //}
                            stopwatch.Stop();
                            //结束检测
                            TimeSpan elapsedTime = stopwatch.Elapsed;
                            double useTime = elapsedTime.TotalMilliseconds;
                            //runTimeLabel.Content = $"{elapsedTime.TotalMilliseconds:F3} ms";

                            // 没必要显示每帧的检测结果，而且这样做导致影响检测速度,并且这里涉及跨线程问题
                            //if (imageSet.轮廓检测)
                            //{
                            //    if (singleFrameExistGlue)
                            //    {
                            //        GlobalVarAndFunc.ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm, ref hWindowModel, ref showing, ref olockShow);
                            //        //
                            //    }
                            //}


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
                        Vision.pointCloudCutSingle(cloudList.CvPtr, poseList.CvPtr, indexImage, Vision.xSize, Vision.ySize, Vision.zSize, Vision.scaleSize * 1000, Vision.offset_z, imgCut.CvPtr);

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
                                    Vision.TrajectoryDiscreteFilter(points, out hXLDCont10mm3D, imageSet.分段距离 * Vision.scaleSize, imageSet.成段点数);
                                }
                                else
                                {
                                    hXLDCont10mm3D = points.Clone();
                                }

                                Vision.singleFrameDetAndResult(points, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);


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
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无图片"));
                return;
            }
            if (cutSet == null && imageSet == null)
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无检测参数"));
                return;
            }
            if (CamParamName == null || camKey == null
                || !Params.Param.TryGetValue(CamParamName, out var camParams) || !camParams.TryGetValue(camKey, out var camParam)
                || !Params.CamPar.TryGetValue(CamParamName, out var hCamPars) || !hCamPars.TryGetValue(camKey, out var hCamPar)
                || !Params.LightInCam.TryGetValue(CamParamName, out var LightInCams) || !LightInCams.TryGetValue(camKey, out var LightInCam)
                || !Params.LightToCam.TryGetValue(CamParamName, out var LightToCams) || !LightToCams.TryGetValue(camKey, out var LightToCam)
                || !Params.CamToTool.TryGetValue(CamParamName, out var CamToTools) || !CamToTools.TryGetValue(camKey, out var CamToTool))
            {
                System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("无相机参数"));
                return;
            }
            // 临时结果保存变量
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

                    Vision.getLaserPosition(imgCut, imageSet.minThreshold, out xy, camParam.OffsetX + LeftX, camParam.OffsetY + TopY);

                    if (xy.Rows > 0)
                    {
                        getOutlineResult = true;
                        //坐标转换
                        Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                        List<double> robotX, robotY, robotZ;


                        Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                        robotPose, out lightXY, out robotX, out robotY, out robotZ);




                    }

                    //单帧检测速度测试
                    if (getOutlineResult)
                    {
                        if (lightXY.Rows > 0)
                        {
                            singleFrameExistOutline = true;
                            //单帧检测(使用激光坐标系)
                            Vision.scalePoint(lightXY, cutSet, out hXLDCont10mm);
                            //如果存在
                            if (!hXLDCont10mm.Empty())
                            {
                                //Vision.singleFrameDetAndResult(hXLDCont10mm, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);

                                //离散滤波
                                if (imageSet.离散去噪)
                                {
                                    Vision.TrajectoryDiscreteFilter(hXLDCont10mm, out hXLDCont10mm3D, imageSet.分段距离 * Vision.scaleSize, imageSet.成段点数);
                                }
                                else
                                {
                                    hXLDCont10mm3D = hXLDCont10mm.Clone();
                                }

                                Vision.singleFrameDetAndResult(hXLDCont10mm3D, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
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

    }
}
