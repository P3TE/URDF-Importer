using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Unity.Robotics.UrdfImporter.Urdf.RuntimeImport
{

    public class UrdfRuntimeTextureManager : MonoBehaviour
    {

        private static bool instanceSet = false;
        private static UrdfRuntimeTextureManager _instance;
        
        private Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();
        
        public static UrdfRuntimeTextureManager Instance
        {
            get
            {
                if (!instanceSet)
                {
                    instanceSet = true;
                    GameObject gameObject = new GameObject();
                    DontDestroyOnLoad(gameObject);
                    _instance = gameObject.AddComponent<UrdfRuntimeTextureManager>();
                    gameObject.name = _instance.GetType().Name;
                }
                return _instance;
            }
        }

        private void OnDestroy()
        {
            instanceSet = false;
            _instance = null;
            CleanupAndDestroyAllTextures();
        }
        
        public Texture2D LoadTextureFromFile(string absoluteFilePath, bool isNormal = false)
        {

            if (loadedTextures.TryGetValue(absoluteFilePath, out Texture2D texture2D))
            {
                //We already have loaded this texture, return it.
                return texture2D;
            }

            
            
            //TODO - Load the texture.
            if (!File.Exists(absoluteFilePath))
            {
                throw new Exception($"No image found at path {absoluteFilePath}");
            }


            Texture2D result;
            if (isNormal)
            {
                result = new Texture2D(2, 2, TextureFormat.RGB24, true, true);
            }
            else
            {
                result = new Texture2D(2, 2);
            }
            
            byte[] fileData = File.ReadAllBytes(absoluteFilePath);
            
            result.LoadImage(fileData); //..this will auto-resize the texture dimensions.
            
            loadedTextures.Add(absoluteFilePath, result);
            return result;
        }
        
        public void CleanupAndDestroyAllTextures()
        {
            foreach (KeyValuePair<string,Texture2D> loadedTexture in loadedTextures)
            {
                Object.Destroy(loadedTexture.Value);
            }
            loadedTextures.Clear();
        }
        
    }
}