using RAIVASCS.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
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
    public class CarShow : NotifyBase
    {
        public string Order { get; set; }
        public string Name { get; set; }
        public string IDs { get; set; }
        public string CamParamName { get; set; }
    }
    public class WindowCarSetModel : NotifyBase
    {
        ObservableCollection<CarShow> _carsRecord = new ObservableCollection<CarShow>();
        public ObservableCollection<CarShow> CarsRecord { get { return _carsRecord; } set { _carsRecord = value; this.DoNotify(); } }

    }
    /// <summary>
    /// WindowCarSet.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCarSet : Window
    {
        //Dictionary<Guid, Car> Cars = new Dictionary<Guid, Car>();
        public Dictionary<Guid, Car> NewCars = new Dictionary<Guid, Car>();
        public WindowCarSetModel mainModel;
        bool isAlter = false;//是否修改过参数

        private void SelectItemByRowAndColumnIndex(int rowIndex, int columnIndex)
        {
            // 获取DataGrid的行容器
            if (rowIndex >= 0 && rowIndex < productInfoDataGrid.Items.Count)
            {
                productInfoDataGrid.SelectedItem = productInfoDataGrid.Items[rowIndex];
                productInfoDataGrid.UpdateLayout();
            }
            //var row = productInfoDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            //if (row != null)
            //{
            //    // 获取行中的单元格
            //    var cell = productInfoDataGrid.Columns[columnIndex].GetCellContent(row);
            //    if (cell != null)
            //    {
            //        // 选中整行
            //        productInfoDataGrid.SelectedItem = productInfoDataGrid.Items[rowIndex];
            //    }
            //}

            //var _cells = productInfoDataGrid.SelectedCells;

            //if (_cells.Any())
            //{

            //}
            //else
            //{

            //}
        }

        private void UpdataDataGrip()
        {
            mainModel.CarsRecord.Clear();

            if (NewCars.Count == 0) return;
            foreach (var item in NewCars.Values)
            {
                if (item != null)
                {

                    int currRow = mainModel.CarsRecord.Count;


                    string Order = Convert.ToString(currRow + 1);
                    string Name = item.Name;
                    string IDs = "";
                    if (item.IDs != null && item.IDs.Count > 0)
                    {
                        IDs = item.IDs[0].ToString();
                        for (int i = 1; i < item.IDs.Count; i++)
                        {
                            IDs += "," + item.IDs[i];
                        }
                    }
                    string CamParamName = item.CamParamName;

                    CarShow newItem = new CarShow();
                    newItem.Order = Order;
                    newItem.Name = Name;
                    newItem.IDs = IDs;
                    newItem.CamParamName = CamParamName;

                    mainModel.CarsRecord.Add(newItem);

                }
            }
        }

        private void ShowCurrItem(int currRowIndex)
        {
            try
            {
                UpdataDataGrip();

                SelectItemByRowAndColumnIndex(currRowIndex, 0);

               
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
        }

        public WindowCarSet(Dictionary<Guid, Car> cars, string[] CamParamNames)
        {
            InitializeComponent();
            mainModel = new WindowCarSetModel();
            this.DataContext = mainModel;

            for (int i = 0; i < CamParamNames.Length; i++) 
            {
                camParaNameComboBox.Items.Add(CamParamNames[i]);
            }
            foreach (var item in cars.Keys)
            {
                //this.mainModel.CarsRecord.Add(new Car(cars[item].Name, cars[item].IDs, cars[item].CamParamName));
                //this.mainModel.CarsGuid.Add(item);
                this.NewCars.Add(item, new Car(cars[item].Name, cars[item].IDs, cars[item].CamParamName));
            }
        }

        private void WindowCarSet_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isAlter)
            {
                DialogResult r = System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("是否保存参数？"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                if (r == System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (r == System.Windows.Forms.DialogResult.Yes)
                {
                    this.DialogResult = true;
                }

            }
        }

        private void WindowCarSet_Loaded(object sender, RoutedEventArgs e)
        {
            //翻译，未启用
            //GeneralFunc.ChangeLanguateFun(typeof(FormCarSet), this);
            ShowCurrItem(-1);

            //// 表格风格修改
            //dgvProductItemIfo.EnableHeadersVisualStyles = false;
            //dgvProductItemIfo.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(38, 56, 81);
            //dgvProductItemIfo.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;

        }


        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //检验格式
                string productName = projectNameTextBox.Text;
                if (productName == string.Empty)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                if (-1 != productName.IndexOfAny(new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }))
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称不能包含字符") + "\\/:*?\"<>|", GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                if (projectIDTextBox.Text == string.Empty)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                List<int> productIds = new List<int>();
                string[] strings = projectIDTextBox.Text.Split(';', ',', '.');
                for (int i = 0; i < strings.Length; i++)
                {
                    if (!int.TryParse(strings[i], out int productId))
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID格式不正确！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        return;
                    }
                    else
                    {
                        productIds.Add(productId);
                    }
                }
                if (productIds.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                //检验是否重复
                foreach (var item in this.NewCars.Values)
                {
                    if (item.Name == productName)
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称已存在，不能重复添加！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        return;
                    }
                    foreach (var productId in productIds)
                    {
                        if (item.IDs.Contains(productId))
                        {
                            System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID") + productId + GlobalVarAndFunc.LanguageTranslate("已存在，不能重复添加！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                //添加
                //mainModel.CarsRecord.Add(new Car(productName, productIds, camParaNameComboBox.Text));
                //mainModel.CarsGuid.Add(Guid.NewGuid());
                this.NewCars.Add(Guid.NewGuid(), new Car(productName, productIds, camParaNameComboBox.Items[camParaNameComboBox.SelectedIndex].ToString()));
                isAlter = true;
                int currRow = this.NewCars.Count;
                ShowCurrItem(currRow - 1);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
        }
        

        private void changeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var _cells = productInfoDataGrid.SelectedCells;

                if (!_cells.Any())
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未选择修改项！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                //检验格式
                string newName = projectNameTextBox.Text;
                if (newName == string.Empty)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                if (-1 != newName.IndexOfAny(new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }))
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称不能包含字符") + "\\/:*?\"<>|", GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                if (projectIDTextBox.Text == string.Empty)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                List<int> newIds = new List<int>();
                string[] strings = projectIDTextBox.Text.Split(';', ',', '.');
                for (int i = 0; i < strings.Length; i++)
                {
                    if (!int.TryParse(strings[i], out int productId))
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID格式不正确！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        return;
                    }
                    else
                    {
                        newIds.Add(productId);
                    }
                }
                if (newIds.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID不能为空！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                //修改前的信息
                int rowIndex = productInfoDataGrid.Items.IndexOf(_cells.First().Item);
                int columnIndex = _cells.First().Column.DisplayIndex;

                //int currRow = dgvProductItemIfo.CurrentRow.Index;
                //string oldName = (string)dgvProductItemIfo.Rows[currRow].Cells[1].Value;
                //object temp = this.productInfoDataGrid.SelectedItem;
                //OneProcessData data = temp as OneProcessData;
                //Process my = Process.GetProcessById(data.Id);
                string oldName = (productInfoDataGrid.Columns[1].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;

                //找到要修改的对象的Guid
                Guid key = new Guid();
                foreach (var item in NewCars.Keys)
                {
                    if (NewCars[item].Name == oldName)
                    {
                        key = item;
                        break;
                    }
                }

                //检验是否重复
                foreach (var item in NewCars.Keys)
                {
                    if (item == key)
                    {
                        continue;
                    }
                    if (NewCars[item].Name == newName)
                    {
                        System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品名称已存在，不能重复添加！"), GlobalVarAndFunc.LanguageTranslate("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    foreach (var productId in newIds)
                    {
                        if (NewCars[item].IDs != null && NewCars[item].IDs.Contains(productId))
                        {
                            System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("产品ID") + productId + GlobalVarAndFunc.LanguageTranslate("已存在，不能重复添加！"), GlobalVarAndFunc.LanguageTranslate("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                //修改
                NewCars[key].Name = newName;
                NewCars[key].IDs = newIds;
                NewCars[key].CamParamName = camParaNameComboBox.SelectedValue.ToString();

                ShowCurrItem(rowIndex);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
        }

        private void delectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (productInfoDataGrid.CurrentCell== null)
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("未选择删除项！"), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                var _cells = productInfoDataGrid.SelectedCells;
                if (_cells.Any())
                {
                    int rowIndex = productInfoDataGrid.Items.IndexOf(_cells.First().Item);
                    int columnIndex = _cells.First().Column.DisplayIndex;
                    string productName = (productInfoDataGrid.Columns[1].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;

                    //找到要删除的对象
                    Guid key = new Guid();
                    bool isfound = false;
                    foreach (var item in NewCars.Keys)
                    {
                        if (NewCars[item].Name == productName)
                        {
                            key = item;
                            isfound = true;
                            break;
                        }
                    }
                    if (isfound)
                    {
                        //移除
                        NewCars.Remove(key);
                        isAlter = true;
                        ShowCurrItem(rowIndex - 1);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(GlobalVarAndFunc.LanguageTranslate("请先选择删除对象。"),
                        GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }
                
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), GlobalVarAndFunc.LanguageTranslate("提示"), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
        }

        private void productInfoDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            try
            {
                var _cells = productInfoDataGrid.SelectedCells;
                if (_cells.Any())
                {

                    int rowIndex = productInfoDataGrid.Items.IndexOf(_cells.First().Item);
                    int columnIndex = _cells.First().Column.DisplayIndex;
                    string oldName = (productInfoDataGrid.Columns[1].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;

                    //projectNameTextBox.Text = (productInfoDataGrid.Columns[1].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;
                    //projectIDTextBox.Text = (productInfoDataGrid.Columns[2].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;
                    //camParaNameComboBox.Text = (productInfoDataGrid.Columns[3].GetCellContent(productInfoDataGrid.Items[rowIndex]) as TextBlock).Text;

                    projectNameTextBox.Text = mainModel.CarsRecord[rowIndex].Name;
                    projectIDTextBox.Text = mainModel.CarsRecord[rowIndex].IDs;
                    camParaNameComboBox.SelectedValue = mainModel.CarsRecord[rowIndex].CamParamName;

                }

            }
            catch (Exception ex) { System.Windows.Forms.MessageBox.Show(ex.ToString()); }
        }
    }

    

}
