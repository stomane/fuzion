using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Programs
{
    public static class LocalGameData
    {
        public static List<string> Names = new List<string>();
        public static List<string> Paths = new List<string>();
        public static List<string> Icons = new List<string>();
        public static List<string> Arguments = new List<string>();
        public static List<string> PathType = new List<string>();

        public static void AddEmptyEntry()
        {
            Names.Add("");
            Paths.Add("");
            Icons.Add("");
            Arguments.Add("");
            PathType.Add("");
        }

        public static void Add(string name, string path, string icon, string arguments, string pathType)
        {
            Names.Add(name);
            Paths.Add(path);
            Icons.Add(icon);
            Arguments.Add(arguments);
            PathType.Add(pathType);
        }
    }
}
