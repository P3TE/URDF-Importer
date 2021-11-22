/*
© Siemens AG, 2017-2019
Author: Dr. Martin Bischoff (martin.bischoff@siemens.com)
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

namespace Unity.Robotics.UrdfImporter
{
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
    [RequireComponent(typeof(ArticulationBody))]
#else
    //[RequireComponent(typeof(Joint))]
#endif
    public abstract class UrdfJoint : MonoBehaviour
    {
        public enum JointTypes
        {
            Fixed,
            Continuous,
            Revolute,
            Floating,
            Prismatic,
            Planar
        }

        public int xAxis = 0;

#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
        protected ArticulationBody unityJoint;
        protected Vector3 axisofMotion;
#else
        protected UnityEngine.Joint unityJoint;
#endif
        public string jointName;

        public abstract JointTypes JointType { get; } // Clear out syntax
        public bool IsRevoluteOrContinuous => JointType == JointTypes.Revolute || JointType == JointTypes.Revolute;
        public double EffortLimit = 1e3;
        public double VelocityLimit = 1e3;

        protected const int RoundDigits = 6;
        protected const float Tolerance = 0.0000001f;

        public static UrdfJoint Create(GameObject linkObject, JointTypes jointType, UrdfJointDescription joint = null)
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
#else
            Rigidbody parentRigidbody = linkObject.transform.parent.gameObject.GetComponent<Rigidbody>();
            if (parentRigidbody == null) throw new Exception($"No attached Rigidbody on {linkObject.transform.parent.gameObject.name}");
#endif
            UrdfJoint urdfJoint = AddCorrectJointType(linkObject, jointType);

            if (joint != null)
            {
                urdfJoint.jointName = joint.name;
                urdfJoint.ImportJointData(joint);
            }
            return urdfJoint;
        }

        private static UrdfJoint AddCorrectJointType(GameObject linkObject, JointTypes jointType)
        {
            UrdfJoint urdfJoint = null;

            switch (jointType)
            {
                case JointTypes.Fixed:
                    urdfJoint = UrdfJointFixed.Create(linkObject);
                    break;
                case JointTypes.Continuous:
                    urdfJoint = UrdfJointContinuous.Create(linkObject);
                    break;
                case JointTypes.Revolute:
                    urdfJoint = UrdfJointRevolute.Create(linkObject);
                    break;
                case JointTypes.Floating:
                    urdfJoint = UrdfJointFloating.Create(linkObject);
                    break;
                case JointTypes.Prismatic:
                    urdfJoint = UrdfJointPrismatic.Create(linkObject);
                    break;
                case JointTypes.Planar:
                    urdfJoint = UrdfJointPlanar.Create(linkObject);
                    break;
            }


#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
#else
            SetupConnectedBody(linkObject);
#endif

            return urdfJoint;
        }
        
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
#else

        

        public static Rigidbody FindFixedParent(GameObject linkObject)
        {
            Transform currentTransform = linkObject.transform.parent;
            while (currentTransform != null)
            {
                
                Rigidbody connectedBody = currentTransform.GetComponent<Rigidbody>();
                if (connectedBody != null)
                {
                    UrdfJointFixed urdfJointFixed = connectedBody.GetComponent<UrdfJointFixed>();
                    if (urdfJointFixed == null)
                    {
                        return connectedBody;
                    }
                }
                
                currentTransform = currentTransform.parent;
            }

            throw new Exception("No connectedBody found!");
        }

        private static void SetupConnectedBody(GameObject linkObject)
        {
            UnityEngine.Joint unityJoint = linkObject.GetComponent<UnityEngine.Joint>();
            if (unityJoint != null)
            {
                //Go up the hierarchy until you find the first rigidbody to connect to:
                Rigidbody connectedBody = FindFixedParent(linkObject);
                unityJoint.connectedBody = connectedBody;
                unityJoint.autoConfigureConnectedAnchor = true;
            }
        }
#endif

        /// <summary>
        /// Changes the type of the joint
        /// </summary>
        /// <param name="linkObject">Joint whose type is to be changed</param>
        /// <param name="newJointType">Type of the new joint</param>
        public static void ChangeJointType(GameObject linkObject, JointTypes newJointType)
        {
            linkObject.transform.DestroyImmediateIfExists<UrdfJoint>();
            linkObject.transform.DestroyImmediateIfExists<HingeJointLimitsManager>();
            linkObject.transform.DestroyImmediateIfExists<PrismaticJointLimitsManager>();
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            linkObject.transform.DestroyImmediateIfExists<UnityEngine.ArticulationBody>();
#else
                        linkObject.transform.DestroyImmediateIfExists<UnityEngine.Joint>();
#endif
            AddCorrectJointType(linkObject, newJointType);
        }

        #region Runtime

        public void Start()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            unityJoint = GetComponent<ArticulationBody>();
#else
                        unityJoint = GetComponent<Joint>();
#endif
        }

        public virtual float GetPosition()
        {
            return 0;
        }

        public virtual float GetVelocity()
        {
            return 0;
        }

        public virtual float GetEffort()
        {
            return 0;
        }

        public void UpdateJointState(float deltaState)
        {
            OnUpdateJointState(deltaState);
        }
        protected virtual void OnUpdateJointState(float deltaState) { }

        #endregion

        #region Import Helpers

        public static JointTypes GetJointType(string jointType)
        {
            switch (jointType)
            {
                case "fixed":
                    return JointTypes.Fixed;
                case "continuous":
                    return JointTypes.Continuous;
                case "revolute":
                    return JointTypes.Revolute;
                case "floating":
                    return JointTypes.Floating;
                case "prismatic":
                    return JointTypes.Prismatic;
                case "planar":
                    return JointTypes.Planar;
                default:
                    return JointTypes.Fixed;
            }
        }

        protected virtual void ImportJointData(UrdfJointDescription joint) { }

        protected static Vector3 GetAxis(UrdfJointDescription.Axis axis)
        {
            return axis.xyz.ToVector3().Ros2Unity();
        }

        protected static Vector3 GetDefaultAxis()
        {
            return new Vector3(-1, 0, 0);
        }

        protected virtual void AdjustMovement(UrdfJointDescription joint) { }

        protected void SetDynamics(UrdfJointDescription.Dynamics dynamics)
        {
            
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            if (unityJoint == null)
            {
                unityJoint = GetComponent<ArticulationBody>();
            }
            unityJoint.linearDamping = dynamics.Damping();
            unityJoint.angularDamping = dynamics.Damping();
            unityJoint.jointFriction = dynamics.Friction();
#else
            //TODO - Implement
            Debug.LogError("TODO - Implement.");
#endif
        }

        #endregion

        #region Export

        public string UsedJointName
        {
            get
            {
                if (jointName == null)
                {
                    GenerateUniqueJointName();
                }
                string usedJointName = jointName;
                if (usedJointName == "")
                {
                    usedJointName = $"{gameObject.name}_joint";
                    Debug.LogWarning($"No joint name speficied for {gameObject.name}, defaulting to {usedJointName}");
                }
                return usedJointName;
            }
        }

        public UrdfJointDescription ExportJointData()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            unityJoint = GetComponent<UnityEngine.ArticulationBody>();
#else
                        unityJoint = GetComponent<UnityEngine.Joint>();
#endif
            CheckForUrdfCompatibility();

            //Data common to all joints
            UrdfJointDescription joint = new UrdfJointDescription(
                UsedJointName,
                JointType.ToString().ToLower(),
                gameObject.transform.parent.name,
                gameObject.name,
                UrdfOrigin.ExportOriginData(transform));

            joint.limit = ExportLimitData();
            return ExportSpecificJointData(joint);
        }

        public static UrdfJointDescription ExportDefaultJoint(Transform transform)
        {
            return new UrdfJointDescription(
                transform.parent.name + "_" + transform.name + "_joint",
                JointTypes.Fixed.ToString().ToLower(),
                transform.parent.name,
                transform.name,
                UrdfOrigin.ExportOriginData(transform));
        }

        #region ExportHelpers

        protected virtual UrdfJointDescription ExportSpecificJointData(UrdfJointDescription joint)
        {
            return joint;
        }

        protected virtual UrdfJointDescription.Limit ExportLimitData()
        {
            return null; // limits aren't used
        }

        public virtual bool AreLimitsCorrect()
        {
            return true; // limits aren't needed
        }

        protected virtual bool IsJointAxisDefined()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            if (axisofMotion == null)
                return false;
            else
                return true;
#else
                        UnityEngine.Joint joint = GetComponent<UnityEngine.Joint>();
                        return !(Math.Abs(joint.axis.x) < Tolerance &&
                                 Math.Abs(joint.axis.y) < Tolerance &&
                                 Math.Abs(joint.axis.z) < Tolerance);
#endif
        }

        public void GenerateUniqueJointName()
        {
            jointName = transform.parent.name + "_" + transform.name + "_joint";
        }

        protected static UrdfJointDescription.Axis GetAxisData(Vector3 axis)
        {
            double[] rosAxis = axis.ToRoundedDoubleArray();
            return new UrdfJointDescription.Axis(rosAxis);
        }

        private bool IsAnchorTransformed() // TODO : Check for tolerances before implementation
        {

            UnityEngine.Joint joint = GetComponent<UnityEngine.Joint>();

            return Math.Abs(joint.anchor.x) > Tolerance ||
                Math.Abs(joint.anchor.x) > Tolerance ||
                Math.Abs(joint.anchor.x) > Tolerance;
        }

        private void CheckForUrdfCompatibility()
        {
            if (!AreLimitsCorrect())
                Debug.LogWarning("Limits are not defined correctly for Joint " + jointName + " in Link " + name +
                                 ". This may cause problems when visualizing the robot in RVIZ or Gazebo.",
                                 gameObject);
            if (!IsJointAxisDefined())
                Debug.LogWarning("Axis for joint " + jointName + " is undefined. Axis will not be written to URDF, " +
                                 "and the default axis will be used instead.",
                                 gameObject);
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY

#else
            if (IsAnchorTransformed())
                Debug.LogWarning("The anchor position defined in the joint connected to " + name + " will be" +
                                 " ignored in URDF. Instead of modifying anchor, change the position of the link.", 
                                 gameObject);
#endif

        }

        #endregion

        #endregion
    }
}

