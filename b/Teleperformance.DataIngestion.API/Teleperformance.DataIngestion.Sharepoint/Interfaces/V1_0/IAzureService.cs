namespace Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;

public interface IAzureService
{
    Task<Models.Response.ADUserSearchResponse> SearchADUsersAsync(string term, string? searchType = null);
}
