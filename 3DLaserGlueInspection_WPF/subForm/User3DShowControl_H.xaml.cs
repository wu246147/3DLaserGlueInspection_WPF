using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HelixToolkit.Wpf;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// User3DShowControl_H.xaml 的交互逻辑
    /// </summary>
    public partial class User3DShowControl_H : UserControl
    {
        //private RenderWindowControl renderWindowControl1;
        public User3DShowControl_H()
        {
            InitializeComponent();
            //this.renderWindowControl1 = new RenderWindowControl();

        }

        PointsVisual3D pointCloud;

        public void ClearPointCloud()
        {
            hv3d.Children.Clear();
        }
        public void AddPointCloud(List<System.Windows.Media.Media3D.Point3D> points, System.Windows.Media.Color Color)
        {
            //double scaleFactor;
            double minZ = points.Min(p => p.Z);
            double maxZ = points.Max(p => p.Z);
            double zRange = maxZ - minZ;

            points = points.Select(p => new System.Windows.Media.Media3D.Point3D(p.X, p.Y, p.Z)).ToList();

            pointCloud = new PointsVisual3D
            {
                Points = new Point3DCollection(points),
                Size = 2,
                Color = Color
            };

            hv3d.Children.Add(pointCloud);

            hv3d.ZoomExtents(new Rect3D(0, 0, minZ - zRange * 0.1, 1, 1, zRange * 1.2));

            // 获取HelixViewport3D控件的默认相机
            var defaultCamera = hv3d.Camera as System.Windows.Media.Media3D.PerspectiveCamera;

            // 设置相机的视角和位置
            defaultCamera.Position = new System.Windows.Media.Media3D.Point3D(0, 0, 200); // 设置相机位置
            defaultCamera.LookDirection = new Vector3D(0, 0, -1); // 设置相机观察方向
            defaultCamera.FieldOfView = 60; // 设置相机视角

            // 调整缩放比例999
            hv3d.ZoomExtents(); // 自动调整缩放比例以适应所有点的显示

        }

    }
}
