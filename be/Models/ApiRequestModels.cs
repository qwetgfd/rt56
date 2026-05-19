namespace Sharepoint_Api.Models;

/// <summary>Lists children at a site path. Auth headers sent separately.</summary>
public class ListChildrenByPathRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string? FolderPath { get; set; }
    public string? DriveId { get; set; }
    public string? DriveName { get; set; }
}

/// <summary>Gets/downloads a file by site path. Auth headers sent separately.</summary>
public class FileByPathRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? DriveId { get; set; }
    public string? DriveName { get; set; }
}

/// <summary>Stream file — site/host/drive in headers or body, filePath in body.</summary>
public class StreamFilePathRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public string? SitePath { get; set; }
    public string? DriveName { get; set; }
}

/// <summary>List children — site/host in headers, optional folderPath in body.</summary>
public class ListChildrenBodyRequest
{
    public string? FolderPath { get; set; }
}

/// <summary>Creates a folder by site path. Auth headers sent separately.</summary>
public class CreateFolderByPathApiRequest
{
    public string HostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string? ParentFolderPath { get; set; }
    public string? DriveId { get; set; }
    public string? DriveName { get; set; }
    public string? ConflictBehavior { get; set; } = "rename";
}
