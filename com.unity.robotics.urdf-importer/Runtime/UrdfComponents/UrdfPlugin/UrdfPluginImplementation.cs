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

        public static bool GetUrdfLinkWithNameFromElement(XElement element, string linkElementName,
            out UrdfLink urdfLink, bool required = true)
        {
            bool linkNameExists = ReadStringFromChildXElement(element, linkElementName, out string linkName, required);
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
        
        public static bool GetUrdfJointWithNameFromElement(XElement element, string jointElementName,
            out UrdfJoint urdfJoint, bool required = true)
        {
            bool linkNameExists = ReadStringFromChildXElement(element, jointElementName, out string jointName, required);
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
         * Calls CheckForInvalidElements with an auto-generated HashSet of validElements.
         * Takes a static class in the form of:
         * static class ListOfIds{
         *  public const string IDA = "id_a";
         *  public const string IDB = "id_b";
         * }
         * and generates a HashSet<string> with the values ("id_a", "id_b") which are
         * fed to CheckForInvalidElements.
         * Automates the process of building the HashSet some.
         */
        public static void CheckForInvalidElements(XElement element, params Type[] staticClassWithIdsType)
        {
            HashSet<string> validElements = new HashSet<string>();
            foreach (Type type in staticClassWithIdsType)
            {
                validElements.UnionWith(PluginReflectionHelper.GetConstStringValues(type));
            }
            CheckForInvalidElements(element, validElements);
        }
        
        public static void CheckForInvalidAttributes(XElement element, params Type[] staticClassWithIdsType)
        {
            HashSet<string> validAttributes = new HashSet<string>();
            foreach (Type type in staticClassWithIdsType)
            {
                validAttributes.UnionWith(PluginReflectionHelper.GetConstStringValues(type));
            }
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
        
        public static bool ReadStringFromChildXElement(XElement node, string childElementName, ref string result)
        {
            return ReadStringFromChildXElement(node, childElementName, out result, false, result);
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

        public static bool ReadIntegerFromChildXElement(XElement node, string childElementName, ref int result)
        {
            int defualtValue = result;
            return ReadIntegerFromChildXElement(node, childElementName, out result, false, defualtValue);
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

        public static bool ReadFloatFromChildXElement(XElement node, string childElementName, ref float value)
        {
            return ReadFloatFromChildXElement(node, childElementName, out value, false, value);
        }

        public static bool ReadFloatFromChildXElement(XElement node, string childElementName, out float result, 
            bool required = true, float defaultValue = 0.0f)
        {
            bool wasSuccess = ReadDoubleFromChildXElement(node, childElementName, out double resultAsDouble, required, defaultValue);
            result = (float) resultAsDouble;
            return wasSuccess;
        }

        public static bool ReadFloatFromXElementAttribute(XElement node, string attributeName, ref float value)
        {
            return ReadFloatFromXElementAttribute(node, attributeName, out value, false, value);
        }

        public static bool ReadFloatFromXElementAttribute(XElement node, string attributeName, out float result,
            bool required = true, float defaultValue = 0.0f)
        {
            bool wasSuccess = ReadDoubleFromXElementAttribute(node, attributeName, out double resultAsDouble, required, defaultValue);
            result = (float) resultAsDouble;
            return wasSuccess;
        }
        
        public static bool ReadVector3FromXElementAttribute(XElement node, string attributeName, out Vector3 result,
            bool required = true, Vector3 defaultValue = new(),
            BuiltInExtensions.UrdfRosUnityVector3Conversion appliedConversion = BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection)
        {
            return ReadVector3FromXElementAttribute(node, attributeName, out result, appliedConversion, required, defaultValue);
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
            }

            result = new Vector3(vector3AsMatrix[0][0], vector3AsMatrix[0][1], vector3AsMatrix[0][2]);
            result = result.Ros2Unity(appliedConversion);

            return true;
        }
        
        private static class Vector3XYZ
        {
            public const string XYZ = "xyz";
        }

        public static bool ReadVector3FromXElementWithXYZAttribute(XElement node, string elementName,
            out Vector3 result,
            BuiltInExtensions.UrdfRosUnityVector3Conversion appliedConversion = BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection, bool required = true)
        {
            return ReadVector3FromXElementWithXYZAttribute(node, elementName, out result, appliedConversion, required,
                Vector3.zero);
        }

        public static bool ReadVector3FromXElementWithXYZAttribute(XElement node, string elementName, out Vector3 result,
            BuiltInExtensions.UrdfRosUnityVector3Conversion appliedConversion, bool required, Vector3 defaultValue)
        {
            bool nodeExists = GetXElement(node, elementName, out XElement startPositionNode, required);
            if (!nodeExists)
            {
                result = defaultValue;
                return false;
            }
            return ReadVector3FromXElementAttribute(startPositionNode, Vector3XYZ.XYZ, out result,
                appliedConversion, false,
                defaultValue);
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

        private static string[] ReadArrayValuesFromString(string arrayString)
        {
            string[] arraySplit = arrayString.Split(' ');
            List<string> values = new List<string>();

            foreach (string splitValue in arraySplit)
            {
                if (string.IsNullOrWhiteSpace(splitValue))
                {
                    continue;
                }

                values.Add(splitValue);
            }
            
            return values.ToArray();
        }
        
        private static bool ReadBoolArrayFromString(string nodeName, string arrayString, out bool[] result)
        {
            
            string[] valuesToParse = ReadArrayValuesFromString(arrayString);
            List<bool> values = new List<bool>();
            foreach (string valueToParse in valuesToParse)
            {
                if (!ParseBoolean(valueToParse, out bool value))
                {
                    throw new Exception($"Node {nodeName} array element value expected a bool, received: {valueToParse}");
                }
                values.Add(value);
            }
            result = values.ToArray();
            return true;
        }
        
        private static bool ReadIntArrayFromString(string nodeName, string arrayString, out int[] result)
        {
            
            string[] valuesToParse = ReadArrayValuesFromString(arrayString);
            List<int> values = new List<int>();
            foreach (string valueToParse in valuesToParse)
            {
                if (!int.TryParse(valueToParse, out int value))
                {
                    throw new Exception($"Node {nodeName} array element value expected a int, received: {valueToParse}");
                }
                values.Add(value);
            }
            result = values.ToArray();
            return true;
        }

        private static bool ReadFloatArrayFromString(string nodeName, string arrayString, out float[] result)
        {
            string[] valuesToParse = ReadArrayValuesFromString(arrayString);
            List<float> values = new List<float>();
            foreach (string valueToParse in valuesToParse)
            {
                if (!float.TryParse(valueToParse, out float value))
                {
                    throw new Exception($"Node {nodeName} array element value expected a float, received: {valueToParse}");
                }
                
                values.Add(value);
            }
            result = values.ToArray();
            return true;
        }
        
        public static bool ReadIntArrayFromChildXElement(XElement node, string childElementName, out int[] result)
        {
            if (!ReadStringFromChildXElement(node, childElementName, out string arrayString, true))
            {
                result = Array.Empty<int>();
                return false;
            }

            return ReadIntArrayFromString(node.Name.ToString(), arrayString, out result);
        }

        public static bool ReadFloatArrayFromChildXElement(XElement node, string childElementName, out float[] result, bool required = true)
        {
            if (!ReadStringFromChildXElement(node, childElementName, out string arrayString, required))
            {
                result = Array.Empty<float>();
                return false;
            }

            return ReadFloatArrayFromString(node.Name.ToString(), arrayString, out result);
        }

        public static bool ReadFloatArrayFromXElementAttribute(XElement node, string attributeName, out float[] result, bool required = true)
        {
            if (!ReadStringFromXElementAttribute(node, attributeName, out string arrayString, required))
            {
                result = Array.Empty<float>();
                return false;
            }

            return ReadFloatArrayFromString(node.Name.ToString(), arrayString, out result);
        }
        
        public static bool ReadBooleanArrayFromXElementAttribute(XElement node, string attributeName, out bool[] result, bool required = true)
        {
            if (!ReadStringFromXElementAttribute(node, attributeName, out string arrayString, required))
            {
                result = Array.Empty<bool>();
                return false;
            }
            return ReadBoolArrayFromString(node.Name.ToString(), arrayString, out result);
        }

        public static bool ReadBooleanFromChildXElement(XElement node, string childElementName, ref bool value)
        {
            return ReadBooleanFromChildXElement(node, childElementName, out value, false, value);
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

        public static bool ReadBooleanFromXElementAttribute(XElement node, string attributeName, ref bool value)
        {
            return ReadBooleanFromXElementAttribute(node, attributeName, out value, false, value);
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

        public static bool ReadColorFromXElement(XElement node, string elementName, ref Color value)
        {
            bool elementFound = ReadColorFromXElement(node, elementName, out Color result, false);
            if (elementFound)
            {
                value = result;
            }
            return elementFound;
        }
        
        public static bool ReadColorFromXElement(XElement node, string elementName, out Color result,
            bool required = true)
        {
            if(!GetXElement(node, elementName, out XElement element, required))
            {
                result = Color.white;
                return false;
            }
            return ReadColorFromXElementAttribute(element, "rgba", out result, true);
        }

        public static bool ReadColorFromXElementAttribute(XElement node, string attributeName, ref Color value)
        {
            bool elementFound = ReadColorFromXElementAttribute(node, attributeName, out Color result, false);
            if (elementFound)
            {
                value = result;
            }
            return elementFound;
        }

        public static bool ReadColorFromXElementAttribute(XElement node, string attributeName, out Color result,
            bool required = true)
        {
            
            if (!ReadFloatArrayFromXElementAttribute(node, attributeName, out float[] colorArray, required))
            {
                result = Color.white;
                return false;
            }

            result = XAttributeExtensions.FloatArrayToColor(colorArray);
            
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
        
        public static string GetEnumNames(Type enumType)
        {
            string[] names = Enum.GetNames(enumType);
            StringBuilder resultBuilder = new StringBuilder();
            for (var i = 0; i < names.Length; i++)
            {
                if (i > 0)
                {
                    resultBuilder.Append(", ");
                }
                resultBuilder.Append(names[i]);
            }
            return resultBuilder.ToString();
        }

        public static void ConvertTranslationRotationMatrix6x6ToUnity(float[][] matrix)
        {
            //For the 6x6 matrix, split it up into 4 3x3 matrices
            ConvertMatrix3x3ToUnity(matrix, 0, 0, BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection, BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection);
            ConvertMatrix3x3ToUnity(matrix, 3, 0, BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection, BuiltInExtensions.UrdfRosUnityVector3Conversion.Rotation);
            ConvertMatrix3x3ToUnity(matrix, 0, 3, BuiltInExtensions.UrdfRosUnityVector3Conversion.Rotation, BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection);
            ConvertMatrix3x3ToUnity(matrix, 3, 3, BuiltInExtensions.UrdfRosUnityVector3Conversion.Rotation, BuiltInExtensions.UrdfRosUnityVector3Conversion.Rotation);
        }
        

        public static void ConvertMatrix3x3ToUnity(float[][] matrix, int offsetI = 0, int offsetJ = 0, 
            BuiltInExtensions.UrdfRosUnityVector3Conversion rowConversion = BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection,
            BuiltInExtensions.UrdfRosUnityVector3Conversion columnConversion = BuiltInExtensions.UrdfRosUnityVector3Conversion.PositionDirection)
        {
            //For the 3x3 matrix:
            //For each row, apply the Vec3 conversion
            //For each column, apply the Vec3 conversion
            
            //Rows:
            for (int iOffset = 0; iOffset < 3; iOffset++)
            {
                int i = offsetI + iOffset;
                Vector3 rowRos = new Vector3(
                    matrix[i][offsetJ + 0],
                    matrix[i][offsetJ + 1],
                    matrix[i][offsetJ + 2]
                );
                Vector3 rowUnity = rowRos.Ros2Unity(rowConversion);
                matrix[i][offsetJ + 0] = rowUnity[0];
                matrix[i][offsetJ + 1] = rowUnity[1];
                matrix[i][offsetJ + 2] = rowUnity[2];
            }
                        
            //Columns:
            for (int jOffset = 0; jOffset < 3; jOffset++)
            {
                int j = offsetJ + jOffset;
                Vector3 columnRos = new Vector3(
                    matrix[offsetI + 0][j],
                    matrix[offsetI + 1][j],
                    matrix[offsetI + 2][j]
                );
                Vector3 columnUnity = columnRos.Ros2Unity(columnConversion);
                matrix[offsetI + 0][j] = columnUnity[0];
                matrix[offsetI + 1][j] = columnUnity[1];
                matrix[offsetI + 2][j] = columnUnity[2];
            }
        }
        
        public static void ConvertMatrix3x3ToRos(float[][] matrix, int offsetI = 0, int offsetJ = 0)
        {
            //For the 3x3 matrix:
            //For each row, apply the Vec3 conversion
            //For each column, apply the Vec3 conversion
            
            //Rows:
            for (int iOffset = 0; iOffset < 3; iOffset++)
            {
                int i = offsetI + iOffset;
                Vector3 rowUnity = new Vector3(
                    matrix[i][offsetJ + 0],
                    matrix[i][offsetJ + 1],
                    matrix[i][offsetJ + 2]
                );
                Vector3 rowRos = rowUnity.Unity2Ros();
                matrix[i][offsetJ + 0] = rowRos[0];
                matrix[i][offsetJ + 1] = rowRos[1];
                matrix[i][offsetJ + 2] = rowRos[2];
            }
                        
            //Columns:
            for (int jOffset = 0; jOffset < 3; jOffset++)
            {
                int j = offsetJ + jOffset;
                Vector3 columnUnity = new Vector3(
                    matrix[offsetI + 0][j],
                    matrix[offsetI + 1][j],
                    matrix[offsetI + 2][j]
                );
                Vector3 columnRos = columnUnity.Unity2Ros();
                matrix[offsetI + 0][j] = columnRos[0];
                matrix[offsetI + 1][j] = columnRos[1];
                matrix[offsetI + 2][j] = columnRos[2];
            }
        }
    }
}