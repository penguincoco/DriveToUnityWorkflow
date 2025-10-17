# Google Drive <img width="32" height="32" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" /> to Unity <img width="32" height="32" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" /> Workflow
This workflow combines Google Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> and Unity Editor code to streamline downloading assets from Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" /> to Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" />. No more needing to manually download and import, it can all be done with a single button click! 

This workflow is best run in bulk, once every week or every two weeks, as some of the processes are slow. 

> [!WARNING]
> You must be connected to the Internet for this workflow to work! 

## Initial Setup 

> <details>
>  <summary>▶️ Watch demo</summary>
>  <video src="https://github.com/user-attachments/assets/f42f311d-7dd5-4809-8642-bad455d0f504" controls width="600"></video>
> </details>

1. In Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" />, open Drive Image Sync under Tools > Drive Image Sync.
  <p align="center">
    <img width="729" height="386" alt="image" src="https://github.com/user-attachments/assets/5562c217-c0c4-4ca6-a3de-c124acdd6984" />
  </p>

2. In Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" />, create a parent folder that you want. Title it whatever you need, having an "Art" parent folder and "2D" and "3D" subfolders is recommended. Upload your assets to their appropriate folders. See example Drive setup [here](<https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=drive_link>).
> [!NOTE]
> The entire folder must be **public**, otherwise Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" /> will throw an error when sending the Web Request. Below is a recommended folder structure.
> ```
> - Art //This folder should be public, if the parent folder is public, all children folders and content will be as well. 
>    |-- 2D
>       | -- UIButtons
>    |-- 3D
>       | -- Flowers
>         | -- etc
>             | -- etc
> ```

3. Create a spreadsheet in Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" />
 and create an Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> by clicking Extensions > Apps Script. This will open the Apps Script IDE in a new tab. (**no text needs to be input on the Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/7738f9c1-cf00-4988-8328-2cacd7f753ca" />, the running the Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> will take care of that!**)

4. In Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" />, navigate to a file called `AssetSync.gs` (Path: `/Assets/AppsScript/AssetSync.gs`). Copy and paste the contents of this script into the Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> and save. 
   
> <details>
>   <summary>Expand to See Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> Code</summary>
>
> ### How this Code works
>
> Starting from the root folder, it will check every sub folder and grab all assets of type: `png`, `jpg`, `jpeg`, `psd`, `pdf`, `fbx`, `obj` (technically, it can download any asset of any type, but these are the ones you should generally be uploading, as this is a tool for syncing art assets!). It will then turn each of those assets into a downloadble link and populate that link, along with the asset path, to the Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" />.
>
> ```
> // doGet is how Unity can trigger running this script!
> function doGet(e) {
>  try {
>     // Check if folderId was provided AND is not empty
>    if (!e.parameter.folderId || e.parameter.folderId.trim() === "") {
>      return ContentService.createTextOutput(JSON.stringify({
>        status: "error",
>        message: "Missing required parameter: folderId"
>      })).setMimeType(ContentService.MimeType.JSON);
>    }
>
>    var folderId = e.parameter.folderId;
>    populate(folderId);
>
>    // return the success response
>    return ContentService.createTextOutput(JSON.stringify({
>      status: "success",
>      message: "Spreadsheet populated successfully"
>    })).setMimeType(ContentService.MimeType.JSON);
>  } catch (error) {
>    return ContentService.createTextOutput(JSON.stringify({
>      status: "error",
>      message: error.toString()
>    })).setMimeType(ContentService.MimeType.JSON);
>  }
> }
>
> function populate(folderId) {
>   var files = getAllPngsInFolder(folderId)
>
>   // Sort by folder path, then by filename
>   files.sort(function(a, b) {
>     if (a.path === b.path) {
>       return a.name.localeCompare(b.name);
>     }
>     return a.path.localeCompare(b.path);
>   });
>
>   var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
>   sheet.clear();
>   sheet.appendRow(["File Name", "Direct Download Link", "Folder Path"]);
>
>   files.forEach(function(fileObj) {
>     sheet.appendRow([fileObj.name, fileObj.link, fileObj.path]);
>   });
> }
>
> //get all pngs in the folder and SUB folders 
> function getAllPngsInFolder(folderId, parentPath) {
>   var folder = DriveApp.getFolderById(folderId);
>   var files = folder.getFiles();
>   var subfolders = folder.getFolders();
>   var results = [];
>   
>   var currentPath = parentPath ? parentPath + "/" + folder.getName() : folder.getName();
>   
>   // Collect PNGs in current folder
>   while (files.hasNext()) {
>     var file = files.next();
>
>     var allowedTypes = ["image/png", "image/jpeg", "application/pdf"]; // etc.
>     var fileName = file.getName().toLowerCase();
>
>     if (allowedTypes.includes(file.getMimeType()) || fileName.endsWith(".fbx") || fileName.endsWith(".obj")|| fileName.endsWith(".psd")) {
>       results.push({
>         name: file.getName(),
>         link: "https://drive.google.com/uc?export=download&id=" + file.getId(),
>         path: currentPath
>       });
>     }
>   }
>   
>   // Recurse into subfolders
>   while (subfolders.hasNext()) {
>     var subfolder = subfolders.next();
>     results = results.concat(getAllPngsInFolder(subfolder.getId(), currentPath));
>   }
>   
>   return results;
> }
>
> function convertDriveLink(url) {
>   if (!url) return "";
>
>   var match = url.match(/\/d\/(.*)\/view/);
>     if (match && match[1]) {
>       return "https://drive.google.com/uc?export=download&id=" + match[1];
>     }
>     
>     return "Invalid link";
> }
> ```
>
> Running this Apps Script will populate the Asset Manager Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" /> to look like this. Note that it has 3 columns: Asset Name, Download Link, File Path, and it will output onto the Sheet by folder first, then file name (alphabetically). 
>
> <p align="center">
>   <img width="624" height="234" alt="image" src="https://github.com/user-attachments/assets/926eb9e1-b770-40b7-baae-59643543205d" />
> </p>
>
> </details>

5. Running the Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> for the first time will require giving permissions manually. Click "Run" at the top. This will open a permissions window. Select all (as this will allow the Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" /> to access your Drive and write to the sheet).
<p align="center">
  <img width="790" height="475" alt="image" src="https://github.com/user-attachments/assets/6a886cb0-4188-4160-b8f8-c2c96b26bdbb" /> <img width="476" height="235" alt="image" src="https://github.com/user-attachments/assets/bc7fad4b-81e2-4d63-b76b-5fec5c834fec" />
</p>

6. Copy and paste your root Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" /> folder ID into "Folder ID". The folder ID is the `string` after "/folders/" until the "?". For example, the link: https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=sharing has a folder ID of `1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7`

> <details>
>  <summary>💡 Engineering Explanation</summary>
> It is recommended to paste in the correct folder ID, but there are several checks in place to grab a valid ID (as long as you have a valid ID somewhere in the string, it will work).  
> If you enter an invalid ID (e.g. `123abc`), an error will be thrown and output to the console.
> </details>

7. At the top right, click Deploy > New Deployment (Web App) and set the access to "Anyone". Copy that link and paste it into "Apps Script Link". 

8. Go back to the Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" /> File > Share > Publish. Copy that link and paste it into "Read from Link" 
<p align="center">
  <img width="528" height="468" alt="image" src="https://github.com/user-attachments/assets/2820b4e2-4b05-4efa-b179-f3f7cd21a9df" />
</p>

9. In Unity, create a new .csv (recommended to call it `AssetList.csv`) and drag and drop that into "Target CSV".

10. At this point, the "Run Apps Script" button should appear. This button will not be visible unless all 3 fields are filled, and a warning will show. Click "Run Apps Script". This action will:

    a. Run the Apps Script, which searches for all .pngs in the Art folder and all of its subfolders, generate a downloadable link, and then populate that to the AssetManager Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" />.
   
    b. Populate `AssetList.csv` (in the Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" /> project, Path: `Assets/Art/AssetList.csv`) with the same data from the AssetManager Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" />. You can preview the assets in the `AssetList.csv` by expanding the CSV Preview.
<p align="center"> 
 <img width="580" height="384" alt="image" src="https://github.com/user-attachments/assets/bd7aae31-9d59-4764-8518-3e08e867aa44" />
</p>

11. Once that process is complete (the editor window will show a status that says "Successfully synced!" or "Ready to Sync" (if 3 seconds have passed), click “Populate Assets”. This action will:
    
    a. Download all .pngs from their downloadable links. The Unity folder structure will mirror the Drive structure under /Assets/Art/__2D. If an asset already exists, it will overwrite the data. If an asset does not already exist, it will create it to the correct folder. (If a folder doesn’t exist, it will also generate the folder)

## Best Practices 


## Demo Setup
There is an example setup attached with this repo. 

[Google Sample Folder](https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=drive_link): This is the folder with example assets that will be downloaded. 

[Example CSV](https://docs.google.com/spreadsheets/d/e/2PACX-1vSD0wsz9X6wcklx6PwozDh9bWJ-1g-GKkbB9Zd5Ekq_O_RSqIBXZS8udugH7XvacHxoiSrvRApw_u9Q/pub?gid=0&single=true&output=csv): the example .csv Unity will read from. 

[Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" />](https://script.google.com/macros/s/AKfycbxJPQ-CGCj5lB1aCjvUthByU01F-Wc4iFVIMlKoU7eyKRnbl5Rrx14EVC7KYeUlIP13/exec): The code used to generate downloadable links for all assets in the Google Sample Folder and populate it to the Example .csv. This code (and web app link) are accessible from the Example .csv under Extensions > Apps Script 

### How to Use

1. On Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" />, navigate to [AssetManager](https://docs.google.com/spreadsheets/d/15rIOjOh3fr7UmQMU2SRhSoWyAal-lo5Gpp-CJmURg0o/edit?usp=sharing). 

2. From the AssetManager Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" />, open the Apps Script editor under Extensions > Apps Script. The Apps Script <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/a7401c1b-2667-486c-96f1-dad11738b480" />
 editor will open in a new tab. 
<p align="center">
  <img width="350" height="234" alt="image" src="https://github.com/user-attachments/assets/2f41f98f-92f2-404f-92dd-2de191055e97" />
</p>

3. Click Deploy > Manage Deployments in the top right and copy the Web App link for the latest deployment. 
<p align="center">
  <img width="495" height="332" alt="image" src="https://github.com/user-attachments/assets/9ff34514-b306-4511-ac5c-aafd844e47c3" />
</p>

4. Open the Drive Image Sync Editor under Tools > Drive Image Sync. Paste the link into “Apps Script Link”.
<p align="center">
 <img width="734" height="137" alt="image" src="https://github.com/user-attachments/assets/b8ca88f4-de7b-4e6d-86ae-97c64ae1dbb1" />
</p>

5. On the Asset Manager Google Sheet <img width="12" height="16" alt="image" src="https://github.com/user-attachments/assets/bf7b2a0e-5735-4acf-b49b-4c3c9a840062" />, get the published, downloadable link under File > Share > Publish to Web as a csv. Paste that link into “Read from Link”.
<p align="center">
  <img width="286" height="356" alt="image" src="https://github.com/user-attachments/assets/7d21eb26-1078-47f8-9e5b-692f21fb9c70" />
</p>

6. Assign the `AssetList.csv` TextAsset to "Target CSV".

7. Paste this folder ID into Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" />: `1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7`

8. In Unity <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/d1686289-6d84-4e03-ae1f-eb69a18ee406" />, click “Run Apps Script".
 
9. Once it’s done, click “Populate Assets”. You will see the download progress.

<p align="center">
  <img width="732" height="119" alt="image" src="https://github.com/user-attachments/assets/8987b864-be7d-43a5-98b3-a274cd02a893" />
</p>

## Current Constraints
### For Artists:

1. You can upload new assets and create new folders in Google Drive <img width="16" height="16" alt="image" src="https://github.com/user-attachments/assets/051a0bf6-9ecd-45cc-a6fc-4936b69114a8" />. No problem! Try to come up with a good asset name and don’t change it after. Try to not move assets to other folders, or rename folders as well.

> <details>
>  <summary>💡 Engineering Explanation</summary>
>
> This won’t break the path, it will just create two assets in the Unity project. E.g. you have an asset named “Cat” in Unity. You upload the same .png but renamed it to "Cat-Brown” and still have "Cat" in Drive, the asset will appear in Unity twice with the different names. You can remove the duplicate asset from Unity either by manually removing it, or removing it from Drive. 
> </details>

2. For updating existing assets, replace the existing asset by uploading a file with the exact same name and selecting “Replace existing file”.
> [!WARNING]
> **Do not delete the old one and upload a new one, as this will break the download path.**
<p align="center">
<img width="429" height="245" alt="image" src="https://github.com/user-attachments/assets/86a101e5-ab49-4aff-a8af-9f6914898be1" />
</p>

### For Unity: 

1. If an asset was updated in Drive, you do not need to sync the .csv again. This is because the asset download path is the same (if the asset was replaced as mentioned above). But, for best practice just, never hurts to Sync again :’) 

## Next Features
1. Ability to change Sprite Import settings via the editor window (right now they all download as a Sprite, alpha is transparency, Single Sprite mode.
<p align="center">
  <img width="509" height="429" alt="image" src="https://github.com/user-attachments/assets/ffd4c044-5f74-4f66-8bc6-e49fb2c5e7f5" />
</p>

2. Ability to stop the download/populate

### FAQ 
**Common Errors When Running the Tool**

| Error Code  | Console Output | Debugging Steps |
| ------------- | ------------- | ------------- |
| 0  | Cannot resolve destination host | Not connected to the Internet. Connect to the Internet! 
| 403  | HTTP/1.1 403 Forbidden  | Apps Script does not have the correct permissions (see Step #5 in [Initial Setup](#initial-setup) for steps |

## Code Diagram
<img width="4800" height="2048" alt="DriveToUnityWorkflow" src="https://github.com/user-attachments/assets/5036be9a-1eee-46b4-8b76-f363bf1e1000" />
