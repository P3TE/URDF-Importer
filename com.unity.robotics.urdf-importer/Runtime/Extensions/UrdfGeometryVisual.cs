/*
© Siemens AG, 2018
Author: Suzannah Smith (suzannah.smith@siemens.com)
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using UnityEngine;
using UnityMeshImporter;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfGeometryVisual : UrdfGeometry
    {
        public static void Create(Transform parent, GeometryTypes geometryType, UrdfLinkDescription.Geometry geometry = null)
        {
            GameObject geometryGameObject = null;

            switch (geometryType)
            {
                case GeometryTypes.Box:
                    geometryGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    geometryGameObject.transform.DestroyImmediateIfExists<BoxCollider>();
                    break;
                case GeometryTypes.Cylinder:
                    geometryGameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    geometryGameObject.transform.DestroyImmediateIfExists<CapsuleCollider>();
                    break;
                case GeometryTypes.Capsule:
                    geometryGameObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    geometryGameObject.transform.DestroyImmediateIfExists<CapsuleCollider>();
                    break;
                case GeometryTypes.Sphere:
                    geometryGameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    geometryGameObject.transform.DestroyImmediateIfExists<SphereCollider>();
                    break;
                case GeometryTypes.Mesh:
                        if (geometry != null)
                        {
                            geometryGameObject = CreateMeshVisual(geometry.mesh);
                        }
                        //else, let user add their own mesh gameObject
                    break;
            }

            if (geometryGameObject != null)
            {
                geometryGameObject.transform.SetParentAndAlign(parent);
                if (geometry != null)
                {
                    SetScale(parent, geometry, geometryType);
                }
            }
        }

        private static GameObject CreateMeshVisual(UrdfLinkDescription.Geometry.Mesh mesh)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                GameObject meshObject = LocateAssetHandler.FindUrdfAsset<GameObject>(mesh.filename);
                return meshObject == null ? null : (GameObject)RuntimeUrdf.PrefabUtility_InstantiatePrefab(meshObject);
            }
#endif
            return CreateMeshVisualRuntime(mesh);
        }

        private static GameObject CreateMeshVisualRuntime(UrdfLinkDescription.Geometry.Mesh mesh)
        {
            GameObject meshObject = null;
            if (!string.IsNullOrEmpty(mesh.filename))
            {
                try 
                {
                    string meshFilePath = UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(mesh.filename, false);
                    if (meshFilePath.ToLower().EndsWith(".stl"))
                    {
                        meshObject = StlAssetPostProcessor.CreateStlGameObjectRuntime(meshFilePath);
                    }
                    else if (meshFilePath.ToLower().EndsWith(".dae"))
                    {
                        float globalScale = ColladaAssetPostProcessor.ReadGlobalScale(meshFilePath);
                        meshObject = MeshImporter.Load(meshFilePath, globalScale, globalScale, globalScale);
                        
                        Quaternion rotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
                        meshObject.transform.rotation = rotation * meshObject.transform.rotation;
                        
                        /* TODO - Review
                        // I understand why this has been made; however, this means that different mesh formats with the same export settings behave differently.
                        // Blender appears to not actually change this value when selecting other up axes, it is always Z_UP.
                        // RVIZ and Gazebo appear to ignore this value. Changing it does not cause the mesh to rotate.
                        // In order to have a standard FLU coordinate frame, always export all meshes X Forward, Z Up out of Blender (left and right should align with the named views when editing).
                        if (meshObject != null) 
                        {
                            ColladaAssetPostProcessor.ApplyColladaOrientation(meshObject, meshFilePath);
                        }
                        */
                    }
                    else if (meshFilePath.ToLower().EndsWith(".obj"))
                    {
                        meshObject = MeshImporter.Load(meshFilePath);
                        
                        Quaternion rotation = Quaternion.Euler(-90.0f, 0.0f, 90.0f);
                        meshObject.transform.rotation = rotation * meshObject.transform.rotation;
                    }
                }
                catch (Exception ex) 
                {
                    Debug.LogAssertion(ex);
                }
                
                if (meshObject == null) 
                {
                    Debug.LogError("Unable to load visual mesh: " + mesh.filename);
                }
            }
            return meshObject;
        }
    }
}
