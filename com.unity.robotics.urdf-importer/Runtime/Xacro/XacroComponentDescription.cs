#define LOG_XACRO_PARSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Urdf.Xacro
{
    public class XacroComponentDescription
    {
        private Dictionary<string, XacroParameter> attributes = new();

        private XacroComponentDescription parent = null;

        private List<XacroComponentDescription> children;

        private XNode root;

        private XacroComponentDescription CurrentContext { get; set; }

        public XacroParameter GetParameter(string parameterName)
        {
            if (attributes.TryGetValue(parameterName, out XacroParameter result))
            {
                return result;
            }

            if (parent != null)
            {
                return parent.GetParameter(parameterName);
            }

            return null;
        }

        public XacroComponentDescription(XacroComponentDescription parent)
        {
            this.parent = parent;
        }

        public XacroComponentDescription(string filename, XacroComponentDescription parent = null) : this(parent)
        {
            XDocument xdoc = XDocument.Load(filename);
            root = xdoc;
            LoadXacro(root);
        }

        public XacroComponentDescription(XElement node, XacroComponentDescription parent) : this(parent)
        {
            LoadXacro(node);
        }

        private void LoadXacro(XNode xNode)
        {
            CurrentContext = this;
            ProcessXNode(xNode);
        }

        private void ProcessXNode(XNode xNode)
        {
            if (xNode is XContainer node)
            {
                ProcessXContainer(node);
            }
        }

        private void ProcessXContainer(XContainer xContainer)
        {
            if (xContainer is XElement xElement)
            {
                ProcessXElement(xElement);
            }

            foreach (XNode xNode in xContainer.Nodes())
            {
                ProcessXNode(xNode);
            }
        }

        private void ProcessXElement(XElement xElement)
        {
#if LOG_XACRO_PARSE
            Debug.Log($"parsing xElement, name = {xElement.Name.LocalName}");
#endif

            XName xName = xElement.Name;
            if (xName.Namespace.NamespaceName.Contains("xacro"))
            {
                string elementLocalName = xName.LocalName;
                switch (elementLocalName)
                {
                    case "arg":
                        ProcessXElementArg(xElement);
                        return;
                    case "property":
                        ProcessXElementProperty(xElement);
                        return;
                    case "include":
                        ProcessXElementInclude(xElement);
                        return;
                    case "macro":
                        Debug.LogError("TODO - Can't currently process macros!");
                        return;
                }

                //It's a macro maybe.
                Debug.LogError($"Found macro with id {elementLocalName}, but currently unable to parse macros!");
            }
            else
            {
                //Regular URDF xml.
                /*case "robot":
                //TODO...
                Debug.LogError("TODO - I'm not sure how to handle this yet...");
                return;*/
                Debug.LogWarning("TODO - Regular URDF - I'm not sure how to handle this yet...");
            }
        }

        private void ProcessXElementArg(XElement xElementArg)
        {
            const string _AttributeNameKey = "name";
            const string _AttributeDefaultValueKey = "default";

            //name=""
            string argumentName = null;
            //default=""
            string argumentDefaultValue = null;

            foreach (XAttribute xAttribute in xElementArg.Attributes())
            {
                string attributeName = xAttribute.Name.ToString();
                string attributeValue = xAttribute.Value;

                switch (attributeName)
                {
                    case _AttributeNameKey:
                        argumentName = attributeValue;
                        break;
                    case _AttributeDefaultValueKey:
                        argumentDefaultValue = attributeValue;
                        break;
                    default:
                        Debug.LogWarning($"Unknown attribute with name: {attributeName}");
                        break;
                }
            }

            if (argumentName == null)
            {
                Debug.LogError($"Unable to parse attribute, couldn't find {_AttributeNameKey}");
                return;
            }

            if (argumentDefaultValue == null)
            {
                Debug.LogWarning(
                    $"No default value set for attribute with name {_AttributeNameKey}, missing: {_AttributeDefaultValueKey}, defaulting to empty string");
                argumentDefaultValue = "";
            }

            //Add to the current context.
            if (CurrentContext.attributes.ContainsKey(argumentName))
            {
                Debug.LogError($"Multiple arguments with the same name ({argumentName})");
                return;
            }

#if LOG_XACRO_PARSE
            Debug.Log($"Adding argument name={argumentName} default={argumentDefaultValue}");
#endif
            CurrentContext.attributes.Add(argumentName, ExpandXacroParameter(argumentDefaultValue));
        }

        private void ProcessXElementProperty(XElement xElementArg)
        {
            const string _AttributeNameKey = "name";
            const string _AttributeValueKey = "value";

            //name=""
            string propertyName = null;
            //value=""
            string propertyValue = null;

            foreach (XAttribute xAttribute in xElementArg.Attributes())
            {
                string attributeName = xAttribute.Name.ToString();
                string attributeValue = xAttribute.Value;

                switch (attributeName)
                {
                    case _AttributeNameKey:
                        propertyName = attributeValue;
                        break;
                    case _AttributeValueKey:
                        propertyValue = attributeValue;
                        break;
                    default:
                        Debug.LogWarning($"Unknown attribute with name: {attributeName}");
                        break;
                }
            }

            if (propertyName == null)
            {
                Debug.LogError($"Unable to parse attribute, couldn't find {_AttributeNameKey}");
                return;
            }

            if (propertyValue == null)
            {
                Debug.LogWarning(
                    $"No default value set for attribute with name {_AttributeNameKey}, missing: {_AttributeValueKey}, defaulting to empty string");
                propertyValue = "";
            }

            //Add to the current context.
            if (CurrentContext.attributes.ContainsKey(propertyName))
            {
                Debug.LogError($"Multiple arguments with the same name ({propertyName})");
                return;
            }

#if LOG_XACRO_PARSE
            Debug.Log($"Adding property name={propertyName} default={propertyValue}");
#endif
            CurrentContext.attributes.Add(propertyName, ExpandXacroParameter(propertyValue));
        }

        private void ProcessXElementInclude(XElement xElementArg)
        {
            const string _AttributeFilenameKey = "filename";

            //filename="$(find package_name)/relative/path"
            string includeFilename = null;

            foreach (XAttribute xAttribute in xElementArg.Attributes())
            {
                string attributeName = xAttribute.Name.ToString();
                string attributeValue = xAttribute.Value;

                switch (attributeName)
                {
                    case _AttributeFilenameKey:
                        includeFilename = attributeValue;
                        break;
                    default:
                        Debug.LogWarning($"Unknown attribute with name: {attributeName}");
                        break;
                }
            }

            if (includeFilename == null)
            {
                Debug.LogError($"Unable to parse attribute, couldn't find {_AttributeFilenameKey}");
                return;
            }

            XacroParameter xacroParameter = ExpandXacroParameter(includeFilename);
            string resolvedFileName = xacroParameter.ResolveStringParameter(CurrentContext);


#if LOG_XACRO_PARSE
            Debug.Log($"Including file at {resolvedFileName}");
#endif
            XacroComponentDescription includedFileXacro = new XacroComponentDescription(resolvedFileName, parent);
            children.Add(includedFileXacro);
        }


        public static XacroParameter ExpandXacroParameter(string rawText)
        {
            Regex pattern = new Regex(@"(\$[\(\{][\w ]*[\)\}])");
            Match match = pattern.Match(rawText);

            XacroCompositeParameter xacroComposite = new XacroCompositeParameter();

            int currentIndex = 0;
            foreach (Capture matchCapture in match.Captures)
            {
                //Add the preceding text.
                if (matchCapture.Index != currentIndex)
                {
                    string precedingText = rawText.Substring(currentIndex, matchCapture.Index - currentIndex);
                    xacroComposite.components.Add(new XacroParameterString(precedingText));
                }

                //Extract the contents of the command.
                string xacroCommandContents = rawText.Substring(matchCapture.Index + 2, matchCapture.Length - 3);

                char commandTypeChar = rawText[matchCapture.Index + 1];
                switch (commandTypeChar)
                {
                    case '(':
                        //Extract the arguments of the command.
                        string[] xacroCommandSplit = xacroCommandContents.Split(' ');
                        XacroRospackCommand xacroCommandRospack = new XacroRospackCommand(xacroCommandSplit);
                        xacroComposite.components.Add(xacroCommandRospack);
                        break;
                    case '{':
                        //Store the name of the parameter
                        xacroComposite.components.Add(new XacroParameterReference(xacroCommandContents));
                        break;
                    default:
                        Debug.LogError($"Unhandled command wrapper {commandTypeChar}");
                        break;
                }

                //Update the current index.
                currentIndex = matchCapture.Index + match.Length;
            }

            //Add any remaining text
            if (rawText.Length != currentIndex)
            {
                string precedingText = rawText.Substring(currentIndex, rawText.Length - currentIndex);
                xacroComposite.components.Add(new XacroParameterString(precedingText));
            }

            //Simplify if applicable.
            return xacroComposite.Simplified;
        }
    }
}