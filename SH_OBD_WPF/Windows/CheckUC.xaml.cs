using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SH_OBD_DLL;
using SH_OBD_Main;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SH_OBD_WPF {
    /// <summary>
    /// CheckUC.xaml 的交互逻辑
    /// </summary>
    public partial class CheckUC : UserControl {
        private readonly ModelLocal _dbLocal;
        private readonly DataTable _dtContent;
        private readonly Logger _log;
        private readonly string _strSort;

        public CheckUC(ModelLocal dbLocal, Logger log) {
            InitializeComponent();
            _dtContent = new DataTable("VehicleType");
            _dbLocal = dbLocal;
            _log = log;
            _strSort = "Project ASC,Type ASC,ECU_ID ASC,CAL_ID ASC,CVN ASC";
        }

        private void SetDataGridColumnsStyle(DataGrid dg) {
            for (int i = 0; i < dg.Columns.Count; i++) {
                dg.Columns[i].Width = DataGridLength.SizeToCells;
                dg.Columns[i].CanUserSort = false;
            }
        }

        private void UpdateDataGridUI(DataGrid dg) {
            dg.ItemsSource = null;
            dg.ItemsSource = _dtContent.DefaultView;
            if (dg.Columns.Count > 1) {
                SetDataGridColumnsStyle(dg);
            }
        }

        private void SetDataTableContent() {
            _dbLocal.GetEmptyTable(_dtContent);
            _dtContent.DefaultView.Sort = "ID ASC";
            _dbLocal.GetRecords(_dtContent, null);
            UpdateDataGridUI(dgCheck);
        }

        private void ArrangeRecords(DataTable dt, string strSort) {
            if (dt.Rows.Count <= 0) {
                return;
            }
            string[] strColumns = strSort.Replace(" ASC", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            dt.DefaultView.Sort = strSort;
            dt = dt.DefaultView.ToTable(true, strColumns);
            _dbLocal.DeleteAllRecords("VehicleType");
            _dbLocal.ResetTableID("VehicleType");
            try {
                _dbLocal.InsertRecords(dt);
            } catch (Exception ex) {
                _log.TraceError("整理数据出错: " + ex.Message);
                HandyControl.Controls.MessageBox.Error(ex.Message, "整理数据出错");
            }
        }

        private void SetSelectedRow(DataGrid dg, int index) {
            if (!(dg.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow)) {
                // 可能是虚拟化的，将其拉入视图内重试一遍
                // 若已在视图内则无需操作
                dg.UpdateLayout();
                dg.ScrollIntoView(dg.Items[index]);
                DataGridRow dgRow = dg.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
                dgRow.IsSelected = true;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            btnModify.FontSize = txtBoxProject.FontSize;
            btnInsert.FontSize = txtBoxProject.FontSize;
            btnRemove.FontSize = txtBoxProject.FontSize;
            btnOthers.FontSize = txtBoxProject.FontSize;
            contextMenu.FontSize = txtBoxProject.FontSize;
            SetDataTableContent();
        }

        private void DgCheck_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (dgCheck.SelectedItems.Count > 0) {
                if (dgCheck.SelectedItems[dgCheck.SelectedItems.Count - 1] is DataRowView drView) {
                    txtBoxProject.Text = drView["Project"].ToString();
                    txtBoxType.Text = drView["Type"].ToString();
                    txtBoxECUID.Text = drView["ECU_ID"].ToString();
                    txtBoxCALID.Text = drView["CAL_ID"].ToString();
                    txtBoxCVN.Text = drView["CVN"].ToString();
                }
            } else {
                txtBoxProject.Text = string.Empty;
                txtBoxType.Text = string.Empty;
                txtBoxECUID.Text = string.Empty;
                txtBoxCALID.Text = string.Empty;
                txtBoxCVN.Text = string.Empty;
            }
        }

        private void BtnModify_Click(object sender, RoutedEventArgs e) {
            if (txtBoxType.Text.Length > 0 && txtBoxECUID.Text.Length > 0 && txtBoxCALID.Text.Length > 0 && txtBoxCVN.Text.Length > 0) {
                if (dgCheck.SelectedItems.Count > 0) {
                    if (dgCheck.SelectedItems[dgCheck.SelectedItems.Count - 1] is DataRowView drView) {
                        DataTable dtModify = new DataTable("VehicleType");
                        _dbLocal.GetEmptyTable(dtModify);
                        dtModify.Columns.Remove("ID");
                        DataRow dr = dtModify.NewRow();
                        dr["Project"] = txtBoxProject.Text;
                        dr["Type"] = txtBoxType.Text;
                        dr["ECU_ID"] = txtBoxECUID.Text;
                        dr["CAL_ID"] = txtBoxCALID.Text;
                        dr["CVN"] = txtBoxCVN.Text;
                        dtModify.Rows.Add(dr);
                        List<string> whereVals = new List<string>() { drView["ID"].ToString() };
                        try {
                            _dbLocal.UpdateRecords(dtModify, "ID", whereVals);
                            int index = dgCheck.Items.IndexOf(drView);
                            SetDataTableContent();
                            SetSelectedRow(dgCheck, index);
                        } catch (Exception ex) {
                            _log.TraceError("修改数据出错: " + ex.Message);
                            HandyControl.Controls.MessageBox.Error(ex.Message, "修改数据出错");
                        }
                        dtModify.Dispose();
                    }
                }
            }
        }

        private void BtnInsert_Click(object sender, RoutedEventArgs e) {
            if (txtBoxType.Text.Length > 0 && txtBoxECUID.Text.Length > 0 && txtBoxCALID.Text.Length > 0 && txtBoxCVN.Text.Length > 0) {
                DataTable dtInsert = new DataTable("VehicleType");
                _dbLocal.GetEmptyTable(dtInsert);
                DataRow dr = dtInsert.NewRow();
                dr["Project"] = txtBoxProject.Text;
                dr["Type"] = txtBoxType.Text;
                dr["ECU_ID"] = txtBoxECUID.Text;
                dr["CAL_ID"] = txtBoxCALID.Text;
                dr["CVN"] = txtBoxCVN.Text;
                dtInsert.Rows.Add(dr);
                try {
                    int index = dgCheck.Items.Count;
                    _dbLocal.InsertRecords(dtInsert);
                    SetDataTableContent();
                    SetSelectedRow(dgCheck, index);
                } catch (Exception ex) {
                    _log.TraceError("插入数据出错: " + ex.Message);
                    HandyControl.Controls.MessageBox.Error(ex.Message, "插入数据出错");
                }
                dtInsert.Dispose();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e) {
            int selectedCount = dgCheck.SelectedItems.Count;
            if (selectedCount > 0) {
                List<string> IDs = new List<string>(selectedCount);
                foreach (var item in dgCheck.SelectedItems) {
                    if (item is DataRowView drView) {
                        IDs.Add(drView["ID"].ToString());
                    }
                }
                int deletedCount = _dbLocal.DeleteRecords("VehicleType", "ID", IDs);
                SetDataTableContent();
                if (deletedCount != selectedCount) {
                    _log.TraceError(string.Format("Remove error, removed count: {0}, selected item count: {1}", deletedCount, selectedCount));
                    HandyControl.Controls.MessageBox.Error(string.Format("实际删除行数与预期不符，实际：{0}，预期: {1}", deletedCount, selectedCount), "删除数据出错");
                }
                if (_dtContent.Rows.Count > 0) {
                    ArrangeRecords(_dtContent, _strSort);
                    SetDataTableContent();
                }
            }
        }

        private void MenuItemArrange_Click(object sender, RoutedEventArgs e) {
            ArrangeRecords(_dtContent, _strSort);
            SetDataTableContent();
        }

        private void MenuItemImport_Click(object sender, RoutedEventArgs e) {
            DataTable dtImport = new DataTable("VehicleType");
            _dbLocal.GetEmptyTable(dtImport);
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Title = "打开 Excel 导入文件",
                Filter = "Excel 2007 及以上 (*.xlsx)|*.xlsx",
                FilterIndex = 0,
                RestoreDirectory = true
            };
            bool? result = openFileDialog.ShowDialog();
            try {
                if (result == true && openFileDialog.FileName.Length > 0) {
                    FileInfo xlFile = new FileInfo(openFileDialog.FileName);
                    ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                    using (ExcelPackage package = new ExcelPackage(xlFile, true)) {
                        ExcelWorksheet worksheet1 = package.Workbook.Worksheets[0];
                        for (int i = 2; i < worksheet1.Cells.Rows; i++) {
                            if (worksheet1.Cells[i, 1].Value == null || worksheet1.Cells[i, 1].Value.ToString().Length == 0) {
                                break;
                            }
                            DataRow dr = dtImport.NewRow();
                            dr["Project"] = worksheet1.Cells[i, 2].Value.ToString();
                            dr["Type"] = worksheet1.Cells[i, 3].Value.ToString();
                            dr["ECU_ID"] = worksheet1.Cells[i, 4].Value.ToString();
                            dr["CAL_ID"] = worksheet1.Cells[i, 5].Value.ToString();
                            dr["CVN"] = worksheet1.Cells[i, 6].Value.ToString();
                            dtImport.Rows.Add(dr);
                        }
                    }
                    _dbLocal.InsertRecords(dtImport);
                    _dbLocal.GetRecords(_dtContent, null);
                    if (_dtContent.Rows.Count > 0) {
                        ArrangeRecords(_dtContent, _strSort);
                        SetDataTableContent();
                        HandyControl.Controls.MessageBox.Info("导入Excel数据完成", "导入数据");
                    }
                }
            } finally {
                dtImport.Dispose();
            }
        }

        private void MenuItemExport_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog saveFileDialog = new SaveFileDialog {
                Title = "保存 Excel 导出文件",
                Filter = "Excel 2007 及以上 (*.xlsx)|*.xlsx",
                FilterIndex = 0,
                RestoreDirectory = true,
                OverwritePrompt = true,
                FileName = DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd") + "_Export"
            };
            bool? result = saveFileDialog.ShowDialog();
            if (result == true) {
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using (ExcelPackage package = new ExcelPackage()) {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("车型校验文件");
                    // 标题
                    for (int i = 0; i < _dtContent.Columns.Count; i++) {
                        worksheet.Cells[1, i + 1].Value = _dtContent.Columns[i].ColumnName;
                        // 边框
                        worksheet.Cells[1, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin, System.Drawing.Color.Black);
                    }
                    // 格式化标题
                    using (var range = worksheet.Cells[1, 1, 1, _dtContent.Columns.Count]) {
                        range.Style.Font.Bold = true;
                        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }

                    // 记录
                    for (int iRow = 0; iRow < _dtContent.Rows.Count; iRow++) {
                        for (int iCol = 0; iCol < _dtContent.Columns.Count; iCol++) {
                            worksheet.Cells[iRow + 2, iCol + 1].Value = _dtContent.Rows[iRow][iCol].ToString();
                            // 边框
                            worksheet.Cells[iRow + 2, iCol + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin, System.Drawing.Color.Black);
                        }
                    }
                    // 格式化记录
                    using (var range = worksheet.Cells[2, 1, _dtContent.Rows.Count + 1, _dtContent.Columns.Count]) {
                        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                    // 自适应列宽
                    worksheet.Cells.AutoFitColumns(0);
                    // 保存文件
                    FileInfo xlFile = new FileInfo(saveFileDialog.FileName);
                    package.SaveAs(xlFile);
                }
                HandyControl.Controls.MessageBox.Info("导出Excel数据完成", "导出数据");
            }
        }

        private void MenuItemRefresh_Click(object sender, RoutedEventArgs e) {
            SetDataTableContent();
        }

        private void BtnOthers_Click(object sender, RoutedEventArgs e) {
            //目标
            contextMenu.PlacementTarget = btnOthers;
            //位置
            contextMenu.Placement = PlacementMode.Bottom;
            //显示菜单
            contextMenu.IsOpen = true;
        }
    }
}
