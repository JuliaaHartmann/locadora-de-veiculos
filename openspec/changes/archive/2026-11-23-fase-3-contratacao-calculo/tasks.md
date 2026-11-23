# Tasks

As tarefas abaixo registram a implementação e a verificação da F3. A aprovação explícita dos artefatos e a solicitação separada de apply estão registradas nas [evidências](evidencias.md).

## 1. Cálculo e elegibilidade (F3-01)

- [x] 1.1 Conferir as versões e APIs oficiais da stack efetivamente usada (Angular/Signal Forms, Tailwind, ASP.NET Core, EF Core/SQL Server e runtimes) antes de alterar código; registrar versões, fontes e compatibilidade, sem instalar dependências sem autorização (DT-01–04, DT-14).
- [x] 1.2 Implementar cálculo puro de duração e plano, com teste de 0 h no domínio, 24 h exatas, 24 h + 1 min e exemplos de CA-07: Diário R$ 500,00, Controlado R$ 300,00 e Livre R$ 200,00 com entradas conhecidas. Não usar quilometragem final desconhecida na estimativa nem expor fechamento (RN-01–05, DT-09).
- [x] 1.3 Calcular componentes de taxa fixa/diária e seguros independentes; testar arredondamento meio para cima, soma por componente, caução separada, plano Livre e indicação de parcela pendente em Diário/Controlado (RN-02–06, RN-09).
- [x] 1.4 Reutilizar validações documentais e exigir vínculo direto, plano do grupo e referências ativas. Testar PF como próprio condutor, PJ com/sem vínculo, documento inválido, CNH ausente, validade antes/no dia local do retorno, hodômetro inválido, retorno não posterior e offset inválido/ambíguo (RF-10, RF-14–15, CA-02, CA-04, CA-06; DT-08–09).

## 2. Persistência, estimativa e contratação (F3-01, F3-02, F3-04)

- [x] 2.1 Mapear aluguel, revisão, itens e registro idempotente com filtros/guarda de tenant, FKs compostas, sem cascata física, `rowversion` e índice filtrado de uma ocupação por veículo. Em SQL real, validar migration de banco vazio e desde F2 com dados preservados, constraints de vínculo e rejeição de duplicata (DT-04, DT-07–10).
- [x] 2.2 Implementar prévia sem gravação com relógio controlado e preços aplicáveis. Testar que duas prévias não ocupam veículo, seguro não selecionado não exige preço, preço ausente do selecionado bloqueia, zero funciona e combustível de devolução não é pré-condição (RF-12, RF-14, RN-06, CA-07).
- [x] 2.3 Confirmar sob o lock da empresa, revalidando relógio, referências, preços e ocupação. Testar em SQL real duas conexões para o mesmo veículo e exceção antes do commit, inclusive na auditoria; só um aluguel confirmado, sem estado parcial (RF-07, RF-14; RNF-03, RNF-05; CA-06; DT-10, DT-15).
- [x] 2.4 Implementar e testar idempotência por empresa/ator/operação: mesma chave e payload, reenvio depois do commit ou de revisão posterior, mesma chave com entrada diferente e reenvio após falha antes do commit. Verificar resultado original, uma ocupação e um evento de sucesso, sem cache de documentos (RNF-02–03, CA-06).

## 3. Revisão, consulta e bloqueios (F3-03, F3-04)

- [x] 3.1 Revisar aluguel com saída imutável, ETag e nova revisão. Testar preço mantido apesar de alteração de plano/seguro, inclusão/substituição pelo preço corrente, mudança só de item incrementando versão, revisão inválida/obsoleta sem mutação ou perda do histórico (RF-15, CA-06; DT-08–10).
- [x] 3.2 Trocar veículo sob o mesmo lock; testar sucesso, grupo/plano incompatível e corrida troca × contratação com conexões independentes; garantir ocupação original intacta quando a troca falha (RF-06–07, RF-15, CA-06; DT-10).
- [x] 3.3 Efetivar bloqueios de Frota, Pessoas e Preços já especificados. Cobrir SQL real de criação concorrendo com alteração/exclusão de veículo, condutor ou taxa, bloqueio de cliente, guardas de CPF/CNH/validade/vínculo, alteração indireta via cliente PF e exclusão de plano usado; verificar que edições permitidas não reescrevem contrato (RF-05–06, RF-08–09, RF-11, RF-19; CA-02–05).
- [x] 3.4 Implementar lista, filtro por veículo/período e detalhe pela revisão, com ordem estável, paginação e dados do RF-16. Testar precisão de representação e distinção entre estimativa e total final, alteração cadastral sem reescrita e identidade histórica independente de reutilização de identificador (CA-07; DT-07–09).

## 4. API e SPA (F3-01–F3-05)

- [x] 4.1 Publicar somente os contratos F3 no OpenAPI e registrar exemplos em documentação de API sob `app/`. Cobrir com WebApplicationFactory: 201/Location/ETag, 200, 400, 401, 404, 409, 412, 428 e falha controlada de dependência sem vazamento; testar sessão revogada, empresa A/B, anexação cruzada e duas origens CORS permitidas (RF-02–03; RNF-01–03; DT-05–07, DT-13–14).
- [x] 4.2 Construir lista, navegação e detalhe conforme [Aluguéis](prototypes/alugueis.html), com testes de rota protegida, paginação, filtros, estados de consulta e estimativa identificada, além da recuperação de erro (RF-16, RNF-04, CA-07; DT-01–02).
- [x] 4.3 Construir contratação e edição com Signal Forms conforme [Aluguéis](prototypes/alugueis.html), com testes de opções vinculadas sem corte silencioso, prévia invalidada ao mudar campos, caução separada, focos/erros, confirmação, reenvio idempotente e conflito de versão com recarga explícita (RF-10, RF-14–15; CA-06–07).
- [x] 4.4 Integrar feedback das guardas nas telas existentes conforme [Bloqueios cadastrais](prototypes/bloqueios-cadastrais.html); verificar mensagens e preservação dos dados, sem substituir a navegação da F2 nem inserir o seletor de revisão no produto (CA-02–05, RNF-04).

## 5. Verificação integrada e evidências (F3-05)

- [x] 5.1 Executar builds, regressões das F1/F2, MSTest e integração do SQL Server em ambiente isolado via `app/backend/scripts/Testar-Integracao.ps1`, testes da SPA e jornada J-03 no Playwright; registrar comandos, ambiente, resultados e limitações. Não usar volume Development nem marcar aceite de devolução, foto ou publicação.
- [x] 5.2 Verificar em navegador a paridade nos dois sentidos com [Aluguéis](prototypes/alugueis.html) e [Bloqueios cadastrais](prototypes/bloqueios-cadastrais.html): cenários previstos têm tela/estado correspondente, e nenhum campo, ação ou funcionalidade carece de base na spec. Exercitar teclado, foco, reflow/zoom nativo, rotulagem, feedback e contraste; registrar divergências justificadas e separar acessibilidade observada de certificação.
- [x] 5.3 Registrar evidências reais no fechamento da implementação e revisar F3-01–F3-05 no roadmap. Só marcar o que tiver evidência e separar execução local, revisão, integração, homologação e publicação (DT-21).

## Workflow follow-up

- Preparar PR com rastreabilidade e verificações apenas se solicitado/autorizado; não integrar por fora do PR.
- Sincronizar e arquivar a change somente mediante solicitação e após os gates aplicáveis.
