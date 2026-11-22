# Design

## Context

No início desta change, o repositório continha somente a documentação do produto e do desenho técnico. A F1 respeita o alvo Angular 22 + .NET 10, a organização em `app/`, SQL Server real para integração, duas origens locais e o schema `spec-driven`. Os requisitos funcionais e de segurança estão detalhados em `proposal.md` e nas especificações desta change. O estado executado e suas limitações são registrados separadamente em `evidencias.md`.

## Goals / Non-Goals

**Goals:**

- Criar uma solução executável com API, SPA, persistência Identity/dados da empresa e testes reproduzíveis.
- Garantir registro atômico, autenticação, revogação imediata, refresh com detecção de reúso e isolamento server-side.
- Expor o registro por uma tela Angular pública, acessível em `/registro`, sem persistir credenciais no navegador.
- Definir uma proteção concreta para o registro público, sem reutilizar automaticamente os parâmetros do login.
- Permitir desenvolvimento local com migrations somente em Development e sem segredos versionados.

**Non-Goals:**

- Implementar cadastros, frota, preços, pessoas, locações, fotos ou publicação em Azure.
- Criar recuperação de senha, múltiplas contas, perfis ou administração da plataforma.
- Criar cadastros de negócio ou qualquer tela posterior ao registro e login.
- Criar CI, provisionar serviços externos ou executar migrations automáticas fora de Development.

## Decisions

### Estrutura e composição

- Usar Clean Architecture com `Locadora.Domain`, `Locadora.Application`, `Locadora.Infrastructure` e `Locadora.Api`; a composição de infraestrutura ocorre apenas na API.
- Criar o frontend Angular standalone em `app/frontend` e os testes backend em `app/backend/tests`. A SPA chama a API por URL configurada, sem proxy de mesma origem.
- Verificar SDKs, Node, patches e lockfiles na implementação antes do primeiro build; não fixar versões não verificadas neste planejamento.
- Complemento autorizado em 22/11/2026: materializar DT-02 ainda na F1, com Tailwind CSS 4 via PostCSS e o pacote oficial standalone `@lucide/angular`. Preservar controles nativos, rótulos, foco e mensagens; ícones são decorativos, sem substituir texto ou validação. Verificar versões/peers antes da instalação e repetir build, testes e fluxo visual.

### Persistência e transações

- Usar um `DbContext` baseado em Identity para manter empresa, usuário, sessão, refresh e auditoria no mesmo banco.
- O registro grava empresa e usuário em uma transação única, com `EmpresaId` único no usuário e username normalizado único na plataforma.
- Sessões guardam expiração absoluta, versão de acesso, revogação e família de refresh; o refresh é armazenado somente como hash.
- Toda mutação da F1 usa a unidade transacional da aplicação. A consulta da sessão ocorre em cada requisição protegida; falha de SQL falha fechada.
- Migrations versionadas serão aplicadas por `Database.MigrateAsync()` somente no ambiente Development. Testes prepararão uma base isolada e não usarão SQLite, EF InMemory ou Testcontainers.

### Autenticação e sessão

- Usar ASP.NET Core Identity para hash, normalização e bloqueio de senha. A API emitirá JWT HS256 próprio com `kid`, claims mínimas, access token de 15 minutos e tolerância de relógio de 30 segundos.
- Chaves JWT serão Base64 de exatamente 32 bytes, carregadas de configuração externa; a versão anterior continuará validando tokens durante sua tolerância. Nenhuma chave será gerada automaticamente ou enviada ao frontend.
- A sessão terá limite absoluto de 8 horas. Refresh opaco será mantido em memória na SPA e rotacionado em transação; reúso de refresh consumido revogará a família.
- Logout revogará a sessão no SQL antes de limpar o estado local. A SPA não usará cookies, `localStorage`, `sessionStorage`, IndexedDB ou `withCredentials`.

### Rate limit do registro

- Para materializar a DT-20 nesta SDD, o endpoint público de registro terá limite de **3 requisições por 10 minutos por IP**, em janela fixa, sem fila; excedentes retornam `429` e `Retry-After`.
- O limite será aplicado antes da criação e não contará com o rate limit do login. O IP será obtido somente da cadeia de proxy confiável configurada; em desenvolvimento direto, será o endereço remoto da conexão.
- O login manterá os parâmetros já definidos na DT-19: 5 requisições por minuto por IP e bloqueio por cinco falhas durante 15 minutos.
- Revisão autorizada em 22/11/2026: o refresh terá janela fixa de 30 requisições por minuto por IP, sem fila, configurável, com `429` e `Retry-After`. Testes usarão instância SQL exclusiva, sem volume de Development, credenciais sintéticas em memória e conexão fornecida pelo processo, sem User Secrets pessoais.
- A implementação deve tornar os parâmetros configuráveis e testáveis. Em instância única local, o armazenamento em memória é suficiente para F1; o design não promete distribuição horizontal.

### Isolamento de empresa

- O contexto da empresa será resolvido pela conta e pela sessão no servidor, nunca por valor aceito do corpo, query ou cabeçalho.
- A camada Application receberá o contexto autorizado por uma porta explícita; a Infrastructure aplicará `EmpresaId` em consultas, escritas e relações. A API não aceitará um tenant arbitrário como autorização.
- ProblemDetails não exporá SQL, stack trace, credenciais ou documentos completos. Recursos de outra empresa serão tratados como inexistentes ou não autorizados sem revelar sua existência.

### CORS, configuração e ambiente

- CORS usará allowlist de origens exatas configurada por ambiente, sem `AllowAnyOrigin`, curinga de preview ou `AllowCredentials`. O bearer será enviado apenas à URL da API.
- JSON versionado conterá apenas parâmetros não sensíveis. User Secrets guardarão conexão local, senha do SQL e chaves JWT em Development; variáveis de ambiente sobrescreverão a configuração na execução.
- O `compose.yml` raiz conterá somente SQL Server Developer, healthcheck e volume nomeado, publicando a porta em `127.0.0.1`; API, Angular e testes rodarão no host.

### Tela de registro de empresa

- As rotas públicas `/registro` e `/login` usarão componentes standalone e Signal Forms, conforme DT-01, mantendo estado e credenciais somente em memória durante o envio.
- O formulário enviará somente usuário, e-mail e senha ao contrato `POST /api/v1/registro`; não exigirá nome empresarial. A confirmação de senha será validada no cliente e não será enviada. O registro criará empresa e conta sem emitir tokens ou abrir sessão; o acesso dependerá do login separado.
- Os nomes HTTP seguem o TDD: `username`, `email`, `password`; usuário e e-mail têm máximo de 256 caracteres, conforme o schema do Identity, sem truncamento. A resposta é `201` com `empresaId` e `usuarioId`, sem `Location` (exceção autorizada em 22/11/2026, pois não há rota de consulta da conta criada). Login usa `username` e `password`; refresh usa `refreshToken`.
- Erros de validação serão associados aos campos quando possível; erros de conflito, limite e indisponibilidade serão apresentados como mensagem geral sem expor SQL, stack trace ou credenciais.
- Após sucesso, a tela limpará os dados sensíveis e navegará para `/login` com uma mensagem de confirmação transitória.

### Verificação

- MSTest cobrirá regras de sessão, refresh, bloqueio, rate limit, revogação e resolução do tenant.
- `WebApplicationFactory` com SQL Server real cobrirá registro atômico, migrations, ProblemDetails, CORS, logout, SQL indisponível e empresas A/B com IDs conhecidos.
- A validação da F1 registrará comandos, ambiente, data, resultado e limitações. Nenhum teste será declarado executado antes de sua execução efetiva.

## Risks / Trade-offs

- **[Rate limit local não distribuído]** → manter o mecanismo configurável e documentar que a F1 suporta a instância local; distribuição futura exige decisão própria antes de escalar.
- **[Consulta SQL por requisição protegida]** → aceitar o custo em favor do logout imediato e falha fechada; medir somente quando houver contexto de carga aprovado.
- **[Refresh apenas em memória]** → recarga perde a sessão e exige novo login; isso é intencional para evitar persistência de bearer e refresh no navegador.
- **[Chave HMAC compartilhada]** → restringir a chave ao servidor, usar `kid`, User Secrets/App Settings e rotação procedimental; não publicar JWKS nem compartilhar a chave com o cliente.
- **[Migrations automáticas apenas em Development]** → a inicialização local falha se o banco não estiver pronto; fora de Development, aplicar schema em etapa controlada, sem fallback destrutivo.
- **[SQL Server real aumenta o custo de testes]** → usar Compose/instância isolada e fixtures explícitas; não trocar por SQLite ou InMemory, pois locks, índices e transações são parte do comportamento validado.

## Migration Plan

1. Verificar compatibilidade de SDKs e ferramentas e criar os manifests/lockfiles dentro de `app/`.
2. Subir o SQL Server local pelo Compose, configurar User Secrets e aplicar migrations em Development.
3. Executar testes unitários e de integração da F1 em banco isolado.
4. Em caso de falha, parar a API e preservar o volume para diagnóstico; não executar migration destrutiva nem publicar recursos.

## Open Questions

- Nenhuma questão pendente altera os contratos desta change. A efetividade do limite de registro em ambiente distribuído e a medição de desempenho permanecem temas posteriores, fora da F1 local.
