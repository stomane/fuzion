using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fuzion.Programs;

namespace Fuzion.SQL
{
    /// <summary>
    /// Facade the rest of the app calls for shared game/program metadata. Everything goes to
    /// the Fuzion backend over HTTP via <see cref="FuzionDB"/>.
    ///
    /// This used to fall back to a self-hosted MySQL database when the HTTP call failed. That
    /// database is gone - the fallback still carried placeholder connection details
    /// (SERVER_IP / DB_NAME / USER), so it could not have connected for a long time - and with
    /// it went the MySql.Data dependency and the DB_PASSWORD setting.
    /// </summary>
    public class DbConnection
    {
        public static void PushList(List<Game> games)
        {
            FuzionDB.PushList(games);
        }

        public static void PushList(List<Program> programs)
        {
            FuzionDB.PushList(programs);
        }

        public static bool GameExistsInDatabase(string gameName)
        {
            return FuzionDB.GameExistsInDatabase(gameName);
        }

        public static bool ProgramExistsInDatabase(string name)
        {
            return FuzionDB.ProgramExistsInDatabase(name);
        }

        public static Task<Tuple<string, int>> GetIconDataTupleAsync(string name)
        {
            return Task.FromResult(FuzionDB.GetIconTuple(name));
        }
    }
}
