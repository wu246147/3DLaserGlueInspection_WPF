//using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace _3DLaserGlueInspection.subForm
{
    public delegate void MyEventHandler();

    /// <summary>
    /// UserHWindowControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserHWindowControl : UserControl
    {
        //    //public readonly HWindow HalconWindow = null;
        //    //HWindowControl hWindowControl = new HWindowControl();
        //    static MyEventHandler sizeChangedDelayEventHandler;

        //    public UserHWindowControl()
        //    {
        //        InitializeComponent();

        //        hWindowControlWPF.HMouseWheel += HWindowControlWPF_HMouseWheel;
        //        hWindowControlWPF.HMouseDown += HWindowControlWPF_HMouseDown;
        //        hWindowControlWPF.HMouseMove += HWindowControlWPF_HMouseMove;
        //        hWindowControlWPF.HMouseUp += HWindowControlWPF_HMouseUp;
        //        hWindowControlWPF.SizeChanged += HWindowControlWPF_SizeChanged;


        //        //HalconWindow = hWindowControlWPF.HalconWindow;
        //        //HalconWindow.SetWindowParam("background_color", "black");
        //        sizeChangedDelayEventHandler += sizeChangedDelayEvent;

        //    }


        //    double imgWidth = 0, imgHeight = 0;
        //    int beginRow, beginCol, endRow, endCol;//SetPart
        //    HObject showHImage = null;
        //    List<HObject> showHObjects = new List<HObject>();
        //    List<string> showModes = new List<string>();
        //    List<string> showColors = new List<string>();
        //    object olock = new object();
        //    public void DispClear()
        //    {
        //        lock (olock)
        //        {
        //            _DispClear();
        //        }
        //    }
        //    private void _DispClear()
        //    {
        //        foreach (HObject obj in showHObjects)
        //        {
        //            obj.Dispose();
        //        }
        //        showHImage = null;
        //        showHObjects.Clear();
        //        showModes.Clear();
        //        showColors.Clear();
        //        hWindowControlWPF.HalconWindow.ClearWindow();
        //    }

        //    public void DispObj(HObject hObject, string mode = null, string color = null)
        //    {
        //        lock (olock)
        //        {
        //            _DispObj(hObject, mode, color, true, true);
        //        }
        //    }
        //    void _DispObj(HObject hObject, string mode, string color, bool bClone, bool bAdd)
        //    {
        //        if (hObject == null)
        //        {
        //            return;
        //        }
        //        if (bAdd)
        //        {
        //            showHObjects.Add(bClone ? hObject.Clone() : hObject);
        //            showModes.Add(mode);
        //            showColors.Add(color);
        //        }
        //        if (!string.IsNullOrEmpty(mode))
        //        {
        //            hWindowControlWPF.HalconWindow.SetDraw(mode);
        //        }
        //        if (!string.IsNullOrEmpty(color))
        //        {
        //            if (int.TryParse(color, out int num))
        //            {
        //                hWindowControlWPF.HalconWindow.SetColored(num);
        //            }
        //            else
        //            {
        //                hWindowControlWPF.HalconWindow.SetColor(color);
        //            }
        //        }
        //        hWindowControlWPF.HalconWindow.DispObj(hObject);
        //    }
        //    public void DispImage(HObject image)
        //    {
        //        lock (olock)
        //        {
        //            _DispClear();
        //            if (image == null) return;
        //            showHImage = image.Clone();
        //            _SetPart(showHImage);
        //            _DispObj(showHImage, null, null, false, true);
        //        }
        //    }
        //    public void DispImageWithoutClone(HObject image)
        //    {
        //        lock (olock)
        //        {
        //            _DispClear();
        //            showHImage = image;
        //            _SetPart(showHImage);
        //            _DispObj(showHImage, null, null, false, true);
        //        }
        //    }
        //    void _SetPart(HObject image)
        //    {
        //        if (image == null)
        //        {
        //            return;
        //        }

        //        HOperatorSet.GetImageSize(image, out HTuple imgW, out HTuple imgH);
        //        imgWidth = imgW.D;
        //        imgHeight = imgH.D;
        //        hWindowControlWPF.HalconWindow.GetWindowExtents(out int row, out int column, out int width, out int height);
        //        double wndRatio = width / (double)height;
        //        double imgRatio = imgWidth / imgHeight;

        //        if (wndRatio > imgRatio)
        //        {
        //            beginRow = 0;
        //            endRow = (int)imgHeight;
        //            beginCol = (int)((imgWidth - imgHeight * wndRatio) / 2d);
        //            endCol = (int)(imgWidth - (imgWidth - imgHeight * wndRatio) / 2d);
        //        }
        //        else
        //        {
        //            beginRow = (int)(-(imgWidth / wndRatio - imgHeight) / 2d);
        //            endRow = (int)(imgHeight + (imgWidth / wndRatio - imgHeight) / 2d);
        //            beginCol = 0;
        //            endCol = (int)imgWidth;
        //        }
        //        hWindowControlWPF.HalconWindow.SetPart(beginRow, beginCol, endRow, endCol);
        //    }
        //    void _SetPart()
        //    {
        //        hWindowControlWPF.HalconWindow.SetPart(beginRow, beginCol, endRow, endCol);
        //    }
        //    public void SetPart(int row1, int column1, int row2, int column2)
        //    {
        //        hWindowControlWPF.HalconWindow.SetPart(row1, column1, row2, column2);
        //    }
        //    public void DispTextInImage(HTuple text, HTuple row, HTuple column)
        //    {
        //        hWindowControlWPF.HalconWindow.DispText(text, "image", row, column, new HTuple("black"), new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        //    }
        //    public void DispTextInImage(string text, double row, double column)
        //    {
        //        DispTextInImage(new HTuple(text), new HTuple(row), new HTuple(column));
        //    }
        //    public void DispTextInWindow(HTuple text, HTuple row, HTuple column)
        //    {
        //        hWindowControlWPF.HalconWindow.DispText(text, "window", row, column, new HTuple("black"), new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        //    }
        //    public void DispTextInWindow(string text, double row, double column)
        //    {
        //        DispTextInWindow(new HTuple(text), new HTuple(row), new HTuple(column));
        //    }

        //    double mvposRow = 0.0, mvposCol = 0.0; int mvposBtn = 0;
        //    /// <summary>
        //    ///获取点击鼠标坐标
        //    /// </summary>
        //    private void HWindowControlWPF_HMouseDown(object sender, HMouseEventArgsWPF e)
        //    {
        //        lock (olock)
        //        {
        //            try
        //            {
        //                if (showHObjects.Count > 0)
        //                {
        //                    if (e.Button == System.Windows.Input.MouseButton.Middle)
        //                    {
        //                        hWindowControlWPF.HalconWindow.GetMpositionSubPix(out mvposRow, out mvposCol, out mvposBtn);

        //                    }
        //                    else if (e.Button == System.Windows.Input.MouseButton.Left)
        //                    {

        //                    }
        //                    else if (e.Button == System.Windows.Input.MouseButton.Right)
        //                    {

        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {

        //            }
        //        }
        //    }
        //    /// <summary>
        //    ///中键移动鼠标，平移图像
        //    /// </summary>
        //    private void HWindowControlWPF_HMouseMove(object sender, HMouseEventArgsWPF e)
        //    {
        //        lock (olock)
        //        {
        //            try
        //            {
        //                if (e.Button == System.Windows.Input.MouseButton.Middle)
        //                {
        //                    if (showHObjects.Count > 0)
        //                    {
        //                        if (mvposBtn == 2)
        //                        {
        //                            double mposRow = 0.0, mposCol = 0.0; int mposBtn = 0;
        //                            hWindowControlWPF.HalconWindow.GetMpositionSubPix(out mposRow, out mposCol, out mposBtn);
        //                            int oldBeginRow = 0, oldBeginCol = 0, oldEndRow = 0, oldEndCol = 0;
        //                            hWindowControlWPF.HalconWindow.GetPart(out oldBeginRow, out oldBeginCol, out oldEndRow, out oldEndCol);

        //                            int zoomBeginRow = 0, zoomBeginCol = 0, zoomEndRow = 0, zoomEndCol = 0;
        //                            zoomBeginRow = -(int)(mposRow - mvposRow) + oldBeginRow;
        //                            zoomBeginCol = -(int)(mposCol - mvposCol) + oldBeginCol;
        //                            zoomEndRow = -(int)(mposRow - mvposRow) + oldEndRow;
        //                            zoomEndCol = -(int)(mposCol - mvposCol) + oldEndCol;

        //                            hWindowControlWPF.HalconWindow.ClearWindow();
        //                            hWindowControlWPF.HalconWindow.SetPaint("default");
        //                            hWindowControlWPF.HalconWindow.SetPart(zoomBeginRow, zoomBeginCol, zoomEndRow, zoomEndCol);
        //                            for (int i = 0; i < showHObjects.Count; i++)
        //                            {
        //                                _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                            }
        //                        }
        //                    }
        //                }
        //                else if (e.Button == System.Windows.Input.MouseButton.Right)
        //                {
        //                    if (showHImage != null)
        //                    {
        //                        double mposRow = 0.0, mposCol = 0.0; int mposBtn = 0;
        //                        hWindowControlWPF.HalconWindow.GetMpositionSubPix(out mposRow, out mposCol, out mposBtn);
        //                        HOperatorSet.GetGrayval(showHImage, mposRow + 0.5, mposCol + 0.5, out HTuple grayval);
        //                        //HalconWindow.DispCross(mposRow, mposCol, 3, 1.57);
        //                        //显示文字
        //                        //HalconWindow.SetColor("red");
        //                        //HalconWindow.SetTposition(10, 10);
        //                        //HalconWindow.SetFont("-Arial-9-5-");   // "-Times New Roman-Normal-28-"
        //                        //HalconWindow.DispText(grayval.ToString(), "", 12, 12, "red", new HTuple("box", "shadow"), new HTuple("false", "false"));
        //                        string text = $"[{grayval.D:000}";
        //                        for (int i = 1; i < grayval.Length; i++)
        //                        {
        //                            text += $", {grayval[i].D:000}";
        //                        }
        //                        text += "]";
        //                        hWindowControlWPF.HalconWindow.DispText(text, "window", 12, 12, "black", new HTuple("box", "shadow", "box_color", "shadow_color"), new HTuple("true", "false", "white", "orange"));
        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {

        //            }
        //        }
        //    }

        //    DateTime dateTime = DateTime.Now;
        //    /// <summary>
        //    ///放开鼠标
        //    /// </summary>
        //    private void HWindowControlWPF_HMouseUp(object sender, HMouseEventArgsWPF e)
        //    {
        //        lock (olock)
        //        {
        //            try
        //            {
        //                if (e.Button == System.Windows.Input.MouseButton.Middle)
        //                {
        //                    if (showHObjects.Count > 0)
        //                    {
        //                        //var dddd = (DateTime.Now - dateTime).TotalMilliseconds;
        //                        if ((DateTime.Now - dateTime).TotalMilliseconds < 300)
        //                        {
        //                            hWindowControlWPF.HalconWindow.ClearWindow();
        //                            _SetPart();
        //                            for (int i = 0; i < showHObjects.Count; i++)
        //                            {
        //                                _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                            }
        //                        }
        //                        dateTime = DateTime.Now;
        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {

        //            }
        //        }
        //    }
        //    /// <summary>
        //    /// 滚动滑轮，缩放图像
        //    /// </summary>
        //    private void HWindowControlWPF_HMouseWheel(object sender, HMouseEventArgsWPF e)
        //    {
        //        lock (olock)
        //        {
        //            try
        //            {
        //                if (showHObjects.Count > 0)
        //                {
        //                    hWindowControlWPF.HalconWindow.GetMpositionSubPix(out double mposRow, out double mposCol, out int button);
        //                    hWindowControlWPF.HalconWindow.GetPart(out int oldBeginRow, out int oldBeginCol, out int oldEndRow, out int oldEndCol);

        //                    int zoomBeginRow = 0, zoomBeginCol = 0, zoomEndRow = 0, zoomEndCol = 0;
        //                    if (e.Delta > 0)
        //                    {
        //                        zoomBeginRow = (int)(oldBeginRow + (mposRow - oldBeginRow) * 0.2d);
        //                        zoomBeginCol = (int)(oldBeginCol + (mposCol - oldBeginCol) * 0.2d);
        //                        zoomEndRow = (int)(oldEndRow - (oldEndRow - mposRow) * 0.2d);
        //                        zoomEndCol = (int)(oldEndCol - (oldEndCol - mposCol) * 0.2d);
        //                    }
        //                    else
        //                    {
        //                        zoomBeginRow = (int)(mposRow - (mposRow - oldBeginRow) / 0.8d);
        //                        zoomBeginCol = (int)(mposCol - (mposCol - oldBeginCol) / 0.8d);
        //                        zoomEndRow = (int)(mposRow + (oldEndRow - mposRow) / 0.8d);
        //                        zoomEndCol = (int)(mposCol + (oldEndCol - mposCol) / 0.8d);
        //                    }

        //                    hWindowControlWPF.HalconWindow.GetWindowExtents(out int windowRow, out int windowColumn, out int windowWidth, out int windowHeight);

        //                    bool outOfArea = zoomBeginRow >= imgHeight || zoomEndRow <= 0 || zoomBeginCol >= imgWidth || zoomEndCol <= 0;
        //                    bool outOfSize = (zoomEndRow - zoomBeginRow) > imgWidth * 20 || (zoomEndCol - zoomBeginCol) > imgWidth * 20;
        //                    bool outOfPixel = (windowHeight / (zoomEndRow - zoomBeginRow) > 500)
        //                        || (windowWidth / (zoomEndCol - zoomBeginCol)) > 500;

        //                    if (outOfArea || outOfSize)
        //                    {

        //                    }
        //                    else if (!outOfPixel)
        //                    {
        //                        hWindowControlWPF.HalconWindow.ClearWindow();
        //                        hWindowControlWPF.HalconWindow.SetPaint(new HTuple("default"));
        //                        hWindowControlWPF.HalconWindow.SetPart(zoomBeginRow, zoomBeginCol, zoomEndRow, zoomBeginCol + (zoomEndRow - zoomBeginRow) * windowWidth / windowHeight);
        //                        for (int i = 0; i < showHObjects.Count; i++)
        //                        {
        //                            _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);
        //                        }
        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {

        //            }
        //        }
        //    }


        //    static async Task _DispObjDelay(int delayTime)
        //    {

        //        await Task.Delay(delayTime); // 延时3秒

        //        sizeChangedDelayEventHandler();
        //    }

        //    private void sizeChangedDelayEvent()
        //    {

        //        for (int i = 0; i < showColors.Count; i++)
        //        {
        //            _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);

        //        }
        //    }
        //    private void HWindowControlWPF_SizeChanged(object sender, EventArgs e)
        //    {
        //        lock (olock)
        //        {
        //            try
        //            {
        //                //hWindowControlWPF.HalconWindow.ClearWindow();
        //                //hWindowControlWPF.HalconWindow.SetPaint(new HTuple("default"));

        //                _SetPart(showHImage);
        //                //for (int i = 0; i < showColors.Count; i++)
        //                //{
        //                //    _DispObj(showHObjects[i], showModes[i], showColors[i], false, false);

        //                //}
        //                // WPF界面更新需要化时间，延时更新
        //                _DispObjDelay(1);
        //            }
        //            catch (Exception)
        //            {

        //            }
        //        }
        //    }


    }
}
