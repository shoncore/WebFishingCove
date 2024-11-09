using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WFServer
{
    public class ConfigReader
    {
        public static Dictionary<string, string> ReadConfig(string fileName)
        {
            string filePath = AppDomain.CurrentDomain.BaseDirectory + fileName;
            if (File.Exists(filePath))
            {
                return ReadFile(File.ReadAllText(filePath));
            } else
            {
                bool assemblyHasConfig = false;
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name;
                Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList().ForEach(name =>
                {
                    if (name == resourceName + "." + fileName)
                    {
                        assemblyHasConfig = true;
                    }
                });
                if (assemblyHasConfig)
                {
                    string fileContence = readFromAssembly(resourceName + "." + fileName);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Using default {fileName}, it has been added to the root directory for editing!");
                    Console.ResetColor();

                    File.WriteAllText(filePath, fileContence );

                    return ReadFile(fileContence);
                } else
                {
                    throw new Exception($"Cannot find config file that is trying to be read: {fileName}");
                }
            }
        }

        private static string readFromAssembly(string fileIdentifyer)
        {

            using (Stream fileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileIdentifyer))
            {
                if (fileStream != null)
                {
                    StreamReader reader = new StreamReader(fileStream);
                    string content = reader.ReadToEnd();

                    return content;

                } else
                {
                    throw new Exception("Cant file file in sssembly!");
                }
            }

        }

        public static Dictionary<string, string> ReadFile(string fileContent)
        {
            string[] fileLines = fileContent.Split("\n");
            Dictionary<string, string> configValues = new Dictionary<string, string>();

            for (int i = 0; i < fileLines.Length; i++)
            {
                string line = fileLines[i].Trim();
                // if the line is a comment
                if (line.ToCharArray().Length == 0 || line.Substring(0, 1) == "#")
                {
                    continue;
                }

                string[] parts = line.Split("=");
                configValues[parts[0].Trim()] = parts[1].Trim();

            }

            return configValues;
        }
    }
}
