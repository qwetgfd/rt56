namespace Sharepoint_Api.Models;

public class SharePointConfigDto
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SharepointHostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string DriveName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DefaultDriveName { get; set; } = "Documents";
}
