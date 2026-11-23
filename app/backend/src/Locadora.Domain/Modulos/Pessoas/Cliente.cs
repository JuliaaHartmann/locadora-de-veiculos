namespace Locadora.Domain;

public enum TipoCliente
{
    PF,
    PJ,
}

public sealed class Cliente : EntidadeEmpresa
{
    public TipoCliente Tipo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Rg { get; set; }
    public string? Cnh { get; set; }
    public string? Cnpj { get; set; }
    public string Telefone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Endereco Endereco { get; set; } = new();
    public ICollection<VinculoPfPj> VinculosComoPf { get; set; } = new List<VinculoPfPj>();
    public ICollection<VinculoPfPj> VinculosComoPj { get; set; } = new List<VinculoPfPj>();

    public static Cliente Criar(TipoCliente tipo, string? nome, string? cpf, string? rg, string? cnh,
        string? cnpj, string? telefone, string? email, string? estado, string? cidade, string? bairro,
        string? rua, string? numero, Guid empresaId, DateTimeOffset agora)
    {
        var dados = Normalizar(tipo, nome, cpf, rg, cnh, cnpj, telefone, email, estado, cidade, bairro, rua, numero);
        return new Cliente
        {
            Tipo = dados.Tipo,
            Nome = dados.Nome,
            Cpf = dados.Cpf,
            Rg = dados.Rg,
            Cnh = dados.Cnh,
            Cnpj = dados.Cnpj,
            Telefone = dados.Telefone,
            Email = dados.Email,
            Endereco = dados.Endereco,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void SincronizarCnh(string cnh, DateTimeOffset agora)
    {
        if (Tipo != TipoCliente.PF) return;
        Cnh = cnh.Trim();
        AtualizadoEm = agora;
    }

    public void Atualizar(TipoCliente tipo, string? nome, string? cpf, string? rg, string? cnh,
        string? cnpj, string? telefone, string? email, string? estado, string? cidade, string? bairro,
        string? rua, string? numero, DateTimeOffset agora)
    {
        var dados = Normalizar(tipo, nome, cpf, rg, cnh, cnpj, telefone, email, estado, cidade, bairro, rua, numero);
        Tipo = dados.Tipo;
        Nome = dados.Nome;
        Cpf = dados.Cpf;
        Rg = dados.Rg;
        Cnh = dados.Cnh;
        Cnpj = dados.Cnpj;
        Telefone = dados.Telefone;
        Email = dados.Email;
        Endereco = dados.Endereco;
        AtualizadoEm = agora;
    }

    private static Dados Normalizar(TipoCliente tipo, string? nome, string? cpf, string? rg, string? cnh,
        string? cnpj, string? telefone, string? email, string? estado, string? cidade, string? bairro,
        string? rua, string? numero)
    {
        var erros = new List<ErroValidacao>();
        if (!Enum.IsDefined(tipo)) erros.Add(new ErroValidacao("tipo", "Selecione PF ou PJ."));
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        var telefoneNormalizado = (telefone ?? string.Empty).Trim();
        var emailNormalizado = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Validador.Adicionar(erros, "nome", Validador.Obrigatorio(nomeNormalizado, 200));
        Validador.Adicionar(erros, "telefone", Validador.Obrigatorio(telefoneNormalizado, 32));
        Validador.Adicionar(erros, "email", Validador.EmailOpcional(emailNormalizado));
        var endereco = Endereco.Criar(estado, cidade, bairro, rua, numero, erros);

        var cpfNormalizado = DocumentoBrasileiro.Normalizar(cpf);
        var cnpjNormalizado = DocumentoBrasileiro.Normalizar(cnpj);
        var rgNormalizado = string.IsNullOrWhiteSpace(rg) ? null : rg.Trim();
        var cnhNormalizada = string.IsNullOrWhiteSpace(cnh) ? null : cnh.Trim();

        if (tipo == TipoCliente.PF)
        {
            if (cpfNormalizado.Length != 11 || !DocumentoBrasileiro.CpfValido(cpfNormalizado))
                erros.Add(new ErroValidacao("cpf", "Informe um CPF válido."));
            Validador.Adicionar(erros, "rg", Validador.Obrigatorio(rgNormalizado, 32));
            Validador.Adicionar(erros, "cnh", Validador.Obrigatorio(cnhNormalizada, 32));
            cnpjNormalizado = string.Empty;
        }
        else if (tipo == TipoCliente.PJ)
        {
            if (cnpjNormalizado.Length != 14 || !DocumentoBrasileiro.CnpjValido(cnpjNormalizado))
                erros.Add(new ErroValidacao("cnpj", "Informe um CNPJ válido."));
            cpfNormalizado = string.Empty;
            rgNormalizado = null;
            cnhNormalizada = null;
        }

        Validador.Lancar(erros);
        return new Dados(tipo, nomeNormalizado, cpfNormalizado.Length == 0 ? null : cpfNormalizado,
            rgNormalizado, cnhNormalizada, cnpjNormalizado.Length == 0 ? null : cnpjNormalizado,
            telefoneNormalizado, emailNormalizado, endereco);
    }

    private sealed record Dados(TipoCliente Tipo, string Nome, string? Cpf, string? Rg, string? Cnh,
        string? Cnpj, string Telefone, string? Email, Endereco Endereco);
}
