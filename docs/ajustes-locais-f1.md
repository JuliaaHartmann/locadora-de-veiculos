# Ajustes de configuração local da F1

## Escopo autorizado

Em 22/11/2026, após relatar `Versão ativa JWT ausente do mapa`, o solicitante aprovou manter JWT nos User Secrets, usar `.env` na raiz para SQL do Compose e solicitou a aplicação dos ajustes.

- PRD: não altera RF-01–RF-03, RNF-01–02, J-01 ou CA-01.
- TDD: refinamento de DT-17/DT-18 e seção 6.3, sem trocar tecnologia, dependências ou estratégia de segredos.
- SDD: as três specs funcionais da F1 permanecem inalteradas. Não há nova capacidade nem alteração de comportamento de negócio; o archive da F1 é preservado como registro histórico.
- PR: atualização no PR #1 existente, sem novo PR ou merge.

## Ajustes executados

- Ignorar `.env` e variantes locais na raiz antes de criar o arquivo real; versionar somente `.env.example` sem senha.
- Conservar connection string e JWT nos User Secrets da `Locadora.Api`. Preservar chave atual válida; se existir apenas `Jwt:ChaveAtiva`, migrar o mesmo valor para `Autenticacao:Jwt:Chaves:<versao>`, sem removê-lo ou imprimi-lo. Gerar 32 bytes aleatórios somente na ausência de chave existente.
- Criar `.env` com a senha SQL já existente na conexão, sem gerar outra senha, alterar container ou apagar volume.
- Alinhar o perfil HTTP de `dotnet run` à porta 5000; manter Development e a validação fechada da configuração JWT.
- Documentar que `.env` é lido pelo Compose, não pela API, e que o ambiente exportado tem precedência.

## Validação e limites

Verificações executadas em 22/11/2026:

| Verificação | Resultado |
| --- | --- |
| User Secrets da API | Chave antiga válida migrada para `Autenticacao:Jwt:Chaves:v1`, preservando o mesmo material de 32 bytes, sem rotação; connection string e entrada antiga preservadas |
| `.env` real | Criado depois da regra de ignore, somente com `MSSQL_SA_PASSWORD`; senha existente reutilizada, sem valor exibido |
| Resolução do Compose | Modelo capturado apenas em memória confirmou senha igual à da conexão; sem variável de terminal sobrepondo o arquivo durante essa comparação |
| `git check-ignore -v .env` | Ignorado por `/.env` |
| Conferência de arquivos tracked | `.env` não versionado; `.env.example` não ignorado e sem senha real |
| `docker compose config --quiet` | Passou |
| Perfil HTTP | Development e `http://localhost:5000`, conferidos no JSON |
| `dotnet restore Locadora.slnx --locked-mode` | Passou |
| `dotnet build Locadora.slnx --configuration Release --no-restore` | Passou; zero avisos e erros |
| `pwsh -NoProfile -File scripts/Testar-Integracao.ps1` | 8 unitários e 40 integrações SQL real passaram; zero falhas/ignorados |
| `git diff --check` | Passou |

Não foram exibidos segredos, tokens, connection strings ou a renderização completa do Compose. A API não ganhou leitura automática de `.env`, fallback JWT ou dependência nova. A chave local foi configurada pelo Secret Manager usando JSON em stdin, não valor em argumento.

As primeiras tentativas de preparação local foram interrompidas antes de gravar o JWT: corrigidos o uso do setter do parser de conexão no PowerShell e a remoção efetiva de variável de ambiente vazia para que não sobrepusesse `.env`. Arquivos `.env` criados por essas tentativas foram removidos de forma delimitada; nenhum arquivo preexistente foi sobrescrito. A execução final e a comparação dos valores passaram.

Os testes continuaram em SQL exclusivo, mesmo com `.env` real presente na raiz: sua senha é sobreposta pelo material sintético do processo de testes, conforme a precedência documentada do Compose. O projeto/volume de teste foi removido ao fim. Não houve reset de senha SQL, reinicialização de volume Development ou aplicação de migrations nele.

**Limite:** não foi executado `dotnet run` contra o banco Development preexistente, porque a inicialização nesse ambiente aplica migrations automaticamente. O build, a configuração local e a suíte isolada foram verificados; a conexão/autenticação e a atualização de schema desse banco específico não são declaradas testadas nesta etapa. Testes frontend/Playwright não foram repetidos, pois não houve alteração de código ou comportamento do frontend.
