using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService
{
    public async Task<RespostaComVersao<AluguelDetalhe>?> ObterAsync(Guid id, CancellationToken ct, bool incluirOcultos = false)
    {
        var encontrado = await repositorio.LerAtualAsync(id, false, ct, incluirOcultos);
        if (encontrado is not { } registro) return null;
        var (aluguel, revisao) = registro;
        var detalhe = aluguel.Estado == EstadoAluguel.Concluido
            ? Detalhe(aluguel, revisao, await repositorio.LerFinalDaRevisaoAsync(aluguel.Id, revisao.Id, ct))
            : Detalhe(aluguel, revisao);
        return new(detalhe, Etag.De(aluguel.Versao));
    }

    public async Task<Pagina<AluguelResumo>> ListarAsync(int? page, int? pageSize, Guid? veiculoId,
        DateOnly? saidaDe, DateOnly? saidaAte, CancellationToken ct, EstadoAluguel? estado = null, bool incluirOcultos = false)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        if (saidaDe > saidaAte)
            throw new ValidacaoException("saidaAte", "A data final não pode preceder a data inicial.");
        var inicio = saidaDe is { } de ? HorarioLocacao.InicioDia(de, "saidaDe") : (DateTimeOffset?)null;
        var fim = saidaAte is { } ate && ate < DateOnly.MaxValue
            ? HorarioLocacao.InicioDia(ate.AddDays(1), "saidaAte") : (DateTimeOffset?)null;
        var resultado = await repositorio.ListarAsync(new(veiculoId, inicio, fim, estado, incluirOcultos), pagina, tamanho, ct);
        return new(resultado.Items.Select(x =>
        {
            var a = x.Aluguel;
            var r = x.Revisao;
            return new AluguelResumo(a.Id, a.Estado, r.CondutorNome, r.VeiculoId, r.VeiculoPlaca, r.VeiculoMarca,
                r.VeiculoModelo, r.ModalidadePlano, HorarioLocacao.ParaSaoPaulo(a.SaidaUtc),
                HorarioLocacao.ParaSaoPaulo(r.RetornoPrevistoUtc),
                a.Estado == EstadoAluguel.EmAberto ? Valor(r.ParcelasConhecidas) : null,
                a.Estado == EstadoAluguel.Concluido ? Valor(x.TotalFinal!.Value) : null, a.ExcluidoEmUtc is not null);
        }).ToArray(), pagina, tamanho, resultado.Total);
    }
}
