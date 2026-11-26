# grupos-planos-veiculos Specification

## Purpose

Permite ao administrador preparar a frota da empresa, organizando veículos por grupos e configurando as modalidades de plano que poderão ser usadas em operações futuras.

## Requirements

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

### Requirement: Apresentação acessível da Frota em desktop
A SPA SHALL apresentar Grupos, Planos e Veículos com a identidade visual aprovada, preservando campos, ações, validações, mensagens, paginação e regras existentes, com adaptação a janelas e zoom em desktops e notebooks (RF-04–07, RF-19, J-02, RNF-04, CA-02/03/11).

#### Scenario: Navegar pelas consultas e editores
- **WHEN** a pessoa autenticada abre Frota e acessa Grupos, Planos ou Veículos
- **THEN** a tela identifica Frota na navegação compartilhada e permite consultar, cadastrar, editar e excluir os registros conforme as regras existentes
- **AND** apresenta as colunas, campos, valores, opções e ações existentes, sem acrescentar filtros
- **AND** antes de excluir grupo, plano ou veículo, apresenta diálogo com título específico da entidade, sem ícone de lixeira, com a pergunta que identifica o registro separada do aviso de preservação do histórico; oferece Cancelar e Excluir, com altura e tratamento de borda uniformes e hover restrito à borda; Excluir usa fundo `#945B43` e texto branco
- **AND** o foco inicia em Cancelar; cancelar ou usar Esc não altera o registro e devolve o foco à origem; somente após confirmar é tentada a exclusão lógica, preservando os bloqueios e anunciando o sucesso identificado quando ocorrer

#### Scenario: Consultar veículos filtrados ou agrupados
- **WHEN** a pessoa filtra veículos por grupo ou ativa o agrupamento
- **THEN** a lista mantém o filtro, a identificação dos grupos, as miniaturas, as ações e a paginação existentes
- **AND** o agrupamento organiza apenas a página carregada

#### Scenario: Usar planos por modalidade
- **WHEN** a pessoa cadastra ou edita um plano
- **THEN** a tela apresenta os parâmetros correspondentes às modalidades Diário, Controlado ou Livre
- **AND** preserva grupo e modalidade imutáveis na edição e a mensagem existente sobre preços contratados

#### Scenario: Receber estados e erros
- **WHEN** uma consulta carrega, retorna vazia ou falha, ou uma gravação é confirmada ou rejeitada
- **THEN** a tela mantém os estados, mensagens, nova tentativa, resumo de erros e associações aos campos existentes, sem depender apenas de cor
- **AND** preserva os dados em edição em caso de erro e permite operar os controles por teclado

#### Scenario: Reduzir janela ou ampliar a página
- **WHEN** a pessoa usa janela reduzida ou zoom nativo de 200% em desktop ou notebook
- **THEN** navegação, formulários, mensagens e ações permanecem legíveis e alcançáveis por teclado, com foco perceptível e sem rolagem horizontal da página
- **AND** quando necessária, a rolagem horizontal fica restrita à região acessível da tabela

### Requirement: Padrão final das tabelas operacionais
As tabelas operacionais SHALL preservar a legibilidade dos dados e a coerência de suas ações em desktop, inclusive com ampliação (RNF-04, CA-11).

#### Scenario: Apresentar uma coluna com apenas Editar e Excluir
- **WHEN** a pessoa consulta Grupos, Planos, Veículos, Condutores ou Taxas
- **THEN** os ícones permanecem no mesmo eixo do cabeçalho Ações, com dimensões e intervalo uniformes, nomes acessíveis e foco perceptível
- **AND** o recuo direito é compartilhado com Cadastrar e Próxima; se a tabela tiver rolagem horizontal, as bordas direitas coincidem com a tabela rolada até o fim
- **AND** o padrão não substitui ações adicionais de Clientes nem Consultar em Aluguéis

#### Scenario: Consultar dados em janela reduzida ou a 200% de zoom
- **WHEN** a pessoa consulta uma tabela operacional em janela reduzida ou a 200% de zoom
- **THEN** cabeçalhos, placas, documentos, datas, modalidades e rótulos de botões permanecem inteiros, sem truncar nomes longos, que podem quebrar linha
- **AND** quando necessária, a rolagem horizontal permanece na região focalizável da tabela, sem rolagem horizontal da página

#### Scenario: Distinguir quantidade na paginação
- **WHEN** a tabela muda entre um e vários registros
- **THEN** a paginação distingue singular e plural e mantém controles compactos e operáveis por teclado
