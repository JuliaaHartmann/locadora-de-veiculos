using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class EmailOpcionalCondutorTests
{
    [TestMethod]
    public async Task Email_opcional_preserva_legado_aceita_ausencia_valida_formato_e_protege_reversao()
    {
        const string anterior = "20261126143553_CodigosApresentacao";
        const string condutores = "/api/v1/condutores";
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var migrador = db.GetService<IMigrator>();
        try
        {
            await migrador.MigrateAsync(anterior);
            var dados = await EntrarEPreparar(http, fabrica);
            var legado = $"{condutores}/{dados.Condutor.Id}";
            using var antes = await http.GetAsync(legado);
            Assert.AreEqual(HttpStatusCode.OK, antes.StatusCode);
            var etag = antes.Headers.ETag!.ToString();
            await migrador.MigrateAsync();
            using var depois = await http.GetAsync(legado);
            Assert.AreEqual(HttpStatusCode.OK, depois.StatusCode);
            Assert.AreEqual(etag, depois.Headers.ETag!.ToString());
            var preservado = await depois.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual("condutor@example.invalid", preservado.GetProperty("email").GetString());
            Assert.AreEqual("0001", preservado.GetProperty("numeroExibicao").GetString());

            var comando = new
            {
                clienteId = dados.Condutor.ClienteId, nome = "Condutor sem e-mail", cpf = "11144477735",
                cnh = "CNH-TESTE", validadeCnh = "2034-01-01", telefone = "11000000000",
            };
            using var criado = await http.PostAsJsonAsync(condutores, comando);
            Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode, await criado.Content.ReadAsStringAsync());
            var corpo = await criado.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual(JsonValueKind.Null, corpo.GetProperty("email").ValueKind);
            var rota = $"{condutores}/{corpo.GetProperty("id").GetGuid()}";

            async Task<HttpResponseMessage> Editar(string? email, string versao) =>
                await Enviar(http, HttpMethod.Put, rota, new { comando.clienteId, comando.nome, comando.cpf,
                    comando.cnh, comando.validadeCnh, comando.telefone, email }, "If-Match", versao);

            using var invalido = await Editar("invalido", criado.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.BadRequest, invalido.StatusCode);
            Assert.IsTrue((await invalido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("email", out _));
            using var preenchido = await Editar("  novo@example.invalid  ", criado.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.OK, preenchido.StatusCode);
            Assert.AreEqual("novo@example.invalid",
                (await preenchido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString());
            using var limpo = await Editar("  ", preenchido.Headers.ETag!.ToString());
            Assert.AreEqual(HttpStatusCode.OK, limpo.StatusCode);

            var bloqueio = await Assert.ThrowsExactlyAsync<SqlException>(() => migrador.MigrateAsync(anterior));
            Assert.AreEqual(51024, bloqueio.Number);
            using var consultado = await http.GetAsync(rota);
            Assert.AreEqual(HttpStatusCode.OK, consultado.StatusCode);
            Assert.AreEqual(JsonValueKind.Null,
                (await consultado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").ValueKind);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
