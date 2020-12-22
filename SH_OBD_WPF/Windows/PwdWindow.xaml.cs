using SH_OBD_Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SH_OBD_WPF {
    /// <summary>
    /// PwdWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PwdWindow : Window {
        private readonly OBDTest _obdTest;

        public PwdWindow(OBDTest obdTest) {
            InitializeComponent();
            _obdTest = obdTest;
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] output = md5.ComputeHash(Encoding.Default.GetBytes(txtBoxPwd.Text.Trim()));
            string strValue = BitConverter.ToString(output).Replace("-", "");
            if (strValue == _obdTest.DbLocal.GetPassWord()) {
                _obdTest.AccessAdvancedMode = 1;
            } else {
                _obdTest.AccessAdvancedMode = -1;
            }
            md5.Dispose();
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            txtBoxPwd.Focus();
        }
    }
}
