# Evidências de implementação — F2

Este arquivo registra somente verificações realmente executadas. Planejamento, protótipo e revisão de SDD não são evidência de implementação.

## 22/11/2026 — gates iniciais

### Validação da SDD

- Aprovação explícita obtida por `question` na revisão dos artefatos (`Validar SDD revisada`), registrada em `proposal.md`.
- `openspec validate fase-2-cadastros --strict` → `Change 'fase-2-cadastros' is valid`.
- Branch de trabalho: `feat/fase-2-cadastros`. Alterações preexistentes `openspec/config.yaml` e `openspec/schemas/` preservadas.

### Versões e documentação oficial consultada

| Tecnologia | Versão no projeto | Fonte consultada | Uso |
| --- | --- | --- | --- |
| .NET SDK | 10.0.404 (`global.json`) | ambiente local | build e testes |
| EF Core / SqlServer | 10.0.15 (`packages.lock.json`) | Context7 `/dotnet/entityframework.docs` — `IsRowVersion` e `HasFilter` | rowversion e índices únicos filtrados |
| ASP.NET Core | 10.0.15 | Context7 `/dotnet/aspnetcore.docs` — CORS `WithExposedHeaders` | expor `ETag` à SPA em origem distinta |
| Angular | 22.3.x (`package.json`) | Context7 `/angular/angular` — Signal Forms `form`, `required`, `validate` | formulários e validação de tela |
| Tailwind CSS | 4.3.5 (`package.json`) | projeto | estilos da SPA |
| Vitest | 5.x (`package.json`) | projeto | testes de UI |
| Node / npm | 24.21.0 / 11.19.0 | ambiente local | build e testes do frontend |

Não houve instalação de bibliotecas nem provisionamento de serviços nesta etapa. O SQL Server local do Compose está saudável (`127.0.0.1:1433`); os testes de integração usam o script que cria instância exclusiva, não o banco de Development.

## Continuação — implementação e verificações locais

### Alterações efetivamente realizadas

- Contratos de leitura de detalhes de grupos, planos, veículos, clientes, condutores e taxas agora devolvem estado e versão pela Application; os controllers retornam o estado no corpo e ETag no cabeçalho. Não há consulta separada da versão que possa produzir um par corpo/ETag inconsistente.
- Restaurada a guarda de escrita do recurso de teste tenant da F1, removida inadvertidamente na expansão do DbContext; a regressão foi detectada pelos testes existentes.
- Sincronização de CNH registra também a entidade correspondente na auditoria, no mesmo protocolo transacional. Inclusão/remoção de vínculo força atualização da raiz PF mesmo se o relógio repetir o instante.
- Corrigidos testes que usavam ETag anterior a uma mudança de vínculos, consultavam PF já excluído ou presumiam o vencedor de uma corrida entre escritas.
- SPA: corrigido o mapeamento de respostas de seguros/combustíveis; ausência e zero são distintos. Histórico identifica a versão vigente pelo ID, não pela primeira linha de cada página.
- Resumo de erros recebe foco após renderização, quando deixa de estar oculto. Seletores carregam todas as páginas de opções ativas, sem corte silencioso nos primeiros 100 registros.
- Documentação adicional consultada no Context7: `/dotnet/aspnetcore.docs` (cabeçalhos de resposta e precondições customizadas) e `/angular/angular` (`afterNextRender`, opção `injector` e foco após renderização).

### Comandos e resultados

| Comando | Resultado observado |
| --- | --- |
| `dotnet build app/backend/Locadora.slnx --no-restore -v minimal` | Sucesso, zero erros/avisos naquela execução |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` | Última execução: 54 testes unitários e 47 de integração aprovados, zero falhas/ignorados; build Release incluído |
| `npm test -- --watch=false` em `app/frontend/` | Última execução: 24 testes aprovados em 4 arquivos |
| `npm run build` em `app/frontend/` | Build de produção concluído após as correções |
| `git diff --check` | Sucesso; avisos de normalização LF/CRLF, sem erro de whitespace |

O script de integração criou e encerrou recursos SQL Server sintéticos exclusivos, com porta/volume próprios e segredos apenas no processo. Não foi usado o banco de Development. A primeira execução teve quatro falhas, a seguinte uma regressão da guarda F1; ambas foram corrigidas e a suíte inteira reexecutada.

### Alcance e pendências

- Cobertura implementada inclui criação, conflitos de modalidade/documentos/placa, exclusão/reuso de identidade, vínculos, CNH nos dois sentidos, primeira configuração condicional, concorrência de seguro, versões de combustível, rollback por falha de auditoria e regressão de autenticação F1.
- Os testes novos da SPA cobrem contrato HTTP/ETag/paginação, mais de 100 opções, formatação textual, seguros ausente/zero, edição preenchida, preço negativo/foco e histórico em página posterior. Não equivalem à cobertura completa das três telas.
- A suíte SQL atual passa, mas ainda faltam cenários específicos exigidos no checklist (por exemplo, falha parcial da sincronização CNH, corrida de combustível com relógio controlado e preflight dos cadastros em duas origens autorizadas).
- OpenAPI ainda precisa de revisão dos schemas/status/ETag. Não houve inspeção visual Playwright, verificação manual de zoom/teclado nem revisão completa de paridade.
- `tasks.md` permanece com 2/31 tarefas concluídas: os incrementos restantes ainda não satisfazem todos os critérios agrupados de implementação e comprovação. Não considerar F2 homologada ou pronta para publicação.
- Sem commits, push ou PR. Alterações permanecem na branch `feat/fase-2-cadastros`.

## Rodada complementar — contratos, cobertura e navegador

Esta seção atualiza o estado parcial da rodada anterior, sem apagar suas falhas/limitações históricas.

### Implementação e rastreabilidade

- RF-04/05 e F2-01: grupos/planos, validação de modalidades, conflitos e bloqueios cadastrais.
- RF-06/07, RF-19 e F2-02/F2-05: dados de veículo, filtro/agrupamento paginado, exclusão lógica e nova identidade no reuso; sem fotos e sem atestar disponibilidade contratual.
- RF-08/09/10/19 e F2-03/F2-05: PF/PJ/endereço, vínculos, condutores, CNH coordenada, exclusão/reuso e referência histórica ao cliente excluído. Alterar dados não relacionados do condutor preserva a referência original; nova associação exige cliente ativo.
- RF-11/12/13 e F2-04: taxas, seguros independentes e combustível versionado. Snapshots e aluguéis continuam fora da F2.
- RNF-01/02/03/04/05 e F2-06: isolamento, validação, precondições, auditoria, formulários e estados; o gate manual complementar permanece pendente.
- DT-01/02/04/06/07/08/09/10/13/14/15/21 materializadas no recorte cadastral, sem nova stack ou dependência.

### Correções e contratos

- OpenAPI agora declara respostas tipadas, paginação, padrões de strings decimais, ProblemDetails, If-Match obrigatório, ETag e Location. Teste lê `/openapi/v1.json` e verifica metadados; sem rota de foto.
- Documento de uso em `app/backend/docs/contratos-f2.md`. Valores Unicode fora do decimal canônico e overflow de offset paginado são rejeitados.
- Enums obrigatórios ausentes não assumem o primeiro valor. Erros de binding e validação usam nomes camelCase, inclusive parâmetros de records e caminhos JSON.
- Migration F2 preserva tipo/tamanho original de `Auditorias.Evento`; somente acrescenta metadados. Teste migra explicitamente a F1, preserva evento anterior e verifica novas colunas nulas.
- Auditorias das entidades sincronizadas compartilham correlação da mesma transação. Suprimido o log bruto de atualização EF, pois a mensagem de índice duplicado do SQL pode conter CPF/CNPJ; respostas e logs de exceções de domínio não incluem valores pessoais.
- SPA trata limites de texto/Int32 sem truncar, respostas antigas de outra aba, falha de histórico e atualização parcial de vínculos. Cadastro/preço zero continua diferente de ausência.
- Verificação visual detectou e corrigiu abas inativas com baixo contraste. Preço de combustível agora busca versão ao abrir o editor e bloqueia a ação durante consulta; a corrida foi reproduzida no navegador e tem teste de regressão.

### Resultados efetivos

| Verificação | Resultado |
| --- | --- |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` — rodada `f2-verificacao-completa.log` em Temp/opencode | 65 testes unitários e 59 de integração aprovados, zero falhas/ignorados; build Release incluído |
| `npm test -- --watch=false` em `app/frontend/` | 47 testes aprovados em 7 arquivos |
| `npm run build` em `app/frontend/` | Build de produção passou após os últimos ajustes de estado/estilo |
| OpenAPI | Teste de schemas, padrões decimais, respostas/ETag/Location/If-Match e ausência de fotos aprovado |
| SQL adicional | Plano concorrente, veículo inválido sem mutação, vínculo concorrente, combustível concorrente com relógio fixo, CNH revertida com auditoria, dois seguros e preservação de taxa excluída aprovados |

Falhas intermediárias não foram ignoradas: corrigidos fixture de migration (schema real da F1), alvo da validação de records e nomes de campo do binding. A suíte completa foi reexecutada. Fixtures de CORS sobrescrevem apenas a política do ambiente Testing com duas origens exatas; o navegador exercitou a configuração real do ambiente visual.

### Playwright — execução real

Ambiente isolado iniciado por `Testar-Integracao.ps1 -Visual`, API `http://localhost:5001`, SPAs `http://localhost:4201` e `http://127.0.0.1:4201`. O servidor Angular padrão escutou somente IPv6/localhost; para a segunda origem foi iniciado outro servidor Angular em loopback IPv4 com a mesma configuração. Sem proxy, cookie ou armazenamento persistente.

- Empresa sintética A: registro/login; grupo e plano Diário; veículo com erro de capacidade e foco no resumo, gravação, agrupamento, edição preenchida e exclusão; clientes PF/PJ e vínculo; próprio PF como condutor com validade vencida; taxa; seguro zero distinto de ausência; duas versões de combustível com uma vigente e outra anterior.
- Empresa sintética B na segunda origem: lista inicial vazia, grupo próprio; GET de grupo da A retornou **404**, sem exposição; combustível da B sem vigências da A.
- B: veículo criado, excluído e placa reutilizada com **ID diferente**. Logout confirmou navegação ao login; requisição com sessão revogada retornou **401**.
- `localStorage` e `sessionStorage` da segunda origem estavam vazios. Tokens usados para verificar isolamento/revogação permaneceram somente em memória do teste e não foram gravados.
- Teclado: Tab do nome do grupo alcançou Cancelar, com outline sólido; Enter cancelou. Erro de capacidade focou resumo `role=alert`. Rótulos/campos e edição também verificados em Vitest.
- Screenshot inspecionada pelo agente em estado normal e com **zoom CSS 200%**, documento de 1280 px sem overflow global; tabela mantém rolagem local. Isso não equivale a teste manual com zoom nativo, nem certifica acessibilidade.
- Os processos visuais criados nesta execução foram encerrados pelos seus PIDs; o script encerrou seu SQL/volume exclusivos. Nenhum recurso de Development foi encerrado ou excluído.

Revisão bidirecional em `paridade.md`. Não houve commits, push, PR, sync ou archive. Não declarar F2 homologada ou iniciar F3 enquanto o gate manual registrado em 7.3 permanecer aberto.

### Conferência documental e de working tree

- `openspec validate fase-2-cadastros --strict` passou após as alterações.
- `git diff --check` passou, com avisos LF/CRLF; sem erro de whitespace.
- Conferidos arquivos/áreas alterados, configurações e manifests: sem nova dependência, arquivo de credencial, artefato de build ou screenshot na lista de alterações Git. Screenshots locais ficam em `.playwright-mcp/`, ignorado.
- Busca limitada por padrões de chave privada e tokens AWS/GitHub em código/testes/documentação de `app/` não encontrou correspondências. Não equivale a auditoria independente de segredos; credenciais de testes são sintéticas e segredos operacionais não foram lidos/adicionados.
- `docs/tdd.md` agora registra o estado real do schema, os contratos aprovados e o checklist F2, preservando IDs DT/RF/CA e pendências F3/F4/F5. Nenhuma aprovação de arquitetura/produção foi inferida.

### Encerramento desta execução

- OpenSpec Apply confirmou **30/31 tarefas concluídas**; somente 7.3 permanece aberta pela inspeção manual complementar com zoom nativo.
- Consultado pelo sistema `question`, o usuário escolheu **“Manter pendente”**. Isso registra a pendência, não a dispensa nem constitui homologação da F2.
- Mantida a solicitação expressa de **não realizar commits ou PR**. Também não houve push, merge, sync ou archive.

## Preparação do PR — 22/11/2026 (execuções em 23/11 UTC)

Esta seção atualiza o encerramento anterior sem reescrever seu histórico. A solicitação atual autoriza revisão, commits e abertura do PR da branch `feat/fase-2-cadastros` para `main`; não autoriza merge, publicação, sync ou archive.

### Confirmações do solicitante

- Via `question`, confirmou que a branch pretendida é `feat/fase-2-cadastros`.
- Informou “realizei a inspeçao, tudo ok” e confirmou explicitamente o alcance: aplicação (não apenas protótipos), telas Frota, Pessoas e Preços, zoom nativo do navegador, legibilidade, acesso às ações, rolagem, teclado e foco, sem problemas relatados.
- Navegador/versão, ambiente detalhado e percentual de zoom não foram informados. Não foram inferidos nem atribuídos ao agente. Trata-se de relato do solicitante, não execução do agente, certificação de acessibilidade, revisão independente ou homologação.
- O relato complementa a jornada Playwright anterior e encerra 7.3/F2-06. O checklist atual contém 31/31 tarefas marcadas.
- Autorizou expressamente incluir as alterações preexistentes de `openspec/config.yaml` e `openspec/schemas/prototype-first/` em commit próprio, conforme DT-21.

### Revisão e ajuste

- `git fetch origin` confirmou `HEAD`, `main` e `origin/main` em `e1585c1` antes dos novos commits. Não havia PR para a branch.
- Consultados template `.github/pull_request_template.md`, labels existentes e proteção da `main`. Labels selecionadas: `enhancement` e `documentation`; nenhuma label global criada/alterada.
- Revisados escopo PRD/TDD/SDD, contratos, isolamento, transações, concorrência, auditoria, migration e navegação/formulários. Sem nova dependência, mudança de escopo ou antecipação de locações/fotos.
- Detectada exibição de vigência no fuso do dispositivo em `formatarData`. Ajustada para `America/Sao_Paulo`, conforme DT-09 e premissa do PRD. Teste de regressão cobre mudança de dia, horário de verão histórico, ausência e data inválida.
- Reconsultada documentação oficial de concorrência EF Core via Context7 `/dotnet/entityframework.docs`: `rowversion`, tokens na condição de escrita e `DbUpdateConcurrencyException`.
- A checagem dos arquivos novos detectou uma linha vazia extra no fim do README do schema fornecido. Removida somente essa linha e atualizado o hash correspondente em `.install.json`; instruções/templates do schema preservados.

### Verificações repetidas nesta preparação

| Comando | Resultado |
| --- | --- |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` | Build Release; 65 unitários e 59 de integração aprovados, zero falhas/ignorados; SQL Server sintético exclusivo encerrado pelo script |
| `npm test -- --watch=false` em `app/frontend/` | 48 testes aprovados em 7 arquivos após o ajuste de fuso |
| `npm run build` em `app/frontend/` | Build de produção aprovado após o ajuste de fuso |
| `openspec validate fase-2-cadastros --strict` | Change válida |
| `openspec status --change fase-2-cadastros --json` | Schema `prototype-first`; todos os artefatos de planejamento completos (não equivale a homologação) |
| `git diff --check` | Sem erros de whitespace; avisos de normalização LF/CRLF |
| Busca limitada de padrões de segredos nos arquivos alterados/novos | Nenhuma correspondência para chaves privadas, tokens GitHub/AWS ou AccountKey; não equivale a auditoria completa |

A jornada Playwright não foi reexecutada nesta preparação; sua evidência pertence à rodada anterior. O complemento manual pertence ao solicitante. Não houve provisão externa, instalação de dependências nem alteração do SQL de Development. A divisão dos commits organiza trabalho já existente; não pretende reconstruir artificialmente a cronologia de implementação.

### Limites da entrega

- PR destinado à revisão do recorte cadastral RF-04–13/19 e CA-02–05; RF-10, RNF-03/07 e CA-10/12 permanecem parciais/dependentes das fases contratuais. RN-01–09 de cobrança não foram implementadas aqui.
- F3 mantém bloqueios por aluguel, elegibilidade até retorno e snapshots; F4 mantém devolução/demonstrativos/combustível retroativo; F5 mantém fotos. Publicação e gates operacionais continuam futuros.
- Abertura do PR Ready for review não significa aprovação ou integração. Estado, commits e checks devem ser conferidos na plataforma após publicação; nenhum resultado de CI ou revisão independente é presumido.

## Fechamento OpenSpec antes do merge — 23/11/2026

- PR existente: #2, `feat(cadastros): implementar cadastros da fase 2`; branch `feat/fase-2-cadastros`, destino `main`.
- O solicitante confirmou via `question`: “A inspeção manual da tarefa 7.3 já foi realizada e aprovada por mim, sem problemas identificados.” O alcance e as limitações do relato permanecem na seção de preparação do PR; não se presume certificação de acessibilidade, aprovação independente ou homologação de produção.
- Antes do Sync/Archive, 7.3 já estava marcada `[x]`. `openspec list --json` confirmou **31/31 tarefas concluídas**, e `openspec status --change fase-2-cadastros --json` confirmou todos os artefatos completos.
- `openspec validate fase-2-cadastros --strict` passou antes do Sync. OpenSpec instalado: **2.0.0**. A ajuda da CLI confirma que Archive atualiza specs por padrão e oferece `--skip-specs`.
- Autorizados Sync das três capabilities e Archive com `--skip-specs` após validação, evitando sincronização duplicada; novo commit apenas de OpenSpec e push para a mesma PR. Merge, force push e alteração de commits anteriores não autorizados.
- Retomada autorizada após interrupção: corrigida somente a vírgula omitida em `openspec/specs/pessoas/spec.md`. A comparação integral com os deltas, normalizando apenas finais de linha e os cabeçalhos próprios do formato principal, confirmou Purpose, requisitos e cenários preservados nas três capabilities: frota com 5 requisitos/26 cenários; pessoas com 5/30; preços com 5/22. Nenhuma nova divergência.
- Antes do Archive, `openspec validate --specs --strict` aprovou as 6 specs principais (zero falhas), e `openspec validate fase-2-cadastros --strict` aprovou a change. Os artefatos continuam completos e todas as 31 tarefas concluídas.
- `openspec archive fase-2-cadastros --skip-specs --yes --json` concluiu o arquivamento em `openspec/changes/archive/2026-11-23-fase-2-cadastros/`, com `specsUpdated: false`: o Sync já havia sido realizado, sem repetição no Archive.
- Após o Archive, `openspec list --json` retornou nenhuma change ativa e `openspec validate --specs --strict` aprovou novamente as 6 specs. Verificação de integridade confirmou os 12 arquivos arquivados, incluindo `.openspec.yaml`, preservados em relação ao commit anterior, exceto pelas adições autorizadas neste registro de evidências. As três specs principais continuam idênticas aos deltas arquivados, com apenas a transformação dos cabeçalhos de delta para spec principal.
- Alterações limitadas a `openspec/`: três specs principais novas e movimentação da change para o arquivo, com este registro da aprovação/validações. Código, requisitos e regras de negócio não foram alterados. Testes da aplicação não foram reexecutados nesta operação documental.
