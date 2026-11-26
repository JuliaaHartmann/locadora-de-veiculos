# Delta — account-access

## ADDED Requirements

### Requirement: Apresentação acessível do Painel autenticado em desktop

A SPA SHALL apresentar o Painel autenticado e a navegação compartilhada com a identidade visual aprovada, adaptando-os a desktops, notebooks, janelas e zoom, com controles e mensagens legíveis e operáveis por teclado e saída disponível nos módulos autenticados (RF-02, J-01, RNF-01, RNF-04, CA-01 e CA-11).

#### Scenario: Painel e acessos existentes

- **WHEN** a pessoa autenticada abre o Painel em desktop ou notebook
- **THEN** a tela apresenta o logotipo aprovado, navegação para Início, Frota, Pessoas, Preços e Aluguéis, o título Início e os quatro acessos existentes aos módulos no conteúdo, além da ação Sair

#### Scenario: Rota ativa identificável

- **WHEN** uma rota da navegação autenticada está ativa
- **THEN** o respectivo link é identificado visualmente por mais de um recurso, sem depender apenas da cor, e possui `aria-current="page"`

#### Scenario: Janela reduzida ou ampliação

- **WHEN** a pessoa reduz a janela ou usa zoom de 200% no navegador de desktop ou notebook
- **THEN** a navegação, os quatro acessos, Sair e mensagens permanecem legíveis e acessíveis por teclado, com foco visível e sem exigir rolagem horizontal para operá-los

#### Scenario: Navegação aos módulos

- **WHEN** a pessoa aciona um dos links existentes da navegação ou dos acessos do Painel
- **THEN** segue para a rota correspondente, sem alterar as funcionalidades do módulo de destino

#### Scenario: Saída confirmada

- **WHEN** a pessoa aciona Sair em uma rota autenticada, a saída é autorizada e a revogação remota é confirmada
- **THEN** a sessão em memória é encerrada e a pessoa é encaminhada ao login

#### Scenario: Falha de revogação remota

- **WHEN** timeout, desconexão ou falha do servidor deixam a revogação sem confirmação
- **THEN** a tela preserva temporariamente a tela e os dados em memória, suspende novas gravações e não apresenta a saída como concluída
- **AND** permite nova tentativa explícita para a mesma sessão quando autenticável; só libera gravações após confirmar que a sessão segue ativa, que a revogação anterior terminou sem revogar e não pode mais produzir efeitos, e encerrar a tentativa de saída; se não houver recuperação segura, exige reautenticação

#### Scenario: Coordenar proteções e solicitações repetidas
- **WHEN** a pessoa aciona Sair
- **THEN** as proteções são avaliadas antes da revogação e reavaliadas após eventual confirmação; novas gravações não começam durante o envio da revogação
- **AND** solicitações simultâneas da mesma sessão não abrem outro diálogo nem disparam outra revogação em voo; navegar ao login após saída autorizada não pede nova confirmação

#### Scenario: Coordenar saída e renovação
- **WHEN** a saída autorizada coincide com renovação em andamento ou não há bearer válido
- **THEN** aguarda a renovação em andamento e, se ainda necessário e a sessão for renovável, permite no máximo uma renovação controlada exclusivamente para revogar; não repete refresh de resultado incerto ou comandos de negócio automaticamente
- **AND** respostas tardias da sessão anterior não restauram credenciais, não alteram uma sessão posterior, não repetem comandos nem redirecionam essa sessão

#### Scenario: Sessão expirada ou reautenticação necessária
- **WHEN** a expiração é confirmada ou não é possível recuperar o acesso protegido com segurança
- **THEN** encerra o contexto local em memória, bloqueia acesso protegido e encaminha ao login com aviso distinto de sucesso de logout
- **AND** nem o encerramento local nem um `401` isolado são prova de revogação no servidor

#### Scenario: Ação de saída compartilhada

- **WHEN** a navegação compartilhada é apresentada em outra rota autenticada
- **THEN** ela identifica a rota ativa e apresenta a ação funcional Sair na região inferior do menu

#### Scenario: Confirmar saída com alterações não salvas
- **WHEN** a pessoa aciona Sair com alterações não salvas
- **THEN** antes da revogação, a tela apresenta a confirmação “Sair do sistema?” e “Existem alterações não salvas. Se você sair agora, essas alterações serão perdidas.”, com ações Cancelar e Sair
- **AND** o foco inicial fica em Cancelar; cancelar preserva os dados e devolve o foco ao controle que abriu a confirmação

#### Scenario: Confirmar saída após falha de foto
- **WHEN** o veículo foi cadastrado, o envio da foto falhou e a pessoa solicita Sair
- **THEN** antes da revogação, a tela pede confirmação informando que o veículo já foi cadastrado e que a seleção pendente da foto será perdida
- **AND** cancelar preserva o estado e devolve o foco ao controle de saída

#### Scenario: Operação de gravação ou foto em andamento
- **WHEN** há salvamento, inclusive contratação ou devolução, ou envio, substituição ou remoção de foto em andamento
- **THEN** Sair fica temporariamente desabilitado com indicação acessível do motivo até a operação ser resolvida
- **AND** a tela não presume que cancelar uma requisição desfaça uma operação no servidor

#### Scenario: Proteção existente da devolução
- **WHEN** uma contratação ou devolução tem resultado incerto e a pessoa solicita Sair
- **THEN** antes de revogar, a tela avisa que sair fará perder a possibilidade de repetir a mesma tentativa nesta tela
- **AND** cancelar mantém sessão e dados disponíveis e preserva a identidade da tentativa, sem iniciar outra contratação ou devolução
