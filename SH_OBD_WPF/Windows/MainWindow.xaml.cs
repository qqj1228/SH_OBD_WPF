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
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private double _lastHeight;
        public static bool _bCanOBDTest;
        private string _serialRecvBuf;
        private readonly OBDIfEx _obdIfEx;
        private readonly OBDTest _obdTest;
        private AdvancedWindow w_Advanced;
        CancellationTokenSource _ctsOBDTestStart;
        CancellationTokenSource _ctsSetupColumnsDone;
        CancellationTokenSource _ctsWriteDbStart;

        public MainWindow() {
            InitializeComponent();
            Title += " Ver(Main/Dll): " + MainFileVersion.AssemblyVersion + "/" + DllVersion<SH_OBD_Dll>.AssemblyVersion;
            _serialRecvBuf = "";
            _bCanOBDTest = true;
            _lastHeight = Height;
            _obdIfEx = new OBDIfEx();
            if (_obdIfEx.StrLoadConfigResult.Length > 0) {
                _obdIfEx.StrLoadConfigResult += "是否要以默认配置运行程序？点击\"否\"：将会退出程序。";
                MessageBoxResult result = HandyControl.Controls.MessageBox.Ask(_obdIfEx.StrLoadConfigResult, "加载配置文件出错");
                if (result == MessageBoxResult.No) {
                    Environment.Exit(0);
                }
            }
            _obdTest = new OBDTest(_obdIfEx);
            if (_obdIfEx.ScannerPortOpened) {
                _obdIfEx.ScannerSP.DataReceived += new SerialPortClass.SerialPortDataReceiveEventArgs(SerialDataReceived);
            }
            _obdTest.OBDTestStart += new Action(OnOBDTestStart);
            _obdTest.SetupColumnsDone += new Action(OnSetupColumnsDone);
            _obdTest.WriteDbStart += new Action(OnWriteDbStart);
            _obdTest.WriteDbDone += new Action(OnWriteDbDone);

            // 测试本地数据库连接是否正常
            Task.Factory.StartNew(TestNativeDatabase);
        }

        private void TestNativeDatabase() {
            try {
                _obdTest.DbLocal.GetPassWord();
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("Access native database failed: " + ex.Message);
                HandyControl.Controls.MessageBox.Error("检测到本地数据库通讯异常，请排查相关故障：\n" + ex.Message, "本地数据库通讯异常");
            }
        }

        private CancellationTokenSource UpdateUITask(string strMsg) {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            Task.Factory.StartNew(() => {
                int count = 0;
                while (!token.IsCancellationRequested) {
                    try {
                        Dispatcher.Invoke(new Action(() => {
                            txtBlkResult.Foreground = SystemColors.ControlTextBrush;
                            if (count == 0) {
                                txtBlkResult.Text = strMsg + "。。。";
                            } else {
                                txtBlkResult.Text = strMsg + "，用时" + count.ToString() + "s";
                            }
                        }));
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
            if (!_obdTest.AdvancedMode) {
                _ctsOBDTestStart = UpdateUITask("OBD检测中");
            }
        }

        void OnSetupColumnsDone() {
            if (!_obdTest.AdvancedMode) {
                _ctsOBDTestStart.Cancel();
                _ctsSetupColumnsDone = UpdateUITask("正在读取结果");
            }
        }

        void OnWriteDbStart() {
            if (!_obdTest.AdvancedMode) {
                _ctsSetupColumnsDone.Cancel();
                _ctsWriteDbStart = UpdateUITask("正在写入本地数据库");
            }
        }

        void OnWriteDbDone() {
            if (!_obdTest.AdvancedMode) {
                _ctsWriteDbStart.Cancel();
                Dispatcher.Invoke(() => {
                    txtBlkResult.Foreground = SystemColors.ControlTextBrush;
                    txtBlkResult.Text = "写入本地数据库结束";
                });
            }
        }

        void SerialDataReceived(object sender, SerialDataReceivedEventArgs e, byte[] bits) {
            bool bIsTxtBox = false;
            Dispatcher.Invoke(() => {
                var con = FocusManager.GetFocusedElement(gdContainer);
                if (con is TextBox tb) {
                    bIsTxtBox = true;
                }
            });
            if (bIsTxtBox) {
                _serialRecvBuf += Encoding.Default.GetString(bits).ToUpper();
                if (_serialRecvBuf.Contains("\n")) {
                    if (!_bCanOBDTest) {
                        Dispatcher.Invoke(() => {
                            txtBoxVIN.SelectAll();
                            HandyControl.Controls.MessageBox.Warning("上一辆车还未完全结束检测过程，请稍后再试", "OBD检测出错");
                        });
                        _serialRecvBuf = "";
                        return;
                    }
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
                        if (!_obdTest.AdvancedMode) {
                            Task.Factory.StartNew(StartOBDTest);
                        }
                    }
                }
            }
        }

        private void StartOBDTest() {
            _bCanOBDTest = false;
            Dispatcher.Invoke(() => {
                txtBlkResult.Foreground = SystemColors.ControlTextBrush;
                txtBlkResult.Text = "准备OBD检测";
                bderVINErr.Background = null;
                txtBlkVINErr.Foreground = SystemColors.GrayTextBrush;
                bderCALIDCVN.Background = null;
                txtBlkCALIDCVN.Foreground = SystemColors.GrayTextBrush;
                bderDTC.Background = null;
                txtBlkDTC.Foreground = SystemColors.GrayTextBrush;
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
                    txtBlkResult.Foreground = new SolidColorBrush(Colors.Red);
                    txtBlkResult.Text = "连接车辆失败！";
                });
                _bCanOBDTest = true;
                return;
            }
            tokenSource.Cancel();

            string errorMsg = "";
            try {
                _obdTest.StartOBDTest(out errorMsg);
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("OBD test occurred error: " + errorMsg + ", " + ex.Message);
                HandyControl.Controls.MessageBox.Error(ex.Message + "\n" + errorMsg, "OBD检测出错");
            } finally {
                _obdTest.StrVIN_IN = "";
                _obdTest.StrType_IN = "";
                if (_ctsOBDTestStart != null) {
                    _ctsOBDTestStart.Cancel();
                }
                if (_ctsSetupColumnsDone != null) {
                    _ctsSetupColumnsDone.Cancel();
                }
                if (_ctsWriteDbStart != null) {
                    _ctsWriteDbStart.Cancel();
                }
                _bCanOBDTest = true;
            }

            Dispatcher.Invoke(() => {
                if (_obdTest.OBDResult) {
                    txtBlkResult.Foreground = new SolidColorBrush(Colors.GreenYellow);
                    txtBlkResult.Text = "OBD检测结果：合格";
                } else {
                    if (!_obdTest.VINResult) {
                        bderVINErr.Background = new SolidColorBrush(Colors.Red);
                        txtBlkVINErr.Foreground = SystemColors.ControlTextBrush;
                    }
                    if (!_obdTest.CALIDCVNResult) {
                        bderCALIDCVN.Background = new SolidColorBrush(Colors.Red);
                        txtBlkCALIDCVN.Foreground = SystemColors.ControlTextBrush;
                    }
                    if (!_obdTest.DTCResult) {
                        bderDTC.Background = new SolidColorBrush(Colors.Red);
                        txtBlkDTC.Foreground = SystemColors.ControlTextBrush;
                    }
                    txtBlkResult.Foreground = new SolidColorBrush(Colors.Red);
                    if (_obdTest.VehicleTypeExist && _obdTest.CALIDCheckResult && _obdTest.CVNCheckResult) {
                        txtBlkResult.Text = "OBD检测结果：不合格";
                    } else {
                        bderCALIDCVN.Background = new SolidColorBrush(Colors.Red);
                        txtBlkCALIDCVN.Foreground = SystemColors.ControlTextBrush;
                        txtBlkResult.Text = "结果：";
                    }
                    if (!_obdTest.VehicleTypeExist) {
                        txtBlkResult.Text += "缺少车型数据";
                    }
                    if (!_obdTest.CALIDCheckResult) {
                        if (txtBlkResult.Text.Length > 3) {
                            txtBlkResult.Text += "，";
                        }
                        txtBlkResult.Text += "CALID校验不合格";
                    }
                    if (!_obdTest.CVNCheckResult) {
                        if (txtBlkResult.Text.Length > 3) {
                            txtBlkResult.Text += "，";
                        }
                        txtBlkResult.Text += "CVN校验不合格";
                    }

                }
            });
        }

        /// <summary>
        /// 查找子控件
        /// </summary>
        /// <typeparam name="T">控件类型</typeparam>
        /// <param name="parent">父控件依赖对象</param>
        /// <param name="lstT">子控件列表</param>
        public static void FindVisualChild<T>(DependencyObject parent, ref List<T> lstT) where T : DependencyObject {
            if (parent != null) {
                int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < numVisuals; i++) {
                    Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                    if (v is T child) {
                        lstT.Add(child);
                    }
                    FindVisualChild<T>(v, ref lstT);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            _lastHeight = ActualHeight;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (_lastHeight == 0 || _lastHeight == double.NaN) {
                return;
            }
            double scale = ActualHeight / _lastHeight;
            txtBoxVIN.FontSize *= scale;
            txtBoxVehicleType.FontSize *= scale;
            txtBlkResult.FontSize *= scale;
            _lastHeight = ActualHeight;
        }

        private void BtnAdvMode_Click(object sender, RoutedEventArgs e) {
            _obdTest.AccessAdvancedMode = 0;
            PwdWindow pwdWindow = new PwdWindow(_obdTest) {
                Owner = this
            };
            pwdWindow.ShowDialog();
            if (_obdTest.AccessAdvancedMode > 0) {
                _obdTest.AdvancedMode = true;
                w_Advanced = new AdvancedWindow(_obdIfEx, _obdTest) {
                    Owner = this
                };
                this.Hide();
                w_Advanced.ShowDialog();
                this.Show();
            } else if (_obdTest.AccessAdvancedMode < 0) {
                HandyControl.Controls.MessageBox.Error("密码错误！", "拒绝访问");
                txtBoxVIN.Focus();
            } else {
                txtBoxVIN.Focus();
            }
        }

        private void Window_Activated(object sender, EventArgs e) {
            var con = FocusManager.GetFocusedElement(gdContainer);
            if (con is TextBox tb) {
                tb.Focus();
            } else {
                txtBoxVIN.Focus();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (w_Advanced != null) {
                w_Advanced.Close();
            }
            Monitor.Enter(_obdIfEx);
            if (_obdIfEx.OBDIf.ConnectedStatus) {
                _obdIfEx.OBDIf.Disconnect();
            }
            Monitor.Exit(_obdIfEx);
        }

        private void TxtBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e.Text.Contains("\r")) { // 按下回车后WPF TextBox的TextInput事件传入的Text参数为'\r'而不是通常的'\n'
                if (!_bCanOBDTest) {
                    txtBoxVIN.SelectAll();
                    HandyControl.Controls.MessageBox.Warning("上一辆车还未完全结束检测过程，请稍后再试", "OBD检测出错");
                    return;
                }
                TextBox tb = sender as TextBox;
                string[] codes = tb.Text.Split('*');
                if (codes != null) {
                    if (codes.Length > 2) {
                        _obdTest.StrVIN_IN = codes[2];
                    }
                    _obdTest.StrType_IN = codes[0];
                    txtBoxVIN.Text = _obdTest.StrVIN_IN;
                    txtBoxVehicleType.Text = _obdTest.StrType_IN;
                }
                if (_obdTest.StrVIN_IN.Length == 17 && _obdTest.StrType_IN.Length >= 10) {
                    if (!_obdTest.AdvancedMode) {
                        Task.Factory.StartNew(StartOBDTest);
                        txtBoxVIN.SelectAll();
                        txtBoxVehicleType.SelectAll();
                    }
                }
            }
        }
    }
}
