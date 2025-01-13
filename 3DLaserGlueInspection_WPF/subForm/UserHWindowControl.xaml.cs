using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using HalconDotNet;
using static System.Net.Mime.MediaTypeNames;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// UserHWindowControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserHWindowControl : UserControl
    {
        public readonly HWindow HalconWindow = null;
        //HWindowControl hWindowControl = new HWindowControl();
        public UserHWindowControl()
        {
            InitializeComponent();

            //hWindowControl.Dock = DockStyle.Fill;
            //hWindowControl.HMouseWheel += hWindowControl_HMouseWheel;
            //hWindowControl.HMouseDown += HWindowControl_HMouseDown;
            //hWindowControl.HMouseMove += HWindowControl_HMouseMove;
            //hWindowControl.HMouseUp += HWindowControl_HMouseUp;
            //hWindowControl.SizeChanged += HWindowControl_SizeChanged;

            HalconWindow = hWindowControlWPF.HalconWindow;
            //HalconWindow.SetWindowParam("background_color", "black");
        }

        private void HWindowControl_SizeChanged(object sender, EventArgs e)
        {
            lock (olock)
            {
                try
                {
                    _SetPart(showHImage);
                    for (int i = 0; i < showColors.Count; i++)
                    {
                        _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        double imgWidth = 0, imgHeight = 0;
        int beginRow, beginCol, endRow, endCol;//SetPart
        HObject showHImage = null;
        List<HObject> showHObjects = new List<HObject>();
        List<string> showModes = new List<string>();
        List<string> showColors = new List<string>();
        object olock = new object();
        public void DispClear()
        {
            lock (olock)
            {
                _DispClear();
            }
        }
        private void _DispClear()
        {
            foreach (HObject obj in showHObjects)
            {
                obj.Dispose();
            }
            showHImage = null;
            showHObjects.Clear();
            showModes.Clear();
            showColors.Clear();
            HalconWindow.ClearWindow();
        }

        public void DispObj(HObject hObject, string mode = null, string color = null)
        {
            lock (olock)
            {
                _DispObj(hObject, mode, color, true, true);
            }
        }
        void _DispObj(HObject hObject, string mode, string color, bool bClone, bool bAdd)
        {
            if (hObject == null)
            {
                return;
            }
            if (bAdd)
            {
                showHObjects.Add(bClone ? hObject.Clone() : hObject);
                showModes.Add(mode);
                showColors.Add(color);
            }
            if (!string.IsNullOrEmpty(mode))
            {
                HalconWindow.SetDraw(mode);
            }
            if (!string.IsNullOrEmpty(color))
            {
                if (int.TryParse(color, out int num))
                {
                    HalconWindow.SetColored(num);
                }
                else
                {
                    HalconWindow.SetColor(color);
                }
            }
            HalconWindow.DispObj(hObject);
        }
        public void DispImage(HObject image)
        {
            lock (olock)
            {
                _DispClear();
                if (image == null) return;
                showHImage = image.Clone();
                _SetPart(showHImage);
                _DispObj(showHImage, null, null, false, true);
            }
        }
        public void DispImageWithoutClone(HObject image)
        {
            lock (olock)
            {
                _DispClear();
                showHImage = image;
                _SetPart(showHImage);
                _DispObj(showHImage, null, null, false, true);
            }
        }
        void _SetPart(HObject image)
        {
            if (image == null)
            {
                return;
            }

            HOperatorSet.GetImageSize(image, out HTuple imgW, out HTuple imgH);
            imgWidth = imgW.D;
            imgHeight = imgH.D;
            HalconWindow.GetWindowExtents(out int row, out int column, out int width, out int height);
            double wndRatio = width / (double)height;
            double imgRatio = imgWidth / imgHeight;

            if (wndRatio > imgRatio)
            {
                beginRow = 0;
                endRow = (int)imgHeight;
                beginCol = (int)((imgWidth - imgHeight * wndRatio) / 2d);
                endCol = (int)(imgWidth - (imgWidth - imgHeight * wndRatio) / 2d);
            }
            else
            {
                beginRow = (int)(-(imgWidth / wndRatio - imgHeight) / 2d);
                endRow = (int)(imgHeight + (imgWidth / wndRatio - imgHeight) / 2d);
                beginCol = 0;
                endCol = (int)imgWidth;
            }
            HalconWindow.SetPart(beginRow, beginCol, endRow, endCol);
        }
        void _SetPart()
        {
            HalconWindow.SetPart(beginRow, beginCol, endRow, endCol);
        }
        public void SetPart(int row1, int column1, int row2, int column2)
        {
            HalconWindow.SetPart(row1, column1, row2, column2);
        }
        public void DispTextInImage(HTuple text, HTuple row, HTuple column)
        {
            HalconWindow.DispText(text, "image", row, column, new HTuple("black"), new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        }
        public void DispTextInImage(string text, double row, double column)
        {
            DispTextInImage(new HTuple(text), new HTuple(row), new HTuple(column));
        }
        public void DispTextInWindow(HTuple text, HTuple row, HTuple column)
        {
            HalconWindow.DispText(text, "window", row, column, new HTuple("black"), new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        }
        public void DispTextInWindow(string text, double row, double column)
        {
            DispTextInWindow(new HTuple(text), new HTuple(row), new HTuple(column));
        }
        //double mvposRow = 0.0, mvposCol = 0.0; int mvposBtn = 0;
        ///// <summary>
        /////获取点击鼠标坐标
        ///// </summary>
        //private void HWindowControl_HMouseDown(object sender, HMouseEventArgs e)
        //{
        //    lock (olock)
        //    {
        //        try
        //        {
        //            if (showHObjects.Count > 0)
        //            {
        //                if (e.Button == MouseButtons.Middle)
        //                {
        //                    HalconWindow.GetMpositionSubPix(out mvposRow, out mvposCol, out mvposBtn);

        //                }
        //                else if (e.Button == MouseButtons.Left)
        //                {

        //                }
        //                else if (e.Button == MouseButtons.Right)
        //                {

        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }
        //}
        ///// <summary>
        /////中键移动鼠标，平移图像
        ///// </summary>
        //private void HWindowControl_HMouseMove(object sender, HMouseEventArgs e)
        //{
        //    lock (olock)
        //    {
        //        try
        //        {
        //            if (e.Button == MouseButtons.Middle)
        //            {
        //                if (showHObjects.Count > 0)
        //                {
        //                    if (mvposBtn == 2)
        //                    {
        //                        double mposRow = 0.0, mposCol = 0.0; int mposBtn = 0;
        //                        HalconWindow.GetMpositionSubPix(out mposRow, out mposCol, out mposBtn);
        //                        int oldBeginRow = 0, oldBeginCol = 0, oldEndRow = 0, oldEndCol = 0;
        //                        HalconWindow.GetPart(out oldBeginRow, out oldBeginCol, out oldEndRow, out oldEndCol);

        //                        int zoomBeginRow = 0, zoomBeginCol = 0, zoomEndRow = 0, zoomEndCol = 0;
        //                        zoomBeginRow = -(int)(mposRow - mvposRow) + oldBeginRow;
        //                        zoomBeginCol = -(int)(mposCol - mvposCol) + oldBeginCol;
        //                        zoomEndRow = -(int)(mposRow - mvposRow) + oldEndRow;
        //                        zoomEndCol = -(int)(mposCol - mvposCol) + oldEndCol;

        //                        HalconWindow.ClearWindow();
        //                        HalconWindow.SetPaint("default");
        //                        HalconWindow.SetPart(zoomBeginRow, zoomBeginCol, zoomEndRow, zoomEndCol);
        //                        for (int i = 0; i < showHObjects.Count; i++)
        //                        {
        //                            _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                        }
        //                    }
        //                }
        //            }
        //            else if (e.Button == MouseButtons.Right)
        //            {
        //                if (showHImage != null)
        //                {
        //                    double mposRow = 0.0, mposCol = 0.0; int mposBtn = 0;
        //                    HalconWindow.GetMpositionSubPix(out mposRow, out mposCol, out mposBtn);
        //                    HOperatorSet.GetGrayval(showHImage, mposRow + 0.5, mposCol + 0.5, out HTuple grayval);
        //                    //HalconWindow.DispCross(mposRow, mposCol, 3, 1.57);
        //                    //显示文字
        //                    //HalconWindow.SetColor("red");
        //                    //HalconWindow.SetTposition(10, 10);
        //                    //HalconWindow.SetFont("-Arial-9-5-");   // "-Times New Roman-Normal-28-"
        //                    //HalconWindow.DispText(grayval.ToString(), "", 12, 12, "red", new HTuple("box", "shadow"), new HTuple("false", "false"));
        //                    string text = $"[{grayval.D:000}";
        //                    for (int i = 1; i < grayval.Length; i++)
        //                    {
        //                        text += $", {grayval[i].D:000}";
        //                    }
        //                    text += "]";
        //                    HalconWindow.DispText(text, "window", 12, 12, "black", new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }
        //}

        //DateTime dateTime = DateTime.Now;
        ///// <summary>
        /////放开鼠标
        ///// </summary>
        //private void HWindowControl_HMouseUp(object sender, HMouseEventArgs e)
        //{
        //    lock (olock)
        //    {
        //        try
        //        {
        //            if (e.Button == MouseButtons.Middle)
        //            {
        //                if (showHObjects.Count > 0)
        //                {
        //                    //var dddd = (DateTime.Now - dateTime).TotalMilliseconds;
        //                    if ((DateTime.Now - dateTime).TotalMilliseconds < 300)
        //                    {
        //                        HalconWindow.ClearWindow();
        //                        _SetPart();
        //                        for (int i = 0; i < showHObjects.Count; i++)
        //                        {
        //                            _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                        }
        //                    }
        //                    dateTime = DateTime.Now;
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }
        //}
        ///// <summary>
        ///// 滚动滑轮，缩放图像
        ///// </summary>
        //private void hWindowControl_HMouseWheel(object sender, HMouseEventArgs e)
        //{
        //    lock (olock)
        //    {
        //        try
        //        {
        //            if (showHObjects.Count > 0)
        //            {
        //                HalconWindow.GetMpositionSubPix(out double mposRow, out double mposCol, out int button);
        //                HalconWindow.GetPart(out int oldBeginRow, out int oldBeginCol, out int oldEndRow, out int oldEndCol);

        //                int zoomBeginRow = 0, zoomBeginCol = 0, zoomEndRow = 0, zoomEndCol = 0;
        //                if (e.Delta > 0)
        //                {
        //                    zoomBeginRow = (int)(oldBeginRow + (mposRow - oldBeginRow) * 0.2d);
        //                    zoomBeginCol = (int)(oldBeginCol + (mposCol - oldBeginCol) * 0.2d);
        //                    zoomEndRow = (int)(oldEndRow - (oldEndRow - mposRow) * 0.2d);
        //                    zoomEndCol = (int)(oldEndCol - (oldEndCol - mposCol) * 0.2d);
        //                }
        //                else
        //                {
        //                    zoomBeginRow = (int)(mposRow - (mposRow - oldBeginRow) / 0.8d);
        //                    zoomBeginCol = (int)(mposCol - (mposCol - oldBeginCol) / 0.8d);
        //                    zoomEndRow = (int)(mposRow + (oldEndRow - mposRow) / 0.8d);
        //                    zoomEndCol = (int)(mposCol + (oldEndCol - mposCol) / 0.8d);
        //                }

        //                bool outOfArea = zoomBeginRow >= imgHeight || zoomEndRow <= 0 || zoomBeginCol >= imgWidth || zoomEndCol <= 0;
        //                bool outOfSize = (zoomEndRow - zoomBeginRow) > imgWidth * 20 || (zoomEndCol - zoomBeginCol) > imgWidth * 20;
        //                bool outOfPixel = (hWindowControl.Height / (zoomEndRow - zoomBeginRow) > 500)
        //                    || (hWindowControl.Width / (zoomEndCol - zoomBeginCol)) > 500;

        //                if (outOfArea || outOfSize)
        //                {

        //                }
        //                else if (!outOfPixel)
        //                {
        //                    HalconWindow.ClearWindow();
        //                    HalconWindow.SetPaint(new HTuple("default"));
        //                    HalconWindow.SetPart(zoomBeginRow, zoomBeginCol, zoomEndRow, zoomBeginCol + (zoomEndRow - zoomBeginRow) * hWindowControl.Width / hWindowControl.Height);
        //                    for (int i = 0; i < showHObjects.Count; i++)
        //                    {
        //                        _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }
        //}

    }
}
