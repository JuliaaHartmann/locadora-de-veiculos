using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class DevolucaoTests
{
    [TestMethod]
    public async Task Confirmacao_http_exige_precondicao_e_replay_recupera_o_final_apos_perda_de_resposta()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        using var semResposta = fabrica.CreateDefaultClient(new PerderRespostaFechamento());
        var dados = await EntrarEPreparar(a, fabrica);
        await EntrarEPreparar(b, fabrica);
        semResposta.DefaultRequestHeaders.Authorization = a.DefaultRequestHeaders.Authorization;
        var (id, etag, saida) = await Contratar(a, dados);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var entrada = await PrepararMedicoes(db, dados.Empresa, saida);
        var previa = await ObterPrevia(a, id, entrada);
        var rota = $"{Rota}/{id}/devolucao";

        var obrigatoria = await Ler(await EnviarFechamento(a, id, entrada, previa, null, "fechamento"), HttpStatusCode.PreconditionRequired);
        Assert.AreEqual("precondicao_obrigatoria", obrigatoria.GetProperty("code").GetString());
        await Ler(await EnviarFechamento(a, id, entrada, previa, "\"antigo\"", "fechamento"), HttpStatusCode.PreconditionFailed);
        await Ler(await EnviarFechamento(b, id, entrada, previa, etag, "fechamento"), HttpStatusCode.NotFound);
        await Ler(await EnviarFechamento(a, id, entrada, previa, etag, null), HttpStatusCode.BadRequest);

        var alterado = await Ler(await EnviarFechamento(a, id, entrada with { LitrosRestantes = "41" }, previa, etag, "fechamento"),
            HttpStatusCode.Conflict);
        Assert.IsTrue(alterado.GetProperty("errors").TryGetProperty("versaoAceita", out _));
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            EnviarFechamento(semResposta, id, entrada, previa, etag, "fechamento"));

        var respostaReplay = await EnviarFechamento(a, id, entrada with { LitrosRestantes = "40.000" }, previa, null, "fechamento");
        var final = await Ler(respostaReplay, HttpStatusCode.OK);
        Assert.AreEqual("Concluido", final.GetProperty("estado").GetString());
        var salvo = await db.FechamentosAlugueis.AsNoTracking().SingleAsync();
        Assert.AreEqual(salvo.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            final.GetProperty("final").GetProperty("total").GetString());
        Assert.AreNotEqual(etag, respostaReplay.Headers.ETag!.ToString());
        var chaveRepetida = await Ler(await EnviarFechamento(a, id, entrada with { HodometroFinal = 520 }, previa, etag, "fechamento"),
            HttpStatusCode.Conflict);
        Assert.IsTrue(chaveRepetida.GetProperty("errors").TryGetProperty("idempotencyKey", out _));
        var outraChave = await Ler(await EnviarFechamento(a, id, entrada, previa, etag, "outra"), HttpStatusCode.Conflict);
        Assert.IsTrue(outraChave.GetProperty("errors").TryGetProperty("estado", out _));
        await Ler(await EnviarFechamento(b, id, entrada, previa, etag, "fechamento"), HttpStatusCode.NotFound);
        Assert.AreEqual(1, await db.IdempotenciaAlugueis.CountAsync(x => x.Operacao == "devolver"));
        Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.devolvido"));
        Assert.AreEqual(1, (await a.GetFromJsonAsync<JsonElement>("/api/v1/veiculos?disponiveis=true")).GetProperty("total").GetInt32());
        await Ler(await a.PostAsync("/api/v1/session/logout", null), HttpStatusCode.NoContent);
        await Ler(await a.PostAsJsonAsync(rota, entrada), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task OpenAPI_documenta_endpoint_de_confirmacao_e_versao_de_previa()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        var doc = await a.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var rotas = doc.GetProperty("paths");
        var fechar = rotas.GetProperty($"{Rota}/{{id}}/devolucao").GetProperty("post");
        var parametros = fechar.GetProperty("parameters").EnumerateArray().ToArray();
        foreach (var header in new[] { "If-Match", "Idempotency-Key" })
            Assert.IsTrue(parametros.Single(x => x.GetProperty("name").GetString() == header).GetProperty("required").GetBoolean());
        var respostas = fechar.GetProperty("responses");
        foreach (var status in new[] { "400", "401", "404", "409", "412", "428", "503" })
            Assert.IsTrue(respostas.GetProperty(status).GetProperty("content").TryGetProperty("application/problem+json", out _));
        Assert.IsTrue(respostas.GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _));
        Assert.IsTrue(rotas.GetProperty($"{Rota}/{{id}}/devolucao/previa").GetProperty("post").GetProperty("responses")
            .GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _));
    }

    private static async Task<string> ObterPrevia(HttpClient http, Guid id, ComandoDevolucao comando) =>
        (await Ler(await http.PostAsJsonAsync($"{Rota}/{id}/devolucao/previa", comando), HttpStatusCode.OK))
            .GetProperty("versaoAceita").GetString()!;

    private static async Task<HttpResponseMessage> EnviarFechamento(HttpClient http, Guid id, ComandoDevolucao entrada,
        string versaoAceita, string? etag, string? chave)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, $"{Rota}/{id}/devolucao");
        pedido.Content = JsonContent.Create(new { entrada.RetornoReal, entrada.HodometroFinal, entrada.LitrosRestantes,
            entrada.TaxaIds, versaoAceita });
        if (etag is not null) pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (chave is not null) pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        return await http.SendAsync(pedido);
    }

    private sealed class PerderRespostaFechamento : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var resposta = await base.SendAsync(request, cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode, await resposta.Content.ReadAsStringAsync(cancellationToken));
            throw new HttpRequestException("Perda sintética da resposta após commit.");
        }
    }
}
