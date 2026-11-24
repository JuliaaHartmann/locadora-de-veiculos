# Evidências — redesign visual

Este documento distingue registros históricos, verificações desta reorganização e trabalho pendente. Não é homologação nem aprovação dos módulos restantes.

## Login — concluído anteriormente

O protótipo e a implementação foram aprovados pela usuária antes desta consolidação; a implementação está no commit `3d5ef7498c2abf9c06271b53877ccc1b3b8b9ae4`. As [evidências originais da implementação](historico/login/evidencias-implementacao.md) e [do protótipo](historico/login/evidencias-prototipo.md), o [design](historico/login/design.md) e as [tarefas](historico/login/tasks.md) foram preservados integralmente.

O relato de uso do Narrador do Windows, seus cenários e limites constam nas evidências originais. A suíte completa de backend não foi concluída naquela rodada; os testes específicos de autenticação e a jornada F1 têm resultados próprios registrados lá. Não se reexecutaram testes do login nesta reorganização.

O [protótipo histórico](historico/login/prototypes/login.html) preserva o SHA-256 `588ff80b3102138e0d63f57a42fc994444e9352ce9f37f2fc4c963b9200d6102`. A [cópia de referência](prototypes/login.html) tem SHA-256 `00da5a2d619fb02bce415c4afa827ca0827886a3000b7e599de45adfe2da743b`: somente duas linhas de comentário do cabeçalho foram atualizadas para apontar à origem histórica e ao estado de aprovação; não se alterou sua apresentação ou comportamento.

## Cadastro de empresa — aprovação visual pendente

A [proposta original](historico/cadastro-empresa/proposal.md), a [delta original](historico/cadastro-empresa/specs/registro-empresa/spec.md), o [protótipo original](historico/cadastro-empresa/prototypes/cadastro-empresa.html) e os metadados foram preservados. As cópias ativas são a [delta](specs/registro-empresa/spec.md) e o [protótipo](prototypes/cadastro-empresa.html). O HTML ativo mantém SHA-256 `0df7a8edbbd00713ec0b89d47acab7187adfd99701383ad11d16eae6301476b1`.

Na etapa anterior, o protótipo foi inspecionado por servidor local em 1440 e 683 px e foram exercitados estados simulados de validação, espera e resposta. A ferramenta bloqueou abertura `file://`; zoom nativo, leitor de tela e contraste não foram verificados naquela etapa. Esses resultados não são evidência da aplicação. A usuária ainda não aprovou visualmente o cadastro.

## Consolidação — 24/11/2026

- Cópias históricas confrontadas com as duas origens: oito arquivos do login e quatro do cadastro, todos com SHA-256 idêntico antes da retirada das pastas originais. Delta e protótipo ativos do cadastro também coincidiram.
- A cópia de referência do login partiu do HTML aprovado; a alteração restringiu-se a duas linhas do comentário inicial. A comparação em bytes confirmou que todo o conteúdo após esse comentário permanece idêntico.
- `openspec validate redesign-visual --type change --strict` passou. `openspec list --json` retornou somente `redesign-visual` ativa. O archive contém as cinco pastas F1–F5 e não contém mais a pasta separada do login.
- A verificação de links relativos dos documentos centrais e históricos não encontrou arquivos ausentes. O diff das áreas protegidas (`app/`, `docs/`, specs principais, schema, configuração e pastas F1–F5) está vazio; `git diff --check` passou para os arquivos rastreados.
- A CLI pode reconhecer `specs` e `prototype` pelo conteúdo presente; isso não comprova a cobertura das sete telas. As especificações e protótipos faltantes permanecem pendentes antes do design e das tarefas globais.

## Pendência para o arquivamento final

- Manter o link relativo `../../specs/account-access/spec.md` em [paridade.md](paridade.md) enquanto a change estiver ativa. No arquivamento final, ajustar o link para `../../../specs/account-access/spec.md` e verificar se ele resolve para a spec principal a partir da pasta datada em `openspec/changes/archive/`. Não alterar a spec nem reaplicar a delta histórica do login.
