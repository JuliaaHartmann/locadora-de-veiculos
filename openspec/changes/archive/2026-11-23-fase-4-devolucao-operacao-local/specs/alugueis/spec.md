# Delta de Aluguéis — F4

## ADDED Requirements

### Requirement: Entrada e prévia de devolução sem efeito na locação
O sistema SHALL apresentar ao administrador a prévia discriminada da devolução da revisão corrente de um aluguel em aberto, sem gravar o fechamento ou liberar o veículo (RF-10, RF-17–18; J-04; CA-09).

#### Scenario: Informar o retorno de um aluguel aberto
- **WHEN** o administrador consulta um aluguel em aberto e escolhe registrar a devolução
- **THEN** o formulário mostra saída, previsão, hodômetro inicial e capacidade contratada como referências somente de leitura; recebe data/hora real no horário de São Paulo, hodômetro final em km inteiros, litros restantes e taxas adicionais opcionais
- **AND** lista taxas ativas elegíveis da própria empresa, sem permitir reaplicar taxa já contratada ou repetir taxa adicional, trocar plano ou alterar os seguros acordados
- **AND** vencimento da CNH após a contratação não impede a devolução real

#### Scenario: Corrigir dados inválidos sem perder a entrada
- **WHEN** o retorno real precede a saída, não representa um horário válido ou é ambíguo sem desambiguação, o hodômetro final não é inteiro ou é inferior ao inicial, os litros estão fora de zero à capacidade contratada, ou há taxa inválida ou repetida
- **THEN** a operação rejeita a entrada, preserva os dados e identifica o campo ou item a corrigir; não apresenta total calculado para a entrada inválida, não conclui e mantém o veículo ocupado

#### Scenario: Revisar uma prévia válida
- **WHEN** o administrador calcula a prévia para dados válidos
- **THEN** a tela distingue o valor proposto do total definitivo e apresenta diárias, km, plano, taxas, seguros, combustível, multa quando aplicável e caução separada; permite confirmar os dados e versões daquela prévia
- **AND** se o administrador alterar qualquer entrada usada no cálculo, a prévia não pode mais ser confirmada e o formulário solicita novo cálculo; o aluguel continua em aberto

### Requirement: Cálculo final verificável pelos valores aplicados
O sistema SHALL calcular prévia e final no servidor com a revisão contratada, retorno real e preços aplicáveis, discriminando os componentes de RN-01–09 sem misturar a caução ao custo (RF-05, RF-11–13, RF-18; CA-07/08/12).

#### Scenario: Calcular duração e quilometragem nas três modalidades
- **WHEN** a prévia ou o fechamento calcula uma devolução válida
- **THEN** a duração é o teto do tempo transcorrido em períodos de 24 horas, com mínimo de uma diária, incluindo retorno igual à saída; 24 h contam uma diária e 24 h mais um minuto, duas
- **AND** registra a diferença inteira entre hodômetros; no Diário, soma diárias e km ao preço contratado; no Controlado, soma diárias e só o excedente da franquia de todas as diárias; no Livre, cobra só diárias e informa os km sem cobrá-los

#### Scenario: Discriminar adicionais e conservar seus preços
- **WHEN** o cálculo usa taxas e seguros contratados e taxas adicionadas no retorno
- **THEN** o demonstrativo discrimina cada item com o preço contratado ou, para taxa adicional, o preço vigente na inclusão aceita; cobra taxa fixa uma vez e taxa diária e cada seguro por todas as diárias efetivas
- **AND** não cobra outro exemplar de taxa já contratada ou adicional repetida

#### Scenario: Aplicar a vigência de combustível do retorno real
- **WHEN** há preço do tipo de combustível contratado com vigência no instante do retorno real
- **THEN** a prévia e o final discriminam tipo, litros a repor e preço aplicado; a reposição é a diferença entre a capacidade contratada e os litros restantes multiplicada pelo preço, sem taxa extra
- **AND** o preço escolhido é a última versão que já vigorava no retorno real, inclusive retroativo; em empate vale a ordem serial e o identificador da versão. Com tanque cheio a reposição é zero, preservado o preço usado

#### Scenario: Bloquear quando falta preço aplicável
- **WHEN** não existe versão aplicável ao instante real do retorno, mesmo se o tanque estiver cheio
- **THEN** a tela informa o combustível e a configuração faltante, sem usar preço posterior, inventar preço zero, habilitar fechamento ou liberar o veículo

#### Scenario: Distinguir multa, total e caução
- **WHEN** o sistema calcula os valores da devolução
- **THEN** arredonda o subtotal do plano para duas casas antes da multa; cobra multa única de 10% só sobre esse subtotal quando o retorno real supera o previsto, sem tolerância, mesmo se a quantidade de diárias não mudar
- **AND** arredonda cada componente de meio para cima antes de somá-los, sem incidência de multa sobre taxas, seguros, combustível ou caução; exibe R$ 1.000,00 separadamente, sem inclusão, desconto ou movimentação
- **AND** com 2 diárias, plano de R$ 200,00, taxa fixa de R$ 30,00, taxa diária de R$ 10,00, seguros diários de R$ 5,00 e R$ 7,00 e 10 L a R$ 6,00, exibe R$ 334,00 sem atraso e R$ 354,00 com atraso mantendo as diárias; R$ 1,005 arredonda para R$ 1,01

### Requirement: Fechamento consistente e idempotente
O sistema SHALL concluir apenas um aluguel aberto com a versão atual e as condições aceitas na prévia, salvando demonstrativo final imutável, liberação do veículo, auditoria e resultado idempotente juntos (RF-07, RF-17; RNF-03/05/07; CA-09; TDD §5).

#### Scenario: Confirmar a prévia aceita
- **WHEN** o administrador confirma uma prévia válida da revisão corrente com entradas e versões ainda correspondentes
- **THEN** o aluguel fica Concluído, o veículo volta a estar disponível na mesma operação e a tela mostra o demonstrativo definitivo com os valores gravados
- **AND** o final conserva revisão, entradas, instantes, regra aplicada, quantidades, preços, versões relevantes e componentes usados, sem depender do cadastro atual

#### Scenario: Exigir nova prévia quando mudam os preços aplicáveis
- **WHEN** preço ou configuração aplicável difere da versão aceita ao confirmar
- **THEN** a operação informa o conflito, preserva a entrada e exige nova prévia e confirmação explícita; não aplica silenciosamente o novo valor nem libera o veículo

#### Scenario: Não sobrescrever revisão ou estado concorrente
- **WHEN** falta a precondição da revisão, o aluguel foi revisado ou já foi concluído por outro comando
- **THEN** a nova confirmação é rejeitada e a tela informa que não foi gravada, orienta recarregar o contrato e não sobrescreve a revisão ou o fechamento vencedor; a entrada não é descartada no erro

#### Scenario: Repetir o mesmo fechamento sem duplicar efeitos
- **WHEN** a mesma empresa e o mesmo ator repetem a confirmação com a mesma chave e comando após resposta incerta
- **THEN** o servidor retorna o final original, mesmo se versão ou estado já mudaram, sem repetir auditoria, fechamento ou liberação; a interface permite repetir o mesmo envio sem editar aquela intenção
- **AND** outra entrada com a mesma chave gera conflito e outra chave não conclui novamente um aluguel já concluído

#### Scenario: Falha de gravação não altera disponibilidade
- **WHEN** o fechamento falha antes da confirmação da transação
- **THEN** a tela não apresenta sucesso, preserva os dados e o aluguel permanece aberto e o veículo ocupado, sem final nem auditoria de sucesso parciais

### Requirement: Ocultação lógica preserva a consulta histórica
O sistema SHALL permitir ao administrador ocultar um aluguel concluído da operação e consultá-lo novamente apenas com inclusão histórica explícita e autorizada, sem apagar o contrato ou alterar o demonstrativo (RF-03, RF-19; J-05; CA-10/12).

#### Scenario: Ocultar aluguel concluído da operação
- **WHEN** o administrador escolhe ocultar um concluído da própria empresa com a versão corrente
- **THEN** o registro deixa a lista padrão e a interface informa que o contrato foi preservado; ocultar não edita o demonstrativo, não reabre o contrato nem altera o estado do veículo

#### Scenario: Consultar registros ocultos explicitamente
- **WHEN** o administrador aciona a opção de incluir ocultos na consulta de Aluguéis
- **THEN** os concluídos ocultos da empresa voltam a ser consultáveis na lista e no detalhe, com indicação de ocultação e o mesmo contrato e total original; a consulta padrão não os revela

#### Scenario: Restringir exclusão, edição e reabertura
- **WHEN** o administrador consulta um aluguel em aberto, concluído ou oculto
- **THEN** a tela não permite ocultar aberto, editar ou devolver novamente um concluído, reabrir, apagar fisicamente ou desfazer ocultação
- **AND** a tentativa de ocultar aberto ou usar versão vencida é rejeitada no servidor sem retirar o registro da lista; a interface informa falha e orienta nova consulta quando a versão mudou

## MODIFIED Requirements

### Requirement: Consulta e demonstrativo de aluguel aberto
O sistema SHALL listar e detalhar os aluguéis da própria empresa usando a revisão contratual pertinente, distinguindo estimativa de aberto de total final persistido (RF-16, RF-19; J-05; CA-07/10/12).

#### Scenario: Listar e consultar contrato
- **WHEN** o administrador acessa Aluguéis e abre um registro
- **THEN** a lista paginada exibe ID, condutor, veículo, plano, saída, retorno previsto, situação e valor identificado como estimativa ou total final; permite filtrar por situação, veículo e período de saída
- **AND** no aberto, o detalhe exibe dados preservados do contratante, do condutor, do veículo, do grupo e do plano, hodômetro inicial, demonstrativo estimado, taxas, seguros e caução, com ações de editar e registrar devolução
- **AND** no concluído, o detalhe apresenta os dados preservados, retorno real, hodômetro final, litros finais e demonstrativo definitivo discriminado com caução separada, sem edição ou nova devolução

#### Scenario: Exibir estados acessíveis da consulta e dos formulários
- **WHEN** a consulta carrega, não encontra resultados ou falha, ou o formulário conclui uma operação
- **THEN** a interface distingue carregamento, vazio, falha com tentativa novamente e resultado de gravação; erros preservam a entrada, recebem foco no resumo e são associados aos campos quando aplicável
- **AND** permite operar por teclado, com rótulos, foco visível e mensagens que não dependem só de cor (RNF-04)

#### Scenario: Cadastro alterado não reescreve o contrato
- **WHEN** um cadastro ou preço de origem é alterado em operação permitida, ou um cadastro é excluído logicamente
- **THEN** o aluguel continua exibindo os dados e valores preservados na revisão ou no final, sem substituir a identidade por outro cadastro que reutilize placa ou documento
- **AND** editar plano, seguros ou preço de combustível, ou excluir cadastros permitidos após a conclusão, não muda o contrato, os preços aplicados nem o total histórico

### Requirement: Isolamento, bloqueios cadastrais e auditoria
O sistema SHALL restringir a operação à empresa autenticada e preservar a integridade dos cadastros vinculados, do contrato e da auditoria (RF-03, RF-05–11, RF-19; RNF-01–03, RNF-05, RNF-07).

#### Scenario: Bloquear cadastros protegidos por vínculo contratual
- **WHEN** na tela cadastral o administrador solicita edição/exclusão de veículo ou taxa com aluguel aberto, exclusão de cliente ou condutor vinculado, alteração protegida do condutor, ou exclusão de plano usado em aluguel
- **THEN** a tela cadastral informa a restrição correspondente e o cadastro e o contrato mantêm seu estado
- **AND** alteração coordenada de CNH via cliente PF não contorna o bloqueio do condutor

#### Scenario: Negar acesso e vínculos de outra empresa
- **WHEN** falta sessão válida ou a solicitação usa aluguel, cadastro ou preço de outra empresa, inclusive na devolução, ocultação e consulta de ocultos
- **THEN** o sistema nega acesso ou vínculo e não expõe a existência nem dados da outra empresa

#### Scenario: Auditar criação e revisão confirmadas
- **WHEN** uma criação ou revisão é confirmada
- **THEN** empresa, ator, instante, entidade e ação ficam rastreáveis junto da mutação, sem documentos completos nem segredos no registro técnico; operação abortada não gera evento de sucesso

#### Scenario: Auditar devolução e ocultação confirmadas
- **WHEN** o administrador conclui a devolução ou oculta um aluguel concluído
- **THEN** a mudança e a auditoria com empresa, ator, instante, entidade e ação são persistidas juntas, sem documentos completos nem segredos; falha e replay não geram evento adicional de sucesso
