using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media;
using System.Windows.Automation.Peers;

namespace Wpf_Replace_halcon
{

   
    static class HFileIO
    {
        static public CameraParameters ReadCamPara(string filePath)
        {
            var parameters = new CameraParameters();
            string[] lines = File.ReadAllLines(filePath,Encoding.UTF8);

            foreach (var line in lines)
            {
                // 跳过注释行和空行 
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.Split(new[] { ' ' })[0] == "Focus")
                {
                    parameters.Focus = double.Parse(line.Split(new[] { ' ' })[1]);
                }
                else if (line.Split(new[] { ' ' })[0] == "Kappa")
                {
                    parameters.Kappa = double.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "Sx")
                {
                    parameters.Sx = double.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "Sy")
                {
                    parameters.Sy = double.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "Cx")
                {
                    parameters.Cx = double.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "Cy")
                {
                    parameters.Cy = double.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "ImageWidth")
                {
                    parameters.ImageWidth = int.Parse(line.Split(new[] { ' ' })[1]);

                }
                else if (line.Split(new[] { ' ' })[0] == "ImageHeight")
                {
                    parameters.ImageHeight = int.Parse(line.Split(new[] { ' ' })[1]);

                }
            }
            return parameters;
        }

        static public int WriteCamPara(string filePath,CameraParameters para)
        {
            string[] info= new string[8];
            info[0] = $"Focus {para.Focus}";
            info[1] = $"Kappa {para.Kappa}";
            info[2] = $"Sx {para.Sx}";
            info[3] = $"Sy {para.Sy}";

            info[4] = $"Cx {para.Cx}";
            info[5] = $"Cy {para.Cy}";
            info[6] = $"ImageWidth {para.ImageWidth}";
            info[7] = $"ImageHeight {para.ImageHeight}";

            File.WriteAllLines(filePath, info);
            return 0;
            
        }

        static public PoseParameters ReadPosePara(string filePath)
        {
            PoseParameters parameters = new PoseParameters();

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                // 跳过注释行和空行 
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.Split(new[] { ' ' })[0] == "f")
                {
                    parameters.PoseType = int.Parse(line.Split(new[] { ' ' })[1]);
                }
                else if (line.Split(new[] { ' ' })[0] == "t")
                {
                    parameters.x = double.Parse(line.Split(new[] { ' ' })[1]);
                    parameters.y = double.Parse(line.Split(new[] { ' ' })[2]);
                    parameters.z = double.Parse(line.Split(new[] { ' ' })[3]);


                }
                else if (line.Split(new[] { ' ' })[0] == "r")
                {
                    parameters.rx = double.Parse(line.Split(new[] { ' ' })[1]);
                    parameters.ry = double.Parse(line.Split(new[] { ' ' })[2]);
                    parameters.rz = double.Parse(line.Split(new[] { ' ' })[3]);


                }
            }
            return parameters;

        }


        static public int WritePosePara(string filePath, PoseParameters para)
        {

            string[] info = new string[6];
            info[0] = "# Used representation type:";
            info[1] = $"f {para.PoseType}";
            info[2] = "# Rotation angles [deg] or Rodriguez vector:";
            info[3] = $"r {para.rx} {para.ry} {para.rz}";
            info[4] = "# Translation vector (x y z [m]):";
            info[5] = $"t {para.x} {para.y} {para.z}";

            File.WriteAllLines(filePath, info, new UTF8Encoding(false));
            return 0;

        }
    }
}
