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

        private PointCollection drawPoints = new PointCollection();

        public ImageControl2()
        {
            InitializeComponent();
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
            if (isDraw && drawPoints.Count > 0)
            {
                ////清空前面的显示图案
                //ClearChildren();

                //PointCollection points = new PointCollection();
                //for (int i = 0; i < drawPoints.Count; i += 1)
                //{
                //    points.Add(drawPoints[i]);
                //}

                ////添加当前点
                //mousePosImage = e.GetPosition((IInputElement)grid.Parent);

                //double x = (mousePosImage.X - leftRightSpace) / scaleX;
                //double y = (mousePosImage.Y - topBottomSpace) / scaleY;

                //points.Add(new Point(x, y));

                //if (points.Count > 1)
                //{
                //    AddPolyline(points, System.Windows.Media.Color.FromRgb(255, 0, 0));
                //}


            }
        }

        public void ClearChildren()
        {
            childrenImagePointsList.Clear();
            childrenImageXList.Clear();
            childrenImageYList.Clear();
            childrenList.Clear();
            canvas.Children.Clear();
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {                    
                mousePosImage = e.GetPosition((IInputElement)image.Parent);
                double x = (mousePosImage.X - leftRightSpace) / scaleX;
                double y = (mousePosImage.Y - topBottomSpace) / scaleY;
                byte b = 0;
                byte g = 0;
                byte r = 0;
                //获取图片像素
                {
                    var bitmap = bitmapSource;
                    int width = bitmap.PixelWidth;
                    int height = bitmap.PixelHeight;
                    var pixelFormat = bitmap.Format;
                    int stride = (width * pixelFormat.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[height * stride];
                    bitmap.CopyPixels(pixels, stride, 0);

                    int index = (int)y * stride + (int)x * (pixelFormat.BitsPerPixel / 8);

                    if (pixelFormat == PixelFormats.Bgr24)
                    {
                        b = pixels[index];
                        g = pixels[index + 1];
                        r = pixels[index + 2];
                    }
                    else if (pixelFormat == PixelFormats.Gray8)
                    {
                        b = pixels[index];
                        g = pixels[index];
                        r = pixels[index];
                    }
                    if (pixelFormat == PixelFormats.Bgr32)
                    {
                        b = pixels[index];
                        g = pixels[index + 1];
                        r = pixels[index + 2];
                    }
                }
                

                //显示当前点坐标
                infoLabel.Content = $"x:{x:F2}  y:{y:F2}  r:{r}  g:{g}  b:{b}";
                if (isDraw)
                {
                    //添加当前点


                    drawPoints.Add(new Point(x, y));

                    //清空前面的显示图案
                    ClearChildren();

                    if (drawPoints.Count > 1)
                    {
                        AddPolyline(drawPoints, System.Windows.Media.Color.FromRgb(255, 0, 0));
                    }

                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (isDraw)
                {
                    isDraw = false;
                    //清空前面的显示图案
                    ClearChildren();

                    if (drawPoints.Count > 1)
                    {
                        AddPolyline(drawPoints, System.Windows.Media.Color.FromRgb(255, 255, 255));

                        finishCreatePolylineEventHandler(drawPoints);
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
            //放大一点，限制10000
            if (canvas.Children.Count > 10000)
            {
                canvas.Children.RemoveAt(0);
                childrenList.RemoveAt(0);
                childrenImageXList.RemoveAt(0);
                childrenImageYList.RemoveAt(0);
                childrenImagePointsList.RemoveAt(0);

            }


        }

        /// <summary>
        /// 添加字符
        /// </summary>
        /// <param name="children">输入字符</param>
        /// <param name="x">图像的x坐标</param>
        /// <param name="y">图像的y坐标</param>
        public void AddTextBlock(string meg, Color color, int imageX, int imageY, double fontSize = 12)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = meg;
            System.Windows.Media.Brush brush = new SolidColorBrush(color);
            textBlock.Foreground = brush;
            AddChildren(textBlock, imageX, imageY);

        }

        /// <summary>
        /// 添加线段图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="StrokeThickness"></param>
        public void AddCircle(Point point,int radio, Color color, int StrokeThickness = 2)
        {
            canvas.BeginInit();

            System.Windows.Shapes.Ellipse ellipse = new System.Windows.Shapes.Ellipse
            {
                
                Width = 2 * radio,
                Height = 2 * radio,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = StrokeThickness
            };
            
            AddChildren(ellipse, 0, 0);
            canvas.EndInit();

        }


        /// <summary>
        /// 添加线段图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="StrokeThickness"></param>
        public void AddPolyline(PointCollection points, Color color, int StrokeThickness = 2)
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

        }

        /// <summary>
        /// 添加多边形图案
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="model"></param>
        /// <param name="StrokeThickness"></param>
        public void AddPolygon(PointCollection points, Color color, string model = null, int StrokeThickness = 2)
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
        private void UpdataChildren(int imageX, int imageY, ref UIElement children , PointCollection points)
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

    }
}
