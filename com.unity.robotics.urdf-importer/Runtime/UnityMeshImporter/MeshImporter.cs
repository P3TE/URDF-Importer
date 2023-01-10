/* 
 *  MIT License
 *  
 *  Copyright (c) 2019 UnityMeshImporter - Dongho Kang
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */
#if !ENABLE_IL2CPP
#define ASSIMP_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using System.IO;
#if ASSIMP_SUPPORTED
using Assimp;
using Unity.Robotics.UrdfImporter.Urdf.RuntimeImport;
using UnityEditor.SceneTemplate;
#endif
using Unity.Robotics;
using UnityEngine;
using Material = UnityEngine.Material;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mesh = UnityEngine.Mesh;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;

namespace UnityMeshImporter
{
    class MeshMaterialBinding
    {
        private string meshName;
        private UnityEngine.Mesh mesh;
        private UnityEngine.Material material;
        private AdditionalMeshImportData additionalMeshImportData;
        
        private MeshMaterialBinding() {}    // Do not allow default constructor

        public MeshMaterialBinding(string meshName, Mesh mesh, Material material, AdditionalMeshImportData additionalMeshImportData)
        {
            this.meshName = meshName;
            this.mesh = mesh;
            this.material = material;
            this.additionalMeshImportData = additionalMeshImportData;
        }

        public Mesh Mesh { get => mesh; }
        public Material Material { get => material; }
        public string MeshName { get => meshName; }

        public AdditionalMeshImportData GetAdditionalMeshImportData => additionalMeshImportData;
    }

    [System.Serializable]
    public class AdditionalMeshImportData
    {
        public string materialName = "";
    }

    public class AdditionalMeshImportGameObject : MonoBehaviour
    {
        public AdditionalMeshImportData additionalMeshImportData = null;

        private void Start()
        {
            //Remove the game object, not needed any more.
            Destroy(this);
        }
    }
    
    public class MeshImporter
    {
        public static GameObject Load(string meshPath, float scaleX=1, float scaleY=1, float scaleZ=1)
        {
            if (RuntimeAssetCache.Instance.GetAssetFromCache(meshPath, out GameObject mesh))
            {
                GameObject go = Object.Instantiate(mesh);
                go.SetActive(true);
                return go;
            }
            
#if ASSIMP_SUPPORTED            
            if (!File.Exists(meshPath))
            {
                return null;
            }

            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(meshPath);
            if (scene == null) 
            {
                return null;
            }

            string parentDir = Directory.GetParent(meshPath).FullName;

            // Materials
            //List<UnityEngine.Material> uMaterials = new List<Material>();
            List<Tuple<UnityEngine.Material, AdditionalMeshImportData>> uMaterials = new List<Tuple<UnityEngine.Material, AdditionalMeshImportData>>();
            //AdditionalMeshImportData
            if (scene.HasMaterials)
            {
                foreach (var m in scene.Materials)
                {
                    UnityEngine.Material uMaterial = MaterialExtensions.CreateBasicMaterial();

                    // Albedo
                    if (m.HasColorDiffuse)
                    {
                        Color color = new Color(
                            m.ColorDiffuse.R,
                            m.ColorDiffuse.G,
                            m.ColorDiffuse.B,
                            m.ColorDiffuse.A
                        );
                        MaterialExtensions.SetMaterialColor(uMaterial, color);
                    }

                    // Emission
                    if (m.HasColorEmissive)
                    {
                        Color color = new Color(
                            m.ColorEmissive.R,
                            m.ColorEmissive.G,
                            m.ColorEmissive.B,
                            m.ColorEmissive.A
                        );
                        MaterialExtensions.SetMaterialEmissionColor(uMaterial, color);
                    }
                    
                    // Reflectivity
                    if (m.HasReflectivity)
                    {
                        uMaterial.SetFloat("_Glossiness", m.Reflectivity);
                    }
                    
                    // Texture
                    if (m.HasTextureDiffuse)
                    {
                        /* TODO: Chances are this was commented out as all of our materials are coming through from the xacro.
                                It might still be good however to still support loading from the mesh file as a fallback.
                                Turn this back on and find out if it does nothing bad :)

                        string texturePath = Path.Combine(parentDir, m.TextureDiffuse.FilePath);
                        Texture2D uTexture = RuntimeAssetCache.Instance.LoadTextureFromFile(texturePath);
                        uMaterial.mainTexture = uTexture;
                        uMaterial.SetTexture("_MainTex", uTexture);
                        */
                    }

                    AdditionalMeshImportData additionalMeshImportData = new AdditionalMeshImportData()
                    {
                        materialName = m.Name
                    };
                    uMaterials.Add(new Tuple<Material, AdditionalMeshImportData>(uMaterial, additionalMeshImportData));
                }
            }

            // Mesh
            List<MeshMaterialBinding> uMeshAndMats = new List<MeshMaterialBinding>();
            if (scene.HasMeshes)
            {
                foreach (var m in scene.Meshes)
                {
                    List<Vector3> uVertices = new List<Vector3>();
                    List<Vector3> uNormals = new List<Vector3>();
                    List<Vector2> uUv = new List<Vector2>();
                    List<int> uIndices = new List<int>();
                    
                    // Vertices
                    if (m.HasVertices)
                    {
                        foreach (var v in m.Vertices)
                        {
                            uVertices.Add(new Vector3(v.X, v.Y, v.Z));
                        }
                    }

                    // Normals
                    if (m.HasNormals)
                    {
                        foreach (var n in m.Normals)
                        {
                            uNormals.Add(new Vector3(n.X, n.Y, n.Z));
                        }
                    }

                    // Triangles
                    if (m.HasFaces)
                    {
                        foreach (var f in m.Faces)
                        {
                            // Ignore degenerate faces
                            if (f.IndexCount == 1 || f.IndexCount == 2)
                                continue;

                            for (int i=0;i<(f.IndexCount-2);i++)
                            {
                                uIndices.Add(f.Indices[i+2]);
                                uIndices.Add(f.Indices[i+1]);
                                uIndices.Add(f.Indices[0]);
                            }
                        }
                    }

                    // Uv (texture coordinate) 
                    if (m.HasTextureCoords(0))
                    {
                        foreach (var uv in m.TextureCoordinateChannels[0])
                        {
                            uUv.Add(new Vector2(uv.X, uv.Y));
                        }
                    }
                
                    UnityEngine.Mesh uMesh = new UnityEngine.Mesh();
                    uMesh.vertices = uVertices.ToArray();
                    uMesh.normals = uNormals.ToArray();
                    uMesh.triangles = uIndices.ToArray();
                    uMesh.uv = uUv.ToArray();

                    uMeshAndMats.Add(new MeshMaterialBinding(m.Name, uMesh, uMaterials[m.MaterialIndex].Item1, uMaterials[m.MaterialIndex].Item2));
                }
            }
            
            // Create GameObjects from nodes
            //
            // Originally this was done by decomposing the transform; however, when there are off-axis rotations, non-uniform scales,
            // and skews present, decompose will not recover the the correct rotation and scale values.
            //
            // Fixing this by multiplying the transformation matrix into each vertex instead.
            //
            // Additionally: there seems to be an error in the transform decompose in Assimp.
            // The scaling and rotation should be column-wise, not row-wise (tested using the output of Blender's Collada exporter).
            GameObject NodeToGameObject(Node node, Matrix4x4 parentTransform)
            {
                GameObject uOb = new GameObject(node.Name);

                Assimp.Matrix4x4 aTransform = node.Transform;
                UnityEngine.Matrix4x4 uTransform = new UnityEngine.Matrix4x4(
                    new Vector4(aTransform.A1, aTransform.A2, aTransform.A3, 0.0f),
                    new Vector4(aTransform.B1, aTransform.B2, aTransform.B3, 0.0f),
                    new Vector4(aTransform.C1, aTransform.C2, aTransform.C3, 0.0f),
                    new Vector4(aTransform.D1, aTransform.D2, aTransform.D3, aTransform.D4));
                
                uTransform = parentTransform * uTransform.transpose;
               
                Vector3 translation = parentTransform * new Vector3(aTransform.A4, aTransform.B4, aTransform.C4);
                translation.x *= -1.0f;

                // Set Mesh
                if (node.HasMeshes)
                {
                    foreach (var mIdx in node.MeshIndices)
                    {
                        var uMeshAndMat = uMeshAndMats[mIdx];
                        
                        GameObject uSubOb = new GameObject(uMeshAndMat.MeshName);
                        MeshFilter meshFilter = uSubOb.AddComponent<MeshFilter>();
                        MeshRenderer meshRenderer = uSubOb.AddComponent<MeshRenderer>();
                        AdditionalMeshImportGameObject additionalMeshImportData =
                            uSubOb.AddComponent<AdditionalMeshImportGameObject>();
                        additionalMeshImportData.additionalMeshImportData = uMeshAndMat.GetAdditionalMeshImportData;
                    
                        Mesh mesh = uMeshAndMat.Mesh;
                        meshRenderer.material = uMeshAndMat.Material;
                        uSubOb.transform.SetParent(uOb.transform, true);
                        uSubOb.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                        Vector3[] vertices = mesh.vertices;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Vector3 v = vertices[i];
                            v = uTransform * new Vector4(v.x, v.y, v.z, 1.0f);
                            v.x *= -1.0f;
                            vertices[i] = v;
                        }

                        Vector3[] normals = mesh.normals;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Vector3 n = normals[i];
                            n = uTransform * new Vector4(n.x, n.y, n.z, 0.0f);
                            n.x *= -1.0f;
                            normals[i] = n.normalized;
                        }

                        mesh.vertices = vertices;
                        mesh.normals = normals;
                        mesh.RecalculateBounds();

                        meshFilter.mesh = mesh;
                    }
                }
                
                uOb.transform.localPosition = translation;
            
                if (node.HasChildren)
                {
                    foreach (var cn in node.Children)
                    {
                        GameObject uObChild = NodeToGameObject(cn, uTransform);
                        uObChild.transform.SetParent(uOb.transform, false);
                    }
                }
                return uOb;
            }

            GameObject cachedAsset = NodeToGameObject(scene.RootNode, Matrix4x4.identity);
            GameObject result = Object.Instantiate(cachedAsset);
            
            RuntimeAssetCache.Instance.AddGameObjectToCache(meshPath, cachedAsset);
            
            return result;
#else
            Debug.LogError("Runtime import of collada files is not currently supported in builds created with 'IL2CPP' scripting backend." + 
                           "\nEither create a build with the scripting backend set as 'Mono' in 'Player Settings' or use STL meshes instead of Collada (dae) meshes.");
            return null;
#endif
        }
    }
}
