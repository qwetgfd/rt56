namespace Sharepoint_Plugin.Interfaces.V_1_0;

public interface IAuthService
{
    Task<string> GetAccessTokenAsync();
    bool IsTokenValid { get; }
    string GraphEndpoint { get; }
    int TimeoutSeconds { get; }
}
