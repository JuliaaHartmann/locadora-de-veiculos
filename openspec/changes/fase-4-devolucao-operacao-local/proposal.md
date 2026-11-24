# Proposta — F4: devolução e operação local

## Why

A F3 permite contratar, revisar e consultar aluguéis em aberto, mas não registrar o retorno nem conhecer o valor final. A F4 completa a operação local permitindo fechar a locação com cobrança verificável, liberar o veículo e consultar o contrato concluído sem depender dos cadastros atuais.

## What Changes

- **J-04 / F4-01–02:** no aluguel em aberto, informar retorno real, hodômetro final, litros restantes e taxas adicionais opcionais; revisar a prévia discriminada antes de confirmar. A prévia não conclui nem libera o veículo. A confirmação revalida as versões aceitas, grava o final e libera o veículo na mesma operação, com replay seguro do mesmo comando.
- **J-05 / F4-03–04:** ampliar a consulta de Aluguéis para distinguir estimativa de total final, abrir contrato e demonstrativo imutáveis e ocultar um concluído da operação sem apagá-lo. Ocultos só entram em consulta histórica explícita e autorizada.
- **F4-05:** verificar localmente as jornadas J-01 a J-05 por teclado, sem foto, preservando registro, acesso, cadastros, contratação e edição.
- Fora do escopo: fotos (F5), homologação e publicação (F6), reserva, cancelamento, reabertura, pagamento, movimentação de caução ou novos perfis. Não se introduzem dependências, serviços ou integrações.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `alugueis`: estende o mesmo contrato com devolução, cálculo final, consulta histórica e ocultação; preserva as regras de contratação, revisão e isolamento. `precos-operacionais`, `grupos-planos-veiculos`, `pessoas`, `account-access` e `tenant-isolation` são dependências de comportamento já especificado, não novos CRUDs ou novas políticas.

## Impact

- **PRD:** RF-16–19 e RN-01–09; RF-03, RF-05, RF-07 e RF-10–13 nos recortes de isolamento, disponibilidade, CNH e preços; RNF-01–05 e RNF-07. CA-08/09/12 são centrais; CA-05/07/10/11 complementam configuração, consulta, histórico e acessibilidade. Ver [PRD](../../../docs/prd.md#76-aluguéis-e-devoluções).
- **TDD:** F4-01–05 e DT-01–04, DT-07–10, DT-13–15, DT-21. Ver [TDD](../../../docs/tdd.md#8-roadmap-de-implementação). Impacta o módulo de locações em Domain, Application, Infrastructure, API e SPA, com reaproveitamento das convenções de sessão e dos cadastros das F1–F3.
- **Base verificada:** `main` e `origin/main` apontavam para `2808ff495be76b24867775c8392888b7516d8f7a` na abertura da branch; o [PR #3](https://github.com/JuliaaHartmann/locadora-de-veiculos/pull/3) consta como integrado em 23/11/2026. As evidências anteriores estão nos arquivos de [F1](../archive/2026-11-22-fase-1-fundacao-infraestrutura-local/evidencias.md), [F2](../archive/2026-11-23-fase-2-cadastros/evidencias.md) e [F3](../archive/2026-11-23-fase-3-contratacao-calculo/evidencias.md). Isso não demonstra homologação, publicação nem verificação da F4.
