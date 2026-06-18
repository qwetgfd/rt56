using System.Net.Http.Headers;
using System.Text.Json;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Services.V1_0;

public class AzureService : IAzureService
{
    private static readonly HttpClient _httpClient = new();
    private static string? _cachedToken;
    private static DateTime _tokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static string GraphUrl => Environment.GetEnvironmentVariable("tpgraphbaseurl") ?? "https://graph.microsoft.com/v1.0";
    private static string UserSelect => Environment.GetEnvironmentVariable("tpgraphuserfields") ?? "id,displayName,givenName,surname,userPrincipalName,mail,jobTitle,department,officeLocation,mobilePhone,companyName";

    #region Token Management
    private async Task<string> GetGraphTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return _cachedToken;
            var clientId = Environment.GetEnvironmentVariable("tpgraphclientid") ?? "";
            var clientSecret = Environment.GetEnvironmentVariable("tpgraphclientsecret") ?? "";
            var tenantId = Environment.GetEnvironmentVariable("tpgraphtenantid") ?? "";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId, ["client_secret"] = clientSecret,
                ["scope"] = Environment.GetEnvironmentVariable("tpgraphoauthscope") ?? "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            });
            var tokenBaseUrl = Environment.GetEnvironmentVariable("tpgraphtokenendpoint") ?? "https://login.microsoftonline.com";
            using var response = await _httpClient.PostAsync($"{tokenBaseUrl}/{tenantId}/oauth2/v2.0/token", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _cachedToken = doc.RootElement.GetProperty("access_token").GetString()!;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
            return _cachedToken;
        }
        finally { _tokenLock.Release(); }
    }
    #endregion

    #region Search
    public async Task<ADUserSearchResponse> SearchADUsersAsync(string term, string? searchType = null)
    {
        if (string.IsNullOrWhiteSpace(term)) return new ADUserSearchResponse();
        try
        {
            var filter = BuildSearchFilter(term, searchType);
            var token = await GetGraphTokenAsync();
            return await SearchWithFilterAsync(filter, token);
        }
        catch (Exception ex) { throw new Exception($"Graph API Search Error: {ex.Message}", ex); }
    }

    private static string BuildSearchFilter(string term, string? searchType)
    {
        if (string.IsNullOrWhiteSpace(term)) return string.Empty;
        var escaped = term.Replace("'", "''");
        return (searchType ?? "all").ToLowerInvariant() switch
        {
            "upn" => $"startsWith(userPrincipalName,'{escaped}')",
            "email" => $"startsWith(userPrincipalName,'{escaped}')",
            "displayname" => $"startsWith(displayName,'{escaped}')",
            "firstname" => $"startsWith(givenName,'{escaped}')",
            "lastname" => $"startsWith(surname,'{escaped}')",
            _ => $"startsWith(userPrincipalName,'{escaped}') or startsWith(displayName,'{escaped}') or startsWith(givenName,'{escaped}') or startsWith(surname,'{escaped}')"
        };
    }

    private async Task<ADUserSearchResponse> SearchWithFilterAsync(string filter, string token)
    {
        if (string.IsNullOrWhiteSpace(filter)) return new ADUserSearchResponse();
        var url = $"{GraphUrl}/users?$filter={Uri.EscapeDataString(filter)}&$select={UserSelect}&$count=true";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("ConsistencyLevel", "eventual");
        using var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return new ADUserSearchResponse();
        using var doc = JsonDocument.Parse(json);
        var users = new List<ADUser>();
        if (doc.RootElement.TryGetProperty("value", out var valueArray))
            foreach (var item in valueArray.EnumerateArray())
                users.Add(JsonSerializer.Deserialize<ADUser>(item.GetRawText(), _jsonOptions)!);
        return new ADUserSearchResponse { Users = users, Count = users.Count };
    }
    #endregion
}
