using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NLog;

namespace Sharepoint_Plugin.Utilities;

public static class DbExecutor
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static async Task<T> ExecuteAsync<T>(
        string connectionString,
        int commandTimeout,
        string commandText,
        Func<SqlConnection, Task<T>> action,
        CommandType commandType = CommandType.StoredProcedure)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            return await action(connection).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "DB operation failed: {Command}", commandText);
            throw;
        }
    }
}
