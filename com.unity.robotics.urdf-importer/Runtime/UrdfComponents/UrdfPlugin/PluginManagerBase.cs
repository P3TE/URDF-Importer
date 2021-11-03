using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class PluginManagerBase : MonoBehaviour
    {

        public abstract Dictionary<string, Type> ImplementedPlugins
        {
            get;
        }

        public const string _GazeboTag = "gazebo";
        public const string _PluginTag = "plugin";
        public const string _FilenameAttribute = "filename";
        public const string _NameAttribute = "name";

        public UrdfPluginImplementationOld GeneratePlugin(UrdfPluginDescription pluginDescription)
        {

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(pluginDescription.text);

            XmlNode gazeboNode = xmlDocument.DocumentElement.SelectSingleNode(_GazeboTag);
            if (gazeboNode == null)
            {
                Debug.LogError($"Plugin must begin with {_GazeboTag}");
                return null;
            }
            
            XmlNode pluginNode = gazeboNode.SelectSingleNode(_PluginTag);
            if (pluginNode == null)
            {
                Debug.LogError($"Plugin must contain a {_PluginTag} tag");
                return null;
            }

            string filenameAttribute = pluginNode.Attributes[_FilenameAttribute]?.InnerText;
            if (filenameAttribute == null)
            {
                Debug.LogError($"Plugin must contain a {_FilenameAttribute} attribute");
                return null;
            }
            
            string nameAttribute = pluginNode.Attributes[_NameAttribute]?.InnerText;
            if (nameAttribute == null)
            {
                nameAttribute = "";
            }

            if (!ImplementedPlugins.ContainsKey(filenameAttribute))
            {
                return null;
            }

            Type pluginType = ImplementedPlugins[filenameAttribute];

            bool validPlugin = false;
            foreach (Type typeInterface in pluginType.GetInterfaces())
            {
                if (typeInterface == typeof(UrdfPluginImplementationOld))
                {
                    validPlugin = true;
                    break;
                }
            }

            if (!validPlugin)
            {
                Debug.LogError($"Plugin with filename {filenameAttribute} is invalid, " +
                               $"it must extend {nameof(UrdfPluginImplementationOld)}");
                return null;
            }
            
            throw new NotImplementedException();

        }

        public static UrdfPluginDescription BuildXmlDocument(UrdfPluginImplementationOld plugin, string robotName = "")
        {
            XmlDocument pluginDocument = new XmlDocument();
            plugin.BuildExportData(robotName, pluginDocument);
            
            return new UrdfPluginDescription(pluginDocument.InnerXml);
        }

        /*const string _UrdfPluginBuilderPrefabName = "UrdfPluginBuilder";
        GameObject pluginBuilderPrefab = Resources.Load<GameObject>(_UrdfPluginBuilderPrefabName);

            if (pluginBuilderPrefab == null)
        {
            Debug.LogError($"Unable to locate {_UrdfPluginBuilderPrefabName} in the resources directory of this project! No custom plugins loaded.");
        }

        PluginManagerBase pluginManagerBase = pluginBuilderPrefab.GetComponent<PluginManagerBase>();
            
        //Cleanup.
        Object.DestroyImmediate(pluginBuilderPrefab, true);*/

    }
}