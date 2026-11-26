using Locadora.Application;
using Locadora.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed partial class RepositorioAlugueis(LocadoraDbContext db, IProtocoloEscrita escrita) : IRepositorioAlugueis
{
    public async Task<ReferenciasLocacao> LerReferenciasAsync(ComandoAluguel comando, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == comando.ClienteId, ct)
            ?? throw new NaoEncontradoException();
        var condutor = await db.Condutores.AsNoTracking().SingleOrDefaultAsync(x => x.Id == comando.CondutorId, ct)
            ?? throw new NaoEncontradoException();
        var veiculo = await db.Veiculos.AsNoTracking().Include(x => x.Grupo)
            .SingleOrDefaultAsync(x => x.Id == comando.VeiculoId, ct) ?? throw new NaoEncontradoException();
        var plano = await db.Planos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == comando.PlanoId, ct)
            ?? throw new NaoEncontradoException();
        var taxas = comando.TaxaIds ?? [];
        var seguros = comando.Seguros ?? [];
        var taxasAtivas = taxas.Count == 0 ? [] : await db.TaxasServico.AsNoTracking().Where(x => taxas.Contains(x.Id)).ToListAsync(ct);
        var segurosAtivos = seguros.Count == 0 ? [] : await db.Seguros.AsNoTracking().Where(x => seguros.Contains(x.Tipo)).ToListAsync(ct);
        return new(cliente, condutor, veiculo, plano, taxasAtivas, segurosAtivos);
    }

    public Task<bool> VeiculoOcupadoAsync(Guid veiculoId, Guid? aluguelAtual, CancellationToken ct) =>
        db.Alugueis.AnyAsync(x => x.VeiculoId == veiculoId && x.Estado == EstadoAluguel.EmAberto &&
            (!aluguelAtual.HasValue || x.Id != aluguelAtual.Value), ct);

    public async Task<IReadOnlyList<TaxaServico>> LerTaxasAtivasAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.TaxasServico.AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);

    public Task<PrecoCombustivel?> LerPrecoNoRetornoAsync(Combustivel tipo, DateTimeOffset retornoUtc, CancellationToken ct) =>
        db.PrecosCombustivel.AsNoTracking().Where(x => x.Tipo == tipo && x.InicioVigenciaUtc <= retornoUtc)
            .OrderByDescending(x => x.InicioVigenciaUtc).ThenByDescending(x => x.Ordem).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<T> ExecutarEscritaAsync<T>(Func<CancellationToken, Task<T>> operacao, bool idempotente,
        CancellationToken ct)
    {
        for (int tentativa = 0; ; tentativa++)
        {
            try { return await escrita.ExecutarAsync(operacao, ct); }
            catch (Exception erro) when (tentativa < 2 && !ct.IsCancellationRequested && PodeRepetir(erro, idempotente))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(30 * (tentativa + 1)), ct);
            }
            catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro) &&
                erro.InnerException?.Message.Contains("IX_Alugueis_EmpresaId_VeiculoId", StringComparison.Ordinal) == true)
            {
                throw new ConflitoException("O veículo não está mais disponível. Selecione outro veículo para continuar.", "veiculoId");
            }
        }
    }

    private static bool PodeRepetir(Exception erro, bool idempotente)
    {
        var sql = erro as SqlException ?? erro.InnerException as SqlException;
        return sql is not null && (sql.Number == 1205 ||
            (idempotente && sql.Number is -2 or 64 or 233 or 10053 or 10054 or 10060 or 40197 or 40501 or 40613));
    }

    public Task<RegistroIdempotenciaAluguel?> LerChaveAsync(Guid usuarioId, string operacao, string chave, CancellationToken ct) =>
        db.IdempotenciaAlugueis.AsNoTracking().SingleOrDefaultAsync(x => x.UsuarioId == usuarioId &&
            x.Operacao == operacao && x.Chave == chave, ct);

    public async Task<(Aluguel Aluguel, ContratoSnapshot Revisao)> LerResultadoOriginalAsync(Guid revisaoId, CancellationToken ct)
    {
        var revisao = await db.ContratosSnapshot.AsNoTracking().Include(x => x.Itens).SingleAsync(x => x.Id == revisaoId, ct);
        // Replay autorizado pela chave já consultada, mesmo se o aluguel tiver sido ocultado posteriormente.
        var aluguel = await AlugueisVisiveis(incluirOcultos: true).AsNoTracking().SingleAsync(x => x.Id == revisao.AluguelId, ct);
        return (aluguel, revisao);
    }

    public async Task<(FechamentoAluguel Final, ContratoSnapshot Revisao, int NumeroExibicao)> LerFinalOriginalAsync(Guid finalId, CancellationToken ct)
    {
        var agregado = await (from final in db.FechamentosAlugueis.Include(x => x.Adicionais)
                              join revisao in db.ContratosSnapshot
                                  on new { final.EmpresaId, final.AluguelId, Id = final.RevisaoId }
                                  equals new { revisao.EmpresaId, revisao.AluguelId, revisao.Id }
                              join aluguel in AlugueisVisiveis(incluirOcultos: true)
                                  on new { final.EmpresaId, final.AluguelId }
                                  equals new { aluguel.EmpresaId, AluguelId = aluguel.Id }
                              where final.Id == finalId
                              select new { final, revisao, aluguel.NumeroExibicao }).AsNoTracking().AsSingleQuery().SingleAsync(ct);
        return (agregado.final, agregado.revisao, agregado.NumeroExibicao);
    }

    public Task<FechamentoAluguel> LerFinalDaRevisaoAsync(Guid aluguelId, Guid revisaoId, CancellationToken ct) =>
        db.FechamentosAlugueis.AsNoTracking().Include(x => x.Adicionais).AsSingleQuery()
            .SingleAsync(x => x.AluguelId == aluguelId && x.RevisaoId == revisaoId, ct);

    private IQueryable<Aluguel> AlugueisVisiveis(bool incluirOcultos)
    {
        if (!incluirOcultos) return db.Alugueis;
        var empresaId = escrita.EmpresaId;
        // Nunca desabilitar o filtro de empresa ou os filtros dos outros cadastros na consulta composta.
        return db.Alugueis.IgnoreQueryFilters([LocadoraDbContext.FiltroAluguelOculto]).Where(x => x.EmpresaId == empresaId);
    }

    public async Task<(Aluguel Aluguel, ContratoSnapshot Revisao)?> LerAtualAsync(Guid id, bool paraEdicao, CancellationToken ct,
        bool incluirOcultos = false)
    {
        var consulta = from aluguel in AlugueisVisiveis(incluirOcultos)
                       join revisao in db.ContratosSnapshot.Include(x => x.Itens)
                           on new { aluguel.EmpresaId, AluguelId = aluguel.Id, Numero = aluguel.RevisaoAtual }
                           equals new { revisao.EmpresaId, revisao.AluguelId, revisao.Numero }
                       select new { aluguel, revisao };
        // Uma única leitura para nunca parear o corpo com a versão de outra revisão.
        var agregado = await (paraEdicao ? consulta : consulta.AsNoTracking()).AsSingleQuery()
            .SingleOrDefaultAsync(x => x.aluguel.Id == id, ct);
        return agregado is null ? null : (agregado.aluguel, agregado.revisao);
    }

    public async Task<Pagina<LinhaAluguel>> ListarAsync(FiltroAluguel filtro, int pagina, int tamanho, CancellationToken ct)
    {
        var alugueis = AlugueisVisiveis(filtro.IncluirOcultos).AsNoTracking();
        if (filtro.Estado is EstadoAluguel estado) alugueis = alugueis.Where(x => x.Estado == estado);
        if (filtro.VeiculoId is Guid veiculoId) alugueis = alugueis.Where(x => x.VeiculoId == veiculoId);
        if (filtro.InicioUtc is { } inicio) alugueis = alugueis.Where(x => x.SaidaUtc >= inicio);
        if (filtro.FimExclusivoUtc is { } fim) alugueis = alugueis.Where(x => x.SaidaUtc < fim);
        var consulta = from aluguel in alugueis
                       join revisao in db.ContratosSnapshot.AsNoTracking()
                           on new { aluguel.EmpresaId, AluguelId = aluguel.Id, Numero = aluguel.RevisaoAtual }
                           equals new { revisao.EmpresaId, revisao.AluguelId, revisao.Numero }
                        select new { aluguel, revisao };
        var total = await consulta.CountAsync(ct);
        var itens = await (from linha in consulta
                           join final in db.FechamentosAlugueis.AsNoTracking()
                               on new { linha.aluguel.EmpresaId, AluguelId = linha.aluguel.Id, RevisaoId = linha.revisao.Id }
                               equals new { final.EmpresaId, final.AluguelId, final.RevisaoId } into finais
                           from final in finais.DefaultIfEmpty()
                           orderby linha.aluguel.SaidaUtc descending, linha.aluguel.Id
                           select new LinhaAluguel(linha.aluguel, linha.revisao, (decimal?)final.Total))
            .Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new(itens, pagina, tamanho, total);
    }

    public void Adicionar(Aluguel aluguel) => db.Alugueis.Add(aluguel);
    public void Adicionar(ContratoSnapshot revisao) => db.ContratosSnapshot.Add(revisao);
    public void Adicionar(FechamentoAluguel final) => db.FechamentosAlugueis.Add(final);

    public async Task RegistrarResultadoAsync(Aluguel aluguel, RegistroIdempotenciaAluguel registro, CancellationToken ct)
    {
        try
        {
            // A primeira gravação obtém a versão definitiva; todas as gravações permanecem sob o mesmo lock e commit.
            await db.SaveChangesAsync(ct);
            registro.VersaoOriginal = aluguel.Versao.ToArray();
            db.IdempotenciaAlugueis.Add(registro);
            if (registro.FinalId is not null) await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (registro.FinalId is not null && erro is not DbUpdateConcurrencyException &&
            !PodeRepetir(erro, true) && !SqlErrors.Duplicidade(erro))
        {
            // Falha conhecida de gravação antes de tentar commit. Falhas do próprio commit continuam ambíguas.
            throw new FalhaGravacaoDevolucaoException(erro);
        }
    }
}
