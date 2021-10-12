using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using J2N.Text;
using Debug = UnityEngine.Debug;

namespace Unity.Robotics.UrdfImporter
{
    public class RosPackagePathHelper
    {

        public const string kRosPackagePathEnvironmentVariable = "ROS_PACKAGE_PATH";
        
        public static List<string> packagePaths = new List<string>();

        public static Dictionary<string, string> foundPackages = new Dictionary<string, string>();

        public static void ResetPackagePaths()
        {
            packagePaths.Clear();
            foundPackages.Clear();
        }

        public static void LoadFromEnvironmentVariable()
        {
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
                if (!packagePaths.Contains(expandedPath))
                {
                    packagePaths.Add(expandedPath);
                }
            }
        }

        public static void SearchPackagePathsForPackages()
        {
            foundPackages.Clear();

            foreach (string packagePath in packagePaths)
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
                        if (!foundPackages.ContainsKey(packageName))
                        {
                            foundPackages.Add(packageName, directoryInfo.FullName);
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