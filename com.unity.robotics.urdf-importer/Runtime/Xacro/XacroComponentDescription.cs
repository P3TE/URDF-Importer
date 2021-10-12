using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Unity.Robotics.UrdfImporter.Urdf.Xacro
{
    
    
    
    public class XacroComponentDescription
    {
        private Dictionary<string, XacroParameter> attributes = new();


        public void LoadXacro(string filename)
        {
            XDocument xdoc = XDocument.Load(filename);
            XElement node = xdoc.Element("robot");
            LoadXacro(node);
        }
        
        public void LoadXacro(XElement node)
        {

            foreach (XAttribute xAttribute in node.Attributes())
            {
                attributes.Add(xAttribute.Name.NamespaceName, ExpandXacroParameter(xAttribute.Value));
            }

            foreach (XNode xNode in node.Nodes())
            {
                
            }
            
        }
        
        
        
        
        

        public static XacroParameter ExpandXacroParameter(string rawText)
        {

            Regex pattern = new Regex(@"(\$\([\w ]*\))");
            Match match = pattern.Match(rawText);

            XacroCompositeParameter xacroComposite = new XacroCompositeParameter();

            int currentIndex = 0;
            foreach (Capture matchCapture in match.Captures)
            {
                
                //Add the preceding text.
                if (matchCapture.Index != currentIndex)
                {
                    string precedingText = rawText.Substring(currentIndex, matchCapture.Index - currentIndex);
                    xacroComposite.components.Append(new XacroParameterString(precedingText));
                }

                //Extract the contents of the command.
                string xacroCommandContents = rawText.Substring(matchCapture.Index + 2, matchCapture.Length - 3);

                //Extract the arguments of the command.
                string[] xacroCommandSplit = xacroCommandContents.Split(' ');
                XacroParameterCommand xacroCommandParameter = new XacroParameterCommand(xacroCommandSplit);
                xacroComposite.components.Append(xacroCommandParameter);

                //Update the current index.
                currentIndex = matchCapture.Index + match.Length;

            }
            
            //Add any remaining text
            if (rawText.Length != currentIndex)
            {
                string precedingText = rawText.Substring(currentIndex, rawText.Length - currentIndex);
                xacroComposite.components.Append(new XacroParameterString(precedingText));
            }

            //Simplify if applicable.
            return xacroComposite.Simplified;

        }
        
        
        
        
    }
}