# precos-operacionais Specification

## Purpose

Permite ao administrador configurar os valores cobrados pela locadora, distinguindo taxas, seguros e combustíveis e preservando os preços já associados a operações históricas.

## Requirements

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

### Requirement: Apresentação acessível de Preços em desktop

A SPA SHALL apresentar Preços com a identidade visual aprovada e preservar as consultas, campos, ações, regras e estados das configurações de taxas e serviços, seguros e combustíveis (RF-11–13, RNF-04, CA-05/10/11/12). Planos por grupo permanecem em Frota, e a unidade do preço de combustível é a exibida pela aplicação, sem redefinição nesta etapa. A política de Sair é a definida em `account-access`.

#### Scenario: Navegar e consultar taxas, seguros e combustíveis
- **WHEN** a pessoa abre Preços ou alterna entre Taxas e serviços, Seguros e Combustíveis
- **THEN** vê Preços como rota ativa e consulta a configuração correspondente, com listagem, paginação e total de taxas, os dois tipos de seguro e os quatro tipos de combustível
- **AND** o histórico de combustível permanece somente leitura, selecionável por tipo, com versões anteriores distinguíveis da vigente

#### Scenario: Cadastrar, editar e excluir logicamente taxa
- **WHEN** a pessoa cadastra ou edita uma taxa com nome, preço não negativo e modalidade fixa ou diária, ou exclui uma taxa sem vínculo impeditivo
- **THEN** o editor abre na área de Preços com os campos atuais preenchidos na edição e a listagem passa a apresentar os valores ou a ausência do registro
- **AND** preserva obrigatoriedade e limites existentes e, antes de excluir taxa, apresenta o diálogo “Excluir taxa”, sem ícone de lixeira, com a pergunta que identifica a taxa separada do aviso de preservação do histórico; oferece Cancelar e Excluir, com altura e tratamento de borda uniformes e hover restrito à borda; Excluir usa fundo `#945B43` e texto branco
- **AND** o foco inicia em Cancelar; cancelar ou usar Esc não altera o registro e devolve o foco à origem; somente após confirmar é tentada a exclusão lógica, preservando os bloqueios e anunciando o sucesso identificado quando ocorrer

#### Scenario: Configurar seguros independentes
- **WHEN** a pessoa consulta ou configura o preço diário do seguro do cliente ou de terceiros
- **THEN** cada tipo é apresentado e gravado separadamente, distinguindo preço não configurado de preço zero explícito

#### Scenario: Adicionar preço de combustível e consultar histórico
- **WHEN** a pessoa informa preço válido para um tipo de combustível
- **THEN** a vigência é atribuída pelo servidor e uma nova versão passa a ser exibida como vigente, sem alterar versões anteriores
- **AND** o cliente não edita início de vigência nem preço passado, e a unidade exibida permanece a da aplicação

#### Scenario: Receber estados, impedimentos e resultados
- **WHEN** uma consulta carrega, retorna vazia ou falha, ou uma gravação ou exclusão é confirmada ou rejeitada
- **THEN** a tela diferencia carregamento, vazio, configuração ausente, erro de consulta, sucesso, falha, conflito de versão e impedimento por aluguel aberto, oferecendo nova tentativa de consulta
- **AND** erros de gravação preservam a entrada, focam o resumo e são associados aos campos quando aplicável

#### Scenario: Operar Preços por teclado com janela reduzida ou ampliação
- **WHEN** a pessoa navega por teclado ou usa janela reduzida ou zoom nativo de 200% em desktop ou notebook
- **THEN** abas, rótulos, mensagens e ações permanecem legíveis e alcançáveis, com foco perceptível, nomes acessíveis por registro e rolagem horizontal restrita à região acessível da tabela quando necessária

#### Scenario: Aplicar a saída compartilhada em Preços
- **WHEN** a pessoa aciona Sair em Preços
- **THEN** aplica-se a política de `account-access`, considerando alterações efetivas em taxa, seguro ou combustível em relação ao que já foi gravado
- **AND** durante uma gravação ou exclusão Sair fica indisponível com indicação acessível; em caso de falha de revogação, a tela e sua edição são preservadas

### Requirement: Composição final de Taxas e configurações de preços
Preços SHALL seguir o padrão operacional de Frota sem perder a composição própria de Seguros e Combustíveis (RF-11–13, RNF-04).

#### Scenario: Consultar taxas ou alternar a aba
- **WHEN** a pessoa consulta Taxas e serviços ou navega pelas abas
- **THEN** Taxas aplica a coluna Ações compacta de Frota, e as abas mantêm foco perceptível por teclado sem contorno involuntário por mouse

#### Scenario: Consultar cartões e histórico
- **WHEN** a pessoa abre Seguros ou Combustíveis
- **THEN** os cartões usam fundo creme; os de Seguros têm dimensões equivalentes, largura contida e alinhamento à esquerda
- **AND** Combustíveis mostra a unidade por litro e mantém o seletor junto ao título do histórico quando houver espaço, sem ampliar horizontalmente a página
