using _3DLaserGlueInspection;
using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace _3DLaserGlueInspection
{
    public class MainModel: NotifyBase
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


        public int OKCountControl { get { return OKCount; } set { OKCount = value; this.DoNotify(); } }
        public int NGCountControl { get { return NGCount; } set { NGCount = value; this.DoNotify(); } }
        public int totalCountControl { get { return totalCount; } set { totalCount = value; this.DoNotify(); } }
        public string passRateControl { get { return passRate; } set { passRate = value; this.DoNotify(); } }

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


        public class ImageResultRecord : NotifyBase
        {
            public string Cam1 { get; set; }
            public string Cam2 { get; set; }
            public string Cam3 { get; set; }
            public string Cam4 { get; set; }
            public string Cam1Result { get; set; }
            public string Cam2Result { get; set; }
            public string Cam3Result { get; set; }
            public string Cam4Result { get; set; }

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


    }
}
