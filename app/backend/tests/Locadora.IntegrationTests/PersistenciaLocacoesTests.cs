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
public sealed class PersistenciaLocacoesTests
{
    [TestMethod]
    public async Task Migration_F4_preserva_aluguel_em_aberto_revisao_e_replay_da_F3()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
        var migra = db.GetService<IMigrator>();
        try
        {
            // Gera a resposta e a chave de criação reais. A reversão sem devoluções deixa os dados
            // no schema da F3, sem FinalId, antes de exercitar o caminho de upgrade.
            await migra.MigrateAsync();
            var dados = await ContratosLocacoesTests.EntrarEPreparar(http, fabrica);
            contexto.Definir(dados.Empresa);
            var comando = ContratosLocacoesTests.ComandoAtual(dados);
            var inicial = await Criar();
            var chave = await db.IdempotenciaAlugueis.AsNoTracking().SingleAsync();
            var revisao = await db.ContratosSnapshot.AsNoTracking().SingleAsync();
            await migra.MigrateAsync("20261123145643_Fase3ContratacaoCalculo");
            Assert.AreEqual("20261123145643_Fase3ContratacaoCalculo",
                (await db.Database.GetAppliedMigrationsAsync()).Last());
            await migra.MigrateAsync();

            Assert.AreEqual(0, await db.FechamentosAlugueis.CountAsync());
            Assert.AreEqual(0, await db.ItensFechamentoAluguel.CountAsync());
            var contrato = await db.Alugueis.AsNoTracking().SingleAsync();
            Assert.AreEqual(EstadoAluguel.EmAberto, contrato.Estado);
            Assert.AreEqual(revisao.Id, (await db.ContratosSnapshot.AsNoTracking().SingleAsync()).Id);
            var preservada = await db.IdempotenciaAlugueis.AsNoTracking().SingleAsync();
            Assert.IsNull(preservada.FinalId);
            CollectionAssert.AreEqual(chave.HashComando, preservada.HashComando);
            Assert.AreEqual(inicial, await Criar());
            Assert.AreEqual(1, await db.Alugueis.CountAsync());

            async Task<(string Corpo, string Etag)> Criar()
            {
                using var resposta = await ContratosLocacoesTests.Enviar(http, HttpMethod.Post, "/api/v1/alugueis",
                    comando, "Idempotency-Key", "legado-f3");
                Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
                var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
                return (corpo.GetRawText(), resposta.Headers.ETag!.ToString());
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [TestMethod]
    public async Task Migration_F3_preserva_cadastros_de_F2()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
        try
        {
            var migra = db.GetService<IMigrator>();
            await migra.MigrateAsync("20261123005316_Fase2Cadastros");
            var dados = await CriarCadastros(db, contexto);
            db.ChangeTracker.Clear();
            await migra.MigrateAsync();
            Assert.AreEqual("Frota de teste", (await db.Grupos.SingleAsync()).Nome);
            Assert.AreEqual(dados.Veiculo.Id, (await db.Veiculos.SingleAsync()).Id);
            Assert.AreEqual(0, await db.Alugueis.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [TestMethod]
    public async Task Banco_vazio_aplica_migrations_e_protege_ocupacao_referencia_e_itens()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
        var outraEmpresa = await CriarCadastros(db, contexto);
        var dados = await CriarCadastros(db, contexto);

        var locacao = dados.NovoAluguel();
        var revisao = dados.NovaRevisao(locacao);
        var item = dados.NovoItem(revisao);
        db.AddRange(locacao, revisao, item);
        await db.SaveChangesAsync();
        Assert.HasCount(8, locacao.Versao);
        Assert.AreEqual(1, await db.Alugueis.CountAsync());
        await Rejeitar(dados.NovoAluguel());
        await Rejeitar(dados.NovoItem(revisao));

        var cruzado = dados.NovoAluguel();
        cruzado.VeiculoId = outraEmpresa.Veiculo.Id;
        await Rejeitar(cruzado);
        contexto.Definir(outraEmpresa.Empresa);
        Assert.AreEqual(0, await db.Alugueis.CountAsync());
        Assert.AreEqual(0, await db.ContratosSnapshot.CountAsync());
        Assert.AreEqual(0, await db.ItensContratados.CountAsync());
        Assert.AreEqual(0, await db.FechamentosAlugueis.CountAsync());
        Assert.AreEqual(0, await db.ItensFechamentoAluguel.CountAsync());
        db.Alugueis.Add(dados.NovoAluguel());
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());

        async Task Rejeitar(object entidade)
        {
            db.Add(entidade);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
        }
    }

    [TestMethod]
    public async Task Duas_previas_sao_leituras_e_distinguem_seguro_ausente_de_zero()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var http = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>();
        var dados = await CriarCadastros(db, contexto);
        var escrita = escopo.ServiceProvider.GetRequiredService<IProtocoloEscrita>();
        var servico = new AluguelService(new RepositorioAlugueis(db, escrita), escrita, new RelogioLocacaoTeste(dados.Agora));
        var comando = dados.Comando();
        var primeira = await servico.EstimarAsync(comando, default);
        var segunda = await servico.EstimarAsync(comando, default);
        Assert.AreEqual("130.00", primeira.Estimativa.ParcelasConhecidas);
        Assert.AreEqual(primeira.Estimativa.ParcelasConhecidas, segunda.Estimativa.ParcelasConhecidas);
        Assert.IsTrue(primeira.Estimativa.QuilometragemPendente);
        Assert.AreEqual(0, await db.PrecosCombustivel.CountAsync());
        var comSeguro = comando with { Seguros = [TipoSeguro.Cliente] };
        var erro = await Assert.ThrowsExactlyAsync<ValidacaoException>(() => servico.EstimarAsync(comSeguro, default));
        Assert.AreEqual("seguros.cliente", erro.Erros.Single().Campo);
        db.Seguros.Add(SeguroConfiguracao.Criar(TipoSeguro.Cliente, 0m, dados.Empresa, dados.Agora));
        await db.SaveChangesAsync();
        var valores = (await servico.EstimarAsync(comSeguro, default)).Estimativa;
        Assert.AreEqual("0.00", valores.Adicionais.Single(x => x.Categoria == CategoriaAdicional.SeguroCliente).Valor);
        Assert.AreEqual(0, await db.Alugueis.CountAsync());
        Assert.AreEqual(0, await db.ContratosSnapshot.CountAsync());
    }

    internal sealed class RelogioLocacaoTeste(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora { get; set; } = agora;
    }

    internal static async Task<DadosLocacao> CriarCadastros(LocadoraDbContext db, IContextoEmpresa contexto, Guid? existente = null)
    {
        var agora = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        var empresa = new Empresa { Id = existente ?? Guid.NewGuid(), CriadaEm = agora };
        contexto.Definir(empresa.Id);
        var grupo = new Grupo { EmpresaId = empresa.Id, Nome = "Frota de teste", CriadoEm = agora, AtualizadoEm = agora };
        var plano = Plano.Criar(grupo.Id, ModalidadePlano.Diario, 100m, 2m, null, empresa.Id, agora);
        var veiculo = Veiculo.Criar(grupo.Id, "EXEMPLO", "Aurora", "Lume", "Prata", 2026,
            Combustivel.Gasolina, 50m, empresa.Id, agora);
        var cliente = Cliente.Criar(TipoCliente.PJ, "Contratante Exemplo", null, null, null, "11222333000181", "Tel",
            null, "SP", "Cidade", "Bairro", "Rua", "1", empresa.Id, agora);
        var condutor = Condutor.Criar(cliente.Id, "Condutor Exemplo", "condutor@example.invalid", "52998224725",
            "CNH-EXEMPLO", new DateOnly(2027, 11, 23), "Tel", empresa.Id, agora);
        var taxa = TaxaServico.Criar("Taxa de teste", 30m, ModalidadeTaxa.Fixa, empresa.Id, agora);
        if (!existente.HasValue) db.Empresas.Add(empresa);
        db.AddRange(grupo, plano, veiculo, cliente, condutor, taxa);
        await db.SaveChangesAsync();
        return new(empresa.Id, agora, grupo, plano, veiculo, cliente, condutor, taxa);
    }

    internal sealed record DadosLocacao(Guid Empresa, DateTimeOffset Agora, Grupo Grupo, Plano Plano, Veiculo Veiculo,
        Cliente Cliente, Condutor Condutor, TaxaServico Taxa)
    {
        public ComandoAluguel Comando() => new(Cliente.Id, Condutor.Id, Veiculo.Id, Plano.Id,
            "2026-11-24T09:00:00-03:00", 500, [Taxa.Id], []);

        public Aluguel NovoAluguel() => new()
        {
            EmpresaId = Empresa, CriadoEm = Agora, AtualizadoEm = Agora, SaidaUtc = Agora, RevisaoAtual = 1,
            ClienteId = Cliente.Id, CondutorId = Condutor.Id, VeiculoId = Veiculo.Id, PlanoId = Plano.Id,
        };

        public ContratoSnapshot NovaRevisao(Aluguel aluguel) => new()
        {
            EmpresaId = Empresa, CriadoEm = Agora, AtualizadoEm = Agora, AluguelId = aluguel.Id, Numero = 1,
            ClienteId = Cliente.Id, CondutorId = Condutor.Id, VeiculoId = Veiculo.Id, PlanoId = Plano.Id, GrupoId = Grupo.Id,
            RetornoPrevistoUtc = Agora.AddDays(1), Diarias = 1, Caucao = CalculoLocacao.Caucao,
        };

        public ItemContratado NovoItem(ContratoSnapshot revisao) => new()
        {
            EmpresaId = Empresa, CriadoEm = Agora, AtualizadoEm = Agora, RevisaoId = revisao.Id,
            TaxaId = Taxa.Id, Categoria = CategoriaAdicional.Taxa, Modalidade = Taxa.Modalidade, Nome = Taxa.Nome,
            Preco = Taxa.Preco, Valor = Taxa.Preco,
        };
    }
}
