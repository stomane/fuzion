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
using System.Windows.Threading;
using static Fuzion.WindowsManager.OpenWindow;

namespace Fuzion.WindowsManager
{
    /// <summary>
    /// Interaction logic for FuzionToastWindow.xaml
    /// </summary>
    public partial class FuzionToastWindow : Window
    {
        public string MessageText { get; set; }
        public string MessageTitle { get; set; }
        public ImageSource ImageSrc { get; set; }
        DispatcherTimer HideTimer { get; set; }

        public FuzionToastWindow(string text, string title, double width = 210d, double height = 110d, ImageSource img = null, double hideInterval = 3d)
        {
            MessageText = text;
            Title = title;
            MessageTitle = title;
            ImageSrc = img;

            InitializeComponent();
            HideTimer = SetHideTimer(hideInterval);
            HideTimer.Start();
        }

        DispatcherTimer SetHideTimer(double hideInterval)
        {
            var res = new DispatcherTimer();
            res.Interval = TimeSpan.FromSeconds(hideInterval);
            res.Tick += HideTimer_Tick;
            return res;
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
