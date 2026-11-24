# alugueis Specification

## Purpose

Permite ao administrador estimar, contratar, consultar e revisar aluguéis em aberto na sua empresa, com disponibilidade consistente e preservação dos preços e dados acordados.

## Requirements

### Requirement: Contratação com cadastros elegíveis
O sistema SHALL oferecer ao administrador a contratação com os dados de RF-10 e RF-14, validando referências ativas, documentação, disponibilidade e configuração aplicável na própria empresa.

#### Scenario: Selecionar contratante e condutor
- **WHEN** o administrador escolhe o cliente PF ou PJ no formulário
- **THEN** a seleção de condutor apresenta os condutores vinculados diretamente ao contratante, inclusive o próprio PF quando cadastrado como condutor; vínculo PF–PJ isolado não substitui esse vínculo
- **AND** se não houver condutor vinculado, a tela informa o impedimento sem criar um cadastro implícito

#### Scenario: Selecionar veículo, plano e adicionais
- **WHEN** o administrador preenche a contratação
- **THEN** o formulário oferece veículos disponíveis, planos ativos do grupo do veículo escolhido, retorno previsto, hodômetro inicial inteiro não negativo e as taxas e os dois seguros opcionais
- **AND** não oferece saída editável e informa que o tanque sai cheio; obriga nova escolha de plano se a seleção anterior não pertencer ao grupo do veículo escolhido

#### Scenario: Rejeitar entrada inválida ou configuração indispensável ausente
- **WHEN** a entrada contém retorno não posterior à saída, hodômetro inválido, documento inválido, CNH ausente ou vencida antes do dia local do retorno, vínculo/plano inválido, item repetido ou falta de configuração necessária para o que foi selecionado
- **THEN** o servidor não estima nem grava a entrada inválida e o formulário preserva os dados, indica o campo ou a configuração a corrigir e não apresenta um total garantido
- **AND** ausência de seguro não é preço zero; os dois seguros continuam opcionais e zero explícito é configuração válida

#### Scenario: CNH válida no dia do retorno
- **WHEN** a validade da CNH preenchida coincide com a data local do retorno previsto e os demais dados são válidos
- **THEN** a CNH é elegível, sem consulta externa, documento de veículo adicional ou exigência de cadastro de funcionário

### Requirement: Estimativa discriminada sem ocupação
O sistema SHALL calcular no servidor as parcelas conhecidas para a duração prevista, discriminar o que ainda não pode ser apurado e exibir caução separada, sem ocupar o veículo (RN-01–06, RN-09; CA-07).

#### Scenario: Solicitar estimativa de contratação
- **WHEN** o administrador pede a estimativa com dados válidos
- **THEN** a tela exibe diárias iniciadas de 24 horas transcorridas, plano e parâmetros aplicáveis, cada taxa fixa uma vez, cada taxa diária e cada seguro por diária, e a soma das parcelas conhecidas arredondadas
- **AND** informa que a confirmação recalcula com saída e preços capturados pelo servidor, sem tratar a prévia como reserva ou garantia de preço final

#### Scenario: Planos com parcela pendente por quilômetro
- **WHEN** a prévia ou o demonstrativo de aluguel aberto usa Diário ou Controlado
- **THEN** a interface indica a parcela por quilômetro como pendente; no Controlado, identifica a franquia diária, a total e o preço do quilômetro excedente
- **AND** não apresenta quilometragem desconhecida como zero cobrado nem a soma conhecida como total final

#### Scenario: Plano Livre e caução separada
- **WHEN** a estimativa tem plano Livre
- **THEN** a interface não cria parcela de cobrança por quilômetro e não garante o valor final da devolução
- **AND** em qualquer modalidade mostra R$ 1.000,00 como caução separada, sem abater, somar ao custo ou movimentar valores

#### Scenario: Precisão e duração
- **WHEN** o cálculo recebe uma duração de 24 horas exatas ou 24 horas e um minuto
- **THEN** aplica respectivamente uma ou duas diárias, medindo tempo transcorrido e exibindo datas no fuso de São Paulo
- **AND** a regra de domínio tem mínimo de uma diária para duração zero, sem permitir saída e retorno previsto iguais na contratação; arredonda valores de meio para cima a duas casas por componente antes da soma

### Requirement: Confirmação atômica e idempotente
O sistema SHALL confirmar um único aluguel Em aberto por veículo, persistindo saída real, snapshots, estimativa e resultado idempotente em conjunto, sem ocupação parcial (RF-14; RNF-03; CA-06).

#### Scenario: Confirmar criação
- **WHEN** o administrador confirma a contratação válida
- **THEN** a operação atribui a saída no servidor, revalida dados e preços, ocupa o veículo e apresenta o aluguel Em aberto com sua estimativa recalculada e seus dados preservados

#### Scenario: Veículo ocupado enquanto o formulário estava aberto
- **WHEN** duas criações concorrentes tentam ocupar o mesmo veículo ou confirmação usa disponibilidade desatualizada
- **THEN** só uma confirma; a outra informa que o veículo não está mais disponível, preserva a entrada restante e não cria aluguel nem reserva

#### Scenario: Repetir criação ou falhar sem efeito parcial
- **WHEN** a mesma chave de criação é repetida com a mesma intenção pela mesma empresa e ator
- **THEN** a API devolve o resultado originalmente confirmado, sem duplicar aluguel, snapshot ou auditoria; alteração de entrada com a mesma chave gera conflito
- **AND** falha antes do commit não ocupa veículo nem grava dados parciais; em resultado incerto a interface permite repetir o mesmo envio sem duplicar a contratação

### Requirement: Revisão de aluguel em aberto
O sistema SHALL editar apenas aluguel aberto com versão atual, revalidando a contratação, preservando saída e preços mantidos, e capturando preços atuais de itens novos ou substituídos (RF-15; CA-06).

#### Scenario: Editar dados e preservar preços
- **WHEN** o administrador abre e salva edição válida
- **THEN** o formulário carrega os dados atuais e exibe a saída original sem edição; o demonstrativo atualizado usa os parâmetros contratados para plano, taxas e seguros mantidos e os valores atuais para itens substituídos ou incluídos
- **AND** a revisão anterior é preservada; a operação não cria reserva nem aceita estado ou preço final definidos pelo cliente

#### Scenario: Trocar veículo sem efeito parcial
- **WHEN** o administrador troca o veículo em uma revisão válida
- **THEN** a interface apresenta o veículo novo e o plano compatível no contrato revisado; o novo fica ocupado e o anterior volta a estar disponível no mesmo commit
- **AND** se a troca for inválida ou conflitar, a tela informa o motivo e o veículo original continua ocupado pelo aluguel inalterado

#### Scenario: Tratar versão desatualizada sem sobrescrever
- **WHEN** o administrador tenta salvar sem versão corrente ou sobre uma revisão posterior
- **THEN** a API rejeita a atualização; a interface explica que é necessário recarregar para reavaliar a edição, preserva a entrada até essa decisão e não repete a gravação automaticamente

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
