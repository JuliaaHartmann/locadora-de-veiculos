# Proposta — F5: fotos dos veículos

## Why

A Frota já permite administrar veículos, mas ainda não oferece a foto opcional prevista em RF-06. A F5 entrega esse recurso obrigatório da v1 sem tornar a imagem obrigatória no cadastro ou condição para alugar.

## What Changes

- Na jornada J-02, permitir selecionar uma foto opcional no formulário inicial. Salvar o veículo primeiro, pelo contrato atual sem arquivo; somente após sucesso, enviar a foto separadamente com o ID e o ETag retornados. Sem foto, o cadastro segue normalmente; se o cadastro falhar, não há envio. Se só a foto falhar, informar o sucesso parcial e permitir repetir apenas o envio, sem cadastrar outro veículo.
- Na edição de um veículo cadastrado, manter consulta, adição, substituição e remoção da sua única foto ativa.
- Na listagem de Veículos, incluir a coluna **Foto** com a miniatura da imagem ativa ou um indicador acessível com ícone de veículo; manter disponíveis a listagem e suas ações enquanto a foto carrega ou está indisponível.
- Restringir operações à sessão da empresa. Bloquear alterações de foto com aluguel aberto, tratar versões desatualizadas e manter a foto anterior quando uma substituição falhar.
- Aceitar somente JPEG e PNG estáticos dentro dos limites do TDD; servir apenas a imagem saneada pela API privada. A indisponibilidade da foto não impede consulta cadastral ou locação que não dependa da imagem.
- Fora do escopo: galeria, múltiplas fotos, URL externa ou pública, novos perfis, mudanças de cobrança, fotos em contratos, expurgo de fotos retidas sem política aprovada e homologação/publicação F6.

## Capabilities

### New Capabilities

- `fotos-veiculos`: leitura e ciclo da foto privada, validação, concorrência, preservação e recuperação de falhas.

### Modified Capabilities

- `grupos-planos-veiculos`: substituir a limitação da F2 pela integração da foto ao fluxo de Frota, preservando cadastro sem arquivo, unicidade, bloqueios e demais cenários.

## Impact

- [PRD](../../../../docs/prd.md): RF-03, RF-06/07, RF-19 no recorte de histórico, RNF-01/02/04/05/07, J-02, CA-01/03/11. Sessão e isolamento genéricos já especificados em `account-access` e `tenant-isolation`; locações permanecem inalteradas.
- [TDD](../../../../docs/tdd.md): F5-01 a F5-06, §5.5, DT-01/02/04–08/10–15/17/21. F2 é pré-requisito; F3/F4 já integradas são base de regressão. A implementação abrange Frota/SPA e o módulo Fotos no monólito. A prova da F5 inclui SQL Server de teste, volume privado, compatibilidade nativa e limites em ambiente local, além do contrato em bucket R2 isolado com autorização separada.
- Refinamentos confirmados no harness: a seleção de foto no cadastro inicial substitui a decisão anterior de só disponibilizar o seletor na edição; a gravação do veículo continua anterior ao envio. Para F5-06 prevalece o §5.5 (a falha da foto não bloqueia a locação); o ajuste editorial da linha do roadmap fica separado. Ver o [registro de aprovação](tasks.md).
- Aprovado em 24/11/2026: somente a validação específica no App Service Linux F1 e no ingresso HTTPS reais passa ao critério obrigatório [F6-06](../../../../docs/tdd.md#qualificação-de-fotos-no-app-service-linux-f6-06), antes da homologação e da publicação definitiva. A implementação, os requisitos e as evidências locais e do R2 permanecem na F5. A modalidade de implantação será decidida na F6.
