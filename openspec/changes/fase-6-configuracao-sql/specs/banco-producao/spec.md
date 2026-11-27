# Banco e publicação do backend

## Purpose

Publicar o backend da LocaCar com credenciais SQL separadas e acesso mínimo ao banco de produção.

## ADDED Requirements

### Requirement: Publicação automática do backend

Um único workflow SHALL executar em pushes na `main` protegida e sequenciar build e publish, migrations e permissões, configuração do App Service via OIDC e deploy. Os segredos SHALL vir de Repository Secrets e ser fornecidos somente às etapas que os utilizam.

#### Scenario: Merge na main
- **WHEN** o merge atualiza a `main`
- **THEN** o workflow executa a sequência em ordem, interrompe a publicação se uma etapa falhar e não publica o frontend.

#### Scenario: Banco Serverless temporariamente indisponível
- **WHEN** a abertura da conexão retorna `40613`
- **THEN** a ferramenta tenta reconectar até duas vezes com espera progressiva, sem repetir migrations já iniciadas.

### Requirement: Privilégios específicos por tabela

A API SHALL receber apenas as permissões necessárias. O usuário SQL contido SHALL ser criado somente se estiver ausente, e a senha SHALL ser lida de `SQLSERVER_RUNTIME_CONNECTIONSTRING` sem ser escrita no código, logs do workflow ou pacote.

#### Scenario: Criação ou reexecução
- **WHEN** as migrations são concluídas
- **THEN** a ferramenta cria o usuário se ausente, aplica e valida os grants antes do commit e confirma a autenticação em conexão independente antes de configurar o App Service.

#### Scenario: Divergência ou falha de autenticação
- **WHEN** a matriz diverge ou a autenticação independente falha
- **THEN** a publicação é interrompida; apenas a falha pré-commit reverte a criação e os grants da execução, preservando as migrations concluídas.

#### Scenario: Evolução do schema
- **WHEN** uma migration muda as permissões necessárias à API
- **THEN** o mesmo PR acompanha a mudança de grants ou revokes sem conceder DDL ou acesso ao histórico de migrations.
