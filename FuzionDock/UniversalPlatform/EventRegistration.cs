using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Fuzion.UniversalPlatform
{
    class EventRegistration
    {
        public static void RegisterPointerEvents(System.Windows.Controls.ScrollViewer scv)
        {
            Border b = new Border();
            b.Width = scv.ActualWidth;

            b.PointerEntered += Elmnt_PointerEntered;
        }

        private static void Elmnt_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Console.WriteLine("Pointer entered event, activating");
        }
    }
}
