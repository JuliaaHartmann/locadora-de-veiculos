# Delta — fotos-veiculos

## ADDED Requirements

### Requirement: Apresentação acessível das fotos no redesign da Frota
A SPA SHALL apresentar a foto opcional, miniaturas e estados de sua operação na identidade visual aprovada, preservando integralmente as regras de acesso, gravação e recuperação existentes (RF-03/06/07, RNF-01/02/04, CA-01/03/11).

#### Scenario: Consultar foto e miniaturas
- **WHEN** a pessoa consulta veículos em lista simples ou agrupada ou abre a edição de um veículo
- **THEN** a tela apresenta a imagem com texto alternativo ou o estado existente de carregamento, ausência ou indisponibilidade, sem bloquear os dados e ações permitidos

#### Scenario: Dispor a foto no editor de veículo
- **WHEN** a pessoa cadastra ou edita um veículo
- **THEN** a seção de foto aparece abaixo dos dados cadastrais, com representação e controles lado a lado quando houver espaço
- **AND** as operações de foto na edição permanecem independentes do salvamento dos dados do veículo

#### Scenario: Apresentar ausência de foto nos formulários
- **WHEN** a pessoa cadastra ou edita um veículo sem foto
- **THEN** a área de foto apresenta o mesmo ícone de veículo e o texto “Veículo sem foto” nos dois formulários
- **AND** os demais estados de foto e suas mensagens e operações permanecem disponíveis

#### Scenario: Gerenciar foto e sucesso parcial
- **WHEN** a pessoa seleciona, envia, substitui ou remove uma foto ou tenta novamente um envio após cadastro confirmado
- **THEN** a tela mantém os controles, limites, mensagens, estados e bloqueios existentes, com rótulos e foco perceptível
- **AND** falha de foto não desfaz o cadastro confirmado nem repete sua criação

#### Scenario: Informar erro de foto
- **WHEN** ocorre erro de arquivo, conflito de versão, bloqueio por aluguel, indisponibilidade ou resultado incerto
- **THEN** a tela mantém a mensagem e as orientações existentes, associa o erro ao arquivo quando aplicável e preserva os dados em edição
