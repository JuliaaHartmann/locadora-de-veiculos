# Spec Delta

## Purpose

Permite ao administrador estimar, contratar, consultar e revisar aluguéis em aberto na sua empresa, com disponibilidade consistente e preservação dos preços e dados acordados.

## ADDED Requirements

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
O sistema SHALL listar e detalhar os aluguéis em aberto da própria empresa usando os dados da revisão contratual, distinguindo estimativa de total final (RF-16; CA-07).

#### Scenario: Listar e consultar contrato
- **WHEN** o administrador acessa Aluguéis e abre um registro
- **THEN** a lista paginada exibe ID, condutor, veículo, plano, saída, retorno previsto, situação e estimativa identificada como tal; permite filtrar por veículo e período de saída
- **AND** o detalhe exibe dados preservados do contratante, do condutor, do veículo, do grupo e do plano, hodômetro inicial, demonstrativo, taxas, seguros e caução; não oferece ações de fases posteriores

#### Scenario: Exibir estados acessíveis da consulta e dos formulários
- **WHEN** a consulta carrega, não encontra resultados ou falha, ou o formulário conclui uma operação
- **THEN** a interface distingue carregamento, vazio, falha com tentativa novamente e resultado de gravação; erros preservam a entrada, recebem foco no resumo e são associados aos campos quando aplicável
- **AND** permite operar por teclado, com rótulos, foco visível e mensagens que não dependem só de cor (RNF-04)

#### Scenario: Cadastro alterado não reescreve o contrato
- **WHEN** um cadastro ou preço de origem é alterado em operação permitida
- **THEN** o aluguel continua exibindo os dados e valores preservados na revisão, sem substituir a identidade por outro cadastro que reutilize placa ou documento

### Requirement: Isolamento, bloqueios cadastrais e auditoria
O sistema SHALL restringir a operação à empresa autenticada e preservar a integridade dos cadastros vinculados, do contrato e da auditoria (RF-03, RF-05–11, RF-19; RNF-01–03, RNF-05, RNF-07).

#### Scenario: Bloquear cadastros protegidos por vínculo contratual
- **WHEN** na tela cadastral o administrador solicita edição/exclusão de veículo ou taxa com aluguel aberto, exclusão de cliente ou condutor vinculado, alteração protegida do condutor, ou exclusão de plano usado em aluguel
- **THEN** a tela cadastral informa a restrição correspondente e o cadastro e o contrato mantêm seu estado
- **AND** alteração coordenada de CNH via cliente PF não contorna o bloqueio do condutor

#### Scenario: Negar acesso e vínculos de outra empresa
- **WHEN** falta sessão válida ou a solicitação usa aluguel, cadastro ou preço de outra empresa
- **THEN** o sistema nega acesso ou vínculo e não expõe a existência nem dados da outra empresa

#### Scenario: Auditar criação e revisão confirmadas
- **WHEN** uma criação ou revisão é confirmada
- **THEN** empresa, ator, instante, entidade e ação ficam rastreáveis junto da mutação, sem documentos completos nem segredos no registro técnico; operação abortada não gera evento de sucesso
