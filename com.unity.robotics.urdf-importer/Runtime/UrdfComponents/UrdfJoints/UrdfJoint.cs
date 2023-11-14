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
#if  URDF_FORCE_ARTICULATION_BODY
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

#if  URDF_FORCE_ARTICULATION_BODY
        protected ArticulationBody unityJoint;
        protected Vector3 axisofMotion;
#else
        public UnityEngine.Joint unityJoint;
#endif
        public string jointName;

        public abstract JointTypes JointType { get; } // Clear out syntax
        public bool IsRevoluteOrContinuous => JointType == JointTypes.Revolute || JointType == JointTypes.Continuous;
        public double EffortLimit = 1e3;
        public double VelocityLimit = 1e3;

        protected const int RoundDigits = 6;
        protected const float Tolerance = 0.0000001f;
        
        protected Quaternion originalLocalRotation = Quaternion.identity;

        public bool publishJointStateIfApplicable = false;

        //Whether the joint state publisher should publish the joint state.
        public bool ShouldPublishJointState
        {
            get
            {
                if (!publishJointStateIfApplicable)
                {
                    return false;
                }

                switch (JointType)
                {
                    case JointTypes.Fixed:
                        UrdfJointFixed urdfJointFixed = (UrdfJointFixed)this;
                        return urdfJointFixed.JointIsDynamicForJointStatePublishing;
                    default:
                        return true;
                }
            }
        }

        public static UrdfJoint Create(GameObject linkObject, JointTypes jointType, UrdfJointDescription joint = null)
        {
#if  URDF_FORCE_ARTICULATION_BODY
#else
            Rigidbody parentRigidbody = FindCrucialParent(linkObject);
            if (parentRigidbody == null)
            {
                UrdfLink link = linkObject.transform.parent.gameObject.GetComponent<UrdfLink>();
                
                throw new Exception($"No attached Rigidbody on {linkObject.transform.parent.gameObject.name}");
            }
#endif
            UrdfJoint urdfJoint = AddCorrectJointType(linkObject, jointType);
            urdfJoint.originalLocalRotation = linkObject.transform.localRotation;

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


#if  URDF_FORCE_ARTICULATION_BODY
#else
            SetupConnectedBody(linkObject);
#endif

            return urdfJoint;
        }
        
#if  URDF_FORCE_ARTICULATION_BODY
#else

        
        /// <summary>
        /// Given a link, search up the tree to find a rigidbody.
        /// If a rigidbody is connected by a fixed joint, keep searching. 
        /// </summary>
        /// <param name="linkObject"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Rigidbody FindCrucialParent(GameObject linkObject)
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
                Rigidbody connectedBody = FindCrucialParent(linkObject);
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
#if  URDF_FORCE_ARTICULATION_BODY
            linkObject.transform.DestroyImmediateIfExists<UnityEngine.ArticulationBody>();
#else
            linkObject.transform.DestroyImmediateIfExists<UnityEngine.Joint>();
#endif
            AddCorrectJointType(linkObject, newJointType);
        }

        #region Runtime

        public void Start()
        {
#if  URDF_FORCE_ARTICULATION_BODY
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
            return axis.AxisUnity;
        }

        protected static Vector3 GetDefaultAxis()
        {
            return new Vector3(-1, 0, 0);
        }

        protected virtual void AdjustMovement(UrdfJointDescription joint) { }
        protected virtual void SetAxisData(Vector3 axisofMotion) { }
        protected  virtual void SetLimits(Joint joint){}

        protected void SetDynamics(UrdfJointDescription.Dynamics dynamics)
        {
            
#if  URDF_FORCE_ARTICULATION_BODY
            if (unityJoint == null)
            {
                unityJoint = GetComponent<ArticulationBody>();
            }
            unityJoint.linearDamping = dynamics.Damping();
            unityJoint.angularDamping = dynamics.Damping();
            unityJoint.jointFriction = dynamics.Friction();
#else
            
            if (unityJoint is HingeJoint hingeJoint)
            {
                hingeJoint.useSpring = true;
                hingeJoint.spring = new JointSpring()
                {
                    //damper = 0.0001f,
                    //spring = 0.001f,
                    damper = (float) dynamics.damping,
                    spring = (float) dynamics.spring,
                };
                
                //Note: HingeJoint doesn't have any friction component and will be ignored.
            } else if (unityJoint is ConfigurableJoint configurableJoint)
            {
                configurableJoint.xDrive = UpdateSingleAxis(dynamics, configurableJoint.xDrive, configurableJoint.xMotion);
                configurableJoint.yDrive = UpdateSingleAxis(dynamics, configurableJoint.yDrive, configurableJoint.yMotion);
                configurableJoint.zDrive = UpdateSingleAxis(dynamics, configurableJoint.zDrive, configurableJoint.zMotion);
            
                configurableJoint.angularXDrive = UpdateSingleAxis(dynamics, configurableJoint.angularXDrive, configurableJoint.angularXMotion);
                configurableJoint.angularYZDrive = UpdateSingleAxis(dynamics, configurableJoint.angularYZDrive, configurableJoint.angularYMotion, configurableJoint.angularZMotion);
                
                //Note: ConfigurableJoint doesn't have any friction component and will be ignored.
            }
            else
            {
                throw new Exception($"Unhandled joint of type {unityJoint.GetType().Name}, unable to continue!");
            }
            
#endif
        }

        private bool UpdatesMotion(ConfigurableJointMotion configurableJointMotion)
        {
            switch (configurableJointMotion)
            {
                case ConfigurableJointMotion.Locked:
                    //Nothing done, return.
                    return false;
                case ConfigurableJointMotion.Limited:
                case ConfigurableJointMotion.Free:
                    //Allow the function to continue.
                    return true;
                default:
                    throw new Exception($"ConfigurableJointMotion not implemented: {configurableJointMotion}");
            }
        }

        private JointDrive UpdateSingleAxis(UrdfJointDescription.Dynamics dynamics,
            JointDrive originalJointDrive,
            ConfigurableJointMotion configurableJointMotionA, 
            ConfigurableJointMotion configurableJointMotionB = ConfigurableJointMotion.Locked)
        {
            if (!UpdatesMotion(configurableJointMotionA) && !UpdatesMotion(configurableJointMotionB))
            {
                return originalJointDrive;
            }

            JointDrive result = new JointDrive()
            {
                maximumForce = originalJointDrive.maximumForce,
                positionDamper = (float) dynamics.damping,
                positionSpring = (float) dynamics.spring
            };
            return result;
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
#if  URDF_FORCE_ARTICULATION_BODY
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
#if  URDF_FORCE_ARTICULATION_BODY
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

        protected static UrdfJointDescription.Axis GetAxisData(Vector3 axisRosEnu)
        {
            return new UrdfJointDescription.Axis(axisRosEnu);
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
#if  URDF_FORCE_ARTICULATION_BODY

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

