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

using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfJointFixed : UrdfJoint
    {
        public override JointTypes JointType => JointTypes.Fixed;

        public static UrdfJoint Create(GameObject linkObject)
        {
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            UrdfJointFixed urdfJoint = linkObject.AddComponent<UrdfJointFixed>();
            urdfJoint.unityJoint = linkObject.GetComponent<ArticulationBody>();
#else
            //FixedJoint unityFixedJoint = linkObject.AddComponent<FixedJoint>();
            OptimizeFixedJoint(linkObject);
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
        
        /**
         * The goal here is to have no 'FixedJoints' and instead merge the components together.
         */
        private static void OptimizeFixedJoint(GameObject fixedJointToOptimize)
        {

            Rigidbody rigidbody = fixedJointToOptimize.GetComponent<Rigidbody>();

            PreviousRigidbodyConstants previousRigidbodyConstants = fixedJointToOptimize.AddComponent<PreviousRigidbodyConstants>();
            previousRigidbodyConstants.mass = rigidbody.mass;
            previousRigidbodyConstants.drag = rigidbody.drag;
            previousRigidbodyConstants.angularDrag = rigidbody.angularDrag;
            previousRigidbodyConstants.centerOfMass = rigidbody.centerOfMass;

            Rigidbody fixedParent = FindFixedParent(fixedJointToOptimize);
            UrdfInertial fixedParentUrdfInertial = fixedParent.GetComponent<UrdfInertial>();

            float totalMass = fixedParent.mass + rigidbody.mass;

            float rigidbodyWeighting = rigidbody.mass / totalMass;
            float fixedParentWeighting = fixedParent.mass / totalMass;
            
            float newDrag = rigidbodyWeighting * rigidbody.drag + fixedParentWeighting * fixedParent.drag;
            float newAngularDrag = rigidbodyWeighting * rigidbody.angularDrag + fixedParentWeighting * fixedParent.angularDrag;

            Vector3 fixedParentPreviousLocalCenterOfMass = fixedParent.centerOfMass;
            
            Vector3 rigidbodyPreviousLocalCenterOfMass = rigidbody.centerOfMass;
            Vector3 rigidbodyPreviousWorldCenterOfMass = rigidbody.transform.TransformPoint(rigidbodyPreviousLocalCenterOfMass);
            Vector3 rigidbodyPreviousCenterOfMassParentFrame = fixedParent.transform.InverseTransformPoint(rigidbodyPreviousWorldCenterOfMass);

            Vector3 newFixedParentCenterOfMass = rigidbodyWeighting * rigidbodyPreviousCenterOfMassParentFrame +
                                                 fixedParentWeighting * fixedParentPreviousLocalCenterOfMass;

            fixedParent.mass = totalMass;
            fixedParent.drag = newDrag;
            fixedParent.angularDrag = newAngularDrag;
            fixedParent.centerOfMass = newFixedParentCenterOfMass;
            //NOTE - Although we set the center of mass, on Start the center of mass is overriden by the 
            //       center of mass in the UrdfInertial, so we have to set that value to be correct as well.
            fixedParentUrdfInertial.AdjustedCenterOfMass = newFixedParentCenterOfMass;
            
            //Remove the rigidbody for the FixedJoint that is being removed.
            Destroy(rigidbody);

        }
        
#endif
    }
}

