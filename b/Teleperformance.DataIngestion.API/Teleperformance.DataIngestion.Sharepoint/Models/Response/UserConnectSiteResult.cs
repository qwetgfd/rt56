namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

public class UserConnectSiteResult
{
    public string SiteTitle { get; set; } = string.Empty;
    public string SiteSlug { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public IReadOnlyList<SharePointLibrary> Libraries { get; set; } = Array.Empty<SharePointLibrary>();
}
