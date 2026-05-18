namespace Sharepoint_Plugin.Models;

public class SharePointDriveItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public long Size { get; set; }
    public string? MimeType { get; set; }
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public string? WebUrl { get; set; }
    public string? Path { get; set; }
    public int? ChildCount { get; set; }
}
