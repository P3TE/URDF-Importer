﻿/*
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
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfVisualsExtensions
    {
        public static UrdfVisuals Create(Transform parent, List<UrdfLinkDescription.Visual> visuals = null)
        {
            GameObject visualsObject = new GameObject("Visuals");
            visualsObject.transform.SetParentAndAlign(parent);
            UrdfVisuals urdfVisuals = visualsObject.AddComponent<UrdfVisuals>();

            visualsObject.hideFlags = HideFlags.NotEditable;
            urdfVisuals.hideFlags = HideFlags.None;

            if (visuals != null)
            {
                foreach (UrdfLinkDescription.Visual visual in visuals)
                {
                    UrdfVisualExtensions.Create(urdfVisuals.transform, visual);
                }
            }

            return urdfVisuals;
        }

        public static List<UrdfLinkDescription.Visual> ExportVisualsData(this UrdfVisuals urdfVisuals)
        {
            UrdfVisual[] urdfVisualsList = urdfVisuals.GetComponentsInChildren<UrdfVisual>();

            return urdfVisualsList.Select(urdfCollision => urdfCollision.ExportVisualData()).ToList();
        }
    }
}

