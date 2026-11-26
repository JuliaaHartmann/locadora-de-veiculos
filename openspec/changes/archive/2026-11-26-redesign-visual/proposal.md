# Proposta — redesign visual

## Why

Unificar a identidade visual das sete telas do sistema, suas subtelas e os ajustes finais de identidade, preservando suas funcionalidades. O redesign é uma etapa intermediária não numerada entre as fases 5 e 6; não modifica o planejamento original nem substitui a homologação e a publicação da F6.

## What Changes

- Evoluir a apresentação para desktops e notebooks, preservando adaptação a dimensões de janelas e zoom, legibilidade, contraste, foco e navegação por teclado, sem criar variantes específicas para celular ou tablet.
- Preservar a paleta e os recursos aprovados: oliva principal `#606B45`, oliva secundário `#89916A`, bege `#E9E1D0`, creme `#F5F0E6`, terracota oficial `#B96645`, grafite `#30332B` e branco `#FFFFFF`; a estrada decorativa usa `#BA7C52`. **LocaCar** é o nome definitivo. Preservar o logotipo original aprovado sem alterações.
- **Login:** módulo já implementado e aprovado, sem novas alterações nesta reorganização. Seu requisito já sincronizado é baseline; decisões, protótipo, aprovações, testes e limitações estão em [historico/login](historico/login/).
- **Cadastro de empresa:** preservar a proposta original e a delta, e seguir o [protótipo visualmente aprovado em 24/11/2026](prototypes/cadastro-empresa.html). A composição `1.04fr / 1fr` mantém o painel de identidade na altura útil, independente do formulário; o cartão de quatro campos fica centralizado quando cabe e somente o painel direito rola quando necessário. Em largura útil reduzida, os painéis se empilham e a página rola naturalmente. A associação condicional dos campos inválidos ao resumo permanece prevista. A [aprovação e as verificações](evidencias.md#cadastro-de-empresa--protótipo-aprovado-visualmente) são do protótipo, não da implementação.
- **Início e navegação:** preservar o [protótipo aprovado](prototypes/painel.html), sem alterá-lo. A prévia de outras rotas ainda esconde Sair e não é referência para esse comportamento. A [delta de `account-access`](specs/account-access/spec.md) consolida a política aprovada: Sair no menu autenticado, proteção de alterações e operações, tentativa única em voo, coordenação com renovação e recuperação segura. A simulação não comprova revogação real. A [integração e suas verificações](paridade.md#pendências-antes-da-implementação-da-saída-compartilhada) continuam pendentes.
- **Frota:** [protótipo](prototypes/frota.html) com aprovação visual individual e resultado das correções técnicas aprovado, autorizado como referência para a futura implementação e para a estrutura operacional da revisão global. A seção de fotos fica **abaixo dos dados** no cadastro e na edição, com representação e controles lado a lado quando há espaço. O resultado geral da avaliação manual com o Narrador do Windows foi informado como satisfatório pela usuária. A [matriz de rastreabilidade](paridade.md#matriz-de-rastreabilidade-da-frota) e as [evidências de aprovação](evidencias.md#frota--aprovação-da-versão-corrigida) delimitam essa aprovação: não há certificação WCAG, comprovação de paridade integral, validação de integrações ou homologação.
- **Pessoas:** [protótipo](prototypes/pessoas.html) com aprovação visual individual e correções técnicas aprovadas, referência para a futura implementação. Mantém o nome Pessoas, abas Clientes e Condutores, editores e vínculos na mesma área e ações visíveis por registro. A usuária considerou satisfatório o teste manual com o Narrador do Windows. A [delta](specs/pessoas/spec.md), a [matriz de rastreabilidade](paridade.md#matriz-de-rastreabilidade-de-pessoas) e as [evidências de aprovação](evidencias.md#pessoas--aprovação-da-versão-corrigida) delimitam o escopo e os limites da avaliação.
- **Preços:** [protótipo](prototypes/precos.html) com aprovação visual individual e correções técnicas concluídas antes da revisão global. A [delta](specs/precos-operacionais/spec.md), a [matriz](paridade.md#matriz-de-rastreabilidade-de-preços) e as [evidências](evidencias.md#preços--aprovação-e-correções-técnicas) delimitam o que foi verificado. Preservam-se o painel compacto de Seguros, a composição de Combustíveis, a unidade de combustível e os planos por grupo em Frota. A saída compartilhada continua simulada.
- **Aluguéis:** [delta](specs/alugueis/spec.md) e [protótipo](prototypes/alugueis.html) para consulta, contratação, edição, devolução e histórico. Composição aprovada visualmente; revisão técnica independente sem achados altos e três correções técnicas verificadas antes da revisão global. O relato geral da usuária sobre o Narrador, os testes e seus limites estão nas [evidências](evidencias.md#aluguéis--aprovação-visual-e-revisão-técnica). A confirmação “Ocultar aluguel” identifica o contrato e esclarece que o histórico permanece. Integração real e [saída compartilhada](paridade.md#pendências-antes-da-implementação-da-saída-compartilhada) seguem pendentes.
- **Favicon aprovado:** letra **L** maiúscula centralizada, em Georgia, negrito e itálico, na cor creme `#F5F0E6`, sobre fundo oliva `#606B45` quadrado com cantos arredondados. Sem símbolo do carro, traço inferior separado, sombras, brilho ou gradientes. Verificar a legibilidade em `16×16` e `32×32 px`. Preservar regras de negócio, contratos, API e dados; a saída compartilhada e sua política de proteção são a ampliação funcional expressamente autorizada.
- **Revisão visual global aprovada:** a usuária aprovou os sete protótipos e os últimos ajustes de Preços e Aluguéis. Frota foi a referência para a estrutura operacional e os diálogos de confirmação. Em Preços, Seguros e Combustíveis usam o creme `#F5F0E6` dos cartões de detalhes de Aluguéis, distinto do painel e da página. Aluguéis preserva a ação “Ocultar aluguel” e apresenta o aviso de preservação do histórico em parágrafo separado. Os [hashes finais e limites da aprovação](evidencias.md#aprovação-da-revisão-visual-global) não substituem validações da aplicação. A consolidação da política de saída compartilhada foi aprovada; sua integração continua pendente.
- Usar uma única branch e change; obter aprovação visual individual antes de implementar cada módulo, verificar e registrar resultados por módulo e realizar a validação integrada antes do fechamento. Seguir o schema vigente: especificar e prototipar todos os fluxos afetados antes de finalizar design e tarefas globais.

**Ordem aprovada:** 1. Login; 2. Cadastro de Empresa; 3. Painel; 4. Frota e subtelas; 5. Pessoas e subtelas; 6. Preços e subtelas; 7. Aluguéis e subtelas.

**Ajuste transversal aprovado durante a etapa 7:** códigos permanentes nos oito tipos identificados em [RF-20/DT-23](proposta-identificadores.md), incluindo o histórico de combustível. É uma exceção expressa à ausência de migration/API nesta proposta original; os UUIDs internos, a sequência de etapas e os protótipos permanecem intactos.

## Decisões aprovadas para a revisão global

As decisões aplicáveis orientam os protótipos. A revisão visual global está aprovada; a implementação ainda não foi autorizada nesta consolidação.

**Refinamentos visuais aprovados:** nas decisões 3, 5 e 6, Editar e Excluir seguem o padrão visual de Frota (`#374526` e `#945B43`, respectivamente), com espaçamento uniforme, hover restrito à borda e cabeçalho e conjunto de ações no mesmo eixo central. Na 9, o seletor de Grupo e o agrupamento ficam juntos quando há espaço, com rótulo de agrupamento mais discreto. Na 10, o painel de Seguros é mais estreito, com cartões iguais e alinhados à esquerda; Combustíveis ocupa a largura operacional e usa título, rótulo e seletor compacto de histórico na mesma linha quando há espaço. Na 11, o tipo aparece por extenso abaixo do nome, sem alterar documentos ou telefone. Na 12, os títulos dos diálogos são específicos por entidade e não têm lixeira; pergunta e preservação do histórico ficam separadas. Cancelar e Excluir têm bordas e hover uniformes; Excluir usa `#945B43` com texto branco. Foco, cancelamento e bloqueios foram preservados.

1. Mensagem de sucesso em caixa verde, com ícone, título, identificação do registro cadastrado, atualizado ou excluído, fechamento manual e anúncio acessível.
2. Dois registros fictícios por listagem; em Clientes, um PF e um PJ. Registros extras somente para testes temporários.
3. Editar e Excluir usam, respectivamente, lápis verde-oliva escuro e lixeira terracota, com bordas finas, dimensões iguais, tooltip no hover e no foco e nomes acessíveis.
4. Botões secundários de 34–36 px, com texto de 14 px quando aplicável. Cadastrar, Salvar e Cancelar permanecem com 44 px.
5. Nas ações operacionais padronizadas, o hover altera somente a cor da borda, sem deslocar o controle ou alterar fundo e texto. No menu da aplicação, Sair segue a [definição definitiva](design.md#protótipos-e-sequência-incremental): altura de 36 px, texto de 14 px, ícone de 16 px e raio de 8 px. Os protótipos não mudam.
6. Cabeçalho e botões da coluna Ações centralizados. Em Clientes PF: Vínculos PJ, Usar como condutor, Editar e Excluir, preferencialmente na mesma linha, com quebra centralizada quando necessária.
7. Paginação compacta: botões de 32–34 px, texto de 13–14 px e menor espaçamento.
8. Tabelas compactas: cabeçalhos de aproximadamente 36 px e linhas simples de 48–52 px, com crescimento natural e sem cortes.
9. Filtros compactos, próximos à tabela e alinhados horizontalmente quando houver espaço. Em Frota, rótulo Grupo ao lado do seletor e checkbox Agrupar por grupo adjacente.
10. Cartões de Seguros e Combustíveis compactos, com fundo creme, largura contida, preços de 20 px, títulos de 16 px, informações secundárias de 13–14 px e ausência de preço em texto discreto. Ações alinhadas e seletor de histórico junto ao título quando possível.
11. Em Clientes, nome na primeira linha e tipo abaixo, telefone sem quebra desnecessária, documentos em linhas separadas e CPF/CNPJ com pontuação convencional. Estender a formatação aos módulos pertinentes, sem alterar armazenamento ou validação.
12. Confirmar exclusão em diálogo sem lixeira, com título específico, pergunta que identifica o registro e aviso de preservação do histórico em texto separado. Oferecer Cancelar e Excluir conforme o refinamento aprovado, com foco inicial em Cancelar, suporte a Esc e retorno de foco ao cancelar. Após sucesso, exibir a mensagem verde personalizada. Preservar a exclusão lógica e os bloqueios existentes. A lixeira permanece na ação da tabela.

A confirmação aprovada **substitui a exclusão imediata**; o refinamento posterior **substitui o ícone no diálogo**. As deltas de [Frota](specs/grupos-planos-veiculos/spec.md), [Pessoas](specs/pessoas/spec.md) e [Preços](specs/precos-operacionais/spec.md) registram a apresentação vigente sem alterar regras ou bloqueios. Aluguéis mantém seu diálogo próprio de ocultação, conforme a [delta do módulo](specs/alugueis/spec.md).

## Capabilities

### New Capabilities

- `codigos-apresentacao`: numeração permanente por empresa e tipo para os oito recursos aprovados (RF-20/DT-23).

### Modified Capabilities

- `registro-empresa`: apresentação acessível do cadastro em desktop, com preservação dos requisitos funcionais existentes.
- `account-access`: apresentação acessível do Painel e política de saída compartilhada consolidada para revisão documental; preserva o requisito principal do login e o contrato de revogação existente.
- `grupos-planos-veiculos`: apresentação acessível de consultas e editores da Frota.
- `fotos-veiculos`: apresentação acessível dos estados e operações de foto.
- `pessoas`: apresentação acessível das consultas e editores de clientes, condutores e vínculos PJ.
- `precos-operacionais`: apresentação acessível das configurações de taxas e serviços, seguros e combustíveis.
- `alugueis`: apresentação acessível dos fluxos existentes e confirmação de ocultação lógica; sem mudanças em cálculo, contratos ou dados.

`account-access` já contém a apresentação aprovada do login e não recebe uma segunda adição do mesmo requisito.

## Impact

- O PRD (RF-02, J-01/J-03/J-04 e CA-01/06/09/11) e o TDD (DT-22, complementar a DT-05/06/10/14) registram as decisões aprovadas da saída compartilhada. Cinco fases anteriores e planejamento da F6 permanecem inalterados. Rastreabilidade adicional da Frota: RF-03–07/19, J-02, RNF-01/02/04/05/07, CA-01/02/03/10/11 e DT-01/02/06/07/10/11/12/14/21.
- A implementação futura contempla a saída compartilhada expressamente autorizada, além do redesign visual; não altera contratos de frota ou fotos. A estrutura compartilhada e Início antecedem a Frota. Esta etapa não modifica a aplicação.
- O commit `3d5ef7498c2abf9c06271b53877ccc1b3b8b9ae4` registra o login concluído. Seu histórico original e a delta já sincronizada são preservados fora de `specs/` ativo; não devem ser reaplicados.
- Protótipos são HTML autocontido com dados fictícios e simulações locais. Aprovação visual de um protótipo não é teste da aplicação ou aprovação dos demais módulos. Sync, archive e PR são posteriores e únicos para a change geral, sujeitos às validações e autorizações aplicáveis.
