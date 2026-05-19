namespace Sharepoint_Api.Configuration;

public class SharePointOptions
{
    public const string SectionName = "SharePoint";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SharePointHostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string DriveName { get; set; } = "Documents";
    public string FilePath { get; set; } = string.Empty;
    public string DefaultDriveName { get; set; } = "Documents";
}
