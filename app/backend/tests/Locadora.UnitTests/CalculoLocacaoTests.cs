using Locadora.Domain;

namespace Locadora.UnitTests;

[TestClass]
public sealed class CalculoLocacaoTests
{
    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1440, 1)]
    [DataRow(1441, 2)]
    public void Diarias_medem_tempo_transcorrido_e_nao_dias_civis(int minutos, int esperado)
    {
        var saida = new DateTimeOffset(2018, 11, 3, 10, 0, 0, TimeSpan.FromHours(-3));
        var retorno = saida.AddMinutes(minutos).ToOffset(TimeSpan.FromHours(-2));
        Assert.AreEqual(esperado, CalculoLocacao.Diarias(saida, retorno));
    }

    [TestMethod]
    [DataRow(ModalidadePlano.Diario, 150, 500)]
    [DataRow(ModalidadePlano.Controlado, 250, 300)]
    [DataRow(ModalidadePlano.Controlado, 150, 200)]
    [DataRow(ModalidadePlano.Livre, 250, 200)]
    public void Plano_reproduz_exemplos_do_CA_07(ModalidadePlano modalidade, int km, int esperado)
        => Assert.AreEqual((decimal)esperado, CalculoLocacao.Plano(Parametros(modalidade), 2, km).Valor);

    [TestMethod]
    [DataRow(ModalidadePlano.Diario, true)]
    [DataRow(ModalidadePlano.Controlado, true)]
    [DataRow(ModalidadePlano.Livre, false)]
    public void Estimativa_nao_substitui_quilometragem_desconhecida_por_zero(ModalidadePlano modalidade, bool pendente)
    {
        var parcela = CalculoLocacao.Plano(Parametros(modalidade), 2, null);
        Assert.AreEqual(200m, parcela.Valor);
        Assert.AreEqual(pendente, parcela.QuilometragemPendente);
        Assert.IsNull(parcela.Quilometragem);
        Assert.AreEqual(modalidade == ModalidadePlano.Controlado ? 200L : (long?)null, parcela.FranquiaTotalKm);
    }

    [TestMethod]
    public void Estimativa_soma_componentes_arredondados_sem_incluir_caucao()
    {
        var inicio = DateTimeOffset.UnixEpoch;
        var plano = new ParametrosPlano(ModalidadePlano.Livre, 50.0025m, null, null);
        var estimativa = CalculoLocacao.Estimar(inicio, inicio.AddDays(2), plano,
        [
            new(Guid.NewGuid(), "Taxa fixa", CategoriaAdicional.Taxa, ModalidadeTaxa.Fixa, 1.005m),
            new(Guid.NewGuid(), "Taxa diária", CategoriaAdicional.Taxa, ModalidadeTaxa.Diaria, 1.0025m),
            new(Guid.NewGuid(), "Seguro do cliente", CategoriaAdicional.SeguroCliente, ModalidadeTaxa.Diaria, 5m),
            new(Guid.NewGuid(), "Seguro de terceiros", CategoriaAdicional.SeguroTerceiros, ModalidadeTaxa.Diaria, 7m),
        ]);
        CollectionAssert.AreEqual(new[] { 1.01m, 2.01m, 10m, 14m }, estimativa.Adicionais.Select(x => x.Valor).ToArray());
        Assert.AreEqual(100.01m, estimativa.Plano.Valor);
        Assert.AreEqual(127.03m, estimativa.ParcelasConhecidas);
        Assert.AreEqual(1000m, estimativa.Caucao);
        Assert.IsTrue(estimativa.Incompleta);
        Assert.IsFalse(estimativa.Plano.QuilometragemPendente);
    }

    private static ParametrosPlano Parametros(ModalidadePlano modalidade) =>
        new(modalidade, 100m, modalidade == ModalidadePlano.Livre ? null : 2m,
            modalidade == ModalidadePlano.Controlado ? 100 : null);
}
