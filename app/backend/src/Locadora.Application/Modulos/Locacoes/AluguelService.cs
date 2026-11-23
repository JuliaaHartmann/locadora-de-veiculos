using System.Globalization;
using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService(IRepositorioAlugueis repositorio, IProtocoloEscrita escrita, IRelogio relogio)
{
    public async Task<PreviaAluguel> EstimarAsync(ComandoAluguel comando, CancellationToken ct)
    {
        var saida = relogio.Agora;
        var dados = await PrepararAsync(comando, saida, null, null, ct);
        return new(HorarioLocacao.ParaSaoPaulo(saida), dados.Retorno,
            Demonstrativo(comando.PlanoId, dados.Estimativa));
    }

    private async Task<DadosPreparados> PrepararAsync(ComandoAluguel comando, DateTimeOffset saida,
        Guid? aluguelAtual, ContratoSnapshot? anterior, CancellationToken ct)
    {
        var retorno = HorarioLocacao.InterpretarRetorno(comando.RetornoPrevisto);
        var taxas = comando.TaxaIds ?? [];
        var seguros = comando.Seguros ?? [];
        var erros = new List<ErroValidacao>();
        if (comando.ClienteId == Guid.Empty) erros.Add(new("clienteId", "Selecione o cliente contratante."));
        if (comando.CondutorId == Guid.Empty) erros.Add(new("condutorId", "Selecione um condutor vinculado ao cliente."));
        if (comando.VeiculoId == Guid.Empty) erros.Add(new("veiculoId", "Selecione um veículo disponível."));
        if (comando.PlanoId == Guid.Empty) erros.Add(new("planoId", "Selecione um plano do grupo do veículo."));
        if (taxas.Contains(Guid.Empty) || taxas.Distinct().Count() != taxas.Count)
            erros.Add(new("taxaIds", "Informe taxas válidas, sem repetir uma taxa."));
        if (seguros.Any(x => !Enum.IsDefined(x)) || seguros.Distinct().Count() != seguros.Count)
            erros.Add(new("seguros", "Informe seguros válidos, sem repetir um seguro."));
        Validador.Lancar(erros);

        var referencias = await repositorio.LerReferenciasAsync(comando, ct);
        if (!ElegibilidadeLocacao.CondutorVinculado(referencias.Cliente, referencias.Condutor))
            throw new ConflitoException("Selecione um condutor vinculado diretamente ao cliente contratante.", "condutorId");
        if (!ElegibilidadeLocacao.PlanoCompativel(referencias.Veiculo, referencias.Plano))
            throw new ConflitoException("Selecione um plano ativo do grupo do veículo.", "planoId");
        ElegibilidadeLocacao.Validar(referencias.Cliente, referencias.Condutor, comando.HodometroInicial,
            saida, retorno, DateOnly.FromDateTime(retorno.DateTime));
        if (await repositorio.VeiculoOcupadoAsync(comando.VeiculoId, aluguelAtual, ct))
            throw VeiculoIndisponivel();

        var plano = anterior?.PlanoId == comando.PlanoId
            ? new(anterior.ModalidadePlano, anterior.PrecoDiario, anterior.PrecoKm, anterior.FranquiaDiariaKm)
            : ParametrosPlano.De(referencias.Plano);
        var itens = new List<ParametrosAdicional>();
        foreach (var taxa in referencias.Taxas.OrderBy(x => x.Id))
        {
            var mantido = anterior?.Itens.SingleOrDefault(x => x.TaxaId == taxa.Id);
            itens.Add(mantido is null ? new(taxa.Id, taxa.Nome, CategoriaAdicional.Taxa, taxa.Modalidade, taxa.Preco)
                : Parametros(mantido));
        }
        if (referencias.Taxas.Count != taxas.Count)
            throw new NaoEncontradoException();
        foreach (var tipo in seguros.Order())
        {
            var categoria = tipo == TipoSeguro.Cliente ? CategoriaAdicional.SeguroCliente : CategoriaAdicional.SeguroTerceiros;
            var mantido = anterior?.Itens.SingleOrDefault(x => x.Categoria == categoria);
            if (mantido is not null) { itens.Add(Parametros(mantido)); continue; }
            var nome = tipo == TipoSeguro.Cliente ? "Seguro do cliente" : "Seguro de terceiros";
            var seguro = referencias.Seguros.SingleOrDefault(x => x.Tipo == tipo);
            if (seguro is null)
                throw new ValidacaoException($"seguros.{tipo.ToString().ToLowerInvariant()}",
                    $"Configure o preço de {nome.ToLowerInvariant()} em Preços operacionais ou retire a seleção.");
            itens.Add(new(seguro.Id, nome, categoria, ModalidadeTaxa.Diaria, seguro.PrecoDiario));
        }

        try { return new(retorno, referencias, CalculoLocacao.Estimar(saida, retorno, plano, itens)); }
        catch (OverflowException) { throw new ValidacaoException("retornoPrevisto", "O valor da estimativa excede o intervalo suportado."); }
    }

    private static ParametrosAdicional Parametros(ItemContratado item) =>
        new(item.TaxaId ?? item.SeguroId!.Value, item.Nome, item.Categoria, item.Modalidade, item.Preco);

    private static ConflitoException VeiculoIndisponivel() =>
        new("O veículo não está mais disponível. Selecione outro veículo para continuar.", "veiculoId");

    private static string Valor(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

    private static PlanoContratado PlanoDto(Guid id, ModalidadePlano modalidade, decimal precoDiario,
        decimal? precoKm, int? franquia) =>
        new(id, modalidade, Decimais.FormatacaoMonetaria(precoDiario),
            precoKm is decimal km ? Decimais.FormatacaoMonetaria(km) : null, franquia);

    private static DemonstrativoEstimado Demonstrativo(Guid planoId, EstimativaLocacao estimativa) =>
        new(estimativa.Diarias, PlanoDto(planoId, estimativa.Parametros.Modalidade, estimativa.Parametros.PrecoDiario,
                estimativa.Parametros.PrecoKm, estimativa.Parametros.FranquiaDiariaKm),
            Valor(estimativa.Plano.Valor), estimativa.Plano.FranquiaTotalKm, estimativa.Plano.QuilometragemPendente,
            estimativa.Adicionais.Select(x => new ComponenteContratado(x.Item.OrigemId, x.Item.Categoria, x.Item.Nome,
                x.Item.Modalidade, Decimais.FormatacaoMonetaria(x.Item.Preco), Valor(x.Valor))).ToArray(),
            Valor(estimativa.ParcelasConhecidas), Valor(estimativa.Caucao), estimativa.Incompleta);

    private sealed record DadosPreparados(DateTimeOffset Retorno, ReferenciasLocacao Referencias, EstimativaLocacao Estimativa);
}
