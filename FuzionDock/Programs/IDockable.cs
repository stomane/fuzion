using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Programs
{
    interface IDockable
    {
        bool IsGame { get; set; }
        bool IsUserModified { get; set; }
        string DisplayName { get; set; }
        string DockName { get; set; }
        string Path { get; set; }
        string Arguments { get; set; }
        string UninstallPath { get; set; }
        string WorkDir { get; set; }
        string Icon { get; set; }
        string ChangedIcon { get; set; }
        string SystemIcon { get; set; }
        string ExeName { get; set; }
        string EpicAppName { get; set; }
        string SteamAppID { get; set; }
        string UWPAppID { get; set; }
        string IconURI { get; set; }
        string IconGUID { get; }

        // Originals
        string OriginalPath { get; set; }
        string OriginalArguments { get; set; }
        string OriginalIcon { get; }

        int Index { get; set; }

        BelongsToLauncher Launcher { get; set; }
        BelongsToLauncher OriginalLauncher { get; set; }
        PathType PathType { get; set; }
        PathType OriginalPathType { get; set; }
    }
}
