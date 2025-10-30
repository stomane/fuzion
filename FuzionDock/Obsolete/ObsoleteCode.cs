using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Obsolete
{
    class ObsoleteCode
    {
        //public void PopulateGrid() //May add additional event handlers
        //{
        //    int i = 0;

        //    foreach (Game gameButton in gameObjects)
        //    {
        //        //if(mainGrid.Width > 1920) //scale max size of icons if dock is longer than screen
        //        //Button parameters
        //        //gameButton.Height = defaultIconSize; // it's defaultIconSize because that's the one i'm controlling via settings
        //        gameButton.Width = defaultIconSize;
        //        gameButton.Margin = new Thickness(1.5, 5, 1.5, 5);
        //        gameButton.Index = i; //redo indexes

        //        gameButton.Style = Resources["GameButtonStyleStatic"] as Style;
        //        RenderOptions.SetBitmapScalingMode(gameButton, BitmapScalingMode.HighQuality);

        //        //Set Context Menu
        //        gameButton.ContextMenu = Resources["GlobalContextMenu"] as ContextMenu;

        //        //Place in grid
        //        mainGrid.Children.Add(gameButton);
        //        Grid.SetColumn(gameButton, gameButton.Index);

        //        //Set image
        //        //gameButton.Source = ReloadGameIcon(gameButton.Index);

        //        //Alignment
        //        gameButton.HorizontalAlignment = HorizontalAlignment.Center;
        //        gameButton.VerticalAlignment = VerticalAlignment.Top;

        //        if (i == 0)
        //        {
        //            gameButton.Margin = new Thickness(20, 5, 1.5, 5);
        //        }

        //        if (i == gameObjects.Count - 1)
        //        {
        //            gameButton.Margin = new Thickness(1.5, 5, 20, 5);
        //        }

        //        i++;
        //    }

        //    UpdateLayout();
        //    CenterWindowOnScreen(fuzionPosition);
        //    UpdateSettings();
        //}

        #region Get Log of Install/Uninstall
        //        EventLog eLog = new EventLog();
        //        eLog.Log = "Application"; //MsiInstaller events are written in Application
        //eLog.EntryWritten += Log_NewInstallUninstallOccured; //Add the event and remove it when you want to stop listening
        //eLog.EnableRaisingEvents = true; // Enable event raising

        //private void Log_NewInstallUninstallOccured(object sender, EntryWrittenEventArgs e)
        //        {
        //            if (e.Entry.Source == "MsiInstaller") //MsiInstaller is the source responsible for installation related events
        //            {
        //                if (e.Entry.Message.Contains("Installation completed successfully."))
        //                {
        //                    Console.WriteLine("Installation Occured");
        //                }
        //                else if (e.Entry.Message.Contains("Removal completed successfully."))
        //                {
        //                    Console.WriteLine("Removal Occured");
        //                }
        //                else
        //                {
        //                    Console.WriteLine("Other Installation Event Occured");
        //                }
        //            }
        //        }
        #endregion
    }
}
