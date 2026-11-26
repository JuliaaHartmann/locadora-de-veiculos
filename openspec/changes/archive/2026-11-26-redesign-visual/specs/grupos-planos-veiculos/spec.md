# Delta — grupos-planos-veiculos

## ADDED Requirements

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
