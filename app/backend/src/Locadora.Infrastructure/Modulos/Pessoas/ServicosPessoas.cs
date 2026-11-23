using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed class ClienteService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : IClienteService
{
    public async Task<Pagina<ClienteResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.Clientes.AsNoTracking().OrderBy(x => x.Nome).ThenBy(x => x.Id);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => new ClienteResumo(x.Id, x.Tipo, x.Nome, x.Telefone, x.Email,
                x.Cpf, x.Rg, x.Cnh, x.Cnpj)).ToListAsync(ct);
        return new Pagina<ClienteResumo>(itens, pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<ClienteDetalhe>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cliente is null) return null;
        return new(Mapear(cliente, await VinculosAsync(id, ct)), Etag.De(cliente.Versao));
    }

    public async Task<RespostaComVersao<ClienteDetalhe>> CriarAsync(ComandoCliente comando, CancellationToken ct)
    {
        try
        {
            var cliente = await escrita.ExecutarAsync(async token =>
            {
                var novo = Cliente.Criar(comando.Tipo, comando.Nome, comando.Cpf, comando.Rg, comando.Cnh,
                    comando.Cnpj, comando.Telefone, comando.Email, comando.Estado, comando.Cidade,
                    comando.Bairro, comando.Rua, comando.Numero, escrita.EmpresaId, relogio.Agora);
                if (novo.Tipo == TipoCliente.PF && novo.Cpf is not null)
                {
                    var incompativel = await db.Condutores
                        .AnyAsync(x => x.Cpf == novo.Cpf && x.Cnh != novo.Cnh, token);
                    if (incompativel)
                        throw new ConflitoException("A CNH deve coincidir com a da mesma pessoa já cadastrada.", "cnh");
                }
                db.Clientes.Add(novo);
                escrita.Auditar("cliente.criado", nameof(Cliente), novo.Id,
                    "tipo", "nome", "documentos", "telefone", "email", "endereco");
                return novo;
            }, ct);
            return new RespostaComVersao<ClienteDetalhe>(Mapear(cliente, []), Etag.De(cliente.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException(comando.Tipo == TipoCliente.PF
                ? "CPF já usado por cliente ativo." : "CNPJ já usado por cliente ativo.",
                comando.Tipo == TipoCliente.PF ? "cpf" : "cnpj");
        }
    }

    public async Task<RespostaComVersao<ClienteDetalhe>> EditarAsync(Guid id, ComandoCliente comando,
        string? ifMatch, CancellationToken ct)
    {
        try
        {
            var cliente = await escrita.ExecutarAsync(async token =>
            {
                var atual = await db.Clientes.SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new NaoEncontradoException();
                Etag.Exigir(ifMatch, atual.Versao);
                atual.Atualizar(comando.Tipo, comando.Nome, comando.Cpf, comando.Rg, comando.Cnh,
                    comando.Cnpj, comando.Telefone, comando.Email, comando.Estado, comando.Cidade,
                    comando.Bairro, comando.Rua, comando.Numero, relogio.Agora);
                if (atual.Tipo == TipoCliente.PF && atual.Cpf is not null && atual.Cnh is not null)
                {
                    var condutores = await db.Condutores.Where(x => x.Cpf == atual.Cpf).ToListAsync(token);
                    foreach (var condutor in condutores)
                    {
                        condutor.SincronizarCnh(atual.Cnh, relogio.Agora);
                        db.Entry(condutor).Property(x => x.Cnh).IsModified = true;
                        escrita.Auditar("condutor.cnh-sincronizada", nameof(Condutor), condutor.Id, "cnh");
                    }
                }
                escrita.Auditar("cliente.editado", nameof(Cliente), atual.Id,
                    "tipo", "nome", "documentos", "telefone", "email", "endereco", "cnh");
                return atual;
            }, ct);
            return new RespostaComVersao<ClienteDetalhe>(
                Mapear(cliente, await VinculosAsync(id, ct)), Etag.De(cliente.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException(comando.Tipo == TipoCliente.PF
                ? "CPF já usado por cliente ativo." : "CNPJ já usado por cliente ativo.",
                comando.Tipo == TipoCliente.PF ? "cpf" : "cnpj");
        }
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var cliente = await db.Clientes.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, cliente.Versao);
            cliente.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("cliente.excluido", nameof(Cliente), cliente.Id);
            return true;
        }, ct);
    }

    public async Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> ListarVinculosAsync(
        Guid clientePfId, CancellationToken ct)
    {
        var pf = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientePfId, ct)
            ?? throw new NaoEncontradoException();
        if (pf.Tipo != TipoCliente.PF)
            throw new ValidacaoException("id", "Vínculos PJ pertencem a um cliente PF.");
        return new RespostaComVersao<IReadOnlyList<ClienteVinculado>>(
            await VinculosAsync(clientePfId, ct), Etag.De(pf.Versao));
    }

    public async Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> AdicionarVinculoAsync(
        Guid clientePfId, ComandoVinculo comando, string? ifMatch, CancellationToken ct)
    {
        var pf = await escrita.ExecutarAsync(async token =>
        {
            var raiz = await db.Clientes.SingleOrDefaultAsync(x => x.Id == clientePfId, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, raiz.Versao);
            if (raiz.Tipo != TipoCliente.PF)
                throw new ValidacaoException("id", "Vínculos PJ pertencem a um cliente PF.");
            if (comando.ClientePjId == Guid.Empty)
                throw new ValidacaoException("clientePjId", "Selecione um cliente PJ.");
            var pj = await db.Clientes.SingleOrDefaultAsync(x => x.Id == comando.ClientePjId, token)
                ?? throw new ValidacaoException("clientePjId", "Selecione um cliente PJ ativo.");
            if (pj.Tipo != TipoCliente.PJ)
                throw new ValidacaoException("clientePjId", "O destino do vínculo deve ser um cliente PJ.");
            if (await db.Vinculos.AnyAsync(x => x.ClientePfId == clientePfId &&
                    x.ClientePjId == comando.ClientePjId, token))
                throw new ConflitoException("Vínculo já existente.", "clientePjId");
            db.Vinculos.Add(new VinculoPfPj
            {
                EmpresaId = escrita.EmpresaId,
                ClientePfId = clientePfId,
                ClientePjId = comando.ClientePjId,
                CriadoEm = relogio.Agora,
            });
            raiz.AtualizadoEm = relogio.Agora;
            db.Entry(raiz).Property(x => x.AtualizadoEm).IsModified = true;
            escrita.Auditar("vinculo.criado", nameof(VinculoPfPj), clientePfId, "clientePjId");
            return raiz;
        }, ct);
        return new RespostaComVersao<IReadOnlyList<ClienteVinculado>>(
            await VinculosAsync(clientePfId, ct), Etag.De(pf.Versao));
    }

    public async Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> RemoverVinculoAsync(
        Guid clientePfId, Guid clientePjId, string? ifMatch, CancellationToken ct)
    {
        var pf = await escrita.ExecutarAsync(async token =>
        {
            var raiz = await db.Clientes.SingleOrDefaultAsync(x => x.Id == clientePfId, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, raiz.Versao);
            if (raiz.Tipo != TipoCliente.PF)
                throw new ValidacaoException("id", "Vínculos PJ pertencem a um cliente PF.");
            var vinculo = await db.Vinculos.SingleOrDefaultAsync(x =>
                x.ClientePfId == clientePfId && x.ClientePjId == clientePjId, token)
                ?? throw new NaoEncontradoException();
            db.Vinculos.Remove(vinculo);
            raiz.AtualizadoEm = relogio.Agora;
            db.Entry(raiz).Property(x => x.AtualizadoEm).IsModified = true;
            escrita.Auditar("vinculo.removido", nameof(VinculoPfPj), clientePfId, "clientePjId");
            return raiz;
        }, ct);
        return new RespostaComVersao<IReadOnlyList<ClienteVinculado>>(
            await VinculosAsync(clientePfId, ct), Etag.De(pf.Versao));
    }

    private async Task<IReadOnlyList<ClienteVinculado>> VinculosAsync(Guid clientePfId, CancellationToken ct)
    {
        var ids = await db.Vinculos.AsNoTracking().Where(x => x.ClientePfId == clientePfId)
            .Select(x => x.ClientePjId).ToListAsync(ct);
        return await db.Clientes.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.Tipo == TipoCliente.PJ)
            .OrderBy(x => x.Nome).ThenBy(x => x.Id)
            .Select(x => new ClienteVinculado(x.Id, x.Nome, x.Cnpj)).ToListAsync(ct);
    }

    private static ClienteDetalhe Mapear(Cliente cliente, IReadOnlyList<ClienteVinculado> vinculos) =>
        new(cliente.Id, cliente.Tipo, cliente.Nome, cliente.Telefone, cliente.Email,
            cliente.Cpf, cliente.Rg, cliente.Cnh, cliente.Cnpj,
            new EnderecoContrato(cliente.Endereco.Estado, cliente.Endereco.Cidade,
                cliente.Endereco.Bairro, cliente.Endereco.Rua, cliente.Endereco.Numero),
            vinculos);
}

public sealed class CondutorService(
    LocadoraDbContext db,
    IProtocoloEscrita escrita,
    IRelogio relogio) : ICondutorService
{
    public async Task<Pagina<CondutorResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var (pagina, tamanho) = Paginacao.Normalizar(page, pageSize);
        var consulta = db.Condutores.AsNoTracking().OrderBy(x => x.Nome).ThenBy(x => x.Id);
        var total = await consulta.CountAsync(ct);
        var condutores = await consulta.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        var nomes = await NomesClientesAsync(condutores.Select(x => x.ClienteId), ct);
        return new Pagina<CondutorResumo>(condutores.Select(x => Mapear(x, nomes)).ToList(),
            pagina, tamanho, total);
    }

    public async Task<RespostaComVersao<CondutorResumo>?> ObterAsync(Guid id, CancellationToken ct)
    {
        var condutor = await db.Condutores.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (condutor is null) return null;
        return new(Mapear(condutor, await NomesClientesAsync([condutor.ClienteId], ct)), Etag.De(condutor.Versao));
    }

    public async Task<RespostaComVersao<CondutorResumo>> CriarAsync(ComandoCondutor comando, CancellationToken ct)
    {
        try
        {
            var condutor = await escrita.ExecutarAsync(async token =>
            {
                if (!await db.Clientes.AnyAsync(x => x.Id == comando.ClienteId, token))
                    throw new ValidacaoException("clienteId", "Selecione um cliente ativo.");
                var novo = Condutor.Criar(comando.ClienteId, comando.Nome, comando.Email, comando.Cpf,
                    comando.Cnh, comando.ValidadeCnh, comando.Telefone, escrita.EmpresaId, relogio.Agora);
                var incompativel = await db.Clientes.AnyAsync(x =>
                    x.Tipo == TipoCliente.PF && x.Cpf == novo.Cpf && x.Cnh != novo.Cnh, token);
                if (incompativel)
                    throw new ConflitoException("A CNH deve coincidir com a do cliente PF da mesma pessoa.", "cnh");
                db.Condutores.Add(novo);
                escrita.Auditar("condutor.criado", nameof(Condutor), novo.Id,
                    "clienteId", "nome", "email", "cpf", "cnh", "validadeCnh", "telefone");
                return novo;
            }, ct);
            return new RespostaComVersao<CondutorResumo>(
                Mapear(condutor, await NomesClientesAsync([condutor.ClienteId], ct)),
                Etag.De(condutor.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("CPF já usado por condutor ativo.", "cpf");
        }
    }

    public async Task<RespostaComVersao<CondutorResumo>> EditarAsync(Guid id, ComandoCondutor comando,
        string? ifMatch, CancellationToken ct)
    {
        try
        {
            var condutor = await escrita.ExecutarAsync(async token =>
            {
                var atual = await db.Condutores.SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new NaoEncontradoException();
                Etag.Exigir(ifMatch, atual.Versao);
                if (atual.ClienteId != comando.ClienteId &&
                    !await db.Clientes.AnyAsync(x => x.Id == comando.ClienteId, token))
                    throw new ValidacaoException("clienteId", "Selecione um cliente ativo.");
                atual.Atualizar(comando.ClienteId, comando.Nome, comando.Email, comando.Cpf,
                    comando.Cnh, comando.ValidadeCnh, comando.Telefone, relogio.Agora);
                var clientes = await db.Clientes.Where(x =>
                    x.Tipo == TipoCliente.PF && x.Cpf == atual.Cpf).ToListAsync(token);
                foreach (var cliente in clientes)
                {
                    cliente.SincronizarCnh(atual.Cnh, relogio.Agora);
                    db.Entry(cliente).Property(x => x.Cnh).IsModified = true;
                    escrita.Auditar("cliente.cnh-sincronizada", nameof(Cliente), cliente.Id, "cnh");
                }
                escrita.Auditar("condutor.editado", nameof(Condutor), atual.Id,
                    "clienteId", "nome", "email", "cpf", "cnh", "validadeCnh", "telefone");
                return atual;
            }, ct);
            return new RespostaComVersao<CondutorResumo>(
                Mapear(condutor, await NomesClientesAsync([condutor.ClienteId], ct)),
                Etag.De(condutor.Versao));
        }
        catch (DbUpdateException erro) when (SqlErrors.Duplicidade(erro))
        {
            throw new ConflitoException("CPF já usado por condutor ativo.", "cpf");
        }
    }

    public async Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await escrita.ExecutarAsync(async token =>
        {
            var condutor = await db.Condutores.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, condutor.Versao);
            condutor.ExcluidoEmUtc = relogio.Agora;
            escrita.Auditar("condutor.excluido", nameof(Condutor), condutor.Id);
            return true;
        }, ct);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NomesClientesAsync(
        IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToArray();
        return await db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == escrita.EmpresaId && lista.Contains(x.Id))
            .Select(x => new { x.Id, x.Nome })
            .ToDictionaryAsync(x => x.Id, x => x.Nome, ct);
    }

    private static CondutorResumo Mapear(Condutor condutor, IReadOnlyDictionary<Guid, string> nomes) =>
        new(condutor.Id, condutor.ClienteId,
            nomes.TryGetValue(condutor.ClienteId, out var nome) ? nome : "Cliente excluído",
            condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh, condutor.ValidadeCnh,
            condutor.Telefone);
}
