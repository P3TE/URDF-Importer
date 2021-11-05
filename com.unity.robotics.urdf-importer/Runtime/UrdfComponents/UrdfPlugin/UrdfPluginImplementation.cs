using System;
using System.Xml.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class UrdfPluginImplementation : MonoBehaviour
    {

        public const string _DefaultLinkName = "linkName";

        public abstract void DeserialiseFromXml(XElement node);

        public virtual void FinaliseImport(UrdfPlugins urdfPlugins)
        {
            //Called after the deserialise is complete for all plugins.
            //Useful for additional processing after all data from all plugins is loaded.
        }

        public UrdfLink FindRootMostLink()
        {
            UrdfLink currentLink = null;
            Transform currentCheckTransform = transform;
            while (currentCheckTransform != null)
            {
                UrdfLink nextCheckOption = currentCheckTransform.GetComponent<UrdfLink>();
                if (nextCheckOption != null)
                {
                    currentLink = nextCheckOption;
                }
                
                UrdfRobot urdfRobot = currentCheckTransform.GetComponent<UrdfRobot>();
                if (urdfRobot != null)
                {
                    //We can stop searching now, we aren't choosing anything outside the robot.
                    break;
                }
                
                currentCheckTransform = currentCheckTransform.parent;
                
            }
            return currentLink;
        }
        
        public static bool AttemptToFindLink(XElement node, [CanBeNull] out UrdfLink urdfLink)
        {
            if (ReadStringFromXElement(node, _DefaultLinkName, out string linkName, false))
            {
                return UrdfLinkExtensions.TryFindLink(linkName, out urdfLink);
            }
            urdfLink = null;
            return false;
        }

        public static bool GetXElement(XElement node, string childElementName, out XElement result, bool required = true)
        {
            result = node.Element(childElementName);
            if (result == null)
            {
                if (required)
                {
                    throw new Exception($"Node with name {node.Name} missing child element: {childElementName}");
                }
                return false;
            }
            return true;
        }
        
        public static bool ReadStringFromXElement(XElement node, string childElementName, out string result, 
            bool required = true, string defaultValue = "")
        {
            if (!GetXElement(node, childElementName, out XElement childXElement, required))
            {
                result = defaultValue;
                return false;
            }
            
            result = childXElement.Value;
            return true;
        }
        
        public static bool ReadIntegerFromXElement(XElement node, string childElementName, out int result, 
            bool required = true, int defaultValue = 0)
        {
            if (!GetXElement(node, childElementName, out XElement childXElement, required))
            {
                result = defaultValue;
                return false;
            }

            bool parseSuccess = int.TryParse(childXElement.Value, out result);
            if (!parseSuccess)
            {
                if (required)
                {
                    throw new Exception($"Node {node.Name} value expected an integer, received: {childXElement.Value}");
                }
                result = defaultValue;
                return false;
            }
            return true;
        }
        
        public static bool ReadDoubleFromXElement(XElement node, string childElementName, out double result, 
            bool required = true, float defaultValue = 0.0f)
        {
            if (!GetXElement(node, childElementName, out XElement childXElement, required))
            {
                result = defaultValue;
                return false;
            }

            bool parseSuccess = double.TryParse(childXElement.Value, out result);
            if (!parseSuccess)
            {
                if (required)
                {
                    throw new Exception($"Node {node.Name} value expected a double, received: {childXElement.Value}");
                }
                result = defaultValue;
                return false;
            }
            return true;
        }

        public static bool ReadFloatFromXElement(XElement node, string childElementName, out float result, 
            bool required = true, float defaultValue = 0.0f)
        {
            bool wasSuccess = ReadDoubleFromXElement(node, childElementName, out double resultAsDouble, required, defaultValue);
            result = (float) resultAsDouble;
            return wasSuccess;
        }
        
        public static bool ReadBooleanFromXElement(XElement node, string childElementName, out bool result, 
            bool required = true, bool defaultValue = false)
        {
            if (!GetXElement(node, childElementName, out XElement childXElement, required))
            {
                result = defaultValue;
                return false;
            }

            string originalCaseXmlValue = childXElement.Value.Trim();

            if (originalCaseXmlValue == "1")
            {
                result = true;
                return true;
            }
            if (originalCaseXmlValue == "0")
            {
                result = false;
                return true;
            }
            
            string lowerCaseValue = originalCaseXmlValue.ToLower();
            
            if (lowerCaseValue == "true")
            {
                result = true;
                return true;
            }
            if (lowerCaseValue == "false")
            {
                result = false;
                return true;
            }
            
            if (required)
            {
                throw new Exception($"Node {node.Name} value expected a bool, received: {originalCaseXmlValue}");
            }
            
            result = defaultValue;
            return false;
        }
        
    }
}