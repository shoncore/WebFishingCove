namespace Cove.Server.Plugins
{
    public class PluginInstance
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
}
