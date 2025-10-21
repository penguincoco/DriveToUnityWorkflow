using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu(fileName = "ImageSyncConfig", menuName = "Image Sync/Image Sync Config")]
public class ImageSyncConfig : ScriptableObject
{
    [Header("Drive / Google Apps Script")]
    public string folderId;
    public string appsScriptURL = "";
    public string sourceCSV = "";
    private string targetCsvPath = "";  //this is the csv in the Unity project that will be mirrored from Google Sheets!
   
    [Header("Unity .csv")]
    public TextAsset targetCSV;
    
    public void UpdateTargetCsvPath()
    {
#if UNITY_EDITOR
        if (targetCSV != null)
            targetCsvPath = AssetDatabase.GetAssetPath(targetCSV);
        else
            targetCsvPath = "";
#endif
    }

    public string GetTargetCsvPath()
    {
#if UNITY_EDITOR
        // Always get fresh path in case asset moved
        if (targetCSV != null)
            return AssetDatabase.GetAssetPath(targetCSV);
#endif
        return targetCsvPath;
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(folderId) && 
               !string.IsNullOrEmpty(appsScriptURL) &&
               !string.IsNullOrEmpty(sourceCSV) && 
               !string.IsNullOrEmpty(GetTargetCsvPath());
    }
}