/*
© Siemens AG, 2017
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

using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfJointDescription
    {
        public string name;
        public string type;
        public UrdfOriginDescription origin;
        public string parent;
        public string child;
        public Axis axis;
        public Calibration calibration;
        public Dynamics dynamics;
        public Limit limit;
        public Mimic mimic;
        public SafetyController safetyController;

        public UrdfLinkDescription ChildLink;

        public UrdfJointDescription(XElement node)
        {
            name = (string)node.Attribute("name"); // required
            type = (string)node.Attribute("type"); // required
            origin = (node.Element("origin") != null) ? new UrdfOriginDescription(node.Element("origin")) : null; // optional  
            parent = (string)node.Element("parent").Attribute("link"); // required
            child = (string)node.Element("child").Attribute("link"); // required
            axis = (node.Element("axis") != null) ? new Axis(node.Element("axis")) : Axis.DefaultAxis;  // optional 
            calibration = (node.Element("calibration") != null) ? new Calibration(node.Element("calibration")) : null;  // optional 
            dynamics = (node.Element("dynamics") != null) ? new Dynamics(node.Element("dynamics")) : Dynamics.DefaultDynamics;  // optional 
            limit = (node.Element("limit") != null) ? new Limit(node.Element("limit")) : Limit.DefaultLimit;  // required only for revolute and prismatic joints
            mimic = (node.Element("mimic") != null) ? new Mimic(node.Element("mimic")) : null;  // optional
            safetyController = (node.Element("safety_controller") != null) ? new SafetyController(node.Element("safety_controller")) : null;  // optional
        }

        public UrdfJointDescription(string name, string type, string parent, string child,
            UrdfOriginDescription origin = null, Axis axis = null, Calibration calibration = null,
            Dynamics dynamics = null, Limit limit = null, Mimic mimic = null, SafetyController safetyController = null)
        {
            this.name = name;
            this.type = type;
            this.parent = parent;
            this.child = child;
            this.origin = origin;
            this.axis = axis;
            this.calibration = calibration;
            this.dynamics = dynamics;
            this.limit = limit;
            this.mimic = mimic;
            this.safetyController = safetyController;
        }

        public void WriteToUrdf(XmlWriter writer)
        {
            writer.WriteStartElement("joint");

            writer.WriteAttributeString("name", name);
            writer.WriteAttributeString("type", type);

            origin?.WriteToUrdf(writer);

            writer.WriteStartElement("parent");
            writer.WriteAttributeString("link", parent);
            writer.WriteEndElement();

            writer.WriteStartElement("child");
            writer.WriteAttributeString("link", child);
            writer.WriteEndElement();

            axis?.WriteToUrdf(writer);
            calibration?.WriteToUrdf(writer);
            dynamics?.WriteToUrdf(writer);
            limit?.WriteToUrdf(writer);
            mimic?.WriteToUrdf(writer);
            safetyController?.WriteToUrdf(writer);

            writer.WriteEndElement();
        }

        public class Axis
        {

            public Vector3 axisROS;
            
            const string _AtrributeName = "xyz";

            private static Vector3 GetDefaultAxisValues()
            {
                return new Vector3(1, 0, 0);
            }

            private void SetAxisRos(Vector3 newAxisRos)
            {
                if (newAxisRos.sqrMagnitude < 0.001f)
                {
                    RuntimeUrdf.AddImportWarning($"Axis attribute '{_AtrributeName}' magnitude should be non zero! Found: {newAxisRos}, using default axis.");
                    axisROS = GetDefaultAxisValues();
                    return;
                }
                axisROS = newAxisRos.normalized;
            }

            public static Axis DefaultAxis => new Axis(GetDefaultAxisValues());

            public Axis(XElement node)
            {
                
                XAttribute xyzAttribute = node.Attribute(_AtrributeName);
                if (xyzAttribute == null)
                {
                    RuntimeUrdf.AddImportWarning($"Axis missing attribute '{_AtrributeName}', using default axis.");
                    axisROS = GetDefaultAxisValues();
                    return;
                }
                double[] xyz = xyzAttribute.ReadDoubleArray();
                if (xyz.Length != 3)
                {
                    RuntimeUrdf.AddImportWarning($"Axis attribute '{_AtrributeName}' should have exactly 3 values, but has {xyz.Length}, using default axis.");
                    axisROS = GetDefaultAxisValues();
                    return;
                }
                SetAxisRos(new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]));
            }

            public Axis(Vector3 axisRosEnu)
            {
                SetAxisRos(axisRosEnu);
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                if (!(axisROS.sqrMagnitude > 0.001f))
                {
                    double[] xyz = { axisROS.x, axisROS.y, axisROS.z };
                    writer.WriteStartElement("axis");
                    writer.WriteAttributeString("xyz", xyz.DoubleArrayToString());
                    writer.WriteEndElement();
                }
            }

            public Vector3 AxisUnity => axisROS.Ros2Unity();

            public Vector3 SecondaryAxisEstimateUnity
            {
                get
                {
                    //We know the dot product of 2 vectors is 0 when they are perpendicular.
                    Vector3 axis = AxisUnity;
                    float absAxisX = Mathf.Abs(axis.x);
                    float absAxisY = Mathf.Abs(axis.y);
                    float absAxisZ = Mathf.Abs(axis.z);

                    //Dot(axis, secondaryAxis) = 0
                    //axis.x * secondaryAxis.x + axis.y * secondaryAxis.y + axis.z * secondaryAxis.z = 0
                    Vector3 secondaryAxis = new Vector3(0, 1, 0); //Set a default value.

                    if (absAxisX > absAxisY && absAxisX > absAxisZ)
                    {
                        //use axisX
                        if (absAxisX > 0.0001)
                        {
                            //Let secondaryAxis.y = 1, secondaryAxis.z = 1
                            //axis.x * secondaryAxis.x + axis.y * 1 + axis.z * 1 = 0
                            //axis.x * secondaryAxis.x + axis.y + axis.z = 0
                            //axis.x * secondaryAxis.x = -axis.y - axis.z
                            //secondaryAxis.x = (- axis.y - axis.z) / axis.x
                            float secondaryAxisX = (-axis.y - axis.z) / axis.x;
                            secondaryAxis = new Vector3(secondaryAxisX, 1, 1).normalized;
                        }
                    } else if (absAxisY > absAxisZ)
                    {
                       //use axisY
                       if (absAxisY > 0.0001)
                       {
                           //Let secondaryAxis.x = 1, secondaryAxis.z = 1
                           //axis.x * 1 + axis.y * secondaryAxis.y + axis.z * 1 = 0
                           //axis.y * secondaryAxis.y + axis.x + axis.z = 0
                           //axis.y * secondaryAxis.y = -axis.x - axis.z
                           //secondaryAxis.y = (- axis.x - axis.z) / axis.y
                           float secondaryAxisY = (-axis.x - axis.z) / axis.y;
                           secondaryAxis = new Vector3(1, secondaryAxisY, 1).normalized;
                       }
                    }
                    else
                    {
                        //Use axisZ
                        if (absAxisZ > 0.0001)
                        {
                            //Let secondaryAxis.x = 1, secondaryAxis.y = 1
                            //axis.x * 1 + axis.y * 1 + axis.z * secondaryAxis.z = 0
                            //axis.z * secondaryAxis.z + axis.x + axis.y = 0
                            //axis.z * secondaryAxis.z = -axis.x - axis.y
                            //secondaryAxis.z = (- axis.x - axis.y) / axis.z
                            float secondaryAxisZ = (-axis.x - axis.y) / axis.z;
                            secondaryAxis = new Vector3(1, 1, secondaryAxisZ).normalized;
                        }
                    }
                    return secondaryAxis;
                }
            }
        }

        public class Calibration
        {
            public double rising;
            public double falling;

            public Calibration(XElement node)
            {
                rising = node.Attribute("rising").ReadOptionalDouble();  // optional
                falling = node.Attribute("falling").ReadOptionalDouble();  // optional
            }

            public Calibration(double rising = 0, double falling = 0)
            {
                this.rising = rising;
                this.falling = falling;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("calibration");

                //TODO only output one or the other
                writer.WriteAttributeString("rising", rising + "");
                writer.WriteAttributeString("falling", falling + "");

                writer.WriteEndElement();
            }
        }

        public class Dynamics
        {
            public double spring = 1000.0;
            public double damping = 10.0;
            public double friction = 0.0;

            public Dynamics(XElement node)
            {
                spring = node.Attribute("spring").ReadOptionalDouble(spring); // optional
                damping = node.Attribute("damping").ReadOptionalDouble(damping); // optional
                friction = node.Attribute("friction").ReadOptionalDouble(friction); // optional
            }
            
            private Dynamics()
            {
            }

            public static Dynamics DefaultDynamics => new Dynamics();

            public Dynamics(double spring, double damping, double friction)
            {
                this.spring = spring;
                this.damping = damping;
                this.friction = friction;
            }

            public void WriteToUrdf(XmlWriter writer)
            {

                writer.WriteStartElement("dynamics");

                if (spring != 0)
                {
                    writer.WriteAttributeString("spring", spring + "");
                }
                if (damping != 0)
                {
                    writer.WriteAttributeString("damping", damping + "");
                }
                if (friction != 0)
                {
                    writer.WriteAttributeString("friction", friction + "");
                }

                writer.WriteEndElement();
            }
        }

        public class Limit
        {
            public double lowerRadians;
            public double upperRadians;
            public double effort;
            public double velocity;

            private const string _LowerAttributeName = "lower";
            private const string _UpperAttributeName = "upper";

            public static Limit DefaultLimit => new Limit(0f, 0f, float.PositiveInfinity, 0f);

            public Limit(XElement node)
            {
                lowerRadians = node.Attribute(_LowerAttributeName).ReadOptionalDouble(); // optional
                upperRadians = node.Attribute(_UpperAttributeName).ReadOptionalDouble(); // optional
                effort = (double)node.Attribute("effort"); // required
                velocity = (double)node.Attribute("velocity"); // required
            }
            
            public Limit(double lowerRadians, double upperRadians, double effort, double velocity)
            {
                this.lowerRadians = lowerRadians;
                this.upperRadians = upperRadians;
                this.effort = effort;
                this.velocity = velocity;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("limit");

                writer.WriteAttributeString("lower", lowerRadians + "");
                writer.WriteAttributeString("upper", upperRadians + "");

                writer.WriteAttributeString("effort", effort + "");
                writer.WriteAttributeString("velocity", velocity + "");

                writer.WriteEndElement();
            }
        }

        public class Mimic
        {
            public string joint;
            public double multiplier;
            public double offset;

            public Mimic(XElement node)
            {
                joint = (string)node.Attribute("joint"); // required
                multiplier = node.Attribute("multiplier").ReadOptionalDouble(); // optional
                offset = node.Attribute("offset").ReadOptionalDouble(); // optional   
            }

            public Mimic(string joint, double multiplier = 0, double offset = 0)
            {
                this.joint = joint;
                this.multiplier = multiplier;
                this.offset = offset;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                if (multiplier == 1 && offset == 0)
                    return;

                writer.WriteStartElement("mimic");

                writer.WriteAttributeString("joint", joint);
                if (multiplier != 1)
                {
                    writer.WriteAttributeString("multiplier", multiplier + "");
                }
                if (offset != 0)
                {
                    writer.WriteAttributeString("offset", offset + "");
                }

                writer.WriteEndElement();
            }
        }

        public class SafetyController
        {
            public double softLowerLimit;
            public double softUpperLimit;
            public double kPosition;
            public double kVelocity;

            public SafetyController(XElement node)
            {
                softLowerLimit = node.Attribute("soft_lower_limit").ReadOptionalDouble(); // optional
                softUpperLimit = node.Attribute("soft_upper_limit").ReadOptionalDouble(); // optional
                kPosition = node.Attribute("k_position").ReadOptionalDouble(); // optional
                kVelocity = node.Attribute("k_velocity").ReadOptionalDouble(); // required   
            }

            public SafetyController(double softLowerLimit, double softUpperLimit, double kPosition, double kVelocity)
            {
                this.softLowerLimit = softLowerLimit;
                this.softUpperLimit = softUpperLimit;
                this.kPosition = kPosition;
                this.kVelocity = kVelocity;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("safetyController");

                if (softLowerLimit != 0)
                {
                    writer.WriteAttributeString("soft_lower_limit", softLowerLimit + "");
                }
                if (softUpperLimit != 0)
                {
                    writer.WriteAttributeString("soft_upper_limit", softUpperLimit + "");
                }
                if (kPosition != 0)
                {
                    writer.WriteAttributeString("k_position", kPosition + "");
                }
                writer.WriteAttributeString("k_velocity", kVelocity + "");

                writer.WriteEndElement();
            }
        }
    }

}
