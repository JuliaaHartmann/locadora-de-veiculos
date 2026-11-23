using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService
{
    public async Task<RespostaComVersao<AluguelDetalhe>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var encontrado = await repositorio.LerAtualAsync(id, false, ct);
        return encontrado is { } registro
            ? new(Detalhe(registro.Aluguel, registro.Revisao), Etag.De(registro.Aluguel.Versao))
            : null;
    }

    public async Task<Pagina<AluguelResumo>> ListarAsync(int? page, int? pageSize, Guid? veiculoId,
        DateOnly? saidaDe, DateOnly? saidaAte, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        if (saidaDe > saidaAte)
            throw new ValidacaoException("saidaAte", "A data final não pode preceder a data inicial.");
        var inicio = saidaDe is { } de ? HorarioLocacao.InicioDia(de, "saidaDe") : (DateTimeOffset?)null;
        var fim = saidaAte is { } ate && ate < DateOnly.MaxValue
            ? HorarioLocacao.InicioDia(ate.AddDays(1), "saidaAte") : (DateTimeOffset?)null;
        var resultado = await repositorio.ListarAsync(new(veiculoId, inicio, fim), pagina, tamanho, ct);
        return new(resultado.Items.Select(x =>
        {
            var a = x.Aluguel;
            var r = x.Revisao;
            return new AluguelResumo(a.Id, a.Estado, r.CondutorNome, r.VeiculoId, r.VeiculoPlaca, r.VeiculoMarca,
                r.VeiculoModelo, r.ModalidadePlano, HorarioLocacao.ParaSaoPaulo(a.SaidaUtc),
                HorarioLocacao.ParaSaoPaulo(r.RetornoPrevistoUtc), Valor(r.ParcelasConhecidas));
        }).ToArray(), pagina, tamanho, resultado.Total);
    }
}
