﻿/*
© Siemens AG, 2017-2019
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System;
using System.Text;


namespace Unity.Robotics.UrdfImporter
{
    public class Robot
    {
        public string filename;
        public string name;
        public Link root;
        public List<Link.Visual.Material> materials;

        public List<Link> links;
        public List<Joint> joints;
        public List<Plugin> plugins;
        public List<Tuple<string, string>> ignoreCollisionPair;

        public Robot(string filename)
        {
            
            this.filename = filename;
            
            XDocument xdoc = XDocument.Load(filename);
            XElement node = xdoc.Element("robot");

            name = node.Attribute("name").Value;
            materials = ReadMaterials(node);
            links = ReadLinks(node);
            joints = ReadJoints(node);
            plugins = ReadPlugins(node);
            ignoreCollisionPair = ReadDisableCollision(node);
            

            // build tree structure from link and joint lists:
            foreach (Link link in links)
                link.joints = joints.FindAll(v => v.parent == link.name);
            foreach (Joint joint in joints)
                joint.ChildLink = links.Find(v => v.name == joint.child);

            // save root node only:
            root = FindRootLink(links, joints);
            
        }

        public Robot(string filename, string name)
        {
            this.filename = filename;
            this.name = name;

            links = new List<Link>();
            joints = new List<Joint>();
            plugins = new List<Plugin>();
            materials = new List<Link.Visual.Material>();
        }

        private static List<Link.Visual.Material> ReadMaterials(XElement node)
        {
            var materials =
                from child in node.Elements("material")
                select new Link.Visual.Material(child);
            return materials.ToList();
        }

        private static List<Link> ReadLinks(XElement node)
        {
            List<String> importedLinks = new List<String>();
            List<Link> links = new List<Link>();
            foreach (XElement child in node.Elements("link"))
            {
                string name = (String)child.Attribute("name");
                if (importedLinks.Find(p => name == p ? true : false) == null)
                {
                    links.Add(new Link(child));
                    importedLinks.Add(name);
                }
                else
                    throw new InvalidNameException("Two links cannot have the same name");
            }
            return links;
        }

        private static List<Joint> ReadJoints(XElement node)
        {
            var joints =
                from child in node.Elements("joint")
                select new Joint(child);
            return joints.ToList();
        }

        private List<Plugin> ReadPlugins(XElement node)
        {
            var plugins =
                from child in node.Elements()
                where child.Name != "link" && child.Name != "joint" && child.Name != "material"
                select new Plugin(child.ToString());
            return plugins.ToList();
        }

        private List<Tuple<string,string>> ReadDisableCollision(XElement node)
        {
            var disable_collisions =
                from child in node.Elements("disable_collision")
                select new Tuple<string,string>(child.Attribute("link1").Value,child.Attribute("link2").Value);
            return disable_collisions.ToList();
        }

        private static Link FindRootLink(List<Link> links, List<Joint> joints)
        {
            if (joints.Count == 0)
                return links[0];

            Joint joint = joints[0];
            string parent;
            do
            {
                parent = joint.parent;
                joint = joints.Find(v => v.child == parent);
            }
            while (joint != null);
            return links.Find(v => v.name == parent);
        }

        public void WriteToUrdf()
        {
            // executing writeToUrdf() in separate thread to ensure CultureInfo does not change in main thread
            Thread thread = new Thread(delegate () { writeToUrdf(); });
            thread.Start();
            thread.Join();
        }
        
        public class UTF8StringWriter : StringWriter
        {
            public override Encoding Encoding
            {
                get
                {
                    return Encoding.UTF8;
                }
            }
        }
        
        private void writeToUrdf()
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, NewLineOnAttributes = false, Encoding = new UTF8Encoding(true) };

            //StringBuilder xmlBuilder = new StringBuilder();
            StringWriter stringWriter = new UTF8StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("robot");
                writer.WriteAttributeString("name", name);

                foreach (var material in materials)
                    material.WriteToUrdf(writer);
                foreach (var link in links)
                    link.WriteToUrdf(writer);
                foreach (var joint in joints)
                    joint.WriteToUrdf(writer);
                foreach (var plugin in plugins)
                    plugin.WriteToUrdf(writer);
                
                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Close();
            }
            
            File.WriteAllBytes(filename, settings.Encoding.GetBytes(stringWriter.ToString()));
        }
    }
    public class InvalidNameException : System.Exception
    {
        public InvalidNameException() : base() { }
        public InvalidNameException(string message) : base(message) { }
        public InvalidNameException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected InvalidNameException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
