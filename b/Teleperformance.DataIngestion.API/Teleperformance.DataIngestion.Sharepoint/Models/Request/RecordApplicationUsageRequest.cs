namespace Teleperformance.DataIngestion.Sharepoint.Models.Request;

public class RecordApplicationUsageRequest
{
    public string? DisplayName { get; set; }
    public string? UsedByUpn { get; set; }
    public string? UsedByDisplayName { get; set; }
}
