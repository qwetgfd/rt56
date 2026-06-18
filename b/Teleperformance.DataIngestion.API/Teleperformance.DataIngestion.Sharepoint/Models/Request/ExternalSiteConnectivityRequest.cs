namespace Teleperformance.DataIngestion.Sharepoint.Models.Request;

public sealed class ExternalSiteConnectivityRequest
{
    public string SiteName { get; set; } = string.Empty;
    /// <summary>SharePoint host to probe (e.g. contoso.sharepoint.com). Required when using Microsoft Entra credentials below; otherwise the internal app's host is used.</summary>
    public string? HostName { get; set; }
    /// <summary>Registered internal application used to probe Graph access. Ignored when TenantId, ClientId, and ClientSecret are supplied.</summary>
    public Guid? InternalApplicationId { get; set; }
    /// <summary>Optional Microsoft Entra credentials to probe with (new application registration). When set, InternalApplicationId is not used.</summary>
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
