using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using static Teleperformance.DataIngestion.Sharepoint.Constants.Constants;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Utilities;

public static class CommonUtilities
{
    public static string NormalizeSharePointSitePath(string? siteName)
    {
        if (string.IsNullOrWhiteSpace(siteName)) return string.Empty;
        var trimmed = siteName.Trim().Trim('/');
        return trimmed.StartsWith("sites/", StringComparison.OrdinalIgnoreCase) ? trimmed : $"sites/{trimmed}";
    }

    public static string EncodeGraphPath(string path)
    {
        path = path.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        return string.IsNullOrEmpty(path)
            ? path
            : string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    public static string ExtractFileNameFromPath(string filePath)
    {
        var slash = filePath.LastIndexOf('/');
        return slash >= 0 && slash + 1 < filePath.Length ? filePath[(slash + 1)..] : filePath;
    }

    public static SharePointItem MapSharePointItem(JsonElement element, string? parentPath)
    {
        var name = element.JsonGetString("name");
        return new SharePointItem
        {
            Id = element.JsonGetString("id"),
            Name = name,
            IsFolder = element.JsonHasProperty("folder"),
            Size = element.JsonGetInt64("size"),
            MimeType = element.TryGetProperty("file", out var file) ? file.JsonGetStringOrNull("mimeType") : null,
            LastModifiedDateTime = element.JsonGetDateTimeOffsetOrNull("lastModifiedDateTime"),
            WebUrl = element.JsonGetStringOrNull("webUrl"),
            Path = string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath.TrimEnd('/')}/{name}",
            ChildCount = element.TryGetProperty("folder", out var folder) ? folder.JsonGetInt32("childCount") : null
        };
    }

    public static SharePointLibrary? MapSharePointLibrary(JsonElement element)
    {
        var id = element.JsonGetString("id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new SharePointLibrary
        {
            Id = id,
            Name = element.JsonGetString("name"),
            Description = element.JsonGetStringOrNull("description"),
            WebUrl = element.JsonGetStringOrNull("webUrl")
        };
    }

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

    public static string? ResolveBearerToken(IHeaderDictionary headers, IQueryCollection? query = null, bool allowQueryToken = true)
    {
        if (allowQueryToken && query is not null)
        {
            var fromQuery = query["access_token"].FirstOrDefault()
                ?? query["token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromQuery))
                return fromQuery.Trim();
        }

        var authHeader = GetHeader(headers, "Authorization");

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public static string? GetHeader(IHeaderDictionary headers, string key) =>
        headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString().Trim()
            : null;

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

    public static async Task WriteFileStreamAsync(
        HttpResponse response,
        FileStreamContent content,
        bool inline,
        CancellationToken cancellationToken)
    {
        response.StatusCode = content.StatusCode;
        response.ContentType = ResolveContentType(content.FileName, content.ContentType);
        response.Headers["Accept-Ranges"] = !string.IsNullOrWhiteSpace(content.AcceptRanges)
            ? content.AcceptRanges
            : "bytes";
        if (!string.IsNullOrWhiteSpace(content.ContentRange))
            response.Headers["Content-Range"] = content.ContentRange;
        if (content.ContentLength.HasValue)
            response.Headers["Content-Length"] = content.ContentLength.Value.ToString();

        if (inline)
            response.Headers["Content-Disposition"] = "inline";
        else if (!string.IsNullOrWhiteSpace(content.FileName))
            response.Headers["Content-Disposition"] = $"attachment; filename=\"{content.FileName}\"";

        await content.Content.CopyToAsync(response.Body, cancellationToken).ConfigureAwait(false);
    }

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

    public static string ToGraphShareToken(string shareUrl)
    {
        var bytes = Encoding.UTF8.GetBytes(shareUrl.Trim());
        var b64 = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "u!" + b64;
    }
}

public sealed class GraphApiConfiguration
{
    private const string DefaultGraphEndpoint = "https://graph.microsoft.com";
    private const string DefaultGraphApiVersion = "v1.0";

    public string Endpoint { get; }
    public string ApiVersion { get; }

    public GraphApiConfiguration(string? endpointOverride = null, string? apiVersionOverride = null)
    {
        Endpoint = endpointOverride?.TrimEnd('/')
            ?? Environment.GetEnvironmentVariable("sharepointgraphendpoint")?.TrimEnd('/')
            ?? DefaultGraphEndpoint;
        ApiVersion = apiVersionOverride?.Trim('/')
            ?? Environment.GetEnvironmentVariable("sharepointgraphapiversion")?.Trim('/')
            ?? DefaultGraphApiVersion;
    }

    public string BuildUrl(string relativePath) => $"{Endpoint}/{ApiVersion}/{relativePath}";
}

public static class AuthConfiguration
{
    public static string Issuer =>
        Environment.GetEnvironmentVariable("authissuer") ?? "Teleperformance.DataIngestion.API";

    public static string Audience =>
        Environment.GetEnvironmentVariable("authaudience") ?? "Teleperformance.DataIngestion.API.Consumers";

    public static int TokenLifetimeMinutes
    {
        get
        {
            var lifetimeEnv = Environment.GetEnvironmentVariable("authtokenlifetimeminutes");
            return !string.IsNullOrWhiteSpace(lifetimeEnv) && int.TryParse(lifetimeEnv, out var parsed)
                ? parsed
                : 60;
        }
    }

    public static string? SigningKey => Environment.GetEnvironmentVariable("authsigningkey");

    public static SymmetricSecurityKey RequireSigningKey()
    {
        var signingKey = SigningKey ?? throw new InvalidOperationException(SigningKeyNotConfigured);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public static SigningCredentials CreateSigningCredentials() =>
        new(RequireSigningKey(), SecurityAlgorithms.HmacSha256);

    public static TokenValidationParameters CreateValidationParameters(SymmetricSecurityKey key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    public static (JwtSecurityToken Token, DateTime ExpiresUtc) CreateJwt(IEnumerable<Claim> claims, DateTime? expiresUtc = null)
    {
        var expires = expiresUtc ?? DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expires,
            signingCredentials: CreateSigningCredentials());
        return (token, expires);
    }

    public static string WriteToken(JwtSecurityToken token) => new JwtSecurityTokenHandler().WriteToken(token);

    public static int ExpiresInSeconds(DateTime expiresUtc) =>
        Math.Max(0, (int)(expiresUtc - DateTime.UtcNow).TotalSeconds);
}

public static class DbExecutor
{
    public static async Task<T> ExecuteAsync<T>(
        string connectionString,
        int commandTimeout,
        string commandText,
        Func<SqlConnection, Task<T>> action)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            return await action(connection).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"DB operation failed ({commandText}): {ex.Message}");
            throw;
        }
    }
}

public static class UserSiteUrlBuilder
{
    private static readonly Regex SiteOnlyPath = new(
        @"^/sites/([^/]+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SiteHomePagePath = new(
        @"^/sites/([^/]+)/SitePages/Home\.aspx$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string BuildLibraryShareUrl(string? siteUrl, string? siteName, string? hostName, string? libraryName)
    {
        var library = string.IsNullOrWhiteSpace(libraryName) ? "Documents" : libraryName.Trim();

        if (!string.IsNullOrWhiteSpace(siteUrl))
            return NormalizeSiteUrlInput(siteUrl.Trim(), library);

        if (string.IsNullOrWhiteSpace(siteName))
            throw new ArgumentException("Provide a SharePoint site URL or site name.");

        var site = ExtractSiteNameFromInput(siteName.Trim());
        var host = NormalizeHost(hostName);
        return $"https://{host}/sites/{site}/{library}";
    }

    public static string ResolveSiteSlug(string? siteUrl, string? siteName, string? hostName)
    {
        if (!string.IsNullOrWhiteSpace(siteUrl))
            return ExtractSiteNameFromInput(siteUrl.Trim());
        if (!string.IsNullOrWhiteSpace(siteName))
            return ExtractSiteNameFromInput(siteName.Trim());
        throw new ArgumentException("Provide a SharePoint site URL or site name.");
    }

    public static bool IsSiteLandingUrl(string shareUrl)
    {
        if (string.IsNullOrWhiteSpace(shareUrl)) return false;
        if (!Uri.TryCreate(NormalizeUrl(shareUrl.Trim()), UriKind.Absolute, out var uri))
            return false;
        var path = uri.AbsolutePath.TrimEnd('/');
        return SiteOnlyPath.IsMatch(path) || SiteHomePagePath.IsMatch(path);
    }

    private static string NormalizeSiteUrlInput(string input, string library)
    {
        var url = NormalizeUrl(input);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Site URL is not valid.");

        var path = uri.AbsolutePath.TrimEnd('/');
        var siteOnly = SiteOnlyPath.Match(path);
        if (siteOnly.Success)
            return $"{uri.Scheme}://{uri.Host}/sites/{siteOnly.Groups[1].Value}/{library}";

        var siteHome = SiteHomePagePath.Match(path);
        if (siteHome.Success)
            return $"{uri.Scheme}://{uri.Host}/sites/{siteHome.Groups[1].Value}/{library}";

        return url;
    }

    private static string ExtractSiteNameFromInput(string input)
    {
        if (input.Contains("sharepoint.com", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var url = NormalizeUrl(input);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("Site URL is not valid.");
            var match = Regex.Match(uri.AbsolutePath, @"/sites/([^/]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new ArgumentException("URL must include /sites/{site-name}.");
            return match.Groups[1].Value;
        }

        return input.Trim().Trim('/');
    }

    private static string NormalizeHost(string? hostName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
            throw new ArgumentException("Host name is required when only a site name is provided.");
        var host = hostName.Trim().TrimEnd('/');
        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host["https://".Length..];
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host["http://".Length..];
        return host.TrimEnd('/');
    }

    private static string NormalizeUrl(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return input;
        return "https://" + input.TrimStart('/');
    }
}
