namespace Sharepoint_Plugin.Models;

public class DriveRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
}

public class FilePathRequest : DriveRequest
{
    public string FilePath { get; set; } = string.Empty;
}

public class FileIdRequest : DriveRequest
{
    public string FileId { get; set; } = string.Empty;
}

public class ListChildrenRequest : DriveRequest
{
    public string? FolderPath { get; set; }
}

public class SiteRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
}

public class SitePathRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string? DriveId { get; set; }
    public string? DriveName { get; set; }
}

public class CreateFolderRequest : DriveRequest
{
    public string FolderName { get; set; } = string.Empty;
    public string? ParentFolderPath { get; set; }
    public string? ConflictBehavior { get; set; } = "rename";
}

public class CreateFolderByPathRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string? ParentFolderPath { get; set; }
    public string? DriveId { get; set; }
    public string? DriveName { get; set; }
    public string? ConflictBehavior { get; set; } = "rename";
}
