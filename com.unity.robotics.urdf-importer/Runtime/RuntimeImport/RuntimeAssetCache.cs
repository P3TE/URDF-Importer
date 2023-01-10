using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Urdf.RuntimeImport
{
    public class RuntimeAssetCache : MonoBehaviour
    {
        private static bool instanceSet = false;
        private static RuntimeAssetCache instance;
        
        public static RuntimeAssetCache Instance
        {
            get
            {
                if (!instanceSet)
                {
                    instanceSet = true;
                    GameObject gameObject = new GameObject();
                    DontDestroyOnLoad(gameObject);
                    instance = gameObject.AddComponent<RuntimeAssetCache>();
                    gameObject.name = instance.GetType().Name;
                }
                return instance;
            }
        }
        
        private readonly Dictionary<string, Object> loadedAssets = new();

        private void OnDestroy()
        {
            instanceSet = false;
            instance = null;

            foreach (Object asset in loadedAssets.Values)
            {
                Destroy(asset);
            }

            loadedAssets.Clear();
        }
        
        public Texture2D LoadTextureFromFile(string absoluteFilePath, bool isNormal = false)
        {
            if (loadedAssets.TryGetValue(absoluteFilePath, out Object asset))
            {
                if (asset is not Texture2D texture2D)
                    throw new FileLoadException($"Asset at {absoluteFilePath} attempted load as multiple types!");
                
                //We already have loaded this texture, return it.
                return texture2D;
            }
            
            if (!File.Exists(absoluteFilePath))
                throw new FileNotFoundException($"No image found at path {absoluteFilePath}");
            
            Texture2D result = isNormal
                ? new Texture2D(2, 2, TextureFormat.RGB24, true, true)
                : new Texture2D(2, 2);
            
            byte[] fileData = File.ReadAllBytes(absoluteFilePath);
            
            result.LoadImage(fileData); //this will auto-resize the texture dimensions.
            
            loadedAssets.Add(absoluteFilePath, result);
            return result;
        }

        public bool GetAssetFromCache<T>(string key, out T asset) where T : Object
        {
            if (loadedAssets.TryGetValue(key, out Object cachedAsset))
            {
                if (cachedAsset is T typedCachedAsset)
                {
                    asset = typedCachedAsset;
                    return true;
                }
            }

            asset = default;
            return false;
        }

        public void AddAssetToCache(string key, Object asset)
        {
            if (loadedAssets.ContainsKey(key))
            {
                Debug.LogWarning($"Asset '{key}' already in cache!");
                return;
            }
            
            loadedAssets.Add(key, asset);
        }

        public void AddGameObjectToCache(string key, GameObject asset)
        {
            if (loadedAssets.ContainsKey(key))
            {
                Debug.LogWarning($"Asset '{key}' already in cache!");
                return;
            }
            
            asset.SetActive(false);
            asset.transform.parent = transform;
            
            loadedAssets.Add(key, asset);
        }
    }
}