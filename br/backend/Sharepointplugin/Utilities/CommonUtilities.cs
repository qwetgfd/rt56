using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sharepoint_Plugin.Models.Request;
using Sharepoint_Plugin.Models.Response;

namespace Sharepoint_Plugin.Utilities;

public static class CommonUtilities
{
    #region Json Helpers

    public static string JsonGetString(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) ? (prop.GetString() ?? "") : "";

    public static string? JsonGetStringOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;

    public static long JsonGetInt64(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt64(out var value) ? value : 0;

    public static int JsonGetInt32(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out var value) ? value : 0;

    public static DateTimeOffset? JsonGetDateTimeOffsetOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && DateTimeOffset.TryParse(prop.GetString(), out var value) ? value : null;

    public static bool JsonHasProperty(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out _);

    #endregion

    #region Drive Name Helper

    public static string NormalizeDriveName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Trim();
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(s);
                if (string.Equals(decoded, s, StringComparison.Ordinal)) break;
                s = decoded;
            }
            catch (UriFormatException)
            {
                s = s.Replace("%20", " ", StringComparison.OrdinalIgnoreCase).Replace("%2B", "+", StringComparison.OrdinalIgnoreCase);
                break;
            }
        }
        return s.Replace('+', ' ').Trim();
    }

    public static SharePointLibrary? FindDrive(IReadOnlyList<SharePointLibrary> drives, string target)
    {
        var normalizedTarget = NormalizeDriveName(target);
        if (string.IsNullOrWhiteSpace(normalizedTarget)) return null;
        var compactTarget = CompactDriveKey(normalizedTarget);

        return drives.FirstOrDefault(d => DriveNamesMatch(normalizedTarget, d.Name))
            ?? drives.FirstOrDefault(d => CompactDriveKey(d.Name) == compactTarget)
            ?? drives.FirstOrDefault(d => d.Name.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) || normalizedTarget.Contains(d.Name, StringComparison.OrdinalIgnoreCase))
            ?? drives.FirstOrDefault(d => UrlMatchesLibrary(d.WebUrl, normalizedTarget));
    }

    private static bool DriveNamesMatch(string? requested, string? driveName)
        => string.Equals(NormalizeDriveName(requested), NormalizeDriveName(driveName), StringComparison.OrdinalIgnoreCase);

    private static string CompactDriveKey(string? value) => new(NormalizeDriveName(value).Where(char.IsLetterOrDigit).ToArray());

    private static bool UrlMatchesLibrary(string? webUrl, string target)
    {
        if (string.IsNullOrWhiteSpace(webUrl)) return false;
        var segments = new[] { target, target.Replace(" ", "%20", StringComparison.Ordinal), Uri.EscapeDataString(target) };
        return segments.Where(s => !string.IsNullOrWhiteSpace(s)).Any(segment =>
            webUrl.Contains("/" + segment + "/", StringComparison.OrdinalIgnoreCase) ||
            webUrl.EndsWith("/" + segment, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Header Helpers

    public static string? GetHeader(IHeaderDictionary headers, string key)
    {
        return headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString().Trim()
            : null;
    }

    public static WorkspaceCredentials ExtractCredentials(IHeaderDictionary headers)
    {
        var credentials = new WorkspaceCredentials();

        if (Guid.TryParse(GetHeader(headers, "x-application-id"), out var appId))
            credentials.ApplicationId = appId;

        credentials.TenantId = GetHeader(headers, "x-tenant-id");
        credentials.ClientId = GetHeader(headers, "x-client-id");
        credentials.ClientSecret = GetHeader(headers, "x-client-secret");
        credentials.HostName = GetHeader(headers, "x-host-name");
        credentials.SiteName = GetHeader(headers, "x-site-name");
        credentials.LibraryName = GetHeader(headers, "x-library-name");

        return credentials;
    }

    public static WorkspaceCredentials ExtractCredentialsFromQuery(IQueryCollection query)
    {
        var credentials = new WorkspaceCredentials();

        if (Guid.TryParse(query["applicationId"].FirstOrDefault(), out var appId))
            credentials.ApplicationId = appId;

        credentials.TenantId = query["tenantId"].FirstOrDefault();
        credentials.ClientId = query["clientId"].FirstOrDefault();
        credentials.ClientSecret = query["clientSecret"].FirstOrDefault();
        credentials.HostName = query["hostName"].FirstOrDefault();
        credentials.SiteName = query["siteName"].FirstOrDefault();
        credentials.LibraryName = query["libraryName"].FirstOrDefault();

        return credentials;
    }

    /// <summary>
    /// Reads <c>path</c>, <c>filePath</c>, or base64url <c>path64</c> (avoids slashes in query strings that break IIS/proxies/browsers).
    /// </summary>
    public static string? GetDecodedFilePathFromQuery(IQueryCollection query)
    {
        var path64 = query["path64"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path64))
        {
            try
            {
                var s = path64.Trim().Replace('-', '+').Replace('_', '/');
                switch (s.Length % 4)
                {
                    case 2: s += "=="; break;
                    case 3: s += "="; break;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(s));
            }
            catch
            {
                return null;
            }
        }

        return query["path"].FirstOrDefault()
            ?? query["filePath"].FirstOrDefault();
    }

    #endregion

    #region Content Type Helper

    public static string ResolveContentType(string fileName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback) && !fallback.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
            return fallback.Split(';')[0].Trim();

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".3gp" => "video/3gpp",
            ".ogv" => "video/ogg",
            ".mpg" or ".mpeg" => "video/mpeg",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".flac" => "audio/flac",
            ".opus" => "audio/opus",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            _ => string.IsNullOrWhiteSpace(fallback) ? "application/octet-stream" : fallback
        };
    }

    #endregion
}
