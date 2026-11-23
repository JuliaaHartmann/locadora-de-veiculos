# Proposal

## Why

A F2 entrega os cadastros e preços, mas o administrador ainda não consegue usá-los para contratar um aluguel. A F3 habilita a jornada J-03 com estimativa verificável, disponibilidade consistente e preservação dos dados e valores acordados.

## What Changes

- Selecionar cliente PF/PJ, condutor vinculado e habilitado, veículo disponível e plano do grupo; informar retorno previsto, hodômetro inicial e adicionais. A saída é atribuída na contratação e ocorre com tanque cheio (RF-10, RF-14).
- Exibir estimativa discriminada para Diário, Controlado e Livre, com parcelas pendentes identificadas e caução de R$ 1.000,00 separada (RF-18; RN-01–06, RN-09 no recorte da contratação).
- Confirmar apenas um aluguel Em aberto por veículo, inclusive sob concorrência e repetição da mesma solicitação, sem efeitos parciais (RF-07, RF-14; RNF-03).
- Consultar lista, demonstrativo estimado e dados preservados; editar aluguel aberto com nova revisão, saída imutável e preços de itens mantidos preservados (RF-15–16).
- Efetivar os bloqueios cadastrais já especificados nas capabilities da F2 quando houver vínculo contratual real (RF-05–06, RF-08–09, RF-11, RF-19).
- Delimitar CA-06 e CA-07 ao recorte F3: sem devolução, total final, multa, reposição de combustível, ocultação ou consulta de histórico de concluídos (F4), fotos (F5) ou publicação (F6). Sem reserva, cancelamento ou movimentação financeira.

## Capabilities

### New Capabilities

- `alugueis`: estimativa, contratação, consulta e revisão de aluguéis em aberto.

### Modified Capabilities

Nenhuma. `grupos-planos-veiculos`, `pessoas` e `precos-operacionais` já especificam os bloqueios cadastrais que esta change passa a exercitar; seus textos e cenários permanecem.

## Impact

- Futuro módulo de Locações nas camadas existentes; integração com cadastros, preços, autenticação, auditoria e protocolo de escrita por empresa. Não há nova dependência proposta.
- API versionada de estimativa, criação, consulta e edição, com contratos descritos no TDD §5.2. SPA administrativa com entrada para Aluguéis, lista/detalhe e formulário de contratação/edição.
- Planejamento F3-01–F3-05, com protótipos HTML autocontidos e aprovação explícita da SDD antes de qualquer implementação (DT-21).
