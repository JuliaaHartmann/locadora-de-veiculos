namespace Locadora.Domain;

public sealed class Condutor : EntidadeEmpresa
{
    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Cnh { get; set; } = string.Empty;
    public DateOnly ValidadeCnh { get; set; }
    public string Telefone { get; set; } = string.Empty;

    public static Condutor Criar(Guid clienteId, string? nome, string? email, string? cpf, string? cnh,
        DateOnly? validadeCnh, string? telefone, Guid empresaId, DateTimeOffset agora)
    {
        var dados = Normalizar(clienteId, nome, email, cpf, cnh, validadeCnh, telefone);
        return new Condutor
        {
            ClienteId = dados.ClienteId,
            Nome = dados.Nome,
            Email = dados.Email,
            Cpf = dados.Cpf,
            Cnh = dados.Cnh,
            ValidadeCnh = dados.ValidadeCnh,
            Telefone = dados.Telefone,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void Atualizar(Guid clienteId, string? nome, string? email, string? cpf, string? cnh,
        DateOnly? validadeCnh, string? telefone, DateTimeOffset agora)
    {
        var dados = Normalizar(clienteId, nome, email, cpf, cnh, validadeCnh, telefone);
        ClienteId = dados.ClienteId;
        Nome = dados.Nome;
        Email = dados.Email;
        Cpf = dados.Cpf;
        Cnh = dados.Cnh;
        ValidadeCnh = dados.ValidadeCnh;
        Telefone = dados.Telefone;
        AtualizadoEm = agora;
    }

    public void SincronizarCnh(string cnh, DateTimeOffset agora)
    {
        Cnh = cnh.Trim();
        AtualizadoEm = agora;
    }

    private static Dados Normalizar(Guid clienteId, string? nome, string? email, string? cpf, string? cnh,
        DateOnly? validadeCnh, string? telefone)
    {
        var erros = new List<ErroValidacao>();
        if (clienteId == Guid.Empty) erros.Add(new ErroValidacao("clienteId", "Selecione um cliente."));
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        var emailNormalizado = (email ?? string.Empty).Trim();
        var cpfNormalizado = DocumentoBrasileiro.Normalizar(cpf);
        var cnhNormalizada = (cnh ?? string.Empty).Trim();
        var telefoneNormalizado = (telefone ?? string.Empty).Trim();
        Validador.Adicionar(erros, "nome", Validador.Obrigatorio(nomeNormalizado, 200));
        Validador.Adicionar(erros, "email", Validador.EmailObrigatorio(emailNormalizado));
        if (cpfNormalizado.Length != 11 || !DocumentoBrasileiro.CpfValido(cpfNormalizado))
            erros.Add(new ErroValidacao("cpf", "Informe um CPF válido."));
        Validador.Adicionar(erros, "cnh", Validador.Obrigatorio(cnhNormalizada, 32));
        Validador.Adicionar(erros, "validadeCnh", Validador.DataObrigatoria(validadeCnh));
        Validador.Adicionar(erros, "telefone", Validador.Obrigatorio(telefoneNormalizado, 32));
        Validador.Lancar(erros);
        return new Dados(clienteId, nomeNormalizado, emailNormalizado, cpfNormalizado, cnhNormalizada,
            validadeCnh!.Value, telefoneNormalizado);
    }

    private sealed record Dados(Guid ClienteId, string Nome, string Email, string Cpf, string Cnh,
        DateOnly ValidadeCnh, string Telefone);
}
