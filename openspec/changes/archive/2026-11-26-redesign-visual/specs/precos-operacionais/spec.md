# Delta — precos-operacionais

## ADDED Requirements

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
