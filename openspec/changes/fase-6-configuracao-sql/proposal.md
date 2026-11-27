# Preparação inicial do Azure SQL da LocaCar

## Why

O banco de produção informado para a LocaCar é novo. A F6 precisa validar o acesso pelo runner e separar a credencial administrativa de migrations da credencial restrita que a API usará.

## What Changes

- Preparar dois modos manuais na `main`: somente conectividade e configuração inicial, ambos sem publicação.
- Criar um usuário SQL contido para a API e versionar os grants específicos por tabela. Preservar o caminho para migrations e grants automáticos antes do deploy futuro.
- Priorizar a configuração de rede atual. Alterar firewall, recursos, secrets e workflows de frontend ou ativar a publicação está fora deste recorte.

## Capabilities

### New Capabilities

- `banco-producao`: conectividade, configuração inicial e manutenção dos privilégios do banco de produção.

### Modified Capabilities

Nenhuma.

## Impact

GitHub Actions, Azure SQL e scripts operacionais em `app/backend/`. Sem alteração dos contratos HTTP ou da interface LocaCar. Rastreabilidade: RNF-01, RNF-02, RNF-05 e RNF-07; DT-04, DT-16 e DT-25; F6-03 e F6-04.
