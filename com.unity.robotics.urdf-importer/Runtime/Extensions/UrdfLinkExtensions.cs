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
    
    public static class UrdfLinkExtensions
    {
        
        private static Dictionary<string, UrdfLink> linkMap = new Dictionary<string, UrdfLink>();
        private static Dictionary<string, UrdfJoint> jointMap = new Dictionary<string, UrdfJoint>();

        public static void ClearLinkAndJointMaps()
        {
            linkMap.Clear();
            jointMap.Clear();
        }

        public static bool TryFindLink(string linkName, out UrdfLink value)
        {
            return linkMap.TryGetValue(linkName, out value);
        }
        
        public static bool TryFindJoint(string jointName, out UrdfJoint value)
        {
            return jointMap.TryGetValue(jointName, out value);
        }
        
        public static GameObject Create(Transform parent, UrdfLinkDescription link = null, UrdfJointDescription joint = null)
        {
            GameObject linkObject = new GameObject("link");
            linkObject.transform.SetParentAndAlign(parent);
            UrdfLink urdfLink = linkObject.AddComponent<UrdfLink>();
            urdfLink.Visuals = UrdfVisualsExtensions.Create(linkObject.transform, link?.visuals);
            urdfLink.Collisions = UrdfCollisionsExtensions.Create(linkObject.transform, link?.collisions);

            if (link != null)
            {
                urdfLink.ImportLinkData(link, joint);
            }
            else
            {
                UrdfInertial.Create(linkObject);
#if UNITY_EDITOR
                UnityEditor.EditorGUIUtility.PingObject(linkObject);
#endif
            }

            return linkObject;
        }

        private static void ImportLinkData(this UrdfLink urdfLink, UrdfLinkDescription link, UrdfJointDescription joint)
        {
            if (joint == null)
            {
                urdfLink.IsBaseLink = true;
            }
            urdfLink.gameObject.name = link.name;
            linkMap.Add(link.name, urdfLink);
            
            if (joint?.origin != null)
                UrdfOrigin.ImportOriginData(urdfLink.transform, joint.origin);

            bool hasJoint = joint != null;
            string jointName = "Unknown";
            UrdfJoint.JointTypes jointType = default;
            if (hasJoint)
            {
                jointName = joint.name;
                jointType = UrdfJoint.GetJointType(joint.type);
            }
            
            
            bool shouldCreateOptimisedFixedJoint = ShouldCreatedOptimizedFixedJoint(joint);
            if (shouldCreateOptimisedFixedJoint)
            {
                UrdfJoint newJoint = UrdfJointFixed.CreateOptimizeFixedJoint(urdfLink.gameObject, link, joint);
                jointMap.Add(jointName, newJoint);
                return;
            }
            
            if (link.inertial != null)
            {
                UrdfInertial.Create(urdfLink.gameObject, link.inertial);
            }
        
            if (hasJoint)
            {
                UrdfJoint newJoint = UrdfJoint.Create(urdfLink.gameObject, jointType, joint);
                jointMap.Add(jointName, newJoint);
            }
        }

        public static bool ShouldCreatedOptimizedFixedJoint(UrdfJointDescription joint)
        {
            if (joint == null)
            {
                return false;
            }
            
            UrdfJoint.JointTypes jointType = UrdfJoint.GetJointType(joint.type);
            switch (jointType)
            {
                case UrdfJoint.JointTypes.Fixed:
                    return true;
                case UrdfJoint.JointTypes.Revolute:
                    return UrdfJointRevolute.ShouldRevoluteJointBeOptimisedToFixed(joint, out _);
                default:
                    return false;
            }
        }

        

        public static void RemoveJoint(UrdfJoint urdfJoint)
        {
            Joint unityJoint = urdfJoint.unityJoint;
            GameObject gameObject = unityJoint.gameObject;
            Rigidbody bodyToRemove = gameObject.GetComponent<Rigidbody>();
            
            Rigidbody connectedBody = unityJoint.connectedBody;

            PreviousRigidbodyConstants previousRigidbodyConstants =
                gameObject.AddComponent<PreviousRigidbodyConstants>();
            previousRigidbodyConstants.SetValues(bodyToRemove);

            UrdfJointFixed.OptimizeFixedJoint(gameObject, previousRigidbodyConstants);
            Object.DestroyImmediate(unityJoint);
            Object.DestroyImmediate(bodyToRemove);
        }
        
        public static UrdfLinkDescription ExportLinkData(this UrdfLink urdfLink)
        {
            if (urdfLink.transform.localScale != Vector3.one)
                Debug.LogWarning("Only visuals should be scaled. Scale on link \"" + urdfLink.gameObject.name + "\" cannot be saved to the URDF file.", urdfLink.gameObject);
            UrdfInertial urdfInertial = urdfLink.gameObject.GetComponent<UrdfInertial>();
            UrdfLinkDescription link = new UrdfLinkDescription(urdfLink.gameObject.name)
            {
                visuals = urdfLink.GetComponentInChildren<UrdfVisuals>().ExportVisualsData(),
                collisions = urdfLink.GetComponentInChildren<UrdfCollisions>().ExportCollisionsData(),
                inertial = urdfInertial == null ? null : urdfInertial.ExportInertialData()
            };
            
            return link;
        }
    }
}