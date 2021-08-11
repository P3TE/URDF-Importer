using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Urdf.Extensions
{
    public class UrdfUnityMaterial
    {

        
        public interface IWritesToUrdf
        {
            void WriteToUrdf(XmlWriter writer);
        }
        
        public class ExportTexture : IWritesToUrdf
        {
            public Texture unityTexture;

            public string exportFilePath;

            public string shaderPropertyName = "";

            public ExportTexture()
            {
                
            }

            public ExportTexture(ExportTexture other)
            {
                this.unityTexture = other.unityTexture;
                this.exportFilePath = other.exportFilePath;
                this.shaderPropertyName = other.shaderPropertyName;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                
                writer.WriteStartElement("texture");
                if ("" != shaderPropertyName)
                {
                    writer.WriteAttributeString("property", shaderPropertyName);
                }
                writer.WriteAttributeString("filename", exportFilePath);
                writer.WriteEndElement();
                
                /*
                writer.WriteStartElement("material");
                writer.WriteAttributeString("name", name);

                color?.WriteToUrdf(writer);
                texture?.WriteToUrdf(writer);

                writer.WriteEndElement();*/
            }
            
            public void ExportTextureData()
            {
                string oldTexturePath = UrdfAssetPathHandler.GetFullAssetPath(RuntimeUrdf.AssetDatabase_GetAssetPath(unityTexture));
                string newTexturePath = UrdfExportPathHandler.GetNewMeshTexturePath(Path.GetFileName(oldTexturePath));
                if (oldTexturePath != newTexturePath)
                {
                    string parentDirectoryPath = Directory.GetParent(newTexturePath).FullName;
                    if (!Directory.Exists(parentDirectoryPath))
                    {
                        Directory.CreateDirectory(parentDirectoryPath);
                    }
                    File.Copy(oldTexturePath, newTexturePath, true);
                }

                exportFilePath = UrdfExportPathHandler.GetPackagePathForMeshTexture(newTexturePath);
            }
        }

        public class ExportColor : IWritesToUrdf
        {
            public Color color;

            public double[] AsRgbaDoubleArray
            {
                get
                {
                    return new double[] {color.r, color.g, color.b, color.a};
                }
            }
            
            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("color");
                writer.WriteAttributeString("rgba", AsRgbaDoubleArray.DoubleArrayToString());
                writer.WriteEndElement();
            }
        }

        public class ExportMaterial : IWritesToUrdf
        {
            
            public Material unityMaterial;
            
            public string exportedName;

            public ExportColor color;
            public ExportTexture exportedTexture;

            public List<IWritesToUrdf> unityElements = new List<IWritesToUrdf>();

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("material");
                writer.WriteAttributeString("name", exportedName);
                color?.WriteToUrdf(writer);
                exportedTexture?.WriteToUrdf(writer);

                if (unityElements.Count > 0)
                {
                    writer.WriteStartElement("unity");
                    foreach (IWritesToUrdf unityElement in unityElements)
                    {
                        unityElement.WriteToUrdf(writer);
                    }
                    writer.WriteEndElement();
                }
                
                //writer.WriteAttributeString("filename", exportFilePath);
                writer.WriteEndElement();
            }
        }


        private static List<ExportTexture> _exportTextures = new List<ExportTexture>();
        private static List<ExportMaterial> _exportMaterials = new List<ExportMaterial>();

        public static void ClearPreviousExportData()
        {
            _exportTextures.Clear();
            _exportMaterials.Clear();
        }

        public static string SanitiseName(string unityName)
        {
            const string _InstancePostfix = " (Instance)";
            while (unityName.EndsWith(_InstancePostfix))
            {
                unityName = unityName.Remove(unityName.Length - _InstancePostfix.Length, _InstancePostfix.Length);
            }

            return unityName;
        }

        private static string GetMaterialName(Material unityMaterial)
        {
            return SanitiseName(unityMaterial.name);
        }

        private static ExportMaterial GetPreviouslyExportedMaterial(Material unityMaterial)
        {
            foreach (ExportMaterial exportMaterial in _exportMaterials)
            {
                if (exportMaterial.exportedName == GetMaterialName(unityMaterial))
                {
                    return exportMaterial;
                }
            }
            return null;
        }
        
        private static ExportTexture GetPreviouslyExportedTexture(Texture unityTexture)
        {
            foreach (ExportTexture exportTexture in _exportTextures)
            {
                if (exportTexture.unityTexture == unityTexture)
                {
                    return exportTexture;
                }
            }
            return null;
        }

        private static ExportTexture GenerateExportTexture(Texture from)
        {
            ExportTexture result = GetPreviouslyExportedTexture(from);
            if (result == null)
            {
                result = new ExportTexture();
                result.unityTexture = from;
                result.ExportTextureData();
                
                _exportTextures.Add(result);
            }
            else
            {
                result = new ExportTexture(result);
            }

            return result;
        }
        
        public static ExportMaterial GenerateAndExportNewMaterial(Material unityMaterial)
        {
            if (unityMaterial == null)
            {
                return null;
            }
            ExportMaterial previouslyExportedMaterial = GetPreviouslyExportedMaterial(unityMaterial);
            if (previouslyExportedMaterial != null)
            {
                //Already exported.
                return null;
            }

            ExportMaterial newExportMaterial = new ExportMaterial();
            newExportMaterial.unityMaterial = unityMaterial;

            newExportMaterial.exportedName = GetMaterialName(unityMaterial); //TODO - Matching up textures...

            //TODO the existing unity code has either a color or a texture, never both, should we only have one? Does a colour override a texture? 
            newExportMaterial.color = new ExportColor
            {
                color = unityMaterial.color
            };
            if (unityMaterial.mainTexture != null)
            {
                newExportMaterial.exportedTexture = GenerateExportTexture(unityMaterial.mainTexture);
                newExportMaterial.exportedTexture.shaderPropertyName = "";
            }
            
#if UNITY_EDITOR
            //unityMaterial.mainTexture
            for(int i=0; i< UnityEditor.ShaderUtil.GetPropertyCount(unityMaterial.shader); i++) {

                if(UnityEditor.ShaderUtil.GetPropertyType(unityMaterial.shader, i) == UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv) {
                    Texture texture = unityMaterial.GetTexture(UnityEditor.ShaderUtil.GetPropertyName(unityMaterial.shader, i));
                    if (texture == null)
                    {
                        continue;
                    }
                    ExportTexture exportTexture = GenerateExportTexture(texture);
                    exportTexture.shaderPropertyName = UnityEditor.ShaderUtil.GetPropertyName(unityMaterial.shader, i);
                    newExportMaterial.unityElements.Add(exportTexture);
                } else if(UnityEditor.ShaderUtil.GetPropertyType(unityMaterial.shader, i) == UnityEditor.ShaderUtil.ShaderPropertyType.Color) {
                    //TODO - We should consider implementing the other shader components, but for now, eh.
                }
                
            }
#endif

            _exportMaterials.Add(newExportMaterial);
            return newExportMaterial;
            
        }


    }
}