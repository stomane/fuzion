using Fuzion.Programs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Fuzion.Icons.IconManager;
using Fuzion.WindowsManager;
using System.IO;
using System.Windows.Media.Animation;

namespace Fuzion
{
    public class ChangeGameInfoEventArgs : EventArgs
    {
        public ChangeGameInfoEventArgs(int index, string path, string arguments, string icon, bool isUserModded, PathType pathType, string dockName, BelongsToLauncher launcher)
        {
            Index = index;
            Path = path;
            Arguments = arguments;
            Icon = icon;
            IsUserModified = isUserModded;
            PathType = pathType;
            DockName = dockName;
            Launcher = launcher;
        }

        public int Index { get; }
        public string Path { get; }
        public string Arguments { get; }
        public string Icon { get; }
        public PathType PathType { get; }
        public bool IsUserModified { get; }
        public string DockName { get; }
        public BelongsToLauncher Launcher { get; }
    }

    /// <summary>
    /// Interaction logic for EditGameWindow.xaml
    /// </summary>
    public partial class EditGameWindow : Window
    {
        private Game editedGame;
        private bool loaded;

        private int editedIndex;
        private string newIconPath;
        private string newGamePath;
        private string newArguments;
        private PathType newPathType;
        private bool isUserModified;
        private string newDockName;
        private BelongsToLauncher newLauncher;

        private string selectedIconPath;
        private bool revertIconOnSave = false;

        public event EventHandler<ChangeGameInfoEventArgs> SendEditedInfoEvent;

        private Storyboard highlightWindowStoryboard;

        #region Blur - Too slow
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            // ...
            WCA_ACCENT_POLICY = 19
            // ...
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }
        internal void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);

            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData();
            data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;

            _ = SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
        #endregion

        public EditGameWindow()
        {
            InitializeComponent();

            // Arrange windows in grid - unfinished
            //var windowInitialPosition = WindowsManager.EditGameWindowsManager.GetNextWindowPosition(this);

            //Left = windowInitialPosition.X;
            //Top = windowInitialPosition.Y;

        }
        private void EditGameWindow_Loaded(object sender, RoutedEventArgs e) //Runs once
        {
            // Test area
            //onlineImage0.Source = ImageFromPath(@"http://www.americanlayout.com/wp/wp-content/uploads/2012/08/C-To-Go-300x300.png");

            // Hide online icon grid and initialize ui images list
            InitializeOnlineIcon();

            // Initialize online images list
            onlineImagesList = new List<Image>();

            for (int i = 0; i < onlineIconChooserGrid.Children.Count; i++)
            {
                try
                {
                    onlineImagesList.Add((Image)onlineIconChooserGrid.Children[i]);
                }
                catch (Exception)
                {

                }
            }

            //EnableBlur(); // To use blur I'd need a square design, no rounded edges

            // Reset selectedIconPath
            selectedIconPath = "";

            pathTypeSelector.ItemContainerStyle = (Style)TryFindResource("FuzionComboBoxItemGreen");
            pathTypeSelector.ItemsSource = new List<string> { "Path", "URI" };

            LauncherSelector.ItemContainerStyle = (Style)TryFindResource("FuzionComboBoxItemGreen");
            LauncherSelector.ItemsSource = new List<BelongsToLauncher>
            {
                BelongsToLauncher.Standalone,
                BelongsToLauncher.Steam,
                BelongsToLauncher.BattleNet,
                BelongsToLauncher.Epic,
                BelongsToLauncher.GOG,
                BelongsToLauncher.Uplay,
                BelongsToLauncher.Origin,
                BelongsToLauncher.UWP
            };

            loaded = true;
            SetPathArgumentsTextColorOnLoad();

            //Later - Load the current state of the game settings
            if (controlledCheckbox.IsChecked == true)
            {
                argumentsTextBox.Visibility = Visibility.Hidden;
                pathTextBox.Visibility = Visibility.Hidden;
                pathTypeSelector.Visibility = Visibility.Hidden;
                LauncherSelector.Visibility = Visibility.Hidden;
                selectPathButton.Visibility = Visibility.Hidden;
                isUserModified = false;
            }
            else
            {
                argumentsTextBox.Visibility = Visibility.Visible;
                pathTextBox.Visibility = Visibility.Visible;
                pathTypeSelector.Visibility = Visibility.Visible;
                LauncherSelector.Visibility = Visibility.Visible;
                isUserModified = true;
            }

            // Set highlight storyboard
            highlightWindowStoryboard = (Storyboard)TryFindResource("HighlightEditGameWindowBorder");
        }

        public void EditCurrentGame(Game game)
        {
            // Update Dock name if missing
            if (game?.DockName.Length == 0)
            {
                game.DockName = game.DisplayName;
            }

            editedGame = game;
            displayName.Text = game?.DockName;
            DockNameTextBox.Text = game.DockName;
            Title = game.DockName;

            //Set image to current game icon
            ChangeCardIcon(game.Icon);

            editedIndex = game.Index;
            newIconPath = game.Icon;
            newGamePath = game.Path;
            newArguments = game.Arguments;
            newPathType = game.PathType;
            newDockName = game.DockName;

            // Update Launcher
            newLauncher = game.Launcher;

            //Update game card
            pathTextBox.Text = game.Path;

            if (game.Arguments.Length != 0)
                argumentsTextBox.Text = game.Arguments;

            if (game.IsUserModified)
            {
                controlledCheckbox.IsChecked = false;
            }
            else
            {
                controlledCheckbox.IsChecked = true;
            }
        }

        private void Controlled_Checked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (loaded)
            {
                argumentsTextBox.Visibility = Visibility.Hidden;
                pathTextBox.Visibility = Visibility.Hidden;
                pathTypeSelector.Visibility = Visibility.Hidden;
                LauncherSelector.Visibility = Visibility.Hidden;
                selectPathButton.Visibility = Visibility.Hidden;
                isUserModified = false;
            }

        }

        private void Controlled_Unchecked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (loaded)
            {
                argumentsTextBox.Visibility = Visibility.Visible;
                pathTextBox.Visibility = Visibility.Visible;
                pathTypeSelector.Visibility = Visibility.Visible;
                LauncherSelector.Visibility = Visibility.Visible;
                pathTextBox.Text = newGamePath;


                if (pathTypeSelector.SelectedIndex == 0)
                    selectPathButton.Visibility = Visibility.Visible;

                isUserModified = true;
            }

        }

        private void ChangeIcon_Click(object sender, RoutedEventArgs e) //have to change quite a few things here
        {
            e.Handled = true;

            selectedIconPath = EditIcon();

            if (selectedIconPath != null) //New icon selected
            {
                string ext = System.IO.Path.GetExtension(selectedIconPath);

                if (ext == ".jpg" || ext == ".jpeg" || ext == ".jpe" || ext == ".bmp" || ext == ".png")
                {
                    ChangeCardIcon(selectedIconPath);

                }
                else if (ext == ".exe" || ext == ".ico")
                {
                    // Selected icon path needs to be changed because we extracted the icon whereas in the upper example it's directly loaded
                    ChangeCardIcon(PathToJumboIcon(selectedIconPath, editedGame.IconGUID));
                    selectedIconPath = Fuzion.MainWindow.DefaultAssetPath + @"temp\" + editedGame.IconGUID + ".png";
                }
                else
                {
                    OpenWindow.Notification(Properties.Resources.UnsupportedFormatMessage);
                }
            }
        }

        private void RevertIcon_Click(object sender, RoutedEventArgs e) //have to change quite a few things here
        {
            e.Handled = true;

            revertIconOnSave = true;
            ChangeCardIcon(Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + editedGame.IconGUID + ".png");
        }

        private void RevertSystemIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            revertIconOnSave = false;

            string jumboOrNoIcon = GetPathToJumboOrNoIcon(editedGame);
            selectedIconPath = jumboOrNoIcon;
            ChangeCardIcon(jumboOrNoIcon);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            //Close();
        }

        private void Window_Drag(object sender, MouseButtonEventArgs e)
        {
            if (CloseWindow.IsMouseOver == false && e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void PathTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (pathTypeSelector.SelectedIndex == 0)
            {
                pathTextBox.Text = Properties.Resources.PathPathType;
                pathTextBox.IsReadOnly = true;
                selectPathButton.Visibility = Visibility.Visible;
                newPathType = PathType.Path;
                //pathTextBox.MouseDown += SelectPath_MouseDown;
                //Console.WriteLine("Added listener");
            }
            else
            {
                pathTextBox.Text = Properties.Resources.URIPathType;
                pathTextBox.IsReadOnly = false;
                selectPathButton.Visibility = Visibility.Hidden;
                newPathType = PathType.URI;
                //pathTextBox.MouseDown -= SelectPath_MouseDown;
                //Console.WriteLine("Removed listener");
            }
        }

        private void SelectPath_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            OpenFileDialog fd = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Games (*.exe)|*.exe"
            };

            if (!string.IsNullOrEmpty(editedGame.WorkDir) && Directory.Exists(editedGame.WorkDir))
            {
                try
                {
                    if (!string.IsNullOrEmpty(Path.GetFullPath(editedGame.WorkDir)))
                        fd.InitialDirectory = editedGame.WorkDir;
                }
                catch (Exception)
                {

                }

            }

            bool? dialog = fd.ShowDialog();

            if (dialog == true)
            {
                newGamePath = fd.FileName;
                pathTextBox.Text = fd.FileName;
                Console.WriteLine("New path assigned: " + fd.FileName);
            }

        }

        public void TextBoxFocused(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;

            if (tb?.Text == "Path" || tb.Text == "URI" || tb.Text == "Arguments")
            {
                tb.Text = string.Empty;
            }
        }

        private void SaveButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Save the name
            DefaultReturnButton_Click(sender, null);

            // Has the user changed the icon?
            if (selectedIconPath != null && selectedIconPath.Length != 0)
            {
                if (Properties.Settings.Default.CropManuallyAddedIcons)
                {
                    if (IsImageFittingGrid(selectedIconPath))
                    {
                        Icons.BitmapTools.CropSave(selectedIconPath, editedGame.IconGUID);
                    }
                    else
                    {
                        ClipToCircleAndSave(selectedIconPath, Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + editedGame.IconGUID + ".png", AppDomain.CurrentDomain.BaseDirectory + @"Assets\iconframe.png");
                    }
                }
                else
                {
                    Icons.BitmapTools.CropSave(selectedIconPath, editedGame.IconGUID);
                }

                RefreshGameIcon(editedGame);
            }

            if (revertIconOnSave)
            {
                try
                {
                    File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + editedGame.IconGUID + ".png",
                    Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + editedGame.IconGUID + ".png", true);
                    RefreshGameIcon(editedGame);
                }
                catch (Exception)
                {

                }
            }

            if (isUserModified && argumentsTextBox.Text == "Arguments")
                newArguments = "";
            else if (isUserModified)
                newArguments = argumentsTextBox.Text;

            if (isUserModified && newGamePath == "Path" || newGamePath == "URI")
                newGamePath = @"C:\";
            else if (isUserModified)
                newGamePath = pathTextBox.Text;

            SendEditedInfoEvent(this, new ChangeGameInfoEventArgs(editedIndex, newGamePath, newArguments, newIconPath, isUserModified, newPathType, newDockName, newLauncher));
        }


        private void ChangeCardIcon(string path)
        {
            BitmapImage image = new BitmapImage();
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();

            changeIcon.Source = image;
        }

        private void PathTypeSelector_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ComboBox cBox = sender as ComboBox;

            if (newPathType == PathType.Path)
            {
                cBox.SelectedIndex = 0;
            }
            else
            {
                cBox.SelectedIndex = 1;
            }

            //if(newPathType == PathType.Path)
            //{
            //    pathTypeSelector.SelectedIndex = 0;
            //}
            //else
            //{
            //    pathTypeSelector.SelectedIndex = 1;
            //}
        }

        private static string EditIcon()
        {
            //Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fuzion Icons"));
            //string picturesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fuzion Icons");

            OpenFileDialog chooseIcon = new OpenFileDialog
            {
                Filter = "Images (*.jpg, *.jpeg, *.jpe, *.bmp, *.png, *.exe, *.ico) | *.jpg; *.jpeg; *.jpe; *.bmp; *.png; *.exe; *.ico",
                FilterIndex = 1,
                Multiselect = false,
                //InitialDirectory = picturesDir,
                Title = "Select game icon"
            };

            bool? result = chooseIcon.ShowDialog();

            if (result == true)
            {
                Console.WriteLine("New Icon selected: " + chooseIcon.FileName);
                return chooseIcon.FileName;
            }

            else { return null; }

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            WindowsManager.EditGameWindowsManager.RemoveWndRef(this);
            OpenWindowsManager.WindowReferenceControl($"{editedGame} Card", this, OpenWindowsManager.R.Remove);
        }

        private void CloseWindowOn_MiddleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Close();
            }
        }

        private void pathTypeSelector_DropDownOpened(object sender, EventArgs e)
        {
            ArgumentsTextBoxBlurEffect.Radius = 4;
            PathTextBoxBlurEffect.Radius = 4;
        }

        private void pathTypeSelector_DropDownClosed(object sender, EventArgs e)
        {
            ArgumentsTextBoxBlurEffect.Radius = 0;
            PathTextBoxBlurEffect.Radius = 0;
        }

        private void SetPathArgumentsTextColorOnLoad()
        {
            if (pathTextBox.Text == "Path")
            {
                pathTextBox.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
            else
            {
                pathTextBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            }

            if (argumentsTextBox.Text == "Arguments")
            {
                argumentsTextBox.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
            else
            {
                argumentsTextBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            }
        }

        private void pathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (pathTextBox.Text == "Path")
            {
                pathTextBox.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
            else
            {
                pathTextBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            }
        }

        private void argumentsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (argumentsTextBox.Text == "Arguments")
            {
                argumentsTextBox.Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }
            else
            {
                argumentsTextBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            }
        }

        private void displayName_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tBlock = sender as TextBlock;

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                displayName.Visibility = Visibility.Hidden;
                DockNameTextBox.Visibility = Visibility.Visible;
                DockNameTextBox.SelectAll();
                DockNameTextBox.Focus();
            }
        }

        private void DockNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            displayName.Text = DockNameTextBox.Text;
        }

        private void DefaultReturnButton_Click(object sender, RoutedEventArgs e)
        {
            // save changes to name
            if (DockNameTextBox.Text.Length != 0)
            {
                newDockName = DockNameTextBox.Text;

                displayName.Visibility = Visibility.Visible;
                DockNameTextBox.Visibility = Visibility.Hidden;
            }
            else
            {
                // revert changes to name
                displayName.Text = newDockName;
                DockNameTextBox.Text = newDockName;

                displayName.Visibility = Visibility.Visible;
                DockNameTextBox.Visibility = Visibility.Hidden;
            }
        }

        private void DefaultCancelButton_Click(object sender, RoutedEventArgs e)
        {
            // revert changes to name
            displayName.Text = newDockName;
            DockNameTextBox.Text = newDockName;

            displayName.Visibility = Visibility.Visible;
            DockNameTextBox.Visibility = Visibility.Hidden;
        }

        private void LauncherSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            newLauncher = (BelongsToLauncher)LauncherSelector.SelectedItem;
        }

        private void LauncherSelector_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            LauncherSelector.SelectedItem = newLauncher;
        }

        private void LauncherSelector_DropDownClosed(object sender, EventArgs e)
        {

        }

        private void LauncherSelector_DropDownOpened(object sender, EventArgs e)
        {

        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CloseWindow.IsMouseOver)
            {
                Close();
            }
        }

        private void InitializeOnlineIcon()
        {
            animatedBorder.VerticalAlignment = VerticalAlignment.Top;
            animatedBorder.Height = 180d;
            //animatedBorder.Margin = new Thickness(0d, 0d, 0d, 65d);
            onlineIconChooserGrid.Visibility = Visibility.Hidden;
            expandOnlineIconsAnimation = (Storyboard)TryFindResource("OnlineIconsExpandBorder");
            shrinkOnlineIconsAnimation = (Storyboard)TryFindResource("OnlineIconsShrinkBorder");
        }

        bool toggleOnlineBorder; // goes to false when initialized in loaded event
        List<Image> onlineImagesList; // set in loaded event
        Storyboard expandOnlineIconsAnimation;
        Storyboard shrinkOnlineIconsAnimation;

        // Make it async so it doesn't block the UI thread
        // REENABLE FOR ONLINE ICONS
        //private void ShowOnlineIcons_Click(object sender, MouseButtonEventArgs e)
        //{
        //    // RE ENABLE THE WHOLE THING FOR ONLINE ICONS
        //    toggleOnlineBorder = !toggleOnlineBorder;

        //    if (toggleOnlineBorder) // Show online image grid
        //    {
        //        animatedBorder.BeginStoryboard(expandOnlineIconsAnimation);
        //        //animatedBorder.Height = 240d;
        //        //animatedBorder.Margin = new Thickness(0d);
        //        onlineIconChooserGrid.Visibility = Visibility.Visible;

        //        try
        //        {
        //            var iconLinksList = GetIconLinksForGame(displayName.Text, onlineIconChooserGrid.ColumnDefinitions.Count);

        //            if (iconLinksList.Count == 0)
        //                return;

        //            for (int i = 0; i < iconLinksList.Count; i++)
        //            {
        //                onlineImagesList[i].Source = Icons.BitmapTools.ImageFromPath(iconLinksList[i], true);
        //                //HideLoadingRectangle(i);
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }

        //    }
        //    else // Hide online image grid
        //    {
        //        animatedBorder.BeginStoryboard(shrinkOnlineIconsAnimation);
        //        //animatedBorder.Height = 180d;
        //        //animatedBorder.Margin = new Thickness(0d, 0d, 0d, 65d);
        //        onlineIconChooserGrid.Visibility = Visibility.Hidden;
        //    }
        //}

        private void SetFromOnlineImage_Click(object sender, MouseButtonEventArgs e)
        {
            //Console.WriteLine("Set from online image source: " + ((Image)sender).Source);

            try
            {
                string iconPath = DownloadIconFromLinkToTempFolder(((Image)sender).Source.ToString(System.Globalization.CultureInfo.InvariantCulture), "onlineicons");
                ChangeCardIcon(iconPath);
                selectedIconPath = iconPath;
                revertIconOnSave = false;
            }
            catch (Exception)
            {
                OpenWindow.Notification(Properties.Resources.UnsupportedFormatMessage);
            }
        }

        private void HideLoadingRectangle(int index)
        {
            //int rectIndex = imageName[imageName.Length - 1];
            Console.WriteLine("rectIndex = " + index);

            switch (index)
            {
                case 0:
                    Rect0.Visibility = Visibility.Hidden;
                    break;
                case 1:
                    Rect1.Visibility = Visibility.Hidden;
                    break;
                case 2:
                    Rect2.Visibility = Visibility.Hidden;
                    break;
                case 3:
                    Rect3.Visibility = Visibility.Hidden;
                    break;
                case 4:
                    Rect4.Visibility = Visibility.Hidden;
                    break;
                default:
                    break;
            }
        }

        private void OnlineImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            string name = ((Image)sender).Name;
            Console.WriteLine(name + " size changed");
            var i = Convert.ToInt32(char.GetNumericValue(name[name.Length - 1]));

            HideLoadingRectangle(i);
        }

        public void AnimateWindowHighlight()
        {
            HighlightWindowBorder.BeginStoryboard(highlightWindowStoryboard);
        }
    }
}
