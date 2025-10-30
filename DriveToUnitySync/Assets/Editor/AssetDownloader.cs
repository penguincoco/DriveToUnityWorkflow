using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;
using UnityEditor;

public class AssetDownloader
{
    private EditorWindow window;
    
    public Dictionary<string, float> DownloadProgress { get; private set; } = new Dictionary<string, float>();
    public HashSet<string> CurrentlyDownloading { get; private set; } = new HashSet<string>();

    public bool IsDownloading { get; private set; } = false;
    private bool shouldStop = false;

    public AssetDownloader(EditorWindow window)
    {
        this.window = window;
    }

    public void StopAll() => shouldStop = true;
    
    //Downloading the CSV (path is the download link) 
    public IEnumerator DownloadCSV(string savePath, string path, Action<string> onStatusUpdate)
    {
        //lowkey this onStatusUpdate is getting eaten up because the sync itself is pretty fast but that's fine lol
        onStatusUpdate?.Invoke("syncing...");
        
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                onStatusUpdate?.Invoke("Failed to fetch sheet: " + www.error);
            else
            {
                File.WriteAllText(savePath, www.downloadHandler.text);
                //Debug.Log("Synced successfully");
                onStatusUpdate?.Invoke("Synced successfully!");
                window.Repaint();
                AssetDatabase.Refresh();
                //LoadCSVData();
                /*
                EditorCoroutineUtility.StartCoroutineOwnerless(WaitForSeconds(
                    newValue => statusMessage = newValue,
                    "Ready to Sync",
                    3f
                )); */
            }
        }
    }
    
    //Downloading assets
    public IEnumerator DownloadAndSave(string fileName, string sourcePath, string destinationPath, Action<string> onStatusUpdate, bool trackProgress = false)
    {
        // ---------- for redownloading individual assets, show the progress bar as it redownloads ----------
        if (trackProgress)
        {
            CurrentlyDownloading.Add(fileName);
            DownloadProgress[fileName] = 0f;
        }
        // ---------- end ----------
    
        onStatusUpdate?.Invoke($"Downloading {fileName}...");

        string extension = Path.GetExtension(fileName).ToLower();
        bool isImage = extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        UnityWebRequest uwr = isImage ? UnityWebRequestTexture.GetTexture(sourcePath) : UnityWebRequest.Get(sourcePath);
        
        using (uwr)
        {
            UnityWebRequestAsyncOperation operation = uwr.SendWebRequest();
        
            // ---------- for redownloading individual assets, show the progress bar as it redownloads ----------
            if (trackProgress)
            {
                while (!operation.isDone)
                {
                    DownloadProgress[fileName] = uwr.downloadProgress;
                    window.Repaint();
                    yield return null;
                }
                DownloadProgress[fileName] = 1f;
                window.Repaint();
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
            CurrentlyDownloading.Remove(fileName);
            DownloadProgress.Remove(fileName);
            window.Repaint();
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

                if (savePath.Contains("Animation"))
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                else
                    importer.spriteImportMode = SpriteImportMode.Single;
                
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                /*
                importer.textureType = this.textureType;
                importer.spriteImportMode = this.spriteImportMode;  //potential TODO: maybe make this dynamic based off of a naming convention? it's hard to set a standard for all sprites...
                importer.spritePixelsPerUnit = this.pixelsPerUnit;
                importer.alphaIsTransparency = this.alphaIsTransparency;
                importer.filterMode = this.filterMode;
                importer.textureCompression = this.compressionType; */

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