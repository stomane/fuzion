using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Programs.Serialization;
using Fuzion.Extensions;

namespace Fuzion.Programs
{
    static class LocalDatabase
    {
        public static readonly string Path = System.IO.Path.Combine(Fuzion.MainWindow.DefaultAssetPath, @"db\");
        public const string ProgramsFileName = "programs.fzn";
        public const string GamesFileName = "games.fzn";
        private static readonly List<Program> dbPrograms = DeserializeListFromPath(Path + ProgramsFileName);
        private static readonly List<Program> dbGames = DeserializeListFromPath(Path + GamesFileName);
        private static List<Game> dbGamesOnline = new List<Game>();


        static LocalDatabase()
        {                
            Directory.CreateDirectory(Path);
        }

        public static async void UpdateDatabaseProgramsAsync(bool pushToWeb = false)
        {
            if (ProgramObjects != null && ProgramObjects.Count != 0)
            {
                dbPrograms.Clear();

                await Task.Run(() =>
                {
                    for (int i = 0; i < ProgramObjects.Count; i++)
                    {
                        // Overwrite the database instead of adding to it due to new scoring system and online database
                        if (ProgramObjects[i].IsGame == false)
                        {
                            dbPrograms.Add(ProgramObjects[i]);
                        }
                    }

                    if (pushToWeb)
                    {
                        SQL.DbConnection.PushList(dbPrograms);
                    }
                  
                }).ConfigureAwait(false);

                SerializeList(dbPrograms, Path, ProgramsFileName);
                Console.WriteLine("Local & Online Program Database updated count: "+dbPrograms.Count);
            }

            // New database ready bool will signify which gameobjects can be pushed from now on
            if (GameObjects != null && GameObjects.Count != 0)
            {
                // Clear local file and overwrite
                dbGames.Clear();
                // Online database needs to be a list of games not programs because the PushList overload will auto push to Games DB
                dbGamesOnline.Clear();

                await Task.Run(() =>
                {
                    for (int i = 0; i < GameObjects.Count; i++)
                    {
                        // Add only if marked IsGame which only Fuzion can do
                        if (GameObjects[i].IsGame)
                        {
                            dbGames.Add(GameObjects[i].ToProgram());

                            //if (GameObjects[i].DatabaseReady)
                            //{
                            //    dbGamesOnline.Add(GameObjects[i]);
                            //}
                        }
                    }


                    // only recently added database ready games for online db
                    if (RecentlyAddedGames != null && RecentlyAddedGames.Count > 0)
                    {
                        //for (int i = 0; i < RecentlyAddedGames.Count; i++)
                        //{
                        //    Console.WriteLine("RAG URI "+RecentlyAddedGames[i].IconURI);
                        //    dbGamesOnline.Add(RecentlyAddedGames[i]);
                        //}

                        //// redundant, can push recentlyaddedgames directly
                        //dbGamesOnline = RecentlyAddedGames.ToList();

                        if (pushToWeb)
                        {
                            //for (int i = 0; i < dbGamesOnline.Count; i++)
                            //{
                            //    Console.WriteLine("Db Games Online "+dbGamesOnline[i].DisplayName);
                            //    Console.WriteLine("Db Games Online URI "+dbGamesOnline[i].IconURI);
                            //}

                            // Web database
                            SQL.DbConnection.PushList(RecentlyAddedGames);
                            // Vanx database
                            SQL.FuzionDB.PushList(RecentlyAddedGames);
                        }
                    }

                    //// Remove programs with the same name as objects in the grid
                    //foreach (Game g in gameObjects)
                    //{
                    //    if (dbPrograms.Any(p => p.DisplayName == g.DisplayName))
                    //    {
                    //        dbPrograms.Remove(dbPrograms.FirstOrDefault(p => p.DisplayName == g.DisplayName));
                    //    }
                    //}
                }).ConfigureAwait(false);

                SerializeList(dbGames, Path, GamesFileName);
                Console.WriteLine("Local Game Database updated count: " + dbGames.Count);
                Console.WriteLine("Online Game Database updated count: " + dbGamesOnline.Count);
            }
        }

        public static bool IsProgram(Program prog)
        {
            return dbPrograms.Any(p => p.DisplayName == prog?.DisplayName);
        }

        public static bool IsProgram(string progName)
        {
            if(dbPrograms == null || dbPrograms.Count == 0)
            {
                return false;
            }

            return dbPrograms.Any(p => p.DisplayName == progName);
        }

        public static bool IsGame(string progName)
        {
            if(dbGames == null || dbGames.Count == 0)
            {
                return false;
            }

            return dbGames.Any(g => g.DisplayName == progName);
        }
    }
}
