namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

public sealed class UserConnectSiteFailedException : Exception
{
    public MeDrivesDiscoveryReport? DiscoveryReport { get; }

    public UserConnectSiteFailedException(string message, MeDrivesDiscoveryReport? discoveryReport, Exception? inner = null)
        : base(message, inner)
    {
        DiscoveryReport = discoveryReport;
    }
}
