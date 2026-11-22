# Evidências da F1 — 22/11/2026

## Estado e limites da entrega

Implementação **local verificada**, na branch `feat/fase-1-fundacao-infraestrutura`, baseada no mesmo commit de `main`/`origin/main` (`15315c8`) na revisão. Atende RF-01–RF-03, J-01 e CA-01 no recorte da F1, RNF-01–02 e RNF-04 no acesso; não comprova fotos, cadastros ou histórico de contratos de fases posteriores. Aprovação/revisão do PR, integração, homologação e publicação são etapas distintas.

As marcações anteriores de 28 tarefas não tinham evidências suficientes. Foram reabertas e verificadas novamente; não se presumiu execução passada. O solicitante autorizou pelo sistema `question`:

- correções aderentes aos requisitos aprovados e atualização de dependências vulneráveis;
- alinhamento de J-01/SDD ao RF-01: sem nome empresarial obrigatório e sem autenticação automática;
- Signal Forms, preservando DT-01;
- materialização de DT-02 ainda na F1, com instalação de Tailwind CSS/PostCSS e `@lucide/angular`, preservando fluxos e acessibilidade;
- SQL Server de teste exclusivo, sem volume/credenciais de Development, com segredos sintéticos no processo;
- refresh em janela fixa configurável de 30/minuto/IP, sem fila, `429` e `Retry-After`;
- exceção restrita ao registro: 201 com `empresaId` e `usuarioId`, sem `Location`, pois não existe rota de consulta da conta criada na F1.

Isso não representa revisão independente, homologação com administrador real ou aprovação para produção.

## Ambiente efetivamente usado

| Item | Valor verificado |
| --- | --- |
| Host | Windows x64, build 10.0.26300 |
| .NET | SDK 10.0.404; runtime ASP.NET Core/.NET 10.0.15 |
| Backend | EF Core/Identity/JwtBearer 10.0.15; IdentityModel JWT 8.19.2; MSTest 4.0.2 |
| Frontend | Angular/CLI 22.3.0; TypeScript 6.0.3; RxJS 7.8.2; Vitest 5.1.1 |
| UI (DT-02) | Tailwind CSS e plugin PostCSS 4.3.5; PostCSS 8.5.33; `@lucide/angular` 1.77.0, peers Angular >=17 |
| Node/npm | 24.21.0 / 11.19.0 |
| Infraestrutura | Docker 29.7.2; Compose 5.4.0; imagem oficial SQL Server 2022 Developer, Linux amd64 |
| Imagem SQL fixa | `mcr.microsoft.com/mssql/server@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718` |
| SDD | OpenSpec CLI 2.0.0, `openspec/config.yaml`, schema `spec-driven` |
| Origens visuais | SPA `http://localhost:4201`; API `http://localhost:5001/api/v1` |

As versões foram confrontadas com [Angular](https://angular.dev/reference/versions), [Signal Forms](https://angular.dev/guide/forms/signals/overview), [CLI define](https://angular.dev/cli/serve), documentação ASP.NET Core/Identity e EF Core via Context7 e manifests publicados no NuGet. A separação de porta da fixture usa [`!override` do Compose](https://docs.docker.com/reference/compose-file/merge/).

## Comandos e resultados reais

Comandos backend executados em `app/backend`; frontend em `app/frontend`, salvo indicação.

| Comando/verificação | Resultado |
| --- | --- |
| `dotnet restore Locadora.slnx --locked-mode` | Passou nos seis projetos |
| `dotnet build Locadora.slnx --configuration Release --no-restore` | Passou; zero avisos e zero erros na rodada final |
| `pwsh -NoProfile -File scripts/Testar-Integracao.ps1` | Passou: **8 unitários + 40 integrações**, zero falhas e zero ignorados |
| `pwsh -NoProfile -File scripts/Testar-Integracao.ps1 -Visual` | Backend passou e iniciou as duas origens com SQL/chaves exclusivos; fluxo visual executado abaixo |
| Compose do script: `config --quiet`, `up --detach --pull never`, healthcheck, criação de marcador, restart e consulta do marcador | Passou; volume persistiu após restart; porta publicada somente em loopback e relida após restart |
| `dotnet list Locadora.slnx package --vulnerable --include-transitive --no-restore` | Nenhum pacote vulnerável nas fontes consultadas |
| `dotnet ef migrations has-pending-model-changes --project src/Locadora.Infrastructure --startup-project src/Locadora.Api --configuration Verificacao --no-build` | Nenhuma alteração pendente no modelo; conexão de design não usada para conectar |
| `npm run build` | Passou; bundle dentro dos budgets configurados |
| `npm test -- --watch=false` | **17 testes** em dois arquivos, todos passaram |
| `npx tsc --noEmit --project tsconfig.spec.json` | Passou sem erros |
| `npm ls --depth=0` | Dependências instaladas consistentes; versões resolvidas registradas acima |
| `npm audit --json` | Zero vulnerabilidades, incluindo dependências de desenvolvimento |
| `openspec validate fase-1-fundacao-infraestrutura-local --strict` | Passou antes do fechamento |
| `git diff --cached --check` e revisão de caminhos staged | Sem erro de whitespace; sem `bin`, `obj`, `node_modules`, `dist`, `.angular`, `.playwright-mcp`, `.env` ou `secrets.json` no diff |
| Busca `git grep --cached --files-with-matches -I -E` por tokens GitHub, chaves privadas PEM e IDs AWS | Nenhum padrão de alta confiança encontrado; complementada por leitura de configurações, contratos e diff |

O script prepara a mesma composição SQL do repositório em projeto/volume exclusivos, não o banco de Development. Fixtures usam `Testing` e configuração do processo; não carregam User Secrets pessoais. `Migrate` é explícito na fixture, não automático pelo ambiente Testing. Em Development, a API visual criou seu catálogo e aplicou as migrations antes de aceitar tráfego. O teste de ambiente não-Development verificou health de processo sem criação do banco.

`node_modules` já existia e havia servidor preexistente em uso. Depois da autorização da DT-02, foram executados `npm install --save-dev --save-exact tailwindcss@4.3.5 @tailwindcss/postcss@4.3.5 postcss@8.5.33` e `npm install --save-exact @lucide/angular@1.77.0`, atualizando o lockfile. `npm ci` está documentado para checkout limpo, mas **não foi executado nesta revisão**. Os avisos de allowScripts do npm para dependências nativas não foram contornados com aprovação global; build e testes passaram. Não há script de lint ou pipeline CI aprovado/configurado para declarar executado.

## Cenários comprovados

### Backend e SQL Server real

- Registro retorna 201 sem sessão/tokens e aceita senha mínima sem composição extra. Usuário normalizado duplicado é rejeitado. Falha do Identity não deixa empresa órfã.
- Falha injetada **após inserção da conta e antes da auditoria** reverte empresa, conta e auditoria juntas.
- Migration sobre schema anterior com empresa/conta/sessão/refresh sintéticos preserva contas, preenche `EmpresaId` dos hashes e revoga sessões antigas. Schema/modelo final consistente.
- Login rejeita senha errada; bloqueia após cinco falhas e libera após vencimento. A resposta HTTP é genérica.
- JWT contém claims mínimas e `kid`; assinatura, emissor, audiência, tempo, algoritmo, `none`, `kid` ausente/desconhecido e empresa/usuário/versão forjados são rejeitados. Chave anterior autorizada valida; novos tokens usam a ativa.
- Access limitado a 15 minutos e ao fim da sessão absoluta de oito horas. Refresh não estende esse fim.
- Refresh rotativo preserva sessão/família. Reúso revoga a família. Duas renovações simultâneas em requisições/conexões separadas emitem somente uma renovação; o reúso revoga o token resultante.
- Conta e versão são reconsultadas; alteração do security stamp nega access e refresh.
- Logout idempotente revoga sessão/família e audita uma vez. Reúso de ambos os tokens retorna 401.
- Registro, login e refresh têm limites independentes com 429/Retry-After, sem gravação excedente ou emissão de tokens. CORS exato é verificado inclusive na rejeição do limitador.
- Empresas A/B: filtro de leitura, guarda síncrona/assíncrona em todos os overloads de SaveChanges, attach com tenant forjado, referência cruzada por FK composta e histórico **de auditoria disponível na F1**. Valores do corpo/query/cabeçalho não escolhem a empresa. Sessão revogada nega acesso antes do controller de dados.
- SQL indisponível no registro/login/refresh e em requisição protegida retorna 503 sem dados nem sucesso parcial.
- ProblemDetails contém status, code e traceId, sem senha, tokens, SQL ou stack trace no corpo.

Os recursos/controllers sintéticos de prova não equivalem a cadastros ou histórico de locação. O controller pertence somente à assembly de integração. Filtros, snapshots e fotos das fases seguintes exigem seus próprios testes.

### Frontend e fluxo Playwright

Executado pelo Playwright do harness com `filename: "app/e2e/fase1.playwright.js"`:

1. `/registro` vazio e confirmação divergente: **zero requisições**, erros associados e resumo focado.
2. Teclado: Tab de usuário para e-mail, outline de 3 px; links entre login/registro acionáveis com Enter. Documento em `pt-BR`.
3. Registro válido real: 201, somente IDs no retorno, aviso transitório no login e sem autenticação automática.
4. Senha incorreta: 401 e mensagem genérica. Login válido leva ao painel.
5. `localStorage`, `sessionStorage`, cookies e IndexedDB vazios; recarga redireciona ao login.
6. Consulta autenticada à rota sintética de tenant na API visual retorna 404: controller de teste não foi publicado.
7. Falha de rede injetada no logout: UI informa que a revogação remota não foi confirmada e não apresenta sucesso.
8. Novo logout confirmado: reutilização do access e refresh retorna **401/401**.
9. Registro duplicado real: **409**, erro compreensível, usuário/e-mail preservados e senha limpa.

O cenário foi repetido depois da adoção de Tailwind/Lucide e passou novamente. Inspeção adicional no navegador confirmou SVG decorativo (`aria-hidden=true`, `focusable=false`), largura de 20 px por utilitário Tailwind, formulário com max-width de 512 px e padding de 24 px. Os botões mantêm rótulos textuais.

O console apresentou erros esperados dos cenários negativos (404 da rota sintética, falha de rede do logout, 401/401 de reúso e 409 de duplicado), sem erro de aplicação não tratado observado. Vitest complementa os cenários com restrição de bearer por origem/segmento, destinos externos, serialização de refresh, falha de renovação e confirmação de logout.

Automação de teclado/foco não substitui inspeção humana com leitor de tela, zoom e contraste nem certifica acessibilidade.

## Rastreabilidade do checklist de saída

| Item | Evidência |
| --- | --- |
| F1-01 | Solução/camadas, builds, SDK/Node/Angular compatíveis e lockfiles |
| F1-02 | Compose SQL fixo, healthcheck, volume persistente após restart, loopback e instância de teste separada; API/SPA no host |
| F1-03 | Configuração externa, mapa JWT validado, chaves de teste próprias e ausência de segredo no diff |
| F1-04 | Registro atômico, rollback e limite 3/10 minutos/IP |
| F1-05 | Login/refresh/sessão/logout, limites temporais, serialização e recarga sem sessão |
| F1-06 | CORS exato sem credenciais, preflight e limitação com cabeçalhos CORS |
| F1-07 | Contexto server-side; empresas A/B, attach, filtros e FKs |
| F1-08 | MSTest/WebApplicationFactory/SQL real; migrations da aplicação só em Development |
| F1-09 | CA-01 no recorte F1, revogação, reúso e falha fechada |
| F1-10 | Signal Forms, confirmação, mensagens, foco e navegação real entre registro/login |

Decisões atendidas no recorte: DT-01–07, DT-13–15 e DT-17–21. DT-02 foi materializada com Tailwind/PostCSS, ícones standalone Lucide e controles nativos próprios acessíveis, sem Material. DT-08–12 e DT-16 permanecem vinculadas às fases correspondentes.

## Falhas encontradas e corrigidas durante a revisão

- Vulnerabilidades NU1902/NU1903 em Pkcs 9.0.4: dependências atualizadas e auditoria repetida sem achados.
- Primeiro restore atualizado detectou downgrade de IdentityModel: alinhado ao mínimo 8.19.2 exigido pelo novo JwtBearer/SqlClient.
- Build Debug bloqueado pela API preexistente: usados builds Release, sem encerrar o processo do usuário.
- Nova fixture inicialmente descartava o host duas vezes; corrigido descarte idempotente, sem recriar host durante falha de inicialização.
- Teste de falha parcial revelou captura indevida de InvalidOperationException como conflito: conflito passou a ter exceção tipada; falha inesperada agora é 500 genérico e rollback foi comprovado.
- Teste da composição com restart revelou mudança de porta aleatória: a conexão passou a reler a porta após restart.
- Primeira tentativa visual teve erro de quoting do define através do npm.cmd; substituída por configuração Angular pública `verificacao-f1`.
- Um teste Vitest precisava aguardar a renovação assíncrona; passou a aguardar o efeito sem sleep fixo.

Rodadas finais acima passaram. As falhas iniciais não foram ocultadas nem tratadas como evidência de sucesso.

## Operação, riscos e pendências

- **Impacto da rodada inicial:** antes da correção da fixture, o teste original `Recurso_de_teste_nao_cruza_empresas_mesmo_com_empresa_forjada` usou o banco Development a partir de User Secrets. Sua execução aprovada criou duas empresas/contas sintéticas (usuários `tenant-A-<guid>` e `tenant-B-<guid>`) e dois recursos sintéticos nesse banco. Não foi feita limpeza, pois os IDs exatos não foram preservados na saída e não se deve apagar por prefixo amplo. Essa rodada não é a evidência de isolamento do ambiente de testes.
- Todas as rodadas posteriores à correção usaram SQL exclusivo, com volume e credenciais sintéticos. As migrations corretivas não foram aplicadas ao banco Development preexistente; não houve remoção/recriação ou reconfiguração de seu volume. As árvores de processo API/SPA e projetos SQL sintéticos criados para a verificação foram encerrados/removidos de forma delimitada; o servidor preexistente do usuário foi preservado.
- As migrations da correção removem o campo de nome empresarial não aprovado e invalidam sessões antigas. O Down não recupera nomes removidos. Atualização de um banco existente exige revisão e cópia prévia; não apagar volume nem executar downgrade destrutivo como rollback.
- A CLI EF 10.0.9 emitiu aviso de patch inferior ao runtime 10.0.15. Não houve erro de geração, aplicação ou divergência de modelo; padronizar a ferramenta em ambiente futuro é manutenção recomendada.
- Limitação de taxa é local/em memória e o IP é remoto direto. Escala ou proxy precisam de decisão própria. Não houve teste de carga/SLO.
- A avaliação foi local em ambiente Windows/SQL Server Linux real. Não houve CI, revisão independente, homologação com usuários, Azure/R2, publicação ou certificação de acessibilidade.
- Retenção, recuperação de conta, segurança operacional e gates de produção continuam nas pendências do PRD/TDD. A F1 não é a v1 completa e não autoriza uso real.

## Fechamento SDD executado

- `openspec list --json` confirmou 28/28 tarefas concluídas, sem presumir conclusão a partir do status dos artefatos.
- O solicitante autorizou sincronizar e arquivar via `question`.
- Criadas `openspec/specs/account-access/spec.md`, `registro-empresa/spec.md` e `tenant-isolation/spec.md`: seis requisitos no total, Purpose e cenários preservados, sem cabeçalhos de delta nas specs principais.
- Comparação integral das três specs com os deltas (alterando somente título e cabeçalho de Requirements) confirmou equivalência; `openspec validate --specs --strict`: **3 passaram, 0 falharam**.
- `openspec archive fase-1-fundacao-infraestrutura-local --skip-specs --yes --json` arquivou a change em `2026-11-22-fase-1-fundacao-infraestrutura-local`. `--skip-specs` evitou reaplicar o sync **já executado e verificado**; não pulou a validação nem o sync exigido no workflow.
- Nova validação estrita das três specs passou; `openspec list --json` retornou lista de changes ativas vazia.
