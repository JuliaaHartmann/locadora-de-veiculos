namespace Locadora.Domain;

public sealed class Endereco
{
    public string Estado { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;

    public static Endereco Criar(string? estado, string? cidade, string? bairro, string? rua, string? numero,
        List<ErroValidacao> erros)
    {
        var normalizado = new Endereco
        {
            Estado = (estado ?? string.Empty).Trim(),
            Cidade = (cidade ?? string.Empty).Trim(),
            Bairro = (bairro ?? string.Empty).Trim(),
            Rua = (rua ?? string.Empty).Trim(),
            Numero = (numero ?? string.Empty).Trim(),
        };
        Validador.Adicionar(erros, "endereco.estado", Validador.Obrigatorio(normalizado.Estado, 100));
        Validador.Adicionar(erros, "endereco.cidade", Validador.Obrigatorio(normalizado.Cidade, 200));
        Validador.Adicionar(erros, "endereco.bairro", Validador.Obrigatorio(normalizado.Bairro, 200));
        Validador.Adicionar(erros, "endereco.rua", Validador.Obrigatorio(normalizado.Rua, 200));
        Validador.Adicionar(erros, "endereco.numero", Validador.Obrigatorio(normalizado.Numero, 40));
        return normalizado;
    }
}
