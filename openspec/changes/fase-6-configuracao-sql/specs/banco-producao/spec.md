# Banco de produção

## Purpose

Preparar o banco da LocaCar com credenciais separadas e permitir que a API execute apenas as operações necessárias em produção.

## ADDED Requirements

### Requirement: Diagnóstico de conectividade isolado

O workflow SHALL oferecer um modo manual restrito à `main` que valide uma conexão autenticada ao banco de produção com a rede atual e reporte o resultado sem divulgar credenciais. Os dois modos SHALL usar Repository Secrets, sem GitHub Environments.

#### Scenario: Validação de conectividade
- **WHEN** o operador escolhe o modo de conectividade na `main`
- **THEN** o job testa o acesso a `locacar-db` usando a credencial de migrations, sem alterar o banco ou firewall e sem publicar componentes.

#### Scenario: Tentativa fora da main
- **WHEN** o workflow é disparado em outra referência
- **THEN** o job deste workflow não é executado.

### Requirement: Configuração inicial sem publicação

O workflow SHALL exigir um disparo manual separado na `main` e aprovação explícita após a verificação de auditoria e tracing para aplicar as migrations e configurar `locacar_api`. Esse disparo MUST NOT publicar backend ou frontend.

#### Scenario: Aprovação ausente
- **WHEN** o modo de configuração é solicitado sem aprovação explícita
- **THEN** o job encerra antes de acessar o banco ou fornecer credenciais à ferramenta.

#### Scenario: Primeira configuração
- **WHEN** a configuração inicial é solicitada e a conectividade foi confirmada pelo job
- **THEN** o job aplica as migrations com a credencial administrativa, cria o usuário no banco de destino, concede os privilégios versionados e valida o acesso usando a credencial restrita.

#### Scenario: Nova execução após configuração parcial ou concluída
- **WHEN** a configuração é disparada novamente e o usuário já existe
- **THEN** o job preserva sua senha sem enviá-la em um comando SQL, reaplica os grants de forma idempotente e falha se a credencial de runtime não autenticar na conexão independente.

#### Scenario: Falha de conectividade
- **WHEN** o job não consegue se conectar ao banco
- **THEN** ele falha antes de aplicar migrations, sem alterar regras de firewall.

### Requirement: Privilégios específicos por tabela

A API SHALL receber apenas as permissões exigidas pelas operações das tabelas que usa, sem privilégios administrativos, DDL ou acesso ao histórico de migrations. O manifesto de acesso MUST acompanhar as migrations que mudem essas necessidades.

#### Scenario: Evolução do schema
- **WHEN** uma migration futura introduz ou modifica o uso de uma tabela pela API
- **THEN** o mesmo PR atualiza os grants ou registra os revokes necessários, e o futuro fluxo automático de publicação aplica migrations e permissões antes de publicar.

#### Scenario: Acesso excedente ao runtime
- **WHEN** a credencial de runtime tenta executar DDL, alterar ou excluir auditoria, ou consultar o histórico de migrations
- **THEN** o banco nega a operação.

#### Scenario: Divergência antes do commit
- **WHEN** o usuário tem permissões faltantes ou excedentes na validação transacional
- **THEN** a criação do usuário e os grants daquela execução sofrem rollback; migrations previamente aplicadas permanecem.

#### Scenario: Falha de autenticação após o commit
- **WHEN** a conexão independente com a credencial restrita falha após o commit
- **THEN** a execução falha, preservando as migrations, o usuário e os grants já confirmados.
