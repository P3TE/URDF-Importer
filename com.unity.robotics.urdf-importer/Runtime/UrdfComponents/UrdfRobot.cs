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
using System;
using System.Collections.Generic;

namespace Unity.Robotics.UrdfImporter
{
    public enum GeometryTypes { Box, Cylinder, Sphere, Mesh }

    public enum UrdfCoordinateSpace
    {
        ENUFLU = 0, NEDFRD = 1
    }

    public class UrdfRobot : MonoBehaviour
    {
        [SerializeField] private string robotNamespace;
        [SerializeField] private string robotName;
        
        public string FilePath;
        public ImportSettings.axisType choosenAxis ;
        [SerializeField]
        private ImportSettings.axisType currentOrientation = ImportSettings.axisType.yAxis;
        public List<CollisionIgnore> collisionExceptions;

        [SerializeField] public string exportPackageDirectory = "";
        [SerializeField] public string exportPackageName = "";
        [SerializeField] public bool exportPlugins = true;

        [SerializeField] public UrdfCoordinateSpace coordinateSpace = UrdfCoordinateSpace.ENUFLU;
        
        [SerializeField] private int m_LayerIndex = 0;
        private int previousLayer = 0;
        
        private bool startCalled = false;

        //Current Settings
        public static bool collidersConvex = true;
        public static bool useUrdfInertiaData = false;
        public static bool useGravity = true;
        public static bool addController = true;
        public static bool addFkRobot = true;
        public static bool changetoCorrectedSpace = false;

        public string RobotNamespace => robotNamespace;
        
        public string RobotName => robotName;
        
        public string ModelName => gameObject.name;

        public void SetRobotNamespace(string robotNamespace)
        {
            this.robotNamespace = robotNamespace.EndsWith('/')
                ? robotNamespace.Substring(0, robotNamespace.Length - 1)
                : robotNamespace;
        }
        
        public void SetRobotName(string robotName)
        {
            this.robotName = robotName;
        }
        
        public void SetLayer(int layerIndex)
        {
            this.m_LayerIndex = layerIndex;
            if (startCalled)
            {
                UpdateLayerForAllTransforms();
            }
        }

        #region Configure Robot

        public void SetCollidersConvex()
        {
            foreach (MeshCollider meshCollider in GetComponentsInChildren<MeshCollider>())
                meshCollider.convex = !collidersConvex;
            collidersConvex = !collidersConvex;
        }


        public void SetUseUrdfInertiaData()
        {
            foreach (UrdfInertial urdfInertial in GetComponentsInChildren<UrdfInertial>())
                urdfInertial.useUrdfData = !useUrdfInertiaData;
            useUrdfInertiaData = !useUrdfInertiaData;
        }

        public void SetRigidbodiesUseGravity()
        {
            foreach (ArticulationBody ar in GetComponentsInChildren<ArticulationBody>())
                ar.useGravity = !useGravity;
            useGravity = !useGravity;
        }

        public void GenerateUniqueJointNames()
        {
            foreach (UrdfJoint urdfJoint in GetComponentsInChildren<UrdfJoint>())
                urdfJoint.GenerateUniqueJointName();
        }

        // Add a rotation in the model which gives the correct correspondence between UnitySpace and RosSpace
        public void ChangeToCorrectedSpace()
        {
            //this.transform.Rotate(0, 180, 0);
            changetoCorrectedSpace = !changetoCorrectedSpace;
        }

        public bool CheckOrientation()
        {
            return currentOrientation == choosenAxis;
        }

        public void SetOrientation()
        {
            currentOrientation = choosenAxis;
        }

        public void AddController()
        {
            if (!addController && this.gameObject.GetComponent< Unity.Robotics.UrdfImporter.Control.Controller>() == null)
            {
                this.gameObject.AddComponent<Unity.Robotics.UrdfImporter.Control.Controller>();
            }
            else
            {
                DestroyImmediate(this.gameObject.GetComponent<Unity.Robotics.UrdfImporter.Control.Controller>());
                DestroyImmediate(this.gameObject.GetComponent<Unity.Robotics.UrdfImporter.Control.FKRobot>());
                JointControl[] scriptList = GetComponentsInChildren<JointControl>();
                foreach (JointControl script in scriptList)
                    DestroyImmediate(script);
            }
            addController = !addController;
        }

        public void AddFkRobot()
        {
            if (!addFkRobot && this.gameObject.GetComponent<Unity.Robotics.UrdfImporter.Control.FKRobot>() == null)
            {
                this.gameObject.AddComponent<Unity.Robotics.UrdfImporter.Control.FKRobot>();
            }
            else
            {
                DestroyImmediate(this.gameObject.GetComponent<Unity.Robotics.UrdfImporter.Control.FKRobot>());
            }
            addFkRobot = !addFkRobot;
        }

        public void SetAxis(ImportSettings.axisType setAxis)
        {
            this.choosenAxis = setAxis;
        }

        void Start()
        {
            startCalled = true;
            CreateCollisionExceptions();
            UpdateLayerForAllTransforms();
        }

        public void CreateCollisionExceptions()
        {
            if (collisionExceptions != null)
            {
                foreach (CollisionIgnore ignoreCollision in collisionExceptions)
                {
                    Collider[] collidersObject1 = ignoreCollision.Link1.GetComponentsInChildren<Collider>();
                    Collider[] collidersObject2 = ignoreCollision.Link2.GetComponentsInChildren<Collider>();
                    foreach (Collider colliderMesh1 in collidersObject1)
                    {
                        foreach (Collider colliderMesh2 in collidersObject2)
                        {
                            Physics.IgnoreCollision(colliderMesh1, colliderMesh2);
                        }
                    }
                }
            }
        }

        private void UpdateLayerForAllTransforms()
        {
            if (m_LayerIndex == previousLayer)
            {
                return;
            }

            LinkedList<Transform> transformQueue = new LinkedList<Transform>();
            transformQueue.AddLast(transform);
            while (transformQueue.Count > 0)
            {
                Transform current = transformQueue.First.Value;
                transformQueue.RemoveFirst();

                if (current.gameObject.layer == previousLayer)
                {
                    current.gameObject.layer = m_LayerIndex;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    transformQueue.AddLast(current.GetChild(i));
                }

            }

            previousLayer = m_LayerIndex;
        }
        
        #endregion

        public UrdfLink FindBaseLink()
        {
            //Go for a breadth first search.
            LinkedList<Transform> queue = new LinkedList<Transform>();
            queue.AddLast(transform);

            while (queue.Count > 0)
            {
                Transform current = queue.First.Value;
                queue.RemoveFirst();
                UrdfLink currentLink = current.GetComponent<UrdfLink>();
                if (currentLink != null)
                {
                    if (currentLink.IsBaseLink)
                    {
                        return currentLink;
                    }
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    queue.AddLast(current.GetChild(i));
                }
            }
            
            //Couldn't find the base_link!
            return null;
        }

    }
}