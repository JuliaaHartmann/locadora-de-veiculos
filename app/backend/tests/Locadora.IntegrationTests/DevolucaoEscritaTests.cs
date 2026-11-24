using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class DevolucaoTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Corrida_serializa_duas_devolucoes_ou_edicao_contra_devolucao(bool comEdicao)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        b.DefaultRequestHeaders.Authorization = a.DefaultRequestHeaders.Authorization;
        var (id, etag, saida) = await Contratar(a, dados);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var comando = await PrepararMedicoes(db, dados.Empresa, saida);
        var previa = await ComServico(fabrica, a, dados.Empresa, s => s.PreverDevolucaoAsync(id, comando, default));

        var primeira = TentarDevolver("primeira");
        var segunda = comEdicao ? TentarEditar() : TentarDevolver("segunda");
        var resultados = await Task.WhenAll(primeira, segunda);
        Assert.AreNotEqual(resultados[0], resultados[1]);
        var fechou = !comEdicao || resultados[0];
        var atual = await db.Alugueis.AsNoTracking().SingleAsync();
        Assert.AreEqual(fechou ? EstadoAluguel.Concluido : EstadoAluguel.EmAberto, atual.Estado);
        Assert.AreEqual(fechou ? 1 : 2, atual.RevisaoAtual);
        Assert.AreEqual(fechou ? 1 : 0, await db.FechamentosAlugueis.CountAsync());
        Assert.AreEqual(fechou ? 1 : 0, await db.IdempotenciaAlugueis.CountAsync(x => x.Operacao == "devolver"));
        Assert.AreEqual(fechou ? 1 : 0, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.devolvido"));
        Assert.AreEqual(fechou ? 0 : 1, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.editado"));

        async Task<bool> TentarDevolver(string chave)
        {
            try
            {
                await ComServico(fabrica, a, dados.Empresa,
                    s => s.ConcluirAsync(id, comando, previa.Valor.VersaoAceita, etag, chave, default));
                return true;
            }
            catch (PrecondicaoException) when (comEdicao) { return false; }
            catch (ConflitoException erro) when (!comEdicao && erro.Campo == "estado") { return false; }
        }

        async Task<bool> TentarEditar()
        {
            var editado = await Enviar(b, HttpMethod.Put, $"{Rota}/{id}",
                ComandoAtual(dados) with { HodometroInicial = 480 }, "If-Match", etag);
            Assert.IsTrue(editado.StatusCode is HttpStatusCode.OK or HttpStatusCode.PreconditionFailed);
            return editado.StatusCode == HttpStatusCode.OK;
        }
    }

    [TestMethod]
    public async Task Preco_alterado_e_falha_antes_da_confirmacao_preservam_ocupacao_sem_resultado_parcial()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var (id, etag, saida) = await Contratar(a, dados);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var comando = await PrepararMedicoes(db, dados.Empresa, saida);
        var primeira = await Previa();
        db.PrecosCombustivel.Add(Preco(dados.Empresa, saida.AddHours(1), 2, 7.25m));
        await db.SaveChangesAsync();
        var conflito = await Assert.ThrowsExactlyAsync<ConflitoException>(() => Confirmar(primeira.VersaoAceita));
        Assert.AreEqual("versaoAceita", conflito.Campo);
        await ContinuaOcupado();
        var segunda = await Previa();
        Assert.AreNotEqual(primeira.VersaoAceita, segunda.VersaoAceita);

        // A primeira gravação interna já aplicou estado, final e auditoria. A segunda falha ao salvar a chave.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IdempotenciaAlugueis ADD CONSTRAINT CK_TesteFalhaFechamento CHECK (Operacao <> 'devolver');");
        await Assert.ThrowsExactlyAsync<FalhaGravacaoDevolucaoException>(() => Confirmar(segunda.VersaoAceita));
        await ContinuaOcupado();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE IdempotenciaAlugueis DROP CONSTRAINT CK_TesteFalhaFechamento;");
        var concluido = await Confirmar(segunda.VersaoAceita);
        Assert.AreEqual(segunda.Demonstrativo.Total, concluido.Valor.Final.Total);
        Assert.AreEqual(1, await db.FechamentosAlugueis.CountAsync());
        Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.devolvido"));
        Assert.AreEqual(EstadoAluguel.Concluido, await db.Alugueis.Select(x => x.Estado).SingleAsync());

        async Task ContinuaOcupado()
        {
            Assert.AreEqual(EstadoAluguel.EmAberto, await db.Alugueis.Select(x => x.Estado).SingleAsync());
            Assert.AreEqual(0, await db.FechamentosAlugueis.CountAsync());
            Assert.AreEqual(0, await db.ItensFechamentoAluguel.CountAsync());
            Assert.AreEqual(0, await db.IdempotenciaAlugueis.CountAsync(x => x.Operacao == "devolver"));
            Assert.AreEqual(0, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.devolvido"));
        }

        Task<RespostaComVersao<DevolucaoConcluida>> Confirmar(string versao) => ComServico(fabrica, a, dados.Empresa,
            s => s.ConcluirAsync(id, comando, versao, etag, "fechar", default));
        async Task<PreviaDevolucao> Previa() => (await ComServico(fabrica, a, dados.Empresa,
            s => s.PreverDevolucaoAsync(id, comando, default))).Valor;
    }

    private static async Task<ComandoDevolucao> PrepararMedicoes(Infrastructure.LocadoraDbContext db, Guid empresa,
        DateTimeOffset saida)
    {
        db.PrecosCombustivel.Add(Preco(empresa, saida, 1, 6));
        await db.SaveChangesAsync();
        return new(Local(saida.AddHours(27)), 510, "40", []);
    }

    private static async Task<T> ComServico<T>(SqlServerWebApplicationFactory fabrica, HttpClient http,
        Guid empresa, Func<AluguelService, Task<T>> executar)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(http.DefaultRequestHeaders.Authorization!.Parameter);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa, Guid.Parse(token.Subject));
        return await executar(escopo.ServiceProvider.GetRequiredService<AluguelService>());
    }
}
