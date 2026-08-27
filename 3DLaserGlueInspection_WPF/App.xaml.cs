using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace _3DLaserGlueInspection
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {


            GlobalVarAndFunc.InitLanguage();
            base.OnStartup(e);

            bool processExist = false;

            Process[] processes = Process.GetProcesses();
            Process currentProcess = Process.GetCurrentProcess();

            foreach (Process p in processes)
            {
                if (p.ProcessName == currentProcess.ProcessName &&
                    p.Id != currentProcess.Id)
                {
                    processExist = true;
                    break;
                }
            }

            if (processExist)
            {
                MessageBox.Show(
                    _3DLaserGlueInspection.Resources.LanguageDict.SingleInstanceDisabled,
                    _3DLaserGlueInspection.Resources.LanguageDict.DuplicateInstance,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }
        }
    }
}
