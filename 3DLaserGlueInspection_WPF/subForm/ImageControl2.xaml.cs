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
using static System.Net.Mime.MediaTypeNames;



//namespace Wpf_halcon
namespace _3DLaserGlueInspection.subForm
{
    public enum EventState
    {
        MouseDown,
        MouseUp,
        MouseMove,
        MouseWheel,
        None
    }

    public delegate void FinishCreatePolylineEventHandler(PointCollection drawPoints);



    #region 可交互矩形

    /// <summary>
    /// 矩形数据（图像坐标系）
    /// </summary>
    public class RectData
    {
        public string Id { get; set; }
        /// <summary>左上角 X（图像坐标）</summary>
        public double X { get; set; }
        /// <summary>左上角 Y（图像坐标）</summary>
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        /// <summary>旋转角度（度），绕中心顺时针</summary>
        public double Angle { get; set; }
        /// <summary>是否可拖动移动</summary>
        public bool IsDraggable { get; set; }
        /// <summary>是否可旋转</summary>
        public bool IsRotatable { get; set; }
        public Color StrokeColor { get; set; }
        public int StrokeThickness { get; set; }

        /// <summary>中心 X（图像坐标）</summary>
        public double CenterX => X + Width / 2.0;
        /// <summary>中心 Y（图像坐标）</summary>
        public double CenterY => Y + Height / 2.0;
    }

    internal enum RectHandleHit
    {
        None,
        Center,         // ← 原 Body，改为仅中心小圆点
        TopLeft, TopRight, BottomLeft, BottomRight,
        Top, Bottom, Left, Right,
        Rotation
    }

    internal class RectVisual
    {
        public string Id;
        public RectData Data;
        public Canvas Container;
        public System.Windows.Shapes.Rectangle Shape;
        public List<Ellipse> CornerHandles = new List<Ellipse>();
        public List<Ellipse> EdgeHandles = new List<Ellipse>();
        public Ellipse RotationHandle;
        public Line RotationLine;
        public Ellipse CenterDot;  // ← 新增：中心拖拽点
    }

    #endregion

    /// <summary>
    /// ImageControl2.xaml 的交互逻辑
    /// </summary>
    public partial class ImageControl2 : UserControl
    {

        private bool isMouseMove { get; set; } = false;

        private bool isDraw { get; set; } = false;

        public FinishCreatePolylineEventHandler finishCreatePolylineEventHandler;

        /// <summary>
        /// image控件的鼠标坐标
        /// </summary>
        private System.Windows.Point mousePosImage { get; set; }

        /// <summary>
        /// canvas控件的鼠标坐标
        /// </summary>
        private System.Windows.Point mousePosCanvas { get; set; }
        private bool IsUseCallback { get; set; } = false;
        private EventState eventState { get; set; } = EventState.None;
        private Action Callback { get; set; } = null;
        //private Image image { get; set; }


        private BitmapSource bitmapSource { get; set; }


        /// <summary>
        /// x方向放缩比例，控件尺寸除以图像尺寸
        /// </summary>
        private double scaleX { get; set; } = 1;
        /// <summary>
        /// y方向放缩比例，控件尺寸除以图像尺寸
        /// </summary>
        private double scaleY { get; set; } = 1;
        /// <summary>
        /// 图片距离控件左侧距离，按控件坐标系
        /// </summary>
        private double leftRightSpace { get; set; } = 0;
        /// <summary>
        /// 图片距离控件上侧距离，按控件坐标系
        /// </summary>
        private double topBottomSpace { get; set; } = 0;

        private List<UIElement> childrenList { get; set; } = new List<UIElement>();

        private List<int> childrenImageXList { get; set; } = new List<int>();

        private List<int> childrenImageYList { get; set; } = new List<int>();

        private List<PointCollection> childrenImagePointsList { get; set; } = new List<PointCollection>();

        private List<double> childrenCircleRadiusList = new List<double>();

        private PointCollection drawPoints = new PointCollection();

        #region 可拖拉矩形参数

        // ============ 矩形系统 ============
        private Canvas _rectCanvas;
        private List<RectVisual> _rectVisuals = new List<RectVisual>();

        // 交互状态
        private int _activeRectIndex = -1;
        private RectHandleHit _activeHandle = RectHandleHit.None;
        private Point _dragStartImg;
        private double _origX, _origY, _origW, _origH, _origAngle, _origCX, _origCY;

        // 视觉常量
        private const double HANDLE_SIZE = 8.0;
        private const double HANDLE_HIT_RADIUS = 12.0;
        private const double ROTATION_GAP = 25.0;
        private const double MIN_RECT_SIZE = 2.0;

        #endregion


        public ImageControl2()
        {
            InitializeComponent();


            // 创建矩形专用覆盖层（不影响原有 canvas）
            _rectCanvas = new Canvas
            {
                IsHitTestVisible = false   // 鼠标事件穿透，由代码手动命中检测
            };
            grid.Children.Add(_rectCanvas);

            AddHandlers(null);
            IsUseCallback = false;
        }

        public void startDraw()
        {
            isDraw = true;
            drawPoints.Clear();
            ClearChildren();
        }

        /// <summary>
        /// 初始化图像位置参数
        /// </summary>
        private void Img_ResizeSpaceCal()
        {
            // 获取Image控件的source

            if (bitmapSource != null)
            {
                // 获取图片的原始像素尺寸
                //int originalWidth = bitmapSource.PixelWidth;
                //int originalHeight = bitmapSource.PixelHeight;

                double originalWidth = bitmapSource.Width;
                double originalHeight = bitmapSource.Height;
                // 获取Image控件的当前尺寸
                double controlWidth = image.ActualWidth;
                double controlHeight = image.ActualHeight;

                // 获取 Canvas 的实际宽度和高度
                double canvasWidth = grid.ActualWidth;
                double canvasHeight = grid.ActualHeight;
                // 计算缩放比例
                scaleX = controlWidth / originalWidth;
                scaleY = controlHeight / originalHeight;
                double scale = Math.Min(scaleX, scaleY);
                // 计算缩放后的图像尺寸
                double scaledWidth = originalWidth * scale;
                double scaledHeight = originalHeight * scale;

                // 计算空间剩余
                double remainingWidth = canvasWidth - scaledWidth;
                double remainingHeight = canvasHeight - scaledHeight;

                // 如果需要分别获取左右和上下的剩余空间，可以这样计算
                leftRightSpace = remainingWidth / 2; // 假设均匀分布
                topBottomSpace = remainingHeight / 2; // 假设均匀分布
            }
        }

        /// <summary>
        /// 添加交互事件
        /// </summary>
        /// <param name="callback"></param>
        public void AddHandlers(Action callback = null)
        {
            grid.MouseDown += Image_MouseDown;
            grid.MouseUp += Image_MouseUp;
            grid.MouseMove += Image_MouseMove;
            image.MouseWheel += Image_MouseWheel;
            image.SizeChanged += Image_SizeChanged;
            this.Callback = callback;

            image.MouseDown += Canvas_MouseDown;
            image.MouseUp += Canvas_MouseUp;
            image.MouseMove += Canvas_MouseMove;

        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point gridPt = e.GetPosition((IInputElement)image.Parent);

            // ====== 矩形操作中 ======
            if (_activeRectIndex >= 0 && _activeRectIndex < _rectVisuals.Count
                && _activeHandle != RectHandleHit.None)
            {
                double imgX = (gridPt.X - leftRightSpace) / scaleX;
                double imgY = (gridPt.Y - topBottomSpace) / scaleY;
                RectVisual rv = _rectVisuals[_activeRectIndex];

                if (_activeHandle == RectHandleHit.Center)
                    Rect_ApplyMove(rv, imgX, imgY);
                else if (_activeHandle == RectHandleHit.Rotation)
                    Rect_ApplyRotation(rv, gridPt);
                else
                    Rect_ApplyResize(rv, _activeHandle, imgX, imgY);
                return;
            }

            // ====== 悬浮光标 ======
            if (!isDraw)
            {
                int hitIdx = Rect_HitTestAll(gridPt);
                if (hitIdx >= 0)
                {
                    RectHandleHit hit = Rect_HitTestSingle(_rectVisuals[hitIdx], gridPt);
                    this.Cursor = Rect_GetCursor(hit);
                }
                else
                {
                    this.Cursor = Cursors.Arrow;
                }
            }

            // 多段线实时预览（原逻辑，按需启用）
            // if (isDraw && drawPoints.Count > 0) { ... }
        }
        public void ClearChildren()
        {
            childrenImagePointsList.Clear();
            childrenImageXList.Clear();
            childrenImageYList.Clear();
            childrenList.Clear();
            canvas.Children.Clear();

            // 同步清除矩形引用（canvas.Children 已被清空）
            ClearAllRects();
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _activeHandle != RectHandleHit.None)
            {
                _activeHandle = RectHandleHit.None;
                _activeRectIndex = -1;
                image.ReleaseMouseCapture();
            }
        }
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Point gridPt = e.GetPosition((IInputElement)image.Parent);
                double imgX = (gridPt.X - leftRightSpace) / scaleX;
                double imgY = (gridPt.Y - topBottomSpace) / scaleY;

                // ====== 矩形交互（非绘制模式下） ======
                if (!isDraw)
                {
                    int hitIdx = Rect_HitTestAll(gridPt);
                    if (hitIdx >= 0)
                    {
                        RectVisual rv = _rectVisuals[hitIdx];
                        RectHandleHit hit = Rect_HitTestSingle(rv, gridPt);
                        if (hit != RectHandleHit.None)
                        {
                            _activeRectIndex = hitIdx;
                            _activeHandle = hit;
                            _dragStartImg = new Point(imgX, imgY);
                            _origX = rv.Data.X; _origY = rv.Data.Y;
                            _origW = rv.Data.Width; _origH = rv.Data.Height;
                            _origAngle = rv.Data.Angle;
                            _origCX = rv.Data.CenterX;
                            _origCY = rv.Data.CenterY;
                            image.CaptureMouse();
                            return;
                        }
                    }
                }

                // ====== 像素信息 ======
                {
                    byte b = 0, g = 0, r = 0;
                    if (bitmapSource != null)
                    {
                        var bmp = bitmapSource;
                        int w = bmp.PixelWidth, h = bmp.PixelHeight;
                        if (imgX >= 0 && imgX < w && imgY >= 0 && imgY < h)
                        {
                            var pf = bmp.Format;
                            int stride = (w * pf.BitsPerPixel + 7) / 8;
                            byte[] px = new byte[h * stride];
                            bmp.CopyPixels(px, stride, 0);
                            int idx = (int)imgY * stride + (int)imgX * (pf.BitsPerPixel / 8);
                            if (pf == PixelFormats.Bgr24 || pf == PixelFormats.Bgr32)
                            { b = px[idx]; g = px[idx + 1]; r = px[idx + 2]; }
                            else if (pf == PixelFormats.Gray8)
                            { b = g = r = px[idx]; }
                        }
                    }
                    if (infoLabel != null)
                        infoLabel.Content = $"x:{imgX:F2}  y:{imgY:F2}  r:{r}  g:{g}  b:{b}";
                }

                // ====== 多段线 ======
                if (isDraw)
                {
                    drawPoints.Add(new Point(imgX, imgY));
                    ClearChildren();
                    if (drawPoints.Count > 1)
                        AddPolyline(drawPoints, Color.FromRgb(255, 0, 0));
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                // 取消矩形操作
                if (_activeHandle != RectHandleHit.None)
                {
                    _activeHandle = RectHandleHit.None;
                    _activeRectIndex = -1;
                    image.ReleaseMouseCapture();
                    return;
                }

                // 多段线完成
                if (isDraw)
                {
                    isDraw = false;
                    ClearChildren();
                    if (drawPoints.Count > 1)
                    {
                        AddPolyline(drawPoints, Color.FromRgb(255, 255, 255));
                        finishCreatePolylineEventHandler?.Invoke(drawPoints);
                    }
                }
            }
        }
        private void Image_SizeChanged(object sender, SizeChangedEventArgs e)
        {

            var group = grid.RenderTransform as TransformGroup;
            var transform = group.Children[1] as TranslateTransform;
            transform.X = 0;
            transform.Y = 0;

            var transform1 = group.Children[0] as ScaleTransform;
            transform1.ScaleX = 1;
            transform1.ScaleY = 1;

            Img_ResizeSpaceCal();
            UpdataChildren();
            UpdateAllRectVisuals();  // ← 新增：同步更新矩形
        }

        public void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            eventState = EventState.MouseDown;
            if (e.ChangedButton == MouseButton.Middle)
            {
                isMouseMove = true;
                mousePosImage = e.GetPosition((IInputElement)grid.Parent);
                //mousePosCanvas = e.GetPosition((IInputElement)canvas.Parent);
                //图像捕抓鼠标
                grid.CaptureMouse();
                ////canvas捕抓鼠标
                //canvas.CaptureMouse();
            }

        }


        public void Image_MouseMove(object sender, MouseEventArgs e)
        {
            eventState = EventState.MouseMove;
            if (isMouseMove)
            {
                //图片平移
                {
                    var position = e.GetPosition((IInputElement)grid.Parent);
                    var group = grid.RenderTransform as TransformGroup;
                    var transform = group.Children[1] as TranslateTransform;
                    transform.X -= mousePosImage.X - position.X;
                    transform.Y -= mousePosImage.Y - position.Y;
                    mousePosImage = position;
                }
                ////canvas平移
                //{
                //    var position = e.GetPosition((IInputElement)canvas.Parent);
                //    var group = canvas.RenderTransform as TransformGroup;
                //    var transform = group.Children[1] as TranslateTransform;
                //    transform.X -= mousePosCanvas.X - position.X;
                //    transform.Y -= mousePosCanvas.Y - position.Y;
                //    mousePosCanvas = position;
                //}
            }

            if (IsUseCallback) Callback();
        }

        public void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            eventState = EventState.MouseUp;
            isMouseMove = false;
            grid.ReleaseMouseCapture();
            //canvas.ReleaseMouseCapture();
        }

        public void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            eventState = EventState.MouseWheel;
            var delta = e.Delta * 0.001;
            var group = grid.RenderTransform as TransformGroup;
            var transform = group.Children[0] as ScaleTransform;
            var previousPoint = e.GetPosition(grid);
            // 禁止无限缩小
            if (transform.ScaleX + delta < 0.1) return;
            transform.ScaleX += delta;
            transform.ScaleY += delta;

            var transform1 = group.Children[1] as TranslateTransform;
            // 当前位置加偏移量
            transform1.X += -1 * previousPoint.X * delta;
            transform1.Y += -1 * previousPoint.Y * delta;

            if (IsUseCallback) Callback();
        }

        public TransformGroup GetTransformGroup()
        {
            return this.grid.RenderTransform as TransformGroup;
        }

        public void ResetTransform(DependencyObject d)
        {
            // 同步调用
            d.Dispatcher.Invoke(new System.Action(() =>
            {
                TransformGroup group = new TransformGroup();
                ScaleTransform scale = new ScaleTransform();
                group.Children.Add(scale);
                TranslateTransform translate = new TranslateTransform();
                group.Children.Add(translate);
                RotateTransform rotate = new RotateTransform();
                group.Children.Add(rotate);
                grid.RenderTransform = group;
                // 刷新页面
                this.grid.UpdateLayout();
            }), System.Windows.Threading.DispatcherPriority.Background);

        }


        /// <summary>
        /// 添加图案
        /// </summary>
        /// <param name="children">输入图案</param>
        /// <param name="x">图像的x坐标</param>
        /// <param name="y">图像的y坐标</param>
        public void AddChildren(UIElement children, int imageX, int imageY, PointCollection points = null)
        {
            //
            UpdataChildren(imageX, imageY, ref children, points);

            canvas.Children.Add(children);
            childrenList.Add(children);
            childrenImageXList.Add(imageX);
            childrenImageYList.Add(imageY);
            childrenImagePointsList.Add(points);

            //限制最大个数，不能超过3000个
            if (canvas.Children.Count > 3000)
            {
                canvas.Children.RemoveAt(0);
                childrenList.RemoveAt(0);
                childrenImageXList.RemoveAt(0);
                childrenImageYList.RemoveAt(0);
                childrenImagePointsList.RemoveAt(0);

            }
        }

        public void RemoveChildren(UIElement children)
        {
            if (children == null)
            {
                return;
            }
            if (canvas.Children.Contains(children))
            {
                int id = canvas.Children.IndexOf(children);

                canvas.Children.RemoveAt(id);
                childrenList.RemoveAt(id);
                childrenImageXList.RemoveAt(id);
                childrenImageYList.RemoveAt(id);
                childrenImagePointsList.RemoveAt(id);
            }
        }


        public bool contains(UIElement children)
        {
            return canvas.Children.Contains(children);
        }

        /// <summary>
        /// 添加字符
        /// </summary>
        /// <param name="children">输入字符</param>
        /// <param name="x">图像的x坐标</param>
        /// <param name="y">图像的y坐标</param>
        public TextBlock AddTextBlock(string meg, Color color, int imageX, int imageY, double fontSize = 12)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = meg;
            System.Windows.Media.Brush brush = new SolidColorBrush(color);
            textBlock.Foreground = brush;
            AddChildren(textBlock, imageX, imageY);

            return textBlock;

        }

        /// <summary>
        /// 添加线段图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="StrokeThickness"></param>
        public Polygon AddCircle(Point point, int radio, Color color, int StrokeThickness = 2)
        {
            //canvas.BeginInit();

            //System.Windows.Shapes.Ellipse ellipse = new System.Windows.Shapes.Ellipse
            //{

            //    Width = 2 * radio,
            //    Height = 2 * radio,
            //    Fill = Brushes.Transparent,
            //    Stroke = new SolidColorBrush(color),
            //    StrokeThickness = StrokeThickness
            //};

            //AddChildren(ellipse, 0, 0);
            //canvas.EndInit();

            PointCollection points = new PointCollection();
            int segments = 64;
            for (int i = 0; i <= segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                points.Add(new Point(
                    point.X + radio * Math.Cos(angle),
                    point.Y + radio * Math.Sin(angle)));
            }
            Polygon polygon = AddPolygon(points, color, null, StrokeThickness);


            return polygon;
        }


        /// <summary>
        /// 添加线段图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="StrokeThickness"></param>
        public Polyline AddPolyline(PointCollection points, Color color, int StrokeThickness = 2)
        {
            canvas.BeginInit();
            Polyline polyline = new Polyline
            {
                Points = new PointCollection(),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };
            for (int i = 0; i < points.Count; i++)
            {
                Point point = new Point();
                point.X = points[i].X * scaleX;
                point.Y = points[i].Y * scaleY;
                polyline.Points.Add(point);
            }
            AddChildren(polyline, 0, 0, points);
            canvas.EndInit();
            return polyline;
        }

        /// <summary>
        /// 添加多边形图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="model"></param>
        /// <param name="StrokeThickness"></param>
        public Polygon AddPolygon(PointCollection points, Color color, string model = null, int StrokeThickness = 2)
        {
            Polygon polygon;
            polygon = new Polygon
            {
                Points = new PointCollection(),
            };
            if (model == "fill")
            {
                polygon.Fill = new SolidColorBrush(color);
            }
            else
            {
                polygon.Stroke = new SolidColorBrush(color);
                polygon.StrokeThickness = StrokeThickness;
            }
            for (int i = 0; i < points.Count; i++)
            {
                Point point = new Point();
                point.X = points[i].X * scaleX;
                point.Y = points[i].Y * scaleY;
                polygon.Points.Add(point);
            }
            AddChildren(polygon, 0, 0, points);
            return polygon;
        }

        /// <summary>
        /// 更新所有图案位置，主要是控件大小变化时用到
        /// </summary>
        public void UpdataChildren()
        {
            for (int i = 0; i < childrenList.Count; i++)
            {
                int imageX = childrenImageXList[i];
                int imageY = childrenImageYList[i];
                UIElement children = canvas.Children[i];
                PointCollection points = childrenImagePointsList[i];
                UpdataChildren(imageX, imageY, ref children, points);

            }
        }

        /// <summary>
        /// 更新图案位置
        /// </summary>
        /// <param name="imageX"></param>
        /// <param name="imageY"></param>
        /// <param name="children"></param>
        /// <param name="points"></param>
        private void UpdataChildren(int imageX, int imageY, ref UIElement children, PointCollection points)
        {
            double controlX = 0, controlY = 0;
            controlX = leftRightSpace + imageX * scaleX;
            controlY = topBottomSpace + imageY * scaleY;

            Canvas.SetLeft(children, controlX);
            Canvas.SetTop(children, controlY);

            if (children is Polyline)
            {
                ((Polyline)children).Points.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    Point point = new Point();
                    point.X = points[i].X * scaleX;
                    point.Y = points[i].Y * scaleY;
                    ((Polyline)children).Points.Add(point);
                }
            }

            if (children is Polygon)
            {
                ((Polygon)children).Points.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    Point point = new Point();
                    point.X = points[i].X * scaleX;
                    point.Y = points[i].Y * scaleY;
                    ((Polygon)children).Points.Add(point);
                }
            }

        }


        /// <summary>
        /// 设置图片
        /// </summary>
        /// <param name="bitmap">输入图片</param>
        public void SetImageSource(BitmapImage bitmap)
        {
            Clear();
            if (bitmap == null)
            {
                this.image.Source = null;
                bitmapSource = null;
            }
            else
            {
                this.image.Source = bitmap.Clone();
                bitmapSource = bitmap;
            }

            var group = grid.RenderTransform as TransformGroup;
            var transform = group.Children[1] as TranslateTransform;
            transform.X = 0;
            transform.Y = 0;

            var transform1 = group.Children[0] as ScaleTransform;
            transform1.ScaleX = 1;
            transform1.ScaleY = 1;

            Img_ResizeSpaceCal();
            UpdataChildren();
        }
        public void ClearImage()
        {
            this.image.Source = null;
            bitmapSource = null;
        }
        public void Clear()
        {
            ClearChildren();
            ClearImage();
        }

        #region 添加拖拉矩形框

        /// <summary>
        /// 添加普通矩形（左上角 + 宽高，图像坐标系）
        /// </summary>
        public RectData AddRect(string id, double x, double y, double width, double height,
            bool isDraggable, Color color, int strokeThickness = 2)
        {
            RemoveRect(id);  // 同ID覆盖

            RectData data = new RectData
            {
                Id = id,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Angle = 0,
                IsDraggable = isDraggable,
                IsRotatable = false,
                StrokeColor = color,
                StrokeThickness = strokeThickness
            };
            CreateRectVisual(data);
            return data;
        }

        /// <summary>
        /// 添加可旋转矩形（中心坐标 + 宽高 + 角度，图像坐标系）
        /// </summary>
        public RectData AddRotatedRect(string id, double cx, double cy, double width, double height,
            double angle, bool isDraggable, Color color, int strokeThickness = 2)
        {
            RemoveRect(id);

            RectData data = new RectData
            {
                Id = id,
                X = cx - width / 2.0,
                Y = cy - height / 2.0,
                Width = width,
                Height = height,
                Angle = angle,
                IsDraggable = isDraggable,
                IsRotatable = true,
                StrokeColor = color,
                StrokeThickness = strokeThickness
            };
            CreateRectVisual(data);
            return data;
        }

        /// <summary>
        /// 按 ID 删除矩形
        /// </summary>
        public bool RemoveRect(string id)
        {
            int idx = _rectVisuals.FindIndex(rv => rv.Id == id);
            if (idx < 0) return false;

            _rectCanvas.Children.Remove(_rectVisuals[idx].Container);
            _rectVisuals.RemoveAt(idx);

            if (_activeRectIndex == idx)
            { _activeRectIndex = -1; _activeHandle = RectHandleHit.None; }
            else if (_activeRectIndex > idx)
                _activeRectIndex--;

            return true;
        }

        /// <summary>
        /// 清除所有矩形
        /// </summary>
        public void ClearAllRects()
        {
            _rectCanvas.Children.Clear();
            _rectVisuals.Clear();
            _activeRectIndex = -1;
            _activeHandle = RectHandleHit.None;
        }

        /// <summary>
        /// 获取指定矩形数据（引用，坐标与图像绑定）
        /// </summary>
        public RectData GetRectData(string id)
        {
            var rv = _rectVisuals.FirstOrDefault(r => r.Id == id);
            return rv?.Data;
        }

        /// <summary>
        /// 矩形是否存在
        /// </summary>
        public bool RectExists(string id)
        {
            return _rectVisuals.Any(r => r.Id == id);
        }

        /// <summary>
        /// 获取所有矩形 ID
        /// </summary>
        public List<string> GetAllRectIds()
        {
            return _rectVisuals.Select(rv => rv.Id).ToList();
        }

        /// <summary>
        /// 更新矩形位置（左上角，图像坐标系）
        /// </summary>
        public bool SetRectPosition(string id, double x, double y)
        {
            var rv = _rectVisuals.FirstOrDefault(r => r.Id == id);
            if (rv == null) return false;
            rv.Data.X = x;
            rv.Data.Y = y;
            UpdateRectVisual(rv);
            return true;
        }

        /// <summary>
        /// 更新矩形大小
        /// </summary>
        public bool SetRectSize(string id, double width, double height)
        {
            var rv = _rectVisuals.FirstOrDefault(r => r.Id == id);
            if (rv == null) return false;
            rv.Data.Width = width;
            rv.Data.Height = height;
            UpdateRectVisual(rv);
            return true;
        }

        /// <summary>
        /// 更新旋转矩形角度
        /// </summary>
        public bool SetRectAngle(string id, double angle)
        {
            var rv = _rectVisuals.FirstOrDefault(r => r.Id == id);
            if (rv == null) return false;
            rv.Data.Angle = angle;
            UpdateRectVisual(rv);
            return true;
        }

        private Point Rect_ImgToCanvasPt(Point imgPt)
        {
            return new Point(
                imgPt.X * scaleX + leftRightSpace,
                imgPt.Y * scaleY + topBottomSpace);
        }

        private Point Rect_GridToImgPt(Point gridPt)
        {
            return new Point(
                (gridPt.X - leftRightSpace) / scaleX,
                (gridPt.Y - topBottomSpace) / scaleY);
        }

        private void CreateRectVisual(RectData data)
        {
            RectVisual rv = new RectVisual { Id = data.Id, Data = data };

            rv.Container = new Canvas { IsHitTestVisible = false };

            // 矩形本体
            rv.Shape = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(data.StrokeColor),
                StrokeThickness = data.StrokeThickness,
                Fill = new SolidColorBrush(Color.FromArgb(15,
                    data.StrokeColor.R, data.StrokeColor.G, data.StrokeColor.B)),
                IsHitTestVisible = false
            };
            rv.Container.Children.Add(rv.Shape);

            // 角点手柄（仅可拖动时创建）
            if (data.IsDraggable)
            {
                // 中心拖拽点
                double dotSize = HANDLE_SIZE * 1.2;
                rv.CenterDot = new Ellipse
                {
                    Width = dotSize,
                    Height = dotSize,
                    Fill = new SolidColorBrush(Color.FromArgb(180,
                        data.StrokeColor.R, data.StrokeColor.G, data.StrokeColor.B)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                rv.Container.Children.Add(rv.CenterDot);

                for (int i = 0; i < 4; i++)
                {
                    Ellipse h = new Ellipse
                    {
                        Width = HANDLE_SIZE,
                        Height = HANDLE_SIZE,
                        Fill = Brushes.White,
                        Stroke = new SolidColorBrush(data.StrokeColor),
                        StrokeThickness = 1.5,
                        IsHitTestVisible = false
                    };
                    rv.CornerHandles.Add(h);
                    rv.Container.Children.Add(h);
                }
                for (int i = 0; i < 4; i++)
                {
                    Ellipse h = new Ellipse
                    {
                        Width = HANDLE_SIZE * 0.7,
                        Height = HANDLE_SIZE * 0.7,
                        Fill = Brushes.White,
                        Stroke = new SolidColorBrush(data.StrokeColor),
                        StrokeThickness = 1,
                        IsHitTestVisible = false
                    };
                    rv.EdgeHandles.Add(h);
                    rv.Container.Children.Add(h);
                }
            }

            // 旋转手柄（仅可旋转时创建）
            if (data.IsRotatable)
            {
                rv.RotationLine = new Line
                {
                    Stroke = Brushes.Orange,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    IsHitTestVisible = false
                };
                rv.Container.Children.Add(rv.RotationLine);

                rv.RotationHandle = new Ellipse
                {
                    Width = HANDLE_SIZE * 1.3,
                    Height = HANDLE_SIZE * 1.3,
                    Fill = Brushes.Orange,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                rv.Container.Children.Add(rv.RotationHandle);
            }

            _rectCanvas.Children.Add(rv.Container);
            _rectVisuals.Add(rv);
            UpdateRectVisual(rv);
        }

        /// <summary>
        /// 更新单个矩形视觉（容器定位到中心，子元素相对中心布局，旋转围绕中心）
        /// </summary>
        private void UpdateRectVisual(RectVisual rv)
        {
            RectData d = rv.Data;

            double cx = d.CenterX * scaleX + leftRightSpace;
            double cy = d.CenterY * scaleY + topBottomSpace;
            double hw = d.Width * scaleX / 2.0;
            double hh = d.Height * scaleY / 2.0;

            // 容器定位到矩形中心
            Canvas.SetLeft(rv.Container, cx);
            Canvas.SetTop(rv.Container, cy);

            // 矩形本体（相对中心）
            rv.Shape.Width = hw * 2;
            rv.Shape.Height = hh * 2;
            Canvas.SetLeft(rv.Shape, -hw);
            Canvas.SetTop(rv.Shape, -hh);

            // 中心点定位到 (0,0)
            if (rv.CenterDot != null)
            {
                double dotR = rv.CenterDot.Width / 2.0;
                Canvas.SetLeft(rv.CenterDot, -dotR);
                Canvas.SetTop(rv.CenterDot, -dotR);
            }

            // 角点手柄: 0=TL 1=TR 2=BL 3=BR
            Point[] corners = { new Point(-hw, -hh), new Point(hw, -hh),
                        new Point(-hw, hh),  new Point(hw, hh) };
            for (int i = 0; i < rv.CornerHandles.Count; i++)
            {
                double s = HANDLE_SIZE / 2.0;
                Canvas.SetLeft(rv.CornerHandles[i], corners[i].X - s);
                Canvas.SetTop(rv.CornerHandles[i], corners[i].Y - s);
            }

            // 边中点手柄: 0=Top 1=Bottom 2=Left 3=Right
            Point[] edges = { new Point(0, -hh), new Point(0, hh),
                      new Point(-hw, 0), new Point(hw, 0) };
            for (int i = 0; i < rv.EdgeHandles.Count; i++)
            {
                double s = HANDLE_SIZE * 0.7 / 2.0;
                Canvas.SetLeft(rv.EdgeHandles[i], edges[i].X - s);
                Canvas.SetTop(rv.EdgeHandles[i], edges[i].Y - s);
            }

            // 旋转手柄（顶部延伸）
            if (d.IsRotatable && rv.RotationHandle != null)
            {
                rv.RotationLine.X1 = 0; rv.RotationLine.Y1 = -hh;
                rv.RotationLine.X2 = 0; rv.RotationLine.Y2 = -hh - ROTATION_GAP;

                double s = HANDLE_SIZE * 1.3 / 2.0;
                Canvas.SetLeft(rv.RotationHandle, -s);
                Canvas.SetTop(rv.RotationHandle, -hh - ROTATION_GAP - s);
            }

            // 旋转（围绕容器原点 = 矩形中心）
            rv.Container.RenderTransform = new RotateTransform(d.Angle, 0, 0);
        }

        private void UpdateAllRectVisuals()
        {
            foreach (var rv in _rectVisuals)
                UpdateRectVisual(rv);
        }

        private double Rect_Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 将网格坐标变换到矩形局部坐标（消除旋转影响）
        /// </summary>
        private Point Rect_ToLocal(RectVisual rv, Point gridPt)
        {
            double cx = rv.Data.CenterX * scaleX + leftRightSpace;
            double cy = rv.Data.CenterY * scaleY + topBottomSpace;
            double dx = gridPt.X - cx, dy = gridPt.Y - cy;
            double rad = -rv.Data.Angle * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return new Point(dx * c - dy * s, dx * s + dy * c);
        }

        /// <summary>
        /// 命中检测：遍历所有矩形，后添加的优先
        /// </summary>
        private int Rect_HitTestAll(Point gridPt)
        {
            for (int i = _rectVisuals.Count - 1; i >= 0; i--)
            {
                if (Rect_HitTestSingle(_rectVisuals[i], gridPt) != RectHandleHit.None)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 单个矩形命中检测
        /// </summary>
        private RectHandleHit Rect_HitTestSingle(RectVisual rv, Point gridPt)
        {
            if (!rv.Data.IsDraggable && !rv.Data.IsRotatable)
                return RectHandleHit.None;

            Point local = Rect_ToLocal(rv, gridPt);
            double hw = rv.Data.Width * scaleX / 2.0;
            double hh = rv.Data.Height * scaleY / 2.0;
            double hr = HANDLE_HIT_RADIUS;

            // 旋转手柄（优先级最高）
            if (rv.Data.IsRotatable)
            {
                double rotY = -hh - ROTATION_GAP;
                if (Rect_Dist(local.X, local.Y, 0, rotY) < hr * 1.5)
                    return RectHandleHit.Rotation;
            }

            if (rv.Data.IsDraggable)
            {

                // 中心小圆（取代原来的整个内部区域）
                double dotHitRadius = HANDLE_SIZE * 1.2;  // 与 CenterDot 尺寸匹配
                if (Rect_Dist(local.X, local.Y, 0, 0) < dotHitRadius)
                    return RectHandleHit.Center;

                // 角点
                if (Rect_Dist(local.X, local.Y, -hw, -hh) < hr) return RectHandleHit.TopLeft;
                if (Rect_Dist(local.X, local.Y, hw, -hh) < hr) return RectHandleHit.TopRight;
                if (Rect_Dist(local.X, local.Y, -hw, hh) < hr) return RectHandleHit.BottomLeft;
                if (Rect_Dist(local.X, local.Y, hw, hh) < hr) return RectHandleHit.BottomRight;

                // 边中点
                if (Rect_Dist(local.X, local.Y, 0, -hh) < hr) return RectHandleHit.Top;
                if (Rect_Dist(local.X, local.Y, 0, hh) < hr) return RectHandleHit.Bottom;
                if (Rect_Dist(local.X, local.Y, -hw, 0) < hr) return RectHandleHit.Left;
                if (Rect_Dist(local.X, local.Y, hw, 0) < hr) return RectHandleHit.Right;

                //// 内部（移动）
                //if (Math.Abs(local.X) <= hw && Math.Abs(local.Y) <= hh)
                //    return RectHandleHit.Body;
            }

            return RectHandleHit.None;
        }

        /// <summary>
        /// 移动矩形
        /// </summary>
        private void Rect_ApplyMove(RectVisual rv, double imgX, double imgY)
        {
            rv.Data.X = _origX + (imgX - _dragStartImg.X);
            rv.Data.Y = _origY + (imgY - _dragStartImg.Y);
            UpdateRectVisual(rv);
        }

        /// <summary>
        /// 缩放矩形（支持旋转矩形的局部坐标缩放，对角固定）
        /// </summary>
        private void Rect_ApplyResize(RectVisual rv, RectHandleHit handle, double imgX, double imgY)
        {
            // 鼠标位置转到「原始」局部坐标系（以拖拽起始时的中心和角度为基准）
            double dx = imgX - _origCX;
            double dy = imgY - _origCY;
            double rad = -_origAngle * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            double lx = dx * c - dy * s;
            double ly = dx * s + dy * c;

            double hw = _origW / 2.0, hh = _origH / 2.0;
            double nL = -hw, nR = hw, nT = -hh, nB = hh;

            switch (handle)
            {
                case RectHandleHit.TopLeft: nL = lx; nT = ly; break;
                case RectHandleHit.TopRight: nR = lx; nT = ly; break;
                case RectHandleHit.BottomLeft: nL = lx; nB = ly; break;
                case RectHandleHit.BottomRight: nR = lx; nB = ly; break;
                case RectHandleHit.Top: nT = ly; break;
                case RectHandleHit.Bottom: nB = ly; break;
                case RectHandleHit.Left: nL = lx; break;
                case RectHandleHit.Right: nR = lx; break;
            }

            // 最小尺寸约束
            if (nR - nL < MIN_RECT_SIZE)
            {
                if (nL != -hw) nL = nR - MIN_RECT_SIZE;
                else nR = nL + MIN_RECT_SIZE;
            }
            if (nB - nT < MIN_RECT_SIZE)
            {
                if (nT != -hh) nT = nB - MIN_RECT_SIZE;
                else nB = nT + MIN_RECT_SIZE;
            }

            double newW = nR - nL, newH = nB - nT;
            double newLocalCX = (nL + nR) / 2.0;
            double newLocalCY = (nT + nB) / 2.0;

            // 局部中心 → 全局中心
            double cosA = Math.Cos(_origAngle * Math.PI / 180.0);
            double sinA = Math.Sin(_origAngle * Math.PI / 180.0);
            double newCX = _origCX + newLocalCX * cosA - newLocalCY * sinA;
            double newCY = _origCY + newLocalCX * sinA + newLocalCY * cosA;

            // 中心 → 左上角
            rv.Data.X = newCX - newW / 2.0;
            rv.Data.Y = newCY - newH / 2.0;
            rv.Data.Width = newW;
            rv.Data.Height = newH;
            UpdateRectVisual(rv);
        }

        /// <summary>
        /// 旋转矩形（围绕中心）
        /// </summary>
        private void Rect_ApplyRotation(RectVisual rv, Point gridPt)
        {
            double cx = rv.Data.CenterX * scaleX + leftRightSpace;
            double cy = rv.Data.CenterY * scaleY + topBottomSpace;
            double dx = gridPt.X - cx;
            double dy = gridPt.Y - cy;
            // 旋转手柄默认在正上方 → atan2(dx, -dy) 从正上方顺时针计量
            rv.Data.Angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            UpdateRectVisual(rv);
        }

        /// <summary>
        /// 根据手柄类型返回光标
        /// </summary>
        private Cursor Rect_GetCursor(RectHandleHit hit)
        {
            switch (hit)
            {
                case RectHandleHit.TopLeft:
                case RectHandleHit.BottomRight: return Cursors.SizeNWSE;
                case RectHandleHit.TopRight:
                case RectHandleHit.BottomLeft: return Cursors.SizeNESW;
                case RectHandleHit.Top:
                case RectHandleHit.Bottom: return Cursors.SizeNS;
                case RectHandleHit.Left:
                case RectHandleHit.Right: return Cursors.SizeWE;
                case RectHandleHit.Rotation: return Cursors.Hand;
                case RectHandleHit.Center: return Cursors.SizeAll;
                default: return Cursors.Arrow;
            }
        }

        #endregion

    }
}