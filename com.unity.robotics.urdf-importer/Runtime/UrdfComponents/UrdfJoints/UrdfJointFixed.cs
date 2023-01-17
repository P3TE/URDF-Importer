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

using System;
using System.Text;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfJointFixed : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Fixed;

        /*
         * Some joints that weren't previously fixed joints in the xacro
         * but were optimised into fixed joints won't have their transform
         * published by a joint state publisher without their joint state
         * being published. This flags that the joint state should be
         * published for this joint even though it is fixed.
         */
        public bool JointIsDynamicForJointStatePublishing
        {
            get;
            set;
        }

        public static UrdfJoint Create(GameObject linkObject)
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            UrdfJointFixed urdfJoint = linkObject.AddComponent<UrdfJointFixed>();
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
#else
            //FixedJoint unityFixedJoint = linkObject.AddComponent<FixedJoint>();
            
            //Remove any rigidbody which does not already have any joints depending on it.
            if (linkObject.GetComponent<Joint>() == null)
            {
                PreviousRigidbodyConstants previousRigidbodyConstants = AddPreviousRigidbodyConstantsUsingRigidBody(linkObject);
                OptimizeFixedJoint(linkObject, previousRigidbodyConstants);   
            }
            UrdfJointFixed urdfJoint = linkObject.AddComponent<UrdfJointFixed>();
            //urdfJoint.unityJoint = unityFixedJoint;
            //urdfJoint.unityJoint.autoConfigureConnectedAnchor = true;
#endif

            return urdfJoint;
        }

        protected override bool IsJointAxisDefined()
        {
            return true; //Axis isn't used
        }
        
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
#else

        public static UrdfJointFixed CreateOptimizeFixedJoint(GameObject fixedJointToOptimize, UrdfLinkDescription link, UrdfJointDescription joint)
        {
            UrdfInertial.Create(fixedJointToOptimize, link.inertial, false);
            PreviousRigidbodyConstants previousRigidbodyConstants =
                AddPreviousRigidbodyConstantsUsingUrdfLinkDescription(fixedJointToOptimize, link);
            UrdfJointFixed result = fixedJointToOptimize.AddComponent<UrdfJointFixed>();
            
            JointTypes jointType = GetJointType(joint.type);
            result.originalLocalRotation = fixedJointToOptimize.transform.localRotation;
            result.jointName = joint.name;

            switch (jointType)
            {
                case JointTypes.Revolute:
                case JointTypes.Continuous:
                case JointTypes.Prismatic:
                    result.JointIsDynamicForJointStatePublishing = true;
                    break;
            }
            //JointIsDynamicForJointStatePublishing
            
            OptimizeFixedJoint(fixedJointToOptimize, previousRigidbodyConstants);
            return result;
        }
        
        private static PreviousRigidbodyConstants AddPreviousRigidbodyConstantsUsingUrdfLinkDescription(GameObject fixedJointToOptimize, UrdfLinkDescription link)
        {
            if (link.inertial == null)
            {
                return null;
            }
            PreviousRigidbodyConstants previousRigidbodyConstants = fixedJointToOptimize.AddComponent<PreviousRigidbodyConstants>();
            previousRigidbodyConstants.SetValues(link);
            return previousRigidbodyConstants;
        }

        public static PreviousRigidbodyConstants AddPreviousRigidbodyConstantsUsingRigidBody(GameObject fixedJointToOptimize)
        {
            Rigidbody rigidbody = fixedJointToOptimize.GetComponent<Rigidbody>();

            PreviousRigidbodyConstants previousRigidbodyConstants = fixedJointToOptimize.AddComponent<PreviousRigidbodyConstants>();
            previousRigidbodyConstants.SetValues(rigidbody);
            
            Debug.LogWarning("This method shouldn't be used anymore, use AddPreviousRigidbodyConstantsUsingUrdfLinkDescription instead.");
            //Remove the rigidbody for the FixedJoint that is being removed.
            Destroy(rigidbody);

            return previousRigidbodyConstants;
        }

        public static void OptimizeFixedJoint(GameObject fixedJointToOptimize,
            PreviousRigidbodyConstants previousRigidbodyConstants)
        {

            if (previousRigidbodyConstants == null)
            {
                return;
            }

            Rigidbody fixedParent;
            try
            {
                fixedParent = FindCrucialParent(fixedJointToOptimize);
            }
            catch
            {
                //Ghosts (bots which are receiving their pose externally) have no rigidbody anywhere in their hierarchy.
                return;
            }
            UrdfInertial fixedParentUrdfInertial = fixedParent.GetComponent<UrdfInertial>();

            if (previousRigidbodyConstants.inertiaCalculationType.CanInheritInertiaCalculation())
            {
                //All good, use parent inertiaCalculationType.
            }
            else
            {
                //Check for a mismatch.
                bool fixedParentIsAutomatic = fixedParentUrdfInertial.inertiaCalculationType.AutomaticInertiaCalculation();
                bool childIsAutomatic = previousRigidbodyConstants.inertiaCalculationType.AutomaticInertiaCalculation();

                if (fixedParentIsAutomatic != childIsAutomatic)
                {
                    StringBuilder errorMessageBuilder = new StringBuilder();
                    errorMessageBuilder.Append("Failed to OptimizeFixedJoint! ");
                    errorMessageBuilder.Append("Child with name ");
                    errorMessageBuilder.Append(fixedJointToOptimize.name);
                    errorMessageBuilder.Append(" will not inherit the inertia calculation type and requires ");
                    errorMessageBuilder.Append(previousRigidbodyConstants.inertiaCalculationType.GetType());
                    errorMessageBuilder.Append(" put the parent with name ");
                    errorMessageBuilder.Append(fixedParent.name);
                    errorMessageBuilder.Append(" is of type ");
                    errorMessageBuilder.Append(fixedParentUrdfInertial.inertiaCalculationType.GetType());
                    errorMessageBuilder.Append("!");
                    errorMessageBuilder.Append(" Either set the child to an inherit mode or change the type so the parent matches the child.");
                    throw new Exception(errorMessageBuilder.ToString());
                }
            }
            
            Matrix3x3 inertiaOriginal = UrdfInertial.CalculateInertiaTensorMatrix(fixedParent.inertiaTensor, fixedParent.inertiaTensorRotation, Quaternion.identity);
            Matrix3x3 inertiaAdded = UrdfInertial.CalculateInertiaTensorMatrix(previousRigidbodyConstants.inertiaTensor, previousRigidbodyConstants.inertiaTensorRotation, Quaternion.identity);

            Transform fixedParentTransform = fixedParent.transform;
            Vector3 worldPositionFixedParent = fixedParentTransform.position;
            Quaternion worldRotationFixedParent = fixedParentTransform.rotation;
            Quaternion worldRotationFixedParentInverse = Quaternion.Inverse(worldRotationFixedParent);
            
            Transform addedBodyTransform = previousRigidbodyConstants.transform;
            Vector3 worldPositionAddedBody = addedBodyTransform.position;
            Quaternion worldRotationAddedBody = addedBodyTransform.rotation;
            
            Vector3 relativePositionAdded = worldRotationFixedParentInverse * (worldPositionAddedBody - worldPositionFixedParent);
            Quaternion relativeRotationAdded = worldRotationFixedParentInverse * worldRotationAddedBody;
            
            float fixedBodyMassRatio = CombineRigidbodiesMassInertiaLocal(
                fixedParent.centerOfMass, fixedParent.mass, inertiaOriginal,
                relativePositionAdded, relativeRotationAdded, previousRigidbodyConstants.centerOfMass, 
                previousRigidbodyConstants.mass, inertiaAdded,
                out float totalMass, out Vector3 newComLocal, out Vector3 combinedInertiaTensor, out Quaternion combinedInertiaTensorRotation);
            
            fixedParent.mass = totalMass;
            fixedParent.centerOfMass = newComLocal;
            fixedParent.inertiaTensor = combinedInertiaTensor;
            fixedParent.inertiaTensorRotation = combinedInertiaTensorRotation;

            //Calculate the rest using the lerp values.
            fixedParent.drag = Mathf.Lerp(previousRigidbodyConstants.drag, fixedParent.drag, fixedBodyMassRatio);
            fixedParent.angularDrag = Mathf.Lerp(previousRigidbodyConstants.angularDrag, fixedParent.angularDrag, fixedBodyMassRatio);

        }


        private static float CombineRigidbodiesMassInertiaLocal(
            Vector3 localComOriginal, float massOriginal, Matrix3x3 intertiaOriginal,
            Vector3 relativePositionAdded, Quaternion relativeRotationAdded, Vector3 localComAdded, float massAdded, Matrix3x3 intertiaAdded,
            out float totalMass, out Vector3 newComLocal, out Vector3 combinedInertiaTensor, out Quaternion combinedInertiaTensorRotation)
        {
            
            totalMass = massOriginal + massAdded;
            float oneOnTotalMass = 1.0f / totalMass;
            float bodyMassWeightingOriginal = massOriginal * oneOnTotalMass;
            float bodyMassWeightingAdded = massAdded * oneOnTotalMass;

            Vector3 relativeComAdded = relativePositionAdded + (relativeRotationAdded * localComAdded);
            
            newComLocal = localComOriginal * bodyMassWeightingOriginal + 
                          relativeComAdded * bodyMassWeightingAdded;

            Vector3 inertiaTensorAdded = intertiaAdded.PxDiagonalize(out Quaternion inertiaTensorRotationAdded);
            //TODO - Verify that relativeRotationAdded is the correct parameter for inertialAxisRotation when combining inertias from objects with different rotations.
            Matrix3x3 existingInertiaAdded = UrdfInertial.CalculateInertiaTensorMatrix(inertiaTensorAdded, inertiaTensorRotationAdded, relativeRotationAdded);
            Matrix3x3 existingInertia = intertiaOriginal + existingInertiaAdded;
            
            //Calculate the additional offset using parallel axis theorem
            Matrix3x3 offsetInertiaOriginal = CalculateOffsetInertia(newComLocal, Quaternion.identity, Vector3.zero, massOriginal);
            Matrix3x3 offsetInertiaAdded = CalculateOffsetInertia(newComLocal, Quaternion.identity, relativePositionAdded, massAdded);
            Matrix3x3 totalOffsetInertia = offsetInertiaOriginal + offsetInertiaAdded;
            Matrix3x3 totalInertiaMatrix = existingInertia + totalOffsetInertia;

            combinedInertiaTensor = totalInertiaMatrix.PxDiagonalize(out combinedInertiaTensorRotation);

            return bodyMassWeightingOriginal;
        }
        
        private static Matrix3x3 CalculateOffsetInertia(Vector3 com, Quaternion rotation, Vector3 position, float mass)
        {
            Vector3 fromComToM = position - com;
            float distance = fromComToM.magnitude;
            if (distance < 0.0001f)
            {
                return new Matrix3x3();
            }
            Vector3 fromComToMNormalised = fromComToM / distance;
            Vector3 rightAxis = rotation * Vector3.right;
            Quaternion offsetRotation = Quaternion.FromToRotation(rightAxis, fromComToMNormalised);
            
            float diagonals = mass * distance * distance;
            Vector3 inertialTensor = new Vector3(0f, diagonals, diagonals);

            Matrix3x3 inertiaMatrix =
                UrdfInertial.CalculateInertiaTensorMatrix(inertialTensor, offsetRotation, Quaternion.identity);
            return inertiaMatrix;
        }
        
#endif
    }
}

