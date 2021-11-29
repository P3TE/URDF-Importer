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
    public class UrdfJointRevolute : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Revolute;

        public static UrdfJoint Create(GameObject linkObject)
        {
            UrdfJointRevolute urdfJoint = linkObject.AddComponent<UrdfJointRevolute>();
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.RevoluteJoint;

#else
            ConfigurableJoint configurableJoint = linkObject.AddComponent<ConfigurableJoint>();
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
        public override float GetPosition()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            return ((ArticulationBody)unityJoint).jointPosition[xAxis];
#else
            //return -((HingeJoint)unityJoint).angle * Mathf.Deg2Rad;
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();
            return UrdfJointContinuous.GetCurrentAngleRad(rigidbody, configurableJoint, originalLocalRotation);
#endif
        }

        /// <summary>
        /// Returns the current velocity of joint in radians per second
        /// </summary>
        /// <returns>floating point for joint velocity in radians per second</returns>
        public override float GetVelocity()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            return ((ArticulationBody)unityJoint).jointVelocity[xAxis];
#else
            //return -((HingeJoint)unityJoint).velocity * Mathf.Deg2Rad;
            return UrdfJointContinuous.GetCurrentLocalVelocity((ConfigurableJoint) unityJoint);
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
            //return -((HingeJoint)unityJoint).motor.force;
            return UrdfJointContinuous.GetEffort((ConfigurableJoint) unityJoint);
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
            drive.target += deltaState * Mathf.Rad2Deg;
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
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            joint.axis = GetAxisData(axisofMotion);
            joint.dynamics = new UrdfJointDescription.Dynamics(unityJoint.angularDamping, unityJoint.jointFriction);
            joint.limit = ExportLimitData();
#else
            joint.axis = GetAxisData(unityJoint.axis);
            //joint.dynamics = new UrdfJointDescription.Dynamics(((HingeJoint)unityJoint).spring.damper, ((HingeJoint)unityJoint).spring.spring);
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            //Note: Note 100% Sure on this, it could also be the Angular X Limit Spring
            //IE: SoftJointLimitSpring - configurableJoint.angularXLimitSpring.damper
            joint.dynamics = new UrdfJointDescription.Dynamics(configurableJoint.angularXDrive.positionSpring, configurableJoint.angularXDrive.positionDamper, configurableJoint.angularXDrive.positionSpring);

            joint.limit = ExportLimitData();
#endif

            return joint;
        }

        public override bool AreLimitsCorrect()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody drive = GetComponent<ArticulationBody>();
            return drive.linearLockX == ArticulationDofLock.LimitedMotion && drive.xDrive.lowerLimit < drive.xDrive.upperLimit;
#else
            //HingeJointLimitsManager limits = GetComponent<HingeJointLimitsManager>();
            //return limits != null && limits.LargeAngleLimitMin < limits.LargeAngleLimitMax;
            return true; //TODO - Verify
#endif
        }

        protected override UrdfJointDescription.Limit ExportLimitData()
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationDrive drive = unityJoint.xDrive;
            return new UrdfJointDescription.Limit(drive.lowerLimit * Mathf.Deg2Rad, drive.upperLimit * Mathf.Deg2Rad, drive.forceLimit, unityJoint.maxAngularVelocity);
#else
            //HingeJointLimitsManager hingeJointLimits = GetComponent<HingeJointLimitsManager>();
            //return new UrdfJointDescription.Limit(
            //    System.Math.Round(hingeJointLimits.LargeAngleLimitMin * Mathf.Deg2Rad, RoundDigits),
            //    System.Math.Round(hingeJointLimits.LargeAngleLimitMax * Mathf.Deg2Rad, RoundDigits),
            //    EffortLimit,
            //    VelocityLimit);

            //HingeJoint hingeJoint = (HingeJoint) unityJoint;
            //return new UrdfJointDescription.Limit(
            //    System.Math.Round(hingeJoint.limits.min * Mathf.Deg2Rad, RoundDigits),
            //    System.Math.Round(hingeJoint.limits.max * Mathf.Deg2Rad, RoundDigits),
            //    EffortLimit,
            //    VelocityLimit);
            
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            return new UrdfJointDescription.Limit(
                System.Math.Round(configurableJoint.lowAngularXLimit.limit * Mathf.Deg2Rad, RoundDigits),
                System.Math.Round(configurableJoint.highAngularXLimit.limit * Mathf.Deg2Rad, RoundDigits),
                EffortLimit,
                VelocityLimit);
#endif
        }

        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void AdjustMovement(UrdfJointDescription joint)
        {
#if !URDF_FORCE_RIGIDBODY
            axisofMotion = (joint.axis != null && joint.axis.xyz != null) ? joint.axis.xyz.ToVector3() : new Vector3(1, 0, 0);
            unityJoint.linearLockX = ArticulationDofLock.LimitedMotion;
            unityJoint.linearLockY = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockZ = ArticulationDofLock.LockedMotion;
            unityJoint.twistLock = ArticulationDofLock.LimitedMotion;

            Vector3 axisofMotionUnity = axisofMotion.Ros2Unity();
            Quaternion Motion = new Quaternion();
            Motion.SetFromToRotation(new Vector3(1, 0, 0), -1 * axisofMotionUnity);
            unityJoint.anchorRotation = Motion;

            if (joint.limit != null)
            {
                ArticulationDrive drive = unityJoint.xDrive;
                drive.upperLimit = (float)(joint.limit.upper * Mathf.Rad2Deg);
                drive.lowerLimit = (float)(joint.limit.lower * Mathf.Rad2Deg);
                drive.forceLimit = (float)(joint.limit.effort);
                unityJoint.maxAngularVelocity = (float)(joint.limit.velocity);
                drive.damping = unityJoint.xDrive.damping;
                drive.stiffness = unityJoint.xDrive.stiffness;
                unityJoint.xDrive = drive;
            }
#else
            
            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            UrdfJointContinuous.AdjustMovementShared(configurableJoint, joint);
            configurableJoint.angularXMotion = ConfigurableJointMotion.Limited;
            if (Math.Abs(joint.limit.lowerRadians - joint.limit.upperRadians) < 0.0001f)
            {
                string warningMessage = $"For Revolute Joint with name {joint.name}, " +
                                        $"joint.limit.lowerRadians == joint.limit.upperRadians should be distinct!" +
                                        $"Consider using a Continuous Joint or a Fixed Joint Instead.";
                RuntimeUrdf.AddImportWarning(warningMessage);
            }
            configurableJoint.lowAngularXLimit = new SoftJointLimit()
            {
                limit = Mathf.Rad2Deg * (float) joint.limit.lowerRadians
            };
            configurableJoint.highAngularXLimit = new SoftJointLimit()
            {
                limit = Mathf.Rad2Deg * (float) joint.limit.upperRadians
            };

            //TODO - Spring.
            configurableJoint.angularXLimitSpring = new SoftJointLimitSpring()
            {
                damper = (float) joint.dynamics.damping,
            };
            
#endif
        }
    }
}

