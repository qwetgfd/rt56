using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Sharepoint_Plugin.Utility;

public sealed class SharePointSwaggerHeaderOperationFilter : IOperationFilter
{
    private static readonly HashSet<string> SharePointActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "StreamFile",
        "StreamFileInline",
        "DownloadFile",
        "GetFileInfo",
        "CreateFolder",
        "GetSite",
        "ListDrives",
        "ListChildren"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionName = context.MethodInfo.Name;

        if (!string.Equals(actionName, "GenerateAccessToken", StringComparison.OrdinalIgnoreCase)
            && !SharePointActions.Contains(actionName))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();

        AddHeader(operation, "tenantId", "Azure tenant id.", required: true);
        AddHeader(operation, "clientId", "Azure app registration client id.", required: true);
        AddHeader(operation, "clientSecret", "Azure app registration client secret.", required: true);
        AddHeader(operation, "graphScope", "Optional Graph scope override.", required: false);
        AddHeader(operation, "grantType", "Optional OAuth grant type override.", required: false);
        AddHeader(operation, "tokenEndpoint", "Optional token endpoint override.", required: false);

        if (!SharePointActions.Contains(actionName))
            return;

        AddHeader(operation, "hostName", "Optional SharePoint host name. Defaults to environment variable hostName.", required: false);
        AddHeader(operation, "sitePath", "SharePoint site path, for example sites/Sharepoint_PluginTest.", required: true);
        AddHeader(operation, "driveId", "Optional SharePoint drive id. If provided, drive name lookup is skipped.", required: false);
        AddHeader(operation, "driveName", "Optional document library name. Defaults to Documents.", required: false);
    }

    private static void AddHeader(OpenApiOperation operation, string name, string description, bool required)
    {
        if (operation.Parameters.Any(parameter =>
            string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
            && parameter.In == ParameterLocation.Header))
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
