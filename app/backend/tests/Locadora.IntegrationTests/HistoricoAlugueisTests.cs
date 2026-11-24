using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class DevolucaoTests
{
    [TestMethod]
    public async Task CA12_final_permanece_integral_apos_precos_exclusoes_e_reutilizacao_de_identificadores()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var plano = Plano.Criar(dados.Grupo.Id, ModalidadePlano.Livre, 100m, null, null, dados.Empresa, dados.Agora);
        var extra = TaxaServico.Criar("Suporte de viagem", 10m, ModalidadeTaxa.Diaria, dados.Empresa, dados.Agora);
        db.AddRange(plano, extra, SeguroConfiguracao.Criar(TipoSeguro.Cliente, 5m, dados.Empresa, dados.Agora),
            SeguroConfiguracao.Criar(TipoSeguro.Terceiros, 7m, dados.Empresa, dados.Agora));
        await db.SaveChangesAsync();
        var contratacao = ComandoAtual(dados with { Plano = plano }) with
        {
            RetornoPrevisto = Local(DateTimeOffset.UtcNow.AddHours(30)), Seguros = [TipoSeguro.Cliente, TipoSeguro.Terceiros],
            HodometroInicial = 21_430,
        };
        var criacao = await Enviar(a, HttpMethod.Post, Rota, new
        {
            contratacao.ClienteId, contratacao.CondutorId, contratacao.VeiculoId, contratacao.PlanoId,
            contratacao.RetornoPrevisto, contratacao.HodometroInicial, contratacao.TaxaIds,
            Seguros = contratacao.Seguros!.Select(x => x.ToString()).ToArray(),
        }, "Idempotency-Key", "original");
        var aluguel = await Ler(criacao, HttpStatusCode.Created);
        var id = aluguel.GetProperty("id").GetGuid();
        var rota = $"{Rota}/{id}";
        var saida = aluguel.GetProperty("saida").GetDateTimeOffset();
        var previsto = aluguel.GetProperty("retornoPrevisto").GetDateTimeOffset();
        var combustivel = Preco(dados.Empresa, saida, 1, 6m);
        db.PrecosCombustivel.Add(combustivel);
        await db.SaveChangesAsync();

        var emDia = new ComandoDevolucao(Local(previsto), 21_680, "40", [extra.Id]);
        var previa = await Ler(await a.PostAsJsonAsync($"{rota}/devolucao/previa", emDia), HttpStatusCode.OK);
        Assert.AreEqual("334.00", previa.GetProperty("demonstrativo").GetProperty("total").GetString());
        var atrasado = emDia with { RetornoReal = Local(previsto.AddMinutes(1)) };
        var final = (await Ler(await EnviarFechamento(a, id, atrasado, await ObterPrevia(a, id, atrasado),
            criacao.Headers.ETag!.ToString(), "original"), HttpStatusCode.OK)).GetProperty("final");
        Assert.AreEqual("354.00", final.GetProperty("total").GetString());
        var adicional = final.GetProperty("adicionais").EnumerateArray().Single(x => x.GetProperty("incluidoNoRetorno").GetBoolean());
        Assert.AreEqual(2, adicional.GetProperty("quantidade").GetInt32());
        Assert.AreEqual("10", adicional.GetProperty("preco").GetString());
        Assert.AreEqual("20.00", adicional.GetProperty("parcela").GetProperty("valor").GetString());
        Assert.AreEqual(combustivel.Id, final.GetProperty("combustivel").GetProperty("versaoId").GetGuid());
        Assert.AreEqual("1000.00", final.GetProperty("caucao").GetString());
        var antes = await Ler(await a.GetAsync(rota), HttpStatusCode.OK);

        var (novaContratacao, versaoNova, saidaNova) = await Contratar(a, dados, "mesmo-veiculo-liberado");
        var retornoNovo = new ComandoDevolucao(Local(saidaNova.AddHours(1)), 510, "50", []);
        await Ler(await EnviarFechamento(a, novaContratacao, retornoNovo, await ObterPrevia(a, novaContratacao, retornoNovo),
            versaoNova, "segunda"), HttpStatusCode.OK);

        await Alterar($"/api/v1/planos/{plano.Id}", new { precoDiario = "175" });
        await Alterar("/api/v1/configuracoes/seguros/Cliente", new { precoDiario = "26" });
        await Alterar("/api/v1/configuracoes/seguros/Terceiros", new { precoDiario = "27" });
        await Alterar("/api/v1/configuracoes/combustiveis/Gasolina", new { preco = "8.75" });
        await Alterar($"/api/v1/taxas-servicos/{extra.Id}", new { nome = "Outro suporte", preco = "99", modalidade = "Fixa" });

        string veiculo = $"/api/v1/veiculos/{dados.Veiculo.Id}", cliente = $"/api/v1/clientes/{dados.Cliente.Id}";
        foreach (var excluir in new[] { veiculo, $"/api/v1/condutores/{dados.Condutor.Id}", cliente,
                     $"/api/v1/taxas-servicos/{extra.Id}", $"/api/v1/taxas-servicos/{dados.Taxa.Id}" })
        {
            await Ler(await Enviar(a, HttpMethod.Delete, excluir, null, "If-Match", await Versao(excluir)), HttpStatusCode.NoContent);
            await Ler(await a.GetAsync(excluir), HttpStatusCode.NotFound);
        }
        var reusoPlaca = await Ler(await a.PostAsJsonAsync("/api/v1/veiculos", new
        {
            dados.Veiculo.GrupoId, dados.Veiculo.Placa, marca = "Outra marca", modelo = "Outra identidade",
            dados.Veiculo.Cor, dados.Veiculo.Ano, combustivel = "Diesel", capacidadeLitros = "70",
        }), HttpStatusCode.Created);
        var reusoCnpj = await Ler(await a.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Outro contratante", dados.Cliente.Cnpj, dados.Cliente.Telefone, dados.Cliente.Endereco,
        }), HttpStatusCode.Created);
        Assert.AreNotEqual(dados.Veiculo.Id, reusoPlaca.GetProperty("id").GetGuid());
        Assert.AreNotEqual(dados.Cliente.Id, reusoCnpj.GetProperty("id").GetGuid());
        var depois = await Ler(await a.GetAsync(rota), HttpStatusCode.OK);
        Assert.AreEqual(antes.GetRawText(), depois.GetRawText());
        Assert.AreEqual(final.GetRawText(), depois.GetProperty("final").GetRawText());
        Assert.AreEqual(dados.Veiculo.Id, depois.GetProperty("veiculo").GetProperty("id").GetGuid());
        Assert.AreEqual(dados.Cliente.Id, depois.GetProperty("cliente").GetProperty("id").GetGuid());
        Assert.AreEqual(0, await Total(a, $"?veiculoId={reusoPlaca.GetProperty("id").GetGuid()}"));

        var finalGravado = await db.FechamentosAlugueis.AsNoTracking().Include(x => x.Adicionais).SingleAsync(x => x.AluguelId == id);
        var revisao = await db.ContratosSnapshot.AsNoTracking().Include(x => x.Itens).SingleAsync(x => x.Id == finalGravado.RevisaoId);
        Assert.AreEqual(dados.Condutor.Id, revisao.CondutorId);
        Assert.AreEqual(dados.Grupo.Id, revisao.GrupoId);
        Assert.AreEqual(dados.Veiculo.Id, revisao.VeiculoId);
        Assert.AreEqual(plano.Id, revisao.PlanoId);
        Assert.HasCount(3, revisao.Itens);
        Assert.AreEqual(100m, revisao.PrecoDiario);
        Assert.AreEqual(21_430, finalGravado.HodometroInicial);
        Assert.AreEqual(21_680, finalGravado.HodometroFinal);
        Assert.AreEqual(40m, finalGravado.LitrosRestantes);
        Assert.AreEqual(atrasado.RetornoReal, Local(finalGravado.RetornoRealUtc));
        Assert.AreEqual(354m, finalGravado.Total);
        Assert.HasCount(4, finalGravado.Adicionais);
        CollectionAssert.AreEqual(extra.Versao, finalGravado.Adicionais.Single(x => x.TaxaId == extra.Id).VersaoTaxaAdicional!);

        async Task<string> Versao(string caminho)
        {
            var resposta = await a.GetAsync(caminho);
            await Ler(resposta, HttpStatusCode.OK);
            return resposta.Headers.ETag!.ToString();
        }

        async Task Alterar(string caminho, object corpo) => await Ler(
            await Enviar(a, HttpMethod.Put, caminho, corpo, "If-Match", await Versao(caminho)), HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Ocultacao_exige_concluido_Etag_e_auditoria_atomica_e_preserva_replays_e_guarda_de_plano()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        await EntrarEPreparar(b, fabrica);
        var (id, etag, saida) = await Contratar(a, dados);
        var rota = $"{Rota}/{id}";
        var inicial = await Ler(await a.GetAsync(rota), HttpStatusCode.OK);
        await Ler(await Ocultar(a, etag), HttpStatusCode.Conflict);
        await Ler(await Ocultar(a, null), HttpStatusCode.PreconditionRequired);
        await Ler(await Ocultar(b, etag), HttpStatusCode.NotFound);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var comando = await PrepararMedicoes(db, dados.Empresa, saida);
        var previa = await ObterPrevia(a, id, comando);
        var resposta = await EnviarFechamento(a, id, comando, previa, etag, "fechamento");
        var fechamento = await Ler(resposta, HttpStatusCode.OK);
        var concluido = resposta.Headers.ETag!.ToString();
        await Ler(await Enviar(a, HttpMethod.Put, rota, ComandoAtual(dados), "If-Match", concluido), HttpStatusCode.Conflict);
        await Ler(await a.PostAsync($"{rota}/reabrir", null), HttpStatusCode.NotFound);
        await Ler(await Ocultar(a, etag), HttpStatusCode.PreconditionFailed);

        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Auditorias ADD CONSTRAINT CK_TesteFalhaOcultar CHECK (Evento <> N'aluguel.ocultado');");
        await Ler(await Ocultar(a, concluido), HttpStatusCode.ServiceUnavailable);
        await Ler(await a.GetAsync(rota), HttpStatusCode.OK);
        Assert.AreEqual(0, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.ocultado"));
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Auditorias DROP CONSTRAINT CK_TesteFalhaOcultar;");

        await Ler(await Ocultar(a, concluido), HttpStatusCode.NoContent);
        await Ler(await Ocultar(a, concluido), HttpStatusCode.NotFound);
        await Ler(await a.GetAsync(rota), HttpStatusCode.NotFound);
        var historico = await Ler(await a.GetAsync($"{rota}?incluirOcultos=true"), HttpStatusCode.OK);
        Assert.AreEqual("Concluido", historico.GetProperty("estado").GetString());
        Assert.IsTrue(historico.GetProperty("oculto").GetBoolean());
        Assert.AreEqual(fechamento.GetProperty("final").GetRawText(), historico.GetProperty("final").GetRawText());
        var replay = await EnviarFechamento(a, id, comando, previa, "\"antigo\"", "fechamento");
        Assert.AreEqual(fechamento.GetRawText(), (await Ler(replay, HttpStatusCode.OK)).GetRawText());
        Assert.AreEqual(concluido, replay.Headers.ETag!.ToString());
        var criacao = await Enviar(a, HttpMethod.Post, Rota, ComandoAtual(dados) with
        {
            RetornoPrevisto = inicial.GetProperty("retornoPrevisto").GetString(),
        }, "Idempotency-Key", "contratar");
        Assert.AreEqual(inicial.GetRawText(), (await Ler(criacao, HttpStatusCode.Created)).GetRawText());
        Assert.AreEqual(etag, criacao.Headers.ETag!.ToString());
        var auditoria = await db.Auditorias.SingleAsync(x => x.Evento == "aluguel.ocultado");
        Assert.AreEqual(id, auditoria.EntidadeId);
        Assert.AreEqual(dados.Empresa, auditoria.EmpresaId);
        Assert.IsNotNull(auditoria.UsuarioId);
        Assert.AreEqual("oculto", auditoria.CamposAlterados);
        Assert.AreEqual(1, (await a.GetFromJsonAsync<JsonElement>("/api/v1/veiculos?disponiveis=true")).GetProperty("total").GetInt32());
        var plano = $"/api/v1/planos/{dados.Plano.Id}";
        await Ler(await Enviar(a, HttpMethod.Delete, plano, null, "If-Match", (await a.GetAsync(plano)).Headers.ETag!.ToString()),
            HttpStatusCode.Conflict);

        Task<HttpResponseMessage> Ocultar(HttpClient http, string? versao) =>
            Enviar(http, HttpMethod.Delete, rota, null, versao is null ? null : "If-Match", versao);
    }

    [TestMethod]
    public async Task Historico_filtra_ordena_e_isola_inclusive_ocultos_e_lendo_snapshot_sem_cadastro_atual()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var outros = await EntrarEPreparar(b, fabrica);
        var (id, etag, saida) = await Contratar(a, dados);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var entrada = await PrepararMedicoes(db, dados.Empresa, saida);
        var previa = await ObterPrevia(a, id, entrada);
        var fechado = await Ler(await EnviarFechamento(a, id, entrada, previa, etag, "final"), HttpStatusCode.OK);
        var outroVeiculo = Veiculo.Criar(dados.Grupo.Id, "ATESTEF4", "Outra", "Veículo", "Cinza", 2026,
            Combustivel.Gasolina, 50, dados.Empresa, saida);
        (await db.Veiculos.SingleAsync(x => x.Id == dados.Veiculo.Id)).ExcluidoEmUtc = saida.AddHours(28);
        db.Veiculos.Add(outroVeiculo);
        await db.SaveChangesAsync();
        var (idAberto, _, _) = await Contratar(a, dados with { Veiculo = outroVeiculo }, "outra-locacao");

        var lista = await Lista(a, "?pageSize=1");
        Assert.AreEqual(2, lista.GetProperty("total").GetInt32());
        Assert.AreEqual(idAberto, lista.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.IsTrue(lista.GetProperty("items")[0].TryGetProperty("parcelasConhecidas", out _));
        var segunda = (await Lista(a, "?pageSize=1&page=2")).GetProperty("items")[0];
        Assert.AreEqual(id, segunda.GetProperty("id").GetGuid());
        Assert.AreEqual(fechado.GetProperty("final").GetProperty("total").GetString(), segunda.GetProperty("totalFinal").GetString());
        Assert.IsFalse(segunda.TryGetProperty("parcelasConhecidas", out _));
        var dia = DateOnly.FromDateTime(saida.DateTime);
        Assert.AreEqual(1, await Total(a, $"?estado=Concluido&veiculoId={dados.Veiculo.Id}&saidaDe={dia:yyyy-MM-dd}&saidaAte={dia:yyyy-MM-dd}"));
        Assert.AreEqual(0, await Total(a, $"?estado=EmAberto&veiculoId={dados.Veiculo.Id}"));
        Assert.AreEqual(0, await Total(a, $"?veiculoId={outros.Veiculo.Id}&incluirOcultos=true"));
        Assert.AreEqual(0, await Total(a, $"?saidaAte={dia.AddDays(-1):yyyy-MM-dd}"));
        await Ler(await a.GetAsync($"{Rota}?estado=1"), HttpStatusCode.BadRequest);
        await Ler(await a.GetAsync($"/api/v1/veiculos/{dados.Veiculo.Id}"), HttpStatusCode.NotFound);
        var detalhe = await Ler(await a.GetAsync($"{Rota}/{id}"), HttpStatusCode.OK);
        Assert.AreEqual(dados.Veiculo.Placa, detalhe.GetProperty("veiculo").GetProperty("placa").GetString());
        Assert.IsFalse(detalhe.TryGetProperty("estimativa", out _));

        var aluguel = await db.Alugueis.SingleAsync(x => x.Id == id);
        aluguel.ExcluidoEmUtc = DateTimeOffset.UtcNow; // Prepara o recorte de leitura; a rota de ocultação é verificada à parte.
        await db.SaveChangesAsync();
        Assert.AreEqual(0, await Total(a, "?estado=Concluido"));
        Assert.AreEqual(1, await Total(a, "?estado=Concluido&incluirOcultos=true"));
        var oculto = (await Lista(a, "?estado=Concluido&incluirOcultos=true")).GetProperty("items")[0];
        Assert.IsTrue(oculto.GetProperty("oculto").GetBoolean());
        await Ler(await a.GetAsync($"{Rota}/{id}"), HttpStatusCode.NotFound);
        var historico = await Ler(await a.GetAsync($"{Rota}/{id}?incluirOcultos=true"), HttpStatusCode.OK);
        Assert.IsTrue(historico.GetProperty("oculto").GetBoolean());
        Assert.AreEqual(fechado.GetProperty("final").GetRawText(), historico.GetProperty("final").GetRawText());
        Assert.AreEqual(0, await Total(b, "?incluirOcultos=true"));
        await Ler(await b.GetAsync($"{Rota}/{id}?incluirOcultos=true"), HttpStatusCode.NotFound);
        await Ler(await b.GetAsync($"{Rota}/{idAberto}"), HttpStatusCode.NotFound);
    }

    private static Task<JsonElement> Lista(HttpClient http, string query) => http.GetFromJsonAsync<JsonElement>(Rota + query);
    private static async Task<int> Total(HttpClient http, string query) => (await Lista(http, query)).GetProperty("total").GetInt32();
}
