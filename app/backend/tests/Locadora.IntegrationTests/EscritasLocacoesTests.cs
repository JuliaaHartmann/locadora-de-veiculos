using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Locadora.IntegrationTests.PersistenciaLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class EscritasLocacoesTests
{
    [TestMethod]
    public async Task Confirmacao_recalcula_e_apenas_uma_conexao_ocupa_veiculo()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (dados, ator) = await Preparar(fabrica);
        var comando = dados.Comando();
        var previa = await ComServico(fabrica, dados, ator, s => s.EstimarAsync(comando, default));
        Assert.AreEqual("130.00", previa.Estimativa.ParcelasConhecidas);
        await ComBanco(fabrica, dados, ator, async db =>
        {
            var plano = await db.Planos.SingleAsync();
            plano.AlterarParametros(140m, 3m, null, dados.Agora);
            await db.SaveChangesAsync();
        });

        async Task<RespostaComVersao<AluguelDetalhe>?> Tentar(string chave)
        {
            try
            {
                return await ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, chave, default),
                    dados.Agora.AddMinutes(1));
            }
            catch (ConflitoException erro)
            {
                Assert.AreEqual("veiculoId", erro.Campo);
                return null;
            }
        }
        var resultados = await Task.WhenAll(Tentar("corrida-A"), Tentar("corrida-B"));
        var sucesso = resultados.Single(x => x is not null)!;
        Assert.AreEqual("170.00", sucesso.Valor.Estimativa!.ParcelasConhecidas);
        Assert.AreEqual(dados.Agora.AddMinutes(1), sucesso.Valor.Saida);
        await ComBanco(fabrica, dados, ator, async db =>
        {
            Assert.AreEqual(1, await db.Alugueis.CountAsync());
            Assert.AreEqual(1, await db.ContratosSnapshot.CountAsync());
            Assert.AreEqual(1, await db.IdempotenciaAlugueis.CountAsync());
            var evento = await db.Auditorias.SingleAsync(x => x.Evento == "aluguel.criado");
            Assert.AreEqual(ator, evento.UsuarioId);
            Assert.AreEqual(dados.Empresa, evento.EmpresaId);
            Assert.AreEqual(sucesso.Valor.Id, evento.EntidadeId);
            Assert.IsNotNull(evento.CorrelacaoId);
            Assert.DoesNotContain("CNH-EXEMPLO", evento.CamposAlterados!);
        });
    }

    [TestMethod]
    public async Task Falha_da_auditoria_reverte_e_mesma_chave_pode_confirmar_depois()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (dados, ator) = await Preparar(fabrica);
        var comando = dados.Comando();
        await ComBanco(fabrica, dados, ator, async db => await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Auditorias ADD CONSTRAINT CK_TesteFalhaLocacao CHECK (Evento <> N'aluguel.criado');"));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, "repetir", default)));
        await ComBanco(fabrica, dados, ator, async db =>
        {
            Assert.AreEqual(0, await db.Alugueis.CountAsync());
            Assert.AreEqual(0, await db.ContratosSnapshot.CountAsync());
            Assert.AreEqual(0, await db.ItensContratados.CountAsync());
            Assert.AreEqual(0, await db.IdempotenciaAlugueis.CountAsync());
            Assert.IsFalse(await db.Auditorias.AnyAsync(x => x.Evento == "aluguel.criado"));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Auditorias DROP CONSTRAINT CK_TesteFalhaLocacao;");
        });
        var resultado = await ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, "repetir", default));
        Assert.AreEqual(dados.Veiculo.Id, resultado.Valor.Veiculo.Id);
    }

    [TestMethod]
    public async Task Repeticao_de_criacao_preserva_retorno_original_depois_da_revisao()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (dados, ator) = await Preparar(fabrica);
        var comando = dados.Comando() with { Seguros = [TipoSeguro.Cliente] };
        Guid taxaNova = default;
        await ComBanco(fabrica, dados, ator, async db =>
        {
            var seguro = SeguroConfiguracao.Criar(TipoSeguro.Cliente, 5m, dados.Empresa, dados.Agora);
            var taxa = TaxaServico.Criar("Nova taxa", 8m, ModalidadeTaxa.Diaria, dados.Empresa, dados.Agora);
            db.AddRange(seguro, taxa);
            await db.SaveChangesAsync();
            taxaNova = taxa.Id;
        });
        var criado = await ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, "original", default));
        var replay = await ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, "original", default));
        Assert.AreEqual(criado.Etag, replay.Etag);
        Assert.AreEqual(criado.Valor.Id, replay.Valor.Id);
        Assert.AreEqual("135.00", replay.Valor.Estimativa!.ParcelasConhecidas);
        var conflito = await Assert.ThrowsExactlyAsync<ConflitoException>(() =>
            ComServico(fabrica, dados, ator, s => s.CriarAsync(comando with { HodometroInicial = 25 }, "original", default)));
        Assert.AreEqual("idempotencyKey", conflito.Campo);
        await Assert.ThrowsExactlyAsync<ConflitoException>(() => ComServico(fabrica, dados, ator, s =>
            s.CriarAsync(comando with { RetornoPrevisto = "data inválida" }, "original", default)));
        await ComBanco(fabrica, dados, ator, async db =>
        {
            (await db.Planos.SingleAsync()).AlterarParametros(160m, 3m, null, dados.Agora);
            (await db.Seguros.SingleAsync()).AlterarPreco(15m, dados.Agora);
            (await db.Clientes.SingleAsync()).Nome = "Nome atual diferente do snapshot";
            (await db.TaxasServico.SingleAsync(x => x.Id == taxaNova)).Atualizar("Nova taxa", 10m, ModalidadeTaxa.Diaria, dados.Agora);
            await db.SaveChangesAsync();
        });
        var revisao = await ComServico(fabrica, dados, ator, s => s.EditarAsync(criado.Valor.Id,
            comando with { TaxaIds = [dados.Taxa.Id, taxaNova] }, criado.Etag, default));
        Assert.AreNotEqual(criado.Etag, revisao.Etag);
        Assert.AreEqual(criado.Valor.Saida, revisao.Valor.Saida);
        var revisada = revisao.Valor.Estimativa!;
        Assert.AreEqual("100", revisada.Plano.PrecoDiario);
        Assert.AreEqual("5", revisada.Adicionais.Single(x => x.Categoria == CategoriaAdicional.SeguroCliente).Preco);
        Assert.AreEqual("145.00", revisada.ParcelasConhecidas);
        Assert.AreEqual(criado.Valor.Cliente.Nome, revisao.Valor.Cliente.Nome);
        await Assert.ThrowsExactlyAsync<PrecondicaoException>(() => ComServico(fabrica, dados, ator, s =>
            s.EditarAsync(criado.Valor.Id, comando, criado.Etag, default)));
        await Assert.ThrowsExactlyAsync<ValidacaoException>(() => ComServico(fabrica, dados, ator, s =>
            s.EditarAsync(criado.Valor.Id, comando with { HodometroInicial = -1 }, revisao.Etag, default)));
        var repetido = await ComServico(fabrica, dados, ator, s => s.CriarAsync(comando, "original", default));
        Assert.AreEqual(criado.Etag, repetido.Etag);
        Assert.AreEqual("135.00", repetido.Valor.Estimativa!.ParcelasConhecidas);
        await ComBanco(fabrica, dados, ator, async db =>
        {
            Assert.AreEqual(1, await db.Alugueis.CountAsync());
            Assert.AreEqual(2, await db.ContratosSnapshot.CountAsync());
            Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.criado"));
            Assert.AreEqual(1, await db.Auditorias.CountAsync(x => x.Evento == "aluguel.editado"));
        });
    }

    [TestMethod]
    public async Task Mesma_chave_nao_colide_entre_empresas_e_nao_revela_outro_contrato()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (a, atorA) = await Preparar(fabrica);
        var (b, atorB) = await Preparar(fabrica);
        var primeira = await ComServico(fabrica, a, atorA, s => s.CriarAsync(a.Comando(), "mesma-chave", default));
        var outra = await ComServico(fabrica, b, atorB, s => s.CriarAsync(b.Comando(), "mesma-chave", default));
        Assert.AreNotEqual(primeira.Valor.Id, outra.Valor.Id);
        Assert.IsNull(await ComServico(fabrica, b, atorB, s => s.ObterAsync(primeira.Valor.Id, default)));
        await Assert.ThrowsExactlyAsync<NaoEncontradoException>(() => ComServico(fabrica, b, atorB, s =>
            s.EstimarAsync(b.Comando() with { VeiculoId = a.Veiculo.Id }, default)));
        await ComBanco(fabrica, a, atorA, async db =>
        {
            var chave = await db.IdempotenciaAlugueis.SingleAsync();
            Assert.AreEqual(atorA, chave.UsuarioId);
            Assert.AreEqual("criar", chave.Operacao);
            Assert.HasCount(32, chave.HashComando);
        });
    }

    [TestMethod]
    public async Task Troca_incompativel_nao_muda_ocupacao_e_corrida_serializa_troca_com_criacao()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (dados, ator) = await Preparar(fabrica);
        var grupo = new Grupo { EmpresaId = dados.Empresa, Nome = "Outro grupo", CriadoEm = dados.Agora, AtualizadoEm = dados.Agora };
        var plano = Plano.Criar(grupo.Id, ModalidadePlano.Livre, 200m, null, null, dados.Empresa, dados.Agora);
        var novoVeiculo = Veiculo.Criar(grupo.Id, "NOVO", "Aurora", "Teste", "Prata", 2026, Combustivel.Gasolina,
            50m, dados.Empresa, dados.Agora);
        await ComBanco(fabrica, dados, ator, async db => { db.AddRange(grupo, plano, novoVeiculo); await db.SaveChangesAsync(); });
        var criado = await ComServico(fabrica, dados, ator, s => s.CriarAsync(dados.Comando(), "ocupado", default));
        var errado = dados.Comando() with { VeiculoId = novoVeiculo.Id };
        var conflito = await Assert.ThrowsExactlyAsync<ConflitoException>(() =>
            ComServico(fabrica, dados, ator, s => s.EditarAsync(criado.Valor.Id, errado, criado.Etag, default)));
        Assert.AreEqual("planoId", conflito.Campo);

        var certo = errado with { PlanoId = plano.Id };
        async Task<bool> Tentar(Func<AluguelService, Task<RespostaComVersao<AluguelDetalhe>>> executar)
        {
            try { await ComServico(fabrica, dados, ator, executar); return true; }
            catch (ConflitoException erro) { Assert.AreEqual("veiculoId", erro.Campo); return false; }
        }
        var corrida = await Task.WhenAll(Tentar(s => s.EditarAsync(criado.Valor.Id, certo, criado.Etag, default)),
            Tentar(s => s.CriarAsync(certo, "contra-troca", default)));
        Assert.AreNotEqual(corrida[0], corrida[1]);
        await ComBanco(fabrica, dados, ator, async db =>
        {
            var atual = await db.Alugueis.SingleAsync(x => x.Id == criado.Valor.Id);
            Assert.AreEqual(corrida[0] ? novoVeiculo.Id : dados.Veiculo.Id, atual.VeiculoId);
            Assert.AreEqual(1, await db.Alugueis.CountAsync(x => x.VeiculoId == novoVeiculo.Id));
            Assert.AreEqual(corrida[0] ? 2 : 1, atual.RevisaoAtual);
        });
    }

    [TestMethod]
    public async Task Lista_pagina_por_saida_e_snapshot_antigo_nao_herda_placa_reutilizada()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        var (dados, ator) = await Preparar(fabrica);
        var original = await ComServico(fabrica, dados, ator, s => s.CriarAsync(dados.Comando(), "primeiro", default));
        Guid segundoVeiculo = default;
        await ComBanco(fabrica, dados, ator, async db =>
        {
            var novo = Veiculo.Criar(dados.Grupo.Id, "NOVO", "Aurora", "Segundo", "Prata", 2026,
                Combustivel.Gasolina, 50m, dados.Empresa, dados.Agora);
            db.Veiculos.Add(novo); await db.SaveChangesAsync(); segundoVeiculo = novo.Id;
        });
        await ComServico(fabrica, dados, ator, s => s.EditarAsync(original.Valor.Id,
            dados.Comando() with { VeiculoId = segundoVeiculo }, original.Etag, default));
        Guid reutilizado = default;
        await ComBanco(fabrica, dados, ator, async db =>
        {
            var antigo = await db.Veiculos.SingleAsync(x => x.Id == dados.Veiculo.Id);
            antigo.ExcluidoEmUtc = dados.Agora;
            await db.SaveChangesAsync();
            var novo = Veiculo.Criar(dados.Grupo.Id, dados.Veiculo.Placa, "Outra marca", "Terceiro",
                "Prata", 2026, Combustivel.Gasolina, 50m, dados.Empresa, dados.Agora);
            db.Veiculos.Add(novo); await db.SaveChangesAsync(); reutilizado = novo.Id;
        });
        var segundo = await ComServico(fabrica, dados, ator, s => s.CriarAsync(
            dados.Comando() with { VeiculoId = reutilizado }, "nova-identidade", default), dados.Agora.AddMinutes(1));
        var primeiraPagina = await ComServico(fabrica, dados, ator, s => s.ListarAsync(1, 1, null, null, null, default));
        var segundaPagina = await ComServico(fabrica, dados, ator, s => s.ListarAsync(2, 1, null, null, null, default));
        Assert.AreEqual(2, primeiraPagina.Total);
        Assert.AreEqual(segundo.Valor.Id, primeiraPagina.Items.Single().Id);
        Assert.AreEqual(original.Valor.Id, segundaPagina.Items.Single().Id);
        Assert.AreEqual(segundoVeiculo, segundaPagina.Items.Single().VeiculoId);
        var replay = await ComServico(fabrica, dados, ator, s => s.CriarAsync(dados.Comando(), "primeiro", default));
        Assert.AreEqual(dados.Veiculo.Id, replay.Valor.Veiculo.Id);
        Assert.AreEqual(dados.Veiculo.Placa, replay.Valor.Veiculo.Placa);
        Assert.AreNotEqual(reutilizado, replay.Valor.Veiculo.Id);
    }

    internal static async Task<(DadosLocacao Dados, Guid Ator)> Preparar(SqlServerWebApplicationFactory fabrica)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var dados = await CriarCadastros(db, escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>());
        return (dados, await CriarAtor(db, dados.Empresa));
    }

    internal static async Task<Guid> CriarAtor(LocadoraDbContext db, Guid empresa)
    {
        var nome = $"Teste-{Guid.NewGuid():N}";
        var ator = new UsuarioIdentity { Id = Guid.NewGuid(), EmpresaId = empresa, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
        db.Users.Add(ator);
        await db.SaveChangesAsync();
        return ator.Id;
    }

    internal static async Task<T> ComServico<T>(SqlServerWebApplicationFactory fabrica, DadosLocacao dados, Guid ator,
        Func<AluguelService, Task<T>> operacao, DateTimeOffset? agora = null)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var provedor = escopo.ServiceProvider;
        provedor.GetRequiredService<IContextoEmpresa>().Definir(dados.Empresa, ator);
        return await operacao(new AluguelService(provedor.GetRequiredService<IRepositorioAlugueis>(),
            provedor.GetRequiredService<IProtocoloEscrita>(), new RelogioLocacaoTeste(agora ?? dados.Agora)));
    }

    internal static async Task ComBanco(SqlServerWebApplicationFactory fabrica, DadosLocacao dados, Guid ator,
        Func<LocadoraDbContext, Task> operacao)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(dados.Empresa, ator);
        await operacao(escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>());
    }
}
