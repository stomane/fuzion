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

namespace Fuzion
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {
        public DebugWindow()
        {
            InitializeComponent();
        }

        private void DebugTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox text = sender as TextBox;
            text.ScrollToEnd();
        }

        static readonly List<string> listCommands = new List<string>()
        {
            "games",
            "programs",
            "gridrows",
            "gridcolumns"
        };

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox text = sender as TextBox;

            if (e.Key == Key.Enter)
            {
                DebugTextBox.AppendText("\n" + "User: " + text.Text);
                CheckUserCommand(text.Text);
                text.Clear();
            }
        }

        private void CheckUserCommand(string cmd)
        {
            string[] cmds = cmd.Split();

            if(cmds.Length > 1 && cmds[0] == "list" && listCommands.Contains(cmds[1]))
            {
                DebugTextBox.AppendText($"\nListing {cmds[1]}");
            } else
            {
                DebugTextBox.AppendText($"\nResponse: Unknown command");
            }
        }
    }
}
