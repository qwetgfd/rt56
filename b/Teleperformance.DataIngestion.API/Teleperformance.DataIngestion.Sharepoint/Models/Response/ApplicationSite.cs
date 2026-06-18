namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

public sealed class ApplicationSite
{
    public Guid ApplicationSiteId { get; set; }
    public Guid ApplicationId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string? LibraryName { get; set; }
    public string? FolderPath { get; set; }
    public int SortOrder { get; set; }
}
