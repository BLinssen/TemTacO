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

namespace TemOverlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool detailedChart = false;
        bool showChart = false;
        DateTime ClickTime = new DateTime();
        int logoSize = 1;
        bool loaded = false;
        public MainWindow()
        {
            InitializeComponent();
            loaded = true;
            LoadSettings();
        }

        private void LoadSettings()
        {
            detailedChart = Properties.Settings.Default.ChartDetail;
            showChart = Properties.Settings.Default.ChartVis;
            logoSize = Properties.Settings.Default.LogoSize;
            ResizeLogo(0);
            SliderScale.Value = Properties.Settings.Default.ChartScale;
            RefreshChart();
            CheckBoxChartDetail.IsChecked = detailedChart;
            CheckBoxDisplayOverlay.IsChecked = showChart;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.LogoSize = logoSize;
            Properties.Settings.Default.ChartDetail = detailedChart;
            Properties.Settings.Default.ChartScale = SliderScale.Value;
            Properties.Settings.Default.ChartVis = showChart;
            Properties.Settings.Default.Save();
        }

        private void ToggleWindow()
        {
            TemSettings.Visibility =  TemSettings.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CheckBoxDisplayOverlay_Checked(object sender, RoutedEventArgs e)
        {
            showChart = true;
            RefreshChart();
        }

        private void CheckBoxDisplayOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            showChart = false;
            RefreshChart();
        }

        private void CheckBoxChartDetail_Checked(object sender, RoutedEventArgs e)
        {
            detailedChart = true;
            RefreshChart();
        }

        private void CheckBoxChartDetail_Unchecked(object sender, RoutedEventArgs e)
        {
            detailedChart = false;
            RefreshChart();
        }

        private void RefreshChart()
        {
            if(showChart)
            {
                if (detailedChart)
                {
                    TemTypeImageSmall.Visibility = Visibility.Collapsed;
                    TemTypeImageLarge.Visibility = Visibility.Visible;
                }
                else
                {
                    TemTypeImageSmall.Visibility = Visibility.Visible;
                    TemTypeImageLarge.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                TemTypeImageSmall.Visibility = Visibility.Collapsed;
                TemTypeImageLarge.Visibility = Visibility.Collapsed;
            }
            SaveSettings();
        }

        private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (loaded)
            {
                TemTypeImageLarge.Width = 450 * e.NewValue;
                TemTypeImageLarge.Height = 450 * e.NewValue;
                TemTypeImageSmall.Width = 450 * e.NewValue;
                TemTypeImageSmall.Height = 450 * e.NewValue;
                SaveSettings();
            }
        }

        private void BtnLogoSizeSmaller_Click(object sender, RoutedEventArgs e)
        {
            ResizeLogo(-1);
        }

        private void BtnLogoSizeLarger_Click(object sender, RoutedEventArgs e)
        {
            ResizeLogo(1);
        }

        private void ResizeLogo(int change)
        {
            logoSize = Math.Max(Math.Min(2,(logoSize+change)),0);
            if (logoSize == 0)
            {
                TemLogo.Width = 30;
                TemLogo.Height = 30;
                LogoSizeVal.Content = "Small";
            }
            else if (logoSize == 1)
            {
                TemLogo.Width = 45;
                TemLogo.Height = 45;
                LogoSizeVal.Content = "Medium";
            }
            else
            {
                TemLogo.Width = 60;
                TemLogo.Height = 60;
                LogoSizeVal.Content = "Large";
            }
            SaveSettings();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TemLogo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ClickTime = DateTime.Now;
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void TemLogo_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DateTime.Now.Subtract(ClickTime).TotalSeconds < 0.15)
            {
                ToggleWindow();
            }
        }
    }
}
