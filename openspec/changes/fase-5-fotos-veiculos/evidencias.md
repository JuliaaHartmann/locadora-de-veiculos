# Evidências — F5: fotos dos veículos

## Escopo do primeiro apply — 24/11/2026

- Branch `feat/fase-5-fotos-veiculos`, base `5a70ba8` de `main`/`origin/main`. A autorização de apply e de restauração de SkiaSharp, binários nativos e AWSSDK.S3 foi dada pelo solicitante. Nenhum recurso externo foi criado ou acessado com credenciais.
- O comportamento pedido já constava na proposta, nos deltas e no protótipo aprovados: seleção opcional na criação, `POST` cadastral inalterado, `PUT` posterior com ID/ETag e retry só da foto. Não houve ajuste de escopo. O [design](design.md#registro-do-apply-local--24112026) e os [contratos F5](../../../app/backend/docs/contratos-f5.md) registram as decisões materializadas.

## Abertura técnica e limites

- .NET SDK 10.0.404; EF CLI 10.0.9 e runtime EF 10.0.15; Docker 29.7.2; Node 24.21.0. Consultadas a [documentação de SkiaSharp](https://github.com/mono/SkiaSharp), a [referência do SDK .NET para R2](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/) e a [compatibilidade S3](https://developers.cloudflare.com/r2/api/s3/api/).
- NuGet oficial: `SkiaSharp` e `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.157.0 e `AWSSDK.S3` 4.1.2, com lockfiles. `dotnet restore app/backend/Locadora.slnx --locked-mode --source https://api.nuget.org/v3/index.json` aprovado; auditoria de dependências diretas/transitivas não reportou pacote vulnerável nas fontes consultadas. Binário Linux x86-64 carregado via `ldd` e `ctypes` no Ubuntu/WSL (milestone 157); WSL sem .NET disponível para o ensaio gerenciado. Não é prova do App Service.
- Ensaio sintético em Windows: `dotnet test app/backend/tests/Locadora.UnitTests --no-restore --filter FullyQualifiedName~SaneadorFotoTests --logger 'console;verbosity=detailed'`: 4 aprovados. PNG de 4.000 × 4.000 pixels, 70.475 bytes antes/depois, 619 ms em uma rodada. Limites de corpo/pixels, saneamento e cancelamento antes do processamento exercitados. Há uma vaga sem fila, janela de 15 s para leitura/saneamento, limite de saída de 65 MiB e buffer de pixels RGBA de até 64.000.000 bytes. Não houve medição de pico total de memória ou de tempo nativo no ambiente-alvo; o decode nativo não é preemptivo, e resultado fora do prazo não ativa foto.
- `dotnet ef migrations has-pending-model-changes --project src/Locadora.Infrastructure --startup-project src/Locadora.Api`: nenhuma divergência; aviso de patch da CLI EF. Usada configuração de design descartável, sem banco de desenvolvimento.

## Resultados locais

| Verificação | Resultado |
| --- | --- |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF5` — etapa backend | **104 unitários e 97 de integração**, nenhum ignorado. SQL Server Developer isolado: banco vazio e upgrade F4, limites com/sem `Content-Length`, corpos e versões, cabeçalhos, isolamento A/B, logout, aluguel aberto, ETag de edição/remoção, rollback e auditoria. Testes de falha incluem perda simulada após commit, retomada em outro host com o mesmo SQL/volume, duas retomadas e reuso da chave interna. Locação e devolução continuam mesmo com falha real injetada no adaptador do teste. |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF5 -FiltroIntegracao 'FullyQualifiedName~FotosTests'` | Os **11 testes de fotos** (subconjunto dos 97) e a jornada J-02 da F5 passaram. Chromium nas duas origens: quatro resultados do cadastro, ID/ETag, reenvio sem duplicidade, consulta e ciclo de edição, isolamento e revogação. Na primeira origem: foco, contraste amostral de **7,58:1**, reflow a 320 px e zoom nativo de 200%. |
| `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF4` | Backend passou novamente; no Chromium, **J-01 a J-05** passaram nas duas origens por teclado. Conferidos isolamento A/B, revogação, repetição de fechamento após perda de resposta, histórico, veículo liberado, reflow a 320 px e zoom nativo de 200%. |
| `npm test -- --watch=false`, `npm run build`, `./node_modules/.bin/tsc.cmd --noEmit --project tsconfig.spec.json` em `app/frontend` | **81 testes em 10 arquivos**; build e tipos aprovados. Inclui os quatro resultados do cadastro, conflito/resultado incerto sem reenvio automático, multipart com bearer, revogação de URL temporária e resposta atrasada descartada. |
| `openspec validate --all --strict --no-interactive`, `git diff --check` e verificação de whitespace de arquivos novos | Change e sete especificações principais aprovadas; sem erros de whitespace. |

As contagens não somam rodadas. O primeiro teste HTTP detectou consumo do multipart pelo binding MVC antes da ação; corrigido usando leitura limitada direta, só após a precondição. Outra rodada detectou sobrescrita do código específico no ProblemDetails e uma asserção dependente da ordem textual dos cabeçalhos; ambas corrigidas. A primeira jornada F5 passou; após acrescentar foco no título, a rodada seguinte parou no segundo cadastro porque o teste enviava teclas ao seletor enquanto ele ainda estava desabilitado pelo salvamento anterior. O roteiro passou a aguardar foco e controle habilitado. A etapa de navegador usa Playwright 1.65.1 e Chromium 159.0.8154.6 já disponíveis, sem instalar dependência da SPA.

Na revisão local, a auditoria de foto emite evento, identidade do veículo e nome do campo alterado, sem bytes, nome original ou credenciais. Os erros esperados do provedor são convertidos em mensagens genéricas; não se verificaram logs do R2 real. A retomada foi ensaiada recriando o host de testes com o mesmo SQL e volume, sem reiniciar um processo Kestrel em execução.

## Comparação com o protótipo aprovado

| Comportamento aprovado | Verificação |
| --- | --- |
| Selecionar foto no cadastro, salvar ou rejeitar dados antes de enviar | Vitest verifica ausência de PUT sem foto ou diante de falha. Playwright faz o cadastro/erro reais no SQL isolado, com seleção por teclado e arquivo sintético. |
| Cadastro e foto salvos, ou sucesso parcial com nova tentativa só da foto | Vitest verifica ordem e ID/ETag. Playwright induz um `503` antes do primeiro PUT chegar à API e envia o próximo PUT real pelo proxy, sem repetir a criação; também prova sucesso direto nas duas etapas. |
| Foto existente, adição, substituição, remoção, indisponibilidade e impedimentos | Ciclo real em Playwright; Vitest e integração HTTP exercitam estados de erro, precondição, conflito, objeto ausente e revogação da object URL. Nenhuma captura por arquivo local é apresentada como prova do R2. |
| SPA → protótipo | Os controles novos são seleção, envio/reenviar, substituição e remoção; nenhuma nova rota, modo de simulação ou área de navegação foi incluída. A SPA mantém paginação, filtros, ações e navegação reais preexistentes; o protótipo simula apenas o recorte F5. |

O protótipo incorpora tokens MIT de Tailwind CSS 4.3.5 e CSS inline; a SPA usa Tailwind/PostCSS nativo 4.3.5, shell e controles reais. O template do editor existente conserva “Cancelar” fora do sucesso parcial e adiciona a seção em duas colunas, empilhada em telas estreitas. A SPA exibe o blob autorizado, não a ilustração do HTML. Inspeção visual de capturas de edição a 1366 e 320 px confirmou rótulos, foco do seletor, leitura dos limites e empilhamento dos campos e ações; capturas sintéticas fora do repositório. Essa inspeção estática não equivale à inspeção manual interativa da jornada nem à certificação de acessibilidade.

## Pendências registradas no primeiro apply

- **R2 / F5-01 e tarefa 5.1:** o primeiro apply não tinha bucket nem credencial autorizados. O [ensaio posterior](#ensaio-real-no-r2--24112026) registra a validação externa.
- **1.1 / 1.2:** ensaio gerenciado no runtime Linux-alvo e verificação dos limites efetivos de recursos nele. O ensaio local não valida esse ambiente nem demonstra limite rígido para uma chamada síncrona ao codec nativo.
- **5.2:** inspeção manual interativa complementar da jornada. **5.3:** consolidação de todos os gates, incluindo R2. Não houve homologação ou publicação.
- **F5-06:** interpretação aprovada do §5.5: falha de armazenamento não bloqueia locação sem dependência de imagem; não se adotou a redação ambígua do checklist como regra de produto.

## Revisão: coluna Foto na listagem — 24/11/2026

O solicitante aprovou a revisão dos artefatos e o apply local no harness. O [protótipo revisado](prototypes/frota-foto.html) passou em Chromium offline: coluna e texto alternativo, ausência e indisponibilidade, agrupamento, remoção e retorno por teclado, rolagem restrita à tabela a 320 px e zero requisições externas. O HTML usa ilustrações sintéticas e tokens Tailwind 4.3.5 já incorporados; a SPA usa o mesmo Tailwind nativo e o blob recebido da API. A revisão do HTML não é inspeção manual da aplicação.

`npm test -- --watch=false` em `app/frontend`: **84 testes aprovados em 11 arquivos**, incluindo o ensaio focado de miniaturas (3 testes com `HttpTestingController` e o interceptor real). Conferidos o GET autenticado como blob, a espera pela proximidade da linha, o limite de quatro leituras, a liberação da fila, a distinção `404`/`503`/falha no `<img>` e a limpeza ao filtrar, desmontar e encerrar sessão. `npm run build` e `./node_modules/.bin/tsc.cmd --noEmit --project tsconfig.spec.json` passaram.

`pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF5 -FiltroIntegracao 'FullyQualifiedName~FotosTests'`: **11 testes de integração aprovados** com SQL Server isolado, além da J-02 com Playwright nas duas origens locais (`localhost:4201` e `127.0.0.1:4201`). O navegador conferiu miniatura decodificável, ausência e falha de leitura simulada para um único veículo, com as demais fotos e ações disponíveis. Confirmou a lista agrupada, os pixels da nova foto após substituição, o estado “Sem foto” após remoção e a miniatura após adição. Conferiu também isolamento A/B, revogação de sessão e, a 320 px, foco e rolagem horizontal por teclado restrita à tabela.

Regressão: `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF4 -FiltroIntegracao 'FullyQualifiedName~DevolucaoTests'`: **10 testes de integração aprovados** e J-01 a J-05 por teclado nas mesmas duas origens, incluindo fechamento, histórico e liberação do veículo. `openspec validate --all --strict --no-interactive`: **8 itens aprovados**. As tarefas 6.1 e 6.2 foram concluídas; o checklist está em 10 de 15, sem encerrar os gates anteriores.

A comparação da SPA com o protótipo conferiu a posição da coluna, dimensões, texto alternativo, ícone e estados, lista simples/agrupada, retorno da edição e rolagem em tela estreita. A SPA conserva ações, filtro e paginação preexistentes; não adiciona novas rotas, controles de foto na lista nem modos de simulação. As capturas de revisão da SPA são sintéticas e estão em `%LOCALAPPDATA%\Temp\opencode\f5-miniaturas\f5-lista.png` e `f5-lista-320.png`, fora do repositório.

**Inspeção manual (relato do solicitante):** o solicitante informou ter inspecionado e aprovado a interface local, incluindo **cadastro com foto, edição e miniaturas na listagem**. A aprovação complementa a automação e os registros visuais; não constitui ensaio do armazenamento remoto nem homologação.

## Ensaio gerenciado do SkiaSharp em Linux — 24/11/2026

- Com autorização, baixada a imagem oficial `mcr.microsoft.com/dotnet/sdk:10.0.404` para `linux/amd64` (digest `sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d`). Ambiente: Ubuntu 24.04.5 LTS x86-64, runtimes .NET e ASP.NET Core 10.0.15, SkiaSharp e assets nativos 4.157.0.
- `dotnet build app/backend/tests/Locadora.UnitTests/Locadora.UnitTests.csproj --no-restore --configuration Debug` aprovado sem avisos. Montado somente `app/backend/tests/Locadora.UnitTests/bin/Debug/net10.0` em `/verificacao`, somente leitura. `docker run --rm --pull never --platform linux/amd64 --network none --read-only --tmpfs /tmp:rw,size=128m --memory 512m --memory-swap 512m --cpus 1 --pids-limit 128` (também sem capacidades adicionais e sem privilégios novos). Não houve acesso a credenciais, R2 ou SQL.
- Comando no contêiner: `dotnet vstest /verificacao/Locadora.UnitTests.dll /TestCaseFilter:FullyQualifiedName~SaneadorFotoTests /ResultsDirectory:/tmp/test-results /Logger:"console;verbosity=detailed"`. A primeira tentativa não chegou ao codec porque o VSTest tentou criar `TestResults` no volume somente leitura; redirecionar a saída para `/tmp` corrigiu o ensaio. Resultado: **4 testes aprovados**, incluindo decodificação/reencode de JPEG e PNG, remoção de metadados de teste, rejeições e cancelamento antes da decodificação.
- Amostra PNG sintética de 4.000 × 4.000: 70.475 bytes na entrada e na saída, 836 ms; pico **197.373.952 bytes** (cerca de 188,2 MiB), medido no cgroup de todo o contêiner, incluindo SDK e runner. Não é medição isolada do codec nem de pior caso. Continua pendente a qualificação no App Service Linux e a verificação de limites efetivos sob cargas variadas; o cancelamento continua cooperativo, sem interromper chamada nativa síncrona em andamento. Não constitui conclusão de `1.1`, `1.2` ou F5-02.

## Ensaio real no R2 — 24/11/2026

- O solicitante autorizou a execução no bucket `locadora-veiculos-dev` e confirmou no painel: `r2.dev` desativado, nenhum domínio público ativo e credencial **Object Read & Write** restrita a esse bucket. A verificação das opções do painel é relato do solicitante, não consulta administrativa do agente.
- `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -R2`: **1 teste de integração aprovado** (22 s no teste) com .NET 10.0.15, AWSSDK.S3 4.1.2, API em TestServer/Development e SQL Server isolado em loopback. O host de testes importou só a seção `Fotos` dos User Secrets da API e confirmou o tipo efetivo `ArmazenamentoR2Fotos` e o bucket esperado. Nenhum valor de credencial foi listado, alterado ou incluído no log. A assinatura com região `auto` funcionou em requisições reais.
- `PUT` PNG para as empresas sintéticas A e B, `GET` autenticado para ambas e substituição por JPEG: ETags fortes renovados e SHA-256/tamanho iguais ao SQL. O proxy serviu MIME esperado, `private, no-store`, `nosniff` e não redirecionou. `GET`/`PUT`/`DELETE` sem sessão ou após revogação retornaram `401`; nos dois sentidos entre empresas, com identificador e ETag conhecidos, retornaram `404`. O S3 sem assinatura negou a leitura do objeto de teste, com erro de autenticação e sem imagem ou redirecionamento. A configuração sem domínios públicos foi confirmada separadamente no painel.
- A divergência de tamanho foi induzida só no SQL de teste: a leitura real respondeu `503 foto_indisponivel` e a consulta do veículo seguiu `200`. Falha de SQL injetada após escrita confirmada gerou pendência; a foto anterior e seu ETag permaneceram visíveis. A compensação removeu fisicamente **um órfão sintético** e remover de novo a mesma chave foi idempotente. O `DELETE` HTTP das fotos ativas foi **lógico** (`204` seguido de `GET 404`).
- Duas tentativas iniciais pararam sem chamar o R2 porque o TestServer não carregou automaticamente a seção `Fotos`. Uma rodada posterior já gravou e leu dois PNG, mas parou porque esperava somente `403` para o `GET` sem assinatura e o provedor respondeu `400`. A asserção passou a exigir rejeição de autenticação para `400`, sem registrar o corpo bruto. A rodada completa deixou três objetos sintéticos retidos conforme o contrato; os dois da tentativa interrompida também ficaram no bucket. Nenhum objeto preexistente foi consultado, alterado ou expurgado. Os bancos e volumes SQL isolados foram descartados.
- Regressão sem R2: `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` aprovou **104 unitários e 97 de integração**, sem falhas ou ignorados. O runner local excluiu `R2Real` e usou somente armazenamento privado no volume de teste.
- Evidência para `5.1` e F5-01/F5-05 no recorte de desenvolvimento. Não houve publicação, uso de dados reais, validação no App Service ou homologação.

Naquela etapa, o checklist ficou em **12 de 15**: `5.1` (ensaio R2) e `5.2` (automação e aprovação manual) foram concluídas; `1.1`, `1.2` e `5.3` ainda estavam abertas sob os critérios anteriores.

## Qualificação local dos limites de processamento — 24/11/2026

Os limites de produto (5 MiB no corpo multipart, 16.000.000 de pixels, JPEG/PNG estáticos) e os critérios de saída não foram alterados. O ensaio não acessou Azure ou R2, não leu User Secrets e não alterou os objetos sintéticos já existentes no bucket.

### Compatibilidade Linux e medições

- Imagem já disponível `mcr.microsoft.com/dotnet/sdk:10.0.404`, Ubuntu 24.04.5 LTS `linux/amd64`, glibc 2.39 e .NET/ASP.NET Core 10.0.15. SkiaSharp e NativeAssets.Linux.NoDependencies **4.157.0**. `ldd` no `libSkiaSharp.so` de `runtimes/linux-x64/native` não acusou biblioteca ausente (`libstdc++`, `libpthread`, `libdl`, `libm`, `libc`, carregador ELF, `librt`, `libgcc_s`). SHA-256 da biblioteca: `37d70d52efbd0d03f1f1e6d458f9050e55d1f47ec8760d0fdbe5043416dbdac8`, igual ao da saída Release da API.
- Fixtures geradas no Windows, fora da medição, com `dotnet test app/backend/tests/Locadora.UnitTests/Locadora.UnitTests.csproj --no-restore --configuration Release --filter TestCategory=FotoAmostrasF5 --logger "console;verbosity=detailed"` e `LOCADORA_F5_AMOSTRAS` apontando para pasta temporária exclusiva e vazia. `AmostrasFotoLimites` gera quatro arquivos determinísticos, variando os pixels para atingir a faixa comprimida (sem completar o tamanho com metadados).
- Linux: `dotnet vstest /verificacao/Locadora.UnitTests.dll /TestCaseFilter:"FullyQualifiedName~SaneadorFotoTests|TestCategory=FotoLimitesF5" /ResultsDirectory:/tmp/test-results /Logger:"console;verbosity=detailed"`, sob `timeout 120s` para a suíte inteira. Docker com `--pull never --network none --read-only --memory 512m --memory-swap 512m --cpus 1 --pids-limit 128`; somente binários `Debug/net10.0` e as amostras montados em leitura. **8 testes aprovados**: os 4 já existentes e 4 novos, estes com três saneamentos por amostra. A primeira tentativa não mediu as amostras por ordem incorreta dos argumentos da asserção de faixa; a correção foi exclusiva do teste.
- O tempo inclui `Sanear`, não a geração, o multipart, o SQL ou o armazenamento. RSS do processo de testes amostrado em intervalos de 10 ms durante cada saneamento; o valor não atribui toda a memória ao codec. `GC.GetAllocatedBytesForCurrentThread` registrou de **16.775.440 a 16.892.136 bytes** por medição, sem contar alocação nativa. Pico total do cgroup (runner/SDK/processos e cache incluídos): **365.846.528 bytes** (348,9 MiB), com `max`, `oom` e `oom_kill` em zero. Não é medida do consumo da API completa no Azure.

| Amostra | Dimensões (pixels) | Entrada (B) | Saída (B) | Tempo, mín.–máx. de 3 (ms) | Maior RSS amostrado (B) |
| --- | ---: | ---: | ---: | ---: | ---: |
| PNG RGB com textura | 4.000 × 4.000 (16.000.000) | 5.101.219 | 5.841.275 | 1.359,8–1.669,7 | 185.978.880 |
| PNG com transparência | 5.000 × 3.199 (15.995.000) | 5.054.202 | 5.054.202 | 852,4–1.173,1 | 196.300.800 |
| JPEG colorido | 4.000 × 4.000 (16.000.000) | 5.068.154 | 5.466.839 | 698,7–825,8 | 235.323.392 |
| JPEG em tons de cinza | 5.000 × 3.199 (15.995.000) | 5.209.138 | 6.591.694 | 737,3–905,3 | 274.522.112 |

A saída permaneceu abaixo da proteção de 65 MiB em todas as amostras. Pode ser maior que a entrada: 5 MiB limita o **upload**, não a imagem que o servidor reencoda. Os valores acima caracterizam o conjunto ensaiado, não um pior caso de memória/tempo, SLO ou prova da oferta Azure.

### Limites, concorrência e falha segura pela API local

`pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -FiltroIntegracao 'FullyQualifiedName~Limites_F5' -Detalhado`: **3 testes aprovados** em TestServer no Windows, SQL Server isolado, empresas sintéticas e volume privado. `R2Real` foi excluído.

| Verificação | Resultado |
| --- | --- |
| Fronteiras e entradas inválidas | Corpo multipart válido de **5.242.880 B** aceito (`204`), **5.242.881 B** rejeitado (`413`), com e sem `Content-Length`. PNG/JPEG de 16.004.000 pixels, CRC incorreto, PNG animado, JPEG truncado e arquivo vazio receberam `400`; GIF e divergência MIME/assinatura receberam `415`. Não houve gravação, registro nem ativação para entradas rejeitadas. Rejeitar uma substituição preservou bytes e ETag da foto anterior, sem auditoria de sucesso adicional. |
| Concorrência entre empresas | Com o upload JPEG de 5.068.154 B admitido e a vaga ainda ocupada no armazenamento controlado, quatro envios simultâneos da outra empresa receberam `503 foto_processamento_indisponivel` em **27,4–67,9 ms** (sem fila). Nenhum dos quatro criou registro; consulta e ETag permaneceram válidos. O primeiro upload e o reenvio posterior de PNG perto do limite foram aceitos. Não é teste de capacidade de rede ou vazão. |
| Prazo cooperativo | Dobra de teste retornou o resultado já saneado após receber o cancelamento da janela real de **15 s**, simulando chamada síncrona não preemptiva. A API retornou `503` em **15.048,0 ms**, sem objeto gravado, foto ativada ou mudança de ETag; um novo envio foi aceito após liberar a vaga. Isso prova descarte do resultado tardio, não interrupção de código nativo durante decode/encode. |

O TestServer não oferece `IHttpMaxRequestBodySizeFeature`; o teste exercita o contador manual e o contrato HTTP da aplicação, não limites do ingresso Azure. A amostragem de memória do codec foi feita separadamente no contêiner Linux, com uma execução por vez; não mede RSS da API inteira sob tráfego ou outras cargas.

Regressão local sem R2: `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1` aprovou **104 unitários e 100 de integração**, incluindo os 3 novos cenários HTTP, sem falhas ou ignorados. O ensaio opt-in de medição do codec ficou fora da regressão e está registrado separadamente acima. `openspec validate --all --strict --no-interactive`: **8 itens aprovados** naquela rodada. O checklist ficou em **12/15** antes da decisão formal.

## Revisão aprovada dos critérios de saída — 24/11/2026

O solicitante aprovou formalmente atribuir à F6 apenas a validação no processo/plano real do App Service Linux e no ingresso HTTPS, e confirmou a revisão dos artefatos no harness. Permanecem na F5 a implementação, a compatibilidade nativa local, a prova local dos limites e da falha segura, o ensaio real do R2 e a aprovação manual da interface.

| Critério | Evidência de saída da F5 |
| --- | --- |
| F5-01 e F5-03 | Proxy autenticado e armazenamento privado em volume local e R2; negação sem sessão, entre empresas e de acesso direto sem assinatura no R2. |
| F5-02 (`1.1` e `1.2`) | Versões e compatibilidade local do SkiaSharp e interoperabilidade real do AWSSDK.S3/R2. Oito testes no Ubuntu/.NET 10, com 12 medições; três testes HTTP/SQL de fronteiras, concorrência sem fila e descarte sem ativação. |
| F5-04 | Cadastro sem foto e envio separado, com retry só da imagem, verificados em integração, SPA e navegador. |
| F5-05 | Falhas de escrita e SQL, retomada local e remoção idempotente de um órfão sintético no ensaio autorizado do R2; sem expurgo de fotos ativas ou retidas. |
| F5-06 | Testes de locação e devolução com falha de armazenamento e regressão em navegador; falha da foto não bloqueia a operação que não depende dela. |

[F6-06](../../../docs/tdd.md#qualificação-de-fotos-no-app-service-linux-f6-06) permanece **pendente e obrigatório antes da homologação e da publicação definitiva**: carregar os binários reais, testar os limites pelo ingresso HTTPS, observar CPU/memória/concorrência/reinícios no plano gratuito e provar que imagens inválidas, excessivas ou processadas fora do prazo não são ativadas. O risco residual é a diferença entre o Ubuntu ensaiado e a plataforma compartilhada. Se o ensaio real falhar ou a oferta gratuita não couber, parar e revisar; não reduzir limites, trocar a stack ou adotar plano pago automaticamente.

Para a futura verificação, a documentação da Microsoft distingue [disponibilidade e configuração do runtime .NET](https://learn.microsoft.com/azure/app-service/configure-language-dotnetcore) das [cotas e métricas da instância App Service](https://learn.microsoft.com/azure/app-service/web-sites-monitor), inclusive tempo de CPU no plano gratuito/compartilhado. Consultada apenas a documentação pública; oferta, região, arquitetura, runtime efetivo, quotas e métricas da conta não foram consultados nem presumidos.

A modalidade de implantação permanece indefinida até a F6. Nesta revisão não houve acesso ao Azure ou ao R2, exclusão dos cinco objetos sintéticos remanescentes, homologação ou publicação.

**Verificação de prontidão da F5:** `openspec validate --all --strict --no-interactive` aprovou 8 itens; `openspec instructions apply --change fase-5-fotos-veiculos --json` confirmou **15/15 tarefas**, estado `all_done`. Os seis critérios da F5 no TDD têm evidência, e F6-06 segue desmarcado. Os ensaios não foram repetidos nesta revisão documental. Não foi identificado bloqueio técnico para iniciar o encerramento formal da F5; esse processo aguarda autorização separada do solicitante.

## Revisão final para entrega — 24/11/2026

- O solicitante autorizou os commits, o push, a abertura da PR para `main` e o Sync/Archive, mas não autorizou merge, acesso ou publicação no Azure nem exclusão dos cinco objetos sintéticos do R2.
- Revisão dos 53 arquivos candidatos ao primeiro conjunto de commits, por lista de caminhos, tipos de arquivo, diff e busca por padrões de segredos e dados pessoais. Os apontamentos foram uma senha deliberadamente sintética de uma empresa criada em SQL de teste e dois hashes públicos de verificação de binários. Nenhuma credencial real, token, dado pessoal real, arquivo temporário, imagem de teste, log ou artefato de build foi identificado no diff. A migration EF gerada e os lockfiles são artefatos versionados intencionalmente.
- Change `prototype-first` com todos os artefatos completos e **15/15 tarefas**; `openspec validate --all --strict --no-interactive`: **8 itens aprovados** antes do Sync. A prova específica no Azure continua obrigatória e pendente em F6-06. A validação histórica do R2 e a inspeção manual permanecem registradas separadamente.
- Em `app/frontend`, `npm test -- --watch=false`: **84 testes em 11 arquivos**. `npm run build` e `./node_modules/.bin/tsc.cmd --noEmit --project tsconfig.spec.json` passaram.
- `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -JornadaF4 -JornadaF5`, sem `-R2`: **104 unitários e 100 de integração** passaram em SQL Server exclusivo, sem falhas ou ignorados. A etapa de navegador da primeira execução parou antes da F4 porque o módulo Playwright não estava apontado no ambiente. A inspeção avulsa do protótipo em Chromium offline confirmou coluna, miniatura, seletor e ausência de requisições externas a 320 px.
