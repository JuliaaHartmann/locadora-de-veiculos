using Locadora.Application;
using Locadora.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed class ProtocoloEscrita(
    LocadoraDbContext db,
    IContextoEmpresa contexto,
    IRelogio relogio) : IProtocoloEscrita
{
    private Guid? correlacaoId;
    public Guid EmpresaId => contexto.EmpresaId
        ?? throw new UnauthorizedAccessException("Contexto de empresa ausente.");

    public Guid? UsuarioId => contexto.UsuarioId;

    public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        correlacaoId = Guid.NewGuid();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM Empresas WITH (UPDLOCK, HOLDLOCK) WHERE Id = {EmpresaId}", cancellationToken);
            var resultado = await operacao(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return resultado;
        }
        finally { correlacaoId = null; }
    }

    public void Auditar(string evento, string entidadeTipo, Guid entidadeId, params string[] campos)
    {
        db.Auditorias.Add(new Auditoria
        {
            EmpresaId = EmpresaId,
            UsuarioId = UsuarioId,
            Evento = evento,
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            CorrelacaoId = correlacaoId
                ?? throw new InvalidOperationException("Auditoria exige uma operação transacional ativa."),
            CamposAlterados = campos.Length == 0 ? null : string.Join(',', campos),
            OcorridoEm = relogio.Agora,
        });
    }
}

internal static class SqlErrors
{
    public static bool Duplicidade(Exception erro)
    {
        var sql = erro as SqlException ?? erro.InnerException as SqlException;
        return sql is not null && sql.Number is 2601 or 2627;
    }
}
