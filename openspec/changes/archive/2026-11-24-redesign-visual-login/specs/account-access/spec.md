# Delta — account-access

## ADDED Requirements

### Requirement: Apresentação da tela pública de login em desktop

A SPA SHALL apresentar a identidade visual aprovada e o formulário de `/login` de forma adaptável às resoluções de desktops e notebooks, à largura da janela e à ampliação do navegador, mantendo os controles e estados existentes legíveis e operáveis por teclado, sem alterar a autenticação por usuário e senha (RF-02, J-01, RNF-04, CA-01, CA-11).

#### Scenario: Identidade e formulário em desktop

- **WHEN** a pessoa abre `/login` em um desktop ou notebook com largura suficiente para a composição em duas colunas
- **THEN** ela vê lado a lado a identidade com o logotipo completo e a ilustração decorativa e o formulário com o título Entrar, os rótulos visíveis Usuário e Senha, a ação Entrar e o link Criar empresa

#### Scenario: Janela estreita ou ampliação no desktop

- **WHEN** a pessoa reduz a largura da janela ou amplia a página no navegador de um desktop ou notebook
- **THEN** o logotipo completo e a ilustração decorativa permanecem visíveis no painel de identidade, e os campos, as ações e as mensagens permanecem legíveis e operáveis por teclado, com foco perceptível e sem rolagem horizontal

#### Scenario: Preenchimento obrigatório e foco

- **WHEN** a pessoa envia o formulário com um ou ambos os campos obrigatórios vazios
- **THEN** o formulário não envia a tentativa, identifica por texto cada campo inválido com erro associado e posiciona o foco visível no resumo

#### Scenario: Envio em andamento

- **WHEN** a pessoa submete o formulário preenchido e o resultado ainda não chegou
- **THEN** a ação Entrar permanece desabilitada até a resposta, sem permitir envio duplicado

#### Scenario: Retorno de autenticação bem-sucedido

- **WHEN** a tentativa com usuário e senha é aceita
- **THEN** a senha é limpa do formulário e a pessoa é encaminhada ao painel existente

#### Scenario: Falha de autenticação

- **WHEN** a tentativa de autenticação falha
- **THEN** a tela mostra o erro genérico existente sem expor dados da conta, limpa a senha sem apagar o usuário, reabilita a ação e posiciona o foco visível no resumo

#### Scenario: Aviso após o registro

- **WHEN** a pessoa chega ao login após criar a empresa pelo registro público
- **THEN** o aviso existente de sucesso do registro fica perceptível e o formulário de login continua disponível sem autenticação automática

#### Scenario: Acesso ao registro por teclado

- **WHEN** a pessoa navega por teclado no login e aciona Criar empresa
- **THEN** o link visível e com foco perceptível leva à tela pública `/registro` existente, conforme o cenário Navegação entre acesso e registro da capacidade `registro-empresa`
