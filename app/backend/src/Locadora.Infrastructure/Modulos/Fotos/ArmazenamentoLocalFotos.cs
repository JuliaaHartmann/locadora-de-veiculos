using Locadora.Application;
using Locadora.Domain;

namespace Locadora.Infrastructure;

public sealed class ArmazenamentoLocalFotos(string raizPrivada) : IArmazenamentoFotos
{
    private readonly string raiz = Path.GetFullPath(raizPrivada);
    public string Deposito => "local-privado";

    private string Caminho(FotoObjeto objeto, bool pendente = false)
    {
        var prefixo = objeto.EmpresaId.ToString("N") + "/";
        if (objeto.Deposito != Deposito || !objeto.Chave.StartsWith(prefixo, StringComparison.Ordinal) ||
            !Guid.TryParseExact(objeto.Chave.AsSpan(prefixo.Length), "N", out var identificador))
            throw FalhaFotoException.Indisponivel();
        return Path.Combine(raiz, objeto.EmpresaId.ToString("N"), identificador.ToString("N") + (pendente ? ".pendente" : ""));
    }

    public async Task GravarAsync(FotoObjeto objeto, ArraySegment<byte> bytes, CancellationToken ct)
    {
        var caminho = Caminho(objeto);
        var temporario = Caminho(objeto, true);
        try
        {
            ct.ThrowIfCancellationRequested();
            var pasta = Path.GetDirectoryName(caminho)!;
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(pasta);
            else Directory.CreateDirectory(pasta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            if (File.Exists(caminho)) return; // A confirmação do hash valida uma gravação anterior da mesma chave.
            await using (var fluxo = new FileStream(temporario, FileMode.Create, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await fluxo.WriteAsync(bytes.AsMemory(), ct);
                await fluxo.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporario, caminho);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw FalhaFotoException.Indisponivel(); }
    }

    public Task<Stream?> LerAsync(FotoObjeto objeto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var fluxo = new FileStream(Caminho(objeto), FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (fluxo.Length == objeto.Tamanho) return Task.FromResult<Stream?>(fluxo);
            fluxo.Dispose();
            throw FalhaFotoException.Indisponivel();
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw FalhaFotoException.Indisponivel(); }
    }

    public Task RemoverAsync(FotoObjeto objeto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            File.Delete(Caminho(objeto, true));
            File.Delete(Caminho(objeto));
            return Task.CompletedTask;
        }
        catch (DirectoryNotFoundException) { return Task.CompletedTask; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw FalhaFotoException.Indisponivel(); }
    }
}
