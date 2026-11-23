namespace Locadora.Domain;

public enum Combustivel
{
    Gasolina,
    Gas,
    Diesel,
    Alcool,
}

public sealed class Veiculo : EntidadeEmpresa
{
    public Guid GrupoId { get; set; }
    public Grupo? Grupo { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Cor { get; set; } = string.Empty;
    public int Ano { get; set; }
    public Combustivel Combustivel { get; set; }
    public decimal CapacidadeLitros { get; set; }

    public static string NormalizarPlaca(string? placa) =>
        new((placa ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static Veiculo Criar(Guid grupoId, string? placa, string? marca, string? modelo, string? cor,
        int? ano, Combustivel combustivel, decimal? capacidadeLitros, Guid empresaId, DateTimeOffset agora)
    {
        var dados = Normalizar(grupoId, placa, marca, modelo, cor, ano, combustivel, capacidadeLitros);
        return new Veiculo
        {
            GrupoId = dados.GrupoId,
            Placa = dados.Placa,
            Marca = dados.Marca,
            Modelo = dados.Modelo,
            Cor = dados.Cor,
            Ano = dados.Ano,
            Combustivel = dados.Combustivel,
            CapacidadeLitros = dados.CapacidadeLitros,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void Atualizar(Guid grupoId, string? placa, string? marca, string? modelo, string? cor,
        int? ano, Combustivel combustivel, decimal? capacidadeLitros, DateTimeOffset agora)
    {
        var dados = Normalizar(grupoId, placa, marca, modelo, cor, ano, combustivel, capacidadeLitros);
        GrupoId = dados.GrupoId;
        Placa = dados.Placa;
        Marca = dados.Marca;
        Modelo = dados.Modelo;
        Cor = dados.Cor;
        Ano = dados.Ano;
        Combustivel = dados.Combustivel;
        CapacidadeLitros = dados.CapacidadeLitros;
        AtualizadoEm = agora;
    }

    private static Dados Normalizar(Guid grupoId, string? placa, string? marca, string? modelo, string? cor,
        int? ano, Combustivel combustivel, decimal? capacidadeLitros)
    {
        var erros = new List<ErroValidacao>();
        if (grupoId == Guid.Empty) erros.Add(new ErroValidacao("grupoId", "Selecione um grupo."));
        var placaNormalizada = NormalizarPlaca(placa);
        Validador.Adicionar(erros, "placa", Validador.Obrigatorio(placaNormalizada, 16));
        Validador.Adicionar(erros, "marca", Validador.Obrigatorio(marca, 200));
        Validador.Adicionar(erros, "modelo", Validador.Obrigatorio(modelo, 200));
        Validador.Adicionar(erros, "cor", Validador.Obrigatorio(cor, 100));
        Validador.Adicionar(erros, "ano", Validador.InteiroNaoNegativo(ano));
        Validador.Adicionar(erros, "capacidadeLitros", Validador.DecimalPositivo(capacidadeLitros));
        if (!Enum.IsDefined(combustivel))
            erros.Add(new ErroValidacao("combustivel", "Selecione um combustível válido."));
        Validador.Lancar(erros);

        return new Dados(grupoId, placaNormalizada, marca!.Trim(), modelo!.Trim(), cor!.Trim(),
            ano!.Value, combustivel, capacidadeLitros!.Value);
    }

    private sealed record Dados(Guid GrupoId, string Placa, string Marca, string Modelo, string Cor,
        int Ano, Combustivel Combustivel, decimal CapacidadeLitros);
}
