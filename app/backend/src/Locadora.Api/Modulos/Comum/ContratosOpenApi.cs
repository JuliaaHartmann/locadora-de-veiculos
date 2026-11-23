using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Locadora.Api;

internal static class ContratosOpenApi
{
    public static void Configurar(OpenApiOptions options)
    {
        options.AddSchemaTransformer((schema, context, ct) =>
        {
            var nome = context.JsonPropertyInfo?.Name;
            if (nome is "preco" or "precoDiario" or "precoKm")
            {
                schema.Pattern = @"^[0-9]{1,13}(\.[0-9]{1,6})?$";
                schema.Description = "Decimal textual não negativo, com ponto e até 6 casas; máximo 9999999999999.999999. Ausência não equivale a zero.";
            }
            if (nome == "capacidadeLitros")
            {
                schema.Pattern = @"^[0-9]{1,9}(\.[0-9]{1,3})?$";
                schema.Description = "Decimal textual positivo, até 3 casas; de 0.001 a 999999999.999 litros.";
            }
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(async (operation, context, ct) =>
        {
            var rota = context.Description.RelativePath ?? "";
            if (!new[] { "grupos", "planos", "veiculos", "clientes", "condutores",
                    "taxas-servicos", "configuracoes" }
                .Any(modulo => rota.StartsWith($"api/v1/{modulo}", StringComparison.Ordinal)))
                return;

            var problema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: ct);
            foreach (var (codigo, descricao) in new[]
            {
                ("400", "Dados inválidos; errors associa mensagens aos campos."),
                ("401", "Sessão inválida ou revogada."), ("403", "Acesso negado."),
                ("404", "Cadastro inexistente, excluído ou de outra empresa."),
                ("409", "Duplicidade ou vínculo impeditivo."),
                ("412", "Versão divergente; recarregue para reavaliar, sem repetição automática."),
                ("428", "If-Match obrigatório na escrita condicional."),
                ("503", "Dependência SQL indisponível."),
            })
            {
                operation.Responses![codigo] = new OpenApiResponse
                {
                    Description = descricao,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = problema },
                    },
                };
            }
            operation.Description = (operation.Description ?? "") +
                " Empresa e ator são derivados da sessão no servidor. Erros incluem code e traceId; validações incluem errors por campo.";
            foreach (var parametro in (operation.Parameters ?? []).OfType<OpenApiParameter>())
            {
                if (parametro.Name == "If-Match")
                {
                    parametro.Required = true;
                    parametro.Description = "ETag forte, entre aspas, obtido na última leitura do recurso.";
                }
                if (parametro.Name is "page" or "pageSize")
                    parametro.Description = parametro.Name == "page"
                        ? "Página a partir de 1; padrão 1."
                        : "Itens por página: 1 a 100; padrão 20.";
            }
            foreach (var resposta in operation.Responses!.Where(x => x.Key is "200" or "201"))
            {
                if (resposta.Value is not OpenApiResponse dadosResposta) continue;
                if (context.Description.HttpMethod == "GET" &&
                    !rota.Contains("{id}") && !rota.Contains("{tipo}")) continue;
                if (rota.EndsWith("/historico", StringComparison.Ordinal)) continue;
                dadosResposta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                dadosResposta.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Versão opaca forte do estado retornado, exposta por CORS.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
                if (resposta.Key == "201")
                    dadosResposta.Headers["Location"] = new OpenApiHeader
                    {
                        Description = "URL do detalhe criado.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                    };
            }
        });
    }
}
