# Contratos HTTP — F4

Rotas em `/api/v1/alugueis`, com sessão administrativa Bearer. Empresa e ator vêm da sessão.
Erros seguem ProblemDetails (`code`, `traceId` e, quando couber, `errors` por campo).

## Prévia: `POST /{id}/devolucao/previa`

```json
{
  "retornoReal": "2026-11-24T16:00:00-03:00",
  "hodometroFinal": 21680,
  "litrosRestantes": "40",
  "taxaIds": []
}
```

`retornoReal` exige ISO 8601 com offset válido de `America/Sao_Paulo`; o cálculo usa UTC.
`hodometroFinal` é um inteiro JSON igual ou maior que o inicial. `litrosRestantes` é uma string
decimal com ponto e até três casas, no intervalo de zero à capacidade contratada. `taxaIds` é
opcional, com IDs distintos de taxas ativas da empresa não incluídas na revisão. A lista ativa
e paginada vem de `GET /api/v1/taxas-servicos`; a revisão identifica as taxas já contratadas.

O `200` devolve `ETag` da revisão lida e `{ versaoAceita, demonstrativo }`. O comparador opaco
vincula a revisão, a entrada canônica, a regra e as versões de taxas selecionadas e de combustível
efetivamente usadas. A vigência de combustível é escolhida pelo instante real, em ordem
decrescente de início, ordem serial e ID. A falta de vigência bloqueia mesmo com tanque cheio;
vencimento da CNH após contratar não é revalidado. A prévia não grava, reserva ou libera veículo.

O demonstrativo tem quantidades, termos do plano, adicionais individualizados, combustível,
multa quando aplicável, total e caução separada. Preços, litros e parcelas são strings decimais;
valores arredondados têm duas casas, e brutos preservam a precisão usada. Taxa adicionada no
retorno é marcada em `incluidoNoRetorno`; `quantidade` é 1 para taxa fixa e o número de diárias
efetivas para taxa diária ou seguro.

## Confirmação: `POST /{id}/devolucao`

Mesmos campos da prévia mais `versaoAceita`; cabeçalhos `If-Match` (ETag da prévia entre aspas)
e `Idempotency-Key` (até 128 caracteres ASCII imprimíveis, sem espaços ou vírgula). O `200`
devolve `{ id, estado: "Concluido", final }` e o ETag original do fechamento. `final` tem
o mesmo formato do demonstrativo da prévia, mas é lido dos valores persistidos. O contrato
concluído e a disponibilidade da frota mudam na mesma transação que auditoria e replay.

- `428`: falta `If-Match` para comando novo. `412`: versão desatualizada no aluguel aberto.
- `409`: estado incompatível (`errors.estado`), cálculo aceito mudou (`errors.versaoAceita`) ou
  mesma chave com outra intenção (`errors.idempotencyKey`). Quando o cálculo muda, deve ser
  feita nova prévia e aceitação explícita; mudanças não aplicáveis não interferem no cálculo.
- `404`: inexistente ou fora da empresa. A chave não permite consultar por outra empresa.
- `503` com `code: "devolucao_nao_gravada"`: falha de gravação identificada antes de tentar
  confirmar a transação. Nada foi gravado por esse comando. Se a resposta for incerta,
  repetir a chave, o corpo e as condições da mesma intenção, sem criar outra.

O replay é restrito a empresa, ator e operação. A comparação da mesma intenção usa o recurso,
as entradas canônicas e a versão aceita; o replay legítimo precede a precondição corrente e
retorna o resultado original sem novos efeitos, inclusive após a ocultação.

## Consultas: `GET /` e `GET /{id}`

Lista de aluguéis da empresa, ordenada por saída decrescente e ID no desempate. Mantém
`{ items, page, pageSize, total }` (20 por padrão, até 100 por página). Filtros opcionais:
`estado` (`EmAberto`, `Concluido`; omitido: ambos), `veiculoId` (identidade original) e
`saidaDe`/`saidaAte` (datas inclusivas ISO `aaaa-mm-dd` interpretadas no fuso de São Paulo).

Só `incluirOcultos=true` permite ver ocultos, tanto na lista como no detalhe por ID. Um oculto
tem `oculto: true`, sem mudar `estado: "Concluido"`; na consulta padrão não entra no envelope
e o detalhe retorna `404`. A opção desabilita apenas o filtro de ocultação do aluguel; o escopo
de empresa continua ativo. Dados de pessoas, veículo e grupo vêm da revisão preservada.
Para aberto, a lista mantém `parcelasConhecidas` e o detalhe `estimativa`. Para concluído,
a lista tem `totalFinal` e o detalhe `final`, lidos dos valores persistidos, sem recalcular
nem consultar o cadastro atual. O detalhe mantém o `ETag` do estado consultado.

## Ocultação: `DELETE /{id}`

Sem corpo; `If-Match` exige o ETag atual. Retorna `204` para concluído da empresa e grava a
ocultação e auditoria juntas. Aberto gera `409`, versão desatualizada `412`, falta de ETag `428`.
Aluguel inexistente, já ocultado ou de outra empresa retorna `404`. Ocultar não mexe no
demonstrativo, contrato, disponibilidade ou guarda de plano usado. Não há edição de concluído,
reabertura, exclusão física ou desfazer ocultação.

## Migration e recuperação

`Fase4DevolucaoOperacaoLocal` adiciona finais, componentes e referência de devolução sem
alterar a revisão, estimativa ou chave de criação preexistentes. O índice filtrado de um
aluguel aberto por veículo continua valendo. Em falha antes do commit, o veículo não é liberado.
O `Down` foi exercitado apenas em banco sintético sem fechamentos. Não rodar o `Down` em base
que contenha finais sem estratégia de preservação aprovada e restauração verificada: ele remove
as novas tabelas. Esta mudança não aplica migration a ambientes operacionais.
