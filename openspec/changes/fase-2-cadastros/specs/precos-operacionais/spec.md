# Spec Delta

## Purpose

Permite ao administrador configurar os valores cobrados pela locadora, distinguindo taxas, seguros e combustíveis e preservando os preços já associados a operações históricas.

## ADDED Requirements

### Requirement: Gestão de taxas e serviços
O sistema SHALL permitir cadastrar, editar, consultar e excluir logicamente taxas/serviços com nome, preço não negativo e modalidade fixa ou diária.

#### Scenario: Criar e consultar taxa
- **WHEN** o administrador informa nome, preço e modalidade válidos na tela de taxas e salva
- **THEN** a taxa aparece na listagem com identificador, nome, preço e modalidade

#### Scenario: Rejeitar preço negativo
- **WHEN** o administrador informa preço negativo para taxa ou serviço
- **THEN** a tela identifica o campo de preço e a operação não é persistida

#### Scenario: Editar taxa permitida
- **WHEN** o administrador abre taxa sem aluguel aberto, altera nome, preço ou modalidade e salva
- **THEN** os campos atuais são previamente preenchidos e a listagem passa a apresentar os novos valores após confirmação

#### Scenario: Excluir taxa permitida
- **WHEN** o administrador exclui taxa sem aluguel aberto
- **THEN** a taxa deixa de constar dos registros ativos e das seleções de novas operações, sem apagar fisicamente suas referências

#### Scenario: Edição ou exclusão com aluguel aberto
- **WHEN** a taxa está vinculada a aluguel aberto e o administrador tenta editá-la ou excluí-la
- **THEN** a operação é rejeitada e a tela informa que o vínculo impede a alteração

#### Scenario: Histórico após mudança
- **WHEN** uma taxa é editada ou excluída logicamente depois de ser usada em operação histórica
- **THEN** o preço e os dados originalmente associados à operação permanecem consultáveis sem serem substituídos pelo cadastro atual

#### Scenario: Lista de taxas vazia
- **WHEN** a empresa não possui taxas/serviços ativos
- **THEN** a tela informa que não há taxas cadastradas

### Requirement: Configuração independente dos seguros
O sistema SHALL permitir configurar separadamente os preços diários dos seguros do cliente e de terceiros por empresa.

#### Scenario: Consultar seguro não configurado
- **WHEN** o administrador consulta tipo de seguro sem preço previamente configurado
- **THEN** a tela indica que o preço está ausente e não o apresenta como zero

#### Scenario: Configurar preço de seguro
- **WHEN** o administrador configura preço não negativo para seguro do cliente ou de terceiros
- **THEN** somente a configuração do tipo selecionado é atualizada e o preço fica disponível para operações posteriores

#### Scenario: Preço zero explícito
- **WHEN** o administrador salva explicitamente preço zero para um dos tipos de seguro
- **THEN** a tela apresenta o valor zero como configuração existente, distinta de preço não configurado

#### Scenario: Configurar ambos os seguros
- **WHEN** o administrador configura os dois tipos de seguro
- **THEN** ambos permanecem independentes e podem ser selecionados simultaneamente em operação que os suporte

### Requirement: Preços de combustível por vigência
O sistema SHALL permitir configurar preços não negativos para gasolina, gás, diesel e álcool, criando versões com vigência atribuída pelo servidor sem reescrever períodos anteriores.

#### Scenario: Configurar preço de combustível
- **WHEN** o administrador salva preço válido de um tipo de combustível
- **THEN** o sistema registra nova versão com início de vigência determinado pelo servidor e a tela exibe a configuração vigente

#### Scenario: Consultar histórico de preço
- **WHEN** o administrador consulta as configurações de um combustível que teve alterações
- **THEN** as versões anteriores permanecem preservadas e distinguíveis da vigente

#### Scenario: Preço negativo
- **WHEN** o administrador informa preço negativo para combustível
- **THEN** a tela identifica o campo e não cria nova vigência

#### Scenario: Vigência sem ambiguidade
- **WHEN** alterações de preço do mesmo combustível têm o mesmo instante atribuído pelo servidor
- **THEN** a ordem serial da alteração e o identificador da versão determinam a versão aplicável sem modificar versões anteriores; o cliente não pode editar início de vigência nem preço passado

#### Scenario: Combustível ainda sem preço
- **WHEN** o administrador consulta tipo de combustível sem preço vigente
- **THEN** a tela informa que a configuração está ausente e não presume valor zero

#### Scenario: Preservar preço aplicado
- **WHEN** o preço de combustível muda após uma operação usar uma versão anterior
- **THEN** a operação histórica continua exibindo o preço e a versão que foram efetivamente aplicados

### Requirement: Isolamento e rastreabilidade de preços
O sistema SHALL restringir configurações de preço à empresa da sessão e registrar alterações críticas sem expor dados pessoais desnecessários.

#### Scenario: Configuração de outra empresa
- **WHEN** o administrador tenta consultar ou alterar preço pertencente a outra empresa
- **THEN** o sistema não revela nem modifica a configuração externa

#### Scenario: Registrar alteração crítica
- **WHEN** o administrador cria ou altera taxa, seguro ou preço de combustível
- **THEN** a alteração fica rastreável com empresa, conta administrativa, momento e tipo de ação, sem registrar segredos ou documentos completos

### Requirement: Estados acessíveis de preços
As telas de preços SHALL apresentar carregamento, vazio ou configuração ausente, erro e resultado da gravação com rótulos, foco visível e erros por campo.

#### Scenario: Consultar configurações e taxas
- **WHEN** o administrador consulta taxas, seguros ou combustíveis
- **THEN** a tela informa carregamento e apresenta os valores ou ausência explícita; listas de taxas e versões de combustível apresentam paginação e total

#### Scenario: Falha ao consultar preços
- **WHEN** a consulta falha
- **THEN** a tela informa o erro e permite tentar novamente, sem afirmar que o preço é zero ou está ausente

#### Scenario: Configuração confirmada ou rejeitada
- **WHEN** o administrador salva preço, taxa ou exclui taxa
- **THEN** a tela informa o resultado; erro preserva a entrada com resumo focado e mensagem associada ao campo; sucesso atualiza os valores e versões exibidos
