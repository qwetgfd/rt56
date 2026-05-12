using System.ComponentModel.DataAnnotations;

namespace Sharepoint_Plugin.Models;

public class SharePointDriveRequest
{
    [Required]
    public string SiteId { get; set; } = string.Empty;

    [Required]
    public string DriveId { get; set; } = string.Empty;
}

public sealed class SharePointFilePathRequest : SharePointDriveRequest
{
    [Required]
    public string FilePath { get; set; } = string.Empty;
    public long? StartByte { get; set; }
    public long? EndByte { get; set; }
}

public sealed class SharePointFileIdRequest : SharePointDriveRequest
{
    [Required]
    public string FileId { get; set; } = string.Empty;
    public long? StartByte { get; set; }
    public long? EndByte { get; set; }
}

public sealed class SharePointListFilesRequest : SharePointDriveRequest
{
    public string? FolderPath { get; set; }
}

public sealed class SharePointSiteRequest
{
    [Required]
    public string HostName { get; set; } = string.Empty;

    [Required]
    public string SitePath { get; set; } = string.Empty;
}

public sealed class SharePointSiteIdRequest
{
    [Required]
    public string SiteId { get; set; } = string.Empty;
}

public sealed class SharePointCreateFolderRequest : SharePointDriveRequest
{
    [Required]
    public string FolderName { get; set; } = string.Empty;

    public string? ParentFolderPath { get; set; }
    public string? ConflictBehavior { get; set; } = "rename";
}

public sealed class SharePointDashboardFileStreamRequest
{
    [Required]
    public string FilePath { get; set; } = string.Empty;
}

public sealed class SharePointFolderRequest
{
    [Required]
    public string FolderName { get; set; } = string.Empty;

    public string? ParentFolderPath { get; set; }
    public string? ConflictBehavior { get; set; } = "rename";
}
