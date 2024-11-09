using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WFServer
{
    public class CovePlugin
    {

        private Server parentServer;

        public CovePlugin(Server parent)
        {
            parentServer = parent;
        }

        // triggered when the plugin is started
        public virtual void onInit() {}
        // triggerd 12/s
        public virtual void onUpdate() {}
        // triggered when a player speaks in anyway (exluding / commands)
        public virtual void onChatMessage(WFPlayer sender, string message) {}
        // triggerd when a player enters the server
        public virtual void onPlayerJoin(WFPlayer player) {}
        // triggered when a player leaves the server
        public virtual void onPlayerLeave(WFPlayer player) {}

        public WFPlayer[] getAllPlayers()
        {
            return parentServer.AllPlayers.ToArray();
        }

        public void sendPlayerChatMessage(WFPlayer receiver, string message, string hexColor = "ffffff")
        {
            // remove a # incase its given
            parentServer.messagePlayer(message, receiver.SteamId, hexColor.Replace("#", ""));
        }

        public void sendGlobalChatMessage(string message, string hexColor = "ffffff")
        {
            parentServer.messageGlobal(message, hexColor.Replace("#", ""));
        }

        public WFActor[] getAllServerActors()
        {
            return parentServer.serverOwnedInstances.ToArray();
        }

        public WFActor? getActorFromID(int id)
        {
            return parentServer.serverOwnedInstances.Find(a => a.InstanceID == id);
        }

        // please make sure you use the correct actorname or the game freaks out!
        public WFActor spawnServerActor(string actorType)
        {
            return parentServer.spawnGenericActor(actorType);
        }

        public void removeServerActor(WFActor actor)
        {
            parentServer.removeServerActor(actor);
        }

        // i on god dont know what this dose to the actual actor but it works in game, so if you nee this its here
        public void setServerActorZone(WFActor actor, string zoneName, int zoneOwner)
        {
            parentServer.setActorZone(actor, zoneName, zoneOwner);
        }

        public void kickPlayer(WFPlayer player)
        {
            parentServer.kickPlayer(player.SteamId);
        }

        public void log(string message)
        {
            parentServer.printPluginLog(message, this);
        }

        public bool isPlayerAdmin(WFPlayer player)
        {
            return parentServer.isPlayerAdmin(player.SteamId);
        }

        public void sendPacketToPlayer(Dictionary<string, object> packet, WFPlayer player)
        {
            parentServer.sendPacketToPlayer(packet, player.SteamId);
        }

        public void sendPacketToAll(Dictionary<string, object> packet)
        {
            parentServer.sendPacketToPlayers(packet);
        }

    }

    class PluginInstance
    {
        public CovePlugin plugin;
        public string pluginName;
        public string pluginID;
        public string pluginAuthor;

        public PluginInstance(CovePlugin pinst, string name, string id, string author)
        {
            this.plugin = pinst;
            this.pluginName = name;
            this.pluginID = id;
            this.pluginAuthor = author;
        }

    }

    public partial class Server
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
                } catch (BadImageFormatException)
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
