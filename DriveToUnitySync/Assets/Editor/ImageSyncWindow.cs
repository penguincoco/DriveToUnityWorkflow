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

/*
 * This script is for the Image Sync Editor Window. It syncs Images and other media from a Folder in Drive to Unity.
 *
 * Major coding logic flow: 
 * Logic flow: SyncSheet() runs the Apps Script which reads the folders in Drive and populates a Google Spreadsheet. When Apps Script is completed, it calls SyncWithGoogleSheets() to sync the
 * Unity-side .csv with the Google Sheet
 */

public class ImageSyncWindow : EditorWindow
{
    private SerializedObject so;
    
    //URLS/needs for Apps Script
    [SerializeField] private string folderId;
    [SerializeField] private string appsScriptURL = "";
    [SerializeField] private string sourceCSV = "";
    private string targetCsvPath = "";  //this is the csv in the Unity project that will be mirrored from Google Sheets!
    [SerializeField] private TextAsset targetCSV;

    private SerializedProperty propFolderId;
    private SerializedProperty propAppsScriptURL;
    private SerializedProperty propSourceCSV;
    private SerializedProperty propTargetCSV;

    //status messages
    private string statusMessage = "";
    private string populateStatusMessage = "";

    private Vector2 scrollPos;
    
    //Preview: downloading/populating assets 
    private Texture2D currentTexturePreview;
    private string currentNamePreview = "";
    private int totalAssets = 0;
    private int currentAssetIndex = 0;
    private Vector2 scollPosition;
    private bool isDownloading = false;
    private bool shouldStopDownload = false;
    private List<EditorCoroutine> activeCoroutines = new List<EditorCoroutine>();
    
    //Preview: CSV contents 
    private Vector2 csvScrollPosition;
    private bool showCsvPreview = false;
    List<Line_Asset> csvLines = new List<Line_Asset>();
    private Dictionary<string, float> downloadProgress = new Dictionary<string, float>();
    private HashSet<string> currentlyDownloading = new HashSet<string>();
    
    //For Sprite setting selection
    [SerializeField] private FilterMode filterMode = FilterMode.Point;
    [SerializeField] private TextureImporterType textureType = TextureImporterType.Default;
    [SerializeField] private  int pixelsPerUnit = 100;
    
    private SerializedProperty propPixelsPerUnit;
    private SerializedProperty propFilterMode;
    private SerializedProperty propTextureType;
    private bool showSpriteSettingEditor = false;
    
    [MenuItem("Tools/Drive Image Sync")]
    public static void ShowWindow() => GetWindow<ImageSyncWindow>("Drive Image Sync");

    private void OnEnable()
    { 
        so = new SerializedObject(this);
        propFolderId = so.FindProperty("folderId");
        propAppsScriptURL = so.FindProperty("appsScriptURL");
        propSourceCSV = so.FindProperty("sourceCSV");
        propTargetCSV = so.FindProperty("targetCSV");
        
        propPixelsPerUnit = so.FindProperty("pixelsPerUnit");
        propFilterMode = so.FindProperty("filterMode");
        propTextureType = so.FindProperty("textureType");
    } 

    void OnGUI()
    {
        GUILayout.Label("Drive Image Sync", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width), GUILayout.Height(position.height - 20));
        
        // ---------- Serialization overhead ----------
        so.Update();
        
        // ---------- For Running the Apps Script and getting the most up-to-date CSV! ----------
        //get the Wep App URL (apps script URL) and the source CSV (the published Google Sheet)
        EditorGUILayout.PropertyField(
            propFolderId,
            new GUIContent("Folder ID", "Copy the folder ID from Google Drive. The folder ID is the string of numbers and letters after /folders/ and ending with a ? (not inclusive of the leading / or ?. Example folder ID: 1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7.")
        );
        EditorGUILayout.PropertyField(propAppsScriptURL, new GUIContent("Apps Script Link"));
        EditorGUILayout.PropertyField(propSourceCSV, new GUIContent("Read from Link"));
        EditorGUILayout.PropertyField(propTargetCSV, new GUIContent("Target CSV"));
    
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
        GUILayout.Space(25);
        
        // ---------- Selecting settings ---------- 
        //WIP feature!
        DrawLine();
        GUILayout.Label("Everything below this line is temp/WIP feature!", EditorStyles.boldLabel);
        GUILayout.Label("Sprite Settings", EditorStyles.boldLabel);
        ShowSpriteSettings();

        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }

    private void ShowSpriteSettings()
    {
        GUILayout.Label($"Current filter mode is {filterMode}");
       
        showSpriteSettingEditor = EditorGUILayout.Foldout(showSpriteSettingEditor,  "Change Sprite Settings", true);

        if (showSpriteSettingEditor)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 5, 5);

            GUILayout.BeginVertical(boxStyle);
            
            //sprite settings
            EditorGUILayout.PropertyField(propFilterMode);
            EditorGUILayout.PropertyField(propTextureType);
            EditorGUILayout.PropertyField(propPixelsPerUnit);
            propPixelsPerUnit.intValue = propPixelsPerUnit.intValue.AtLeast(1).AtMost(100);

            GUILayout.EndVertical();
        }
    }

    private void DrawLine()
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(1 + 10));
        r.height = 1;
        r.y += 5;
        r.x -= 2; 
        r.width += 6; 
        
        EditorGUI.DrawRect(r, Color.grey);
    }
    
    // ---------- MAIN FLOW FUNCTIONS ----------
    private void SyncSheet()
    {
        if (!string.IsNullOrEmpty(folderId) && !string.IsNullOrEmpty(appsScriptURL) && !string.IsNullOrEmpty(sourceCSV) && !string.IsNullOrEmpty(targetCsvPath))
        {
            if (GUILayout.Button("Run Apps Script"))
                RunAppsScriptWrapper();
        }
        //show error message and don't let user run the Apps Script if they are missing a deploy link, source CSV or target csv
        else
            EditorGUILayout.HelpBox("Assign an Apps Script web app link, folder ID, source CSV link and target CSV.", MessageType.Warning);
    }
    
    private void RunAppsScriptWrapper() => EditorCoroutineUtility.StartCoroutineOwnerless(RunAppsScript(appsScriptURL, msg => statusMessage = msg));
    private void SyncWithGoogleSheets() => EditorCoroutineUtility.StartCoroutineOwnerless(DownloadCSV(targetCsvPath, sourceCSV,
            msg => statusMessage = msg));
    
    private void PopulateAssets()
    {
        if (GUILayout.Button("Populate Assets"))
        {
            if (targetCSV == null || string.IsNullOrEmpty(targetCsvPath))
            {
                Debug.LogError("No target CSV assigned.");
                return;
            }
        
            // This try-catch handles an edge case--you assign a Target CSV that is totally empty and try to open the "Preview CSV" menu. It will throw 
            // an error :')))
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

        if (isDownloading)
        {
            if (GUILayout.Button("Stop Download"))
            {
                StopAllDownloads();
            }
        }
    }

    private void StopAllDownloads()
    {
        shouldStopDownload = true;
        
        foreach (EditorCoroutine coroutine in activeCoroutines)
        {
            if (coroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(coroutine);   
            }
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
        currentNamePreview = "";

        Debug.Log("all downloads stopped");
        Repaint();
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

        //in case the user inputs some kind of link, not just the ID
        string cleanedId = AppsScriptUtilities.GetFolderID(folderId);
    
        // Only update if it's valid and different
        if (!string.IsNullOrEmpty(cleanedId) && cleanedId != folderId)
        {
            folderId = cleanedId;
            so.Update();
            so.FindProperty("folderId").stringValue = folderId;
            so.ApplyModifiedProperties();
        }
    
        string urlWithParams = url + "?folderId=" + folderId;
        
        using (UnityWebRequest appsScriptRequest = UnityWebRequest.Get(urlWithParams))
        {
            appsScriptRequest.timeout = 60;
        
            yield return appsScriptRequest.SendWebRequest();

            Debug.Log($"Response Code: {appsScriptRequest.responseCode}");

            if (appsScriptRequest.result != UnityWebRequest.Result.Success)
                Debug.LogError($"Failed to run Apps Script: {appsScriptRequest.error}");
            else
            {
                string responseText = appsScriptRequest.downloadHandler.text;
                Debug.Log($"Full Response: {responseText}");
            
                AppsScriptResponse response = JsonUtility.FromJson<AppsScriptResponse>(responseText);
            
                //request could succeed but Apps Script might not run correctly, error, e.g. invalid folder ID 
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
    
    // ---------- EDITOR PREVIEW FUNCTIONS ----------
    private void DrawCSVPreviewSection()
    {
        GUILayout.Label("CSV Preview", EditorStyles.boldLabel);
        
        if (string.IsNullOrEmpty(targetCsvPath) && csvLines.Count > 0)
            csvLines.Clear();
        
        showCsvPreview = EditorGUILayout.Foldout(showCsvPreview,  $"Show CSV Contents ({csvLines.Count} assets)", true);

        if (showCsvPreview)
        {
            if (csvLines.Count == 0 && File.Exists(targetCsvPath))
                LoadCSVData();

            if (csvLines.Count > 0)
            {
                EditorGUILayout.HelpBox($"TotalAssets: {csvLines.Count}", MessageType.Info);
                csvScrollPosition = EditorGUILayout.BeginScrollView(csvScrollPosition, GUILayout.Height(300));
                
                //visual header
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label("Asset Name", EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label("Path", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label("Download Link", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label("Redownload Asset", EditorStyles.boldLabel, GUILayout.Width(150));
                
                EditorGUILayout.EndHorizontal();

                foreach (Line_Asset line in csvLines)
                {
                    EditorGUILayout.BeginHorizontal("box");

                    GUILayout.Label(line.assetName, GUILayout.Width(200));
                    GUILayout.Label(line.assetPath, GUILayout.Width(150));
                    
                    if (GUILayout.Button("Copy Link", GUILayout.Width(80)))
                        EditorGUIUtility.systemCopyBuffer = line.assetDownloadLink;
                    
                    GUILayout.Space(70);
                    // ---------- Showing progress bar when redownloading an asset ----------
                    if (currentlyDownloading.Contains(line.assetName))
                    {
                        float progress = downloadProgress.ContainsKey(line.assetName) 
                            ? downloadProgress[line.assetName] 
                            : 0f;
                        EditorGUI.ProgressBar(
                            EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(100)), 
                            progress, 
                            $"{(progress * 100):F0}%"
                        );
                    }
                    else
                    {
                        if (GUILayout.Button("Redownload", GUILayout.Width(100)))
                        {
                            EditorCoroutineUtility.StartCoroutineOwnerless(
                                DownloadAndSave(
                                    line.assetName,
                                    line.assetDownloadLink,
                                    "Assets/Art" + line.assetPath,
                                    msg => { }, // Empty status update since we're showing progress bar
                                    trackProgress: true // Enable progress tracking!
                                )
                            );
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            else
                EditorGUILayout.HelpBox("No CSV data loaded. Run Apps Script and sync first.", MessageType.Warning);

            if (GUILayout.Button("Refresh CSV Preview"))
                LoadCSVData();
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

    /*Helper function: This downloads all assets from the CSV, there's an individual DownloadAndSave function that actually
    * does the download for each asset
    */
    private IEnumerator DownloadAllAssets(List<Line_Asset> lines)
    {
        isDownloading = true;
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

        if (shouldStopDownload == false)
        {
            yield return CleanUpDeletedAssets(lines);
            populateStatusMessage = "Populated all assets";
            
            // clear preview 
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
    
    //for handling assets that were removed from Drive, but still exist in the Unity project 
    private IEnumerator CleanUpDeletedAssets(List<Line_Asset> lines)
    {
        HashSet<string> expectedAssets = new HashSet<string>(lines.Select(line => line.assetName));
        //TODO: Make this folder assignable via the Window, it's hardcoded right now! :D 
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
 
    //Downloading the CSV (path is the download link) 
    private IEnumerator DownloadCSV(string savePath, string path, Action<string> onStatusUpdate)
    {
        //Debug.Log("Downloading CSV");
        //lowkey this onStatusUpdate is getting eaten up because the sync itself is pretty fast but that's fine lol
        onStatusUpdate("syncing...");
        
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                onStatusUpdate("Failed to fetch sheet: " + www.error);
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
    
    //Downloading assets
    private IEnumerator DownloadAndSave(string fileName, string sourcePath, string destinationPath, Action<string> onStatusUpdate, bool trackProgress = false)
    {
        // ---------- for redownloading individual assets, show the progress bar as it redownloads ----------
        if (trackProgress)
        {
            currentlyDownloading.Add(fileName);
            downloadProgress[fileName] = 0f;
        }
        // ---------- end ----------
    
        onStatusUpdate($"Downloading {fileName}...");

        string extension = Path.GetExtension(fileName).ToLower();
        bool isImage = extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        UnityWebRequest uwr = isImage ? UnityWebRequestTexture.GetTexture(sourcePath) : UnityWebRequest.Get(sourcePath);
        
        /*
        if (isImage)
            uwr = UnityWebRequestTexture.GetTexture(sourcePath);
        else
            uwr = UnityWebRequest.Get(sourcePath);
        */
        using (uwr)
        {
            UnityWebRequestAsyncOperation operation = uwr.SendWebRequest();
        
            // ---------- for redownloading individual assets, show the progress bar as it redownloads ----------
            if (trackProgress)
            {
                while (!operation.isDone)
                {
                    downloadProgress[fileName] = uwr.downloadProgress;
                    Repaint();
                    yield return null;
                }
                downloadProgress[fileName] = 1f;
                Repaint();
            }
            else
                yield return operation;
            // ---------- end ----------

            if (uwr.result != UnityWebRequest.Result.Success)
                Debug.LogError("Failed to download: " + uwr.error);
            else
            {
                if (!Directory.Exists(destinationPath))
                    Directory.CreateDirectory(destinationPath);
            
                string savePath = Path.Combine(destinationPath, fileName);

                if (isImage)
                    DownloadImage(savePath, uwr);
                else
                    DownloadAsset(savePath, uwr);
            }
        }
    
        // ---------- for redownloading individual assets, show the progress bar as it redownloads ----------
        if (trackProgress)
        {
            yield return new WaitForSeconds(0.5f);
            currentlyDownloading.Remove(fileName);
            downloadProgress.Remove(fileName);
            Repaint();
        }
        // ---------- end ----------
    }

    // ---------- ASSET DOWNLOAD HELPER FUNCTIONS ----------
    //for downloading Images (.png, .jpeg, .jpg) 
    private void DownloadImage(string savePath, UnityWebRequest uwr)
    {
        Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
        byte[] pngData = texture.EncodeToPNG();

        File.WriteAllBytes(savePath, pngData); //update if an asset already exists

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

        //wait for the import to complete, could throw errors if you aren't done with the import and try to set the mode
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
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = this.filterMode;

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
    
    // ---------- HELPER FUNCTIONS ----------
    private IEnumerator WaitForSeconds(Action<string> setter, string textToSet, float delay)
    {
        yield return new WaitForSeconds(delay);
        setter(textToSet);
    }
    
    
    
    
    
    
    
    
    // ---------- Archived functions ----------
    private void SelectParentFolder()
    {
        if (GUILayout.Button("Select Parent Asset Folder"))
        {
            string folder = EditorUtility.OpenFolderPanel("Parent Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                string projectPath = Application.dataPath; // e.g., /Users/username/ProjectName/Assets
                if (folder.StartsWith(projectPath))
                    folder = "Assets" + folder.Substring(projectPath.Length);
                //assetsParentFolder = folder;
            }
        }
        //GUILayout.Label("Parent Folder: " + assetsParentFolder);
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