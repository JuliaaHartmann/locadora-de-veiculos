using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed class GrupoService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : IGrupoService
{
    public async Task<Pagina<GrupoResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.Grupos.AsNoTracking().OrderBy(x => x.Nome).ThenBy(x => x.Id);
        var total = await consulta.CountAsync(ct);
        var grupos = await consulta.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        var ids = grupos.Select(x => x.Id).ToArray();
        var planos = await db.Planos.AsNoTracking()
            .Where(x => ids.Contains(x.GrupoId))
            .OrderBy(x => x.Modalidade).ThenBy(x => x.Id)
            .ToListAsync(ct);
        var itens = grupos.Select(g => new GrupoResumo(g.Id, g.Nome,
            planos.Where(p => p.GrupoId == g.Id).Select(p => Mapear(p, g.Nome)).ToList())).ToList();
        return new Pagina<GrupoResumo>(itens, pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<GrupoDetalhe>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var grupo = await db.Grupos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (grupo is null) return null;
        var planos = await db.Planos.AsNoTracking().Where(x => x.GrupoId == id)
            .OrderBy(x => x.Modalidade).ThenBy(x => x.Id).ToListAsync(ct);
        return new(Mapear(grupo, planos), Etag.De(grupo.Versao));
    }

    public async Task<RespostaComVersao<GrupoDetalhe>> CriarAsync(ComandoGrupo comando, CancellationToken ct)
    {
        var grupo = await escrita.ExecutarAsync(_ =>
        {
            var novo = Grupo.Criar(comando.Nome, escrita.EmpresaId, relogio.Agora);
            db.Grupos.Add(novo);
            escrita.Auditar("grupo.criado", nameof(Grupo), novo.Id, "nome");
            return Task.FromResult(novo);
        }, ct);
        return new RespostaComVersao<GrupoDetalhe>(Mapear(grupo, []), Etag.De(grupo.Versao));
    }

    public async Task<RespostaComVersao<GrupoDetalhe>> EditarAsync(Guid id, ComandoGrupo comando,
        string? ifMatch, CancellationToken ct)
    {
        var grupo = await escrita.ExecutarAsync(async token =>
        {
            var atual = await db.Grupos.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, atual.Versao);
            atual.Renomear(comando.Nome, relogio.Agora);
            escrita.Auditar("grupo.editado", nameof(Grupo), atual.Id, "nome");
            return atual;
        }, ct);
        var planos = await db.Planos.AsNoTracking().Where(x => x.GrupoId == id)
            .OrderBy(x => x.Modalidade).ThenBy(x => x.Id).ToListAsync(ct);
        return new RespostaComVersao<GrupoDetalhe>(Mapear(grupo, planos), Etag.De(grupo.Versao));
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var grupo = await db.Grupos.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, grupo.Versao);
            var empresa = escrita.EmpresaId;
            var possuiVinculos = await db.Planos.IgnoreQueryFilters()
                    .AnyAsync(x => x.EmpresaId == empresa && x.GrupoId == id, token)
                || await db.Veiculos.IgnoreQueryFilters()
                    .AnyAsync(x => x.EmpresaId == empresa && x.GrupoId == id, token);
            if (possuiVinculos)
                throw new ConflitoException("O grupo possui vínculos com planos ou veículos e não pode ser excluído.");
            grupo.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("grupo.excluido", nameof(Grupo), grupo.Id);
            return true;
        }, ct);
    }

    private static GrupoDetalhe Mapear(Grupo grupo, IReadOnlyList<Plano> planos) =>
        new(grupo.Id, grupo.Nome, planos.Select(p => Mapear(p, grupo.Nome)).ToList());

    private static PlanoResumo Mapear(Plano plano, string grupoNome) =>
        new(plano.Id, plano.GrupoId, grupoNome, plano.Modalidade,
            Decimais.FormatacaoMonetaria(plano.PrecoDiario),
            plano.PrecoKm is null ? null : Decimais.FormatacaoMonetaria(plano.PrecoKm.Value),
            plano.FranquiaDiariaKm);
}

public sealed class PlanoService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : IPlanoService
{
    public async Task<Pagina<PlanoResumo>> ListarAsync(Guid? grupoId, int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.Planos.AsNoTracking().Include(x => x.Grupo).AsQueryable();
        if (grupoId is Guid filtro) consulta = consulta.Where(x => x.GrupoId == filtro);
        var ordenada = consulta.OrderBy(x => x.Grupo!.Nome).ThenBy(x => x.Modalidade).ThenBy(x => x.Id);
        var total = await ordenada.CountAsync(ct);
        var planos = await ordenada.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new Pagina<PlanoResumo>(planos.Select(p => Mapear(p, p.Grupo!.Nome)).ToList(), pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<PlanoResumo>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var plano = await db.Planos.AsNoTracking().Include(x => x.Grupo)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return plano is null ? null : new(Mapear(plano, plano.Grupo!.Nome), Etag.De(plano.Versao));
    }

    public async Task<RespostaComVersao<PlanoResumo>> CriarAsync(ComandoPlano comando, CancellationToken ct)
    {
        try
        {
            var plano = await escrita.ExecutarAsync(async token =>
            {
                if (!await db.Grupos.AnyAsync(x => x.Id == comando.GrupoId, token))
                    throw new ValidacaoException("grupoId", "Selecione um grupo ativo.");
                var precoDiario = Decimais.Monetario(comando.PrecoDiario, "precoDiario");
                var precoKm = Decimais.Monetario(comando.PrecoKm, "precoKm");
                var novo = Plano.Criar(comando.GrupoId, comando.Modalidade, precoDiario, precoKm,
                    comando.FranquiaDiariaKm, escrita.EmpresaId, relogio.Agora);
                db.Planos.Add(novo);
                escrita.Auditar("plano.criado", nameof(Plano), novo.Id,
                    "grupoId", "modalidade", "precoDiario", "precoKm", "franquiaDiariaKm");
                return novo;
            }, ct);
            var grupoNome = await db.Grupos.AsNoTracking().Where(x => x.Id == plano.GrupoId)
                .Select(x => x.Nome).SingleAsync(ct);
            return new RespostaComVersao<PlanoResumo>(Mapear(plano, grupoNome), Etag.De(plano.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("Já existe plano desta modalidade no grupo.", "modalidade");
        }
    }

    public async Task<RespostaComVersao<PlanoResumo>> EditarAsync(Guid id, ComandoEdicaoPlano comando,
        string? ifMatch, CancellationToken ct)
    {
        var plano = await escrita.ExecutarAsync(async token =>
        {
            var atual = await db.Planos.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, atual.Versao);
            var precoDiario = Decimais.Monetario(comando.PrecoDiario, "precoDiario");
            var precoKm = Decimais.Monetario(comando.PrecoKm, "precoKm");
            atual.AlterarParametros(precoDiario, precoKm, comando.FranquiaDiariaKm, relogio.Agora);
            escrita.Auditar("plano.editado", nameof(Plano), atual.Id,
                "precoDiario", "precoKm", "franquiaDiariaKm");
            return atual;
        }, ct);
        var grupoNome = await db.Grupos.AsNoTracking().Where(x => x.Id == plano.GrupoId)
            .Select(x => x.Nome).SingleAsync(ct);
        return new RespostaComVersao<PlanoResumo>(Mapear(plano, grupoNome), Etag.De(plano.Versao));
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var plano = await db.Planos.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, plano.Versao);
            plano.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("plano.excluido", nameof(Plano), plano.Id);
            return true;
        }, ct);
    }

    private static PlanoResumo Mapear(Plano plano, string grupoNome) =>
        new(plano.Id, plano.GrupoId, grupoNome, plano.Modalidade,
            Decimais.FormatacaoMonetaria(plano.PrecoDiario),
            plano.PrecoKm is null ? null : Decimais.FormatacaoMonetaria(plano.PrecoKm.Value),
            plano.FranquiaDiariaKm);
}

public sealed class VeiculoService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : IVeiculoService
{
    public async Task<Pagina<VeiculoResumo>> ListarAsync(Guid? grupoId, int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.Veiculos.AsNoTracking().Include(x => x.Grupo).AsQueryable();
        if (grupoId is Guid filtro) consulta = consulta.Where(x => x.GrupoId == filtro);
        var ordenada = consulta.OrderBy(x => x.Placa).ThenBy(x => x.Id);
        var total = await ordenada.CountAsync(ct);
        var veiculos = await ordenada.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return new Pagina<VeiculoResumo>(veiculos.Select(Mapear).ToList(), pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<VeiculoResumo>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var veiculo = await db.Veiculos.AsNoTracking().Include(x => x.Grupo)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return veiculo is null ? null : new(Mapear(veiculo), Etag.De(veiculo.Versao));
    }

    public async Task<RespostaComVersao<VeiculoResumo>> CriarAsync(ComandoVeiculo comando, CancellationToken ct)
    {
        try
        {
            var veiculo = await escrita.ExecutarAsync(async token =>
            {
                if (!await db.Grupos.AnyAsync(x => x.Id == comando.GrupoId, token))
                    throw new ValidacaoException("grupoId", "Selecione um grupo ativo.");
                var capacidade = Decimais.Capacidade(comando.CapacidadeLitros, "capacidadeLitros");
                var novo = Veiculo.Criar(comando.GrupoId, comando.Placa, comando.Marca, comando.Modelo,
                    comando.Cor, comando.Ano, comando.Combustivel, capacidade, escrita.EmpresaId, relogio.Agora);
                db.Veiculos.Add(novo);
                escrita.Auditar("veiculo.criado", nameof(Veiculo), novo.Id,
                    "grupoId", "placa", "marca", "modelo", "cor", "ano", "combustivel", "capacidadeLitros");
                return novo;
            }, ct);
            var grupoNome = await db.Grupos.AsNoTracking().Where(x => x.Id == veiculo.GrupoId)
                .Select(x => x.Nome).SingleAsync(ct);
            return new RespostaComVersao<VeiculoResumo>(Mapear(veiculo, grupoNome), Etag.De(veiculo.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("Placa já usada por veículo ativo.", "placa");
        }
    }

    public async Task<RespostaComVersao<VeiculoResumo>> EditarAsync(Guid id, ComandoVeiculo comando,
        string? ifMatch, CancellationToken ct)
    {
        try
        {
            var veiculo = await escrita.ExecutarAsync(async token =>
            {
                var atual = await db.Veiculos.SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new NaoEncontradoException();
                Etag.Exigir(ifMatch, atual.Versao);
                if (!await db.Grupos.AnyAsync(x => x.Id == comando.GrupoId, token))
                    throw new ValidacaoException("grupoId", "Selecione um grupo ativo.");
                var capacidade = Decimais.Capacidade(comando.CapacidadeLitros, "capacidadeLitros");
                atual.Atualizar(comando.GrupoId, comando.Placa, comando.Marca, comando.Modelo,
                    comando.Cor, comando.Ano, comando.Combustivel, capacidade, relogio.Agora);
                escrita.Auditar("veiculo.editado", nameof(Veiculo), atual.Id,
                    "grupoId", "placa", "marca", "modelo", "cor", "ano", "combustivel", "capacidadeLitros");
                return atual;
            }, ct);
            var grupoNome = await db.Grupos.AsNoTracking().Where(x => x.Id == veiculo.GrupoId)
                .Select(x => x.Nome).SingleAsync(ct);
            return new RespostaComVersao<VeiculoResumo>(Mapear(veiculo, grupoNome), Etag.De(veiculo.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("Placa já usada por veículo ativo.", "placa");
        }
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var veiculo = await db.Veiculos.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, veiculo.Versao);
            veiculo.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("veiculo.excluido", nameof(Veiculo), veiculo.Id);
            return true;
        }, ct);
    }

    private static VeiculoResumo Mapear(Veiculo veiculo) =>
        Mapear(veiculo, veiculo.Grupo?.Nome ?? string.Empty);

    private static VeiculoResumo Mapear(Veiculo veiculo, string grupoNome) =>
        new(veiculo.Id, veiculo.GrupoId, grupoNome, veiculo.Placa, veiculo.Marca, veiculo.Modelo,
            veiculo.Cor, veiculo.Ano, veiculo.Combustivel,
            Decimais.FormatacaoCapacidade(veiculo.CapacidadeLitros));
}
