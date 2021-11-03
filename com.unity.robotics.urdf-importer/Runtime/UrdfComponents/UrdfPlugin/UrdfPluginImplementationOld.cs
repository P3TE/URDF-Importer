using System;
using System.Xml;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    [Obsolete("No longer used...")]
    public abstract class UrdfPluginImplementationOld : MonoBehaviour
    {

        protected string robotName;
        protected bool encode;
        protected XmlDocument xmlDocument;
        
        /**
         * Used to define the type of plugin.
         */
        public abstract string PluginFilename
        {
            get;
        }
        
        /**
         * The name of the plugin will end up being:
         * {robot_name}_{PluginNamePostfix}
         */
        public abstract string PluginNamePostfix
        {
            get;
        }

        internal void BuildExportData(string robotName, XmlDocument xmlDocument)
        {
            this.robotName = robotName;
            this.encode = true;
            this.xmlDocument = xmlDocument;
            ExportGazeboData();
        }

        protected virtual void ExportGazeboData()
        {
            XmlElement gazeboElement = xmlDocument.CreateElement(string.Empty, PluginManagerBase._GazeboTag, string.Empty);
            xmlDocument.AppendChild(gazeboElement);
            BuildPluginElement(gazeboElement);
        }

        protected virtual void BuildPluginElement(XmlElement gazeboXmlElement)
        {
            XmlElement pluginElement = xmlDocument.CreateElement(string.Empty, PluginManagerBase._PluginTag, string.Empty);
            pluginElement.SetAttribute(PluginManagerBase._FilenameAttribute, PluginFilename);
            pluginElement.SetAttribute(PluginManagerBase._NameAttribute, $"{robotName}_{PluginNamePostfix}");
            gazeboXmlElement.AppendChild(pluginElement);
            
            ExportPluginData(pluginElement);
        }

        public string LinkName => transform.name;

        public string JointName
        {
            get
            {
                UrdfJoint urdfJoint = GetComponent<UrdfJoint>();
                if (urdfJoint == null)
                {
                    Debug.LogError($"No joint for plugin: {gameObject.name}");
                    return "";
                }
                else
                {
                    return urdfJoint.UsedJointName;
                }
                
            }
        }
        
        public abstract void ExportPluginData(XmlElement pluginXmlElement);
        
        public abstract void DecodeExportPlugin(XmlDocument pluginDescriptionXml);
        
        
        protected bool TranscodeValue(XmlElement parentXmlElement, string tagName, ref float value, float defaultValue = 0.0f)
        {
            if(encode)
            {
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                newElement.InnerText = value.ToString("0.###");
                parentXmlElement.AppendChild(newElement);
                return true;
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }
        
        protected void TranscodeValue(XmlElement parentXmlElement, string tagName, ref int value)
        {
            if(encode)
            {
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                newElement.InnerText = value.ToString();
                parentXmlElement.AppendChild(newElement);
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }
        
        protected void TranscodeValue(XmlElement parentXmlElement, string tagName, ref string value)
        {
            if(encode)
            {
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                newElement.InnerText = value;
                parentXmlElement.AppendChild(newElement);
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }
        
        protected void TranscodeValue(XmlElement parentXmlElement, string tagName, ref bool value, bool? writeAsInteger = null)
        {
            if(encode)
            {
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                if (writeAsInteger.GetValueOrDefault(false))
                {
                    newElement.InnerText = value ? "1" : "0";
                }
                else
                {
                    newElement.InnerText = value ? "true" : "false";
                }
                parentXmlElement.AppendChild(newElement);
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }
        
        protected void TranscodeValue(XmlElement parentXmlElement, string tagName, ref Vector3 value)
        {
            if(encode)
            {
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                newElement.InnerText = $"{value.x} {value.y} {value.z}"; //TODO RUF/FLU...
                parentXmlElement.AppendChild(newElement);
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }
        
        protected void TranscodePose(XmlElement parentXmlElement, string tagName, ref Vector3 translation, ref Quaternion orientation)
        {
            if(encode)
            {

                Vector3 rollPitchYawDegrees = orientation.eulerAngles;
                Vector3 rollPitchYawRadians = rollPitchYawDegrees * Mathf.Deg2Rad;
                
                XmlElement newElement = xmlDocument.CreateElement(tagName);
                //TODO RUF/FLU...
                newElement.InnerText = $"{translation.x} {translation.y} {translation.z}" +
                                       $"{rollPitchYawRadians.x} {rollPitchYawRadians.y} {rollPitchYawRadians.z}";
                parentXmlElement.AppendChild(newElement);
            }
            else
            {
                throw new NotImplementedException("TODO...");
            }
        }

    }
}