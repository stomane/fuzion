using System;
using System.Collections.Generic;
using Fuzion.Programs;

namespace Fuzion.LauncherSpecific
{
    static class LauncherManager
    {
        private static readonly List<string> launcherNames = new List<string> {"STEAM", "ORIGIN", "BATTLE.NET", "EPIC GAMES", "GOG", "UPLAY" };
        public static List<string> installedLaunchers = new List<string>();

        public static void RestoreLaunchersFromProgramList(List<Program> programList)
        {
            for (int i = 0; i < programList.Count; i++)
            {
                if (launcherNames.Contains(programList[i].DisplayName.ToUpperInvariant()))
                {
                    installedLaunchers.Add(programList[i].DisplayName);
                    Console.WriteLine($"Launcher restored {i}: {programList[i].DisplayName}");
                }
            }
        }

        public static void CloseAnyRunningLaunchers()
        {
            Steam.Close();
            EpicGames.Close();
            Uplay.Close();
            Origin.Close();

            GOG.CloseGOG();
            BattleNet.CloseBattleNetMainWindow();
        }
    }
}
