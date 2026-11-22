# Proposal

## Why

A F1 precisa estabelecer a base executável da aplicação antes dos cadastros e das locações: uma empresa deve poder criar sua única conta administrativa, autenticar-se e operar com sessão revogável. Sem isolamento de empresa, proteção do registro público e um ambiente local reproduzível com SQL Server real, as fases seguintes não terão uma fundação verificável para consistência, segurança e testes.

## What Changes

- Criar a fundação da aplicação em `app/`, com solução .NET e SPA Angular em origens distintas.
- Disponibilizar infraestrutura local somente para o SQL Server via `compose.yml`, com healthcheck, volume nomeado e porta restrita ao loopback.
- Implementar o registro atômico de empresa e conta administrativa, com unicidade de usuário e rate limit obrigatório definido nesta change.
- Disponibilizar a tela pública Angular de registro de empresa em `/registro`, integrada ao endpoint de registro e com validações de formulário.
- Implementar login, refresh rotativo em memória no cliente, consulta de sessão e logout com revogação imediata no servidor.
- Aplicar JWT HS256 versionado, Identity para credenciais e bloqueio, configuração segura de chaves e User Secrets em Development.
- Aplicar CORS por origem exata e resolução server-side do `EmpresaId`, protegendo operações contra acesso entre empresas.
- Configurar migrations para execução automática somente em Development e testes MSTest com `WebApplicationFactory` e SQL Server real.
- Validar CA-01, incluindo logout efetivo, reúso de credenciais revogadas, falha fechada com SQL indisponível e isolamento entre empresas.
- Manter fora do escopo desta change os cadastros, fotos, locações, CI, Azure e qualquer implementação das fases posteriores.

## Capabilities

### New Capabilities

- `account-access`: registro público de empresa e conta administrativa, autenticação, sessão revogável, refresh, logout e limitação de abuso.
- `registro-empresa`: tela pública de registro, validações locais e navegação com login, sem dados empresariais extras.
- `tenant-isolation`: associação server-side entre conta, sessão e empresa, com autorização e proteção contra acesso cruzado.

### Modified Capabilities

- Nenhuma.

## Impact

- Novos módulos backend Clean Architecture em `app/backend`, frontend Angular em `app/frontend` e testes em `app/backend/tests`.
- Novo `compose.yml` na raiz para SQL Server Developer local; configurações sensíveis ficam fora do versionamento.
- Novos contratos REST sob `/api/v1/registro` e `/api/v1/session`, além de CORS e ProblemDetails.
- Dependências-alvo: .NET 10, ASP.NET Core Identity, EF Core, SQL Server, Angular 22, MSTest, Vitest e Playwright, sujeitas à verificação de compatibilidade antes da implementação.
- A mudança estabelece contratos de segurança e sessão usados pelas fases F2–F6, sem criar recursos de publicação ou alterar `openspec/config.yaml`. A revisão autorizada de 22/11/2026 reconcilia J-01/SDD com RF-01, Signal Forms com DT-01 e registra no PRD/TDD o estado verificado, as versões, o limite do refresh e a exceção de Location do registro, sem antecipar funcionalidades posteriores.
