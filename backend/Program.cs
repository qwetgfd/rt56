using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using NLog.Web;
using Sharepoint_Plugin.Constants;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Middleware;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Repositories.V_1_0;
using Sharepoint_Plugin.Services.V_1_0;
using Sharepoint_Plugin.Utility;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

Environment.SetEnvironmentVariable("sharepointGraphEndpoint", "https://graph.microsoft.com");
Environment.SetEnvironmentVariable("sharepointGraphApiVersion", "v1.0");
Environment.SetEnvironmentVariable("sharepointTokenEndpointTemplate", "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
Environment.SetEnvironmentVariable("sharepointGraphScope", "https://graph.microsoft.com/.default");
Environment.SetEnvironmentVariable("sharepointGrantType", "client_credentials");
Environment.SetEnvironmentVariable("sharepointDefaultDriveName", "Documents");
Environment.SetEnvironmentVariable("sharepointTimeoutSeconds", "300");
Environment.SetEnvironmentVariable("sharepointFileContentType", "application/octet-stream");
Environment.SetEnvironmentVariable("hostName", "");

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddHttpClient<ISharePointRepository, SharePointRepository>((sp, client) =>
{
    var auth = sp.GetRequiredService<IAuthService>();
    client.Timeout = TimeSpan.FromSeconds(auth.TimeoutSeconds);
});
builder.Services.AddScoped<ISharePointFileService, SharePointFileService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(APIResponse.Fail(MessageConstants.InvalidRequest, errors));
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("api-version");
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = MessageConstants.SwaggerTitle,
        Version = "v1",
        Description = MessageConstants.SwaggerDescription
    });
    options.OperationFilter<SharePointSwaggerHeaderOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", MessageConstants.SwaggerEndpointName);
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularDev");
app.UseAuthorization();
app.MapControllers();

app.Run();
