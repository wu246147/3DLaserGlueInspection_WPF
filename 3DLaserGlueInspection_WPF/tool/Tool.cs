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


        public static int LANGUAGE_ID = 1; //0默认中文，1英文

        private static Dictionary<string, string> LANGUAGE_DIC ;


        public static void ReadLanguageID()
        {
            string fPath = "Data\\LanguageID";
            if (File.Exists(fPath))
            {
                LANGUAGE_ID = int.Parse(File.ReadAllText(fPath));
            }
        }

        public static void LanguageDicInit()
        {
            if (LANGUAGE_ID == 0)
            {
                LANGUAGE_DIC = null;
            }
            else if (LANGUAGE_ID == 1)
            {
                string jsonFilePath = "Data\\LanguageDictEN.json";
                if (File.Exists(jsonFilePath))
                {
                    string json = File.ReadAllText(jsonFilePath,Encoding.UTF8);
                    LANGUAGE_DIC = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                else
                {
                    LANGUAGE_DIC = null;
                    //MessageBox.Show("Not Exist Data\\LanguageDictEN file.", "Warning", MessageBoxButtons.OKCancel);
                }
            }
        }

        public static string LanguageTranslate(string info)
        {
            string translate;
            if (LANGUAGE_ID == 0)
            {
                translate = info;
            }
            else
            {
                if (LANGUAGE_DIC != null)
                { 
                    if (LANGUAGE_DIC.ContainsKey(info))
                    {
                        translate = LANGUAGE_DIC[info];
                    }
                    else
                    {
                        translate = info;
                    }
                }
                else
                {
                    translate = info;
                }

            }
            return translate;
        }


        public static BitmapImage ConvertMatToBitmapImage(Mat mat)
        {
            // 方法1：编码为JPEG流 
            Cv2.ImEncode(".jpg", mat, out byte[] jpegData);

            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream(jpegData))
            {
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }



        public static void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm, ref ImageControl2 imageControl,ref bool showing, ref object olockShow)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        Mat mat = new Mat();
                        mat = Mat.Zeros((int)(showHeight * Vision.scaleSize), (int)(showWidth * Vision.scaleSize), MatType.CV_8UC3);
                        imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));
                        //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0);
                            point.Y = hXLDCont10mm.At<double>(i, 1);
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
        public static void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, bResult bResult,
             ref ImageControl2 imageControl, ref bool showing, ref object olockShow)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        Mat mat = new Mat();
                        mat = Mat.Zeros((int)(showHeight * Vision.scaleSize), (int)(showWidth * Vision.scaleSize), MatType.CV_8UC3);

                        //DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布
                        imageControl.SetImageSource(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));

                        //Console.WriteLine($"mat.Size:{mat.Size()}");

                        //Console.WriteLine($"Polyline :");

                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0);
                            point.Y = hXLDCont10mm.At<double>(i, 1);
                            points.Add(point);

                            //Console.WriteLine($"point:{point}");

                        }
                        //DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        imageControl.AddPolyline(points, Colors.Gray);

                        if (!hRegion.Empty())
                        {
                            //Console.WriteLine($"text value :");
                            string text = GlobalVarAndFunc.LanguageTranslate("胶高：") + $"{data.胶高:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("胶宽：") + $"{data.胶宽:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("面积：") + $"{data.面积:0.00}";

                            //Console.WriteLine($"point :({data.column},{data.row})");
                            //DispTextInImageHWindowControlEvent(text, Colors.Black, (int)data.column, (int)data.row);
                            imageControl.AddTextBlock(text, Colors.White, (int)data.column + (int)(data.胶宽 / 2 * Vision.scaleSize),
                                (int)data.row + (int)(data.胶高 / 2 * Vision.scaleSize));

                            //Console.WriteLine($"text result :");
                            //hWindowControl.DispTextInImage(text, data.row, data.column);
                            string textWindow1 = GlobalVarAndFunc.LanguageTranslate("胶宽：") + (bResult.胶宽 ? "OK" : "NG");
                            string textWindow2 = GlobalVarAndFunc.LanguageTranslate("胶高：") + (bResult.胶高 ? "OK" : "NG");
                            string textWindow3 = GlobalVarAndFunc.LanguageTranslate("面积：") + (bResult.面积 ? "OK" : "NG");
                            string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
                            //Console.WriteLine($"point :({10},{10})");
                            //DispTextInImageHWindowControlEvent(textWindow, Colors.Black, 10, 10);
                            imageControl.AddTextBlock(textWindow, Colors.White, 10, 10);


                            //Console.WriteLine($"region :");

                            PointCollection regionPoints = new PointCollection();
                            for (int i = 0; i < hRegion.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegion.At<double>(i, 0);
                                point.Y = hRegion.At<double>(i, 1);
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
                                point.X = hRegionSmallestRectangle2.At<double>(i, 0);
                                point.Y = hRegionSmallestRectangle2.At<double>(i, 1);
                                regionSmallestRectangle2Points.Add(point);
                                //Console.WriteLine($"point:{point}");
                            }

                            //DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");
                            imageControl.AddPolygon(regionSmallestRectangle2Points, Colors.Blue, "margin");
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

        public static void AddCrossContour(int size, double rows, double cols, double angles, System.Windows.Media.Color color, ref ImageControl2 imageControl)
        {
            PointCollection Points1 = new PointCollection();
            PointCollection Points2 = new PointCollection();

            System.Windows.Point p1 = new System.Windows.Point(cols + Math.Cos(angles / 180 * Math.PI) * size, rows + Math.Sin(angles / 180 * Math.PI) * size);
            System.Windows.Point p2 = new System.Windows.Point(cols + Math.Cos((angles + 180) / 180 * Math.PI) * size, rows + Math.Sin((angles + 180) / 180 * Math.PI) * size);
            System.Windows.Point p3 = new System.Windows.Point(cols + Math.Cos((angles + 90) / 180 * Math.PI) * size, rows + Math.Sin((angles + 90) / 180 * Math.PI) * size);
            System.Windows.Point p4 = new System.Windows.Point(cols + Math.Cos((angles + 270) / 180 * Math.PI) * size, rows + Math.Sin((angles + 270) / 180 * Math.PI) * size);
            Points1.Add(p1);
            Points1.Add(p2);
            Points2.Add(p3);
            Points2.Add(p4);

            imageControl.AddPolyline(Points1, color);
            imageControl.AddPolyline(Points2, color);
        }
    }

}
