using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace _3DLaserGlueInspection.subForm
{
    /// <summary>
    /// WindowPassWord.xaml 的交互逻辑
    /// </summary>
    public partial class WindowPassWord : Window
    {
        public enum PasswordUserType
        {
            None,
            Administrator,
            SuperAdministrator
        }

        public PasswordUserType LoginUserType { get; private set; } = PasswordUserType.None;

        private string Password = string.Empty;

        private PasswordUserType SelectedUserType =>
            userTypeComboBox.SelectedIndex == 1
                ? PasswordUserType.SuperAdministrator
                : PasswordUserType.Administrator;

        public WindowPassWord()
        {
            InitializeComponent();
        }

        private static string GetPasswordFilePath(PasswordUserType userType)
        {
            return userType == PasswordUserType.SuperAdministrator
                ? "Data\\SupperUser"
                : "Data\\User";
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
            string filePath = GetPasswordFilePath(SelectedUserType);
            Password = System.IO.File.Exists(filePath)
                ? System.IO.File.ReadAllText(filePath)
                : string.Empty;
        }

        private void CompleteLogin()
        {
            LoginUserType = SelectedUserType;
            DialogResult = true;
            Close();
        }

        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            string key = GetPasswordHash(passWordNumericUpDown.Text);
            if (key == Password)
            {
                CompleteLogin();
            }
            else
            {
                tipLabel.Content = _3DLaserGlueInspection.Resources.LanguageDict.PasswordIncorrect;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 每次打开登录窗口都重新读取并验证密码。
            LoadPassword();
        }

        private void userTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadPassword();
            if (passWordNumericUpDown != null)
            {
                passWordNumericUpDown.Clear();
            }
            if (tipLabel != null)
            {
                tipLabel.Content = "    ";
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