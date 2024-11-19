namespace Cove.Server.Plugins
{
    public class PluginInstance(CovePlugin pinst, string name, string id, string author)
    {
        public CovePlugin plugin = pinst;
        public string pluginName = name;
        public string pluginID = id;
        public string pluginAuthor = author;
    }
}
