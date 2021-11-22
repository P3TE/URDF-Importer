using System;
using System.Xml.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class UrdfPluginImplementation : MonoBehaviour
    {
        
        public string LinkName { get; set; }

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

        public static bool GetXAttribute(XElement node, string attributeName, out XAttribute result, bool required = true)
        {
            result = node.Attribute(attributeName);
            if (result == null)
            {
                if (required)
                {
                    throw new Exception($"Node with name {node.Name} missing attribute: {attributeName}");
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

        public static bool ReadStringFromXElementAttribute(XElement node, string attributeName, out string result,
            bool required = true, string defaultValue = "")
        {
            if (!GetXAttribute(node, attributeName, out XAttribute xAttribute, required))
            {
                result = defaultValue;
                return false;
            }

            result = xAttribute.Value;
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
                throw new Exception($"Node {node.Name} value expected an integer, received: {childXElement.Value}");
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
                throw new Exception($"Node {node.Name} value expected a double, received: {childXElement.Value}");
            }
            return true;
        }

        public static bool ReadDoubleFromXElementAttribute(XElement node, string attributeName, out double result,
            bool required = true, double defaultValue = 0.0f)
        {
            if (!GetXAttribute(node, attributeName, out XAttribute xAttribute, required))
            {
                result = defaultValue;
                return false;
            }

            bool parseSuccess = double.TryParse(xAttribute.Value, out result);
            if (!parseSuccess)
            {
                throw new Exception($"Attribute {node.Name}/{xAttribute.Name.LocalName} expected a double, received: {xAttribute.Value}");
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

        public static bool ReadFloatFromXElementAttribute(XElement node, string attributeName, out float result,
            bool required = true, float defaultValue = 0.0f)
        {
            bool wasSuccess = ReadDoubleFromXElementAttribute(node, attributeName, out double resultAsDouble, required, defaultValue);
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