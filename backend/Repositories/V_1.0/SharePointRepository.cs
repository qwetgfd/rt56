using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Repositories.V_1_0;

public sealed class SharePointRepository : ISharePointRepository
{
    private readonly IAuthService _auth;
    private readonly HttpClient _http;

    public SharePointRepository(IAuthService auth, HttpClient http)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<JsonDocument> GetJsonDocumentAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));
        using var response = await _http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    public async Task<JsonDocument> PostJsonDocumentAsync(string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetContentStreamAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return new SharePointFileStreamContent
        {
            Content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            ContentLength = response.Content.Headers.ContentLength
        };
    }
}
