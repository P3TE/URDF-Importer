/*
© Siemens AG, 2018-2019
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
using System.Collections.Generic;
using MeshProcess;
using System.IO;
using Mesh = UnityEngine.Mesh;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfGeometryCollision : UrdfGeometry
    {
        public static void Create(Transform parent, GeometryTypes geometryType,
            UrdfLinkDescription.Geometry geometry = null)
        {
            GameObject geometryGameObject = null;

            switch (geometryType)
            {
                case GeometryTypes.Box:
                    geometryGameObject = new GameObject(geometryType.ToString());
                    geometryGameObject.AddComponent<BoxCollider>();
                    break;
                case GeometryTypes.Cylinder:
                    geometryGameObject = CreateCylinderCollider();
                    break;
                case GeometryTypes.Sphere:
                    geometryGameObject = new GameObject(geometryType.ToString());
                    geometryGameObject.AddComponent<SphereCollider>();
                    break;
                case GeometryTypes.Mesh:
                    if (geometry != null)
                    {
                        geometryGameObject = CreateMeshCollider(geometry.mesh);
                    }
                    else
                    {
                        geometryGameObject = new GameObject(geometryType.ToString());
                        geometryGameObject.AddComponent<MeshCollider>();
                    }

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

        private static GameObject CreateMeshCollider(UrdfLinkDescription.Geometry.Mesh mesh)
        {
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                GameObject prefabObject = LocateAssetHandler.FindUrdfAsset<GameObject>(mesh.filename);
                if (prefabObject == null)
                {
                    Debug.LogError("Unable to create mesh collider for the mesh: " + mesh.filename);
                    return null;
                }

                GameObject meshObject = (GameObject)RuntimeUrdf.PrefabUtility_InstantiatePrefab(prefabObject);
                ConvertMeshToColliders(meshObject, location: mesh.filename);

                return meshObject;
            }

            return CreateMeshColliderRuntime(mesh);
        }

        private static GameObject CreateMeshColliderRuntime(UrdfLinkDescription.Geometry.Mesh mesh)
        {
            string meshFilePath = UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(mesh.filename, false);
            
            FileInfo meshFileInfo = new FileInfo(meshFilePath);
            Debug.Log($"Loading a {meshFileInfo.Extension}! {meshFilePath}");
            GameObject meshObject = TryLoadCollidersWithAssimp(meshFilePath);

            return meshObject;
        }

        private static void SetupTransforms(Assimp.Node node, Transform transform)
        {
            Assimp.Vector3D aScale = new Assimp.Vector3D();
            Assimp.Quaternion aQuat = new Assimp.Quaternion();
            Assimp.Vector3D aTranslation = new Assimp.Vector3D();
            node.Transform.Decompose(out aScale, out aQuat, out aTranslation);

            Quaternion uQuat = new Quaternion(aQuat.X, aQuat.Y, aQuat.Z, aQuat.W);
            Vector3 euler = uQuat.eulerAngles;
            transform.localScale = new Vector3(aScale.X, aScale.Y, aScale.Z);
            transform.localPosition = new Vector3(aTranslation.X, aTranslation.Y, aTranslation.Z);
            transform.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z);
        }

        private static void PrepareNode(ref List<Mesh> meshes, Assimp.Node node, GameObject nodeGO, Transform parent = null)
        {
            if (parent != null)
            {
                nodeGO.transform.SetParentAndAlign(parent, false);   
            }
                
            SetupTransforms(node, nodeGO.transform);
                
            foreach (int meshIndex in node.MeshIndices)
            {
                MeshCollider collider = nodeGO.AddComponent<MeshCollider>();
                collider.sharedMesh = meshes[meshIndex];
                collider.convex = true;
            }

            foreach (Assimp.Node child in node.Children)
            {
                PrepareNode(ref meshes, child, new GameObject(child.Name), nodeGO.transform);
            }
        }

        private static GameObject TryLoadCollidersWithAssimp(string meshFilePath)
        {
            if (!File.Exists(meshFilePath))
            {
                return null;
            }

            Assimp.AssimpContext importer = new Assimp.AssimpContext();
            Assimp.Scene scene = importer.ImportFile(meshFilePath);

            if (scene == null || !scene.HasMeshes)
            {
                return null;
            }

            GameObject baseObject = new GameObject(scene.RootNode.Name);

            List<Mesh> meshes = new List<Mesh>(scene.MeshCount);

            foreach (var m in scene.Meshes)
            {
                if (!m.HasVertices || !m.HasFaces)
                {
                    continue;
                }
                
                bool degenerateFacesWarning = false;

                List<Vector3> uVertices = new List<Vector3>(m.VertexCount);
                List<int> uIndices = new List<int>(m.FaceCount); //Not 100% on how this will behave if faces aren't triangulated.

                foreach (var v in m.Vertices)
                {
                    uVertices.Add(new Vector3(-v.X, v.Y, v.Z));
                }

                foreach (var f in m.Faces)
                {
                    if (f.IndexCount != 3)
                    {
                        degenerateFacesWarning = true;
                        continue;
                    }
                    
                    uIndices.Add(f.Indices[2]);
                    uIndices.Add(f.Indices[1]);
                    uIndices.Add(f.Indices[0]);
                }

                if (degenerateFacesWarning)
                {
                    RuntimeUrdf.AddImportWarning($"{m.Name} contains non-triangular faces!");
                }

                Mesh uMesh = new Mesh
                {
                    vertices = uVertices.ToArray(),
                    triangles = uIndices.ToArray()
                };

                meshes.Add(uMesh);
            }
            
            PrepareNode(ref meshes, scene.RootNode, baseObject);
            baseObject.transform.localRotation *= Quaternion.Euler(0.0f,0.0f, 90.0f); //Unverified: Rotation to match the output of the visual import.
            
            return baseObject;
        }

        private static GameObject CreateCylinderCollider()
        {
            GameObject gameObject = new GameObject("Cylinder");
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();

            UrdfLinkDescription.Geometry.Cylinder
                cylinder = new UrdfLinkDescription.Geometry.Cylinder(0.5, 2); //Default unity cylinder sizes

            meshFilter.sharedMesh = CreateCylinderMesh(cylinder);
            ConvertCylinderToCollider(meshFilter);

            return gameObject;
        }

        private static void ConvertCylinderToCollider(MeshFilter filter)
        {
            GameObject go = filter.gameObject;
            var collider = filter.sharedMesh;
            // Only create an asset if not runtime import
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                var packageRoot = UrdfAssetPathHandler.GetPackageRoot();
                var filePath =
                    RuntimeUrdf.AssetDatabase_GUIDToAssetPath(
                        RuntimeUrdf.AssetDatabase_CreateFolder($"{packageRoot}", "meshes"));
                var name = $"{filePath}/Cylinder.asset";
                Debug.Log($"Creating new cylinder file: {name}");
                RuntimeUrdf.AssetDatabase_CreateAsset(collider, name, uniquePath: true);
                RuntimeUrdf.AssetDatabase_SaveAssets();
            }

            MeshCollider current = go.AddComponent<MeshCollider>();
            current.sharedMesh = collider;
            current.convex = true;
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(filter);
        }

        public static void CreateMatchingMeshCollision(Transform parent, Transform visualToCopy)
        {
            if (visualToCopy.childCount == 0)
            {
                return;
            }

            GameObject objectToCopy = visualToCopy.GetChild(0).gameObject;
            GameObject prefabObject =
                (GameObject)RuntimeUrdf.PrefabUtility_GetCorrespondingObjectFromSource(objectToCopy);

            GameObject collisionObject;
            if (prefabObject != null)
            {
                collisionObject = (GameObject)RuntimeUrdf.PrefabUtility_InstantiatePrefab(prefabObject);
            }
            else
            {
                collisionObject = Object.Instantiate(objectToCopy);
            }

            collisionObject.name = objectToCopy.name;
            ConvertMeshToColliders(collisionObject);

            collisionObject.transform.SetParentAndAlign(parent);
        }

        private static void ConvertMeshToColliders(GameObject gameObject, string location = null, bool setConvex = true)
        {
            MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
            if (UrdfRobotExtensions.importsettings.convexMethod == ImportSettings.convexDecomposer.unity)
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    GameObject child = meshFilter.gameObject;
                    MeshCollider meshCollider = child.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;

                    meshCollider.convex = setConvex;

                    Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(meshFilter);
                }
            }
            else
            {
                string templateFileName = "";
                string filePath = "";
                int meshIndex = 0;
                if (!RuntimeUrdf.IsRuntimeMode() && location != null)
                {
                    string meshFilePath = UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(location, false);
                    templateFileName = Path.GetFileNameWithoutExtension(meshFilePath);
                    filePath = Path.GetDirectoryName(meshFilePath);
                }

                foreach (MeshFilter meshFilter in meshFilters)
                {
                    GameObject child = meshFilter.gameObject;
                    VHACD decomposer = child.AddComponent<VHACD>();
                    List<Mesh> colliderMeshes = decomposer.GenerateConvexMeshes(meshFilter.sharedMesh);
                    foreach (Mesh collider in colliderMeshes)
                    {
                        if (!RuntimeUrdf.IsRuntimeMode())
                        {
                            meshIndex++;
                            string name = $"{filePath}/{templateFileName}_{meshIndex}.asset";
                            Debug.Log($"Creating new mesh file: {name}");
                            RuntimeUrdf.AssetDatabase_CreateAsset(collider, name);
                            RuntimeUrdf.AssetDatabase_SaveAssets();
                        }

                        MeshCollider current = child.AddComponent<MeshCollider>();
                        current.sharedMesh = collider;
                        current.convex = setConvex;
                    }

                    Component.DestroyImmediate(child.GetComponent<VHACD>());
                    Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(meshFilter);
                }
            }
        }
    }
}