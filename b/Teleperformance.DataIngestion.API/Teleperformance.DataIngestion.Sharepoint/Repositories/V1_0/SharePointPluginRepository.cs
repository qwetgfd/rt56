using System.Data;
using System.Text.Json;
using Dapper;
using Teleperformance.DataIngestion.Sharepoint.Constants;
using static Teleperformance.DataIngestion.Sharepoint.Constants.Constants;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
using Teleperformance.DataIngestion.Sharepoint.Utilities;

namespace Teleperformance.DataIngestion.Sharepoint.Repositories.V1_0;

public class SharePointPluginRepository : ISharePointPluginRepository
{
    private readonly string _connectionString;
    private readonly int _commandTimeout;

    public SharePointPluginRepository()
    {
        _connectionString = Environment.GetEnvironmentVariable("connectionstring")
            ?? throw new InvalidOperationException(ConnectionStringMissing);

        var timeoutEnv = Environment.GetEnvironmentVariable("databasecommandtimeoutseconds");
        _commandTimeout = !string.IsNullOrWhiteSpace(timeoutEnv) && int.TryParse(timeoutEnv, out var parsed)
            ? parsed
            : 30;
    }

    public async Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync()
    {
        var result = await DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "sel_applicationtypes",
            connection => connection.QueryAsync<Application.ApplicationType>("sel_applicationtypes",
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout)).ConfigureAwait(false);
        return result.ToList();
    }

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, int? typeId)
    {
        var result = await DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "sel_application",
            connection => connection.QueryAsync<Application>("sel_application",
                new { Ownerkey = ownerKey, Applicationtypeid = typeId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout)).ConfigureAwait(false);
        return result.ToList();
    }

    public Task<Application?> GetApplicationByIdAsync(Guid applicationId)
        => DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "sel_applicationbyid",
            connection => connection.QuerySingleOrDefaultAsync<Application>("sel_applicationbyid",
                new { Applicationid = applicationId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout));

    public Task<Application> SaveApplicationAsync(Application application)
        => DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "commit_application",
            connection => connection.QuerySingleAsync<Application>("commit_application",
                new
                {
                    Applicationid = application.ApplicationId == Guid.Empty ? (Guid?)null : application.ApplicationId,
                    Applicationtypeid = application.ApplicationTypeId,
                    Ownerkey = application.OwnerKey,
                    Displayname = application.DisplayName,
                    Tenantid = application.TenantId,
                    Clientid = application.ClientId,
                    Clientsecret = application.ClientSecret,
                    Hostname = application.HostName,
                    Sitename = application.SiteName,
                    Libraryname = application.LibraryName,
                    Consumerclientid = application.ConsumerClientId,
                    Consumersecret = application.ConsumerSecret,
                    Owner = application.Owner,
                    Ownerupn = application.OwnerUpn,
                    Coowner = application.CoOwner,
                    Coownerupn = application.CoOwnerUpn,
                    Notes = application.Notes
                },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout));

    public Task<IReadOnlyList<ApplicationSite>> GetApplicationSitesAsync(Guid? applicationId = null)
        => DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "sel_applicationsites", async connection =>
        {
            var result = await connection.QueryAsync<ApplicationSite>("sel_applicationsites",
                new { Applicationid = applicationId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout).ConfigureAwait(false);
            return (IReadOnlyList<ApplicationSite>)result.ToList();
        });

    public Task<IReadOnlyList<ApplicationSite>> SaveApplicationSitesAsync(Guid applicationId, IReadOnlyList<ApplicationSite> sites)
    {
        var payload = sites.Select((s, index) => new
        {
            hostName = s.HostName,
            siteName = s.SiteName,
            libraryName = s.LibraryName,
            folderPath = s.FolderPath,
            sortOrder = index
        });
        var json = JsonSerializer.Serialize(payload);
        return DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "commit_applicationsites", async connection =>
        {
            var result = await connection.QueryAsync<ApplicationSite>("commit_applicationsites",
                new { Applicationid = applicationId, SitesJson = json },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout).ConfigureAwait(false);
            return (IReadOnlyList<ApplicationSite>)result.ToList();
        });
    }

    public async Task<bool> DeleteApplicationAsync(Guid applicationId)
    {
        var affected = await DbExecutor.ExecuteAsync(_connectionString, _commandTimeout, "commit_applicationdelete",
            connection => connection.ExecuteScalarAsync<int>("commit_applicationdelete",
                new { Applicationid = applicationId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout)).ConfigureAwait(false);
        return affected > 0;
    }
}
