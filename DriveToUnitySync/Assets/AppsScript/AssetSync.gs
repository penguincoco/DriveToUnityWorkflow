// doGet is how Unity can trigger running this script!
function doGet(e) {
  try {
     // Check if folderId was provided AND is not empty
    if (!e.parameter.folderId || e.parameter.folderId.trim() === "") {
      return ContentService.createTextOutput(JSON.stringify({
        status: "error",
        message: "Missing required parameter: folderId"
      })).setMimeType(ContentService.MimeType.JSON);
    }

    var folderId = e.parameter.folderId;
    populate(folderId);

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

function populate(folderId) {
  var files = getAllPngsInFolder(folderId); // root folder ID

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