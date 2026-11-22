# Tasks

Revisão de 22/11/2026: as marcações anteriores não tinham evidência suficiente. Itens reabertos para correção e verificação, não para redução de escopo. Registro sem dado empresarial e sem autenticação automática, Signal Forms, SQL de testes exclusivo e limite do refresh autorizados pelo solicitante via `question`.

## 1. Compatibilidade e estrutura da aplicação

- [x] 1.1 Verificar .NET 10, Node, Angular 22 e UI da DT-02 (Tailwind/Lucide); registrar versões e lockfiles; executar restore/build e verificar dependências vulneráveis.
- [x] 1.2 Verificar solução, quatro camadas e referências permitidas; manter código e testes em `app/`.
- [x] 1.3 Verificar SPA standalone zoneless, origens configuráveis e ausência de proxy de mesma origem.

## 2. Infraestrutura local e configuração segura

- [x] 2.1 Fixar digest SQL Server 2022 Developer, healthcheck, volume e loopback; verificar Compose e separação da instância de testes.
- [x] 2.2 Verificar User Secrets somente em Development; testes recebem configuração explícita e material próprio; não versionar segredos.
- [x] 2.3 Validar mapa de chaves JWT de 32 bytes, versão ativa, origens exatas e precedência; falhar sem configuração válida.
- [x] 2.4 Verificar migrations versionadas e ausência de DDL automático fora de Development; fixture prepara catálogo isolado.

## 3. Modelo de identidade, empresa e sessões

- [x] 3.1 Verificar empresa, conta única, sessão, refresh/família, versão e auditoria no SQL real.
- [x] 3.2 Registro atômico sem nome empresarial nem emissão de tokens; usuário normalizado único e senha mínima de oito caracteres sem composição extra; provar rollback.
- [x] 3.3 JWT HS256 com kid conhecido, claims da seção 6.2, 15 minutos limitados às oito horas e tolerância de 30 segundos; testar assinatura, emissor, audiência, algoritmo, tempo e chaves inválidos/anteriores.
- [x] 3.4 Login genérico, bloqueio após cinco falhas por 15 minutos e criação transacional da sessão; verificar bloqueio e expiração.
- [x] 3.5 Refresh hash, rotação transacional serializada, conta/sessão/versão, expiração absoluta e revogação da família por reúso; testar concorrência em conexões separadas.
- [x] 3.6 Consulta e logout idempotente, revogação de sessão e família com auditoria; access/refresh negados imediatamente após logout.

## 4. Registro, CORS e isolamento de empresa

- [x] 4.1 Registro 3/10 minutos/IP e refresh 30/minuto/IP, configuráveis e sem fila; testar 429, Retry-After e ausência de gravação excedente.
- [x] 4.2 Login 5/minuto/IP sem fila; testar 429 genérico sem tokens.
- [x] 4.3 CORS por origem exata, sem credenciais/curinga; testar preflight e interceptor restrito à API.
- [x] 4.4 Contexto autorizado pela conta/sessão; filtros, guarda de escrita e relações não podem cruzar empresas; testar empresa forjada.
- [x] 4.5 ProblemDetails com status, code e traceId em autenticação, limite, isolamento e indisponibilidade; sem SQL/stack/credenciais.

## 5. Cliente de sessão e testes de comportamento

- [x] 5.1 Memória apenas, guard, interceptor e renovação serializada; recarga exige login; falha de logout informa revogação não confirmada.
- [x] 5.2 MSTest unitário determinístico para políticas, expiração, limites e contexto; cenários de persistência complementados por integração SQL.
- [x] 5.3 WebApplicationFactory com configuração explícita, SQL real exclusivo e catálogo isolado; testar migrations, Identity, sessão, erros e CORS.
- [x] 5.4 Empresas A/B: leitura, escrita com referência cruzada, empresa forjada, histórico e revogação; nenhum acesso cruzado.
- [x] 5.5 SQL indisponível em operação protegida e sessão/registro; falha fechada e sem sucesso parcial.
- [x] 5.6 Signal Forms em registro/login, confirmação local, erros por campo, foco, links e navegação; sem persistência de credenciais.

## 6. Verificação da F1

- [x] 6.1 Executar CA-01 em duas origens e SQL real; registrar data, comandos, ambiente, resultados e limitações.
- [x] 6.2 Executar restore, builds, testes e auditoria de dependências; registrar resultados reais.
- [x] 6.3 Revisar diff, ausência de segredos/gerados indevidos/escopo posterior; validar OpenSpec estrito.
- [x] 6.4 Exercitar registro visual: sucesso, validação, erro da API, teclado e recarga; registrar evidências e limitações.

## Workflow follow-up

- Revisar e validar artefatos antes da implementação correspondente.
- Sincronizar specs e arquivar somente após implementação e verificações completas, sem declarar aprovação independente ou prontidão operacional.
