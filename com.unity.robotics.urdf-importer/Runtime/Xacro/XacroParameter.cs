using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Urdf.Xacro
{
    public abstract class XacroParameter
    {
        public abstract string ResolveStringParameter(XacroComponentDescription context);
    }

    public class XacroParameterString : XacroParameter
    {
        private string value;

        public XacroParameterString(string value)
        {
            this.value = value;
        }

        public override string ResolveStringParameter(XacroComponentDescription context)
        {
            return value;
        }
    }
    
    public class XacroRospackCommand : XacroParameter
    {
        private string[] args;

        public XacroRospackCommand(string[] args)
        {
            this.args = args;
        }

        private string ResolveArgRospackCommand(XacroComponentDescription context)
        {
            if (EnsureArgumentCount(2) < 0) return "";
            string argumentName = args[1];
            return context.GetParameter(argumentName).ResolveStringParameter(context);
        }
        
        private string ResolveFindRospackCommand()
        {
            if (EnsureArgumentCount(2) < 0) return "";
            string rosPackageName = args[1];
            if (RosPackagePathHelper.TryResolvePackageNamePath(rosPackageName, out string packagePath))
            {
                return packagePath;
            }
            Debug.LogError($"Unable to find package with name {packagePath}, check your ROS_PACKAGE_PATH");
            return "";
        }

        private int EnsureArgumentCount(int requiredCount)
        {
            if (args.Length < requiredCount)
            {
                Debug.LogError($"Rospack Command {ToOriginalString()} does not contain enough arguments!");
            }
            if (args.Length > 2)
            {
                Debug.LogWarning($"Rospack Command {ToOriginalString()} contains too many arguments, expecting {requiredCount}, found {args.Length}!");
            }
            return args.Length - requiredCount;
        }

        private string ToOriginalString()
        {
            StringBuilder result = new StringBuilder();
            for (var index = 0; index < args.Length; index++)
            {
                if (index > 0)
                {
                    result.Append(" ");
                }
                string arg = args[index];
                result.Append(arg);
            }
            return result.ToString();
        }

        public override string ResolveStringParameter(XacroComponentDescription context)
        {

            if (args.Length == 0)
            {
                Debug.LogError($"XacroRospackCommand has no arguments!");
                return "";
            }
            string commandId = args[0];
            switch (commandId)
            {
                case "arg":
                    return ResolveArgRospackCommand(context);
                case "find":
                    return ResolveFindRospackCommand();
            }
            Debug.LogError($"Unknown Rospack Command {commandId}");
            return ToOriginalString();
        }
    }

    public class XacroParameterReference : XacroParameter
    {
        private string parameterName;

        public XacroParameterReference(string parameterName)
        {
            this.parameterName = parameterName;
        }

        public override string ResolveStringParameter(XacroComponentDescription context)
        {
            return context.GetParameter(parameterName).ResolveStringParameter(context);
        }
    }

    public class XacroCompositeParameter : XacroParameter
    {
        public List<XacroParameter> components = new List<XacroParameter>();

        public XacroCompositeParameter()
        {
        }

        public override string ResolveStringParameter(XacroComponentDescription context)
        {
            StringBuilder result = new StringBuilder();
            foreach (XacroParameter component in components)
            {
                result.Append(component.ResolveStringParameter(context));
            }
            return result.ToString();
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