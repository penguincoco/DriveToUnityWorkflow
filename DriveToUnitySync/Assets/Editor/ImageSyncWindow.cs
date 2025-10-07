using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Linq;
using UnityEngine.Serialization;

public class ImageSyncWindow : EditorWindow
{
    private SerializedObject so;
    [SerializeField] private string appsScriptURL = "";
    
    [SerializeField] private string sourceCSV =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vTyF96_q1B9ELrj8sFhfKB6hOuCM8EAS5zrBY7oWQrkPsSnrDMT2RH83jJrbFA1YKMVi1l18kIauX6P/pub?gid=0&single=true&output=csv";

    private string targetCsvPath = "";
    [SerializeField]private TextAsset targetCSV;

    private string statusMessage = "";
    private string populateStatusMessage = "";

    private Vector2 scrollPos;
    
    //image preview
    private Texture2D currentTexturePreview;
    private string currentNamePreview = "";
    private int totalAssets = 0;
    private int currentAssetIndex = 0;
    private Vector2 scollPosition;
    
    //CSV preview
    private Vector2 csvScrollPosition;
    private bool showCsvPreview = false;
    
    //temp testing!
    List<Line_Asset> csvLines = new List<Line_Asset>();
    
    [MenuItem("Tools/Drive Image Sync")]
    public static void ShowWindow()
    {
        GetWindow<ImageSyncWindow>("Drive Image Sync");
    }
    
    private void OnEnable() => so = new SerializedObject(this);

    void OnGUI()
    {
        GUILayout.Label("Drive Image Sync");
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width), GUILayout.Height(position.height - 20));
        
        // ---------- Serialization overhead ----------
        so.Update();
        
        // ---------- For Running the Apps Script and getting the most up-to-date CSV! ----------
        //get the Wep App URL (apps script URL) and the source CSV (the published Google Sheet)
        EditorGUILayout.PropertyField(so.FindProperty("appsScriptURL"), new GUIContent("Apps Script Link:"));
        EditorGUILayout.PropertyField(so.FindProperty("sourceCSV"), new GUIContent("Read from Link:"));
        EditorGUILayout.PropertyField(so.FindProperty("targetCSV"), new GUIContent("Target CSV"));
    
        so.ApplyModifiedProperties();
    
        targetCsvPath = AssetDatabase.GetAssetPath(targetCSV);
        // ---------- end ----------

        GUILayout.Space(15);
        DrawCSVPreviewSection();    //draw CSV preview
        GUILayout.Space(15);
        GUILayout.Label($"Sync Status: {statusMessage}");
        SyncSheet();
        
        // ---------- For Downloading Assets to Unity side ----------
        //SelectParentFolder();
        GUILayout.Space(25);
        DrawPreviewSection();
        PopulateAssets();
        
        EditorGUILayout.EndScrollView();
    }
    
    /*
     * Logic flow: Sync Sheet runs the Apps Script, when Apps Script is completed, it calls SyncWithGoogleSheets()
     */
    private void SyncSheet()
    {
        if (!string.IsNullOrEmpty(appsScriptURL) && !string.IsNullOrEmpty(sourceCSV) && !string.IsNullOrEmpty(targetCsvPath))
        {
            if (GUILayout.Button("Run Apps Script"))
                RunAppsScriptWrapper();
        }
        //show error message and don't let user run the Apps Script if they are missing a deploy link, source CSV or target csv
        else
            EditorGUILayout.HelpBox("Assign an Apps Script web app link, source CSV link and target CSV.", MessageType.Warning);
    }
    
    private void RunAppsScriptWrapper() => EditorCoroutineUtility.StartCoroutineOwnerless(RunAppsScript(appsScriptURL, msg => statusMessage = msg));
    private void SyncWithGoogleSheets() => EditorCoroutineUtility.StartCoroutineOwnerless(DownloadCSV(targetCsvPath, sourceCSV,
            msg => statusMessage = msg));
        
    private void SelectParentFolder()
    {
        if (GUILayout.Button("Select Parent Asset Folder"))
        {
            string folder = EditorUtility.OpenFolderPanel("Parent Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                string projectPath = Application.dataPath; // e.g., /Users/Sammy/ProjectName/Assets
                if (folder.StartsWith(projectPath))
                    folder = "Assets" + folder.Substring(projectPath.Length);
                //assetsParentFolder = folder;
            }
        }
        
        //GUILayout.Label("Parent Folder: " + assetsParentFolder);
    }

    private void DrawCSVPreviewSection()
    {
        GUILayout.Label("CSV Preview", EditorStyles.boldLabel);
        
        if (string.IsNullOrEmpty(targetCsvPath) && csvLines.Count > 0)
            csvLines.Clear();
        
        //collapsible menu thingy for showing the csv preview
        showCsvPreview = EditorGUILayout.Foldout(showCsvPreview,  $"Show CSV Contents ({csvLines.Count} assets)", true);

        if (showCsvPreview)
        {
            if (csvLines.Count == 0 && File.Exists(targetCsvPath))
                LoadCSVData();

            if (csvLines.Count > 0)
            {
                EditorGUILayout.HelpBox($"TotalAssets: {csvLines.Count}", MessageType.Info);
                csvScrollPosition = EditorGUILayout.BeginScrollView(csvScrollPosition, GUILayout.Height(300));
                
                //Header 
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label("Asset Name", EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label("Path", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label("Download Link", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                foreach (Line_Asset line in csvLines)
                {
                    EditorGUILayout.BeginHorizontal("box");

                    GUILayout.Label(line.assetName, GUILayout.Width(300));
                    GUILayout.Label(line.assetPath, GUILayout.Width(150));
                    
                    if (GUILayout.Button("Copy Link", GUILayout.Width(80)))
                        EditorGUIUtility.systemCopyBuffer = line.assetDownloadLink;
                        //Debug.Log($"Copied link for: {line.assetName}");;
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView(); // MOVED INSIDE THE if (csvLines.Count > 0) BLOCK
            }
            else
                EditorGUILayout.HelpBox("No CSV data loaded. Run Apps Script and sync first.", MessageType.Warning);

            //the csv preview doesn't update automatically, press this button to refresh it 
            if (GUILayout.Button("Refresh CSV Preview"))
                LoadCSVData();
        }
    }

    private void LoadCSVData()
    {
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
                factory: parts => new Line_Asset(parts[0], parts[1], parts[2])
            );
        
            Repaint();
        }
        catch (Exception e)
        {
            csvLines.Clear();
            Debug.LogError($"Error loading CSV data: {e.Message}");
        }
    }
    
    private void DrawPreviewSection()
    {
        GUILayout.Label("Download Preview", EditorStyles.boldLabel);

        if (totalAssets > 0)
        {
            float progress = totalAssets > 0 ? (float)currentAssetIndex / totalAssets : 0f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress,
                $"Downloading: {currentAssetIndex}/{totalAssets}");

            GUILayout.Space(10);
        }
        
        if (!string.IsNullOrEmpty(currentNamePreview))
            GUILayout.Label($"Current Asset: {currentNamePreview}", EditorStyles.helpBox);

        if (currentTexturePreview != null)
        {
            GUILayout.Space(10);

            float maxSize = 256f;
            float width = currentTexturePreview.width;
            float height = currentTexturePreview.height;
            float scale = Mathf.Min(maxSize / width, maxSize / height);

            if (scale < 1f)
            {
                width *= scale;
                height *= scale;
            }
            
            Rect previewRect = GUILayoutUtility.GetRect(maxSize, height, GUILayout.ExpandWidth(false));
            previewRect.x = (EditorGUIUtility.currentViewWidth - width) / 2;
            previewRect.width = width;
            previewRect.height = height;
            
            EditorGUI.DrawPreviewTexture(previewRect, currentTexturePreview);
        }
        else if (totalAssets > 0)
            GUILayout.Label("Loading preview...", EditorStyles.centeredGreyMiniLabel);
    }
    
    // Replace your PopulateAssets() method with this:
    private void PopulateAssets()
    {
        if (GUILayout.Button("Populate Assets"))
        {
            if (targetCSV == null || string.IsNullOrEmpty(targetCsvPath))
            {
                Debug.LogError("No target CSV assigned.");
                return;
            }
        
            try
            {
                string csvText = AssetDatabase.LoadAssetAtPath<TextAsset>(targetCsvPath).text;
            
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

    private void EvaluateEntries(string[] parsedArray)
    {
        List<Line_Asset> lines = ParserUtilities.ParseLines(
            parsedArray,
            minParts: 3,
            maxParts: 3,
            factory: parts => new Line_Asset(parts[0], parts[1], parts[2])
        );

        this.csvLines = lines;
        Debug.Log(csvLines.Count);

        currentAssetIndex = 0;
        totalAssets = lines.Count;
        currentTexturePreview = null;
        currentNamePreview = "";
        
        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAllAssets(lines));
    }

    private IEnumerator WaitForSeconds(Action<string> setter, string textToSet, float delay)
    {
        yield return new WaitForSeconds(delay);
        setter(textToSet);
    }

    private IEnumerator RunAppsScript(string url, Action<string> onStatusUpdate)
    {
        Debug.Log("Running Apps Script");
        onStatusUpdate("Running Apps Script...");
        //Debug.Log("EXACT URL BEING CALLED: " + url);
        
        using (UnityWebRequest appsScriptRequest = UnityWebRequest.Get(url))
        {
            appsScriptRequest.timeout = 60;
        
            yield return appsScriptRequest.SendWebRequest();

            Debug.Log($"Response Code: {appsScriptRequest.responseCode}");
            //Debug.Log($"Full Response: {appsScriptRequest.downloadHandler.text}");

            if (appsScriptRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to run Apps Script: {appsScriptRequest.error}");
            }
            else
            {
                //Debug.Log("Apps Script successfully run!");
                onStatusUpdate("Apps Script successfully run!");
                SyncWithGoogleSheets();
            }
        }
    }

    private IEnumerator DownloadCSV(string savePath, string path, Action<string> onStatusUpdate)
    {
        //Debug.Log("Downloading CSV");
        //lowkey this onStatusUpdate is getting eaten up because the sync itself is pretty fast but that's fine lol
        onStatusUpdate("syncing...");
        
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                onStatusUpdate("Failed to fetch sheet: " + www.error);
            }
            else
            {
                File.WriteAllText(savePath, www.downloadHandler.text);
                //Debug.Log("Synced successfully");
                onStatusUpdate("Synced successfully!");
                Repaint();
                AssetDatabase.Refresh();
                LoadCSVData();
                EditorCoroutineUtility.StartCoroutineOwnerless(WaitForSeconds(
                    newValue => statusMessage = newValue,
                    "Ready to Sync",
                    3f
                ));
            }
        }
    }

    private IEnumerator DownloadAllAssets(List<Line_Asset> lines)
    {
        populateStatusMessage = "Populating...";

        foreach (Line_Asset line in lines)
        {
            currentAssetIndex++;
            currentNamePreview = line.assetName;
            Repaint();
            
            yield return DownloadAndSave(
                line.assetName,
                line.assetDownloadLink,
                "Assets/Art" + line.assetPath,
                msg => populateStatusMessage = msg
            );
        }

        yield return CleanUpDeletedAssets(lines);
        populateStatusMessage = "Populated all assets";
        
        // clear preview 
        yield return new WaitForSeconds(2f);
        currentTexturePreview = null;
        currentNamePreview = "All downloads complete!";
        totalAssets = 0;
        currentAssetIndex = 0;
        Repaint();
        
        EditorCoroutineUtility.StartCoroutineOwnerless(WaitForSeconds(
            newValue => populateStatusMessage = newValue,
            "Ready to Populate",
            3f
        ));
    }
    
    //for handling assets that were removed from Drive, but still exist in the Unity project 
    private IEnumerator CleanUpDeletedAssets(List<Line_Asset> lines)
    {
        HashSet<string> expectedAssets = new HashSet<string>(lines.Select(line => line.assetName));
        string[] actualAssetPaths = AssetUtilities.GetAllAssetPathsInFolder("Assets/Art");
        actualAssetPaths = actualAssetPaths.Take(actualAssetPaths.Length - 1).ToArray();

        foreach (string assetPath in actualAssetPaths)
        {
            string fileName = Path.GetFileName(assetPath);

            if (!expectedAssets.Contains(fileName))
            {
                Debug.Log($"Deleting orphaned asset: {fileName}");
                AssetDatabase.DeleteAsset(assetPath);
                yield return null;
            }
        }

        AssetDatabase.Refresh();
    }
    
    private IEnumerator DownloadAndSave(string fileName, string sourcePath, string destinationPath, Action<string> onStatusUpdate)
    {
        onStatusUpdate($"Downloading {fileName}...");

        string extension = Path.GetExtension(fileName).ToLower();
        bool isImage = extension == ".png" || extension == ".jpg" || extension == ".jpeg";

        UnityWebRequest uwr;

        if (isImage == true)
            uwr = UnityWebRequestTexture.GetTexture(sourcePath);
        else
            uwr = UnityWebRequest.Get(sourcePath);
        
        using (uwr)
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
                Debug.LogError("Failed to download: " + uwr.error);
            else
            {
                //check if the directory already exists, if not, create one
                if (!Directory.Exists(destinationPath))
                    Directory.CreateDirectory(destinationPath);
                
                string savePath = Path.Combine(destinationPath, fileName);

                if (isImage == true)
                    DownloadImage(savePath, uwr);
                else
                    DownloadAsset(savePath, uwr);
            }
        }
    }

    //for downloading Images (.png, .jpeg, .jpg) 
    private void DownloadImage(string savePath, UnityWebRequest uwr)
    {
        Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
        byte[] pngData = texture.EncodeToPNG();
                    
        File.WriteAllBytes(savePath, pngData); //update if an asset already exists
                    
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

        //wait for the import to complete, could throw errors if you basically aren't done with the import and try to set the mode
        EditorApplication.delayCall += () =>
        {
            //set imported asset to a sprite
            TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode =
                    SpriteImportMode
                        .Single; //potential TODO: make it multiple? or is it possible to determine if it's a spritesheet? maybe a naming convention... hmmm....

                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        };
    }

    //for downloading FBX, PDF, etc. Any non-image file
    private void DownloadAsset(string savePath, UnityWebRequest uwr)
    {
        byte[] fileData = uwr.downloadHandler.data;
        File.WriteAllBytes(savePath, fileData);

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);
    }
}

public class Line_Asset
{
    public string assetName;
    public string assetDownloadLink;
    public string assetPath;

    public Line_Asset(string assetName, string assetDownloadLink, string assetPath)
    {
        this.assetName = assetName;
        this.assetDownloadLink = assetDownloadLink;
        this.assetPath =  GetOutputPath(assetPath);
    }

    private string GetOutputPath(string assetPath)
    {
        string newAssetPath = assetPath.Substring("Art".Length);
        return newAssetPath;
    }
}