using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public abstract class UrdfPluginImplementation : MonoBehaviour
    {
        
        

        public abstract void DeserialiseFromXml(XElement node);

        public virtual void FinaliseImport(UrdfPlugins urdfPlugins)
        {
            //Called after the deserialise is complete for all plugins.
            //Useful for additional processing after all data from all plugins is loaded.
        }
        
        private bool rootMostLinkFound = false;
        private UrdfLink rootMostLink = null;
        private UrdfRobot urdfRobot = null;

        public PluginManagerBase.PluginData ImplementationPluginData
        {
            get;
            set;
        }

        public UrdfPlugins Plugins => ImplementationPluginData.urdfPlugins;

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

        public static bool GetUrdfLinkWithNameFromElement(XElement element, string linkElementName, out string linkName,
            out UrdfLink urdfLink, bool required = true)
        {
            bool linkNameExists = ReadStringFromChildXElement(element, linkElementName, out linkName, required);
            if (!linkNameExists)
            {
                urdfLink = null;
                return false;
            }

            bool foundLink = UrdfLinkExtensions.TryFindLink(linkName, out urdfLink);
            if (!foundLink)
            {
                throw new Exception(
                    $"For node {GetVerboseXElementName(element)}, unable to find link with name {linkName}");
            }
            return true;
        }
        
        public static bool GetUrdfJointWithNameFromElement(XElement element, string jointElementName, out string jointName,
            out UrdfJoint urdfJoint, bool required = true)
        {
            bool linkNameExists = ReadStringFromChildXElement(element, jointElementName, out jointName, required);
            if (!linkNameExists)
            {
                urdfJoint = null;
                return false;
            }

            bool foundLink = UrdfLinkExtensions.TryFindJoint(jointName, out urdfJoint);
            if (!foundLink)
            {
                throw new Exception(
                    $"For node {GetVerboseXElementName(element)}, unable to find link with name {jointName}");
            }
            return true;
        }
            

        /**
         * This function is designed to help the user find elements in the URDF that shouldn't be there
         * or are mistyped. It will look through all of the elements and see if their name is in a
         * list of valid elements.
         */
        public static void CheckForInvalidElements(XElement element, HashSet<string> validElements)
        {
            foreach (XElement xElement in element.Elements())
            {
                string elementName = xElement.Name.LocalName;
                if (!validElements.Contains(elementName))
                {
                    RuntimeUrdf.AddImportWarning($"Node with name '{GetVerboseXElementName(element)}' has element with name '{elementName}' but no implementation for a parameter with that name exists.");
                }
            }
        }
        
        /**
         * This function is designed to help the user find attributes in the URDF that shouldn't be there
         * or are mistyped. It will look through all of the elements and see if their name is in a
         * list of valid elements.
         */
        public static void CheckForInvalidAttributes(XElement element, HashSet<string> validAttributes)
        {
            foreach (XAttribute xAttribute in element.Attributes())
            {
                string elementName = xAttribute.Name.LocalName;
                if (!validAttributes.Contains(elementName))
                {
                    RuntimeUrdf.AddImportWarning($"Node with name '{GetVerboseXElementName(element)}' has attribute with name '{elementName}' but no implementation for a parameter with that name exists.");
                }
            }
        }

        public static string GetVerboseXElementName(XElement element)
        {
            StringBuilder result = new StringBuilder();
            result.Append("<");
            result.Append(element.Name);
            foreach (XAttribute xAttribute in element.Attributes())
            {
                result.Append(" ");
                result.Append(xAttribute.Name);
                result.Append("=\"");
                result.Append(xAttribute.Value);
                result.Append("\"");
            }
            result.Append(">");
            return result.ToString();
        }

        /**
         * Takes a static class in the form of:
         * static class ListOfIds{
         *  public const string IDA = "id_a";
         *  public const string IDB = "id_b";
         * }
         * and generates a HashSet<string> with the values ("id_a", "id_b") which are
         * fed to CheckForInvalidElements.
         * Automates the process of building the HashSet some.
         */
        public static void CheckForInvalidElements(XElement element, Type staticClassWithIdsType)
        {
            HashSet<string> validElements = PluginReflectionHelper.GetConstStringValues(staticClassWithIdsType);
            CheckForInvalidElements(element, validElements);
        }
        
        public static void CheckForInvalidAttributes(XElement element, Type staticClassWithIdsType)
        {
            HashSet<string> validAttributes = PluginReflectionHelper.GetConstStringValues(staticClassWithIdsType);
            CheckForInvalidAttributes(element, validAttributes);
        }
        
        public static bool GetXElement(XElement node, string childElementName, out XElement result, bool required = true)
        {
            result = node.Element(childElementName);
            if (result == null)
            {
                if (required)
                {
                    throw new Exception($"Node {GetVerboseXElementName(node)} missing child element: {childElementName}");
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
                    throw new Exception($"Node {GetVerboseXElementName(node)} with name {GetVerboseXElementName(node)} missing attribute: {attributeName}");
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

        public static bool ReadVector3FromXElementAttribute(XElement node, string attributeName, out Vector3 result,
            BuiltInExtensions.UrdfRosUnityVector3Conversion appliedConversion = BuiltInExtensions.UrdfRosUnityVector3Conversion.Direction, bool required = true)
        {
            return ReadVector3FromXElementAttribute(node, attributeName, out result, appliedConversion, required, Vector3.zero);
        }

        public static bool ReadVector3FromXElementAttribute(XElement node, string attributeName, out Vector3 result, 
            BuiltInExtensions.UrdfRosUnityVector3Conversion appliedConversion, bool required,
            Vector3 defaultValue)
        {
            bool exists = ReadMatrixNxMFromXElementAttribute(node, attributeName, out float[][] vector3AsMatrix,
                out int width, out int height, required);
            if (!exists)
            {
                result = defaultValue;
                if (required)
                {
                    throw new Exception($"Unable to find Vector3 in node {GetVerboseXElementName(node)}");
                }
                return false;
            }

            if (width != 3 || height != 1)
            {
                throw new Exception($"Failed to parse Vector3 in node {GetVerboseXElementName(node)} Expected elements of dimensions 1x3, found {height}x{width}");
                //Good.
            }

            result = new Vector3(vector3AsMatrix[0][0], vector3AsMatrix[0][1], vector3AsMatrix[0][2]);
            result = result.Ros2Unity(appliedConversion);

            return true;
        }
        
        public static bool ReadMatrixNxMFromXElementAttribute(XElement node, string attributeName, out float[][] result,
            out int width, out int height, bool required = true)
        {
            bool stringExists = ReadStringFromXElementAttribute(node, attributeName, out string matrixAsString, required);
            if (!stringExists)
            {
                width = 0;
                height = 0;
                result = new float[0][];
                return false;
            }

            try
            {
                ReadMatrixNxMFromString(matrixAsString, out result, out width, out height);
            }
            catch (Exception e)
            {
                string improvedMessage = $"Matrix attribute {attributeName} in node {GetVerboseXElementName(node)}: {e.Message}";
                throw new Exception(improvedMessage, e);
            }
            return true;
        }

        public static bool ReadMatrixNxMFromChildXElement(XElement node, string childElementName, out float[][] result,
            out int width, out int height, bool required = true)
        {
            bool stringExists = ReadStringFromChildXElement(node, childElementName, out string matrixAsString, required);
            if (!stringExists)
            {
                width = 0;
                height = 0;
                result = new float[0][];
                return false;
            }

            try
            {
                ReadMatrixNxMFromString(matrixAsString, out result, out width, out height);
            }
            catch (Exception e)
            {
                string improvedMessage = $"Matrix {childElementName} in node {GetVerboseXElementName(node)}: - {e.Message}";
                throw new Exception(improvedMessage, e);
            }
            return true;
        }

        public static bool ReadMatrixNxMFromString(string matrixAsString, out float[][] result,
            out int width, out int height)
        {
            
            width = 0;
            height = 0;

            string[] lines = matrixAsString.Split('\n');
            List<List<string>> lineSplits = new List<List<string>>();
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    continue;
                }
                string[] lineSplit = trimmedLine.Split(' ');
                List<string> lineSplitValues = new List<string>();
                foreach (string value in lineSplit)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        lineSplitValues.Add(value);
                    }
                }
                lineSplits.Add(lineSplitValues);
            }
            
            result = new float[lineSplits.Count][];
            height = lineSplits.Count;
            for (int row = 0; row < height; row++)
            {
                List<string> lineSplit = lineSplits[row];
                int rowColumns = lineSplit.Count;
                if (row == 0)
                {
                    width = rowColumns;
                }
                else
                {
                    if (width != rowColumns)
                    {
                        throw new Exception($"Matrix has an inconsistent column count! row 0 has {width}, row {row} has {rowColumns}");
                    }
                }
                
                float[] rowElements = new float[rowColumns];
                for (int col = 0; col < rowColumns; col++)
                {
                    string element = lineSplit[col];
                    if (!float.TryParse(element, out rowElements[col]))
                    {
                        throw new Exception($"Matrix has invalid value at ({row},{col}): {element}");
                    }
                }
                result[row] = rowElements;
            }
            
            return true;
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