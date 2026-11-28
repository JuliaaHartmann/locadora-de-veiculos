<p align="center">
  <img src="app/frontend/public/login/logo-transparente.png" alt="Logotipo da LocaCar" width="280">
</p>

# LocaCar

Sistema web para a gestão administrativa de locadoras de veículos. Reúne cadastros, preços e o ciclo de locação, da contratação à devolução, com dados separados por empresa.

**[Acessar a aplicação publicada](https://icy-grass-02c6dce0f.3.azurestaticapps.net)**

## Funcionalidades

| Módulo | O que oferece |
| --- | --- |
| **Painel** | Página inicial da área administrativa, com acesso aos módulos. |
| **Frota** | Cadastro de grupos de veículos, planos de locação e veículos, com gerenciamento de fotos privadas. |
| **Pessoas** | Cadastro de clientes pessoa física e jurídica, condutores e vínculos entre clientes PF e PJ. |
| **Preços** | Gestão de taxas de serviço, seguros e preços de combustível, com histórico de vigências do combustível. |
| **Aluguéis** | Contratação, edição de aluguéis em aberto, devolução, cálculo de valores e consulta ao histórico. |

## Tecnologias e arquitetura

| Área | Tecnologias |
| --- | --- |
| Frontend | Angular 22.3, TypeScript e Tailwind CSS 4 |
| Backend | C#, ASP.NET Core 10, Entity Framework Core 10 e ASP.NET Core Identity com JWT |
| Banco de dados | SQL Server 2022 Developer via Docker Compose no desenvolvimento; Azure SQL Database em produção |
| Fotos | SkiaSharp para processamento, armazenamento local no desenvolvimento e Cloudflare R2 privado em produção |

A SPA chama diretamente a API REST (`/api/v1`). O backend é um monólito organizado em quatro camadas: **Domain** (regras e entidades), **Application** (contratos dos casos de uso), **Infrastructure** (persistência e armazenamento) e **Api** (HTTP e autenticação). O navegador acessa as fotos pela API, sem acesso direto ao bucket.

| Caminho | Conteúdo |
| --- | --- |
| [`app/frontend/`](app/frontend/) | Interface Angular |
| [`app/backend/`](app/backend/) | API, camadas de negócio, migrations e scripts operacionais |
| [`docs/`](docs/) | [Requisitos (PRD)](docs/prd.md) e [decisões técnicas (TDD)](docs/tdd.md) |
| [`openspec/`](openspec/) | [Especificações funcionais](openspec/specs/) e [mudanças](openspec/changes/) |

## Execução local

**Pré-requisitos:** .NET SDK `10.0.404`, Node.js `24.21.0`, npm `11.19.0` e Docker com Compose capaz de executar contêineres Linux x86-64.

### 1. Configurar o ambiente

Na raiz, copie [`.env.example`](.env.example) para `.env` e preencha `MSSQL_SA_PASSWORD`. A API não lê esse arquivo. Configure a conexão e a chave JWT nos **User Secrets** do projeto `app/backend/src/Locadora.Api`; a tabela mostra os nomes equivalentes para variáveis de ambiente.

| Configuração | Onde definir | Exemplo ou valor esperado |
| --- | --- | --- |
| `MSSQL_SA_PASSWORD` | `.env` local, na raiz | `<SENHA_FORTE_LOCAL>` |
| `ConnectionStrings:Locadora` / `ConnectionStrings__Locadora` | User Secrets da API ou ambiente do processo | `Server=127.0.0.1,1433;Database=LocadoraLocal;User Id=sa;Password=<SENHA_FORTE_LOCAL>;Encrypt=True;TrustServerCertificate=True` |
| `Autenticacao:Jwt:Chaves:v1` / `Autenticacao__Jwt__Chaves__v1` | User Secrets da API ou ambiente do processo | `<CHAVE_BASE64_DE_32_BYTES_ALEATORIOS>` |

Use a mesma senha local do SQL no `.env` e na conexão. O exemplo de certificado se aplica apenas ao SQL local. O armazenamento local de fotos dispensa credenciais R2. Para detalhes, consulte o [README do backend](app/backend/README.md) e o [contrato de fotos](app/backend/docs/contratos-f5.md).

### 2. Iniciar o banco e a aplicação

Na raiz do repositório, inicie o SQL Server e aguarde o contêiner ficar saudável:

```bash
docker compose up -d
```

Em um terminal, a partir da raiz, inicie a API:

```bash
cd app/backend/src/Locadora.Api
dotnet run
```

Em outro terminal, a partir da raiz, inicie o frontend:

```bash
cd app/frontend
npm ci
npm start
```

A interface fica em **http://localhost:4200** e chama **http://localhost:5000/api/v1**. O perfil local da API aplica as migrations ao iniciar em `Development`.

## Produção e deploy

| Serviço | Papel |
| --- | --- |
| Azure Static Web Apps | Hospeda o frontend Angular e atende às rotas da SPA. |
| Azure App Service | Executa a API ASP.NET Core em Windows, com .NET 10. |
| Azure SQL Database | Armazena os dados da aplicação. O usuário de execução tem permissões separadas da credencial de migrations. |
| Cloudflare R2 | Armazena as fotos em bucket privado, acessado pela API. |

Os [workflows do GitHub Actions](.github/workflows/) são disparados quando a integração atualiza a `main`. O backend gera um pacote Windows x64 dependente do runtime, aplica migrations e permissões, configura o App Service e publica a API usando OIDC. O frontend compila a SPA e publica os arquivos estáticos. Os valores sensíveis usados pelos workflows ficam em GitHub Repository Secrets; apenas a conexão SQL restrita é enviada ao App Service para uso da API.

## Autoria e contexto acadêmico

Projeto de conclusão de curso desenvolvido por [Júlia Hartmann](https://github.com/JuliaaHartmann).
