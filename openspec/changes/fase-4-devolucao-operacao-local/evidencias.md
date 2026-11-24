# Evidências — F4: devolução e operação local

## 23/11/2026 — início do apply

- Branch `feat/fase-4-devolucao-operacao-local` na base `2808ff4` de `main`/`origin/main`. A atualização preexistente do registro da F3 no TDD foi preservada.
- OpenSpec CLI 2.0.0, schema `prototype-first`; `openspec validate fase-4-devolucao-operacao-local --strict` aprovou a estrutura. Aprovações da SDD e dos protótipos: [tarefas](tasks.md) e [design](design.md).
- .NET SDK 10.0.404, runtime ASP.NET Core/.NET 10.0.15, EF Core 10.0.15, CLI EF 10.0.9, MSTest 4.0.2. Docker Server 29.7.2 e Compose 5.4.0, imagem SQL Server 2022 do Compose disponível pelo digest fixado. Consultados via Context7: [concorrência/transações do EF Core](https://learn.microsoft.com/ef/core/saving/transactions), [filtros do EF Core](https://learn.microsoft.com/ef/core/querying/filters) e [metadados OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0).
- Node 24.21.0, npm 11.19.0, Angular/CLI e Signal Forms 22.3.0, TypeScript 6.0.3, Tailwind CSS/PostCSS 4.3.5, Lucide 1.77.0, Vitest 5.1.1. Referências: [Angular — versões](https://angular.dev/reference/versions), [Signal Forms](https://angular.dev/guide/forms/signals/overview), [Tailwind](https://tailwindcss.com/docs/hover-focus-and-other-states), [Vitest](https://vitest.dev/api/mock.html). Sem instalação ou atualização de dependências.

## Verificações intermediárias

| Verificação | Resultado |
| --- | --- |
| `dotnet test app/backend/tests/Locadora.UnitTests --no-restore` | 99 aprovados. Inclui três modalidades, intervalos de 24 horas, componentes de R$ 334,00 e R$ 354,00, arredondamento, tanque cheio, caução e validação de limites. |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` — prévia | 99 unitários e 77 de integração aprovados após corrigir a prioridade de validação do retorno anterior à saída sobre a ausência de preço. Prévia sem escrita; vigência retroativa e desempate; testes A/B; ausência de vigência com tanque cheio; CNH expirada após contratação. |
| Mesmo script — migration | 99 unitários e 78 de integração aprovados. Banco vazio e upgrade do schema F3 com contrato, revisão e replay idempotente preservados. Sem finais artificiais. |
| Mesmo script — conclusão e contrato HTTP | 99 unitários e 83 de integração aprovados. Corridas, preço alterado, rollback após falha na chave, 428/412/409, perda sintética da resposta após commit, replay anterior ao ETag corrente, isolamento, OpenAPI e revogação. |
| Mesmo script — consulta histórica | 99 unitários e 84 de integração aprovados. Ordenação, filtros, leitura do final persistido, ocultos só por opção e isolamento A/B. |
| Mesmo script, `-FiltroIntegracao 'FullyQualifiedName~Ocultacao_exige'` e `-FiltroIntegracao 'FullyQualifiedName~CA12_final_permanece'` | 1 integração aprovada por filtro. Ocultação com auditoria e rollback, replays mantidos e guarda do plano; 334/354 no HTTP, nova contratação do mesmo veículo, mudanças cadastrais/preços, exclusões e reuso de placa/CNPJ sem alterar contrato ou final. A primeira rodada de CA-12 enviou os seguros como enums numéricos e foi corrigida no teste para os nomes exigidos pelo contrato HTTP. |
| `dotnet ef migrations has-pending-model-changes --project src/Locadora.Infrastructure --startup-project src/Locadora.Api` em `app/backend` | Sem alterações pendentes após recompilar a migration gerada. Aviso de patch do CLI EF. Conexão apenas de design, não usada para acessar servidor. |
| `npm test -- --watch=false` e `npm run build` em `app/frontend`, antes das alterações de interface F4 | 57 testes aprovados em 8 arquivos; build de produção aprovado. Linha de base, não é evidência da interface F4. |

A verificação do SQL usa instância, porta e volume exclusivos e senhas sintéticas apenas no processo. O banco local de Development não foi usado.

## Paridade bidirecional — J-04 e J-05

Comparação dos [dois HTMLs aprovados](prototypes/) com o [delta](specs/alugueis/spec.md), a implementação de `app/frontend/src/app/locacoes/` e capturas do Chromium a 1366 px. As capturas são sintéticas e ficam fora do repositório (`Temp/opencode/f4-paridade`, quando `F4_CAPTURAS` é definido).

| Cenários dos protótipos | Representação na SPA e verificação |
| --- | --- |
| Contrato aberto e dados do retorno | A ação abre formulário com referências preservadas, rótulos, horários de São Paulo, hodômetro, litros e somente taxas elegíveis. Erros associados e valores mantidos; ausência de vigência bloqueia com tanque cheio. Verificado em Vitest e Playwright. |
| Prévia e três modalidades | Hierarquia de referências e formulário à esquerda, demonstrativo à direita; diárias, km, franquia/excedente ou ausência de cobrança, taxas e seguros, reposição, multa e caução separada. Edição invalida a confirmação. As três modalidades foram verificadas no domínio; os cenários R$ 334,00/R$ 354,00 foram exercitados também no HTTP e no navegador. |
| Confirmação, preço alterado, revisão concorrente, resposta incerta e falha sem gravação | O final somente leitura substitui a prévia; atualização de preços exige nova aceitação, revisão exige recarga explícita, falha conhecida preserva as entradas e resposta incerta mantém o mesmo envio. Vitest verificou os estados e ações; Playwright interrompeu a resposta depois do `200` real e recuperou o final sem duplicar efeitos. |
| Histórico: lista, filtros, paginação, detalhe, erro e nova tentativa | Mesmas colunas e filtros por situação, veículo, período e inclusão explícita; distingue estimativa de total final. Carregamento, vazio, erro e ETag vencido cobertos em Vitest; lista e ocultação reais verificadas em SQL e Playwright. |
| Ocultação e leitura explícita | Ação só aparece em concluído visível. A consulta padrão não revela ocultos e, após aplicar o filtro, o mesmo final é exibido com a indicação de ocultação. Sem edição ou nova devolução em concluído, nem reabertura, desfazer ocultação ou exclusão física. |

**SPA → protótipos/delta:** os novos controles correspondem às jornadas aprovadas: entrada e cálculo do retorno, confirmação/recuperação, filtros e ocultação. A nova tentativa de carregar taxas e o aviso de saída durante resultado incerto cobrem falha de consulta e proteção da intenção (delta de Aluguéis e TDD §5.4); não são novos fluxos de negócio. A navegação ativa, Nova contratação, Editar aluguel e os dados mascarados de contrato vêm da F3. Não há banners, seletores de cenário ou rotas de simulação na SPA.

**Diferenças verificadas:** protótipos incorporam os tokens de tema vendorizados de **Tailwind CSS 4.3.5** e CSS inline; a SPA usa **Tailwind CSS/PostCSS 4.3.5** nativo no build, navegação e cartões existentes. Mantém o contêiner externo, o botão secundário de filtro e os títulos da aplicação; usa GUIDs, paginação real e dados do servidor em vez dos exemplos reduzidos. O estado do aberto fica acima do cartão de resumo na devolução. A precisão opcional de segundos e fração no mesmo campo e na referência da saída é a diferença aprovada no [design](design.md#prototype). Os HTMLs não foram alterados.

**Acessibilidade local:** foco visível no resumo de erro, título da prévia, resultado do filtro e aviso de sucesso; tabela com rolagem horizontal contida e região focalizável. Contraste amostral no Chromium: **8,36:1** no erro de litros e **6,83:1** no botão de confirmação. Reflow a **320 px** e zoom **nativo de 200%** (`chrome.tabs.setZoom`, com confirmação da redução efetiva da largura) passaram em devolução e histórico. A comparação não é certificação de acessibilidade nem verificação em todos os navegadores.

## Verificação local e critérios de saída — 23/11/2026

| Verificação | Resultado |
| --- | --- |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF4` — rodada final sem filtro | **99 unitários e 86 de integração** aprovados, sem testes ignorados. J-01 a J-05 por teclado aprovadas nas duas origens após a suíte completa. |
| Ambiente da jornada | Playwright 1.65.1 e Chromium 159.0.8154.6 locais, informados por `PLAYWRIGHT_MODULE` e `PLAYWRIGHT_EXECUTABLE`, sem adicioná-los ao projeto. A resolução de `localhost` no Chromium de teste aponta para o loopback IPv4, sem reescrever os cabeçalhos `Origin`; os cabeçalhos de CORS foram verificados em `localhost:4201` e `127.0.0.1:4201`, com a API em `localhost:5001`. |
| `npm test -- --watch=false`, `npm run build` e `./node_modules/.bin/tsc.cmd --noEmit --project tsconfig.spec.json` em `app/frontend` | **68 testes aprovados em 9 arquivos**, build de produção concluído e checagem de tipos aprovada. |
| `dotnet build app/backend/Locadora.slnx --no-restore -v minimal` | Build aprovado com zero erros e zero avisos. |
| `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive`, `git diff --check` | Change válida e sem problemas de whitespace. Contratos de prévia/fechamento validados no OpenAPI e cenários HTTP. |

As rodadas intermediárias de Playwright não foram contabilizadas como sucesso: a primeira encerrou ao verificar `localhost` numa SPA ouvindo só IPv4; as seguintes expuseram faltas de espera por botões habilitados e pelo seletor de tipo de cliente. A rodada aprovada iniciou a SPA no loopback, preservou as duas origens e sincronizou as ações do roteiro. Planos, grupos, veículos, pessoas, taxas, seguros e aluguéis da jornada foram preparados pela UI. A vigência de combustível foi acrescentada via API do mesmo ambiente depois de verificar seu bloqueio na UI; revogação pelo botão Sair e isolamento A/B foram verificados com requisições autenticadas pelos tokens de teste.

| Critério do TDD | Evidência |
| --- | --- |
| **F4-01** | Prévia sem efeitos; retorno fechado uma vez e veículo liberado no mesmo commit que final, auditoria e chave. Confirmação rejeita revisão ou preço incompatível e recupera a mesma intenção após perda da resposta. |
| **F4-02** | RN-01 a RN-09 verificados em domínio e SQL/HTTP no recorte F4; R$ 334,00 sem atraso e R$ 354,00 com multa única nas mesmas duas diárias, caução separada, vigência retroativa e arredondamento. |
| **F4-03** | Histórico lê final persistido e revisão original; ocultação não apaga dados, requer ETag, mantém a guarda de plano usado e não muda a disponibilidade. |
| **F4-04** | CA-09 e CA-12 em SQL Server real: corridas, versão de preço, falhas com rollback, preço da taxa adicional por todas as diárias, reuso de placa/CNPJ e alterações posteriores sem reescrever o final. |
| **F4-05** | J-01 a J-05 por teclado nas duas origens, contas de empresas distintas, CORS, revogação, reenvio e histórico, sem foto; paridade e acessibilidade amostral registradas acima. |

O resultado é de operação local, não homologação, publicação ou v1 concluída. Não houve migration em base operacional. O risco da unidade do combustível gás para uso real permanece conforme PRD/TDD. Não foi exercitada restauração operacional após fechamentos; o [contrato](../../../app/backend/docs/contratos-f4.md#migration-e-recuperação) limita o uso do `Down`. A consulta de opções de veículo usa as páginas do histórico para manter a identidade preservada; volume e desempenho não foram medidos.

## Inspeção manual — aprovação registrada em 23/11/2026

O solicitante informou que realizou e aprovou a inspeção manual da Fase 4, sem problemas identificados. O relato não especifica a data de execução, o ambiente nem os cenários inspecionados. Esta aprovação é distinta das verificações automatizadas acima e não constitui homologação, publicação ou aprovação do PR.

## Revalidação para o PR

- `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF4`: 99 unitários e 86 de integração aprovados, sem ignorados, em SQL Server exclusivo de teste. Playwright passou nas jornadas J-01 a J-05 nas duas origens locais, com reenvio após perda da resposta, liberação do veículo, histórico, isolamento A/B e revogação. Reflow a 320 px e zoom nativo de 200% passaram na devolução e no histórico; contraste amostral de 8,36:1 e 6,83:1.
- `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF3`: mesmos 99 unitários e 86 de integração aprovados em uma segunda execução isolada; roteiro Playwright da J-03 aprovado (prévia sem ocupação, replay, edição com ETag, troca de veículo e bloqueios cadastrais).
- Em `app/frontend`, `npm test -- --watch=false`: 68 testes aprovados em 9 arquivos. `npm run build` e `./node_modules/.bin/tsc.cmd --noEmit --project tsconfig.spec.json` aprovados.
- `dotnet build app/backend/Locadora.slnx --no-restore -v minimal`: zero erros e avisos. Em `app/backend`, `dotnet ef migrations has-pending-model-changes --project src/Locadora.Infrastructure --startup-project src/Locadora.Api`: sem divergência; aviso de patch da CLI EF, conexão apenas de design.
- `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive` e `git diff --check`: sem erros.
