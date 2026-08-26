using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using OpenCvSharp;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using _3DLaserGlueInspection.subForm;
using System.Windows;

namespace _3DLaserGlueInspection
{
    public static class GeneralFunc
    {

        //private static void ChangeControlLanguateFun(ComponentResourceManager resources, Control control)
        //{
        //    //将资源与控件对应
        //    resources.ApplyResources(control, control.Name);
        //    if (control.HasChildren)//子控件，比如组合框GroupBox里的控件
        //    {
        //        foreach (Control controls in control.Controls)
        //            ChangeControlLanguateFun(resources, controls);
        //    }
        //    if (control is MenuStrip)//菜单栏控件
        //    {
        //        MenuStrip ms = (MenuStrip)control;
        //        if (ms.Items.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (ToolStripMenuItem ts in ms.Items)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //                if (ts.DropDownItems.Count > 0)
        //                {
        //                    foreach (ToolStripMenuItem tts in ts.DropDownItems)//子菜单
        //                    {
        //                        resources.ApplyResources(tts, tts.Name);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    if (control is DataGridView)//菜单栏控件
        //    {
        //        DataGridView dgv = (DataGridView)control;
        //        if (dgv.Columns.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (DataGridViewTextBoxColumn ts in dgv.Columns)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //            }
        //        }
        //    }
        //}

        #region  界面翻译
        //public static void ChangeLanguateFun(Type t, Form f)
        //{
        //    int currentLcid = 2052; //1033代表英文，2052代表中文
        //    if (GlobalVarAndFunc.LANGUAGE_ID == 1)
        //    {
        //        currentLcid = 1033;
        //    }
        //    Thread.CurrentThread.CurrentUICulture = new CultureInfo(currentLcid);
        //    ComponentResourceManager resources = new ComponentResourceManager(t);
        //    resources.ApplyResources(f, "$this");//窗体标题


        //    foreach (Control control in f.Controls)//循环当前界面所有的控件
        //    {
        //        ChangeControlLanguateFun(resources, control);
        //    }
        //    //刷新窗体，有时窗体标题无法切换成功，需要刷新一下
        //    f.Refresh();
        //}
        #endregion
    }



    public static class GlobalVarAndFunc
    {

        public static void SwitchLanguage(string cultureName)
        {
            // 保存选择到本地
            //这里要现在项目里添加这个变量。项目 → 右键 → 属性 → 左侧找到"设置"
            GlobalVarAndFunc.LANGUAGE_ID = cultureName;
            WriteLanguageID();

            // 语言切换
            InitLanguage();
            // 弹窗提醒重启后，切换语言
            var result = MessageBox.Show(
                Resources.LanguageDict.LanguageChangeSuccess,
                "Warn",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // // 重启应用，使 x:Static 重新按新语言加载
            //Process.Start(Application.ResourceAssembly.Location);
            //Application.Current.Shutdown();
            //
        }
        public static string LANGUAGE_ID = "zh-CN"; //zh-CN默认中文，en-US英文

        public static void ReadLanguageID()
        {
            string fPath = "Data\\LanguageID";
            if (File.Exists(fPath))
            {
                LANGUAGE_ID = File.ReadAllText(fPath);
            }
        }

        public static void WriteLanguageID()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }
            File.WriteAllText("Data\\LanguageID", GlobalVarAndFunc.LANGUAGE_ID);
        }


        /// <summary>
        /// 启动时恢复上次的语言设置
        /// </summary>
        public static void InitLanguage()
        {
            ReadLanguageID();
            if (!string.IsNullOrEmpty(LANGUAGE_ID))
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(LANGUAGE_ID);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(LANGUAGE_ID);
            }
        }

        public static string LanguageTranslate(string info)
        {
            string translate="";
            //if (LANGUAGE_ID == 0)
            //{
            //    translate = info;
            //}
            //else
            //{
            //    if (LANGUAGE_DIC != null)
            //    { 
            //        if (LANGUAGE_DIC.ContainsKey(info))
            //        {
            //            translate = LANGUAGE_DIC[info];
            //        }
            //        else
            //        {
            //            translate = info;
            //        }
            //    }
            //    else
            //    {
            //        translate = info;
            //    }

            //}
            return translate;
        }


        public static BitmapImage ConvertMatToBitmapImage(Mat mat)
        {            
            var bitmapImage = new BitmapImage();
            try
            {
                if (!mat.Empty())
                {
                    // 方法1：编码为JPEG流 
                    Cv2.ImEncode(".jpg", mat, out byte[] jpegData);

                    using (var stream = new MemoryStream(jpegData))
                    {
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
                    }
                }
            }
            catch (Exception ex)
            {
                
            }
           
            return bitmapImage;
        }



        public static void ShowImageData(int showWidth, int showHeight, CutSet cutSet, Mat hXLDCont10mm, ref ImageControl2 imageControl,ref bool showing, ref object olockShow, double offsetX = 0, double offsetY = 0)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        using (Mat mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3))
                        {
                            imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
                        }
                        //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0) + offsetX;
                            point.Y = hXLDCont10mm.At<double>(i, 1) + offsetY;
                            points.Add(point);
                        }
                        //DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        imageControl.AddPolyline(points, Colors.Gray);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }
        public static void ShowImageData(int showWidth, int showHeight, CutSet cutSet,ImageSet set, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, BResult bResult,
             ref ImageControl2 imageControl, ref bool showing, ref object olockShow, double offsetX = 0, double offsetY = 0)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        using (Mat mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3))
                        {
                            //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                            imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
                        }

                        //Console.WriteLine($"mat.Size:{mat.Size()}");

                        //Console.WriteLine($"Polyline :");

                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0) + offsetX;
                            point.Y = hXLDCont10mm.At<double>(i, 1) + offsetY;
                            points.Add(point);

                            //Console.WriteLine($"point:{point}");

                        }
                        //DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        imageControl.AddPolyline(points, Colors.Gray);


                        string textWindow1 = _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth + ":" + (bResult.glueWidth ? "OK" : "NG") + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                               $": {set.widthMin}~{set.widthMax}";
                        string textWindow2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight + ":" + (bResult.glueHeight ? "OK" : "NG") + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                            $": {set.heightMin}~{set.heightMax}";
                        string textWindow3 = _3DLaserGlueInspection.Resources.LanguageDict.Area + ":" + (bResult.glueArea ? "OK" : "NG") + " " + _3DLaserGlueInspection.Resources.LanguageDict.DetRange +
                            $": {set.areaMin}~{set.areaMax}";
                        string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
                        //Console.WriteLine($"point :({10},{10})");
                        //DispTextInImageHWindowControlEvent(textWindow, Colors.Black, 10, 10);
                        imageControl.AddTextBlock(textWindow1, (bResult.glueWidth ? Colors.Green : Colors.Red), 10, 10);
                        imageControl.AddTextBlock(textWindow2, (bResult.glueHeight ? Colors.Green : Colors.Red), 10, 10 + 24);
                        imageControl.AddTextBlock(textWindow3, (bResult.glueArea ? Colors.Green : Colors.Red), 10, 10 + 48);


                        if (!hRegion.Empty())
                        {
                            //Console.WriteLine($"text value :");
                            string text1 = _3DLaserGlueInspection.Resources.LanguageDict.GlueWidth+":" + $"{data.glueWidth:0.00}";
                            string text2 = _3DLaserGlueInspection.Resources.LanguageDict.GlueHeight+":" + $"{data.glueHeight:0.00}";
                            string text3 = _3DLaserGlueInspection.Resources.LanguageDict.Area+":" + $"{data.glueArea:0.00}";

                            imageControl.AddTextBlock(text1, (bResult.glueWidth ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY));
                            imageControl.AddTextBlock(text2, (bResult.glueHeight ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY + 24));
                            imageControl.AddTextBlock(text3, (bResult.glueArea ? Colors.Green : Colors.Red), (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY + 48));

                            //Console.WriteLine($"text result :");
                            //hWindowControl.DispTextInImage(text, data.row, data.column);
                           

                            //Console.WriteLine($"region :");

                            PointCollection regionPoints = new PointCollection();
                            for (int i = 0; i < hRegion.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegion.At<double>(i, 0)+ offsetX;
                                point.Y = hRegion.At<double>(i, 1) + offsetY;
                                regionPoints.Add(point);
                                //Console.WriteLine($"point:{point}");
                            }

                            imageControl.AddPolygon(regionPoints, Colors.Red, "fill");

                            //DispPolygonjHWindowControlEvent(regionPoints, Colors.Red, "fill");

                            //Console.WriteLine($"regionSmallestRectangle :");
                            PointCollection regionSmallestRectangle2Points = new PointCollection();
                            for (int i = 0; i < hRegionSmallestRectangle2.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegionSmallestRectangle2.At<double>(i, 0)+ offsetX;
                                point.Y = hRegionSmallestRectangle2.At<double>(i, 1) + offsetY;
                                regionSmallestRectangle2Points.Add(point);
                                //Console.WriteLine($"point:{point}");
                            }

                            //DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");
                            imageControl.AddPolygon(regionSmallestRectangle2Points, Colors.Blue, "margin");
                        }


                        if (set.isUseBaseLine)
                        {
                            // 直接使用检测阶段保存的基准线参数，不重复计算基准线。
                            double baselineOffset = set.distBaseLineThre * cutSet.scaleSize;
                            PointCollection baselinePoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY, 0);
                            PointCollection ignoredHeightPoints = CreateLinePoints(data.item0, data.item1, data.item2, data.item3,
                                showWidth * cutSet.scaleSize, showHeight * cutSet.scaleSize, offsetX, offsetY, baselineOffset);

                            if (baselinePoints.Count >= 2)
                            {
                                imageControl.AddPolyline(baselinePoints, Colors.Blue);
                            }
                            if (ignoredHeightPoints.Count >= 2)
                            {
                                imageControl.AddPolyline(ignoredHeightPoints, Colors.Yellow);
                            }
                        }
                    
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

        /// <summary>
        /// 根据 cv::fitLine 返回的方向向量和线上一点，生成与画布边界相交的直线。
        /// offsetDistance 为正时，沿图像上方（Y 减小方向）偏移。
        /// </summary>
        private static PointCollection CreateLinePoints(double vx, double vy, double x0, double y0,
            double imageWidth, double imageHeight, double offsetX, double offsetY, double offsetDistance)
        {
            PointCollection points = new PointCollection();
            double vectorLength = Math.Sqrt(vx * vx + vy * vy);
            if (vectorLength < 1e-12 || imageWidth <= 0 || imageHeight <= 0)
            {
                return points;
            }

            vx /= vectorLength;
            vy /= vectorLength;

            // 取 Y 分量为负的法向量，确保“上方”对应图像坐标的 Y 减小方向。
            double normalX = -vy;
            double normalY = vx;
            if (normalY > 0)
            {
                normalX = -normalX;
                normalY = -normalY;
            }

            double lineX = x0 + offsetX + normalX * offsetDistance;
            double lineY = y0 + offsetY + normalY * offsetDistance;
            double epsilon = 1e-8;
            List<double> intersections = new List<double>();

            Action<double> addIntersection = t =>
            {
                double x = lineX + t * vx;
                double y = lineY + t * vy;
                if (x < -epsilon || x > imageWidth + epsilon || y < -epsilon || y > imageHeight + epsilon)
                {
                    return;
                }

                foreach (double oldT in intersections)
                {
                    if (Math.Abs(oldT - t) < epsilon)
                    {
                        return;
                    }
                }
                intersections.Add(t);
            };

            if (Math.Abs(vx) > epsilon)
            {
                addIntersection(-lineX / vx);
                addIntersection((imageWidth - lineX) / vx);
            }
            if (Math.Abs(vy) > epsilon)
            {
                addIntersection(-lineY / vy);
                addIntersection((imageHeight - lineY) / vy);
            }

            if (intersections.Count >= 2)
            {
                intersections.Sort();
                points.Add(new System.Windows.Point(lineX + intersections[0] * vx, lineY + intersections[0] * vy));
                points.Add(new System.Windows.Point(lineX + intersections[intersections.Count - 1] * vx,
                    lineY + intersections[intersections.Count - 1] * vy));
            }

            return points;
        }
        public static void AddCrossContour(int size, double rows, double cols, double angles, System.Windows.Media.Color color, ref ImageControl2 imageControl)
        {
            //PointCollection Points1 = new PointCollection();
            //PointCollection Points2 = new PointCollection();

            //System.Windows.Point p1 = new System.Windows.Point(cols + Math.Cos(angles / 180 * Math.PI) * size, rows + Math.Sin(angles / 180 * Math.PI) * size);
            //System.Windows.Point p2 = new System.Windows.Point(cols + Math.Cos((angles + 180) / 180 * Math.PI) * size, rows + Math.Sin((angles + 180) / 180 * Math.PI) * size);
            //System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
            //System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
            //Points1.Add(p1);
            //Points1.Add(p2);
            //Points2.Add(p3);
            //Points2.Add(p4);

            //imageControl.AddPolyline(Points1, color, 2);
            //imageControl.AddPolyline(Points2, color, 2);

            PointCollection Points1 = new PointCollection();

            System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
            System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
     
            Points1.Add(p3);
            Points1.Add(p4);

            imageControl.AddPolyline(Points1, color, 2);

        }
    }

}
