using Microsoft.Data.SqlClient;

namespace Entrada.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Entrada")
            ?? throw new InvalidOperationException("Configura ConnectionStrings:Entrada mediante secretos de desarrollo o variables de entorno.");
    }

    public SqlConnection Create() => new(_connectionString);
}
