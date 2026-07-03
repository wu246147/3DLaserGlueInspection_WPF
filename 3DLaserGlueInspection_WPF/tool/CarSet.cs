using _3DLaserGlueInspection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using _3DLaserGlueInspection.subForm;
using RAIVASCS.Common;

namespace _3DLaserGlueInspection
{
    public class CarNameIdSet
    {
        public Dictionary<Guid, Car> Cars = new Dictionary<Guid, Car>();

        public string ErrMsg => _errMsg;
        string _errMsg = string.Empty;


        public void UpdateCarSet(Dictionary<Guid, Car> NewCars)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\Project\\";
            //改名//删除
            foreach (var item in Cars.Keys)
            {
                if (NewCars.ContainsKey(item))
                {
                    if (Cars[item].Name != NewCars[item].Name)
                    {
                        //修改文件夹名称Cars[item].Name→fcs.NewCars[item].Name
                        string oldPath = basePath + Cars[item].Name;
                        string newPath = basePath + NewCars[item].Name;

                        if (Directory.Exists(oldPath))
                        {
                            if (!Directory.Exists(newPath))
                            {
                                Directory.Move(oldPath, newPath);
                            }
                        }
                        else
                        {
                            if (!Directory.Exists(newPath))
                            {
                                Directory.CreateDirectory(newPath);
                            }
                        }
                    }
                }
                else
                {
                    //删除文件夹名称Cars[item].Name
                    if (Directory.Exists(basePath + Cars[item].Name))
                    {
                        Directory.Delete(basePath + Cars[item].Name, true);
                    }
                }
            }
            //添加
            foreach (var item in NewCars.Keys)
            {
                if (!Cars.ContainsKey(item))
                {
                    //添加fcs.NewCars[item].Name
                    Directory.CreateDirectory(basePath + NewCars[item].Name);
                }
            }
            Cars = NewCars;
            Save();
        }

        //先屏蔽，后面在其他地方补上
        public void ShowCarSetForm(string[] CamParamNames)
        {
            Load();
            WindowCarSet fcs = new WindowCarSet(Cars, CamParamNames);
            if (fcs.ShowDialog()==true)
            {
                UpdateCarSet(fcs.NewCars);
            }

        }
        public bool Load()
        {
            bool result = true;
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
                string fPath = basePath + "CarID";
                if (File.Exists(fPath))
                {
                    using (FileStream stream = new FileStream(fPath, FileMode.OpenOrCreate))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        Cars = (Dictionary<Guid, Car>)bf.Deserialize(stream);
                    }
                    if (Cars == null)
                    {
                        Cars = new Dictionary<Guid, Car>();
                        result = false;
                        _errMsg = fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileFormatException;
                    }
                }
                else
                {
                    Cars = new Dictionary<Guid, Car>();
                    result = false;
                    _errMsg = fPath + _3DLaserGlueInspection.Resources.LanguageDict.FileDoesNotExist;
                }
            }
            catch (Exception ex)
            {
                Cars = new Dictionary<Guid, Car>();
                result = false;
                _errMsg = ex.ToString();
            }
            if (!result)
            {
                result = true;
                try
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
                    string fPath = basePath + "CarID_bak";
                    if (File.Exists(fPath))
                    {
                        using (FileStream stream = new FileStream(fPath, FileMode.OpenOrCreate))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            Cars = (Dictionary<Guid, Car>)bf.Deserialize(stream);
                        }
                        if (Cars == null)
                        {
                            Cars = new Dictionary<Guid, Car>();
                            result = false;
                        }
                        else
                        {
                            File.Copy(fPath, basePath + "CarID", true);
                        }
                    }
                    else
                    {
                        Cars = new Dictionary<Guid, Car>();
                        result = false;
                    }
                }
                catch (Exception ex)
                {
                    Cars = new Dictionary<Guid, Car>();
                    result = false;
                }
            }

            return result;
        }
        bool Save()
        {
            bool result = true;
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                string fPath = basePath + "CarID";
                using (FileStream stream = new FileStream(fPath, FileMode.Create))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(stream, Cars);
                }
                File.Copy(fPath, basePath + "CarID_bak", true);
            }
            catch (Exception ex)
            {
                result = false;
                _errMsg = ex.ToString();
            }
            return result;
        }

        public string[] LoadName()
        {
            List<string> names = new List<string>();
            if (Load())
            {
                foreach (var item in Cars.Values)
                {
                    names.Add(item.Name);
                }
            }
            return names.ToArray();
        }
    }
    [Serializable]
    public class Car 
    {
        public string Name;
        public List<int> IDs;
        public string CamParamName;
        public Car() { }
        public Car(string name, List<int> ids, string camParamName)
        {
            this.Name = name;
            this.IDs = ids;
            this.CamParamName = camParamName;
        }
    }
}
