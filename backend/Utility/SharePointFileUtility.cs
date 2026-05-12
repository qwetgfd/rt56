namespace Sharepoint_Plugin.Utility;

public static class SharePointFileUtility
{
    public static string FileContentType => Environment.GetEnvironmentVariable("sharepointFileContentType") ?? "application/octet-stream";
}
