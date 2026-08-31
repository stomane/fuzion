using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Icons;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fuzion.Programs;
using Fuzion.Extensions;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Cleanup.RemoveObsolete;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Fuzion.Icons.BitmapTools;
using static Fuzion.Icons.IconManager;

namespace Fuzion.Icons
{
    class IconCollection
    {
        public List<ScannedImageData> ScannedImageDataList { get; set; } = new List<ScannedImageData>();
        public List<string> SourceLinks { get; set; } = new List<string>();
        public List<string> Paths { get; set; } = new List<string>();
        public List<int> Scores { get; set; } = new List<int>();
    }

    public static class IconManager
    {
        /// <summary>
        /// Update the icon for a game currently in the grid
        /// </summary>
        /// <param name="game">The game in the grid which needs to refresh its icon</param>
        public static void RefreshGameIcon(Game game)
        {
            try
            {
                game?.BeginStoryboard(game.FadeOutInStoryboard);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.UriSource = new Uri(game?.Icon, UriKind.Absolute);
                image.EndInit();
                game.Source = image;
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// Main method to be used with Image.Source for games which are going in the grid
        /// </summary>
        /// <param name="game">The game which you're loading the icon for</param>
        /// <returns></returns>
        public static BitmapImage LoadGameIcon(Game game) // Used for loading the path into UI Image
        {
            try
            {
                return ImageFromGUID(game?.IconGUID);
            }
            catch (Exception)
            {
                if (!string.IsNullOrEmpty(game?.SystemIcon)) //is there a system icon
                {
                    try
                    {
                        return JumboIcon(game);
                    }
                    catch (Exception)
                    {
                        return NoIcon(game);
                    }
                }
                else
                {
                    return NoIcon(game);
                }
            }
        }



        private static BitmapImage NoIcon(Game game)
        {
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + @"Assets\noicon.png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + game.IconGUID + ".png", true);
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + @"Assets\noicon.png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + game.IconGUID + ".png", true);
            return ImageFromGUID(game.IconGUID);
        }

        private static BitmapImage NoIcon(Program prog)
        {
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + @"Assets\noicon.png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + prog.IconGUID + ".png", true);
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + @"Assets\noicon.png", Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + prog.IconGUID + ".png", true);
            return ImageFromGUID(prog.IconGUID);
        }

        // This will scan an image directly from URL speeding up the icon finder substantially - needs to be finished
        public static void ScanImageFromURL(string url)
        {
            //string urlGetImage = "http:yoururlgoeshere.jpg";

            var c = new WebClient();
            var bytes = c.DownloadData(url);
            var ms = new MemoryStream(bytes);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.EndInit();

            //System.Threading.Thread.Sleep(10000);

            var ovalness = ImageOvalness(bi);
            Console.WriteLine("Loaded img width: " + bi.Width);
            Console.WriteLine("Loaded img height: " + bi.Height);
            Console.WriteLine("Ovalness from URL: " + ovalness);

            c.Dispose();
        }

        //static async Task<List<Bitmap>> ImageURLToMemoryStream()
        //{
        //    var images = new List<Bitmap>();
        //    using (var client = new HttpClient())
        //    {
        //        var response = await client.GetAsync("http://i.imgur.com/BsPzIfs.png");
        //        var bitmap = new Bitmap();
        //        if (response != null && response.StatusCode == HttpStatusCode.OK)
        //        {
        //            using (var stream = await response.Content.ReadAsStreamAsync())
        //            {
        //                var memStream = new MemoryStream();
        //                await stream.CopyToAsync(memStream);
        //                memStream.Position = 0;
        //                images.Add(new Bitmap(memStream));
        //            }
        //        }
        //    }
        //    return images;
        //}

        private class ImageSearchResult
        {
            public LinkItem[] items { get; set; }
        }

        private class LinkItem
        {
            public string link { get; set; }
        }

        // Used in Program.FetchIcon()
        public static async Task DownloadIcon(Program program, bool disregardFuzionDB = false) // get best icon main function
        {
            MainWindow.AnimateLoadingRectangle(true, "geticon" + program?.IconGUID);

            #region Tests
            //// Site restricted uri mask
            ////https://www.googleapis.com/customsearch/v1/siterestrict?[parameters]

            //// Limited quota general
            ////Uri rUri = new Uri("https://www.googleapis.com/customsearch/v1?fields=items/link&key=&st=y&tbm=isch&epq=&oq=&eq=&cr=&sitesearch=https://www.deviantart.com/&tbs=ic:trans,iar:s&searchType=image&num=3&q="+gameName+" game icon"); //filetype:png+ // was just before gameName in the query

            //// Removed
            ////&sitesearch=https://www.deviantart.com/
            ////Uri rUri = new Uri("https://www.googleapis.com/customsearch/v1/siterestrict?fields=items/link&key=&st=y&tbm=isch&epq=&oq=&eq=&cr=&tbs=ic:trans,iar:s&searchType=image&num=10&q=" + gameName+" icon"); //filetype:png+ // was just before gameName in the query
            //Uri guri = new Uri("https://www.googleapis.com/customsearch/v1/siterestrict?fields=items/link&key=&st=y&tbm=isch&epq=&oq=&eq=&cr=&tbs=ic:trans,iar:s&searchType=image&num=" + Properties.Settings.Default.IconsPerGame + "&q=" + program.DisplayName + " icon"); //filetype:png+ // was just before gameName in the query
            //                                                                                                                                                                                                                                                                                                                                             //as_st=y&tbm=isch&as_q=hollow+knight+icon&as_epq=&as_oq=&as_eq=&cr=&as_sitesearch=https://www.deviantart.com/&safe=images&tbs=ic:trans,iar:s

            //WebRequest webRequest = WebRequest.Create(guri);
            //Stream strm = webRequest.GetResponse().GetResponseStream();

            //StreamReader strmReader = new StreamReader(strm);
            //string jsonData = strmReader.ReadToEnd();
            //Console.WriteLine(jsonData);

            //var searchRes = JsonConvert.DeserializeObject<ImageSearchResult>(jsonData);
            //foreach (var res in searchRes.items)
            //{
            //    //Console.WriteLine("{0} => {1}\n", res.title, res.url);
            //    Console.WriteLine("JSON link: " + res.link);
            //}

            //throw new Exception();
            #endregion

            Tuple<string, int> fuzionDBData = new Tuple<string, int>(string.Empty, 100);

            if (disregardFuzionDB == false)
            {
                try
                {
                    // Check if it has an icon in fuzion db first and if iconrelevance is good
                    fuzionDBData = await SQL.DbConnection.GetIconDataTupleAsync(program.DisplayName).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    _ = DownloadIcon(program, true);
                    return;
                }
            }

            if (string.IsNullOrEmpty(fuzionDBData.Item1) || fuzionDBData.Item2 < Properties.Settings.Default.IconsPerGame)
            {
                Console.WriteLine("No icon found in Fuzion DB, downloading");
                string gameName = program?.DisplayName.ToLowerNormalized();

                IconCollection iconCollection = new IconCollection();

                string folderName = program.IconGUID;

                try
                {
                    // Download icons
                    var iconLinks = await FetchIconLinksAsync(program).ConfigureAwait(false);

                    int fileNameIndex = 0;

                    for (int i = 0; i < iconLinks.Length; i++)
                    {
                        DownloadTemporaryIcon(iconLinks[i], folderName, fileNameIndex, iconCollection);
                        fileNameIndex++;
                    }

                    //await Task.Run(() => Parallel.For(0, iconLinks.Length, (i) =>
                    //{
                    //    // fills iconCollection with paths and sourcelinks
                    //    DownloadTemporaryIcon(iconLinks[i], folderName, fileNameIndex, iconCollection);
                    //    fileNameIndex++;
                    //})).ConfigureAwait(false);

                    //await Task.Run(() => Parallel.ForEach(iconLinks, (iconLink, state, index) =>
                    //{
                    //    // fills iconCollection with paths and sourcelinks
                    //    //int i = Convert.ToInt32(index);
                    //    DownloadTemporaryIcon(iconLink, folderName, fileNameIndex, iconCollection);
                    //    fileNameIndex++;
                    //})).ConfigureAwait(false);

                    //// Scan them parallel - was causing issues and mixing up scores, needs to be synced properly
                    //await Task.Run(() => Parallel.ForEach(iconCollection.Paths, (path, state, index) =>
                    //{
                    //    iconCollection.ScannedImageDataList.Add(GetScannedImageData(path));

                    //})).ConfigureAwait(false);

                    await Task.Run(() =>
                    {
                        for (int i = 0; i < iconCollection.Paths.Count; i++)
                        {
                            iconCollection.ScannedImageDataList.Add(GetScannedImageData(iconCollection.Paths[i]));
                        }
                        

                    }).ConfigureAwait(false);


                    //// Precache Online Icon Links --  needs review
                    //var precachedOnlineIconLinks = new List<string>();

                    //for (int i = 0; i < iconLinks.Length; i++)
                    //{
                    //    precachedOnlineIconLinks.Add(iconLinks[i]);
                    //}

                    //// Pre Cache Online icons for games which came from the scan
                    //Serialization.SerializeOnlineIconList(program.DisplayName.ToLowerNormalized(), precachedOnlineIconLinks);

                    Console.WriteLine("GETTING BEST ICON FOR " + gameName);

                    await SetBestIcon(iconCollection, program).ConfigureAwait(false);

                    // Copy the newfound icon to replace Default icon only if old icon was noicon - needs to be implemented
                    // Copy to /changed dir so it can be used as revert icon
                    // Moved to prepareiconforgrid
                    //if (File.Exists(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png"))
                    //{
                    //    File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png",
                    //                     Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + program.IconGUID + ".png", true);
                    //}

                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                        RefreshGameIcon(GameObjects.Find(game => game.IconGUID == program.IconGUID))));
                }
                catch (Exception)
                {
                    Console.WriteLine("Exception occured while trying to download icons from the web");
                    //Console.WriteLine("Could not FetchIcon: " + wex.Message);
                    string jumboOrNo = GetPathToJumboOrNoIcon(program);
                    PrepareIconForGrid(jumboOrNo, 1d, program);
                }
            }
            else // Fuzion database contains an icon
            {
                Console.WriteLine("Getting icon from Fuzion DB, link: "+fuzionDBData.Item1);
                GetSpecificIcon(program, fuzionDBData.Item1);
            }

            // Indicate icon fetch is complete for db ready bool
            //Console.WriteLine("Icon Fetch Complete, prog icon: " + program.IconURI);
            //Console.WriteLine("Icon Fetch Complete, game icon: " + GameObjects.Find(game => game.IconGUID == program.IconGUID).IconURI);
            //Console.WriteLine("Icon Fetch Complete, game icon: " + RecentlyAddedGames.Find(game => game.IconGUID == program.IconGUID).IconURI);
            program.IconFetchComplete = true;

            MainWindow.AnimateLoadingRectangle(false, "geticon" + program?.IconGUID);
        }

        public static async Task<string[]> FetchIconLinksAsync(Program program)
        {
            if (!Constants.HasGoogleSearchAccess)
            {
                return Array.Empty<string>();
            }

            string gameName = program?.DisplayName.ToLowerNormalized();
            string requestUrl = Constants.BuildGoogleImageSearchUrl(gameName + " icon", Properties.Settings.Default.IconsPerGame, false);

            using (var httpClient = new HttpClient())
            {
                try
                {
                    string jsonData = await httpClient.GetStringAsync(requestUrl).ConfigureAwait(false);
                    ImageSearchResult searchRes = JsonConvert.DeserializeObject<ImageSearchResult>(jsonData);

                    if (searchRes == null || searchRes.items == null)
                    {
                        return Array.Empty<string>();
                    }

                    // Result
                    var result = new string[searchRes.items.Length];
                    for (int i = 0; i < searchRes.items.Length; i++)
                    {
                        result[i] = searchRes.items[i].link;
                    }

                    return result;
                }
                catch (HttpRequestException)
                {
                    return Array.Empty<string>();
                }
            }
        }

        public static async Task<string[]> FetchIconLinksAsync(string name)
        {
            if (!Constants.HasGoogleSearchAccess)
            {
                return Array.Empty<string>();
            }

            string gameName = name.ToLowerNormalized();
            string requestUrl = Constants.BuildGoogleImageSearchUrl(gameName + " icon", Properties.Settings.Default.IconsPerGame, true);

            using (var httpClient = new HttpClient())
            {
                try
                {
                    string jsonData = await httpClient.GetStringAsync(requestUrl).ConfigureAwait(false);
                    ImageSearchResult searchRes = JsonConvert.DeserializeObject<ImageSearchResult>(jsonData);

                    if (searchRes == null || searchRes.items == null)
                    {
                        return Array.Empty<string>();
                    }

                    // Result
                    var result = new string[searchRes.items.Length];
                    for (int i = 0; i < searchRes.items.Length; i++)
                    {
                        result[i] = searchRes.items[i].link;
                    }

                    return result;
                }
                catch (HttpRequestException)
                {
                    return Array.Empty<string>();
                }
            }
        }
        // REENABLE FOR ONLINE ICONS
        //public static List<string> GetIconLinksForGame(string game, int count)
        //{
        //    string gameName = game.ToLowerNormalized();

        //    if (File.Exists(Path.Combine(MainWindow.DefaultAssetPath, "db", "onlineicons.json")))
        //    {
        //        var listDict = Serialization.OnlineIconsList;

        //        if (listDict != null && listDict.Count > 0)
        //        {
        //            for (int i = 0; i < listDict.Count; i++)
        //            {
        //                if (listDict[i].ContainsKey(gameName) && listDict[i][gameName].Count > 0)
        //                {
        //                    Console.WriteLine("Serialized online icon results already exist for " + gameName);
        //                    return listDict[i][gameName];
        //                }
        //            }
        //        }
        //    }

        //    List<string> result = new List<string>();

        //    try
        //    {
        //        result = FetchIconLinks(game).ToList();
        //    }
        //    catch (WebException wex)
        //    {
        //        Console.WriteLine("Couldn't fetch online icons" + wex.Message);
        //    }

        //    if (result.Count > 0)
        //    {
        //        Serialization.SerializeOnlineIconList(gameName, result);
        //        Console.WriteLine("Serializing Icon List to JSON");
        //    }

        //    return result;
        //}

        private static void DownloadTemporaryIcon(string link, string folderName, int index, IconCollection iconCollection)
        {
            Directory.CreateDirectory(MainWindow.DefaultAssetPath + @"temp\" + folderName);
            string newPath = MainWindow.DefaultAssetPath + @"temp\" + folderName + @"\" + index + ".png";

            WebClient webClient = new WebClient();
            webClient.DownloadFile(link, newPath);
            webClient.Dispose();
            iconCollection.Paths.Add(newPath);
            iconCollection.SourceLinks.Add(link);
        }

        /// <summary>
        /// Downloads an icon from a link to a specified folder name in /temp and names it 0.png
        /// Used when clicking on an online image from Edit Game window
        /// </summary>
        /// <param name="link"></param>
        /// <param name="folderName"></param>
        /// <returns>The path to the downloaded file</returns>
        public static string DownloadIconFromLinkToTempFolder(string link, string folderName)
        {
            Directory.CreateDirectory(MainWindow.DefaultAssetPath + @"temp\" + folderName);
            string newPath = MainWindow.DefaultAssetPath + @"temp\" + folderName + @"\" + "0.png";

            WebClient webClient = new WebClient();
            webClient.DownloadFile(link, newPath);
            webClient.Dispose();
            return newPath;
        }

        // Constants
        /* Ovalness Loose Min & Max are used to determine whether the image will be cropped
         * Ovalness Tight Min & Max are used for extra points when choosing best image */

        const double ovalnessTightMinThreshold = 0.29d; //prev 0.29d
        const double ovalnessTightMaxThreshold = 0.84d; //prev 0.47d
        const double ovalnessLooseMinThreshold = 0.25d; //prev 0.25d
        const double ovalnessLooseMaxThreshold = 0.88d; //prev 0.51d

        private static async Task SetBestIcon(IconCollection iconCollection, Program program)
        {
            string tempPath;
            // Debug var
            bool fromDeviantArt = false;
            // Set scores array so we can fill it
            for (int i = 0; i < iconCollection.Paths.Count; i++)
            {
                iconCollection.Scores.Add(0);
            }

            await Task.Run(() => Parallel.ForEach(iconCollection.Paths, (path, state, index) =>
            {
                int i = (int)index;

                if (iconCollection.ScannedImageDataList[i].AspectRatio > 0.98d && iconCollection.ScannedImageDataList[i].AspectRatio < 1.02d)
                {
                    iconCollection.Scores[i]++;
                }

                if (iconCollection.ScannedImageDataList[i].AspectRatio > 0.9d && iconCollection.ScannedImageDataList[i].AspectRatio < 1.1d)
                {
                    iconCollection.Scores[i]++;
                }

                if (iconCollection.SourceLinks.Contains("wixmp.com"))
                {
                    iconCollection.Scores[i]++;
                    fromDeviantArt = true; // used for debugging
                }

                if (iconCollection.ScannedImageDataList[i].TransparencyPercentage >= 20
                    && iconCollection.ScannedImageDataList[i].TransparencyPercentage <= 75)
                {
                    iconCollection.Scores[i]++;
                }

                if (iconCollection.ScannedImageDataList[i].TransparencyPercentage >= 30
                    && iconCollection.ScannedImageDataList[i].TransparencyPercentage <= 40)
                {
                    iconCollection.Scores[i]++;
                }
                //recently added
                if (iconCollection.ScannedImageDataList[i].BlackPercentage < 99)
                {
                    iconCollection.Scores[i]++;
                }
                //recently added
                if (iconCollection.ScannedImageDataList[i].WhitePercentage < 99)
                {
                    iconCollection.Scores[i]++;
                }

                //Sample from 33 deviantart images
                //MIN OVALNESS: 0.297365196078431
                //MAX OVALNESS: 0.462475752685543

                if (iconCollection.ScannedImageDataList[i].Ovalness >= ovalnessTightMinThreshold
                    && iconCollection.ScannedImageDataList[i].Ovalness <= ovalnessTightMaxThreshold)
                {
                    iconCollection.Scores[i]++;
                }

                if (iconCollection.ScannedImageDataList[i].Ovalness >= ovalnessLooseMinThreshold
                    && iconCollection.ScannedImageDataList[i].Ovalness <= ovalnessLooseMaxThreshold)
                {
                    iconCollection.Scores[i]++;
                }

                Console.WriteLine("<<< Icon Evaluation Start >>>");
                Console.WriteLine($"Path: {iconCollection.Paths[i]}");
                Console.WriteLine($"Oval score: {iconCollection.ScannedImageDataList[i].Ovalness}");
                Console.WriteLine($"Aspect ratio: {iconCollection.ScannedImageDataList[i].AspectRatio}");
                Console.WriteLine($"From Deviant: {fromDeviantArt}");
                Console.WriteLine($"Transparency %: {iconCollection.ScannedImageDataList[i].TransparencyPercentage}");
                Console.WriteLine($"Black %: {iconCollection.ScannedImageDataList[i].BlackPercentage}");
                Console.WriteLine($"White %: {iconCollection.ScannedImageDataList[i].WhitePercentage}");
                Console.WriteLine($"Score: {iconCollection.Scores[i]}");
                Console.WriteLine("<<< Icon Evaluation End >>>");
            })).ConfigureAwait(false);

            int chosenIndex = 0;
            double aspectToPass = 0;
            double ovalnessToPass = -1;

            // If it found any icons
            if (iconCollection.Scores.Count > 0)
            {
                //Which icon was chosen?
                chosenIndex = iconCollection.Scores.IndexOf(iconCollection.Scores.Max());

                // Add Icon URL to program immediately - this is running async
                program.UpdateIconLinkData(iconCollection.SourceLinks[chosenIndex]);

                tempPath = iconCollection.Paths[chosenIndex];
                aspectToPass = iconCollection.ScannedImageDataList[chosenIndex].AspectRatio;
                ovalnessToPass = iconCollection.ScannedImageDataList[chosenIndex].Ovalness;

                //Is this too black or white? Then get system or no icon
                if (iconCollection.ScannedImageDataList[chosenIndex].BlackPercentage > 88
                    || iconCollection.ScannedImageDataList[chosenIndex].BlackPercentage > 88) // was 98
                {
                    // saves to TEMP
                    tempPath = GetPathToJumboOrNoIcon(program);
                    // pass aspect 1
                    aspectToPass = 1;
                    // for clarity
                    ovalnessToPass = -1;
                }
            }
            else
            {
                // Get system or no icon, because no other icons were found
                tempPath = GetPathToJumboOrNoIcon(program); // saves to TEMP
                // pass aspect 1
                aspectToPass = 1;
                // for clarity
                ovalnessToPass = -1;
            }

            // Do clipping, cropping, framing, etc. and copy over to the main icon folder
            PrepareIconForGrid(tempPath, aspectToPass, program, ovalnessToPass);
        }

        /// <summary>
        /// Checks if the icon fits the grid by evaluating aspect ratio and ovalness and if it doesn't it will clip to circle and frame it, then saves it as main icon
        /// </summary>
        /// <param name="path"></param>
        /// <param name="aspectRatio"></param>
        /// <param name="program"></param>
        /// <param name="ovalness">If -1 ovalness will be re-evaluated</param>
        public static void PrepareIconForGrid(string path, double aspectRatio, Program program, double ovalness = -1)
        {
            string defIconPath = MainWindow.DefaultAssetPath + @"Icons\" + program?.IconGUID + ".png";
            //string changedIconPath = MainWindow.DefaultAssetPath + @"Icons\changed\" + program?.IconGUID + ".png";

            if (Properties.Settings.Default.CropManuallyAddedIcons)
            {
                if (IsImageFittingGrid(path, aspectRatio, ovalness))
                {
                    //MoveIconToFuzionFolder(path, program?.IconGUID, IconSaveDestination.Default);
                    CropSave(path, program.IconGUID);

                }
                else
                {
                    ClipToCircleAndSave(path, defIconPath, AppDomain.CurrentDomain.BaseDirectory + @"Assets\iconframe.png");
                }
            }
            else
            {
                CropSave(path, program.IconGUID);
                //MoveIconToFuzionFolder(path, program?.IconGUID, IconSaveDestination.Default);
            }

            // Copy it over to changed folder so we don't get exceptions when reverting icon
            MoveIconToFuzionFolder(defIconPath, program?.IconGUID, IconSaveDestination.Changed);

            //// Move the icon to replace default icon only if it was noicon
            //if (File.Exists(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png"))
            //{
            //    File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png",
            //                     Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + program.IconGUID + ".png", true);
            //}

        }

        public static bool IsImageFittingGrid(string pathToImage, double aspect = -1d, double ovalness = -1d)
        {
            Console.WriteLine($"IsImageFittingGrid: IN Aspect {aspect}, Ovalness {ovalness}, Path {pathToImage}");

            if (ovalness == -1d || aspect == -1d)
            {
                var imageData = GetScannedImageData(pathToImage);
                aspect = imageData.AspectRatio;
                ovalness = imageData.Ovalness;
            }

            if (aspect < 0.9d || aspect > 1.1d)
            {
                return false;
            }

            // If it's not oval enough
            if (ovalness < ovalnessLooseMinThreshold || ovalness > ovalnessLooseMaxThreshold)
            {
                return false;
            }

            return true;
        }

        private static double ImageOvalness(BitmapSource bitmap)
        {
            double patternScore = 0;
            double result;

            if (bitmap.Format != PixelFormats.Bgra32)
            {
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            }

            var pixelCount = bitmap.PixelWidth * bitmap.PixelHeight;
            var pixels = new byte[4 * pixelCount];

            bitmap.CopyPixels(pixels, 4 * bitmap.PixelWidth, 0);

            // First pixel is not transparent, return 0 ovalness
            if (pixels[3] != 0)
            {
                return 0;
            }

            for (var i = 3; i < 4 * pixelCount / 2; i += 4) // start at first alpha value end at half the image
            {
                if (pixels[i] != 0)
                {
                    patternScore++;
                }
            }

            result = patternScore / pixelCount;
            //Console.WriteLine("OVALNESS: " + result);

            return result;
        }

        const byte pixelTransparencyThreshold = 60;

        /// <summary>
        /// Main function for ovalness
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static double GetImageOvalness(string path)
        {
            BitmapSource bitmapSource = ImageFromPath(path);

            if (bitmapSource.Format != PixelFormats.Bgra32)
            {
                bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
            }

            var pixelCount = bitmapSource.PixelWidth * bitmapSource.PixelHeight;
            var pixelBytes = new byte[4 * pixelCount];

            bitmapSource.CopyPixels(pixelBytes, 4 * bitmapSource.PixelWidth, 0);

            // First pixel is not transparent, return 0 ovalness
            if (pixelBytes[3] > pixelTransparencyThreshold)
            {
                return 0;
            }

            // Init min max
            Point min = new Point(int.MaxValue, int.MaxValue);
            Point max = new Point(int.MinValue, int.MinValue);
            int minXPositionY = 0;

            // Get min max
            int x = 1;
            int y = 1;
            for (int i = 3; i < 4 * pixelCount; i += 4)
            {
                if (pixelBytes[i] > pixelTransparencyThreshold)
                {
                    if (x < min.X)
                    {
                        min.X = x;
                        minXPositionY = y;
                    }
                    if (y < min.Y) min.Y = y;

                    if (x > max.X) max.X = x;
                    if (y > max.Y) max.Y = y;
                }

                x++;

                if ((i + 1) % (4 * bitmapSource.PixelWidth) == 0)
                {
                    x = 1;
                    y++;
                }
            }


            // Adjust values +-1 pixel as we're grabbing the first non-transparent pixel (so one before and after)
            min.X--;
            min.Y--;

            Console.WriteLine($"Min {min.X}, {min.Y} : Max {max.X}, {max.Y}");
            Console.WriteLine("MinXPosY "+minXPositionY);

            // Get ovalness
            int actualWidth = max.X - min.X;
            int ovalnessScore = 0;
            for (int i = 3; i < 4 * actualWidth * (minXPositionY - min.Y); i += 4)
            {
                if (pixelBytes[i] <= pixelTransparencyThreshold)
                {
                    ovalnessScore++;
                }
            }

            Console.WriteLine($"GetImageOvalness:{(double)ovalnessScore} / ({actualWidth} * ({minXPositionY} - {min.Y}))");
            Console.WriteLine($"GetImageOvalness returns: {(double)ovalnessScore / (actualWidth * (minXPositionY - min.Y))}");
            return (double)ovalnessScore / (actualWidth * (minXPositionY - min.Y));
        }

        private static bool IsSquareAspect(string imagePath)
        {
            Bitmap img = new Bitmap(imagePath);

            double aspectRatio = (double)(img.Width / img.Height);
            //Console.WriteLine("ASPECT WIDTH: " + img.Width + " HEIGHT: " + img.Height);

            //Console.WriteLine("ASPECT: "+aspectRatio+" FOR: "+imagePath);

            if (aspectRatio > 0.9d && aspectRatio < 1.1d)
            {
                img.Dispose();
                return true;
            }

            img.Dispose();
            return false;
        }

        public static void ClipToCircleAndSave(string imageToClipPath, string outputPath, string pathToFrame)//, PointF center, float radius)
        {
            try
            {
                Bitmap original = new Bitmap(imageToClipPath);

                int x = original.Width / 2;
                int y = original.Height / 2;
                double radius;

                if(original.Width <= original.Height)
                {
                    radius = original.Width / 2;
                }
                else
                {
                    radius = original.Height / 2;
                }
                
                Bitmap tmp = new Bitmap((int)(2 * radius), (int)(2 * radius));
                Graphics g = Graphics.FromImage(tmp);

                g.TranslateTransform(tmp.Width / 2, tmp.Height / 2);

                GraphicsPath path = new GraphicsPath();
                path.AddEllipse((float)(0d - radius), (float)(0d - radius), (float)(radius * 2d), (float)(radius * 2d));
                Region region = new Region(path);
                g.SetClip(region, CombineMode.Replace);

                Bitmap bmp = new Bitmap(imageToClipPath);
                g.DrawImage(bmp, new Rectangle((int)-radius, (int)-radius, 
                    (int)(2 * radius), (int)(2 * radius)),
                    new Rectangle((int)(x - radius), (int)(y - radius), (int)(2 * radius), (int)(2 * radius)),
                    GraphicsUnit.Pixel);

                // Frame
                if(original.Width <= original.Height)
                {
                    Bitmap frameBmp = new Bitmap(pathToFrame);
                    g.DrawImage(frameBmp, new Rectangle((int)-radius, (int)-radius,
                        original.Width, original.Width),
                        new Rectangle(0, 0, 255, 255),
                        GraphicsUnit.Pixel);

                    frameBmp.Dispose();
                }
                else
                {
                    Bitmap frameBmp = new Bitmap(pathToFrame);
                    g.DrawImage(frameBmp, new Rectangle((int)-radius, (int)-radius,
                        original.Height, original.Height),
                        new Rectangle(0, 0, 255, 255),
                        GraphicsUnit.Pixel);

                    frameBmp.Dispose();
                }
            
 
                // Resize
                var resultBitmap = ResizeImage(tmp, desiredImageWidth, desiredImageWidth);
                tmp.Dispose();

                resultBitmap.Save(outputPath);

                resultBitmap.Dispose();
                g.Dispose();
                path.Dispose();
                region.Dispose();
                bmp.Dispose();
                original.Dispose();
            }
            catch (Exception)
            {

            }
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        public static Bitmap ResizeBitmap(Bitmap bmp, int maxWidth)
        {
            var srcRectangle = new Rectangle(0, 0, bmp.Width, bmp.Height);
            double scaleMultiplier = (double)maxWidth / bmp.Width;
            var scaledBitmapSize = new Point(Convert.ToInt32(bmp.Width * scaleMultiplier), Convert.ToInt32(bmp.Height * scaleMultiplier));
            var destRectangle = new Rectangle(0, 0, scaledBitmapSize.X, scaledBitmapSize.Y);
            var newBitmap = new Bitmap(scaledBitmapSize.X, scaledBitmapSize.Y);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(bmp, destRectangle, srcRectangle, GraphicsUnit.Pixel);
                g.Dispose();
            }

            return newBitmap;
        }


        public static Bitmap ClipToCircle(Bitmap original)//, PointF center, float radius)
        {
            PointF center = new PointF(original.Width / 2, original.Height / 2);
            float radius = original.Width / 2;

            Bitmap copy = new Bitmap(original);
            using (Graphics g = Graphics.FromImage(copy))
            {
                RectangleF r = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(r);
                g.Clip = new Region(path);
                g.DrawImage(original, 0, 0);
                path.Dispose();
                return copy;
            }
        }

        private static void GetSpecificIcon(Program program, string link)
        {
            try
            {
                Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + program.IconGUID);
                string tempPath = Fuzion.MainWindow.DefaultAssetPath + @"temp\" + program.IconGUID + @"\" + "0" + ".png";

                WebClient webClient = new WebClient();
                webClient.DownloadFile(link, tempPath);
                webClient.Dispose();

                var imageData = GetScannedImageData(tempPath);

                if (IsImageFittingGrid(tempPath, imageData.AspectRatio, imageData.Ovalness))
                {
                    MoveIconToFuzionFolder(tempPath, program.IconGUID, IconSaveDestination.Default);
                }
                else
                {
                    ClipToCircleAndSave(tempPath,
                        Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png",
                        AppDomain.CurrentDomain.BaseDirectory + @"Assets\iconframe.png");
                }

                // Copy the newfound icon to replace Default icon only if old icon was noicon - needs to be implemented
                if (File.Exists(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png"))
                {
                    File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + program.IconGUID + ".png",
                                     Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + program.IconGUID + ".png", true);
                }

                // Set the link so when pushing to web db the icon link will be coming
                // from Fuzion online db
                program.UpdateIconLinkData(link);

                //Console.WriteLine("Game Object found: " + gameObjects.Find(game => game.IconGUID == program.IconGUID).DisplayName);
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                                RefreshGameIcon(GameObjects.Find(game => game.IconGUID == program.IconGUID))
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to download specific icon, attempting to get icon from web: " + ex.Message);
                DownloadIcon(program, true);
            }
        }

        private static double CheckOvalnessScore(string imagePath)
        {
            double result = 0;
            // Load the bitmap
            Bitmap originalBitmap = new Bitmap(imagePath);

            // Find the min/max non-white/transparent pixels
            System.Drawing.Point min = new System.Drawing.Point(int.MaxValue, int.MaxValue);
            System.Drawing.Point max = new System.Drawing.Point(int.MinValue, int.MinValue);

            for (int x = 0; x < originalBitmap.Width; x++)
            {
                for (int y = 0; y < originalBitmap.Height / 2; y++)
                {
                    if (originalBitmap.GetPixel(x, y).A != 0)
                    {
                        if (x < min.X) min.X = x;
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }
                }
            }

            //add one cause zero based
            min.X += 1;
            min.Y += 1;
            max.X += 1;
            max.Y += 1;

            //Console.WriteLine($"Min: {min} & Max: {max}");

            int newWidth = originalBitmap.Width - min.X - (originalBitmap.Width - max.X);
            int newHeight = originalBitmap.Height / 2 - min.Y;

            //iSelector.newWidth.Add(newWidth);
            //iSelector.newHeight.Add(newHeight * 2); // *2 because it's scanning only the upper half of the image

            //Console.WriteLine($"New Width: {newWidth} & New Height: {newHeight}");

            originalBitmap.Dispose();

            result = CalculateImageOvalPattern(ImageFromPath(imagePath), newWidth, newHeight);

            return result;
        }

        private static double CalculateImageOvalPattern(BitmapSource bitmap, int newWidth, int newHeight)
        {
            double patternScore = 0;
            double result;

            if (bitmap.Format != PixelFormats.Bgra32)
            {
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            }

            var pixelCount = bitmap.PixelWidth * bitmap.PixelHeight;
            var newPixelCount = newWidth * newHeight; //size without transparent edges
                                                      //Console.WriteLine(bitmap.PixelWidth + " " + bitmap.PixelHeight);
            var pixels = new byte[4 * pixelCount];

            bitmap.CopyPixels(pixels, 4 * bitmap.PixelWidth, 0);

            // First pixel is not transparent, return 0 ovalness
            if (pixels[3] != 0)
            {
                return 0;
            }

            for (var i = 3; i < 4 * pixelCount / 2; i += 4) // start at first alpha value; end at half the image
            {
                if (pixels[i] != 0)
                {
                    patternScore++;
                }
            }

            result = patternScore / newPixelCount;
            //Console.WriteLine("Ovalness score: " + Math.Round(result, 2));

            return result;
        }

        public class ScannedImageData
        {
            public string Source { get; set; }
            public double AspectRatio { get; set; }
            public double TransparencyPercentage { get; set; }
            public double BlackPercentage { get; set; }
            public double WhitePercentage { get; set; }
            public double Ovalness { get; set; }
        }

        public class PixelData
        {
            public int FullyTransparentPixelCount { get; set; }
            public int SemiTransparentPixelCount { get; set; }
            public int NonTransparentPixelCount { get; set; }
            public int WhitePixelCount { get; set; }
            public int BlackPixelCount { get; set; }
            public int OvalnessPatternScore { get; set; }
        }

        public const int desiredImageWidth = 512;

        public static ScannedImageData GetScannedImageData(string path, bool writeConsole = false)
        {
            //var watch = System.Diagnostics.Stopwatch.StartNew();
            // the code that you want to measure comes here

            // Filename
            //string fileName = Path.GetFileName(path);
            string originalDir = Path.GetDirectoryName(path);
            string tempFileName = Path.GetFileNameWithoutExtension(path) + "_temp.png";
            //Console.WriteLine("DirectoryName " + originalDir);
            //Console.WriteLine("tempFileName " + tempFileName);

            // Create the data object
            var imageData = new ScannedImageData();

            // Define local evaluation parameters
            var pixelData = new PixelData();

            // Load the bitmap only to check for resizing
            Bitmap bitmap = (Bitmap)Bitmap.FromFile(path);

            //// Create the temporary directory
            //var dirInfo = Directory.CreateDirectory(Path.Combine(originalDir, tempFileName));

            // resize first if necessary
            if (bitmap.Width > desiredImageWidth)
            {
                var newBitmap = ResizeBitmap(bitmap, desiredImageWidth);
                bitmap.Dispose();
                newBitmap.Save(Path.Combine(originalDir, tempFileName));
                newBitmap.Dispose();

                File.Copy(Path.Combine(originalDir, tempFileName), path, true);
                File.Delete(Path.Combine(originalDir, tempFileName));
            }
            else
            {
                bitmap.Dispose();
            }

            // Load bitmapSource
            BitmapSource bitmapSource = ImageFromPath(path);

            // Convert if the wrong format
            if (bitmapSource.Format != PixelFormats.Bgra32)
            {
                bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
            }

            // Get Min Max and initialize pixels array
            var pixelCount = bitmapSource.PixelWidth * bitmapSource.PixelHeight;
            var pixelBytes = new byte[4 * pixelCount];

            bitmapSource.CopyPixels(pixelBytes, 4 * bitmapSource.PixelWidth, 0);

            // Set min max
            Point min = new Point(int.MaxValue, int.MaxValue);
            Point max = new Point(int.MinValue, int.MinValue);
            int minXPositionY = 0;

            // The first alpha value is not transparent, skip Min Max
            if (pixelBytes[3] <= pixelTransparencyThreshold) // <= 60
            {
                // Scan alpha only to see if we need to crop before evaluation

                int x = 1;
                int y = 1;
                for (int i = 3; i < 4 * pixelCount; i += 4)
                {
                    if (pixelBytes[i] > pixelTransparencyThreshold) // >60
                    {
                        if (x < min.X)
                        {
                            min.X = x;
                            minXPositionY = y;
                        }
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }

                    x++;

                    if ((i + 1) % (4 * bitmapSource.PixelWidth) == 0)
                    {
                        x = 1;
                        y++;
                    }
                }

                // Adjust values +-1 pixel as we're grabbing the first non-transparent pixel (so one before and after)
                min.X--;
                min.Y--;
                //// Can go without adding to max
                //max.X++;
                //max.Y++;

                // Do crop
                bitmap = (Bitmap)Bitmap.FromFile(path);
                var croppedBitmap = Crop(bitmap, min, max, desiredImageWidth);
                bitmap.Dispose();
                croppedBitmap.Save(Path.Combine(originalDir, tempFileName));
                croppedBitmap.Dispose();

                // Adjust minXPositionY
                minXPositionY -= min.Y;

                // Move the cropped bitmap to replace the original one
                File.Copy(Path.Combine(originalDir, tempFileName), path, true);
                File.Delete(Path.Combine(originalDir, tempFileName));

                // Reload BitmapSource to the new cropped image
                bitmapSource = ImageFromPath(path);
                pixelCount = bitmapSource.PixelWidth * bitmapSource.PixelHeight;
                pixelBytes = new byte[4 * pixelCount];
                bitmapSource.CopyPixels(pixelBytes, 4 * bitmapSource.PixelWidth, 0);
            }
            else
            {
                // Set min max to image size
                min.X = 0;
                max.X = bitmapSource.PixelWidth;
                min.Y = 0;
                max.Y = bitmapSource.PixelHeight;
                minXPositionY = bitmapSource.PixelHeight / 2;
            }

            if (writeConsole)
            {
                Console.WriteLine("Bitmap Source Final - Width: " + bitmapSource.PixelWidth + " Height: " + bitmapSource.PixelHeight);
                Console.WriteLine("Bitmap Source pixelCount is " + pixelCount);
            }

            // Proceed to full image scan after a crop & resize has been done
            // Main Scan Loop
            for (int i = 3; i < 4 * pixelCount; i += 4)
            {
                // Get transparent
                if (pixelBytes[i] != 255)
                {
                    pixelData.SemiTransparentPixelCount++;
                }

                if (pixelBytes[i] == 0)
                {
                    pixelData.FullyTransparentPixelCount++;
                }

                // Get white
                if (pixelBytes[i - 1] == 255 && pixelBytes[i - 2] == 255 && pixelBytes[i - 3] == 255)
                {
                    pixelData.WhitePixelCount++;
                }

                // Get black
                if (pixelBytes[i - 1] == 0 && pixelBytes[i - 2] == 0 && pixelBytes[i - 3] == 0)
                {
                    pixelData.BlackPixelCount++;
                }

                // Get ovalness
                if (pixelBytes[i] < 4 * bitmapSource.PixelWidth * minXPositionY)
                {
                    if (pixelBytes[i] <= pixelTransparencyThreshold) // <= 60
                    {
                        pixelData.OvalnessPatternScore++;
                    }
                }
            }

            pixelData.NonTransparentPixelCount = pixelCount - pixelData.FullyTransparentPixelCount;

            // Fill Image Data
            imageData.AspectRatio = (double)bitmapSource.PixelWidth / bitmapSource.PixelHeight;
            imageData.TransparencyPercentage = ((double)pixelData.SemiTransparentPixelCount / pixelCount) * 100;
            imageData.BlackPercentage = ((double)pixelData.BlackPixelCount / pixelData.NonTransparentPixelCount) * 100;
            imageData.WhitePercentage = ((double)pixelData.WhitePixelCount / pixelData.NonTransparentPixelCount) * 100;
            imageData.Ovalness = (double)pixelData.OvalnessPatternScore / (bitmapSource.PixelWidth * minXPositionY);

            Console.WriteLine($"FILE: {Path.GetFileName(path)}");
            Console.WriteLine($"OVALNESS {imageData.Ovalness} = PatternScore: {pixelData.OvalnessPatternScore} / (PixelWidth: {bitmapSource.PixelWidth} * minXPosY {minXPositionY})");

            if (writeConsole)
            {
                // Output
                Console.WriteLine("<< IMAGE DATA START >>");
                Console.WriteLine("Aspect Ratio: " + imageData.AspectRatio);
                Console.WriteLine("Transparency Percentage: " + imageData.TransparencyPercentage);
                Console.WriteLine("Black Percentage: " + imageData.BlackPercentage);
                Console.WriteLine("White Percentage: " + imageData.WhitePercentage);
                Console.WriteLine("Ovalness: " + imageData.Ovalness);
                Console.WriteLine("<< IMAGE DATA END >>");
                Console.WriteLine("MinXPositionY: " + minXPositionY);
                Console.WriteLine("Ovalness scan area (pixels): " + bitmapSource.PixelWidth * minXPositionY);

            }

            //watch.Stop();
            //if (writeConsole)
            //{
            //    Console.WriteLine("Execution time: " + watch.ElapsedMilliseconds + " ms");
            //    Console.WriteLine("Execution time: " + watch.ElapsedTicks + " ticks");
            //}

            return imageData;
        }

        /// <summary>
        /// Static holder of NoIcon bitmap
        /// </summary>
        static Bitmap noIconBitmap;

        /// <summary>
        /// If the scan occurs too fast trying to copy noicon twice in quick succession from disk may lock the image and cause an exception.
        /// Therefore noicon is now saved to disk from memory
        /// </summary>
        /// <param name="toFile"></param>
        static void SafeCopyNoIcon(string toFile)
        {
            if(noIconBitmap == null)
            {
                noIconBitmap = new Bitmap(AppDomain.CurrentDomain.BaseDirectory + @"Assets\noicon.png");
            }

            // Still having issue with this, it may occur when the noicon is trying to be saved and
            // another icon is trying to replace it at the same time
            //noIconBitmap.Save(toFile);

            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    using (FileStream fs = new FileStream(toFile, FileMode.Create, FileAccess.ReadWrite))
                    {
                        noIconBitmap.Save(memory, ImageFormat.Png);
                        byte[] bytes = memory.ToArray();
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }
            }
            catch (Exception)
            {

            }
          
        }

        public static string GetPathToJumboOrNoIcon(Program program)
        {
            Directory.CreateDirectory(MainWindow.DefaultAssetPath + @"temp\");
            string savePath = MainWindow.DefaultAssetPath + @"temp\" + program.IconGUID + ".png";

            try
            {
                if (File.Exists(program?.SystemIcon))
                {
                    Icon ico = IconsExtractor.ExtractIconFromFile(program.SystemIcon);

                    ico.ToBitmap().Save(savePath, ImageFormat.Png);

                    ico.Dispose();
                }
                else if (File.Exists(program.Path))
                {
                    Icon ico = IconsExtractor.ExtractIconFromFile(program.Path);

                    ico.ToBitmap().Save(savePath, ImageFormat.Png);

                    ico.Dispose();
                }
                else
                {
                    // Get no icon
                    SafeCopyNoIcon(savePath);
                }
            }
            catch (Exception)
            {
                // Get no icon
                SafeCopyNoIcon(savePath);
            }

            return savePath;
        }

        public static string GetPathToJumboOrNoIcon(Game program)
        {
            Directory.CreateDirectory(MainWindow.DefaultAssetPath + @"temp\");
            string savePath = MainWindow.DefaultAssetPath + @"temp\" + program.IconGUID + ".png";

            try
            {
                if (File.Exists(program?.SystemIcon))
                {
                    Icon ico = IconsExtractor.ExtractIconFromFile(program.SystemIcon);

                    ico.ToBitmap().Save(savePath, ImageFormat.Png);

                    ico.Dispose();
                }
                else if (File.Exists(program.Path))
                {
                    Icon ico = IconsExtractor.ExtractIconFromFile(program.Path);

                    ico.ToBitmap().Save(savePath, ImageFormat.Png);

                    ico.Dispose();
                }
                else
                {
                    // Get no icon
                    SafeCopyNoIcon(savePath);
                }
            }
            catch (Exception)
            {
                // Get no icon
                SafeCopyNoIcon(savePath);
            }

            return savePath;
        }

        public static string PathToJumboIcon(string pathToFile, string guidName)
        {
            Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"temp\");
            Icon ico = IconsExtractor.ExtractIconFromFile(pathToFile);

            //string guidName = "image";//Guid.NewGuid().ToString();

            ico.ToBitmap().Save(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + guidName + ".png", System.Drawing.Imaging.ImageFormat.Png);

            //CropAndSaveIcon(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + program.IconGUID + ".png", program.IconGUID);
            ico.Dispose();
            return Fuzion.MainWindow.DefaultAssetPath + @"temp\" + guidName + ".png";
        }

        /// <summary>
        /// Replace the current game icon with the Jumbo Icon
        /// </summary>
        /// <param name="game">The game which will have its icon replaced</param>
        static void GetJumboIcon(Game game)
        {
            Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"temp\");
            Icon ico = IconsExtractor.ExtractIconFromFile(game.SystemIcon);
            ico.ToBitmap().Save(@"temp\" + game.IconGUID + ".png", System.Drawing.Imaging.ImageFormat.Png);

            CropSave(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + game.IconGUID + ".png", game.IconGUID);

            //return game.Icon;
        }

        static BitmapImage ImageFromGUID(string gameGUID)
        {
            string path = Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + gameGUID + ".png";

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            return image;
        }

        public static BitmapImage JumboIcon(Game game)
        {
            Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"temp\");
            Icon ico = IconsExtractor.ExtractIconFromFile(game?.SystemIcon);
            ico.ToBitmap().Save(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + game.IconGUID + ".png", System.Drawing.Imaging.ImageFormat.Png);

            CropSave(Fuzion.MainWindow.DefaultAssetPath + @"temp\" + game.IconGUID + ".png", game.IconGUID);

            // Copy jumbo icon to changed in case no other icon was found
            File.Copy(Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + game.IconGUID + ".png",
                Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + game.IconGUID + ".png", true);

            return ImageFromGUID(game.IconGUID);

        }
    }
}
