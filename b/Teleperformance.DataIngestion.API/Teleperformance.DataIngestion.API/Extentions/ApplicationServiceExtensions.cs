using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.ServiceModel;
using Teleperformance.DataAssist;
using Teleperformance.DataIngestion.API.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists;
using Teleperformance.DataIngestion.DataAccess.Repository;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v3._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v4._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v4._1;
using Teleperformance.DataIngestion.DataAccess.Repository.v4._1.DataAssists;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v2._0;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v4._0;
using Teleperformance.DataIngestion.DataAccess.Services.v4._1;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Repositories.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Services.V1_0;
using ZstdSharp.Unsafe;

namespace Teleperformance.DataIngestion.API.Extentions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApiVersioning(config =>
            {
                //config.AssumeDefaultVersionWhenUnspecified = true;
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new ApiVersion(1, 0);
                // Combine (or not) API Versioning Mechanisms:
                config.ApiVersionReader = ApiVersionReader.Combine(
                   // The Default versioning mechanism which reads the API version from the "api-version" Query String paramater.
                   // new QueryStringApiVersionReader("api-version")
                   // Use the following, if you would like to specify the version as a custom HTTP Header.
                   new HeaderApiVersionReader("x-tpdi-api-version"),
                //Use the following, if you would like to specify the version as a Media Type Header.
                  new MediaTypeApiVersionReader("x-tpdi-api-version")
                );


            }).AddMvc();

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 367001600;
            });
            services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = 367001600;
                x.MultipartBodyLengthLimit = 367001600;
            });

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true; //prevent JS access
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; //only over https
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            services.AddScoped<IDapperService, DapperService>();
            services.AddScoped<IFileLoadingProcessConfiguration, FileLoadingProcessConfigurationService>();
            services.AddScoped<IFileLoadingProcessConfigurationRepository, FileLoadingProcessConfigurationRepository>();
            services.AddScoped<ISMBLibraryServices, SMBLibraryServices>();
            services.AddScoped<ISMBLibraryRepository, SMBLibraryRepository>();
            services.AddScoped<IFileLoadingProcessService, FileLoadingProcessService>();
            services.AddScoped<IFileProcessingService, FileProcessingService>();
            //IFileLoadingProcessRepository
            services.AddScoped<IFileLoadingProcessRepository, FileLoadingProcessRepository>();
            services.AddScoped<IBlobStorageService, BlobStorageService>();
            services.AddScoped<IBronzeDbRepository, FLPBronzeDbRepository>();
            services.AddScoped<ICsvToParquetConverterService, CsvToParquetConverterService>();
            services.AddScoped<ITxtToParquetConverterService, TxtToParquetConverterService>();
            services.AddScoped<IExcelToParquetConverterService, ExcelToParquetConverterService>();
            services.AddScoped<ISchemaValidationService, SchemaValidationService>();
            services.AddScoped<IStatusService, StatusService>();
            services.AddScoped<IStatusRepository, StatusRepository>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IProcessConfigRepository, ProcessConfigRepository>();
            services.AddScoped<IProcessConfigRepositoryV3, ProcessConfigRepositoryV3>();
            services.AddScoped<IProcessConfigService, ProcessConfigService>();
            services.AddScoped<IProcessConfigServiceV3, ProcessConfigServiceV3>();
            services.AddScoped<ICache, Cache>();
            services.AddAutoMapper(cfg => { }, typeof(AutoMapperProfiles).Assembly);
            services.AddMemoryCache();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IProcessConfigurationService, ProcessConfigurationService>();
            services.AddScoped<IProcessConfigurationRepository, ProcessConfigurationRepository>();

            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IEmailNotificationRepository, EmailNotificationRepository>();
            services.AddScoped<IEmailNotificationService, EmailNotificationService>();
            services.AddHttpContextAccessor();
            services.AddScoped<IHeaderService, HeaderService>();
            services.AddScoped<IFileProcessService, FileProcessService>();
            services.AddScoped<ITextToParquetService, TxtToParquetService>();
            services.AddScoped<IValidateSchemaRepository, ValidateSchemaRepository>();
            services.AddScoped<IValidateSchemaService, ValidateSchemaService>();
            services.AddScoped<IFlpProcessingService, FlpProcessingService>();
            services.AddScoped<ICsvToParquetService, CsvToParquetService>();
            services.AddScoped<IExcelToParquetService, ExcelToParquetService>();
            services.AddScoped<IChangeProcessStatusService, ChangeProcessStatusService>();
            services.AddScoped<IChangeProcessStatusRepository, ChangeProcessStatusRepository>();
            services.AddScoped<IDataSliceAPIService, DataSliceAPIService>();
            services.AddScoped<IFileProcessServiceV4, FileProcessServiceV4>();
            services.AddScoped<IFlpProcessingServiceV4, FlpProcessingServiceV4>();
            services.AddScoped<IExcelToParquetServiceV4, ExcelToParquetServiceV4>();
            services.AddScoped<ITextToParquetServiceV4, TxtToParquetServiceV4>();
            services.AddScoped<ICsvToParquetServiceV4, CsvToParquetServiceV4>();
            services.AddScoped<IDatabricksAPIDbRepository, DatabricksAPIDbRepository>();
            services.AddScoped<IDataBricksJobStatusService, DataBricksJobStatusService>();
            services.AddScoped<IProcessConfigRepositoryV4, ProcessConfigRepositoryV4>();
            //
            services.AddScoped<IExcelToParquetServiceV4_1, ExcelToParquetServiceV4_1>();
            services.AddScoped<IFileProcessServiceV4_1, FileProcessServiceV4_1>();
            services.AddScoped<IFlpProcessingServiceV4_1, FlpProcessingServiceV4_1>();
            services.AddScoped<IProcessConfigurationServiceV4_1, ProcessConfigurationServiceV4_1>();
            services.AddScoped<IProcessConfigurationRepositoryV4_1, ProcessConfigurationRepositoryV4_1>();
            services.AddScoped<IFileLoadingProcessConfigurationRepositoryV4_1, FileLoadingProcessConfigurationRepositoryV4_1>();
            services.AddScoped<IFileLoadingProcessConfigurationServiceV4_1, FileLoadingProcessConfigurationServiceV4_1>();
            services.AddScoped<IBlobStorageServiceV4_1, BlobStorageServiceV4_1>();
            services.AddScoped<IValidateSchemaServiceV4_1, ValidateSchemaServiceV4_1>();
            services.AddScoped<IDatabricksDbRepository, FlpDatabricksDbRepository>();
            services.AddScoped<ICsvToParquetServiceV4_1, CsvToParquetServiceV4_1>();

            #region Sharepoint Workspace - AY
            services.AddScoped<ISharePointPluginRepository, SharePointPluginRepository>();
            services.AddScoped<ISharePointPluginService, SharePointPluginService>();
            services.AddScoped<ISharePointUserContextService, SharePointUserContextService>();
            services.AddScoped<IAzureService, AzureService>();
            #endregion

            //services.AddScoped<IStatusServiceV4_1, StatusServiceV4_1>();
            //services.AddScoped<IStatusRepositoryV4_1, StatusRepositoryV4_1>();

            services.AddScoped<IEIBServiceV4_1, EIBServiceV4_1>();
            services.AddScoped<IEIBRepositoryV4_1, EIBRepositoryV4_1>();

            services.AddScoped<IDataValidationServiceV4_1, DataAssists>();
            services.AddScoped<IDataValidationRepositoryV4_1, DataValidationRepository>();
            services.AddScoped<ILandingLayerService, LandingLayerService>();
            services.AddScoped<ILandingLayerRepository, LandingLayerRepository>();
            services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(500));
            services.AddHostedService<QueuedBackgroundWorker>();
            return services;
        }
    }
}
