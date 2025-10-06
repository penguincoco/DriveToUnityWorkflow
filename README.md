# Drive to Unity Workflow
This workflow combines Google Apps Script and Unity Editor code to streamline downloading assets from Google Drive to Unity. No more needing to manually download all assets from Drive and import to Unity, it can all be done with a single button click! 
How to Use
Whenever you want to sync Drive assets with Unity, follow these steps and workflow: 
This workflow is best run in bulk, once every week or every 2 weeks, as some of the processes are slow. 

## How to Use
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
For Artists:

You can upload new assets and create new folders in Google Drive. No problem! 
Try to come up with a good asset name and don’t change it after. Try to not move assets to other folders, or rename folders as well. 
Engineering explanation: this won’t break the path, it will just create two assets in the Unity project. E.g. you have an asset named “Cat”, and it is in Unity. If you upload the .png, but have renamed it to “Cat-Brown”, the asset will appear in Unity twice with the different names, and it will need to be manually deleted. 
For updating existing assets, replace the existing asset by uploading a file with the exact same name and selecting “Replace existing file”. Do not delete the old one and upload a new one, as this will break the download path. 


For Populators: 

The parser does not work for assets or folders that were deleted. You will have to manually delete them in the Unity project. 
If an asset was updated in Drive, you do not need to sync the .csv again. This is because the asset download path is the same (if the asset was replaced as mentioned above). But, for best practice just, never hurts to Sync again :’) 

## Next Features
