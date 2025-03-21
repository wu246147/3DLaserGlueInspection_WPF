using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace _3DLaserGlueInspection.subForm
{

    /// <summary>
    /// WindowCopy.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCopy : Window
    {
        public int startID, endID;

        public WindowCopy(int value, int maxValue)
        {
            InitializeComponent();
            //startIDNumericUpDown.Maximum = maxValue;
            startID = value;
            startIDNumericUpDown.Text = startID.ToString();
            //endIDNumericUpDown.Maximum = maxValue;
            endID = value;
            endIDNumericUpDown.Text = endID.ToString();
        }
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //判断输入是否正负号，小数点，或数字。
            e.Handled = new Regex(@"[^0-9+\-.]+").IsMatch(e.Text);

        }
        private void ensureButton_Click(object sender, RoutedEventArgs e)
        {
            startID = Convert.ToInt16(startIDNumericUpDown.Text);
            endID = Convert.ToInt16(endIDNumericUpDown.Text);
            DialogResult = true;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
