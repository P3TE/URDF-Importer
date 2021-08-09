#if (UNITY_EDITOR)
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Editor
{
    
    
    
    public class ConvertToUrdfJoint {
        
        [MenuItem("GameObject/URDF/Convert to URDF Joint")]
        private static void ConvertToUrdfJointImpl() {
            GameObject selectedObject = Selection.activeObject as GameObject;
            ConvertSingleToUrdf(selectedObject);
        }
        
        [MenuItem("GameObject/URDF/Convert to URDF Joint (Recursive)")]
        private static void ConvertToUrdfJointImplRecursive() {
            GameObject selectedObject = Selection.activeObject as GameObject;
            ConvertSingleToUrdf(selectedObject, true);
        }

        private static void ConvertSingleToUrdf(GameObject toConvert, bool recursive = false)
        {
            
            if (toConvert.TryGetComponent<UrdfVisuals>(out _))
            {
                return;
            }
            if (toConvert.TryGetComponent<UrdfCollisions>(out _))
            {
                return;
            }
            
            if (!toConvert.TryGetComponent<UrdfLink>(out _))
            {
                UrdfLink urdfLink = toConvert.AddComponent<UrdfLink>();
            }
            
            if (!toConvert.TryGetComponent<UrdfInertial>(out _))
            {
                UrdfInertial.Create(toConvert);
                ArticulationBody articulationBody = toConvert.GetComponent<ArticulationBody>();
                articulationBody.mass = 0.01f;
            }

            if (!toConvert.TryGetComponent<UrdfJoint>(out _))
            {
                UrdfJointFixed urdfLinkFixed = toConvert.AddComponent<UrdfJointFixed>();
            }
            

            bool createVisuals = true;
            bool createCollisions = true;
            for (int i = 0; i < toConvert.transform.childCount; i++)
            {
                GameObject child = toConvert.transform.GetChild(i).gameObject;
                if (child.TryGetComponent<UrdfVisuals>(out _))
                {
                    createVisuals = false;
                }
                if (child.TryGetComponent<UrdfCollisions>(out _))
                {
                    createCollisions = false;
                }
            }

            if (createVisuals)
            {
                GameObject visuals = new GameObject("Visuals");
                visuals.transform.SetParent(toConvert.transform);
                visuals.AddComponent<UrdfVisuals>();
                visuals.transform.SetSiblingIndex(0);
            }

            if (createCollisions)
            {
                GameObject collisions = new GameObject("Collisions");
                collisions.transform.SetParent(toConvert.transform);
                collisions.AddComponent<UrdfCollisions>();
                collisions.transform.SetSiblingIndex(1);
            }

            if (recursive)
            {
                for (int i = 0; i < toConvert.transform.childCount; i++)
                {
                    GameObject child = toConvert.transform.GetChild(i).gameObject;
                    ConvertSingleToUrdf(child, true);
                }
            }
            

        }
        
    }
}
#endif
