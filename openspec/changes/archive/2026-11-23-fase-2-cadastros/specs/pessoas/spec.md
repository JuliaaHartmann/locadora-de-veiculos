# Spec Delta

## Purpose

Permite ao administrador manter os clientes PF/PJ e os condutores vinculados à sua empresa, preservando a validade documental, os vínculos permitidos e os dados usados por operações futuras.

## ADDED Requirements

### Requirement: Cadastro de clientes pessoa física e jurídica
O sistema SHALL permitir cadastrar, editar, consultar e excluir logicamente clientes PF e PJ com os dados definidos para cada tipo no RF-08.

#### Scenario: Cadastrar cliente PF
- **WHEN** o administrador informa os dados obrigatórios de pessoa física e salva
- **THEN** o cliente é cadastrado e a listagem apresenta identificador, nome, telefone, documentos e tipo

#### Scenario: Campos obrigatórios por tipo
- **WHEN** o administrador preenche o formulário de cliente
- **THEN** PF exige nome, CPF, RG, CNH, telefone e endereço; PJ exige nome, CNPJ, telefone e endereço; o endereço exige estado, cidade, bairro, rua e número; e-mail é opcional para ambos

#### Scenario: Editar cliente com dados atuais
- **WHEN** o administrador abre a edição de um cliente
- **THEN** o formulário apresenta seus dados atuais e permite salvar alterações válidas, mantendo a coerência de CNH com condutores da mesma pessoa

#### Scenario: Cadastrar cliente PJ
- **WHEN** o administrador informa os dados obrigatórios de pessoa jurídica e salva
- **THEN** o cliente é cadastrado e a listagem apresenta identificador, nome, telefone, documentos e tipo

#### Scenario: E-mail opcional e endereço
- **WHEN** o administrador cadastra PF ou PJ sem e-mail, com os demais dados exigidos e endereço completo
- **THEN** o cadastro pode ser salvo sem exigir e-mail e mantém estado, cidade, bairro, rua e número informados

#### Scenario: Tipo de cliente selecionado
- **WHEN** o administrador escolhe PF ou PJ no formulário de cliente
- **THEN** a tela solicita os documentos correspondentes ao tipo e não exige documentos próprios do outro tipo

#### Scenario: Documento inválido ou duplicado
- **WHEN** o administrador informa CPF/CNPJ inválido ou já ativo em cliente do mesmo tipo e empresa
- **THEN** a operação é rejeitada, a tela informa o campo correspondente e nenhum dado é gravado

#### Scenario: Reutilizar documento de cadastro excluído
- **WHEN** o administrador cadastra cliente com documento de cliente anteriormente excluído logicamente
- **THEN** o novo cliente é um registro distinto, sem herdar os aluguéis ou histórico do registro excluído

#### Scenario: Exclusão bloqueada por aluguel aberto
- **WHEN** o cliente possui relação com aluguel aberto e o administrador tenta excluí-lo
- **THEN** a operação é rejeitada e a tela informa o vínculo impeditivo

### Requirement: Vínculo entre clientes PF e PJ
O sistema SHALL permitir manter vínculos entre clientes PF e PJ da mesma empresa sem transformar o vínculo em conta ou perfil de acesso.

#### Scenario: Associar PF a PJ
- **WHEN** o administrador associa um cliente PF a um cliente PJ da própria empresa
- **THEN** o vínculo fica disponível para consulta e para seleção conforme as regras de elegibilidade do cliente contratante
- **AND** o vínculo PF–PJ não substitui nem é pré-requisito adicional do vínculo direto obrigatório entre condutor e cliente contratante

#### Scenario: Remover vínculo PF–PJ
- **WHEN** o administrador remove um vínculo PF–PJ permitido
- **THEN** o vínculo deixa de ser oferecido em novas operações sem apagar os dados dos clientes

#### Scenario: Referência de outra empresa
- **WHEN** o administrador tenta vincular cliente de outra empresa
- **THEN** o sistema rejeita a operação sem revelar nem alterar o cadastro externo

### Requirement: Cadastro de condutores e elegibilidade documental
O sistema SHALL permitir cadastrar, editar, consultar e excluir logicamente condutores PF vinculados a cliente, validando os documentos declarados e as restrições de edição aplicáveis.

#### Scenario: Cadastrar condutor vinculado
- **WHEN** o administrador informa nome, e-mail, CPF, CNH, validade, telefone e cliente relacionado e salva
- **THEN** o condutor aparece na listagem com identificador, nome, cliente, CPF, CNH e validade

#### Scenario: Cliente PF como próprio condutor
- **WHEN** o administrador seleciona o cliente PF como condutor da mesma pessoa
- **THEN** o cadastro permite a representação como cliente e condutor, mantendo a CNH coerente entre os registros

#### Scenario: Preencher condutor a partir de cliente PF
- **WHEN** o administrador escolhe usar o próprio cliente PF como condutor
- **THEN** o formulário reaproveita nome, CPF, CNH e contatos disponíveis, solicita os dados obrigatórios ainda ausentes e mantém vínculo direto com o cliente

#### Scenario: Cliente PJ com condutor PF
- **WHEN** o administrador prepara condutor para cliente PJ
- **THEN** o condutor é uma pessoa física vinculada ao cliente contratante por relação permitida, sem presumir que qualquer condutor da empresa seja elegível
- **AND** não é exigido criar previamente um cliente PF ou vínculo PF–PJ para cadastrar esse condutor

#### Scenario: Editar condutor com dados atuais
- **WHEN** o administrador abre a edição do condutor sem restrição de aluguel aberto
- **THEN** o formulário apresenta seus dados atuais e permite salvar alterações válidas, preservando a coerência de CNH com cliente PF da mesma pessoa

#### Scenario: CPF inválido ou duplicado entre condutores ativos
- **WHEN** o administrador informa CPF inválido ou já usado por condutor ativo da mesma empresa
- **THEN** a operação é rejeitada e a tela identifica o erro sem impedir que o mesmo CPF exista no cadastro correspondente do próprio cliente

#### Scenario: CNH e validade obrigatórias
- **WHEN** o administrador salva condutor sem CNH ou sem validade informada
- **THEN** a tela aponta os campos ausentes e não confirma o cadastro

#### Scenario: Manter cadastro com CNH vencida
- **WHEN** o administrador salva condutor com validade informada anterior ao dia atual e os demais dados válidos
- **THEN** o cadastro é permitido sem atestar elegibilidade para contratar; a F3 exige validade até o retorno previsto

#### Scenario: Reutilizar CPF de condutor excluído
- **WHEN** o administrador cadastra condutor usando CPF de condutor anteriormente excluído logicamente
- **THEN** o novo condutor é distinto e não herda os contratos do registro excluído

#### Scenario: Restrições durante aluguel aberto
- **WHEN** o condutor possui aluguel aberto e o administrador tenta excluí-lo ou alterar CPF, CNH, validade da CNH ou vínculo
- **THEN** a operação é rejeitada e a tela informa a restrição aplicável

#### Scenario: CNH coerente entre cliente PF e condutor da mesma pessoa
- **WHEN** o administrador altera dados de cliente PF que também está cadastrado como condutor
- **THEN** o sistema mantém a CNH coerente nos registros correspondentes ou rejeita a alteração incompatível, sem contornar a restrição de aluguel aberto

### Requirement: Isolamento, privacidade e preservação de pessoas
O sistema SHALL restringir clientes, vínculos e condutores à empresa da sessão e preservar referências históricas após exclusão lógica.

#### Scenario: Consulta de cadastro de outra empresa
- **WHEN** o administrador solicita cliente, vínculo ou condutor pertencente a outra empresa
- **THEN** o sistema não revela seus dados nem permite relacioná-los aos registros locais

#### Scenario: Exclusão lógica de cadastro permitido
- **WHEN** o administrador exclui logicamente cliente ou condutor sem impedimento de aluguel aberto
- **THEN** o registro deixa de ser selecionável em novas operações e os dados históricos que o referenciam permanecem preservados

#### Scenario: Erro sem exposição de documento
- **WHEN** a validação de cadastro falha
- **THEN** mensagens de interface e erros não expõem CPF, CNPJ, RG ou CNH completos em registros técnicos

### Requirement: Estados acessíveis de pessoas
As consultas e formulários de pessoas SHALL apresentar paginação, carregamento, vazio, falha e resultado da gravação, com rótulos e mensagens por campo operáveis por teclado.

#### Scenario: Consultar clientes ou condutores
- **WHEN** o administrador abre ou pagina uma listagem de pessoas
- **THEN** a tela informa carregamento e apresenta identificadores, colunas previstas, página e total, ou mensagem de ausência de registros

#### Scenario: Falha de consulta
- **WHEN** a consulta de pessoas falha
- **THEN** a tela mostra mensagem compreensível e ação de tentar novamente, sem apresentar a falha como lista vazia

#### Scenario: Validação ou conflito de gravação
- **WHEN** uma gravação de cliente, vínculo ou condutor é rejeitada
- **THEN** a tela mantém os dados preenchidos, informa o impedimento e foca o resumo com erros associados aos respectivos campos quando aplicável

#### Scenario: Exclusão ou gravação confirmada
- **WHEN** a operação permitida de gravação ou exclusão lógica é confirmada
- **THEN** a tela informa o resultado e atualiza a listagem e as opções de vínculos ativos, sem apagar referências históricas
