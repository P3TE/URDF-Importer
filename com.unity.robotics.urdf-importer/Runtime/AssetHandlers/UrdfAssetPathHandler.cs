/*
© Siemens AG, 2017-2018
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
using System.IO;
using System.Text;
using UnityEngine;
using System;
using System.Diagnostics;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfAssetPathHandler
    {
        //Relative to Assets folder
        private static string packageRoot;
        private const string MaterialFolderName = "Materials";
        
        private const string _AbsoluteFilePathPrefix = @"file://";
        private const string _PackageFilePathPrefix = @"package://";

        private const string _PackageXmlFileName = "package.xml";

        #region SetAssetRootFolder
        public static void SetPackageRoot(string newPath, bool correctingIncorrectPackageRoot = false)
        {
            string oldPackagePath = packageRoot;

            packageRoot = GetRelativeAssetPath(newPath);

            if (!RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(packageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(packageRoot, MaterialFolderName);
            }

            if (correctingIncorrectPackageRoot)
            {
                MoveMaterialsToNewLocation(oldPackagePath);
            }
        }
        #endregion

        #region GetPaths
        public static string GetPackageRoot()
        {
            return packageRoot;
        }
        
        public static string GetRelativeAssetPath(string absolutePath)
        {
            string assetPath = absolutePath;
            var absolutePathUnityFormat = absolutePath.SetSeparatorChar();
            if (!absolutePathUnityFormat.StartsWith(Application.dataPath.SetSeparatorChar()))
            {
#if UNITY_EDITOR
                if (!RuntimeUrdf.IsRuntimeMode())
                {
                    if (absolutePath.Length > Application.dataPath.Length)
                    {
                        assetPath = absolutePath.Substring(Application.dataPath.Length - "Assets".Length);
                    }
                }
#endif
            }
            else 
            {
                assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return assetPath.SetSeparatorChar();
        }

        public static string GetFullAssetPath(string relativePath)
        {
            string fullPath = Application.dataPath;
            if (relativePath.Substring(0, "Assets".Length) == "Assets")
            {
                fullPath += relativePath.Substring("Assets".Length);
            }
            else 
            {
                fullPath = fullPath.Substring(0, fullPath.Length - "Assets".Length) + relativePath;
            }
            return fullPath.SetSeparatorChar();
        }
        
        // This method accepts two strings the represent two files to
        // compare. A return value of 0 indicates that the contents of the files
        // are the same. A return value of any other value indicates that the
        // files are not the same.
        private static bool FileCompare(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Determine if the same file was referenced two times.
            if (file1 == file2)
            {
                // Return true to indicate that the files are the same.
                return true;
            }

            // Open the two files.
            fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read);
            fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read);

            // Check the file sizes. If they are not the same, the files
            // are not the same.
            if (fs1.Length != fs2.Length)
            {
                // Close the file
                fs1.Close();
                fs2.Close();

                // Return false to indicate files are different
                return false;
            }

            // Read and compare a byte from each file until either a
            // non-matching set of bytes is found or until the end of
            // file1 is reached.
            do
            {
                // Read one byte from each file.
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));

            // Close the files.
            fs1.Close();
            fs2.Close();

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }

        public static bool DirectoryContainsFileWithName(string directoryPath, string filename)
        {
            string[] filesInDirectory = Directory.GetFiles(directoryPath);
            foreach (string path in filesInDirectory)
            {
                if (Path.GetFileName(path) == filename)
                {
                    return true;
                }
            }
            return false;
        }
        
        private static string FromAbsolutePath(string urdfPath)
        {
            Assert.IsTrue(urdfPath.StartsWith(_AbsoluteFilePathPrefix),
                $"path {urdfPath} should start with {_AbsoluteFilePathPrefix}");
            
            string absolutePath = urdfPath.Substring(_AbsoluteFilePathPrefix.Length);
            
            if (!File.Exists(absolutePath))
            {
                throw new Exception($"No file found at {absolutePath}");
            }

            return absolutePath;
        }

        // loading assets relative path from ROS/ROS2 package.
        private static string FromPackagePath(string urdfPath)
        {
            
            //A URDF may have a line like this:
            //<mesh filename="package://urdf_tutorial/meshes/l_finger.dae"/>
            //For this to work, we have to know this 'package' this is referring to.
            //My proposal to figure this out would be to use the file location of the
            //imported URDF.
            
            Assert.IsTrue(urdfPath.StartsWith(_PackageFilePathPrefix),
                $"path {urdfPath} should start with {_PackageFilePathPrefix}");
            
            string packagePath = urdfPath.Substring(_PackageFilePathPrefix.Length).SetSeparatorChar();
            
            // TODO - This doesn't work if you don't know what that was (urdf was a ros parameter).
            RuntimeUrdf.AddImportWarning("Package relative paths for URDF imports are not currently working properly, needs more work...");

            return packagePath;
        }

        private static void TryFindRosPackageForFile(string absolutePath)
        {
            
            string fileName = Path.GetFileName(absolutePath);

            LinkedList<string> relativePathList = new LinkedList<string>();
            relativePathList.AddLast(fileName);
            
            DirectoryInfo parentDirectoryInfo = Directory.GetParent(absolutePath);
            
            bool packageFound = false;
            
            while (!packageFound && parentDirectoryInfo != null && parentDirectoryInfo.Exists)
            {
                relativePathList.AddFirst(parentDirectoryInfo.Name);
                if(DirectoryContainsFileWithName(parentDirectoryInfo.FullName, _PackageXmlFileName))
                {
                    packageFound = true;
                }
                parentDirectoryInfo = Directory.GetParent(parentDirectoryInfo.FullName);
            }
            
            if (packageFound)
            {

                string packageXmlPath = $"{parentDirectoryInfo.FullName}{Path.PathSeparator}{_PackageXmlFileName}";
                FileInfo packageXmlFile = new FileInfo(packageXmlPath);
                RosPackagePathHelper.ReadPackageXmlForPackageName(packageXmlFile, out string packageName);
                
                StringBuilder relativePathBuilder = new StringBuilder();

                LinkedListNode<string> relativePathNode = relativePathList.First;
                for (int i = 0; relativePathNode != null; i++)
                {
                    if (i > 0)
                    {
                        relativePathBuilder.Append(Path.PathSeparator);
                    }
                    relativePathBuilder.Append(relativePathNode.Value);
                    relativePathNode = relativePathNode.Next;
                }

                string relativePath = relativePathBuilder.ToString();

                string packagePath = parentDirectoryInfo.FullName;
            
                Assert.AreEqual(absolutePath, $"{packagePath}{Path.PathSeparator}{relativePath}", "");
            }
            else
            {
                
            }

        }

        private static string AttemptToCopyFileToAssets(string urdfPath)
        {

            string absolutePath = urdfPath.Substring(7);
            
            if (!File.Exists(absolutePath))
            {
                throw new Exception($"No file found at {absolutePath}");
            }

            string fileName = Path.GetFileName(absolutePath);

            DirectoryInfo parentDirectoryInfo = Directory.GetParent(absolutePath);

            bool useFallback = true;

            LinkedList<string> result = new LinkedList<string>();

            while (useFallback && parentDirectoryInfo != null && parentDirectoryInfo.Exists)
            {
                result.AddFirst(parentDirectoryInfo.Name);
                if(DirectoryContainsFileWithName(parentDirectoryInfo.FullName, "package.xml"))
                {
                    useFallback = false;
                }
                parentDirectoryInfo = Directory.GetParent(parentDirectoryInfo.FullName);
            }
            
            
            if (useFallback)
            {
                result.Clear();
                result.AddLast("resources");
            }

            //Copy the file to the Assets directory.
            string localDirectoryLocalPath = packageRoot;
            string localDirectoryLocalPathNoRoot = "";

            if (!parentDirectoryInfo.FullName.StartsWith(packageRoot))
            {
                localDirectoryLocalPath = parentDirectoryInfo.FullName;
                return absolutePath;
            }
            
            foreach (string directoryName in result)
            {
                localDirectoryLocalPathNoRoot = Path.Combine(localDirectoryLocalPathNoRoot, directoryName);
            }

            localDirectoryLocalPath = Path.Combine(localDirectoryLocalPath, localDirectoryLocalPathNoRoot);

#if UNITY_EDITOR
            string parentFolder = packageRoot;
            foreach (string directoryName in result)
            {
                if (!AssetDatabase.IsValidFolder(localDirectoryLocalPath))
                {
                    AssetDatabase.CreateFolder(parentFolder, directoryName);
                }
                parentFolder = Path.Combine(parentFolder, directoryName);
            }
#else
            if (!Directory.Exists(localDirectoryLocalPath))
            {
                Directory.CreateDirectory(localDirectoryLocalPath);
            }
#endif
            
            string localFilePath = Path.Combine(localDirectoryLocalPath, fileName);
            bool copyNewFile = true;
            if (File.Exists(localFilePath))
            {
                if (FileCompare(absolutePath, localFilePath))
                {
                    copyNewFile = false;
                }
                else
                {
                    File.Delete(localFilePath);
                }
            }

            if (copyNewFile)
            {
                File.Copy(absolutePath, localFilePath);
#if UNITY_EDITOR
                AssetDatabase.ImportAsset(localFilePath);
#endif
            }
            
            string packagePrefixFilePath = @"package://" + Path.Combine(localDirectoryLocalPathNoRoot, fileName);
            return packagePrefixFilePath;
        }

        public static string GetRelativeAssetPathFromUrdfPath(string urdfPath, bool convertToPrefab=true)
        {
            
            string path;
            bool useFileUri = false;
            const string _UpOneDirectoryPath = "../";
            
            if (urdfPath.StartsWith(_AbsoluteFilePathPrefix))
            {
                path = FromAbsolutePath(urdfPath);
                useFileUri = true;
            } else if (urdfPath.StartsWith(_PackageFilePathPrefix))
            {
                path = FromPackagePath(urdfPath);
            }
            else if (urdfPath.StartsWith(_UpOneDirectoryPath))
            {
                RuntimeUrdf.AddImportWarning($"Attempting to replace file path's starting instance of `{_UpOneDirectoryPath}` with standard package notation `{_PackageFilePathPrefix}` to prevent manual path traversal at root of directory!");
                string urdfPackagePath = _PackageFilePathPrefix + urdfPath.Substring(_UpOneDirectoryPath.Length);
                path = FromPackagePath(urdfPackagePath);
            }
            else
            {
                path = urdfPath.SetSeparatorChar();
            }

            if (convertToPrefab) 
            {
                if (Path.GetExtension(path)?.ToLowerInvariant() == ".stl")
                    path = path.Substring(0, path.Length - 3) + "prefab";

            }
            
            if (useFileUri) {
                return path;
            }
            return Path.Combine(packageRoot, path);
        }
        #endregion

        public static bool IsValidAssetPath(string path)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                return Directory.Exists(path) || File.Exists(path);
            }
#endif
            //RuntimeImporter. TODO: check if the path really exists
            return true;
        }

        #region Materials

        private static void MoveMaterialsToNewLocation(string oldPackageRoot)
        {
            if (RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(oldPackageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_MoveAsset(
                    Path.Combine(oldPackageRoot, MaterialFolderName),
                    Path.Combine(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName));
            }
            else
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName);
            }
        }

        public static string GetMaterialAssetPath(string materialName)
        {
            string result = Path.Combine(MaterialFolderName, Path.GetFileName(materialName) + ".mat");
            if (packageRoot != null)
            {
                result = Path.Combine(packageRoot, result);
            }
            return result;
        }

        #endregion
    }

}