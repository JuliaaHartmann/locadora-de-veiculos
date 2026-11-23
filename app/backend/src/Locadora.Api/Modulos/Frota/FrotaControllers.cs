using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/grupos")]
public sealed class GruposController(IGrupoService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<GrupoResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GrupoDetalhe>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<GrupoDetalhe>(201)]
    public async Task<IActionResult> Criar(GrupoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(new ComandoGrupo(requisicao.Nome), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<GrupoDetalhe>(200)]
    public async Task<IActionResult> Editar(Guid id, GrupoRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, new ComandoGrupo(requisicao.Nome), ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Excluir(Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await servico.ExcluirAsync(id, ifMatch, ct);
        return NoContent();
    }
}

public sealed record GrupoRequisicao(string? Nome);

[ApiController]
[Authorize]
[Route("api/v1/planos")]
public sealed class PlanosController(IPlanoService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<PlanoResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] Guid? grupoId, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(grupoId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlanoResumo>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<PlanoResumo>(201)]
    public async Task<IActionResult> Criar(PlanoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(new ComandoPlano(requisicao.GrupoId, requisicao.Modalidade!.Value,
            requisicao.PrecoDiario, requisicao.PrecoKm, requisicao.FranquiaDiariaKm), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PlanoResumo>(200)]
    public async Task<IActionResult> Editar(Guid id, PlanoEdicaoRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, new ComandoEdicaoPlano(requisicao.PrecoDiario,
            requisicao.PrecoKm, requisicao.FranquiaDiariaKm), ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Excluir(Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await servico.ExcluirAsync(id, ifMatch, ct);
        return NoContent();
    }
}

public sealed record PlanoRequisicao(Guid GrupoId,
    [System.ComponentModel.DataAnnotations.Required] ModalidadePlano? Modalidade, string? PrecoDiario,
    string? PrecoKm, int? FranquiaDiariaKm);

public sealed record PlanoEdicaoRequisicao(string? PrecoDiario, string? PrecoKm, int? FranquiaDiariaKm);

[ApiController]
[Authorize]
[Route("api/v1/veiculos")]
public sealed class VeiculosController(IVeiculoService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<VeiculoResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] Guid? grupoId, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(grupoId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VeiculoResumo>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<VeiculoResumo>(201)]
    public async Task<IActionResult> Criar(VeiculoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(new ComandoVeiculo(requisicao.GrupoId, requisicao.Placa,
            requisicao.Marca, requisicao.Modelo, requisicao.Cor, requisicao.Ano, requisicao.Combustivel!.Value,
            requisicao.CapacidadeLitros), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VeiculoResumo>(200)]
    public async Task<IActionResult> Editar(Guid id, VeiculoRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, new ComandoVeiculo(requisicao.GrupoId, requisicao.Placa,
            requisicao.Marca, requisicao.Modelo, requisicao.Cor, requisicao.Ano, requisicao.Combustivel!.Value,
            requisicao.CapacidadeLitros), ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Excluir(Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await servico.ExcluirAsync(id, ifMatch, ct);
        return NoContent();
    }
}

public sealed record VeiculoRequisicao(Guid GrupoId, string? Placa, string? Marca, string? Modelo,
    string? Cor, int? Ano,
    [System.ComponentModel.DataAnnotations.Required] Combustivel? Combustivel, string? CapacidadeLitros);
