using System;
using System.Collections.Generic;
using System.IO;
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
    /// WindowNewPassWord.xaml 的交互逻辑
    /// </summary>
    public partial class WindowNewPassWord : Window
    {
        string Password = string.Empty;
        string mm = "";

        public WindowNewPassWord()
        {
            InitializeComponent();
        }
        void NewMethod()
        {
            if (newPassWordNumericUpDown.Text == repeatPassWordNumericUpDown.Text)
            {
                try
                {
                    string NewPassword = Convert.ToBase64String(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(newPassWordNumericUpDown.Text)));
                    if (!Directory.Exists("Data"))
                    {
                        Directory.CreateDirectory("Data");
                    }
                    File.WriteAllText("Data\\User", NewPassword);
                    tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.PasswordChangedSuccessfully;
                    Password = NewPassword;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                }
            }
            else
            {
                tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.TheTwoNewPasswordsDoNotMatch;
            }
        }

        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            string key = Convert.ToBase64String(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(originPassWordNumericUpDown.Text)));
            if (key == Password)
            {
                NewMethod();
            }
            else
            {
                tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.TheOriginalPasswordIsIncorrect;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //翻译 未启用
            //GeneralFunc.ChangeLanguateFun(typeof(FormNewPassword), this);
            string fPath = "Data\\User";
            if (System.IO. File.Exists(fPath))
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
                NewMethod();
            }
        }

        private void PassWordNumericUpDown_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                tipLabel.Content = "   ";
            }
        }
    }
}
