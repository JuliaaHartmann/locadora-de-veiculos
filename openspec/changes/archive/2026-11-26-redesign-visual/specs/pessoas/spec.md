# Delta — pessoas

## ADDED Requirements

### Requirement: E-mail opcional no cadastro de condutores

O sistema SHALL aceitar condutores sem e-mail no cadastro e na edição, preservando o vínculo, os demais campos obrigatórios e as regras de elegibilidade de RF-09 e RF-10.

#### Scenario: Salvar ou remover o e-mail de um condutor
- **WHEN** os demais dados são válidos e o administrador cadastra sem e-mail ou limpa o e-mail ao editar
- **THEN** a SPA não exige o campo e a API representa a ausência como `null` na resposta; no comando, omissão, `null` e valor em branco representam a mesma ausência
- **AND** os e-mails existentes permanecem intactos na migração

#### Scenario: Validar e-mail informado
- **WHEN** o administrador informa um e-mail não vazio
- **THEN** formato e limite existentes continuam validados; um valor inválido é rejeitado no campo `email`, sem alteração no registro

### Requirement: Apresentação acessível de Pessoas em desktop

A SPA SHALL apresentar Pessoas com a identidade visual aprovada e preservar as consultas, campos, ações, regras e estados dos cadastros de clientes, condutores e vínculos (RF-08–10, RNF-04, CA-04/11). A política de Sair é a definida em `account-access`.

#### Scenario: Navegar e consultar clientes e condutores
- **WHEN** a pessoa abre Pessoas ou alterna entre Clientes e Condutores
- **THEN** vê Pessoas como rota ativa e consulta a lista correspondente com as colunas, paginação e ações por registro existentes, sem acrescentar filtros
- **AND** antes de excluir cliente ou condutor, apresenta diálogo com título específico da entidade, sem ícone de lixeira, com a pergunta que identifica o registro separada do aviso de preservação do histórico; oferece Cancelar e Excluir, com altura e tratamento de borda uniformes e hover restrito à borda; Excluir usa fundo `#945B43` e texto branco
- **AND** o foco inicia em Cancelar; cancelar ou usar Esc não altera o registro e devolve o foco à origem; somente após confirmar é tentada a exclusão lógica, preservando os bloqueios e anunciando o sucesso identificado quando ocorrer

#### Scenario: Cadastrar ou editar cliente e condutor
- **WHEN** a pessoa cadastra ou edita um cliente ou condutor, inclusive por Usar como condutor em um cliente PF
- **THEN** o editor abre na área de Pessoas, mantém campos, obrigatoriedade, condições PF/PJ, dados atuais ou reaproveitados e opções de cliente, inclusive o vínculo original excluído em edição
- **AND** preserva validações, coerência de CNH e distinção entre cadastro e elegibilidade na contratação

#### Scenario: Gerenciar vínculos PJ e receber resultado parcial
- **WHEN** a pessoa abre Vínculos PJ de um cliente PF e salva uma seleção alterada
- **THEN** pode marcar ou desmarcar PJs ativos sem alterar o vínculo direto do condutor
- **AND** se parte das alterações for gravada antes de uma falha, recebe indicação das alterações aplicadas e das pendentes, sem anunciar sucesso integral, desfazer o que foi gravado ou perder a seleção

#### Scenario: Receber estados, impedimentos e resultados
- **WHEN** uma consulta carrega, retorna vazia ou falha, ou uma gravação ou exclusão é confirmada ou rejeitada
- **THEN** a tela diferencia os estados, oferece nova tentativa de consulta após falha e comunica o resultado, inclusive conflito de versão e impedimento por aluguel aberto
- **AND** erros de gravação preservam os dados em edição, focam o resumo e são associados aos campos quando aplicável, sem expor documentos nas mensagens de erro

#### Scenario: Operar Pessoas por teclado com janela reduzida ou ampliação
- **WHEN** a pessoa navega por teclado ou usa janela reduzida ou zoom nativo de 200% em desktop ou notebook
- **THEN** navegação, rótulos, mensagens e ações permanecem legíveis e alcançáveis, com foco perceptível, ações identificáveis por registro e rolagem horizontal restrita à região acessível da tabela quando necessária

#### Scenario: Aplicar a saída compartilhada em Pessoas
- **WHEN** a pessoa aciona Sair em Pessoas
- **THEN** aplica-se a política de `account-access`, considerando alterações efetivas nos campos ou na seleção de vínculos em relação ao que já foi gravado
- **AND** durante uma gravação, inclusive de vínculos ou exclusão, Sair fica indisponível com indicação acessível; em caso de falha de revogação, a tela e sua edição são preservadas
