using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Models.Request;

public class WorkspaceCredentials
{
    public Guid? ApplicationId { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? HostName { get; set; }
    public string? SiteName { get; set; }
    public string? LibraryName { get; set; }

    public static WorkspaceCredentials FromApplication(Application app) => new()
    {
        ApplicationId = app.ApplicationId,
        TenantId = app.TenantId,
        ClientId = app.ClientId,
        ClientSecret = app.ClientSecret,
        HostName = app.HostName,
        SiteName = app.SiteName,
        LibraryName = app.LibraryName
    };

    public static WorkspaceCredentials Merge(WorkspaceCredentials? primary, WorkspaceCredentials fallback)
    {
        if (primary is null) return fallback;

        return new WorkspaceCredentials
        {
            ApplicationId = primary.ApplicationId ?? fallback.ApplicationId,
            TenantId = string.IsNullOrWhiteSpace(primary.TenantId) ? fallback.TenantId : primary.TenantId,
            ClientId = string.IsNullOrWhiteSpace(primary.ClientId) ? fallback.ClientId : primary.ClientId,
            ClientSecret = string.IsNullOrWhiteSpace(primary.ClientSecret) ? fallback.ClientSecret : primary.ClientSecret,
            HostName = string.IsNullOrWhiteSpace(primary.HostName) ? fallback.HostName : primary.HostName,
            SiteName = string.IsNullOrWhiteSpace(primary.SiteName) ? fallback.SiteName : primary.SiteName,
            LibraryName = string.IsNullOrWhiteSpace(primary.LibraryName) ? fallback.LibraryName : primary.LibraryName
        };
    }
}

public class WorkspaceBrowseRequest
{
    public WorkspaceCredentials? Credentials { get; set; }
    public string? FolderPath { get; set; }
}

public class WorkspaceFetchFileRequest
{
    public WorkspaceCredentials? Credentials { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>
/// Credentials sent to POST /auth/token. <see cref="ApplicationId"/> is the registered app GUID;
/// <see cref="ClientSecret"/> is the API secret issued when the app was registered (not Microsoft Entra).
/// </summary>
public class TokenRequest
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// Token response containing the access token and metadata.
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }
}

/// <summary>
/// Simplified file fetch request using only token and file path.
/// </summary>
public class FileStreamRequest
{
    public string FilePath { get; set; } = string.Empty;
}
