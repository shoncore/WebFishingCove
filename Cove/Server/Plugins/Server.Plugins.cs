using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cove.Server.Plugins;
using Cove.Server.Utils;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public readonly bool PluginsEnabled = false;

        public readonly List<PluginInstance> LoadedPlugins = [];

        /// <summary>
        /// Loads all plugins from the plugins directory.
        /// </summary>
        protected void LoadAllPlugins()
        {
            if (!PluginsEnabled)
                return;

            Console.WriteLine("\n------------ WARNING ------------");
            Console.WriteLine("YOU HAVE ENABLED PLUGINS. PLUGINS RUN CODE THAT IS NOT APPROVED OR MADE BY COVE.");
            Console.WriteLine("ANY AND ALL DAMAGE TO YOUR COMPUTER IS YOUR FAULT ALONE.");
            Console.WriteLine("DO NOT RUN ANY UNTRUSTED PLUGINS!");
            Console.WriteLine("IF YOU ARE RUNNING UNTRUSTED PLUGINS, EXIT COVE NOW.");
            Console.WriteLine("------------ WARNING ------------\n");

            Thread.Sleep(5000);

            Console.WriteLine("Loading Plugins...");

            string pluginsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

            if (!Directory.Exists(pluginsFolder))
            {
                Console.WriteLine($"Plugins folder not found at {pluginsFolder}");
                return;
            }

            var pluginAssemblies = new List<Assembly>();

            // Get all files in the plugins folder
            foreach (string fileName in Directory.GetFiles(pluginsFolder, "*.dll"))
            {
                try
                {
                    // Load the assembly
                    var assembly = Assembly.LoadFrom(fileName);
                    pluginAssemblies.Add(assembly);
                }
                catch (BadImageFormatException)
                {
                    Console.WriteLine($"File '{fileName}' is not a valid plugin!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load '{fileName}': {ex.Message}");
                }
            }

            Console.WriteLine($"Found {pluginAssemblies.Count} plugin(s)!");

            foreach (var assembly in pluginAssemblies)
            {
                // Get all types in the assembly
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.WriteLine($"Failed to load types from '{assembly.FullName}': {ex.Message}");
                    continue;
                }

                // Iterate over each type and check if it inherits from CovePlugin
                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(CovePlugin)))
                    {
                        try
                        {
                            // Create an instance of the plugin
                            var instance = Activator.CreateInstance(type, this) as CovePlugin;
                            if (instance != null)
                            {
                                // Read plugin configuration
                                string pluginConfig = ReadConfigFromPlugin($"{assembly.GetName().Name}.plugin.cfg", assembly);
                                var config = ConfigReader.ReadConfig(pluginConfig);

                                if (config.TryGetValue("name", out var name) &&
                                    config.TryGetValue("id", out var id) &&
                                    config.TryGetValue("author", out var author))
                                {
                                    var pluginInstance = new PluginInstance(instance, name, id, author);
                                    LoadedPlugins.Add(pluginInstance);
                                    Console.WriteLine($"Plugin Init: {name}");

                                    // Initialize the plugin
                                    instance.OnInit();
                                }
                                else
                                {
                                    Console.WriteLine($"Plugin '{assembly.GetName().Name}' missing required config entries.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Unable to create instance of plugin '{type.FullName}'.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading plugin '{type.FullName}': {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads the configuration file embedded in the plugin assembly.
        /// </summary>
        /// <param name="fileIdentifier">The name of the embedded config file.</param>
        /// <param name="assembly">The plugin assembly.</param>
        /// <returns>The contents of the configuration file as a string.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the config file is not found in the assembly.</exception>
        private static string ReadConfigFromPlugin(string fileIdentifier, Assembly assembly)
        {
            using Stream? fileStream = assembly.GetManifestResourceStream(fileIdentifier);
            if (fileStream != null)
            {
                using var reader = new StreamReader(fileStream);
                return reader.ReadToEnd();
            }
            else
            {
                throw new FileNotFoundException($"Plugin '{assembly.GetName().Name}' doesn't have a '{fileIdentifier}' file!");
            }
        }
    }
}
