namespace Teleperformance.DataIngestion.Sharepoint.Constants;

public static class Constants
{
    #region Messages

    public const string ConnectionStringMissing = "Connection string is not configured.";
    public const string ApplicationNotFound = "Application not found.";
    public const string ApplicationTypeNotRecognised = "Application type '{0}' is not recognised.";
    public const string CredentialsRequired = "TenantId, ClientId, ClientSecret, and HostName are required when no application is selected.";
    public const string SiteIdMissing = "Microsoft Graph did not return a site id.";
    public const string NoLibrariesAvailable = "No document libraries are available on the resolved site.";
    public const string LibraryNotFound = "Document library '{0}' was not found. Available: {1}";
    public const string ClientIdAndSecretRequired = "ClientId and ClientSecret are required.";
    public const string AuthNotConfigured = "Authentication is not configured on the server.";
    public const string InvalidClientCredentials = "Invalid client credentials.";
    public const string SigningKeyNotConfigured = "Auth signing key is not configured.";
    public const string FilePathRequired = "FilePath is required.";
    public const string ApplicationOwnerRequired = "Owner is required for application registration.";
    public const string ExternalSiteNameRequired = "SharePoint site name is required for external applications.";
    public const string InternalConnectivityApplicationNotConfigured = "Internal application credentials are not configured. Set sharepointinternaltenantid, sharepointinternalclientid, sharepointinternalclientsecret, and sharepointinternalhostname environment variables.";
    public const string ExternalSiteNotAccessible = "The internal application cannot access site '{0}'. Raise an access request for this site before registering it.";
    public const string ExternalSiteConnectivityVerified = "Site is reachable using the internal application.";

    #endregion
}
