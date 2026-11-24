# Design — redesign visual do login

## Context

A [proposta](proposal.md#what-changes) limita a mudança a `/login`. A [delta de `account-access`](specs/account-access/spec.md) especifica a apresentação em desktops e notebooks e os estados existentes que precisam ser preservados. A rota já carrega `LoginComponent`, standalone, com Signal Forms; o componente reúne a apresentação e a chamada de login. Os estilos base de `app/frontend/src/styles.css` são compartilhados com outras telas.

## Goals / Non-Goals

**Objetivos:** reproduzir a versão visual aprovada, manter formulário e estados acessíveis com janela reduzida ou zoom e limitar a alteração de apresentação ao componente de login.

**Fora do escopo:** autenticação, sessão, API, regras de negócio, rotas, validações funcionais, demais telas, variantes dedicadas a celular ou tablet, novas dependências, configuração de deploy e mudança do nome do sistema. O registro e o painel continuam como destinos existentes, sem redesign nesta change.

## Decisions

| Decisão | Abordagem e motivo |
| --- | --- |
| **D-01 — Estilos isolados** | Usar template e estilos do `LoginComponent` em `app/frontend/src/app/`, com o escopo padrão de estilos do Angular. Ajustar localmente a herança de `main`, `form`, `p`, `input`, `button`, `a` e indicadores de foco. Manter `styles.css`, a navegação principal e o shell intactos. Isso evita que uma alteração de tema global afete registro ou módulos autenticados. Preservar as proporções, tipografia, cores, espaçamentos e o ponto de reorganização do protótipo; abaixo de sua largura de transição, conservar os dois painéis completos, sem ocultar a paisagem. |
| **D-02 — Recursos visuais aprovados** | Usar o PNG transparente com os bytes originais, sob `app/frontend/public/` e entregue pela configuração de assets existente. Pode ser obtido da referência aprovada ou do data URI do HTML, sempre conferindo o SHA-256 `d2268020cb4fcd9afa47c32c5466ed8f1487e2431935a5c1f487eb6f9b05bed6`. Manter as dimensões intrínsecas e a proporção, sem filtros nem recriação. Reutilizar a geometria, os preenchimentos e a escala do SVG aprovado para a paisagem, **sem faixas nem substitutos**; manter também as formas aprovadas dos ícones. A paisagem e os ícones são decorativos, sem foco e sem interferir na digitação; o logotipo mantém o texto alternativo. Não usar fonte, CDN ou biblioteca nova. |
| **D-03 — Ligação aos estados reais** | Manter Signal Forms, `novalidate`, associações entre rótulos, campos e erros, `autocomplete="username"` e `autocomplete="current-password"`, `RouterLink` para `/registro` e a lógica de `entrar()` sem alteração. Preservar `POST /api/v1/session/login` com `{ username, password }`, tratamento genérico de falha, limpeza da senha sem limpar o usuário, navegação para `/` após sucesso e sessão em memória. O aviso consome o estado existente; o indicador visual de espera usa `enviando()`, sem simulação, temporizador ou nova tentativa automática. Manter a região de alerta e o resumo focável no DOM, evitando ocultá-lo de forma que impeça a chamada de foco existente. A substituição da lógica por JavaScript do protótipo quebraria a paridade funcional. |
| **D-04 — Paridade verificável** | Comparar a implementação futura ao HTML aprovado na mesma largura útil e no mesmo estado, descontando apenas os elementos de revisão. Reutilizar Vitest e o fluxo Playwright de acesso existente. Complementar com inspeção no navegador desktop e zoom **nativo** de 200%, pois a emulação anterior apenas reduziu a largura útil. |

## Prototype

**Referência visual aprovada pela usuária:** [`prototypes/login.html`](prototypes/login.html), SHA-256 `588ff80b3102138e0d63f57a42fc994444e9352ce9f37f2fc4c963b9200d6102`, incluindo a estrada sem faixas e os dois contornos em `#BA7C52`. A remoção das faixas substituiu o refinamento de sinalização previsto inicialmente, conforme o [registro do protótipo](evidencias-prototipo.md). A revisão cromática posterior mudou apenas os contornos de `#B96645` para `#BA7C52`, tom predominante do traço do logotipo. A [proposta](proposal.md#what-changes) reflete ambas as decisões: `#BA7C52` é exclusivo da estrada, `#B96645` permanece o terracota oficial e `#606B45`, o verde oliva oficial. O logotipo e a geometria da ilustração permanecem intactos. A usuária também aprovou visualmente o login implementado no sistema, incluindo a harmonização.

O protótipo usa Tailwind CSS **4.3.5** (MIT) compilado no próprio arquivo e CSS local. A aplicação usará o Tailwind existente e estilos do componente no build Angular, não o CSS compilado colado no HTML. O PNG será servido como asset e os SVGs decorativos preservarão o desenho aprovado. A única diferença intencional na tela é a ausência dos elementos exclusivos de avaliação (faixa, atalho, painel, avisos de simulação e espaço reservado à faixa). Os estados simulados do HTML serão representados pelos estados e destinos reais já existentes.

**Complemento de D-03 — Associação acessível (§5.4 do TDD):** na aplicação, cada campo preserva a referência ao erro individual em `aria-describedby` e inclui a referência ao resumo apenas quando está inválido e o resumo contém uma mensagem. O foco e os anúncios usam a região de alerta já existente. Esse ajuste não altera a apresentação, o protótipo ou a lógica de validação. A usuária relatou resultados satisfatórios na [avaliação manual com o Narrador do Windows](evidencias-implementacao.md#validação-final-pela-usuária), limitada aos cenários ali registrados.

### Rastreabilidade

| Cenários da delta | Área no protótipo | Decisões e verificação futura |
| --- | --- | --- |
| **Identidade e formulário em desktop**; **Janela estreita ou ampliação no desktop** | Painel de identidade, paisagem, cartão e campos | D-01, D-02 e D-04; tarefas 1.1, 1.2 e 2.1. RNF-04, CA-11, DT-01 e DT-02. |
| **Preenchimento obrigatório e foco**; **Envio em andamento**; **Falha de autenticação** | Campos, erros, resumo e estado de envio | D-03 e D-04; tarefas 1.2, 1.3, 2.1 e 2.2. RF-02, RNF-04, CA-01, CA-11, DT-14 e DT-19. |
| **Retorno de autenticação bem-sucedido**; **Aviso após o registro**; **Acesso ao registro por teclado** | Envio, aviso e link Criar empresa; destinos simulados apenas no HTML | D-03 e D-04; tarefas 1.3, 2.1 e 2.2. RF-02, J-01, CA-01, CA-11, DT-05 e DT-06; cenário **Navegação entre acesso e registro** de `registro-empresa`. |

## Risks / Trade-offs

- **Vazamento de estilos para outras telas** → estilos restritos ao login e verificação de registro, painel e navegação autenticada.
- **Divergência entre o protótipo e o build Angular** → comparar a tela lado a lado nos estados cobertos e revisar tanto elementos ausentes quanto extras; a referência visual não deve ser redefinida durante a implementação.
- **Perda de foco, texto truncado ou contraste insuficiente com estados dinâmicos** → testar teclado, erro, espera e aviso, inspecionar contraste e fazer a verificação em zoom nativo; a inspeção anterior do HTML não comprova o resultado da aplicação.
- **Troca do logo, retorno das faixas ou adoção do nome provisório fora da imagem** → conferir digest e paridade do painel e manter título do documento, favicon e demais áreas fora do escopo.

## Migration Plan

Não há migração de dados, configuração, contrato ou infraestrutura. Em uma entrega futura autorizada, a mudança é empacotada no build normal do frontend. Um eventual rollback da unidade visual deve restaurar a apresentação anterior de `/login`, sem intervenção em contas, sessões ou dados.
