using System;
using System.Xml.Linq;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class UrdfPluginImplementation : MonoBehaviour
    {
        
        public abstract void DeserialiseFromXml(XElement node);



        protected bool GetXElement(XElement node, string childElementName, out XElement result, bool required = true)
        {
            result = node.Element(childElementName);
            if (result == null)
            {
                if (required)
                {
                    throw new Exception($"{GetType().Name}: node {node.Name} missing child element: {childElementName}");
                }
                return false;
            }
            return true;
        }
        
        protected bool ReadStringFromXElement(XElement node, string childElementName, out string result, 
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
        
        protected bool ReadIntegerFromXElement(XElement node, string childElementName, out int result, 
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
                    throw new Exception($"{GetType().Name}: node {node.Name} value expected an integer, received: {childXElement.Value}");
                }
                result = defaultValue;
                return false;
            }
            return true;
        }
        
    }
}