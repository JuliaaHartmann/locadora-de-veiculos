# Preparação inicial do Azure SQL da LocaCar

## Why

O banco de produção informado para a LocaCar é novo. A F6 precisa validar o acesso pelo runner e separar a credencial administrativa de migrations da credencial restrita que a API usará.

## What Changes

- Usar um workflow automático na `main`: build e publish, migrations e grants, App Settings e deploy da API via OIDC.
- Provisionar o usuário SQL contido na primeira execução e reaplicar os grants específicos por tabela nas seguintes.
- Preservar a rede e a branch do frontend. A execução na Azure e a integração exigem autorização separada.

## Capabilities

### New Capabilities

- `banco-producao`: conectividade, configuração inicial e manutenção dos privilégios do banco de produção.

### Modified Capabilities

Nenhuma.

## Impact

GitHub Actions, Azure SQL e scripts operacionais em `app/backend/`. Sem alteração dos contratos HTTP ou da interface LocaCar. Rastreabilidade: RNF-01, RNF-02, RNF-05 e RNF-07; DT-04, DT-16 e DT-25; F6-03 e F6-04.
