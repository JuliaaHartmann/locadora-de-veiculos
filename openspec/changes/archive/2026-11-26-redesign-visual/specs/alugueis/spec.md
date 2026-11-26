# Delta — alugueis

## ADDED Requirements

### Requirement: Apresentação acessível de Aluguéis em desktop

A SPA SHALL apresentar a operação de Aluguéis com a identidade aprovada para desktops e notebooks, preservando os fluxos e regras da contratação, revisão, devolução e consulta histórica (RF-10, RF-13–19, J-03–05, RNF-04 e CA-04–12 aplicáveis).

#### Scenario: Navegar pela consulta e pelo contrato
- **WHEN** a pessoa acessa Aluguéis, aplica filtros ou consulta um contrato
- **THEN** a lista oferece nova contratação, situação, veículo, período de saída, inclusão explícita de ocultos, paginação e consulta; exibe Código (RF-20), condutor, veículo, plano, saída, retorno previsto, situação e estimativa ou total final rotulado
- **AND** o detalhe apresenta os dados preservados, documento mascarado, demonstrativo e caução separada; aberto oferece edição e devolução; concluído mostra retorno real, hodômetro final e litros finais, com ocultação disponível somente se ainda visível
- **AND** o retorno preserva o contexto da lista ou do contrato, conforme a origem

#### Scenario: Contratar ou revisar aluguel
- **WHEN** a pessoa abre contratação ou edição
- **THEN** o formulário apresenta cliente, condutor diretamente vinculado, veículo elegível, plano do grupo, saída somente leitura, tanque cheio, retorno previsto, hodômetro inicial e taxas e seguros opcionais, distinguindo seguro sem configuração de zero explícito
- **AND** apresenta impedimentos de elegibilidade, documentação, período e configuração no contexto aplicável; na criação, exige estimativa discriminada antes da confirmação e distingue parcelas pendentes de valores conhecidos
- **AND** na edição exibe o demonstrativo atual, preserva a saída original e distingue o preço mantido do preço atual de item novo ou substituído

#### Scenario: Conferir e confirmar devolução
- **WHEN** a pessoa registra uma devolução
- **THEN** a tela apresenta as referências preservadas, retorno real, hodômetro final, litros restantes e taxas adicionais elegíveis, sem duplicar taxas contratadas nem alterar plano ou seguros
- **AND** diferencia prévia discriminada de demonstrativo definitivo; entradas modificadas ou preços alterados exigem nova prévia antes de confirmar; falta de preço aplicável de combustível impede concluir
- **AND** preserva a caução separada, identifica vigência do combustível e multa quando aplicável, e apresenta o final persistido somente após confirmação ou consulta do fechamento

#### Scenario: Recuperar falhas e conflitos sem repetir efeitos
- **WHEN** há carregamento, vazio, falha de leitura, validação, indisponibilidade, conflito de veículo, revisão ou preço, resultado incerto ou sucesso
- **THEN** a interface diferencia os estados, permite nova consulta quando aplicável e preserva entradas nas falhas; não repete gravação silenciosamente
- **AND** conflitos de revisão orientam recarregar e reavaliar; resultado incerto de contratação ou devolução permite repetir a mesma intenção sem editar seu conteúdo; sucesso identificado é anunciado e pode ser dispensado

#### Scenario: Operar Aluguéis por teclado com janela reduzida ou ampliação
- **WHEN** a pessoa usa teclado, janela reduzida ou zoom de 200%
- **THEN** navegação, formulários, demonstrativos e ações têm foco perceptível e ordem utilizável, com rótulos, estados textuais e rolagem horizontal restrita à região da tabela quando necessária
- **AND** erros recebem foco no resumo e associação aos campos; diálogos devolvem foco à origem ao cancelar, e a navegação encaminha o foco ao destino
- **AND** as transições e os diálogos mantêm o foco funcional sem contorno intrusivo por mouse; a navegação por teclado mantém foco perceptível, inclusive em Cancelar. Resultados são anunciáveis sem disputar o foco do título da tela.

#### Scenario: Digitar e corrigir datas, horários e medições
- **WHEN** a pessoa digita ou cola datas e horas nos filtros e formulários ou corrige um campo inválido
- **THEN** a tela acrescenta os separadores de `dd/mm/aaaa` e, conforme o campo, `HH:mm`, com exemplos e possibilidade de edição e colagem; a apresentação usa horas e minutos, sem perda da precisão já conhecida pelas regras temporais
- **AND** o erro associado ao campo substitui temporariamente sua orientação e informa a correção em linguagem natural; o limite inferior do hodômetro final usa a quilometragem inicial do contrato

#### Scenario: Aplicar a saída compartilhada em Aluguéis
- **WHEN** a pessoa solicita Sair em Aluguéis
- **THEN** aplica-se a política de `account-access`, incluindo alterações não salvas, bloqueio acessível enquanto grava e respeito à proteção existente de devolução antes da revogação
- **AND** com resultado incerto de contratação ou devolução, a confirmação avisa da perda da possibilidade de repetir o envio nesta tela; cancelar preserva comando e chave originais e, na devolução, a versão aplicável
- **AND** resultado incerto da revogação suspende novas gravações e preserva temporariamente a edição em memória, conforme `account-access`, sem anunciar saída concluída nem repetir o envio automaticamente

#### Scenario: Voltar ou trocar de módulo com edição pendente
- **WHEN** a pessoa tenta deixar contratação, edição ou devolução por Voltar ou pela navegação
- **THEN** a tela pede confirmação apenas se houver rascunho alterado, seleção pendente ou resultado incerto; cancelar preserva os dados e a identidade de um envio incerto, se houver, e devolver o foco não dispara gravação nem revogação
- **AND** a mudança de tela não contorna bloqueios de gravação ou de revogação, e não reapresenta a confirmação após o encerramento autorizado de sessão

## MODIFIED Requirements

### Requirement: Ocultação lógica preserva a consulta histórica

O sistema SHALL permitir ao administrador ocultar um aluguel concluído da operação e consultá-lo novamente apenas com inclusão histórica explícita e autorizada, sem apagar o contrato ou alterar o demonstrativo (RF-03, RF-19; J-05; CA-10/12).

#### Scenario: Ocultar aluguel concluído da operação
- **WHEN** o administrador escolhe ocultar um concluído da própria empresa com a versão corrente
- **THEN** a tela apresenta o diálogo “Ocultar aluguel”, identifica o contrato, explica a preservação do histórico e oferece Cancelar e Ocultar aluguel, com foco inicial em Cancelar e suporte a Esc
- **AND** cancelar mantém o registro e devolve o foco; somente após confirmação bem-sucedida o registro deixa a lista padrão e a interface anuncia que o contrato foi preservado; ocultar não edita o demonstrativo, não reabre o contrato nem altera o estado do veículo

#### Scenario: Consultar registros ocultos explicitamente
- **WHEN** o administrador aciona a opção de incluir ocultos na consulta de Aluguéis
- **THEN** os concluídos ocultos da empresa voltam a ser consultáveis na lista e no detalhe, com indicação de ocultação e o mesmo contrato e total original; a consulta padrão não os revela

#### Scenario: Restringir exclusão, edição e reabertura
- **WHEN** o administrador consulta um aluguel em aberto, concluído ou oculto
- **THEN** a tela não permite ocultar aberto, editar ou devolver novamente um concluído, reabrir, apagar fisicamente ou desfazer ocultação
- **AND** a tentativa de ocultar aberto ou usar versão vencida é rejeitada no servidor sem retirar o registro da lista; a interface informa falha e orienta nova consulta quando a versão mudou
