using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class ContratosF2Tests
{
    [TestMethod]
    public async Task Vinculos_concorrentes_exigem_versao_da_raiz_e_condutor_preserva_cliente_excluido()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var pf = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa", cpf = "52998224725", rg = "RG", cnh = "CNH",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
            empresaId = Guid.NewGuid(),
        });
        Assert.AreEqual(HttpStatusCode.Created, pf.StatusCode);
        var pfId = (await pf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var pj = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Empresa", cnpj = "11222333000181", telefone = "Tel",
            endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        });
        var pjId = (await pj.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var outroHttp = fabrica.CreateClient();
        outroHttp.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Task<HttpResponseMessage> Vincular(HttpClient cliente)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/clientes/{pfId}/vinculos-pj")
            { Content = JsonContent.Create(new { clientePjId = pjId }) };
            pedido.Headers.IfMatch.Add(pf.Headers.ETag!);
            return cliente.SendAsync(pedido);
        }
        var resultados = await Task.WhenAll(Vincular(http), Vincular(outroHttp));
        Assert.AreEqual(1, resultados.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, resultados.Count(x => x.StatusCode == HttpStatusCode.PreconditionFailed));
        var condutor = await http.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Condutor", email = "condutor@example.invalid",
            cpf = "52998224725", cnh = "CNH", validadeCnh = "2020-01-01", telefone = "Tel",
        });
        var condutorId = (await condutor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var excluir = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/clientes/{pfId}");
        excluir.Headers.IfMatch.Add(resultados.Single(x => x.IsSuccessStatusCode).Headers.ETag!);
        Assert.AreEqual(HttpStatusCode.NoContent, (await http.SendAsync(excluir)).StatusCode);
        using var editar = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/condutores/{condutorId}")
        { Content = JsonContent.Create(new
            {
                clienteId = pfId, nome = "Condutor atualizado", email = "condutor@example.invalid",
                cpf = "52998224725", cnh = "CNH", validadeCnh = "2020-01-01", telefone = "Tel",
            }) };
        editar.Headers.IfMatch.Add(condutor.Headers.ETag!);
        Assert.AreEqual(HttpStatusCode.OK, (await http.SendAsync(editar)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Outro", email = "outro@example.invalid",
            cpf = "11144477735", cnh = "CNH", validadeCnh = "2030-01-01", telefone = "Tel",
        })).StatusCode);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var registroPf = await banco.Clientes.IgnoreQueryFilters().SingleAsync(x => x.Id == pfId);
        Assert.AreEqual(Empresa(tokens), registroPf.EmpresaId);
        Assert.AreEqual(Empresa(tokens), (await banco.Auditorias.SingleAsync(x =>
            x.Evento == "cliente.criado" && x.EntidadeId == pfId)).EmpresaId);
        using var externa = fabrica.CreateClient();
        await externa.PostAsJsonAsync("/api/v1/registro",
            new { username = "empresaExterna", email = "externa@example.invalid", password = "12345678" });
        var login = await externa.PostAsJsonAsync("/api/v1/session/login",
            new { username = "empresaExterna", password = "12345678" });
        externa.DefaultRequestHeaders.Authorization = new("Bearer",
            (await login.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!.AccessToken);
        Assert.AreEqual(HttpStatusCode.NotFound,
            (await externa.GetAsync($"/api/v1/condutores/{condutorId}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound,
            (await externa.GetAsync($"/api/v1/clientes/{pjId}")).StatusCode);
    }

    [TestMethod]
    public async Task Dois_seguros_independentes_e_exclusao_de_taxa_preservam_registros()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        foreach (var (tipo, preco) in new[] { ("Cliente", "0"), ("Terceiros", "25.5") })
        {
            var leitura = await http.GetAsync($"/api/v1/configuracoes/seguros/{tipo}");
            using var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/configuracoes/seguros/{tipo}")
            { Content = JsonContent.Create(new { precoDiario = preco }) };
            pedido.Headers.IfMatch.Add(leitura.Headers.ETag!);
            Assert.AreEqual(HttpStatusCode.OK, (await http.SendAsync(pedido)).StatusCode);
        }
        Assert.AreEqual("0", (await http.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/seguros/Cliente"))
            .GetProperty("precoDiario").GetString());
        Assert.AreEqual("25.5", (await http.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/seguros/Terceiros"))
            .GetProperty("precoDiario").GetString());
        var taxa = await http.PostAsJsonAsync("/api/v1/taxas-servicos",
            new { nome = "Taxa sintética", preco = "10", modalidade = "Fixa" });
        var id = (await taxa.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var excluir = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/taxas-servicos/{id}");
        excluir.Headers.IfMatch.Add(taxa.Headers.ETag!);
        Assert.AreEqual(HttpStatusCode.NoContent, (await http.SendAsync(excluir)).StatusCode);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var registro = await db.TaxasServico.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        Assert.IsNotNull(registro.ExcluidoEmUtc);
        Assert.AreEqual(10m, registro.Preco);
        Assert.AreEqual(Empresa(tokens), registro.EmpresaId);
    }

    [TestMethod]
    [DataRow("taxas-servicos")]
    [DataRow("clientes")]
    [DataRow("veiculos")]
    [DataRow("planos")]
    public async Task Enum_obrigatorio_ausente_nao_assume_primeiro_valor(string rota)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var resposta = await http.PostAsJsonAsync($"/api/v1/{rota}", new { nome = "Sem enum", preco = "0" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(erro.GetProperty("errors").EnumerateObject().Any(x =>
            x.Name is "modalidade" or "tipo" or "combustivel"));
    }

    [TestMethod]
    public async Task Planos_concorrentes_e_edicao_de_veiculo_preservam_estado_e_isolamento()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var grupoResposta = await http.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo de concorrência" });
        var grupo = (await grupoResposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var corpoPlano = new { grupoId = grupo, modalidade = "Livre", precoDiario = "100" };
        using var concorrente = fabrica.CreateClient();
        concorrente.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var resultados = await Task.WhenAll(http.PostAsJsonAsync("/api/v1/planos", corpoPlano),
            concorrente.PostAsJsonAsync("/api/v1/planos", corpoPlano));
        Assert.AreEqual(1, resultados.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(1, resultados.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        var criado = resultados.Single(x => x.IsSuccessStatusCode);
        var plano = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var edicao = await Enviar(http, $"/api/v1/planos/{plano}", criado, new { precoDiario = "125.5" });
        Assert.AreEqual(HttpStatusCode.OK, edicao.StatusCode);
        var estadoPlano = await edicao.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("125.5", estadoPlano.GetProperty("precoDiario").GetString());
        Assert.AreEqual("Livre", estadoPlano.GetProperty("modalidade").GetString());
        Assert.AreEqual(grupo, estadoPlano.GetProperty("grupoId").GetGuid());
        Assert.AreEqual(HttpStatusCode.PreconditionFailed,
            (await Enviar(http, $"/api/v1/planos/{plano}", criado, new { precoDiario = "999" })).StatusCode);
        var veiculoCriado = await http.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupo, placa = "TESTE-1", marca = "Marca", modelo = "Modelo", cor = "Azul",
            ano = 2020, combustivel = "Gasolina", capacidadeLitros = "50",
        });
        Assert.AreEqual(HttpStatusCode.Created, veiculoCriado.StatusCode);
        var veiculo = (await veiculoCriado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var invalido = await Enviar(http, $"/api/v1/veiculos/{veiculo}", veiculoCriado, new
        {
            grupoId = grupo, placa = "TESTE-2", marca = "Outra", modelo = "Modelo", cor = "Verde",
            ano = 2020, combustivel = "Gasolina", capacidadeLitros = "0",
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, invalido.StatusCode);
        var aposFalha = await http.GetAsync($"/api/v1/veiculos/{veiculo}");
        Assert.AreEqual(veiculoCriado.Headers.ETag, aposFalha.Headers.ETag);
        Assert.AreEqual("TESTE1", (await aposFalha.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("placa").GetString());
        var valido = await Enviar(http, $"/api/v1/veiculos/{veiculo}", veiculoCriado, new
        {
            grupoId = grupo, placa = "TESTE-2", marca = "Outra", modelo = "Modelo", cor = "Verde",
            ano = 2021, combustivel = "Diesel", capacidadeLitros = "55.5",
        });
        Assert.AreEqual(HttpStatusCode.OK, valido.StatusCode);
        Assert.AreNotEqual(veiculoCriado.Headers.ETag, valido.Headers.ETag);
        // Empresa diferente recebe 404 no detalhe e não pode criar plano referenciando o grupo.
        using var outra = fabrica.CreateClient();
        var registroB = await outra.PostAsJsonAsync("/api/v1/registro",
            new { username = "outraEmpresa", email = "outra@example.invalid", password = "12345678" });
        Assert.AreEqual(HttpStatusCode.Created, registroB.StatusCode);
        var loginB = await outra.PostAsJsonAsync("/api/v1/session/login",
            new { username = "outraEmpresa", password = "12345678" });
        var tokensB = (await loginB.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
        outra.DefaultRequestHeaders.Authorization = new("Bearer", tokensB.AccessToken);
        Assert.AreEqual(HttpStatusCode.NotFound, (await outra.GetAsync($"/api/v1/planos/{plano}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest,
            (await outra.PostAsJsonAsync("/api/v1/planos", corpoPlano)).StatusCode);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(Empresa(tokensB));
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        db.Planos.Add(Plano.Criar(grupo, ModalidadePlano.Livre, 100, null, null, Empresa(tokensB), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        static Task<HttpResponseMessage> Enviar(HttpClient cliente, string rota, HttpResponseMessage anterior, object corpo)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Put, rota) { Content = JsonContent.Create(corpo) };
            pedido.Headers.IfMatch.Add(anterior.Headers.ETag!);
            return cliente.SendAsync(pedido);
        }
    }

    [TestMethod]
    public async Task Migration_f2_preserva_empresa_e_evento_f1_com_colunas_aditivas_nulas()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        try
        {
            var migrador = db.GetService<IMigrator>();
            await migrador.MigrateAsync("20261122193001_ProtegerEscritasTenant");
            var empresa = Guid.NewGuid();
            var agora = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Empresas (Id, CriadaEm) VALUES ({empresa}, {agora});
                INSERT INTO Auditorias (EmpresaId, UsuarioId, Evento, OcorridoEm)
                    VALUES ({empresa}, NULL, N'Registro', {agora});
                """);
            await migrador.MigrateAsync();
            var registro = await db.Auditorias.AsNoTracking().SingleAsync(x => x.EmpresaId == empresa);
            Assert.AreEqual("Registro", registro.Evento);
            Assert.AreEqual(empresa, registro.EmpresaId);
            Assert.AreEqual(agora, registro.OcorridoEm);
            Assert.IsNull(registro.EntidadeTipo);
            Assert.IsNull(registro.EntidadeId);
            Assert.IsNull(registro.CorrelacaoId);
            Assert.IsNull(registro.CamposAlterados);
            Assert.IsNotEmpty((await db.Empresas.AsNoTracking().SingleAsync()).Versao);
            Assert.AreEqual(0, await db.Grupos.IgnoreQueryFilters().CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [TestMethod]
    public async Task Preflight_e_etag_em_duas_origens_autorizadas_sem_credenciais()
    {
        string[] origens = ["http://localhost:4201", "http://127.0.0.1:4201"];
        using var fabrica = new SqlServerWebApplicationFactory(origens: origens);
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        foreach (var origem in origens.Append("http://nao-autorizada.example.invalid"))
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/grupos");
            preflight.Headers.Add("Origin", origem);
            preflight.Headers.Add("Access-Control-Request-Method", "PUT");
            preflight.Headers.Add("Access-Control-Request-Headers", "Authorization,Content-Type,If-Match");
            var resposta = await http.SendAsync(preflight);
            var permitida = origens.Contains(origem);
            Assert.AreEqual(permitida, resposta.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
            if (!permitida) continue;
            http.DefaultRequestHeaders.Remove("Origin");
            http.DefaultRequestHeaders.Add("Origin", origem);
            var criado = await http.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo CORS" });
            Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
            Assert.AreEqual(origem, criado.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.IsTrue(criado.Headers.GetValues("Access-Control-Expose-Headers")
                .Any(x => x.Contains("ETag", StringComparison.OrdinalIgnoreCase)));
            Assert.IsNotNull(criado.Headers.ETag);
        }
    }

    [TestMethod]
    public async Task Openapi_documenta_schemas_decimais_paginacao_etag_e_erros_sem_fotos()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var resposta = await cliente.GetAsync("/openapi/v1.json");
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        var documento = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var caminhos = documento.GetProperty("paths");
        Assert.IsFalse(caminhos.EnumerateObject().Any(x => x.Name.Contains("foto")));
        var detalhe = caminhos.GetProperty("/api/v1/veiculos/{id}").GetProperty("get");
        Assert.IsTrue(detalhe.GetProperty("responses").GetProperty("200").GetProperty("headers")
            .TryGetProperty("ETag", out _));
        var editar = caminhos.GetProperty("/api/v1/veiculos/{id}").GetProperty("put");
        var ifMatch = editar.GetProperty("parameters").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "If-Match");
        Assert.IsTrue(ifMatch.GetProperty("required").GetBoolean());
        foreach (var codigo in new[] { "400", "404", "409", "412", "428", "503" })
            Assert.IsTrue(editar.GetProperty("responses").TryGetProperty(codigo, out _));
        var schemas = documento.GetProperty("components").GetProperty("schemas");
        Assert.IsTrue(schemas.EnumerateObject().Any(x =>
            x.Value.TryGetProperty("properties", out var props) &&
            props.TryGetProperty("preco", out var preco) &&
            preco.TryGetProperty("pattern", out var pattern) &&
            pattern.GetString()!.Contains("{1,6}")));
        foreach (var rota in new[] { "grupos", "planos", "veiculos", "clientes", "condutores", "taxas-servicos" })
        {
            var post = caminhos.GetProperty($"/api/v1/{rota}").GetProperty("post");
            var criada = post.GetProperty("responses").GetProperty("201");
            Assert.IsTrue(criada.GetProperty("headers").TryGetProperty("Location", out _));
            Assert.IsTrue(criada.GetProperty("content").TryGetProperty("application/json", out _));
        }
    }

    [TestMethod]
    public async Task Combustivel_mesmo_instante_serializa_corrida_e_preserva_versoes()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        var empresa = Empresa(tokens);
        var instante = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        async Task<RespostaComVersao<CombustivelEstado>> Gravar(string? etag, string preco)
        {
            await using var escopo = fabrica.Services.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
            var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
            contexto.Definir(empresa);
            var relogio = new RelogioFixo(instante);
            var escrita = new ProtocoloEscrita(db, contexto, relogio);
            return await new CombustivelService(db, escrita, relogio)
                .ConfigurarAsync(Combustivel.Gasolina, new(preco), etag, default);
        }
        await using var leitura = fabrica.Services.CreateAsyncScope();
        leitura.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
        var inicial = await leitura.ServiceProvider.GetRequiredService<ICombustivelService>()
            .ObterAsync(Combustivel.Gasolina, default);
        var primeira = await Gravar(inicial.Etag, "5");
        var corridas = new[] { Gravar(primeira.Etag, "6"), Gravar(primeira.Etag, "7") };
        try { await Task.WhenAll(corridas); } catch (PrecondicaoException) { }
        Assert.AreEqual(1, corridas.Count(x => x.IsCompletedSuccessfully));
        Assert.AreEqual(1, corridas.Count(x => x.Exception?.InnerException is PrecondicaoException));
        var historico = await leitura.ServiceProvider.GetRequiredService<ICombustivelService>()
            .HistoricoAsync(Combustivel.Gasolina, 1, 20, default);
        Assert.AreEqual(2, historico.Total);
        Assert.AreEqual(2L, historico.Items[0].Ordem);
        Assert.AreEqual(1L, historico.Items[1].Ordem);
        Assert.IsTrue(historico.Items.All(x => x.InicioVigenciaUtc == instante));
        Assert.AreEqual("5", historico.Items[1].Preco);
        Assert.AreNotEqual(historico.Items[0].Id, historico.Items[1].Id);
    }

    [TestMethod]
    public async Task Falha_na_auditoria_da_cnh_reverte_cliente_condutor_e_versoes()
    {
        using var fabrica = new SqlServerWebApplicationFactory(falharAuditoriaEvento: "condutor.cnh-sincronizada");
        using var http = fabrica.CreateClient();
        var tokens = await Registrar(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var criado = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa sintética", cpf = "52998224725", rg = "RG", cnh = "CNH-1",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        });
        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var condutor = await http.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = id, nome = "Condutor", email = "condutor@example.invalid", cpf = "52998224725",
            cnh = "CNH-1", validadeCnh = "2030-01-01", telefone = "Tel",
        });
        Assert.AreEqual(HttpStatusCode.Created, condutor.StatusCode);
        var condutorId = (await condutor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/clientes/{id}")
        {
            Content = JsonContent.Create(new
            {
                tipo = "PF", nome = "Pessoa sintética", cpf = "52998224725", rg = "RG", cnh = "CNH-2",
                telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
            }),
        };
        pedido.Headers.IfMatch.Add(criado.Headers.ETag!);
        Assert.AreEqual(HttpStatusCode.InternalServerError, (await http.SendAsync(pedido)).StatusCode);
        var pfAtual = await http.GetAsync($"/api/v1/clientes/{id}");
        var condutorAtual = await http.GetAsync($"/api/v1/condutores/{condutorId}");
        Assert.AreEqual(criado.Headers.ETag, pfAtual.Headers.ETag);
        Assert.AreEqual(condutor.Headers.ETag, condutorAtual.Headers.ETag);
        Assert.AreEqual("CNH-1", (await pfAtual.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cnh").GetString());
        Assert.AreEqual("CNH-1", (await condutorAtual.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cnh").GetString());
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.IsFalse(await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>()
            .Auditorias.AnyAsync(x => x.Evento == "cliente.editado" || x.Evento == "condutor.cnh-sincronizada"));
    }

    private static async Task<ResultadoAutenticacao> Registrar(HttpClient http)
    {
        var registro = await http.PostAsJsonAsync("/api/v1/registro",
            new { username = "administrador", email = "admin@example.invalid", password = "12345678" });
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        var login = await http.PostAsJsonAsync("/api/v1/session/login",
            new { username = "administrador", password = "12345678" });
        return (await login.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
    }

    private static Guid Empresa(ResultadoAutenticacao tokens) =>
        Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(tokens.AccessToken).Claims.Single(x => x.Type == "empresaId").Value);

    private sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora => agora;
    }
}
