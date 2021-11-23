/*
© Siemens AG, 2018-2019
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

using System;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfJointContinuous : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Continuous;

        public static UrdfJoint Create(GameObject linkObject)
        {
            UrdfJointContinuous urdfJoint;
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            urdfJoint = linkObject.AddComponent<UrdfJointContinuous>();
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.RevoluteJoint;
#else
            ConfigurableJoint configurableJoint = linkObject.AddComponent<ConfigurableJoint>();
            urdfJoint = linkObject.AddComponent<UrdfJointContinuous>();
            urdfJoint.unityJoint = configurableJoint;
            urdfJoint.unityJoint.autoConfigureConnectedAnchor = true;
#endif
            return urdfJoint;
        }

        #region Runtime
        /// <summary>
        /// Returns the current position of the joint in radians
        /// </summary>
        /// <returns>floating point number for joint position in radians</returns>
        public override float GetPosition() // Check Units
        {

#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            return unityJoint.jointPosition[xAxis];
#else
            //return ((HingeJoint) unityJoint).angle;
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();
            
            Quaternion currentLocalRotation = rigidbody.transform.localRotation;
            
            //Useful for verifying the result:
            //float unsignedResult = Quaternion.Angle(currentLocalRotation, originalLocalRotation);

            Vector3 localUpAxis = configurableJoint.axis.normalized;
            Vector3 localForwardAxis = configurableJoint.secondaryAxis.normalized;
            Vector3 localRightAxis = Vector3.Cross(localUpAxis, localForwardAxis);
            
            Quaternion offsetRotation = currentLocalRotation * Quaternion.Inverse(originalLocalRotation);

            Vector3 offsetRightVector = offsetRotation * localRightAxis;
            
            float adjacent = Vector3.Dot(localRightAxis, offsetRightVector);
            float opposite = Vector3.Dot(localForwardAxis, offsetRightVector);
            
            float currentAngleRad = Mathf.Atan2(opposite, adjacent);
            return currentAngleRad;
            
#endif
        }

        /// <summary>
        /// Returns the current velocity of joint in radians per second in the reduced coordinates
        /// </summary>
        /// <returns>floating point for joint velocity in radians per second</returns>
        public override float GetVelocity()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            return unityJoint.jointVelocity[xAxis];
#else
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();

            Vector3 angularVelocityWorld = rigidbody.angularVelocity;
            Vector3 angularVelocityLocal = Quaternion.Inverse(rigidbody.rotation) * angularVelocityWorld;

            float angularVelocityUnity = Vector3.Dot(configurableJoint.axis, angularVelocityLocal);
            
            return angularVelocityUnity;
#endif
        }

        /// <summary>
        /// Returns current joint torque in Nm
        /// </summary>
        /// <returns>floating point in Nm</returns>
        public override float GetEffort()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            return unityJoint.jointForce[xAxis];
#else
            //TODO - Implement.
            return 0f;
            //return -((HingeJoint)unityJoint).motor.force;
#endif

        }

        /// <summary>
        /// Rotates the joint by deltaState radians 
        /// </summary>
        /// <param name="deltaState">amount in radians by which joint needs to be rotated</param>
        protected override void OnUpdateJointState(float deltaState)
        {

#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationDrive drive = unityJoint.xDrive;
            drive.target += deltaState;
            unityJoint.xDrive = drive;
#else
            Quaternion rot = Quaternion.AngleAxis(-deltaState * Mathf.Rad2Deg, unityJoint.axis);
            transform.rotation = transform.rotation * rot;
#endif
        }

        #endregion

        protected override void ImportJointData(UrdfJointDescription joint)
        {
            AdjustMovement(joint);
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            SetDynamics(joint.dynamics);
#else
            ConfigurableJoint configurableJoint = AsUnityJoint;
            configurableJoint.angularXDrive = new JointDrive()
            {
                maximumForce = configurableJoint.angularXDrive.maximumForce,
                positionDamper = joint.dynamics.Damping(),
                positionSpring = configurableJoint.angularXDrive.positionSpring
            };
            Debug.LogWarning("Dynamics friction not implemented.");
#endif
        }

        protected override UrdfJointDescription ExportSpecificJointData(UrdfJointDescription joint)
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            joint.axis = GetAxisData(axisofMotion);
            joint.dynamics = new UrdfJointDescription.Dynamics(unityJoint.angularDamping, unityJoint.jointFriction);
            joint.limit = ExportLimitData();
#else
            joint.axis = GetAxisData(unityJoint.axis);
            joint.dynamics = new UrdfJointDescription.Dynamics(
                ((HingeJoint)unityJoint).spring.damper, 
                ((HingeJoint)unityJoint).spring.spring);
#endif
            return joint;
        }
        
        ConfigurableJoint AsUnityJoint => (ConfigurableJoint) unityJoint;


        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void AdjustMovement(UrdfJointDescription joint)
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            axisofMotion = (joint.axis != null && joint.axis.xyz != null) ? joint.axis.xyz.ToVector3() : new Vector3(1, 0, 0);
            unityJoint.linearLockX = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockY = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockZ = ArticulationDofLock.LockedMotion;
            unityJoint.twistLock = ArticulationDofLock.FreeMotion;

            Vector3 axisofMotionUnity = axisofMotion.Ros2Unity();
            Quaternion motion = new Quaternion();
            motion.SetFromToRotation(new Vector3(1, 0, 0), -1 * axisofMotionUnity);
            unityJoint.anchorRotation = motion;

            if (joint.limit != null)
            {
                ArticulationDrive drive = unityJoint.xDrive;
                drive.forceLimit = (float)(joint.limit.effort);
                unityJoint.maxAngularVelocity = (float)(joint.limit.velocity);
                drive.damping = unityJoint.xDrive.damping;
                drive.stiffness = unityJoint.xDrive.stiffness;
                unityJoint.xDrive = drive;
            }
#else
            
            //Rigidbody rigidbody = unityJoint.GetComponent<Rigidbody>();
            //rigidbody.constraints = RigidbodyConstraints.FreezePosition; - I think this is a bad idea.
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;

            Vector3 axisOfMotionUnity = joint.axis.AxisUnity;
            Vector3 secondaryAxisOfMotionUnity = joint.axis.SecondaryAxisEstimateUnity;

            configurableJoint.axis = axisOfMotionUnity;
            configurableJoint.secondaryAxis = secondaryAxisOfMotionUnity;

            configurableJoint.xMotion = ConfigurableJointMotion.Locked;
            configurableJoint.yMotion = ConfigurableJointMotion.Locked;
            configurableJoint.zMotion = ConfigurableJointMotion.Locked;

            configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
            configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;

            configurableJoint.angularXDrive = new JointDrive()
            {
                maximumForce = (float) joint.limit.effort
            };
            configurableJoint.targetAngularVelocity = new Vector3(1.0f, 0.0f, 0.0f) * (float) joint.limit.velocity;  
            
            //Note: Ignoring joint.limit.lower & joint.limit.upper
#endif
        }

    }
}
