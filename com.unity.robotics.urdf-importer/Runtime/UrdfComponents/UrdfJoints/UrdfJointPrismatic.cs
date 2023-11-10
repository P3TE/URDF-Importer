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
    public class UrdfJointPrismatic : UrdfJoint
    {
        private ArticulationDrive drive;
#if UNITY_2020_1
        private float maxLinearVelocity;
#endif

        public override JointTypes JointType => JointTypes.Prismatic;

        public static UrdfJoint Create(GameObject linkObject)
        {
            UrdfJointPrismatic urdfJoint = linkObject.AddComponent<UrdfJointPrismatic>();
#if  URDF_FORCE_ARTICULATION_BODY
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
            urdfJoint.unityJoint.jointType = ArticulationJointType.PrismaticJoint;
#else
            urdfJoint.unityJoint = linkObject.AddComponent<ConfigurableJoint>();
            urdfJoint.unityJoint.autoConfigureConnectedAnchor = true;

            ConfigurableJoint configurableJoint = (ConfigurableJoint) urdfJoint.unityJoint;

            // degrees of freedom:
            configurableJoint.xMotion = ConfigurableJointMotion.Limited;
            configurableJoint.yMotion = ConfigurableJointMotion.Locked;
            configurableJoint.zMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularXMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;

            linkObject.AddComponent<PrismaticJointLimitsManager>();
#endif
            return urdfJoint;
        }

        #region Runtime

        /// <summary>
        /// Returns the current position of the joint in meters
        /// </summary>
        /// <returns>floating point number for joint position in meters</returns>
        public override float GetPosition()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointPosition[xAxis];
#else
            return Vector3.Dot(unityJoint.transform.localPosition - unityJoint.connectedAnchor, unityJoint.axis);
#endif
        }

        /// <summary>
        /// Returns the current velocity of joint in meters per second
        /// </summary>
        /// <returns>floating point for joint velocity in meters per second</returns>
        public override float GetVelocity()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointVelocity[xAxis];
#else
            return float.NaN;
#endif
        }

        /// <summary>
        /// Returns current joint torque in N
        /// </summary>
        /// <returns>floating point in N</returns>
        public override float GetEffort()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            return unityJoint.jointForce[xAxis];
#else
                return float.NaN;
#endif

        }

        /// <summary>
        /// Rotates the joint by deltaState m
        /// </summary>
        /// <param name="deltaState">amount in m by which joint needs to be rotated</param>
        protected override void OnUpdateJointState(float deltaState)
        {
#if  URDF_FORCE_ARTICULATION_BODY
            ArticulationDrive drive = unityJoint.xDrive;
            drive.target += deltaState;
            unityJoint.xDrive = drive;
#else
            transform.Translate(unityJoint.axis * deltaState);
#endif
        }

        #endregion

        #region Import

        protected override void ImportJointData(UrdfJointDescription joint)
        {
#if  URDF_FORCE_ARTICULATION_BODY
            var axis = (joint.axis != null && joint.axis.xyz != null) ? joint.axis.xyz.ToVector3() : new Vector3(1, 0, 0);
            SetAxisData(axis);
            SetLimits(joint);
            SetDynamics(joint.dynamics);
#else
            throw new NotImplementedException();
#endif
        }

#if  URDF_FORCE_ARTICULATION_BODY
        /// <summary>
        /// Reads axis joint information and rotation to the articulation body to produce the required motion
        /// </summary>
        /// <param name="joint">Structure containing joint information</param>
        protected override void SetAxisData(Vector3 axis) // Test this function
        {
            axisofMotion = axis;
            Vector3 axisofMotionUnity = axisofMotion.Ros2Unity();
            Quaternion Motion = new Quaternion();
            Motion.SetFromToRotation(new Vector3(1, 0, 0), axisofMotionUnity);
            unityJoint.anchorRotation = Motion;
        }

        protected override void SetLimits(Joint joint)
        {
            unityJoint.linearLockX = (joint.limit != null) ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
            unityJoint.linearLockY = ArticulationDofLock.LockedMotion;
            unityJoint.linearLockZ = ArticulationDofLock.LockedMotion;
            
            if (joint.limit != null)
            {
                ArticulationDrive drive = unityJoint.xDrive;
                drive.upperLimit = (float)joint.limit.upper;
                drive.lowerLimit = (float)joint.limit.lower;
                drive.forceLimit = (float)joint.limit.effort;

                unityJoint.maxLinearVelocity = (float)joint.limit.velocity;
                unityJoint.xDrive = drive;
            }
        }

#endif
        
        #endregion
        
        #region Export

        protected override UrdfJointDescription ExportSpecificJointData(UrdfJointDescription joint)
        {
#if  URDF_FORCE_ARTICULATION_BODY
            
            joint.axis = new UrdfJointDescription.Axis((unityJoint.anchorRotation * Vector3.right).Unity2Ros());
            joint.dynamics = new Joint.Dynamics(unityJoint.linearDamping, unityJoint.jointFriction);
            joint.limit = ExportLimitData();
            return joint;
#else
            ConfigurableJoint configurableJoint = (ConfigurableJoint)unityJoint;

            joint.axis = GetAxisData(configurableJoint.axis);
            joint.dynamics = new UrdfJointDescription.Dynamics(configurableJoint.xDrive.positionSpring, configurableJoint.xDrive.positionDamper, configurableJoint.xDrive.positionSpring);
            joint.limit = ExportLimitData();
#endif
            return joint;
        }

        public override bool AreLimitsCorrect()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            ArticulationBody joint = GetComponent<ArticulationBody>();
            return joint.linearLockX == ArticulationDofLock.LimitedMotion && joint.xDrive.lowerLimit < joint.xDrive.upperLimit;
#else
            PrismaticJointLimitsManager limits = GetComponent<PrismaticJointLimitsManager>();
            return limits != null && limits.PositionLimitMin < limits.PositionLimitMax;
#endif
        }

        protected override UrdfJointDescription.Limit ExportLimitData()
        {
#if  URDF_FORCE_ARTICULATION_BODY
            ArticulationDrive drive = GetComponent<ArticulationBody>().xDrive;
#if UNITY_2020_2_OR_NEWER
            return new UrdfJointDescription.Limit(drive.lowerLimit, drive.upperLimit, drive.forceLimit, unityJoint.maxLinearVelocity);
#elif UNITY_2020_1
            return new Joint.Limit(drive.lowerLimit, drive.upperLimit, drive.forceLimit, maxLinearVelocity);
#endif
#else
            PrismaticJointLimitsManager prismaticLimits = GetComponent<PrismaticJointLimitsManager>();
            return new UrdfJointDescription.Limit(
                System.Math.Round(prismaticLimits.PositionLimitMin, RoundDigits),
                System.Math.Round(prismaticLimits.PositionLimitMax, RoundDigits),
                EffortLimit,
                VelocityLimit);
#endif
        }

        #endregion
    }
}
