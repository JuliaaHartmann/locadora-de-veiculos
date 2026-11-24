using Locadora.Domain;

namespace Locadora.Application;

public static class LimitesFoto
{
    public const int Corpo = 5 * 1024 * 1024;
    public const int Pixels = 16_000_000;
    public const int Saida = 65 * 1024 * 1024;
    public static readonly TimeSpan Processamento = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Armazenamento = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Reserva = TimeSpan.FromMinutes(2);
}

public sealed record EntradaFoto(byte[] Bytes, string Mime);
public sealed record ImagemSaneada(ArraySegment<byte> Bytes, string Mime);
public sealed record ConteudoFoto(Stream Fluxo, string Mime, long Tamanho);

public enum TipoFalhaFoto { LimiteCorpo, Formato, Indisponivel, NaoGravada, Processamento }

public sealed class FalhaFotoException(TipoFalhaFoto tipo, string mensagem, string? campo = null)
    : Exception(mensagem)
{
    public TipoFalhaFoto Tipo { get; } = tipo;
    public string? Campo { get; } = campo;
    public static FalhaFotoException Limite() => new(TipoFalhaFoto.LimiteCorpo, "O envio completo da foto deve ter até 5 MiB.", "arquivo");
    public static FalhaFotoException Formato() => new(TipoFalhaFoto.Formato, "Selecione uma imagem JPEG ou PNG estática.", "arquivo");
    public static FalhaFotoException Indisponivel() => new(TipoFalhaFoto.Indisponivel, "A foto está temporariamente indisponível.");
    public static FalhaFotoException NaoGravada() => new(TipoFalhaFoto.NaoGravada, "Não foi possível salvar a foto. A foto anterior, se houver, foi preservada.");
    public static FalhaFotoException Ocupado() => new(TipoFalhaFoto.Processamento, "O processamento de fotos está ocupado ou excedeu o tempo disponível. Tente novamente.");
}

public interface ISaneadorFoto
{
    ImagemSaneada Sanear(EntradaFoto entrada, CancellationToken ct);
}

public interface IArmazenamentoFotos
{
    string Deposito { get; }
    Task GravarAsync(FotoObjeto objeto, ArraySegment<byte> bytes, CancellationToken ct);
    Task<Stream?> LerAsync(FotoObjeto objeto, CancellationToken ct);
    Task RemoverAsync(FotoObjeto objeto, CancellationToken ct);
}

public interface IRepositorioFotos
{
    Task<Veiculo> ValidarVeiculoAsync(Guid id, string? ifMatch, bool rastrear, CancellationToken ct);
    Task<FotoObjeto?> LerAtivaAsync(Guid veiculo, bool rastrear, CancellationToken ct);
    Task<FotoObjeto?> LerAsync(Guid id, bool rastrear, CancellationToken ct);
    Task<FotoObjeto?> SelecionarCompensacaoAsync(string deposito, DateTimeOffset agora, CancellationToken ct);
    void Adicionar(FotoObjeto foto);
    void AtualizarVersao(Veiculo veiculo, DateTimeOffset agora);
    void LimparRastreamento();
    Task SalvarAsync(CancellationToken ct);
}

public sealed class LimiteProcessamentoFotos : IDisposable
{
    internal SemaphoreSlim Vaga { get; } = new(1, 1);
    public void Dispose() => Vaga.Dispose();
}
