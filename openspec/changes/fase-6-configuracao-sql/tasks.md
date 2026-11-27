# Tarefas

## 1. Configuração inicial (F6-03 e F6-04)

- [x] 1.1 Versionar o SQL idempotente de grants em `app/backend/`; conferir tabelas e operações com o modelo e o código e manter o histórico EF e os privilégios administrativos de fora.
- [x] 1.2 Preparar o diagnóstico e a configuração do usuário contido em `app/backend/`, reutilizando a infraestrutura e as migrations reais; compilar localmente e verificar a validação de entradas e a sanitização de falhas com valores fictícios, sem acesso à produção.
- [x] 1.3 Criar o workflow manual do backend com os dois modos; validar localmente o YAML, a guarda da `main`, o isolamento de secrets e a ausência de gatilhos e etapas de publicação. Registrar os resultados e as limitações da validação local.
- [x] 1.4 Bloquear o modo de configuração sem aprovação explícita e enviar a senha como parâmetro somente se o principal ainda não existir. Verificar o bloqueio local com entradas fictícias e conferir o isolamento dos secrets no YAML.
- [x] 1.5 Usar a mesma matriz para aplicar e validar permissões efetivas antes do commit, rejeitando excessos. Verificar localmente a matriz e a ordem transacional e documentar o limite do rollback após a conexão independente.
- [x] 1.6 Remover a dependência de GitHub Environments e conferir no YAML a guarda da `main`, os Repository Secrets por modo e o bloqueio de configuração; registrar o limite dessa guarda.

## Evidências locais

- Restore em modo bloqueado a partir do cache local e build Release da ferramenta: sucesso, sem avisos nem erros. Foram conferidas as versões resolvidas de EF Core e SqlClient e as cópias dos scripts na saída.
- Quatro cenários com entradas fictícias (modo inválido, secret ausente, string inválida e destino inesperado) falharam sem conexão e sem ecoar o valor fictício. A inspeção do snapshot EF conferiu os 22 grants, o `DELETE` restrito a `VinculosPfPj` e o `INSERT` exclusivo de `Auditorias`.
- Verificação estrutural do YAML: somente disparo manual, guarda da `main`, modos exclusivos, secrets por passo e ausência de publicação. A branch do frontend permanece independente.
- As verificações locais não executam o SQL. Antes de usar `configurar-banco`, verificar se a auditoria ou o tracing de Azure SQL não gravam valores de credenciais do lote de criação. Conectividade no runner e privilégios efetivos continuam pendentes.
- Na revisão de segurança, o build Release direcionado passou sem avisos ou erros. Cinco casos fictícios verificaram que o bloqueio é o padrão, que apenas `true` o libera e que ele não afeta o diagnóstico. A análise do YAML confirmou o bloqueio antes da etapa com secrets; a análise estática conferiu as 22 tabelas, o envio da senha apenas na criação e a ordem da checagem transacional, commit e autenticação independente. O comportamento de `EXECUTE AS` e a redação pela auditoria não foram ensaiados nesta revisão local.
- Na revisão de escopo, a inspeção estrutural do YAML confirmou a retirada do ambiente, a guarda da `main` e os secrets por modo. O passo Bash de bloqueio foi verificado localmente: valor ausente e `false` falham, `true` libera. Nenhum secret foi usado nesses testes.

## 2. Publicação definitiva (DT-25)

- [x] 2.1 Substituir os modos manuais pelo fluxo automático na `main`; verificar a ordem, o uso de OIDC e a separação dos secrets no YAML.
- [x] 2.2 Reusar a ferramenta SQL com reconexão limitada para `40613` e configurar App Settings em memória; compilar a ferramenta e verificar localmente a sintaxe e o mapeamento das configurações.

**Evidências:** build Release da ferramenta e publish local da API passaram. A análise do YAML confirmou a sequência, OIDC e a separação das credenciais. O PowerShell foi validado com chamadas ARM simuladas e valores fictícios, incluindo a preservação de uma configuração existente. A reconexão e o deploy não foram executados na Azure.

## Workflow follow-up

- Os modos manuais e o bloqueio por aprovação da seção 1 são evidências históricas, substituídas pela seção 2. A execução na Azure requer autorização para integrar na `main`.
