using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fuzion.GameDeals
{
    class RedditJSONParser
    {
        public class MediaEmbed
        {
        }

        public class SecureMediaEmbed
        {
        }

        public class Gildings
        {
            public int? gid_2 { get; set; }
        }

        public class Source
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Resolution
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Obfuscated
        {
            public Source source { get; set; }
            public IList<Resolution> resolutions { get; set; }
        }

        public class Variants
        {
            public Obfuscated obfuscated { get; set; }
        }

        public class Image
        {
            public Source source { get; set; }
            public IList<Resolution> resolutions { get; set; }
            public Variants variants { get; set; }
            public string id { get; set; }
        }

        public class Preview
        {
            public IList<Image> images { get; set; }
            public bool enabled { get; set; }
        }

        public class ResizedIcon
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class AllAwarding
        {
            public object giver_coin_reward { get; set; }
            public object subreddit_id { get; set; }
            public bool is_new { get; set; }
            public int days_of_drip_extension { get; set; }
            public int coin_price { get; set; }
            public string id { get; set; }
            public object penny_donate { get; set; }
            public int coin_reward { get; set; }
            public string icon_url { get; set; }
            public int days_of_premium { get; set; }
            public int icon_height { get; set; }
            public IList<ResizedIcon> resized_icons { get; set; }
            public int icon_width { get; set; }
            public object start_date { get; set; }
            public bool is_enabled { get; set; }
            public string description { get; set; }
            public object end_date { get; set; }
            public int subreddit_coin_reward { get; set; }
            public int count { get; set; }
            public string name { get; set; }
            public object icon_format { get; set; }
            public string award_sub_type { get; set; }
            public object penny_price { get; set; }
            public string award_type { get; set; }
        }

        public class Data
        {
            public object approved_at_utc { get; set; }
            public string subreddit { get; set; }
            public string selftext { get; set; }
            public string author_fullname { get; set; }
            public bool saved { get; set; }
            public object mod_reason_title { get; set; }
            public int gilded { get; set; }
            public bool clicked { get; set; }
            public string title { get; set; }
            public IList<object> link_flair_richtext { get; set; }
            public string subreddit_name_prefixed { get; set; }
            public bool hidden { get; set; }
            public int pwls { get; set; }
            public string link_flair_css_class { get; set; }
            public int downs { get; set; }
            public int? thumbnail_height { get; set; }
            public bool hide_score { get; set; }
            public string name { get; set; }
            public bool quarantine { get; set; }
            public string link_flair_text_color { get; set; }
            public double upvote_ratio { get; set; }
            public string author_flair_background_color { get; set; }
            public string subreddit_type { get; set; }
            public int ups { get; set; }
            public int total_awards_received { get; set; }
            public MediaEmbed media_embed { get; set; }
            public int? thumbnail_width { get; set; }
            public object author_flair_template_id { get; set; }
            public bool is_original_content { get; set; }
            public IList<object> user_reports { get; set; }
            public object secure_media { get; set; }
            public bool is_reddit_media_domain { get; set; }
            public bool is_meta { get; set; }
            public object category { get; set; }
            public SecureMediaEmbed secure_media_embed { get; set; }
            public string link_flair_text { get; set; }
            public bool can_mod_post { get; set; }
            public int score { get; set; }
            public object approved_by { get; set; }
            public bool author_premium { get; set; }
            public string thumbnail { get; set; }
            public object edited { get; set; }
            public string author_flair_css_class { get; set; }
            public IList<object> author_flair_richtext { get; set; }
            public Gildings gildings { get; set; }
            public string post_hint { get; set; }
            public object content_categories { get; set; }
            public bool is_self { get; set; }
            public object mod_note { get; set; }
            public double created { get; set; }
            public string link_flair_type { get; set; }
            public int wls { get; set; }
            public object removed_by_category { get; set; }
            public object banned_by { get; set; }
            public string author_flair_type { get; set; }
            public string domain { get; set; }
            public bool allow_live_comments { get; set; }
            public string selftext_html { get; set; }
            public object likes { get; set; }
            public object suggested_sort { get; set; }
            public object banned_at_utc { get; set; }
            public object view_count { get; set; }
            public bool archived { get; set; }
            public bool no_follow { get; set; }
            public bool is_crosspostable { get; set; }
            public bool pinned { get; set; }
            public bool over_18 { get; set; }
            public Preview preview { get; set; }
            public IList<AllAwarding> all_awardings { get; set; }
            public IList<object> awarders { get; set; }
            public bool media_only { get; set; }
            public bool can_gild { get; set; }
            public bool spoiler { get; set; }
            public bool locked { get; set; }
            public string author_flair_text { get; set; }
            public IList<object> treatment_tags { get; set; }
            public bool visited { get; set; }
            public object removed_by { get; set; }
            public object num_reports { get; set; }
            public object distinguished { get; set; }
            public string subreddit_id { get; set; }
            public object mod_reason_by { get; set; }
            public object removal_reason { get; set; }
            public string link_flair_background_color { get; set; }
            public string id { get; set; }
            public bool is_robot_indexable { get; set; }
            public object report_reasons { get; set; }
            public string author { get; set; }
            public object discussion_type { get; set; }
            public int num_comments { get; set; }
            public bool send_replies { get; set; }
            public string whitelist_status { get; set; }
            public bool contest_mode { get; set; }
            public IList<object> mod_reports { get; set; }
            public bool author_patreon_flair { get; set; }
            public string author_flair_text_color { get; set; }
            public string permalink { get; set; }
            public string parent_whitelist_status { get; set; }
            public bool stickied { get; set; }
            public string url { get; set; }
            public int subreddit_subscribers { get; set; }
            public double created_utc { get; set; }
            public int num_crossposts { get; set; }
            public object media { get; set; }
            public bool is_video { get; set; }

            public string modhash { get; set; }
            public int dist { get; set; }
            public IList<Child> children { get; set; }
            public string after { get; set; }
            public object before { get; set; }
        }

        public class Child
        {
            public string kind { get; set; }
            public Data data { get; set; }
        }


        public class RedditGameDealsObject
        {
            public string kind { get; set; }
            public Data data { get; set; }
        }

        public static void GetGameDealsReddit()
        {
            string gamedealsjson = string.Empty;

            using (WebClient wc = new WebClient())
            {
                gamedealsjson = wc.DownloadString("https://www.reddit.com/r/GameDeals/best.json");
            }

            RedditGameDealsObject robj = new RedditGameDealsObject();

            robj = JsonConvert.DeserializeObject<RedditGameDealsObject>(gamedealsjson);

            RedditDealsFromJSON(robj);

        }

        public static List<Deal> RedditDealsFromJSON(RedditGameDealsObject redditJsonObject = null)
        {
            List<Deal> result = new List<Deal>();

            // Get the json if no override object specified
            if(redditJsonObject == null)
            {
                string gamedealsjson = string.Empty;

                using (WebClient wc = new WebClient())
                {
                    gamedealsjson = wc.DownloadString("https://www.reddit.com/r/GameDeals/hot.json");
                }

                redditJsonObject = JsonConvert.DeserializeObject<RedditGameDealsObject>(gamedealsjson);
            }


            for (int i = 0; i < redditJsonObject.data.children.Count; i++)
            {
                Deal d = new Deal();

                // Deal Raw Title
                d.Name = redditJsonObject.data.children[i].data.title;

                // Deal Source
                d.DealSource = Regex.Match(redditJsonObject.data.children[i].data.title, @"[^\[\]]+").Value;

                // Price and discount
                string priceDiscountString = redditJsonObject.data.children[i].data.title.Split('(', ')')[1];

                string firstString = string.Empty;
                string secondString = string.Empty;

                try
                {
                    firstString = priceDiscountString.Split('/')[0];
                    secondString = priceDiscountString.Split('/')[1];
                }
                catch (Exception)
                {

                }

                // The first one is the discount, usually it's the second
                if (firstString.Contains('%'))
                {
                    d.Price = secondString;
                    d.DiscountPercent = GetPercentFromDealPost(firstString);
                } else
                {
                    d.Price = firstString;
                    d.DiscountPercent = GetPercentFromDealPost(secondString);
                }

                // Cleanup Name
                d.Name = d.Name.Replace(d.DealSource, string.Empty);
                d.Name = Regex.Match(d.Name, @"[^\(\)]+").Value;
                d.Name = d.Name.Replace("[", string.Empty);
                d.Name = d.Name.Replace("]", string.Empty);

                // Link
                d.Link = redditJsonObject.data.children[i].data.url;

                // Output
                Console.WriteLine("Deal Name: "+d.Name);
                Console.WriteLine("Deal Price: "+d.Price);
                Console.WriteLine("Deal Discount: "+d.DiscountPercent);
                Console.WriteLine("Deal Source: "+d.DealSource);
                Console.WriteLine("Deal Link: "+d.Link);

                result.Add(d);

            }

            return result;
        }

        private static string GetPercentFromDealPost(string percent)
        {
            return new string(percent.Where(x => char.IsDigit(x) || x == ('%')).ToArray());
        }

    }
}
