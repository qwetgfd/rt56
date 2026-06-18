namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

public sealed class ExternalSiteConnectivityResult
{
    public bool IsConnected { get; set; }
    public bool RequiresAccessRequest { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string? SiteTitle { get; set; }
    public int LibraryCount { get; set; }
    public IReadOnlyList<SharePointLibrary> Libraries { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}
