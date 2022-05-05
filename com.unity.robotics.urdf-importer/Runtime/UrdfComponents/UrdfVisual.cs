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

        private Dictionary<string, UrdfVisualRenderable> instantiatedMaterialMap = new Dictionary<string, UrdfVisualRenderable>();

        public void AddInstantiatedMaterial(UrdfVisualRenderable instantiatedMaterial)
        {
            bool addSuccess = instantiatedMaterialMap.TryAdd(instantiatedMaterial.materialName, instantiatedMaterial);
            if (!addSuccess)
            {
                RuntimeUrdf.AddImportWarning($"Multiple materials with the same name ({instantiatedMaterial.materialName}) on a single visual!");
            }
        }

        public bool TryGetMaterialByName(string materialName, out UrdfVisualRenderable renderable)
        {
            return instantiatedMaterialMap.TryGetValue(materialName, out renderable);
        }
        
        public class UrdfVisualRenderable
        {
            public readonly string materialName;
            public readonly Renderer correspondingRenderer;
            public readonly Material correspondingMaterial;

            public UrdfVisualRenderable(string materialName, Renderer correspondingRenderer, Material correspondingMaterial)
            {
                this.materialName = materialName;
                this.correspondingRenderer = correspondingRenderer;
                this.correspondingMaterial = correspondingMaterial;
            }
        }
    }


}