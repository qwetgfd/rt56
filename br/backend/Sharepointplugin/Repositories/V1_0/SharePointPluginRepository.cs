using System.Data;
using Dapper;
using Sharepoint_Plugin.Constants;
using static Sharepoint_Plugin.Constants.Constants;
using Sharepoint_Plugin.Interfaces.V1_0;
using Sharepoint_Plugin.Models.Response;
using Sharepoint_Plugin.Utilities;

namespace Sharepoint_Plugin.Repositories.V1_0;

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
        var result = await DbExecutor.ExecuteAsync(
            _connectionString, _commandTimeout, "sel_applicationtypes",
            connection => connection.QueryAsync<Application.ApplicationType>(
                "sel_applicationtypes", commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout),
            CommandType.StoredProcedure).ConfigureAwait(false);
        return result.ToList();
    }

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, int? typeId)
    {
        var result = await DbExecutor.ExecuteAsync(
            _connectionString, _commandTimeout, "sel_application",
            connection => connection.QueryAsync<Application>(
                "sel_application", new { Ownerkey = ownerKey, Applicationtypeid = typeId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout),
            CommandType.StoredProcedure).ConfigureAwait(false);
        return result.ToList();
    }

    public Task<Application?> GetApplicationByIdAsync(Guid applicationId)
        => DbExecutor.ExecuteAsync(
            _connectionString, _commandTimeout, "sel_applicationbyid",
            connection => connection.QuerySingleOrDefaultAsync<Application>(
                "sel_applicationbyid", new { Applicationid = applicationId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout),
            CommandType.StoredProcedure);

    public Task<Application> SaveApplicationAsync(Application application)
        => DbExecutor.ExecuteAsync(
            _connectionString, _commandTimeout, "commit_application",
            connection => connection.QuerySingleAsync<Application>(
                "commit_application",
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
                    Notes = application.Notes
                },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout),
            CommandType.StoredProcedure);

    public async Task<bool> DeleteApplicationAsync(Guid applicationId)
    {
        var affected = await DbExecutor.ExecuteAsync(
            _connectionString, _commandTimeout, "commit_applicationdelete",
            connection => connection.ExecuteScalarAsync<int>(
                "commit_applicationdelete", new { Applicationid = applicationId },
                commandType: CommandType.StoredProcedure, commandTimeout: _commandTimeout),
            CommandType.StoredProcedure).ConfigureAwait(false);
        return affected > 0;
    }
}
