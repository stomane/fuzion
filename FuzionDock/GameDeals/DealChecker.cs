using Reddit.AuthTokenRetriever;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Fuzion.MainWindow;

namespace Fuzion.GameDeals
{
    class Deal
    {
        public string Name { get; set; }
        public string Price { get; set; }
        public string OriginalPrice { get; set; }
        public string DiscountPercent { get; set; }
        public string Link { get; set; }
        public string ImageLink { get; set; }
        public string DealSource { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    static class DealChecker
    {
        public static List<Deal> GameDeals { get; set; } = new List<Deal>();
        public static int CurrentDealIndex { get; private set; }

        public static void CheckCurrentGameDeals()
        {
            //var myClient = new WebClient();
            //Stream response = myClient.OpenRead("https://yesthereisadeal.com/");

            //string webpageString = await myClient.DownloadStringTaskAsync("https://yesthereisadeal.com/").ConfigureAwait(false);

            Uri uri = new Uri("https://www.indiegamebundles.com/category/free/");

            WebRequest webRequest;
            Stream objStream;

            webRequest = WebRequest.Create(uri);
            objStream = webRequest.GetResponse().GetResponseStream();
            StreamReader streamReader = new StreamReader(objStream);

            string streamLine = "";
            int i = 0;

            while (streamLine != null)
            {
                i++;
                streamLine = streamReader.ReadLine();
                //Console.WriteLine("Game Deal Line: " + streamLine);

                if (streamLine != null && streamLine.Contains("rel=\"bookmark\""))
                {
                    //Console.WriteLine("Current Deal: " + streamLine);

                    Regex regex = new Regex("(?<=\")(.*?)(?=\")"); // between "" but excluding "
                    MatchCollection matches = regex.Matches(streamLine);
                   
                    string link = matches[2].Value;
                    string name = string.Empty;

                    for (int x = 0; x < matches.Count; x++)
                    {
                        //Console.WriteLine("Value at: "+x + " is: "+matches[x].Value);

                        if(matches[x].Value.Contains("title="))
                        {
                            name = matches[x + 1].Value;
                            //Console.WriteLine("Found Match at index "+x);
                            break;
                        }
                    }

                   

                    if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(link) && !GameDeals.Any(gd => gd.Name == name))
                    {
                        Console.WriteLine("Adding Free Game");
                        Console.WriteLine("Name: " + name);
                        Console.WriteLine("Link: " + link);

                        Deal d = new Deal
                        {
                            Name = name,
                            Link = link,
                            Price = "Free"
                        };

                        GameDeals.Add(d);
                    }

                    if (GameDeals.Count > 0)
                    {
                        AppWindow.DealName.Content = GameDeals[0].Name;
                        AppWindow.DealPrice.Content = GameDeals[0].Price;
                        AppWindow.DealDiscountPercent.Content = GameDeals[0].DiscountPercent;
                        AppWindow.DealSource.Content = GameDeals[0].DealSource;
                        AppWindow.DealLink.Content = GameDeals[0].Link;
                    }

                    //foreach (var m in matches)
                    //{
                    //    Console.WriteLine("Match: " + m.ToString());
                    //}

                    //Console.WriteLine("Matches count: " + matches.Count);
                }
            }



            Console.ReadLine();
            streamReader.Close();

        }

        public static Deal GetNextDeal()
        {
            CurrentDealIndex++;

            if (CurrentDealIndex > GameDeals.Count - 1)
                CurrentDealIndex = 0;

            return GameDeals[CurrentDealIndex];
        }

        public static void LoadDeals()
        {
            GameDeals = RedditJSONParser.RedditDealsFromJSON();

            if (GameDeals.Count > 0)
            {
                AppWindow.DealName.Content = GameDeals[0].Name;
                AppWindow.DealPrice.Content = GameDeals[0].Price;
                AppWindow.DealDiscountPercent.Content = GameDeals[0].DiscountPercent;
                AppWindow.DealSource.Content = GameDeals[0].DealSource;
                AppWindow.DealLink.Content = GameDeals[0].Link;
                
            }
        }

        public static List<Deal> RedditDeals()
        {
            // use https://reddit.com/r/GameDeals/best.json for a start. Can't get the api to work yet
            // use json2csharp to get results quick

            List<Deal> result = new List<Deal>();


            //Uri uri = new Uri("https://www.reddit.com/r/GameDeals/");

            //WebRequest webRequest;
            //Stream objStream;

            //webRequest = WebRequest.Create(uri);
            //objStream = webRequest.GetResponse().GetResponseStream();
            //StreamReader streamReader = new StreamReader(objStream);

            //string streamLine = "";
            //int i = 0;

            //while (streamLine != null)
            //{
            //    i++;
            //    streamLine = streamReader.ReadLine();
            //    //Console.WriteLine("Game Deal Line: " + streamLine);

            //    if (streamLine != null && streamLine.Contains("h3 class="))
            //    {
            //        Console.WriteLine("Found Line: " + streamLine);

            //        //Regex regex = new Regex("(?<=\")(.*?)(?=\")"); // between "" but excluding "
            //        //MatchCollection matches = regex.Matches(streamLine);

            //        //string link = matches[2].Value;
            //        //string name = string.Empty;

            //        //for (int x = 0; x < matches.Count; x++)
            //        //{
            //        //    //Console.WriteLine("Value at: "+x + " is: "+matches[x].Value);

            //        //    if (matches[x].Value.Contains("title="))
            //        //    {
            //        //        name = matches[x + 1].Value;
            //        //        //Console.WriteLine("Found Match at index "+x);
            //        //        break;
            //        //    }
            //        //}



            //        //if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(link) && !GameDeals.Any(gd => gd.Name == name))
            //        //{
            //        //    Console.WriteLine("Adding Free Game");
            //        //    Console.WriteLine("Name: " + name);
            //        //    Console.WriteLine("Link: " + link);

            //        //    Deal d = new Deal
            //        //    {
            //        //        Name = name,
            //        //        Link = link,
            //        //        Price = "Free"
            //        //    };

            //        //    GameDeals.Add(d);
            //        //}

            //        //if (GameDeals.Count > 0)
            //        //{
            //        //    AppWindow.DealName.Content = GameDeals[0].Name;
            //        //    AppWindow.DealPrice.Content = GameDeals[0].Price;
            //        //    AppWindow.DealDiscountPercent.Content = GameDeals[0].DiscountPercent;
            //        //    AppWindow.DealSource.Content = GameDeals[0].DealSource;
            //        //    AppWindow.DealLink.Content = GameDeals[0].Link;
            //        //}

            //        //foreach (var m in matches)
            //        //{
            //        //    Console.WriteLine("Match: " + m.ToString());
            //        //}

            //        //Console.WriteLine("Matches count: " + matches.Count);
            //    }
            //}

            //Console.ReadLine();
            //streamReader.Close();

            //Reddit.RedditClient r = new Reddit.RedditClient("APpovGWsdSv9EQ");
            //// Get info on another subreddit.
            //var askReddit = r.Subreddit("AskReddit").About();

            //// Get the top post from a subreddit.
            //var topPosts = askReddit.Posts.Best;
            var oauth = new OAuthToken();
            var r = new Reddit.RedditClient("APpovGWsdSv9EQ", oauth.RefreshToken, null, oauth.AccessToken);
            // Get info on another subreddit.
            var askReddit = r.Subreddit("GameDeals").About();

            // Get the top post from a subreddit.
            var topPost = askReddit.Posts.Top[0];

            Console.WriteLine("Top Post: "+topPost.Title);
            //var kek = srs.Subscribers;
            //Console.WriteLine("Subreddit subs: " + srs + " " );

            //foreach (var item in srs)
            //{

            //}
            //Console.WriteLine("refresh token"+refreshToken);

            //var topPosts = new Reddit.RedditClient("APpovGWsdSv9EQ", new OAuthToken().AccessToken).Subreddit("GameDeals").Posts.Best;

            //foreach (var post in topPosts)
            //{
            //    Console.WriteLine(post.Title);
            //}

            return result;
        }

        public static string AuthorizeRedditApp(string appId, string appSecret = null, int port = 8080)
        {
            // Create a new instance of the auth token retrieval library.  --Kris
            Reddit.AuthTokenRetriever.OAuthToken oAuthToken = new OAuthToken();

            AuthTokenRetrieverLib authTokenRetrieverLib = new AuthTokenRetrieverLib(appId, appSecret, port);

            // Start the callback listener.  --Kris
            // Note - Ignore the logging exception message if you see it.  You can use Console.Clear() after this call to get rid of it if you're running a console app.
            authTokenRetrieverLib.AwaitCallback();

            // Open the browser to the Reddit authentication page.  Once the user clicks "accept", Reddit will redirect the browser to localhost:8080, where AwaitCallback will take over.  --Kris
            //OpenBrowser(authTokenRetrieverLib.AuthURL());

            // Replace this with whatever you want the app to do while it waits for the user to load the auth page and click Accept.  --Kris
            //while (true) { }

            // Cleanup.  --Kris
            authTokenRetrieverLib.StopListening();

            return authTokenRetrieverLib.RefreshToken;
        }
    }
}
