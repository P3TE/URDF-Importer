using System;
using System.Collections.Generic;
using System.Xml.Linq;
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
        
        private bool rootMostLinkFound = false;
        private UrdfLink rootMostLink = null;
        private UrdfRobot urdfRobot = null;

        public UrdfLink RootMostLink
        {
            get
            {
                FindRobotComponentsIfApplicable();
                return rootMostLink;
            }
        }
        
        public UrdfRobot CorrespondingRobot
        {
            get
            {
                FindRobotComponentsIfApplicable();
                return urdfRobot;
            }
        }
        
        private void FindRobotComponentsIfApplicable()
        {
            if (rootMostLinkFound)
            {
                return;
            }
            rootMostLinkFound = true;
            
            FindRootMostLink();
            FindUrdfRobot();
        }

        private void FindRootMostLink()
        {
            Transform currentCheckTransform = transform;
            while (currentCheckTransform != null)
            {
                UrdfLink nextCheckOption = currentCheckTransform.GetComponent<UrdfLink>();
                if (nextCheckOption != null)
                {
                    rootMostLink = nextCheckOption;
                }
                
                urdfRobot = currentCheckTransform.GetComponent<UrdfRobot>();
                if (urdfRobot != null)
                {
                    //We can stop searching now, we aren't choosing anything outside the robot.
                    break;
                }
                
                currentCheckTransform = currentCheckTransform.parent;
                
            }
        }

        private void FindUrdfRobot()
        {
            if (urdfRobot == null)
            {
                Transform currentCheckTransform = transform;
                while (currentCheckTransform != null)
                {
                    urdfRobot = currentCheckTransform.GetComponent<UrdfRobot>();
                    if (urdfRobot != null)
                    {
                        //We can stop searching now
                        break;
                    }
                    currentCheckTransform = currentCheckTransform.parent;
                }
            }

            if (urdfRobot != null && rootMostLink == null)
            {
                LinkedList<Transform> searchQueue = new LinkedList<Transform>();
                searchQueue.AddFirst(urdfRobot.transform);
                while (searchQueue.Count > 0)
                {
                    LinkedListNode<Transform> currentTransformNode = searchQueue.First;
                    Transform currentTransform = currentTransformNode.Value;

                    rootMostLink = currentTransform.GetComponent<UrdfLink>();
                    if (rootMostLink != null)
                    {
                        //Found it.
                        break;
                    }

                    //Add all the children to the queue.
                    for (int i = 0; i < currentTransform.childCount; i++)
                    {
                        searchQueue.AddLast(currentTransform.GetChild(i));
                    }
                    
                    searchQueue.RemoveFirst();
                }
            }
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
        
        public static bool ReadStringFromChildXElement(XElement node, string childElementName, out string result, 
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
        
        public static bool ReadIntegerFromChildXElement(XElement node, string childElementName, out int result, 
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

        public static bool ReadIntegerFromXElementAttribute(XElement node, string attributeName, out int result,
            bool required = true, int defaultValue = 0)
        {
            if (!GetXAttribute(node, attributeName, out XAttribute xAttribute, required))
            {
                result = defaultValue;
                return false;
            }

            bool parseSuccess = int.TryParse(xAttribute.Value, out result);
            if (!parseSuccess)
            {
                throw new Exception($"Attribute {node.Name}/{xAttribute.Name.LocalName} expected an integer, received: {xAttribute.Value}");
            }
            return true;
        }
        
        public static bool ReadDoubleFromChildXElement(XElement node, string childElementName, out double result, 
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

        public static bool ReadFloatFromChildXElement(XElement node, string childElementName, out float result, 
            bool required = true, float defaultValue = 0.0f)
        {
            bool wasSuccess = ReadDoubleFromChildXElement(node, childElementName, out double resultAsDouble, required, defaultValue);
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
        
        public static bool ReadBooleanFromChildXElement(XElement node, string childElementName, out bool result, 
            bool required = true, bool defaultValue = false)
        {
            if (!GetXElement(node, childElementName, out XElement childXElement, required))
            {
                result = defaultValue;
                return false;
            }

            if (!ParseBoolean(childXElement.Value, out result))
            {
                throw new Exception($"Node {node.Name} value expected a bool, received: {childXElement.Value}");
            }
            
            return true;
        }

        public static bool ReadBooleanFromXElementAttribute(XElement node, string attributeName, out bool result,
            bool required = true, bool defaultValue = false)
        {
            if (!GetXAttribute(node, attributeName, out XAttribute xAttribute, required))
            {
                result = defaultValue;
                return false;
            }

            if (!ParseBoolean(xAttribute.Value, out result))
            {
                throw new Exception($"Attribute {node.Name}/{xAttribute.Name.LocalName} expected a bool, received: {xAttribute.Value}");
            }
            
            return true;
        }

        private static bool ParseBoolean(string value, out bool result)
        {
            value = value.Trim();
            
            if (value == "1")
            {
                result = true;
                return true;
            }
            if (value == "0")
            {
                result = false;
                return true;
            }

            value = value.ToLower();
            
            if (value == "true")
            {
                result = true;
                return true;
            }
            if (value == "false")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }
    }
}