# Revisão de paridade da F2

Revisão do recorte aprovado de `proposal.md`, dos três delta specs, dos protótipos e da implementação local. Não cobre restrições que dependem de aluguel/snapshot/foto. Não é aprovação independente nem homologação.

| Área/cenários cadastrais | Implementação | Verificação executada |
| --- | --- | --- |
| Grupo: criar, consultar, editar, excluir sem vínculos e rejeitar vínculos inclusive excluídos | `FrotaComponent`, `GrupoService`, `GruposController` | `CadastrosDomainTests`, `CadastrosTests`, `frota.component.spec.ts`; criação no navegador |
| Planos: Diário/Controlado/Livre, parâmetros condicionais, identidade estável, duplicidade, edição e exclusão | `Plano`, `PlanoService`, `PlanosController`, aba Planos | Unidade, corrida SQL/ETag, OpenAPI, Vitest de três modalidades e edição; Diário no navegador |
| Frota: campos, capacidade positiva/escala, placa normalizada, filtro, agrupamento paginado, edição e exclusão/reuso | `Veiculo`, serviços/controllers, aba Veículos | Unidade/HTTP/SQL, rollback, referências externas, Vitest de campos/agrupamento; erro/foco, edição, exclusão e reuso com novo ID no navegador |
| Clientes: PF/PJ, endereço, e-mail opcional, documentos/verificadores, unicidade e reuso | `Cliente`, `DocumentoBrasileiro`, serviços/controllers e `PessoasComponent` | Unidade com limites e documentos; HTTP/SQL; Vitest PF/PJ/endereço/edição; PF e PJ no navegador |
| Vínculos PF–PJ: incluir, consultar, remover; tipos, duplicidade, versão da raiz e empresa | `ClienteService` e editor de vínculos | HTTP/SQL com corrida, ETag e empresa externa; Vitest de remoção; associação no navegador |
| Condutores: contratante direto, próprio PF, dados reaproveitados, vencida permitida, edição e exclusão/reuso | `Condutor`, `CondutorService`, controllers e editor | Unidade/HTTP/SQL, referência excluída preservada e nova associação negada; Vitest de reaproveitamento/ausências/edição; próprio PF no navegador |
| CNH coerente nos dois sentidos, versões/auditoria atômicas | Sincronização sob lock da Empresa | MSTest de incompatibilidade, sincronização nos dois sentidos, correlação de eventos e rollback por falha de auditoria |
| Taxas: fixa/diária, zero, negativo, editar/excluir sem apagar registro | `TaxaServico`, serviços/controllers e `PrecosComponent` | Unidade/SQL/HTTP, exclusão lógica preservada; Vitest de negativo/edição/exclusão; cadastro no navegador |
| Dois seguros: independente, ausente/zero, primeira escrita condicional | `SeguroService` e editores independentes | Unidade/SQL/HTTP, duas configurações e corrida; Vitest; zero e ausência no navegador |
| Combustível: ausente, versões, histórico paginado, relógio/ordem do servidor, sem edição de passado | `CombustivelService`, GET/PUT atual e GET histórico | SQL no mesmo instante e corrida; OpenAPI; Vitest de histórico em outra página e ETag inicial; duas versões no navegador |
| Estados de consulta/gravação, erros por campo, foco, paginação e navegação | Signal Forms, estados Signals, resumo pós-render, guard e interceptor existentes | Vitest de falha/vazio/recuperação, resposta antiga de outra aba, guard/sem armazenamento; navegador com teclado e resumo focado |
| Isolamento e sessão | Contexto server-side, filtros, FKs compostas, guarda, revogação da F1 | SQL/HTTP, corpo com empresa forjada ignorado; navegador em duas origens/empresas com 404 externo e 401 após logout |

## Conferência reversa: controles da aplicação

- Navegação principal e abas apenas para os módulos previstos; rotas protegidas `/frota`, `/pessoas`, `/precos`.
- Campos, tabelas e ações CRUD/vínculos correspondem aos protótipos. Filtro de grupo somente na frota e seleção de combustível para histórico; paginação e agrupamento limitado à página.
- Não foram implementados seletor de revisão, relógio sintético, IDs curtos, exemplos pré-carregados, fotos, upload, validação documental externa, aluguel, pagamentos ou novos perfis de acesso.
- Grupo/modalidade de plano em edição aparecem como texto somente leitura, em vez dos selects desabilitados do protótipo. A identidade permanece imutável; não há novo campo editável.
- Cadastro de condutor mantém visível o contratante original excluído, quando necessário para preservar referência. Esse contratante não é oferecido a cadastros novos.
- A navegação global é integração exigida em 7.1, não um módulo de produto adicional.

## Divergências visuais intencionais e correções

- SPA usa Tailwind 4.3.5 e Lucide já instalados; protótipos usam CSS inline autocontido sem biblioteca. Não foram instaladas dependências. Mantidos tons slate/azul, formulários responsivos e tabelas com rolagem local.
- GUIDs reais têm comprimento maior que os IDs curtos de revisão. Tabelas podem exigir rolagem horizontal, sobretudo em ampliação.
- A inspeção de screenshot detectou texto branco sobre fundo branco nas abas inativas. Corrigido para texto slate escuro sobre branco em todas as telas. Também ajustados selects, checkboxes, espaçamento de tabelas e o estilo de erro que atingia indevidamente a paginação.
- Captura inicial e captura corrigida ampliada estão em `.playwright-mcp/` (artefatos locais ignorados pelo Git). A ampliação foi **CSS 200%**, não o zoom nativo do navegador. Não usar essa evidência para declarar o gate manual integralmente cumprido.

## Cenários deliberadamente ainda não executáveis

- F3: impedimentos por aluguel aberto, plano utilizado, corrida entre cadastro/contratação, preço mantido e CNH até o retorno. Nenhuma porta constante ou flag fictícia substitui essas regras.
- F4: snapshots, demonstrativos finais, seleção retroativa de combustível e CA-12.
- F5: fotos privadas e isolamento de arquivo.
- CA-02 a CA-05 são evidenciados somente no recorte cadastral F2; não declarar o aceite integral dos cenários contratuais.
- Na preparação do PR, o solicitante confirmou via `question` a inspeção manual complementar na aplicação, nas telas Frota, Pessoas e Preços, com zoom nativo, legibilidade, acesso às ações, rolagem, teclado e foco, sem problemas relatados. Navegador/versão e percentual de zoom não foram informados. Esse relato complementa a execução anterior e encerra 7.3/F2-06; não é teste executado pelo agente, revisão independente ou certificação de acessibilidade.
