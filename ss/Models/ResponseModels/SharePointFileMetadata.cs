namespace Sharepoint_Plugin.Models;

public class SharePointFileMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? MimeType { get; set; }
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public string? WebUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? SiteId { get; set; }
    public string? DriveId { get; set; }
    public string? Path { get; set; }
}
