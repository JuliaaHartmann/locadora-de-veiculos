# Delta de Frota — F5

## ADDED Requirements

### Requirement: Gestão de veículos com foto opcional
O sistema SHALL permitir cadastrar, editar, consultar e excluir logicamente veículos da empresa sem exigir foto. A foto pode ser selecionada no cadastro inicial, mas seu envio é separado e só ocorre após a confirmação do veículo (RF-06/07; J-02; CA-03; F5-03/04).

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
- **AND** a identidade nova não herda a foto do registro anterior

#### Scenario: Veículo de outra empresa
- **WHEN** o administrador tenta consultar ou vincular veículo pertencente a outra empresa
- **THEN** o sistema não revela o veículo nem permite criar vínculo com ele

#### Scenario: Edição ou exclusão bloqueada por aluguel aberto
- **WHEN** houver aluguel aberto associado ao veículo e o administrador tentar editá-lo ou excluí-lo
- **THEN** a operação é rejeitada e a tela informa o impedimento

#### Scenario: Salvar cadastro sem foto selecionada
- **WHEN** o administrador salva um veículo novo sem selecionar foto
- **THEN** o cadastro segue normalmente, sem envio de imagem, e o veículo aparece na lista

#### Scenario: Não enviar foto quando o cadastro falha
- **WHEN** o administrador seleciona uma foto e tenta salvar um novo veículo, mas o cadastro não é confirmado
- **THEN** a tela informa a falha do cadastro, preserva a entrada para correção e não tenta enviar a foto

#### Scenario: Enviar foto após cadastro confirmado
- **WHEN** o administrador salva um veículo novo com foto selecionada e o cadastro é confirmado
- **THEN** a aplicação envia a foto em operação separada, usando o identificador e a versão do veículo retornados pelo cadastro
- **AND** se o envio for confirmado, a tela informa que o veículo e a foto foram salvos

#### Scenario: Repetir só a foto após sucesso parcial
- **WHEN** o veículo é cadastrado, mas o envio da foto não é confirmado
- **THEN** a tela informa que o veículo foi cadastrado e que a falha afetou apenas o envio da foto
- **AND** permite tentar novamente apenas a foto para o mesmo veículo, sem repetir o cadastro, com versão válida e as mesmas regras de autorização, validação e bloqueio por aluguel aberto da edição

#### Scenario: Gerenciar foto de veículo existente na edição
- **WHEN** o administrador edita um veículo já cadastrado na aba Veículos
- **THEN** a seção de foto permite consultar o estado e, quando permitido, adicionar, substituir ou remover a foto; falha nessa ação não descarta os dados em edição

#### Scenario: Visualizar a foto na listagem de veículos
- **WHEN** o administrador consulta a lista simples ou agrupada de Veículos
- **THEN** a coluna “Foto” exibe a miniatura ativa com o texto alternativo “Foto do veículo {placa}” ou o ícone de veículo acompanhado de “Carregando…” durante a consulta
- **AND** se não houver foto, o ícone é acompanhado de “Sem foto”; em falha de leitura ou exibição, de “Foto indisponível”
- **AND** filtro, agrupamento, paginação e ações continuam utilizáveis, inclusive com rolagem restrita à tabela em telas estreitas

#### Scenario: Atualizar a miniatura após alterar a foto
- **WHEN** o administrador volta à listagem após adicionar, substituir ou remover a foto de um veículo
- **THEN** a coluna apresenta a nova foto ativa ou “Sem foto”, sem reaproveitar a imagem anterior

#### Scenario: Filtrar frota por grupo
- **WHEN** o administrador seleciona um grupo na consulta da frota
- **THEN** a listagem apresenta somente os veículos daquele grupo e permite reconhecer a qual grupo cada veículo pertence

#### Scenario: Agrupar frota por grupo
- **WHEN** o administrador ativa o agrupamento por grupo na frota
- **THEN** a listagem organiza os veículos pelos respectivos grupos, mantendo os filtros aplicados

#### Scenario: Editar dados do veículo
- **WHEN** o administrador abre a edição de veículo sem aluguel aberto
- **THEN** todos os campos cadastrados são preenchidos com os valores atuais e alterações válidas aparecem na listagem após salvar

## REMOVED Requirements

### Requirement: Gestão de veículos sem fotos na F2

**Reason**: A proibição de oferecer operações de foto descrevia o incremento F2 e é substituída pela integração aprovada na F5.

**Migration**: Os demais cenários passam para “Gestão de veículos com foto opcional”; “Foto indisponível nesta fase” é substituído pelos cenários de seleção, confirmação e recuperação do envio no cadastro inicial, gerenciamento na edição e miniatura na lista. Veículos existentes continuam válidos sem foto, sem preenchimento retroativo.
