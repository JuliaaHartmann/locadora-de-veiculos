namespace Locadora.Domain;

public static class ElegibilidadeLocacao
{
    public static bool CondutorVinculado(Cliente cliente, Condutor condutor) =>
        Ativo(cliente, cliente.EmpresaId) && Ativo(condutor, cliente.EmpresaId) && condutor.ClienteId == cliente.Id;

    public static bool PlanoCompativel(Veiculo veiculo, Plano plano) =>
        Ativo(veiculo, veiculo.EmpresaId) && Ativo(plano, veiculo.EmpresaId) && plano.GrupoId == veiculo.GrupoId;

    public static bool Ativo(EntidadeEmpresa cadastro, Guid empresaId) =>
        cadastro.EmpresaId == empresaId && cadastro.ExcluidoEmUtc is null;

    public static void Validar(Cliente cliente, Condutor condutor, int? hodometroInicial,
        DateTimeOffset saida, DateTimeOffset retorno, DateOnly diaLocalRetorno)
    {
        var erros = new List<ErroValidacao>();
        var documentoValido = cliente.Tipo switch
        {
            TipoCliente.PF => DocumentoBrasileiro.CpfValido(cliente.Cpf),
            TipoCliente.PJ => DocumentoBrasileiro.CnpjValido(cliente.Cnpj),
            _ => false,
        };
        if (!documentoValido)
            erros.Add(new("clienteId", "O documento do cliente é inválido. Revise o cadastro."));
        if (!DocumentoBrasileiro.CpfValido(condutor.Cpf))
            erros.Add(new("condutorId", "O CPF do condutor é inválido. Revise o cadastro."));
        if (string.IsNullOrWhiteSpace(condutor.Cnh))
            erros.Add(new("condutorId", "Informe a CNH no cadastro do condutor."));
        else if (condutor.ValidadeCnh < diaLocalRetorno)
            erros.Add(new("condutorId", "A CNH vence antes da data do retorno previsto."));

        if (retorno <= saida)
            erros.Add(new("retornoPrevisto", "Informe um retorno previsto posterior à saída."));
        if (hodometroInicial is null or < 0)
            erros.Add(new("hodometroInicial", "Informe o hodômetro inicial em quilômetros inteiros e não negativos."));
        Validador.Lancar(erros);
    }
}
