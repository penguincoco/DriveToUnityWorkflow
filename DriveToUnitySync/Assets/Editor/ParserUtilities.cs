using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;

public static class ParserUtilities
{
    public static string[] ParseToArray(string rawText) => rawText.Split('\n');
    
    //takes in a csvFile path, returns an array that is split along new lines
    public static string[] ParseCSV(string filePath)
    {
        string rawText = ReadCSV(filePath); // Read CSV as string

        if (string.IsNullOrEmpty(rawText))
        {
            Debug.LogWarning("CSV file is empty or not found.");
            return null;
        }

        string[] parsedArray = rawText
        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
        .Skip(1) // Skip the first element (header)
        .ToArray();

        return parsedArray;
    }
    
    public static string[] ParseCSVText(string csvText)
    {
        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogWarning("CSV text is empty.");
            return null;
        }

        string[] parsedArray = csvText
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // skip the header
            .ToArray();

        return parsedArray;
    }

    //read in the CSV 
    public static string ReadCSV(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to read CSV: " + e.Message);
            }
        }
        return string.Empty;
    }

    public static string Parse(string csvFilePath)
    {
        if (File.Exists(csvFilePath))
        {
            try
            {
                return File.ReadAllText(csvFilePath);
            }
            catch (System.Exception)
            {
                return string.Empty;
            }
        }
        else
            return string.Empty;
    }

    public static void CreateAsset<T>(T instance, string folderPath, string assetName) where T : ScriptableObject
    {
        if (instance == null)
        {
            Debug.LogError("Cannot create asset: instance is null!");
            return;
        }

        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError("Cannot create asset: folderPath is null or empty!");
            return;
        }

        //create the folder if it doesn't exist
        if (AssetDatabase.IsValidFolder(folderPath) == false)
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        string assetPath = $"{folderPath}/{assetName}.asset";
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(instance, uniquePath);
        AssetDatabase.SaveAssets();
    }

    public static List<T> ParseLines<T>(
        IEnumerable<string> lines,
        int minParts,
        int maxParts,
        Func<string[], T> factory)
    {
        List<T> result = new List<T>();

        foreach (string line in lines)
        {
            string prunedLine = line.Replace("\"", "");
            string[] parts = prunedLine.Split(new char[] { ',' }, maxParts);

            if (parts.Length < minParts) 
                continue;

            result.Add(factory(parts));
        }

        return result;
    }
}