using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class CadastrosTests
{
    private const string CpfA = "52998224725";
    private const string CpfB = "11144477735";
    private const string Cnpj = "11222333000181";

    [TestMethod]
    public async Task Grupos_e_planos_respeitam_etag_modalidade_identidade_e_auditoria()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await RegistrarELogar(cliente);
        Autorizar(cliente, tokens);
        var empresa = EmpresaDe(tokens);

        var criado = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "  Frota executiva  " });
        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        Assert.IsNotNull(criado.Headers.Location);
        var etag = EtagDe(criado);
        var grupo = await Json(criado);
        var grupoId = grupo.GetProperty("id").GetGuid();
        Assert.AreEqual("Frota executiva", grupo.GetProperty("nome").GetString());
        Assert.AreEqual(0, grupo.GetProperty("planos").GetArrayLength());
        Assert.IsTrue(criado.Headers.Location!.ToString().EndsWith($"/api/v1/grupos/{grupoId}", StringComparison.Ordinal));

        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos?page=1&pageSize=20");
        Assert.AreEqual(1, lista.GetProperty("total").GetInt32());
        Assert.AreEqual(1, lista.GetProperty("page").GetInt32());
        Assert.AreEqual(20, lista.GetProperty("pageSize").GetInt32());

        await Problema(await Enviar(cliente, HttpMethod.Put, $"/api/v1/grupos/{grupoId}", new { nome = "Outro" }, null),
            HttpStatusCode.PreconditionRequired);
        await Problema(await Enviar(cliente, HttpMethod.Put, $"/api/v1/grupos/{grupoId}", new { nome = "Outro" }, "invalido"),
            HttpStatusCode.PreconditionFailed);
        await Problema(await Enviar(cliente, HttpMethod.Put, $"/api/v1/grupos/{grupoId}", new { nome = "Outro" }, $"W/\"{etag}\""),
            HttpStatusCode.PreconditionFailed);

        var editado = await Enviar(cliente, HttpMethod.Put, $"/api/v1/grupos/{grupoId}", new { nome = "Frota urbana" }, etag);
        Assert.AreEqual(HttpStatusCode.OK, editado.StatusCode);
        var novoEtag = EtagDe(editado);
        Assert.AreNotEqual(etag, novoEtag);
        Assert.AreEqual("Frota urbana", (await Json(editado)).GetProperty("nome").GetString());

        await Problema(await cliente.GetAsync("/api/v1/grupos?page=0"), HttpStatusCode.BadRequest);
        var paginaInvalida = await Problema(await cliente.GetAsync("/api/v1/grupos?pageSize=101"), HttpStatusCode.BadRequest);
        Assert.IsTrue(paginaInvalida.GetProperty("errors").TryGetProperty("pageSize", out _));

        var plano = await cliente.PostAsJsonAsync("/api/v1/planos", new
        {
            grupoId, modalidade = "Diario", precoDiario = "100.5", precoKm = "1.25",
        });
        Assert.AreEqual(HttpStatusCode.Created, plano.StatusCode);
        var planoId = (await Json(plano)).GetProperty("id").GetGuid();
        var planoEtag = EtagDe(plano);

        var duplicado = await Problema(await cliente.PostAsJsonAsync("/api/v1/planos", new
        {
            grupoId, modalidade = "Diario", precoDiario = "200", precoKm = "2",
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(duplicado.GetProperty("errors").TryGetProperty("modalidade", out _));

        var semFranquia = await Problema(await cliente.PostAsJsonAsync("/api/v1/planos", new
        {
            grupoId, modalidade = "Controlado", precoDiario = "100", precoKm = "2",
        }), HttpStatusCode.BadRequest);
        Assert.IsTrue(semFranquia.GetProperty("errors").TryGetProperty("franquiaDiariaKm", out _));

        var filtrados = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/planos?grupoId={grupoId}");
        Assert.AreEqual(1, filtrados.GetProperty("total").GetInt32());
        Assert.AreEqual("100.5", filtrados.GetProperty("items")[0].GetProperty("precoDiario").GetString());

        await Problema(await Enviar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupoId}", null, novoEtag),
            HttpStatusCode.Conflict);
        Assert.AreEqual(HttpStatusCode.NoContent,
            (await Enviar(cliente, HttpMethod.Delete, $"/api/v1/planos/{planoId}", null, planoEtag)).StatusCode);
        Assert.AreEqual(0, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/planos")).GetProperty("total").GetInt32());

        var recriado = await cliente.PostAsJsonAsync("/api/v1/planos", new
        {
            grupoId, modalidade = "Diario", precoDiario = "150", precoKm = "1.5",
        });
        Assert.AreEqual(HttpStatusCode.Created, recriado.StatusCode);
        Assert.AreNotEqual(planoId, (await Json(recriado)).GetProperty("id").GetGuid());
        await Problema(await Enviar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupoId}", null, novoEtag),
            HttpStatusCode.Conflict);

        var grupoVazio = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "Sem vínculos" });
        var grupoVazioId = (await Json(grupoVazio)).GetProperty("id").GetGuid();
        Assert.AreEqual(HttpStatusCode.NoContent, (await Enviar(cliente, HttpMethod.Delete,
            $"/api/v1/grupos/{grupoVazioId}", null, EtagDe(grupoVazio))).StatusCode);
        await Problema(await cliente.GetAsync($"/api/v1/grupos/{grupoVazioId}"), HttpStatusCode.NotFound);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var eventos = await banco.Auditorias.Select(x => x.Evento).ToListAsync();
        foreach (var evento in new[] { "grupo.criado", "grupo.editado", "grupo.excluido", "plano.criado", "plano.excluido" })
            Assert.Contains(evento, eventos);
        Assert.IsTrue(await banco.Auditorias.Where(x => x.Evento.Contains(".")).AllAsync(x => x.EntidadeTipo != null && x.CorrelacaoId != null));
        Assert.IsTrue(await banco.Auditorias.AllAsync(x => x.CamposAlterados == null ||
            (!x.CamposAlterados.Contains(CpfA) && !x.CamposAlterados.Contains(Cnpj))));
    }

    [TestMethod]
    public async Task Veiculos_normalizam_placa_reutilizam_apos_exclusao_e_isolam_empresas()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokensA = await RegistrarELogar(cliente);
        Autorizar(cliente, tokensA);

        var grupoA = await CriarGrupo(cliente, "Grupo A");
        var grupoA2 = await CriarGrupo(cliente, "Grupo A2");
        var primeiro = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupoA, placa = "abc-1234", marca = "Marca", modelo = "Modelo",
            cor = "Azul", ano = 2020, combustivel = "Gasolina", capacidadeLitros = "50.5",
        });
        Assert.AreEqual(HttpStatusCode.Created, primeiro.StatusCode);
        var veiculo = await Json(primeiro);
        var veiculoId = veiculo.GetProperty("id").GetGuid();
        Assert.AreEqual("ABC1234", veiculo.GetProperty("placa").GetString());
        var etag = EtagDe(primeiro);

        var duplicado = await Problema(await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupoA2, placa = " ABC 1234 ", marca = "Marca", modelo = "Modelo",
            cor = "Azul", ano = 2020, combustivel = "Gasolina", capacidadeLitros = "50",
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(duplicado.GetProperty("errors").TryGetProperty("placa", out _));

        var segundo = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupoA2, placa = "XYZ9876", marca = "Marca", modelo = "Modelo",
            cor = "Prata", ano = 2021, combustivel = "Diesel", capacidadeLitros = "45",
        });
        Assert.AreEqual(HttpStatusCode.Created, segundo.StatusCode);

        var filtro = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/veiculos?grupoId={grupoA}");
        Assert.AreEqual(1, filtro.GetProperty("total").GetInt32());
        Assert.AreEqual("ABC1234", filtro.GetProperty("items")[0].GetProperty("placa").GetString());

        await Problema(await Enviar(cliente, HttpMethod.Put, $"/api/v1/veiculos/{veiculoId}", new
        {
            grupoId = grupoA, placa = "ABC1234", marca = "Marca", modelo = "Modelo",
            cor = "Azul", ano = 2020, combustivel = "Gasolina", capacidadeLitros = "50.5",
        }, null), HttpStatusCode.PreconditionRequired);

        Assert.AreEqual(HttpStatusCode.NoContent,
            (await Enviar(cliente, HttpMethod.Delete, $"/api/v1/veiculos/{veiculoId}", null, etag)).StatusCode);
        await Problema(await cliente.GetAsync($"/api/v1/veiculos/{veiculoId}"), HttpStatusCode.NotFound);

        var reutilizado = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupoA, placa = "ABC1234", marca = "Outra", modelo = "Outro",
            cor = "Verde", ano = 2024, combustivel = "Alcool", capacidadeLitros = "60",
        });
        Assert.AreEqual(HttpStatusCode.Created, reutilizado.StatusCode);
        Assert.AreNotEqual(veiculoId, (await Json(reutilizado)).GetProperty("id").GetGuid());

        var tokensB = await RegistrarELogar(cliente, "empresaB");
        Autorizar(cliente, tokensB);
        await Problema(await cliente.GetAsync($"/api/v1/veiculos/{(await Json(segundo)).GetProperty("id").GetGuid()}"),
            HttpStatusCode.NotFound);
        var referenciaCruzada = await Problema(await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = grupoA, placa = "BBB0001", marca = "Marca", modelo = "Modelo",
            cor = "Cor", ano = 2020, combustivel = "Gasolina", capacidadeLitros = "10",
        }), HttpStatusCode.BadRequest);
        Assert.IsTrue(referenciaCruzada.GetProperty("errors").TryGetProperty("grupoId", out _));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var empresaB = EmpresaDe(tokensB);
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresaB);
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        banco.Veiculos.Add(new Veiculo
        {
            EmpresaId = empresaB, GrupoId = grupoA, Placa = "AAA0000", Marca = "M", Modelo = "M",
            Cor = "C", Ano = 2020, Combustivel = Combustivel.Gasolina, CapacidadeLitros = 10,
            CriadoEm = DateTimeOffset.UtcNow, AtualizadoEm = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => banco.SaveChangesAsync());
    }

    [TestMethod]
    public async Task Clientes_pf_pj_rejeitam_documentos_duplicados_e_vinculos_invalidos()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await RegistrarELogar(cliente);
        Autorizar(cliente, tokens);
        var empresa = EmpresaDe(tokens);

        var pj = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Empresa Sintética", cnpj = "11.222.333/0001-81",
            telefone = "(00) 90000-0002", email = (string?)null,
            endereco = new { estado = "Estado", cidade = "Cidade", bairro = "Bairro", rua = "Rua", numero = "12 A" },
        });
        Assert.AreEqual(HttpStatusCode.Created, pj.StatusCode);
        var pjId = (await Json(pj)).GetProperty("id").GetGuid();

        var pf = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa Sintética", cpf = CpfA, rg = "RG-1", cnh = "CNH-1",
            telefone = "(00) 90000-0001", email = "pessoa@example.invalid",
            endereco = new { estado = "Estado", cidade = "Cidade", bairro = "Bairro", rua = "Rua", numero = "1" },
        });
        Assert.AreEqual(HttpStatusCode.Created, pf.StatusCode);
        var pfDetalhe = await Json(pf);
        var pfId = pfDetalhe.GetProperty("id").GetGuid();
        var pfEtag = EtagDe(pf);
        Assert.AreEqual("Estado", pfDetalhe.GetProperty("endereco").GetProperty("estado").GetString());
        Assert.AreEqual(0, pfDetalhe.GetProperty("vinculosPj").GetArrayLength());

        var cpfDuplicado = await Problema(await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Outra Pessoa", cpf = CpfA, rg = "RG", cnh = "CNH",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(cpfDuplicado.GetProperty("errors").TryGetProperty("cpf", out _));

        var cnpjDuplicado = await Problema(await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Outra Empresa", cnpj = Cnpj, telefone = "Tel",
            endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(cnpjDuplicado.GetProperty("errors").TryGetProperty("cnpj", out _));

        var cpfInvalido = await Problema(await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa", cpf = "12345678900", rg = "RG", cnh = "CNH",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        }), HttpStatusCode.BadRequest);
        Assert.IsTrue(cpfInvalido.GetProperty("errors").TryGetProperty("cpf", out _));

        var vinculo = await Enviar(cliente, HttpMethod.Post, $"/api/v1/clientes/{pfId}/vinculos-pj",
            new { clientePjId = pjId }, pfEtag);
        Assert.AreEqual(HttpStatusCode.OK, vinculo.StatusCode);
        var vinculoEtag = EtagDe(vinculo);
        var vinculos = await Json(vinculo);
        Assert.AreEqual(1, vinculos.GetArrayLength());
        Assert.AreEqual(pjId, vinculos[0].GetProperty("id").GetGuid());

        var duplicado = await Problema(await Enviar(cliente, HttpMethod.Post, $"/api/v1/clientes/{pfId}/vinculos-pj",
            new { clientePjId = pjId }, vinculoEtag), HttpStatusCode.Conflict);
        Assert.IsTrue(duplicado.GetProperty("errors").TryGetProperty("clientePjId", out _));

        var pjEtag = EtagDe(pj);
        var tipoIncompativel = await Problema(await Enviar(cliente, HttpMethod.Post, $"/api/v1/clientes/{pjId}/vinculos-pj",
            new { clientePjId = pfId }, pjEtag), HttpStatusCode.BadRequest);
        Assert.IsTrue(tipoIncompativel.GetProperty("errors").TryGetProperty("id", out _));

        var removido = await Enviar(cliente, HttpMethod.Delete,
            $"/api/v1/clientes/{pfId}/vinculos-pj/{pjId}", null, vinculoEtag);
        Assert.AreEqual(HttpStatusCode.OK, removido.StatusCode);
        Assert.AreEqual(0, (await Json(removido)).GetArrayLength());

        var condutor = await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Pessoa Sintética", email = "pessoa@example.invalid",
            cpf = CpfA, cnh = "CNH-1", validadeCnh = "2020-01-10", telefone = "(00) 90000-0001",
        });
        Assert.AreEqual(HttpStatusCode.Created, condutor.StatusCode);
        var condutorId = (await Json(condutor)).GetProperty("id").GetGuid();

        var excluido = await Enviar(cliente, HttpMethod.Delete, $"/api/v1/clientes/{pfId}", null, EtagDe(removido));
        Assert.AreEqual(HttpStatusCode.NoContent, excluido.StatusCode);
        await Problema(await cliente.GetAsync($"/api/v1/clientes/{pfId}"), HttpStatusCode.NotFound);

        var preservado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/condutores/{condutorId}");
        Assert.AreEqual(pfId, preservado.GetProperty("clienteId").GetGuid());
        Assert.AreEqual("Pessoa Sintética", preservado.GetProperty("clienteNome").GetString());

        var recriado = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa Sintética", cpf = CpfA, rg = "RG-2", cnh = "CNH-1",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "2" },
        });
        Assert.AreEqual(HttpStatusCode.Created, recriado.StatusCode);
        var novoPfId = (await Json(recriado)).GetProperty("id").GetGuid();
        Assert.AreNotEqual(pfId, novoPfId);

        var tokensB = await RegistrarELogar(cliente, "empresaB");
        Autorizar(cliente, tokensB);
        var pjB = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Empresa B", cnpj = "11444777000161", telefone = "Tel",
            endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        });
        var pjBId = (await Json(pjB)).GetProperty("id").GetGuid();
        Autorizar(cliente, tokens);
        var cruzado = await Problema(await Enviar(cliente, HttpMethod.Post, $"/api/v1/clientes/{novoPfId}/vinculos-pj",
            new { clientePjId = pjBId }, EtagDe(await cliente.GetAsync($"/api/v1/clientes/{novoPfId}"))),
            HttpStatusCode.BadRequest);
        Assert.IsTrue(cruzado.GetProperty("errors").TryGetProperty("clientePjId", out _));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var eventos = await banco.Auditorias.Select(x => x.Evento).ToListAsync();
        foreach (var evento in new[] { "cliente.criado", "cliente.excluido", "vinculo.criado", "vinculo.removido" })
            Assert.Contains(evento, eventos);
    }

    [TestMethod]
    public async Task Cnh_permanece_coerente_entre_cliente_pf_e_condutor_nos_dois_sentidos()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await RegistrarELogar(cliente);
        Autorizar(cliente, tokens);

        var pf = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Pessoa CNH", cpf = CpfA, rg = "RG", cnh = "CNH-1",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        });
        Assert.AreEqual(HttpStatusCode.Created, pf.StatusCode);
        var pfId = (await Json(pf)).GetProperty("id").GetGuid();
        var pfEtag = EtagDe(pf);

        var condutor = await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Pessoa CNH", email = "pessoa@example.invalid",
            cpf = CpfA, cnh = "CNH-1", validadeCnh = "2020-01-10", telefone = "Tel",
        });
        Assert.AreEqual(HttpStatusCode.Created, condutor.StatusCode);
        var condutorId = (await Json(condutor)).GetProperty("id").GetGuid();
        var condutorEtag = EtagDe(condutor);

        var incompativel = await Problema(await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Pessoa CNH", email = "outro@example.invalid",
            cpf = CpfA, cnh = "CNH-2", validadeCnh = "2030-01-10", telefone = "Tel",
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(incompativel.GetProperty("errors").TryGetProperty("cnh", out _));

        var edicaoCliente = await Enviar(cliente, HttpMethod.Put, $"/api/v1/clientes/{pfId}", new
        {
            tipo = "PF", nome = "Pessoa CNH", cpf = CpfA, rg = "RG", cnh = "CNH-3",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        }, pfEtag);
        Assert.AreEqual(HttpStatusCode.OK, edicaoCliente.StatusCode);
        var condutorAtualizado = await cliente.GetAsync($"/api/v1/condutores/{condutorId}");
        Assert.AreEqual("CNH-3", (await Json(condutorAtualizado)).GetProperty("cnh").GetString());
        Assert.AreNotEqual(condutorEtag, EtagDe(condutorAtualizado));

        var edicaoCondutor = await Enviar(cliente, HttpMethod.Put, $"/api/v1/condutores/{condutorId}", new
        {
            clienteId = pfId, nome = "Pessoa CNH", email = "pessoa@example.invalid",
            cpf = CpfA, cnh = "CNH-4", validadeCnh = "2030-01-10", telefone = "Tel",
        }, EtagDe(condutorAtualizado));
        Assert.AreEqual(HttpStatusCode.OK, edicaoCondutor.StatusCode);
        var clienteAtualizado = await cliente.GetAsync($"/api/v1/clientes/{pfId}");
        Assert.AreEqual("CNH-4", (await Json(clienteAtualizado)).GetProperty("cnh").GetString());

        var criacaoIncompativel = await Problema(await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PF", nome = "Outra", cpf = CpfA, rg = "RG", cnh = "CNH-9",
            telefone = "Tel", endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(criacaoIncompativel.GetProperty("errors").TryGetProperty("cnh", out _));

        var cpfDuplicado = await Problema(await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Pessoa CNH", email = "pessoa@example.invalid",
            cpf = CpfA, cnh = "CNH-4", validadeCnh = "2030-01-10", telefone = "Tel",
        }), HttpStatusCode.Conflict);
        Assert.IsTrue(cpfDuplicado.GetProperty("errors").TryGetProperty("cpf", out _));

        var pj = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "Empresa Condutora", cnpj = Cnpj, telefone = "Tel",
            endereco = new { estado = "E", cidade = "C", bairro = "B", rua = "R", numero = "1" },
        });
        var pjId = (await Json(pj)).GetProperty("id").GetGuid();
        var condutorPj = await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pjId, nome = "Condutor PJ", email = "condutor@example.invalid",
            cpf = CpfB, cnh = "CNH-PJ", validadeCnh = "2030-01-10", telefone = "Tel",
        });
        Assert.AreEqual(HttpStatusCode.Created, condutorPj.StatusCode);

        Assert.AreEqual(HttpStatusCode.NoContent, (await Enviar(cliente, HttpMethod.Delete,
            $"/api/v1/condutores/{condutorId}", null, EtagDe(await cliente.GetAsync($"/api/v1/condutores/{condutorId}")))).StatusCode);
        var recriado = await cliente.PostAsJsonAsync("/api/v1/condutores", new
        {
            clienteId = pfId, nome = "Pessoa CNH", email = "pessoa@example.invalid",
            cpf = CpfA, cnh = "CNH-4", validadeCnh = "2030-01-10", telefone = "Tel",
        });
        Assert.AreEqual(HttpStatusCode.Created, recriado.StatusCode);
        Assert.AreNotEqual(condutorId, (await Json(recriado)).GetProperty("id").GetGuid());

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        Assert.IsTrue(await banco.Auditorias.AnyAsync(x => x.Evento == "cliente.editado" &&
            x.CamposAlterados != null && x.CamposAlterados.Contains("cnh")));
        Assert.IsTrue(await banco.Auditorias.AnyAsync(x => x.Evento == "condutor.editado"));
        var eventoCliente = await banco.Auditorias.SingleAsync(x => x.Evento == "cliente.editado");
        var eventoSincronizado = await banco.Auditorias.SingleAsync(x => x.Evento == "condutor.cnh-sincronizada");
        Assert.AreEqual(eventoCliente.CorrelacaoId, eventoSincronizado.CorrelacaoId);
        Assert.IsNotNull(eventoCliente.UsuarioId);
    }

    [TestMethod]
    public async Task Precos_distinguem_ausente_de_zero_versionam_combustivel_e_serializam_escritas()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await RegistrarELogar(cliente);
        Autorizar(cliente, tokens);
        var empresa = EmpresaDe(tokens);

        var taxa = await cliente.PostAsJsonAsync("/api/v1/taxas-servicos",
            new { nome = "Lavagem", preco = "0", modalidade = "Fixa" });
        Assert.AreEqual(HttpStatusCode.Created, taxa.StatusCode);
        var taxaId = (await Json(taxa)).GetProperty("id").GetGuid();
        var taxaEtag = EtagDe(taxa);
        Assert.AreEqual("0", (await Json(taxa)).GetProperty("preco").GetString());

        await Problema(await cliente.PostAsJsonAsync("/api/v1/taxas-servicos",
            new { nome = "Negativa", preco = "-1", modalidade = "Fixa" }), HttpStatusCode.BadRequest);
        var formato = await Problema(await cliente.PostAsJsonAsync("/api/v1/taxas-servicos",
            new { nome = "Vírgula", preco = "1,5", modalidade = "Diaria" }), HttpStatusCode.BadRequest);
        Assert.IsTrue(formato.GetProperty("errors").TryGetProperty("preco", out _));

        var taxaEditada = await Enviar(cliente, HttpMethod.Put, $"/api/v1/taxas-servicos/{taxaId}",
            new { nome = "Lavagem simples", preco = "25.5", modalidade = "Diaria" }, taxaEtag);
        Assert.AreEqual(HttpStatusCode.OK, taxaEditada.StatusCode);
        Assert.AreEqual("25.5", (await Json(taxaEditada)).GetProperty("preco").GetString());
        Assert.AreEqual(HttpStatusCode.NoContent, (await Enviar(cliente, HttpMethod.Delete,
            $"/api/v1/taxas-servicos/{taxaId}", null, EtagDe(taxaEditada))).StatusCode);
        Assert.AreEqual(0, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/taxas-servicos"))
            .GetProperty("total").GetInt32());

        var seguroAusente = await cliente.GetAsync("/api/v1/configuracoes/seguros/Cliente");
        Assert.AreEqual(HttpStatusCode.OK, seguroAusente.StatusCode);
        var seguroInicial = EtagDe(seguroAusente);
        var estadoAusente = await Json(seguroAusente);
        Assert.IsFalse(estadoAusente.GetProperty("configurado").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, estadoAusente.GetProperty("precoDiario").ValueKind);

        await Problema(await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/seguros/Cliente",
            new { precoDiario = "0" }, null), HttpStatusCode.PreconditionRequired);

        using var concorrente = fabrica.CreateClient();
        Autorizar(concorrente, tokens);
        var respostas = await Task.WhenAll(
            Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/seguros/Cliente", new { precoDiario = "0" }, seguroInicial),
            Enviar(concorrente, HttpMethod.Put, "/api/v1/configuracoes/seguros/Cliente", new { precoDiario = "10" }, seguroInicial));
        Assert.AreEqual(1, respostas.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, respostas.Count(x => x.StatusCode == HttpStatusCode.PreconditionFailed));

        var seguroCliente = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/seguros/Cliente");
        Assert.IsTrue(seguroCliente.GetProperty("configurado").GetBoolean());
        var vencedor = await Json(respostas.Single(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(vencedor.GetProperty("precoDiario").GetString(),
            seguroCliente.GetProperty("precoDiario").GetString());
        var seguroAtual = await cliente.GetAsync("/api/v1/configuracoes/seguros/Cliente");
        var zero = await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/seguros/Cliente",
            new { precoDiario = "0" }, EtagDe(seguroAtual));
        Assert.AreEqual(HttpStatusCode.OK, zero.StatusCode);
        Assert.AreEqual("0", (await Json(zero)).GetProperty("precoDiario").GetString());
        var seguroTerceiros = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/seguros/Terceiros");
        Assert.IsFalse(seguroTerceiros.GetProperty("configurado").GetBoolean());

        var combustivelAusente = await cliente.GetAsync("/api/v1/configuracoes/combustiveis/Gasolina");
        var combustivelEtag = EtagDe(combustivelAusente);
        Assert.IsFalse((await Json(combustivelAusente)).GetProperty("configurado").GetBoolean());
        await Problema(await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/combustiveis/Gasolina",
            new { preco = "5" }, null), HttpStatusCode.PreconditionRequired);

        var primeira = await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/combustiveis/Gasolina",
            new { preco = "5" }, combustivelEtag);
        Assert.AreEqual(HttpStatusCode.OK, primeira.StatusCode);
        var primeiraVersao = await Json(primeira);
        Assert.AreEqual(1, primeiraVersao.GetProperty("ordem").GetInt64());
        await Problema(await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/combustiveis/Gasolina",
            new { preco = "6" }, combustivelEtag), HttpStatusCode.PreconditionFailed);

        var segunda = await Enviar(cliente, HttpMethod.Put, "/api/v1/configuracoes/combustiveis/Gasolina",
            new { preco = "6" }, EtagDe(primeira));
        Assert.AreEqual(HttpStatusCode.OK, segunda.StatusCode);
        Assert.AreEqual(2, (await Json(segunda)).GetProperty("ordem").GetInt64());

        var historico = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/combustiveis/Gasolina/historico");
        Assert.AreEqual(2, historico.GetProperty("total").GetInt32());
        Assert.AreEqual("6", historico.GetProperty("items")[0].GetProperty("preco").GetString());
        Assert.AreEqual("5", historico.GetProperty("items")[1].GetProperty("preco").GetString());

        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
            var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
            var instante = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
            await banco.PrecosCombustivel.ExecuteUpdateAsync(x =>
                x.SetProperty(p => p.InicioVigenciaUtc, instante));
        }
        var historicoEmpatado = await cliente.GetFromJsonAsync<JsonElement>(
            "/api/v1/configuracoes/combustiveis/Gasolina/historico");
        Assert.AreEqual(2, historicoEmpatado.GetProperty("items")[0].GetProperty("ordem").GetInt64());
        Assert.AreEqual(1, historicoEmpatado.GetProperty("items")[1].GetProperty("ordem").GetInt64());

        var tokensB = await RegistrarELogar(cliente, "empresaB");
        Autorizar(cliente, tokensB);
        var seguroB = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/configuracoes/seguros/Cliente");
        Assert.IsFalse(seguroB.GetProperty("configurado").GetBoolean());
        var combustivelB = await cliente.GetFromJsonAsync<JsonElement>(
            "/api/v1/configuracoes/combustiveis/Gasolina/historico");
        Assert.AreEqual(0, combustivelB.GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task Http_mapeia_precondicoes_campos_etag_cors_e_falha_de_auditoria()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add("Origin", "http://localhost:4200");
        var tokens = await RegistrarELogar(cliente);
        Autorizar(cliente, tokens);

        var criado = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo CORS" });
        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        Assert.IsTrue(criado.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsTrue(criado.Headers.TryGetValues("Access-Control-Expose-Headers", out var expostos));
        Assert.IsTrue(expostos!.Any(x => x.Contains("ETag", StringComparison.OrdinalIgnoreCase)));
        var etag = criado.Headers.ETag?.Tag;
        Assert.IsNotNull(etag);
        Assert.IsTrue(etag.StartsWith('"') && etag.EndsWith('"'));

        var enumInvalido = await Problema(await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = Guid.NewGuid(), placa = "AAA1111", marca = "M", modelo = "M", cor = "C",
            ano = 2020, combustivel = "Inexistente", capacidadeLitros = "10",
        }), HttpStatusCode.BadRequest);
        Assert.AreEqual("Dados inválidos", enumInvalido.GetProperty("title").GetString());
        Assert.AreEqual("validacao", enumInvalido.GetProperty("code").GetString());
        Assert.IsTrue(enumInvalido.TryGetProperty("errors", out _));

        var grupoId = (await Json(criado)).GetProperty("id").GetGuid();
        var inexistente = await Problema(await cliente.GetAsync($"/api/v1/grupos/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound);
        Assert.AreEqual("nao_encontrado", inexistente.GetProperty("code").GetString());

        var excluido = await Enviar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupoId}", null, null);
        await Problema(excluido, HttpStatusCode.PreconditionRequired);

        using var falha = new SqlServerWebApplicationFactory(falharAuditoriaEvento: "grupo.criado");
        using var clienteFalha = falha.CreateClient();
        var tokensFalha = await RegistrarELogar(clienteFalha);
        Autorizar(clienteFalha, tokensFalha);
        await Problema(await clienteFalha.PostAsJsonAsync("/api/v1/grupos", new { nome = "Não deve persistir" }),
            HttpStatusCode.InternalServerError);
        Assert.AreEqual(0, (await clienteFalha.GetFromJsonAsync<JsonElement>("/api/v1/grupos"))
            .GetProperty("total").GetInt32());
        await using var escopo = falha.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        Assert.IsFalse(await banco.Auditorias.AnyAsync(x => x.Evento == "grupo.criado"));
        Assert.IsTrue(await banco.Auditorias.AnyAsync(x => x.Evento == "Registro"));
    }

    [TestMethod]
    public async Task Guarda_de_escrita_e_filtros_isolam_cadastros_por_empresa()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokensA = await RegistrarELogar(cliente);
        Autorizar(cliente, tokensA);
        var grupoA = await CriarGrupo(cliente, "Grupo A");
        var tokensB = await RegistrarELogar(cliente, "empresaB");
        Autorizar(cliente, tokensB);
        var grupoB = await CriarGrupo(cliente, "Grupo B");

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        contexto.Definir(EmpresaDe(tokensA));
        var idsA = await banco.Grupos.Select(x => x.Id).ToListAsync();
        CollectionAssert.AreEquivalent(new[] { grupoA }, idsA);

        contexto.Definir(EmpresaDe(tokensB));
        banco.ChangeTracker.Clear();
        var idsB = await banco.Grupos.Select(x => x.Id).ToListAsync();
        CollectionAssert.AreEquivalent(new[] { grupoB }, idsB);

        banco.Grupos.Add(new Grupo
        {
            EmpresaId = EmpresaDe(tokensA), Nome = "Ataque",
            CriadoEm = DateTimeOffset.UtcNow, AtualizadoEm = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => banco.SaveChangesAsync());

        banco.ChangeTracker.Clear();
        var anexado = new Grupo
        {
            Id = grupoA, EmpresaId = EmpresaDe(tokensB), Nome = "Anexado",
            CriadoEm = DateTimeOffset.UtcNow, AtualizadoEm = DateTimeOffset.UtcNow,
        };
        banco.Grupos.Attach(anexado);
        banco.Entry(anexado).Property(x => x.Nome).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => banco.SaveChangesAsync());
    }

    private static async Task<ResultadoAutenticacao> RegistrarELogar(HttpClient cliente, string usuario = "administrador")
    {
        var registro = await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = usuario, email = $"{usuario}@example.test", password = "12345678" });
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        var login = await cliente.PostAsJsonAsync("/api/v1/session/login",
            new { username = usuario, password = "12345678" });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
    }

    private static void Autorizar(HttpClient cliente, ResultadoAutenticacao tokens) =>
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

    private static Guid EmpresaDe(ResultadoAutenticacao tokens) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken)
            .Claims.Single(x => x.Type == "empresaId").Value);

    private static async Task<Guid> CriarGrupo(HttpClient cliente, string nome)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome });
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        return (await Json(resposta)).GetProperty("id").GetGuid();
    }

    private static string EtagDe(HttpResponseMessage resposta)
    {
        var valor = resposta.Headers.ETag?.Tag
            ?? (resposta.Headers.TryGetValues("ETag", out var valores) ? valores.Single() : null);
        Assert.IsNotNull(valor, "Resposta sem ETag.");
        return valor.Trim('"');
    }

    private static Task<HttpResponseMessage> Enviar(HttpClient cliente, HttpMethod metodo, string url,
        object? corpo, string? etag)
    {
        var pedido = new HttpRequestMessage(metodo, url);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
        if (etag is not null) pedido.Headers.TryAddWithoutValidation("If-Match", etag.Contains('"') ? etag : $"\"{etag}\"");
        return cliente.SendAsync(pedido);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage resposta)
    {
        var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.Clone();
    }

    private static async Task<JsonElement> Problema(HttpResponseMessage resposta, HttpStatusCode status)
    {
        Assert.AreEqual(status, resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var json = await Json(resposta);
        Assert.AreEqual((int)status, json.GetProperty("status").GetInt32());
        Assert.IsTrue(json.TryGetProperty("code", out _));
        Assert.IsTrue(json.TryGetProperty("traceId", out _));
        var texto = json.ToString();
        foreach (var proibido in new[] { CpfA, CpfB, Cnpj, "Password=", "SqlException", "stackTrace" })
            Assert.IsFalse(texto.Contains(proibido, StringComparison.OrdinalIgnoreCase));
        return json;
    }
}
