using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Fuzion.Icons
{
    static class BitmapTools
    {
        public enum IconSaveDestination { Default, Changed }
        public enum CropType { Crop, Resize, CropResize, CropFrame }
        public const int DefaultImageSize = 256;

        public static BitmapImage ImageFromPath(string path, bool cache = false,
            bool freeze = false,
            BitmapCacheOption cacheOptions = BitmapCacheOption.OnLoad,
            BitmapCreateOptions createOptions = BitmapCreateOptions.IgnoreImageCache)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            // Don't cache in memory
            if (cache == false)
            {
                image.CacheOption = cacheOptions;
                image.CreateOptions = createOptions;
            }
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            if (freeze)
            {
                image.Freeze();
            }
            return image;
        }

        public static BitmapImage CroppedImageFromPath(string path, string iconGuid)
        {
            CropThis(path, iconGuid);

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            return image;
        }

        public static void CropThis(string pathToImageToCrop, string guidFolder)
        {
            string tempFolder = Path.Combine(MainWindow.DefaultAssetPath, "cropped", "cropthis", guidFolder);

            Directory.CreateDirectory(tempFolder);
            Bitmap originalBitmap = Bitmap.FromFile(pathToImageToCrop) as Bitmap;

            // Find the min/max non-white/transparent pixels
            Point min = new Point(int.MaxValue, int.MaxValue);
            Point max = new Point(int.MinValue, int.MinValue);

            for (int x = 0; x < originalBitmap.Width; ++x)
            {
                for (int y = 0; y < originalBitmap.Height; ++y)
                {
                    System.Drawing.Color pixelColor = originalBitmap.GetPixel(x, y);
                    //if (!(pixelColor.R == 255 && pixelColor.G == 255 && pixelColor.B == 255)
                    //  || pixelColor.A < 255)
                    if (pixelColor.A > 60) //yields best results - needs more experimenting
                    {
                        if (x < min.X) min.X = x;
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }
                }
            }

            //Console.WriteLine("Min X = " + min.X);
            //Console.WriteLine("Max X = " + max.X);
            //Console.WriteLine("Min Y = " + min.Y);
            //Console.WriteLine("Max Y = " + max.Y);

            // Create a new bitmap from the crop rectangle
            System.Drawing.Rectangle srcRectangle = new System.Drawing.Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            System.Drawing.Rectangle destRectangle = new System.Drawing.Rectangle(0, 0, max.X - min.X, max.Y - min.Y);
            Bitmap newBitmap = new Bitmap(srcRectangle.Width, srcRectangle.Height);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(originalBitmap, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                g.Dispose();
            }
            newBitmap.Save(Path.Combine(tempFolder, Path.GetFileName(pathToImageToCrop)));
            originalBitmap.Dispose();
            newBitmap.Dispose();


            // Replace
            File.Copy(Path.Combine(tempFolder, Path.GetFileName(pathToImageToCrop)), pathToImageToCrop, true);
            //File.Delete(Fuzion.MainWindow.DefaultAssetPath + @"cropped\cropthis\" + guidFolder + Path.GetFileName(pathToImageToCrop));
        }

        /// <summary>
        /// Crops all transparency and resizes the image uniformly if it has a bigger width than maxWidth
        /// </summary>
        /// <param name="originalBitmap"></param>
        /// <param name="minXY"></param>
        /// <param name="maxXY"></param>
        /// <param name="maxWidth"></param>
        /// <returns></returns>
        public static Bitmap Crop(Bitmap originalBitmap, Point minXY, Point maxXY, int maxWidth)
        {
            if(originalBitmap.Width > maxWidth) // scale uniform if bigger than maxwidth
            {
                var srcRectangle = new Rectangle(minXY.X, minXY.Y, maxXY.X - minXY.X, maxXY.Y - minXY.Y);
                var croppedSize = new Point(maxXY.X - minXY.X, maxXY.Y - minXY.Y);
                double scaleMultiplier = (double)maxWidth / (double)croppedSize.X;
                var scaledBitmapSize = new Point(Convert.ToInt32(croppedSize.X * scaleMultiplier), Convert.ToInt32(croppedSize.Y * scaleMultiplier));
                //var bitmapOffsets = new Point((uniformSize - scaledBitmapSize.X) / 2, (uniformSize - scaledBitmapSize.Y) / 2);
                var destRectangle = new Rectangle(0,
                                                                0,
                                                                scaledBitmapSize.X,
                                                                scaledBitmapSize.Y);
                var newBitmap = new Bitmap(scaledBitmapSize.X, scaledBitmapSize.Y);
                using (Graphics g = Graphics.FromImage(newBitmap))
                {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    g.DrawImage(originalBitmap, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                    g.Dispose();
                }

                return newBitmap;
            }
            else // just crop because smaller or equal maxwidth
            {
                var srcRectangle = new Rectangle(minXY.X, minXY.Y, maxXY.X - minXY.X, maxXY.Y - minXY.Y);
                var destRectangle = new Rectangle(0, 0, srcRectangle.Width, srcRectangle.Height);
                var newBitmap = new Bitmap(srcRectangle.Width, srcRectangle.Height);

                using (Graphics g = Graphics.FromImage(newBitmap))
                {
                    g.DrawImage(originalBitmap, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                    g.Dispose();
                }

                return newBitmap;
            }
          
        }

        public static Bitmap Crop(Bitmap originalBitmap, byte transparencyThreshold = 60)
        {
            // Find the min/max non-white/transparent pixels
            var min = new Point(int.MaxValue, int.MaxValue);
            var max = new Point(int.MinValue, int.MinValue);

            for (int x = 0; x < originalBitmap.Width; ++x)
            {
                for (int y = 0; y < originalBitmap.Height; ++y)
                {
                    Color pixelColor = originalBitmap.GetPixel(x, y);

                    if (pixelColor.A > transparencyThreshold) //yields best results - needs more experimenting
                    {
                        if (x < min.X) min.X = x;
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }
                }
            }

            var srcRectangle = new Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            var destRectangle = new Rectangle(0, 0, srcRectangle.Width, srcRectangle.Height);
            var newBitmap = new Bitmap(srcRectangle.Width, srcRectangle.Height);

            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(originalBitmap, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                g.Dispose();
            }

            return newBitmap;
        }

        public static Bitmap Crop(Bitmap originalBitmap, int uniformSize, byte transparencyThreshold = 60)
        {
            // Find the min/max non-white/transparent pixels
            var min = new Point(int.MaxValue, int.MaxValue);
            var max = new Point(int.MinValue, int.MinValue);

            for (int x = 0; x < originalBitmap.Width; ++x)
            {
                for (int y = 0; y < originalBitmap.Height; ++y)
                {
                    Color pixelColor = originalBitmap.GetPixel(x, y);

                    if (pixelColor.A > transparencyThreshold) //yields best results - needs more experimenting
                    {
                        if (x < min.X) min.X = x;
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }
                }
            }

            var srcRectangle = new Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            var croppedSize = new Point(max.X - min.X, max.Y - min.Y);
            double scaleMultiplier = (double)uniformSize / (double)(croppedSize.X >= croppedSize.Y ? croppedSize.X : croppedSize.Y);
            var scaledBitmapSize = new Point((int)(croppedSize.X * scaleMultiplier), (int)(croppedSize.Y * scaleMultiplier));
            var bitmapOffsets = new Point((uniformSize - scaledBitmapSize.X) / 2, (uniformSize - scaledBitmapSize.Y) / 2);
            var destRectangle = new Rectangle(bitmapOffsets.X,
                                                            bitmapOffsets.Y,
                                                            scaledBitmapSize.X,
                                                            scaledBitmapSize.Y);
            var newBitmap = new Bitmap(uniformSize, uniformSize);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(originalBitmap, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                g.Dispose();
            }

            return newBitmap;
        }

        public static void CropSave(Bitmap bmp, string guid, IconSaveDestination isd = IconSaveDestination.Default)
        {
            Bitmap newBmp = Crop(bmp);
            SaveBitmapToFuzionFolder(newBmp, guid, isd);
            newBmp.Dispose();
        }

        public static void CropSave(Bitmap bmp, string guid, int uniformSize = DefaultImageSize, IconSaveDestination isd = IconSaveDestination.Default)
        {
            Bitmap newBmp = Crop(bmp, uniformSize);
            SaveBitmapToFuzionFolder(newBmp, guid, isd);
            newBmp.Dispose();
        }

        public static void CropSave(string path, string guid, int uniformSize = DefaultImageSize, IconSaveDestination isd = IconSaveDestination.Default)
        {
            Bitmap bmp = new Bitmap(Image.FromFile(path));
            Bitmap newBmp = Crop(bmp, uniformSize);
            SaveBitmapToFuzionFolder(newBmp, guid, isd);
            newBmp.Dispose();
            bmp.Dispose();
        }

        //public static void CropSave(string pathToImageToCrop, string guidName, IconSaveDestination cropOutput = IconSaveDestination.Default, bool saveOnly = false)
        //{
        //    if (saveOnly == false)
        //    {
        //        Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"cropped\");

        //        // Bitmap originalBitmap = Bitmap.FromFile(pathToImageToCrop);
        //        Bitmap originalBitmap = new Bitmap(Image.FromFile(pathToImageToCrop));

        //        var newBitmap = BitmapTools.Crop(originalBitmap, 256);

        //        newBitmap.Save(Fuzion.MainWindow.DefaultAssetPath + @"cropped\" + guidName + ".png");
        //        originalBitmap.Dispose();
        //        newBitmap.Dispose();

        //        if (cropOutput == IconSaveDestination.Default)
        //        {
        //            File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"cropped\" + guidName + ".png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + guidName + ".png", true);
        //        }

        //        if (cropOutput == IconSaveDestination.Changed)
        //        {
        //            File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"cropped\" + guidName + ".png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + guidName + ".png", true);
        //        }
        //    }
        //    else //save only
        //    {
        //        if (cropOutput == IconSaveDestination.Default)
        //        {
        //            File.Copy(pathToImageToCrop, Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + guidName + ".png", true);
        //        }

        //        if (cropOutput == IconSaveDestination.Changed)
        //        {
        //            File.Copy(pathToImageToCrop, Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + guidName + ".png", true);
        //        }
        //    }
        //}

        static void SaveBitmapToFuzionFolder(Bitmap bmp, string guid, IconSaveDestination isd = IconSaveDestination.Default)
        {
            Directory.CreateDirectory(MainWindow.DefaultAssetPath + @"cropped\");

            string tempPath = MainWindow.DefaultAssetPath + @"cropped\" + guid + ".png";

            bmp.Save(tempPath);

            if (isd == IconSaveDestination.Default)
            {
                File.Copy(tempPath, MainWindow.DefaultAssetPath + @"Icons\" + guid + ".png", true);
            }

            if (isd == IconSaveDestination.Changed)
            {
                File.Copy(tempPath, MainWindow.DefaultAssetPath + @"Icons\changed\" + guid + ".png", true);
            }
        }

        public static void MoveIconToFuzionFolder(string imagePath, string guid, IconSaveDestination isd = IconSaveDestination.Default)
        {
            if (isd == IconSaveDestination.Default)
            {
                File.Copy(imagePath, MainWindow.DefaultAssetPath + @"Icons\" + guid + ".png", true);
            }

            if (isd == IconSaveDestination.Changed)
            {
                File.Copy(imagePath, MainWindow.DefaultAssetPath + @"Icons\changed\" + guid + ".png", true);
            }
        }
    }
}
