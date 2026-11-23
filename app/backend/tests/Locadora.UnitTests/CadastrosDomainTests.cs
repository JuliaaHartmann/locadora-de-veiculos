using System.Globalization;
using Locadora.Application;
using Locadora.Domain;

namespace Locadora.UnitTests;

[TestClass]
public sealed class CadastrosDomainTests
{
    [TestMethod]
    [DataRow("nome", 200)]
    [DataRow("telefone", 32)]
    [DataRow("rg", 32)]
    [DataRow("cnh", 32)]
    [DataRow("estado", 100)]
    [DataRow("cidade", 200)]
    [DataRow("bairro", 200)]
    [DataRow("rua", 200)]
    [DataRow("numero", 40)]
    public void Cliente_rejeita_excesso_sem_truncar_e_aceita_limite(string campo, int limite)
    {
        Cliente Criar(int tamanho)
        {
            var valores = new Dictionary<string, string>
            {
                ["nome"] = "Pessoa", ["telefone"] = "Tel", ["rg"] = "RG", ["cnh"] = "CNH",
                ["estado"] = "E", ["cidade"] = "C", ["bairro"] = "B", ["rua"] = "R", ["numero"] = "1",
            };
            valores[campo] = new string('a', tamanho);
            return Cliente.Criar(TipoCliente.PF, valores["nome"], CpfValido, valores["rg"], valores["cnh"],
                null, valores["telefone"], null, valores["estado"], valores["cidade"], valores["bairro"],
                valores["rua"], valores["numero"], Guid.NewGuid(), Agora);
        }
        Assert.IsNotNull(Criar(limite));
        var erro = Assert.ThrowsExactly<ValidacaoException>(() => Criar(limite + 1));
        var campoContrato = campo is "estado" or "cidade" or "bairro" or "rua" or "numero"
            ? $"endereco.{campo}" : campo;
        Assert.IsTrue(erro.Erros.Any(x => x.Campo == campoContrato));
    }

    [TestMethod]
    public void Edicao_invalida_de_cliente_e_condutor_nao_altera_estado()
    {
        var cliente = Cliente.Criar(TipoCliente.PF, "Pessoa", CpfValido, "RG", "CNH", null, "Tel", null,
            "E", "C", "B", "R", "1", Guid.NewGuid(), Agora);
        Assert.ThrowsExactly<ValidacaoException>(() => cliente.Atualizar(TipoCliente.PF, "Outro", "123", "RG", "CNH-2",
            null, "Tel", null, "E", "C", "B", "R", "1", Agora.AddMinutes(1)));
        Assert.AreEqual("Pessoa", cliente.Nome);
        Assert.AreEqual("CNH", cliente.Cnh);
        var condutor = Condutor.Criar(cliente.Id, "Condutor", "condutor@example.invalid", CpfValido, "CNH",
            new DateOnly(2020, 1, 1), "Tel", cliente.EmpresaId, Agora);
        Assert.ThrowsExactly<ValidacaoException>(() => condutor.Atualizar(Guid.Empty, "Outro", "invalido",
            CpfValido, "CNH-2", null, "Tel", Agora.AddMinutes(1)));
        Assert.AreEqual("Condutor", condutor.Nome);
        Assert.AreEqual(cliente.Id, condutor.ClienteId);
    }

    private const string CpfValido = "52998224725";
    private const string CpfValidoAlternativo = "11144477735";
    private const string CnpjValido = "11222333000181";
    private static readonly DateTimeOffset Agora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("", null)]
    [DataRow("0", "0")]
    [DataRow("10", "10")]
    [DataRow("10.5", "10.5")]
    [DataRow("000010.500000", "10.5")]
    public void Monetario_aceita_ausencia_zero_e_formato_canonico(string? entrada, string? esperado)
    {
        var valor = Decimais.Monetario(entrada, "preco");
        Assert.AreEqual(esperado is null ? null : decimal.Parse(esperado, CultureInfo.InvariantCulture), valor);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("1,5")]
    [DataRow("1.1234567")]
    [DataRow("1e3")]
    [DataRow("1.000.000")]
    [DataRow("10000000000000")]
    [DataRow(".5")]
    [DataRow("1.")]
    [DataRow("١")]
    public void Monetario_rejeita_formato_fora_do_contrato(string entrada)
    {
        var erro = Assert.ThrowsExactly<ValidacaoException>(() => Decimais.Monetario(entrada, "preco"));
        Assert.AreEqual("preco", erro.Erros.Single().Campo);
    }

    [TestMethod]
    public void Monetario_rejeita_valor_acima_do_limite_e_aceita_limite_exato()
    {
        Assert.AreEqual(Decimais.MaximoMonetario, Decimais.Monetario("9999999999999.999999", "preco"));
        Assert.ThrowsExactly<ValidacaoException>(() => Decimais.Monetario("10000000000000", "preco"));
    }

    [TestMethod]
    [DataRow("0", "0")]
    [DataRow("0.001", "0.001")]
    [DataRow("999999999.999", "999999999.999")]
    public void Capacidade_aceita_escala_de_tres_casas(string entrada, string esperado)
    {
        Assert.AreEqual(decimal.Parse(esperado, CultureInfo.InvariantCulture),
            Decimais.Capacidade(entrada, "capacidadeLitros"));
    }

    [TestMethod]
    [DataRow("1.0001")]
    [DataRow("1000000000")]
    [DataRow("-1")]
    public void Capacidade_rejeita_escala_limite_e_sinal(string entrada)
    {
        Assert.ThrowsExactly<ValidacaoException>(() => Decimais.Capacidade(entrada, "capacidadeLitros"));
    }

    [TestMethod]
    public void Formatacao_devolve_contrato_sem_casas_artificiais()
    {
        Assert.AreEqual("10.5", Decimais.FormatacaoMonetaria(10.5m));
        Assert.AreEqual("0", Decimais.FormatacaoMonetaria(0m));
        Assert.AreEqual("10.05", Decimais.FormatacaoMonetaria(10.05m));
        Assert.AreEqual("12.5", Decimais.FormatacaoCapacidade(12.5m));
    }

    [TestMethod]
    public void Paginacao_normaliza_padrao_e_rejeita_limites()
    {
        Assert.AreEqual((1, 20), Paginacao.Normalizar(null, null));
        Assert.AreEqual((2, 100), Paginacao.Normalizar(2, 100));
        Assert.ThrowsExactly<ValidacaoException>(() => Paginacao.Normalizar(0, 20));
        Assert.ThrowsExactly<ValidacaoException>(() => Paginacao.Normalizar(1, 0));
        Assert.ThrowsExactly<ValidacaoException>(() => Paginacao.Normalizar(1, 101));
        Assert.ThrowsExactly<ValidacaoException>(() => Paginacao.Normalizar(int.MaxValue, 100));
    }

    [TestMethod]
    public void Etag_exige_if_match_e_rejeita_versao_divergente_ou_fraca()
    {
        var versao = new byte[] { 1, 2, 3, 4 };
        Assert.ThrowsExactly<PrecondicaoObrigatoriaException>(() => Etag.Exigir(null, versao));
        Assert.ThrowsExactly<PrecondicaoObrigatoriaException>(() => Etag.Exigir("  ", versao));
        Assert.ThrowsExactly<PrecondicaoException>(() => Etag.Exigir("outra", versao));
        Assert.ThrowsExactly<PrecondicaoException>(() => Etag.Exigir($"W/\"{Etag.De(versao)}\"", versao));
        Etag.Exigir($"\"{Etag.De(versao)}\"", versao);
        Etag.Exigir(Etag.De(versao), versao);
    }

    [TestMethod]
    public void Documento_aceita_cpf_e_cnpj_validos_com_ou_sem_mascara()
    {
        Assert.IsTrue(DocumentoBrasileiro.CpfValido(CpfValido));
        Assert.IsTrue(DocumentoBrasileiro.CpfValido("529.982.247-25"));
        Assert.IsTrue(DocumentoBrasileiro.CnpjValido(CnpjValido));
        Assert.IsTrue(DocumentoBrasileiro.CnpjValido("11.222.333/0001-81"));
        Assert.AreEqual(CpfValido, DocumentoBrasileiro.Normalizar("529.982.247-25"));
    }

    [TestMethod]
    [DataRow("00000000000")]
    [DataRow("11111111111")]
    [DataRow("52998224724")]
    [DataRow("123")]
    public void Documento_rejeita_cpf_invalido(string cpf) =>
        Assert.IsFalse(DocumentoBrasileiro.CpfValido(cpf));

    [TestMethod]
    [DataRow("00000000000000")]
    [DataRow("11222333000180")]
    [DataRow("123")]
    public void Documento_rejeita_cnpj_invalido(string cnpj) =>
        Assert.IsFalse(DocumentoBrasileiro.CnpjValido(cnpj));

    [TestMethod]
    public void Grupo_exige_nome_e_limita_tamanho()
    {
        var grupo = Grupo.Criar("  Frota executiva  ", Guid.NewGuid(), Agora);
        Assert.AreEqual("Frota executiva", grupo.Nome);
        Assert.ThrowsExactly<ValidacaoException>(() => Grupo.Criar("  ", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Grupo.Criar(new string('a', 201), Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Plano_diario_exige_preco_por_km_e_ignora_franquia()
    {
        var plano = Plano.Criar(Guid.NewGuid(), ModalidadePlano.Diario, 100m, 1.5m, 50, Guid.NewGuid(), Agora);
        Assert.AreEqual(100m, plano.PrecoDiario);
        Assert.AreEqual(1.5m, plano.PrecoKm);
        Assert.IsNull(plano.FranquiaDiariaKm);
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.NewGuid(), ModalidadePlano.Diario, 100m, null, null, Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Plano_controlado_exige_franquia_e_preco_excedente()
    {
        var plano = Plano.Criar(Guid.NewGuid(), ModalidadePlano.Controlado, 100m, 2m, 200, Guid.NewGuid(), Agora);
        Assert.AreEqual(2m, plano.PrecoKm);
        Assert.AreEqual(200, plano.FranquiaDiariaKm);
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.NewGuid(), ModalidadePlano.Controlado, 100m, 2m, null, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.NewGuid(), ModalidadePlano.Controlado, 100m, 2m, -1, Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Plano_livre_nao_grava_parametros_por_km()
    {
        var plano = Plano.Criar(Guid.NewGuid(), ModalidadePlano.Livre, 100m, 9m, 9, Guid.NewGuid(), Agora);
        Assert.IsNull(plano.PrecoKm);
        Assert.IsNull(plano.FranquiaDiariaKm);
    }

    [TestMethod]
    public void Plano_rejeita_preco_negativo_grupo_vazio_e_modalidade_invalida()
    {
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.NewGuid(), ModalidadePlano.Livre, -1m, null, null, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.Empty, ModalidadePlano.Livre, 1m, null, null, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() =>
            Plano.Criar(Guid.NewGuid(), (ModalidadePlano)99, 1m, null, null, Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Veiculo_normaliza_placa_e_valida_campos()
    {
        var veiculo = Veiculo.Criar(Guid.NewGuid(), " fic-1a23 ", "Marca", "Modelo", "Azul", 2020,
            Combustivel.Gasolina, 50.5m, Guid.NewGuid(), Agora);
        Assert.AreEqual("FIC1A23", veiculo.Placa);
        Assert.AreEqual(50.5m, veiculo.CapacidadeLitros);
        Assert.ThrowsExactly<ValidacaoException>(() => Veiculo.Criar(Guid.Empty, "AAA1111", "M", "M", "C", 2020,
            Combustivel.Gasolina, 1m, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Veiculo.Criar(Guid.NewGuid(), "AAA1111", "M", "M", "C", 2020,
            Combustivel.Gasolina, 0m, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Veiculo.Criar(Guid.NewGuid(), "AAA1111", "M", "M", "C", -1,
            Combustivel.Gasolina, 1m, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Veiculo.Criar(Guid.NewGuid(), "AAA1111", "M", "M", "C", 2020,
            (Combustivel)99, 1m, Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Cliente_pf_exige_documentos_e_endereco_completo()
    {
        var cliente = Cliente.Criar(TipoCliente.PF, "Pessoa Sintética", CpfValido, "RG-1", "CNH-1",
            null, "(00) 90000-0001", null, "Estado", "Cidade", "Bairro", "Rua", "12 A", Guid.NewGuid(), Agora);
        Assert.AreEqual(CpfValido, cliente.Cpf);
        Assert.IsNull(cliente.Cnpj);
        Assert.IsNull(cliente.Email);
        Assert.AreEqual("12 A", cliente.Endereco.Numero);

        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PF, "Pessoa", null, "RG", "CNH",
            null, "Tel", null, "E", "C", "B", "R", "1", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PF, "Pessoa", CpfValido, null, "CNH",
            null, "Tel", null, "E", "C", "B", "R", "1", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PF, "Pessoa", CpfValido, "RG", null,
            null, "Tel", null, "E", "C", "B", "R", "1", Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Cliente_pj_nao_grava_documentos_de_pf()
    {
        var cliente = Cliente.Criar(TipoCliente.PJ, "Empresa Sintética", CpfValido, "RG", "CNH", CnpjValido,
            "(00) 90000-0002", "empresa@example.invalid", "E", "C", "B", "R", "1", Guid.NewGuid(), Agora);
        Assert.AreEqual(CnpjValido, cliente.Cnpj);
        Assert.IsNull(cliente.Cpf);
        Assert.IsNull(cliente.Rg);
        Assert.IsNull(cliente.Cnh);
        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PJ, "Empresa", null, null, null,
            null, "Tel", null, "E", "C", "B", "R", "1", Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Cliente_rejeita_email_invalido_e_endereco_incompleto()
    {
        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PJ, "Empresa", null, null, null,
            CnpjValido, "Tel", "invalido", "E", "C", "B", "R", "1", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Cliente.Criar(TipoCliente.PJ, "Empresa", null, null, null,
            CnpjValido, "Tel", null, "", "C", "B", "R", "1", Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Cliente_sincroniza_cnh_somente_para_pf()
    {
        var pf = Cliente.Criar(TipoCliente.PF, "Pessoa", CpfValido, "RG", "CNH-1", null, "Tel", null,
            "E", "C", "B", "R", "1", Guid.NewGuid(), Agora);
        pf.SincronizarCnh("  CNH-2  ", Agora);
        Assert.AreEqual("CNH-2", pf.Cnh);

        var pj = Cliente.Criar(TipoCliente.PJ, "Empresa", null, null, null, CnpjValido, "Tel", null,
            "E", "C", "B", "R", "1", Guid.NewGuid(), Agora);
        pj.SincronizarCnh("CNH-3", Agora);
        Assert.IsNull(pj.Cnh);
    }

    [TestMethod]
    public void Condutor_exige_email_documentos_e_validade_mas_aceita_cnh_vencida()
    {
        var vencida = new DateOnly(2020, 1, 10);
        var condutor = Condutor.Criar(Guid.NewGuid(), "Condutor Sintético", "condutor@example.invalid",
            CpfValidoAlternativo, "CNH-9", vencida, "Tel", Guid.NewGuid(), Agora);
        Assert.AreEqual(vencida, condutor.ValidadeCnh);
        Assert.AreEqual(CpfValidoAlternativo, condutor.Cpf);

        Assert.ThrowsExactly<ValidacaoException>(() => Condutor.Criar(Guid.NewGuid(), "Condutor", null,
            CpfValidoAlternativo, "CNH", vencida, "Tel", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Condutor.Criar(Guid.NewGuid(), "Condutor",
            "condutor@example.invalid", CpfValidoAlternativo, "CNH", null, "Tel", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Condutor.Criar(Guid.Empty, "Condutor",
            "condutor@example.invalid", CpfValidoAlternativo, "CNH", vencida, "Tel", Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => Condutor.Criar(Guid.NewGuid(), "Condutor",
            "condutor@example.invalid", "123", "CNH", vencida, "Tel", Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Taxa_aceita_zero_e_rejeita_ausencia_negativo_e_nome_longo()
    {
        var taxa = TaxaServico.Criar("Lavagem", 0m, ModalidadeTaxa.Fixa, Guid.NewGuid(), Agora);
        Assert.AreEqual(0m, taxa.Preco);
        Assert.ThrowsExactly<ValidacaoException>(() => TaxaServico.Criar("Lavagem", null,
            ModalidadeTaxa.Fixa, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => TaxaServico.Criar("Lavagem", -1m,
            ModalidadeTaxa.Fixa, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => TaxaServico.Criar(new string('a', 201), 1m,
            ModalidadeTaxa.Fixa, Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Seguro_aceita_zero_e_rejeita_ausencia_negativo_e_tipo_invalido()
    {
        var seguro = SeguroConfiguracao.Criar(TipoSeguro.Cliente, 0m, Guid.NewGuid(), Agora);
        Assert.AreEqual(0m, seguro.PrecoDiario);
        Assert.ThrowsExactly<ValidacaoException>(() => SeguroConfiguracao.Criar(TipoSeguro.Cliente, null,
            Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => SeguroConfiguracao.Criar(TipoSeguro.Cliente, -1m,
            Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => SeguroConfiguracao.Criar((TipoSeguro)99, 1m,
            Guid.NewGuid(), Agora));
    }

    [TestMethod]
    public void Combustivel_aceita_zero_e_rejeita_ausencia_negativo_ordem_e_tipo()
    {
        var preco = PrecoCombustivel.Criar(Combustivel.Gasolina, 0m, Agora, 1, Guid.NewGuid(), Agora);
        Assert.AreEqual(0m, preco.Preco);
        Assert.AreEqual(Agora, preco.InicioVigenciaUtc);
        Assert.ThrowsExactly<ValidacaoException>(() => PrecoCombustivel.Criar(Combustivel.Gasolina, null,
            Agora, 1, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => PrecoCombustivel.Criar(Combustivel.Gasolina, -1m,
            Agora, 1, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => PrecoCombustivel.Criar(Combustivel.Gasolina, 1m,
            Agora, 0, Guid.NewGuid(), Agora));
        Assert.ThrowsExactly<ValidacaoException>(() => PrecoCombustivel.Criar((Combustivel)99, 1m,
            Agora, 1, Guid.NewGuid(), Agora));
    }
}
