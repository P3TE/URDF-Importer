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
            dynamics = (node.Element("dynamics") != null) ? new Dynamics(node.Element("dynamics")) : null;  // optional 
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
            public double[] xyz;

            public static Axis DefaultAxis => new Axis(new double[] {1, 0, 0});

            public Axis(XElement node)
            {
                xyz = node.Attribute("xyz") != null ? node.Attribute("xyz").ReadDoubleArray() : null;
            }

            public Axis(double[] xyz)
            {
                this.xyz = xyz;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                if (!(xyz[0] == 0 && xyz[1] == 0 && xyz[2] == 0))
                {
                    writer.WriteStartElement("axis");
                    writer.WriteAttributeString("xyz", xyz.DoubleArrayToString());
                    writer.WriteEndElement();
                }
            }

            public int AxisofMotion()
            {
                for (int i = 0; i < 3; i++)
                {
                    if (xyz[i] > 0)
                    {
                        return i;
                    }
                }
                return -1;
            }
            
            public Vector3 AxisROS => new Vector3((float) xyz[0], (float) xyz[1], (float) xyz[2]);

            public Vector3 AxisUnity => AxisROS.Ros2Unity();

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

                    if (absAxisX > absAxisY && absAxisX < absAxisZ)
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
                            secondaryAxis = new Vector3(secondaryAxisX, 1, 1);
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
                           secondaryAxis = new Vector3(1, secondaryAxisY, 1);
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
                            secondaryAxis = new Vector3(1, 1, secondaryAxisZ);
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
            public double damping;
            public double friction;

            public Dynamics(XElement node)
            {
                damping = node.Attribute("damping").ReadOptionalDouble(); // optional
                friction = node.Attribute("friction").ReadOptionalDouble(); // optional
            }

            public Dynamics(double damping, double friction)
            {
                this.damping = damping;
                this.friction = friction;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                if (damping == 0 & friction == 0)
                    return;

                writer.WriteStartElement("dynamics");

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
            public double lower;
            public double upper;
            public double effort;
            public double velocity;

            private const string _LowerAttributeName = "lower";
            private const string _UpperAttributeName = "upper";

            public static Limit DefaultLimit => new Limit(0f, 0f, float.PositiveInfinity, 0f);

            public Limit(XElement node)
            {
                lower = node.Attribute(_LowerAttributeName).ReadOptionalDouble(); // optional
                upper = node.Attribute(_UpperAttributeName).ReadOptionalDouble(); // optional
                effort = (double)node.Attribute("effort"); // required
                velocity = (double)node.Attribute("velocity"); // required
            }
            
            public Limit(double lower, double upper, double effort, double velocity)
            {
                this.lower = lower;
                this.upper = upper;
                this.effort = effort;
                this.velocity = velocity;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("limit");

                writer.WriteAttributeString("lower", lower + "");
                writer.WriteAttributeString("upper", upper + "");

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
