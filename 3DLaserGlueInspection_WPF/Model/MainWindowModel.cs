
//using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Xml.Serialization;
using RAIVASCS.Common;
using static _3DLaserGlueInspection.MainWindowModel;
using static _3DLaserGlueInspection.MainModel;
using System.Windows.Input;
using System.Drawing;
using System.Collections.ObjectModel;
using OpenCvSharp;
using System.Windows.Media.Imaging;
using _3DLaserGlueInspection.subForm;
using System.Windows.Media.Media3D;
using Wpf_Replace_halcon;
using System.Windows.Markup;
using HelixToolkit.Wpf;
using static HelixToolkit.Wpf.Viewport3DHelper;
using System.Windows.Interop;
using Newtonsoft.Json.Linq;

namespace _3DLaserGlueInspection
{
    public delegate void DispClearHWindowEventHandler();
    public delegate void DispImageHWindowEventHandler(BitmapImage img);
    public delegate void DispImageWithoutCloneHWindowEventHandler(BitmapImage img);
    public delegate void DispTextInImageHWindowEventHandler(string meg, System.Windows.Media.Color color, int imageX, int imageY, double fontSize = 12);
    public delegate void DispTextInWindowHWindowEventHandler(string meg, System.Windows.Media.Color color, int imageX, int imageY, double fontSize = 12);
    public delegate void DispPolylineHWindowEventHandler(PointCollection points, System.Windows.Media.Color color, int StrokeThickness = 2);
    public delegate void DispPolygonHWindowEventHandler(PointCollection points, System.Windows.Media.Color color, string model, int StrokeThickness = 2);

    public delegate void Disp3DPointEventHandler_H(List<System.Windows.Media.Media3D.Point3D> points, System.Windows.Media.Color Color);

    public delegate void Disp3DPointEventHandler_V(List<double> Xs, List<double> Ys, List<double> Zs);
    public delegate void Clear3DPointEventHandler();

    public delegate void RefreshPointsEventHandler();
    public delegate void RefreshOnEventHandler(int time, bool autoSize);
    public delegate void RefreshOFFEventHandler();

    public class MainWindowModel : NotifyBase
    {
        public MainModel mainModel { get; set; } = new MainModel();

        public event DispImageHWindowEventHandler DispImageHWindowNumericalModelDiagramEvent;
        public event DispPolylineHWindowEventHandler DispPolylineHWindowNumericalModelDiagramEvent;

        public event DispClearHWindowEventHandler DispClearHWindowControlEvent;
        public event DispImageWithoutCloneHWindowEventHandler DispImageWithoutCloneHWindowControlEvent;
        public event DispTextInImageHWindowEventHandler DispTextInImageHWindowControlEvent;
        public event DispTextInWindowHWindowEventHandler DispTextInWindowHWindowControlEvent;
        public event DispPolylineHWindowEventHandler DispPolylinejHWindowControlEvent;
        public event DispPolygonHWindowEventHandler DispPolygonjHWindowControlEvent;

        public event Disp3DPointEventHandler_V Disp3DPointControlEvent;
        public event Clear3DPointEventHandler Clear3DPointControlEvent;
        public event RefreshPointsEventHandler RefreshPointsEvent;
        public event RefreshOnEventHandler RefreshOnEvent;
        public event RefreshOFFEventHandler RefreshOFFEvent;


        Dictionary<string, string> labelColorEnum = new Dictionary<string, string>{
            { "gray", "Gray" },
            { "green", "LightGreen" },
            { "red", "Red" }
        };

        public enum LogType
        {
            normal = 0,
            ok = 1,
            ng = 2,
            warn = 3,
        };

        public bool stop = true;
        Thread mainThread = null;

        CarNameIdSet cars = new CarNameIdSet();
        CamParams Params = new CamParams();
        Dictionary<string, Cam> cams = new Dictionary<string, Cam>();
        //Vision vision = new Vision();
        JAKARobot robot = new JAKARobot();
        Mmf mmf = new Mmf();
        ISignal io;
        Dictionary<string, Setting> sets = new Dictionary<string, Setting>();

        object olockDataGridViewImageList = new object();
        Stopwatch watch = new Stopwatch();

        SynchronizedList<long> robotPoseKeys = new SynchronizedList<long>();
        SynchronizedList<Wpf_Replace_halcon.PoseParameters> robotPoseValues = new SynchronizedList<Wpf_Replace_halcon.PoseParameters>();
        Task taskRobot = null;

        public Dictionary<string, SynchronizedList<SynchronizedList<long>>> ImageKeys = new Dictionary<string, SynchronizedList<SynchronizedList<long>>>();//指示拍照位置
        Dictionary<string, SynchronizedList<Dictionary<long, Mat>>> Images = new Dictionary<string, SynchronizedList<Dictionary<long, Mat>>>();//相机-分段-时间-图片
        SynchronizedList<int> dataGridViewImageListRowsStartPoint = new SynchronizedList<int>();
        Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>> Robot3DPose = new Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>>();//相机-分段-时间-机器位姿
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DXs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();//相机-分段-时间-图片数据
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DYs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DZs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();
        public Dictionary<string, SynchronizedList<Dictionary<long, Mat>>> outLineDict = new Dictionary<string, SynchronizedList<Dictionary<long, Mat>>>();
        public Dictionary<string, SynchronizedList<Dictionary<long, Mat>>> glueRegionDict = new Dictionary<string, SynchronizedList<Dictionary<long, Mat>>>();
        public Dictionary<string, SynchronizedList<Dictionary<long, Mat>>> glueSmallRectRegionDict = new Dictionary<string, SynchronizedList<Dictionary<long, Mat>>>();
        public Dictionary<string, SynchronizedList<Dictionary<long, Data>>> glueDataDict = new Dictionary<string, SynchronizedList<Dictionary<long, Data>>>();
        public Dictionary<string, SynchronizedList<Dictionary<long, BResult>>> glueResultDict = new Dictionary<string, SynchronizedList<Dictionary<long, BResult>>>();

        public Dictionary<string, SynchronizedList<System.Windows.Size>> displaySize = new Dictionary<string, SynchronizedList<System.Windows.Size>>();

        Dictionary<string, Task> tasks = new Dictionary<string, Task>();//相机-处理任务

        Task taskPoint3D = null;
        Task taskShow3D = null;

        int indexImageCut = -1;//指示正在图像采集段数
        bool totalResult = true;

        public bool simulation = false;

        public string simulationPath = "";

        public void MainRun()
        {
            try
            {
                if (simulation)
                {
                    io = mmf;
                }
                else
                {
                    io = robot;
                }

                //加载参数
                #region 加载参数
                if (Params.Load())
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机参数加载成功"));
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机参数加载失败：") + Params.ErrMsg, LogType.ng);
                    return;
                }
                if (cars.Load())
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("产品配置参数加载成功"));
                    sets.Clear();
                    bool bLoad = true;
                    foreach (var item in cars.Cars.Values)
                    {
                        Setting set = new Setting(item.Name);
                        if (set.Load())
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("产品参数") + " " + item.Name + " " + GlobalVarAndFunc.LanguageTranslate("加载成功"));
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("产品参数") + " " + item.Name + " " + GlobalVarAndFunc.LanguageTranslate("加载失败：") + set.ErrMsg, LogType.ng);
                            bLoad = false;
                        }
                        sets.Add(item.Name, set);
                    }
                    if (!bLoad)
                    {
                        return;
                    }
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("产品配置参数加载失败：") + cars.ErrMsg, LogType.ng);
                    return;
                }
                if (robot.Load())
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人参数加载成功"));
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人参数加载失败：") + robot.ErrMsg, LogType.ng);
                    return;
                }
                if (io.Load())
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO参数加载成功"));
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO参数加载失败：") + io.ErrMsg, LogType.ng);
                    return;
                }
                #endregion

                //连接设备
                #region 连接设备
                if (!simulation)
                {
                    if (robot.Open())
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人连接成功"));

                        mainModel.robotCommunicationLabelColorControl = labelColorEnum["green"];
                    }
                    else
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人连接失败：") + robot.ErrMsg, LogType.ng);

                        mainModel.robotCommunicationLabelColorControl = labelColorEnum["red"];

                        return;
                    }
                }
                if (io.Open())
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO连接成功"));
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO连接失败：") + io.ErrMsg, LogType.ng);
                    return;
                }
                #endregion

                mainModel.softwareRunLabelColorControl = labelColorEnum["green"];
                while (!stop)
                {
                    if (!Write(DO.Running, false)) return;
                    if (!Write(DO.Triggering, false)) return;
                    //输出准备号好
                    if (!Write(DO.Ready, true)) return;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("输出Ready信号"));

                    //等待开始信号
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待开始信号"));
                    while (true)
                    {
                        bool val;
                        if (Read(DI.Start, out val))
                        {
                            if (val == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到开始信号"));
                                break;
                            }
                        }
                        else
                        {
                            return;
                        }
                        Thread.Sleep(60);
                        if (stop) return;
                    }

                    //清除数据
                    #region 清除数据
                    foreach (var item in Images.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    foreach (var item in outLineDict.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    foreach (var item in glueRegionDict.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    foreach (var item in glueSmallRectRegionDict.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    robotPoseKeys.Clear();
                    robotPoseValues.Clear();
                    ImageKeys.Clear();
                    Images.Clear();
                    dataGridViewImageListRowsStartPoint.Clear();
                    Robot3DPose.Clear();
                    Point3DXs.Clear();
                    Point3DYs.Clear();
                    Point3DZs.Clear();
                    outLineDict.Clear();
                    glueRegionDict.Clear();
                    glueSmallRectRegionDict.Clear();
                    glueDataDict.Clear();
                    glueResultDict.Clear();
                    displaySize.Clear();
                    tasks.Clear();
                    #endregion
                    //Invoke(new Action(() => { form3DShow.ClearCloud(); }));
                    //清空结果
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clear3DPointControlEvent();
                        DispClearHWindowControlEvent();
                    });

                    GC.Collect();

                    //清除信号
                    foreach (DO item in Enum.GetValues(typeof(DO)))
                    {
                        if (item == DO.Alive)
                        {
                            continue;
                        }
                        if (item == DO.Ready)
                        {
                            continue;
                        }
                        if (!Write(item, false)) return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainModel.ImageResultRecords.Clear();
                    });

                    //获取产品号
                    ushort ID = 0;
                    Car car = new Car();
                    if (Read(DI.CarNumber, out ID))
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到车型号为") + " " + ID);
                        bool isExist = false;
                        foreach (var item in cars.Cars.Values)
                        {
                            if (item.IDs.Contains(ID))
                            {
                                car = item;
                                isExist = true;
                                break;
                            }
                        }
                        if (!isExist)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在车型") + " " + ID, LogType.ng);
                            continue;
                        }
                    }
                    else
                    {
                        return;
                    }

                    //获取车架号VIN
                    string inVIN = "";

                    DateTime dateTime = DateTime.Now;
                    mainModel.productIDControl = ID.ToString();
                    mainModel.nameControl = car.Name;
                    mainModel.VINControl = inVIN;
                    mainModel.timeControl = dateTime.ToString("G");
                    mainModel.resultControl = "--";
                    mainModel.resultColorControl = "White";


                    //检测参数是否存在
                    string camParamName = car.CamParamName;
                    if (!Params.Param.TryGetValue(camParamName, out Dictionary<string, CamParam> camParam))
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在相机参数：") + camParamName, LogType.ng);
                        return;
                    }
                    if (!sets.TryGetValue(car.Name, out Setting set))
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在产品参数：") + car.Name, LogType.ng);
                        return;
                    }
                    if (stop) return;

                    //显示NumericalModelDiagram
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DispImageHWindowNumericalModelDiagramEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(set.image));
                    });
                    List<double>[] rowss = new List<double>[set.XLDDatas.Count];
                    List<double>[] colss = new List<double>[set.XLDDatas.Count];
                    List<double>[] angless = new List<double>[set.XLDDatas.Count];
                    for (int i = 0; i < set.XLDDatas.Count; i++)
                    {
                        int setCount = set.CutSets[i].EndImageIndex - set.CutSets[i].StartImageIndex + 1;
                        if (setCount < 1) setCount = 1;
                        Vision.XLDDataDivide(set.XLDDatas[i], setCount, out rowss[i], out colss[i], out angless[i]);

                        for (int j = 0; j < setCount; j++)
                        {
                            hWindowNumericalModelDiagramDispCross(rowss[i][j], colss[i][j], set.CutSets[i].Size, angless[i][j], Colors.Blue);
                        }
                    }
                    if (stop) return;

                    //连相机
                    foreach (var item in camParam)
                    {
                        if (item.Value.Enable)
                        {
                            if (!cams.TryGetValue(item.Value.CamName, out Cam cam))
                            {
                                cam = new Cam();
                                cams.Add(item.Value.CamName, cam);
                            }
                            if (!simulation)
                            {
                                if (!cam.IsOpen)
                                {
                                    if (cam.OpenBySN(item.Value.CamName))
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("打开成功"));
                                    }
                                    else
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("打开失败：") + cam.ErrMsg, LogType.ng);
                                        //Invoke(new Action(() =>
                                        //{
                                        //    e灯颜色 = 灯颜色.红;
                                        //    label相机.Refresh();
                                        //}));
                                        mainModel.camCommunicationLabelColorControl = labelColorEnum["red"];

                                        return;
                                    }
                                }
                                if (cam.InitSet(item.Value))
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("初始化设置成功"));
                                }
                                else
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("初始化设置失败：") + cam.ErrMsg, LogType.ng);
                                    //return;
                                }
                            }
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + " " + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("未启用"));
                        }
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机连接完成"));
                    mainModel.camCommunicationLabelColorControl = labelColorEnum["green"];


                    //初始化数据
                    foreach (var item in camParam)
                    {
                        if (item.Value.Enable)
                        {
                            if (Params.CamPar.ContainsKey(camParamName) && Params.CamPar[camParamName].ContainsKey(item.Key))
                            {
                                if (Params.LightInCam.ContainsKey(camParamName) && Params.LightInCam[camParamName].ContainsKey(item.Key))
                                {
                                    if (Params.ToolInCam.ContainsKey(camParamName) && Params.ToolInCam[camParamName].ContainsKey(item.Key))
                                    {
                                        if (Params.LightToCam.ContainsKey(camParamName) && Params.LightToCam[camParamName].ContainsKey(item.Key))
                                        {
                                            if (Params.CamToTool.ContainsKey(camParamName) && Params.CamToTool[camParamName].ContainsKey(item.Key))
                                            {
                                                var dictImageKey = new SynchronizedList<SynchronizedList<long>>();
                                                ImageKeys.Add(item.Key, dictImageKey);
                                                var dictImage = new SynchronizedList<Dictionary<long, Mat>>();
                                                Images.Add(item.Key, dictImage);

                                                var dictRobotPose = new SynchronizedList<Dictionary<long, PoseParameters>>();
                                                Robot3DPose.Add(item.Key, dictRobotPose);

                                                var dictX = new SynchronizedList<Dictionary<long, List<double>>>();
                                                Point3DXs.Add(item.Key, dictX);
                                                var dictY = new SynchronizedList<Dictionary<long, List<double>>>();
                                                Point3DYs.Add(item.Key, dictY);
                                                var dictZ = new SynchronizedList<Dictionary<long, List<double>>>();
                                                Point3DZs.Add(item.Key, dictZ);
                                                var dictXLD = new SynchronizedList<Dictionary<long, Mat>>();
                                                outLineDict.Add(item.Key, dictXLD);
                                                var dictRegion = new SynchronizedList<Dictionary<long, Mat>>();
                                                glueRegionDict.Add(item.Key, dictRegion);
                                                var dictRegionRectangle2 = new SynchronizedList<Dictionary<long, Mat>>();
                                                glueSmallRectRegionDict.Add(item.Key, dictRegionRectangle2);
                                                var dictData = new SynchronizedList<Dictionary<long, Data>>();
                                                glueDataDict.Add(item.Key, dictData);
                                                var dictResult = new SynchronizedList<Dictionary<long, BResult>>();
                                                glueResultDict.Add(item.Key, dictResult);

                                                displaySize.Add(item.Key, new SynchronizedList<System.Windows.Size>());
                                            }
                                            else
                                            {
                                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(CamToTool)不存在"), LogType.ng);
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(LightToCam)不存在"), LogType.ng);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("外参(ToolInCam.dat)不存在"), LogType.ng);
                                        return;
                                    }
                                }
                                else
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("外参(LightInCam.dat)不存在"), LogType.ng);
                                    return;
                                }
                            }
                            else
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("内参(camparam.cal)不存在"), LogType.ng);
                                return;
                            }
                        }
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("初始化数据成功"));

                    indexImageCut = -1;//指示正在图像采集段数
                    totalResult = true;
                    if (stop) return;

                    // 3D 每隔100毫秒再刷新一下结果
                    RefreshOnEvent(100, true);

                    watch.Restart();
                    bool bRobotRun = true;
                    //启动机器人姿态获取(安川20ms)
                    taskRobot = Task.Run(() =>
                    {
                        // 暂时屏蔽
                        //form3DShow.RefreshOn(10, true);
                        double colorUpperLimit = -0.5;
                        double colorLowerLimit = 0.5;
                        double rangeSize = colorLowerLimit - colorUpperLimit;
                        if (simulation)
                        {
                            SynchronizedList<long> robotPoseKeysSimulation = new SynchronizedList<long>();
                            SynchronizedList<Wpf_Replace_halcon.PoseParameters> robotPoseValuesSimulation = new SynchronizedList<Wpf_Replace_halcon.PoseParameters>();
                            string basePath = simulationPath;
                            try
                            {
                                string robotPoseKeysPath = $"{basePath}\\robotPoseKeys.xml";
                                if (File.Exists(robotPoseKeysPath))
                                {
                                    XmlSerializer xml = new XmlSerializer(typeof(SynchronizedList<long>));
                                    using (FileStream stream = new FileStream(robotPoseKeysPath, FileMode.OpenOrCreate))
                                    {
                                        var paramList = (SynchronizedList<long>)xml.Deserialize(stream);
                                        if (paramList != null)
                                        {
                                            robotPoseKeysSimulation = paramList;
                                        }
                                        else
                                        {
                                            ShowMessage(robotPoseKeysPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常"));
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    ShowMessage(robotPoseKeysPath + GlobalVarAndFunc.LanguageTranslate("文件不存在"));
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                ShowMessage(ex.ToString());
                                return;
                            }
                            try
                            {
                                string robotPoseValuesPath = $"{basePath}\\robotPoseValues.xml";
                                if (File.Exists(robotPoseValuesPath))
                                {
                                    XmlSerializer xml = new XmlSerializer(typeof(SynchronizedList<double[]>));
                                    using (FileStream stream = new FileStream(robotPoseValuesPath, FileMode.OpenOrCreate))
                                    {
                                        var paramList = (SynchronizedList<double[]>)xml.Deserialize(stream);
                                        if (paramList != null)
                                        {
                                            robotPoseValuesSimulation.Clear();
                                            //robotPoseValues = paramList;
                                            foreach (var pose in paramList)
                                            {
                                                PoseParameters posePara = new PoseParameters();
                                                posePara.x = pose[0];
                                                posePara.y = pose[1];
                                                posePara.z = pose[2];
                                                posePara.rx = pose[3];
                                                posePara.ry = pose[4];
                                                posePara.rz = pose[5];
                                                posePara.PoseType = (int)pose[6];
                                                robotPoseValuesSimulation.Add(posePara);
                                            }
                                        }
                                        else
                                        {
                                            System.Windows.Forms.MessageBox.Show(robotPoseValuesPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常"));
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    ShowMessage(robotPoseValuesPath + GlobalVarAndFunc.LanguageTranslate("文件不存在"));
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                ShowMessage(ex.ToString());
                                return;
                            }

                            for (int i = 0; i < robotPoseKeysSimulation.Count; i++)
                            {
                                if (!bRobotRun)
                                {
                                    break;
                                }
                                var key = robotPoseKeysSimulation[i];
                                var hPose = robotPoseValuesSimulation[i];
                                {
                                    robotPoseValues.Add(hPose);
                                    robotPoseKeys.Add(key);
                                }
                                //form3DShow.InsertNextPoint(hPose.RawData[0], hPose.RawData[1], hPose.RawData[2], (hPose.RawData[2].D - 颜色下限值) / 范围);
                                Thread.Sleep(20);
                            }
                        }
                        else
                        {
                            while (bRobotRun)
                            {
                                if (robot.ReadPose(out Wpf_Replace_halcon.PoseParameters hPose))
                                {
                                    var key = watch.ElapsedTicks;
                                    {
                                        robotPoseValues.Add(hPose);
                                        robotPoseKeys.Add(key);
                                    }
                                    //form3DShow.InsertNextPoint(hPose.RawData[0], hPose.RawData[1], hPose.RawData[2], (hPose.RawData[2].D - 颜色下限值) / 范围);
                                }
                                Thread.Sleep(20);
                                if (stop) break;
                            }
                        }
                    });
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人姿态获取任务启动完成"));

                    bool bTaskRun = true;
                    //启动图像处理任务
                    foreach (var item in camParam)
                    {
                        if (item.Value.Enable)
                        {
                            /// 相机内参，用于将像素坐标转为图像坐标
                            var hCamPar = Params.CamPar[camParamName][item.Key];
                            /// 用于将图像坐标转为激光坐标，应该是ImageToLight才对。
                            var LightInCam = Params.LightInCam[camParamName][item.Key];
                            /// 用于将激光坐标转为相机坐标
                            var LightToCam = Params.LightToCam[camParamName][item.Key];
                            /// 相机坐标转为法兰盘坐标
                            var CamToTool = Params.CamToTool[camParamName][item.Key];
                            tasks.Add(item.Key, Task.Run((Action)(() =>
                            {
                                while (indexImageCut < 0)//等待采集开始，数据集合完成添加
                                {
                                    Thread.Sleep(10);
                                    if (!bRobotRun) return;
                                    if (stop) return;
                                }
                                int indexRobotPose = 1;
                                int indexTaskCut = 0;//指示正在图像处理段数
                                while (true)//分段循环
                                {
                                    var dictImageKey = ImageKeys[item.Key][indexTaskCut];
                                    var dictImage = Images[item.Key][indexTaskCut];
                                    var dictRobotPose = Robot3DPose[item.Key][indexTaskCut];
                                    var dictX = Point3DXs[item.Key][indexTaskCut];
                                    var dictY = Point3DYs[item.Key][indexTaskCut];
                                    var dictZ = Point3DZs[item.Key][indexTaskCut];
                                    var dictXLD = outLineDict[item.Key][indexTaskCut];
                                    var dictRegion = glueRegionDict[item.Key][indexTaskCut];
                                    var dictRegionRectangle2 = glueSmallRectRegionDict[item.Key][indexTaskCut];
                                    var dictData = glueDataDict[item.Key][indexTaskCut];
                                    var dictResult = glueResultDict[item.Key][indexTaskCut];

                                    int indexImage = 0;
                                    bool bRun = true;
                                    while (bRun)//段内循环
                                    {
                                        bool bAdd = false;
                                        long imageKey = 0;
                                        if (dictImageKey.Count > indexImage)//有新增图片
                                        {
                                            if (robotPoseKeys.Count > indexRobotPose)//有新增姿态
                                            {
                                                if (robotPoseKeys[indexRobotPose] >= dictImageKey[indexImage])//循环到的姿态晚于等于图片，处理
                                                {
                                                    imageKey = dictImageKey[indexImage];
                                                    bAdd = true;
                                                }
                                                else//循环到的姿态早于图片，忽略
                                                {
                                                    indexRobotPose++;
                                                }
                                            }
                                            else
                                            {
                                                if (!bRobotRun)//退出条件
                                                {
                                                    return;
                                                }
                                            }
                                        }
                                        else//没有新增图片
                                        {
                                            if (indexImageCut > indexTaskCut)//进入下一段条件
                                            {
                                                indexTaskCut++;
                                                bRun = false;
                                            }
                                            else
                                            {
                                                if (!bRobotRun)//退出条件
                                                {
                                                    return;
                                                }
                                            }
                                            Thread.Sleep(10);
                                        }

                                        if (bAdd)
                                        {
                                            try
                                            {
                                                //int indexTaskCut传递 = indexTaskCut;//BeginInvoke用
                                                //int indexImage传递 = indexImage;//BeginInvoke用
                                                int camIndex = item.Key == "Cam1" ? 0 : item.Key == "Cam2" ? 1 : item.Key == "Cam3" ? 2 : 3;
                                                if (set.CutSets.Count > indexTaskCut && set.CutSets[indexTaskCut].imageSet.Count > camIndex && set.CutSets[indexTaskCut].imageSet[camIndex].Count > indexImage)
                                                {
                                                    var cutSet = set.CutSets[indexTaskCut];
                                                    var imageSet = set.CutSets[indexTaskCut].imageSet[camIndex][indexImage];
                                                    // 临时结果保存变量
                                                    bool getOutlineResult = false;
                                                    bool singleFrameExisOutline = false;
                                                    bool singleFrameExistGlue = false;
                                                    Data resultData = new Data();
                                                    BResult bResult = new BResult();
                                                    Mat outMaxRegion = new Mat();
                                                    Mat outRegionRectangle2 = new Mat();
                                                    Mat hXLDCont10mm = new Mat();
                                                    List<double> robotX, robotY, robotZ;
                                                    robotX = new List<double>();
                                                    robotY = new List<double>();
                                                    robotZ = new List<double>();

                                                    if (imageSet.轮廓检测)
                                                    {
                                                        //激光轮廓提取
                                                        Mat xy = new Mat();
                                                        Vision.getLaserPosition(dictImage[imageKey], imageSet.minThreshold, out xy, item.Value.OffsetX, item.Value.OffsetY);
                                                        //坐标转换
                                                        Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                                                        HMatrixTransform.mathHPose(robotPoseValues[indexRobotPose - 1],
                                                            robotPoseValues[indexRobotPose], out robotPose,
                                                            (imageKey - robotPoseKeys[indexRobotPose - 1]) /
                                                            (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1])
                                                            );
                                                        //三维数据添加机器人坐标
                                                        dictRobotPose.Add(imageKey, robotPose);

                                                        if (xy.Rows > 0)
                                                        {
                                                            getOutlineResult = true;
                                                           
                                                            Mat lightXY = new Mat();
                                                            Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                                                                robotPose, out lightXY, out robotX, out robotY, out robotZ);

                                                            //三维数据添加
                                                            dictX.Add(imageKey, robotX);
                                                            dictY.Add(imageKey, robotY);
                                                            dictZ.Add(imageKey, robotZ);

                                                            //if (imageSet.单帧检测)
                                                            //{
                                                            //    Mat detImage = dictImage[imageKey];
                                                            //    //单帧检测
                                                            //    Vision.singleFrameDetTotal(item.Value, hCamPar, LightInCam, detImage, cutSet, imageSet, ref singleFrameExistGlue, ref singleFrameExisOutline, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2, ref hXLDCont10mm, xy, lightXY);
                                                            //    if (!bResult.Result)
                                                            //    {
                                                            //        totalResult = false;
                                                            //    }
                                                            //    if (hXLDCont10mm.Rows > 0)
                                                            //    {
                                                            //        dictXLD.Add(imageKey, hXLDCont10mm);
                                                            //    }
                                                            //    if (resultData.面积 > 0)
                                                            //    {
                                                            //        dictRegion.Add(imageKey, outMaxRegion);
                                                            //        dictRegionRectangle2.Add(imageKey, outRegionRectangle2);
                                                            //        dictData.Add(imageKey, resultData);
                                                            //        dictResult.Add(imageKey, bResult);
                                                            //    }
                                                            //}
                                                        }
                                                    }

                                                    //界面更新：
                                                    //if (getOutlineResult)
                                                    //{
                                                    //    // 暂时屏蔽
                                                    //    //form3DShow.InsertNextPoints(robotX.DArr, robotY.DArr, robotZ.DArr, ((robotZ - cutSet.ShowColorMin / 1000) / ((cutSet.ShowColorMax - cutSet.ShowColorMin) / 1000)).DArr);
                                                    //    List<Point3D> points = new List<Point3D>();
                                                    //    for (int i = 0; i < robotX.Count; i++)
                                                    //    {
                                                    //        points.Add(new Point3D(robotX[i], robotY[i], robotZ[i]));
                                                    //    }
                                                    //    //Application.Current.Dispatcher.Invoke(() =>
                                                    //    //{
                                                    //    //    Disp3DPointControlEvent(points, Colors.Blue);
                                                    //    //});
                                                    //}
                                                    if (singleFrameExistGlue)
                                                    {
                                                        Application.Current.Dispatcher.Invoke(() =>
                                                        {
                                                            ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm, outMaxRegion, outRegionRectangle2, resultData, bResult);
                                                        });
                                                    }
                                                    if (imageSet.轮廓检测)
                                                    {
                                                        //if (imageSet.单帧检测)
                                                        //{
                                                        //    // 已开放
                                                        //    hWindowNumericalModelDiagramDispCross(rowss[indexTaskCut], colss[indexTaskCut], angless[indexTaskCut], indexImage, cutSet, bResult.Result ? Colors.Green : Colors.Red);

                                                        //}
                                                        //else
                                                        //{
                                                            // 已开放
                                                            hWindowNumericalModelDiagramDispCross(rowss[indexTaskCut], colss[indexTaskCut], angless[indexTaskCut], indexImage, cutSet, Colors.Yellow);
                                                        //}
                                                    }

                                                }
                                                else
                                                {
                                                    //无检测参数
                                                }
                                            }
                                            catch (Exception ex)
                                            {

                                            }
                                            indexImage++;
                                        }
                                        if (!bTaskRun) return;
                                        if (stop) return;
                                    }
                                    if (!bTaskRun) return;
                                    if (stop) return;
                                }
                            })));
                        }
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理任务启动完成"));

                    //启动三维图
                    taskShow3D = Task.Run(() =>
                    {
                        while (indexImageCut < 0)//等待采集开始，数据集合完成添加
                        {
                            Thread.Sleep(10);
                            if (!bRobotRun) return;
                            if (stop) return;
                        }
                        int indexTaskCut = 0;//指示正在图像处理段数
                        int intervalShow3d = 3;
                        List<int> indexImageList = new List<int> { 0, 0, 0, 0 };
                        while (true)//分段循环
                        {
                            //int indexImage = 0;
                            bool bRun = true;
                            while (bRun)//段内循环
                            {
                                try
                                {
                                    int indexCam = 0;
                                    //相机循环
                                    foreach (var item in camParam)
                                    {
                                        var dictImageKey = ImageKeys[item.Key][indexTaskCut];
                                        var dictX = Point3DXs[item.Key][indexTaskCut];
                                        var dictY = Point3DYs[item.Key][indexTaskCut];
                                        var dictZ = Point3DZs[item.Key][indexTaskCut];

                                        bool bAdd = false;
                                        long imageKey = 0;
                                        if (dictX.Count > indexImageList[indexCam])//有新增点云数据
                                        {
                                            imageKey = dictImageKey[indexImageList[indexCam]];
                                            bAdd = true;
                                        }
                                        else//没有新增图片
                                        {
                                            if (indexImageCut > indexTaskCut)//进入下一段条件
                                            {
                                                indexTaskCut++;
                                                bRun = false;
                                            }
                                            else
                                            {
                                                if (!bRobotRun)//退出条件
                                                {
                                                    return;
                                                }
                                            }
                                            Thread.Sleep(1);
                                        }

                                        if (bAdd)
                                        {
                                            if (dictX[imageKey].Count > 0 && indexImageList[indexCam] % intervalShow3d == 0)
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    //显示点云
                                                    Disp3DPointControlEvent(dictX[imageKey], dictY[imageKey], dictZ[imageKey]);
                                                });
                                            }
                                            indexImageList[indexCam]++;
                                        }
                                        indexCam++;
                                    }

                                    if (!bTaskRun) return;
                                    if (stop) return;
                                }
                                catch (Exception ex)
                                {

                                }
                            }
                            if (!bTaskRun) return;
                            if (stop) return;
                        }
                    });

                    //输出运行中信号
                    if (!Write(DO.Running, true)) return;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("输出Running信号"));
                    if (!Write(DO.Ready, false)) return;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("关闭Ready信号"));

                    if (stop) return;
                    bool bAbort = false;

                    //启动采集任务
                    while (true)
                    {
                        bool bEnd = false;
                        //等触发信号ON
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待触发信号ON"));
                        while (true)
                        {
                            bool val;
                            if (Read(DI.PGON, out val))
                            {
                                if (val == true)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号ON"));
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }

                            if (Read(DI.END, out val))
                            {
                                if (val == true)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                    bEnd = true;
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }
                            if (Read(DI.Abort, out val))
                            {
                                if (val == true)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                    bAbort = true;
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }
                            Thread.Sleep(1);
                        }
                        if (bEnd) break;
                        if (bAbort) break;

                        bool bTriggering = true;
                        //int 起点 = dataGridViewImageList.Rows.Count;
                        int startPoint = mainModel.ImageResultRecords.Count;
                        dataGridViewImageListRowsStartPoint.Add(startPoint);
                        //拍照
                        foreach (var item in camParam)
                        {
                            if (item.Value.Enable)
                            {
                                var dictImageKey = new SynchronizedList<long>();
                                ImageKeys[item.Key].Add(dictImageKey);
                                var dictImage = new Dictionary<long, Mat>(new Dictionary<long, Mat>());
                                Images[item.Key].Add(dictImage);

                                var dictRobotPose = new Dictionary<long, PoseParameters>();
                                Robot3DPose[item.Key].Add(dictRobotPose);

                                var dictX = new Dictionary<long, List<double>>();
                                Point3DXs[item.Key].Add(dictX);
                                var dictY = new Dictionary<long, List<double>>();
                                Point3DYs[item.Key].Add(dictY);
                                var dictZ = new Dictionary<long, List<double>>();
                                Point3DZs[item.Key].Add(dictZ);
                                var dictXLD = new Dictionary<long, Mat>();
                                outLineDict[item.Key].Add(dictXLD);
                                var dictRegion = new Dictionary<long, Mat>();
                                glueRegionDict[item.Key].Add(dictRegion);
                                var dictRegionRectangle2 = new Dictionary<long, Mat>();
                                glueSmallRectRegionDict[item.Key].Add(dictRegionRectangle2);
                                var dictData = new Dictionary<long, Data>();
                                glueDataDict[item.Key].Add(dictData);
                                var dictResult = new Dictionary<long, BResult>();
                                glueResultDict[item.Key].Add(dictResult);

                                var cam = cams[item.Value.CamName];
                                int segmentIndex = indexImageCut + 1;
                                bool CamEnabled = item.Key == "Cam1" ? set.CutSets[segmentIndex].Cam1Enabled :
                                    item.Key == "Cam2" ? set.CutSets[segmentIndex].Cam2Enabled :
                                    item.Key == "Cam3" ? set.CutSets[segmentIndex].Cam3Enabled :
                                    set.CutSets[segmentIndex].Cam4Enabled;
                                displaySize[item.Key].Add(new System.Windows.Size(set.CutSets[segmentIndex].ShowWidth, set.CutSets[segmentIndex].ShowHeight));
                                if (CamEnabled)
                                {
                                    if (!simulation)
                                    {
                                        bool flag = cam.KeepShot(new Action<Mat>(image =>
                                        {
                                            long key = watch.ElapsedTicks;
                                            {
                                                if (dictImageKey.Count < set.CutSets[segmentIndex].ImageNum)
                                                {
                                                    dictImage.Add(key, image);
                                                    dictImageKey.Add(key);
                                                    var dictImageKeyCount = dictImageKey.Count;


                                                    lock (olockDataGridViewImageList)
                                                    {
                                                        if (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount)
                                                        {
                                                            do
                                                            {
                                                                ImageResultRecord imageResultRecord = new ImageResultRecord();

                                                                switch (item.Key)
                                                                {
                                                                    case "Cam1":
                                                                        imageResultRecord.Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam2":
                                                                        imageResultRecord.Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam3":
                                                                        imageResultRecord.Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam4":
                                                                        imageResultRecord.Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        break;

                                                                    default:
                                                                        break;
                                                                }
                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                {
                                                                    mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                });
                                                            }
                                                            while (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount);
                                                        }
                                                        else
                                                        {
                                                            switch (item.Key)
                                                            {
                                                                case "Cam1":
                                                                    Application.Current.Dispatcher.Invoke(() =>
                                                                    {
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                    });
                                                                    break;
                                                                case "Cam2":
                                                                    Application.Current.Dispatcher.Invoke(() =>
                                                                    {
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                    });
                                                                    break;
                                                                case "Cam3":
                                                                    Application.Current.Dispatcher.Invoke(() =>
                                                                    {
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                    });
                                                                    break;
                                                                case "Cam4":
                                                                    Application.Current.Dispatcher.Invoke(() =>
                                                                    {
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                    });
                                                                    break;

                                                                default:
                                                                    break;
                                                            }
                                                        }
                                                    }

                                                }
                                            }
                                        }));
                                        if (flag)
                                        {
                                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("开始连续采集成功"));
                                        }
                                        else
                                        {
                                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("开始连续采集失败：") + cams[item.Value.CamName].ErrMsg, LogType.ng);
                                        }
                                    }
                                    else
                                    {
                                        string path = $"{simulationPath}\\{segmentIndex}\\{item.Key}";
                                        if (Directory.Exists(path))
                                        {
                                            Task.Run(() =>
                                            {
                                                var filePaths = Directory.GetFiles(path, "*.png").OrderBy(n => n).ToArray();
                                                for (int i = 0; i < filePaths.Length; i++)
                                                {
                                                    if (long.TryParse(Path.GetFileNameWithoutExtension(filePaths[i]), out long key))
                                                    {
                                                        if (dictImageKey.Count < set.CutSets[segmentIndex].ImageNum)
                                                        {
                                                            try
                                                            {
                                                                Mat image = new Mat(filePaths[i], ImreadModes.Unchanged);
                                                                dictImage.Add(key, image);
                                                                dictImageKey.Add(key);
                                                                var dictImageKeyCount = dictImageKey.Count;

                                                                lock (olockDataGridViewImageList)
                                                                {
                                                                    if (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount)
                                                                    {
                                                                        do
                                                                        {
                                                                            ImageResultRecord imageResultRecord = new ImageResultRecord();

                                                                            switch (item.Key)
                                                                            {
                                                                                case "Cam1":
                                                                                    imageResultRecord.Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam2":
                                                                                    imageResultRecord.Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam3":
                                                                                    imageResultRecord.Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam4":
                                                                                    imageResultRecord.Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    break;

                                                                                default:
                                                                                    break;
                                                                            }
                                                                            Application.Current.Dispatcher.Invoke(() =>
                                                                            {
                                                                                mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                            });
                                                                        }
                                                                        while (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount);
                                                                    }
                                                                    else
                                                                    {
                                                                        switch (item.Key)
                                                                        {
                                                                            case "Cam1":
                                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                                {
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                });
                                                                                break;
                                                                            case "Cam2":
                                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                                {
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                });
                                                                                break;
                                                                            case "Cam3":
                                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                                {
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                });
                                                                                break;
                                                                            case "Cam4":
                                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                                {
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                });
                                                                                break;

                                                                            default:
                                                                                break;
                                                                        }
                                                                    }
                                                                }


                                                            }
                                                            catch (Exception ex)
                                                            {
                                                            }
                                                        }
                                                    }
                                                    if (!bTriggering) { break; }
                                                    Thread.Sleep(1);
                                                }
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        indexImageCut++;

                        //输出拍照中信号
                        if (!Write(DO.Triggering, true)) return;

                        //等触发信号OFF
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待触发信号OFF"));
                        while (true)
                        {
                            if (stop) return;
                            bool val;
                            if (Read(DI.PGON, out val))
                            {
                                if (val == false)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号OFF"));
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }

                            if (Read(DI.END, out val))
                            {
                                if (val == true)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                    bEnd = true;
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }
                            if (Read(DI.Abort, out val))
                            {
                                if (val == true)
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                    bAbort = true;
                                    break;
                                }
                            }
                            else
                            {
                                return;
                            }
                            Thread.Sleep(1);
                        }


                        //数据转换
                        //pose 格式转换
                        string camKey = "Cam1";
                        foreach (var item in camParam) //根据使用的第一个相机数据来作为参考，正常是用第一个相机
                        {
                            if (item.Value.Enable)
                            {
                                camKey = item.Key;
                                break;
                            }
                        }
                        long[] imageKeyList = Point3DXs[camKey][indexImageCut].Keys.ToArray();
                        var cutSet = set.CutSets[indexImageCut].Clone();

                        Mat cloudList = new Mat(), poseList = new Mat();
                        poseList = Mat.Zeros(Robot3DPose[camKey][indexImageCut].Values.Count, 6, MatType.CV_64FC1);
                        int id = 0;
                        foreach (var poseKey in Robot3DPose[camKey][indexImageCut].Values)
                        {
                            poseList.At<Double>(id, 0) = poseKey.x;
                            poseList.At<Double>(id, 1) = poseKey.y;
                            poseList.At<Double>(id, 2) = poseKey.z;
                            poseList.At<Double>(id, 3) = poseKey.rx;
                            poseList.At<Double>(id, 4) = poseKey.ry;
                            poseList.At<Double>(id, 5) = poseKey.rz;
                            id++;
                        }
                        //3d 点云格式转换
                        int pointCount = 0;
                        foreach (var item in camParam)
                        {
                            if (item.Value.Enable)
                            {

                                foreach (var imageKey in Point3DXs[item.Key][indexImageCut].Keys)
                                {
                                    pointCount += Point3DXs[item.Key][indexImageCut][imageKey].Count();

                                }
                            }
                        }
                        cloudList = Mat.Zeros(pointCount, 3, MatType.CV_64FC1);
                        id = 0;
                        foreach (var item in camParam)
                        {
                            if (item.Value.Enable)
                            {
                                foreach (var imageKey in Point3DXs[item.Key][indexImageCut].Keys)
                                {
                                    for (int j = 0; j < Point3DXs[item.Key][indexImageCut][imageKey].Count; j++)
                                    {
                                        double x = Point3DXs[item.Key][indexImageCut][imageKey][j];
                                        double y = Point3DYs[item.Key][indexImageCut][imageKey][j];
                                        double z = Point3DZs[item.Key][indexImageCut][imageKey][j];

                                        cloudList.At<Double>(id, 0) = x;
                                        cloudList.At<Double>(id, 1) = y;
                                        cloudList.At<Double>(id, 2) = z;

                                        id++;
                                    }

                                }
                            }
                        }

                        //点云数据处理
                        taskPoint3D = Task.Run(() =>
                        {
                            //Console.WriteLine($"points count:{Xs.Count}.");
                            //if(Xs.Count > 0)
                            //{
                            //    ////保存机器人每张图的位姿数据
                            //    //foreach (var camKey in Robot3DPose.Keys)
                            //    //{
                            //    //    for (int i = 0; i < Robot3DPose[camKey].Count; i++)
                            //    //    {
                            //    //        using (FileStream stream = new FileStream($"{camKey}_{(i+1).ToString()}_robotPoseValues.xml", FileMode.Create))
                            //    //        {
                            //    //            //转化
                            //    //            List<double[]> pose = new List<double[]>();
                            //    //            foreach (var poseKey in Robot3DPose[camKey][i].Values)
                            //    //            {
                            //    //                pose.Add(new double[] { poseKey.x, poseKey.y, poseKey.z, poseKey.rx, poseKey.ry, poseKey.rz, poseKey.PoseType });
                            //    //            }
                            //    //            new XmlSerializer(pose.GetType()).Serialize(stream, pose);
                            //    //        }
                            //    //    }
                            //    //}
                            //    ////保存测试点云数据
                            //    //string fPath = "pointCloud.xml";
                            //    //XmlSerializer xml = new XmlSerializer(pointsSave.GetType());
                            //    //using (FileStream stream = new FileStream(fPath, FileMode.Create))
                            //    //{
                            //    //    xml.Serialize(stream, pointsSave);
                            //    //}
                            //}

                            if (poseList.Rows > 0 && cloudList.Rows > 0)
                            {
                                //3d检测
                                
                                    Mat[] imgList = new Mat[poseList.Rows];
                                    var dictXLD = outLineDict[camKey][indexImageCut];
                                    var dictRegion = glueRegionDict[camKey][indexImageCut];
                                    var dictRegionRectangle2 = glueSmallRectRegionDict[camKey][indexImageCut];
                                    var dictData = glueDataDict[camKey][indexImageCut];
                                    var dictResult = glueResultDict[camKey][indexImageCut];

                                    IntPtr[] imgsPtr = new IntPtr[imgList.Length];
                                    for (int i = 0; i < imgList.Length; i++)
                                    {
                                        imgList[i] = new Mat();
                                        imgsPtr[i] = imgList[i].CvPtr;
                                    }
                                    Vision.pointCloudCutAll(cloudList.CvPtr, poseList.CvPtr, Vision.xSize, Vision.ySize, Vision.zSize, Vision.scaleSize * 1000, Vision.offset_z, imgsPtr);
                                    for (int indexImage = 0; indexImage < imgList.Length; indexImage++)
                                    {
                                    //3d检测参数，先以相机1为标准,后面再把参数统一
                                    var imageSet = cutSet.imageSet[0][indexImage];


                                    if (imageSet._3DGlueDet)
                                    {
                                        var imageKey = imageKeyList[indexImage];
                                        //需要判断图片是否为空，来判断是否有结果
                                        if (!imgList[indexImage].Empty())
                                        {
                                            Mat thinn = new Mat();
                                            Mat points = new Mat();
                                            Vision.thinning3d(imgList[indexImage].CvPtr, thinn.CvPtr, points.CvPtr);

                                            //需要判断图片是否为空，来判断是否有结果
                                            if (!thinn.Empty())
                                            {
                                                //检测
                                                bool singleFrameExistGlue = false;
                                                Data resultData = new Data();
                                                BResult bResult = new BResult();
                                                Mat outMaxRegion = new Mat();
                                                Mat outRegionRectangle2 = new Mat();
                                                Mat hXLDCont10mm;
                                                

                                                //离散滤波
                                                if (imageSet.离散去噪)
                                                {
                                                    Vision.TrajectoryDiscreteFilter(points, out hXLDCont10mm, imageSet.分段距离 * Vision.scaleSize, imageSet.成段点数);
                                                }
                                                else
                                                {
                                                    hXLDCont10mm = points.Clone();
                                                }

                                                Vision.singleFrameDetAndResult(points, imageSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);
                                                if (!bResult.Result)
                                                {
                                                    totalResult = false;
                                                }
                                                if (hXLDCont10mm.Rows > 0)
                                                {
                                                    if (dictXLD.ContainsKey(imageKey))
                                                    {
                                                        dictXLD[imageKey] = hXLDCont10mm;
                                                    }
                                                    else
                                                    {
                                                        dictXLD.Add(imageKey, hXLDCont10mm);
                                                    }

                                                }
                                                if (resultData.面积 > 0)
                                                {
                                                    if (dictRegion.ContainsKey(imageKey))
                                                    {
                                                        dictRegion[imageKey] = outMaxRegion;
                                                    }
                                                    else
                                                    {
                                                        dictRegion.Add(imageKey, outMaxRegion);
                                                    }
                                                    if (dictRegionRectangle2.ContainsKey(imageKey))
                                                    {
                                                        dictRegionRectangle2[imageKey] = outRegionRectangle2;
                                                    }
                                                    else
                                                    {
                                                        dictRegionRectangle2.Add(imageKey, outRegionRectangle2);
                                                    }
                                                    if (dictData.ContainsKey(imageKey))
                                                    {
                                                        dictData[imageKey] = resultData;
                                                    }
                                                    else
                                                    {
                                                        dictData.Add(imageKey, resultData);
                                                    }
                                                    if (dictResult.ContainsKey(imageKey))
                                                    {
                                                        dictResult[imageKey] = bResult;
                                                    }
                                                    else
                                                    {
                                                        dictResult.Add(imageKey, bResult);
                                                    }
                                                }

                                                //结果显示
                                                if (singleFrameExistGlue)
                                                {
                                                    Application.Current.Dispatcher.Invoke(() =>
                                                    {
                                                        ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10mm, outMaxRegion, outRegionRectangle2, resultData, bResult, (cutSet.ShowWidth - Vision.xSize * 1000) * Vision.scaleSize / 2, (cutSet.ShowHeight - Vision.ySize * 1000) * Vision.scaleSize / 2);
                                                    });
                                                }

                                            }
                                            else
                                            {
                                                totalResult = false;
                                            }
                                        }
                                        else
                                        {
                                            totalResult = false;
                                        }

                                    }
                                }


                            }
                        });

                        if (!simulation)
                        {
                            //停止拍照
                            foreach (var item in camParam)
                            {
                                if (item.Value.Enable)
                                {
                                    if (!cams[item.Value.CamName].IsGrabbing || cams[item.Value.CamName].StopGrabbing())
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("停止采集成功"));
                                    }
                                    else
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("停止采集失败：") + cams[item.Value.CamName].ErrMsg, LogType.ng);
                                    }
                                }
                            }

                        }
                        bTriggering = false;

                        //关闭拍照中信号
                        if (!Write(DO.Triggering, false)) return;

                        if (stop) return;
                        if (bEnd) break;
                        if (bAbort) break;

                        //判断段数是否足够
                        if (indexImageCut + 1 >= set.CutSets.Count)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("拍照段数足够") + $"({indexImageCut + 1}/{set.CutSets.Count})，" + GlobalVarAndFunc.LanguageTranslate("退出拍照循环"));
                            break;
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("拍照段数不足") + $"({indexImageCut + 1}/{set.CutSets.Count})，" + GlobalVarAndFunc.LanguageTranslate("继续拍照循环"));
                        }
                    }


                    if (stop) return;
                    if (bAbort) continue;

                    bRobotRun = false;
                    //等待机器人处理完成
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待机器人处理完成"));
                    while (!taskRobot.IsCompleted)
                    {
                        Thread.Sleep(10);
                        if (stop) return;
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人处理完成"));
                    //等待图像处理完成
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待图像处理完成"));
                    foreach (var item in tasks.Values)
                    {
                        while (!item.IsCompleted)
                        {
                            Thread.Sleep(10);
                            if (stop) return;
                        }
                    }
                    RefreshOFFEvent();
                    RefreshPointsEvent();

                    //等待3d显示完毕
                    while (!taskShow3D.IsCompleted)
                    {
                        Thread.Sleep(10);
                        if (stop) return;
                    }
                    //等待3d处理完成
                    while (!taskPoint3D.IsCompleted)
                    {
                        Thread.Sleep(10);
                        if (stop) return;
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理完成"));
                    // 暂时屏蔽
                    if (totalResult)
                    {
                        mainModel.OKCountControl++;
                    }
                    else
                    {
                        mainModel.NGCountControl++;
                    }
                    mainModel.totalCountControl = mainModel.OKCountControl + mainModel.NGCountControl;

                    mainModel.passRateControl = ((double)mainModel.OKCountControl * 100 / mainModel.totalCountControl).ToString("0.00") + "%";

                    mainModel.resultControl = totalResult ? "OK" : "NG";
                    mainModel.resultColorControl = totalResult ? "#FF06BD00" : "Red";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CarResultRecord carResultRecord = new CarResultRecord();
                        carResultRecord.CarDetTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        carResultRecord.CarID = ID.ToString();
                        carResultRecord.CarResult = totalResult ? "OK" : "NG";

                        mainModel.CarResultRecords.Insert(0, carResultRecord);

                    });

                    //机器人轨迹
                    {
                        //double[] X = new double[robotPoseValues.Count];
                        //double[] Y = new double[robotPoseValues.Count];
                        //double[] Z = new double[robotPoseValues.Count];
                        //int index = 0;
                        //foreach (var item in robotPoseValues)
                        //{
                        //    X[index] = item.RawData[0];
                        //    Y[index] = item.RawData[1];
                        //    Z[index] = item.RawData[2];
                        //    index++;
                        //}
                        //Form3DShow form3DShow = new Form3DShow();
                        //form3DShow.InsertNextPoints(X, Y, Z);
                        //form3DShow.ShowDialog();
                    }

                    //图像三维数据
                    {
                        //HTuple hTupleX = new HTuple();
                        //HTuple hTupleY = new HTuple();
                        //HTuple hTupleZ = new HTuple();
                        //foreach (var item in Point3DXs.Values)
                        //{
                        //    foreach (var item2 in item)
                        //    {
                        //        foreach (var item3 in item2.Values)
                        //        {
                        //            hTupleX.Append(item3);
                        //        }
                        //    }
                        //}
                        //foreach (var item in Point3DYs.Values)
                        //{
                        //    foreach (var item2 in item)
                        //    {
                        //        foreach (var item3 in item2.Values)
                        //        {
                        //            hTupleY.Append(item3);
                        //        }
                        //    }
                        //}
                        //foreach (var item in Point3DZs.Values)
                        //{
                        //    foreach (var item2 in item)
                        //    {
                        //        foreach (var item3 in item2.Values)
                        //        {
                        //            hTupleZ.Append(item3);
                        //        }
                        //    }
                        //}
                        //if (hTupleX.Length > 0)
                        //{
                        //    double[] X = hTupleX.DArr;
                        //    double[] Y = hTupleY.DArr;
                        //    double[] Z = hTupleZ.DArr;
                        //    Form3DShow form3DShow = new Form3DShow();
                        //    form3DShow.InsertNextPoints(X, Y, Z);
                        //    form3DShow.ShowDialog();
                        //}
                    }

                    if (!Write(DO.Running, false)) return;
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("关闭Running信号"));

                    //存图
                    if (!simulation)
                    {
                        if ((totalResult && set.OtherSet.SaveOKImage) || (!totalResult && set.OtherSet.SaveNGImage))
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("开始存图"));
                            try
                            {
                                string OKNG = totalResult ? "OK" : "NG";
                                string basePath = $"D:\\image\\{car.Name}\\{dateTime:yyyy-MM-dd HH_mm_ss} {OKNG} [{inVIN}]";
                                Directory.CreateDirectory(basePath);

                                foreach (var camValue in Images)//相机
                                {
                                    for (int i = 0; i < camValue.Value.Count; i++)//段数
                                    {
                                        if (camValue.Value[i].Count > 0)
                                        {
                                            string imageDirectory = $"{basePath}\\{i}\\{camValue.Key}";
                                            Directory.CreateDirectory(imageDirectory);
                                            foreach (var image in camValue.Value[i])//图片
                                            {
                                                //image.Value.WriteImage("png 1", 0, $"{imageDirectory}\\{image.Key:000000000000}.png");
                                                Cv2.ImWrite($"{imageDirectory}\\{image.Key:000000000000}.png", image.Value);

                                            }
                                        }
                                    }
                                }
                                using (FileStream stream = new FileStream($"{basePath}\\robotPoseKeys.xml", FileMode.Create))
                                {
                                    new XmlSerializer(robotPoseKeys.GetType()).Serialize(stream, robotPoseKeys);
                                }
                                using (FileStream stream = new FileStream($"{basePath}\\robotPoseValues.xml", FileMode.Create))
                                {
                                    //转化
                                    List<double[]> pose = new List<double[]>();
                                    foreach (var poseKey in robotPoseValues)
                                    {
                                        pose.Add(new double[] { poseKey.x, poseKey.y, poseKey.z, poseKey.rx, poseKey.ry, poseKey.rz, poseKey.PoseType });
                                    }
                                    new XmlSerializer(pose.GetType()).Serialize(stream, pose);
                                }
                                using (FileStream stream = new FileStream($"{basePath}\\robotPoseValues", FileMode.Create))
                                {
                                    new BinaryFormatter().Serialize(stream, robotPoseValues);
                                }
                            }
                            catch (Exception ex)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("存图异常：") + ex.ToString(), LogType.ng);
                            }
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("存图完成"));
                        }
                    }

                    if (simulation)
                    {
                        //return;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("流程异常：") + ex.ToString(), LogType.ng);
            }
            finally
            {
                robot.Close();
                io.Close();
                //关闭激光和相机
                foreach (var cam in cams.Values)
                {
                    if (cam.IsOpen)
                    {
                        if (cam.SetLine1Inverter(false))
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + "(" + cam.Name + ")" + GlobalVarAndFunc.LanguageTranslate("关闭激光成功"));
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + "(" + cam.Name + ")" + GlobalVarAndFunc.LanguageTranslate("关闭激光失败：") + cam.ErrMsg, LogType.ng);
                        }
                        if (cam.Close())
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + "(" + cam.Name + ")" + GlobalVarAndFunc.LanguageTranslate("关闭成功"));
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + "(" + cam.Name + ")" + GlobalVarAndFunc.LanguageTranslate("关闭失败：") + cam.ErrMsg, LogType.ng);
                        }
                    }
                }
                try
                {
                    //BeginInvoke(new Action(() =>
                    //{
                    //    button启停.Text = GlobalVarAndFunc.LanguageTranslate("启动");
                    //    button启停.Image = Resources._2;
                    //}));

                    mainModel.buttonRunContentControl = GlobalVarAndFunc.LanguageTranslate("启动");
                    mainModel.buttonRunTagControl = "\uE658";
                    //Invoke(new Action(() =>
                    //{
                    //    e灯颜色 = 灯颜色.红;
                    //    label软件.Refresh();
                    //}));

                    mainModel.softwareRunLabelColorControl = labelColorEnum["red"];

                }
                catch { }
            }
        }




        private void hWindowNumericalModelDiagramDispCross(double row, double col, double angle, double size, System.Windows.Media.Color color)
        {
            //if (!showing)
            //{
            //    showing = true;
            //    try
            //    {
            //        lock (olockShow)
            //        {
            Mat hXLDCont = new Mat();
            //生成交叉图案
            Application.Current.Dispatcher.Invoke(() =>
            {
                PointCollection Points1 = new PointCollection();
                PointCollection Points2 = new PointCollection();

                System.Windows.Point p1 = new System.Windows.Point(col + Math.Cos(angle / 180 * Math.PI) * size, row + Math.Sin(angle / 180 * Math.PI) * size);
                System.Windows.Point p2 = new System.Windows.Point(col + Math.Cos((angle + 180) / 180 * Math.PI) * size, row + Math.Sin((angle + 180) / 180 * Math.PI) * size);
                System.Windows.Point p3 = new System.Windows.Point(col + Math.Cos((angle + 90) / 180 * Math.PI) * size, row + Math.Sin((angle + 90) / 180 * Math.PI) * size);
                System.Windows.Point p4 = new System.Windows.Point(col + Math.Cos((angle + 270) / 180 * Math.PI) * size, row + Math.Sin((angle + 270) / 180 * Math.PI) * size);
                Points1.Add(p1);
                Points1.Add(p2);
                Points2.Add(p3);
                Points2.Add(p4);

                DispPolylineHWindowNumericalModelDiagramEvent(Points1, color, 2);
                DispPolylineHWindowNumericalModelDiagramEvent(Points2, color, 2);
            });
            hXLDCont.Dispose();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.ToString());
            //}
            //showing = false;
            //}

        }
        private void hWindowNumericalModelDiagramDispCross(List<double> rows, List<double> cols, List<double> angles, int indexImage, CutSet cutSet, System.Windows.Media.Color color)
        {
            int indexCross = indexImage - cutSet.StartImageIndex;
            if (indexCross >= 0 && indexCross < angles.Count)
            {
                hWindowNumericalModelDiagramDispCross(rows[indexCross], cols[indexCross], angles[indexCross], cutSet.Size, color);
            }
        }



        #region 信号读写
        bool Read(DI di, out bool val)
        {
            bool b失败过 = false;
            while (!stop)
            {
                if (io.Read(di, out val))
                {
                    if (b失败过)
                    {
                        ShowMessage($"{di}" + GlobalVarAndFunc.LanguageTranslate("信号重读成功"));
                    }
                    return true;
                }
                else
                {
                    if (!b失败过)
                    {
                        ShowMessage($"{di}" + GlobalVarAndFunc.LanguageTranslate("信号读取失败，重读中:") + io.ErrMsg, LogType.warn);
                        b失败过 = true;
                    }
                    if (!io.IsOpen)
                    {
                        io.Open();
                    }
                }
                Thread.Sleep(1000);
            }
            val = false;
            return false;
        }
        bool Read(DO dO, out bool val)
        {
            bool b失败过 = false;
            while (!stop)
            {
                if (io.Read(dO, out val))
                {
                    if (b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号重读成功"));
                    }
                    return true;
                }
                else
                {
                    if (!b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号读取失败，重读中:") + io.ErrMsg, LogType.warn);
                        b失败过 = true;
                    }
                    if (!io.IsOpen)
                    {
                        io.Open();
                    }
                }
                Thread.Sleep(1000);
            }
            val = false;
            return false;
        }
        bool Read(DI di, out ushort val)
        {
            bool b失败过 = false;
            while (!stop)
            {
                if (io.Read(di, out val))
                {
                    if (b失败过)
                    {
                        ShowMessage($"{di}" + GlobalVarAndFunc.LanguageTranslate("信号重读成功"));
                    }
                    return true;
                }
                else
                {
                    if (!b失败过)
                    {
                        ShowMessage($"{di}" + GlobalVarAndFunc.LanguageTranslate("信号读取失败，重读中:") + io.ErrMsg, LogType.warn);
                        b失败过 = true;
                    }
                    if (!io.IsOpen)
                    {
                        io.Open();
                    }
                }
                Thread.Sleep(1000);
            }
            val = 0;
            return false;
        }
        bool Read(DO dO, out ushort val)
        {
            bool b失败过 = false;
            while (!stop)
            {
                if (io.Read(dO, out val))
                {
                    if (b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号重读成功"));
                    }
                    return true;
                }
                else
                {
                    if (!b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号读取失败，重读中:") + io.ErrMsg, LogType.warn);
                        b失败过 = true;
                    }
                    if (!io.IsOpen)
                    {
                        io.Open();
                    }
                }
                Thread.Sleep(1000);
            }
            val = 0;
            return false;
        }
        bool Write(DO dO, object val)
        {
            bool b失败过 = false;
            while (!stop)
            {
                if (io.Write(dO, val))
                {
                    if (b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号重写成功"));
                    }
                    return true;
                }
                else
                {
                    if (!b失败过)
                    {
                        ShowMessage($"{dO}" + GlobalVarAndFunc.LanguageTranslate("信号写入失败，重写中:") + io.ErrMsg, LogType.warn);
                        b失败过 = true;
                    }
                    if (!io.IsOpen)
                    {
                        io.Open();
                    }
                }
                Thread.Sleep(1000);
            }
            return false;
        }
        #endregion


        public void ShowMessage(string mesage)
        {
            ShowMessage(mesage, LogType.normal);
        }

        public void ShowMessage(string mesage, LogType type)
        {
            DateTime dateTime = DateTime.Now;

            LogRecord logRecord = new LogRecord();
            logRecord.LogTime = dateTime.ToString("G");
            logRecord.LogInfo = mesage;

            switch (type)
            {
                case LogType.normal:
                    logRecord.LogResult = "normal";
                    break;
                case LogType.ok:
                    logRecord.LogResult = "ok";

                    break;
                case LogType.ng:
                    logRecord.LogResult = "ng";

                    break;
                case LogType.warn:
                    logRecord.LogResult = "warn";

                    break;
                default:
                    break;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                while (mainModel.LogRecords.Count > 1000)
                {
                    mainModel.LogRecords.RemoveAt(mainModel.LogRecords.Count - 1);
                }
                mainModel.LogRecords.Insert(0, logRecord);
            });

        }


        object olockShow = new object();
        bool showing = false;
        void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm)
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

                        DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布

                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0);
                            point.Y = hXLDCont10mm.At<double>(i, 1);
                            points.Add(point);
                        }
                        DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        //hWindowControl.DispImageWithoutClone(new HImage("byte", showWidth * 100, showHeight * 100));//扩画布
                        //hWindowControl.DispObj(hXLDCont10微米, null, "gray");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

        void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, BResult bResult, double offsetX = 0, double offsetY = 0)
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

                        DispImageWithoutCloneHWindowControlEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(mat));//扩画布

                        PointCollection points = new PointCollection();
                        for (int i = 0; i < hXLDCont10mm.Rows; i++)
                        {
                            System.Windows.Point point = new System.Windows.Point();
                            point.X = hXLDCont10mm.At<double>(i, 0) + offsetX;
                            point.Y = hXLDCont10mm.At<double>(i, 1) + offsetY;
                            points.Add(point);
                        }
                        DispPolylinejHWindowControlEvent(points, Colors.Gray);
                        if (!hRegion.Empty())
                        {
                            PointCollection regionPoints = new PointCollection();
                            for (int i = 0; i < hRegion.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegion.At<double>(i, 0) + offsetX;
                                point.Y = hRegion.At<double>(i, 1) + offsetY;
                                regionPoints.Add(point);
                            }

                            DispPolygonjHWindowControlEvent(regionPoints, Colors.Red, "fill");
                            PointCollection regionSmallestRectangle2Points = new PointCollection();
                            for (int i = 0; i < hRegionSmallestRectangle2.Rows; i++)
                            {
                                System.Windows.Point point = new System.Windows.Point();
                                point.X = hRegionSmallestRectangle2.At<double>(i, 0) + offsetX;
                                point.Y = hRegionSmallestRectangle2.At<double>(i, 1) + offsetY;
                                regionSmallestRectangle2Points.Add(point);
                            }

                            DispPolygonjHWindowControlEvent(regionSmallestRectangle2Points, Colors.Blue, "margin");

                            string text = GlobalVarAndFunc.LanguageTranslate("胶高：") + $"{data.胶高:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("胶宽：") + $"{data.胶宽:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("面积：") + $"{data.面积:0.00}";
                            DispTextInImageHWindowControlEvent(text, Colors.White, (int)data.column + (int)(data.胶宽 / 2 * Vision.scaleSize + offsetX),
                                (int)data.row + (int)(data.胶高 / 2 * Vision.scaleSize + offsetY));

                            //hWindowControl.DispTextInImage(text, data.row, data.column);
                            string textWindow1 = GlobalVarAndFunc.LanguageTranslate("胶宽：") + (bResult.胶宽 ? "OK" : "NG");
                            string textWindow2 = GlobalVarAndFunc.LanguageTranslate("胶高：") + (bResult.胶高 ? "OK" : "NG");
                            string textWindow3 = GlobalVarAndFunc.LanguageTranslate("面积：") + (bResult.面积 ? "OK" : "NG");
                            string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
                            DispTextInImageHWindowControlEvent(textWindow, Colors.White, 10, 10);

                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

    }







}
