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

#define CONTINUOUS_AS_HINGE_JOINTS

using System;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfJointContinuous : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Continuous;

        public static UrdfJoint Create(GameObject linkObject)
        {
            UrdfJointContinuous urdfJoint = linkObject.AddComponent<UrdfJointContinuous>();
            
#if  URDF_FORCE_ARTICULATION_BODY
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.RevoluteJoint;
#else
            
#if CONTINUOUS_AS_HINGE_JOINTS
            HingeJoint hingeJoint = linkObject.AddComponent<HingeJoint>();
            urdfJoint.unityJoint = hingeJoint;
#else
            ConfigurableJoint configurableJoint = linkObject.AddComponent<ConfigurableJoint>();
            urdfJoint.unityJoint = configurableJoint;
#endif
            
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

#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointPosition[xAxis];
#else
    #if CONTINUOUS_AS_HINGE_JOINTS
            return GetCurrentAngleRadHingeJoint(unityJoint as HingeJoint);
    #else
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();
            return GetCurrentAngleRadContinuousJoint(rigidbody, configurableJoint, originalLocalRotation);
    #endif
#endif
        }

        public static float GetCurrentAngleRadHingeJoint(HingeJoint hingeJoint)
        {
            return -hingeJoint.angle * Mathf.Deg2Rad;
        }

        public static float GetCurrentAngleRadContinuousJoint(Rigidbody rigidbody, ConfigurableJoint configurableJoint, 
            Quaternion originalLocalRotation)
        {
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
        }

        /// <summary>
        /// Returns the current velocity of joint in radians per second in the reduced coordinates
        /// </summary>
        /// <returns>floating point for joint velocity in radians per second</returns>
        public override float GetVelocity()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointVelocity[xAxis];
#else
            
    #if CONTINUOUS_AS_HINGE_JOINTS
            return GetCurrentLocalVelocityHingeJoint(unityJoint as HingeJoint);
    #else
            return GetCurrentLocalVelocityConfigurableJoint(unityJoint as ConfigurableJoint);
    #endif
#endif
        }

        public static float GetCurrentLocalVelocityHingeJoint(HingeJoint hingeJoint)
        {
            return -hingeJoint.velocity * Mathf.Deg2Rad;
        }
        
        public static float GetCurrentLocalVelocityConfigurableJoint(ConfigurableJoint configurableJoint)
        {
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();
            Rigidbody connectedBody = configurableJoint.connectedBody;

            //Find the difference in angular velocity between the connectedBody and the rigidbody
            Vector3 connectedBodyAngularVelocityWorld = connectedBody.angularVelocity;
            Vector3 rigidbodyAngularVelocityWorld = rigidbody.angularVelocity;
            Vector3 rigidbodyOffsetAngularVelocityWorld =
                rigidbodyAngularVelocityWorld - connectedBodyAngularVelocityWorld;

            //Convert the angular velocity to the local frame
            Vector3 angularVelocityLocal = Quaternion.Inverse(rigidbody.rotation) * rigidbodyOffsetAngularVelocityWorld;

            //Find the angular velocity along the axis of rotation
            float angularVelocityUnity = Vector3.Dot(configurableJoint.axis, angularVelocityLocal);
            
            return angularVelocityUnity;
        }

        /// <summary>
        /// Returns current joint torque in Nm
        /// </summary>
        /// <returns>floating point in Nm</returns>
        public override float GetEffort()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointForce[xAxis];
#else
            
    #if CONTINUOUS_AS_HINGE_JOINTS
            return GetEffortHingeJoint(unityJoint as HingeJoint);
    #else
            return GetEffortConfigurableJoint(unityJoint as ConfigurableJoint);
    #endif
#endif
        }

        public static float GetEffortHingeJoint(HingeJoint hingeJoint)
        {
            return -hingeJoint.motor.force;
        }

        public static float GetEffortConfigurableJoint(ConfigurableJoint configurableJoint)
        {
            //TODO - Verify, this seems dodgy...
            return configurableJoint.angularXDrive.maximumForce;
        }

        /// <summary>
        /// Rotates the joint by deltaState radians 
        /// </summary>
        /// <param name="deltaState">amount in radians by which joint needs to be rotated</param>
        protected override void OnUpdateJointState(float deltaState)
        {

#if  URDF_FORCE_ARTICULATION_BODY
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
            SetDynamics(joint.dynamics);
        }

        protected override UrdfJointDescription ExportSpecificJointData(UrdfJointDescription joint)
        {
#if  URDF_FORCE_ARTICULATION_BODY
            joint.axis = new UrdfJointDescription.Axis((unityJoint.anchorRotation * Vector3.right).Unity2Ros());
            joint.dynamics = new UrdfJointDescription.Dynamics(unityJoint.angularDamping, unityJoint.jointFriction);
            joint.limit = ExportLimitData();
#else
            joint.axis = GetAxisData(unityJoint.axis.Unity2Ros());
            Debug.LogError("TODO - Broken!");
            joint.dynamics = new UrdfJointDescription.Dynamics(
                ((HingeJoint)unityJoint).spring.spring,
                ((HingeJoint)unityJoint).spring.damper, 
                ((HingeJoint)unityJoint).spring.spring);
#endif
            return joint;
        }
        
        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void AdjustMovement(UrdfJointDescription joint)
        {
#if  URDF_FORCE_ARTICULATION_BODY
            axisofMotion = axis;
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

            
            
#if CONTINUOUS_AS_HINGE_JOINTS
            HingeJoint hingeJoint = unityJoint as HingeJoint;
            AdjustMovementSharedHingeJoint(joint, hingeJoint);
            hingeJoint.useLimits = false;
#else
            ConfigurableJoint configurableJoint = unityJoint as ConfigurableJoint; 
            AdjustMovementSharedConfirguableJoint(configurableJoint, joint);
            configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
#endif
            
            
#endif
        }
        
        public static void AdjustMovementSharedHingeJoint(UrdfJointDescription joint, HingeJoint hingeJoint)
        {
            Vector3 axisOfMotionUnity = joint.axis.AxisUnity;
            //Vector3 secondaryAxisOfMotionUnity = joint.axis.SecondaryAxisEstimateUnity;

            hingeJoint.axis = axisOfMotionUnity;

            // useLimits is not shared.

            hingeJoint.motor = new JointMotor()
            {
                force = (float) joint.limit.effort,
                targetVelocity = (float) joint.limit.velocity,
            };
            //TODO - hingeJoint.useMotor  
        }


        private void AdjustMotionConfigurableJoint(UrdfJointDescription joint, ConfigurableJoint configurableJoint)
        {
            AdjustMovementSharedConfirguableJoint(configurableJoint, joint);
            configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
        }

        public static void AdjustMovementSharedConfirguableJoint(ConfigurableJoint configurableJoint, UrdfJointDescription joint)
        {
            Vector3 axisOfMotionUnity = joint.axis.AxisUnity;
            Vector3 secondaryAxisOfMotionUnity = joint.axis.SecondaryAxisEstimateUnity;

            configurableJoint.axis = axisOfMotionUnity;
            configurableJoint.secondaryAxis = secondaryAxisOfMotionUnity;

            configurableJoint.xMotion = ConfigurableJointMotion.Locked;
            configurableJoint.yMotion = ConfigurableJointMotion.Locked;
            configurableJoint.zMotion = ConfigurableJointMotion.Locked;

            //configurableJoint.angularXMotion = ConfigurableJointMotion.Free; Not shared.
            configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;

            configurableJoint.angularXDrive = new JointDrive()
            {
                maximumForce = (float) joint.limit.effort,
                positionDamper = (float) joint.dynamics.damping,
                positionSpring = (float) joint.dynamics.spring,
            };
            configurableJoint.targetAngularVelocity = new Vector3(1.0f, 0.0f, 0.0f) * (float) joint.limit.velocity;  
        }

    }
}
