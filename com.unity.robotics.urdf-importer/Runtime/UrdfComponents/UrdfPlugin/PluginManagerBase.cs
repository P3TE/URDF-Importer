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

        public const string _GazeboTag = "gazebo";
        public const string _PluginTag = "plugin";
        public const string _FilenameAttribute = "filename";
        public const string _NameAttribute = "name";
        public const string _LinkNameElement = "linkName";

        public class PluginData
        {
            //Used for determining the type of plugin.
            public string filename;
            //Generic name for the plugin.
            public string name = "";
            //Used for deserialising the xml.
            public XElement innerPluginXml;
            
            //To neaten the layout, the following parameters are used to define what game object the plugin is added to. 
            public string urdfLinkName = "";
            public UrdfLink urdfLink = null;
            public GameObject fallbackGameObjectParent = null;

            //Used when creating the new Component.
            public GameObject ObjectToAttachTo
            {
                get
                {
                    if (urdfLink != null)
                    {
                        return urdfLink.gameObject;
                    }
                    return fallbackGameObjectParent;
                }
            }
        }

        public UrdfPluginImplementation GeneratePlugin(UrdfPluginDescription pluginDescription)
        {
            PluginData pluginData = new PluginData();
            XElement xmlElement = XElement.Parse(pluginDescription.text);
            pluginData.innerPluginXml = xmlElement.Element(_PluginTag);
            if (pluginData.innerPluginXml == null)
            {
                RuntimeUrdf.AddImportWarning($"Plugin of type {xmlElement.Name} lacks a child of type {_PluginTag} and was ignored!");
                return null;
            }
            
            XAttribute filenameAttribute = pluginData.innerPluginXml.Attribute(_FilenameAttribute);
            if (filenameAttribute == null)
            {
                RuntimeUrdf.AddImportWarning($"Plugin of type {xmlElement.Name}:{_PluginTag} is missing attribute {_FilenameAttribute} and will be ignored!");
                return null;
            }
            pluginData.filename = filenameAttribute.Value;
            XAttribute nameAttribute = pluginData.innerPluginXml.Attribute(_NameAttribute);
            if (nameAttribute != null)
            {
                pluginData.name = filenameAttribute.Value;
            }
            
            //Find the link if applicable.
            if (UrdfPluginImplementation.ReadStringFromXElement(pluginData.innerPluginXml, _LinkNameElement,
                out pluginData.urdfLinkName, false))
            {
                //A valid link name exists.
                UrdfLinkExtensions.TryFindLink(pluginData.urdfLinkName, out pluginData.urdfLink);
            }

            if (PluginFactories.TryGetValue(pluginData.filename, out GeneratePluginDelegate generatePluginDelegate))
            {
                UrdfPluginImplementation result = generatePluginDelegate(pluginData);
                if (result == null)
                {
                    throw new Exception($"Failed to generate plugin with filename {pluginData.filename}");
                }
                result.DeserialiseFromXml(pluginData.innerPluginXml);
                return result;
            }
            
            RuntimeUrdf.AddImportWarning($"No plugin implementation for plugin with filename {pluginData.filename} it will be ignored!");

            return null;
        }

    }
}