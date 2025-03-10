﻿ /*
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

using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfMeshExportHandler
    {
        // TODO: Validate support for Unity built-in import meshes: mb, ma, max, jas, dxf, c4d, blend, lxo, 3ds
        static readonly string[] k_StandardMeshExt = new string[] {".dae", ".obj", ".fbx", ".ma", ".max", ".jas", 
            ".dxf", ".c4d", ".blend", ".lxo", ".3ds"};
        public static string CopyOrCreateMesh(GameObject geometryObject, bool isCollisionGeometry)
        {
            string prefabPath = GetPrefabPath(geometryObject);

            bool foundExistingMeshAsset = false;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                if (k_StandardMeshExt.Contains(Path.GetExtension(prefabPath).ToLower()))
                {
                    foundExistingMeshAsset = true;
                }
                else // No built-in import for STL; find stl file that corresponds to the prefab, if it already exists
                {
                    string[] foldersToSearch = {Path.GetDirectoryName(prefabPath)};
                    string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

                    foreach (string guid2 in RuntimeUrdf.AssetDatabase_FindAssets(prefabName, foldersToSearch))
                    {
                        string possiblePath = RuntimeUrdf.AssetDatabase_GUIDToAssetPath(guid2);
                        if (possiblePath.ToLower().Contains(".stl"))
                        {
                            prefabPath = possiblePath;
                            foundExistingMeshAsset = true;
                            break;
                        }
                    }
                }
            }

            if (foundExistingMeshAsset)
                return CopyMeshToExportDestination(prefabPath);

            return CreateNewStlFile(geometryObject, isCollisionGeometry);
        }

        private static string CopyMeshToExportDestination(string prefabPath)
        {
            string newPrefabPath = UrdfExportPathHandler.GetNewMeshPath(Path.GetFileName(prefabPath));

            if (Path.GetExtension(prefabPath)?.ToLower() == ".dae")
                CopyDaeTextureToExportDestination(prefabPath, Path.GetDirectoryName(newPrefabPath));

            prefabPath = UrdfAssetPathHandler.GetFullAssetPath(prefabPath);
            
            CopyFileToNewLocation(prefabPath, newPrefabPath);

            return newPrefabPath;
        }

        private static void CopyDaeTextureToExportDestination(string prefabPath, string newFolderLocation)
        {
            //Get material from Collada prefab
            Material material = RuntimeUrdf.AssetDatabase_LoadAssetAtPath<Material>(prefabPath);
            if (material == null || material.mainTexture == null)
                return;

            //Get relative subfolder where texture is, compared to the DAE file.
            string commonFolder = Path.GetDirectoryName(prefabPath).SetSeparatorChar();
            string texturePath = RuntimeUrdf.AssetDatabase_GetAssetPath(material.mainTexture).SetSeparatorChar();
            string relativeLocation = "";
            if (texturePath.Contains(commonFolder))
                relativeLocation = texturePath.Substring(commonFolder.Length + 1);
            string newTexturePath = Path.Combine(newFolderLocation, relativeLocation);

            Directory.CreateDirectory(Path.GetDirectoryName(newTexturePath));

            CopyFileToNewLocation(UrdfAssetPathHandler.GetFullAssetPath(texturePath), newTexturePath);
        }

        private static void CopyFileToNewLocation(string oldPath, string newPath)
        {
            if (oldPath != newPath)
                File.Copy(oldPath, newPath, true);
        }

        private static string CreateNewStlFile(GameObject geometryObject, bool isCollisionGeometry)
        {
            Debug.Log("Did not find an existing STL or DAE file for Geometry Mesh "
                      + geometryObject.name + ". Exporting a new STL file.", geometryObject);

            string newMeshPath = UrdfExportPathHandler.GetNewMeshPath(geometryObject.name + ".stl");

            StlExporter stlExporter = new StlExporter(newMeshPath, geometryObject, isCollisionGeometry);
            if (!stlExporter.Export())
                Debug.LogWarning("Mesh export for geometry " + geometryObject.name + " failed.", geometryObject);

            return newMeshPath;
        }

        private static string GetPrefabPath(GameObject gameObject)
        {
            return RuntimeUrdf.AssetDatabase_GetAssetPath(RuntimeUrdf.PrefabUtility_GetCorrespondingObjectFromSource(gameObject));
        } 
    }
}
