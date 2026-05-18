using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Utilities;

namespace Sharepoint_Plugin.Repositories.V_1_0;

public class SharePointRepository : ISharePointRepository
{
    private readonly IAuthService _auth;
    private readonly HttpClient _http;
    private readonly string _graphRoot;
    private readonly string _graphVersion;

    public SharePointRepository(IAuthService auth, HttpClient http)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _graphRoot = auth.GraphEndpoint.TrimEnd('/');
        _graphVersion = (Environment.GetEnvironmentVariable("sharepointGraphApiVersion") ?? "v1.0").Trim('/');
    }

    public async Task<SharePointSiteMetadata> GetSiteAsync(SiteRequest request)
    {
        var url = BuildUrl($"sites/{request.HostName}:/{EncodePath(request.SitePath)}");
        return MapSite((await SendGetAsync(url).ConfigureAwait(false)).RootElement);
    }

    public async Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId)
    {
        var doc = await SendGetAsync(BuildUrl($"sites/{siteId}/drives")).ConfigureAwait(false);
        return doc.RootElement.HasProperty("value")
            ? doc.RootElement.GetProperty("value").EnumerateArray().Select(MapDrive).ToList()
            : [];
    }

    public async Task<SharePointFileMetadata> GetFileMetadataAsync(FilePathRequest request)
    {
        var url = BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root:/{EncodePath(request.FilePath)}");
        return MapFile((await SendGetAsync(url).ConfigureAwait(false)).RootElement, request.SiteId, request.DriveId, request.FilePath);
    }

    public async Task<SharePointFileMetadata> GetFileMetadataByIdAsync(FileIdRequest request)
    {
        var url = BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/items/{request.FileId}");
        return MapFile((await SendGetAsync(url).ConfigureAwait(false)).RootElement, request.SiteId, request.DriveId, null);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamAsync(FilePathRequest request)
    {
        var url = BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root:/{EncodePath(request.FilePath)}:/content");
        return await GetContentStreamAsync(url).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamByIdAsync(FileIdRequest request)
    {
        var url = BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/items/{request.FileId}/content");
        return await GetContentStreamAsync(url).ConfigureAwait(false);
    }

    public async Task<SharePointFolderMetadata> CreateFolderAsync(CreateFolderRequest request)
    {
        var url = string.IsNullOrEmpty(request.ParentFolderPath) || request.ParentFolderPath == "/"
            ? BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root/children")
            : BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root:/{EncodePath(request.ParentFolderPath)}:/children");

        var body = new Dictionary<string, object> { ["name"] = request.FolderName, ["folder"] = new Dictionary<string, object>() };
        if (!string.IsNullOrWhiteSpace(request.ConflictBehavior))
            body["@microsoft.graph.conflictBehavior"] = request.ConflictBehavior;

        return MapFolder((await SendPostAsync(url, body).ConfigureAwait(false)).RootElement, request.SiteId, request.DriveId, request.ParentFolderPath);
    }

    public async Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(ListChildrenRequest request)
    {
        var url = string.IsNullOrEmpty(request.FolderPath) || request.FolderPath == "/"
            ? BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root/children")
            : BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root:/{EncodePath(request.FolderPath)}:/children");

        var doc = await SendGetAsync(url).ConfigureAwait(false);
        return doc.RootElement.HasProperty("value")
            ? doc.RootElement.GetProperty("value").EnumerateArray().Where(i => i.HasProperty("file")).Select(i => MapFile(i, request.SiteId, request.DriveId, request.FolderPath)).ToList()
            : [];
    }

    public async Task<IReadOnlyList<SharePointDriveItem>> ListChildrenAsync(ListChildrenRequest request)
    {
        var url = string.IsNullOrEmpty(request.FolderPath) || request.FolderPath == "/"
            ? BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root/children")
            : BuildUrl($"sites/{request.SiteId}/drives/{request.DriveId}/root:/{EncodePath(request.FolderPath)}:/children");

        var doc = await SendGetAsync(url).ConfigureAwait(false);
        return doc.RootElement.HasProperty("value")
            ? doc.RootElement.GetProperty("value").EnumerateArray().Select(i => MapDriveItem(i, request.DriveId, request.FolderPath)).ToList()
            : [];
    }

    private async Task<JsonDocument> SendGetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));
        using var response = await _http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
    }

    private async Task<JsonDocument> SendPostAsync(string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
    }

    private async Task<SharePointFileStreamContent> GetContentStreamAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
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

    private string BuildUrl(string path) => $"{_graphRoot}/{_graphVersion}/{path}";

    private static string EncodePath(string path)
    {
        path = path.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        return string.IsNullOrEmpty(path) ? path : string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static SharePointSiteMetadata MapSite(JsonElement e) => new()
    {
        Id = JsonHelpers.GetString(e, "id"),
        Name = JsonHelpers.GetString(e, "name"),
        DisplayName = JsonHelpers.GetStringOrNull(e, "displayName"),
        WebUrl = JsonHelpers.GetStringOrNull(e, "webUrl"),
        HostName = e.TryGetProperty("siteCollection", out var sc) ? JsonHelpers.GetStringOrNull(sc, "hostname") : null
    };

    private static SharePointDriveMetadata MapDrive(JsonElement e) => new()
    {
        Id = JsonHelpers.GetString(e, "id"),
        Name = JsonHelpers.GetString(e, "name"),
        DriveType = JsonHelpers.GetStringOrNull(e, "driveType"),
        WebUrl = JsonHelpers.GetStringOrNull(e, "webUrl"),
        Description = JsonHelpers.GetStringOrNull(e, "description")
    };

    private static SharePointFileMetadata MapFile(JsonElement e, string siteId, string driveId, string? path) => new()
    {
        SiteId = siteId,
        DriveId = driveId,
        Path = path,
        Id = JsonHelpers.GetString(e, "id"),
        Name = JsonHelpers.GetString(e, "name"),
        Size = JsonHelpers.GetInt64(e, "size"),
        MimeType = e.TryGetProperty("file", out var f) ? JsonHelpers.GetStringOrNull(f, "mimeType") : null,
        LastModifiedDateTime = JsonHelpers.GetDateTimeOffsetOrNull(e, "lastModifiedDateTime"),
        WebUrl = JsonHelpers.GetStringOrNull(e, "webUrl")
    };

    private static SharePointFolderMetadata MapFolder(JsonElement e, string siteId, string driveId, string? parentPath) => new()
    {
        SiteId = siteId,
        DriveId = driveId,
        ParentFolderPath = parentPath,
        Id = JsonHelpers.GetString(e, "id"),
        Name = JsonHelpers.GetString(e, "name"),
        WebUrl = JsonHelpers.GetStringOrNull(e, "webUrl"),
        CreatedDateTime = JsonHelpers.GetDateTimeOffsetOrNull(e, "createdDateTime"),
        LastModifiedDateTime = JsonHelpers.GetDateTimeOffsetOrNull(e, "lastModifiedDateTime")
    };

    private static SharePointDriveItem MapDriveItem(JsonElement e, string driveId, string? parentPath)
    {
        var name = JsonHelpers.GetString(e, "name");
        return new SharePointDriveItem
        {
            Id = JsonHelpers.GetString(e, "id"),
            Name = name,
            IsFolder = JsonHelpers.HasProperty(e, "folder"),
            Size = JsonHelpers.GetInt64(e, "size"),
            MimeType = e.TryGetProperty("file", out var f) ? JsonHelpers.GetStringOrNull(f, "mimeType") : null,
            LastModifiedDateTime = JsonHelpers.GetDateTimeOffsetOrNull(e, "lastModifiedDateTime"),
            WebUrl = JsonHelpers.GetStringOrNull(e, "webUrl"),
            Path = string.IsNullOrEmpty(parentPath) || parentPath == "/" ? name : $"{parentPath}/{name}",
            ChildCount = e.TryGetProperty("folder", out var folder) ? JsonHelpers.GetInt32(folder, "childCount") : 0
        };
    }
}
