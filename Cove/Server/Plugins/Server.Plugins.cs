using Cove.Server.Plugins;
using Cove.Server.Utils;
using System.Reflection;

namespace Cove.Server
{
    public partial class CoveServer
    {

        private bool arePluginsEnabled = false;

        private List<PluginInstance> loadedPlugins = new List<PluginInstance>();
        protected void loadAllPlugins()
        {

            if (!arePluginsEnabled) return; // plugins are disabled!

            Console.WriteLine("\n------------ WARNING ------------");
            Console.WriteLine("YOU HAVE ENABLED PLUGINS, PLUGINS RUN CODE THAT IS NOT APPROVED OR MADE BY COVE");
            Console.WriteLine("ANY AND ALL DAMMAGE TO YOUR COMPUTER IS YOU AND YOUR FAULT ALONE");
            Console.WriteLine("DO NOT RUN ANY UNTRUSTED PLUGINS!");
            Console.WriteLine("");
            Console.WriteLine("IF YOU ARE RUNNING UNTUSTED PLUGINS EXIT COVE NOW");
            Console.WriteLine("------------ WARNING ------------\n");

            Thread.Sleep(5000);

            Console.WriteLine("Loading Plugins...");

            string pluginsFolder = $"{AppDomain.CurrentDomain.BaseDirectory}plugins";

            List<Assembly> pluginAssemblys = new();

            // get all files in the plugins folder
            foreach (string fileName in Directory.GetFiles(pluginsFolder))
            {
                bool isAssembly = false;
                try
                {
                    AssemblyName thisFile = AssemblyName.GetAssemblyName(fileName); ;
                    isAssembly = true;
                    pluginAssemblys.Add(Assembly.LoadFrom(fileName));
                }
                catch (BadImageFormatException)
                {
                    Console.WriteLine($"File: {fileName} is not a plugin!");
                }
            }

            Console.WriteLine($"Found {pluginAssemblys.Count} plugins!");

            foreach (Assembly assembly in pluginAssemblys)
            {
                // Get all types in the assembly
                Type[] types = assembly.GetTypes();

                // Iterate over each type and check if it inherits from CovePlugin
                foreach (Type type in types)
                {
                    if (type.IsClass && type.IsSubclassOf(typeof(CovePlugin)))
                    {
                        object instance = Activator.CreateInstance(type, this);
                        CovePlugin plugin = instance as CovePlugin;
                        if (plugin != null)
                        {
                            string pluginConfig = readConfigFromPlugin($"{assembly.GetName().Name}.plugin.cfg", assembly);
                            Dictionary<string, string> config = ConfigReader.ReadFile(pluginConfig);

                            PluginInstance thisInstance = new(plugin, config["name"], config["id"], config["author"]);

                            loadedPlugins.Add(thisInstance);
                            Console.WriteLine($"Plugin Init: {config["name"]}");
                            plugin.onInit(); // start the plugin!
                        }
                        else
                            Console.WriteLine($"Unable to load {type.FullName}");
                    }
                }
            }
        }

        string readConfigFromPlugin(string fileIdentifyer, Assembly asm)
        {
            using (Stream fileStream = asm.GetManifestResourceStream(fileIdentifyer))
            {
                if (fileStream != null)
                {
                    StreamReader reader = new StreamReader(fileStream);
                    return reader.ReadToEnd();
                }
                else
                {
                    throw new Exception("Plugin dosen't have a plugin.cfg file!");
                }
            }
        }
    }
}
