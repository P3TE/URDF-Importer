using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.Robotics.UrdfImporter.Urdf.Xacro
{
    public abstract class XacroParameter
    {

        public abstract string StringParameter
        {
            get;
        }
    }

    public class XacroParameterString : XacroParameter
    {
        private string value;

        public XacroParameterString(string value)
        {
            this.value = value;
        }

        public override string StringParameter => value;
    }
    
    public class XacroParameterCommand : XacroParameter
    {
        private string[] args;

        public XacroParameterCommand(string[] args)
        {
            this.args = args;
        }

        public override string StringParameter => throw new NotImplementedException("TODO");
    }

    public class XacroCompositeParameter : XacroParameter
    {
        public List<XacroParameter> components = new List<XacroParameter>();

        public XacroCompositeParameter()
        {
        }

        public override string StringParameter
        {
            get
            {
                StringBuilder result = new StringBuilder();
                foreach (XacroParameter component in components)
                {
                    result.Append(component.StringParameter);
                }
                return result.ToString();
            }
        }

        public XacroParameter Simplified
        {
            get
            {
                if (components.Count == 0)
                {
                    return new XacroParameterString("");
                } else if (components.Count == 1)
                {
                    return components[0];
                }
                return this;
            }
        }
    }
}