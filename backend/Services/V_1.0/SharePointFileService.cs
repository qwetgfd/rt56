using System.Text.Json;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Services;

namespace Sharepoint_Plugin.Services.V_1_0;

public sealed class SharePointFileService : ISharePointFileService
{
    private readonly ISharePointRepository _repo;
    private readonly string _defaultDriveName;
    private readonly string _graphApiVersion;
    private readonly string _graphRoot;

    public SharePointFileService(ISharePointRepository repo, IAuthService auth)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _defaultDriveName = GetEnvironmentValue("sharepointDefaultDriveName", "Documents");
        _graphApiVersion = GetEnvironmentValue("sharepointGraphApiVersion", "v1.0").Trim('/');
        _graphRoot = auth.GraphEndpoint.TrimEnd('/');
    }

    #region Microsoft Graph Discovery And Folder Operations

    public async Task<SharePointSiteMetadata> GetSiteAsync(string hostName, string sitePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostName);
        ArgumentException.ThrowIfNullOrEmpty(sitePath);

        using var doc = await _repo
            .GetJsonDocumentAsync($"{_graphRoot}/{_graphApiVersion}/sites/{hostName}:/{EncodePath(sitePath)}")
            .ConfigureAwait(false);

        return MapSite(doc.RootElement);
    }

    public async Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteId);

        using var doc = await _repo
            .GetJsonDocumentAsync($"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives")
            .ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return Array.Empty<SharePointDriveMetadata>();

        return value.EnumerateArray()
            .Select(MapDrive)
            .ToList();
    }

    public async Task<SharePointFolderMetadata> CreateFolderAsync(
        string siteId,
        string driveId,
        string folderName,
        string? parentFolderPath = null,
        string? conflictBehavior = null)
    {
        ValidateSiteDrive(siteId, driveId);
        ArgumentException.ThrowIfNullOrEmpty(folderName);

        var url = string.IsNullOrEmpty(parentFolderPath) || parentFolderPath == "/"
            ? $"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root/children"
            : $"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root:/{EncodePath(parentFolderPath)}:/children";

        var body = new Dictionary<string, object>
        {
            ["name"] = folderName,
            ["folder"] = new Dictionary<string, object>()
        };

        if (!string.IsNullOrWhiteSpace(conflictBehavior))
            body["@microsoft.graph.conflictBehavior"] = conflictBehavior;

        using var doc = await _repo
            .PostJsonDocumentAsync(url, body)
            .ConfigureAwait(false);

        return MapFolder(doc.RootElement, siteId, driveId, parentFolderPath);
    }

    #endregion

    #region Dashboard One Call Streaming Operations

    public async Task<SharePointFileStreamContent> GetDashboardFileStreamAsync(
        string hostName,
        string sitePath,
        string filePath,
        string? driveId = null,
        string? driveName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostName);
        ArgumentException.ThrowIfNullOrEmpty(sitePath);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var site = await GetSiteAsync(hostName, sitePath).ConfigureAwait(false);
        var resolvedDriveId = string.IsNullOrWhiteSpace(driveId)
            ? await ResolveDriveIdAsync(site.Id, driveName).ConfigureAwait(false)
            : driveId;

        return await GetFileStreamAsync(site.Id, resolvedDriveId, filePath).ConfigureAwait(false);
    }

    public async Task<SharePointFileMetadata> GetDashboardFileMetadataAsync(
        string hostName,
        string sitePath,
        string filePath,
        string? driveId = null,
        string? driveName = null)
    {
        var context = await ResolveSiteDriveAsync(hostName, sitePath, driveId, driveName).ConfigureAwait(false);
        return await GetFileMetadataAsync(context.SiteId, context.DriveId, filePath).ConfigureAwait(false);
    }

    public async Task<SharePointFolderMetadata> CreateDashboardFolderAsync(
        string hostName,
        string sitePath,
        string folderName,
        string? parentFolderPath = null,
        string? driveId = null,
        string? driveName = null,
        string? conflictBehavior = null)
    {
        var context = await ResolveSiteDriveAsync(hostName, sitePath, driveId, driveName).ConfigureAwait(false);
        return await CreateFolderAsync(context.SiteId, context.DriveId, folderName, parentFolderPath, conflictBehavior).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadDashboardFileBytesAsync(
        string hostName,
        string sitePath,
        string filePath,
        string? driveId = null,
        string? driveName = null)
    {
        var context = await ResolveSiteDriveAsync(hostName, sitePath, driveId, driveName).ConfigureAwait(false);
        return await DownloadFileBytesAsync(context.SiteId, context.DriveId, filePath).ConfigureAwait(false);
    }

    #endregion

    #region SharePoint Drive File Operations

    public async Task<SharePointFileMetadata> GetFileMetadataAsync(string siteId, string driveId, string filePath)
    {
        ValidateSiteDrive(siteId, driveId);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return await GetMetadataAsync($"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root:/{EncodePath(filePath)}", siteId, driveId, filePath).ConfigureAwait(false);
    }

    public async Task<SharePointFileMetadata> GetFileMetadataByIdAsync(string siteId, string driveId, string fileId)
    {
        ValidateSiteDrive(siteId, driveId);
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        return await GetMetadataAsync($"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/items/{fileId}", siteId, driveId, null).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamAsync(string siteId, string driveId, string filePath)
    {
        ValidateSiteDrive(siteId, driveId);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return await _repo.GetContentStreamAsync($"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root:/{EncodePath(filePath)}:/content").ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamByIdAsync(string siteId, string driveId, string fileId)
    {
        ValidateSiteDrive(siteId, driveId);
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        return await _repo.GetContentStreamAsync($"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/items/{fileId}/content").ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadFileBytesAsync(string siteId, string driveId, string filePath)
    {
        var streamContent = await GetFileStreamAsync(siteId, driveId, filePath).ConfigureAwait(false);
        await using var stream = streamContent.Content;
        return await FileContentHelpers.ReadStreamToBytesAsync(stream, null).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(string siteId, string driveId, string? folderPath = null)
    {
        ValidateSiteDrive(siteId, driveId);
        var url = string.IsNullOrEmpty(folderPath) || folderPath == "/"
            ? $"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root/children"
            : $"{_graphRoot}/{_graphApiVersion}/sites/{siteId}/drives/{driveId}/root:/{EncodePath(folderPath)}:/children";

        using var doc = await _repo.GetJsonDocumentAsync(url).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("value", out var value))
            return Array.Empty<SharePointFileMetadata>();

        return value.EnumerateArray()
            .Where(item => item.TryGetProperty("file", out _))
            .Select(item => MapDriveItem(item, siteId, driveId, folderPath))
            .ToList();
    }

    public async Task<IReadOnlyList<SharePointDriveItem>> ListChildrenAsync(
        string hostName,
        string sitePath,
        string? folderPath = null,
        string? driveId = null,
        string? driveName = null)
    {
        var context = await ResolveSiteDriveAsync(hostName, sitePath, driveId, driveName).ConfigureAwait(false);

        var url = string.IsNullOrEmpty(folderPath) || folderPath == "/"
            ? $"{_graphRoot}/{_graphApiVersion}/sites/{context.SiteId}/drives/{context.DriveId}/root/children"
            : $"{_graphRoot}/{_graphApiVersion}/sites/{context.SiteId}/drives/{context.DriveId}/root:/{EncodePath(folderPath)}:/children";

        using var doc = await _repo.GetJsonDocumentAsync(url).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("value", out var value))
            return Array.Empty<SharePointDriveItem>();

        return value.EnumerateArray()
            .Select(item => MapDriveItemSummary(item, context.SiteId, context.DriveId, folderPath))
            .ToList();
    }

    #endregion

    private async Task<SharePointFileMetadata> GetMetadataAsync(string url, string siteId, string driveId, string? path)
    {
        using var doc = await _repo.GetJsonDocumentAsync(url).ConfigureAwait(false);
        return MapDriveItem(doc.RootElement, siteId, driveId, path);
    }

    private async Task<SharePointRequestContext> ResolveSiteDriveAsync(string hostName, string sitePath, string? driveId, string? driveName)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostName);
        ArgumentException.ThrowIfNullOrEmpty(sitePath);

        var site = await GetSiteAsync(hostName, sitePath).ConfigureAwait(false);
        var resolvedDriveId = string.IsNullOrWhiteSpace(driveId)
            ? await ResolveDriveIdAsync(site.Id, driveName).ConfigureAwait(false)
            : driveId;

        return new SharePointRequestContext(site.Id, resolvedDriveId);
    }

    private async Task<string> ResolveDriveIdAsync(string siteId, string? driveName)
    {
        var targetDriveName = string.IsNullOrWhiteSpace(driveName) ? _defaultDriveName : driveName;
        var drives = await ListDrivesAsync(siteId).ConfigureAwait(false);
        var drive = drives.FirstOrDefault(d => string.Equals(d.Name, targetDriveName, StringComparison.OrdinalIgnoreCase));

        if (drive is not null)
            return drive.Id;

        var availableDrives = string.Join(", ", drives.Select(d => d.Name).Where(name => !string.IsNullOrWhiteSpace(name)));
        throw new ArgumentException($"Drive '{targetDriveName}' was not found for site '{siteId}'. Available drives: {availableDrives}");
    }

    private static void ValidateSiteDrive(string siteId, string driveId)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteId);
        ArgumentException.ThrowIfNullOrEmpty(driveId);
    }

    private static string EncodePath(string path)
    {
        path = path.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(path)) return path;
        return string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static string GetEnvironmentValue(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static SharePointSiteMetadata MapSite(JsonElement e) => new()
    {
        Id = e.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
        Name = e.TryGetProperty("name", out var name) ? (name.GetString() ?? "") : "",
        DisplayName = e.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null,
        WebUrl = e.TryGetProperty("webUrl", out var webUrl) ? webUrl.GetString() : null,
        HostName = e.TryGetProperty("siteCollection", out var siteCollection)
            && siteCollection.TryGetProperty("hostname", out var hostName)
                ? hostName.GetString()
                : null
    };

    private static SharePointDriveMetadata MapDrive(JsonElement e) => new()
    {
        Id = e.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
        Name = e.TryGetProperty("name", out var name) ? (name.GetString() ?? "") : "",
        DriveType = e.TryGetProperty("driveType", out var driveType) ? driveType.GetString() : null,
        WebUrl = e.TryGetProperty("webUrl", out var webUrl) ? webUrl.GetString() : null,
        Description = e.TryGetProperty("description", out var description) ? description.GetString() : null
    };

    private static SharePointFolderMetadata MapFolder(JsonElement e, string siteId, string driveId, string? parentFolderPath) => new()
    {
        SiteId = siteId,
        DriveId = driveId,
        ParentFolderPath = parentFolderPath,
        Id = e.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
        Name = e.TryGetProperty("name", out var name) ? (name.GetString() ?? "") : "",
        WebUrl = e.TryGetProperty("webUrl", out var webUrl) ? webUrl.GetString() : null,
        CreatedDateTime = e.TryGetProperty("createdDateTime", out var createdDateTime)
            && DateTimeOffset.TryParse(createdDateTime.GetString(), out var created)
                ? created
                : null,
        LastModifiedDateTime = e.TryGetProperty("lastModifiedDateTime", out var lastModifiedDateTime)
            && DateTimeOffset.TryParse(lastModifiedDateTime.GetString(), out var lastModified)
                ? lastModified
                : null
    };

    private static SharePointFileMetadata MapDriveItem(JsonElement e, string siteId, string driveId, string? path) => new()
    {
        SiteId = siteId,
        DriveId = driveId,
        Path = path,
        Id = e.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
        Name = e.TryGetProperty("name", out var name) ? (name.GetString() ?? "") : "",
        Size = e.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var n) ? n : 0,
        MimeType = e.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() : null,
        LastModifiedDateTime = e.TryGetProperty("lastModifiedDateTime", out var lm) && DateTimeOffset.TryParse(lm.GetString(), out var dto) ? dto : null,
        WebUrl = e.TryGetProperty("webUrl", out var w) ? w.GetString() : null
    };

    private static SharePointDriveItem MapDriveItemSummary(JsonElement e, string siteId, string driveId, string? parentPath)
    {
        var isFolder = e.TryGetProperty("folder", out _);
        var name = e.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
        var path = string.IsNullOrEmpty(parentPath) || parentPath == "/"
            ? name
            : $"{parentPath}/{name}";

        return new SharePointDriveItem
        {
            Id = e.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
            Name = name,
            IsFolder = isFolder,
            Size = e.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0,
            MimeType = e.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var m) ? m.GetString() : null,
            LastModifiedDateTime = e.TryGetProperty("lastModifiedDateTime", out var lm) && DateTimeOffset.TryParse(lm.GetString(), out var dto) ? dto : null,
            WebUrl = e.TryGetProperty("webUrl", out var w) ? w.GetString() : null,
            Path = path,
            ChildCount = e.TryGetProperty("folder", out var folder) && folder.TryGetProperty("childCount", out var cc) ? cc.GetInt32() : null
        };
    }

    private sealed record SharePointRequestContext(string SiteId, string DriveId);
}
