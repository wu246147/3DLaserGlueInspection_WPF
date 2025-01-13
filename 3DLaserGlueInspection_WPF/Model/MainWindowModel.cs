
using HalconDotNet;
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

namespace _3DLaserGlueInspection
{
    public class MainWindowModel: NotifyBase
    {
        public MainModel mainModel { get; set; } = new MainModel();

        Dictionary<string, string> labelColorEnum = new Dictionary<string, string>{
            { "gray", "Gray" },
            { "green", "LightGreen" },
            { "red", "Red" }
        };

        public enum LogType { 
            normal=0,
            ok = 1,
            ng = 2,
            warn = 3,
        };

        public bool stop = true;
        Thread mainThread = null;

        CarNameIdSet cars = new CarNameIdSet();
        CamParams Params = new CamParams();
        Dictionary<string, Cam> cams = new Dictionary<string, Cam>();
        Vision vision = new Vision();
        JAKARobot robot = new JAKARobot();
        Mmf mmf = new Mmf();
        ISignal io;
        Dictionary<string, Setting> sets = new Dictionary<string, Setting>();

        object olockDataGridViewImageList = new object();
        Stopwatch watch = new Stopwatch();

        SynchronizedList<long> robotPoseKeys = new SynchronizedList<long>();
        SynchronizedList<HPose> robotPoseValues = new SynchronizedList<HPose>();
        Task taskRobot = null;

        Dictionary<string, SynchronizedList<SynchronizedList<long>>> ImageKeys = new Dictionary<string, SynchronizedList<SynchronizedList<long>>>();//指示拍照位置
        Dictionary<string, SynchronizedList<Dictionary<long, HImage>>> Images = new Dictionary<string, SynchronizedList<Dictionary<long, HImage>>>();//相机-分段-时间-图片
        SynchronizedList<int> dataGridViewImageListRows起点 = new SynchronizedList<int>();
        Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>> Point3DXs = new Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>>();//相机-分段-时间-图片数据
        Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>> Point3DYs = new Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>> Point3DZs = new Dictionary<string, SynchronizedList<Dictionary<long, HTuple>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, HXLDCont>>> 轮廓 = new Dictionary<string, SynchronizedList<Dictionary<long, HXLDCont>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, HRegion>>> 胶区域 = new Dictionary<string, SynchronizedList<Dictionary<long, HRegion>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, HRegion>>> 胶外接 = new Dictionary<string, SynchronizedList<Dictionary<long, HRegion>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, Data>>> 胶数据 = new Dictionary<string, SynchronizedList<Dictionary<long, Data>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, bResult>>> 胶结果 = new Dictionary<string, SynchronizedList<Dictionary<long, bResult>>>();

        Dictionary<string, SynchronizedList<Size>> 画布大小 = new Dictionary<string, SynchronizedList<Size>>();

        Dictionary<string, Task> tasks = new Dictionary<string, Task>();//相机-处理任务

        Task taskPoint3D = null;

        int indexImageCut = -1;//指示正在图像采集段数
        bool 总结果 = true;

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
                        //Application.Current.Dispatcher.Invoke(new Action(() =>
                        //{
                        //    //e灯颜色 = 灯颜色.绿;
                        //    //label机器人.Refresh();
                        //}));
                        mainModel.robotCommunicationLabelColorControl = labelColorEnum["green"];
                    }
                    else
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人连接失败：") + robot.ErrMsg, LogType.ng);
                        //Invoke(new Action(() =>
                        //{
                        //    e灯颜色 = 灯颜色.红;
                        //    label机器人.Refresh();
                        //}));
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

                //Invoke(new Action(() =>
                //{
                //    e灯颜色 = 灯颜色.绿;
                //    label软件.Refresh();
                //}));
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
                    foreach (var item in 轮廓.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    foreach (var item in 胶区域.Values)
                    {
                        foreach (var item2 in item)
                        {
                            foreach (var item3 in item2.Values)
                            {
                                item3?.Dispose();
                            }
                        }
                    }
                    foreach (var item in 胶外接.Values)
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
                    dataGridViewImageListRows起点.Clear();
                    Point3DXs.Clear();
                    Point3DYs.Clear();
                    Point3DZs.Clear();
                    轮廓.Clear();
                    胶区域.Clear();
                    胶外接.Clear();
                    胶数据.Clear();
                    胶结果.Clear();
                    画布大小.Clear();
                    tasks.Clear();
                    #endregion
                    // 暂时屏蔽
                    //Invoke(new Action(() => { form3DShow.ClearCloud(); }));
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
                    //this.dataGridViewImageList.SelectionChanged -= new System.EventHandler(this.dataGridViewImageList_SelectionChanged);
                    //dataGridViewImageList.Invoke(new Action(() =>
                    //{
                    //    dataGridViewImageList.Rows.Clear();
                    //}));
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
                    //if (Read(DI.InVIN, out string in_VIN))
                    //{
                    //    inVIN = in_VIN.ToString();
                    //}
                    //else
                    //{
                    //    return;
                    //}
                    //ShowMessage("收到车架号为" + inVIN);

                    DateTime dateTime = DateTime.Now;
                    //BeginInvoke(new Action(() =>
                    //{
                    //    label产品号.Text = GlobalVarAndFunc.LanguageTranslate("产品号：") + ID;
                    //    label名称.Text = GlobalVarAndFunc.LanguageTranslate("名称：") + car.Name;
                    //    labelVIN.Text = "VIN：" + inVIN;
                    //    label时间.Text = GlobalVarAndFunc.LanguageTranslate("时间：") + dateTime.ToString("G");
                    //    label结果.Text = "--";
                    //    label结果.ForeColor = Colors.White;
                    //}));
                    mainModel.productIDControl = ID.ToString();
                    mainModel.nameControl =  car.Name;
                    mainModel.VINControl =  inVIN;
                    mainModel.timeControl =  dateTime.ToString("G");
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

                    //显示数模图
                    // 暂时屏蔽
                    //hWindow数模图.DispImage(set.HImage);
                    HTuple[] rowss = new HTuple[set.XLDDatas.Count];
                    HTuple[] colss = new HTuple[set.XLDDatas.Count];
                    HTuple[] angless = new HTuple[set.XLDDatas.Count];
                    for (int i = 0; i < set.XLDDatas.Count; i++)
                    {
                        int 步数 = set.CutSets[i].EndImageIndex - set.CutSets[i].StartImageIndex + 1;
                        if (步数 < 1) 步数 = 1;
                        vision.XLDData拆分(set.XLDDatas[i], 步数, out rowss[i], out colss[i], out angless[i]);
                        for (int j = 0; j < 步数; j++)
                        {
                            // 暂时屏蔽
                            //hWindow数模图DispCross(rowss[i][j].D, colss[i][j].D, set.CutSets[i].Size, angless[i][j].D, "blue");
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
                                        mainModel.camCommunicationLabelColorControl= labelColorEnum["red"];

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
                    //Invoke(new Action(() =>
                    //{
                    //    e灯颜色 = 灯颜色.绿;
                    //    label相机.Refresh();
                    //}));
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
                                                var dictImage = new SynchronizedList<Dictionary<long, HImage>>();
                                                Images.Add(item.Key, dictImage);

                                                var dictX = new SynchronizedList<Dictionary<long, HTuple>>();
                                                Point3DXs.Add(item.Key, dictX);
                                                var dictY = new SynchronizedList<Dictionary<long, HTuple>>();
                                                Point3DYs.Add(item.Key, dictY);
                                                var dictZ = new SynchronizedList<Dictionary<long, HTuple>>();
                                                Point3DZs.Add(item.Key, dictZ);
                                                var dictXLD = new SynchronizedList<Dictionary<long, HXLDCont>>();
                                                轮廓.Add(item.Key, dictXLD);
                                                var dictRegion = new SynchronizedList<Dictionary<long, HRegion>>();
                                                胶区域.Add(item.Key, dictRegion);
                                                var dictRegionRectangle2 = new SynchronizedList<Dictionary<long, HRegion>>();
                                                胶外接.Add(item.Key, dictRegionRectangle2);
                                                var dictData = new SynchronizedList<Dictionary<long, Data>>();
                                                胶数据.Add(item.Key, dictData);
                                                var dictResult = new SynchronizedList<Dictionary<long, bResult>>();
                                                胶结果.Add(item.Key, dictResult);

                                                画布大小.Add(item.Key, new SynchronizedList<Size>());
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
                    总结果 = true;
                    if (stop) return;

                    ////打开激光
                    //foreach (var item in camParam)
                    //{
                    //    if (item.Value.Enable)
                    //    {
                    //        if (cams[item.Value.CamName].SetLine1Inverter(true))
                    //        {
                    //            ShowMessage($"相机{item.Key}:{item.Value.CamName}打开激光成功");
                    //        }
                    //        else
                    //        {
                    //            ShowMessage($"相机{item.Key}:{item.Value.CamName}打开激光失败：" + cams[item.Value.CamName].ErrMsg, LogType.ng);
                    //        }
                    //    }
                    //}
                    // 暂时屏蔽
                    //form3DShow.RefreshOn(10, true);

                    watch.Restart();
                    bool bRobotRun = true;
                    //启动机器人姿态获取(安川20ms)
                    taskRobot = Task.Run(() =>
                    {
                        // 暂时屏蔽
                        //form3DShow.RefreshOn(10, true);
                        double 颜色下限值 = -0.5;
                        double 颜色上限值 = 0.5;
                        double 范围 = 颜色上限值 - 颜色下限值;
                        if (simulation)
                        {
                            SynchronizedList<long> robotPoseKeys仿真 = new SynchronizedList<long>();
                            SynchronizedList<HPose> robotPoseValues仿真 = new SynchronizedList<HPose>();
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
                                            robotPoseKeys仿真 = paramList;
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
                                string robotPoseValuesPath = $"{basePath}\\robotPoseValues";
                                if (File.Exists(robotPoseValuesPath))
                                {
                                    using (FileStream stream = new FileStream(robotPoseValuesPath, FileMode.Open))
                                    {
                                        BinaryFormatter bf = new BinaryFormatter();
                                        var list = (SynchronizedList<HPose>)bf.Deserialize(stream);
                                        if (list != null)
                                        {
                                            robotPoseValues仿真 = list;
                                        }
                                        else
                                        {
                                            ShowMessage(robotPoseValuesPath + GlobalVarAndFunc.LanguageTranslate("文件格式异常"));
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

                            for (int i = 0; i < robotPoseKeys仿真.Count; i++)
                            {
                                if (!bRobotRun)
                                {
                                    break;
                                }
                                var key = robotPoseKeys仿真[i];
                                var hPose = robotPoseValues仿真[i];
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
                                if (robot.ReadPose(out HPose hPose))
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
                            //var cam = cams[item.Value.CamName];
                            var hCamPar = Params.CamPar[camParamName][item.Key];
                            var LightInCam = Params.LightInCam[camParamName][item.Key];
                            var LightToCam = Params.LightToCam[camParamName][item.Key];
                            var CamToTool = Params.CamToTool[camParamName][item.Key];
                            tasks.Add(item.Key, Task.Run(() =>
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
                                    var dictX = Point3DXs[item.Key][indexTaskCut];
                                    var dictY = Point3DYs[item.Key][indexTaskCut];
                                    var dictZ = Point3DZs[item.Key][indexTaskCut];
                                    var dictXLD = 轮廓[item.Key][indexTaskCut];
                                    var dictRegion = 胶区域[item.Key][indexTaskCut];
                                    var dictRegionRectangle2 = 胶外接[item.Key][indexTaskCut];
                                    var dictData = 胶数据[item.Key][indexTaskCut];
                                    var dictResult = 胶结果[item.Key][indexTaskCut];

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
                                                int indexTaskCut传递 = indexTaskCut;//BeginInvoke用
                                                int indexImage传递 = indexImage;//BeginInvoke用
                                                int camIndex = item.Key == "Cam1" ? 0 : item.Key == "Cam2" ? 1 : item.Key == "Cam3" ? 2 : 3;
                                                if (set.CutSets.Count > indexTaskCut && set.CutSets[indexTaskCut].imageSet.Count > camIndex && set.CutSets[indexTaskCut].imageSet[camIndex].Count > indexImage)
                                                {
                                                    var cutSet = set.CutSets[indexTaskCut];
                                                    var imageSet = set.CutSets[indexTaskCut].imageSet[camIndex][indexImage];
                                                    if (imageSet.轮廓检测)
                                                    {
                                                        Dictionary<double, double> xy = vision.获取激光像素位置HDR2(dictImage[imageKey], imageSet.minThreshold, item.Value.OffsetX, item.Value.OffsetY);

                                                        if (xy.Count > 0)
                                                        {
                                                            //转激光坐标系
                                                            vision.GetXY(hCamPar, LightInCam, xy, out HTuple lightX, out HTuple lightY);
                                                            HTuple lightZ = new HTuple(new double[lightX.Length]);
                                                            //转相机坐标系
                                                            HTuple camX = LightToCam.AffineTransPoint3d(lightX, lightY, lightZ, out HTuple camY, out HTuple camZ);
                                                            ////转传感器坐标系
                                                            //HTuple sensorX = CamToSensor.AffineTransPoint3d(camX, camY, camZ, out HTuple sensorY, out HTuple sensorZ);
                                                            //转工具
                                                            HTuple toolX = CamToTool.AffineTransPoint3d(camX, camY, camZ, out HTuple toolY, out HTuple toolZ);
                                                            //转机器人坐标
                                                            HPose robotPose = MathHPose(robotPoseValues[indexRobotPose - 1], robotPoseValues[indexRobotPose],
                                                                (imageKey - robotPoseKeys[indexRobotPose - 1]) / (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1]));
                                                            var ToolToRobot = robotPose.PoseToHomMat3d();
                                                            HTuple robotX = ToolToRobot.AffineTransPoint3d(toolX, toolY, toolZ, out HTuple robotY, out HTuple robotZ);

                                                            //三维数据添加(机器人坐标)
                                                            dictX.Add(imageKey, robotX);
                                                            dictY.Add(imageKey, robotY);
                                                            dictZ.Add(imageKey, robotZ);
                                                            // 暂时屏蔽
                                                            //form3DShow.InsertNextPoints(robotX.DArr, robotY.DArr, robotZ.DArr, ((robotZ - cutSet.ShowColorMin / 1000) / ((cutSet.ShowColorMax - cutSet.ShowColorMin) / 1000)).DArr);

                                                            if (imageSet.单帧检测)
                                                            {
                                                                Data data = new Data();
                                                                bResult bResult = new bResult();
                                                                HTuple lightXcut, lightYcut;
                                                                if (imageSet.启用裁剪)
                                                                {
                                                                    dictImage[imageKey].GetImageSize(out int imageWidth, out int imageHeight);
                                                                    double LeftX = imageWidth * imageSet.LeftX + item.Value.OffsetX;
                                                                    double RightX = imageWidth * imageSet.RightX + item.Value.OffsetX;
                                                                    double TopY = imageHeight * imageSet.TopY + item.Value.OffsetY;
                                                                    double DownY = imageHeight * imageSet.DownY + item.Value.OffsetY;
                                                                    List<double> x = new List<double>();
                                                                    List<double> y = new List<double>();
                                                                    foreach (var n in xy)
                                                                    {
                                                                        if (n.Key >= LeftX && n.Key <= RightX && n.Value >= TopY && n.Value <= DownY)
                                                                        {
                                                                            x.Add(n.Key);
                                                                            y.Add(n.Value);
                                                                        }
                                                                    }
                                                                    //转激光坐标系
                                                                    vision.GetXY(hCamPar, LightInCam, x, y, out lightXcut, out lightYcut);
                                                                }
                                                                else
                                                                {
                                                                    lightXcut = lightX;
                                                                    lightYcut = lightY;
                                                                }

                                                                if (lightXcut.Length > 0)
                                                                {
                                                                    //单帧检测(使用激光坐标系)
                                                                    //轮廓只计算整数，所以数据单位放大至0.01mm，并把原点移至画布中心
                                                                    HTuple hx_10微米 = lightXcut * (1000 * 100) + cutSet.ShowWidth * 100 / 2;
                                                                    HTuple hy_10微米 = lightYcut * (1000 * 100 * Math.Cos(5 / 180 * Math.PI)) + cutSet.ShowHeight * 100 / 2;//补偿激光线5°倾斜
                                                                    HXLDCont hXLDCont10微米, 胶轮廓;
                                                                    if (imageSet.拐点分段)
                                                                    {
                                                                        hXLDCont10微米 = vision.轮廓提取(hx_10微米, hy_10微米, imageSet.分段距离 * 100, imageSet.成段点数, imageSet.分段弧度, imageSet.弧度分段距离 * 100, out HTuple 转折坐标X, out HTuple 转折坐标Y, out HTuple 转折标记, out 胶轮廓);
                                                                    }
                                                                    else if (imageSet.离散去噪)
                                                                    {
                                                                        hXLDCont10微米 = vision.轮廓提取(hx_10微米, hy_10微米, imageSet.分段距离 * 100, imageSet.成段点数);
                                                                        胶轮廓 = hXLDCont10微米.Clone();
                                                                    }
                                                                    else
                                                                    {
                                                                        hXLDCont10微米 = new HXLDCont(hy_10微米, hx_10微米);
                                                                        胶轮廓 = hXLDCont10微米.Clone();
                                                                    }
                                                                    dictXLD.Add(imageKey, hXLDCont10微米);

                                                                    if (胶轮廓 != null)
                                                                    {
                                                                        HRegion hRegion = 胶轮廓.GenRegionContourXld("filled");

                                                                        HRegion hRegion1 = hRegion.OpeningCircle(7d);
                                                                        hRegion.Dispose();

                                                                        HRegion hRegionCon = hRegion1.Connection();
                                                                        hRegion1.Dispose();

                                                                        //取一个面积最大的
                                                                        int indexBig = 1;
                                                                        for (int i = 2; i < hRegionCon.CountObj(); i++)
                                                                        {
                                                                            if (hRegionCon[i].Area > hRegionCon[indexBig].Area)
                                                                            {
                                                                                indexBig = i;
                                                                            }
                                                                        }
                                                                        HRegion hRegionBig = hRegionCon[indexBig].Clone();
                                                                        hRegionCon.Dispose();

                                                                        if (hRegionBig.Area > 0)
                                                                        {
                                                                            vision.RunRegion(hRegionBig, imageSet, out var hRegionGenRectangle2, out data, out bResult);
                                                                            dictRegion.Add(imageKey, hRegionBig);
                                                                            dictRegionRectangle2.Add(imageKey, hRegionGenRectangle2);
                                                                            dictData.Add(imageKey, data);
                                                                            dictResult.Add(imageKey, bResult);
                                                                            // 暂时屏蔽
                                                                            //ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10微米, hRegionBig, hRegionGenRectangle2, data, bResult);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        //ShowImageData(cutSet.ShowWidth, cutSet.ShowHeight, hXLDCont10微米);
                                                                    }
                                                                    胶轮廓?.Dispose();
                                                                }

                                                                //dataGridViewImageList.BeginInvoke(new Action(() =>
                                                                //{
                                                                //    lock (olockDataGridViewImageList)
                                                                //    {
                                                                //        dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = bResult.Result ? Colors.Green : LogType.ng;
                                                                //    }
                                                                //}));
                                                                // 暂时屏蔽
                                                                //hWindow数模图DispCross(rowss[indexTaskCut], colss[indexTaskCut], angless[indexTaskCut], indexImage, cutSet, bResult.Result ? "green" : "red");
                                                                if (!bResult.Result)
                                                                {
                                                                    总结果 = false;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ////不单帧检测
                                                                //dataGridViewImageList.BeginInvoke(new Action(() =>
                                                                //{
                                                                //    lock (olockDataGridViewImageList)
                                                                //    {
                                                                //        //dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = Colors.Gray;

                                                                //        if ((dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递) % 2 == 0)
                                                                //        {
                                                                //            dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = Colors.FromArgb(38, 56, 81);
                                                                //        }
                                                                //        else
                                                                //        {
                                                                //            dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = Colors.FromArgb(42, 52, 64);
                                                                //        }
                                                                //    }
                                                                //}));
                                                                //hWindow数模图DispCross(rowss[indexTaskCut], colss[indexTaskCut], angless[indexTaskCut], indexImage, cutSet, "gray");
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        ////不轮廓检测
                                                        //dataGridViewImageList.BeginInvoke(new Action(() =>
                                                        //{
                                                        //    lock (olockDataGridViewImageList)
                                                        //    {
                                                        //        if ((dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递) % 2 == 0)
                                                        //        {
                                                        //            dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = Colors.FromArgb(38, 56, 81);
                                                        //        }
                                                        //        else
                                                        //        {
                                                        //            dataGridViewImageList.Rows[dataGridViewImageListRows起点[indexTaskCut传递] + indexImage传递].Cells[item.Key].Style.BackColor = Colors.FromArgb(42, 52, 64);
                                                        //        }
                                                        //    }
                                                        //}));
                                                        //hWindow数模图DispCross(rowss[indexTaskCut], colss[indexTaskCut], angless[indexTaskCut], indexImage, cutSet, "gray");
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
                            }));
                        }
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理任务启动完成"));

                    //启动三维图
                    taskPoint3D = Task.Run(() => { });
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("三维图像显示任务启动完成"));

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
                        int 起点 = mainModel.ImageResultRecords.Count;
                        dataGridViewImageListRows起点.Add(起点);
                        //拍照
                        foreach (var item in camParam)
                        {
                            if (item.Value.Enable)
                            {
                                var dictImageKey = new SynchronizedList<long>();
                                ImageKeys[item.Key].Add(dictImageKey);
                                var dictImage = new Dictionary<long, HImage>(new Dictionary<long, HImage>());
                                Images[item.Key].Add(dictImage);

                                var dictX = new Dictionary<long, HTuple>();
                                Point3DXs[item.Key].Add(dictX);
                                var dictY = new Dictionary<long, HTuple>();
                                Point3DYs[item.Key].Add(dictY);
                                var dictZ = new Dictionary<long, HTuple>();
                                Point3DZs[item.Key].Add(dictZ);
                                var dictXLD = new Dictionary<long, HXLDCont>();
                                轮廓[item.Key].Add(dictXLD);
                                var dictRegion = new Dictionary<long, HRegion>();
                                胶区域[item.Key].Add(dictRegion);
                                var dictRegionRectangle2 = new Dictionary<long, HRegion>();
                                胶外接[item.Key].Add(dictRegionRectangle2);
                                var dictData = new Dictionary<long, Data>();
                                胶数据[item.Key].Add(dictData);
                                var dictResult = new Dictionary<long, bResult>();
                                胶结果[item.Key].Add(dictResult);

                                var cam = cams[item.Value.CamName];
                                int 段数下标 = indexImageCut + 1;
                                bool CamEnabled = item.Key == "Cam1" ? set.CutSets[段数下标].Cam1Enabled :
                                    item.Key == "Cam2" ? set.CutSets[段数下标].Cam2Enabled :
                                    item.Key == "Cam3" ? set.CutSets[段数下标].Cam3Enabled :
                                    set.CutSets[段数下标].Cam4Enabled;
                                画布大小[item.Key].Add(new Size(set.CutSets[段数下标].ShowWidth, set.CutSets[段数下标].ShowHeight));
                                if (CamEnabled)
                                {
                                    if (!simulation)
                                    {
                                        //if (cam.SetLine1Inverter(true))
                                        //{
                                        //    ShowMessage($"相机{item.Key}:{item.Value.CamName}打开激光成功");
                                        //}
                                        //else
                                        //{
                                        //    ShowMessage($"相机{item.Key}:{item.Value.CamName}打开激光失败：" + cams[item.Value.CamName].ErrMsg, LogType.ng);
                                        //}

                                        bool flag = cam.KeepShot(new Action<HImage>(image =>
                                        {
                                            long key = watch.ElapsedTicks;
                                            {
                                                if (dictImageKey.Count < set.CutSets[段数下标].ImageNum)
                                                {
                                                    dictImage.Add(key, image);
                                                    dictImageKey.Add(key);
                                                    var dictImageKeyCount = dictImageKey.Count;
                                                    //dataGridViewImageList.BeginInvoke(new Action(() =>
                                                    //{
                                                    //    lock (olockDataGridViewImageList)
                                                    //    {
                                                    //        if (dataGridViewImageList.Rows.Count - 起点 < dictImageKeyCount)
                                                    //        {
                                                    //            do
                                                    //            {
                                                    //                int indexRow = dataGridViewImageList.Rows.Add();
                                                    //                dataGridViewImageList.Rows[indexRow].Cells[item.Key].Value = $"{段数下标}:{dictImageKeyCount - 1}";
                                                    //                dataGridViewImageList.FirstDisplayedScrollingRowIndex = indexRow;
                                                    //            }
                                                    //            while (dataGridViewImageList.Rows.Count - 起点 < dictImageKeyCount);
                                                    //        }
                                                    //        else
                                                    //        {
                                                    //            dataGridViewImageList.Rows[起点 + dictImageKeyCount - 1].Cells[item.Key].Value = $"{段数下标}:{dictImageKeyCount - 1}";
                                                    //        }
                                                    //    }
                                                    //}));

                                                    Application.Current.Dispatcher.Invoke(() =>
                                                    {
                                                        lock (olockDataGridViewImageList)
                                                        {
                                                            if (mainModel.ImageResultRecords.Count - 起点 < dictImageKeyCount)
                                                            {
                                                                do
                                                                {
                                                                    ImageResultRecord imageResultRecord = new ImageResultRecord();

                                                                    switch (item.Key)
                                                                    {
                                                                        case "Cam1":
                                                                            imageResultRecord.Cam1 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                            break;
                                                                        case "Cam2":
                                                                            imageResultRecord.Cam2 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                            break;
                                                                        case "Cam3":
                                                                            imageResultRecord.Cam3 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                            break;
                                                                        case "Cam4":
                                                                            imageResultRecord.Cam4 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                            break;

                                                                        default:
                                                                            break;
                                                                    }
                                                                    mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                }
                                                                while (mainModel.ImageResultRecords.Count - 起点 < dictImageKeyCount);
                                                            }
                                                            else
                                                            {
                                                                switch (item.Key)
                                                                {
                                                                    case "Cam1":
                                                                        mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam1 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam2":
                                                                        mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam2 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam3":
                                                                        mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam3 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                        break;
                                                                    case "Cam4":
                                                                        mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam4 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                        break;

                                                                    default:
                                                                        break;
                                                                }
                                                            }
                                                        }
                                                    });
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
                                        string path = $"{simulationPath}\\{段数下标}\\{item.Key}";
                                        if (Directory.Exists(path))
                                        {
                                            Task.Run(() =>
                                            {
                                                var filePaths = Directory.GetFiles(path, "*.png").OrderBy(n => n).ToArray();
                                                for (int i = 0; i < filePaths.Length; i++)
                                                {
                                                    if (long.TryParse(Path.GetFileNameWithoutExtension(filePaths[i]), out long key))
                                                    {
                                                        if (dictImageKey.Count < set.CutSets[段数下标].ImageNum)
                                                        {
                                                            try
                                                            {
                                                                HImage image = new HImage(filePaths[i]);
                                                                dictImage.Add(key, image);
                                                                dictImageKey.Add(key);
                                                                var dictImageKeyCount = dictImageKey.Count;
                                                                Application.Current.Dispatcher.Invoke(() =>
                                                                {
                                                                    lock (olockDataGridViewImageList)
                                                                    {
                                                                        if (mainModel.ImageResultRecords.Count - 起点 < dictImageKeyCount)
                                                                        {
                                                                            do
                                                                            {
                                                                                ImageResultRecord imageResultRecord = new ImageResultRecord();

                                                                                switch (item.Key)
                                                                                {
                                                                                    case "Cam1":
                                                                                        imageResultRecord.Cam1 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                        break;
                                                                                    case "Cam2":
                                                                                        imageResultRecord.Cam2 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                        break;
                                                                                    case "Cam3":
                                                                                        imageResultRecord.Cam3 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                        break;
                                                                                    case "Cam4":
                                                                                        imageResultRecord.Cam4 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                        break;

                                                                                    default:
                                                                                        break;
                                                                                }
                                                                                mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                            }
                                                                            while (mainModel.ImageResultRecords.Count - 起点 < dictImageKeyCount);
                                                                        }
                                                                        else
                                                                        {
                                                                            switch (item.Key)
                                                                            {
                                                                                case "Cam1":
                                                                                    mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam1 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam2":
                                                                                    mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam2 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam3":
                                                                                    mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam3 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                    break;
                                                                                case "Cam4":
                                                                                    mainModel.ImageResultRecords[起点 + dictImageKeyCount - 1].Cam4 = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                                    break;

                                                                                default:
                                                                                    break;
                                                                            }
                                                                        }
                                                                    }
                                                                });


                                                                //        dataGridViewImageList.BeginInvoke(new Action(() =>
                                                                //{
                                                                //    lock (olockDataGridViewImageList)
                                                                //    {
                                                                //        if (dataGridViewImageList.Rows.Count - 起点 < dictImageKeyCount)
                                                                //        {
                                                                //            do
                                                                //            {
                                                                //                int indexRow = dataGridViewImageList.Rows.Add();
                                                                //                dataGridViewImageListdataGridViewImageList.Rows[indexRow].Cells[item.Key].Value = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                //                dataGridViewImageList.FirstDisplayedScrollingRowIndex = indexRow;
                                                                //            }
                                                                //            while (dataGridViewImageList.Rows.Count - 起点 < dictImageKeyCount);
                                                                //        }
                                                                //        else
                                                                //        {
                                                                //            dataGridViewImageList.Rows[起点 + dictImageKeyCount - 1].Cells[item.Key].Value = $"{段数下标}:{dictImageKeyCount - 1}";
                                                                //        }
                                                                //    }
                                                                //}));
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

                            ////关闭激光
                            //foreach (var item in camParam)
                            //{
                            //    if (item.Value.Enable)
                            //    {
                            //        if (cams[item.Value.CamName].SetLine1Inverter(false))
                            //        {
                            //            ShowMessage($"相机{item.Key}:{item.Value.CamName}关闭激光成功");
                            //        }
                            //        else
                            //        {
                            //            ShowMessage($"相机{item.Key}:{item.Value.CamName}关闭激光失败：" + cams[item.Value.CamName].ErrMsg, LogType.ng);
                            //        }
                            //    }
                            //}
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

                    ////关闭激光
                    //if (!simulation)
                    //{
                    //    foreach (var item in camParam)
                    //    {
                    //        if (item.Value.Enable)
                    //        {
                    //            if (cams[item.Value.CamName].SetLine1Inverter(false))
                    //            {
                    //                ShowMessage($"相机{item.Key}:{item.Value.CamName}关闭激光成功");
                    //            }
                    //            else
                    //            {
                    //                ShowMessage($"相机{item.Key}:{item.Value.CamName}关闭激光失败：" + cams[item.Value.CamName].ErrMsg, LogType.ng);
                    //            }
                    //        }
                    //    }
                    //}

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
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理完成"));
                    // 暂时屏蔽
                    //form3DShow.RefreshOFF();
                    //form3DShow.RefreshPoints();
                    if (总结果)
                    {
                        mainModel.OKCountControl++;
                    }
                    else
                    {
                        mainModel.NGCountControl++;
                    }
                    mainModel.totalCountControl = mainModel.OKCountControl + mainModel.NGCountControl;

                    mainModel.passRateControl = ((double)mainModel.OKCountControl * 100 / mainModel.totalCountControl).ToString("0.00") + "%";

                    //this.dataGridViewImageList.SelectionChanged += new System.EventHandler(this.dataGridViewImageList_SelectionChanged);
                    mainModel.resultControl = 总结果 ? "OK" : "NG";
                    mainModel.resultColorControl = 总结果 ? "#FF06BD00" : "Red";
                    //Invoke(new Action(() =>
                    //{
                    //    //label结果.Text = 总结果 ? "OK" : "NG";
                    //    //label结果.ForeColor = 总结果 ? Colors.Green : Colors.DarkRed;
                    //    //label结果.BackColor = 总结果 ? Colors.Green : LogType.ng;
                    //    //ShowChart(OK数, NG数);
                    //    dataGridViewCarList.Rows.Insert(0, dateTime.ToString("yyyy-MM-dd HH:mm:ss"), ID, 总结果 ? "OK" : "NG");
                    //    //dataGridViewCarList.Rows[0].DefaultCellStyle.BackColor = 总结果 ? Colors.White : LogType.ng;//
                    //    if (dataGridViewCarList.Rows.Count % 2 == 0)
                    //    {
                    //        dataGridViewCarList.Rows[0].DefaultCellStyle.BackColor = Colors.FromArgb(38, 56, 81);
                    //    }
                    //    else
                    //    {
                    //        dataGridViewCarList.Rows[0].DefaultCellStyle.BackColor = Colors.FromArgb(42, 52, 64);
                    //    }
                    //    if (总结果)
                    //    {
                    //        dataGridViewCarList.Rows[0].DefaultCellStyle.ForeColor = Colors.Green;
                    //    }
                    //    else
                    //    {
                    //        dataGridViewCarList.Rows[0].DefaultCellStyle.ForeColor = LogType.ng;
                    //    }


                    //    dataGridViewCarList.CurrentCell = null;//
                    //}));
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CarResultRecord carResultRecord = new CarResultRecord();
                        carResultRecord.CarDetTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        carResultRecord.CarID = ID.ToString();
                        carResultRecord.CarResult = 总结果 ? "OK" : "NG";

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
                        if ((总结果 && set.OtherSet.SaveOKImage) || (!总结果 && set.OtherSet.SaveNGImage))
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("开始存图"));
                            try
                            {
                                string OKNG = 总结果 ? "OK" : "NG";
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
                                                image.Value.WriteImage("png 1", 0, $"{imageDirectory}\\{image.Key:000000000000}.png");
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
                                        pose.Add(new double[] { poseKey.RawData[0], poseKey.RawData[1], poseKey.RawData[2], poseKey.RawData[3], poseKey.RawData[4], poseKey.RawData[5], poseKey.RawData[6] });
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
        HPose MathHPose(HPose hPose1, HPose hPose2, double s)
        {
            var mathPose = new HPose(hPose1.RawData + (hPose2.RawData - hPose1.RawData) * s);
            //mathPose.RawData[6] = hPose1.RawData[6];
            return mathPose;
        }

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
                mainModel.LogRecords.Insert(0, logRecord); 
            });

        }

    }





}
