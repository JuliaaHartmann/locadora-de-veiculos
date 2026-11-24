using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed class RepositorioFotos(LocadoraDbContext db) : IRepositorioFotos
{
    public async Task<Veiculo> ValidarVeiculoAsync(Guid id, string? ifMatch, bool rastrear, CancellationToken ct)
    {
        var veiculos = rastrear ? db.Veiculos.AsQueryable() : db.Veiculos.AsNoTracking();
        var veiculo = await veiculos.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NaoEncontradoException();
        Etag.Exigir(ifMatch, veiculo.Versao);
        if (await db.AlugueisAbertos().AnyAsync(x => x.VeiculoId == id, ct))
            throw new ConflitoException("Não é possível alterar a foto de um veículo com aluguel em aberto.", "veiculo");
        return veiculo;
    }

    public Task<FotoObjeto?> LerAtivaAsync(Guid veiculo, bool rastrear, CancellationToken ct)
    {
        var consulta = from foto in db.FotosObjetos
                       join v in db.Veiculos on new { foto.EmpresaId, Id = foto.VeiculoId } equals new { v.EmpresaId, v.Id }
                       where foto.VeiculoId == veiculo && foto.Estado == EstadoFoto.Ativa
                       select foto;
        return (rastrear ? consulta : consulta.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<FotoObjeto?> LerAsync(Guid id, bool rastrear, CancellationToken ct) =>
        (rastrear ? db.FotosObjetos.AsQueryable() : db.FotosObjetos.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<FotoObjeto?> SelecionarCompensacaoAsync(string deposito, DateTimeOffset agora, CancellationToken ct) =>
        db.FotosObjetos.Where(x => x.Deposito == deposito && x.RetomarAposUtc <= agora &&
                (x.Estado == EstadoFoto.Pendente || x.Estado == EstadoFoto.Orfa))
            .OrderBy(x => x.RetomarAposUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);

    public void Adicionar(FotoObjeto foto) => db.FotosObjetos.Add(foto);
    public void LimparRastreamento() => db.ChangeTracker.Clear();
    public async Task SalvarAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    public void AtualizarVersao(Veiculo veiculo, DateTimeOffset agora)
    {
        veiculo.AtualizadoEm = agora;
        db.Entry(veiculo).Property(x => x.AtualizadoEm).IsModified = true;
    }
}
