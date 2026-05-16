using Npgsql;

namespace PAOS.Tests.E2E.Helpers;

public static class CleanupHelper
{
    public static async Task DeleteTablesAsync(NpgsqlDataSource db, params string[] tables)
    {
        await using var conn = await db.OpenConnectionAsync();
        foreach (var table in tables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM \"{table}\"";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public static async Task<long> CountRowsAsync(NpgsqlDataSource db, string table, string whereClause, params NpgsqlParameter[] parameters)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\" WHERE {whereClause}";
        cmd.Parameters.AddRange(parameters);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
