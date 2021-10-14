using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Debug = UnityEngine.Debug;

namespace Unity.Robotics.UrdfImporter
{
    public class RosPackagePathHelper
    {

        public const string kRosPackagePathEnvironmentVariable = "ROS_PACKAGE_PATH";

        private static bool packagePathsResolved = false;
        public static List<string> _packagePaths = new List<string>();

        private static bool foundPackagesResolved = false;
        public static Dictionary<string, string> _foundPackages = new Dictionary<string, string>();

        public static List<string> PackagePaths
        {
            get
            {
                if (!packagePathsResolved)
                {
                    LoadFromEnvironmentVariable();
                }
                return _packagePaths;
            }
        }
        
        public static Dictionary<string, string> FoundPackages
        {
            get
            {
                if (!foundPackagesResolved)
                {
                    SearchPackagePathsForPackages();
                }
                return _foundPackages;
            }
        }

        public static bool TryResolvePackageNamePath(string packageName, out string result)
        {
            return FoundPackages.TryGetValue(packageName, out result);
        }

        public static void ResetPackagePaths(List<string> additionalPackagePaths = null)
        {
            packagePathsResolved = false;
            foundPackagesResolved = false;
            _packagePaths.Clear();
            _foundPackages.Clear();
            if (additionalPackagePaths != null)
            {
                foreach (string additionalPackagePathDirectory in additionalPackagePaths)
                {
                    LoadPathString(additionalPackagePathDirectory);
                }
            }
        }

        public static void LoadFromEnvironmentVariable()
        {
            packagePathsResolved = true;
            string packagePathEnvironmentVariable = Environment.GetEnvironmentVariable(kRosPackagePathEnvironmentVariable);
            if (packagePathEnvironmentVariable == null)
            {
                Debug.Log($"No environment variable set for {kRosPackagePathEnvironmentVariable}");
                return;
            }
            LoadPathString(packagePathEnvironmentVariable);
        }

        public static void LoadPathString(string packagePathString)
        {
            string[] packagePathEnvironmentVariableSplit = packagePathString.Split(":");
            foreach (string path in packagePathEnvironmentVariableSplit)
            {
                //Resolve variables in the string:
                string expandedPath = Environment.ExpandEnvironmentVariables(path);

                //Don't add duplicates.
                if (!_packagePaths.Contains(expandedPath))
                {
                    _packagePaths.Add(expandedPath);
                }
            }
        }

        public static void SearchPackagePathsForPackages()
        {
            _foundPackages.Clear();
            foundPackagesResolved = true;

            foreach (string packagePath in PackagePaths)
            {
                SearchDirectoryForPackages(packagePath);
            }
        }

        private static void SearchDirectoryForPackages(string directoryPath)
        {
            DirectoryInfo packageRootDirectory = new DirectoryInfo(directoryPath);
            if (!packageRootDirectory.Exists)
            {
                Debug.LogWarning($"No directory at {directoryPath}");
                return;
            }
            SearchDirectoryForPackages(packageRootDirectory);
        }

        private static void SearchDirectoryForPackages(DirectoryInfo directoryInfo)
        {

            FileInfo[] files = directoryInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                if (file.Name == "package.xml")
                {
                    try
                    {
                        string packageName = ReadPackageXmlForPackageName(file);
                        if (!FoundPackages.ContainsKey(packageName))
                        {
                            FoundPackages.Add(packageName, directoryInfo.FullName);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    //I'm pretty sure that once you've found a package.xml, there's no point searching any deeper.
                    return;
                }
            }

            DirectoryInfo[] subDirectories = directoryInfo.GetDirectories();
            foreach (DirectoryInfo subDirectory in subDirectories)
            {
                SearchDirectoryForPackages(subDirectory);
            }
            
        }

        private static string ReadPackageXmlForPackageName(FileInfo packageXmlFile)
        {
            using Stream xmlStream = packageXmlFile.OpenRead();
            XDocument xdoc = XDocument.Load(xmlStream);
            XElement packageNode = xdoc.Element("package");
            XElement nameNode = packageNode.Element("name");
            return nameNode.Value;
        }
        
    }
}