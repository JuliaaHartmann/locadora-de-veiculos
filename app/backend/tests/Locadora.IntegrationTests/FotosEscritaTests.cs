using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class FotosTests
{
    [TestMethod]
    [DataRow("aluguel")]
    [DataRow("edicao")]
    [DataRow("remocao")]
    public async Task Revalida_versao_e_ocupacao_depois_de_armazenar(string concorrente)
    {
        using var volume = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: volume);
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var foto = rota + "/foto";
        var inicial = await EnviarFoto(a, foto, await Versao(a, rota));
        await Estado(inicial, HttpStatusCode.NoContent);
        var anterior = await a.GetByteArrayAsync(foto);
        var etag = inicial.Headers.ETag!.ToString();
        var gravado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seguir = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        volume.AposGravar = async (_, ct) => { gravado.TrySetResult(); await seguir.Task.WaitAsync(ct); };
        var envio = EnviarFoto(a, foto, etag, Imagem(SKColors.Yellow));
        try
        {
            await gravado.Task.WaitAsync(TimeSpan.FromSeconds(12));
            CollectionAssert.AreEqual(anterior, await a.GetByteArrayAsync(foto));
            await Estado(await EnviarFoto(a, foto, etag), HttpStatusCode.ServiceUnavailable);
            if (concorrente == "aluguel")
            {
                await Estado(await Enviar(a, HttpMethod.Post, "/api/v1/alugueis", ComandoAtual(dados),
                    "Idempotency-Key", "corrida-aluguel"), HttpStatusCode.Created);
                await Estado(await ExcluirFoto(a, foto, etag), HttpStatusCode.Conflict);
            }
            else if (concorrente == "edicao")
            {
                await Estado(await Enviar(a, HttpMethod.Put, rota, new
                {
                    grupoId = dados.Grupo.Id, placa = dados.Veiculo.Placa, marca = dados.Veiculo.Marca,
                    modelo = dados.Veiculo.Modelo, cor = "Azul", ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50",
                }, "If-Match", etag), HttpStatusCode.OK);
            }
            else await Estado(await ExcluirFoto(a, foto, etag), HttpStatusCode.NoContent);
        }
        finally { seguir.TrySetResult(); }
        await Estado(await envio, concorrente == "aluguel" ? HttpStatusCode.Conflict : HttpStatusCode.PreconditionFailed);
        if (concorrente == "remocao") await Estado(await a.GetAsync(foto), HttpStatusCode.NotFound);
        else CollectionAssert.AreEqual(anterior, await a.GetByteArrayAsync(foto));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        Assert.AreEqual(1, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Pendente));
        Assert.AreEqual(0, await db.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento == "foto.substituida"));
    }

    [TestMethod]
    public async Task Falha_antes_da_ativacao_e_retomada_duravel_preservam_foto_ativa()
    {
        using var storage = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: storage, falharAuditoriaEvento: "foto.substituida");
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var foto = rota + "/foto";
        var enviada = await EnviarFoto(a, foto, await Versao(a, rota));
        await Estado(enviada, HttpStatusCode.NoContent);
        var etag = enviada.Headers.ETag!.ToString();
        var anterior = await a.GetByteArrayAsync(foto);
        storage.FalharGravacao = true;
        var falha = await EnviarFoto(a, foto, etag);
        await Estado(falha, HttpStatusCode.ServiceUnavailable);
        Assert.AreEqual("foto_nao_gravada", (await falha.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        storage.FalharGravacao = false;
        await Estado(await EnviarFoto(a, foto, etag, Imagem(SKColors.Green)), HttpStatusCode.InternalServerError);
        Assert.AreEqual(etag, await Versao(a, rota));
        CollectionAssert.AreEqual(anterior, await a.GetByteArrayAsync(foto));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        Assert.AreEqual(1, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(2, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Pendente));
        Assert.AreEqual(0, await db.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento == "foto.substituida"));

        async Task Expirar() => await db.FotosObjetos.Where(x => x.Estado == EstadoFoto.Pendente || x.Estado == EstadoFoto.Orfa)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.RetomarAposUtc, DateTimeOffset.UtcNow.AddMinutes(-5)));
        async Task RetomarEmNovoEscopo()
        {
            await using var novo = fabrica.Services.CreateAsyncScope();
            Banco(novo, dados.Empresa);
            await novo.ServiceProvider.GetRequiredService<FotosService>().RetomarAsync(default);
        }
        storage.FalharRemocao = true;
        await Expirar();
        await RetomarEmNovoEscopo();
        Assert.AreEqual(1, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Orfa && x.TentativasCompensacao == 1));
        storage.FalharRemocao = false;
        await Expirar();
        await Task.WhenAll(RetomarEmNovoEscopo(), RetomarEmNovoEscopo());
        Assert.AreEqual(2, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Removida));
        Assert.AreEqual(3, storage.Remocoes);
        CollectionAssert.AreEqual(anterior, await a.GetByteArrayAsync(foto));
    }

    [TestMethod]
    public async Task Resposta_incerta_pos_commit_nao_compensa_foto_ativa()
    {
        using var storage = new VolumeTeste();
        var interceptar = new PerdaConfirmacao();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: storage, interceptorFotos: interceptar);
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var foto = rota + "/foto";
        var etag = await Versao(a, rota);
        storage.AposGravar = (_, _) => { interceptar.Falhar = true; return Task.CompletedTask; };
        await Estado(await EnviarFoto(a, foto, etag), HttpStatusCode.InternalServerError);
        var atual = await Versao(a, rota);
        Assert.AreNotEqual(etag, atual);
        await Estado(await a.GetAsync(foto), HttpStatusCode.OK);
        await using var novo = fabrica.Services.CreateAsyncScope();
        var db = Banco(novo, dados.Empresa);
        await novo.ServiceProvider.GetRequiredService<FotosService>().RetomarAsync(default);
        Assert.AreEqual(1, await db.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento == "foto.adicionada"));
        Assert.AreEqual(0, storage.Remocoes);
    }

    [TestMethod]
    public async Task Outro_host_retoma_orfao_do_SQL_e_do_volume_sem_expor_a_pendencia()
    {
        using var storage = new VolumeTeste();
        var identificador = Guid.NewGuid();
        Guid empresa = default;
        string rota = "", tokenSessao = "", etag = "";
        using var reaberta = new SqlServerWebApplicationFactory(prepararSchema: false, armazenamentoFotos: storage,
            bancoCompartilhadoFotos: identificador);
        try
        {
            using (var primeira = new SqlServerWebApplicationFactory(prepararSchema: false, armazenamentoFotos: storage,
                falharAuditoriaEvento: "foto.substituida", bancoCompartilhadoFotos: identificador))
            {
                using var a = primeira.CreateClient();
                await using var inicial = primeira.Services.CreateAsyncScope();
                var db = inicial.ServiceProvider.GetRequiredService<LocadoraDbContext>();
                await db.Database.MigrateAsync();
                var dados = await EntrarEPreparar(a, primeira);
                (empresa, rota, tokenSessao) = (dados.Empresa, $"/api/v1/veiculos/{dados.Veiculo.Id}",
                    a.DefaultRequestHeaders.Authorization!.Parameter!);
                var enviada = await EnviarFoto(a, rota + "/foto", await Versao(a, rota));
                await Estado(enviada, HttpStatusCode.NoContent);
                etag = enviada.Headers.ETag!.ToString();
                await Estado(await EnviarFoto(a, rota + "/foto", etag, Imagem(SKColors.Red)), HttpStatusCode.InternalServerError);
                inicial.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
                await db.FotosObjetos.Where(x => x.Estado == EstadoFoto.Pendente)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.RetomarAposUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
            }
            using var cliente = reaberta.CreateClient();
            cliente.DefaultRequestHeaders.Authorization = new("Bearer", tokenSessao);
            await Estado(await cliente.GetAsync(rota + "/foto"), HttpStatusCode.OK);
            await Estado(await ExcluirFoto(cliente, rota + "/foto", etag), HttpStatusCode.NoContent);
            await Estado(await cliente.GetAsync(rota + "/foto"), HttpStatusCode.NotFound);
            await using var escopo = reaberta.Services.CreateAsyncScope();
            var consulta = Banco(escopo, empresa);
            Assert.AreEqual(1, await consulta.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Removida));
            Assert.AreEqual(1, await consulta.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Retida));
            Assert.AreEqual(1, storage.Remocoes);
        }
        finally
        {
            await using var limpeza = reaberta.Services.CreateAsyncScope();
            await limpeza.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [TestMethod]
    public async Task Segunda_tentativa_de_armazenamento_reutiliza_a_chave_e_a_pendencia()
    {
        using var storage = new VolumeTeste();
        var chaves = new List<string>();
        storage.AposGravar = (objeto, _) =>
        {
            chaves.Add(objeto.Chave);
            if (chaves.Count == 1) throw FalhaFotoException.Indisponivel();
            return Task.CompletedTask;
        };
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: storage);
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        await Estado(await EnviarFoto(a, rota + "/foto", await Versao(a, rota)), HttpStatusCode.NoContent);
        Assert.HasCount(2, chaves);
        Assert.AreEqual(chaves[0], chaves[1]);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        Assert.AreEqual(EstadoFoto.Ativa, (await db.FotosObjetos.SingleAsync()).Estado);
        Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento == "foto.adicionada"));
    }

    [TestMethod]
    public async Task Armazenamento_indisponivel_nao_bloqueia_locacao_ou_cadastro()
    {
        using var storage = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: storage);
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        await Estado(await EnviarFoto(a, rota + "/foto", await Versao(a, rota)), HttpStatusCode.NoContent);
        storage.FalharLeitura = storage.FalharGravacao = true;
        await Estado(await a.GetAsync(rota + "/foto"), HttpStatusCode.ServiceUnavailable);
        await Estado(await a.GetAsync(rota), HttpStatusCode.OK);
        var aluguel = await Enviar(a, HttpMethod.Post, "/api/v1/alugueis", ComandoAtual(dados), "Idempotency-Key", "f5-regressao");
        await Estado(aluguel, HttpStatusCode.Created);
        var dadosAluguel = await aluguel.Content.ReadFromJsonAsync<JsonElement>();
        var aluguelId = dadosAluguel.GetProperty("id").GetGuid();
        var saida = dadosAluguel.GetProperty("saida").GetDateTimeOffset();
        var retorno = HorarioLocacao.ParaSaoPaulo(saida.AddHours(1));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        db.PrecosCombustivel.Add(new PrecoCombustivel
        {
            EmpresaId = dados.Empresa, Tipo = Combustivel.Gasolina, InicioVigenciaUtc = saida, Preco = 6,
            CriadoEm = saida, AtualizadoEm = saida,
        });
        await db.SaveChangesAsync();
        var devolucao = $"/api/v1/alugueis/{aluguelId}/devolucao";
        var previa = await a.PostAsJsonAsync(devolucao + "/previa", new { retornoReal = retorno, hodometroFinal = 500, litrosRestantes = "50" });
        await Estado(previa, HttpStatusCode.OK);
        var versao = (await previa.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versaoAceita").GetString();
        using var confirmar = new HttpRequestMessage(HttpMethod.Post, devolucao)
        {
            Content = JsonContent.Create(new { retornoReal = retorno, hodometroFinal = 500, litrosRestantes = "50", versaoAceita = versao }),
        };
        confirmar.Headers.TryAddWithoutValidation("If-Match", previa.Headers.ETag!.ToString());
        confirmar.Headers.Add("Idempotency-Key", "volta-f5-regressao");
        await Estado(await a.SendAsync(confirmar), HttpStatusCode.OK);
        await Estado(await a.GetAsync(rota + "/foto"), HttpStatusCode.ServiceUnavailable);
    }

    private sealed class PerdaConfirmacao : DbTransactionInterceptor
    {
        public bool Falhar { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Falhar)
            {
                Falhar = false;
                throw new InvalidOperationException("Resposta sintética indisponível após commit.");
            }
            return Task.CompletedTask;
        }
    }
}
