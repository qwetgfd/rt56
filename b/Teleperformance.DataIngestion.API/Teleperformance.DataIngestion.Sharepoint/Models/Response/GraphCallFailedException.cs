namespace Teleperformance.DataIngestion.Sharepoint.Models.Response;

public sealed class GraphCallFailedException : Exception
{
    public string GraphUrl { get; }
    public int StatusCode { get; }
    public string ResponseBody { get; }
    public string TokenScopes { get; }

    public GraphCallFailedException(string graphUrl, int statusCode, string responseBody, string tokenScopes)
        : base($"Microsoft Graph {(statusCode)} for {graphUrl}: {Truncate(responseBody, 400)}")
    {
        GraphUrl = graphUrl;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        TokenScopes = tokenScopes;
    }

    public object ToDiagnostics() => new
    {
        graphUrl = GraphUrl,
        graphStatus = StatusCode,
        graphBody = ResponseBody,
        tokenScopes = TokenScopes
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
