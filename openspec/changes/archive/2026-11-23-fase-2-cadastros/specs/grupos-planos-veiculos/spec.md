# Spec Delta

## Purpose

Permite ao administrador preparar a frota da empresa, organizando veículos por grupos e configurando as modalidades de plano que poderão ser usadas em operações futuras.

## ADDED Requirements

### Requirement: Gestão de grupos de veículos
O sistema SHALL permitir ao administrador criar, editar, consultar e excluir logicamente grupos de veículos da própria empresa.

#### Scenario: Criar e consultar grupo
- **WHEN** o administrador informa um nome válido na tela de grupos e salva
- **THEN** o grupo é persistido na empresa da sessão e aparece na listagem com identificador, nome e valores dos planos associados

#### Scenario: Editar grupo
- **WHEN** o administrador altera o nome de um grupo na tela de edição
- **THEN** a listagem passa a exibir o nome atualizado sem alterar os veículos ou planos vinculados

#### Scenario: Exclusão impedida por vínculo
- **WHEN** o administrador solicita excluir um grupo associado a veículo ou plano
- **THEN** o grupo permanece disponível e a tela informa que há vínculos que impedem a exclusão

#### Scenario: Lista de grupos vazia
- **WHEN** a empresa ainda não possui grupos ativos
- **THEN** a tela de grupos informa que não há grupos cadastrados

### Requirement: Configuração de planos por grupo
O sistema SHALL permitir configurar no máximo um plano de cada modalidade por grupo, com os parâmetros de preço aplicáveis à modalidade.

#### Scenario: Configurar modalidades
- **WHEN** o administrador cria ou edita plano de um grupo
- **THEN** a tela identifica a modalidade Diário, Controlado ou Livre e solicita somente seus parâmetros: Diário com preço diário e por quilômetro; Controlado com preço diário, franquia diária e preço por quilômetro excedente; Livre com preço diário

#### Scenario: Consultar planos do grupo
- **WHEN** o administrador consulta a listagem de grupos e planos
- **THEN** cada plano é apresentado com identificador, modalidade, grupo e seus valores configurados

#### Scenario: Editar parâmetros do plano
- **WHEN** o administrador edita um plano existente e salva preços e franquia válidos
- **THEN** a tela mantém grupo e modalidade, exibe os novos parâmetros e informa que alterações de preço se aplicam a novas contratações, sem alterar valores já contratados

#### Scenario: Parâmetros inválidos de plano
- **WHEN** o administrador informa preço negativo, franquia não inteira ou negativa, ou omite parâmetro obrigatório da modalidade
- **THEN** a tela associa o erro ao campo, mantém os dados preenchidos e não altera o plano

#### Scenario: Exclusão permitida de plano
- **WHEN** o administrador exclui plano nunca usado em aluguel
- **THEN** o plano deixa de aparecer entre os ativos e de ser selecionável, sem apagar fisicamente o registro

#### Scenario: Modalidade duplicada no grupo
- **WHEN** o administrador tenta criar uma segunda configuração da mesma modalidade para o grupo
- **THEN** a tela informa o conflito e não cria um segundo plano

#### Scenario: Exclusão de plano utilizado
- **WHEN** o administrador solicita excluir um plano associado a qualquer aluguel
- **THEN** o plano permanece e a tela informa que seu uso histórico impede a exclusão

### Requirement: Gestão de veículos sem fotos na F2
O sistema SHALL permitir cadastrar, editar, consultar e excluir logicamente veículos da empresa, sem exigir nem oferecer operações de foto nesta fase.

#### Scenario: Cadastrar veículo
- **WHEN** o administrador preenche placa, marca, cor, modelo, combustível, capacidade, ano e grupo e salva
- **THEN** o veículo é registrado no grupo escolhido, pode ser salvo sem foto e aparece na frota com identificador, placa, marca, cor, modelo e combustível

#### Scenario: Capacidade inválida
- **WHEN** o administrador informa capacidade nula ou não positiva
- **THEN** a tela associa uma mensagem ao campo de capacidade e não confirma o cadastro

#### Scenario: Placa já ativa na empresa
- **WHEN** o administrador cadastra veículo com placa já usada por outro veículo ativo da mesma empresa
- **THEN** a operação é rejeitada, a placa duplicada é identificada e nenhum cadastro é alterado

#### Scenario: Placa de veículo excluído logicamente
- **WHEN** o administrador cadastra veículo usando placa de veículo anteriormente excluído logicamente
- **THEN** o novo veículo é tratado como registro distinto e não herda o histórico do registro anterior

#### Scenario: Veículo de outra empresa
- **WHEN** o administrador tenta consultar ou vincular veículo pertencente a outra empresa
- **THEN** o sistema não revela o veículo nem permite criar vínculo com ele

#### Scenario: Edição ou exclusão bloqueada por aluguel aberto
- **WHEN** houver aluguel aberto associado ao veículo e o administrador tentar editá-lo ou excluí-lo
- **THEN** a operação é rejeitada e a tela informa o impedimento

#### Scenario: Foto indisponível nesta fase
- **WHEN** o administrador cadastra ou consulta veículo na F2
- **THEN** o fluxo não exige, envia, exibe nem remove foto

#### Scenario: Filtrar frota por grupo
- **WHEN** o administrador seleciona um grupo na consulta da frota
- **THEN** a listagem apresenta somente os veículos daquele grupo e permite reconhecer a qual grupo cada veículo pertence

#### Scenario: Agrupar frota por grupo
- **WHEN** o administrador ativa o agrupamento por grupo na frota
- **THEN** a listagem organiza os veículos pelos respectivos grupos, mantendo os filtros aplicados

#### Scenario: Editar dados do veículo
- **WHEN** o administrador abre a edição de veículo sem aluguel aberto
- **THEN** todos os campos cadastrados são preenchidos com os valores atuais e alterações válidas aparecem na listagem após salvar

### Requirement: Validação e histórico de frota
O sistema SHALL validar os dados de frota no servidor e preservar os dados históricos associados a registros excluídos logicamente.

#### Scenario: Dados inválidos
- **WHEN** o administrador envia dados obrigatórios inválidos de grupo, plano ou veículo
- **THEN** a operação não é persistida e a tela apresenta erros compreensíveis associados aos campos ou à ação correspondente

#### Scenario: Exclusão lógica de veículo permitido
- **WHEN** o administrador exclui logicamente um veículo sem aluguel aberto
- **THEN** o veículo deixa de ser selecionável para novas operações e seus dados históricos não são apagados

### Requirement: Estados acessíveis das consultas e formulários de frota
As telas de frota SHALL oferecer estados de carregamento, vazio e erro, paginação e feedback de gravação, com rótulos, foco visível e mensagens associadas aos campos, sem depender apenas de cor.

#### Scenario: Consultar lista de frota
- **WHEN** o administrador abre ou muda a página da consulta de grupos, planos ou veículos
- **THEN** a tela informa carregamento e, após a resposta, exibe a página e o total de registros ou estado vazio compreensível

#### Scenario: Consulta indisponível
- **WHEN** a consulta de grupos, planos ou veículos falha
- **THEN** a tela apresenta o erro e permite tentar novamente, sem afirmar que não existem registros

#### Scenario: Cadastro confirmado ou rejeitado
- **WHEN** o administrador salva ou exclui um cadastro
- **THEN** a tela informa o resultado; se houver erro, preserva a entrada e direciona o foco ao resumo com erros associados aos campos; sucesso atualiza a listagem
