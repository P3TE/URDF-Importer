﻿/*
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

using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Unity.Robotics.UrdfImporter.Control;
using Unity.Robotics.UrdfImporter.Urdf.Extensions;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfRobotExtensions
    {
        static string collisionObjectName = "Collisions";
        public static ImportSettings importsettings;
        
        public delegate void OnPostImport(GameObject importedRobotGameObject);

        public static OnPostImport postImportDelegate = null;

        public static void Create()
        {
            CreateTag();
            GameObject robotGameObject = new GameObject("Robot");

            SetTag(robotGameObject);
            robotGameObject.AddComponent<UrdfRobot>();
            robotGameObject.AddComponent<Unity.Robotics.UrdfImporter.Control.Controller>();

            UrdfPlugins.Create(robotGameObject.transform);

            UrdfLink urdfLink = UrdfLinkExtensions.Create(robotGameObject.transform).GetComponent<UrdfLink>();
            urdfLink.name = "base_link";
            urdfLink.IsBaseLink = true;
        }

        #region Import

        // Note: The Create() function is now broken into multiple pipeline stages to facilitate having
        // both a non-blocking coroutine Create function as well as a blocking synchronous one needed for stable runtime import.

        // Is used to pass data and parameters between stages of the import pipeline.
        private class ImportPipelineData
        {
            public ImportSettings settings;
            public bool wasRuntimeMode;
            public bool loadStatus;
            public bool forceRuntimeMode;
            public string robotNamespace;
            public string modelName;
            public UrdfRobotDescription robot;
            public UrdfPlugins urdfPlugins;
            public GameObject robotGameObject;
            public Stack<Tuple<UrdfLinkDescription, Transform, UrdfJointDescription>> importStack;
        }
        
        private static ImportPipelineData ImportPipelineInitFromFile(string filename, ImportSettings settings, bool loadStatus,
            bool forceRuntimeMode, string robotNamespace, string modelName)
        {
            XDocument xdoc = XDocument.Load(filename);
            ImportPipelineData importPipelineData = ImportPipelineInit(xdoc, settings, loadStatus, forceRuntimeMode, robotNamespace, modelName);
            importPipelineData.robot.filename = filename;
            return importPipelineData;
        }
        
        private static ImportPipelineData ImportPipelineInitFromString(string xmlText, ImportSettings settings, bool loadStatus,
            bool forceRuntimeMode, string robotNamespace, string modelName)
        {
            XDocument xdoc = XDocument.Parse(xmlText);
            ImportPipelineData importPipelineData = ImportPipelineInit(xdoc, settings, loadStatus, forceRuntimeMode, robotNamespace, modelName);
            return importPipelineData;
        }

        // Initializes import pipeline and reads the urdf file.
        private static ImportPipelineData ImportPipelineInit(XDocument xdoc, ImportSettings settings, bool loadStatus, bool forceRuntimeMode,
            string robotNamespace, string modelName)
        {
            ImportPipelineData im = new ImportPipelineData();
            im.settings = settings;
            im.loadStatus = loadStatus;
            im.wasRuntimeMode = RuntimeUrdf.IsRuntimeMode();
            im.forceRuntimeMode = forceRuntimeMode;
            im.robotNamespace = robotNamespace;
            im.modelName = modelName;

            if (forceRuntimeMode)
            {
                RuntimeUrdf.SetRuntimeMode(true);
            }
            
            RuntimeUrdf.ClearImportWarningsAndErrors();
            UrdfLinkExtensions.ClearLinkAndJointMaps();

            im.robot = new UrdfRobotDescription(xdoc);

            if (!UrdfAssetPathHandler.IsValidAssetPath(im.robot.filename))
            {
                Debug.LogError("URDF file and resources must be placed in project folder:" +
                    $"\n{Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length)}");
                if (forceRuntimeMode)
                { // set runtime mode back to what it was
                    RuntimeUrdf.SetRuntimeMode(im.wasRuntimeMode);
                }
                return null;
            }
            return im;
        }

        // Creates the robot game object.
        private static void ImportPipelineCreateObject(ImportPipelineData im)
        {
            string usedModelName = im.modelName;
            if (string.IsNullOrWhiteSpace(usedModelName))
            {
                usedModelName = im.robot.name;
            }
            im.robotGameObject = new GameObject(usedModelName);
           
            importsettings = im.settings;
            im.settings.totalLinks = im.robot.links.Count;

            CreateTag();
            SetTag(im.robotGameObject);

            UrdfRobot urdfRobot = im.robotGameObject.AddComponent<UrdfRobot>();
            urdfRobot.SetLayer(im.settings.defaultVehicleLayerIndex);
            if (string.IsNullOrWhiteSpace(im.robotNamespace))
            {
                throw new Exception($"Robot Namespace was not set!");
            }
            urdfRobot.SetRobotNamespace(im.robotNamespace);
            urdfRobot.SetRobotName(im.robot.name);

            im.robotGameObject.AddComponent<Unity.Robotics.UrdfImporter.Control.Controller>();
            if (RuntimeUrdf.IsRuntimeMode())
            {// In runtime mode, we have to disable controller while robot is being constructed.
                im.robotGameObject.GetComponent<Unity.Robotics.UrdfImporter.Control.Controller>().enabled = false;
            }

            im.robotGameObject.GetComponent<UrdfRobot>().SetAxis(im.settings.chosenAxis);

            if(!string.IsNullOrWhiteSpace(im.robot.filename))
            {
                string directoryName;
                try
                {
                    directoryName = Path.GetDirectoryName(im.robot.filename);
                }
                catch (Exception e)
                {
                    throw new Exception($"Unable to get directory from filename {im.robot.filename}", e);
                }
                UrdfAssetPathHandler.SetPackageRoot(directoryName);
            }
            UrdfMaterial.InitializeRobotMaterials(im.robot);
            im.urdfPlugins = UrdfPlugins.Create(im.robotGameObject.transform, im.robot.plugins);
        }

        // Creates the stack of robot joints. Should be called iteratively until false is returned.
        private static bool ProcessJointStack(ImportPipelineData im)
        {
            if (im.importStack == null)
            {
                im.importStack = new Stack<Tuple<UrdfLinkDescription, Transform, UrdfJointDescription>>();
                im.importStack.Push(new Tuple<UrdfLinkDescription, Transform, UrdfJointDescription>(im.robot.root, im.robotGameObject.transform, null));
            }

            if (im.importStack.Count != 0)
            {
                Tuple<UrdfLinkDescription, Transform, UrdfJointDescription> currentLink = im.importStack.Pop();
                if (currentLink.Item1 == null)
                {
                    if (currentLink.Item3 != null)
                    {
                        throw new Exception($"Failed to find link for joint: {currentLink.Item3.name}!");
                    }
                    throw new Exception($"Failed to find link for: {currentLink.Item2.name}!");
                }
                if (currentLink.Item2 == null)
                {
                    throw new Exception($"Failed to find transform for joint: {currentLink.Item3.name}!");
                }                
                GameObject importedLink = UrdfLinkExtensions.Create(currentLink.Item2, currentLink.Item1, currentLink.Item3);
                im.settings.linksLoaded++;
                foreach (UrdfJointDescription childJoint in currentLink.Item1.joints)
                {
                    UrdfLinkDescription child = childJoint.ChildLink;
                    im.importStack.Push(new Tuple<UrdfLinkDescription, Transform, UrdfJointDescription>(child, importedLink.transform, childJoint));
                }
                return true;
            }
            return false;
        }

        // Post creation stuff: add to parent, fix axis and add collision exceptions.
        private static void ImportPipelinePostCreate(ImportPipelineData im)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                GameObjectUtility.SetParentAndAlign(im.robotGameObject, Selection.activeObject as GameObject);
                Undo.RegisterCreatedObjectUndo(im.robotGameObject, "Create " + im.robotGameObject.name);
                Selection.activeObject = im.robotGameObject;
            }
#endif

            CorrectAxis(im.robotGameObject);
            CreateCollisionExceptions(im.robot, im.robotGameObject);

            //Add a verification check.
            IVerifiesValidPluginImport[] verification =
                im.robotGameObject.GetComponentsInChildren<IVerifiesValidPluginImport>();
            foreach (IVerifiesValidPluginImport verifiesValidPluginImport in verification)
            {
                try
                {
                    verifiesValidPluginImport.VerifyPluginImport();
                }
                catch (Exception e)
                {
                    RuntimeUrdf.urdfBuildErrors.AddLast(e);
                }
            }

            if (RuntimeUrdf.urdfBuildErrors.Count == 0)
            {
                //Only run this if there were no errors in the previous steps, it's best to fix things one at a time.
                try
                {
                    postImportDelegate?.Invoke(im.robotGameObject);
                }
                catch (Exception e)
                {
                    RuntimeUrdf.urdfBuildErrors.AddLast(e);
                }
            }

            if (im.forceRuntimeMode) 
            { // set runtime mode back to what it was
                RuntimeUrdf.SetRuntimeMode(im.wasRuntimeMode);
            }

            IEnumerator<string> urdfBuildWarnings = RuntimeUrdf.UrdfBuildWarnings;
            while (urdfBuildWarnings.MoveNext())
            {
                string warning = urdfBuildWarnings.Current;
                //TODO - Display warnings somehow during a build (IE not using Debug.LogWarning(...)).
            }

            foreach (Exception urdfBuildError in RuntimeUrdf.urdfBuildErrors)
            {
                Debug.LogException(urdfBuildError);
            }
        }

        /// <summary>
        /// Coroutine to create a Robot game object from the urdf file
        /// </summary>
        /// <param name="filename">URDF filename</param>
        /// <param name="settings">Import Settings</param>
        /// <param name="loadStatus">If true, will show the progress of import step by step</param>
        /// <param name="forceRuntimeMode">
        /// When true, runs the runtime loading mode even in Editor. When false, uses the default behavior,
        /// i.e. runtime will be enabled in standalone build and disable when running in editor.
        /// In runtime mode, the Controller component of the robot will be added but not activated automatically and has to be enabled manually.
        /// This is to allow initializing the controller values (stiffness, damping, etc.) before the controller.Start() is called
        /// </param>
        /// <returns></returns>
        public static IEnumerator<GameObject> Create(string filename, ImportSettings settings, bool loadStatus = false, bool forceRuntimeMode = false, 
            string robotNamespace = "", string modelName = "")
        {
            UrdfGeometryCollision.BeginNewUrdfImport();
            ImportPipelineData im = ImportPipelineInitFromFile(filename, settings, loadStatus, forceRuntimeMode, robotNamespace, modelName);
            if (im == null)
            {
                yield break;
            }

            ImportPipelineCreateObject(im);

            while (ProcessJointStack(im))
            {
                if (loadStatus)
                {
                    yield return null;
                }
            }

            ImportPipelinePostCreate(im);
            yield return im.robotGameObject;
        }

        /// <summary>
        /// Create a Robot gameobject from filename in runtime.
        /// It is a synchronous (blocking) function and only returns after the gameobject has been created.
        /// </summary>
        /// <param name="filename">URDF filename</param>
        /// <param name="settings">Import Settings</param>
        /// <returns> Robot game object</returns>
        public static GameObject CreateRuntimeFromFile(string filename, ImportSettings settings)
        {
            ImportPipelineData im = ImportPipelineInitFromFile(filename, settings, false, true, "", "");
            return CreateRuntime(im);
        }
        
        /// <summary>
        /// Create a Robot gameobject from filename in runtime.
        /// It is a synchronous (blocking) function and only returns after the gameobject has been created.
        /// </summary>
        /// <param name="xmlText">URDF in text form</param>
        /// <param name="settings">Import Settings</param>
        /// <returns> Robot game object</returns>
        public static GameObject CreateRuntimeFromString(string xmlText, ImportSettings settings, string robotNamespace, string modelName)
        {
            ImportPipelineData im = ImportPipelineInitFromString(xmlText, settings, false, true, robotNamespace, modelName);
            return CreateRuntime(im);
        }

        
        private static GameObject CreateRuntime(ImportPipelineData im)
        {
            if (im == null)
            {
                return null;
            }

            ImportPipelineCreateObject(im);

            while (ProcessJointStack(im))
            {// process the stack until finished.
            }
            
            ImportPipelineLoadPlugins(im);

            ImportPipelinePostCreate(im);

            return im.robotGameObject;
        }

        private static void ImportPipelineLoadPlugins(ImportPipelineData im)
        {

            PluginManagerBase pluginManager = PluginManagerBase.Instance;
            if (pluginManager == null)
            {
                RuntimeUrdf.urdfBuildErrors.AddLast(new Exception("No PluginManagerBase Instance set!"));
                return;
            }

            LinkedList<UrdfPluginImplementation> loadedPlugins = new LinkedList<UrdfPluginImplementation>();
            
            foreach (UrdfPluginDescription urdfPluginDescription in im.robot.plugins)
            {
                XElement xmlElement = XElement.Parse(urdfPluginDescription.text);
                IEnumerable<XElement> pluginElements = xmlElement.Elements(PluginManagerBase._PluginTag);
                foreach (XElement pluginElement in pluginElements)
                {
                    try
                    {
                        PluginManagerBase.PluginData pluginData =
                            new PluginManagerBase.PluginData(im.urdfPlugins, urdfPluginDescription);
                        UrdfPluginImplementation urdfPluginImplementation = pluginManager.GeneratePlugin(pluginData, pluginElement);
                        
                        if (urdfPluginImplementation != null)
                        {
                            loadedPlugins.AddLast(urdfPluginImplementation);
                        }
                    }
                    catch (Exception e)
                    {
                        RuntimeUrdf.urdfBuildErrors.AddLast(e);
                    }
                }
            }

            foreach (UrdfPluginImplementation urdfPluginImplementation in loadedPlugins)
            {
                try
                {
                    urdfPluginImplementation.FinaliseImport(im.urdfPlugins);
                }
                catch (Exception e)
                {
                    RuntimeUrdf.urdfBuildErrors.AddLast(e);
                }
            }
            
        }

        public static void CorrectAxis(GameObject robot)
        {
            UrdfRobot robotScript = robot.GetComponent<UrdfRobot>();
            if (robotScript == null)
            {
                Debug.LogError("Robot has no UrdfRobot component attached. Abandon correcting axis");
                return;
            }

            if (robotScript.CheckOrientation())
            {
                return;
            }
            Quaternion correctYtoZ = Quaternion.Euler(-90, 0, 90);
            Quaternion correctZtoY = Quaternion.Inverse((correctYtoZ));
            Quaternion correction = new Quaternion();

            if (robotScript.chosenAxis == ImportSettings.axisType.zAxis)
            {
                correction = correctYtoZ;
            }
            else
            {
                correction = correctZtoY;
            }

            UrdfVisual[] visualMeshList = robot.GetComponentsInChildren<UrdfVisual>();
            UrdfCollision[] collisionMeshList = robot.GetComponentsInChildren<UrdfCollision>();
            foreach (UrdfVisual visual in visualMeshList)
            {
                visual.transform.localRotation = visual.transform.localRotation * correction;
            }

            foreach (UrdfCollision collision in collisionMeshList)
            {
                if (collision.geometryType == GeometryTypes.Mesh)
                {
                    collision.transform.localRotation = collision.transform.localRotation * correction;
                }
            }
            robotScript.SetOrientation();
        }

        private static void CreateCollisionExceptions(UrdfRobotDescription robot, GameObject robotGameObject)
        {
            List<CollisionIgnore> CollisionList = new List<CollisionIgnore>();
            if (robot.ignoreCollisionPair.Count > 0)
            {
                foreach (System.Tuple<string, string> ignoreCollision in robot.ignoreCollisionPair)
                {
                    Transform collisionObject1 = GameObject.Find(ignoreCollision.Item1).transform.Find(collisionObjectName);
                    Transform collisionObject2 = GameObject.Find(ignoreCollision.Item2).transform.Find(collisionObjectName);

                    CollisionList.Add(new CollisionIgnore(collisionObject1, collisionObject2));
                }
            }
            robotGameObject.GetComponent<UrdfRobot>().collisionExceptions = CollisionList;
        }

        #endregion

        #region Export

        public static void ExportRobotToUrdf(this UrdfRobot urdfRobot, string exportRootFolder, string exportDestination = "")
        {
            
#if UNITY_EDITOR
            UrdfUnityMaterial.ClearPreviousExportData();
            UrdfExportPathHandler.SetExportPath(exportRootFolder, exportDestination);

            urdfRobot.FilePath = Path.Combine(UrdfExportPathHandler.GetExportDestination(), urdfRobot.name + ".urdf");

            UrdfRobotDescription robot = urdfRobot.ExportRobotData();
            if (robot == null) return;

            robot.exportPlugins = urdfRobot.exportPlugins;
            
            robot.WriteToUrdf();

            Debug.Log(robot.name + " was exported to " + UrdfExportPathHandler.GetExportDestination());

            UrdfMaterial.Materials.Clear();
            UrdfExportPathHandler.Clear();
            AssetDatabase.Refresh();
#else
            Debug.LogError("URDF Export is only available in Editor.");
#endif
        }

        private static UrdfRobotDescription ExportRobotData(this UrdfRobot urdfRobot)
        {
#if UNITY_EDITOR
            UrdfRobotDescription robot = new UrdfRobotDescription(urdfRobot.FilePath, urdfRobot.gameObject.name);

            List<string> linkNames = new List<string>();

            foreach (UrdfLink urdfLink in urdfRobot.GetComponentsInChildren<UrdfLink>())
            {
                //Link export
                if (linkNames.Contains(urdfLink.name))
                {
                    EditorUtility.DisplayDialog("URDF Export Error",
                        "URDF export failed. There are several links with the name " +
                        urdfLink.name + ". Make sure all link names are unique before exporting this robot.",
                        "Ok");
                    return null;
                }
                robot.links.Add(urdfLink.ExportLinkData());
                linkNames.Add(urdfLink.name);

                //Joint export
                UrdfJoint urdfJoint = urdfLink.gameObject.GetComponent<UrdfJoint>();
                if (urdfJoint != null)
                    robot.joints.Add(urdfJoint.ExportJointData());
                else if (!urdfLink.IsBaseLink)
                    //Make sure that links with no rigidbodies are still connected to the robot by a default joint
                    robot.joints.Add(UrdfJoint.ExportDefaultJoint(urdfLink.transform));
            }

            robot.materials = UrdfMaterial.Materials.Values.ToList();
            robot.plugins = urdfRobot.GetComponentInChildren<UrdfPlugins>().ExportPluginsData();

            //TODO - Export plugin data using a new system.

            return robot;
#else
            Debug.LogError("URDF Export is only available in Editor.");
            return null;
#endif
        }

        #endregion

        public static void CreateTag()
        {
#if UNITY_EDITOR
            if (RuntimeUrdf.IsRuntimeMode())
            {
                // This is to make the behavior consistent with Runtime mode
                // as tags cannot be created in a Standalone build.
                return;
            }

            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");


            // First check if it is not already present
            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(FKRobot.k_TagName))
                {
                    found = true;
                    break;
                }
            }

            // if not found, add it
            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(0);
                SerializedProperty n = tagsProp.GetArrayElementAtIndex(0);
                n.stringValue = FKRobot.k_TagName;
            }

            tagManager.ApplyModifiedProperties();
#endif
        }

        static void SetTag(GameObject go)
        {
            try
            {
                GameObject.FindWithTag(FKRobot.k_TagName);
            }
            catch (Exception)
            {
                Debug.LogError($"Unable to find tag '{FKRobot.k_TagName}'." +
                               $"Add a tag '{FKRobot.k_TagName}' in the Project Settings in Unity Editor.");
                return;
            }

            if (!go)
                return;

            try
            {
                go.tag = FKRobot.k_TagName;
            }
            catch (Exception)
            {
                Debug.LogError($"Unable to set the GameObject '{go.name}' tag to '{FKRobot.k_TagName}'.");
            }
        }
    }
}
