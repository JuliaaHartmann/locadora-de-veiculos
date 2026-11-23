using Locadora.Application;
using Locadora.Domain;

namespace Locadora.UnitTests;

[TestClass]
public sealed class ElegibilidadeLocacaoTests
{
    private static readonly DateTimeOffset Saida = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateOnly Dia = new(2026, 11, 23);
    private const string CpfSintetico = "52998224725";
    private const string CnpjSintetico = "11222333000181";

    [TestMethod]
    [DataRow(TipoCliente.PF)]
    [DataRow(TipoCliente.PJ)]
    public void Aceita_condutor_direto_e_CNH_no_dia_do_retorno_local(TipoCliente tipo)
    {
        var (cliente, condutor) = Dados(tipo);
        var retorno = HorarioLocacao.InterpretarRetorno("2026-11-23T23:30:00-03:00");
        Assert.AreEqual(24, retorno.UtcDateTime.Day);
        Assert.IsTrue(ElegibilidadeLocacao.CondutorVinculado(cliente, condutor));
        ElegibilidadeLocacao.Validar(cliente, condutor, 0, Saida, retorno, DateOnly.FromDateTime(retorno.DateTime));
    }

    [TestMethod]
    public void Nega_vinculo_indireto_excluido_ou_de_outra_empresa_e_plano_de_outro_grupo()
    {
        var (cliente, condutor) = Dados(TipoCliente.PJ);
        condutor.ClienteId = Guid.NewGuid();
        Assert.IsFalse(ElegibilidadeLocacao.CondutorVinculado(cliente, condutor));
        condutor.ClienteId = cliente.Id;
        condutor.ExcluidoEmUtc = Saida;
        Assert.IsFalse(ElegibilidadeLocacao.CondutorVinculado(cliente, condutor));
        condutor.ExcluidoEmUtc = null;
        condutor.EmpresaId = Guid.NewGuid();
        Assert.IsFalse(ElegibilidadeLocacao.CondutorVinculado(cliente, condutor));
        var veiculo = new Veiculo { EmpresaId = cliente.EmpresaId, GrupoId = Guid.NewGuid() };
        var plano = new Plano { EmpresaId = cliente.EmpresaId, GrupoId = Guid.NewGuid() };
        Assert.IsFalse(ElegibilidadeLocacao.PlanoCompativel(veiculo, plano));
        plano.GrupoId = veiculo.GrupoId;
        Assert.IsTrue(ElegibilidadeLocacao.PlanoCompativel(veiculo, plano));
        plano.ExcluidoEmUtc = Saida;
        Assert.IsFalse(ElegibilidadeLocacao.PlanoCompativel(veiculo, plano));
    }

    [TestMethod]
    public void Rejeita_documentos_invalidos_CNH_ausente_hodometro_e_retorno()
    {
        var (cliente, condutor) = Dados(TipoCliente.PF);
        cliente.Cpf = "0";
        condutor.Cpf = "0";
        condutor.Cnh = "";
        var erro = Assert.ThrowsExactly<ValidacaoException>(() =>
            ElegibilidadeLocacao.Validar(cliente, condutor, -1, Saida, Saida, Dia));
        CollectionAssert.AreEquivalent(new[] { "clienteId", "condutorId", "condutorId", "hodometroInicial", "retornoPrevisto" },
            erro.Erros.Select(x => x.Campo).ToArray());
    }

    [TestMethod]
    public void Rejeita_CNH_vencida_antes_do_dia_local()
    {
        var (cliente, condutor) = Dados(TipoCliente.PJ);
        condutor.ValidadeCnh = Dia.AddDays(-1);
        var erro = Assert.ThrowsExactly<ValidacaoException>(() =>
            ElegibilidadeLocacao.Validar(cliente, condutor, 5, Saida, Saida.AddHours(1), Dia));
        Assert.AreEqual("condutorId", erro.Erros.Single().Campo);
    }

    [TestMethod]
    [DataRow("2019-02-16T23:30:00")]
    [DataRow("2019-02-16T23:30:00-04:00")]
    [DataRow("2018-11-04T00:30:00-03:00")]
    [DataRow("2018-11-04T00:30:00-02:00")]
    public void Rejeita_offset_ausente_incorreto_e_horario_inexistente(string texto) =>
        Assert.ThrowsExactly<ValidacaoException>(() => HorarioLocacao.InterpretarRetorno(texto));

    [TestMethod]
    public void Desambigua_horario_repetido_com_offset_explicito()
    {
        var primeiro = HorarioLocacao.InterpretarRetorno("2019-02-16T23:30:00-02:00");
        var segundo = HorarioLocacao.InterpretarRetorno("2019-02-16T23:30:00-03:00");
        Assert.AreEqual(TimeSpan.FromHours(1), segundo - primeiro);
    }

    private static (Cliente Cliente, Condutor Condutor) Dados(TipoCliente tipo)
    {
        var empresa = Guid.NewGuid();
        var cliente = new Cliente { EmpresaId = empresa, Tipo = tipo, Cpf = CpfSintetico, Cnpj = CnpjSintetico };
        return (cliente, new Condutor
        {
            EmpresaId = empresa, ClienteId = cliente.Id, Cpf = CpfSintetico, Cnh = "CNH-EXEMPLO", ValidadeCnh = Dia,
        });
    }
}
