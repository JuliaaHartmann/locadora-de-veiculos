# Especificação — códigos de apresentação

## Purpose

Identificar registros da operação com códigos legíveis e permanentes, preservando as chaves técnicas e as referências históricas.

## Requirements

### Requirement: Apresentar códigos permanentes

O sistema SHALL usar códigos de `0001` a `999999`, únicos por empresa e tipo, para Grupos, Planos, Veículos, Clientes, Condutores, Taxas e serviços, versões de combustível e Aluguéis (RF-20, DT-23).

#### Scenario: Identificar um registro na interface
- **WHEN** um dos oito recursos é listado ou o usuário consulta uma referência que mostrava o UUID
- **THEN** o cabeçalho é **Código** e a API e a SPA apresentam o mesmo código com no mínimo quatro dígitos, crescendo até seis sem truncamento, sem alterar o número persistido, UUID interno, URL, permissões ou relacionamentos

#### Scenario: Concorrência, reversão e repetição
- **WHEN** ocorrem criações concorrentes, falha de escrita ou repetição idempotente
- **THEN** a alocação compartilha a transação, não duplica códigos dentro da empresa e tipo, não consome código em reversão e não aloca outro no replay

#### Scenario: Migrar acervo e preservar o histórico
- **WHEN** a implantação preenche o acervo ou um registro é ocultado, excluído logicamente ou editado
- **THEN** o preenchimento inclui todos os registros em ordem de criação com desempate pelo UUID, e o código atribuído não muda nem é reutilizado

#### Scenario: Atingir a capacidade de uma sequência
- **WHEN** um tipo de recurso atinge `999999` em uma empresa
- **THEN** a criação desse tipo falha com mensagem clara e sem efeitos parciais, preservando leituras e operações existentes e as sequências dos outros tipos e empresas
