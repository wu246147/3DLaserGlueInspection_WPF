using _3DLaserGlueInspection;
using LiveCharts.Wpf;
using LiveCharts;
using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;




namespace _3DLaserGlueInspection
{
    public class ImageResultRecord : NotifyBase
    {
        //public string Cam1 { get; set; }
        //public string Cam2 { get; set; }
        //public string Cam3 { get; set; }
        //public string Cam4 { get; set; }
        //public string Cam1Result { get; set; }
        //public string Cam2Result { get; set; }
        //public string Cam3Result { get; set; }
        //public string Cam4Result { get; set; }

        private string _cam1;
        public string Cam1
        {
            get => _cam1;
            set { _cam1 = value; DoNotify(); }
        }

        private string _cam1Result;
        public string Cam1Result
        {
            get => _cam1Result;
            set { _cam1Result = value; DoNotify(); }
        }

        private string _cam2;
        public string Cam2
        {
            get => _cam2;
            set { _cam2 = value; DoNotify(); }
        }

        private string _cam2Result;
        public string Cam2Result
        {
            get => _cam2Result;
            set { _cam2Result = value; DoNotify(); }
        }

        private string _cam3;
        public string Cam3
        {
            get => _cam3;
            set { _cam3 = value; DoNotify(); }
        }

        private string _cam3Result;
        public string Cam3Result
        {
            get => _cam3Result;
            set { _cam3Result = value; DoNotify(); }
        }

        private string _cam4;
        public string Cam4
        {
            get => _cam4;
            set { _cam4 = value; DoNotify(); }
        }

        private string _cam4Result;
        public string Cam4Result
        {
            get => _cam4Result;
            set { _cam4Result = value; DoNotify(); }
        }
    }
 

    public class CarResultRecord : NotifyBase
    {
        public string CarDetTime { get; set; }
        public string CarID { get; set; }
        public string CarResult { get; set; }

    }

    public class LogRecord : NotifyBase
    {
        public string LogTime { get; set; }
        public string LogInfo { get; set; }
        public string LogResult { get; set; }

    }


    public class MainModel : NotifyBase
    {

        int OKCount = 0;
        int NGCount = 0;
        int totalCount = 0;
        string passRate = "0.0%";
        string robotCommunicationLabelColor = "Gray";
        string camCommunicationLabelColor = "Gray";
        string softwareRunLabelColor = "Gray";

        string buttonRunContent = GlobalVarAndFunc.LanguageTranslate("启动");
        string buttonRunTag = "\ue658";

        ObservableCollection<ImageResultRecord> _imageResultRecords = new ObservableCollection<ImageResultRecord>();
        ObservableCollection<CarResultRecord> _carResultRecords = new ObservableCollection<CarResultRecord>();
        ObservableCollection<LogRecord> _logRecord = new ObservableCollection<LogRecord>();

        string productID = "--";
        string name = "--";
        string VIN = "-----------------";
        string time = "-----------------";
        string result = "--";
        string resultColor = "White";


        public int OKCountControl { get { return OKCount; } set { OKCount = value; UpdatePie(); this.DoNotify(); } }
        public int NGCountControl { get { return NGCount; } set { NGCount = value; UpdatePie(); this.DoNotify(); } }
        public int totalCountControl { get { return totalCount; } set { totalCount = value; this.DoNotify(); } }
        public string passRateControl { get { return passRate; } set { passRate = value; this.DoNotify(); } }

        public MainModel()
        {
            InitPie();
        }

        private SeriesCollection _pieResultSeries;
        public SeriesCollection PieResultSeries
        {
            get => _pieResultSeries;
            set { _pieResultSeries = value; this.DoNotify(); }
        }
        /// <summary>
        /// 初始化饼图（放在构造函数中调用）
        /// </summary>
        private void InitPie()
        {
            PieResultSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title        = "OK",
                    DataLabels   = true,
                    Fill         = Brushes.LightGreen,
                    FontSize     = 14,
                    Stroke       = Brushes.White,
                    Values       = new ChartValues<double> { 1 }
                },
                new PieSeries
                {
                    Title        = "NG",
                    DataLabels   = true,
                    Fill         = Brushes.Red,
                    FontSize     = 14,
                    Stroke       = Brushes.White,
                    Values       = new ChartValues<double> { 1 }
                }
            };
        }

        /// <summary>
        /// 饼图跟随计数变化
        /// </summary>
        private void UpdatePie()
        {
            if (PieResultSeries == null) return;
            PieResultSeries[0].Values[0] = (double)OKCount;
            PieResultSeries[1].Values[0] = (double)NGCount;
        }


        public string robotCommunicationLabelColorControl { get { return robotCommunicationLabelColor; } set { robotCommunicationLabelColor = value; this.DoNotify(); } }
        public string camCommunicationLabelColorControl { get { return camCommunicationLabelColor; } set { camCommunicationLabelColor = value; this.DoNotify(); } }
        public string softwareRunLabelColorControl { get { return softwareRunLabelColor; } set { softwareRunLabelColor = value; this.DoNotify(); } }

        public string buttonRunContentControl { get { return buttonRunContent; } set { buttonRunContent = value; this.DoNotify(); } }
        public string buttonRunTagControl { get { return buttonRunTag; } set { buttonRunTag = value; this.DoNotify(); } }

        public ObservableCollection<ImageResultRecord> ImageResultRecords { get { return _imageResultRecords; } set { _imageResultRecords = value; this.DoNotify(); } }

        public string productIDControl { get { return productID; } set { productID = value; this.DoNotify(); } }
        public string nameControl { get { return name; } set { name = value; this.DoNotify(); } }
        public string VINControl { get { return VIN; } set { VIN = value; this.DoNotify(); } }
        public string timeControl { get { return time; } set { time = value; this.DoNotify(); } }
        public string resultControl { get { return result; } set { result = value; this.DoNotify(); } }
        public string resultColorControl { get { return resultColor; } set { resultColor = value; this.DoNotify(); } }

        public ObservableCollection<CarResultRecord> CarResultRecords { get { return _carResultRecords; } set { _carResultRecords = value; this.DoNotify(); } }

        public ObservableCollection<LogRecord> LogRecords { get { return _logRecord; } set { _logRecord = value; this.DoNotify(); } }


     

    }
}
