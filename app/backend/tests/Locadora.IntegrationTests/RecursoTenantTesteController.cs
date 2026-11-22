using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests;

[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/teste/tenant")]
public sealed class RecursoTenantTesteController(
    LocadoraDbContext banco,
    IContextoEmpresa contexto) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(RecursoRequisicao requisicao, CancellationToken ct)
    {
        if (contexto.EmpresaId is not Guid empresaId) return NotFound();
        if (requisicao.ReferenciaId is Guid referencia && !await banco.RecursosTenantTeste.AnyAsync(x => x.Id == referencia, ct))
            return NotFound();
        var recurso = new RecursoTenantTeste { EmpresaId = empresaId, Valor = requisicao.Valor, ReferenciaId = requisicao.ReferenciaId };
        banco.RecursosTenantTeste.Add(recurso);
        await banco.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Obter), new { id = recurso.Id }, new { recurso.Id, recurso.Valor });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        if (contexto.EmpresaId is not Guid empresaId) return NotFound();
        var recurso = await banco.RecursosTenantTeste.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return recurso is null ? NotFound() : Ok(new { recurso.Id, recurso.Valor });
    }

    [HttpGet("historico")]
    public async Task<IActionResult> Historico(CancellationToken ct)
    {
        if (contexto.EmpresaId is not Guid empresaId) return NotFound();
        return Ok(await banco.Auditorias.AsNoTracking().Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.EmpresaId, x.Evento }).ToListAsync(ct));
    }
}

public sealed record RecursoRequisicao(string Valor, Guid? EmpresaId, Guid? ReferenciaId);
