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
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Robotics.UrdfImporter
{
#if  UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
    [RequireComponent(typeof(ArticulationBody))]
#else
    //[RequireComponent(typeof(Rigidbody))]
#endif
    public class UrdfInertial : MonoBehaviour
    {
        [SerializeField] public bool displayInertiaGizmo;
        
        const int k_RoundDigits = 10;
        const float k_MinInertia = 1e-6f;
        const float k_MinMass = 0.1f;
        
        public bool useUrdfData;
        public Vector3 centerOfMass;
        public Vector3? _adjustedCenterOfMass = null;
        private bool startCalled = false;
        public UrdfLinkDescription.Inertial.InertiaCalculationType inertiaCalculationType;
        public Vector3 inertiaTensor;
        public Quaternion inertiaTensorRotation;
        public Quaternion inertialAxisRotation;
        
        [SerializeField, HideInInspector]
        UrdfLinkDescription.Inertial m_OriginalValues;
        
        [SerializeField, HideInInspector]
        UrdfLinkDescription.Inertial m_Overrides;

        public Vector3 AdjustedCenterOfMass
        {
            get => _adjustedCenterOfMass.GetValueOrDefault(centerOfMass);
            set
            {
                _adjustedCenterOfMass = value;
                if(startCalled)
                {
                    UpdateLinkData();
                }
            }
        }

        public static void Create(GameObject linkObject, UrdfLinkDescription.Inertial inertialLink = null, bool addRigidBody = true)
        {
            UrdfInertial urdfInertial = linkObject.AddComponent<UrdfInertial>();
        
            //TODO - P3TE_MERGE - addRigidBody ??. Should GetComponent even be called?
#if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody robotLink = urdfInertial.GetComponent<ArticulationBody>();
#else
            Rigidbody robotLink = urdfInertial.GetComponent<Rigidbody>();
            if (addRigidBody)
            {
                robotLink = linkObject.AddComponent<Rigidbody>();
            }
#endif
            if (inertialLink != null)
            {
                robotLink.mass = Mathf.Max((float)inertialLink.mass, k_MinMass);
                if (inertialLink.origin != null) {
                    robotLink.centerOfMass = UrdfOrigin.GetPositionFromUrdf(inertialLink.origin);
                }
                else
                {
                    robotLink.centerOfMass = Vector3.zero;
                }
                //TODO - P3TE_MERGE - Does this function set m_OriginalValues, m_Overrides, useUrdfData ?
                //TODO - P3TE_MERGE - inertialUrdf.m_Overrides = inertialUrdf.ToLinkInertial(robotLink);
                urdfInertial.ImportInertiaData(inertialLink);
                 
                urdfInertial.useUrdfData = true;
            }
            inertialUrdf.UpdateLinkData();
            
            urdfInertial.displayInertiaGizmo = false;
        }

        public void ResetInertial()
        {
            m_Overrides = m_OriginalValues;
            AssignUrdfInertiaData(m_Overrides);
        }

#region Runtime

        void Start()
        {
            startCalled = true;
            UpdateLinkData();
        }

        public void UpdateLinkData(bool copyOverrides = false, bool manualInput = false)
        {

#if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody robotLink = GetComponent<ArticulationBody>();
#else
              Rigidbody robotLink = GetComponent<Rigidbody>();
#endif
            if (robotLink == null)
            {
                //Can be null when FixedJoint optimisations are applied.
                return;
            }

            if (useUrdfData)
            {
                
                if (m_OriginalValues == null)
                {
                    //TODO - P3TE_MERGE - ??
                    Debug.LogWarning(
                        "This instance doesn't have any urdf data stored - " +
                        "creating some using the current inertial values.");
                    m_OriginalValues = ToLinkInertial(articulationBody);
                }
                Assert.IsNotNull(m_OriginalValues);
                
                //TODO - P3TE_MERGE - Why don't we just call ImportInertiaData
                robotLink.centerOfMass = AdjustedCenterOfMass;
                if (inertiaCalculationType.AutomaticInertiaCalculation())
                {
                    robotLink.ResetInertiaTensor();
                }
                else
                {
                    robotLink.inertiaTensor = inertiaTensor;
                    robotLink.inertiaTensorRotation = inertiaTensorRotation * inertialAxisRotation;
                }

                Vector3 safeInertia = EnsureMinimumInertia(robotLink.inertiaTensor, out bool wasBelowMinimumInertia);
                if(wasBelowMinimumInertia)
                {
                    robotLink.inertiaTensor = safeInertia;
                    RuntimeUrdf.AddImportWarning($"Inertia Tensor below {k_MinInertia} detected on {robotLink.gameObject.name}! Due to floating-point precision values lower than {MinInertia} may cause erratic behaviour so the Inertia Tensor for this object has been adjusted to be at least the minimum value.");
                }
                
            }
            else
            {
                robotLink.ResetCenterOfMass();
                robotLink.ResetInertiaTensor();
            }
            
            //TODO - P3TE_MERGE - copyOverrides ???
            if (copyOverrides)
            {
                m_Overrides ??= new Link.Inertial(m_OriginalValues);
            }
            // Ensure that when this script is hot-loaded for the first time that this previously non-existent variable
            // gets some sensible values (by copying them from the current state of the ArticulationBody)
            else if (m_Overrides == null)
            {
                m_Overrides = ToLinkInertial(articulationBody);
            }
            else if (manualInput)
            {
                m_Overrides = ToLinkInertial(articulationBody, false);
            }
        }

        private Vector3 EnsureMinimumInertia(Vector3 originalInertiaTensor, out bool wasBelowMinimumInertia)
        {
            wasBelowMinimumInertia = false;
            Vector3 result = originalInertiaTensor;
            for (int i = 0; i < 3; i++)
            {
                if (result[i] < MinInertia)
                {
                    result[i] = MinInertia;
                    wasBelowMinimumInertia = true;
                }
            }
            return result;
        }

        private void OnDrawGizmosSelected()
        {
            if (displayInertiaGizmo)
            {
                #if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
                    Debug.Log("'ArticulationBody' does not contain a definition for 'inertiaTensorRotation' and no accessible extension method 'inertiaTensorRotation'");
               /* Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, GetComponent<ArticulationBody>().inertiaTensorRotation * Vector3.forward * GetComponent<ArticulationBody>().inertiaTensor.z);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, GetComponent<ArticulationBody>().inertiaTensorRotation * Vector3.up * GetComponent<ArticulationBody>().inertiaTensor.y);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, GetComponent<ArticulationBody>().inertiaTensorRotation * Vector3.right * GetComponent<ArticulationBody>().inertiaTensor.x);*/
                #else
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, GetComponent<Rigidbody>().inertiaTensorRotation * Vector3.forward * GetComponent<Rigidbody>().inertiaTensor.z);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, GetComponent<Rigidbody>().inertiaTensorRotation * Vector3.up * GetComponent<Rigidbody>().inertiaTensor.y);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, GetComponent<Rigidbody>().inertiaTensorRotation * Vector3.right * GetComponent<Rigidbody>().inertiaTensor.x);
                #endif
            }
        }

#endregion

#region Import

        private void ImportInertiaData(UrdfLinkDescription.Inertial inertial)
        {
            

            Matrix3x3 unityInertiaMatrix = ToUnityMatrix3x3(inertial.inertia);
            Vector3 inertialTensorUnity = FixMinInertia(unityInertiaMatrix.PxDiagonalize(out Quaternion inertialTensorRotationUnity));
            
            //TODO - P3TE_MERGE - This previous implementation:
            //Vector3 eigenvalues;
            //Vector3[] eigenvectors;
            //Matrix3x3 rotationMatrix = ToMatrix3x3(inertial.inertia);
            //rotationMatrix.DiagonalizeRealSymmetric(out eigenvalues, out eigenvectors);
            //Vector3 inertialTensorUnity = ToUnityInertiaTensor(FixMinInertia(eigenvalues));
            //Quaternion inertialTensorRotationUnity = ToQuaternion(eigenvectors[0], eigenvectors[1], eigenvectors[2]).Ros2Unity() * this.inertialAxisRotation;
            
#if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody robotLink = GetComponent<ArticulationBody>();
#else
            Rigidbody robotLink = GetComponent<Rigidbody>();
#endif
            
            robotLink.mass = Mathf.Max((float)inertial.mass, k_MinMass);
            if (inertial.origin != null) {
                robotLink.centerOfMass = UrdfOrigin.GetPositionFromUrdf(inertial.origin);
            }
            else
            {
                robotLink.centerOfMass = Vector3.zero;
            }

            Vector3 inertiaEulerAngles;

            if (inertial.origin != null)
            {
                inertiaEulerAngles = UrdfOrigin.GetRotationFromUrdf(inertial.origin);
            }
            else
            {
                inertiaEulerAngles = new Vector3(0, 0, 0);
            }

            this.inertialAxisRotation.eulerAngles = inertiaEulerAngles;
            
            this.inertiaCalculationType = inertial.inertia.inertiaCalculationType;

            this.inertiaTensor = inertialTensorUnity;
            this.inertiaTensorRotation = inertialTensorRotationUnity;

            //TODO - P3TE_MERGE -  -  if the user can specify this in the xacro, we shouldn't be using the rigibidboy (robotLink) version.
            this.centerOfMass = robotLink.centerOfMass;

        }

        private static Vector3 ToUnityInertiaTensor(Vector3 vector3)
        {
            return new Vector3(vector3.y, vector3.z, vector3.x);
        }

        private static Matrix3x3 ToMatrix3x3(UrdfLinkDescription.Inertial.Inertia inertia)
        {
            return new Matrix3x3(
                new[] { (float)inertia.ixx, (float)inertia.ixy, (float)inertia.ixz,
                                             (float)inertia.iyy, (float)inertia.iyz,
                                                                 (float)inertia.izz });
        }
        
        public static Matrix3x3 ToUnityMatrix3x3(UrdfLinkDescription.Inertial.Inertia inertia)
        {
            return new Matrix3x3(
                new[] { (float)inertia.iyy, (float)inertia.iyz, (float)inertia.ixy,
                    (float)inertia.izz, (float)inertia.ixz,
                    (float)inertia.ixx });
        }

        private static Vector3 FixMinInertia(Vector3 vector3)
        {
            for (int i = 0; i < 3; i++)
            {
                if (vector3[i] < k_MinInertia)
                    vector3[i] = k_MinInertia;
            }
            return vector3;
        }

        private static Quaternion ToQuaternion(Vector3 eigenvector0, Vector3 eigenvector1, Vector3 eigenvector2)
        {
            //From http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/
            float tr = eigenvector0[0] + eigenvector1[1] + eigenvector2[2];
            float qw, qx, qy, qz;
            if (tr > 0)
            {
                float s = Mathf.Sqrt(tr + 1.0f) * 2f; // S=4*qw 
                qw = 0.25f * s;
                qx = (eigenvector1[2] - eigenvector2[1]) / s;
                qy = (eigenvector2[0] - eigenvector0[2]) / s;
                qz = (eigenvector0[1] - eigenvector1[0]) / s;
            }
            else if ((eigenvector0[0] > eigenvector1[1]) & (eigenvector0[0] > eigenvector2[2]))
            {
                float s = Mathf.Sqrt(1.0f + eigenvector0[0] - eigenvector1[1] - eigenvector2[2]) * 2; // S=4*qx 
                qw = (eigenvector1[2] - eigenvector2[1]) / s;
                qx = 0.25f * s;
                qy = (eigenvector1[0] + eigenvector0[1]) / s;
                qz = (eigenvector2[0] + eigenvector0[2]) / s;
            }
            else if (eigenvector1[1] > eigenvector2[2])
            {
                float s = Mathf.Sqrt(1.0f + eigenvector1[1] - eigenvector0[0] - eigenvector2[2]) * 2; // S=4*qy
                qw = (eigenvector2[0] - eigenvector0[2]) / s;
                qx = (eigenvector1[0] + eigenvector0[1]) / s;
                qy = 0.25f * s;
                qz = (eigenvector2[1] + eigenvector1[2]) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1.0f + eigenvector2[2] - eigenvector0[0] - eigenvector1[1]) * 2; // S=4*qz
                qw = (eigenvector0[1] - eigenvector1[0]) / s;
                qx = (eigenvector2[0] + eigenvector0[2]) / s;
                qy = (eigenvector2[1] + eigenvector1[2]) / s;
                qz = 0.25f * s;
            }
            return new Quaternion(qx, qy, qz, qw);
        }

#endregion

#region Export
        public UrdfLinkDescription.Inertial ExportInertialData() 
        {
#if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody robotLink = GetComponent<ArticulationBody>();
#else
            Rigidbody robotLink = GetComponent<Rigidbody>();
#endif

            if (robotLink == null)
            {
                Debug.LogWarning("No data to export.");
                return null;
            }

            UpdateLinkData();
            Vector3 originAngles = inertialAxisRotation.eulerAngles;
            UrdfOriginDescription inertialOrigin = new UrdfOriginDescription(robotLink.centerOfMass.Unity2Ros().ToRoundedDoubleArray(), new double[] { (double)originAngles.x, (double)originAngles.y, (double)originAngles.z });
            UrdfLinkDescription.Inertial.Inertia inertia = ExportInertiaData();

            return new UrdfLinkDescription.Inertial(Math.Round(robotLink.mass, k_RoundDigits), inertialOrigin, inertia);
        }

        private UrdfLinkDescription.Inertial.Inertia ExportInertiaData()
        {
#if   UNITY_2020_1_OR_NEWER && !URDF_FORCE_RIGIDBODY
            ArticulationBody robotLink = GetComponent<ArticulationBody>();
#else
            Rigidbody robotLink = GetComponent<Rigidbody>();
#endif
            
            Matrix3x3 inertiaMatrix = CalculateInertiaTensorMatrix(robotLink.inertiaTensor, robotLink.inertiaTensorRotation, inertialAxisRotation);
            
            //TODO - P3TE_MERGE - The upstream had branching code where you could specify whether it was calculated from the articulation body or fell back to inertiaTensor and inertiaTensorRotation

            return ToRosCoordinates(ToInertia(inertiaMatrix));
        }

        public static Matrix3x3 CalculateInertiaTensorMatrix(Vector3 inertiaTensor, Quaternion inertiaTensorRotation, 
            Quaternion inertialAxisRotation)
        {
            
            Matrix3x3 lamdaMatrix = new Matrix3x3(new[] {
                inertiaTensor[0],
                inertiaTensor[1],
                inertiaTensor[2] });
            
            Matrix3x3 qMatrix = Matrix3x3.Quaternion2Matrix(inertiaTensorRotation * Quaternion.Inverse(inertialAxisRotation));

            Matrix3x3 qMatrixTransposed = qMatrix.Transpose();

            Matrix3x3 inertiaMatrix = qMatrix * lamdaMatrix * qMatrixTransposed;
            return inertiaMatrix;
        }
        
        private static UrdfLinkDescription.Inertial.Inertia ToInertia(Matrix3x3 matrix)
        {
            return new UrdfLinkDescription.Inertial.Inertia(matrix[0][0], matrix[0][1], matrix[0][2],
                matrix[1][1], matrix[1][2],
                matrix[2][2]);
        }

        private static UrdfLinkDescription.Inertial.Inertia ToRosCoordinates(UrdfLinkDescription.Inertial.Inertia unityInertia)
        {
            return new UrdfLinkDescription.Inertial.Inertia(0, 0, 0, 0, 0, 0)
            {
                ixx = unityInertia.izz,
                iyy = unityInertia.ixx,
                izz = unityInertia.iyy,

                ixy = -unityInertia.ixz,
                ixz = unityInertia.iyz,
                iyz = -unityInertia.ixy
            };
        }
#endregion
    }
}

