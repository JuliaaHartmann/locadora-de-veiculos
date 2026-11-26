# Delta — registro-empresa

## ADDED Requirements

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
