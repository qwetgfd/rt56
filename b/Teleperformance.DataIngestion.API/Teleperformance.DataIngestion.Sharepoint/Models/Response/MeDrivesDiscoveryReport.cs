namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

/// <summary>Temporary diagnostics for /me/drives site matching. Remove after drive discovery is verified.</summary>
public sealed class MeDrivesDiscoveryReport
{
    public string? RequestSiteUrl { get; set; }
    public string? RequestSiteName { get; set; }
    public string? RequestHostName { get; set; }
    public string ResolvedSiteSlug { get; set; } = string.Empty;
    public string ResolvedHostName { get; set; } = string.Empty;
    public string StrictPathMarker { get; set; } = string.Empty;
    public string FilteringSummary { get; set; } = string.Empty;
    public string? LibrariesDiscoveryNote { get; set; }
    public int TotalDriveCount { get; set; }
    public int DocumentLibraryCandidateCount { get; set; }
    public int StrictMatchCount { get; set; }
    public int SiteSlugOnlyMatchCount { get; set; }
    public List<string> RawMeDrivesResponsePages { get; set; } = new();
    public List<MeDrivesDiscoveryEntry> Drives { get; set; } = new();
}

public sealed class MeDrivesDiscoveryEntry
{
    public int Index { get; set; }
    public string? Name { get; set; }
    public string? Id { get; set; }
    public string? WebUrl { get; set; }
    public string? DriveType { get; set; }
    public bool PassesDocumentLibraryFilter { get; set; }
    public bool StrictSiteAndHostMatch { get; set; }
    public bool SiteSlugPathMatch { get; set; }
    public string MatchResult { get; set; } = string.Empty;
}
