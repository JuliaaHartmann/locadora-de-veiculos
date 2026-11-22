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
