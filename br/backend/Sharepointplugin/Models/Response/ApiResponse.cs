namespace Sharepoint_Plugin.Models.Response;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "Success") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ApiResponse Ok(object? data, string message = "Success") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static ApiResponse Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}

public class Application
{
    public Guid ApplicationId { get; set; }
    public int ApplicationTypeId { get; set; }
    public string ApplicationTypeCode { get; set; } = string.Empty;
    public string ApplicationTypeName { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = "system";
    public string DisplayName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Microsoft Entra (Azure AD) application ID used for Graph / SharePoint access. Never sent by external streaming API consumers.</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>Microsoft Entra client secret for Graph. Stored server-side only for registered apps.</summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>Legacy consumer id (deprecated; use <see cref="ApplicationId"/> + <see cref="ConsumerSecret"/> for /auth/token).</summary>
    public string? ConsumerClientId { get; set; }
    /// <summary>Secret issued to external API consumers (header x-client-secret). Generated on registration.</summary>
    public string? ConsumerSecret { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string? LibraryName { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? ModifiedOn { get; set; }

    public class ApplicationType
    {
        public int ApplicationTypeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

public class SharePointItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public long Size { get; set; }
    public string? MimeType { get; set; }
    public DateTimeOffset? LastModifiedDateTime { get; set; }
    public string? WebUrl { get; set; }
    public string? Path { get; set; }
    public int? ChildCount { get; set; }
}

public class SharePointLibrary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WebUrl { get; set; }
}

public class FileStreamContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public long? ContentLength { get; set; }
    public int StatusCode { get; set; } = 200;
    public string? ContentRange { get; set; }
    public string? AcceptRanges { get; set; }
    public string FileName { get; set; } = string.Empty;
}
