using NLog;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Repositories.V_1_0;
using Sharepoint_Plugin.Services.V_1_0;

var logger = LogManager.GetCurrentClassLogger();

Environment.SetEnvironmentVariable("sharepointGraphEndpoint", "https://graph.microsoft.com");
Environment.SetEnvironmentVariable("sharepointGraphApiVersion", "v1.0");
Environment.SetEnvironmentVariable("sharepointTokenEndpointTemplate", "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
Environment.SetEnvironmentVariable("sharepointGraphScope", "https://graph.microsoft.com/.default");
Environment.SetEnvironmentVariable("sharepointGrantType", "client_credentials");
Environment.SetEnvironmentVariable("sharepointDefaultDriveName", "Documents");
Environment.SetEnvironmentVariable("sharepointTimeoutSeconds", "300");
Environment.SetEnvironmentVariable("sharepointFileContentType", "application/octet-stream");
Environment.SetEnvironmentVariable("hostName", "nocompany102.sharepoint.com");

var http = new HttpClient();
IAuthService authService = new AuthService();
ISharePointRepository repository = new SharePointRepository(authService, http);
ISharePointFileService fileService = new SharePointFileService(repository, authService);

LogManager.Shutdown();
