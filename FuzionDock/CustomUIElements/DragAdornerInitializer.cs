using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Fuzion.MainWindow;
using System.Windows;
using Fuzion.Properties;
using System.Windows.Documents;
using Fuzion.Native;
using System.Windows.Media.Imaging;
using System.IO;
using static Fuzion.Native.NativeMethods;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Fuzion.CustomUIElements
{

    public static class DragAdornerInitializer
    {
        public static DragAdorner DragAdorner { get; private set; }

        static Point adornerCenterOffset = new Point(-DragAdorner.TargetSize.Width / 2, -DragAdorner.TargetSize.Height / 2);
        static AdornerLayer adornerLayer;
        static bool adornerCreated;
        static Point mousePosition;
        static bool dragReallyLeft;

        static DispatcherTimer dragLeaveTimer = GetDragLeaveTimer();

        static DispatcherTimer GetDragLeaveTimer()
        {
            var t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(100);
            t.Tick += DragLeaveDispatcher_Tick;
            return t;
        }

        public static void RunAdorner(UIElement adornedElement, DragEventArgs e)
        {
            dragReallyLeft = false;
            Point outsideOfScreenPoint;

            if(Settings.Default.DockLocation <= 1)
            {
                outsideOfScreenPoint = new Point(-Settings.Default.StartupIconSize, 0d);
                mousePosition = AppWindow.PointFromScreen(GetMousePosPinvoke());
                mousePosition = new Point(mousePosition.X - Settings.Default.StartupIconSize / 2 - DragAdorner.InitialPos.X, DefaultGameMargins.Top);
            }
            else
            {
                outsideOfScreenPoint = new Point(0d, -Settings.Default.StartupIconSize);
                mousePosition = AppWindow.PointFromScreen(GetMousePosPinvoke());
                mousePosition = new Point(DefaultGameMargins.Left, mousePosition.Y - Settings.Default.StartupIconSize * 0.9d - DragAdorner.InitialPos.Y);
            }

            if (!adornerCreated && e != null)
            {
                DragAdorner.TargetSize = new Size(Settings.Default.StartupIconSize, Settings.Default.StartupIconSize);
                adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
                DragAdorner = new DragAdorner(adornedElement, GetShDragImage(e), outsideOfScreenPoint); //render outside of screen first so it doesn't flicker
                adornerLayer.Add(DragAdorner);
                adornerCreated = true;
            }

            //Console.WriteLine("Mouse POS "+mousePosition);

            DragAdorner.Arrange(new Rect(mousePosition, DragAdorner.TargetSize));
        }

        public static void ClearAdorner(bool instant = false)
        {
            AppWindow.DragBorder.Visibility = Visibility.Collapsed;
            checkDragExtensionOnce = false;

            if (instant)
            {
                RemoveAdorner();
                return;
            }

            dragReallyLeft = true;
            dragLeaveTimer.Start();
        }

        private static void DragLeaveDispatcher_Tick(object sender, EventArgs e)
        {
            if (!dragReallyLeft)
            {
                dragLeaveTimer.Stop();
                return;
            }

            RemoveAdorner();
        }

        static void RemoveAdorner()
        {
            if(br != null)
                br.Dispose();

            adornerCreated = false;

            if(DragAdorner != null)
                adornerLayer.Remove(DragAdorner);

            dragLeaveTimer.Stop();
        }

        static BinaryReader br;

        static BitmapSource GetShDragImage(DragEventArgs e)
        {
            MemoryStream imageStream = e.Data.GetData("DragImageBits") as MemoryStream;
            imageStream.Seek(0, SeekOrigin.Begin);
            br = new BinaryReader(imageStream);

            ShDragImage shDragImage;
            shDragImage.sizeDragImage.cx = br.ReadInt32();
            shDragImage.sizeDragImage.cy = br.ReadInt32();
            shDragImage.ptOffset.x = br.ReadInt32();
            shDragImage.ptOffset.y = br.ReadInt32();
            shDragImage.hbmpDragImage = new IntPtr(br.ReadInt32()); // I do not know what this is for!
            shDragImage.crColorKey = br.ReadInt32();
            int stride = shDragImage.sizeDragImage.cx * 4;
            var imageData = new byte[stride * shDragImage.sizeDragImage.cy];
            // We must read the image data as a loop, so it's in a flipped format
            for (int i = (shDragImage.sizeDragImage.cy - 1) * stride; i >= 0; i -= stride)
            {
                br.Read(imageData, i, stride);
            }
            var bitmapSource = BitmapSource.Create(shDragImage.sizeDragImage.cx, shDragImage.sizeDragImage.cy,
                                                        96, 96,
                                                        PixelFormats.Bgra32,
                                                        null,
                                                        imageData,
                                                        stride);

            return bitmapSource;

        }
    }
}
