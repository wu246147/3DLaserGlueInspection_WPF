using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace _3DLaserGlueInspection
{
    public static class GeneralFunc
    {

        //private static void ChangeControlLanguateFun(ComponentResourceManager resources, Control control)
        //{
        //    //将资源与控件对应
        //    resources.ApplyResources(control, control.Name);
        //    if (control.HasChildren)//子控件，比如组合框GroupBox里的控件
        //    {
        //        foreach (Control controls in control.Controls)
        //            ChangeControlLanguateFun(resources, controls);
        //    }
        //    if (control is MenuStrip)//菜单栏控件
        //    {
        //        MenuStrip ms = (MenuStrip)control;
        //        if (ms.Items.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (ToolStripMenuItem ts in ms.Items)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //                if (ts.DropDownItems.Count > 0)
        //                {
        //                    foreach (ToolStripMenuItem tts in ts.DropDownItems)//子菜单
        //                    {
        //                        resources.ApplyResources(tts, tts.Name);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    if (control is DataGridView)//菜单栏控件
        //    {
        //        DataGridView dgv = (DataGridView)control;
        //        if (dgv.Columns.Count > 0)
        //        {
        //            //遍历菜单
        //            foreach (DataGridViewTextBoxColumn ts in dgv.Columns)//主菜单
        //            {
        //                resources.ApplyResources(ts, ts.Name);
        //            }
        //        }
        //    }
        //}

        #region  界面翻译
        //public static void ChangeLanguateFun(Type t, Form f)
        //{
        //    int currentLcid = 2052; //1033代表英文，2052代表中文
        //    if (GlobalVarAndFunc.LANGUAGE_ID == 1)
        //    {
        //        currentLcid = 1033;
        //    }
        //    Thread.CurrentThread.CurrentUICulture = new CultureInfo(currentLcid);
        //    ComponentResourceManager resources = new ComponentResourceManager(t);
        //    resources.ApplyResources(f, "$this");//窗体标题


        //    foreach (Control control in f.Controls)//循环当前界面所有的控件
        //    {
        //        ChangeControlLanguateFun(resources, control);
        //    }
        //    //刷新窗体，有时窗体标题无法切换成功，需要刷新一下
        //    f.Refresh();
        //}
        #endregion
    }



    public static class GlobalVarAndFunc
    {


        public static int LANGUAGE_ID = 1; //0默认中文，1英文

        private static Dictionary<string, string> LANGUAGE_DIC ;


        public static void ReadLanguageID()
        {
            string fPath = "Data\\LanguageID";
            if (File.Exists(fPath))
            {
                LANGUAGE_ID = int.Parse(File.ReadAllText(fPath));
            }
        }

        public static void LanguageDicInit()
        {
            if (LANGUAGE_ID == 0)
            {
                LANGUAGE_DIC = null;
            }
            else if (LANGUAGE_ID == 1)
            {
                string jsonFilePath = "Data\\LanguageDictEN.json";
                if (File.Exists(jsonFilePath))
                {
                    string json = File.ReadAllText(jsonFilePath,Encoding.UTF8);
                    LANGUAGE_DIC = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                else
                {
                    LANGUAGE_DIC = null;
                    //MessageBox.Show("Not Exist Data\\LanguageDictEN file.", "Warning", MessageBoxButtons.OKCancel);
                }
            }
        }

        public static string LanguageTranslate(string info)
        {
            string translate;
            if (LANGUAGE_ID == 0)
            {
                translate = info;
            }
            else
            {
                if (LANGUAGE_DIC != null)
                { 
                    if (LANGUAGE_DIC.ContainsKey(info))
                    {
                        translate = LANGUAGE_DIC[info];
                    }
                    else
                    {
                        translate = info;
                    }
                }
                else
                {
                    translate = info;
                }

            }
            return translate;
        }


    }

}
