using Kitware.VTK;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        double[] xCoords;
        double[] yCoords;
        double[] zCoords;


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
            // 渲染
            vtkRenderWindowControl.RenderWindow.Render();
        }

        public void AddPointCloud(List<double> X, List<double> Y, List<double> Z)
        {
            InsertNextPoints(X.ToArray(), Y.ToArray(), Z.ToArray());
            show_cloud(_points,_colors_rgb);
            // 渲染
            vtkRenderWindowControl.RenderWindow.Render();

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
            //render.AddActor(scalarBar);
            render.SetViewport(0.0, 0.0, 1, 1);//显示范围
            render.ResetCamera();

            render.SetBackground(0.2, 0.3, 0.4);

            //视角相关
            render.GetActiveCamera().SetViewUp(0, 1, 0);

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
                        for (int i = 0; i < X.Length; i++)
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
    }
}
