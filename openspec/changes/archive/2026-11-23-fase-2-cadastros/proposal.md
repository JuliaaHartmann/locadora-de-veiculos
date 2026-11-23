# Proposal

## Why

A fundação da F1 fornece acesso administrativo isolado por empresa, mas ainda não permite preparar os cadastros necessários à operação da locadora. A F2 entrega os cadastros da J-02 para que a frota, os preços e as pessoas possam ser geridos com validação, isolamento e preservação histórica antes da contratação da F3.

## What Changes

- Disponibilizar manutenção e consulta de grupos, modalidades de plano e dados de veículos, sem upload, leitura ou remoção de fotos nesta fase.
- Disponibilizar cadastros de clientes PF/PJ, vínculos PF–PJ e condutores, incluindo elegibilidade documental e coerência de CNH nos limites aplicáveis à F2.
- Disponibilizar taxas/serviços, preços dos dois seguros e preços de combustíveis versionados por vigência.
- Aplicar isolamento por empresa, exclusão lógica, unicidade entre registros ativos e bloqueios por vínculos conforme PRD/TDD.
- Entregar telas de listagem e formulários em português brasileiro, acessíveis por teclado e alinhados aos Signal Forms e à linguagem visual já existente.

## Capabilities

### New Capabilities

- `grupos-planos-veiculos`: gestão e consulta de grupos, planos e dados da frota, sem funcionalidades de foto.
- `pessoas`: gestão de clientes PF/PJ, seus vínculos e condutores.
- `precos-operacionais`: gestão de taxas/serviços e configuração de preços de seguros e combustíveis.

### Modified Capabilities

Nenhuma. As capabilities existentes cobrem acesso e isolamento da F1; esta mudança acrescenta comportamentos de cadastro sem alterar esses contratos.

## Impact

- **Requisitos:** RF-04 a RF-13, RNF-01, RNF-02, RNF-04, RNF-05 e RNF-07; jornada J-02; critérios CA-02 a CA-05 e CA-12 no recorte da F2. Restrições de aluguel aberto que dependem da F3 ficam especificadas como bloqueios aplicáveis quando houver aluguel.
- **Decisões técnicas:** DT-04, DT-07 a DT-10 e DT-13 a DT-15; organização modular Frota e preços, Pessoas e Transversal do TDD.
- **Roadmap:** F2 — Cadastros; depende da F1. Não conclui a v1.
- **Aplicação:** módulos correspondentes em `app/`, endpoints REST sob `/api/v1`, persistência SQL Server e interfaces Angular com Signal Forms. Não adicionar dependências ou serviços sem necessidade e autorização.
- **Limites:** sem locações, reservas, upload/gerenciamento de fotos, consulta externa de documentos, alteração de requisitos da F1 ou publicação.

### Recorte de comprovação e revisão

| Capability | F2 | Dependências de evidência posterior |
| --- | --- | --- |
| `grupos-planos-veiculos` | RF-04–07/19, CA-02/03: CRUD, vínculos grupo/plano/veículo, dados, unicidade ativa, exclusão lógica, filtro e agrupamento | F3: plano usado, veículo ocupado e preservação de preço contratado; F4: demonstrativo histórico; F5: fotos |
| `pessoas` | RF-08/09/19, CA-04: PF/PJ, documentos, vínculo PF–PJ, vínculo direto do condutor, coerência de CNH, exclusão lógica | F3: CNH até o retorno previsto, bloqueios com contrato aberto; F4: histórico contratual |
| `precos-operacionais` | RF-11–13/19, CA-05: taxas, seguros independentes, ausência versus zero, versões de combustível | F3: bloqueios de taxa e preços contratados; F4: combustível no retorno, fechamento e CA-12 |

RNF-01/02/04/05/07 aplicam-se no recorte da fase, sem declarar atendidos os cenários que exigem contratos ou fotos. Os limites são de evidência, não dispensa de requisitos futuros.

Os HTMLs são artefatos de revisão, não aplicação: sem autenticação, autorização, SQL, auditoria real ou persistência. O seletor de estado serve apenas à revisão dos cenários. Não criar aluguéis fictícios no produto nem retornar bloqueios constantes para substituir verificações de vínculo.

Revisão autorizada via `question` em 22/11/2026: completar CRUD e estados dos protótipos, alinhar vínculos/documentos/bloqueios e detalhar contratos e tarefas. Na mesma entrevista, o solicitante selecionou **“Validar SDD revisada”**, aprovando esta base para retomada posterior do Apply. Não é homologação da F2 nem evidência de implementação. Limitações informadas na validação: sem inspeção visual em navegador, sem código/testes da aplicação; HTMLs verificados por sintaxe e interações em JSDOM, e validação estrutural OpenSpec estrita aprovada.
