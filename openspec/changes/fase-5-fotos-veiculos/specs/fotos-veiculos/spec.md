# Delta de Fotos dos Veículos — F5

## Purpose

Permitir à empresa consultar e administrar uma foto opcional por veículo cadastrado, com acesso privado, validação da imagem e preservação da operação quando a foto falhar.

## ADDED Requirements

### Requirement: Leitura privada da foto ativa
O sistema SHALL permitir ler apenas a foto ativa de veículo consultável na empresa da sessão, sem expor o armazenamento ou servir imagens pendentes, substituídas ou removidas (RF-03/06/19; RNF-01/02; CA-01/03; F5-01/03).

#### Scenario: Consultar foto na edição do veículo
- **WHEN** o administrador abre a edição de um veículo cadastrado da própria empresa
- **THEN** a seção de foto informa o carregamento quando aplicável e exibe a imagem com descrição textual ou informa que não há foto
- **AND** a imagem é entregue pela aplicação autenticada, sem redirecionamento nem URL pública ou direta do armazenamento

#### Scenario: Carregar miniaturas privadas sem bloquear a lista
- **WHEN** linhas da lista atual de Veículos se aproximam da área visível
- **THEN** a aplicação usa a mesma leitura autenticada da foto por veículo, com concorrência limitada e sem esperar a imagem para exibir dados e ações da lista
- **AND** cancela a leitura e libera as URLs temporárias de linhas descartadas ao trocar a lista ou sair da tela ou da sessão; resultados antigos não aparecem em outra linha ou sessão
- **AND** não persiste imagens ou URLs no navegador nem expõe URLs do armazenamento

#### Scenario: Negar acesso sem sessão ou entre empresas
- **WHEN** o pedido de leitura, envio ou remoção não tem sessão válida ou usa um veículo de outra empresa, mesmo com identificador conhecido
- **THEN** o sistema nega a operação sem revelar metadados, existência ou conteúdo à outra empresa e sem alterar registros

#### Scenario: Não expor foto inativa
- **WHEN** a imagem está pendente, foi substituída ou removida, ou pertence a veículo excluído logicamente
- **THEN** a API não entrega os bytes dessa referência; o histórico contratual existente continua preservado

### Requirement: Envio e validação da imagem
O sistema SHALL aceitar somente uma imagem JPEG ou PNG estática por envio, dentro dos limites de 5 MiB para o corpo da requisição e 16 milhões de pixels, e tornar disponível apenas a versão reencodada sem metadados de origem (RF-06; J-02; CA-03; DT-12; F5-02).

#### Scenario: Enviar a primeira foto ou substituir uma foto
- **WHEN** o administrador envia um arquivo válido na edição de um veículo elegível ou a aplicação envia a foto selecionada após confirmar o cadastro do veículo
- **THEN** a seção informa que o envio está em andamento e, após confirmação, mostra apenas a nova foto ativa e informa o sucesso; a imagem anterior deixa de ser acessível

#### Scenario: Rejeitar formato ou conteúdo inválido
- **WHEN** o envio excede o limite de corpo ou de pixels, contém arquivo vazio, truncado, animado, com assinatura ou formato incompatível, ou não pode ser decodificado com segurança
- **THEN** a interface associa o erro ao arquivo, mantém o cadastro e a foto ativa anterior, se houver, e permite selecionar outro arquivo

### Requirement: Alteração de foto consistente com a versão do veículo
O sistema SHALL ativar, substituir ou remover a foto somente para veículo da empresa com versão corrente e sem aluguel aberto, sem alterar o cadastro nem perder a foto anterior em falhas parciais (RF-06/07; RNF-05; CA-03; DT-10/15; F5-05).

#### Scenario: Bloquear alterações com aluguel aberto
- **WHEN** o veículo está alugado ao solicitar uma alteração de foto ou é alugado enquanto a nova imagem está sendo armazenada
- **THEN** o sistema não ativa nem remove a foto e mantém a referência anterior
- **AND** a tela informa o impedimento ao abrir a edição já bloqueada ou na seção de foto quando a alteração é tentada na tela já aberta

#### Scenario: Tratar versão desatualizada
- **WHEN** o veículo muda antes da gravação da foto ou durante o envio
- **THEN** o sistema não altera a foto, informa que o cadastro mudou e orienta consultar a versão atual antes de tentar de novo, sem substituir automaticamente os dados em edição

#### Scenario: Remover a foto ativa
- **WHEN** o administrador solicita remover a foto ativa de veículo elegível na versão corrente
- **THEN** a foto deixa de ser acessível e a seção informa a remoção e que não há foto, sem excluir o veículo ou alterar seus demais dados

#### Scenario: Preservar estado em falha parcial e retomar compensação
- **WHEN** a escrita do objeto ou a ativação da nova foto falha antes da confirmação, inclusive em caso de reinício
- **THEN** a foto anterior permanece ativa, nenhuma imagem pendente é entregue, a interface diferencia a falha da confirmação e o trabalho pendente pode ser retomado sem duplicar efeitos ou apagar foto ativa

#### Scenario: Auditar alteração efetiva
- **WHEN** o envio, a substituição ou a remoção é confirmada
- **THEN** a mudança e a auditoria de empresa, conta, momento e ação são registradas juntas, sem imagem, segredo ou nome de arquivo original na auditoria de negócio; falhas sem alteração não geram evento de sucesso

### Requirement: Operação independente da disponibilidade da foto
O sistema SHALL tratar falha no armazenamento de fotos separadamente da consulta cadastral e das operações de locação que não dependem de imagem (F5-06; TDD §5.5).

#### Scenario: Informar falha de leitura sem bloquear o cadastro
- **WHEN** o armazenamento não consegue entregar a foto ativa do veículo consultado
- **THEN** a seção mostra um placeholder acessível com mensagem de indisponibilidade e mantém utilizáveis os dados e ações cadastrais permitidos, sem afirmar que a foto não existe

#### Scenario: Operar locação durante indisponibilidade de imagem
- **WHEN** o armazenamento de fotos está indisponível e o administrador executa uma operação de locação que não depende da imagem
- **THEN** a operação segue as mesmas regras da locação, sem exigir o carregamento, o envio ou a remoção de foto

### Requirement: Interface acessível para o ciclo da foto
A seção de foto SHALL ser operável por teclado, apresentar rótulos e foco perceptível e comunicar estado e erros em pt-BR sem depender somente de cor (RNF-04; J-02; CA-11).

#### Scenario: Operar e receber feedback por teclado
- **WHEN** o administrador seleciona arquivo, solicita envio, substituição ou remoção e recebe um resultado
- **THEN** os controles têm rótulos e foco visível, a tela anuncia o estado da operação e os erros têm foco identificável e associação ao arquivo quando aplicável
