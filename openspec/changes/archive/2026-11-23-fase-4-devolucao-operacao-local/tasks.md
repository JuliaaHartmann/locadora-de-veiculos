# Tarefas — F4: devolução e operação local

Em 23/11/2026, o solicitante aprovou explicitamente a SDD pelo harness (**Aprovar SDD e prosseguir**) e solicitou o apply separadamente. A aprovação anterior dos protótipos permanece registrada no design. Fontes: [proposta](proposal.md), [delta de Aluguéis](specs/alugueis/spec.md) e [design](design.md).

## 1. Domínio e prévia de devolução

- [x] 1.1 Estender o serviço puro para duração, quilometragem, plano e adicionais efetivos, mantendo estimativa da F3; verificar em MSTest retorno na saída, 24 h, 24 h + 1 min, os três planos, franquia e valores de itens fixos, diários e seguros (F4-01–02; RN-01–06; CA-07/12; DT-08/09/14).
- [x] 1.2 Calcular reposição, multa e precisão usando as entradas aplicadas; testar R$ 334,00 sem multa e R$ 354,00 com atraso sem aumentar diárias, R$ 1,005 → R$ 1,01, tanque cheio, caução separada, ausência de vigência, capacidade/litros/hodômetros inválidos e overflow (F4-02; RN-07–09; CA-08/09; DT-09/14).
- [x] 1.3 Preparar e expor prévia sem gravação, obtendo a revisão corrente, taxas elegíveis e vigência do combustível no instante do retorno; documentar entrada/versões e verificar retorno retroativo, empate, ausência com tanque cheio, CNH expirada após contratação, duplicatas e limites de escala/fuso em testes HTTP/SQL Server (F4-01–02; RF-10/13/17–18; CA-05/09/12; DT-07–10/13–14).

## 2. Persistência e confirmação

- [x] 2.1 Mapear estado concluído, final imutável, componentes e referência idempotente do fechamento com FKs compostas e índice de abertos preservado; criar migration aditiva e verificar em SQL Server banco vazio e upgrade com aluguéis abertos da F3, sem finais artificiais nem alteração do replay original (F4-01/03; RNF-03/07; DT-04/07/08/10/14).
- [x] 2.2 Orquestrar conclusão sob `ProtocoloEscrita`, com validação de revisão e versões, recálculo, transição, demonstrativo, auditoria e chave no mesmo commit; testar duas devoluções concorrentes, edição versus devolução, preço entre prévia e confirmação e rollback com veículo ainda ocupado (F4-01/04; RF-07/17; RNF-03/05; CA-09/12; DT-10/14/15).
- [x] 2.3 Expor e documentar confirmação em `app/backend/docs/contratos-f4.md` e OpenAPI, com `If-Match`, `Idempotency-Key` e ProblemDetails; testar 428/412/409, perda de resposta após commit, replay antes de ETag, chave reaproveitada para entrada diferente, outra chave sobre concluído e isolamento com IDs conhecidos (F4-01/04; RF-03/17; RNF-01–03/05; DT-07/10/13–15).

## 3. Histórico, ocultação e cadastros relacionados

- [x] 3.1 Estender lista e detalhe com estados e filtros, mantendo paginação e estimativa para aberto e lendo o final persistido para concluído; documentar DTOs e testar ordenação, filtros, inclusão explícita de ocultos e isolamento em todas as consultas, sem vazar cadastros removidos fora do snapshot (F4-03; RF-03/16/19; CA-07/10; DT-07/08/13/14).
- [x] 3.2 Implementar e documentar ocultação lógica de concluído com ETag e auditoria atômica; testar rejeição para aberto/versão desatualizada, contrato íntegro após ocultação, ausência de edição/reabertura e permanência da guarda de plano usado inclusive por aluguel oculto (F4-03–04; RF-05/17/19; RNF-05/07; CA-10; DT-08/10/13–15).
- [x] 3.3 Provar imutabilidade após mudanças em plano, seguros, combustível e taxas, exclusões permitidas, reutilização de identificadores e nova contratação do veículo liberado; verificar final e snapshots completos, vínculo original e preço de taxa diária adicional em todas as diárias efetivas (F4-03–04; RF-05/07/11–13/19; RNF-07; CA-12; DT-07–10/14).

## 4. Interface

- [x] 4.1 Implementar formulário e prévia J-04 conforme [devolução](prototypes/devolucao.html), com rótulos, referências preservadas, validação e invalidação da prévia; verificar em Vitest campos, taxa elegível, data/escala em pt-BR, estados, erro associado e foco (F4-01–02/05; RF-17/18; RNF-04; CA-08/09/11; DT-01/02/09/14).
- [x] 4.2 Integrar fechamento e recuperação ao demonstrativo definitivo da [devolução](prototypes/devolucao.html); verificar em Vitest mesma intenção após resposta incerta, recalcular quando versão de preço muda, recarregar contrato quando revisão muda, preservação de entradas e ausência de ações incompatíveis após sucesso (F4-01/04–05; RF-17; RNF-03/04; CA-09/11; DT-01/02/10/13/14).
- [x] 4.3 Ampliar lista, filtros, detalhe e ação de ocultar conforme [histórico](prototypes/historico.html); verificar em Vitest estados de consulta, rótulos estimativa/final, filtros, foco, ausência de ação indevida e inclusão explícita com final original (F4-03–05; RF-16/19; RNF-04/07; CA-07/10/11/12; DT-01/02/08/13/14).

## 5. Integração local e critérios de saída

- [x] 5.1 Executar suítes de backend em SQL Server isolado, testes e build da SPA; exercitar J-01 a J-05 por teclado em Playwright nas duas origens locais, sem foto, incluindo duas empresas, revogação, contratação, devolução e histórico (F4-04–05; CA-08/09/10/11/12; DT-14).
- [x] 5.2 Verificar paridade bidirecional da SPA com [devolução](prototypes/devolucao.html), [histórico](prototypes/historico.html) e o delta: todos os cenários visíveis representados e nenhum controle novo sem base. Registrar Tailwind nativo versus tokens vendorizados, e verificar foco, contraste amostral, zoom de 200% e reflow a 320 px (F4-05; RNF-04; DT-01/02/14/21).
- [x] 5.3 Registrar evidências reais, comandos, ambiente, limitações e critérios de saída F4-01–05; revalidar a change estritamente, verificar contrato e documentação e atualizar o checklist do TDD apenas onde houver evidência, sem declarar homologação ou v1 concluída (DT-21).
