using System.Globalization;
using Npgsql;

namespace Actum.Tests.Shared;

/// <summary>
/// Cria um banco temporário <c>actum_test_*</c> no PostgreSQL apontado por
/// <c>ACTUM_TEST_CONNECTION_STRING</c> e o remove ao final da execução.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "ACTUM_TEST_CONNECTION_STRING";

    private const string DefaultAdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=actum;Password=actum";

    private const string DatabasePrefix = "actum_test_";

    private const string TimestampFormat = "yyyyMMddHHmmss";

    private readonly string _adminConnectionString;

    private bool _bancoCriado;

    public PostgresFixture()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)
            ?? DefaultAdminConnectionString;

        DatabaseName = $"{DatabasePrefix}{DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}_{Guid.NewGuid():N}";
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = DatabaseName }.ConnectionString;
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    /// <summary>
    /// Nome do banco temporário desta execução
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Connection string do banco temporário
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Data source do banco temporário
    /// </summary>
    public NpgsqlDataSource DataSource { get; }

    public async ValueTask InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        try
        {
            await admin.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível conectar ao PostgreSQL de testes. Verifique se o servidor está no ar e se o usuário " +
                $"'actum' existe (scripts/criar-usuario-dev.sql), ou defina {ConnectionStringVariable}.",
                ex);
        }

        await RemoverBancosAntigosAsync(admin);

        await using var criar = new NpgsqlCommand($"create database {DatabaseName}", admin);
        await criar.ExecuteNonQueryAsync();
        _bancoCriado = true;
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();

        if (!_bancoCriado)
        {
            return;
        }

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await RemoverBancoAsync(admin, DatabaseName);
    }

    private static async Task RemoverBancosAntigosAsync(NpgsqlConnection admin)
    {
        var limite = DateTime.Now.AddDays(-1);
        var antigos = new List<string>();

        await using (var consulta = new NpgsqlCommand("select datname from pg_database where datname like 'actum\\_test\\_%'", admin))
        await using (var leitor = await consulta.ExecuteReaderAsync())
        {
            while (await leitor.ReadAsync())
            {
                antigos.Add(leitor.GetString(0));
            }
        }

        foreach (var nome in antigos)
        {
            if (nome.Length < DatabasePrefix.Length + TimestampFormat.Length)
            {
                continue;
            }

            var carimbo = nome.Substring(DatabasePrefix.Length, TimestampFormat.Length);
            var valido = DateTime.TryParseExact(carimbo, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var criadoEm);
            if (valido && criadoEm < limite)
            {
                await RemoverBancoAsync(admin, nome);
            }
        }
    }

    private static async Task RemoverBancoAsync(NpgsqlConnection admin, string nome)
    {
        await using var remover = new NpgsqlCommand($"drop database if exists {nome} with (force)", admin);
        await remover.ExecuteNonQueryAsync();
    }
}
