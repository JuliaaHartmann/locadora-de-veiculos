# Design — configuração inicial do Azure SQL

## Context

O SQL lógico `locacar.database.windows.net`, o banco `locacar-db` e o App Service `locacar-api` foram informados como criados. O banco ainda não recebeu migrations. A rede pública está restrita a redes selecionadas com a exceção de serviços Azure habilitada; o acesso pelo runner permanece sem evidência. A decisão aprovada está em [DT-25](../../../docs/tdd.md#dt-25--credenciais-e-configuração-inicial-do-azure-sql-f6).

## Goals / Non-Goals

- Entregar a configuração inicial como etapa manual do workflow que futuramente hospedará o deploy do backend.
- Preservar o workflow do frontend em sua branch e manter publicação, mudanças de rede e uso efetivo de secrets fora da implementação local.

## Decisions

1. **GitHub Actions.** Usar `workflow_dispatch`, com `validar-conectividade` como opção padrão e `configurar-banco` como operação explícita. Restringir à `refs/heads/main`, serializar o acesso de produção e usar Repository Secrets, sem GitHub Environments. O diagnóstico usa somente `SQLSERVER_CONNECTIONSTRING`. Os dois modos começam com uma conexão autenticada ao destino esperado e uma consulta de leitura. Não há gatilho por push, etapas de OIDC, publicação ou chamada a outro workflow no recorte inicial.
2. **Execução .NET.** Usar o SDK `10.0.404` de `app/backend/global.json`, EF Core `10.0.15` e `Microsoft.Data.SqlClient` `6.1.7` já resolvido pelo backend. Reutilizar o contexto e as migrations de `Locadora.Infrastructure`; manter a ferramenta operacional em `app/backend/`, sem inicializar a API. Ao configurar, validar ambos os secrets como conexões SQL explícitas para `locacar-db` com criptografia e verificação de certificado, e validar o usuário esperado antes de migrar.
3. **Usuário e grants.** Após validar a segurança de auditoria e tracing sem desativá-los e obter aprovação explícita, habilitar manualmente a variável de repositório `AZURE_SQL_CONFIGURACAO_APROVADA=true`, sem variável homônima na organização. Sem o valor, o job encerra antes da etapa com secrets. Após as migrations, abrir uma transação e consultar a existência do principal antes de enviar o lote de criação; somente na primeira criação, obter a senha de `SQLSERVER_RUNTIME_CONNECTIONSTRING` em memória e fornecê-la ao lote externo parametrizado. A reaplicação nunca envia a senha como parâmetro SQL nem a redefine. O script de grants mantém uma matriz única usada na aplicação e na checagem dos privilégios efetivos sob o contexto de `locacar_api`, antes do commit. Uma conexão independente depois do commit confirma a autenticação.
4. **Escopo de privilégios.** `SELECT/INSERT/UPDATE` nas tabelas operacionais de acesso, cadastros, preços, locações mutáveis, fotos e `ContadoresCodigo` conforme o código; `SELECT/INSERT` em `Empresas`, snapshots, itens, fechamentos e idempotência; `SELECT/INSERT/DELETE` em `VinculosPfPj`; `INSERT` em `Auditorias`. Não conceder acesso ao histórico EF, às tabelas de Identity não usadas ou a objetos por grants de schema. Conferir a matriz confrontando o uso real e o schema migrado.

## Risks / Trade-offs

- **O runner pode sair por outro IP ou provedor** → validar no próprio job antes de migrations. Se houver bloqueio de firewall, encerrar e revisar uma regra temporária automatizada em decisão separada.
- **Migrations, grants e autenticação têm limites distintos** → uma falha antes do commit da configuração reverte a criação e os grants da execução, mas não as migrations concluídas. Uma falha de autenticação após o commit encerra o job e preserva migrations, usuário e grants já confirmados. Diagnosticar e reexecutar; não executar `Down` nem redefinir senha automaticamente.
- **O frontend tem seu próprio gatilho de push na `main`** → os dois disparos manuais não o acionam. A integração por PR é um evento distinto e deve ser coordenada se o workflow do frontend já estiver na `main`.
- **Repository Secrets não são restritos à `main` por este YAML** → a guarda limita a execução do job definido aqui; outros workflows elegíveis ou versões alteradas em outras branches podem referenciar os secrets. Sem GitHub Environments, não há regra de branch nem revisão de acesso aos secrets fornecida por um ambiente.

## Migration Plan

Após revisão e autorização separada, disponibilizar o arquivo na branch padrão e confirmar os dois secrets SQL no escopo do repositório. Executar primeiro o diagnóstico. Conferir auditoria e diagnósticos do servidor e do banco no portal e, separadamente, as sessões de Extended Events. Validar a captura e a redação do lote em ensaio isolado com configuração equivalente e senha descartável; sem essa evidência, manter a variável de aprovação ausente. Após aprovação explícita, habilitar a variável de repositório e executar a configuração inicial, que repete o diagnóstico. Só em uma etapa posterior, substituir esse modo por migrations e grants automáticos antes do deploy.
