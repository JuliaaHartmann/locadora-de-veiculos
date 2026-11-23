# Design

## Context

Ver proposta em `proposal.md`. A F1 entrega a fundação Angular/API, autenticação administrativa e contexto de empresa. A F2 acrescenta os módulos Frota e preços e Pessoas, seguindo as fronteiras de camadas, persistência SQL Server, contratos HTTP e diretrizes transversais já estabelecidas no TDD. A F2 não inclui locações, portanto alguns bloqueios por aluguel aberto ficam implementados contra os vínculos quando o módulo de locações existir.

## Goals / Non-Goals

**Goals:**

- Implementar os requisitos das três capabilities desta mudança com validação de domínio no servidor e respostas de campo compreensíveis.
- Preservar isolamento por empresa, unicidade de registros ativos, exclusão lógica e dados históricos.
- Entregar listagens e formulários acessíveis, coerentes com a SPA e sem fotos de veículo nesta fase.

**Non-Goals:**

- Criar locações, estimativas de cobrança, reservas, fotos ou integrações externas.
- Resolver a adequação de gás em litros, pendência de domínio já identificada no PRD/TDD.
- Introduzir dependências, publicar serviços ou alterar requisitos e decisões da F1.

## Decisions

1. **Respeitar os quatro limites de camada e os módulos de negócio do TDD.** Casos de uso e portas ficam em Application; invariantes e comportamentos ficam em Domain; EF Core, índices, migrations, Identity e SQL ficam em Infrastructure; controllers REST, DTOs e composição ficam em Api. A alternativa de concentrar validações nos controllers ou criar repositório genérico foi descartada porque enfraquece invariantes e oculta o protocolo transacional definido no TDD.

2. **Persistir cadastros no SQL Server existente e usar migrations versionadas.** Tabelas de grupos, planos, veículos, pessoas, vínculos, taxas, seguros e preços pertencem à empresa autenticada. As tabelas tenant-aware seguem chaves compostas, filtros e salvaguardas do TDD. Não introduzir outro banco, SQLite ou EF InMemory.

3. **Aplicar o protocolo de escrita da empresa em toda mutação.** Criação, edição e exclusão lógica de cadastro/preço começam na transação com lock da linha Empresa, revalidam referências e invariantes, registram auditoria na mesma transação e confirmam em conjunto. Leituras puras não adquirem o lock. Constraint ou corrida vira conflito de negócio, sem SQL cru. Isso mantém consistência com DT-10, em vez de validar vínculo fora da transação.

4. **Reforçar unicidade dos registros ativos no banco.** Placa, CPF de cliente PF, CNPJ de cliente PJ e CPF de condutor são normalizados e protegidos por índices filtrados por `ExcluidoEmUtc IS NULL`, no escopo da empresa e papel correspondente. Exclusão lógica libera a chave de negócio para novo registro sem redirecionar referências ou histórico do registro anterior. O CPF de cliente PF pode coincidir com o do condutor que representa a mesma pessoa, como permitem RF-08 e RF-09.

5. **Representar preços históricos sem sobrescrever valores aplicados.** Taxas mantêm o preço no item de contrato quando a F3/F4 os introduzir; alterações cadastrais não reescrevem contratos. Seguro mantém preço diário configurável por empresa, distinguindo ausência de preço de zero explícito. Preço de combustível é append-only com início de vigência UTC definido pelo servidor; a validação impede vigências ambíguas e preserva versões anteriores. A adequação de gás não recebe conversão nova.

6. **Deixar o estado derivado de disponibilidade e os bloqueios preparados para as fases de locação.** A F2 não cria booleano redundante de disponibilidade nem dados fictícios de aluguel. As operações de edição/exclusão verificam aluguel aberto quando esse relacionamento existir a partir da F3. Veículo pode ser criado sem foto e a API da F2 não expõe endpoints de binário ou referência de foto.

7. **Manter contratos REST versionados e explícitos.** Endpoints seguem `/api/v1`, DTOs, mapeamento manual, ProblemDetails, paginação e ETag/`If-Match` para recursos mutáveis, conforme seção 5 do TDD. Empresa e ator são derivados da sessão; identificadores do cliente nunca selecionam tenant. Documentos não são refletidos integralmente em logs ou erros.

8. **Construir a UI como rotas lazy Angular standalone, Signals e Signal Forms.** A tela orienta entrada e exibe erros, vazio e sucesso descritos nas specs, mas não substitui validação de servidor. A alternativa de usar apenas formulários HTML genéricos no produto foi descartada por contrariar DT-01 e DT-02.

### Detalhamento técnico da revisão de 22/11/2026

Os itens abaixo materializam DT-07–10/13–15 e foram confirmados pelo solicitante por `question` para esta SDD. Não alteram a arquitetura ou a stack do TDD. Continuam necessários testes e validação final dos artefatos antes de código.

**Baseline inspecionada:** `LocadoraDbContext` tem guarda de escrita e filtro de tenant apenas no recurso sintético da F1, não nos novos cadastros. `Auditoria` contém evento, empresa, usuário e instante, ainda sem entidade/correlação/campos. `IContextoEmpresa` não fornece o ator. `Program.cs` autentica e resolve empresa, mas não expõe ETag no CORS e trata genericamente exceções SQL como 503. A implementação deve estender esses pontos dentro das responsabilidades de cada camada, sem enfraquecer a sessão da F1.

#### Contratos de dados

IDs em GUID de servidor, campos camelCase, enums textuais estáveis em português sem acento no contrato (`Diario`, `Controlado`, `Livre`, `Gasolina`, `Gas`, `Diesel`, `Alcool`, `Fixa`, `Diaria`, `Cliente`, `Terceiros`, `PF`, `PJ`). Rótulos traduzidos na UI. Toda resposta individual inclui `id`; empresa, ator, datas de auditoria, exclusão e versão não são campos graváveis pelo cliente.

| Recurso em `/api/v1` | Corpo de escrita e consulta |
| --- | --- |
| `/grupos`, `/{id}` | `nome`; detalhe/lista inclui `planos` com identidade, modalidade e parâmetros |
| `/planos`, `/{id}` | criação: `grupoId`, `modalidade`, `precoDiario`, `precoKm`, `franquiaDiariaKm`; edição altera somente parâmetros, mantendo grupo/modalidade; `precoKm` é preço excedente no Controlado |
| `/veiculos`, `/{id}` | `placa`, `marca`, `modelo`, `cor`, `ano`, `combustivel`, `capacidadeLitros`, `grupoId`; sem foto nem flag persistida de disponibilidade |
| `/clientes`, `/{id}` | `tipo`, `nome`, `cpf`, `rg`, `cnh`, `cnpj`, `telefone`, `email`, `endereco: { estado, cidade, bairro, rua, numero }`; documentos do tipo incompatível ausentes/nulos |
| `/condutores`, `/{id}` | `clienteId`, `nome`, `email`, `cpf`, `cnh`, `validadeCnh` (`yyyy-MM-dd`), `telefone`; cliente PF não substitui condutor |
| `/clientes/{id}/vinculos-pj` | GET: lista de clientes PJ vinculados; POST: `{ clientePjId }`; DELETE `/{clientePjId}`; raiz deve ser PF e destino PJ da mesma empresa |
| `/taxas-servicos`, `/{id}` | `nome`, `preco`, `modalidade` |
| `/configuracoes/seguros/{tipo}` | GET: `{ configurado, precoDiario }`; PUT: `{ precoDiario }`; ausência usa `false` e `null`, não zero |
| `/configuracoes/combustiveis/{tipo}` | GET: configuração atual `{ configurado, preco, inicioVigenciaUtc, id, ordem }`, campos de preço/versão nulos se ausente; PUT `{ preco }` acrescenta versão |
| `/configuracoes/combustiveis/{tipo}/historico` | GET paginado, versões somente leitura com `id`, `preco`, `inicioVigenciaUtc`, `ordem`; sem edição/remoção de passado |

CRUD usa GET de lista/detalhe, POST, PUT e DELETE lógico, exceto onde indicado. O endpoint de histórico apenas torna consultáveis as versões previstas no TDD, não permite vigência retroativa.

Decimais são strings canônicas com ponto, sem separadores de milhar, expoente ou sinal negativo; UI aceita vírgula local e faz conversão textual, sem `number` para dinheiro. `precoDiario`, `precoKm`, `preco` usam `decimal(19,6)` (máximo `9999999999999.999999`); `capacidadeLitros` usa `decimal(12,3)` (máximo `999999999.999`, mínimo positivo representável `0.001`). Não arredondar excesso de escala, truncar ou aceitar preço implícito zero. Franquia e ano são inteiros não negativos representáveis em `Int32`; ano continua obrigatório, sem limite móvel inventado relativo ao ano atual. A modalidade Livre não grava parâmetros por km; Diário não grava franquia.

#### Limites e validação

| Campo | Limite confirmado | Amostra sintética de referência |
| --- | --- | --- |
| Nome, marca, modelo, cidade, bairro e rua | 200 caracteres | `Veículo fictício de demonstração`, `Rua das Araucárias de Exemplo` |
| Cor | 100 | `Azul metálico de demonstração` |
| E-mail | 254 | `cadastro@example.invalid` |
| Telefone | 32 | `(00) 90000-0001` |
| RG e CNH | 32 | `RG-EXEMPLO-1`, `CNH-FICTICIA-1` |
| Número do endereço | 40 | `12 A` |
| Estado | 100 | `Estado de exemplo` |
| Placa normalizada | 16 | `FIC1A23` |
| CPF/CNPJ numérico normalizado | 11 / 14 | bases sintéticas geradas nos protótipos, com verificadores |

Rejeitar excesso explicitamente em servidor e formulário; não truncar. Trim de bordas em textos; documento e placa normalizados para comparação, sem impor uma expressão de placa brasileira como nova regra. CPF/CNPJ rejeitam sequências repetidas e dígitos verificadores inválidos. Não há validação oficial de RG/CNH: exigir preenchimento e tamanho, sem algoritmo de autenticidade inventado. A validação numérica de CNPJ corresponde ao escopo numérico do PRD/TDD atual; uma futura regra alfanumérica não deve ser introduzida silenciosamente.

Campos obrigatórios são exatamente os descritos nas specs: e-mail de cliente opcional; e-mail de condutor exigido conforme RF-09. Não há bloqueio de cadastro baseado apenas em CNH vencida. Os limites têm amostras de referência, não pesquisa de campo ou garantia de adequação a todo dado real; necessidade de ampliá-los deve voltar à SDD antes da implementação afetada.

#### Consultas, concorrência e erros

- Listas: `page` a partir de 1 e `pageSize` padrão 20/máximo 100, envelope `{ items, page, pageSize, total }`; rejeitar parâmetros inválidos. Ordenar estável por nome e ID (placa e ID na frota, vigência/ordem/ID decrescentes no combustível). `grupoId` é filtro de planos/veículos. Agrupamento organiza a página carregada por grupo, sem alterar a seleção ou alegar abranger páginas não carregadas.
- GET detalhe mutável e respostas POST/PUT incluem `ETag` opaco derivado de rowversion. PUT/DELETE exigem `If-Match`: 428 ausente, 412 divergente, sem sobrescrever ou retry automático. Após 412 a UI preserva a entrada e informa que é necessário recarregar o cadastro para reavaliar a edição.
- Vinculação PF–PJ usa o ETag do cliente PF como raiz e incrementa sua versão na inclusão/remoção. Assim duas telas não sobrescrevem vínculos silenciosamente. Relação duplicada é conflito, não segundo vínculo.
- Seguro ainda não configurado precisa de GET/ETag para a primeira escrita condicional. Usar versão da Empresa como precondição inicial; configuração existente usa sua rowversion. Mesma convenção para primeiro preço de combustível; atualizações seguintes usam a versão vigente, e toda nova vigência preserva as anteriores.
- CORS mantém origens exatas e ausência de credenciais da F1; expor `ETag` para leitura da SPA em origem distinta. Não usar curinga, cookies ou headers de empresa selecionável pelo cliente.
- 201 + `Location` em criação; PUT 200 com estado/ETag atualizado (inclusive nova vigência); DELETE 204; 400 validação/escala/formato; 404 inexistente/excluído/outra empresa; 409 vínculo/duplicidade; 412/428 precondição; 503 SQL indisponível. Reusar ProblemDetails com `status`, `title`, `code`, `traceId`, `errors` por campo. Colisão de índice conhecido deve ser mapeada a 409, não a 503 genérico. Erro inesperado não revela SQL ou documentos.

#### Escrita e integridade

- Portas específicas de módulo na Application não recebem DbContext, HTTP ou claims. Uma abstração transversal obtém ator e empresa validados no servidor; nunca usar IDs do body para auditoria/autorização. Infrastructure implementa transação e lock SQL da Empresa e persistência específica, sem repositório genérico escondendo o protocolo.
- Aplicar filtros tenant e exclusão, guarda de escrita e `EmpresaId` como parte das condições de atualização/exclusão a todas as entidades da F2. FKs compostas com `(EmpresaId, Id)`, sem cascade físico. Código que consulta excluídos para verificar vínculo deve reaplicar explicitamente o tenant.
- Grupo com referência a plano ou veículo, mesmo logicamente excluído, continua impedido de exclusão conforme RF-04; o protótipo mantém essas referências em memória. Planos ativos têm no máximo uma modalidade/grupo: índice único filtrado entre ativos permite nova configuração de modalidade excluída nunca usada, com nova identidade; plano usado não pode ser excluído na F3.
- Cliente PF e condutor com mesmo CPF exigem CNH coerente em todos os registros ativos da empresa, mesmo com vínculo a outro contratante. Criação com CNH incompatível é rejeitada; edição válida coordenada atualiza CNH correspondente nos dois sentidos, versões e auditoria no mesmo commit. A F3 acrescentará a rejeição quando houver aluguel aberto. Vínculo PF–PJ não é requisito adicional para cadastrar condutor de PJ.
- Exclusão lógica retira registros das seleções, sem eliminar FKs. Se um cliente excluído ainda é referenciado por condutor ativo, preservar a referência na leitura; nova associação exige cliente ativo. Vínculos PF–PJ deixam de ser oferecidos se uma ponta for excluída, mas não apagam contrato futuro. Reutilização de CPF/placa gera ID distinto.
- Combustível tem sequência serial atribuída no lock da Empresa, além de GUID/instante. Resolver empate por `(inicioVigenciaUtc, ordem, id)`; não editar datas pelo cliente. A seleção retroativa e cópia do preço no demonstrativo são da F4.
- Estender auditoria existente de forma aditiva com tipo/ID da entidade, correlação e nomes de campos alterados, sem valores pessoais, senha ou token. O evento e a mutação devem confirmar/reverter juntos; não auditar como concluída uma transação abortada.

#### Verificação e gates

- Rever documentação oficial das APIs efetivamente utilizadas antes do código, com versões e fontes na evidência. Não instalar bibliotecas ou provisionar serviços nesta revisão.
- Unidade: validações, parâmetros por modalidade, precisão, CNH nos dois sentidos, preços ausente/zero e vigências. Application: orquestração com portas controladas e rollback/auditoria.
- SQL Server real exclusivo de teste: migrations a partir da F1 e banco vazio, índices filtrados, constraints/FKs, tenant em leitura/escrita/anexação, rowversion, serialização concorrente e falha de auditoria. Nunca usar volume de Development ou User Secrets pessoais nos testes.
- HTTP: duas empresas, sessão revogada, 201/Location, paginação, decimais, 400/404/409/412/428, ausência/zero e ETag exposto por CORS. UI: Signal Forms, campos, estados e paridade com protótipos. Playwright e inspeção de teclado: J-02, foco, zoom, rótulos e duas origens.
- Não marcar cenários que exigem aluguel/histórico como executados na F2. A change da F3 deverá rastrear todos os bloqueios por aluguel aberto, plano utilizado e CNH até o retorno; F4 deverá comprovar snapshots, demonstrativos e CA-12; F5 fotos. Não criar tabelas, contratos ou adaptadores constantes para fingir tais provas.

## Prototype

- `prototypes/frota.html`: grupos, planos por modalidade e dados de veículo sem foto.
- `prototypes/pessoas.html`: clientes PF/PJ, vínculo PF–PJ e condutores.
- `prototypes/precos.html`: taxas/serviços, preços independentes de seguros e versões de combustível.
- Os três arquivos usam CSS próprio inline, sem biblioteca, recurso externo ou CDN, para serem autocontidos em `file://`. A composição visual segue os tons slate/azul, contraste, controles e tabelas observados em `app/frontend/src/styles.css`; não foi copiada uma biblioteca para os protótipos.
- A implementação da SPA deve usar Tailwind CSS 4.3.5 já existente no projeto. A divergência intencional é que o protótipo usa CSS autoral simplificado em vez de incorporar a folha/runtime Tailwind; cores, espaçamento e responsividade podem variar levemente, mas telas, campos, mensagens e cenários devem manter paridade com as specs.
- Os arquivos não registram perguntas em aberto nem itens `[unspecified]`. Nenhuma regra de negócio deve ser inferida da aparência ou dos dados sintéticos.
- Os protótipos revisados incluem CRUD permitido, IDs, edição preenchida, exclusão lógica, paginação, campos condicionais e erros associados. O seletor de estado no banner é ferramenta de revisão, não recurso a implementar na SPA. Não implementar os IDs curtos, o relógio sintético, os exemplos pessoais nem os botões de revisão como dados/comportamentos de produção.
- Cálculo decimal do produto continua autoritativo no servidor. Nos protótipos preços são strings e apenas formatados; as validações de escala/limite técnico, tenant, auditoria e ETag devem ser comprovadas nos testes da aplicação, não inferidas dos HTMLs.

## Risks / Trade-offs

- **[Lock por empresa reduz paralelismo de escrita]** → manter transações curtas, sem chamadas externas, e registrar isso como hipótese de capacidade a medir, não como SLO.
- **[Índices filtrados e constraints do SQL Server são específicos do banco]** → testar integração com SQL Server real e preservar migrations versionadas; não substituir por provider em memória.
- **[Validação duplicada entre tela e API pode divergir]** → servidor permanece autoritativo e os testes de contrato/UI cobrem a apresentação dos erros.
- **[Referências de aluguel ainda não existem na F2]** → implementar bloqueios condicionais sem criar locações; validar esses cenários de integração quando a F3 adicionar o módulo.
- **[Unidade de gás ainda não validada]** → manter o tipo e a unidade descritos no PRD/TDD, sem conversão presumida, e comunicar a pendência antes de uso real com gás.
- **[Snapshots e histórico aumentam o modelo de dados]** → preservar as chaves dos registros originais; testar exclusão/reuso de placa e documentos sem transferir contratos.
- **[Protótipo não inclui o Tailwind executável]** → tratar CSS do HTML como referência visual autocontida, não como entrega de componentes; verificar paridade funcional e visual na aplicação.

## Migration Plan

1. Criar migrations aditivas para entidades, índices, constraints e relações da F2; aplicar em Development pelo mecanismo existente e preparar SQL de teste isolado pela fixture.
2. Implantar API e SPA juntas após build, migrations e verificações locais. Não há implantação Azure ou migração destrutiva prevista para esta fase.
3. Se a mudança precisar ser revertida antes de dados reais, reverter a versão da aplicação sem excluir dados cadastrais; qualquer rollback de schema deve ser avaliado contra os dados já persistidos e não deve apagar histórico.
4. Registrar comandos, ambiente, resultados e limitações na evidência da fase somente após execução real. O documento de design não afirma testes executados nem homologação.
