# Tarefas — F5: fotos dos veículos

**Gate de planejamento — 24/11/2026:** o solicitante selecionou **Aprovar protótipo revisado** e **Aprovar SDD revisada** no harness. A aprovação vale para a revisão que permite selecionar a foto no cadastro e repetir só o envio após sucesso parcial. O [protótipo](prototypes/frota-foto.html) foi verificado em Chromium offline: quatro resultados do cadastro, ordem e ID/ETag simulados, retry com uma só criação, operações na edição, teclado, foco e reflow a 320 e 683 px. Isso não é evidência dos testes da aplicação. Somente uma solicitação separada autoriza o apply; acesso ao R2 não foi autorizado nesta etapa.

**Revisão da coluna Foto — 24/11/2026:** o solicitante selecionou **Aprovar revisão** e, após a atualização e verificação offline do protótipo, **Aprovar e implementar** no harness. A aprovação cobre a coluna na lista simples e agrupada e a leitura privada sob demanda; não cobre R2, publicação nem a inspeção manual da aplicação.

**Revisão dos critérios de saída — 24/11/2026:** o solicitante aprovou transferir somente a validação no App Service Linux e no ingresso HTTPS reais para [F6-06](../../../../docs/tdd.md#qualificação-de-fotos-no-app-service-linux-f6-06) e confirmou a revisão documental no harness. As provas locais e do R2 continuam obrigatórias na F5. Esta aprovação não autoriza a finalização, acesso ao Azure ou publicação.

## 1. Abertura técnica

- [x] 1.1 Verificar documentação oficial e compatibilidade de SkiaSharp, binários nativos e SDK S3 com .NET 10 nos ambientes de desenvolvimento, incluindo Linux isolado e interoperabilidade real do SDK com o R2 de teste; registrar versões, plataformas, riscos e evidências. Antes de instalar dependências ou acessar recursos, obter autorização aplicável. Sem autorização, parar a atividade dependente. O carregamento no App Service Linux é verificado em F6-06 (DT-03/11/12/17/21).
- [x] 1.2 Verificar limites operacionais de saída, alocação, concorrência e tempo por ensaio local controlado com amostras sintéticas; documentar configurações e comprovar rejeição sem ativação na API local. Preservar os limites de produto do TDD e retornar à aprovação caso haja incompatibilidade ou mudança relevante. Cotas e comportamento no plano e ingresso reais são verificados em F6-06 (F5-02; DT-12/14).

## 2. Modelo, saneamento e armazenamento privado local

- [x] 2.1 Modelar a foto ativa opcional e pendências duráveis com FKs compostas de empresa/veículo, estados e unicidade; testar em SQL Server isolado banco vazio, upgrade da F4, cadastro sem foto, referência cruzada rejeitada e nova identidade para placa reutilizada. Documentar migration e limites de rollback no contrato técnico (F5-03/04/05; RF-03/06/19; CA-01/03; DT-04/07/08/11).
- [x] 2.2 Implementar o saneamento do JPEG/PNG em teste unitário e de componente: limites de formato, dimensões, assinatura, pixel, truncamento, animação, metadados, saída e recursos; confirmar MIME real e reencode sem EXIF. Cobrir limiares, corrupção e descarte de temporários sem registro ativo (F5-02; DT-12/14).
- [x] 2.3 Implementar porta e adaptador de volume privado, fora da raiz pública; testar gravação, confirmação do objeto esperado, leitura, falhas e remoção idempotente de dados sintéticos. Documentar parâmetros e uso local sem credenciais reais (F5-01/05; DT-11/12/17/18).

## 3. Protocolo de escrita e contrato HTTP

- [x] 3.1 Implementar pendência, envio fora do lock SQL e revalidação no commit; ativar, atualizar ETag e auditar juntos, e remover apenas por operação autorizada. Testar concorrência upload × aluguel/edição/remoção, rollback de ativação e preservação da foto anterior em SQL Server real. Verificar que os logs e a auditoria omitem conteúdo e segredos (F5-03/05; RF-06/07; RNF-05; CA-03/11; DT-07/10/15).
- [x] 3.2 Implementar retomada durável e limitada sem laço contínuo; verificar reinício, falha de escrita, falha SQL depois de armazenar, resultado de commit incerto, duas retomadas e falha de remoção sintética. Comprovar que foto ativa, histórico e foto pendente inacessível permanecem protegidos; registrar limite de retenção no contrato (F5-05; RF-19; RNF-07; DT-08/10/15).
- [x] 3.3 Expor `GET/PUT/DELETE` de foto na API, documentar multipart, limites de corpo com/sem `Content-Length`, precondições, cabeçalhos, versão e ProblemDetails no OpenAPI e na documentação HTTP. Testar com `WebApplicationFactory` e SQL Server real: dois tenants com IDs conhecidos para todas as operações, sessão ausente/revogada, foto pendente/substituída/removida, veículo excluído, conflito de aluguel, versão vencida, leitura indisponível e ausência de URL direta (F5-01/02/03/05; RF-03/06; CA-01/03; DT-05/06/07/10/11/13/14).

## 4. Integração à interface

- [x] 4.1 Integrar a seção de foto inicial e a edição de Frota conforme o [protótipo](prototypes/frota-foto.html). Em Vitest verificar cadastro sem foto (só POST), falha de cadastro (zero PUT e seleção preservada), sucesso (POST sem arquivo antes de PUT no ID retornado, com `If-Match` do ETag retornado), impedimento sem ID/ETag e sucesso parcial (mensagens distintas, retry só do PUT no mesmo ID, uma única criação). Cobrir precondição vencida, aluguel aberto entre operações e resposta de envio incerta sem reenvio automático. Na edição, verificar consulta/adição/substituição/remoção, acessibilidade, nova versão e preservação da foto anterior e dos dados em edição em caso de falha (F5-03/04/05; RF-06/07; RNF-04; J-02; CA-03/11; DT-01/02/10/14).
- [x] 4.2 Integrar blob autenticado e object URL temporária; verificar no Vitest interceptor restrito à API, não persistência e revogação ao trocar/remover foto, sair, trocar de veículo ou encerrar sessão, inclusive com resposta atrasada. Confirmar placeholder textual sem bloquear formulário nem informar falsamente ausência de foto (F5-01/03/06; RF-03; RNF-01/04; CA-01; DT-05/06/11/14).

## 5. Integração R2 e critérios de saída

- [x] 5.1 Se houver acesso autorizado a bucket R2 isolado, implementar e ensaiar o adaptador com credencial limitada, endpoint e região confirmados. Verificar contrato de gravação, confirmação, leitura, falha e remoção idempotente apenas de objetos sintéticos próprios; provar ausência de acesso público. Registrar método, versão e resultado sem segredos; se acesso/compatibilidade faltar, registrar F5-01 bloqueado, sem substituir o ensaio pelo volume local (F5-01/05; DT-11/12/14/17).
- [x] 5.2 Exercitar J-02 por teclado em Playwright nas duas origens locais com empresas distintas: cadastro sem foto, cadastro rejeitado sem upload, sucesso dos dois passos e recuperação do sucesso parcial com apenas um cadastro; verificar operações na edição e regressões de locação, inclusive fechamento com armazenamento indisponível (F5-06 conforme §5.5). Comparar a SPA com o [protótipo](prototypes/frota-foto.html) nos dois sentidos: cenários, estados e controles, sem extras. Verificar rótulos, foco, contraste, reflow/zoom e registrar Tailwind nativo versus tokens vendorizados. Fazer inspeção manual conforme TDD, separada da automação (F5-03/04/06; CA-01/03/11; DT-02/14/21).
- [x] 5.3 Consolidar verificações locais e do R2 isolado, inspeção manual, regressão, comandos, resultados e limitações nas evidências; validar estritamente a change e conferir F5-01 a F5-06 no TDD somente com comprovação. Preservar a redação de F5-06 alinhada ao §5.5 e registrar F6-06 como critério obrigatório ainda não executado. Não declarar v1 publicada ou homologada (DT-14/21).

## 6. Miniaturas na listagem de Frota

- [x] 6.1 Implementar a coluna Foto na lista simples e agrupada conforme o [protótipo](prototypes/frota-foto.html). Testar miniatura com texto alternativo, estados acessíveis, filtragem, carregamento sob demanda e limitado, cancelamento e revogação ao trocar a lista, desmontar e encerrar sessão, sem alterar a API (RF-03/06; RNF-01/04; CA-01/03/11; DT-02/06/11/14).
- [x] 6.2 Verificar no navegador com API e SQL locais a exibição, ausência, indisponibilidade isolada, retorno da edição, agrupamento e uso a 320 px. Comparar a SPA com o protótipo nos dois sentidos e registrar resultados nas evidências sem tratar a revisão do protótipo como inspeção manual da aplicação. Manter o ensaio R2 separado (J-02; F5-03/06; DT-14/21).

## Workflow follow-up

- Com autorização posterior do solicitante, a revisão de entrega, os commits, o push, o [PR #5](https://github.com/JuliaaHartmann/locadora-de-veiculos/pull/5), o Sync e o Archive foram executados na branch da F5. A integração à `main` ainda requer revisão e autorização de merge, conforme `AGENTS.md`.

## Evidências do apply local — 24/11/2026

- Os [resultados e limitações](evidencias.md) e o [contrato F5](../../../../app/backend/docs/contratos-f5.md) registram o que foi executado. A SDD já cobria o upload automático após cadastro e o retry exclusivo da imagem; não foi alterado o escopo aprovado.
- `2.1–3.3`: 104 testes unitários e 97 de integração aprovados na suíte com SQL Server isolado, incluindo migration, validar/sanear arquivo, volume privado, retomada em novo host, reuso da chave, concorrência, isolamento e contratos HTTP.
- `4.1–4.2`: 81 testes em 10 arquivos no Vitest, build e checagem de tipos aprovados; o teste do cliente confirma multipart e bearer só na API própria.
- `1.1` e `1.2`: [qualificação local](evidencias.md#qualificação-local-dos-limites-de-processamento--24112026) passou em Ubuntu/.NET 10 (`ldd` e 8 testes, com 12 medições), e 3 testes com a API/SQL isolados verificaram fronteiras, concorrência e descarte após o prazo. A regressão passou com 104 unitários e 100 de integração. A prova no Azure continua pendente em F6-06; o prazo cooperativo não equivale a interrupção rígida do codec.
- `5.1`: ensaio opt-in passou no bucket de desenvolvimento, com escrita, leitura, hash, substituição, remoção lógica, compensação física idempotente de órfão e negação de leitura sem assinatura. Configuração de acesso público e escopo da credencial confirmados pelo solicitante no painel. Ver [evidências](evidencias.md#ensaio-real-no-r2--24112026).
- `5.2`: Playwright passou na J-02 da F5 e na regressão J-01 a J-05 da F4 nas duas origens. Separadamente, o solicitante confirmou a inspeção manual e aprovou o cadastro com foto, a edição e as miniaturas. Para `5.3`, ver a [consolidação dos critérios](evidencias.md#revisão-aprovada-dos-critérios-de-saída--24112026).
