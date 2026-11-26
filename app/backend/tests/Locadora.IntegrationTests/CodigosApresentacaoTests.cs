using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class CodigosApresentacaoTests
{
    [TestMethod]
    [DataRow(1, "0001")]
    [DataRow(9999, "9999")]
    [DataRow(10000, "10000")]
    [DataRow(100000, "100000")]
    public void Formato_codigo_cresce_sem_truncar(int numero, string esperado) =>
        Assert.AreEqual(esperado, Locadora.Domain.EntidadeNumerada.FormatarCodigo(numero));

    private const string Grupos = "/api/v1/grupos";
    private const string Veiculos = "/api/v1/veiculos";
    private const string Alugueis = "/api/v1/alugueis";
    private const string Fase5 = "20261124131637_Fase5FotosVeiculos";

    [TestMethod]
    public async Task Codigos_concorrentes_isolados_e_reversao_de_insercao_rejeitada()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var paralelo = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        await EntrarEPreparar(b, fabrica);
        paralelo.DefaultRequestHeaders.Authorization = a.DefaultRequestHeaders.Authorization;
        var criados = await Task.WhenAll(
            Criar(a, Grupos, new { nome = "A" }),
            Criar(paralelo, Grupos, new { nome = "B" }),
            Criar(b, Grupos, new { nome = "Outra empresa" }));
        CollectionAssert.AreEquivalent(new[] { "0002", "0003" }, criados.Take(2).Select(Codigo).ToArray());
        Assert.AreEqual("0002", Codigo(criados[2]));
        using var alheio = await b.GetAsync($"{Grupos}/{criados[0].GetProperty("id").GetGuid()}");
        Assert.AreEqual(HttpStatusCode.NotFound, alheio.StatusCode);

        object Veiculo(string placa) => new
        {
            grupoId = dados.Grupo.Id, placa, marca = "Exemplo", modelo = "Aurora",
            cor = "Cinza", ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50",
        };
        using var duplicado = await a.PostAsJsonAsync(Veiculos, Veiculo(dados.Veiculo.Placa));
        Assert.AreEqual(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.AreEqual("0002", Codigo(await Criar(a, Veiculos, Veiculo("SIM0A08"))));
    }

    [TestMethod]
    public async Task Codigos_limite_bloqueia_somente_o_tipo_e_exclusao_nao_reutiliza()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        await EntrarEPreparar(b, fabrica);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.ContadoresCodigo SET UltimoNumero = 999998 WHERE EmpresaId = {dados.Empresa} AND TipoRecurso = 1");
        using var ultimo = await a.PostAsJsonAsync(Grupos, new { nome = "Último" });
        Assert.AreEqual(HttpStatusCode.Created, ultimo.StatusCode);
        var corpo = await ultimo.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("999999", Codigo(corpo));
        var rota = $"{Grupos}/{corpo.GetProperty("id").GetGuid()}";
        using var excluido = await Enviar(a, HttpMethod.Delete, rota, null, "If-Match", ultimo.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, excluido.StatusCode);
        using var limite = await a.PostAsJsonAsync(Grupos, new { nome = "Não deve existir" });
        Assert.AreEqual(HttpStatusCode.Conflict, limite.StatusCode);
        StringAssert.Contains(await limite.Content.ReadAsStringAsync(), "limite de c");
        Assert.AreEqual("0002", Codigo(await Criar(b, Grupos, new { nome = "Outra empresa" })));
        Assert.AreEqual("0002", Codigo(await Criar(a, "/api/v1/taxas-servicos",
            new { nome = "Outro tipo", preco = "1", modalidade = "Fixa" })));
        var lista = await a.GetFromJsonAsync<JsonElement>(Grupos);
        Assert.AreEqual(1, lista.GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task Codigos_migracao_do_legado_inclui_excluidos_preserva_replay_e_exige_ETag_atual()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var a = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var migra = db.GetService<IMigrator>();
        try
        {
            await migra.MigrateAsync();
            var dados = await EntrarEPreparar(a, fabrica);
            var comando = ComandoAtual(dados);
            using var antes = await Enviar(a, HttpMethod.Post, Alugueis, comando, "Idempotency-Key", "codigo-legado");
            Assert.AreEqual(HttpStatusCode.Created, antes.StatusCode);
            var aluguel = await antes.Content.ReadFromJsonAsync<JsonElement>();
            var id = aluguel.GetProperty("id").GetGuid();
            var rotaAluguel = $"{Alugueis}/{id}";
            const string rotaCombustivel = "/api/v1/configuracoes/combustiveis/Gasolina";
            using var semPreco = await a.GetAsync(rotaCombustivel);
            using var preco = await Enviar(a, HttpMethod.Put, rotaCombustivel, new { preco = "6" },
                "If-Match", semPreco.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.OK, preco.StatusCode);
            var retornoReal = aluguel.GetProperty("retornoPrevisto").GetString();
            const int hodometroFinal = 550;
            const string litrosRestantes = "40";
            var devolucao = new { retornoReal, hodometroFinal, litrosRestantes, taxaIds = Array.Empty<Guid>() };
            using var previa = await a.PostAsJsonAsync($"{rotaAluguel}/devolucao/previa", devolucao);
            Assert.AreEqual(HttpStatusCode.OK, previa.StatusCode);
            var versao = (await previa.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versaoAceita").GetString();
            using var confirmar = new HttpRequestMessage(HttpMethod.Post, $"{rotaAluguel}/devolucao")
            {
                Content = JsonContent.Create(new { retornoReal, hodometroFinal, litrosRestantes,
                    devolucao.taxaIds, versaoAceita = versao }),
            };
            confirmar.Headers.TryAddWithoutValidation("If-Match", antes.Headers.ETag!.ToString());
            confirmar.Headers.TryAddWithoutValidation("Idempotency-Key", "devolucao-legada");
            using var final = await a.SendAsync(confirmar);
            Assert.AreEqual(HttpStatusCode.OK, final.StatusCode);
            using var ocultar = await Enviar(a, HttpMethod.Delete, rotaAluguel, null, "If-Match", final.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.NoContent, ocultar.StatusCode);
            var rotaGrupo = $"{Grupos}/{dados.Grupo.Id}";
            using var grupoAntes = await a.GetAsync(rotaGrupo);
            await migra.MigrateAsync(Fase5);

            var excluido = Guid.NewGuid();
            var anterior = dados.Agora.AddDays(-1);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.Grupos (Id, EmpresaId, Nome, CriadoEm, AtualizadoEm, ExcluidoEmUtc)
                VALUES ({excluido}, {dados.Empresa}, N'Grupo legado excluído', {anterior}, {anterior}, {anterior});
                """);
            await migra.MigrateAsync();
            escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(dados.Empresa);
            Assert.AreEqual(1, await db.Grupos.IgnoreQueryFilters().Where(x => x.Id == excluido)
                .Select(x => x.NumeroExibicao).SingleAsync());
            using var grupoDepois = await a.GetAsync(rotaGrupo);
            Assert.AreEqual("0002", Codigo(await grupoDepois.Content.ReadFromJsonAsync<JsonElement>()));
            Assert.AreNotEqual(grupoAntes.Headers.ETag, grupoDepois.Headers.ETag);
            using var desatualizado = await Enviar(a, HttpMethod.Put, rotaGrupo, new { nome = "Não aplicar" },
                "If-Match", grupoAntes.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, desatualizado.StatusCode);
            Assert.AreEqual("0003", Codigo(await Criar(a, Grupos, new { nome = "Após migração" })));

            using var replay = await Enviar(a, HttpMethod.Post, Alugueis, comando, "Idempotency-Key", "codigo-legado");
            Assert.AreEqual(HttpStatusCode.Created, replay.StatusCode);
            var repetido = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual(id, repetido.GetProperty("id").GetGuid());
            Assert.AreEqual("0001", Codigo(repetido));
            Assert.AreEqual(antes.Headers.ETag, replay.Headers.ETag);
            using var oculto = await a.GetAsync(rotaAluguel);
            Assert.AreEqual(HttpStatusCode.NotFound, oculto.StatusCode);
            var historico = await a.GetFromJsonAsync<JsonElement>(rotaAluguel + "?incluirOcultos=true");
            Assert.AreEqual("0001", Codigo(historico));
            Assert.AreEqual(1, await db.Alugueis.IgnoreQueryFilters().CountAsync(x => x.EmpresaId == dados.Empresa));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<JsonElement> Criar(HttpClient cliente, string rota, object corpo)
    {
        using var resposta = await cliente.PostAsJsonAsync(rota, corpo);
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode,
            resposta.StatusCode == HttpStatusCode.Created ? null : await resposta.Content.ReadAsStringAsync());
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string Codigo(JsonElement corpo) => corpo.GetProperty("numeroExibicao").GetString()!;
}
