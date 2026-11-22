# registro-empresa Specification

## Purpose

Disponibilizar a entrada visual do registro público para que uma empresa crie sua conta administrativa sem depender de chamada manual à API.

## Requirements

### Requirement: Tela de registro de empresa

A SPA SHALL disponibilizar uma tela pública em `/registro` com Signal Forms e campos de usuário, e-mail, senha e confirmação de senha, além de ação explícita para criar a conta. Não SHALL exigir nome empresarial ou dados adicionais ao RF-01.

#### Scenario: Registro visual válido

- **WHEN** a pessoa preenche os campos obrigatórios com dados válidos e confirma a senha
- **THEN** a SPA envia somente usuário, e-mail e senha a `POST /api/v1/registro`, não persiste credenciais no armazenamento do navegador e encaminha a pessoa para `/login` após sucesso; o registro não emite tokens nem autentica automaticamente

#### Scenario: Validação local do formulário

- **WHEN** a senha tem menos de oito caracteres, as senhas divergem ou um campo obrigatório está vazio
- **THEN** a SPA impede o envio e informa o erro junto ao campo correspondente

#### Scenario: Erro devolvido pela API

- **WHEN** a API rejeita o registro por conflito, limite de requisições ou indisponibilidade
- **THEN** a SPA mantém os dados não sensíveis preenchidos, não afirma que a conta foi criada e apresenta uma mensagem compreensível

#### Scenario: Navegação entre acesso e registro

- **WHEN** a pessoa acessa `/login` ou `/registro`
- **THEN** cada tela oferece um link visível e navegável por teclado para a outra tela
