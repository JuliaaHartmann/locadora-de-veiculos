using Locadora.Domain;

namespace Locadora.UnitTests;

[TestClass]
public sealed class CalculoDevolucaoTests
{
    private static readonly DateTimeOffset Saida = new(2026, 11, 23, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Previsto = Saida.AddHours(30);
    private static readonly PrecoReposicao Gasolina = new(Guid.NewGuid(), Combustivel.Gasolina, 6m, Saida.AddDays(-1), 1);
    private static readonly ParametrosPlano Livre = new(ModalidadePlano.Livre, 100m, null, null);

    [TestMethod]
    [DataRow(0, 334)]
    [DataRow(1, 354)]
    public void CA08_discrimina_parcelas_e_caucao_sem_aumentar_diarias_pelo_atraso(int atraso, int total)
    {
        var final = CalculoLocacao.CalcularDevolucao(Saida, Previsto, Previsto.AddMinutes(atraso), 21_430, 21_680, Livre,
            [
                new(Guid.NewGuid(), "Preparação", CategoriaAdicional.Taxa, ModalidadeTaxa.Fixa, 30m),
                new(Guid.NewGuid(), "Suporte", CategoriaAdicional.Taxa, ModalidadeTaxa.Diaria, 10m),
                new(Guid.NewGuid(), "Seguro do cliente", CategoriaAdicional.SeguroCliente, ModalidadeTaxa.Diaria, 5m),
                new(Guid.NewGuid(), "Seguro de terceiros", CategoriaAdicional.SeguroTerceiros, ModalidadeTaxa.Diaria, 7m),
            ], Gasolina.Tipo, 50m, 40m, Gasolina);
        Assert.AreEqual(2, final.Parcelas.Diarias);
        Assert.AreEqual(250, final.Parcelas.KmRodados);
        Assert.AreEqual(200m, final.Parcelas.Plano.Valor);
        CollectionAssert.AreEqual(new[] { 30m, 20m, 10m, 14m }, final.Parcelas.Adicionais.Select(x => x.Valor).ToArray());
        Assert.AreEqual(10m, final.LitrosARepor);
        Assert.AreEqual(60m, final.Combustivel.Valor);
        Assert.AreEqual(atraso == 0 ? null : (decimal?)20m, final.Multa?.Valor);
        Assert.AreEqual((decimal)total, final.Total);
        Assert.AreEqual(1000m, final.Caucao);
    }

    [TestMethod]
    public void Arredonda_subtotal_do_plano_antes_da_multa_e_cada_componente_antes_da_soma()
    {
        var final = CalculoLocacao.CalcularDevolucao(Saida, Saida.AddHours(1), Saida.AddHours(2), 0, 0,
            Livre with { PrecoDiario = 1.045m },
            [new(Guid.NewGuid(), "Fixa", CategoriaAdicional.Taxa, ModalidadeTaxa.Fixa, 1.005m)],
            Gasolina.Tipo, 50m, 49.5m, Gasolina with { PrecoPorLitro = 2.01m });
        Assert.AreEqual(1.045m, final.Parcelas.Plano.Diarias);
        Assert.AreEqual(1.05m, final.Parcelas.Plano.Valor);
        Assert.AreEqual(0.105m, final.Multa?.Bruto);
        Assert.AreEqual(0.11m, final.Multa?.Valor);
        Assert.AreEqual(new ComponenteMonetario(1.005m, 1.01m), final.Combustivel);
        Assert.AreEqual(1.01m, final.Parcelas.Adicionais.Single().Valor);
        Assert.AreEqual(3.18m, final.Total);
    }

    [TestMethod]
    public void Tanque_cheio_conserva_preco_e_exige_vigencia()
    {
        CalculoDevolucao Calcular(PrecoReposicao? preco) => CalculoLocacao.CalcularDevolucao(Saida, Previsto,
            Previsto, 0, 0, Livre, [], Gasolina.Tipo, 50m, 50m, preco);
        var final = Calcular(Gasolina);
        Assert.AreEqual(0m, final.LitrosARepor);
        Assert.AreEqual(0m, final.Combustivel.Valor);
        Assert.AreEqual(Gasolina, final.PrecoCombustivel);
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(null));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(Gasolina with { InicioVigenciaUtc = Previsto.AddTicks(1) }));
    }

    [TestMethod]
    public void Rejeita_medicoes_invalidas_e_valores_fora_da_precisao_final()
    {
        CalculoDevolucao Calcular(int kmInicial = 0, int kmFinal = 0, decimal capacidade = 50m, decimal restantes = 0m,
            decimal preco = 6m) => CalculoLocacao.CalcularDevolucao(Saida, Previsto, Previsto, kmInicial, kmFinal,
                Livre, [], Gasolina.Tipo, capacidade, restantes, Gasolina with { PrecoPorLitro = preco });
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(capacidade: 0m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(capacidade: 50.0001m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(capacidade: Decimais.MaximoCapacidade + .001m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(restantes: -1m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(restantes: 50.001m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(restantes: 49.0001m));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(kmInicial: -1));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(kmInicial: 1, kmFinal: 0));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(capacidade: Decimais.MaximoCapacidade,
            preco: Decimais.MaximoMonetario));
        Assert.ThrowsExactly<ValidacaoException>(() => Calcular(capacidade: 10_000m,
            preco: Decimais.MaximoMonetario)); // A parcela cabe; sua soma com o plano, não.
    }
}
