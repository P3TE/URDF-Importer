using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Unity.Robotics.UrdfImporter.Urdf.RuntimeImport;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Robotics.UrdfImporter
{
    #region Material Components

    public abstract class UrdfMaterialPropertyDescription
    {

        private string _PropertyId
        {
            get;
        }

        public const string kPropertyAttributeName = "property";

        public string propertyName = "";
        
        protected UrdfMaterialPropertyDescription(string propertyId, XElement element) : this(propertyId)
        {
            //Reading from xml.
            XAttribute propertyNameAttribute = element.Attribute(kPropertyAttributeName);
            if (propertyNameAttribute != null)
            {
                this.propertyName = propertyNameAttribute.Value;
            }
        }

        protected UrdfMaterialPropertyDescription(string propertyId)
        {
            _PropertyId = propertyId;
            //Building from Unity.
        }

        public void WriteToUrdf(XmlWriter writer)
        {
            writer.WriteStartElement(_PropertyId);
            WritePropertyNameAttributeIfApplicable(writer);
            WriteContentsToUrdf(writer);
            writer.WriteEndElement();
        }

        protected void WritePropertyNameAttributeIfApplicable(XmlWriter writer)
        {
            if (!string.IsNullOrEmpty(propertyName))
            {
                writer.WriteAttributeString(kPropertyAttributeName, propertyName);
            }
        }

        public abstract void WriteContentsToUrdf(XmlWriter writer);

        public abstract void ApplyPropertyToMaterial(Material material);
    }

    public class UrdfColorDescription : UrdfMaterialPropertyDescription
    {
        
        public new const string PropertyId = "color";
        
        public double[] rgba;

        public UrdfColorDescription(XElement element) : base(PropertyId, element)
        {
            rgba = element.Attribute("rgba").ReadDoubleArray(); // required
        }

        public UrdfColorDescription(Color color) : base(PropertyId)
        {
            rgba = new double[] {color.r, color.g, color.b, color.a};
        }

        public override void WriteContentsToUrdf(XmlWriter writer)
        {
            writer.WriteAttributeString("rgba", rgba.DoubleArrayToString());
        }

        public override void ApplyPropertyToMaterial(Material material)
        {
            string usedPropertyName = propertyName;
            if (string.IsNullOrEmpty(usedPropertyName))
            {
                usedPropertyName = MaterialExtensions.DefaultMaterialColourPropertyId;
            }
            material.SetColor(usedPropertyName, AsColour());
        }

        public Color AsColour()
        {
            return new Color(
                (float)rgba[0],
                (float)rgba[1],
                (float)rgba[2],
                (float)rgba[3]);
        }
    }
    
    public class UrdfFloatDescription : UrdfMaterialPropertyDescription
    {
        
        public new const string PropertyId = "float";
        private const string ValueNameId = "value";
        
        public float value;

        public UrdfFloatDescription(XElement element) : base(PropertyId, element)
        {
            value = (float) Convert.ToDouble(element.Attribute(ValueNameId));
        }

        public UrdfFloatDescription(float value) : base(PropertyId)
        {
            this.value = value;
        }

        public override void WriteContentsToUrdf(XmlWriter writer)
        {
            writer.WriteAttributeString(ValueNameId, value.ToString("R"));
        }

        public override void ApplyPropertyToMaterial(Material material)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                Debug.LogWarning("Found float material property without a valid propertyName!");
                return;
            }
            material.SetFloat(propertyName, value);
        }
    }

    public class UrdfTextureDescription : UrdfMaterialPropertyDescription
    {
        
        public new const string PropertyId = "texture";
        
        public string filename;

        public Texture texture;

        public string AbsoluteFilePath => UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(filename);

        public override void WriteContentsToUrdf(XmlWriter writer)
        {
            writer.WriteAttributeString("filename", filename);
        }

        public UrdfTextureDescription(XElement element) : base(PropertyId, element)
        {
            filename = (string) element.Attribute("filename"); // required
        }

        public UrdfTextureDescription(Texture texture) : base(PropertyId)
        {
            this.texture = texture;
            //TODO - How to get a texture from a filename and vice versa
            this.filename = @"${PATH}/" + texture.name + ".png";
        }
        
        public override void ApplyPropertyToMaterial(Material material)
        {
            if (texture == null)
            {
                Debug.LogWarning("TODO - Load a texture...");
            }

            texture = RuntimeTextureManager.LoadTextureFromFile(AbsoluteFilePath);
            
            if (string.IsNullOrEmpty(propertyName))
            {
                material.mainTexture = texture;
            }
            else
            {
                material.SetTexture(propertyName, texture);
            }
        }
    }

    public class UrdfUnityMaterialExtensionDescription : UrdfMaterialPropertyDescription
    {
        
        public new const string PropertyId = "unity";
        
        private List<UrdfMaterialPropertyDescription> propertyDescriptions = new List<UrdfMaterialPropertyDescription>();

        public UrdfUnityMaterialExtensionDescription(XElement element) : base(PropertyId, element)
        {
            foreach (XElement xElement in element.Elements())
            {
                switch (xElement.Name.LocalName)
                {
                    case UrdfColorDescription.PropertyId:
                        propertyDescriptions.Add(new UrdfColorDescription(xElement));
                        break;
                    case UrdfTextureDescription.PropertyId:
                        propertyDescriptions.Add(new UrdfTextureDescription(xElement));
                        break;
                    case UrdfFloatDescription.PropertyId:
                        propertyDescriptions.Add(new UrdfFloatDescription(xElement));
                        break;
                    default:
                        //TODO - Log a warning - unhandled element type.
                        break;
                }
            }
        }

        public UrdfUnityMaterialExtensionDescription(Material unityMaterial) : base(PropertyId)
        {
#if UNITY_EDITOR
            for(int i=0; i< ShaderUtil.GetPropertyCount(unityMaterial.shader); i++)
            {

                ShaderUtil.ShaderPropertyType shaderPropertyType =
                    ShaderUtil.GetPropertyType(unityMaterial.shader, i);
                
                string shaderPropertyName = ShaderUtil.GetPropertyName(unityMaterial.shader, i);

                UrdfMaterialPropertyDescription urdfMaterialPropertyDescription = null;

                switch (shaderPropertyType)
                {
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        Texture texture = unityMaterial.GetTexture(shaderPropertyName);
                        if (texture == null)
                        {
                            continue;
                        }
                        urdfMaterialPropertyDescription = new UrdfTextureDescription(texture);
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        Vector4 colourAsVector4 = unityMaterial.GetVector(shaderPropertyName);
                        Vector4 propertyDefaultVectorValue = unityMaterial.shader.GetPropertyDefaultVectorValue(i);
                        if (colourAsVector4 == propertyDefaultVectorValue)
                        {
                            continue;
                        }
                        Color colour = unityMaterial.GetColor(shaderPropertyName);
                        urdfMaterialPropertyDescription = new UrdfColorDescription(colour);
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                        float floatValue = unityMaterial.GetFloat(shaderPropertyName);
                        float defaultFloatValue = unityMaterial.shader.GetPropertyDefaultFloatValue(i);
                        if (defaultFloatValue == floatValue)
                        {
                            continue;
                        }
                        urdfMaterialPropertyDescription = new UrdfFloatDescription(floatValue);
                        break;
                    default:
                        //TODO - Unhandled type...
                        break;
                }

                if (urdfMaterialPropertyDescription != null)
                {
                    urdfMaterialPropertyDescription.propertyName = shaderPropertyName;
                    propertyDescriptions.Add(urdfMaterialPropertyDescription);
                }
            }
#endif
        }

        public override void WriteContentsToUrdf(XmlWriter writer)
        {
            foreach (UrdfMaterialPropertyDescription propertyDescription in propertyDescriptions)
            {
                propertyDescription.WriteToUrdf(writer);
            }
        }

        public override void ApplyPropertyToMaterial(Material material)
        {
            foreach (UrdfMaterialPropertyDescription propertyDescription in propertyDescriptions)
            {
                propertyDescription.ApplyPropertyToMaterial(material);
            }
        }
    }
    
    #endregion
    
    public class UrdfMaterialDescription
    {
        
        public static Dictionary<string, UrdfLinkDescription.Visual.Material> Materials =
            new Dictionary<string, UrdfLinkDescription.Visual.Material>();
        
        public const string kMaterialPropertyName = "material";
        
        public string name;
        public UrdfColorDescription color;
        public UrdfTextureDescription texture;
        public UrdfUnityMaterialExtensionDescription unityAdditionalAttributes;

        public UrdfMaterialDescription(XElement xElement)
        {
            this.name = (string) xElement.Attribute("name"); // required
            
            XElement colorElement = xElement.Element(UrdfColorDescription.PropertyId);
            if (colorElement != null) color = new UrdfColorDescription(colorElement);
            
            XElement textureElement = xElement.Element(UrdfTextureDescription.PropertyId);
            if (textureElement != null) texture = new UrdfTextureDescription(textureElement);
            
            XElement unityAdditionAttributesElement = xElement.Element(UrdfUnityMaterialExtensionDescription.PropertyId);
            if (unityAdditionalAttributes != null) unityAdditionalAttributes = new UrdfUnityMaterialExtensionDescription(unityAdditionAttributesElement);
        }

        public UrdfMaterialDescription(Material material)
        {
#if UNITY_EDITOR
            color = new UrdfColorDescription(material.color);
            if (material.mainTexture != null)
            {
                texture = new UrdfTextureDescription(material.mainTexture);
            }
            unityAdditionalAttributes = new UrdfUnityMaterialExtensionDescription(material);
#else
            //Nothing done.
#endif
        }

        public void WriteToUrdf(XmlWriter writer)
        {
            writer.WriteStartElement(kMaterialPropertyName);
            color?.WriteToUrdf(writer);
            texture?.WriteToUrdf(writer);
            unityAdditionalAttributes?.WriteToUrdf(writer);
            writer.WriteEndElement();
        }
        
    }

    public static class UrdfMaterialDescriptionExtensions
    {
        public static void PopulateMaterialProperties(this UrdfMaterialDescription urdfMaterial, Material material)
        {
            urdfMaterial.color?.ApplyPropertyToMaterial(material);
            urdfMaterial.texture?.ApplyPropertyToMaterial(material);
            urdfMaterial.unityAdditionalAttributes?.ApplyPropertyToMaterial(material);
        }
    }
}