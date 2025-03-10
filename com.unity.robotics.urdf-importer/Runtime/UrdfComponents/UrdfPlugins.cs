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
    public class UrdfPlugins : MonoBehaviour
    {
        public static UrdfPlugins Create(Transform robot, List<UrdfPluginDescription> plugins = null)
        {
            GameObject pluginsObject = new GameObject("Plugins");
            pluginsObject.transform.SetParentAndAlign(robot);
            UrdfPlugins urdfPlugins = pluginsObject.AddComponent<UrdfPlugins>();

            if (plugins != null)
            {
                foreach (var plugin in plugins)
                {
                    UrdfPlugin.Create(pluginsObject.transform, plugin);
                }
            }
            
            return urdfPlugins;
        }

        public List<UrdfPluginDescription> ExportPluginsData()
        {
            return GetComponents<UrdfPlugin>()
                .Select(urdfPlugin => urdfPlugin.ExportPluginData())
                .Where(plugin => plugin != null)
                .ToList();
        }
    }

}