namespace Sharepoint_Plugin.Models.Request;

public class WorkspaceCredentials
{
    public Guid? ApplicationId { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? HostName { get; set; }
    public string? SiteName { get; set; }
    public string? LibraryName { get; set; }
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
