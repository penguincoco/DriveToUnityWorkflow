using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles all UI drawing logic for the ImageSyncWindow
/// </summary>
public class UIPainter
{
    private ImageSyncWindow window;
    
    private Vector2 csvScrollPosition;
    private Vector2 scrollPos;
    private bool showCsvPreview = false;
    private bool showSpriteSettingEditor = false;

    // Reference to the window's state

    // Configuration data
    private string folderId;
    private string appsScriptUrl;
    private string sourceCsv;
    private string targetCsvPath;
    private TextAsset targetCSV;
    
    public string FolderId => folderId;
    public string AppsScriptUrl => appsScriptUrl;
    public string SourceCsv => sourceCsv;
    public string TargetCsvPath => targetCsvPath;
    public TextAsset TargetCSV => targetCSV;

    public UIPainter(ImageSyncWindow window) => this.window = window;

    public void UpdateConfiguration(
        string folderId,
        string appsScriptUrl,
        string sourceCsv,
        TextAsset targetCsv,
        string targetCsvPath)
    {
        this.folderId = folderId;
        this.appsScriptUrl = appsScriptUrl;
        this.sourceCsv = sourceCsv;
        this.targetCSV = targetCsv;
        this.targetCsvPath = targetCsvPath;
    }

    public bool IsValid() =>
        !string.IsNullOrEmpty(folderId) &&
        !string.IsNullOrEmpty(appsScriptUrl) &&
        !string.IsNullOrEmpty(sourceCsv) &&
        !string.IsNullOrEmpty(targetCsvPath);

    public Vector2 ScrollPos
    {
        get => scrollPos;
        set => scrollPos = value;
    }

    public void DrawMainUI(float width, float height)
    {
        //GUILayout.Label("Drive Image Sync", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(width), GUILayout.Height(height - 20));
    }

    public void EndMainUI() => EditorGUILayout.EndScrollView();

    public void DrawSyncConfigurationFields(
        SerializedProperty propFolderId,
        SerializedProperty propAppsScriptURL,
        SerializedProperty propSourceCSV,
        SerializedProperty propTargetCSV)
    {
        // ---------- For Running the Apps Script and getting the most up-to-date CSV! ----------
        //get the Wep App URL (apps script URL) and the source CSV (the published Google Sheet)
        EditorGUILayout.PropertyField(
            propFolderId,
            new GUIContent("Folder ID", "Copy the folder ID from Google Drive. The folder ID is the string of numbers and letters after /folders/ and ending with a ? (not inclusive of the leading / or ?. Example folder ID: 1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7.")
        );
        EditorGUILayout.PropertyField(propAppsScriptURL, new GUIContent("Apps Script Link"));
        EditorGUILayout.PropertyField(propSourceCSV, new GUIContent("Read from Link"));
        EditorGUILayout.PropertyField(propTargetCSV, new GUIContent("Target CSV"));
        // ---------- end ----------
    }

    public void DrawSyncButton(Action onRunAppsScript)
    {
        if (IsValid())
        {
            if (GUILayout.Button("Run Apps Script"))
                onRunAppsScript();
        }
        else
            EditorGUILayout.HelpBox("Assign an Apps Script web app link, folder ID, source CSV link and target CSV.", MessageType.Warning);
    }

    public void DrawStatusMessage(string statusMessage)
    {
        GUILayout.Label($"Sync Status: {statusMessage}");
    }

    public void DrawCSVPreviewSection(
        List<Line> csvLines,
        string targetCsvPath,
        HashSet<string> currentlyDownloading,
        Dictionary<string, float> downloadProgress,
        Action onRefresh,
        Action<Line> onRedownload)
    {
        GUILayout.Label("CSV Preview", EditorStyles.boldLabel);

        showCsvPreview = EditorGUILayout.Foldout(showCsvPreview, $"Show CSV Contents ({csvLines.Count} assets)", true);

        if (showCsvPreview)
        {
            if (csvLines.Count > 0)
            {
                EditorGUILayout.HelpBox($"TotalAssets: {csvLines.Count}", MessageType.Info);
                csvScrollPosition = EditorGUILayout.BeginScrollView(csvScrollPosition, GUILayout.Height(300));

                DrawCSVHeader();

                foreach (Line line in csvLines)
                {
                    DrawCSVRow(line, currentlyDownloading, downloadProgress, onRedownload);
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No CSV data loaded. Run Apps Script and sync first.", MessageType.Warning);
            }

            if (GUILayout.Button("Refresh CSV Preview"))
                onRefresh();
        }
    }

    private void DrawCSVHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Asset Name", EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.Label("Path", EditorStyles.boldLabel, GUILayout.Width(150));
        GUILayout.Label("Download Link", EditorStyles.boldLabel, GUILayout.Width(150));
        GUILayout.Label("Redownload Asset", EditorStyles.boldLabel, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCSVRow(
        Line line,
        HashSet<string> currentlyDownloading,
        Dictionary<string, float> downloadProgress,
        Action<Line> onRedownload)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(line.assetName, GUILayout.Width(200));
        GUILayout.Label(line.assetPath, GUILayout.Width(150));

        if (GUILayout.Button("Copy Link", GUILayout.Width(80)))
            EditorGUIUtility.systemCopyBuffer = line.assetDownloadLink;

        GUILayout.Space(70);

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
                onRedownload(line);
        }

        EditorGUILayout.EndHorizontal();
    }

    public void DrawPreviewSection(
        int totalAssets,
        int currentAssetIndex,
        string currentNamePreview,
        Texture2D currentTexturePreview)
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
            DrawTexturePreview(currentTexturePreview);
        }
        else if (totalAssets > 0)
        {
            GUILayout.Label("Loading preview...", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawTexturePreview(Texture2D texture)
    {
        GUILayout.Space(10);

        float maxSize = 256f;
        float width = texture.width;
        float height = texture.height;
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

        EditorGUI.DrawPreviewTexture(previewRect, texture);
    }

    public void DrawPopulateButton(
        bool hasTargetCSV,
        bool isDownloading,
        Action onPopulate,
        Action onStop)
    {
        if (GUILayout.Button("Populate Assets"))
        {
            if (!hasTargetCSV)
            {
                Debug.LogError("No target CSV assigned.");
                return;
            }

            onPopulate();
        }

        if (isDownloading)
        {
            if (GUILayout.Button("Stop Download"))
                onStop();
        }
    }

    public void DrawSpriteSettings(
        FilterMode filterMode,
        TextureImporterType textureType,
        int pixelsPerUnit,
        TextureImporterCompression compressionType,
        SpriteImportMode spriteImportMode,
        SerializedProperty propTextureType,
        SerializedProperty propPixelsPerUnit,
        SerializedProperty propFilterMode,
        SerializedProperty propCompressionType,
        SerializedProperty propSpriteImportMode)
    {
        DrawLine();
        GUILayout.Label("Sprite Settings", EditorStyles.boldLabel);
        GUILayout.Label("These are the sprite settings new sprites will download with/existing Sprites will overwrite with.");

        // ---------- Settings ----------
        GUILayout.Label($"Current Texture Type is: {textureType}");
        GUILayout.Label($"Current Pixels Per Unit is: {pixelsPerUnit}");
        GUILayout.Label($"Current Filter Mode is: {filterMode}");
        GUILayout.Label($"Current Compression Type is: {compressionType}");
        GUILayout.Label($"Current Sprite Import Mode is: {spriteImportMode}");

        showSpriteSettingEditor = EditorGUILayout.Foldout(showSpriteSettingEditor, "Change Sprite Settings", true);

        if (showSpriteSettingEditor)
        {
            //background box
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 5, 5);

            GUILayout.BeginVertical(boxStyle);

            // ---------- sprite settings ----------
            EditorGUILayout.PropertyField(propTextureType);
            EditorGUILayout.PropertyField(propPixelsPerUnit);
            propPixelsPerUnit.intValue = propPixelsPerUnit.intValue.AtLeast(1).AtMost(100);
            EditorGUILayout.PropertyField(propFilterMode);
            EditorGUILayout.PropertyField(propCompressionType);
            EditorGUILayout.PropertyField(propSpriteImportMode);
            // ---------- end ----------

            GUILayout.EndVertical();
        }
    }

    public void DrawLine()
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(1 + 10));
        r.height = 1;
        r.y += 5;
        r.x -= 2;
        r.width += 6;

        EditorGUI.DrawRect(r, Color.grey);
    }
}