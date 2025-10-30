using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Fuzion.Extensions;
using Fuzion.WindowsManager;
using Newtonsoft.Json;

namespace Fuzion.Programs
{
    class Serialization
    {
        public enum TargetXMLFile { Programs, Games }

        /// <summary>
        /// Serialize a list of Programs to file and read it later using DeserializedList(). Can't serialize UIElement, that's why this must remain
        /// a Program list.
        /// </summary>
        /// <param name="list">The list to serialize</param>
        /// <param name="targetFile">Should it save to programs.xml or games.xml</param>
        public static void SerializeList(List<Program> list)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Program>));
            using (StringWriter sWriter = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sWriter))
                {
                    xmlSerializer.Serialize(writer, list);
                    string xml = sWriter.ToString();
                    WriteXMLFile(xml, "programs.xml");
                }
            }
        }

        public static void SerializeList(List<Program> list, string path, string filename)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Program>));
            using (StringWriter sWriter = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sWriter))
                {
                    xmlSerializer.Serialize(writer, list);
                    string xml = sWriter.ToString();
                    WriteXMLFile(xml, path, filename);
                }
            }
        }

        public static void SerializeList(List<Game> list)
        {
            List<Program> serializeThis = new List<Program>();

            for (int i = 0; i < list.Count; i++)
            {
                serializeThis.Add(list[i].ToProgram());
            }

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Program>));
            using (StringWriter sWriter = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sWriter))
                {
                    xmlSerializer.Serialize(writer, serializeThis);
                    string xml = sWriter.ToString();
                    WriteXMLFile(xml, "games.xml");
                }
            }
        }

        // REENABLE FOR ONLINE ICONS
        //public static List<Dictionary<string, List<string>>> OnlineIconsList { get; private set; } = DeserializeOnlineIconList();

        // REENABLE FOR ONLINE ICONS
        //public static void SerializeOnlineIconList(string name, List<string> list)
        //{
        //    var serializeThis = new Dictionary<string, List<string>> { { name, list } };

        //    OnlineIconsList.Add(serializeThis);

        //    //open file stream
        //    using (StreamWriter file = File.CreateText(Path.Combine(MainWindow.DefaultAssetPath, "db", "onlineicons.json")))
        //    {
        //        JsonSerializer serializer = new JsonSerializer();
        //        serializer.Serialize(file, OnlineIconsList);
        //    }
        //}

        public static List<Dictionary<string,List<string>>> DeserializeOnlineIconList()
        {
            while (true)
            {
                if (File.Exists(Path.Combine(MainWindow.DefaultAssetPath, "db", "onlineicons.json")))
                {
                    string deserializeThis = File.ReadAllText(Path.Combine(MainWindow.DefaultAssetPath, "db", "onlineicons.json"));
                    var dict = JsonConvert.DeserializeObject<List<Dictionary<string, List<string>>>>(deserializeThis);

                    if (dict == null)
                        break;

                    return dict;
                }
            }
        

            // create empty list
            using (StreamWriter file = File.CreateText(Path.Combine(MainWindow.DefaultAssetPath, "db", "onlineicons.json")))
            {
                JsonSerializer serializer = new JsonSerializer();
                var res = new List<Dictionary<string, List<string>>>();
                serializer.Serialize(file, res);
                return res;
            }
        }

        private static void WriteXMLFile(string xml, string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                OpenWindow.Notification("Cannot save current program list because target filename is: " + filename);
            }
            else
            {
                string programsPath = Fuzion.MainWindow.DefaultAssetPath + @"programs\";

                if (!Directory.Exists(programsPath))
                {
                    Directory.CreateDirectory(programsPath);
                }

                using (StreamWriter writer = File.CreateText(programsPath + filename))
                {
                    writer.Write(xml);
                }
            }
        }

        private static void WriteXMLFile(string xml, string path, string filename)
        {
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(path))
            {
                OpenWindow.Notification("Cannot save current program list because target is: " + path + filename);
            }
            else
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                using (StreamWriter writer = File.CreateText(path + filename))
                {
                    writer.Write(xml);
                }
            }
        }

        public static List<Program> DeserializedList(TargetXMLFile targetFile)
        {
            List<Program> result = new List<Program>();
            string programsFilePath;

            if (targetFile == TargetXMLFile.Programs)
            {
                programsFilePath = Fuzion.MainWindow.DefaultAssetPath + @"programs\" + "programs.xml";
            }
            else
            {
                programsFilePath = Fuzion.MainWindow.DefaultAssetPath + @"programs\" + "games.xml";
            }

            if (File.Exists(programsFilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Program>), new XmlRootAttribute("ArrayOfProgram"));
                string xml = File.ReadAllText(programsFilePath);
                StringReader stringReader = new StringReader(xml);
                result = (List<Program>)serializer.Deserialize(stringReader);
                stringReader.Dispose();
            }

            return result;
        }

        public static List<Program> DeserializeListFromPath(string programsFilePath)
        {
            List<Program> result = new List<Program>();

            try
            {
                if (File.Exists(programsFilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<Program>), new XmlRootAttribute("ArrayOfProgram"));
                    string xml = File.ReadAllText(programsFilePath);
                    StringReader stringReader = new StringReader(xml);
                    result = (List<Program>)serializer.Deserialize(stringReader);
                    stringReader.Dispose();
                }
            }
            catch (Exception)
            {

            }

            return result;
        }

        // Convert all programs to games (does not sort)
        public static List<Game> DeserializedConvertedGamesList()
        {
            List<Game> result = new List<Game>();

            string programsFilePath = Fuzion.MainWindow.DefaultAssetPath + @"programs\" + "games.xml";

            if (File.Exists(programsFilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Program>), new XmlRootAttribute("ArrayOfProgram"));
                string xml = File.ReadAllText(programsFilePath);
                StringReader stringReader = new StringReader(xml);
                List<Program> temporary = (List<Program>)serializer.Deserialize(stringReader);
                stringReader.Dispose();

                for (int i = 0; i < temporary.Count; i++)
                {
                    result.Add(temporary[i].ToGame());
                    Console.WriteLine("Deserialized URI for " + result[i].DisplayName + " is");
                    Console.WriteLine(result[i].IconURI);
                }
            }

            return result;
        }


    }
}
