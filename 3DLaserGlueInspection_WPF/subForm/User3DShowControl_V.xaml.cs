using HelixToolkit.Wpf;
using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// User3DShowControl_V.xaml 的交互逻辑
    /// </summary>
    public partial class User3DShowControl_V : UserControl
    {
        public User3DShowControl_V()
        {
            InitializeComponent();

            _colors_rgb.SetNumberOfComponents(3);

        }

        object olock = new object();
        vtkPoints _points = vtkPoints.New();
        vtkUnsignedCharArray _colors_rgb = vtkUnsignedCharArray.New();
        vtkAxesActor axes = vtkAxesActor.New();

        double[] xCoords;
        double[] yCoords;
        double[] zCoords;

        Task taskRefresh;
        bool bRefresh = false;
        public bool bShowing = false;
        bool bClose = false;
        int interCount = 1;


        /// <summary>
        /// 移除坐标轴
        /// </summary>
        public void RemoveCoord(vtkAxesActor axes)
        {
            if (axes != null)
            {
                vtkRenderWindow renderWindow = vtkRenderWindowControl.RenderWindow;
                vtkRenderer renderer = renderWindow.GetRenderers().GetFirstRenderer();

                renderer.RemoveActor(axes);
                renderer.GetRenderWindow().Render();
            }
        }


        public void ClearPointCloud()
        {
            var points = _points;
            var colors = _colors_rgb;

            lock (olock)
            {
                _points = vtkPoints.New();
                _colors_rgb = vtkUnsignedCharArray.New();
                _colors_rgb.SetNumberOfComponents(3);
                show_cloud(_points, _colors_rgb);
            }
            points.Dispose();
            colors.Dispose();
            RemoveCoord(axes);
            // 渲染
            vtkRenderWindowControl.RenderWindow.Render();
        }
        public void AddPoint(double X, double Y, double Z, double colorSalce)
        {
            InsertNextPoint(X, Y, Z, colorSalce);

        }

        /// <summary>
        /// 显示坐标系，pose必须是zyx
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Z"></param>
        /// <param name="RX"></param>
        /// <param name="RY"></param>
        /// <param name="RZ"></param>
        public void AddCoord(double X, double Y, double Z, double RX, double RY, double RZ,double scale)
        {
            double[] pos = { X, Y, Z };       // 位置
            double[] ori = { RX, RY, RZ };            // 绕Z轴旋转45度

            show_coord(axes, pos, ori, scale);
            // 渲染
            vtkRenderWindowControl.RenderWindow.Render();

        }


        public void AddPointCloud(List<double> X, List<double> Y, List<double> Z, List<double> colorSalce)
        {
            InsertNextPoints(X.ToArray(), Y.ToArray(), Z.ToArray(), colorSalce.ToArray());
            //show_cloud(_points,_colors_rgb);
            //// 渲染
            //vtkRenderWindowControl.RenderWindow.Render();

        }


        public void show_cloud(vtkPoints points, vtkUnsignedCharArray colors_rgb, double r = 1.0, double g = 1.0, double b = 1.0, float size = 4f)
        {
            vtkPolyData polydata = vtkPolyData.New();
            polydata.SetPoints(points);


            //设置点云的渲染标量
            polydata.GetPointData().SetScalars(colors_rgb);


            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInputConnection(polydata.GetProducerPort());

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());


            // 开启颜色渲染
            mapper.ScalarVisibilityOn();


            vtkActor actor = vtkActor.New();
            actor.SetMapper(mapper);


            vtkRenderer render = vtkRenderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer();

            //清空点云
            for (int i = 0; i < render.GetActors().GetNumberOfItems(); i++)
            {
                var item = render.GetActors().GetItemAsObject(i);
                render.RemoveActor((vtkActor)item);
                item.Dispose();
            }

            render.AddActor(actor);
            render.SetBackground(0.2, 0.3, 0.4);


            /// 改为z方向的俯视图

            // 1. 获取点云的包围盒 [xmin,xmax, ymin,ymax, zmin,zmax]
            double[] bounds = actor.GetBounds();

            // 2. 计算中心点
            double cx = (bounds[0] + bounds[1]) / 2.0;
            double cy = (bounds[2] + bounds[3]) / 2.0;
            double cz = (bounds[4] + bounds[5]) / 2.0;

            // 3. 计算合适的相机高度（比点云范围稍高一些）
            double rangeX = bounds[1] - bounds[0];
            double rangeY = bounds[3] - bounds[2];
            double maxRange = Math.Max(rangeX, rangeY);
            double cameraHeight = maxRange * 1.5; // 留一些边距

            // 4. 设置相机参数
            vtkCamera camera = render.GetActiveCamera();

            // 相机位置：在中心点正上方
            camera.SetPosition(cx, cy, cz + cameraHeight);

            // 焦点：点云中心
            camera.SetFocalPoint(cx, cy, cz);

            // ViewUp：Y 轴正方向为"上"
            camera.SetViewUp(0, 1, 0);

            // 平行投影（俯视图通常用正交投影，效果更像工程图纸）
            camera.ParallelProjectionOn();
            camera.SetParallelScale(maxRange / 2.0);

            // 5. 裁剪范围
            render.ResetCameraClippingRange();

            // 6. 渲染
            render.GetRenderWindow().Render();


        }

        public void show_coord(vtkAxesActor coord, double[] position,
         double[] orientation,
         double scale = 1.0)
        {
            coord.SetTotalLength(scale, scale, scale);
            coord.SetShaftTypeToCylinder();
            coord.SetCylinderRadius(0.2 * scale);
            coord.SetAxisLabels(1);

            //// 设置位姿
            //coord.SetPosition(position[0], position[1], position[2]);
            //coord.SetOrientation(orientation[0], orientation[1], orientation[2]);


            var t = vtkTransform.New();
            t.Translate(position[0], position[1], position[2]);
            t.RotateZ(orientation[2]);
            t.RotateY(orientation[1]);
            t.RotateX(orientation[0]);
            coord.SetUserTransform(t);


            vtkRenderer render = vtkRenderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer();

            render.AddActor(coord);
            render.GetRenderWindow().Render();

        }



        private void InsertNextColor(double colorSalce)
        {
            double d = colorSalce * 4;
            if (d <= 0)
            {
                _colors_rgb.InsertNextTuple3(0.0, 0.0, 255.0);
            }
            else if (d <= 1)
            {
                _colors_rgb.InsertNextTuple3(0.0, d * 255.0, 255.0);
            }
            else if (d <= 2)
            {
                _colors_rgb.InsertNextTuple3(0.0, 255.0, (2 - d) * 255.0);
            }
            else if (d <= 3)
            {
                _colors_rgb.InsertNextTuple3((d - 2) * 255.0, 255.0, 0.0);
            }
            else if (d <= 4)
            {
                _colors_rgb.InsertNextTuple3(255.0, (4 - d) * 255.0, 0.0);
            }
            else
            {
                _colors_rgb.InsertNextTuple3(255.0, 0.0, 0.0);
            }
        }


        public void InsertNextPoint(double x, double y, double z, double colorSalce)
        {
            if (!bClose)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lock (olock)
                    {
                        InsertNextColor(colorSalce);
                        _points.InsertNextPoint(x, y, z);
                    }
                }));
            }
        }
        public bool InsertNextPoints(double[] X, double[] Y, double[] Z)
        {
            try
            {
                if (X != null)
                {
                    double minZ = Z.Min();
                    double maxZ = Z.Max();
                    double range = maxZ - minZ;
                    lock (olock)
                    {
                        for (int i = 0; i < X.Length; i+=interCount)
                        {
                            _points.InsertNextPoint(X[i], Y[i], Z[i]);

                            double d = (Z[i] - minZ) / range;
                            InsertNextColor(d);
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool InsertNextPoints(double[] X, double[] Y, double[] Z, double[] colorSalce)
        {
            try
            {
                if (X != null)
                {
                    //double minZ = Z.Min();
                    //double maxZ = Z.Max();
                    //double range = maxZ - minZ;
                    lock (olock)
                    {
                        for (int i = 0; i < X.Length; i += interCount)
                        {
                            _points.InsertNextPoint(X[i], Y[i], Z[i]);

                            InsertNextColor(colorSalce[i]);
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public void RefreshPoints()
        {
            if (!bClose)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    vtkRenderer render = vtkRenderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer();
                    lock (olock)
                    {
                        long count = _points.GetNumberOfPoints();   
                        _points.Modified();
                        _colors_rgb.Modified();
                        render.ResetCamera();//视角不变，范围变化成设置值
                        vtkRenderWindowControl.RenderWindow.Render();
                    }
                }));
            }
        }

        public void RefreshOn(int time, bool autoSize)
        {
            if (!bRefresh)
            {
                bRefresh = true;
                vtkRenderer render = vtkRenderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer();
                taskRefresh = Task.Run(() =>
                {
                    try
                    {
                        while (bRefresh && bShowing && !bClose)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                lock (olock)
                                {
                                    if (bRefresh)
                                    {
                                        _points.Modified();
                                        _colors_rgb.Modified();
                                        if (autoSize) render.ResetCamera();//视角不变，范围变化成设置值
                                        vtkRenderWindowControl.RenderWindow.Render();
                                    }
                                }
                            });
                            Thread.Sleep(time);
                        }
                    }
                    catch (Exception ex)
                    {

                    }

                });
            }
        }
        public void RefreshOFF()
        {
            bRefresh = false;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            bShowing = true;
        }

        private void UserControl_ToolTipClosing(object sender, ToolTipEventArgs e)
        {

        }
    }
}
