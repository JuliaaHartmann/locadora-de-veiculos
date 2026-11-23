# Design

## Context

Ver [proposta](proposal.md) e [delta de Aluguéis](specs/alugueis/spec.md). A `main` contém a F2 integrada e arquivada. `CadastrosService` na SPA já mantém convenções de paginação, decimais, erros e ETag. `ProtocoloEscrita` na Infrastructure já abre a transação SQL, bloqueia a linha da empresa com `UPDLOCK`/`HOLDLOCK` e grava auditoria no mesmo commit. A F2 fornece os cadastros e regras de vínculo, mas a ocupação, o contrato e as guardas que dependem dele não são demonstrados pelos seus testes. Referências: [TDD](../../../../docs/tdd.md) §§4–7 e [contratos F2](../../../../app/backend/docs/contratos-f2.md).

## Goals / Non-Goals

**Goals:** encaixar a F3 na arquitetura, no protocolo de consistência e nos padrões de UI existentes; guardar versões contratuais verificáveis; cobrir CA-06 e o recorte pertinente de CA-07 sem simular entrega de outras fases.

**Non-Goals:** fechar ou cobrar devolução, persistir dados de foto, expor histórico de concluídos, incluir novos papéis, bibliotecas ou infraestrutura, ou publicar a aplicação.

## Decisions

As decisões DT abaixo são as do TDD. O detalhamento da F3 foi aprovado como parte da SDD antes da implementação, conforme o [registro de evidências](evidencias.md), sem alterar o roadmap ou a stack.

| Rastreio | Desenho da F3 e alternativa descartada |
| --- | --- |
| DT-01–03, DT-13; RF-10, RF-14–16 | Integrar `/alugueis` à navegação administrativa, preservando acesso protegido, rotas lazy, componentes standalone e Signal Forms. API com controllers e mapeamento explícito; Application orquestra portas; Domain concentra regras e cálculo; Infrastructure persiste e traduz conflitos SQL. Não duplicar regras de negócio no Angular nem acoplar Domain a EF ou HTTP. |
| DT-04, DT-07–08; RF-03, RF-05–11, RF-19 | Usar a raiz `Aluguel`, revisões de `ContratoSnapshot`, itens contratados e o demonstrativo estimado da revisão; referências incluem `EmpresaId`, sem cascata física. Gravar os dados exibíveis acordados e parâmetros necessários por revisão e ler do snapshot, não dos cadastros vivos; identificador de negócio reutilizado não redireciona referências. Preservar revisões anteriores sem criar tela de histórico. |
| DT-09; RN-01–06, RN-09 | Servidor calcula parcelas em `decimal`, mede duração em UTC e apresenta datas em `America/Sao_Paulo`. Usar ISO 8601 com offset, validar sua relação com o fuso e tratar horários locais inválidos/ambíguos conforme TDD §4.6. Hodômetros em km inteiros; CNH válida até o **dia local** previsto é elegível. Transportar decimais em strings, arredondar meio para cima por componente e somar os componentes arredondados. Não usar `number` para dinheiro nem offset fixo no produto. |
| DT-10, DT-15; RNF-03, RNF-05 | Estender o protocolo de escrita da F2 em vez de criar outro lock. Resolver replays e dependências sob transação, revalidar vínculos e preços e confirmar aluguel, revisão, itens, estimativa, idempotência e auditoria juntos. Índice SQL único filtrado `(EmpresaId, VeiculoId)` para Em aberto; disponibilidade é derivada, não um booleano. Troca de veículo preserva o antigo até o commit. |
| DT-05–07, DT-13, DT-15; RF-02–03, RNF-01–02 | Usar sessão/empresa/ator resolvidos no servidor, filtros, guarda de escrita e FKs compostas, inclusive em consultas com exclusão lógica. CORS por origem exata, sem credenciais; permitir `Idempotency-Key` e `If-Match` e expor `ETag` conforme contrato. Auditoria anota entidade, ação, instante, ator e correlação sem documentos completos ou segredos. |
| DT-14, DT-21; F3-01–F3-05 | A aprovação explícita precede código. Verificar domínio com relógio controlado, HTTP e persistência com SQL Server isolado e UI com Vitest/Playwright; não substituir locks/índices por provider em memória. O protótipo não é prova de implementação. |

### Contratos e persistência

| API `/api/v1` | Contrato no recorte F3 |
| --- | --- |
| `GET /alugueis` | Lista paginada no envelope da F2, com situação, dados do RF-16 e parcelas conhecidas; ordem saída decrescente com ID como desempate. Veículo e período de saída no fuso de negócio. A F3 só possui Em aberto; não expõe operação de ocultação nem precisa de seletor redundante de estado na tela. |
| `GET /alugueis/{id}` | Retorna a revisão corrente com demonstrativo e `ETag` correspondente, sem leituras dissociadas do corpo/versão. Outra empresa ou ausência: 404 sem dados. |
| `POST /alugueis/estimativas` | Recebe dados de contratação. Usa uma saída de referência do servidor e retorna duração, parâmetros do plano, componentes conhecidos, indicação de incompletude e caução separada. Não grava nem ocupa. A criação recalcula. |
| `POST /alugueis` | Mesmo conjunto de entrada e `Idempotency-Key` obrigatória. Captura saída, preços e snapshot, retorna 201, `Location`, corpo e `ETag` da revisão confirmada. Não aceita saída, empresa, preço, situação ou valor final do cliente. |
| `PUT /alugueis/{id}` | Mesmo conjunto editável, sem saída. Exige `If-Match` (428 ausente, 412 divergente); retorna a revisão corrente atualizada com estimativa e `ETag` novos (200). Não repetir automaticamente edição divergente. |

- As entradas representam cliente, condutor, veículo, plano, retorno previsto, hodômetro inicial, IDs das taxas e tipos de seguro. Usar convenções do TDD §5 e F2: IDs como strings, enums textuais, ProblemDetails com códigos e campos, validação server-side, 400 para entrada inválida, 409 para conflito de vínculo, disponibilidade ou chave, e 503 para dependência indisponível. Manter limites já definidos na F2 e validar representação e overflow, sem truncamento silencioso.
- Reutilizar as listas e opções dos cadastros; garantir que a consulta de seleção retorne apenas veículos disponíveis (ou o atual na edição), condutores do contratante direto e planos do grupo, sem corte nos primeiros 100 registros. Nenhuma seleção do navegador substitui a revalidação. Combustível é informação do veículo nesta fase; seu preço usa outra regra na F4 e não bloqueia a estimativa da F3. Preço ausente do seguro selecionado não é zero; seguro não selecionado não é exigido.
- Na revisão, comparar identidades com a revisão corrente: mesmo plano, taxa ou tipo de seguro preserva seus parâmetros; inclusão ou substituição captura os valores atuais. Deduplicar por identidade de origem com restrição no modelo. Qualquer alteração válida cria revisão e incrementa a versão da raiz, inclusive quando muda apenas um item. Revalidar documentação, vínculo e disponibilidade conforme RF-10 e RF-15, sem atualizar a saída.
- A chave de criação tem unicidade por empresa, ator e operação; comparar o hash do comando canônico, incluindo as referências e sem JWT, respeitando o resultado original no replay inclusive após edição. Persistir referência à revisão inicial, sem cache de dados pessoais. A tela mantém a chave do envio incerto em memória para repetir a mesma intenção; sem retry de uma entrada diferente com a mesma chave. Para falha SQL transitória, repetir a unidade transacional completa de modo limitado, nunca só o último `SaveChanges`, conforme TDD §4.5.
- No mesmo lock, as mutações da F2 consultam vínculos: bloquear edição/exclusão de veículo e taxa em aluguel aberto, exclusão de cliente/condutor relacionado, alterações protegidas do condutor (incluindo atualização indireta de CNH pelo PF) e exclusão de plano usado em qualquer aluguel. Não bloquear, por extensão, edição permitida de preço do plano ou outros campos do cadastro.

## Prototype

- [`prototypes/alugueis.html`](prototypes/alugueis.html): J-03, consulta e edição. Destaca a estimativa das parcelas conhecidas e separa a caução; mostra conflito de disponibilidade, versão desatualizada, falha de consulta e repetição após resposta incerta.
- [`prototypes/bloqueios-cadastrais.html`](prototypes/bloqueios-cadastrais.html): recortes de Frota, Pessoas e Preços operacionais para avaliar o feedback das ações que passam a ser bloqueadas por vínculo real. Não substitui o CRUD da F2.
- Ambos usam CSS próprio inline, sem biblioteca embarcada, a partir dos tokens slate/azul, tipografia de sistema, cartões, tabelas, controles e foco de `app/frontend/src/styles.css` e dos componentes existentes. A SPA usará Tailwind CSS 4.3.5 e componentes próprios; os banners e seletores de revisão, o relógio fixo das amostras e a simulação em memória não são recursos do produto. Não há placeholders de decisão em aberto.
- Paridade em duas direções: cada cenário com efeito visual deve ter estado e ação correspondentes; cada ação, campo e mensagem funcional deve ter origem na spec, sem antecipar operações da F4–F6. As simulações de conflito, replay e versão não substituem os testes de SQL/HTTP.

## Risks / Trade-offs

- **Prévia desatualizada por relógio, preços ou disponibilidade** → explicitar que ela não reserva; revalidar e devolver o demonstrativo confirmado, com feedback de conflito quando aplicável.
- **Corrida entre cadastro e contrato** → mesmas transação e ordenação do lock da empresa, com índice único de defesa e testes em conexões independentes; menor paralelismo por empresa é trade-off do TDD.
- **Replay depois de nova revisão** → verificar que se lê o resultado original e não o contrato corrente, sem armazenar resposta duplicada com dados pessoais.
- **Misturar dados atuais com histórico** → mapear listas e detalhes a partir da revisão; testar preço mantido, preço novo e cadastro alterado sem reescrita.

## Migration Plan

Planejar migration aditiva a partir da F2, com tabelas, FKs e índices de aluguel, revisão, itens e idempotência; sem reescrever ou apagar cadastros existentes. Validar avanço a partir de F2 com dados sintéticos e banco vazio em SQL Server isolado. Em Development seguir DT-18; em Testing usar a fixture. Não há rollout ou homologação nesta change. Se uma reversão posterior envolver contratos reais, não remover tabelas para forçar rollback: preservar dados, validar backup/restauração e revisar a estratégia antes de qualquer operação.
