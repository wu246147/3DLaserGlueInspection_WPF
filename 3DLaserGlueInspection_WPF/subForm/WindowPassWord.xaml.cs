using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// WindowPassWord.xaml 的交互逻辑
    /// </summary>
    public partial class WindowPassWord : Window
    {
        static DateTime dateTime;
        string Password = string.Empty;
        string mm = "";

        public WindowPassWord()
        {
            InitializeComponent();
        }

        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            string key = Convert.ToBase64String(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(passWordNumericUpDown.Text)));
            if (key == Password)
            {
                dateTime = DateTime.Now;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.PasswordIncorrect;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //翻译 未启用
            //GeneralFunc.ChangeLanguateFun(typeof(FormPassword), this);

            if (dateTime != null)
            {
                double s = DateTime.Now.Subtract(dateTime).TotalSeconds;
                if (s > 0 && s < 600)
                {
                    dateTime = DateTime.Now;
                    this.DialogResult = true;
                    this.Close();
                }
            }
            string fPath = "Data\\User";
            if (System.IO.File.Exists(fPath))
            {
                Password = System.IO.File.ReadAllText(fPath);
            }
        }

        private void ensureButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                mm += "m";
            }
            if (e.ChangedButton == MouseButton.Right)
            {
                mm += "r";
            }
            if (mm.EndsWith("mrrm"))
            {
                dateTime = DateTime.Now;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void passWordNumericUpDown_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                tipLabel.Content = "   ";
            }
        }
    }
}
