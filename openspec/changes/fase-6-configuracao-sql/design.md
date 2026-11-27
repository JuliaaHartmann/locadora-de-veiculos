# Design — publicação do backend na Azure

## Contexto

O runner já conectou ao Azure SQL; a primeira tentativa retornou `40613` e a segunda passou, conforme relato. A decisão está em [DT-25](../../../docs/tdd.md#dt-25--credenciais-e-configuração-inicial-do-azure-sql-f6).

## Decisões

1. Um único job serializado em pushes na `main` protegida. Gerar o pacote dependente de framework com `app/backend/global.json` e compilar a ferramenta SQL existente. Só avançar se o passo anterior passar.
2. Usar `SQLSERVER_CONNECTIONSTRING` para migrations e grants. Tentar até duas reconexões, após 15 s e 30 s, apenas na abertura e somente para `40613`. Criar `locacar_api` se ausente com a senha do secret de runtime, aplicar e validar a matriz na mesma transação e confirmar autenticação independente após o commit.
3. Autenticar com OIDC federado à `main`. Ler e mesclar App Settings pelo ARM em memória, sem emitir valores ou respostas no log. Usar apenas a conexão de runtime no App Service, além das configurações JWT, R2 e da origem HTTPS pública da SPA. Publicar o pacote em `locacar-api` após configurar o app.

## Riscos e pendências

- A auditoria do servidor e do banco foi reportada como desabilitada. O literal exigido na primeira criação pode ser registrado por tracing do SQL. A decisão aceita o risco residual sem verificação manual adicional, sem desativar mecanismos e sem criar monitoramento.
- A guarda da `main` limita este workflow; outros workflows elegíveis podem acessar os Repository Secrets. A atualização de App Settings pode reiniciar a aplicação existente antes do deploy.
- Falha pré-commit da configuração reverte a criação e os grants; falhas posteriores não revertem migrations, grants ou App Settings já aplicados. Conferir compatibilidade de schema antes de integrar mudanças futuras.
- Cadastrar o secret de runtime, informar `LOCACAR_FRONTEND_ORIGIN` e confirmar o runtime .NET 10 do App Service. O merge na `main` aciona o deploy e exige autorização separada; a branch do frontend permanece independente.
