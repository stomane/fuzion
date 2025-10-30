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
using static Fuzion.WindowsManager.OpenWindow;

namespace Fuzion.WindowsManager
{
    /// <summary>
    /// Interaction logic for FuzionNotificationWindow.xaml
    /// </summary>
    public partial class FuzionNotificationWindow : Window
    {
        public string MessageText { get; set; }
        public string MessageTitle { get; set; }
        public bool IsCancelVisible { get; set; }
        public bool IsRemoveGame { get; set; }
        public bool IsBlacklistChecked { get; set; }
        public string AcceptButtonText { get; set; } = "Ok";
        public string CancelButtonText { get; set; } = "Cancel";
        public NotificationResult NotificationResultStatus { get; private set; }

        public FuzionNotificationWindow(string text, string title, NotificationWindowType nwt = NotificationWindowType.Ok)
        {
            MessageText = text;
            Title = title;
            MessageTitle = title;

            if (nwt == NotificationWindowType.OkCancel)
                IsCancelVisible = true;

            if (nwt == NotificationWindowType.YesNo)
            {
                IsCancelVisible = true;
                AcceptButtonText = "Yes";
                CancelButtonText = "No";
            }

            InitializeComponent();

            if (nwt == NotificationWindowType.RemoveGame)
            {
                IsRemoveGame = true;
                BlacklistCheckbox.Visibility = Visibility.Visible;
                BlacklistLabel.Visibility = Visibility.Visible;
                AcceptButtonText = "Remove";
            }
        }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CloseWindow.IsMouseOver)
            {
                NotificationResultStatus = NotificationResult.No;
                Close();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoveGame && IsBlacklistChecked)
            {
                NotificationResultStatus = NotificationResult.YesBlacklist;
            }
            else
            {
                NotificationResultStatus = NotificationResult.Yes;
            }

            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationResultStatus = NotificationResult.No;
            Close();
        }

        private void Window_Drag(object sender, MouseButtonEventArgs e)
        {
            if (CloseWindow.IsMouseOver == false && e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
