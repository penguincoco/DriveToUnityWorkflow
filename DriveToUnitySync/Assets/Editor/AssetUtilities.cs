using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public static class AssetUtilities
{
    public static T FindAssetByName<T>(string assetName) where T : UnityEngine.Object
    {
        // Search for all assets of type T
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && asset.name == assetName)
            {
                return asset;
            }
        }
        return null;
    }

    public static List<string> FindFoldersByName(string partialName, bool ignoreCase = true)
    {
        List<string> matchingFolders = new List<string>();

        string[] guids = AssetDatabase.FindAssets("t:Folder");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string folderName = System.IO.Path.GetFileName(path);

            if (ignoreCase)
            {
                if (folderName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    matchingFolders.Add(path);
            }
            else
            {
                if (folderName.Contains(partialName))
                    matchingFolders.Add(path);
            }
        }

        return matchingFolders;
    }

    public static Dictionary<string, T> GetExistingAssets<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        Dictionary<string, T> assets = new Dictionary<string, T>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (!assets.ContainsKey(assetName))
                {
                    assets.Add(assetName, asset);
                }
            }
        }

        return assets;
    }

    public static T[] FindAssetsByType<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        T[] assets = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return assets;
    }
    
    public static List<T> FindAssetsByNameContains<T>(string partialName, bool ignoreCase = true) where T : UnityEngine.Object
    {
        List<T> results = new List<T>();

        // Find all assets of type T
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                string assetName = asset.name;

                if (ignoreCase)
                {
                    if (assetName.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(asset);
                    }
                }
                else
                {
                    if (assetName.Contains(partialName))
                    {
                        results.Add(asset);
                    }
                }
            }
        }

        return results;
    }
    
    public static string[] GetAllAssetPathsInFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        
        List<string> assetPaths = new List<string>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Filter out folders - only keep files
            if (!AssetDatabase.IsValidFolder(path))
            {
                assetPaths.Add(path);
            }
        }
        
        return assetPaths.ToArray();
    }
    
    public static Object[] GetAllAssetsInFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        
        List<Object> assets = new List<Object>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Skip folders
            if (!AssetDatabase.IsValidFolder(path))
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
        }
        
        return assets.ToArray();
    }
}