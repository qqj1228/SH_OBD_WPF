using SH_OBD_Main;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace SH_OBD_WPF {
    /// <summary>
    /// AdvancedWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AdvancedWindow : Window {
        public AdvancedWindow(OBDIfEx _obdIfEx, OBDTest _obdTest) {
            InitializeComponent();
        }
    }
}
