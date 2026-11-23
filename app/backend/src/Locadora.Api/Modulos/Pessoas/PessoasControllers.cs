using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/clientes")]
public sealed class ClientesController(IClienteService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<ClienteResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClienteDetalhe>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<ClienteDetalhe>(201)]
    public async Task<IActionResult> Criar(ClienteRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(Mapear(requisicao), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ClienteDetalhe>(200)]
    public async Task<IActionResult> Editar(Guid id, ClienteRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, Mapear(requisicao), ifMatch, ct);
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

    [HttpGet("{id:guid}/vinculos-pj")]
    [ProducesResponseType<IReadOnlyList<ClienteVinculado>>(200)]
    public async Task<IActionResult> ListarVinculos(Guid id, CancellationToken ct)
    {
        var resultado = await servico.ListarVinculosAsync(id, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpPost("{id:guid}/vinculos-pj")]
    [ProducesResponseType<IReadOnlyList<ClienteVinculado>>(200)]
    public async Task<IActionResult> AdicionarVinculo(Guid id, VinculoRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.AdicionarVinculoAsync(id, new ComandoVinculo(requisicao.ClientePjId),
            ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}/vinculos-pj/{clientePjId:guid}")]
    [ProducesResponseType<IReadOnlyList<ClienteVinculado>>(200)]
    public async Task<IActionResult> RemoverVinculo(Guid id, Guid clientePjId,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.RemoverVinculoAsync(id, clientePjId, ifMatch, ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return Ok(resultado.Valor);
    }

    private static ComandoCliente Mapear(ClienteRequisicao requisicao) =>
        new(requisicao.Tipo!.Value, requisicao.Nome, requisicao.Cpf, requisicao.Rg, requisicao.Cnh,
            requisicao.Cnpj, requisicao.Telefone, requisicao.Email,
            requisicao.Endereco?.Estado, requisicao.Endereco?.Cidade, requisicao.Endereco?.Bairro,
            requisicao.Endereco?.Rua, requisicao.Endereco?.Numero);
}

public sealed record ClienteRequisicao(
    [System.ComponentModel.DataAnnotations.Required] TipoCliente? Tipo, string? Nome, string? Cpf, string? Rg,
    string? Cnh, string? Cnpj, string? Telefone, string? Email, EnderecoRequisicao? Endereco);

public sealed record EnderecoRequisicao(string? Estado, string? Cidade, string? Bairro,
    string? Rua, string? Numero);

public sealed record VinculoRequisicao(Guid ClientePjId);

[ApiController]
[Authorize]
[Route("api/v1/condutores")]
public sealed class CondutoresController(ICondutorService servico) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<CondutorResumo>>(200)]
    public async Task<IActionResult> Listar([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await servico.ListarAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CondutorResumo>(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) =>
        RespostasHttp.ComVersao(Response, await servico.ObterAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<CondutorResumo>(201)]
    public async Task<IActionResult> Criar(CondutorRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(Mapear(requisicao), ct);
        RespostasHttp.DefinirEtag(Response, resultado.Etag);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Valor.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CondutorResumo>(200)]
    public async Task<IActionResult> Editar(Guid id, CondutorRequisicao requisicao,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(id, Mapear(requisicao), ifMatch, ct);
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

    private static ComandoCondutor Mapear(CondutorRequisicao requisicao) =>
        new(requisicao.ClienteId, requisicao.Nome, requisicao.Email, requisicao.Cpf, requisicao.Cnh,
            requisicao.ValidadeCnh, requisicao.Telefone);
}

public sealed record CondutorRequisicao(Guid ClienteId, string? Nome, string? Email, string? Cpf,
    string? Cnh, DateOnly? ValidadeCnh, string? Telefone);
