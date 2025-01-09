using Microsoft.Win32;
//using System.Windows.Forms;

namespace _3DLaserGlueInspection
{
    //（1）.Registry类：此类主要封装了七个公有的静态域，而这些静态域分别代表这视窗注册表中的七个基本的主键，具体如下所示： 
    //Registry.ClassesRoot 对应于HKEY_CLASSES_ROOT主键
    //Registry.CurrentUser 对应于HKEY_CURRENT_USER主键
    //Registry.LocalMachine 对应于 HKEY_LOCAL_MACHINE主键
    //Registry.User 对应于 HKEY_USER主键
    //Registry.CurrentConfig 对应于HEKY_CURRENT_CONFIG主键
    //Registry.DynDa 对应于HKEY_DYN_DATA主键
    //Registry.PerformanceData 对应于HKEY_PERFORMANCE_DATA主键 

    //（2）.RegistryKey类：此类中主要封装了对视窗系统注册表的基本操作。在程序设计中，首先通过Registry类找到注册表中的基本主键，然后通过
    //RegistryKey类，来找其下面的子键和处理具体的操作的。
    //OpenSubKey(string name)方法主要是打开指定的子键。
    //GetSubKeyNames()方法是获得主键下面的所有子键的名称，它的返回值是一个字符串数组。
    //GetValueNames()方法是获得当前子键中的所有的键名称，它的返回值也是一个字符串数组。
    //GetValue(string name)方法是获得指定键的键值。


    public static class Reg
    {
        static readonly string AppName = "3DLaserGlueInspection";
        /// <summary>
        /// 读取指定名称的注册表的值
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistData(string name)
        {
            string registData = string.Empty;
            RegistryKey hkml = Registry.LocalMachine;
            RegistryKey software = hkml.OpenSubKey("SOFTWARE");
            RegistryKey aimdir = software.OpenSubKey(AppName);
            if (aimdir != null)
                registData = aimdir.GetValue(name).ToString();
            return registData;
        }
        //以上是读取的注册表中HKEY_LOCAL_MACHINE\SOFTWARE目录下的VIN Defect Detection目录中名称为name的注册表值； 

        /// <summary>
        /// 向注册表中写数据
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tovalue"></param>
        public static void WTRegedit(string name, string tovalue)
        {
            RegistryKey hklm = Registry.LocalMachine;
            RegistryKey software = hklm.OpenSubKey("SOFTWARE", true);
            RegistryKey aimdir = software.CreateSubKey(AppName);
            if (tovalue != null)
            {
                aimdir.SetValue(name, tovalue);
            }
        }
        //以上是在注册表中HKEY_LOCAL_MACHINE\SOFTWARE目录下新建VIN Defect Detection目录并在此目录下创建名称为name值为tovalue的注册表项； 

        /// <summary>
        /// 删除注册表中指定的注册表项
        /// </summary>
        /// <param name="name"></param>
        public static void DeleteRegist(string name)
        {
            string[] aimnames;
            RegistryKey hkml = Registry.LocalMachine;
            RegistryKey software = hkml.OpenSubKey("SOFTWARE", true);
            RegistryKey aimdir = software.OpenSubKey(AppName, true);
            aimnames = aimdir.GetSubKeyNames();
            foreach (string aimKey in aimnames)
            {
                if (aimKey == name)
                    aimdir.DeleteSubKeyTree(name);
            }
        }
        //以上是在注册表中HKEY_LOCAL_MACHINE\SOFTWARE目录下VIN Defect Detection目录中删除名称为name注册表项； 

        /// <summary>
        /// 判断指定注册表项是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsRegeditExit(string name)
        {
            bool _exit = false;
            string[] subkeyNames;
            RegistryKey hkml = Registry.LocalMachine;
            RegistryKey software = hkml.OpenSubKey("SOFTWARE");
            RegistryKey aimdir = software.OpenSubKey(AppName);
            if (aimdir != null)
            {
                subkeyNames = aimdir.GetValueNames();
                foreach (string keyName in subkeyNames)
                {
                    if (keyName == name)
                    {
                        _exit = true;
                        return _exit;
                    }
                }
            }
            return _exit;
        }
        //以上是在注册表中HKEY_LOCAL_MACHINE\SOFTWARE目录下VIN Defect Detection目录中判断名称为name注册表项是否存在，这一方法在删除注册表时已经存在，在新建一注册表项时也应有相应判断。

        //public static bool GetStartUp(out bool isStartUp)
        //{
        //    isStartUp = false;
        //    string path = Application.ExecutablePath;
        //    try
        //    {
        //        RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        //        var str = rk.GetValue(AppName);
        //        rk.Close();
        //        if (str != null && str.ToString() == path)
        //        {
        //            isStartUp = true;
        //        }
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}
        //public static bool SetStartUp(bool isStartUp)
        //{
        //    string path = Application.ExecutablePath;
        //    try
        //    {
        //        RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        //        if (isStartUp)
        //        {
        //            rk.SetValue(AppName, path);
        //        }
        //        else
        //        {
        //            rk.DeleteValue(AppName, false);
        //        }
        //        rk.Close();
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}
    }
}
