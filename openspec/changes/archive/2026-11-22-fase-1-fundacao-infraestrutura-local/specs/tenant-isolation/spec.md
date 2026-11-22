# Spec Delta

## Purpose

Garante que a conta administrativa opere exclusivamente sobre a empresa vinculada à sua identidade e que identificadores fornecidos pelo cliente não permitam leitura ou escrita de dados de outra empresa.

## ADDED Requirements

### Requirement: Empresa da sessão resolvida no servidor
O sistema SHALL determinar a empresa autorizada a partir da conta e da sessão autenticadas; valores de empresa enviados no corpo, query ou cabeçalho pelo cliente SHALL ser ignorados para autorização.

#### Scenario: Operação autenticada
- **WHEN** uma conta autenticada acessa uma operação protegida
- **THEN** o sistema usa a empresa vinculada à conta e à sessão para autorizar a operação

#### Scenario: Empresa forjada
- **WHEN** o cliente envia o identificador de outra empresa em corpo, query ou cabeçalho
- **THEN** o sistema não altera o contexto autorizado e processa a solicitação somente no escopo da empresa da sessão

### Requirement: Isolamento de leitura e escrita
O sistema SHALL impedir que uma empresa leia, altere ou vincule recursos de outra empresa, inclusive quando conhece seus identificadores técnicos ou referências relacionadas.

#### Scenario: Leitura com identificador de outra empresa
- **WHEN** uma conta da empresa A solicita um recurso pertencente à empresa B
- **THEN** o sistema não revela o recurso nem seus dados e retorna resposta indistinguível de recurso inexistente ou acesso não autorizado

#### Scenario: Escrita com referência cruzada
- **WHEN** uma conta da empresa A tenta criar ou alterar uma relação usando recurso da empresa B
- **THEN** o sistema rejeita a operação sem modificar dados de A ou B

#### Scenario: Sessão encerrada
- **WHEN** uma requisição usa token ou refresh de sessão já revogada
- **THEN** o sistema rejeita a operação protegida e não consulta dados de negócio como se a sessão estivesse ativa
