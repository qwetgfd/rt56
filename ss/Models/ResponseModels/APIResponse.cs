using Sharepoint_Plugin.Constants;

namespace Sharepoint_Plugin.Models;

public class APIResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static APIResponse Ok(object? data, string message = MessageConstants.Success) => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static APIResponse Fail(string message, object? data = null) => new()
    {
        Success = false,
        Message = message,
        Data = data
    };
}
