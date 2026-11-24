using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed partial class DevolucaoTests
{
    private const string Rota = "/api/v1/alugueis";

    [TestMethod]
    public async Task Previa_aplica_preco_retroativo_e_desempate_sem_revalidar_CNH_nem_gravar()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var outros = await EntrarEPreparar(b, fabrica);
        var (id, etag, _) = await Contratar(a, dados);
        var rota = $"{Rota}/{id}/devolucao/previa";
        var retorno = new DateTimeOffset(2028, 1, 1, 12, 0, 0, TimeSpan.FromHours(-3));
        Assert.IsTrue(DateOnly.FromDateTime(retorno.DateTime) > dados.Condutor.ValidadeCnh);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var extra = TaxaServico.Criar("Suporte", 10m, ModalidadeTaxa.Diaria, dados.Empresa, retorno);
        var antes = Preco(dados.Empresa, retorno.AddDays(-1), 10, 3);
        var empateOrdem = Preco(dados.Empresa, retorno, 4, 4);
        var empateId = Preco(dados.Empresa, retorno, 5, 5, Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var vigente = Preco(dados.Empresa, retorno, 5, 6, Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var posterior = Preco(dados.Empresa, retorno.AddTicks(1), 11, 8);
        db.AddRange(antes, empateOrdem, empateId, vigente, posterior, extra);
        await db.SaveChangesAsync();
        var auditorias = await db.Auditorias.CountAsync();
        var comando = new ComandoDevolucao(Local(retorno), 510, "40", [extra.Id]);
        var resposta = await a.PostAsJsonAsync(rota, comando);
        var previa = await Ler(resposta, HttpStatusCode.OK);
        Assert.AreEqual(etag, resposta.Headers.ETag!.ToString());
        var demonstrativo = previa.GetProperty("demonstrativo");
        var combustivel = demonstrativo.GetProperty("combustivel");
        Assert.AreEqual(vigente.Id, combustivel.GetProperty("versaoId").GetGuid());
        Assert.AreEqual("60.00", combustivel.GetProperty("parcela").GetProperty("valor").GetString());
        var adicional = demonstrativo.GetProperty("adicionais").EnumerateArray().Single(x => x.GetProperty("incluidoNoRetorno").GetBoolean());
        Assert.AreEqual(demonstrativo.GetProperty("diarias").GetInt32(), adicional.GetProperty("quantidade").GetInt32());
        Assert.AreEqual(previa.GetProperty("versaoAceita").GetString(),
            (await Ler(await a.PostAsJsonAsync(rota, comando with { LitrosRestantes = "40.000" }), HttpStatusCode.OK))
                .GetProperty("versaoAceita").GetString());
        Assert.AreEqual(antes.Id, (await Ler(await a.PostAsJsonAsync(rota, comando with { RetornoReal = Local(retorno.AddTicks(-1)) }),
            HttpStatusCode.OK)).GetProperty("demonstrativo").GetProperty("combustivel").GetProperty("versaoId").GetGuid());
        await Ler(await b.PostAsJsonAsync(rota, comando), HttpStatusCode.NotFound);
        await Ler(await a.PostAsJsonAsync(rota, comando with { TaxaIds = [outros.Taxa.Id] }), HttpStatusCode.NotFound);
        Assert.AreEqual(auditorias, await db.Auditorias.CountAsync());
        Assert.AreEqual(1, await db.ContratosSnapshot.CountAsync());
        Assert.AreEqual(EstadoAluguel.EmAberto, await db.Alugueis.Select(x => x.Estado).SingleAsync());
        Assert.AreEqual(0, (await a.GetFromJsonAsync<JsonElement>("/api/v1/veiculos?disponiveis=true")).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task Previa_rejeita_medidas_invalidas_taxas_repetidas_e_falta_de_preco_mesmo_com_tanque_cheio()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var (id, _, saida) = await Contratar(a, dados);
        var rota = $"{Rota}/{id}/devolucao/previa";
        var comando = new ComandoDevolucao(Local(saida.AddHours(1)), 500, "50", []);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        db.PrecosCombustivel.Add(Preco(dados.Empresa, saida.AddHours(2), 1, 6));
        await db.SaveChangesAsync();
        await Erro(comando, "combustivel");
        await Erro(comando with { LitrosRestantes = "49.0000" }, "litrosRestantes");
        await Erro(comando with { TaxaIds = [dados.Taxa.Id, dados.Taxa.Id] }, "taxaIds");
        await Erro(comando with { TaxaIds = [dados.Taxa.Id] }, "taxaIds");
        await Erro(comando with { RetornoReal = "2019-02-16T23:30:00" }, "retornoReal");
        await Erro(comando with { RetornoReal = "2018-11-04T00:30:00-03:00" }, "retornoReal");
        await Erro(comando with { RetornoReal = Local(saida.AddTicks(-1)) }, "retornoReal");
        using var fracionario = await a.PostAsJsonAsync(rota, new
        {
            comando.RetornoReal, HodometroFinal = 500.5m, comando.LitrosRestantes,
        });
        await Ler(fracionario, HttpStatusCode.BadRequest);

        async Task Erro(ComandoDevolucao entrada, string campo)
        {
            var problema = await Ler(await a.PostAsJsonAsync(rota, entrada), HttpStatusCode.BadRequest);
            Assert.IsTrue(problema.GetProperty("errors").TryGetProperty(campo, out _), problema.ToString());
        }
    }

    private static LocadoraDbContext Banco(AsyncServiceScope escopo, Guid empresa)
    {
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
        return escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
    }

    private static PrecoCombustivel Preco(Guid empresa, DateTimeOffset instante, long ordem, decimal preco, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(), EmpresaId = empresa, Tipo = Combustivel.Gasolina, Preco = preco,
            Ordem = ordem, InicioVigenciaUtc = instante.ToUniversalTime(), CriadoEm = instante, AtualizadoEm = instante,
        };

    private static string Local(DateTimeOffset data) =>
        HorarioLocacao.ParaSaoPaulo(data).ToString("O", CultureInfo.InvariantCulture);

    private static async Task<(Guid Id, string Etag, DateTimeOffset Saida)> Contratar(HttpClient http,
        PersistenciaLocacoesTests.DadosLocacao dados, string chave = "contratar")
    {
        var resposta = await Enviar(http, HttpMethod.Post, Rota, ComandoAtual(dados), "Idempotency-Key", chave);
        var contrato = await Ler(resposta, HttpStatusCode.Created);
        return (contrato.GetProperty("id").GetGuid(), resposta.Headers.ETag!.ToString(), contrato.GetProperty("saida").GetDateTimeOffset());
    }

    private static async Task<JsonElement> Ler(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        var json = await resposta.Content.ReadAsStringAsync();
        Assert.AreEqual(esperado, resposta.StatusCode, $"{resposta.RequestMessage?.RequestUri} — {json}");
        return json.Length == 0 ? default : JsonDocument.Parse(json).RootElement.Clone();
    }
}
