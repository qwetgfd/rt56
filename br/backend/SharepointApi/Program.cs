using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using Sharepoint_Plugin.Interfaces.V1_0;
using Sharepoint_Plugin.Repositories.V1_0;
using Sharepoint_Plugin.Services.V1_0;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var sp = builder.Configuration.GetSection("SharepointPlugin");

    var connectionString = sp["ConnectionString"];
    if (!string.IsNullOrWhiteSpace(connectionString))
        Environment.SetEnvironmentVariable("connectionstring", connectionString);

    var authSigningKey = sp["AuthSigningKey"];
    if (!string.IsNullOrWhiteSpace(authSigningKey))
        Environment.SetEnvironmentVariable("authsigningkey", authSigningKey);

    var authIssuer = sp["AuthIssuer"];
    if (!string.IsNullOrWhiteSpace(authIssuer))
        Environment.SetEnvironmentVariable("authissuer", authIssuer);

    var authAudience = sp["AuthAudience"];
    if (!string.IsNullOrWhiteSpace(authAudience))
        Environment.SetEnvironmentVariable("authaudience", authAudience);

    var authTokenLifetime = sp["AuthTokenLifetimeMinutes"];
    if (!string.IsNullOrWhiteSpace(authTokenLifetime))
        Environment.SetEnvironmentVariable("authtokenlifetimeminutes", authTokenLifetime);

    var authClientId = sp["AuthClientId"];
    if (!string.IsNullOrWhiteSpace(authClientId))
        Environment.SetEnvironmentVariable("authclientid", authClientId);

    var authClientSecret = sp["AuthClientSecret"];
    if (!string.IsNullOrWhiteSpace(authClientSecret))
        Environment.SetEnvironmentVariable("authclientsecret", authClientSecret);

    var graphEndpoint = sp["GraphEndpoint"];
    if (!string.IsNullOrWhiteSpace(graphEndpoint))
        Environment.SetEnvironmentVariable("sharepointgraphendpoint", graphEndpoint);

    var graphApiVersion = sp["GraphApiVersion"];
    if (!string.IsNullOrWhiteSpace(graphApiVersion))
        Environment.SetEnvironmentVariable("sharepointgraphapiversion", graphApiVersion);

    var graphTokenTemplate = sp["GraphTokenEndpointTemplate"];
    if (!string.IsNullOrWhiteSpace(graphTokenTemplate))
        Environment.SetEnvironmentVariable("sharepointgraphtokenendpointtemplate", graphTokenTemplate);

    var graphScope = sp["GraphScope"];
    if (!string.IsNullOrWhiteSpace(graphScope))
        Environment.SetEnvironmentVariable("sharepointgraphscope", graphScope);

    var graphGrantType = sp["GraphGrantType"];
    if (!string.IsNullOrWhiteSpace(graphGrantType))
        Environment.SetEnvironmentVariable("sharepointgraphgranttype", graphGrantType);

    var graphDefaultLibrary = sp["GraphDefaultLibraryName"];
    if (!string.IsNullOrWhiteSpace(graphDefaultLibrary))
        Environment.SetEnvironmentVariable("sharepointgraphdefaultlibraryname", graphDefaultLibrary);

    Environment.SetEnvironmentVariable(
        "databasecommandtimeoutseconds",
        sp["DatabaseCommandTimeoutSeconds"] ?? "30");

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<ISharePointPluginRepository, SharePointPluginRepository>();
    builder.Services.AddScoped<ISharePointPluginService, SharePointPluginService>();
    builder.Services.AddHttpClient();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("Accept-Ranges", "Content-Range", "Content-Length", "Content-Disposition", "Content-Type"));
    });

    var signingKey = Environment.GetEnvironmentVariable("authsigningkey");
    if (!string.IsNullOrWhiteSpace(signingKey))
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Environment.GetEnvironmentVariable("authissuer") ?? "SharepointApi",
                    ValidateAudience = true,
                    ValidAudience = Environment.GetEnvironmentVariable("authaudience") ?? "SharepointApi.Consumers",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        builder.Services.AddAuthorization();
    }

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SharepointApi");
        c.RoutePrefix = string.Empty;
    });

    app.UseRouting();
    app.UseCors();
    app.UseHttpsRedirection();

    if (!string.IsNullOrWhiteSpace(signingKey))
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.UseExceptionHandler(handler =>
    {
        handler.Run(async context =>
        {
            var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            var requestLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            requestLogger.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var message = app.Environment.IsDevelopment()
                ? (exception?.Message ?? "Internal server error.")
                : "Internal server error.";
            await context.Response.WriteAsJsonAsync(new { success = false, message, data = (object?)null });
        });
    });

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped unexpectedly.");
    throw;
}
finally
{
    LogManager.Shutdown();
}
