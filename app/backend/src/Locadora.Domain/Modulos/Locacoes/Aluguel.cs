namespace Locadora.Domain;

public enum EstadoAluguel
{
    EmAberto = 0,
    Concluido = 1,
}

public sealed class Aluguel : EntidadeEmpresa
{
    public EstadoAluguel Estado { get; set; }
    public Guid ClienteId { get; set; }
    public Guid CondutorId { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid PlanoId { get; set; }
    public DateTimeOffset SaidaUtc { get; init; }
    public int RevisaoAtual { get; set; }
    public ICollection<ContratoSnapshot> Revisoes { get; set; } = [];

    public void Aplicar(ContratoSnapshot revisao, DateTimeOffset agora)
    {
        ClienteId = revisao.ClienteId;
        CondutorId = revisao.CondutorId;
        VeiculoId = revisao.VeiculoId;
        PlanoId = revisao.PlanoId;
        RevisaoAtual = revisao.Numero;
        AtualizadoEm = agora;
    }
}

public sealed class ContratoSnapshot : EntidadeEmpresa
{
    public Guid AluguelId { get; set; }
    public int Numero { get; set; }
    public Guid ClienteId { get; set; }
    public TipoCliente ClienteTipo { get; set; }
    public string ClienteNome { get; set; } = "";
    public string ClienteDocumento { get; set; } = "";
    public Guid CondutorId { get; set; }
    public string CondutorNome { get; set; } = "";
    public string CondutorCpf { get; set; } = "";
    public string CondutorCnh { get; set; } = "";
    public DateOnly ValidadeCnh { get; set; }
    public Guid VeiculoId { get; set; }
    public string VeiculoPlaca { get; set; } = "";
    public string VeiculoMarca { get; set; } = "";
    public string VeiculoModelo { get; set; } = "";
    public Combustivel Combustivel { get; set; }
    public decimal CapacidadeLitros { get; set; }
    public Guid GrupoId { get; set; }
    public string GrupoNome { get; set; } = "";
    public Guid PlanoId { get; set; }
    public ModalidadePlano ModalidadePlano { get; set; }
    public decimal PrecoDiario { get; set; }
    public decimal? PrecoKm { get; set; }
    public int? FranquiaDiariaKm { get; set; }
    public DateTimeOffset RetornoPrevistoUtc { get; set; }
    public int HodometroInicial { get; set; }
    public int Diarias { get; set; }
    public decimal ValorPlano { get; set; }
    public long? FranquiaTotalKm { get; set; }
    public decimal ParcelasConhecidas { get; set; }
    public decimal Caucao { get; set; }
    public ICollection<ItemContratado> Itens { get; set; } = [];
}

public sealed class ItemContratado : EntidadeEmpresa
{
    public Guid RevisaoId { get; set; }
    public Guid? TaxaId { get; set; }
    public Guid? SeguroId { get; set; }
    public string Nome { get; set; } = "";
    public CategoriaAdicional Categoria { get; set; }
    public ModalidadeTaxa Modalidade { get; set; }
    public decimal Preco { get; set; }
    public decimal Valor { get; set; }
}

public sealed class RegistroIdempotenciaAluguel : EntidadeEmpresa
{
    public Guid UsuarioId { get; set; }
    public string Operacao { get; set; } = "";
    public string Chave { get; set; } = "";
    public byte[] HashComando { get; set; } = [];
    public Guid RevisaoId { get; set; }
    public Guid? FinalId { get; set; }
    public byte[] VersaoOriginal { get; set; } = [];
}
