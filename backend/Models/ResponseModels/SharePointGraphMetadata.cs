namespace Sharepoint_Plugin.Models;

public sealed class SharePointSiteMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? WebUrl { get; set; }
    public string? HostName { get; set; }
}

public sealed class SharePointDriveMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DriveType { get; set; }
    public string? WebUrl { get; set; }
    public string? Description { get; set; }
}

public sealed class SharePointFolderMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? WebUrl { get; set; }
    public DateTimeOffset? CreatedDateTime { get; set; }
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public string? SiteId { get; set; }
    public string? DriveId { get; set; }
    public string? ParentFolderPath { get; set; }
}

public sealed class SharePointFileStreamContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public long? ContentLength { get; set; }
}
