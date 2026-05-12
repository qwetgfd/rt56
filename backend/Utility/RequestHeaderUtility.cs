namespace Sharepoint_Plugin.Utility;

public static class RequestHeaderUtility
{
    public static string GetRequired(HttpRequest request, string headerName)
    {
        return TryGetValue(request, headerName, out var value)
            ? value
            : throw new ArgumentException($"{headerName} header is required.");
    }

    public static string GetOptional(HttpRequest request, string headerName, string? fallback = null)
    {
        return TryGetValue(request, headerName, out var value) ? value : fallback ?? string.Empty;
    }

    public static string GetRequiredHeaderOrQuery(HttpRequest request, string key)
    {
        return TryGetValue(request, key, out var value)
            || TryGetQueryValue(request, key, out value)
                ? value
                : throw new ArgumentException($"{key} header or query value is required.");
    }

    public static string GetOptionalHeaderOrQuery(HttpRequest request, string key, string? fallback = null)
    {
        return TryGetValue(request, key, out var value) || TryGetQueryValue(request, key, out value)
            ? value
            : fallback ?? string.Empty;
    }

    private static bool TryGetValue(HttpRequest request, string headerName, out string value)
    {
        if (request.Headers.TryGetValue(headerName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            value = headerValue.ToString();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetQueryValue(HttpRequest request, string key, out string value)
    {
        if (request.Query.TryGetValue(key, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
        {
            value = queryValue.ToString();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
