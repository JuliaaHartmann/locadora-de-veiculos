namespace Locadora.Application;

public static class PoliticaSessao
{
    public static readonly TimeSpan AccessToken = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SessaoAbsoluta = TimeSpan.FromHours(8);
    public static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromSeconds(30);

    public static bool Expirada(DateTimeOffset agora, DateTimeOffset expiraEm) => expiraEm <= agora;
}

public static class PoliticaRateLimit
{
    public const int RegistroPermitido = 3;
    public const int LoginPermitido = 5;
    public static readonly TimeSpan RegistroJanela = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan LoginJanela = TimeSpan.FromMinutes(1);
}
