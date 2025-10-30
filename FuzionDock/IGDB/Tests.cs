using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IGDB.Models;

namespace Fuzion.IGDB
{
    class Tests
    {
        public static async void IGDBNugetTest(string name)
        {
            var igdb = new global::IGDB.IGDBClient("a65661d40ccb4f78fbf311d8ba1d6559","ADD_CLIENT_SECRET_HERE");


            // Simple fields
            string query = "fields name; limit 1; search \"" + name + "\";";// where name ~ \"" + name + "\";";
            Console.WriteLine(query);
            var games = await igdb.QueryAsync<Game>(global::IGDB.IGDBClient.Endpoints.Games, query: query).ConfigureAwait(false);

            if(games.Length > 0)
            {
                var game = games.First();
                Console.WriteLine("Game Found: " + game.Name);
            } else
            {
                Console.WriteLine("No games found");
            }


            //// Reference fields
            //var games = await igdb.QueryAsync<Game>(global::IGDB.Client.Endpoints.Games, query: "fields id,name,cover; where id = 4;");
            //var game = games.First();
            //game.Cover.Id.HasValue; // true
            //game.Cover.Id.Value; // 65441

            //// Expanded fields
            //var games = await igdb.QueryAsync<Game>(global::IGDB.Client.Endpoints.Games, query: "fields id,name,cover.*; where id = 4;");
            //var game = games.First();

            //// Id will not be populated but the full Cover object will be
            //game.Cover.Id.HasValue; // false
            //game.Cover.Value.Width; // 756
            //game.Cover.Value.Height;
        }
    }
}
