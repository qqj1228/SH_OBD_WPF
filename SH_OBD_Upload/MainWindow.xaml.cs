using LibBase;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SH_OBD_Upload {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private readonly Logger _log;
        private readonly Config _cfg;
        private readonly ModelOracle _oraMES;
        private readonly DataTable _dtShow;
        private readonly Dictionary<string, DataTable> _dicResults;
        private readonly string _strTester;

        public MainWindow() {
            InitializeComponent();
            _log = new Logger(".\\log", EnumLogLevel.LogLevelAll, true, 100);
            _cfg = new Config(_log);
            _oraMES = new ModelOracle(_cfg.OracleMES.Data, _log);
            _dtShow = new DataTable();
            _dicResults = new Dictionary<string, DataTable>();
            _strTester = "OBDPad";
        }

        private FileInfo[] ShowXmlFiles(string strDir) {
            DirectoryInfo di = new DirectoryInfo(strDir);
            FileInfo[] fis = di.GetFiles();
            _dtShow.Rows.Clear();
            _dicResults.Clear();
            for (int i = 0; i < fis.Length; i++) {
                DataTable dt = new DataTable("OBDData");
                dt.ReadXml(fis[i].FullName);
                _dicResults.Add(fis[i].FullName, dt);
                DataRow dr = _dtShow.NewRow();
                dr["文件名"] = fis[i].Name;
                dr["OBD是否合格"] = dt.Rows[0]["Result"].ToString() == "1" ? "合格" : "不合格";
                dr["是否已上传"] = dt.Rows[0]["Upload"].ToString() == "1" ? "已上传" : "未上传";
                _dtShow.Rows.Add(dr);
            }
            return fis;
        }

        private void SetDataTable1Oracle(string strVIN, DataTable dt) {
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("VIN", typeof(string));
            dt.Columns.Add("CREATIONTIME", typeof(DateTime));
            dt.Columns.Add("CREATOR", typeof(string));
            dt.Columns.Add("ISDELETED", typeof(string));

            DataRow dr = dt.NewRow();
            dr["ID"] = _oraMES.IDValue;
            dr["VIN"] = strVIN;
            dr["CREATIONTIME"] = DateTime.Now.ToLocalTime();
            dr["CREATOR"] = _strTester;
            dr["ISDELETED"] = "0";
            dt.Rows.Add(dr);
            int iRet;

            DataTable dtID = dt.Clone();
            _oraMES.GetRecords(dtID, new Dictionary<string, string>() { { "VIN", strVIN } });
            if (dtID.Rows.Count > 0) {
                List<string> whereVals = new List<string>() { dtID.Rows[dtID.Rows.Count - 1]["ID"].ToString() };
                dt.Columns.Remove("ID");
                iRet = _oraMES.UpdateRecords(dt, "ID", whereVals);
            } else {
                iRet = _oraMES.InsertRecords(dt, true);
            }
            if (iRet <= 0) {
                throw new Exception("插入或更新 MES 数据出错，返回的影响行数: " + iRet.ToString());
            }
        }

        private void SetDataTable3Oracle(string strKeyID, string strOBDResult, DataTable dt) {
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("WQPF_ID", typeof(string));
            dt.Columns.Add("TESTTYPE", typeof(string));
            dt.Columns.Add("OPASS", typeof(string));
            dt.Columns.Add("OTESTDATE", typeof(string));
            dt.Columns.Add("RESULT", typeof(string));
            dt.Columns.Add("CREATIONTIME", typeof(DateTime));
            dt.Columns.Add("CREATOR", typeof(string));
            dt.Columns.Add("ISDELETED", typeof(string));

            DataRow dr = dt.NewRow();
            dr["ID"] = _oraMES.IDValue;
            dr["WQPF_ID"] = strKeyID;
            dr["TESTTYPE"] = "0";
            dr["OPASS"] = strOBDResult;
            dr["OTESTDATE"] = DateTime.Now.ToLocalTime().ToString("yyyyMMdd");
            dr["RESULT"] = strOBDResult;
            dr["CREATIONTIME"] = DateTime.Now.ToLocalTime();
            dr["CREATOR"] = _strTester;
            dr["ISDELETED"] = "0";
            dt.Rows.Add(dr);
            int iRet;

            DataTable dtID = dt.Clone();
            _oraMES.GetRecords(dtID, new Dictionary<string, string>() { { "WQPF_ID", strKeyID } });
            if (dtID.Rows.Count > 0) {
                List<string> whereVals = new List<string>() { dtID.Rows[dtID.Rows.Count - 1]["ID"].ToString() };
                dt.Columns.Remove("ID");
                iRet = _oraMES.UpdateRecords(dt, "ID", whereVals);
            } else {
                iRet = _oraMES.InsertRecords(dt, true);
            }
            if (iRet <= 0) {
                throw new Exception("插入或更新 MES 数据出错，返回的影响行数: " + iRet.ToString());
            }
        }

        private void SetDataTable4Oracle(string strKeyID, DataTable dt, DataTable dtResult) {
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("WQPF_ID", typeof(string));
            dt.Columns.Add("OBD", typeof(string));
            dt.Columns.Add("ODO", typeof(string));
            dt.Columns.Add("CREATIONTIME", typeof(DateTime));
            dt.Columns.Add("CREATOR", typeof(string));
            dt.Columns.Add("ISDELETED", typeof(string));

            DataRow dr = dt.NewRow();
            dr["ID"] = _oraMES.IDValue;
            dr["WQPF_ID"] = strKeyID;
            dr["OBD"] = dtResult.Rows[0]["OBD_SUP"].ToString().Split(',')[0];
            dr["ODO"] = dtResult.Rows[0]["ODO"].ToString().Replace("不适用", "");
            dr["CREATIONTIME"] = DateTime.Now.ToLocalTime();
            dr["CREATOR"] = _strTester;
            dr["ISDELETED"] = "0";
            dt.Rows.Add(dr);
            int iRet;

            DataTable dtID = dt.Clone();
            _oraMES.GetRecords(dtID, new Dictionary<string, string>() { { "WQPF_ID", strKeyID } });
            if (dtID.Rows.Count > 0) {
                List<string> whereVals = new List<string>() { dtID.Rows[dtID.Rows.Count - 1]["ID"].ToString() };
                dt.Columns.Remove("ID");
                iRet = _oraMES.UpdateRecords(dt, "ID", whereVals);
            } else {
                iRet = _oraMES.InsertRecords(dt, true);
            }
            if (iRet <= 0) {
                throw new Exception("插入或更新 MES 数据出错，返回的影响行数: " + iRet.ToString());
            }
        }

        private void SetDataTable4AOracle(string strKeyID, string strKeyID4, DataTable dt, DataTable dtResult) {
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("WQPF_ID", typeof(string));
            dt.Columns.Add("WQPF4_ID", typeof(string));
            dt.Columns.Add("MODULEID", typeof(string));
            dt.Columns.Add("CALID", typeof(string));
            dt.Columns.Add("CVN", typeof(string));
            dt.Columns.Add("CREATIONTIME", typeof(DateTime));
            dt.Columns.Add("CREATOR", typeof(string));
            dt.Columns.Add("ISDELETED", typeof(string));

            string[] CALIDArray = dtResult.Rows[0]["CAL_ID"].ToString().Split(',');
            string[] CVNArray = dtResult.Rows[0]["CVN"].ToString().Split(',');
            for (int i = 0; i < 2; i++) {
                DataRow dr = dt.NewRow();
                dr["ID"] = _oraMES.IDValue;
                dr["WQPF_ID"] = strKeyID;
                dr["WQPF4_ID"] = strKeyID4;
                dr["MODULEID"] = dtResult.Rows[0]["ECU_ID"];
                dr["CALID"] = CALIDArray.Length > i ? CALIDArray[i] : "-";
                dr["CVN"] = CVNArray.Length > i ? CVNArray[i] : "-";
                dr["CREATIONTIME"] = DateTime.Now.ToLocalTime();
                dr["CREATOR"] = _strTester;
                dr["ISDELETED"] = "0";
                dt.Rows.Add(dr);
            }

            for (int iRow = 1; iRow < dtResult.Rows.Count; iRow++) {
                CALIDArray = dtResult.Rows[iRow]["CAL_ID"].ToString().Split(',');
                CVNArray = dtResult.Rows[iRow]["CVN"].ToString().Split(',');
                for (int i = 0; i < CALIDArray.Length; i++) {
                    DataRow dr = dt.NewRow();
                    dr["ID"] = _oraMES.IDValue;
                    dr["WQPF_ID"] = strKeyID;
                    dr["WQPF4_ID"] = strKeyID4;
                    dr["MODULEID"] = dtResult.Rows[iRow]["ECU_ID"];
                    dr["CALID"] = CALIDArray[i];
                    dr["CVN"] = CVNArray.Length > i ? CVNArray[i] : "";
                    dr["CREATIONTIME"] = DateTime.Now.ToLocalTime();
                    dr["CREATOR"] = _strTester;
                    dr["ISDELETED"] = "0";
                    dt.Rows.Add(dr);
                }
            }
            int iRet;

            DataTable dtID = dt.Clone();
            _oraMES.GetRecords(dtID, new Dictionary<string, string>() { { "WQPF_ID", strKeyID } });
            if (dtID.Rows.Count > 0) {
                List<string> whereVals = new List<string>();
                for (int i = 0; i < dtID.Rows.Count; i++) {
                    whereVals.Add(dtID.Rows[i]["ID"].ToString());
                }
                dt.Columns.Remove("ID");
                iRet = _oraMES.UpdateRecords(dt, "ID", whereVals);
            } else {
                iRet = _oraMES.InsertRecords(dt, true);
            }
            if (iRet <= 0) {
                throw new Exception("插入或更新 MES 数据出错，返回的影响行数: " + iRet.ToString());
            }
        }

        private bool UploadDataOracle(DataTable dtIn) {
            string strVIN = dtIn.Rows[0]["VIN"].ToString();
            string strOBDResult = dtIn.Rows[0]["Result"].ToString();
            DataTable dt1 = new DataTable("IF_EM_WQPF_1");
            DataTable dt3 = new DataTable("IF_EM_WQPF_3");
            DataTable dt4 = new DataTable("IF_EM_WQPF_4");
            DataTable dt4A = new DataTable("IF_EM_WQPF_4_A");
            try {
                SetDataTable1Oracle(strVIN, dt1);
                dt1.Columns.Clear();
                dt1.Rows.Clear();
                dt1.Columns.Add("ID");
                _oraMES.GetRecords(dt1, new Dictionary<string, string> { { "VIN", strVIN } });
                string strKeyID = dt1.Rows[0]["ID"].ToString();
                SetDataTable3Oracle(strKeyID, strOBDResult, dt3);
                SetDataTable4Oracle(strKeyID, dt4, dtIn);
                dt4.Columns.Clear();
                dt4.Rows.Clear();
                dt4.Columns.Add("ID");
                _oraMES.GetRecords(dt4, new Dictionary<string, string> { { "WQPF_ID", strKeyID } });
                string strKeyID4 = dt4.Rows[0]["ID"].ToString();
                SetDataTable4AOracle(strKeyID, strKeyID4, dt4A, dtIn);
                for (int i = 0; i < dtIn.Rows.Count; i++) {
                    dtIn.Rows[i]["Upload"] = 1;
                }
            } catch (Exception ex) {
                _log.TraceError("UploadDataOracle Error: " + ex.Message);
                throw;
            } finally {
                dt1.Dispose();
                dt3.Dispose();
                dt4.Dispose();
                dt4A.Dispose();
            }
            return true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            _dtShow.Columns.Add("文件名");
            _dtShow.Columns.Add("OBD是否合格");
            _dtShow.Columns.Add("是否已上传");
            if (txtBoxDir.Text.Length > 0) {
                ShowXmlFiles(txtBoxDir.Text);
            }
            dgDisplay.ItemsSource = _dtShow.DefaultView;
        }

        private void BtnXmlDir_Click(object sender, RoutedEventArgs e) {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog {
                IsFolderPicker = true // 设置为选择文件夹
            };
            if (CommonFileDialogResult.Ok == dialog.ShowDialog()) {
                txtBoxDir.Text = dialog.FileName;
                if (txtBoxDir.Text.Length > 0) {
                    ShowXmlFiles(txtBoxDir.Text);
                }
                dgDisplay.ItemsSource = _dtShow.DefaultView;
            }
        }

        private void BtnReflash_Click(object sender, RoutedEventArgs e) {
            if (txtBoxDir.Text.Length > 0) {
                ShowXmlFiles(txtBoxDir.Text);
            }
            dgDisplay.ItemsSource = _dtShow.DefaultView;
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e) {
            foreach (string path in _dicResults.Keys) {
                if (_dicResults[path].Rows[0]["Result"].ToString() == "1") {
                    UploadDataOracle(_dicResults[path]);
                    _dicResults[path].WriteXml(path, XmlWriteMode.WriteSchema, true);
                }
            }
            MessageBox.Show("全部数据上传结束", this.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            btnReflash.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, btnReflash));
        }
    }
}
