using NLog;
using NLog.Web;
using Sharepoint_Api.Configuration;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Repositories.V_1_0;
using Sharepoint_Plugin.Services.V_1_0;

var logger = LogManager.GetCurrentClassLogger();

Environment.SetEnvironmentVariable("sharepointGraphEndpoint", "https://graph.microsoft.com");
Environment.SetEnvironmentVariable("sharepointGraphApiVersion", "v1.0");
Environment.SetEnvironmentVariable("sharepointTokenEndpointTemplate", "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
Environment.SetEnvironmentVariable("sharepointGraphScope", "https://graph.microsoft.com/.default");
Environment.SetEnvironmentVariable("sharepointGrantType", "client_credentials");
Environment.SetEnvironmentVariable("sharepointTimeoutSeconds", "300");
Environment.SetEnvironmentVariable("sharepointFileContentType", "application/octet-stream");

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SharePointOptions>(builder.Configuration.GetSection(SharePointOptions.SectionName));

var sharePointConfig = builder.Configuration.GetSection(SharePointOptions.SectionName).Get<SharePointOptions>();
if (sharePointConfig != null)
{
    if (!string.IsNullOrWhiteSpace(sharePointConfig.SharePointHostName))
        Environment.SetEnvironmentVariable("hostName", sharePointConfig.SharePointHostName.Trim());
    if (!string.IsNullOrWhiteSpace(sharePointConfig.DefaultDriveName))
        Environment.SetEnvironmentVariable("sharepointDefaultDriveName", sharePointConfig.DefaultDriveName.Trim());
    else if (!string.IsNullOrWhiteSpace(sharePointConfig.DriveName))
        Environment.SetEnvironmentVariable("sharepointDefaultDriveName", sharePointConfig.DriveName.Trim());
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddHttpClient<ISharePointRepository, SharePointRepository>((sp, client) =>
{
    var auth = sp.GetRequiredService<IAuthService>();
    client.Timeout = TimeSpan.FromSeconds(auth.TimeoutSeconds);
});
builder.Services.AddScoped<ISharePointFileService, SharePointFileService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Accept-Ranges", "Content-Range", "Content-Length")
              .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularDev");
app.UseAuthorization();
app.MapControllers();

app.Run();
