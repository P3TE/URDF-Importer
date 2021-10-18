using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Robotics.UrdfImporter.Urdf.RuntimeImport
{
    public static class RuntimeTextureManager
    {

        private static Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

        public static Texture2D LoadTextureFromFile(string absoluteFilePath)
        {

            if (loadedTextures.TryGetValue(absoluteFilePath, out Texture2D texture2D))
            {
                //We already have loaded this texture, return it.
                return texture2D;
            }
            
            //TODO - Load the texture.
            if (File.Exists(absoluteFilePath))
            {
                throw new Exception($"No image found at path {absoluteFilePath}");
            }
            
            byte[] fileData = File.ReadAllBytes(absoluteFilePath);
            Texture2D result = new Texture2D(2, 2);
            result.LoadImage(fileData); //..this will auto-resize the texture dimensions.
            
            loadedTextures.Add(absoluteFilePath, result);
            return result;
        }

        public static void CleanupAndDestroyAllTextures()
        {
            foreach (KeyValuePair<string,Texture2D> loadedTexture in loadedTextures)
            {
                Object.Destroy(loadedTexture.Value);
            }
            loadedTextures.Clear();
        }
        
        
    }
}