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

using System.Collections.Generic;
using System.Linq;
using Unity.Robotics.UrdfImporter.Urdf.Extensions;
using UnityEngine;
using UnityMeshImporter;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfVisualExtensions
    {
        public static void Create(Transform parent, GeometryTypes type)
        {
            GameObject visualObject = new GameObject("unnamed");
            visualObject.transform.SetParentAndAlign(parent);
            UrdfVisual urdfVisual = visualObject.AddComponent<UrdfVisual>();

            urdfVisual.geometryType = type;
            UrdfGeometryVisual.Create(visualObject.transform, type);
#if UNITY_EDITOR
            UnityEditor.EditorGUIUtility.PingObject(visualObject);
#endif
        }

        public static void Create(Transform parent, UrdfLinkDescription.Visual visual)
        {
            GameObject visualObject = new GameObject(visual.name ?? "unnamed");
            visualObject.transform.SetParentAndAlign(parent);
            UrdfVisual urdfVisual = visualObject.AddComponent<UrdfVisual>();

            urdfVisual.geometryType = UrdfGeometry.GetGeometryType(visual.geometry);
            UrdfGeometryVisual.Create(visualObject.transform, urdfVisual.geometryType, visual.geometry);

            //UrdfMaterial.SetUrdfMaterial(visualObject, visual.material);
            SetupMaterials(visualObject, visual);
            UrdfOrigin.ImportOriginData(visualObject.transform, visual.origin);
        }

        private static void SetupMaterials(GameObject visualObject, UrdfLinkDescription.Visual visual)
        {

            Renderer[] renderers = visualObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                string rendererName = renderer.name;
                bool matchingMaterialFound = false;

                AdditionalMeshImportGameObject additionalMeshImportData = renderer.GetComponent<AdditionalMeshImportGameObject>();

                if (additionalMeshImportData != null)
                {
                    foreach (UrdfMaterialDescription urdfMaterialDescription in visual.materials)
                    {
                        string materialName = urdfMaterialDescription.name;
                        if (additionalMeshImportData.additionalMeshImportData.materialName == materialName)
                        {
                            matchingMaterialFound = true;
                            renderer.sharedMaterial = urdfMaterialDescription.CreateMaterial();
                            break;
                        }
                    }
                }

                if (!matchingMaterialFound)
                {
                    if (visual.materials.Count > 0)
                    {
                        UrdfMaterialDescription fallbackMaterial = visual.materials[0];
                        RuntimeUrdf.AddImportWarning($"No material found for mesh with name {rendererName}, falling back to {fallbackMaterial.name}");
                        //renderer.sharedMaterial = fallbackMaterial.CreateMaterial();
                    }
                    else
                    {
                        RuntimeUrdf.AddImportWarning($"No material found for mesh with name {rendererName}, and no materials specified for this visual component, falling back to default shader");
                        //renderer.sharedMaterial = UrdfMaterial.GetDefaultMaterial();
                    }
                }
            }
        }

        public static void AddCorrespondingCollision(this UrdfVisual urdfVisual)
        {
            UrdfCollisions collisions = urdfVisual.GetComponentInParent<UrdfLink>().GetComponentInChildren<UrdfCollisions>();
            UrdfCollisionExtensions.Create(collisions.transform, urdfVisual.geometryType, urdfVisual.transform);
        }


        private static List<UrdfUnityMaterial.ExportMaterial> ExportMaterialData(this UrdfVisual urdfVisual)
        {

            List<UrdfUnityMaterial.ExportMaterial> exportMaterials = new List<UrdfUnityMaterial.ExportMaterial>();
            
            MeshRenderer[] meshRenderers = urdfVisual.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                foreach (Material meshRendererMaterial in meshRenderer.sharedMaterials)
                {
                    UrdfUnityMaterial.ExportMaterial urdfUnityMaterial = UrdfUnityMaterial.GenerateAndExportNewMaterial(meshRendererMaterial);
                    if (urdfUnityMaterial != null)
                    {
                        exportMaterials.Add(urdfUnityMaterial);
                    }
                }
            }

            return exportMaterials;
        }

        public static UrdfLinkDescription.Visual ExportVisualData(this UrdfVisual urdfVisual)
        {
            UrdfGeometry.CheckForUrdfCompatibility(urdfVisual.transform, urdfVisual.geometryType);

            UrdfLinkDescription.Geometry geometry = UrdfGeometry.ExportGeometryData(urdfVisual.geometryType, urdfVisual.transform);

            UrdfMaterialDescription material = null;
            List<UrdfUnityMaterial.ExportMaterial> exportMaterials = null;
            if ((geometry.mesh != null ))
            {
                exportMaterials = urdfVisual.ExportMaterialData();
                //material = UrdfMaterial.ExportMaterialData(urdfVisual.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            }
            string visualName = urdfVisual.name == "unnamed" ? null : urdfVisual.name;

            return new UrdfLinkDescription.Visual(geometry, visualName, UrdfOrigin.ExportOriginData(urdfVisual.transform), material, exportMaterials);
        }
    }
}