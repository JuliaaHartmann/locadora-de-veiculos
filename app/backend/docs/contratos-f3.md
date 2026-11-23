# Contratos HTTP — F3: contratação e cálculo

Rotas administrativas protegidas por bearer em `/api/v1`. Empresa e ator vêm da sessão, nunca da requisição. Consultas e referências de outra empresa não são reveladas. A F3 não expõe devolução, cancelamento, valor final, ocultação ou fotos.

| Método e rota | Resposta de sucesso |
| --- | --- |
| `GET /alugueis?page=1&pageSize=20&veiculoId=…&saidaDe=2026-11-23&saidaAte=2026-11-25` | `200`, envelope `items`, `page`, `pageSize`, `total`. Filtros opcionais; datas de saída inclusivas no fuso de São Paulo. Ordem por saída decrescente e ID crescente. |
| `GET /alugueis/{id}` | `200` e `ETag`: revisão corrente, com dados preservados e estimativa. |
| `POST /alugueis/estimativas` | `200`: `saidaReferencia`, `retornoPrevisto`, `estimativa`; sem gravação, reserva ou `ETag`. |
| `POST /alugueis` | `201`, `Location` e `ETag` com a revisão criada. Exige `Idempotency-Key`. O servidor atribui a saída, revalida e recalcula. |
| `PUT /alugueis/{id}` | `200` com nova revisão e `ETag`. Exige `If-Match` com a versão da última leitura; saída imutável. |

Exemplo sintético de entrada comum à estimativa, criação e edição:

```json
{
  "clienteId": "10000000-0000-0000-0000-000000000001",
  "condutorId": "10000000-0000-0000-0000-000000000002",
  "veiculoId": "10000000-0000-0000-0000-000000000003",
  "planoId": "10000000-0000-0000-0000-000000000004",
  "retornoPrevisto": "2026-11-24T09:00:00-03:00",
  "hodometroInicial": 21430,
  "taxaIds": [],
  "seguros": ["Cliente", "Terceiros"]
}
```

IDs devem pertencer a cadastros ativos da empresa. As listas de adicionais são opcionais; IDs/tipos repetidos são rejeitados. `retornoPrevisto` precisa de offset explícito correspondente a `America/Sao_Paulo`. Não enviar saída, situação, empresa ou preço. O demonstrativo discrimina plano, duração, taxas/seguros, `quilometragemPendente`, `parcelasConhecidas`, `caucao` e `incompleta`. Valores monetários são strings com ponto; parcelas arredondadas e caução têm duas casas. No Livre, não há parcela pendente por quilômetro, mas a estimativa não é um total final.

Use a mesma `Idempotency-Key` (texto ASCII visível, sem espaços nem vírgula, até 128 caracteres) apenas para repetir a mesma criação. Repetição confirmada devolve a representação e o ETag originais, mesmo se o contrato foi revisado depois; o `Location` continua apontando para o detalhe corrente. Uma entrada alterada com a mesma chave resulta em `409`. A edição só aceita o ETag atual; `428` sem `If-Match` e `412` se divergente.

Consultas auxiliares usam contratos da F2: `GET /veiculos?disponiveis=true[&aluguelAtualId={id}]`, `GET /condutores?clienteId={id}`, `GET /planos?grupoId={id}`, clientes, taxas e configurações de seguro. A lista de veículos sem filtro mantém o contrato da F2. A lista filtrada inclui o próprio veículo do aluguel informado, somente na empresa da sessão; a gravação sempre revalida disponibilidade e vínculos.

Para abrir a edição cadastral de veículo ou taxa, a SPA usa `GET /veiculos/{id}?paraEdicao=true` ou `GET /taxas-servicos/{id}?paraEdicao=true`: `409` se o vínculo com aluguel aberto já impedir a edição. A leitura comum continua disponível e o `PUT`/`DELETE` ainda confere o vínculo dentro da transação.

Falhas seguem ProblemDetails com `code`, `traceId` e, quando aplicável, `errors` por campo. `400` para erro de entrada (inclusive seguro selecionado sem preço), `401` para sessão inválida ou revogada, `404` para referência ausente/não visível, `409` para conflito de ocupação, vínculo ou idempotência e `503` para indisponibilidade SQL. CORS mantém origens exatas, sem credenciais; `ETag` é exposto para a SPA.
