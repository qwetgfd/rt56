using Microsoft.Extensions.Caching.Distributed;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using System.IdentityModel.Tokens.Jwt;
using Teleperformance.DataIngestion.API.Extentions;
using Teleperformance.DataIngestion.API.Middleware;
using Teleperformance.DataIngestion.API.SignalRHubs;
using Teleperformance.DataIngestion.Common;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
try
{
    // Production:
    // var decryptedConnectionString = KeyVault.GetKeyVaultValue("TPDataIngestionV2ConnectionString").Result;
    // LogManager.Configuration.Variables["DecryptedConnectionString"] = decryptedConnectionString;

    // Local environment — set useLocalEnvironment to false for production deploy
    const bool useLocalEnvironment = true;

    string decryptedConnectionString;
    if (useLocalEnvironment)
    {
        #region Sharepoint Workspace - AY
        decryptedConnectionString =
            "";

        Environment.SetEnvironmentVariable("connectionstring", decryptedConnectionString);
        Environment.SetEnvironmentVariable("authsigningkey", "local-dev-signing-key-not-for-production-use");
        Environment.SetEnvironmentVariable("authissuer", "Teleperformance.DataIngestion.API");
        Environment.SetEnvironmentVariable("authaudience", "Teleperformance.DataIngestion.API.Consumers");
        Environment.SetEnvironmentVariable("authtokenlifetimeminutes", "60");
        Environment.SetEnvironmentVariable("authclientid", "Teleperformance.DataIngestion.API");
        Environment.SetEnvironmentVariable("authclientsecret", "local-dev-signing-key-not-for-production-use");
        Environment.SetEnvironmentVariable("TPDataIngestionClientId", "Teleperformance.DataIngestion.API");
        Environment.SetEnvironmentVariable("TPDataIngestionTenantId", "Teleperformance.DataIngestion.API.Consumers");
        Environment.SetEnvironmentVariable("TPDataIngestionClientSecret", "local-dev-signing-key-not-for-production-use");
        Environment.SetEnvironmentVariable("sharepointgraphendpoint", "https://graph.microsoft.com");
        Environment.SetEnvironmentVariable("sharepointgraphapiversion", "v1.0");
        Environment.SetEnvironmentVariable("sharepointgraphtokenendpointtemplate", "https://login.microsoftonline.com/{0}/oauth2/v2.0/token");
        Environment.SetEnvironmentVariable("sharepointgraphscope", "https://graph.microsoft.com/.default");
        Environment.SetEnvironmentVariable("sharepointgraphgranttype", "client_credentials");
        Environment.SetEnvironmentVariable("sharepointgraphdefaultlibraryname", "Documents");
        Environment.SetEnvironmentVariable("databasecommandtimeoutseconds", "30");
        Environment.SetEnvironmentVariable("sharepointinternaltenantid", "");
        Environment.SetEnvironmentVariable("sharepointinternalclientid", "");
        Environment.SetEnvironmentVariable("sharepointinternalclientsecret", "");
        Environment.SetEnvironmentVariable("sharepointinternalhostname", "");
        Environment.SetEnvironmentVariable("tpgraphclientid", "");
        Environment.SetEnvironmentVariable("tpgraphclientsecret", "");
        Environment.SetEnvironmentVariable("tpgraphtenantid", "");
        Environment.SetEnvironmentVariable("tpgraphbaseurl", "https://graph.microsoft.com/v1.0");
        Environment.SetEnvironmentVariable("tpgraphoauthscope", "https://graph.microsoft.com/.default");
        Environment.SetEnvironmentVariable("tpgraphtokenendpoint", "https://login.microsoftonline.com");
        Environment.SetEnvironmentVariable("tpgraphuserfields", "id,displayName,givenName,surname,userPrincipalName,mail,jobTitle,department,officeLocation,mobilePhone,companyName");
        #endregion
    }
    else
    {
        decryptedConnectionString = KeyVault.GetKeyVaultValue("TPDataIngestionV2ConnectionString").Result;
    }

    var nlogConfiguration = LogManager.Configuration;
    if (nlogConfiguration is not null)
    {
        nlogConfiguration.Variables["DecryptedConnectionString"] = decryptedConnectionString;
    }

    var builder = WebApplication.CreateBuilder(args);
    // Disable Kestrel 'Server' header
    builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);


    // Add services to the container.
    // builder.Services.AddControllers();
    // builder.Services.AddApplicationServices(builder.Configuration);
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = false;

            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = new List<string>();

                foreach (var modelState in context.ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        // Skip generic "field is required" errors for the root object/parameter
                        if (modelState.Key == "request" || modelState.Key == "$" || modelState.Key == "")
                        {
                            // Only add JSON parsing errors, skip generic required messages
                            if (error.Exception != null)
                            {
                                errors.Add(error.Exception.Message);
                            }
                            else if (!string.IsNullOrEmpty(error.ErrorMessage) &&
                                     !error.ErrorMessage.Contains("field is required"))
                            {
                                errors.Add(error.ErrorMessage);
                            }
                            continue;
                        }

                        // Handle JSON conversion errors with user-friendly messages
                        string errorMessage;
                        if (error.Exception != null && error.Exception.Message.Contains("JSON value could not be converted"))
                        {
                            // Extract property name from modelState.Key (e.g., "$.regionId" -> "RegionId")
                            var propertyName = modelState.Key.TrimStart('$', '.').Trim();
                            if (!string.IsNullOrEmpty(propertyName))
                            {
                                // Capitalize first letter
                                propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                                errorMessage = $"{propertyName} has an invalid value or type.";
                            }
                            else
                            {
                                errorMessage = error.Exception.Message;
                            }
                        }
                        else
                        {
                            // Use custom error message if available, otherwise use exception message
                            errorMessage = !string.IsNullOrEmpty(error.ErrorMessage)
                                ? error.ErrorMessage
                                : error.Exception?.Message ?? "Invalid value";
                        }

                        errors.Add(errorMessage);
                    }
                }

                var response = new
                {
                    statusCode = 400,
                    message = "Validation failed",
                    errors = errors
                };

                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response)
                {
                    StatusCode = 400
                };
            };
        });
    builder.Services.AddApplicationServices(builder.Configuration);

    //builder.Services.AddStackExchangeRedisCache(options => {
    //    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    //    options.InstanceName = "auth",
    //});

    builder.Services.AddIdentityService();
    builder.Services.AddHttpClient();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSignalR();
    //builder.Services.AddSwaggerGen();
    builder.Services.AddSwaggerGen(options =>
    {
        options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    });

    builder.Services.AddDistributedMemoryCache(); // provides IDistributedCache for Session

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(20);

        options.Cookie.Name = ".TP.Session";

#if DEBUG
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
#endif

#if !DEBUG
options.Cookie.HttpOnly = true;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif

        options.Cookie.IsEssential = true;

    });






    // NLog: Setup NLog for Dependency injection
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.Host.UseNLog();

    builder.Logging.ClearProviders();  // Configure logging
    builder.Services.AddLogging(loggingBuilder =>
    {

        loggingBuilder.ClearProviders();
        loggingBuilder.AddNLog(new NLogProviderOptions
        {
            CaptureMessageTemplates = true,
            CaptureMessageProperties = true
        });
    });

    //Add Cors Policy
    builder.Services.AddCors(p => p.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins("http://localhost:5173"
            #region Sharepoint Workspace - AY
            , "http://localhost:4200"
            #endregion
            )
        .AllowAnyMethod().AllowAnyHeader()
        .WithExposedHeaders("X-Token-Revoked")
        .AllowCredentials(); //required for SignalR
    }));


    var app = builder.Build();
    // app.UseForwardedHeaders();
    // Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // app.UseHttpsRedirection();
    #region Sharepoint Workspace - AY
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    #endregion
    app.UseCors("CorsPolicy");
    // app.UseMiddleware<ForbiddenHeaderMiddleware>();
    //app.UseMiddleware<SecurityHeadersMiddleware>(); // Security Headers middleware
    //app.UseMiddleware<ExceptionMiddleware>(); // Configure exception middleware
    #region Sharepoint Workspace - AY
    // Disabled locally; KeyVault token decrypt breaks SharePoint /api/applications without Azure
    // app.UseMiddleware<TokenDecryptionMiddleware>();
    #endregion

    app.UseAuthentication();
    app.UseMiddleware<TokenVersionMiddleware>();
    app.UseAuthorization();
    app.UseSession();
    //app.UseMiddleware<IdleTimeoutMiddleware>();

    app.MapControllers();
    app.MapHub<StatusHub>("/statusHub");
    app.Run();
}
catch (Exception exception)
{

    // NLog: catch setup errors
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    NLog.LogManager.Shutdown();
}
