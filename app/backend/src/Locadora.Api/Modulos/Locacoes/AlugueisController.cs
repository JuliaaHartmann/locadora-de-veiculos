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
        [FromQuery] DateOnly? saidaDe, [FromQuery] DateOnly? saidaAte, [FromQuery] string? estado,
        [FromQuery] bool? incluirOcultos, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, veiculoId, saidaDe, saidaAte, ct, estado switch
        {
            null => null,
            "EmAberto" => EstadoAluguel.EmAberto,
            "Concluido" => EstadoAluguel.Concluido,
            _ => throw new ValidacaoException("estado", "Informe EmAberto ou Concluido."),
        }, incluirOcultos == true));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AluguelDetalhe>(200)]
    public async Task<IActionResult> Obter(Guid id, [FromQuery] bool? incluirOcultos, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct, incluirOcultos == true));

    [HttpPost("estimativas")]
    [ProducesResponseType<PreviaAluguel>(200)]
    public async Task<IActionResult> Estimar(AluguelRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.EstimarAsync(requisicao.Comando(), ct));

    [HttpPost("{id:guid}/devolucao/previa")]
    [ProducesResponseType<PreviaDevolucao>(200)]
    public async Task<IActionResult> PreverDevolucao(Guid id, DevolucaoRequisicao requisicao, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.PreverDevolucaoAsync(id, requisicao.Comando(), ct));

    [HttpPost("{id:guid}/devolucao")]
    [ProducesResponseType<DevolucaoConcluida>(200)]
    public async Task<IActionResult> ConcluirDevolucao(Guid id, ConfirmacaoDevolucaoRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromHeader(Name = "Idempotency-Key")] string? chave,
        CancellationToken ct)
    {
        ExigirCabecalhoDeChaveUnico();
        return RespostasHttp.ComVersao(Response, await servico.ConcluirAsync(id, requisicao.Comando(),
            requisicao.VersaoAceita, ifMatch, chave, ct));
    }

    [HttpPost]
    [ProducesResponseType<AluguelDetalhe>(201)]
    public async Task<IActionResult> Criar(AluguelRequisicao requisicao,
        [FromHeader(Name = "Idempotency-Key")] string? chave, CancellationToken ct)
    {
        ExigirCabecalhoDeChaveUnico();
        var resultado = await servico.CriarAsync(requisicao.Comando(), chave, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AluguelDetalhe>(200)]
    public async Task<IActionResult> Editar(Guid id, AluguelRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.EditarAsync(id, requisicao.Comando(), ifMatch, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Ocultar(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await servico.OcultarAsync(id, ifMatch, ct);
        return NoContent();
    }

    private void ExigirCabecalhoDeChaveUnico()
    {
        if (Request.Headers["Idempotency-Key"].Count > 1)
            throw new ValidacaoException("idempotencyKey", "Envie uma única chave de idempotência.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DevolucaoRequisicao([Required] string? RetornoReal,
    [Required][property: JsonNumberHandling(JsonNumberHandling.Strict)] int? HodometroFinal,
    [Required] string? LitrosRestantes, Guid[]? TaxaIds)
{
    public ComandoDevolucao Comando() => new(RetornoReal, HodometroFinal, LitrosRestantes, TaxaIds);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmacaoDevolucaoRequisicao(string? RetornoReal,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] int? HodometroFinal,
    string? LitrosRestantes, Guid[]? TaxaIds, string? VersaoAceita)
{
    public ComandoDevolucao Comando() => new(RetornoReal, HodometroFinal, LitrosRestantes, TaxaIds);
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
