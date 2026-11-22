# Backend — F1 local

.NET SDK 10.0.404, ASP.NET Core/EF Core 10.0.15 e SQL Server 2022 Developer em Linux x86-64. Aplicação e testes ficam no host; Compose contém somente SQL. Esta fundação não é uma v1 pronta nem autorização para produção.

## Restaurar e verificar

Neste diretório:

```powershell
dotnet restore Locadora.slnx --locked-mode
dotnet build Locadora.slnx --configuration Release --no-restore
pwsh -NoProfile -File scripts/Testar-Integracao.ps1
dotnet list Locadora.slnx package --vulnerable --include-transitive --no-restore
```

O script exige Docker com Compose compatível com `!override` (2.24.4+), a imagem do `compose.yml` já disponível e autorização para criar recursos locais de teste. Não baixa imagens (`--pull never`). Cria um projeto Compose único, porta aleatória em loopback, volume exclusivo e senha sintética em memória. Verifica healthcheck e persistência após restart. Fornece conexão e chaves pelo processo, sem ler User Secrets pessoais. Cada fixture prepara um catálogo `LocadoraTeste_<guid>`. Ao terminar, remove **somente** o projeto/volume de teste criado naquela execução. Nunca usa o container, a senha ou o volume de Development.

Também é possível executar `dotnet test --configuration Release --no-restore` com `ConnectionStrings__Locadora`, `Autenticacao__Jwt__Chaves__v1` e `Autenticacao__Jwt__Chaves__anterior` fornecidos externamente ao processo, **exclusivamente contra uma instância de teste** com permissão para criar/remover catálogos. Sem esses valores, a integração falha: não usa SQLite ou InMemory nem simula sucesso.

## Desenvolvimento

Configure pelo Secret Manager, somente para `src/Locadora.Api`, os seguintes valores (fora do repositório):

- `ConnectionStrings:Locadora`: conexão para `127.0.0.1,1433`, banco local e credencial do SQL Developer.
- `Autenticacao:Jwt:Chaves:v1`: Base64 de 32 bytes de `RandomNumberGenerator`.
- A versão ativa, emissor e audiência não sensíveis ficam em `appsettings.json`.

O nome da entrada deve corresponder à versão ativa: `v1` exige `Autenticacao:Jwt:Chaves:v1`. A antiga entrada `Jwt:ChaveAtiva` não é lida pela API. Na migração local, preserve o valor de uma chave antiga válida de 32 bytes no novo nome; gere material novo somente quando não houver chave existente, sem imprimir o valor.

Use entrada segura/interativa ou JSON pelo stdin do Secret Manager; não ponha valores secretos em argumentos, histórico, `.env` versionado ou logs. User Secrets não cifram o armazenamento local e não são configuração de publicação. Não imprima a lista de segredos em evidências.

O Compose **não lê User Secrets**: a senha do SQL fica em `.env` na raiz, ignorado pelo Git, com a única entrada `MSSQL_SA_PASSWORD`. `.env.example` é um modelo sem senha real. JWT e connection string permanecem somente nos User Secrets; a senha SQL do `.env` deve ser igual à da conexão. Não altere a senha de um volume existente para tentar corrigir erro de conexão.

O Compose lê `.env` automaticamente. Variáveis já exportadas no terminal têm precedência: uma `MSSQL_SA_PASSWORD` antiga no processo pode sobrescrever o arquivo. A API no host **não lê `.env`** e não há biblioteca dotenv. Valide na raiz com `docker compose config --quiet`, sem imprimir a configuração expandida.

Depois de preencher o `.env`, inicie o SQL na raiz:

```powershell
docker compose up -d
```

Depois do healthcheck, em `app/backend/src/Locadora.Api`:

```powershell
dotnet run
```

O perfil HTTP usa `Development` e `http://localhost:5000`, a mesma porta configurada no frontend. Para execução explícita a partir de `app/backend`, sem perfil:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Locadora.Api --configuration Release --no-launch-profile --urls http://localhost:5000
```

Em outro terminal, inicie o frontend conforme `../frontend/README.md`. Não há proxy de mesma origem.

`MigrateAsync` só roda em Development, antes de aceitar HTTP. Testing prepara schema pela fixture. Outros ambientes não criam/aplicam schema automaticamente. `/health` verifica apenas o processo, sem consulta contínua ao SQL. A factory de design requer `ConnectionStrings__Locadora` explícita; gerar migration não conecta ao banco, aplicá-la conecta.

## Contratos e segurança

| Rota | Entrada | Resultado |
| --- | --- | --- |
| `POST /api/v1/registro` | `username`, `email`, `password` | 201 com `empresaId`, `usuarioId`; sem Location por exceção aprovada; sem tokens/sessão |
| `POST /api/v1/session/login` | `username`, `password` | JWT, refresh opaco, sessão e expiração |
| `POST /api/v1/session/refresh` | `refreshToken` | rotação na mesma sessão/família, sem extensão das oito horas |
| `GET /api/v1/session` | bearer | identidade mínima; exige conta, versão e sessão válidas |
| `POST /api/v1/session/logout` | bearer | 204 idempotente; revoga sessão e família no SQL |

Usuário/e-mail têm o limite de 256 caracteres das colunas Identity. Senha mínima de oito caracteres, sem composição extra; login bloqueia após cinco falhas por 15 minutos. Limites independentes por IP remoto: registro 3/10 minutos, login 5/minuto e refresh 30/minuto, janela fixa, sem fila. São limites locais em memória, não distribuídos. Não confiar em `X-Forwarded-For` sem cadeia de proxy aprovada.

JWT HS256 tem `kid` conhecido no mapa local, `sub`, `sid`, `jti`, `iat`, `exp`, `versaoAcesso` e `empresaId`. Chaves antigas autorizadas somente validam; a ativa assina. Expiração é o menor prazo entre 15 minutos e o limite absoluto de oito horas. Requisições protegidas consultam conta/sessão/versão no SQL e falham fechadas em indisponibilidade. Respostas da API têm `no-store`. Tokens nunca são enviados por cookie nem em URL.

O controller `/api/v1/teste/tenant` existe **somente na assembly de testes**, adicionado pela fixture. O schema contém recurso sintético para provar filtro, guarda, attach/update, FK composta e histórico de auditoria; não é um cadastro de negócio nem uma API administrativa entregue.

## Migrations e rollback

As migrations iniciais preexistentes na implementação foram preservadas. `CorrigirContratosESegurancaF1` remove o campo empresarial não aprovado, reforça conta única/FKs, preenche o tenant dos hashes anteriores e revoga sessões do contrato antigo. O `Down` não recupera valores removidos de nome empresarial. `ProtegerEscritasTenant` registra o token de concorrência do tenant no modelo, sem DDL adicional.

Não foram aplicadas essas migrations ao volume de desenvolvimento preexistente durante a revisão. Antes de atualizar um banco existente, revisar schema/dados e preservar cópia; não remover volume para contornar erro. Fora de Development, schema exige procedimento separado. Rollback operacional local é parar a nova API e preservar o volume para diagnóstico; não fazer downgrade destrutivo ou restaurar código antigo que reative um contrato de autenticação vulnerável.

## Verificação visual isolada

```powershell
pwsh -NoProfile -File scripts/Testar-Integracao.ps1 -Visual
```

Após passar o backend, inicia API em `http://localhost:5001` e Angular em `http://localhost:4201`, com SQL/chaves de teste próprios. As portas devem estar livres. O frontend usa a configuração pública `verificacao-f1`, sem proxy. Logs e PIDs, sem valores secretos, ficam em `%LOCALAPPDATA%/Temp/opencode`. Encerrar a árvore do processo frontend criado por essa execução permite ao script finalizar a API e remover apenas seu SQL/volume sintético.
