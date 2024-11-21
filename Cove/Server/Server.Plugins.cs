using System.Reflection;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public bool PluginsEnabled = false;

        public readonly List<PluginInstance> LoadedPlugins = [];

        /// <summary>
        /// Loads all plugins from the plugins directory.
        /// </summary>
        protected void LoadAllPlugins()
        {
            if (!PluginsEnabled)
                return;

            Logger.LogInformation(
                """
------------ WARNING ------------
YOU HAVE ENABLED PLUGINS. PLUGINS RUN CODE THAT IS NOT APPROVED OR MADE BY COVE.
ANY AND ALL DAMAGE TO YOUR COMPUTER IS YOUR FAULT ALONE.
DO NOT RUN ANY UNTRUSTED PLUGINS!
IF YOU ARE RUNNING UNTRUSTED PLUGINS, EXIT COVE NOW.
------------ WARNING ------------
"""
            );

            Thread.Sleep(5000);

            Logger.LogInformation("Loading Plugins...");

            string pluginsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

            if (!Directory.Exists(pluginsFolder))
            {
                Logger.LogInformation("Plugins folder not found at {Folder}", pluginsFolder);
                return;
            }

            var pluginAssemblies = new List<Assembly>();

            foreach (string fileName in Directory.GetFiles(pluginsFolder, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(fileName);
                    pluginAssemblies.Add(assembly);
                }
                catch (BadImageFormatException)
                {
                    Logger.LogError("File {File} is not a valid plugin!", fileName);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load plugin.");
                }
            }

            Logger.LogInformation("Found {Count} plugin(s)!", pluginAssemblies.Count);

            foreach (var assembly in pluginAssemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Logger.LogError(ex, "Failed to load types from {Assembly}", assembly.FullName);
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(CovePlugin)))
                    {
                        try
                        {
                            if (Activator.CreateInstance(type, this) is CovePlugin instance)
                            {
                                string pluginConfigContent = ReadConfigFromPlugin(
                                    $"{assembly.GetName().Name}.plugin.cfg",
                                    assembly
                                );

                                var configReader = new ConfigReader(
                                    LoggerFactory.CreateLogger<ConfigReader>()
                                );
                                var config = configReader.ReadConfigFromString(pluginConfigContent);

                                if (
                                    config.TryGetValue("name", out var name)
                                    && config.TryGetValue("id", out var id)
                                    && config.TryGetValue("author", out var author)
                                )
                                {
                                    var pluginInstance = new PluginInstance(
                                        instance,
                                        name,
                                        id,
                                        author
                                    );
                                    LoadedPlugins.Add(pluginInstance);
                                    Logger.LogInformation("Plugin Initialized: {Plugin}", name);

                                    // Initialize the plugin
                                    instance.OnInit();
                                }
                                else
                                {
                                    Logger.LogWarning(
                                        "Plugin '{Plugin}' missing required config entries.",
                                        assembly.GetName().Name
                                    );
                                }
                            }
                            else
                            {
                                Logger.LogWarning(
                                    "Unable to create instance of plugin {Type}",
                                    type.FullName
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error loading plugin {Type}", type.FullName);
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
        private string ReadConfigFromPlugin(string fileIdentifier, Assembly assembly)
        {
            var resourceNames = assembly.GetManifestResourceNames();
            Logger.LogInformation(
                "Resources found in {Assembly}: {Resources}",
                assembly.FullName,
                string.Join(", ", resourceNames)
            );

            using Stream? fileStream = assembly.GetManifestResourceStream(fileIdentifier);
            if (fileStream != null)
            {
                using var reader = new StreamReader(fileStream);
                return reader.ReadToEnd();
            }

            throw new FileNotFoundException(
                $"Plugin '{assembly.GetName().Name}' doesn't have a '{fileIdentifier}' file!"
            );
        }
    }
}
