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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SH_OBD_WPF {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private double _lastHeight;

        public MainWindow() {
            InitializeComponent();
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
            bderVINErr.Background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            txtBlkVINErr.Foreground = SystemColors.ControlTextBrush;
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
    }
}
