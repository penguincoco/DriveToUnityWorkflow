using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Linq;

/*
 * This script is for the Image Sync Editor Window. It syncs Images and other media from a Folder in Drive to Unity.
 *
 * Major coding logic flow: 
 * Logic flow: SyncSheet() runs the Apps Script which reads the folders in Drive and populates a Google Spreadsheet. When Apps Script is completed, it calls SyncWithGoogleSheets() to sync the
 * Unity-side .csv with the Google Sheet
 */

public class ImageSyncWindow : EditorWindow
{
    // ---------- Helpers and Serialization ----------
    private AssetDownloader downloader;
    private UIPainter painter;
    private SerializedObject so;
    [SerializeField] private ImageSyncConfig config;
    private SerializedProperty propConfig;
    
    private SerializedObject configSO;
    private SerializedProperty configPropFolderId;
    private SerializedProperty configPropAppsScriptURL;
    private SerializedProperty configPropSourceCSV;
    private SerializedProperty configPropTargetCSV;

    // ---------- Status messages ----------
    private string statusMessage = "";
    private string populateStatusMessage = "";
    
    // ---------- Preview: downloading/populating assets ----------
    private Texture2D currentTexturePreview;
    private string currentNamePreview = "";
    private int totalAssets = 0;
    private int currentAssetIndex = 0;
    private bool isDownloading = false;
    private bool shouldStopDownload = false;
    private List<EditorCoroutine> activeCoroutines = new List<EditorCoroutine>();
    
    // Preview: CSV contents 
    private List<Line> csvLines = new List<Line>();
    private Dictionary<string, float> downloadProgress = new Dictionary<string, float>();
    private HashSet<string> currentlyDownloading = new HashSet<string>();
    
    // For Sprite setting selection
    [SerializeField] private FilterMode filterMode = FilterMode.Point;
    [SerializeField] private TextureImporterType textureType = TextureImporterType.Default;
    [SerializeField] private int pixelsPerUnit = 100;
    [SerializeField] private TextureImporterCompression compressionType = TextureImporterCompression.Uncompressed;
    [SerializeField] private SpriteImportMode spriteImportMode = SpriteImportMode.Single;
    [SerializeField] private bool alphaIsTransparency = true;
    
    private SerializedProperty propPixelsPerUnit;
    private SerializedProperty propFilterMode;
    private SerializedProperty propTextureType;
    private SerializedProperty propCompressionType;
    private SerializedProperty propSpriteImportMode;
    private SerializedProperty propAlphaIsTransparency;
    
    [MenuItem("Tools/Drive Image Sync")]
    public static void ShowWindow() => GetWindow<ImageSyncWindow>("Drive Image Sync");
    
    private void OnEnable()
    {
        downloader = new AssetDownloader(this);
        painter = new UIPainter(this);
        EditorApplication.update += RepaintWindow;
        
        LoadOrCreateConfig();
        SerializeProperties();
    }

    private void OnDisable() => EditorApplication.update -= RepaintWindow;
    
    private void RepaintWindow()
    {
        if (downloader.IsDownloading || downloader.CurrentlyDownloading.Count > 0)
            Repaint();
    }

    private void LoadOrCreateConfig()
    {
        //Note: this assumes you only ever have one config! 
        if (config == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:ImageSyncConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<ImageSyncConfig>(path);
            }
        }

        // if the config is currently null and one was not already found in the project
        if (config == null)
        {
            config = CreateInstance<ImageSyncConfig>();
            AssetDatabase.CreateAsset(config, "Assets/ImageSyncConfig.asset");
            AssetDatabase.SaveAssets();
        }
    }

    private void SerializeProperties()
    {
        so = new SerializedObject(this);
        propConfig = so.FindProperty("config");
        
        propPixelsPerUnit = so.FindProperty("pixelsPerUnit");
        propFilterMode = so.FindProperty("filterMode");
        propTextureType = so.FindProperty("textureType");
        propCompressionType = so.FindProperty("compressionType");
        propSpriteImportMode = so.FindProperty("spriteImportMode");
        propAlphaIsTransparency = so.FindProperty("alphaIsTransparency");
        
        // Create SerializedObject for the config
        if (config != null)
        {
            configSO = new SerializedObject(config);
            configPropFolderId = configSO.FindProperty("folderId");
            configPropAppsScriptURL = configSO.FindProperty("appsScriptURL");
            configPropSourceCSV = configSO.FindProperty("sourceCSV");
            configPropTargetCSV = configSO.FindProperty("targetCSV");
        }
    }

    void OnGUI()
    {
        so.Update();
        painter.DrawMainUI(position.width, position.height);
        
        // ---------- Config Assignment ----------
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(propConfig, new GUIContent("Config Asset"));
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            SerializeProperties(); //have to reserialize if the config asset is changed
        }
        
        //if the config is null upon opening the window, it means that there was none found in the project, presumably. 
        //display a button for creating a config
        if (config == null)
        {
            EditorGUILayout.HelpBox("Please assign or create an ImageSyncConfig asset.", MessageType.Warning);
            if (GUILayout.Button("Create New Config"))
            {
                config = CreateInstance<ImageSyncConfig>();
                AssetDatabase.CreateAsset(config, "Assets/Config/ImageSyncConfig.asset");
                AssetDatabase.SaveAssets();
                so.Update();
                propConfig.objectReferenceValue = config;
                so.ApplyModifiedProperties();
                SerializeProperties();
            }
            
            painter.EndMainUI(); 
            so.ApplyModifiedProperties();
            return;
        }

        config.UpdateTargetCsvPath();
        configSO.Update();
        
        painter.UpdateConfiguration(
            config.folderId, 
            config.appsScriptURL, 
            config.sourceCSV, 
            config.targetCSV, 
            config.GetTargetCsvPath()
        );
        
        //showing in editor the fields for changing these values
        painter.DrawSyncConfigurationFields(
            configPropFolderId, 
            configPropAppsScriptURL, 
            configPropSourceCSV, 
            configPropTargetCSV
        );
        
        configSO.ApplyModifiedProperties();
        
        painter.DrawSyncButton(RunAppsScriptWrapper);
        GUILayout.Space(15);
        
        //csv preview
        painter.DrawCSVPreviewSection(
            csvLines,
            config.GetTargetCsvPath(),
            currentlyDownloading,
            downloadProgress,
            LoadCSVData,
            OnRedownloadAsset
        );
        
        GUILayout.Space(15);
        painter.DrawStatusMessage(statusMessage);
        GUILayout.Space(25);
        
        //download preview bar thingy
        painter.DrawPreviewSection(totalAssets, currentAssetIndex, currentNamePreview, currentTexturePreview);
        
        //populate button
        painter.DrawPopulateButton(
            config.targetCSV != null && !string.IsNullOrEmpty(config.GetTargetCsvPath()),
            isDownloading,
            OnPopulateAssets,
            StopAllDownloads
        );
        
        GUILayout.Space(25);
        
        // showing current sprite settings
        painter.DrawSpriteSettings(
            filterMode,
            textureType,
            pixelsPerUnit,
            compressionType,
            spriteImportMode,
            propTextureType,
            propPixelsPerUnit,
            propFilterMode,
            propCompressionType,
            propSpriteImportMode
        );

        so.ApplyModifiedProperties();
        painter.EndMainUI();
    }
    
    private void RunAppsScriptWrapper() => EditorCoroutineUtility.StartCoroutineOwnerless(RunAppsScript(config.appsScriptURL, msg => statusMessage = msg));
    private void SyncWithGoogleSheets() => EditorCoroutineUtility.StartCoroutineOwnerless(downloader.DownloadCSV(config.GetTargetCsvPath(), config.sourceCSV, msg => statusMessage = msg));

    private void StopAllDownloads()
    {
        shouldStopDownload = true;
        
        foreach (EditorCoroutine coroutine in activeCoroutines)
        {
            if (coroutine != null)
                EditorCoroutineUtility.StopCoroutine(coroutine);
        }

        activeCoroutines.Clear();
        isDownloading = false;
        currentlyDownloading.Clear();
        downloadProgress.Clear();

        populateStatusMessage = "Download stopped";
        currentNamePreview = "Download cancelled";
        
        currentAssetIndex = 0;
        totalAssets = 0;
        currentTexturePreview = null;

        Debug.Log("All downloads stopped");
        Repaint();
    }

    private void EvaluateEntries(string[] parsedArray)
    {
        List<Line> lines = ParserUtilities.ParseLines(
            parsedArray,
            minParts: 3,
            maxParts: 3,
            factory: parts => new Line(parts[0], parts[1], parts[2])
        );

        csvLines = lines;
        
        activeCoroutines.Clear();
        shouldStopDownload = false;

        EditorCoroutine downloadCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAllAssets(lines));
        activeCoroutines.Add(downloadCoroutine);
        
        currentAssetIndex = 0;
        totalAssets = lines.Count;
        currentTexturePreview = null;
        currentNamePreview = "";
    }

    private IEnumerator RunAppsScript(string url, Action<string> onStatusUpdate)
    {
        Debug.Log("Running Apps Script");
        onStatusUpdate("Running Apps Script...");

        string cleanedId = AppsScriptUtilities.GetFolderID(config.folderId);
    
        if (!string.IsNullOrEmpty(cleanedId) && cleanedId != config.folderId)
        {
            // Update via SerializedObject for undo support
            configSO.Update();
            configPropFolderId.stringValue = cleanedId;
            configSO.ApplyModifiedProperties();
        }
    
        string urlWithParams = url + "?folderId=" + config.folderId;
        
        using (UnityWebRequest appsScriptRequest = UnityWebRequest.Get(urlWithParams))
        {
            appsScriptRequest.timeout = 60;
            yield return appsScriptRequest.SendWebRequest();

            Debug.Log($"Response Code: {appsScriptRequest.responseCode}");

            if (appsScriptRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to run Apps Script: {appsScriptRequest.error}");
            }
            else
            {
                string responseText = appsScriptRequest.downloadHandler.text;
                Debug.Log($"Full Response: {responseText}");
            
                AppsScriptResponse response = JsonUtility.FromJson<AppsScriptResponse>(responseText);
            
                if (response.status == "error")
                {
                    Debug.LogError($"Apps Script Error: {response.message}");
                    onStatusUpdate($"Error: {response.message}");
                }
                else
                {
                    onStatusUpdate("Apps Script successfully run!");
                    SyncWithGoogleSheets();
                }
            }
        }
    }

    private void LoadCSVData()
    {
        string targetCsvPath = config.GetTargetCsvPath();
        
        if (!File.Exists(targetCsvPath) || string.IsNullOrEmpty(targetCsvPath))
        {
            csvLines.Clear();
            Debug.LogWarning("CSV file not found or path is empty.");
            return;
        }
    
        try
        {
            TextAsset csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(targetCsvPath);
        
            if (csvAsset == null || string.IsNullOrEmpty(csvAsset.text))
            {
                csvLines.Clear();
                Debug.LogWarning("CSV file is empty or could not be loaded.");
                return;
            }
        
            string[] parsedArray = ParserUtilities.ParseCSVText(csvAsset.text);
        
            if (parsedArray == null || parsedArray.Length == 0)
            {
                csvLines.Clear();
                Debug.LogWarning("CSV file contains no data.");
                return;
            }
        
            csvLines = ParserUtilities.ParseLines(
                parsedArray,
                minParts: 3,
                maxParts: 3,
                factory: parts => new Line(parts[0], parts[1], parts[2])
            );
        
            Repaint();
        }
        catch (Exception e)
        {
            csvLines.Clear();
            Debug.LogError($"Error loading CSV data: {e.Message}");
        }
    }

    private IEnumerator DownloadAllAssets(List<Line> lines)
    {
        isDownloading = true;
        populateStatusMessage = "Populating...";

        foreach (Line line in lines)
        {
            currentAssetIndex++;
            currentNamePreview = line.assetName;
            Repaint();
            
            yield return downloader.DownloadAndSave(
                line.assetName,
                line.assetDownloadLink,
                "Assets/Art" + line.assetPath,
                msg => populateStatusMessage = msg
            );
        }

        if (!shouldStopDownload)
        {
            yield return CleanUpDeletedAssets(lines);
            populateStatusMessage = "Populated all assets";
            
            yield return new WaitForSeconds(2f);
            currentTexturePreview = null;
            currentNamePreview = "All downloads complete!";
        }
        else
        {
            populateStatusMessage = "Download stopped";
            currentNamePreview = "Download cancelled";
        }
       
        totalAssets = 0;
        currentAssetIndex = 0;
        Repaint();
        
        EditorCoroutineUtility.StartCoroutineOwnerless(WaitForSeconds(
            newValue => populateStatusMessage = newValue,
            "Ready to Populate",
            3f
        ));

        isDownloading = false;
        shouldStopDownload = false;
    }
    
    private IEnumerator CleanUpDeletedAssets(List<Line> lines)
    {
        HashSet<string> expectedAssets = new HashSet<string>(lines.Select(line => line.assetName));
        string[] actualAssetPaths = AssetUtilities.GetAllAssetPathsInFolder("Assets/Art");
        actualAssetPaths = actualAssetPaths.Take(actualAssetPaths.Length - 1).ToArray();

        foreach (string assetPath in actualAssetPaths)
        {
            string fileName = Path.GetFileName(assetPath);

            if (!expectedAssets.Contains(fileName))
            {
                currentNamePreview = $"Deleting orphaned asset: {fileName}";
                Debug.Log($"Deleting orphaned asset: {fileName}");
                AssetDatabase.DeleteAsset(assetPath);
                yield return null;
            }
        }

        AssetDatabase.Refresh();
    }
   
    private IEnumerator WaitForSeconds(Action<string> setter, string textToSet, float delay)
    {
        yield return new WaitForSeconds(delay);
        setter(textToSet);
    }
    
    // ---------- CALLBACK HANDLERS ----------
    private void OnRedownloadAsset(Line line)
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(
            downloader.DownloadAndSave(
                line.assetName,
                line.assetDownloadLink,
                "Assets/Art" + line.assetPath,
                msg => { },
                trackProgress: true
            )
        );
    }

    private void OnPopulateAssets()
    {
        try
        {
            string csvText = AssetDatabase.LoadAssetAtPath<TextAsset>(config.GetTargetCsvPath()).text;
            
            if (string.IsNullOrEmpty(csvText))
            {
                Debug.LogError("CSV file is empty.");
                return;
            }
            
            string[] parsedArray = ParserUtilities.ParseCSVText(csvText);
            
            if (parsedArray == null || parsedArray.Length == 0)
            {
                Debug.LogError("CSV contains no valid data.");
                return;
            }
            
            EvaluateEntries(parsedArray);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error populating assets: {e.Message}");
        }
    }
}