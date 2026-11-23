using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.PersistenciaLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class ContratosLocacoesTests
{
    private const string Rota = "/api/v1/alugueis";
    private const string Chave = "Idempotency-Key";

    [TestMethod]
    public async Task Contratos_http_paginacao_disponibilidade_precondicao_e_tenant()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        await Status(await a.GetAsync(Rota), HttpStatusCode.Unauthorized);
        var dadosA = await EntrarEPreparar(a, fabrica);
        var dadosB = await EntrarEPreparar(b, fabrica);
        var comando = ComandoAtual(dadosA);
        await Status(await a.PostAsJsonAsync(Rota, comando), HttpStatusCode.BadRequest);
        await Status(await a.PostAsJsonAsync($"{Rota}/estimativas", new { comando.ClienteId, saida = "não permitida" }),
            HttpStatusCode.BadRequest);
        var previa = await Json(await a.PostAsJsonAsync($"{Rota}/estimativas", comando), HttpStatusCode.OK);
        Assert.IsTrue(previa.GetProperty("estimativa").GetProperty("quilometragemPendente").GetBoolean());
        var criado = await Enviar(a, HttpMethod.Post, Rota, comando, Chave, "contratacao-A");
        var objeto = await Json(criado, HttpStatusCode.Created);
        var id = objeto.GetProperty("id").GetGuid();
        Assert.AreEqual("EmAberto", objeto.GetProperty("estado").GetString());
        Assert.IsNotNull(criado.Headers.ETag);
        var detalhe = Rota + "/" + id;
        StringAssert.EndsWith(criado.Headers.Location!.ToString(), detalhe);
        await Status(await a.GetAsync(detalhe), HttpStatusCode.OK);
        await Status(await b.GetAsync(detalhe), HttpStatusCode.NotFound);
        await Status(await Enviar(b, HttpMethod.Put, detalhe, ComandoAtual(dadosB), "If-Match", criado.Headers.ETag!.ToString()),
            HttpStatusCode.NotFound);
        await Status(await Enviar(b, HttpMethod.Post, Rota, ComandoAtual(dadosB) with { VeiculoId = dadosA.Veiculo.Id },
            Chave, "vinculo-externo"), HttpStatusCode.NotFound);
        await Status(await Enviar(a, HttpMethod.Post, Rota, comando, Chave, "outra-chave"), HttpStatusCode.Conflict);
        await Status(await Enviar(a, HttpMethod.Put, detalhe, comando, null, null), HttpStatusCode.PreconditionRequired);
        await Status(await Enviar(a, HttpMethod.Put, detalhe, comando, "If-Match", "\"incorreto\""), HttpStatusCode.PreconditionFailed);
        var editado = await Enviar(a, HttpMethod.Put, detalhe, comando with { HodometroInicial = 600 }, "If-Match", criado.Headers.ETag.ToString());
        Assert.AreNotEqual(criado.Headers.ETag, editado.Headers.ETag);
        var nova = await Json(editado, HttpStatusCode.OK);
        Assert.AreEqual(600, nova.GetProperty("hodometroInicial").GetInt32());
        Assert.AreEqual(objeto.GetProperty("saida").GetString(), nova.GetProperty("saida").GetString());

        var dia = DateOnly.FromDateTime(objeto.GetProperty("saida").GetDateTimeOffset().DateTime);
        var lista = await a.GetFromJsonAsync<JsonElement>(
            $"{Rota}?veiculoId={dadosA.Veiculo.Id}&saidaDe={dia:yyyy-MM-dd}&saidaAte={dia:yyyy-MM-dd}&pageSize=1");
        Assert.AreEqual(1, lista.GetProperty("total").GetInt32());
        var resumo = lista.GetProperty("items")[0];
        Assert.AreEqual("Condutor Exemplo", resumo.GetProperty("condutor").GetString());
        Assert.AreEqual(id, resumo.GetProperty("id").GetGuid());
        Assert.IsTrue(resumo.GetProperty("parcelasConhecidas").GetString()!.Contains('.', StringComparison.Ordinal));
        Assert.IsFalse(resumo.TryGetProperty("totalFinal", out _));
        Assert.AreEqual(0, await Total(a, $"{Rota}?saidaAte={dia.AddDays(-1):yyyy-MM-dd}"));
        Assert.AreEqual(0, await Total(a, $"{Rota}?veiculoId={dadosB.Veiculo.Id}"));
        Assert.AreEqual(0, await Total(b, Rota));
        Assert.AreEqual(0, await Total(a, "/api/v1/veiculos?disponiveis=true"));
        Assert.AreEqual(1, await Total(a, $"/api/v1/veiculos?disponiveis=true&aluguelAtualId={id}"));
        Assert.AreEqual(1, await Total(a, $"/api/v1/condutores?clienteId={dadosA.Cliente.Id}"));
        Assert.AreEqual(0, await Total(a, $"/api/v1/condutores?clienteId={dadosB.Cliente.Id}"));
        await Status(await a.PostAsync("/api/v1/session/logout", null), HttpStatusCode.NoContent);
        await Status(await a.GetAsync(detalhe), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task CORS_OpenAPI_e_falha_de_dependencia()
    {
        string[] origens = ["http://localhost:4201", "http://127.0.0.1:4201"];
        using var fabrica = new SqlServerWebApplicationFactory(origens: origens);
        using var http = fabrica.CreateClient();
        var dados = await EntrarEPreparar(http, fabrica);
        var comando = ComandoAtual(dados);
        var doc = await http.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var rotas = doc.GetProperty("paths");
        Assert.HasCount(3, rotas.EnumerateObject().Where(x => x.Name.StartsWith(Rota, StringComparison.Ordinal)).ToArray());
        var post = rotas.GetProperty(Rota).GetProperty("post");
        Assert.IsTrue(post.GetProperty("parameters").EnumerateArray().Any(x => x.GetProperty("name").GetString() == Chave
            && x.GetProperty("required").GetBoolean()));
        var headers = post.GetProperty("responses").GetProperty("201").GetProperty("headers");
        Assert.IsTrue(headers.TryGetProperty("ETag", out _) && headers.TryGetProperty("Location", out _));
        Assert.IsFalse(rotas.GetProperty($"{Rota}/estimativas").GetProperty("post").GetProperty("responses")
            .GetProperty("200").TryGetProperty("headers", out _));
        foreach (var origem in origens)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Options, Rota);
            requisicao.Headers.TryAddWithoutValidation("Origin", origem);
            requisicao.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
            requisicao.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "Authorization, Content-Type, Idempotency-Key, If-Match");
            var resposta = await http.SendAsync(requisicao);
            Assert.AreEqual(origem, resposta.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
            http.DefaultRequestHeaders.Remove("Origin");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", origem);
            var criado = await Enviar(http, HttpMethod.Post, Rota, comando, Chave, "idempotente-cors");
            await Status(criado, HttpStatusCode.Created);
            Assert.IsTrue(criado.Headers.GetValues("Access-Control-Expose-Headers").Any(x => x.Contains("ETag", StringComparison.OrdinalIgnoreCase)));
        }
        using var indisponivel = new SqlServerWebApplicationFactory(indisponivel: true);
        using var cliente = indisponivel.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = http.DefaultRequestHeaders.Authorization;
        var erro = await Json(await cliente.GetAsync(Rota), HttpStatusCode.ServiceUnavailable);
        Assert.AreEqual("dependencia_indisponivel", erro.GetProperty("code").GetString());
        Assert.IsFalse(erro.TryGetProperty("detail", out _));
    }

    [TestMethod]
    public async Task Guardas_dos_cadastros_preservam_contrato_e_respeitam_edicoes_permitidas()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var dados = await EntrarEPreparar(http, fabrica);
        using var escopo = fabrica.Services.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(dados.Empresa);
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var pf = Cliente.Criar(TipoCliente.PF, "Condutor Exemplo", dados.Condutor.Cpf, "RG-EXEMPLO", dados.Condutor.Cnh,
            null, "Tel", null, "SP", "Cidade", "Bairro", "Rua", "1", dados.Empresa, dados.Agora);
        db.Clientes.Add(pf);
        await db.SaveChangesAsync();
        var criado = await Enviar(http, HttpMethod.Post, Rota, ComandoAtual(dados), Chave, "protecao-cadastros");
        var id = (await Json(criado, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        string veiculo = $"/api/v1/veiculos/{dados.Veiculo.Id}", cliente = $"/api/v1/clientes/{dados.Cliente.Id}",
            condutor = $"/api/v1/condutores/{dados.Condutor.Id}", taxa = $"/api/v1/taxas-servicos/{dados.Taxa.Id}",
            plano = $"/api/v1/planos/{dados.Plano.Id}";
        await Status(await http.GetAsync(veiculo + "?paraEdicao=true"), HttpStatusCode.Conflict);
        await Status(await http.GetAsync(taxa + "?paraEdicao=true"), HttpStatusCode.Conflict);
        foreach (string rota in new[] { veiculo, cliente, condutor, taxa, plano })
        {
            var versao = (await http.GetAsync(rota)).Headers.ETag!.ToString();
            var erro = await Json(await Enviar(http, HttpMethod.Delete, rota, null, "If-Match", versao), HttpStatusCode.Conflict);
            StringAssert.Contains(erro.GetProperty("detail").GetString(), "aluguel");
        }
        await Alterar(veiculo, new { grupoId = dados.Grupo.Id, placa = "MUDAR" }, HttpStatusCode.BadRequest);
        await Alterar(veiculo, new { grupoId = dados.Grupo.Id, placa = "MUDAR", marca = "Aurora", modelo = "Lume", cor = "Prata",
            ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50" }, HttpStatusCode.Conflict);
        await Alterar(taxa, new { nome = "Mudança", preco = "100", modalidade = "Fixa" }, HttpStatusCode.Conflict);
        var y = dados.Condutor;
        await Alterar(condutor, new { y.ClienteId, y.Nome, y.Email, y.Cpf, cnh = "NOVA", y.ValidadeCnh, y.Telefone }, HttpStatusCode.Conflict);
        await Alterar(condutor, new { y.ClienteId, y.Nome, y.Email, cpf = "11144477735", y.Cnh, y.ValidadeCnh, y.Telefone }, HttpStatusCode.Conflict);
        await Alterar(condutor, new { y.ClienteId, y.Nome, y.Email, y.Cpf, y.Cnh, validadeCnh = y.ValidadeCnh.AddDays(1), y.Telefone }, HttpStatusCode.Conflict);
        await Alterar(condutor, new { clienteId = pf.Id, y.Nome, y.Email, y.Cpf, y.Cnh, y.ValidadeCnh, y.Telefone }, HttpStatusCode.Conflict);
        await Alterar($"/api/v1/clientes/{pf.Id}", new { tipo = "PF", pf.Nome, pf.Cpf, pf.Rg, cnh = "NOVA", pf.Telefone, pf.Endereco }, HttpStatusCode.Conflict);
        await Alterar(condutor, new { y.ClienteId, nome = "Outro nome", y.Email, y.Cpf, y.Cnh, y.ValidadeCnh, y.Telefone }, HttpStatusCode.OK);
        await Alterar(plano, new { precoDiario = "500", precoKm = "6" }, HttpStatusCode.OK);
        var preservado = await http.GetFromJsonAsync<JsonElement>($"{Rota}/{id}");
        Assert.AreEqual("Condutor Exemplo", preservado.GetProperty("condutor").GetProperty("nome").GetString());
        Assert.AreEqual("100", preservado.GetProperty("estimativa").GetProperty("plano").GetProperty("precoDiario").GetString());

        async Task Alterar(string rota, object corpo, HttpStatusCode status)
        {
            var versao = (await http.GetAsync(rota)).Headers.ETag!.ToString();
            await Status(await Enviar(http, HttpMethod.Put, rota, corpo, "If-Match", versao), status);
        }
    }

    [TestMethod]
    [DataRow("veiculo")]
    [DataRow("condutor")]
    [DataRow("taxa")]
    public async Task Corrida_entre_criar_e_excluir_cadastro_nao_deixa_vinculo_invalido(string tipo)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var contratacao = fabrica.CreateClient();
        using var cadastro = fabrica.CreateClient();
        var dados = await EntrarEPreparar(contratacao, fabrica);
        cadastro.DefaultRequestHeaders.Authorization = contratacao.DefaultRequestHeaders.Authorization;
        var rota = tipo switch
        {
            "veiculo" => $"/api/v1/veiculos/{dados.Veiculo.Id}",
            "condutor" => $"/api/v1/condutores/{dados.Condutor.Id}",
            _ => $"/api/v1/taxas-servicos/{dados.Taxa.Id}",
        };
        var etag = (await cadastro.GetAsync(rota)).Headers.ETag!.ToString();
        var resultado = await Task.WhenAll(
            Enviar(contratacao, HttpMethod.Post, Rota, ComandoAtual(dados), Chave, "corrida-cadastro"),
            Enviar(cadastro, HttpMethod.Delete, rota, null, "If-Match", etag));
        if (resultado[0].StatusCode == HttpStatusCode.Created)
        {
            await Status(resultado[1], HttpStatusCode.Conflict);
            Assert.AreEqual(1, await Total(contratacao, Rota));
            await Status(await cadastro.GetAsync(rota), HttpStatusCode.OK);
        }
        else
        {
            await Status(resultado[0], HttpStatusCode.NotFound);
            await Status(resultado[1], HttpStatusCode.NoContent);
            Assert.AreEqual(0, await Total(contratacao, Rota));
        }
    }

    [TestMethod]
    public async Task Corrida_entre_criar_e_vencer_CNH_revalida_o_cadastro_dentro_do_lock()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var contratacao = fabrica.CreateClient();
        using var cadastro = fabrica.CreateClient();
        var dados = await EntrarEPreparar(contratacao, fabrica);
        cadastro.DefaultRequestHeaders.Authorization = contratacao.DefaultRequestHeaders.Authorization;
        var condutor = dados.Condutor;
        var rota = $"/api/v1/condutores/{condutor.Id}";
        var etag = (await cadastro.GetAsync(rota)).Headers.ETag!.ToString();
        var resultado = await Task.WhenAll(
            Enviar(contratacao, HttpMethod.Post, Rota, ComandoAtual(dados), Chave, "corrida-cnh"),
            Enviar(cadastro, HttpMethod.Put, rota, new
            {
                condutor.ClienteId, condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh, condutor.Telefone,
                validadeCnh = "2000-01-01",
            }, "If-Match", etag));
        if (resultado[0].StatusCode == HttpStatusCode.Created)
        {
            await Status(resultado[1], HttpStatusCode.Conflict);
            Assert.AreEqual(1, await Total(contratacao, Rota));
        }
        else
        {
            var erro = await Json(resultado[0], HttpStatusCode.BadRequest);
            Assert.IsTrue(erro.GetProperty("errors").TryGetProperty("condutorId", out _));
            await Status(resultado[1], HttpStatusCode.OK);
            Assert.AreEqual(0, await Total(contratacao, Rota));
        }
    }

    internal static async Task<DadosLocacao> EntrarEPreparar(HttpClient http, SqlServerWebApplicationFactory fabrica)
    {
        var nome = $"locacoes-{Guid.NewGuid():N}";
        var corpo = new { username = nome, email = $"{nome}@example.invalid", password = "senha-sintetica" };
        await Status(await http.PostAsJsonAsync("/api/v1/registro", corpo), HttpStatusCode.Created);
        var sessao = await Json(await http.PostAsJsonAsync("/api/v1/session/login", corpo), HttpStatusCode.OK);
        var token = sessao.GetProperty("accessToken").GetString()!;
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var empresa = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(x => x.Type == "empresaId").Value);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await CriarCadastros(escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>(),
            escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>(), empresa);
    }

    internal static ComandoAluguel ComandoAtual(DadosLocacao dados) => dados.Comando() with
    {
        RetornoPrevisto = HorarioLocacao.ParaSaoPaulo(DateTimeOffset.UtcNow.AddDays(2)).ToString("O", CultureInfo.InvariantCulture),
    };

    internal static async Task<HttpResponseMessage> Enviar(HttpClient http, HttpMethod metodo, string rota,
        object? corpo, string? cabecalho, string? valor)
    {
        using var pedido = new HttpRequestMessage(metodo, rota);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
        if (cabecalho is not null) pedido.Headers.TryAddWithoutValidation(cabecalho, valor);
        return await http.SendAsync(pedido);
    }

    private static async Task<int> Total(HttpClient http, string rota) =>
        (await http.GetFromJsonAsync<JsonElement>(rota)).GetProperty("total").GetInt32();

    private static async Task<JsonElement> Json(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        await Status(resposta, esperado);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task Status(HttpResponseMessage resposta, HttpStatusCode esperado) =>
        Assert.AreEqual(esperado, resposta.StatusCode, $"Método/rota: {resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri}: " +
            (resposta.StatusCode == esperado ? "" : await resposta.Content.ReadAsStringAsync()));
}
