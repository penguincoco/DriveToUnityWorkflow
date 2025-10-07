# Drive to Unity Workflow
This workflow combines Google Apps Script and Unity Editor code to streamline downloading assets from Google Drive to Unity. No more needing to manually download all assets from Drive and import to Unity, it can all be done with a single button click! 

## Initial Setup 
1. In Unity, open Drive Image Sync under Tools > Drive Image Sync.
  <img width="656" height="332" alt="image" src="https://github.com/user-attachments/assets/15d82b45-141c-4252-8a56-ae612af7f114" />
2. In Google Drive, create a parent folder that you want. Title it whatever you need, but I recommend having an "Art" parent folder and "2D" and "3D" art subfolders. Upload your assets in the appropriate places.

[example Drive setup](https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=drive_link)
   
Note: the entire folder must be **public**.

```
- Art
    |-- 2D
       | -- UIButtons
    |-- 3D
       | -- Flowers
         | -- etc
             | -- etc
```

3. Create a spreadsheet in Google Drive and create an Apps Script by clicking Extensions > Apps Script. This will open the Apps Script IDE in a new tab. (no text needs to be input on the Google Sheet, the Apps Script sync will take care of that!)
4. Navigate to the Unity project to a file called `AssetSync.gs`. Copy and paste the contents of this script into the Apps Script and save. 
   
 <details>
   <summary>Expand to See Apps Script Code</summary>
  
### How this Code works
Starting from the root folder (which you will need to copy and paste into files (this line is commented with a `REPLACE THIS WITH YOUR OWN ROOT FOLDER ID`)), it will check every sub folder and grab all assets of type: `png`, `jpg`, `jpeg`, `psd`, `pdf`, `fbx`, `obj` (technically, it can download any asset of any type, but these are the ones you should generally be uploading, as this is a tool for syncing art assets!). It will then turn each of those assets into a downloadble link and populate that link, along with the asset path, to the Google Sheet. 
 
 ```
 // doGet is how Unity can trigger running this script!
 function doGet(e) {
   try {
     populate();
     
     // return the success response
     return ContentService.createTextOutput(JSON.stringify({
       status: "success",
       message: "Spreadsheet populated successfully"
     })).setMimeType(ContentService.MimeType.JSON);
   } catch (error) {
     return ContentService.createTextOutput(JSON.stringify({
       status: "error",
       message: error.toString()
     })).setMimeType(ContentService.MimeType.JSON);
   }
 }
 
 function populate() {
   var files = getAllPngsInFolder("1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7"); // REPLACE THIS WITH YOUR OWN ROOT FOLDER ID
 
   // Sort by folder path, then by filename
   files.sort(function(a, b) {
     if (a.path === b.path) {
       return a.name.localeCompare(b.name);
     }
     return a.path.localeCompare(b.path);
   });
 
   var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
   sheet.clear();
   sheet.appendRow(["File Name", "Direct Download Link", "Folder Path"]);
 
   files.forEach(function(fileObj) {
     sheet.appendRow([fileObj.name, fileObj.link, fileObj.path]);
   });
 }
 
 //get all pngs in the folder and SUB folders 
 function getAllPngsInFolder(folderId, parentPath) {
   var folder = DriveApp.getFolderById(folderId);
   var files = folder.getFiles();
   var subfolders = folder.getFolders();
   var results = [];
   
   var currentPath = parentPath ? parentPath + "/" + folder.getName() : folder.getName();
   
   // Collect PNGs in current folder
   while (files.hasNext()) {
     var file = files.next();
 
     var allowedTypes = ["image/png", "image/jpeg", "application/pdf"]; // etc.
     var fileName = file.getName().toLowerCase();
 
     if (allowedTypes.includes(file.getMimeType()) || fileName.endsWith(".fbx") || fileName.endsWith(".obj")|| fileName.endsWith(".psd")) {
       results.push({
         name: file.getName(),
         link: "https://drive.google.com/uc?export=download&id=" + file.getId(),
         path: currentPath
       });
     }
   }
   
   // Recurse into subfolders
   while (subfolders.hasNext()) {
     var subfolder = subfolders.next();
     results = results.concat(getAllPngsInFolder(subfolder.getId(), currentPath));
   }
   
   return results;
 }
 
 function convertDriveLink(url) {
   if (!url) return "";
 
   var match = url.match(/\/d\/(.*)\/view/);
     if (match && match[1]) {
       return "https://drive.google.com/uc?export=download&id=" + match[1];
     }
     
     return "Invalid link";
 }
 ```
 </details>
5. At the top right, click Deploy > New Deployment (Web App) and set the access to "Anyone". Copy that link and paste it into "Apps Script Link"
6. Go back to the Google Sheet File > Share > Publish. Copy that link and paste it into "Read from Link" 
<img width="528" height="468" alt="image" src="https://github.com/user-attachments/assets/2820b4e2-4b05-4efa-b179-f3f7cd21a9df" />

6. In Unity, create a new .csv (recommended to call it AssetList.csv) and drag and drop that into "Target CSV".
7. At this point, the "Run Apps Script" button should appear. This button will not be visible unless all 3 fields are filled, and a warning will show. Click "Run Apps Script". This action will:

    a. Run the Apps Script, which searches for all .pngs in the Art folder and all of its subfolders, generate a downloadable link, and then populate that to the AssetManager Google Sheet.
   
    b. Populate AssetList.csv (in the Unity Project, path: Assets/Art/AssetList.csv) with the same data from the AssetManager Google Sheet.

8. You can preview the assets in the AssetList.csv by expanding the CSV Preview.

 <img width="643" height="411" alt="image" src="https://github.com/user-attachments/assets/c4ac059b-c7a4-48b7-8f85-2ececfc017fd" />


9. Once that process is complete (the editor window will show a status that says "Successfully synced!" or "Ready to Sync" if 3 seconds have passed), click “Populate Assets”. This action will:
    
    a. Download all .pngs from their downloadable links. The Unity folder structure will mirror the Drive structure under /Assets/Art/__2D. If an asset already exists, it will overwrite the data. If an asset does not already exist, it will create it to the correct folder. (If a folder doesn’t exist, it will also generate the folder)

## Demo Setup
There is an example setup attached with this repo. 
[Google Sample Folder](https://drive.google.com/drive/folders/1pKIJrvFdqV3zNfmC8rYZzgt6yGeWsrE7?usp=drive_link): This is the folder with example assets that will be downloaded. 

[Example CSV](https://docs.google.com/spreadsheets/d/e/2PACX-1vSD0wsz9X6wcklx6PwozDh9bWJ-1g-GKkbB9Zd5Ekq_O_RSqIBXZS8udugH7XvacHxoiSrvRApw_u9Q/pub?gid=0&single=true&output=csv): the example .csv Unity will read from. 
[Apps Script](https://script.google.com/macros/s/AKfycbxJPQ-CGCj5lB1aCjvUthByU01F-Wc4iFVIMlKoU7eyKRnbl5Rrx14EVC7KYeUlIP13/exec): The code used to generate downloadable links for all assets in the Google Sample Folder and populate it to the Example .csv. This code (and web app link) are accessible from the Example .csv under Extensions > Apps Script 

## How to Use
Whenever you want to sync Drive assets with Unity, follow these steps and workflow: 
This workflow is best run in bulk, once every week or every 2 weeks, as some of the processes are slow. 

1. On Google Drive, navigate to [AssetManager](https://docs.google.com/spreadsheets/d/15rIOjOh3fr7UmQMU2SRhSoWyAal-lo5Gpp-CJmURg0o/edit?usp=sharing)
2. Open the Apps Scripts editor under Extensions > Apps Script. The editor will open in a new tab. 
<img width="700" height="468" alt="image" src="https://github.com/user-attachments/assets/2f41f98f-92f2-404f-92dd-2de191055e97" />

3. Click Deploy > Manage Deployments in the top right and copy the Web App link.
4. <img width="991" height="665" alt="image" src="https://github.com/user-attachments/assets/9ff34514-b306-4511-ac5c-aafd844e47c3" />

5. Open the Drive Image Sync Editor under Tools > Drive Image Sync. Paste the link into “Apps Script Link”.
 <img width="1298" height="460" alt="image" src="https://github.com/user-attachments/assets/a8235765-889d-49ed-b92a-ba05570f2f45" />

6. On the AssetManager sheet, Publish the sheet under File > Share > Publish to Web as a csv. Paste that link into “Read from Link”.
<img width="1146" height="1426" alt="image" src="https://github.com/user-attachments/assets/7d21eb26-1078-47f8-9e5b-692f21fb9c70" />

7. Click “Run Apps Script”. This action will:
   
    a. Run the Apps Script, which searches for all .pngs in the Art folder and all of its subfolders, generate a downloadable link, and then populate that to the AssetManager Google Sheet.
   
    b. Populate AssetList.csv (in the Unity Project, path: Assets/Art/AssetList.csv) with the same data from the AssetManager Google Sheet.

9. Once it’s done, click “Populate Assets”. This action will:
    
    a. Download all .pngs from their downloadable links. The Unity folder structure will mirror the Drive structure under /Assets/Art/__2D. If an asset already exists, it will overwrite the data. If an asset does not already exist, it will create it to the correct folder. (If a folder doesn’t exist, it will also generate the folder)


## Current Constraints
### For Artists:

You can upload new assets and create new folders in Google Drive. No problem! 
Try to come up with a good asset name and don’t change it after. Try to not move assets to other folders, or rename folders as well. 
Engineering explanation: this won’t break the path, it will just create two assets in the Unity project. E.g. you have an asset named “Cat”, and it is in Unity. If you upload the .png, but have renamed it to “Cat-Brown”, the asset will appear in Unity twice with the different names, and it will need to be manually deleted. 
For updating existing assets, replace the existing asset by uploading a file with the exact same name and selecting “Replace existing file”. Do not delete the old one and upload a new one, as this will break the download path. 


### For Unity: 

The parser does not work for assets or folders that were deleted. You will have to manually delete them in the Unity project. 
If an asset was updated in Drive, you do not need to sync the .csv again. This is because the asset download path is the same (if the asset was replaced as mentioned above). But, for best practice just, never hurts to Sync again :’) 

## Next Features
