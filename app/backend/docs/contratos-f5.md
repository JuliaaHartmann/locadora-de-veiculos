# Contratos HTTP e operação local — F5

O cadastro continua usando `POST /api/v1/veiculos` com JSON cadastral, sem arquivo. Se houver foto selecionada, a SPA aguarda `201` e usa o ID do corpo e o `ETag` recebido. Se a criação falhar, não chama o upload. Se apenas o upload falhar, o cadastro permanece e repetir a foto não repete o `POST`.

## `/api/v1/veiculos/{id}/foto`

Todos os métodos exigem sessão administrativa; empresa e ator são resolvidos no servidor. Não há consulta por chave de objeto, nome de arquivo, placa ou URL de armazenamento.

| Método | Requisição | Sucesso |
| --- | --- | --- |
| `GET` | Sem corpo | `200` com a foto ativa saneada, MIME real, `Cache-Control: private, no-store` e `X-Content-Type-Options: nosniff`; sem redirecionamento. |
| `PUT` | `If-Match` forte do veículo; multipart com um arquivo no campo `arquivo` | `204` e novo `ETag` do veículo. |
| `DELETE` | `If-Match` forte do veículo | `204` e novo `ETag`; a remoção da referência é lógica. |

`401` sem sessão válida; `404` se o recurso não é consultável na empresa da sessão ou não há referência ativa para leitura/remoção; `409` para alteração com aluguel aberto, `428` sem precondição e `412` para versão divergente. O corpo multipart completo é limitado a 5 MiB, com ou sem `Content-Length` (`413`). Tipos divergentes ou não aceitos produzem `415`; formulário ou imagem estática inválida pode produzir `400` associado a `arquivo`. Imagem esperada não disponível e falhas de processamento/armazenamento retornam `503`. `foto_nao_gravada` identifica falha confirmada antes da ativação; se a resposta for incerta, consulte a foto atual antes de nova tentativa. A aplicação não usa retry automático do cadastro.

O servidor verifica a assinatura, dimensões positivas e limite de 16.000.000 de pixels, recusa animação e truncamento e reencoda JPEG/PNG sem metadados de origem. A saída tem limite de 65 MiB; o buffer de pixels RGBA tem até 64.000.000 bytes. Há uma vaga de envio/saneamento por processo, sem fila; a leitura do upload e o saneamento compartilham uma janela de 15 s. Gravação e confirmação compartilham outra janela de 15 s; a remoção compensatória e a abertura inicial do fluxo de leitura têm janelas próprias de 15 s. Depois de aberto, o corpo do `GET` é transmitido até terminar ou a requisição ser cancelada. O codec nativo não é preemptivo: o tempo é verificado antes/depois das etapas e na escrita de saída; se o decode ultrapassar a janela, a foto não é ativada. Esses limites não definem SLA nem representam limite total de memória do processo; a análise no runtime-alvo ainda é necessária.

## Consistência e recuperação

O banco guarda estado, referência, MIME, tamanho e hash, mas não os bytes. O upload reserva um registro pendente em transação curta, grava e confirma o SHA-256 fora do lock SQL (uma repetição de armazenamento dentro do mesmo prazo e com a mesma chave) e revalida versão, empresa e ocupação em nova transação. A foto ativa, o `rowversion` e a auditoria são atualizados no mesmo commit. A foto anterior permanece ativa quando a nova não é confirmada antes do commit, salvo se uma operação concorrente a tiver removido ou trocado.

A próxima mutação de foto da empresa pode compensar uma pendência expirada. Há reserva de dois minutos, remoção idempotente fora do SQL, novas tentativas duráveis (até três falhas) e registro final consultável no SQL. Não há worker contínuo. Pendências nunca são servidas. Fotos que chegaram a ser ativas tornam-se retidas após troca/remoção; sem política aprovada, não são expurgadas. A exclusão lógica de veículo não expõe sua imagem e o reuso de placa não transfere referências.

A migration `Fase5FotosVeiculos` é aditiva; sem preenchimento retroativo de foto. `Down` remove a tabela de referências e não remove objetos. Não aplicar `Down` em base operacional com fotos ou pendências sem parar mutações, preservar referências e objetos e verificar plano de restauração. Nada nesta etapa valida recuperação operacional ou define retenção.

## Desenvolvimento e prova local

Em `Development` e `Testing`, o provedor padrão é `Local`. `Fotos__PastaPrivada` aceita caminho absoluto dedicado, fora da aplicação e das raízes públicas. Em desenvolvimento, o padrão é a pasta de dados do usuário, `Locadora/fotos-privadas`. Restrinja o acesso à identidade do processo; não use uma pasta servida pelo SPA/API ou um vínculo com raiz pública. Não versionar arquivos. O script `Testar-Integracao.ps1 -JornadaF5` força o adaptador local com pasta de teste exclusiva e SQL Server isolado. Fora de desenvolvimento/testes o armazenamento fica desativado se nenhum provedor for explicitamente habilitado; cadastro e locações não dependem da foto.

## R2: configuração e ensaio autorizado

No ambiente local, o adaptador S3 é selecionado só quando configurado como `R2`. O ensaio isolado confirmou assinatura na região `auto`, operações e privacidade no bucket de desenvolvimento; veja as [evidências](../../../openspec/changes/fase-5-fotos-veiculos/evidencias.md#ensaio-real-no-r2--24112026). Para configuração externa por ambiente, as chaves correspondentes são `Fotos__Provedor=R2`, `Fotos__R2__Endpoint` (HTTPS do R2), `Fotos__R2__Bucket`, `Fotos__R2__AccessKeyId` e `Fotos__R2__SecretAccessKey`. Não incluir valores reais em arquivos versionados nem no navegador. Trocar provedor não migra os objetos locais.

Para executar o roteiro explicitamente autorizado, configure a seção `Fotos` nos User Secrets de `src/Locadora.Api/Locadora.Api.csproj` (`Fotos:Provedor`, `Fotos:R2:Endpoint`, `Fotos:R2:Bucket`, `Fotos:R2:AccessKeyId`, `Fotos:R2:SecretAccessKey`). Da raiz, execute `pwsh -NoProfile -File app/backend/scripts/Testar-Integracao.ps1 -R2`. Esse modo usa `Development` para ler os User Secrets, mas usa conexão e chaves de sessão sintéticas, SQL Server em loopback exclusivo e somente o bucket `locadora-veiculos-dev`. Sem `-R2`, não há ensaio remoto. O `DELETE` HTTP é lógico; a remoção física do ensaio ocorre só para um órfão sintético da própria execução. Objetos sintéticos retidos não são expurgados automaticamente. Não execute em bucket operacional nem com credencial ampla.

Roteiro de verificação real:

1. Confirmar compatibilidade e assinatura do SDK com o endpoint isolado e política de credencial limitada.
2. Gravar um JPEG/PNG sintético por chave própria, conferir tamanho/hash, ler pelo proxy, MIME e cabeçalhos; negar leitura pública e entre empresas.
3. Simular falhas, confirmar preservação da referência anterior e testar remoção/retomada idempotentes só em objetos sintéticos órfãos desse ensaio, sem expurgar fotos ativas ou retidas.
4. Registrar plataformas, versões, comandos e resultados sem segredos. A aprovação em volume local não substitui esta prova.
