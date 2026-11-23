using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/taxas-servicos")]
public sealed class TaxasServicosController(ITaxaService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<TaxaResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaxaResumo>(200)]
    public async Task<IActionResult> Obter(Guid id, [FromQuery] bool paraEdicao, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct, paraEdicao));

    [HttpPost]
    [ProducesResponseType<TaxaResumo>(201)]
    public async Task<IActionResult> Criar(TaxaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(new ComandoTaxa(requisicao.Nome, requisicao.Preco,
            requisicao.Modalidade!.Value), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TaxaResumo>(200)]
    public async Task<IActionResult> Editar(Guid id, TaxaRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, new ComandoTaxa(requisicao.Nome, requisicao.Preco,
            requisicao.Modalidade!.Value), ifMatch, ct);
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

public sealed record TaxaRequisicao(string? Nome, string? Preco,
    [System.ComponentModel.DataAnnotations.Required] ModalidadeTaxa? Modalidade);

[ApiController]
[Authorize]
[Route("api/v1/configuracoes")]
public sealed class ConfiguracoesController(
    ISeguroService seguros,
    ICombustivelService combustiveis) : ControllerBase
{
    [HttpGet("seguros/{tipo}")]
    [ProducesResponseType<SeguroEstado>(200)]
    public async Task<IActionResult> ObterSeguro(TipoSeguro tipo, CancellationToken ct)
    {
        var resultado = await seguros.ObterAsync(tipo, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpPut("seguros/{tipo}")]
    [ProducesResponseType<SeguroEstado>(200)]
    public async Task<IActionResult> ConfigurarSeguro(TipoSeguro tipo, SeguroRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await seguros.ConfigurarAsync(tipo,
            new ComandoSeguro(requisicao.PrecoDiario), ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpGet("combustiveis/{tipo}")]
    [ProducesResponseType<CombustivelEstado>(200)]
    public async Task<IActionResult> ObterCombustivel(Combustivel tipo, CancellationToken ct)
    {
        var resultado = await combustiveis.ObterAsync(tipo, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpPut("combustiveis/{tipo}")]
    [ProducesResponseType<CombustivelEstado>(200)]
    public async Task<IActionResult> ConfigurarCombustivel(Combustivel tipo, CombustivelRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await combustiveis.ConfigurarAsync(tipo,
            new ComandoCombustivel(requisicao.Preco), ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpGet("combustiveis/{tipo}/historico")]
    [ProducesResponseType<Pagina<CombustivelVersao>>(200)]
    public async Task<IActionResult> HistoricoCombustivel(Combustivel tipo, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await combustiveis.HistoricoAsync(tipo, page, pageSize, ct));
}

public sealed record SeguroRequisicao(string? PrecoDiario);

public sealed record CombustivelRequisicao(string? Preco);
