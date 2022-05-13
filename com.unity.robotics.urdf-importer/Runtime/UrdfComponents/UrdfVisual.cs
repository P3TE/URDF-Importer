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
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    [SelectionBase]
    public class UrdfVisual : MonoBehaviour
    {
        [SerializeField]
        public GeometryTypes geometryType;

        private Dictionary<string, UrdfVisualRenderable> instantiatedMaterialMap = new();

        public void AddInstantiatedMaterial(UrdfVisualRenderable instantiatedMaterial)
        {
            if (instantiatedMaterialMap.TryGetValue(instantiatedMaterial.materialName, out UrdfVisualRenderable renderable))
            {
                foreach (Renderer correspondingRenderer in instantiatedMaterial.correspondingRenderers)
                {
                    renderable.AddRenderer(correspondingRenderer);
                }
            }
            else
            {
                instantiatedMaterialMap.Add(instantiatedMaterial.materialName, instantiatedMaterial);
            }
        }

        public bool TryGetMaterialsByName(string materialName, out UrdfVisualRenderable renderable)
        {
            return instantiatedMaterialMap.TryGetValue(materialName, out renderable);
        }
        
        public class UrdfVisualRenderable
        {
            public readonly string materialName;
            public readonly List<Renderer> correspondingRenderers;
            public readonly Material correspondingMaterial;

            public UrdfVisualRenderable(string materialName, Renderer correspondingRenderer, Material correspondingMaterial)
            {
                this.materialName = materialName;
                correspondingRenderers = new List<Renderer> { correspondingRenderer };
                this.correspondingMaterial = correspondingMaterial;
            }

            public void AddRenderer(Renderer renderer)
            {
                correspondingRenderers.Add(renderer);
            }
        }
    }


}