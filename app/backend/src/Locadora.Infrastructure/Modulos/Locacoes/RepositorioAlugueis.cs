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

    public async Task<T> ExecutarEscritaAsync<T>(Func<CancellationToken, Task<T>> operacao, bool criacaoIdempotente,
        CancellationToken ct)
    {
        for (int tentativa = 0; ; tentativa++)
        {
            try { return await escrita.ExecutarAsync(operacao, ct); }
            catch (Exception erro) when (tentativa < 2 && !ct.IsCancellationRequested && PodeRepetir(erro, criacaoIdempotente))
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

    private static bool PodeRepetir(Exception erro, bool criacaoIdempotente)
    {
        var sql = erro as SqlException ?? erro.InnerException as SqlException;
        return sql is not null && (sql.Number == 1205 ||
            (criacaoIdempotente && sql.Number is -2 or 64 or 233 or 10053 or 10054 or 10060 or 40197 or 40501 or 40613));
    }

    public Task<RegistroIdempotenciaAluguel?> LerChaveAsync(Guid usuarioId, string operacao, string chave, CancellationToken ct) =>
        db.IdempotenciaAlugueis.AsNoTracking().SingleOrDefaultAsync(x => x.UsuarioId == usuarioId &&
            x.Operacao == operacao && x.Chave == chave, ct);

    public async Task<(Aluguel Aluguel, ContratoSnapshot Revisao)> LerResultadoOriginalAsync(Guid revisaoId, CancellationToken ct)
    {
        var revisao = await db.ContratosSnapshot.AsNoTracking().Include(x => x.Itens).SingleAsync(x => x.Id == revisaoId, ct);
        var aluguel = await db.Alugueis.AsNoTracking().SingleAsync(x => x.Id == revisao.AluguelId, ct);
        return (aluguel, revisao);
    }

    public async Task<(Aluguel Aluguel, ContratoSnapshot Revisao)?> LerAtualAsync(Guid id, bool paraEdicao, CancellationToken ct)
    {
        var consulta = from aluguel in db.Alugueis
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
        var alugueis = db.AlugueisAbertos().AsNoTracking();
        if (filtro.VeiculoId is Guid veiculoId) alugueis = alugueis.Where(x => x.VeiculoId == veiculoId);
        if (filtro.InicioUtc is { } inicio) alugueis = alugueis.Where(x => x.SaidaUtc >= inicio);
        if (filtro.FimExclusivoUtc is { } fim) alugueis = alugueis.Where(x => x.SaidaUtc < fim);
        var consulta = from aluguel in alugueis
                       join revisao in db.ContratosSnapshot.AsNoTracking()
                           on new { aluguel.EmpresaId, AluguelId = aluguel.Id, Numero = aluguel.RevisaoAtual }
                           equals new { revisao.EmpresaId, revisao.AluguelId, revisao.Numero }
                       orderby aluguel.SaidaUtc descending, aluguel.Id
                       select new LinhaAluguel(aluguel, revisao);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new(itens, pagina, tamanho, total);
    }

    public void Adicionar(Aluguel aluguel) => db.Alugueis.Add(aluguel);
    public void Adicionar(ContratoSnapshot revisao) => db.ContratosSnapshot.Add(revisao);

    public async Task RegistrarResultadoAsync(Aluguel aluguel, RegistroIdempotenciaAluguel registro, CancellationToken ct)
    {
        // Continua sob o lock da empresa; o segundo SaveChanges do protocolo persiste a referência e confirma tudo junto.
        await db.SaveChangesAsync(ct);
        registro.VersaoOriginal = aluguel.Versao.ToArray();
        db.IdempotenciaAlugueis.Add(registro);
    }
}
