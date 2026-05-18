using System.Text.Json;

namespace Sharepoint_Plugin.Utilities;

/// <summary>
/// Reusable helpers for safe JsonElement property access.
/// </summary>
public static class JsonHelpers
{
    public static string GetString(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) ? (prop.GetString() ?? "") : "";

    public static string? GetStringOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;

    public static long GetInt64(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt64(out var value) ? value : 0;

    public static int GetInt32(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out var value) ? value : 0;

    public static DateTimeOffset? GetDateTimeOffsetOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && DateTimeOffset.TryParse(prop.GetString(), out var value) ? value : null;

    public static bool HasProperty(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out _);
}
