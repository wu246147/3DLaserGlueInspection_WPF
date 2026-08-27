using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// WindowNewPassWord.xaml 的交互逻辑
    /// </summary>
    public partial class WindowNewPassWord : Window
    {
        private string Password = string.Empty;
        private string mm = "";

        private string GetPasswordFilePath()
        {
            return userTypeComboBox.SelectedIndex == 1
                ? "Data\\SupperUser"
                : "Data\\User";
        }

        public WindowNewPassWord()
        {
            InitializeComponent();
        }

        private static string GetPasswordHash(string password)
        {
            using (MD5 md5 = MD5.Create())
            {
                return Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty)));
            }
        }

        private void LoadPassword()
        {
            string filePath = GetPasswordFilePath();
            Password = File.Exists(filePath)
                ? File.ReadAllText(filePath)
                : string.Empty;
        }

        private void NewMethod()
        {
            if (newPassWordNumericUpDown.Text == repeatPassWordNumericUpDown.Text)
            {
                try
                {
                    string newPassword = GetPasswordHash(newPassWordNumericUpDown.Text);
                    if (!Directory.Exists("Data"))
                    {
                        Directory.CreateDirectory("Data");
                    }

                    File.WriteAllText(GetPasswordFilePath(), newPassword);
                    tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.PasswordChangedSuccessfully;
                    Password = newPassword;
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
            string key = GetPasswordHash(originPassWordNumericUpDown.Text);
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
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPassword();
        }

        private void userTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadPassword();
            if (originPassWordNumericUpDown != null)
            {
                originPassWordNumericUpDown.Clear();
                newPassWordNumericUpDown.Clear();
                repeatPassWordNumericUpDown.Clear();
            }
            if (tipLabel != null)
            {
                tipLabel.Content = "    ";
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