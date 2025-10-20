using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
using System;

public static class AppsScriptUtilities 
{
    //https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=drive_link (if you do Share > Copy Link link)
    //https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=sharing (LOL -- if you do Share > Share > Copy Link link)
    /*
     * This is for correcting folder IDs in case the user puts in weird links.
     * This function will spit out the folder ID given a Share Link or Folder Link (see example links above), or return the folder ID if it's already
     * in the correct format.
     */
    public static string GetFolderID(string link)
    {
        if (string.IsNullOrEmpty(link))
            return "";
    
        string extractedId = "";
    
        if (!link.Contains("drive.google.com"))
            extractedId = link.Trim();
        else
        {
            int foldersIndex = link.IndexOf("/folders/");
            if (foldersIndex == -1)
            {
                Debug.LogError("Invalid Drive URL: Missing '/folders/' in link");
                return "";
            }

            int startIndex = foldersIndex + "/folders/".Length;
            int questionMarkIndex = link.IndexOf("?", startIndex);
            int endIndex = questionMarkIndex != -1 ? questionMarkIndex : link.Length;

            extractedId = link.Substring(startIndex, endIndex - startIndex);
        }
    
        //remove any non accepted characters (e.g. / or ?) 
        extractedId = System.Text.RegularExpressions.Regex.Replace(extractedId, @"[^a-zA-Z0-9_-]", "");
    
        if (IsValidFolderId(extractedId))
        {
            //Debug.Log($"Extracted valid ID: {extractedId}");
            return extractedId;
        }
        else
        {
            Debug.LogError($"Invalid folder ID format: {extractedId}");
            return "";
        }
    }

    //validate the ID: needs to have enough characters, and must be alpha numeric values
    private static bool IsValidFolderId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
    
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9_-]{25,35}$");
        return regex.Match(id).Success;
    }
}
