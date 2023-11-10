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

#define REVOLUTE_AS_HINGE_JOINTS


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
#if  URDF_FORCE_ARTICULATION_BODY
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.RevoluteJoint;

#else
            
#if REVOLUTE_AS_HINGE_JOINTS
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
        public override float GetPosition()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return ((ArticulationBody)unityJoint).jointPosition[xAxis];
#else
    #if REVOLUTE_AS_HINGE_JOINTS
            return UrdfJointContinuous.GetCurrentAngleRadHingeJoint(unityJoint as HingeJoint);
    #else
            ConfigurableJoint configurableJoint = unityJoint as ConfigurableJoint;
            Rigidbody rigidbody = configurableJoint.GetComponent<Rigidbody>();
            return UrdfJointContinuous.GetCurrentAngleRadContinuousJoint(rigidbody, configurableJoint, originalLocalRotation);
    #endif
#endif
        }

        /// <summary>
        /// Returns the current velocity of joint in radians per second
        /// </summary>
        /// <returns>floating point for joint velocity in radians per second</returns>
        public override float GetVelocity()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return ((ArticulationBody)unityJoint).jointVelocity[xAxis];
#else
    #if REVOLUTE_AS_HINGE_JOINTS
            return UrdfJointContinuous.GetCurrentLocalVelocityHingeJoint(unityJoint as HingeJoint);
    #else
            return UrdfJointContinuous.GetCurrentLocalVelocityConfigurableJoint(unityJoint as ConfigurableJoint);
    #endif
#endif
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
    #if REVOLUTE_AS_HINGE_JOINTS
            return UrdfJointContinuous.GetEffortHingeJoint(unityJoint as HingeJoint);
    #else
            return UrdfJointContinuous.GetEffortConfigurableJoint(unityJoint as ConfigurableJoint);
    #endif
#endif
        }


        /// <summary>
        /// Rotates the joint by deltaState radians 
        /// </summary>
        /// <param name="deltaState">amount in radians by which joint needs to be rotated</param>
        protected override void OnUpdateJointState(float deltaState)
        {
#if  URDF_FORCE_ARTICULATION_BODY
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
#if  URDF_FORCE_ARTICULATION_BODY
            joint.axis = new UrdfJointDescription.Axis((unityJoint.anchorRotation * Vector3.right).Unity2Ros());
            joint.dynamics = new Joint.Dynamics(unityJoint.angularDamping, unityJoint.jointFriction);
            joint.limit = ExportLimitData();
            return joint;
#else
            joint.axis = GetAxisData(unityJoint.axis);
            if (unityJoint is HingeJoint hingeJoint)
            {
                joint.dynamics = new UrdfJointDescription.Dynamics(hingeJoint.spring.damper, hingeJoint.spring.spring, 0);
            } else if (unityJoint is ConfigurableJoint configurableJoint)
            {
                //Note: Note 100% Sure on this, it could also be the Angular X Limit Spring
                //IE: SoftJointLimitSpring - configurableJoint.angularXLimitSpring.damper
                joint.dynamics = new UrdfJointDescription.Dynamics(configurableJoint.angularXDrive.positionSpring, configurableJoint.angularXDrive.positionDamper, 0);
            }
            else
            {
                throw new Exception($"Unsupported joint type: {unityJoint.GetType().Name}");
            }
            
            

            joint.limit = ExportLimitData();
#endif

            return joint;
        }

        public override bool AreLimitsCorrect()
        {
#if  URDF_FORCE_ARTICULATION_BODY
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
#if  URDF_FORCE_ARTICULATION_BODY
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
            
            if (unityJoint is HingeJoint hingeJoint)
            {
                return new UrdfJointDescription.Limit(
                    System.Math.Round(hingeJoint.limits.min * Mathf.Deg2Rad, RoundDigits),
                    System.Math.Round(hingeJoint.limits.max * Mathf.Deg2Rad, RoundDigits),
                    EffortLimit,
                    VelocityLimit
                );
            } else if (unityJoint is ConfigurableJoint configurableJoint)
            {
                return new UrdfJointDescription.Limit(
                    System.Math.Round(configurableJoint.lowAngularXLimit.limit * Mathf.Deg2Rad, RoundDigits),
                    System.Math.Round(configurableJoint.highAngularXLimit.limit * Mathf.Deg2Rad, RoundDigits),
                    EffortLimit,
                    VelocityLimit);
            }
            else
            {
                throw new Exception($"Unsupported joint type: {unityJoint.GetType().Name}");
            }
#endif
        }
        
        public static bool ShouldRevoluteJointBeOptimisedToFixed(UrdfJointDescription joint, out string reason)
        {
            if (Math.Abs(joint.limit.lowerRadians - joint.limit.upperRadians) < 0.0001f)
            {
                /*
                 * Note: This typically happens because Gazebo requires a revolute joint for sensors otherwise it will crash.
                 *       To keep things cross compatible with gazebo, we'll just remove revolute joints that should be fixed.
                 */
                reason = $"For Revolute Joint with name {joint.name}, " +
                                 "joint.limit.lowerRadians == joint.limit.upperRadians should be distinct!" +
                                 " Consider using a Continuous Joint or a Fixed Joint Instead.";
                return true;
            }

            reason = "";
            return false;
        }

        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void AdjustMovement(UrdfJointDescription joint)
        {
#if URDF_FORCE_ARTICULATION_BODY

            axisofMotion = axis;
            Vector3 axisofMotionUnity = axisofMotion.Ros2Unity();
            Quaternion Motion = new Quaternion();
            Motion.SetFromToRotation(new Vector3(1, 0, 0), -1 * axisofMotionUnity);
            unityJoint.anchorRotation = Motion;

            unityJoint.linearLockX = ArticulationDofLock.LimitedMotion;
            unityJoint.linearLockY = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockZ = ArticulationDofLock.LockedMotion;
            unityJoint.twistLock = ArticulationDofLock.LimitedMotion;

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


#if REVOLUTE_AS_HINGE_JOINTS
            HingeJoint hingeJoint = (HingeJoint)unityJoint;
            UrdfJointContinuous.AdjustMovementSharedHingeJoint(joint, hingeJoint);
            
            hingeJoint.useLimits = true;
            hingeJoint.limits = new JointLimits()
            {
                min = Mathf.Rad2Deg * (float)joint.limit.lowerRadians,
                max = Mathf.Rad2Deg * (float)joint.limit.upperRadians
            };
#else

            ConfigurableJoint configurableJoint = (ConfigurableJoint) unityJoint;
            UrdfJointContinuous.AdjustMovementSharedConfirguableJoint(configurableJoint, joint);
            configurableJoint.angularXMotion = ConfigurableJointMotion.Limited;
            if (ShouldRevoluteJointBeOptimisedToFixed(joint, out string warningMessage))
            {
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
            /*configurableJoint.angularXLimitSpring = new SoftJointLimitSpring()
            {
                damper = (float) joint.dynamics.damping,
            };*/
#endif
            
            
            
            
#endif
        }
    }
}

