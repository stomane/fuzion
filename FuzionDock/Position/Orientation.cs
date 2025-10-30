using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fuzion.Properties;
using static Fuzion.MainWindow;

namespace Fuzion.Position
{
    public static class Orientation
    {
        public static double GetCalculatedWindowWidth()
        {
            double result = 0;

            if (Settings.Default.ShowChatBar)
            {
                double chatMargins = DefaultChatBarThickness.Left + DefaultChatBarThickness.Right;
                result = result + AppWindow.ChatGrid.ActualWidth + chatMargins; // width + margin
            }

            double gameMargins = DefaultGameMargins.Left + DefaultGameMargins.Right;
            result = result + Settings.Default.StartupIconSize + gameMargins + AppWindow.LoadingRectangle.ActualWidth + searchResultsGridMaxWidth;
            Console.WriteLine("Calculated window width is " + result);
            return result;
        }

        public static double GetCalculatedWindowHeight()
        {
            double result = 0;

            if (Settings.Default.ShowChatBar)
            {
                result = result + 25 + 8; // height + margin
            }
            else
            {
                // + searchBar
                result = result + 20;
            }

            // iconSize + iconMargins + loadingRect + searchResultsRow (5 of them)
            result = result + Settings.Default.StartupIconSize + 10 + 10 + searchResultRowHeight * 5;
            Console.WriteLine("Calculated window height is " + result);
            return result;
        }
    }
}
