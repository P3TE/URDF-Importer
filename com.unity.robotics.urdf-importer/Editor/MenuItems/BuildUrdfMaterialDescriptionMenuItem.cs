#if (UNITY_EDITOR)
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Editor
{
    
    
    
    public class BuildUrdfMaterialDescriptionMenuItem {
        
        [MenuItem("Assets/URDF/Build Urdf Material Description")]
        private static void ConvertToUrdfJointImpl() {
            Material selectedObject = Selection.activeObject as Material;
            if (selectedObject == null)
            {
                Debug.LogError("Please select a Material.");
                return;
            }
            UrdfMaterialDescription urdfMaterialDescription = new UrdfMaterialDescription(selectedObject);

            string result;
            using (var stringWriter = new StringWriter()) {
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings()
                {
                    Indent = true,
                    OmitXmlDeclaration = true
                })) {
                    // Build Xml with xw.
                    urdfMaterialDescription.WriteToUrdf(xmlWriter);
                }
                result = stringWriter.ToString();
            }
            Debug.Log(result);
        }
    }
}
#endif
