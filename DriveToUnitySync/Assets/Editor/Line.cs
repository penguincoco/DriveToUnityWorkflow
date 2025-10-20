public class Line
{
    public string assetName;
    public string assetDownloadLink;
    public string assetPath;

    public Line(string assetName, string assetDownloadLink, string assetPath)
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