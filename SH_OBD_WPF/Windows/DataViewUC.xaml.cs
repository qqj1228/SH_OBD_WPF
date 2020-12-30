using SH_OBD_DLL;
using SH_OBD_Main;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace SH_OBD_WPF {
    /// <summary>
    /// DataViewUC.xaml 的交互逻辑
    /// </summary>
    public partial class DataViewUC : UserControl {
        private readonly OBDIfEx _obdIfEx;
        private readonly OBDTest _obdTest;
        private CancellationTokenSource _ctsOBDTestStart;
        private CancellationTokenSource _ctsSetupColumnsDone;
        private CancellationTokenSource _ctsWriteDbStart;
        private string _serialRecvBuf;


        public DataViewUC(OBDIfEx obdIfEx, OBDTest obdTest) {
            InitializeComponent();
            _obdIfEx = obdIfEx;
            _obdTest = obdTest;
        }

        private CancellationTokenSource UpdateUITask(string strMsg) {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            Task.Factory.StartNew(() => {
                int count = 0;
                while (!token.IsCancellationRequested) {
                    try {
                        Dispatcher.Invoke(() => {
                            txtBlkInfo.Foreground = SystemColors.ControlTextBrush;
                            if (count == 0) {
                                txtBlkInfo.Text = strMsg + "。。。";
                            } else {
                                txtBlkInfo.Text = strMsg + "，用时" + count.ToString() + "s";
                            }
                        });
                    } catch (ObjectDisposedException ex) {
                        _obdIfEx.Log.TraceWarning(ex.Message);
                    }
                    Thread.Sleep(1000);
                    ++count;
                }
            }, token);
            return tokenSource;
        }

        void OnOBDTestStart() {
            _ctsOBDTestStart = UpdateUITask("开始OBD检测");
        }

        void OnSetupColumnsDone() {
            if (_ctsOBDTestStart != null) {
                _ctsOBDTestStart.Cancel();
            }
            _ctsSetupColumnsDone = UpdateUITask("正在读取车辆信息");
        }

        void OnWriteDbStart() {
            _ctsSetupColumnsDone.Cancel();
            _ctsWriteDbStart = UpdateUITask("正在写入本地数据库");
            Dispatcher.Invoke(() => {
                txtBoxVIN.IsEnabled = true;
                txtBoxVehicleType.IsEnabled = true;
                UpdateDataGridUI(dgInfo);
                UpdateDataGridUI(dgECUInfo);
                UpdateDataGridUI(dgIUPR);
                SetStatusBarContent();
            });
        }

        void OnWriteDbDone() {
            _ctsWriteDbStart.Cancel();
            Dispatcher.Invoke(() => {
                txtBlkInfo.Foreground = SystemColors.ControlTextBrush;
                txtBlkInfo.Text = "数据库写入完成";
                txtBlkProtocol.Text = _obdIfEx.OBDIf.GetProtocol().ToString();
                txtBlkStandard.Text = _obdIfEx.OBDIf.GetStandard().ToString();
            });
        }

        void OnReadDataFromDBDone() {
            Dispatcher.Invoke(() => {
                txtBlkInfo.Foreground = SystemColors.ControlTextBrush;
                txtBlkInfo.Text = "结果数据显示完毕";
                txtBoxVIN.IsEnabled = true;
                txtBoxVehicleType.IsEnabled = true;
                UpdateDataGridUI(dgInfo);
                UpdateDataGridUI(dgECUInfo);
                UpdateDataGridUI(dgIUPR);
            });
        }

        void OnSetDataTableColumnsError(object sender, SetDataTableColumnsErrorEventArgs e) {
            Dispatcher.Invoke(() => {
                txtBlkInfo.Foreground = new SolidColorBrush(Colors.Red);
                txtBlkInfo.Text = e.ErrorMsg;
                txtBoxVIN.IsEnabled = true;
                txtBoxVehicleType.IsEnabled = true;
            });
        }

        void SerialDataReceived(object sender, SerialDataReceivedEventArgs e, byte[] bits) {
            bool bIsTxtBox = false;
            Dispatcher.Invoke(() => {
                var con = FocusManager.GetFocusedElement(gdInput);
                if (con is TextBox tb) {
                    bIsTxtBox = true;
                }
            });
            if (bIsTxtBox) {
                _serialRecvBuf += Encoding.Default.GetString(bits).ToUpper();
                if (_serialRecvBuf.Contains("\n")) {
                    string strTxt = _serialRecvBuf.Split('\n')[0];
                    _serialRecvBuf = _serialRecvBuf.Split('\n')[1];
                    string[] codes = strTxt.Trim().Split('*');
                    if (codes != null) {
                        if (codes.Length > 2) {
                            _obdTest.StrVIN_IN = codes[2];
                        }
                        _obdTest.StrType_IN = codes[0];
                        Dispatcher.Invoke(() => {
                            txtBoxVIN.Text = _obdTest.StrVIN_IN;
                            txtBoxVehicleType.Text = _obdTest.StrType_IN;
                        });
                    }
                    if (_obdTest.StrVIN_IN.Length == 17 && _obdTest.StrType_IN.Length >= 10) {
                        Task.Factory.StartNew(StartOBDTest);
                    }
                }
            }
        }

        private void SetDataGridColumnsStyle(DataGrid dg) {
            dg.Columns[0].Width = DataGridLength.SizeToHeader;
            dg.Columns[0].CanUserSort = false;
            for (int i = 1; i < dg.Columns.Count; i++) {
                dg.Columns[i].Width = DataGridLength.SizeToCells;
                dg.Columns[i].CanUserSort = false;
            }
        }

        private void UpdateDataGridUI(DataGrid dg) {
            dg.ItemsSource = null;
            switch (dg.Name) {
            case "dgInfo":
                dg.ItemsSource = _obdTest.GetDataTable(DataTableType.dtInfo).DefaultView;
                break;
            case "dgECUInfo":
                dg.ItemsSource = _obdTest.GetDataTable(DataTableType.dtECUInfo).DefaultView;
                break;
            case "dgIUPR":
                dg.ItemsSource = _obdTest.GetDataTable(DataTableType.dtIUPR).DefaultView;
                break;
            }
            if (dg.Columns.Count > 1) {
                SetDataGridColumnsStyle(dg);
            }
        }

        private void StartOBDTest() {
            if (!_obdTest.AdvancedMode) {
                return;
            }
            Dispatcher.Invoke(() => {
                txtBlkInfo.Foreground = SystemColors.ControlTextBrush;
                txtBlkInfo.Text = "准备OBD检测";
            });
            _obdIfEx.Log.TraceInfo(string.Format(">>>>>>>>>> Start to test vehicle of [VIN: {0}, VehicleType: {1}] MainVersion: {2} <<<<<<<<<<",
                                    _obdTest.StrVIN_IN, _obdTest.StrType_IN, MainFileVersion.AssemblyVersion));
            if (_obdIfEx.OBDIf.ConnectedStatus) {
                _obdIfEx.OBDIf.Disconnect();
            }
            CancellationTokenSource tokenSource = UpdateUITask("正在连接车辆");
            if (!_obdIfEx.OBDDll.ConnectOBD()) {
                tokenSource.Cancel();
                Dispatcher.Invoke(() => {
                    txtBlkInfo.Foreground = new SolidColorBrush(Colors.Red);
                    txtBlkInfo.Text = "连接车辆失败！";
                });
                return;
            }
            tokenSource.Cancel();

            string errorMsg = string.Empty;
            try {
                _obdTest.StartOBDTest(out errorMsg);
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("OBD test occurred error: " + errorMsg + ", " + ex.Message);
                HandyControl.Controls.MessageBox.Error(ex.Message, "OBD检测出错");
            }

            Dispatcher.Invoke(() => {
                if (_obdTest.OBDResult) {
                    txtBlkInfo.Foreground = new SolidColorBrush(Colors.ForestGreen);
                    txtBlkInfo.Text = "OBD检测结束，结果：合格";
                } else {
                    string strCat = string.Empty;
                    if (!_obdTest.DTCResult) {
                        strCat += "，存在DTC故障码";
                    }
                    if (!_obdTest.CALIDCVNResult) {
                        strCat += "，CAL_ID或CVN数据异常";
                    }
                    if (!_obdTest.VINResult) {
                        strCat += "，VIN号不匹配";
                    }
                    if (!_obdTest.VehicleTypeExist) {
                        strCat += "，缺少车型数据";
                    }
                    if (!_obdTest.CALIDCheckResult) {
                        strCat += "，CAL_ID校验不合格";
                    }
                    if (!_obdTest.CVNCheckResult) {
                        strCat += "，CVN校验不合格";
                    }
                    txtBlkInfo.Foreground = new SolidColorBrush(Colors.Red);
                    txtBlkInfo.Text = "OBD检测结束，结果：不合格" + strCat;
                }
                txtBoxVIN.IsEnabled = true;
                txtBoxVehicleType.IsEnabled = true;
            });
            if (_ctsOBDTestStart != null) {
                _ctsOBDTestStart.Cancel();
            }
            if (_ctsSetupColumnsDone != null) {
                _ctsSetupColumnsDone.Cancel();
            }
            if (_ctsWriteDbStart != null) {
                _ctsWriteDbStart.Cancel();
            }
        }

        private void ShowDataFromDB() {
            if (!_obdTest.AdvancedMode) {
                return;
            }
            _obdIfEx.Log.TraceInfo("Start ShowDataFromDB");
            txtBlkInfo.Foreground = SystemColors.ControlTextBrush;
            txtBlkInfo.Text = "手动读取数据";
            try {
                _obdTest.ShowDataFromDB(txtBoxVIN.Text);
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("ShowDataFromDB occurred error: " + ex.Message);
                HandyControl.Controls.MessageBox.Error(ex.Message, "手动读取数据出错");
            }
            if (_ctsOBDTestStart != null) {
                _ctsOBDTestStart.Cancel();
            }
            if (_ctsSetupColumnsDone != null) {
                _ctsSetupColumnsDone.Cancel();
            }
            if (_ctsWriteDbStart != null) {
                _ctsWriteDbStart.Cancel();
            }
        }

        private void SetStatusBarContent() {
            txtBlkHardware.Text = _obdIfEx.OBDIf.GetDevice().ToString().Replace("ELM327", "SH-VCI-302U");
            txtBlkProtocol.Text = _obdIfEx.OBDIf.GetProtocol().ToString();
            txtBlkStandard.Text = _obdIfEx.OBDIf.GetStandard().ToString();
            txtBlkPort.Text = _obdIfEx.OBDIf.DllSettings.ComPortName;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            _obdTest.AdvancedMode = true;
            if (_obdIfEx.ScannerPortOpened) {
                _obdIfEx.ScannerSP.DataReceived += new SerialPortClass.SerialPortDataReceiveEventArgs(SerialDataReceived);
            }
            _obdTest.OBDTestStart += new Action(OnOBDTestStart);
            _obdTest.SetupColumnsDone += new Action(OnSetupColumnsDone);
            _obdTest.WriteDbStart += new Action(OnWriteDbStart);
            _obdTest.WriteDbDone += new Action(OnWriteDbDone);
            _obdTest.ReadDataFromDBDone += new Action(OnReadDataFromDBDone);
            _obdTest.SetDataTableColumnsError += OnSetDataTableColumnsError;
            btnStart.FontSize = txtBoxVIN.FontSize;
            chkBoxShowData.FontSize = txtBoxVIN.FontSize;
            UpdateDataGridUI(dgInfo);
            UpdateDataGridUI(dgECUInfo);
            UpdateDataGridUI(dgIUPR);
            txtBoxVIN.Text = _obdTest.StrVIN_IN;
            txtBoxVehicleType.Text = _obdTest.StrType_IN;
            SetStatusBarContent();
        }

        private void TxtBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e.Text.Contains("\r")) { // 按下回车后WPF TextBox的TextInput事件传入的Text参数为'\r'而不是通常的'\n'
                TextBox tb = sender as TextBox;
                string[] codes = tb.Text.Split('*');
                if (codes != null) {
                    if (codes.Length > 2) {
                        _obdTest.StrVIN_IN = codes[2];
                        _obdTest.StrType_IN = codes[0];
                        txtBoxVIN.Text = _obdTest.StrVIN_IN;
                        txtBoxVehicleType.Text = _obdTest.StrType_IN;
                    } else {
                        if (tb.Name == "txtBoxVIN") {
                            _obdTest.StrVIN_IN = codes[0];
                        } else if (tb.Name == "txtBoxVehicleType") {
                            _obdTest.StrType_IN = codes[0];
                        }
                    }
                }
                if (_obdTest.StrVIN_IN.Length == 17 && _obdTest.StrType_IN.Length >= 10) {
                    _obdIfEx.Log.TraceInfo("Get VIN: " + txtBoxVIN.Text);
                    if (btnStart.IsEnabled) {
                        txtBoxVIN.IsEnabled = false;
                        txtBoxVehicleType.IsEnabled = false;
                        btnStart.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    }
                }
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e) {
            if (chkBoxShowData.IsChecked == true) {
                ShowDataFromDB();
            } else {
                Task.Factory.StartNew(StartOBDTest);
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) {
            if (_obdIfEx.ScannerPortOpened) {
                _obdIfEx.ScannerSP.DataReceived -= new SerialPortClass.SerialPortDataReceiveEventArgs(SerialDataReceived);
            }
            _obdTest.AdvancedMode = false;
            _obdTest.OBDTestStart -= new Action(OnOBDTestStart);
            _obdTest.SetupColumnsDone -= new Action(OnSetupColumnsDone);
            _obdTest.WriteDbStart -= new Action(OnWriteDbStart);
            _obdTest.WriteDbDone -= new Action(OnWriteDbDone);
            _obdTest.ReadDataFromDBDone -= new Action(OnReadDataFromDBDone);
            _obdTest.SetDataTableColumnsError -= OnSetDataTableColumnsError;
        }
    }
}
