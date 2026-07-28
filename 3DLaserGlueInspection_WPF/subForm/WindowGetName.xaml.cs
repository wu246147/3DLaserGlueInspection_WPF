using System;
using System.Collections.Generic;
using System.Linq;
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
    /// WindowGetName.xaml 的交互逻辑
    /// </summary>
    public partial class WindowGetName : Window
    {
        public string Value;
        public string[] CheckName;
        public string CopyName = string.Empty;

        private void WindowGetName_Load(object sender, RoutedEventArgs e)
        {
            projectNameTextBox.Text = this.Value;
            if (CheckName != null && CheckName.Length > 0)
            {
                for (int i = 0;i<CheckName.Length;i++)
                {
                    projectNameComboBox.Items.Add(CheckName[i]);
                }
            }
        }

        public WindowGetName()
        {
            InitializeComponent();
        }

        private void openCamButton_Click(object sender, RoutedEventArgs e)
        {
            if (projectNameTextBox.Text.Trim() == string.Empty)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.TheNameIsEmpty, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (CheckName != null)
            {
                foreach (var item in CheckName)
                {
                    if (projectNameTextBox.Text.Trim() == item)
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DuplicateName, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            if ((bool)copyProjectCheckBox.IsChecked)
            {
                CopyName = projectNameComboBox.Items[projectNameComboBox.SelectedIndex].ToString();
            }
            this.Value = projectNameTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            if (projectNameTextBox.Text.Trim() == string.Empty)
            {
                System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.TheNameIsEmpty, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (CheckName != null)
            {
                foreach (var item in CheckName)
                {
                    if (projectNameTextBox.Text.Trim() == item)
                    {
                        System.Windows.Forms.MessageBox.Show(_3DLaserGlueInspection.Resources.LanguageDict.DuplicateName, _3DLaserGlueInspection.Resources.LanguageDict.Prompt, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            if ((bool)copyProjectCheckBox.IsChecked)
            {
                CopyName = projectNameComboBox.Items[projectNameComboBox.SelectedIndex].ToString();
            }
            this.Value = projectNameTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
