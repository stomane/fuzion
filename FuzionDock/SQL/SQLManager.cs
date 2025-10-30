using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using static Fuzion.SQL.DatabaseManipulation;
using Fuzion.Programs;
using System.Collections.Generic;
using System.Text;
using Fuzion.Properties;
using System.Globalization;
using System.Net;
using System.IO;
using Fuzion.Extensions;
using Newtonsoft.Json;

namespace Fuzion.SQL
{
    public class DbConnection
    // Needs refactoring
    {

        private string databaseName = string.Empty;
        public string DatabaseName
        {
            get { return databaseName; }
            set { databaseName = value; }
        }

        public string Password { get; set; }
        private MySqlConnection connection = null;
        public MySqlConnection Connection
        {
            get { return connection; }
        }

        private static DbConnection _instance = null;
        public static DbConnection Instance()
        {
            if (_instance == null)
                _instance = new DbConnection();
            return _instance;
        }

        public bool IsConnected()
        {
            if (Connection == null)
            {
                if (String.IsNullOrEmpty(databaseName))
                    return false;
                string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Server=SERVER_IP; database={0}; UID=TABLE_UID; password={1}", databaseName, Constants.dbPassword);
                connection = new MySqlConnection(connString);
                connection.Open();
            }

            return true;
        }

        public void Close()
        {
            connection.Close();
        }

        public enum Database { Games, Programs }

        public static void PushList(List<Game> games) //Push in bulk
        {
            string databaseName = "DB_NAME";
            string server = "SERVER_IP";
            string user = "USER";
            string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

            //Build the query
            using (MySqlConnection connection = new MySqlConnection(connString))
            {
                try
                {
                    StringBuilder sBuilder = new StringBuilder("INSERT INTO `DB_TABLE`.`mainTable` (`gameName`,`iconLink`,`exeName`,`iconRelevance`) VALUES ");

                    List<string> rows = new List<string>();

                    for (int i = 0; i < games?.Count; i++)
                    {
                        rows.Add(string.Format(CultureInfo.InvariantCulture,
                            "('{0}','{1}','{2}','{3}')"
                            , MySqlHelper.EscapeString(games[i].DisplayName)
                            , MySqlHelper.EscapeString(games[i].IconURI)
                            , MySqlHelper.EscapeString(games[i].ExeName)
                            , MySqlHelper.EscapeString(10.ToString())));
                    }

                    sBuilder.Append(string.Join(",", rows));
                    sBuilder.Append(" ON DUPLICATE KEY UPDATE " +
                        "`iconLink`=IF(VALUES(`iconRelevance`)>=`iconRelevance`, IF(VALUES(`iconLink`)='',`iconLink`, VALUES(`iconLink`)),`iconLink`)" +
                        ",`exeName`= IF(VALUES(`exeName`) = '',`exeName`, VALUES(`exeName`))" +
                        ",`iconRelevance`=IF(VALUES(`iconRelevance`)>`iconRelevance` AND VALUES(`iconLink`) != '',VALUES(`iconRelevance`),`iconRelevance`);");

                    using (MySqlCommand command = new MySqlCommand())
                    {
                        Console.WriteLine("Pushing to database Games");
                        //Console.WriteLine("SQL: "+ sBuilder.ToString());

                        connection.Open();

                        command.Connection = connection;
                        command.CommandType = System.Data.CommandType.Text;
                        command.CommandText = sBuilder.ToString();
                        command.ExecuteNonQuery();
                        //command.Dispose(); // can i?
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine("Error while pushing to db: " + x.Message);
                }
            }
        }

        public static void PushList(List<Program> programs) //Push in bulk
        {
            string databaseName = "DB_NAME";
            string server = "SERVER_IP";
            string user = "USER";
            string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

            //Build the query
            using (MySqlConnection connection = new MySqlConnection(connString))
            {
                try
                {
                    StringBuilder sBuilder = new StringBuilder("INSERT INTO `DB_TABLE`.`programsTable` (`name`,`iconLink`,`exeName`) VALUES ");

                    List<string> rows = new List<string>();

                    for (int i = 0; i < programs?.Count; i++)
                    {
                        rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "('{0}','{1}','{2}')", MySqlHelper.EscapeString(programs[i].DisplayName), MySqlHelper.EscapeString(programs[i].IconURI), MySqlHelper.EscapeString(programs[i].ExeName)));
                    }
                    sBuilder.Append(string.Join(",", rows));
                    sBuilder.Append(" ON DUPLICATE KEY UPDATE `iconLink`=VALUES(`iconLink`),`exeName`=VALUES(`exeName`);");

                    using (MySqlCommand command = new MySqlCommand())
                    {
                        Console.WriteLine("Pushing to database Programs");

                        connection.Open();

                        command.Connection = connection;
                        command.CommandType = System.Data.CommandType.Text;
                        command.CommandText = sBuilder.ToString();
                        command.ExecuteNonQuery();
                        //command.Dispose(); // can i?
                    }
                }
                catch (MySqlException)
                {

                }
            }


        }

        public static void ManipulateDatabase(string name, string icon, string exe, Database db = Database.Games) //Push single
        {
            string databaseName = "DB_NAME";
            string server = "SERVER_IP";
            string user = "USER";
            string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture ,
                "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

            if(db == Database.Games)
            {
                //Build the query
                using (MySqlConnection connection = new MySqlConnection(connString))
                {
                    try
                    {
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            Console.WriteLine("Pushing to database Games");

                            connection.Open();

                            command.Connection = connection;
                            command.CommandText = "INSERT INTO `DB_TABLE`.`mainTable` (`gameName`,`iconLink`,`exeName`) VALUES (@gameName, @gameIcon, @exeName) ON DUPLICATE KEY UPDATE index = index;"; // gameName = @gameName, iconLink = @gameIcon, exeName = @exeName;";
                            command.Parameters.AddWithValue("@gameName", name);
                            command.Parameters.AddWithValue("@gameIcon", icon);
                            command.Parameters.AddWithValue("@exeName", exe);
                            command.ExecuteNonQuery();
                            //command.Dispose(); // can i?
                        }
                    }
                    catch (MySqlException)
                    {

                    }
                }
            }

            if(db == Database.Programs)
            {
                //Build the query
                using (MySqlConnection connection = new MySqlConnection(connString))
                {
                    try
                    {
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            Console.WriteLine("Pushing to database Programs");

                            connection.Open();

                            command.Connection = connection;
                            command.CommandText = "INSERT INTO `DB_TABLE`.`programsTable` (`name`,`iconLink`,`exeName`) VALUES (@gameName, @gameIcon, @exeName) ON DUPLICATE KEY UPDATE index = index;";
                            command.Parameters.AddWithValue("@gameName", name);
                            command.Parameters.AddWithValue("@gameIcon", icon);
                            command.Parameters.AddWithValue("@exeName", exe);
                            command.ExecuteNonQuery();
                            //command.Dispose(); // can i?
                        }
                    }
                    catch (MySqlException)
                    {

                    }
                }
            }
           
        }

        public static async void ManipulateDatabase() //Get
        {
            string databaseName = "DB_NAME";
            string server = "SERVER_IP";
            string user = "USER";
            string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

            using (MySqlConnection connection = new MySqlConnection(connString))
            {
                try
                {
                    connection.Open();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = "SELECT * FROM DB_TABLE.mainTable;";

                        var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                        while (reader.Read())
                        {
                            dbGameNames.Add(reader.GetString(0));
                            dbIconPaths.Add(reader.GetString(1));
                            dbExeNames.Add(reader.GetString(2));
                        }
                        reader.Close();
                    }
                }
                catch (MySqlException)
                {
                    //Exception
                }

            }

            Console.WriteLine("Get from DB finished");
        }

        public static bool GameExistsInDatabase(string gameName)
        {
            try
            {
                return FuzionDB.GameExistsInDatabase(gameName);
            }
            catch (Exception) // if it fails, try to reach the old web db
            {
                #region Old DB code
                string databaseName = "DB_NAME";
                string server = "SERVER_IP";
                string user = "USER";
                string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

                using (MySqlConnection connection = new MySqlConnection(connString))
                {
                    try
                    {
                        connection.Open();
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            command.Connection = connection;
                            command.Parameters.AddWithValue("@g", gameName);
                            command.Parameters.AddWithValue("@f", 0);
                            command.CommandText = "SELECT * FROM DB_TABLE.mainTable WHERE gameName = @g AND falsePositive = @f;";

                            var reader = command.ExecuteReader();
                            if (reader.HasRows)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                #endregion
            }
        }

        public static bool ProgramExistsInDatabase(string name)
        {
            try
            {
                return FuzionDB.ProgramExistsInDatabase(name);
            }
            catch (Exception) //try the old web db if it fails
            {

                string databaseName = "DB_NAME";
                string server = "SERVER_IP";
                string user = "USER";
                string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

                using (MySqlConnection connection = new MySqlConnection(connString))
                {
                    try
                    {
                        connection.Open();
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            command.Connection = connection;
                            command.Parameters.AddWithValue("@p", name);
                            command.Parameters.AddWithValue("f", 0);
                            command.CommandText = "SELECT * FROM DB_TABLE.programsTable WHERE name = @p AND falsePositive = @f;";

                            var reader = command.ExecuteReader();
                            if (reader.HasRows)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    catch (MySqlException)
                    {
                        return false;
                    }
                }
            }
        }

        public static Tuple<string,int> GetIconDataTuple(string name)
        {
            var result = Tuple.Create(string.Empty, 0);

            try
            {
                return FuzionDB.GetIconTuple(name);
            }
            catch (Exception) // check web db if fuzion db fails
            {
                string databaseName = "DB_NAME";
                string server = "SERVER_IP";
                string user = "USER";
                string connString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Server={0}; database={1}; UID={2}; password={3}", server, databaseName, user, Constants.dbPassword);

                using (MySqlConnection connection = new MySqlConnection(connString))
                {
                    try
                    {
                        connection.Open();
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            command.Connection = connection;
                            command.Parameters.AddWithValue("@p", name);
                            //command.Parameters.AddWithValue("f", 0); //unused
                            command.CommandText = "SELECT * FROM DB_TABLE.mainTable WHERE gameName = @p";

                            var reader = command.ExecuteReader();
                            if (reader.HasRows)
                            {
                                // entry exists, get icon link
                                reader.Read();
                                result = Tuple.Create(reader.GetString("iconLink"), reader.GetInt32("iconRelevance"));
                                Console.WriteLine("MYSQL Icon Link: " + result.Item1 + " and Icon Relevance" + result.Item2);
                            }
                        }
                    }
                    catch (MySqlException mx)
                    {
                        Console.WriteLine("SQL Icon get failed: " + mx.Message);
                    }
                }

                return result;
            }

        }


    }
}
