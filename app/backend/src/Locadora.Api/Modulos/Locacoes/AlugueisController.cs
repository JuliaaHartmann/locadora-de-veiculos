using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/alugueis")]
public sealed class AlugueisController(AluguelService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<AluguelResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] Guid? veiculoId,
        [FromQuery] DateOnly? saidaDe, [FromQuery] DateOnly? saidaAte, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, veiculoId, saidaDe, saidaAte, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AluguelDetalhe>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost("estimativas")]
    [ProducesResponseType<PreviaAluguel>(200)]
    public async Task<IActionResult> Estimar(AluguelRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.EstimarAsync(requisicao.Comando(), ct));

    [HttpPost]
    [ProducesResponseType<AluguelDetalhe>(201)]
    public async Task<IActionResult> Criar(AluguelRequisicao requisicao,
        [FromHeader(Name = "Idempotency-Key")] string? chave, CancellationToken ct)
    {
        if (Request.Headers["Idempotency-Key"].Count > 1)
            throw new ValidacaoException("idempotencyKey", "Envie uma única chave de idempotência.");
        var resultado = await servico.CriarAsync(requisicao.Comando(), chave, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AluguelDetalhe>(200)]
    public async Task<IActionResult> Editar(Guid id, AluguelRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.EditarAsync(id, requisicao.Comando(), ifMatch, ct));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AluguelRequisicao(Guid ClienteId, Guid CondutorId, Guid VeiculoId, Guid PlanoId,
    [Required] string? RetornoPrevisto, [Required] int? HodometroInicial, Guid[]? TaxaIds, string[]? Seguros)
{
    public ComandoAluguel Comando() => new(ClienteId, CondutorId, VeiculoId, PlanoId, RetornoPrevisto, HodometroInicial,
        TaxaIds, Seguros?.Select(x => x switch
        {
            "Cliente" => TipoSeguro.Cliente,
            "Terceiros" => TipoSeguro.Terceiros,
            _ => throw new ValidacaoException("seguros", "Selecione seguro do cliente ou de terceiros."),
        }).ToArray());
}
