# Proposta — redesign visual

## Why

Unificar a identidade visual das sete telas do sistema, suas subtelas e os ajustes finais de identidade, preservando suas funcionalidades. O redesign é uma etapa intermediária não numerada entre as fases 5 e 6; não modifica o planejamento original nem substitui a homologação e a publicação da F6.

## What Changes

- Evoluir a apresentação para desktops e notebooks, preservando adaptação a dimensões de janelas e zoom, legibilidade, contraste, foco e navegação por teclado, sem criar variantes específicas para celular ou tablet.
- Preservar a paleta e os recursos aprovados: oliva principal `#606B45`, oliva secundário `#89916A`, bege `#E9E1D0`, creme `#F5F0E6`, terracota oficial `#B96645`, grafite `#30332B` e branco `#FFFFFF`; a estrada decorativa usa `#BA7C52`. Não redesenhar o logotipo ou o símbolo. LocaCar permanece provisório.
- **Login:** módulo já implementado e aprovado, sem novas alterações nesta reorganização. Seu requisito já sincronizado é baseline; decisões, protótipo, aprovações, testes e limitações estão em [historico/login](historico/login/).
- **Cadastro de empresa:** preservar a proposta, a delta e o protótipo existentes. A direção usa a proporção de painéis `1.04fr / 1fr`, a identidade do login e um cartão para quatro campos em coluna; inclui a associação condicional de campos inválidos ao resumo. A aprovação visual do protótipo permanece pendente.
- **Demais módulos e subtelas:** inventariar, especificar e prototipar conforme os fluxos existentes e as aprovações aplicáveis, sem inventar requisitos nem presumir suas decisões visuais.
- Ajustes globais, inclusive favicon e nome definitivo, ficam para o final e dependem das autorizações aplicáveis. Não alterar regras de negócio, contratos, API, fluxos ou dados como efeito do redesign.
- Usar uma única branch e change; obter aprovação visual individual antes de implementar cada módulo, verificar e registrar resultados por módulo e realizar a validação integrada antes do fechamento. Seguir o schema vigente: especificar e prototipar todos os fluxos afetados antes de finalizar design e tarefas globais.

## Capabilities

### New Capabilities

Nenhuma identificada nesta etapa.

### Modified Capabilities

- `registro-empresa`: apresentação acessível do cadastro em desktop, com preservação dos requisitos funcionais existentes.

As demais capacidades afetadas serão identificadas durante a análise de cada fluxo e acrescentadas à proposta e às deltas pertinentes antes dos respectivos protótipos; não se presume que todas terão requisitos alterados. `account-access` já contém a apresentação aprovada do login e não recebe uma segunda adição do mesmo requisito.

## Impact

- PRD, TDD, cinco fases anteriores e planejamento da F6 permanecem inalterados. Rastreabilidade do login: RF-02, J-01, RNF-04, CA-01 e CA-11; do cadastro: RF-01, J-01, RNF-01, RNF-02, RNF-04, CA-01 e CA-11; decisões aplicáveis no TDD incluem DT-01, DT-02, DT-05, DT-06, DT-14, DT-19 e DT-21.
- A implementação futura é visual, respeita as autorizações de estilos globais e não cria novas funcionalidades. Esta reorganização não modifica a aplicação.
- O commit `3d5ef7498c2abf9c06271b53877ccc1b3b8b9ae4` registra o login concluído. Seu histórico original e a delta já sincronizada são preservados fora de `specs/` ativo; não devem ser reaplicados.
- Protótipos são HTML autocontido com dados fictícios e simulações locais. Aprovação visual de um protótipo não é teste da aplicação ou aprovação dos demais módulos. Sync, archive e PR são posteriores e únicos para a change geral, sujeitos às validações e autorizações aplicáveis.
