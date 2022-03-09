using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class PluginManagerBase : MonoBehaviour
    {

        public static PluginManagerBase Instance
        {
            get;
            protected set;
        }
        
        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple instances of {GetType().Name}");
            }
            Instance = this;
        }

        public delegate UrdfPluginImplementation GeneratePluginDelegate(PluginData pluginData);

        public abstract Dictionary<string, GeneratePluginDelegate> BuildPluginFactories();

        private Dictionary<string, GeneratePluginDelegate> pluginFactories = null;

        private Dictionary<string, GeneratePluginDelegate> PluginFactories
        {
            get
            {
                if (pluginFactories == null)
                {
                    pluginFactories = BuildPluginFactories();
                }
                return pluginFactories;
            }
        }

        public const string _PluginTag = "plugin";
        public const string _FilenameAttribute = "filename";
        public const string _NameAttribute = "name";

        public class PluginData
        {
            //Used for determining the type of plugin.
            public string filename;
            //Generic name for the plugin.
            public string name = "";
            //Used for deserialising the xml.
            public XElement innerPluginXml;
            
            public UrdfPlugins urdfPlugins;

            public UrdfPluginDescription pluginDescription;
            public XElement xmlElement;

            public PluginData(UrdfPlugins urdfPlugins, UrdfPluginDescription pluginDescription)
            {
                this.urdfPlugins = urdfPlugins;
                this.pluginDescription = pluginDescription;
                xmlElement = XElement.Parse(pluginDescription.text);
            }

            //Used when creating the new Component.
            public GameObject ObjectToAttachTo => urdfPlugins.gameObject;
        }

        public UrdfPluginImplementation GeneratePlugin(PluginData pluginData, XElement innerPluginXml)
        {

            XAttribute filenameAttribute = innerPluginXml.Attribute(_FilenameAttribute);
            if (filenameAttribute == null)
            {
                RuntimeUrdf.AddImportWarning($"Plugin of type {innerPluginXml.Name}:{_PluginTag} is missing attribute {_FilenameAttribute} and will be ignored!");
                return null;
            }
            pluginData.filename = filenameAttribute.Value;
            XAttribute nameAttribute = innerPluginXml.Attribute(_NameAttribute);
            if (nameAttribute != null)
            {
                pluginData.name = filenameAttribute.Value;
            }

            if (PluginFactories.TryGetValue(pluginData.filename, out GeneratePluginDelegate generatePluginDelegate))
            {
                UrdfPluginImplementation result = generatePluginDelegate(pluginData);
                if (result == null)
                {
                    throw new Exception($"Failed to generate plugin with filename {pluginData.filename}");
                }

                result.ImplementationPluginData = pluginData;
                result.DeserialiseFromXml(innerPluginXml);
                return result;
            }
            
            RuntimeUrdf.AddImportWarning($"No plugin implementation for plugin with filename {pluginData.filename} it will be ignored!");

            return null;
        }

    }
}