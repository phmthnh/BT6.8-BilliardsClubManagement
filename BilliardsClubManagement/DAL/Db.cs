using System.Data;
using Microsoft.Data.SqlClient;

namespace BilliardsClubManagement.DAL;

internal static class Db
{
    internal static SqlConnection Open()
    {
        var settings = Environment.GetEnvironmentVariable("BILLIARDSDB_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=BilliardsDB;Integrated Security=True;Encrypt=False";
        var connection = new SqlConnection(settings);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction,
        string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    internal static DataTable Query(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var command = Command(connection, null, sql, parameters);
        using var reader = command.ExecuteReader();
        var result = new DataTable();
        result.Load(reader);
        return result;
    }

    internal static int Execute(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var command = Command(connection, null, sql, parameters);
        return command.ExecuteNonQuery();
    }

    internal static T InTransaction<T>(Func<SqlConnection, SqlTransaction, T> action)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = action(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            if (transaction.Connection is not null)
                transaction.Rollback();
            throw;
        }
    }
}
