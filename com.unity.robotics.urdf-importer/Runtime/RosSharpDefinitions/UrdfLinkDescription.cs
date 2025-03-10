﻿/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Unity.Robotics.UrdfImporter.Urdf.Extensions;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfLinkDescription
    {
        public string name;
        public Inertial inertial;
        public List<Visual> visuals;
        public List<Collision> collisions;
        public List<UrdfJointDescription> joints;

        public UrdfLinkDescription(XElement node)
        {
            name = (string)node.Attribute("name");  // required
            inertial = (node.Element("inertial") != null) ? new Inertial(node.Element("inertial")) : null;  // optional     
            visuals = readVisuals(node); // multiple
            collisions = readCollisions(node); // optional   
        }

        public UrdfLinkDescription(string name, Inertial inertial = null)
        {
            this.name = name;
            this.inertial = inertial;

            visuals = new List<Visual>();
            collisions = new List<Collision>();
            joints = new List<UrdfJointDescription>();
        }

        public void WriteToUrdf(XmlWriter writer)
        {
            writer.WriteStartElement("link");
            writer.WriteAttributeString("name", name);

            inertial?.WriteToUrdf(writer);

            foreach (var visual in visuals)
                visual.WriteToUrdf(writer);
            foreach (var collision in collisions)
                collision.WriteToUrdf(writer);

            writer.WriteEndElement();
        }

        private static List<Collision> readCollisions(XElement node)
        {
            var collisions =
                from child in node.Elements("collision")
                select new Collision(child);
            return collisions.ToList();

        }
        private static List<Visual> readVisuals(XElement node)
        {
            var visuals =
                from child in node.Elements("visual")
                select new Visual(child);
            return visuals.ToList();
        }

        [System.Serializable]
        public class Inertial
        {
            public double mass;
            public UrdfOriginDescription origin;
            public Inertia inertia;
            
            public enum InertiaCalculationType
            {
                Inherit_Fallback_Manual = 0, //The default.
                Inherit_Fallback_Automatic,
                Force_Manual,
                Force_Automatic
            }

            public Inertial(XElement node)
            {
                origin = (node.Element("origin") != null) ? new UrdfOriginDescription(node.Element("origin")) : null; // optional  
                mass = (double)node.Element("mass").Attribute("value");// required
                inertia = new Inertia(node.Element("inertia")); // required
            }

            public Inertial(double mass, UrdfOriginDescription origin, Inertia inertia)
            {
                this.mass = mass;
                this.origin = origin;
                this.inertia = inertia;
            }

            public Inertial(Inertial other)
            {
                mass = other.mass;
                origin = other.origin;
                inertia = other.inertia;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("inertial");

                origin.WriteToUrdf(writer);

                writer.WriteStartElement("mass");
                writer.WriteAttributeString("value", mass + "");
                writer.WriteEndElement();

                inertia.WriteToUrdf(writer);

                writer.WriteEndElement();
            }

            [System.Serializable]
            public class Inertia
            {

                private static Dictionary<string, InertiaCalculationType> StringToInertiaCalculationTypeMap =
                    new Dictionary<string, InertiaCalculationType>()
                    {
                        {"force_automatic", InertiaCalculationType.Force_Automatic},
                        {"force_manual", InertiaCalculationType.Force_Manual},
                        {"inherit_fallback_automatic", InertiaCalculationType.Inherit_Fallback_Automatic},
                        {"inherit_fallback_manual", InertiaCalculationType.Inherit_Fallback_Manual}
                    };


                private const string _InertiaCalculationTypeId = "unity_automatic_inertia";
                public InertiaCalculationType inertiaCalculationType = InertiaCalculationType.Inherit_Fallback_Manual;

                public bool automaticInertiaDefined; //Used for warning messages.
                
                public double ixx;
                public double ixy;
                public double ixz;
                public double iyy;
                public double iyz;
                public double izz;
                
                public string StringNameFromInertiaCalculationType(InertiaCalculationType inertiaCalculationType)
                {
                    foreach (KeyValuePair<string, InertiaCalculationType> keyValuePair in StringToInertiaCalculationTypeMap)
                    {
                        if (keyValuePair.Value == inertiaCalculationType)
                        {
                            return keyValuePair.Key;
                        }
                    }

                    throw new Exception($"Unmapped InertiaCalculationType: {inertiaCalculationType}");
                }

                public InertiaCalculationType InertiaCalculationTypeFromString(string value)
                {
                    if (StringToInertiaCalculationTypeMap.TryGetValue(value, out InertiaCalculationType result))
                    {
                        return result;
                    }

                    StringBuilder errorMessage = new StringBuilder();
                    
                    errorMessage.Append($"Unknown value for '{_InertiaCalculationTypeId}'! Provided: '{value}'");
                    int count = 0;
                    foreach (KeyValuePair<string, InertiaCalculationType> keyValuePair in StringToInertiaCalculationTypeMap)
                    {
                        if (count == 0)
                        {
                            errorMessage.Append(", available values include: ");
                        }
                        else
                        {
                            errorMessage.Append(", ");
                        }
                        count++;
                        errorMessage.Append("'");
                        errorMessage.Append(keyValuePair.Key);
                        errorMessage.Append("'");
                    }
                    throw new Exception(errorMessage.ToString());
                }

                public Inertia(XElement node)
                {
                    XAttribute automaticInertiaAttribute = node.Attribute(_InertiaCalculationTypeId);
                    automaticInertiaDefined = automaticInertiaAttribute != null;
                    if (automaticInertiaDefined)
                    {
                        inertiaCalculationType = InertiaCalculationTypeFromString(automaticInertiaAttribute.Value);
                    }
                    else
                    {
                        StringBuilder warningBuilder = new StringBuilder();
                        warningBuilder.Append("In the <inertial> within ");
                        warningBuilder.Append(UrdfPluginImplementation.GetVerboseXElementName(node.Parent.Parent));
                        warningBuilder.Append(" inertial was defined without specifying the ");
                        warningBuilder.Append(_InertiaCalculationTypeId);
                        warningBuilder.Append("' behaviour, it is recommended that this is set. For automatic '");
                        warningBuilder.Append(StringNameFromInertiaCalculationType(InertiaCalculationType.Inherit_Fallback_Automatic));
                        warningBuilder.Append("' is recommended, if the inertia is well characterised '");
                        warningBuilder.Append(StringNameFromInertiaCalculationType(InertiaCalculationType.Inherit_Fallback_Manual));
                        warningBuilder.Append("' is recommended. To force a inertia mode, use '");
                        warningBuilder.Append(StringNameFromInertiaCalculationType(InertiaCalculationType.Force_Automatic));
                        warningBuilder.Append("' or '");
                        warningBuilder.Append(StringNameFromInertiaCalculationType(InertiaCalculationType.Force_Manual));
                        warningBuilder.Append("'");
                        RuntimeUrdf.AddImportWarning(warningBuilder.ToString());
                    }
                    ixx = (double)node.Attribute("ixx");
                    ixy = (double)node.Attribute("ixy");
                    ixz = (double)node.Attribute("ixz");
                    iyy = (double)node.Attribute("iyy");
                    iyz = (double)node.Attribute("iyz");
                    izz = (double)node.Attribute("izz");
                }

                public Inertia(double ixx, double ixy, double ixz, double iyy, double iyz, double izz)
                {
                    this.ixx = ixx;
                    this.ixy = ixy;
                    this.ixz = ixz;
                    this.iyy = iyy;
                    this.iyz = iyz;
                    this.izz = izz;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("inertia");

                    foreach (KeyValuePair<string,InertiaCalculationType> keyValuePair in StringToInertiaCalculationTypeMap)
                    {
                        if (keyValuePair.Value == inertiaCalculationType)
                        {
                            writer.WriteAttributeString(_InertiaCalculationTypeId, keyValuePair.Key);
                        }
                    }
                    
                    writer.WriteAttributeString("ixx", ixx + "");
                    writer.WriteAttributeString("ixy", ixy + "");
                    writer.WriteAttributeString("ixz", ixz + "");
                    writer.WriteAttributeString("iyy", iyy + "");
                    writer.WriteAttributeString("iyz", iyz + "");
                    writer.WriteAttributeString("izz", izz + "");
                    writer.WriteEndElement();
                }

            }
        }

        public class Collision
        {
            public string name;
            public UrdfOriginDescription origin;
            public Geometry geometry;

            public Collision(XElement node)
            {
                name = (string)node.Attribute("name"); // optional
                origin = (node.Element("origin") != null) ? new UrdfOriginDescription(node.Element("origin")) : null; // optional  
                geometry = new Geometry(node.Element("geometry")); // required
            }

            public Collision(Geometry geometry, string name = null, UrdfOriginDescription origin = null)
            {
                this.name = name;
                this.origin = origin;
                this.geometry = geometry;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("collision");

                if (name != null)
                    writer.WriteAttributeString("name", name);

                origin?.WriteToUrdf(writer);
                geometry?.WriteToUrdf(writer);

                writer.WriteEndElement();

            }
        }
        public class Visual
        {
            public string name;
            public UrdfOriginDescription origin;
            public Geometry geometry;
            public List<UrdfMaterialDescription> materials;
            public List<UrdfUnityMaterial.ExportMaterial> exportedMaterials;

            public Visual(XElement node)
            {
                name = (string)node.Attribute("name"); // optional
                origin = (node.Element("origin") != null) ? new UrdfOriginDescription(node.Element("origin")) : null; // optional
                geometry = new Geometry(node.Element("geometry")); // required
                materials = new List<UrdfMaterialDescription>();
                IEnumerable<XElement> materialElements = node.Elements("material");
                foreach (XElement materialElement in materialElements)
                {
                    materials.Add(new UrdfMaterialDescription(materialElement));
                }
            }

            public Visual(Geometry geometry, string name = null, UrdfOriginDescription origin = null, UrdfMaterialDescription material = null,
                List<UrdfUnityMaterial.ExportMaterial> exportedMaterials = null)
            {
                this.name = name;
                this.origin = origin;
                this.geometry = geometry;
                this.materials = new List<UrdfMaterialDescription>() {material}; //TODO - Add support for multiple materials.
                this.exportedMaterials = exportedMaterials;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("visual");

                if (name != null)
                    writer.WriteAttributeString("name", name);

                origin?.WriteToUrdf(writer);
                geometry?.WriteToUrdf(writer);
                foreach (UrdfMaterialDescription urdfMaterialDescription in materials)
                {
                    urdfMaterialDescription?.WriteToUrdf(writer);
                }
                if(exportedMaterials != null && exportedMaterials.Count > 0)
                {
                    foreach (UrdfUnityMaterial.ExportMaterial exportedMaterial in exportedMaterials)
                    {
                        exportedMaterial.WriteToUrdf(writer);
                    }
                }

                writer.WriteEndElement();
            }

            public class Material
            {
                public string name;
                public Color color;
                public Texture texture;

                public Material(XElement node)
                {
                    name = (string)node.Attribute("name"); // required
                    color = (node.Element("color") != null) ? new Color(node.Element("color")) : null; // optional  
                    texture = (node.Element("texture") != null) ? new Texture(node.Element("texture")) : null;
                }

                public Material(string name, Color color = null, Texture texture = null)
                {
                    this.name = name;
                    this.color = color;
                    this.texture = texture;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("material");
                    writer.WriteAttributeString("name", name);

                    color?.WriteToUrdf(writer);
                    texture?.WriteToUrdf(writer);

                    writer.WriteEndElement();
                }

                public class Texture
                {
                    public string filename;

                    public Texture(XElement node)
                    {
                        filename = (string)node.Attribute("filename"); // required
                    }

                    public Texture(string filename)
                    {
                        this.filename = filename;
                    }

                    public void WriteToUrdf(XmlWriter writer)
                    {
                        writer.WriteStartElement("texture");
                        writer.WriteAttributeString("filename", filename);
                        writer.WriteEndElement();
                    }
                }

                public class Color
                {
                    public double[] rgba;

                    public Color(XElement node)
                    {
                        rgba = node.Attribute("rgba").ReadDoubleArray(); // required
                    }

                    public Color(double[] rgba)
                    {
                        this.rgba = rgba;
                    }

                    public void WriteToUrdf(XmlWriter writer)
                    {
                        writer.WriteStartElement("color");
                        writer.WriteAttributeString("rgba", rgba.DoubleArrayToString());
                        writer.WriteEndElement();
                    }
                }

            }
        }

        public class Geometry
        {
            public Box box;
            public Cylinder cylinder;
            public Capsule capsule;
            public Sphere sphere;
            public Mesh mesh;

            public Geometry(XElement node)
            {
                box = (node.Element("box") != null) ? new Box(node.Element("box")) : null; // optional  
                cylinder = (node.Element("cylinder") != null) ? new Cylinder(node.Element("cylinder")) : null; // optional  
                capsule = (node.Element("capsule") != null) ? new Capsule(node.Element("capsule")) : null; // optional
                sphere = (node.Element("sphere") != null) ? new Sphere(node.Element("sphere")) : null; // optional  
                mesh = (node.Element("mesh") != null) ? new Mesh(node.Element("mesh")) : null; // optional           
            }

            public Geometry(Box box = null, Cylinder cylinder = null, Capsule capsule = null, Sphere sphere = null, Mesh mesh = null)
            {
                this.box = box;
                this.cylinder = cylinder;
                this.capsule = capsule;
                this.sphere = sphere;
                this.mesh = mesh;
            }

            public void WriteToUrdf(XmlWriter writer)
            {
                writer.WriteStartElement("geometry");

                box?.WriteToUrdf(writer);
                cylinder?.WriteToUrdf(writer);
                capsule?.WriteToUrdf(writer);
                sphere?.WriteToUrdf(writer);
                mesh?.WriteToUrdf(writer);

                writer.WriteEndElement();
            }

            public class Box
            {
                public double[] size;

                public Box(XElement node)
                {
                    size = node.Attribute("size") != null ? node.Attribute("size").ReadDoubleArray() : null;
                }

                public Box(double[] size)
                {
                    this.size = size;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("box");
                    writer.WriteAttributeString("size", size.DoubleArrayToString());
                    writer.WriteEndElement();
                }
            }


            public class Cylinder
            {
                public double radius;
                public double length;

                public Cylinder(XElement node)
                {
                    radius = (double)node.Attribute("radius");
                    length = (double)node.Attribute("length");
                }

                public Cylinder(double radius, double length)
                {
                    this.radius = radius;
                    this.length = length;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("cylinder");
                    writer.WriteAttributeString("length", length + "");
                    writer.WriteAttributeString("radius", radius + "");
                    writer.WriteEndElement();
                }
            }


            public class Capsule
            {
                public double radius;
                public double length;

                public Capsule(XElement node)
                {
                    radius = (double)node.Attribute("radius");
                    length = (double)node.Attribute("length");
                }

                public Capsule(double radius, double length)
                {
                    this.radius = radius;
                    this.length = length;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("capsule");
                    writer.WriteAttributeString("length", length + "");
                    writer.WriteAttributeString("radius", radius + "");
                    writer.WriteEndElement();
                }
            }


            public class Sphere
            {
                public double radius;

                public Sphere(XElement node)
                {
                    radius = (double)node.Attribute("radius");
                }

                public Sphere(double radius)
                {
                    this.radius = radius;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("sphere");
                    writer.WriteAttributeString("radius", radius + "");
                    writer.WriteEndElement();
                }
            }

            public class Mesh
            {
                public string filename;
                public double[] scale;
                public bool colliderShouldBeConvex = true;

                public Mesh(XElement node)
                {
                    filename = (string)node.Attribute("filename");
                
                    XAttribute convexAttribute = node.Attribute("convex");
                    if (convexAttribute is not null) colliderShouldBeConvex = (bool)convexAttribute; // optional
                    
                    scale = node.Attribute("scale") != null ? node.Attribute("scale").ReadDoubleArray() : null;
                }

                public Mesh(string filename, double[] scale)
                {
                    this.filename = filename;
                    this.scale = scale;
                }

                public void WriteToUrdf(XmlWriter writer)
                {
                    writer.WriteStartElement("mesh");
                    writer.WriteAttributeString("filename", filename);

                    if (scale != null)
                        writer.WriteAttributeString("scale", scale.DoubleArrayToString());

                    writer.WriteEndElement();

                }
            }
        }
    }
    
    public static class InertiaCalculationTypeHelper
    {
        public static bool AutomaticInertiaCalculation(this UrdfLinkDescription.Inertial.InertiaCalculationType inertiaCalculationType)
        {
            switch (inertiaCalculationType)
            {
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Inherit_Fallback_Automatic:
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Force_Automatic:
                    return true;
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Inherit_Fallback_Manual:
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Force_Manual:
                    return false;
            }

            throw new Exception($"Unhandled InertiaCalculationType {inertiaCalculationType}");
        }
        
        public static bool CanInheritInertiaCalculation(this UrdfLinkDescription.Inertial.InertiaCalculationType inertiaCalculationType)
        {
            switch (inertiaCalculationType)
            {
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Inherit_Fallback_Automatic:
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Inherit_Fallback_Manual:
                    return true;
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Force_Automatic:
                case UrdfLinkDescription.Inertial.InertiaCalculationType.Force_Manual:
                    return false;
            }

            throw new Exception($"Unhandled InertiaCalculationType {inertiaCalculationType}");
        }
        
    }
}
