using Fuzion.Programs;
using System;
using System.Collections.Generic;

namespace Fuzion.LauncherSpecific
{
    class UWP
    {
        public static List<Program> GetUWPGames()
        {
            // UWP support disabled for standalone build
            return new List<Program>();
        }

        public static void UWPUpdateProgramObjects(List<Program> uwpProgramList, List<Program> listToUpdate)
        {
            // No-op
        }

        public static void LaunchUWPGame()
        {
            // No-op
        }
    }
}
