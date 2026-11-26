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

### Requirement: Apresentação acessível do cadastro de empresa em desktop

A SPA SHALL apresentar `/registro` com a identidade aprovada e adaptar a apresentação a desktops, notebooks, janelas e zoom, mantendo legíveis e operáveis por teclado os campos, ações e estados existentes, sem alterar o registro de empresa (RF-01, J-01, RNF-04, CA-01 e CA-11).

#### Scenario: Identidade e formulário em desktop

- **WHEN** a pessoa abre `/registro` em desktop ou notebook com largura suficiente
- **THEN** ela vê a identidade aprovada e o formulário lado a lado, com logotipo completo, paisagem decorativa, título e texto existentes, campos Usuário, E-mail, Senha e Confirmar senha, ação Criar empresa e link Entrar

#### Scenario: Janela estreita ou ampliação no desktop

- **WHEN** a pessoa reduz a janela ou amplia a página em desktop ou notebook
- **THEN** a identidade completa e a paisagem permanecem visíveis, e os campos, ações e mensagens permanecem legíveis e operáveis por teclado, com foco perceptível e sem rolagem horizontal

#### Scenario: Campos inválidos e resumo

- **WHEN** a pessoa tenta registrar dados inválidos, incluindo campos obrigatórios vazios, usuário apenas com espaços, e-mail inválido, senha curta ou confirmação divergente
- **THEN** a tela impede o envio, mostra mensagens junto aos campos, identifica os campos inválidos e foca o resumo textual de erro

#### Scenario: Associação condicional ao resumo

- **WHEN** um campo é indicado como inválido e o resumo contém uma mensagem
- **THEN** a tela associa o campo ao seu erro individual e ao resumo; ao corrigir o campo ou esvaziar o resumo, remove a associação adicional ao resumo e mantém a associação ao erro individual

#### Scenario: Envio em andamento

- **WHEN** a pessoa envia dados válidos e a resposta ainda não chegou
- **THEN** a tela indica que está criando a conta e mantém a ação de envio desabilitada, sem permitir envio duplicado

#### Scenario: Cadastro concluído

- **WHEN** o registro é aceito
- **THEN** a tela limpa os dados do formulário e encaminha ao login, que apresenta o aviso de empresa criada sem autenticar automaticamente

#### Scenario: Falha do cadastro

- **WHEN** o registro falha por conflito, limitação, indisponibilidade ou outra rejeição
- **THEN** a tela apresenta a mensagem existente adequada, foca o resumo, mantém usuário e e-mail, limpa senha e confirmação e reabilita a ação sem afirmar sucesso

#### Scenario: Retorno ao login

- **WHEN** a pessoa aciona o link Entrar no cadastro usando teclado ou apontador
- **THEN** a navegação segue para `/login`
