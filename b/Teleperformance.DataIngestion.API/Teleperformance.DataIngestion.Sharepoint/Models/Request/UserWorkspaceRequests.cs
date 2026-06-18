namespace Teleperformance.DataIngestion.Sharepoint.Models.Request;

public class UserConnectSiteRequest
{
    public string? SiteUrl { get; set; }
    public string? SiteName { get; set; }
    public string? HostName { get; set; }
}

public class UserBrowseRequest
{
    public string DriveId { get; set; } = string.Empty;
    public string? FolderPath { get; set; }
}

public class UserFileRequest
{
    public string DriveId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
