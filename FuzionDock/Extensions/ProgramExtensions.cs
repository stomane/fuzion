using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fuzion.Programs;
using Fuzion.Properties;
using static Fuzion.MainWindow;
using static Fuzion.Programs.Launch;

namespace Fuzion.Extensions
{
    static class ProgramExtensions
    {
        #region Adapters
        // Convert Program to Game
        public static Game ToGame(this Program program)
        {
            // Iterate through each property instead of coming back here every time
            //foreach (var propertyInfo in program.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            //{
            //    propertyInfo.SetValue()
            //}

            Game game = new Game
            {
                IsGame = program.IsGame,
                IsUserModified = program.IsUserModified,
                IsManuallyAdded = program.IsManuallyAdded,
                HasDownloadedIcon = program.HasDownloadedIcon,
                DisplayName = program.DisplayName,
                DockName = program.DockName,
                Path = program.Path,
                Arguments = program.Arguments,
                UninstallPath = program.UninstallPath,
                WorkDir = program.WorkDir,
                Icon = program.Icon,
                ChangedIcon = program.ChangedIcon,
                SystemIcon = program.SystemIcon,
                ExeName = program.ExeName,
                EpicAppName = program.EpicAppName,
                SteamAppID = program.SteamAppID,
                UplayAppID = program.UplayAppID,
                UWPAppID = program.UWPAppID,
                IconGUID = program.IconGUID,
                IconURI = program.IconURI,
                DatabaseReady = program.DatabaseReady,


                //Originals
                OriginalPath = program.OriginalPath,
                OriginalArguments = program.OriginalArguments,
                OriginalIcon = program.OriginalIcon,

                Index = program.Index,
                Launcher = program.Launcher,
                PathType = program.PathType,
                OriginalLauncher = program.OriginalLauncher,
                OriginalPathType = program.OriginalPathType
            };

            return game;
        }

        public static Program ToProgram(this Game game) 
        {
            Program prog = new Program
            {
                IsGame = game.IsGame,
                IsUserModified = game.IsUserModified,
                IsManuallyAdded = game.IsManuallyAdded,
                HasDownloadedIcon = game.HasDownloadedIcon,
                DisplayName = game.DisplayName,
                DockName = game.DockName,
                Path = game.Path,
                Arguments = game.Arguments,
                UninstallPath = game.UninstallPath,
                WorkDir = game.WorkDir,
                Icon = game.Icon,
                ChangedIcon = game.ChangedIcon,
                SystemIcon = game.SystemIcon,
                ExeName = game.ExeName,
                EpicAppName = game.EpicAppName,
                SteamAppID = game.SteamAppID,
                UplayAppID = game.UplayAppID,
                UWPAppID = game.UWPAppID,
                IconGUID = game.IconGUID,
                IconURI = game.IconURI,
                DatabaseReady = game.DatabaseReady,


                //Originals
                OriginalPath = game.OriginalPath,
                OriginalArguments = game.OriginalArguments,
                OriginalIcon = game.OriginalIcon,

                Index = game.Index,
                Launcher = game.Launcher,
                PathType = game.PathType,
                OriginalLauncher = game.OriginalLauncher,
                OriginalPathType = game.OriginalPathType

            };

            return prog;
        }
        #endregion

        public static bool IsProgram(this Program prog)
        {
            return LocalDatabase.IsProgram(prog);
        }

        public static void MoveTo(this Game target, double newX, double newY, int milliseconds, bool reset = false, bool instant = false)
        {
            TransformGroup tg = target.RenderTransform as TransformGroup;
            TranslateTransform trans = tg.Children[1] as TranslateTransform;

            if(!trans.IsFrozen && !trans.IsSealed)
            {
                if (instant)
                {
                    trans.X = newX;
                    trans.Y = newY;
                    return;
                }

                if (!reset)
                {
                    DoubleAnimation xAnimation = new DoubleAnimation(0, newX, TimeSpan.FromMilliseconds(milliseconds));
                    DoubleAnimation yAnimation = new DoubleAnimation(0, newY, TimeSpan.FromMilliseconds(milliseconds));
                    trans.BeginAnimation(TranslateTransform.XProperty, xAnimation);
                    trans.BeginAnimation(TranslateTransform.YProperty, yAnimation);
                }
                else
                {
                    trans.X = 0;
                    trans.Y = 0;
                }
            }
        }

        public static void MoveToAdditive(this Game target, double addX, double addY, int milliseconds, bool instant = false)
        {
            TransformGroup tg = target.RenderTransform as TransformGroup;
            TranslateTransform trans = tg.Children[1] as TranslateTransform;

            if (!trans.IsFrozen && !trans.IsSealed)
            {
                if (instant) //Just move
                {
                    trans.X += addX;
                    trans.Y += addY;
                }
                else //Animate
                {
                    DoubleAnimation anim1 = new DoubleAnimation(trans.X, trans.X + addX, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    //DoubleAnimation anim2 = new DoubleAnimation(0, newY, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    trans.BeginAnimation(TranslateTransform.XProperty, anim1);
                    //trans.BeginAnimation(TranslateTransform.YProperty, anim2);
                }
            }
        }


        public static void MoveToRelative(this Game target, double newX, double newY, int milliseconds, bool reset = false)
        {
           
            TransformGroup tg = target.RenderTransform as TransformGroup;
            TranslateTransform trans = tg.Children[1] as TranslateTransform;


            if (!trans.IsFrozen && !trans.IsSealed)
            {
                if (!reset)
                {
                    DoubleAnimation anim1 = new DoubleAnimation(trans.X, newX, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    //DoubleAnimation anim2 = new DoubleAnimation(0, newY, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    trans.BeginAnimation(TranslateTransform.XProperty, anim1);
                    //trans.BeginAnimation(TranslateTransform.YProperty, anim2);
                }
                else
                {
                    trans.X = 0;
                    //trans.Y = 0;
                }
            }
        }

        public static void MoveToRelativeSubtract(this Game target, double newX, double newY, int milliseconds, bool reset = false)
        {

            TransformGroup tg = target.RenderTransform as TransformGroup;
            TranslateTransform trans = tg.Children[1] as TranslateTransform;


            if (!trans.IsFrozen && !trans.IsSealed)
            {
                if (!reset)
                {
                    DoubleAnimation anim1 = new DoubleAnimation(trans.X, trans.X - newX, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    //DoubleAnimation anim2 = new DoubleAnimation(0, newY, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                    trans.BeginAnimation(TranslateTransform.XProperty, anim1);
                    //trans.BeginAnimation(TranslateTransform.YProperty, anim2);
                }
                else
                {
                    trans.X = 0;
                    //trans.Y = 0;
                }
            }
        }


        //public static bool IsBlacklisted(this Game target)
        //{
        //    if (Settings.Default.Blacklist.Contains(target.DisplayName))
        //    {
        //        return true;
        //    } else
        //    {
        //        return false;
        //    }
        //}
    }

}
