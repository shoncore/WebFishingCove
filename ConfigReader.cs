using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFSermver
{
    public class ConfigReader
    {
        public static Dictionary<string, string> ReadConfig(string fileName)
        {
            string filePath = AppDomain.CurrentDomain.BaseDirectory + fileName;
            return ReadFile(File.ReadAllText(filePath));
        }

        public static Dictionary<string, string> ReadFile(string fileContent)
        {
            string[] fileLines = fileContent.Split("\n");
            Dictionary<string, string> configValues = new Dictionary<string, string>();

            for (int i = 0; i < fileLines.Length-1; i++)
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
