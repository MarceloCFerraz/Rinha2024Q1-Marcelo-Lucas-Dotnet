using System.Data;
using Npgsql; // (Ensure you have the Npgsql provider package)

public class DatabaseHelper
{
    private string _connectionString;

    public DatabaseHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public CreateDataSource()
    {
        return await using var dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public IDbConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
