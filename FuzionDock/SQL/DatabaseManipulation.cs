using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.SQL
{
    class DatabaseManipulation
    {
        public static List<string> dbGameNames = new List<string>();
        public static List<string> dbIconPaths = new List<string>();
        public static List<string> dbExeNames = new List<string>();

        //private static void LookForProgramInDatabase(string programName, int index, string programPath)
        //{
        //    if (dbGameNames.Contains(programName) && !programName.ToLower().Equals("unity") && !programName.ToLower().Equals("twitch"))
        //    {
        //        gamesList.Add(programName);
        //        gamePaths.Add(programPath);
        //        workDirPaths.Add(programPath);
        //        dbExeNames.Add(""); // add empty and then replace index with most apropriate exe
        //    }
        //}

        //private void PopulateDatabase()
        //{
        //    for (int i = 0; i < gamesList.Count; i++)
        //    {
        //        SQL.DbConnection.ManipulateDatabase(gamesList[i], dbIconPaths[i], dbExeNames[i]);
        //    }
        //}
    }
}
