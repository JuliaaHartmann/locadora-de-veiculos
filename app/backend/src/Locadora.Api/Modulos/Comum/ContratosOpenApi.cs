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
            if (context.JsonTypeInfo.Type == typeof(Controllers.ConfirmacaoDevolucaoRequisicao))
            {
                schema.Required ??= new HashSet<string>();
                schema.Required.UnionWith(["retornoReal", "hodometroFinal", "litrosRestantes", "versaoAceita"]);
            }
            if (nome is "preco" or "precoDiario" or "precoKm" or "precoPorLitro")
            {
                schema.Pattern = @"^[0-9]{1,13}(\.[0-9]{1,6})?$";
                schema.Description = "Decimal textual não negativo, com ponto e até 6 casas; máximo 9999999999999.999999. Ausência não equivale a zero.";
            }
            if (nome == "capacidadeLitros")
            {
                schema.Pattern = @"^[0-9]{1,9}(\.[0-9]{1,3})?$";
                schema.Description = "Decimal textual positivo, até 3 casas; de 0.001 a 999999999.999 litros.";
            }
            if (nome is "litrosRestantes" or "litrosARepor")
            {
                schema.Pattern = @"^[0-9]{1,9}(\.[0-9]{1,3})?$";
                schema.Description = "Decimal textual não negativo com ponto e até 3 casas.";
            }
            if (nome is "parcelasConhecidas" or "valorPlano" or "caucao" or "valor" or "totalFinal" or "total" &&
                context.JsonTypeInfo.Type == typeof(string))
                schema.Pattern = @"^[0-9]+\.[0-9]{2}$";
            if (nome == "versaoAceita") schema.Description = "Comparador opaco devolvido pela prévia, sem efeito de reserva.";
            if (nome == "retornoReal") schema.Description = "Data/hora ISO 8601 com offset explícito de America/Sao_Paulo.";
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(async (operation, context, ct) =>
        {
            var rota = context.Description.RelativePath ?? "";
            if (!new[] { "grupos", "planos", "veiculos", "clientes", "condutores",
                    "taxas-servicos", "configuracoes", "alugueis" }
                .Any(modulo => rota.StartsWith($"api/v1/{modulo}", StringComparison.Ordinal)))
                return;

            var problema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: ct);
            var locacao = rota.StartsWith("api/v1/alugueis", StringComparison.Ordinal);
            var previa = rota.EndsWith("/estimativas", StringComparison.Ordinal);
            var concluir = locacao && rota.EndsWith("/devolucao", StringComparison.Ordinal);
            var ocultar = locacao && context.Description.HttpMethod == "DELETE";
            var foto = rota.EndsWith("/foto", StringComparison.Ordinal);
            foreach (var (codigo, descricao) in new[]
            {
                ("400", "Dados inválidos; errors associa mensagens aos campos."),
                ("401", "Sessão inválida ou revogada."), ("403", "Acesso negado."),
                ("404", "Cadastro inexistente, excluído ou de outra empresa."),
                ("409", concluir ? "Estado, versão aceita da prévia ou chave idempotente em conflito." :
                    ocultar ? "Somente aluguel concluído pode ser ocultado." : "Duplicidade ou vínculo impeditivo."),
                ("412", "Versão divergente; recarregue para reavaliar, sem repetição automática."),
                ("428", "If-Match obrigatório na escrita condicional."),
                ("503", concluir ? "Dependência indisponível ou devolução comprovadamente não gravada (devolucao_nao_gravada)." :
                    foto ? "Imagem, processamento ou armazenamento indisponível; foto_nao_gravada indica falha confirmada antes da ativação." : "Dependência SQL indisponível."),
                ("413", "Corpo multipart completo acima de 5 MiB."),
                ("415", "Tipo de mídia não aceito."),
            })
            {
                if ((codigo is "413" or "415") && (!foto || context.Description.HttpMethod != "PUT")) continue;
                if (foto && context.Description.HttpMethod == "GET" && codigo is ("400" or "409" or "412" or "428")) continue;
                if (locacao && (((codigo is "412" or "428") && context.Description.HttpMethod is not ("PUT" or "DELETE") && !concluir) ||
                    ((codigo is "404" or "409") && rota == "api/v1/alugueis" && context.Description.HttpMethod == "GET")))
                    continue;
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
            if (concluir)
                operation.Description += " Replay da mesma intenção retorna o resultado e ETag gravados antes de verificar a revisão atual.";
            if (ocultar)
                operation.Description += " Oculta somente concluídos; preserva o contrato, o demonstrativo e a disponibilidade.";
            if (foto && context.Description.HttpMethod == "PUT")
            {
                operation.Description += " JPEG ou PNG estático, no máximo 16 milhões de pixels. O limite de 5 MiB inclui o multipart inteiro.";
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Required = new HashSet<string> { "arquivo" },
                                Properties = new Dictionary<string, IOpenApiSchema>
                                {
                                    ["arquivo"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                                },
                            },
                        },
                    },
                };
            }
            if (foto && context.Description.HttpMethod == "GET" && operation.Responses!["200"] is OpenApiResponse leitura)
            {
                leitura.Content = new[] { "image/jpeg", "image/png" }.ToDictionary(x => x, _ => new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                });
                leitura.Headers = new Dictionary<string, IOpenApiHeader>
                {
                    ["Cache-Control"] = new OpenApiHeader { Description = "private, no-store", Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                    ["X-Content-Type-Options"] = new OpenApiHeader { Description = "nosniff", Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                };
            }
            foreach (var parametro in (operation.Parameters ?? []).OfType<OpenApiParameter>())
            {
                if (parametro.Name == "If-Match")
                {
                    parametro.Required = true;
                    parametro.Description = "ETag forte, entre aspas, obtido na última leitura do recurso.";
                }
                if (parametro.Name == "Idempotency-Key")
                {
                    parametro.Required = true;
                    parametro.Description = "Chave da operação: reenvie a mesma chave somente para a mesma intenção.";
                }
                if (parametro.Name is "page" or "pageSize")
                    parametro.Description = parametro.Name == "page"
                        ? "Página a partir de 1; padrão 1."
                        : "Itens por página: 1 a 100; padrão 20.";
                if (locacao && parametro.Name == "estado")
                    parametro.Description = "EmAberto ou Concluido. Omitido: ambas as situações.";
                if (locacao && parametro.Name == "incluirOcultos")
                    parametro.Description = "Padrão falso. true inclui concluídos ocultos da empresa na consulta.";
            }
            foreach (var resposta in operation.Responses!.Where(x => x.Key is "200" or "201" || (foto && x.Key == "204")))
            {
                if (resposta.Value is not OpenApiResponse dadosResposta) continue;
                if (foto && context.Description.HttpMethod == "GET") continue;
                if (locacao && previa) continue;
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
