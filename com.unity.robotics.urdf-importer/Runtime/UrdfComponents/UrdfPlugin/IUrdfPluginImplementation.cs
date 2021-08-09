using System.Xml;

namespace Unity.Robotics.UrdfImporter
{
    public interface IUrdfPluginImplementation
    {

        /**
         * Used to define the type of plugin.
         */
        string PluginFilename
        {
            get;
        }
        
        /**
         * The name of the plugin will end up being:
         * {robot_name}_{PluginNamePostfix}
         */
        string PluginNamePostfix
        {
            get;
        }
        
        void BuildExportPluginData(XmlDocument xmlDocument, XmlElement pluginXmlElement);
        
        void DecodeExportPlugin(XmlDocument pluginDescriptionXml);

    }
}