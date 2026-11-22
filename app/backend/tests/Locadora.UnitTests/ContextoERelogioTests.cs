using Locadora.Infrastructure;

namespace Locadora.UnitTests;

[TestClass]
public sealed class ContextoERelogioTests
{
    [TestMethod]
    public void Contextos_distintos_nao_compartilham_empresa()
    {
        var a = new ContextoEmpresa();
        var b = new ContextoEmpresa();
        var empresa = Guid.NewGuid();
        Assert.IsNull(a.EmpresaId);
        a.Definir(empresa);
        Assert.AreEqual(empresa, a.EmpresaId);
        Assert.IsNull(b.EmpresaId);
    }

    [TestMethod]
    public void Relogio_obedece_time_provider_controlado()
    {
        var instante = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var tempo = new TempoControlado { Agora = instante };
        var relogio = new Relogio(tempo);
        Assert.AreEqual(instante, relogio.Agora);
        tempo.Agora = instante.AddHours(8);
        Assert.AreEqual(instante.AddHours(8), relogio.Agora);
    }

    private sealed class TempoControlado : TimeProvider
    {
        public DateTimeOffset Agora { get; set; }
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
