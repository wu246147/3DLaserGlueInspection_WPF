
//using HalconDotNet;
using HelixToolkit.Wpf;
using HslCommunication.Core.Net;
using LiveCharts.Wpf;
using OpenCvSharp;
using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Xml.Serialization;
using TCPIP;
using Wpf_Replace_halcon;

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

    public delegate void Disp3DPointEventHandler_V(List<double> Xs, List<double> Ys, List<double> Zs, List<double> colorScale);
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

        //当前车型
        public Car car;

        CamParams Params = new CamParams();
        Dictionary<string, Cam> cams = new Dictionary<string, Cam>();
        //Vision vision = new Vision();
        //JAKARobot robot = new JAKARobot();
        //FanucRobot robot = new FanucRobot();
        //机器人通讯，获取坐标
        KukaRobot robot = new KukaRobot();
        //socket通讯，获取io信号
        // TCP 通讯
        TCP_Server Server;
        Thread threadReconnect = null; // 通讯重连线程

        //仿真通讯，获取io信号
        Mmf mmf = new Mmf();
        ISignal io;
        public Dictionary<string, Setting> sets = new Dictionary<string, Setting>();

        object olockDataGridViewImageList = new object();
        Stopwatch watch = new Stopwatch();
        bool bRobotRun = true;//控制机器人线程是否运行

        SynchronizedList<long> robotPoseKeys = new SynchronizedList<long>();
        SynchronizedList<Wpf_Replace_halcon.PoseParameters> robotPoseValues = new SynchronizedList<Wpf_Replace_halcon.PoseParameters>();
        Task taskRobot = null;

        public Dictionary<string, SynchronizedList<SynchronizedList<long>>> ImageKeys = new Dictionary<string, SynchronizedList<SynchronizedList<long>>>();//指示拍照位置
        Dictionary<string, SynchronizedList<Dictionary<long, Mat>>> Images = new Dictionary<string, SynchronizedList<Dictionary<long, Mat>>>();//相机-分段-时间-图片
        SynchronizedList<int> dataGridViewImageListRowsStartPoint = new SynchronizedList<int>();
        Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>> Robot3DPose = new Dictionary<string, SynchronizedList<Dictionary<long, Wpf_Replace_halcon.PoseParameters>>>();//相机-分段-时间-机器位姿

        Dictionary<string, SynchronizedList<Dictionary<long,Point3D>>> ResultCenter3DPoint = new Dictionary<string, SynchronizedList<Dictionary<long, Point3D>>>(); //相机-分段-时间-涂漆中点坐标

        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DXs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();//相机-分段-时间-图片数据
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DYs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();
        Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>> Point3DZs = new Dictionary<string, SynchronizedList<Dictionary<long, List<double>>>>();

        Dictionary<string, SynchronizedList<Dictionary<long, double>>> glueVols = new Dictionary<string, SynchronizedList<Dictionary<long, double>>>(); //体积结果 //相机-分段-时间-体积结果

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

        int displayIntervalID = 4;

        public Dictionary<string, int> indexImageCutProcessDict = new Dictionary<string, int>(); // 表示正在图像处理的段数
        bool totalResult = true;

        public bool simulation = false;

        public string simulationPath = "";

        /// <summary>
        /// 开始信号
        /// </summary>
        
        bool isStart = false;


        /// <summary>
        /// 触发信号
        /// </summary>
        bool isPGON = false;


        /// <summary>
        /// 开始信号
        /// </summary>

        bool isStartEnd = false;


        /// <summary>
        /// 触发信号
        /// </summary>
        bool isPGONEnd = false;

        /// <summary>
        /// 中断信号
        /// </summary>
        bool isAbort = false;

        /// <summary>
        /// 结束信号
        /// </summary>
        bool isEND = false;

        //ushort 256

        /// <summary>
        /// 车型号
        /// </summary>
        int CarNumber = -1;


        public void resetSignal()
        {
            isStart = false;
            isPGON = false ;
            isAbort = false ;
            isEND = false ;
            isStartEnd = false ;
            isPGONEnd   = false ;
        }


        /// <summary>
        /// 根据通讯方式初始化连接线程
        /// </summary>
        public void InitCommunicationConnection()
        {

            IniTCPSocketServer();

        }

        /// <summary>
        /// 停止通讯线程
        /// </summary>
        public void StopCommunicationThread()
        {
            if (Server != null)
            {

                // TCP 停止监听并null
                if (Server != null)
                {
                    Server.StopListen();
                    Server = null;

                    //_timer.Stop();
                }
                // 等待线程结束
                if (!threadReconnect.Join(1000))  // 等待最多1秒
                {
                    try
                    {
                        threadReconnect.Abort();  // 强制终止（不推荐，但有时必要）
                    }
                    catch { }
                }
                threadReconnect = null;
            }
        }

        private void IniTCPSocketServer()
        {
            threadReconnect = new Thread(IniRobotTCPserver);
            threadReconnect.Start();

            //// TCP计时器
            //_timer = new DispatcherTimer();
            //_timer.Tick += Timer_Tick;
            //_timer.Interval = TimeSpan.FromMilliseconds(100); // 0.1秒间隔
            //_timer.Start();
        }
        private void IniRobotTCPserver()
        {

            try
            {
                Server = new TCP_Server();

                bool rt = Server.Load();
                if (rt)
                {
                    //绑定委托与事件
                    Server.reserveInfoSignal += ProcessInfo;

                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("服务器信号加载成功"));

                    //开始监听
                    Server.StartListen();

                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("TCP通讯服务器建立成功!"));

                    return;
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("服务器信号加载失败"));

                }


            }

            catch (Exception ex)
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("TCP通讯服务器建立失败"));

                return;
            }
        }

        void ProcessInfo(string info)
        {
            int PROGRAM_ID = 0;
            int COMMAND_ID = 0;
            int POINT_ID = 0;
            Thread cameraThread;
            try
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到信号：")+info);

                //string[] tempReceivedParts;
                //tempReceivedParts = info.Split(',');
                
                if(robot.GetType() == typeof(KukaRobot))
                {
                    var doc = XDocument.Parse(info);
                    var root = doc.Root;

                    int programId = int.Parse(root.Element("PROGRAM_ID").Value);
                    int commandId = int.Parse(root.Element("COMMAND_ID").Value);
                    int pointId = int.Parse(root.Element("POINT_ID").Value);


                    CarNumber = programId;
                    switch(commandId)
                    {
                        case 0:
                            //开始信号
                            if (pointId == 1)
                            {
                                isStart = true;
                            }
                            else if (pointId == 0)
                            {
                                isStart = false;
                            }
                            break;
                        case 1:
                            //启动信号
                            if (pointId == 1)
                            {
                                isPGON = true;
                            }
                            else if (pointId == 0)
                            {
                                isPGON = false;
                            }
                            break;

                        case 2:
                            //开始信号结束
                            if (pointId == 1)
                            {
                                isStartEnd = true;
                            }
                            else if (pointId == 0)
                            {
                                isStartEnd = false;
                            }
                            break;
                        case 3:
                            //启动信号结束
                            if (pointId == 1)
                            {
                                isPGONEnd = true;
                            }
                            else if (pointId == 0)
                            {
                                isPGONEnd = false;
                            }
                            break;
                        case 4:
                            //中断信号
                            if (pointId == 1)
                            {
                                isAbort = true;
                            }
                            else if (pointId == 0)
                            {
                                isAbort = false;
                            }
                            break;
                        case 5:
                            //停止信号
                            if (pointId == 1)
                            {
                                isEND = true;
                            }
                            else if (pointId == 0)
                            {
                                isEND = false;
                            }
                            break;
                    }

                }




            }
            catch (Exception ex)
            {
                
            }
        }


        public string serializationInfo(double x, double y, double z, double a, double b, double c, int result, string setflat)
        {

            XElement xml = new XElement("VISION",


                new XElement("ROBOTPOS",
                    new XAttribute("X", x),
                    new XAttribute("Y", y),
                    new XAttribute("Z", z),
                    new XAttribute("A", a),
                    new XAttribute("B", b),
                    new XAttribute("C", c),
                    "" 
                ),

                new XElement("RESULT", result),


                new XAttribute("Set_Flag", setflat), "" 

            );

            return xml.ToString();
        }


        void SocketSend(int RESULT = 0)
        {
            if (robot.GetType() == typeof(KukaRobot))
            {
                string msg = serializationInfo(0, 0, 0, 0, 0, 0, RESULT, "11");
                Server.Send(msg);
            }
        }

        public void MainRun()
        {
            try
            {
                //if (simulation)
                //{
                //    io = mmf;
                //}
                //else
                //{
                //    io = robot;
                //}

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
                //if (io.Load())
                //{
                //    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO参数加载成功"));
                //}
                //else
                //{
                //    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO参数加载失败：") + io.ErrMsg, LogType.ng);
                //    return;
                //}
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
                //if (io.Open())
                //{
                //    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO连接成功"));
                //}
                //else
                //{
                //    ShowMessage(GlobalVarAndFunc.LanguageTranslate("IO连接失败：") + io.ErrMsg, LogType.ng);
                //    return;
                //}
                #endregion

                mainModel.softwareRunLabelColorControl = labelColorEnum["green"];
                //初始化
                resetSignal();

                while (!stop)
                {
                    //if (!Write(DO.Running, false)) return;
                    //if (!Write(DO.Triggering, false)) return;
                    //输出准备号好
                    //if (!Write(DO.Ready, true)) return;


                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("输出Ready信号"));

                    //等待开始信号
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待开始信号"));
                    while (true)
                    {
                        bool val;
                        if (isStart)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到开始信号"));
                            break;
                        }

                        Thread.Sleep(60);
                        if (stop) return;
                    }
                    //收到信号后，就立刻恢复状态
                    resetSignal();

                    ushort ID;
                    //Car car;
                    string inVIN;
                    DateTime dateTime;
                    Dictionary<string, CamParam> camParam;
                    Setting set;


                    bool rt = initRun(out ID, out car, out inVIN, out dateTime, out camParam, out set);

                    //回复开始ON信号
                    if (rt)
                    {
                        SocketSend(0);
                    }
                    else
                    {
                        SocketSend(-1);
                        return;
                    } 

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
                            if (stop)
                            {
                                return;
                            }

                            if (isPGON)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号ON"));
                                break;
                            }
                            if (isEND)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                bEnd = true;
                                break;
                            }
                            if (isAbort)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                bAbort = true;
                                break;
                            }
                            Thread.Sleep(1);
                        }
                        if (bEnd) break;
                        if (bAbort) break;
                        //回复触发ON信号
                        //收到信号后，就立刻恢复状态
                        resetSignal();

                        SocketSend(0);

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

                                var dictResultCenter3DPoint = new Dictionary<long, Point3D>();
                                ResultCenter3DPoint[item.Key].Add(dictResultCenter3DPoint);
                                

                                var dictX = new Dictionary<long, List<double>>();
                                Point3DXs[item.Key].Add(dictX);
                                var dictY = new Dictionary<long, List<double>>();
                                Point3DYs[item.Key].Add(dictY);
                                var dictZ = new Dictionary<long, List<double>>();
                                Point3DZs[item.Key].Add(dictZ);

                                var dictV = new Dictionary<long, double>();
                                glueVols[item.Key].Add(dictV);


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

                                                    double fps = dictImageKey.Count * 1000.0 * 1000.0 / key;
                                                    //Console.WriteLine("fps:{0}", fps);

                                                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                                    {
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
                                                                    mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                    //Application.Current.Dispatcher.Invoke(() =>
                                                                    //{
                                                                    //    mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                    //});
                                                                }
                                                                while (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount);
                                                            }
                                                            else
                                                            {
                                                                switch (item.Key)
                                                                {
                                                                    case "Cam1":
                                                                        //Application.Current.Dispatcher.Invoke(() =>
                                                                        //{
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        //});
                                                                        break;
                                                                    case "Cam2":
                                                                        //Application.Current.Dispatcher.Invoke(() =>
                                                                        //{
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        //});
                                                                        break;
                                                                    case "Cam3":
                                                                        //Application.Current.Dispatcher.Invoke(() =>
                                                                        //{
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        //});
                                                                        break;
                                                                    case "Cam4":
                                                                        //Application.Current.Dispatcher.Invoke(() =>
                                                                        //{
                                                                        mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                        //});
                                                                        break;

                                                                    default:
                                                                        break;
                                                                }
                                                            }
                                                        }
                                                    }

                                               ));
                                                }
                                            }
                                        }));
                                        if (flag)
                                        {
                                            ShowMessage(item.Key);
                                            //相机1要软触发启动
                                            if (item.Key == "Cam1")
                                            {
                                                bool rt2 = cam.TriggerSoftwareExecute();
                                                if (rt2)
                                                {
                                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("软触发成功"));
                                                }
                                                else
                                                {
                                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("软触发失败"));
                                                }

                                            }
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
                                                    if (long.TryParse(System.IO.Path.GetFileNameWithoutExtension(filePaths[i]), out long key))
                                                    {
                                                        if (dictImageKey.Count < set.CutSets[segmentIndex].ImageNum)
                                                        {
                                                            try
                                                            {
                                                                Mat image = new Mat(filePaths[i], ImreadModes.Unchanged);

                                                                dictImage.Add(key, image);
                                                                dictImageKey.Add(key);
                                                                var dictImageKeyCount = dictImageKey.Count;

                                                                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                                                {

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
                                                                                mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                                //Application.Current.Dispatcher.Invoke(() =>
                                                                                //{
                                                                                //    mainModel.ImageResultRecords.Add(imageResultRecord);
                                                                                //});
                                                                            }
                                                                            while (mainModel.ImageResultRecords.Count - startPoint < dictImageKeyCount);
                                                                        }
                                                                        else
                                                                        {
                                                                            switch (item.Key)
                                                                            {
                                                                                case "Cam1":
                                                                                    //Application.Current.Dispatcher.Invoke(() =>
                                                                                    //{
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam1 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    //});
                                                                                    break;
                                                                                case "Cam2":
                                                                                    //Application.Current.Dispatcher.Invoke(() =>
                                                                                    //{
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam2 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    //});
                                                                                    break;
                                                                                case "Cam3":
                                                                                    //Application.Current.Dispatcher.Invoke(() =>
                                                                                    //{
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam3 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    //});
                                                                                    break;
                                                                                case "Cam4":
                                                                                    //Application.Current.Dispatcher.Invoke(() =>
                                                                                    //{
                                                                                    mainModel.ImageResultRecords[startPoint + dictImageKeyCount - 1].Cam4 = $"{segmentIndex}:{dictImageKeyCount - 1}";
                                                                                    //});
                                                                                    break;

                                                                                default:
                                                                                    break;
                                                                            }
                                                                        }
                                                                    }

                                                                }));
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                            }
                                                        }
                                                    }
                                                    if (!bTriggering) { break; }
                                                    Thread.Sleep(1);
                                                }

                                                ShowMessage(item.Key + GlobalVarAndFunc.LanguageTranslate("仿真图片遍历完"));

                                            });


                                        }
                                    }
                                }
                            }
                        }
                        indexImageCut++;

                        ////输出拍照中信号
                        //if (!Write(DO.Triggering, true)) return;

                        //等触发信号OFF
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待触发信号OFF"));
                        while (true)
                        {
                            if (stop) return;

                            if (isPGONEnd == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号OFF"));
                                break;
                            }

                            if (isEND == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                bEnd = true;
                                break;
                            }
                            if (isAbort == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                bAbort = true;
                                break;
                            }
                            Thread.Sleep(1);
                        }
                        //收到信号后，就立刻恢复状态
                        resetSignal();

                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待数据转换完成"));

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

                        ////关闭拍照中信号
                        //if (!Write(DO.Triggering, false)) return;

                        if (stop) return;
                        if (bEnd) break;
                        if (bAbort) break;

                        //回复触发OFF信号

                        if (totalResult)
                        {
                            SocketSend(0);
                        }
                        else
                        {
                            SocketSend(-1);
                        }

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

                    ////等待3d显示完毕
                    //while (!taskShow3D.IsCompleted)
                    //{
                    //    Thread.Sleep(10);
                    //    if (stop) return;
                    //}

                    //等待3d处理完成
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待3d图像处理完成"));

                    //while (!taskPoint3D.IsCompleted)
                    //{
                    //    Thread.Sleep(10);
                    //    if (stop) return;
                    //}
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理完成"));

                    //保存检测结果文件
                    if (true && simulation)
                    {
                        foreach (var camID in glueDataDict.Keys)
                        {
                            var camResultDir = glueDataDict[camID];
                            for (int partID = 0; partID < camResultDir.Count; partID++)
                            {
                                var partResultDict = camResultDir[partID];

                                string path = simulationPath + $"\\{camID}_{partID}_result.csv";
                                if (!File.Exists(path))
                                    File.Create(path).Close();

                                using (StreamWriter sw = new StreamWriter(path, true, Encoding.UTF8))
                                {

                                    foreach (var imageID in partResultDict.Keys)
                                    {
                                        var imageResult = partResultDict[imageID];
                                        sw.Write($"{imageResult.glueWidth},");
                                        sw.Write($"{imageResult.glueHeight},");
                                        sw.Write($"{imageResult.glueArea}\r\n");
                                    }
                                }
                            }
                        }
                    }


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
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CarResultRecord carResultRecord = new CarResultRecord();
                        carResultRecord.CarDetTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        carResultRecord.CarID = ID.ToString();
                        carResultRecord.CarResult = totalResult ? "OK" : "NG";

                        mainModel.CarResultRecords.Insert(0, carResultRecord);

                    });

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

                    ////临时测试，保存涂胶坐标
                    //{
                    //    using (FileStream stream = new FileStream($"./resultCenterPointValues.xml", FileMode.Create))
                    //    {
                    //        //转化
                    //        List<double[]> pose = new List<double[]>();
                    //        foreach (var poseKey in ResultCenter3DPoint["Cam3"][0])
                    //        {
                    //            pose.Add(new double[] { poseKey.Value.X, poseKey.Value.Y, poseKey.Value.Z, 0,0,0,2 });
                    //        }
                    //        new XmlSerializer(pose.GetType()).Serialize(stream, pose);
                    //    }
                    //}


                    //等待开始信号OFF
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待开始信号OFF"));
                    while (true)
                    {
                        bool val;
                        if (isStartEnd)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到开始信号OFF"));
                            break;
                        }

                        Thread.Sleep(60);
                        if (stop) return;
                    }
                    //收到信号后，就立刻恢复状态
                    resetSignal();

                    SocketSend(0);

                }
            }
            catch (Exception ex)
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("流程异常：") + ex.ToString(), LogType.ng);
            }
            finally
            {
                robot.Close();
                //io.Close();
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


        public void AcqAndRobotTest()
        {
            try
            {

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
                #endregion

                mainModel.softwareRunLabelColorControl = labelColorEnum["green"];
                //初始化
                resetSignal();

                while (!stop)
                {

                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("输出Ready信号"));

                    //等待开始信号
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待开始信号"));
                    while (true)
                    {
                        bool val;
                        if (isStart)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到开始信号"));
                            break;
                        }

                        Thread.Sleep(60);
                        if (stop) return;
                    }
                    //收到信号后，就立刻恢复状态
                    resetSignal();

                    ushort ID;
                    //Car car;
                    string inVIN;
                    DateTime dateTime;
                    Dictionary<string, CamParam> camParam;
                    Setting set;


                    set = new Setting("1");
                    inVIN = "";
                    camParam = new Dictionary<string, CamParam>();
                    dateTime = DateTime.Now;

                    //清除数据
                    clearData();

                    //开始计时
                    watch.Restart();

                    //获取产品号
                    ID = 0;
                    car = new Car();
                    ID = (ushort)CarNumber;
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
                        return ;
                    }


                    //获取车架号VIN
                    inVIN = "";
                    dateTime = DateTime.Now;
                    mainModel.productIDControl = ID.ToString();
                    mainModel.nameControl = car.Name;
                    mainModel.VINControl = inVIN;
                    mainModel.timeControl = dateTime.ToString("G");
                    mainModel.resultControl = "--";
                    mainModel.resultColorControl = "White";


                    //检测参数是否存在
                    string camParamName = car.CamParamName;
                    if (!Params.Param.TryGetValue(camParamName, out camParam))
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在相机参数：") + camParamName, LogType.ng);
                        return ;
                    }
                    if (!sets.TryGetValue(car.Name, out set))
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在产品参数：") + car.Name, LogType.ng);
                        return ;
                    }
                    if (stop) return ;

                    Setting set_copy = set;
                    //显示NumericalModelDiagram
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DispImageHWindowNumericalModelDiagramEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(set_copy.image));
                    });



                    List<double>[] rowss = new List<double>[set.XLDDatas.Count];
                    List<double>[] colss = new List<double>[set.XLDDatas.Count];
                    List<double>[] angless = new List<double>[set.XLDDatas.Count];
                    for (int i = 0; i < set.XLDDatas.Count; i++)
                    {
                        if (set.XLDDatas[i].ControlRows.Length < 2)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("显示的轨迹没有设置好。"));
                            return ;

                        }
                        if (set.CutSets[i].EndImageIndex > set.CutSets[i].StartImageIndex)
                        {
                            int setCount = set.CutSets[i].EndImageIndex - set.CutSets[i].StartImageIndex + 1;
                            if (setCount < 1) setCount = 1;
                            Vision.XLDDataDivide(set.XLDDatas[i], setCount, out rowss[i], out colss[i], out angless[i]);

                            for (int j = 0; j < setCount; j++)
                            {
                                hWindowNumericalModelDiagramDispCross(rowss[i][j], colss[i][j], angless[i][j], set.CutSets[i].Size, Colors.Blue);
                            }
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("显示的起点图像序号和结束图像序号没有设置好。"));

                            return ;
                        }
                    }
                    if (stop) return ;

                    //连相机
                    foreach (var item in camParam)
                    {
                        item.Value.Key = item.Key;
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

                                        return ;
                                    }
                                }
                                if (cam.InitSet(item.Value, false))
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
                            var dictImageKey = new SynchronizedList<SynchronizedList<long>>();
                            ImageKeys.Add(item.Key, dictImageKey);
                            var dictImage = new SynchronizedList<Dictionary<long, Mat>>();
                            Images.Add(item.Key, dictImage);

                            displaySize.Add(item.Key, new SynchronizedList<System.Windows.Size>());
                        }
                    }
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("初始化数据成功"));

                    indexImageCut = -1;//指示正在图像采集段数
                    totalResult = true;
                    if (stop) return;

                    // 3D 每隔100毫秒再刷新一下结果
                    RefreshOnEvent(500, true);
                    bRobotRun = true;
                    //启动机器人姿态获取(安川20ms)
                    taskRobot = Task.Run(() =>
                    {
                        // 暂时屏蔽
                        RefreshOnEvent(500, true);
                        //form3DShow.RefreshOn(10, true);
                        double colorUpperLimit = -0.5;
                        double colorLowerLimit = 0.5;
                        double rangeSize = colorLowerLimit - colorUpperLimit;
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
                            Thread.Sleep(30);

                            if (stop) break;
                        }

                    });
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("机器人姿态获取任务启动完成"));

                    //回复开始ON信号
                    if (true)
                    {
                        SocketSend(0);
                    }
                    else
                    {
                        SocketSend(-1);
                        return;
                    }

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
                            if (stop)
                            {
                                return;
                            }

                            if (isPGON)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号ON"));
                                break;
                            }
                            if (isEND)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                bEnd = true;
                                break;
                            }
                            if (isAbort)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                bAbort = true;
                                break;
                            }
                            Thread.Sleep(1);
                        }
                        if (bEnd) break;
                        if (bAbort) break;
                        //回复触发ON信号
                        //收到信号后，就立刻恢复状态
                        resetSignal();

                        SocketSend(0);

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


                                var cam = cams[item.Value.CamName];
                                int segmentIndex = indexImageCut + 1;
                                bool CamEnabled = item.Key == "Cam1" ? set.CutSets[segmentIndex].Cam1Enabled :
                                    item.Key == "Cam2" ? set.CutSets[segmentIndex].Cam2Enabled :
                                    item.Key == "Cam3" ? set.CutSets[segmentIndex].Cam3Enabled :
                                    set.CutSets[segmentIndex].Cam4Enabled;
                                displaySize[item.Key].Add(new System.Windows.Size(set.CutSets[segmentIndex].ShowWidth, set.CutSets[segmentIndex].ShowHeight));
                                if (CamEnabled)
                                {
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

                                                }
                                            }
                                        }));
                                        if (flag)
                                        {
                                            ShowMessage(item.Key);
                                            //相机1要软触发启动
                                            if (item.Key == "Cam1")
                                            {
                                                bool rt2 = cam.TriggerSoftwareExecute();
                                                if (rt2)
                                                {
                                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("软触发成功"));
                                                }
                                                else
                                                {
                                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("软触发失败"));
                                                }

                                            }
                                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("开始连续采集成功"));
                                        }
                                        else
                                        {
                                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + item.Key + ":" + item.Value.CamName + GlobalVarAndFunc.LanguageTranslate("开始连续采集失败：") + cams[item.Value.CamName].ErrMsg, LogType.ng);
                                        }
                                    }
                                    
                                }
                            }
                        }
                        indexImageCut++;

                        ////输出拍照中信号
                        //if (!Write(DO.Triggering, true)) return;

                        //等触发信号OFF
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待触发信号OFF"));
                        while (true)
                        {
                            if (stop) return;

                            if (isPGONEnd == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到触发信号OFF"));
                                break;
                            }

                            if (isEND == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到END信号,退出拍照循环"), LogType.warn);
                                bEnd = true;
                                break;
                            }
                            if (isAbort == true)
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到Abort信号,流程重新开始"), LogType.warn);
                                bAbort = true;
                                break;
                            }
                            Thread.Sleep(1);
                        }
                        //收到信号后，就立刻恢复状态
                        resetSignal();

                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待数据转换完成"));

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

                        ////关闭拍照中信号
                        //if (!Write(DO.Triggering, false)) return;

                        if (stop) return;
                        if (bEnd) break;
                        if (bAbort) break;

                        //回复触发OFF信号

                        if (totalResult)
                        {
                            SocketSend(0);
                        }
                        else
                        {
                            SocketSend(-1);
                        }

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

                    ////等待3d显示完毕
                    //while (!taskShow3D.IsCompleted)
                    //{
                    //    Thread.Sleep(10);
                    //    if (stop) return;
                    //}

                    //等待3d处理完成
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待3d图像处理完成"));

                    //while (!taskPoint3D.IsCompleted)
                    //{
                    //    Thread.Sleep(10);
                    //    if (stop) return;
                    //}
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("图像处理完成"));

                    //保存检测结果文件
                    if (true && simulation)
                    {
                        foreach (var camID in glueDataDict.Keys)
                        {
                            var camResultDir = glueDataDict[camID];
                            for (int partID = 0; partID < camResultDir.Count; partID++)
                            {
                                var partResultDict = camResultDir[partID];

                                string path = simulationPath + $"\\{camID}_{partID}_result.csv";
                                if (!File.Exists(path))
                                    File.Create(path).Close();

                                using (StreamWriter sw = new StreamWriter(path, true, Encoding.UTF8))
                                {

                                    foreach (var imageID in partResultDict.Keys)
                                    {
                                        var imageResult = partResultDict[imageID];
                                        sw.Write($"{imageResult.glueWidth},");
                                        sw.Write($"{imageResult.glueHeight},");
                                        sw.Write($"{imageResult.glueArea}\r\n");
                                    }
                                }
                            }
                        }
                    }


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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CarResultRecord carResultRecord = new CarResultRecord();
                        carResultRecord.CarDetTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        carResultRecord.CarID = ID.ToString();
                        carResultRecord.CarResult = totalResult ? "OK" : "NG";

                        mainModel.CarResultRecords.Insert(0, carResultRecord);

                    });

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

                    //等待开始信号OFF
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("等待开始信号OFF"));
                    while (true)
                    {
                        bool val;
                        if (isStartEnd)
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("收到开始信号OFF"));
                            break;
                        }

                        Thread.Sleep(60);
                        if (stop) return;
                    }
                    //收到信号后，就立刻恢复状态
                    resetSignal();

                    SocketSend(0);

                }
            }
            catch (Exception ex)
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("流程异常：") + ex.ToString(), LogType.ng);
            }
            finally
            {
                //robot.Close();


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


        private bool initRun(out ushort ID, out Car car, out string inVIN, out DateTime dateTime, out Dictionary<string, CamParam> camParam, out Setting set)
        {
            set = new Setting("1");
            inVIN = "";
            camParam = new Dictionary<string, CamParam>();
            dateTime = DateTime.Now;

            //清除数据
            clearData();

            //获取产品号
            ID = 0;
            car = new Car();
            ID = (ushort)CarNumber;
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
                return false; 
            }


            //获取车架号VIN
            inVIN = "";
            dateTime = DateTime.Now;
            mainModel.productIDControl = ID.ToString();
            mainModel.nameControl = car.Name;
            mainModel.VINControl = inVIN;
            mainModel.timeControl = dateTime.ToString("G");
            mainModel.resultControl = "--";
            mainModel.resultColorControl = "White";


            //检测参数是否存在
            string camParamName = car.CamParamName;
            if (!Params.Param.TryGetValue(camParamName, out camParam))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在相机参数：") + camParamName, LogType.ng);
                return false;
            }
            if (!sets.TryGetValue(car.Name, out set))
            {
                ShowMessage(GlobalVarAndFunc.LanguageTranslate("不存在产品参数：") + car.Name, LogType.ng);
                return false;
            }
            if (stop) return false;

            Setting set_copy = set;
            //显示NumericalModelDiagram
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DispImageHWindowNumericalModelDiagramEvent(GlobalVarAndFunc.ConvertMatToBitmapImage(set_copy.image));
            });



            List<double>[] rowss = new List<double>[set.XLDDatas.Count];
            List<double>[] colss = new List<double>[set.XLDDatas.Count];
            List<double>[] angless = new List<double>[set.XLDDatas.Count];
            for (int i = 0; i < set.XLDDatas.Count; i++)
            {
                if (set.XLDDatas[i].ControlRows.Length < 2)
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("显示的轨迹没有设置好。"));
                    return false; 

                }
                if (set.CutSets[i].EndImageIndex > set.CutSets[i].StartImageIndex)
                {
                    int setCount = set.CutSets[i].EndImageIndex - set.CutSets[i].StartImageIndex + 1;
                    if (setCount < 1) setCount = 1;
                    Vision.XLDDataDivide(set.XLDDatas[i], setCount, out rowss[i], out colss[i], out angless[i]);

                    for (int j = 0; j < setCount; j++)
                    {
                        hWindowNumericalModelDiagramDispCross(rowss[i][j], colss[i][j], angless[i][j], set.CutSets[i].Size, Colors.Blue);
                    }
                }
                else
                {
                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("显示的起点图像序号和结束图像序号没有设置好。"));

                    return false;
                }
            }
            if (stop) return false;

            //连相机
            foreach (var item in camParam)
            {
                item.Value.Key = item.Key;
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

                                return false;
                            }
                        }
                        if (cam.InitSet(item.Value, false))
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
                            if (Params.LightToCam.ContainsKey(camParamName) && Params.LightToCam[camParamName].ContainsKey(item.Key))
                            {
                                if (Params.CamToCam1.ContainsKey(camParamName) && Params.CamToCam1[camParamName].ContainsKey(item.Key))
                                {
                                    if (Params.CenterToCam1.ContainsKey(camParamName) && Params.CenterToCam1[camParamName].ContainsKey(item.Key))
                                    {
                                        if (Params.CamHandEyeType.ContainsKey(camParamName) && Params.CamHandEyeType[camParamName] == 0)
                                        {
                                            if (Params.Cam1ToTool.ContainsKey(camParamName) && Params.Cam1ToTool[camParamName].ContainsKey(item.Key))
                                            {

                                            }
                                            else
                                            {
                                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(CamToTool)不存在"), LogType.ng);
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            if (Params.Cam1ToBase.ContainsKey(camParamName) && Params.Cam1ToBase[camParamName].ContainsKey(item.Key))
                                            {

                                            }
                                            else
                                            {
                                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(CamToBase)不存在"), LogType.ng);
                                                return false;
                                            }
                                        }

                                    }
                                    else
                                    {
                                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(CenterToCam1)不存在"), LogType.ng);
                                        return false;
                                    }

                                }
                                else
                                {
                                    ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("多相机转换(CamToCam1)不存在"), LogType.ng);
                                    return false;
                                }

                               
                            }
                            else
                            {
                                ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("坐标转换(LightToCam)不存在"), LogType.ng);
                                return false;
                            }
                        }
                        else
                        {
                            ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("外参(LightInCam.dat)不存在"), LogType.ng);
                            return false;
                        }
                    }
                    else
                    {
                        ShowMessage(GlobalVarAndFunc.LanguageTranslate("相机") + $" ({item.Key}:{item.Value.CamName})" + GlobalVarAndFunc.LanguageTranslate("内参(camparam.cal)不存在"), LogType.ng);
                        return false;
                    }

                    var dictImageKey = new SynchronizedList<SynchronizedList<long>>();
                    ImageKeys.Add(item.Key, dictImageKey);
                    var dictImage = new SynchronizedList<Dictionary<long, Mat>>();
                    Images.Add(item.Key, dictImage);

                    var dictRobotPose = new SynchronizedList<Dictionary<long, PoseParameters>>();
                    Robot3DPose.Add(item.Key, dictRobotPose);

                    var dictResultCenter3DPoint = new SynchronizedList<Dictionary<long, Point3D>>();
                    ResultCenter3DPoint.Add(item.Key, dictResultCenter3DPoint);


                    var dictX = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DXs.Add(item.Key, dictX);
                    var dictY = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DYs.Add(item.Key, dictY);
                    var dictZ = new SynchronizedList<Dictionary<long, List<double>>>();
                    Point3DZs.Add(item.Key, dictZ);
                    var dictXLD = new SynchronizedList<Dictionary<long, Mat>>();

                    var dictV = new SynchronizedList<Dictionary<long, double>>();
                    glueVols.Add(item.Key, dictV);

                    outLineDict.Add(item.Key, dictXLD);
                    var dictRegion = new SynchronizedList<Dictionary<long, Mat>>();
                    glueRegionDict.Add(item.Key, dictRegion);
                    var dictRegionRectangle2 = new SynchronizedList<Dictionary<long, Mat>>();
                    glueSmallRectRegionDict.Add(item.Key, dictRegionRectangle2);
                    var dictData = new SynchronizedList<Dictionary<long, Data>>();
                    glueDataDict.Add(item.Key, dictData);
                    var dictResult = new SynchronizedList<Dictionary<long, BResult>>();
                    glueResultDict.Add(item.Key, dictResult);

                    indexImageCutProcessDict.Add(item.Key, 0);

                    displaySize.Add(item.Key, new SynchronizedList<System.Windows.Size>());
                }
            }
            ShowMessage(GlobalVarAndFunc.LanguageTranslate("初始化数据成功"));

            indexImageCut = -1;//指示正在图像采集段数
            totalResult = true;
            if (stop) return false;

            // 3D 每隔100毫秒再刷新一下结果
            RefreshOnEvent(500, true);
            bRobotRun = true;
            watch.Restart();
            //启动机器人姿态获取(安川20ms)
            taskRobot = Task.Run(() =>
            {
                // 暂时屏蔽
                RefreshOnEvent(500, true);
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
                        ////暂时定间隔20ms
                        //Thread.Sleep(20);

                        //加快到5ms,机器人设置太快不行
                        //Thread.Sleep(5);

                        //暂时先30，后面在测试一下加快
                        Thread.Sleep(30);

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
                    /// 相机转相机1坐标
                    var CamToCam1 = Params.CamToCam1[camParamName][item.Key];
                    /// 中心坐标转相机1
                    var CenterToCam1 = Params.CenterToCam1[camParamName][item.Key];

                    /// 相机1坐标转为法兰盘坐标
                    Mat Cam1ToTool = new Mat();
                    Mat Cam1ToBase = new Mat();
                    Mat CamToTool = new Mat();
                    Mat CamToBase = new Mat();

                    if (Params.CamHandEyeType[camParamName] == 0)
                    {
                        Cam1ToTool = Params.Cam1ToTool[camParamName][item.Key];
                        CamToTool = Cam1ToTool * CamToCam1;
                    }
                    else
                    {
                        //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                        Cam1ToBase = Params.Cam1ToBase[camParamName][item.Key];
                        CamToBase = Cam1ToBase * CamToCam1;
                    }

                    tasks.Add(item.Key, Task.Run((Action)(() =>
                    {
                        while (indexImageCut < 0)//等待采集开始，数据集合完成添加
                        {
                            Thread.Sleep(10);
                            if (!bRobotRun) return;
                            if (stop) return;
                        }
                        int indexRobotPose = 1;
                        //int indexImageCutProcessDict[item.Key] = 0;//指示正在图像处理段数

                        indexImageCutProcessDict[item.Key] = 0;
                        while (true)//分段循环
                        {
                            var dictImageKey = ImageKeys[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictImage = Images[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictRobotPose = Robot3DPose[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictResultCenter3DPoint = ResultCenter3DPoint[item.Key][indexImageCutProcessDict[item.Key]];

                            var dictX = Point3DXs[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictY = Point3DYs[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictZ = Point3DZs[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictV = glueVols[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictXLD = outLineDict[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictRegion = glueRegionDict[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictRegionRectangle2 = glueSmallRectRegionDict[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictData = glueDataDict[item.Key][indexImageCutProcessDict[item.Key]];
                            var dictResult = glueResultDict[item.Key][indexImageCutProcessDict[item.Key]];

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
                                        Thread.Sleep(10);
                                    }
                                }
                                else//没有新增图片
                                {
                                    if (indexImageCut > indexImageCutProcessDict[item.Key])//进入下一段条件
                                    {
                                        indexImageCutProcessDict[item.Key]++;
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
                                        int camIndex = item.Key == "Cam1" ? 0 : item.Key == "Cam2" ? 1 : item.Key == "Cam3" ? 2 : 3;
                                        if (set_copy.CutSets.Count > indexImageCutProcessDict[item.Key] && set_copy.CutSets[indexImageCutProcessDict[item.Key]].imageSet.Count > camIndex && set_copy.CutSets[indexImageCutProcessDict[item.Key]].imageSet[camIndex].Count > indexImage)
                                        {
                                            var cutSet = set_copy.CutSets[indexImageCutProcessDict[item.Key]];
                                            var imageSet = set_copy.CutSets[indexImageCutProcessDict[item.Key]].imageSet[camIndex][indexImage];
                                            // 结果保存变量
                                            bool getOutlineResult = false;
                                            bool singleFrameExistOutline = false;
                                            bool singleFrameExistGlue = false;
                                            Data resultData = new Data();
                                            BResult bResult = new BResult();
                                            Mat outMaxRegion = new Mat();
                                            Mat outRegionRectangle2 = new Mat();
                                            Mat hXLDCont10mm = new Mat();
                                            List<double> robotX, robotY, robotZ, colorScale;
                                            robotX = new List<double>();
                                            robotY = new List<double>();
                                            robotZ = new List<double>();
                                            Mat lightXY = new Mat();
                                            double PoseD = 0;
                                            double V = 0;

                                            Point3D resultCenterPoint = new Point3D();

                                            double robotAndCamAngle = int.MaxValue;

                                            //坐标转换
                                            Wpf_Replace_halcon.PoseParameters robotPose = new PoseParameters();
                                            HMatrixTransform.mathHPose(robotPoseValues[indexRobotPose - 1],
                                                robotPoseValues[indexRobotPose], out robotPose,
                                                (imageKey - robotPoseKeys[indexRobotPose - 1]) /
                                                (double)(robotPoseKeys[indexRobotPose] - robotPoseKeys[indexRobotPose - 1])
                                                );
                                            // 计算机器人移动距离
                                            if (dictRobotPose.Count > 0)
                                            {
                                                var last = dictRobotPose.Last();
                                                var lastRobotPose = last.Value;

                                                PoseD = Math.Sqrt(Math.Pow((robotPose.x - lastRobotPose.x), 2) +
                                                    Math.Pow((robotPose.y - lastRobotPose.y), 2) +
                                                    Math.Pow((robotPose.z - lastRobotPose.z), 2));
                                            }

                                            //三维数据添加机器人坐标
                                            if (Params.CamHandEyeType[camParamName] == 0)
                                            {
                                                //改为添加相机中心的坐标
                                                Mat ToolToBase = new Mat();
                                                Mat CenterToBase = new Mat();
                                                PoseParameters centerInBase = new PoseParameters();
                                                Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                CenterToBase = ToolToBase * Cam1ToTool * CenterToCam1;
                                                centerInBase.PoseType = 2;
                                                Vision.HomMat3dToPose(centerInBase.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToBase.CvPtr);
                                                centerInBase.x = centerX;
                                                centerInBase.y = centerY;
                                                centerInBase.z = centerZ;
                                                centerInBase.rx = centerRX;
                                                centerInBase.ry = centerRY;
                                                centerInBase.rz = centerRZ;
                                                //dictRobotPose.Add(imageKey, centerInBase);
                                            }
                                            else
                                            {
                                                //也改为添加相机中心的坐标，但是这里是法兰盘的坐标系
                                                PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                                                Mat BaseToTool = new Mat();
                                                PoseParameters centerInTool = new PoseParameters();
                                                Vision.poseToHomMat3d(BaseInTool.PoseType, BaseInTool.x, BaseInTool.y, BaseInTool.z, BaseInTool.rx, BaseInTool.ry, BaseInTool.rz, BaseToTool.CvPtr);
                                                Mat CenterToTool = new Mat();
                                                CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;
                                                centerInTool.PoseType = 2;
                                                Vision.HomMat3dToPose(centerInTool.PoseType, out double centerX, out double centerY, out double centerZ, out double centerRX, out double centerRY, out double centerRZ, CenterToTool.CvPtr);
                                                centerInTool.x = centerX;
                                                centerInTool.y = centerY;
                                                centerInTool.z = centerZ;
                                                centerInTool.rx = centerRX;
                                                centerInTool.ry = centerRY;
                                                centerInTool.rz = centerRZ;
                                                //dictRobotPose.Add(imageKey, centerInTool);

                                            }
                                            dictRobotPose.Add(imageKey, robotPose);

                                            if (imageSet.轮廓检测)
                                            {
                                                //ROI裁剪
                                                Mat xy = new Mat();
                                                Mat imgCut = new Mat();
                                                int LeftX = 0;
                                                int TopY = 0;
                                                if (imageSet.启用裁剪)
                                                {
                                                    int imageWidth, imageHeight;
                                                    imageWidth = dictImage[imageKey].Cols;
                                                    imageHeight = dictImage[imageKey].Rows;

                                                    LeftX = (int)(imageWidth * imageSet.LeftX);
                                                    TopY = (int)(imageHeight * imageSet.TopY);
                                                    int cutWidth = (int)((imageSet.RightX - imageSet.LeftX) * imageWidth);
                                                    int cutHeight = (int)((imageSet.DownY - imageSet.TopY) * imageHeight);
                                                    imgCut = new Mat(dictImage[imageKey], new OpenCvSharp.Rect(LeftX, TopY, cutWidth, cutHeight));
                                                }
                                                else
                                                {
                                                    imgCut = dictImage[imageKey].Clone();
                                                }
                                                //激光轮廓提取
                                                Vision.getLaserPosition(imgCut, imageSet.minThreshold, imageSet.laserMinWidth, out xy, item.Value.OffsetX + LeftX, item.Value.OffsetY + TopY);

                                                // 计算机器人与相机的夹角
                                                #region 使用机器人移动轨迹计算夹角
                                                // 必须要机器人有移动
                                                int intervalCount = 1;
                                                if (dictRobotPose.Count > intervalCount +1 && PoseD > 0)
                                                {
                                                    //计算CamToTool的矩阵
                                                    //打包前后机器人pose
                                                    //var last = dictRobotPose.Last();
                                                    //var last = dictRobotPose.Skip(dictRobotPose.Count - 2).First();
                                                    var last = dictRobotPose.Skip(dictRobotPose.Count - intervalCount - 1).First();
                                                    var lastRobotPose = last.Value;
                                                    if (Params.CamHandEyeType[camParamName] == 0)
                                                    {
                                                        Mat robotPoseMat = new Mat();
                                                        robotPoseMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                                                        robotPoseMat.At<double>(0, 0) = lastRobotPose.x;
                                                        robotPoseMat.At<double>(0, 1) = lastRobotPose.y;
                                                        robotPoseMat.At<double>(0, 2) = lastRobotPose.z;
                                                        robotPoseMat.At<double>(0, 3) = lastRobotPose.rx;
                                                        robotPoseMat.At<double>(0, 4) = lastRobotPose.ry;
                                                        robotPoseMat.At<double>(0, 5) = lastRobotPose.rz;
                                                        robotPoseMat.At<double>(0, 6) = lastRobotPose.PoseType;

                                                        robotPoseMat.At<double>(1, 0) = robotPose.x;
                                                        robotPoseMat.At<double>(1, 1) = robotPose.y;
                                                        robotPoseMat.At<double>(1, 2) = robotPose.z;
                                                        robotPoseMat.At<double>(1, 3) = robotPose.rx;
                                                        robotPoseMat.At<double>(1, 4) = robotPose.ry;
                                                        robotPoseMat.At<double>(1, 5) = robotPose.rz;
                                                        robotPoseMat.At<double>(1, 6) = robotPose.PoseType;

                                                        Mat ToolToBase = new Mat();
                                                        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                        CamToBase = ToolToBase * CamToTool;

                                                        Vision.robotAndCamVectorAngle(robotPoseMat.CvPtr, CamToBase.CvPtr, 2, 0, out robotAndCamAngle);
                                                        //大于90的，都取缩小后的值
                                                        if (robotAndCamAngle > 90)
                                                        {
                                                            robotAndCamAngle = 180 - robotAndCamAngle;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Mat camInTools = new Mat();
                                                        camInTools = Mat.Zeros(2, 7, MatType.CV_64FC1);

                                                        //这里直接使用Cam2Tool，后面可以使用Center2Tool
                                                        //轨迹的前一个点，要转成center2Tool
                                                        {
                                                            Mat ToolToBase = new Mat();
                                                            Mat BaseToTool = new Mat();
                                                            Mat CenterToTool = new Mat();
                                                            Vision.poseToHomMat3d(lastRobotPose.PoseType, lastRobotPose.x, lastRobotPose.y, lastRobotPose.z,
                                                                lastRobotPose.rx, lastRobotPose.ry, lastRobotPose.rz, ToolToBase.CvPtr);

                                                            BaseToTool = ToolToBase.Inv();

                                                            CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                                            double x, y, z, rx, ry, rz;
                                                            Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                                            camInTools.At<double>(0, 0) = x;
                                                            camInTools.At<double>(0, 1) = y;
                                                            camInTools.At<double>(0, 2) = z;
                                                            camInTools.At<double>(0, 3) = rx;
                                                            camInTools.At<double>(0, 4) = ry;
                                                            camInTools.At<double>(0, 5) = rz;
                                                            camInTools.At<double>(0, 6) = 2;

                                                        }
                                                        //轨迹的后一个点，要转成center2Tool
                                                        {
                                                            Mat ToolToBase = new Mat();
                                                            Mat BaseToTool = new Mat();
                                                            Mat CenterToTool = new Mat();
                                                            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z,
                                                                robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);

                                                            BaseToTool = ToolToBase.Inv();

                                                            CenterToTool = BaseToTool * Cam1ToBase * CenterToCam1;

                                                            double x, y, z, rx, ry, rz;
                                                            Vision.HomMat3dToPose(2, out x, out y, out z, out rx, out ry, out rz, CenterToTool.CvPtr);

                                                            camInTools.At<double>(1, 0) = x;
                                                            camInTools.At<double>(1, 1) = y;
                                                            camInTools.At<double>(1, 2) = z;
                                                            camInTools.At<double>(1, 3) = rx;
                                                            camInTools.At<double>(1, 4) = ry;
                                                            camInTools.At<double>(1, 5) = rz;
                                                            camInTools.At<double>(1, 6) = 2;

                                                        }

                                                        //检测的相机位姿
                                                        {
                                                            //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                                                            //Mat BaseToTool = robotPoseMat.Inv();
                                                            Mat ToolToBase = new Mat();
                                                            Mat BaseToTool = new Mat();
                                                            Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                            BaseToTool = ToolToBase.Inv();

                                                            CamToTool = BaseToTool * Cam1ToBase * CamToCam1;

                                                            Vision.robotAndCamVectorAngle(camInTools.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                                                            //眼在手外，要减180度
                                                            robotAndCamAngle = 180 - robotAndCamAngle;
                                                            //大于90的，都取缩小后的值
                                                            if (robotAndCamAngle > 90)
                                                            {
                                                                robotAndCamAngle = 180 - robotAndCamAngle;
                                                            }
                                                        }
                                                    }
                                                    Console.WriteLine($"imageKey:{imageKey}");
                                                    Console.WriteLine($"robotAndCamAngle:{robotAndCamAngle}");

                                                }
                                                #endregion

                                                #region 使用胶的中心坐标，计算夹角
                                                //// 必须要有10个以上涂胶中心
                                                //int intervalCount = 20;
                                                //int smoothCount = 4;
                                                //if (dictResultCenter3DPoint.Count > intervalCount && PoseD > 0)
                                                //{
                                                //    //计算CamToTool的矩阵
                                                //    //打包前后机器人pose
                                                //    //var last = dictResultCenter3DPoint.Last();
                                                //    //var lastResultCenter3DPoint = last.Value;
                                                //    //var currentResultCenter3DPoint = dictResultCenter3DPoint.Skip(dictResultCenter3DPoint.Count - intervalCount - 1).First().Value;

                                                //    Point3D[] points = dictResultCenter3DPoint.Values.ToArray();
                                                //    var lastResultCenter3DPoint = Vision.GaussianSmoothInRange(points, dictResultCenter3DPoint.Count - 1 - intervalCount, 
                                                //        dictResultCenter3DPoint.Count - 1, dictResultCenter3DPoint.Count - 1, smoothCount);
                                                //    var currentResultCenter3DPoint = Vision.GaussianSmoothInRange(points, dictResultCenter3DPoint.Count - 1 - intervalCount,
                                                //        dictResultCenter3DPoint.Count - 1, dictResultCenter3DPoint.Count - 1 - intervalCount, smoothCount);

                                                //    Mat ResultCenterMat = new Mat();
                                                //    ResultCenterMat = Mat.Zeros(2, 7, MatType.CV_64FC1);
                                                //    ResultCenterMat.At<double>(0, 0) = lastResultCenter3DPoint.X;
                                                //    ResultCenterMat.At<double>(0, 1) = lastResultCenter3DPoint.Y;
                                                //    ResultCenterMat.At<double>(0, 2) = lastResultCenter3DPoint.Z;
                                                //    ResultCenterMat.At<double>(0, 3) = 0;
                                                //    ResultCenterMat.At<double>(0, 4) = 0;
                                                //    ResultCenterMat.At<double>(0, 5) = 0;
                                                //    ResultCenterMat.At<double>(0, 6) = 2;

                                                //    ResultCenterMat.At<double>(1, 0) = currentResultCenter3DPoint.X;
                                                //    ResultCenterMat.At<double>(1, 1) = currentResultCenter3DPoint.Y;
                                                //    ResultCenterMat.At<double>(1, 2) = currentResultCenter3DPoint.Z;
                                                //    ResultCenterMat.At<double>(1, 3) = 0;
                                                //    ResultCenterMat.At<double>(1, 4) = 0;
                                                //    ResultCenterMat.At<double>(1, 5) = 0;
                                                //    ResultCenterMat.At<double>(1, 6) = 2;
                                                //    if (Params.CamHandEyeType[camParamName] == 0)
                                                //    {


                                                //        Mat ToolToBase = new Mat();
                                                //        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                //        CamToBase = ToolToBase * CamToTool;

                                                //        Vision.robotAndCamVectorAngle(ResultCenterMat.CvPtr, CamToBase.CvPtr, 2, 0, out robotAndCamAngle);
                                                //        //大于90的，都取缩小后的值
                                                //        if (robotAndCamAngle > 90)
                                                //        {
                                                //            robotAndCamAngle = 180 - robotAndCamAngle;
                                                //        }
                                                //    }
                                                //    else
                                                //    {
                                                //        //眼在手外，求Cam1ToTool,需要机器人pose才可以完成转换
                                                //        Mat ToolToBase = new Mat();
                                                //        Mat BaseToTool = new Mat();
                                                //        Vision.poseToHomMat3d(robotPose.PoseType, robotPose.x, robotPose.y, robotPose.z, robotPose.rx, robotPose.ry, robotPose.rz, ToolToBase.CvPtr);
                                                //        BaseToTool = ToolToBase.Inv();

                                                //        CamToTool = BaseToTool * Cam1ToBase * CamToCam1;

                                                //        Vision.robotAndCamVectorAngle(ResultCenterMat.CvPtr, CamToTool.CvPtr, 2, 0, out robotAndCamAngle);

                                                //        //大于90的，都取缩小后的值
                                                //        if (robotAndCamAngle > 90)
                                                //        {
                                                //            robotAndCamAngle = 180 - robotAndCamAngle;
                                                //        }
                                                //    }

                                                //    Console.WriteLine($"imageKey:{imageKey}");
                                                //    Console.WriteLine($"robotAndCamAngle2:{robotAndCamAngle}");
                                                //}
                                                #endregion

                                                //需要保证检测到有点，并且机器人已经处于移动状态
                                                if (xy.Rows > 0 && dictRobotPose.Count > 0 && PoseD > 0)
                                                {
                                                    getOutlineResult = true;

                                                    if (Params.CamHandEyeType[camParamName] == 0)
                                                    {
                                                        Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToTool,
                                                    robotPose, out lightXY, out robotX, out robotY, out robotZ);
                                                    }
                                                    else 
                                                    {
                                                        //搞个robot的逆pose，后面再专门打包个算法搞逆pose
                                                        PoseParameters BaseInTool = Vision.PoseInv(robotPose);
                                                        Vision.pointTransform2CamAndRobot(xy, hCamPar, LightInCam, LightToCam, CamToBase,
                                                            BaseInTool, out lightXY, out robotX, out robotY, out robotZ);
                                                    }

                                                    //如果还没到开始id，则跳过显示
                                                    int indexCross = indexImage - cutSet.StartImageIndex;
                                                    if (indexCross >= 0 && indexCross < angless[indexImageCutProcessDict[item.Key]].Count)
                                                    {
                                                        //间隔显示，减少显示时间
                                                        if (indexCross % displayIntervalID == 0)
                                                        {
                                                            colorScale = new List<double>();
                                                            //计算显示颜色
                                                            for (int i = 0; i < robotZ.Count; i++)
                                                            {
                                                                double color = ((robotZ[i] - cutSet.ShowColorMin / 1000) / ((cutSet.ShowColorMax - cutSet.ShowColorMin) / 1000));

                                                                colorScale.Add(color);
                                                            }
                                                            //Application.Current.Dispatcher.Invoke(() =>
                                                            //{
                                                            //显示点云
                                                            Disp3DPointControlEvent(robotX, robotY, robotZ, colorScale);

                                                            //Console.WriteLine($"indexCross:{indexCross}");
                                                            //});
                                                        }

                                                    }
                                                    //三维数据添加
                                                    dictX.Add(imageKey, robotX);
                                                    dictY.Add(imageKey, robotY);
                                                    dictZ.Add(imageKey, robotZ);
                                                    //}


                                                }
                                            }

                                            // 单帧检测
                                            if (imageSet.轮廓检测)
                                            {
                                                if (imageSet._3DGlueDet)
                                                {

                                                    if (lightXY.Rows > 0)
                                                    {

                                                        singleFrameExistOutline = true;
                                                        Vision.scalePoint(lightXY, cutSet, 90 - LightInCam.rx, out hXLDCont10mm);
                                                        if (imageSet.isUseAngleOpt)
                                                        {
                                                            //对x方向进行矫正
                                                            double scaleX = 1;
                                                            scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                                                            Mat correctionPoints = new Mat();
                                                            correctionPoints = hXLDCont10mm.Clone();

                                                            for (int id = 0; id < correctionPoints.Rows; id++)
                                                            {
                                                                correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                                            }

                                                            hXLDCont10mm = correctionPoints.Clone();
                                                        }
                                                        {
                                                            //对两个方向进行矫正
                                                            double scaleX = imageSet.correctionScaleSizeX;
                                                            double scaleY = imageSet.correctionScaleSizeY;

                                                            Mat correctionPoints = new Mat();
                                                            correctionPoints = hXLDCont10mm.Clone();

                                                            for (int id = 0; id < correctionPoints.Rows; id++)
                                                            {
                                                                correctionPoints.At<double>(id, 0) = correctionPoints.At<double>(id, 0) * scaleX;
                                                                correctionPoints.At<double>(id, 1) = correctionPoints.At<double>(id, 1) * scaleY;

                                                            }

                                                            hXLDCont10mm = correctionPoints.Clone();
                                                        }
                                                        //如果存在
                                                        if (!hXLDCont10mm.Empty())
                                                        {

                                                            Mat hXLDContPorcess = new Mat();
                                                            //离散滤波
                                                            if (imageSet.离散去噪)
                                                            {
                                                                Vision.TrajectoryDiscreteFilter(hXLDCont10mm, out hXLDContPorcess, imageSet.分段距离 * cutSet.scaleSize, imageSet.成段点数);
                                                            }
                                                            else
                                                            {
                                                                hXLDContPorcess = hXLDCont10mm.Clone();
                                                            }

                                                            Vision.singleFrameDetAndResult(hXLDContPorcess, imageSet, cutSet, ref singleFrameExistGlue, ref resultData, ref bResult, ref outMaxRegion, ref outRegionRectangle2);

                                                            //计算涂胶体积
                                                            V = resultData.glueArea * PoseD;

                                                            #region 计算胶3d中点坐标
                                                            //double resultCenter2dX = resultData.column;
                                                            //double resultCenter2dY = resultData.row;
                                                            //List<double> resultCenter3dX, resultCenter3dY, resultCenter3dZ;

                                                            //// 先把2d坐标变回来
                                                            //{
                                                            //    //除以矫正
                                                            //    {
                                                            //        double scaleX = cutSet.correctionScaleSizeX;
                                                            //        double scaleY = cutSet.correctionScaleSizeY;

                                                            //        resultCenter2dX/=scaleX;
                                                            //        resultCenter2dY/=scaleY;
                                                            //    }
                                                            //    //除以夹角矫正
                                                            //    if (imageSet.isUseAngleOpt)
                                                            //    {
                                                            //        double scaleX = 1;
                                                            //        scaleX = Math.Cos(robotAndCamAngle / 180 * Math.PI);
                                                            //        resultCenter2dX/= scaleX;
                                                            //    }
                                                            //    //y方向矫正、光平面的
                                                            //    {
                                                            //        //XY_10um = new Mat(lightXYcut.Size(), lightXYcut.Type());
                                                            //        ////X
                                                            //        //Cv2.Multiply(lightXYcut.Col(0), new Scalar(1000 * cutSet.scaleSize), XY_10um.Col(0));
                                                            //        //Cv2.Add(XY_10um.Col(0), new Scalar(cutSet.ShowWidth * cutSet.scaleSize / 2), XY_10um.Col(0));
                                                            //        ////Y
                                                            //        //Cv2.Multiply(lightXYcut.Col(1), new Scalar(1000 * cutSet.scaleSize * Math.Cos(lightAngle / 180 * Math.PI)), XY_10um.Col(1));
                                                            //        //Cv2.Add(XY_10um.Col(1), new Scalar(cutSet.ShowHeight * cutSet.scaleSize / 2), XY_10um.Col(1));


                                                            //        resultCenter2dX -= cutSet.scaleSize * cutSet.ShowWidth / 2;
                                                            //        resultCenter2dX /= cutSet.scaleSize * 1000 ;


                                                            //        double scaleY = Math.Cos((90 - LightInCam.rx) / 180 * Math.PI);
                                                            //        resultCenter2dY -= cutSet.scaleSize * cutSet.ShowHeight / 2;
                                                            //        resultCenter2dY /= (cutSet.scaleSize * scaleY * 1000);
                                                            //    }
                                                            //}
                                                            ////转3d坐标
                                                            //{
                                                            //    Mat resultCenter2d = Mat.Zeros(1, 2, MatType.CV_64FC1);
                                                            //    resultCenter2d.At<double>(0, 0) = resultCenter2dX;
                                                            //    resultCenter2d.At<double>(0, 1) = resultCenter2dY;

                                                            //    //double x = lightXY.At<double>(0, 0);
                                                            //    //double y = lightXY.At<double>(0, 1);

                                                            //    //double x2 = hXLDCont10mm.At<double>(0, 0);
                                                            //    //double y2 = hXLDCont10mm.At<double>(0, 1);

                                                            //    //Vision.scalePoint(lightXY, cutSet, 90 - LightInCam.rx, out Mat hXLDCont10mmTest);

                                                            //    //double x3 = hXLDCont10mmTest.At<double>(0, 0);
                                                            //    //double y3 = hXLDCont10mmTest.At<double>(0, 1);

                                                            //    //resultCenter2d.At<double>(0, 0) = lightXY.At<double>(0, 0);
                                                            //    //resultCenter2d.At<double>(0, 1) = lightXY.At<double>(0, 1);


                                                            //    if (Params.CamHandEyeType[camParamName] == 0)
                                                            //    {
                                                            //        Vision.pointTransform2LightAndRobot(resultCenter2d,  LightToCam, CamToTool,
                                                            //    robotPose, out resultCenter3dX, out resultCenter3dY, out resultCenter3dZ);
                                                            //    }
                                                            //    else
                                                            //    {
                                                            //        //搞个robot的逆pose，后面再专门打包个算法搞逆pose
                                                            //        PoseParameters BaseInTool = Vision.PoseInv(robotPose);

                                                            //        Vision.pointTransform2LightAndRobot(resultCenter2d, LightToCam, CamToBase,
                                                            //            BaseInTool, out resultCenter3dX, out resultCenter3dY, out resultCenter3dZ);

                                                            //    }

                                                            //}
                                                            //// 记录结果
                                                            //resultCenterPoint.X = resultCenter3dX[0];
                                                            //resultCenterPoint.Y = resultCenter3dY[0];
                                                            //resultCenterPoint.Z = resultCenter3dZ[0];

                                                            #endregion


                                                        }
                                                        else
                                                        {
                                                            bResult.Result = false;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        bResult.Result = false;
                                                    }
                                                }
                                                if (!bResult.Result)
                                                {
                                                    totalResult = false;
                                                }
                                                //结果统计
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
                                                if (resultData.glueArea > 0)
                                                {
                                                    //体积结果
                                                    if (dictV.ContainsKey(imageKey))
                                                    {
                                                        dictV[imageKey] = V;
                                                    }
                                                    else
                                                    {
                                                        dictV.Add(imageKey, V);
                                                    }
                                                    //胶区域结果
                                                    if (dictRegion.ContainsKey(imageKey))
                                                    {
                                                        dictRegion[imageKey] = outMaxRegion;
                                                    }
                                                    else
                                                    {
                                                        dictRegion.Add(imageKey, outMaxRegion);
                                                    }
                                                    //胶最小外接矩形结果
                                                    if (dictRegionRectangle2.ContainsKey(imageKey))
                                                    {
                                                        dictRegionRectangle2[imageKey] = outRegionRectangle2;
                                                    }
                                                    else
                                                    {
                                                        dictRegionRectangle2.Add(imageKey, outRegionRectangle2);
                                                    }
                                                    //胶检测数据结果
                                                    if (dictData.ContainsKey(imageKey))
                                                    {
                                                        dictData[imageKey] = resultData;
                                                    }
                                                    else
                                                    {
                                                        dictData.Add(imageKey, resultData);
                                                    }
                                                    //胶检测结果
                                                    if (dictResult.ContainsKey(imageKey))
                                                    {
                                                        dictResult[imageKey] = bResult;
                                                    }
                                                    else
                                                    {
                                                        dictResult.Add(imageKey, bResult);
                                                    }
                                                    // 胶3d坐标中心记录
                                                    if (dictResultCenter3DPoint.ContainsKey(imageKey))
                                                    {
                                                        dictResultCenter3DPoint[imageKey] = resultCenterPoint;
                                                    }
                                                    else
                                                    {
                                                        dictResultCenter3DPoint.Add(imageKey, resultCenterPoint);
                                                    }
                                                }
                                            }



                                            // 结果显示
                                            // 没必要显示每帧的检测结果，而且这样做导致影响检测速度
                                            // 2d轨迹结果更新
                                            if (imageSet.轮廓检测)
                                            {
                                                if (imageSet._3DGlueDet)
                                                {
                                                    // 已开放
                                                    int indexCross = indexImage - cutSet.StartImageIndex;
                                                    if (indexCross >= 0 && indexCross < angless[indexImageCutProcessDict[item.Key]].Count)
                                                    {
                                                        hWindowNumericalModelDiagramDispCross(rowss[indexImageCutProcessDict[item.Key]][indexCross],
                                                            colss[indexImageCutProcessDict[item.Key]][indexCross],
                                                            angless[indexImageCutProcessDict[item.Key]][indexCross], cutSet.Size, bResult.Result ? Colors.Green : Colors.Red);
                                                    }
                                                }
                                            }
                                            //结果列表颜色
                                            switch (item.Key)
                                            {
                                                case "Cam1":
                                                    mainModel.ImageResultRecords[dataGridViewImageListRowsStartPoint[indexImageCut] + indexImage].Cam1Result = bResult.Result ? "OK" : "NG";
                                                    break;
                                                case "Cam2":
                                                    mainModel.ImageResultRecords[dataGridViewImageListRowsStartPoint[indexImageCut] + indexImage].Cam2Result = bResult.Result ? "OK" : "NG";
                                                    break;
                                                case "Cam3":
                                                    mainModel.ImageResultRecords[dataGridViewImageListRowsStartPoint[indexImageCut] + indexImage].Cam3Result = bResult.Result ? "OK" : "NG";
                                                    break;
                                                case "Cam4":
                                                    mainModel.ImageResultRecords[dataGridViewImageListRowsStartPoint[indexImageCut] + indexImage].Cam4Result = bResult.Result ? "OK" : "NG";
                                                    break;
                                            }

                                            dictResult.Add(imageKey, bResult);
                                            dictData.Add(imageKey, resultData);
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

            ////输出运行中信号
            //if (!Write(DO.Running, true)) return;
            //ShowMessage(GlobalVarAndFunc.LanguageTranslate("输出Running信号"));
            //if (!Write(DO.Ready, false)) return;
            //ShowMessage(GlobalVarAndFunc.LanguageTranslate("关闭Ready信号"));

            return true;
        }

        private void clearData()
        {
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

            ResultCenter3DPoint.Clear();


            Point3DXs.Clear();
            Point3DYs.Clear();
            Point3DZs.Clear();
            glueVols.Clear();
            outLineDict.Clear();
            glueRegionDict.Clear();
            glueSmallRectRegionDict.Clear();
            glueDataDict.Clear();
            glueResultDict.Clear();
            indexImageCutProcessDict.Clear();
            displaySize.Clear();
            tasks.Clear();
            #endregion
            //Invoke(new Action(() => { form3DShow.ClearCloud(); }));
            //清空结果
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Clear3DPointControlEvent();
                DispClearHWindowControlEvent();
            });

            GC.Collect();

            System.Windows. Application.Current.Dispatcher.Invoke(() =>
            {
                mainModel.ImageResultRecords.Clear();
            });
        }

        private void hWindowNumericalModelDiagramDispCross(double row, double col, double angle, double size, System.Windows.Media.Color color)
        {
            //生成交叉图案
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                //Mat hXLDCont = new Mat();
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

                //DispPolylineHWindowNumericalModelDiagramEvent(Points1, color, 2);
                DispPolylineHWindowNumericalModelDiagramEvent(Points2, color, 2);
                //hXLDCont.Dispose();
            }
            ));




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


        public void ShowMessage(string message)
        {
            ShowMessage(message, LogType.normal);
        }

        object olock = new object();

        public void ShowMessage(string message, LogType type)
        {
            DateTime dateTime = DateTime.Now;
            string day = dateTime.ToString("yyyy-MM-dd");
            string time = dateTime.ToString("HH:mm:ss.fff");


            LogRecord logRecord = new LogRecord();
            logRecord.LogTime = dateTime.ToString("G");
            logRecord.LogInfo = message;

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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                while (mainModel.LogRecords.Count > 10000)
                    //while (mainModel.LogRecords.Count > 0)
                {
                    mainModel.LogRecords.RemoveAt(mainModel.LogRecords.Count - 1);
                }
                mainModel.LogRecords.Insert(0, logRecord);
            });

            lock (olock)
            {
                if (!Directory.Exists("RunLog"))
                {
                    Directory.CreateDirectory("RunLog");
                }
                using (StreamWriter writer = new StreamWriter("RunLog\\" + day + ".log", true))
                {
                    writer.WriteLine(time + " " + message);
                }
                if (type == LogType.ng || type == LogType.warn)
                {
                    try
                    {
                        File.AppendAllText("Error.log", dateTime.ToString("yyyy-MM-dd HH:mm:ss  ") + message + "\r\n\r\n");
                    }
                    catch { }
                }
            }

        }


        object olockShow = new object();
        bool showing = false;
        void ShowImageData(int showWidth, int showHeight, CutSet cutSet, Mat hXLDCont10mm)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        Mat mat = new Mat();
                        mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3);

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
                    System.Windows.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

        void ShowImageData(int showWidth, int showHeight, Mat hXLDCont10mm, Mat hRegion, Mat hRegionSmallestRectangle2, Data data, BResult bResult,CutSet cutSet, double offsetX = 0, double offsetY = 0)
        {
            if (!showing)
            {
                showing = true;
                try
                {
                    lock (olockShow)
                    {
                        Mat mat = new Mat();
                        mat = Mat.Zeros((int)(showHeight * cutSet.scaleSize), (int)(showWidth * cutSet.scaleSize), MatType.CV_8UC3);

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

                            string text = GlobalVarAndFunc.LanguageTranslate("胶高：") + $"{data.glueHeight:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("胶宽：") + $"{data.glueWidth:0.00}\r\n"
                               + GlobalVarAndFunc.LanguageTranslate("面积：") + $"{data.glueArea:0.00}";
                            DispTextInImageHWindowControlEvent(text, Colors.White, (int)data.column + (int)(data.glueWidth / 2 * cutSet.scaleSize + offsetX),
                                (int)data.row + (int)(data.glueHeight / 2 * cutSet.scaleSize + offsetY));

                            //hWindowControl.DispTextInImage(text, data.row, data.column);
                            string textWindow1 = GlobalVarAndFunc.LanguageTranslate("胶宽：") + (bResult.glueWidth ? "OK" : "NG");
                            string textWindow2 = GlobalVarAndFunc.LanguageTranslate("胶高：") + (bResult.glueHeight ? "OK" : "NG");
                            string textWindow3 = GlobalVarAndFunc.LanguageTranslate("面积：") + (bResult.glueArea ? "OK" : "NG");
                            string textWindow = textWindow1 + "\r\n" + textWindow2 + "\r\n" + textWindow3;
                            DispTextInImageHWindowControlEvent(textWindow, Colors.White, 10, 10);

                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.ToString());
                }
                showing = false;
            }
        }

    }







}
