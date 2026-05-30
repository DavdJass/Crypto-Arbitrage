using Npgsql;

namespace ArbitrageBot.Infrastructure.Persistence;

/// <summary>
/// Factoría de conexiones Npgsql.
/// </summary>
public class PostgresConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
