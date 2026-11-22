using Locadora.Application;

namespace Locadora.UnitTests;

[TestClass]
public sealed class PoliticaSenhaTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("1234567")]
    public void Rejeita_senhas_com_menos_de_oito_caracteres(string? senha) =>
        Assert.IsFalse(PoliticaSenha.EhValida(senha));

    [TestMethod]
    public void Aceita_senha_com_oito_caracteres() =>
        Assert.IsTrue(PoliticaSenha.EhValida("12345678"));

    [TestMethod]
    public void Sessao_e_access_token_tem_duracoes_definidas()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(15), PoliticaSessao.AccessToken);
        Assert.AreEqual(TimeSpan.FromHours(8), PoliticaSessao.SessaoAbsoluta);
        Assert.AreEqual(TimeSpan.FromSeconds(30), PoliticaSessao.ToleranciaRelogio);
    }

    [TestMethod]
    public void Expiracao_e_rate_limits_sao_deterministicos()
    {
        var agora = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        Assert.IsFalse(PoliticaSessao.Expirada(agora, agora.AddSeconds(1)));
        Assert.IsTrue(PoliticaSessao.Expirada(agora, agora));
        Assert.AreEqual(TimeSpan.FromMinutes(10), PoliticaRateLimit.RegistroJanela);
        Assert.AreEqual(TimeSpan.FromMinutes(1), PoliticaRateLimit.LoginJanela);
    }
}
