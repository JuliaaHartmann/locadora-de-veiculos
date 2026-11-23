using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed class TaxaService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : ITaxaService
{
    public async Task<Pagina<TaxaResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.TaxasServico.AsNoTracking().OrderBy(x => x.Nome).ThenBy(x => x.Id);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => new TaxaResumo(x.Id, x.Nome, Decimais.FormatacaoMonetaria(x.Preco), x.Modalidade))
            .ToListAsync(ct);
        return new Pagina<TaxaResumo>(itens, pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<TaxaResumo>?> ObterAsync(Guid id, CancellationToken ct, bool paraEdicao = false)
    {
        var taxa = await db.TaxasServico.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (paraEdicao && taxa is not null && await db.TaxaEmAluguelAbertoAsync(id, ct))
            throw new ConflitoException("Esta taxa está vinculada a um aluguel em aberto e não pode ser editada.");
        return taxa is null ? null : new(Mapear(taxa), Etag.De(taxa.Versao));
    }

    public async Task<RespostaComVersao<TaxaResumo>> CriarAsync(ComandoTaxa comando, CancellationToken ct)
    {
        var taxa = await escrita.ExecutarAsync(_ =>
        {
            var preco = Decimais.Monetario(comando.Preco, "preco");
            var nova = TaxaServico.Criar(comando.Nome, preco, comando.Modalidade, escrita.EmpresaId, relogio.Agora);
            db.TaxasServico.Add(nova);
            escrita.Auditar("taxa.criada", nameof(TaxaServico), nova.Id, "nome", "preco", "modalidade");
            return Task.FromResult(nova);
        }, ct);
        return new RespostaComVersao<TaxaResumo>(Mapear(taxa), Etag.De(taxa.Versao));
    }

    public async Task<RespostaComVersao<TaxaResumo>> EditarAsync(Guid id, ComandoTaxa comando,
        string? ifMatch, CancellationToken ct)
    {
        var taxa = await escrita.ExecutarAsync(async token =>
        {
            var atual = await db.TaxasServico.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, atual.Versao);
            if (await db.TaxaEmAluguelAbertoAsync(id, token))
                throw new ConflitoException("Esta taxa está vinculada a um aluguel em aberto e não pode ser editada.");
            var preco = Decimais.Monetario(comando.Preco, "preco");
            atual.Atualizar(comando.Nome, preco, comando.Modalidade, relogio.Agora);
            escrita.Auditar("taxa.editada", nameof(TaxaServico), atual.Id, "nome", "preco", "modalidade");
            return atual;
        }, ct);
        return new RespostaComVersao<TaxaResumo>(Mapear(taxa), Etag.De(taxa.Versao));
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var taxa = await db.TaxasServico.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, taxa.Versao);
            if (await db.TaxaEmAluguelAbertoAsync(id, token))
                throw new ConflitoException("Esta taxa está vinculada a um aluguel em aberto e não pode ser excluída.");
            taxa.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("taxa.excluida", nameof(TaxaServico), taxa.Id);
            return true;
        }, ct);
    }

    private static TaxaResumo Mapear(TaxaServico taxa) =>
        new(taxa.Id, taxa.Nome, Decimais.FormatacaoMonetaria(taxa.Preco), taxa.Modalidade);
}

public sealed class SeguroService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : ISeguroService
{
    public async Task<RespostaComVersao<SeguroEstado>> ObterAsync(TipoSeguro tipo, CancellationToken ct)
    {
        if (!Enum.IsDefined(tipo)) throw new ValidacaoException("tipo", "Selecione um seguro válido.");
        var config = await db.Seguros.AsNoTracking().SingleOrDefaultAsync(x => x.Tipo == tipo, ct);
        if (config is not null)
            return new RespostaComVersao<SeguroEstado>(Mapear(config), Etag.De(config.Versao));
        var versao = await db.Empresas.AsNoTracking().Where(x => x.Id == escrita.EmpresaId)
            .Select(x => x.Versao).SingleAsync(ct);
        return new RespostaComVersao<SeguroEstado>(
            new SeguroEstado(tipo, false, null), Etag.De(versao));
    }

    public async Task<RespostaComVersao<SeguroEstado>> ConfigurarAsync(TipoSeguro tipo, ComandoSeguro comando,
        string? ifMatch, CancellationToken ct)
    {
        try
        {
            var config = await escrita.ExecutarAsync(async token =>
            {
                var empresa = await db.Empresas.SingleAsync(x => x.Id == escrita.EmpresaId, token);
                var atual = await db.Seguros.SingleOrDefaultAsync(x => x.Tipo == tipo, token);
                Etag.Exigir(ifMatch, atual?.Versao ?? empresa.Versao);
                var preco = Decimais.Monetario(comando.PrecoDiario, "precoDiario");
                if (atual is null)
                {
                    atual = SeguroConfiguracao.Criar(tipo, preco, escrita.EmpresaId, relogio.Agora);
                    db.Seguros.Add(atual);
                }
                else
                {
                    atual.AlterarPreco(preco, relogio.Agora);
                }
                escrita.Auditar(atual.Versao.Length == 0 ? "seguro.configurado" : "seguro.alterado",
                    nameof(SeguroConfiguracao), atual.Id, "precoDiario");
                return atual;
            }, ct);
            return new RespostaComVersao<SeguroEstado>(Mapear(config), Etag.De(config.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("A configuração deste seguro já existe.", "tipo");
        }
    }

    private static SeguroEstado Mapear(SeguroConfiguracao config) =>
        new(config.Tipo, true, Decimais.FormatacaoMonetaria(config.PrecoDiario));
}

public sealed class CombustivelService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : ICombustivelService
{
    public async Task<RespostaComVersao<CombustivelEstado>> ObterAsync(Combustivel tipo, CancellationToken ct)
    {
        if (!Enum.IsDefined(tipo)) throw new ValidacaoException("tipo", "Selecione um combustível válido.");
        var atual = await AtualAsync(tipo, ct);
        if (atual is not null)
            return new RespostaComVersao<CombustivelEstado>(Mapear(atual), Etag.De(atual.Versao));
        var versao = await db.Empresas.AsNoTracking().Where(x => x.Id == escrita.EmpresaId)
            .Select(x => x.Versao).SingleAsync(ct);
        return new RespostaComVersao<CombustivelEstado>(
            new CombustivelEstado(tipo, false, null, null, null, null), Etag.De(versao));
    }

    public async Task<Pagina<CombustivelVersao>> HistoricoAsync(Combustivel tipo, int? page, int? pageSize,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(tipo)) throw new ValidacaoException("tipo", "Selecione um combustível válido.");
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.PrecosCombustivel.AsNoTracking().Where(x => x.Tipo == tipo);
        var total = await consulta.CountAsync(ct);
        var versoes = await consulta
            .OrderByDescending(x => x.InicioVigenciaUtc).ThenByDescending(x => x.Ordem)
            .ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new Pagina<CombustivelVersao>(versoes.Select(x => new CombustivelVersao(
            x.Id, x.Tipo, Decimais.FormatacaoMonetaria(x.Preco), x.InicioVigenciaUtc, x.Ordem)).ToList(),
            pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<CombustivelEstado>> ConfigurarAsync(Combustivel tipo,
        ComandoCombustivel comando, string? ifMatch, CancellationToken ct)
    {
        var versao = await escrita.ExecutarAsync(async token =>
        {
            var empresa = await db.Empresas.SingleAsync(x => x.Id == escrita.EmpresaId, token);
            var atual = await AtualAsync(tipo, token);
            Etag.Exigir(ifMatch, atual?.Versao ?? empresa.Versao);
            var preco = Decimais.Monetario(comando.Preco, "preco");
            var ordem = (await db.PrecosCombustivel.MaxAsync(x => (long?)x.Ordem, token) ?? 0) + 1;
            var nova = PrecoCombustivel.Criar(tipo, preco, relogio.Agora, ordem,
                escrita.EmpresaId, relogio.Agora);
            db.PrecosCombustivel.Add(nova);
            escrita.Auditar("combustivel.configurado", nameof(PrecoCombustivel), nova.Id,
                "tipo", "preco", "inicioVigenciaUtc", "ordem");
            return nova;
        }, ct);
        return new RespostaComVersao<CombustivelEstado>(Mapear(versao), Etag.De(versao.Versao));
    }

    private Task<PrecoCombustivel?> AtualAsync(Combustivel tipo, CancellationToken ct) =>
        db.PrecosCombustivel.AsNoTracking().Where(x => x.Tipo == tipo)
            .OrderByDescending(x => x.InicioVigenciaUtc).ThenByDescending(x => x.Ordem)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    private static CombustivelEstado Mapear(PrecoCombustivel preco) =>
        new(preco.Tipo, true, preco.Id, Decimais.FormatacaoMonetaria(preco.Preco),
            preco.InicioVigenciaUtc, preco.Ordem);
}
