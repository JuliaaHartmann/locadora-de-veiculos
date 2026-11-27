using System.Data;
using Locadora.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string servidor = "locacar.database.windows.net";
const string banco = "locacar-db";
const string usuarioApi = "locacar_api";

var etapa = "validar SQLSERVER_CONNECTIONSTRING";
try
{
    var configuracaoAdmin = LerConexao("SQLSERVER_CONNECTIONSTRING", runtime: false);
    etapa = "validar SQLSERVER_RUNTIME_CONNECTIONSTRING";
    var configuracaoRuntime = LerConexao("SQLSERVER_RUNTIME_CONNECTIONSTRING", runtime: true);
    if (configuracaoRuntime.Password == configuracaoAdmin.Password)
    {
        Console.Error.WriteLine("As credenciais de migrations e de runtime precisam de senhas distintas.");
        return 1;
    }

    etapa = "validar conectividade com o Azure SQL";
    await using var admin = new SqlConnection(configuracaoAdmin.ConnectionString);
    await AbrirConexaoAsync(admin);
    await using (var consulta = new SqlCommand("SELECT 1 WHERE DB_NAME() = N'locacar-db';", admin))
    {
        if (await consulta.ExecuteScalarAsync() is not 1)
            throw new InvalidOperationException();
    }
    Console.WriteLine("Conexão autenticada com locacar-db confirmada.");

    etapa = "aplicar as migrations";
    var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(admin).Options;
    await using (var contexto = new LocadoraDbContext(opcoes, new ContextoEmpresa()))
        await contexto.Database.MigrateAsync();
    Console.WriteLine("Migrations verificadas e atualizadas.");

    etapa = "configurar o usuário contido e validar os grants na transação";
    await ConfigurarUsuarioAsync(admin, configuracaoRuntime.Password);

    etapa = "confirmar a autenticação de runtime após o commit (migrations, usuário e grants já persistidos)";
    await ConfirmarAutenticacaoRuntimeAsync(configuracaoRuntime.ConnectionString);
    Console.WriteLine("Matriz de permissões validada e autenticação de runtime confirmada.");
    return 0;
}
catch (SqlException erro)
{
    // Não registrar a exceção: ela pode conter detalhes do comando ou da conexão.
    var categoria = erro.Number switch
    {
        40615 or 47073 => "acesso de rede ou firewall bloqueado",
        40613 => "banco temporariamente indisponível",
        18456 or 4060 => "autenticação ou acesso ao banco recusado",
        -2 => "tempo limite excedido",
        _ => "erro do SQL ou do transporte",
    };
    Console.Error.WriteLine($"Falha ao {etapa}: {categoria} (SQL {erro.Number}).");
    return 1;
}
catch (Exception)
{
    Console.Error.WriteLine($"Falha ao {etapa}. Revise a configuração da etapa.");
    return 1;
}

static async Task AbrirConexaoAsync(SqlConnection conexao)
{
    const int maxReconexoes = 2;
    for (var tentativa = 0; ; tentativa++)
    {
        try
        {
            await conexao.OpenAsync();
            return;
        }
        catch (SqlException erro) when (erro.Number == 40613 && tentativa < maxReconexoes)
        {
            Console.WriteLine($"Azure SQL indisponível (40613); reconexão {tentativa + 1}/{maxReconexoes}.");
            await Task.Delay(TimeSpan.FromSeconds(15 * (tentativa + 1)));
        }
    }
}

static SqlConnectionStringBuilder LerConexao(string secret, bool runtime)
{
    var valor = Environment.GetEnvironmentVariable(secret);
    if (string.IsNullOrWhiteSpace(valor))
        throw new InvalidOperationException();

    var entrada = new SqlConnectionStringBuilder(valor);
    var destino = entrada.DataSource.ToLowerInvariant();
    if (destino is not (servidor or $"tcp:{servidor}" or $"{servidor},1433" or $"tcp:{servidor},1433") ||
        !string.Equals(entrada.InitialCatalog, banco, StringComparison.OrdinalIgnoreCase) ||
        entrada.IntegratedSecurity ||
        entrada.Authentication is not (SqlAuthenticationMethod.NotSpecified or SqlAuthenticationMethod.SqlPassword) ||
        entrada.TrustServerCertificate || entrada.Encrypt == SqlConnectionEncryptOption.Optional ||
        string.IsNullOrWhiteSpace(entrada.UserID) || string.IsNullOrWhiteSpace(entrada.Password) ||
        entrada.Password.Length > 128 || entrada.Password.Any(char.IsControl) ||
        (runtime ? entrada.UserID != usuarioApi :
            string.Equals(entrada.UserID, usuarioApi, StringComparison.OrdinalIgnoreCase)))
    {
        throw new ArgumentException();
    }

    // Reconstruir só com as opções aprovadas; aliases e opções de redirecionamento não são propagados.
    return new SqlConnectionStringBuilder
    {
        DataSource = $"tcp:{servidor},1433",
        InitialCatalog = banco,
        Authentication = SqlAuthenticationMethod.SqlPassword,
        UserID = entrada.UserID,
        Password = entrada.Password,
        Encrypt = entrada.Encrypt,
        TrustServerCertificate = false,
        ConnectTimeout = 60,
        ConnectRetryCount = 0,
        Pooling = false,
    };
}

static async Task ConfigurarUsuarioAsync(SqlConnection admin, string senha)
{
    var pastaSql = Path.Combine(AppContext.BaseDirectory, "sql");
    var permissoes = await File.ReadAllTextAsync(Path.Combine(pastaSql, "permissoes-api.sql"));

    await using var transacao = (SqlTransaction)await admin.BeginTransactionAsync();
    await using var consultarPrincipal = new SqlCommand(
        "SELECT DATABASE_PRINCIPAL_ID(N'locacar_api');", admin, transacao);
    if (await consultarPrincipal.ExecuteScalarAsync() is DBNull)
    {
        var criacao = await File.ReadAllTextAsync(Path.Combine(pastaSql, "criar-usuario.sql"));
        await using var criarUsuario = new SqlCommand(criacao, admin, transacao);
        criarUsuario.Parameters.Add("@senha", SqlDbType.NVarChar, 128).Value = senha;
        await criarUsuario.ExecuteNonQueryAsync();
    }

    await using var concederPermissoes = new SqlCommand(permissoes, admin, transacao);
    await concederPermissoes.ExecuteNonQueryAsync();
    await transacao.CommitAsync();
}

static async Task ConfirmarAutenticacaoRuntimeAsync(string conexao)
{
    await using var runtime = new SqlConnection(conexao);
    await AbrirConexaoAsync(runtime);
    await using var consulta = new SqlCommand(
        "SELECT 1 WHERE DB_NAME() = N'locacar-db' AND USER_NAME() = N'locacar_api';", runtime);
    if (await consulta.ExecuteScalarAsync() is not 1)
        throw new InvalidOperationException();
}
