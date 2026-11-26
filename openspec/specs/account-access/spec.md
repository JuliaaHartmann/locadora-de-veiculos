# account-access Specification

## Purpose

Permite que cada empresa crie sua conta administrativa única e que o administrador acesse o sistema por uma sessão revogável, sem expor credenciais ou permitir abuso do registro e do login.

## Requirements

### Requirement: Registro atômico de empresa e conta
O sistema SHALL aceitar o registro público de uma empresa com usuário, e-mail e senha, criando exatamente uma conta administrativa vinculada à empresa; usuário já existente SHALL ser rejeitado sem criar dados parciais.

#### Scenario: Registro válido
- **WHEN** uma solicitação contém usuário único, e-mail e senha que atendem à política
- **THEN** o sistema cria a empresa e sua única conta administrativa na mesma operação e retorna `201` com `empresaId` e `usuarioId`, sem `Location`, expor a senha, emitir tokens ou abrir sessão; não exige nome empresarial ou dados adicionais ao RF-01

#### Scenario: Usuário duplicado
- **WHEN** uma solicitação usa um usuário já registrado na plataforma
- **THEN** o sistema rejeita o registro com erro de conflito e não cria empresa nem conta órfã

#### Scenario: Senha inválida
- **WHEN** uma solicitação usa senha com menos de oito caracteres ou rejeitada pela política vigente
- **THEN** o sistema rejeita o registro sem persistir a credencial

### Requirement: Sessão administrativa revogável
O sistema SHALL autenticar a conta por usuário e senha, emitir acesso temporário e refresh rotativo, permitir consultar e encerrar a sessão, e SHALL negar o uso de uma sessão revogada em novas requisições protegidas.

#### Scenario: Login válido
- **WHEN** o administrador informa credenciais válidas e a conta não está bloqueada
- **THEN** o sistema emite access token com validade máxima de 15 minutos e refresh opaco associado a uma sessão absoluta de 8 horas

#### Scenario: Credencial inválida ou conta bloqueada
- **WHEN** o administrador informa senha inválida ou tenta entrar durante bloqueio
- **THEN** o sistema retorna erro genérico sem revelar se a conta existe e não emite tokens

#### Scenario: Refresh válido
- **WHEN** o administrador envia um refresh ainda não consumido e a sessão permanece válida
- **THEN** o sistema consome o refresh e emite novo access token e novo refresh para a mesma família

#### Scenario: Reúso de refresh
- **WHEN** um refresh já consumido é enviado novamente
- **THEN** o sistema revoga a família de refresh e nega a renovação

#### Scenario: Logout
- **WHEN** o administrador encerra uma sessão autenticada
- **THEN** o sistema revoga a sessão no servidor e nega o access token e o refresh correspondentes nas requisições seguintes

#### Scenario: Recarga sem persistência local
- **WHEN** o navegador é recarregado depois que a memória da aplicação foi perdida
- **THEN** o administrador precisa autenticar-se novamente e nenhum token é recuperado de armazenamento persistente do navegador

### Requirement: Limitação de abuso de autenticação e registro
O sistema SHALL limitar tentativas de login e registros públicos, usando parâmetros explícitos e distintos para cada superfície, e SHALL responder sem revelar credenciais ou existência de contas.

#### Scenario: Excesso de tentativas de login
- **WHEN** um endereço IP excede cinco requisições de login por minuto
- **THEN** o sistema responde com `429` sem enfileirar a solicitação e sem incluir senha, token ou indicação de existência da conta

#### Scenario: Bloqueio por falhas
- **WHEN** uma conta acumula cinco falhas de autenticação conforme a janela de segurança
- **THEN** o sistema bloqueia novas autenticações dessa conta por 15 minutos e mantém resposta genérica

#### Scenario: Excesso de registros
- **WHEN** uma origem excede o limite de registro público definido para esta operação
- **THEN** o sistema responde com `429` e não cria empresa nem conta

#### Scenario: Excesso de refresh
- **WHEN** um endereço IP excede trinta requisições de refresh por minuto
- **THEN** o sistema responde com `429`, `Retry-After` e erro genérico, sem fila ou emissão de tokens

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
- **THEN** a senha é limpa do formulário sem exibir erro indevido de preenchimento obrigatório durante a transição, e a pessoa é encaminhada ao painel existente

#### Scenario: Falha de autenticação

- **WHEN** a tentativa de autenticação falha
- **THEN** a tela mostra o erro genérico existente sem expor dados da conta, limpa a senha sem apagar o usuário, reabilita a ação e posiciona o foco visível no resumo

#### Scenario: Aviso após o registro

- **WHEN** a pessoa chega ao login após criar a empresa pelo registro público
- **THEN** o aviso existente de sucesso do registro fica perceptível e o formulário de login continua disponível sem autenticação automática

#### Scenario: Acesso ao registro por teclado

- **WHEN** a pessoa navega por teclado no login e aciona Criar empresa
- **THEN** o link visível e com foco perceptível leva à tela pública `/registro` existente, conforme o cenário Navegação entre acesso e registro da capacidade `registro-empresa`

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

### Requirement: Identidade e apresentação final da navegação
A SPA SHALL manter a identidade LocaCar e a navegação autenticada operável em desktops e notebooks (RF-02, RNF-04, CA-11).

#### Scenario: Exibir a identidade aprovada
- **WHEN** a pessoa abre uma rota pública ou autenticada
- **THEN** o título do documento é LocaCar, o logotipo original é preservado e o favicon apresenta um L creme centralizado, em Georgia, negrito e itálico, sobre fundo oliva quadrado com cantos arredondados, sem símbolo de carro ou efeitos decorativos
- **AND** o monograma é legível a 16×16 e 32×32

#### Scenario: Rolar conteúdo em uma rota autenticada
- **WHEN** a pessoa rola o conteúdo principal com largura e altura suficientes
- **THEN** o menu lateral e Sair permanecem visíveis; Sair permanece no rodapé do menu com altura de 36 px, texto de 14 px, ícone de 16 px e raio de 8 px na apresentação desktop
- **AND** com espaço insuficiente, inclusive a 200% de zoom, o menu volta ao fluxo natural sem perder acesso por teclado ou foco perceptível

#### Scenario: Usar preenchimento automático nativo
- **WHEN** o navegador preenche automaticamente um campo em uma tela pública ou autenticada
- **THEN** o campo exibe fundo branco sem alterar o valor preenchido, desativar o recurso, mudar suas bordas ou suprimir o foco
- **AND** o modo de cores forçadas preserva o tratamento nativo do navegador

### Requirement: Confirmação segura do resultado da saída
A SPA SHALL distinguir revogação confirmada de encerramento local e proteger as gravações durante resultados incertos (RF-02, DT-22).

#### Scenario: Interpretar a resposta de revogação
- **WHEN** o logout retorna `204`, `401` ou fica sem confirmação por falha de transporte ou de servidor
- **THEN** `204` confirma a revogação; `401` isolado não a comprova, e uma leitura `200` da mesma sessão ativa não libera gravações enquanto a tentativa anterior puder produzir efeitos
- **AND** na incerteza há aviso acessível e recuperação explícita conforme o estado da sessão; a reautenticação não anuncia sucesso de logout

#### Scenario: Receber rejeição tardia de leitura ou gravação
- **WHEN** chega um `401` de operação iniciada antes da reserva de saída ou sob uma sessão substituída
- **THEN** a resposta não provoca renovação automática, repetição de comando ou redirecionamento indevido enquanto a saída estiver reservada ou incerta ou em outra sessão
