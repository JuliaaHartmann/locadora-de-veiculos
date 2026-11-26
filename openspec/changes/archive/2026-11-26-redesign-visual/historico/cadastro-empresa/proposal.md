# Proposta — redesign visual do cadastro de empresa

## Why

Aplicar à tela pública de cadastro a identidade visual já aprovada no login, preservando a continuidade entre as duas telas. A apresentação precisa acomodar quatro campos e seus estados sem alterar o registro de empresa.

## What Changes

- Apresentar `/registro` com a mesma proporção de painéis `1.04fr / 1fr` do login em larguras adequadas, reproduzindo o painel oliva, o logotipo intacto e a paisagem aprovada, sem faixas e com contornos da estrada em `#BA7C52`. Manter a paisagem dentro do painel.
- Preservar fundo bege, cartão creme e linguagem visual do login, adaptando o cartão aos quatro campos em coluna e a mensagens que podem crescer. Reorganizar os painéis completos quando a janela ou o zoom exigir, sem layout específico para celular ou tablet.
- Preservar textos, campos, validações, respostas, navegação e estado de envio existentes. Associar condicionalmente os campos inválidos ao resumo de erro quando houver mensagem, mantendo a associação individual e o foco, conforme o padrão validado no login.
- Manter teclado, contraste, foco, leitura por tecnologia assistiva e uso com zoom de 200% como critérios de verificação; nenhum novo controle ou fluxo de produto.
- O nome LocaCar permanece provisório. Não modificar login, estilos globais, favicon, identidade aprovada, API, regras de negócio, PRD, TDD ou outras telas.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `registro-empresa`: acrescentar contrato observável para apresentação desktop e acessibilidade do registro, preservando os requisitos e cenários funcionais já existentes.

## Impact

- A implementação futura será limitada à apresentação do cadastro e às verificações correspondentes; esta etapa produz apenas proposta, delta e protótipo para avaliação.
- O protótipo não acessa API, rede ou armazenamento; navegação e respostas são simuladas. Design, tarefas e implementação aguardam aprovação visual.
- Rastreabilidade: PRD **RF-01**, **J-01**, **RNF-01**, **RNF-02**, **RNF-04**, **CA-01** e **CA-11**; TDD **DT-01**, **DT-02**, **DT-05**, **DT-06**, **DT-14**, **DT-19** e **DT-21**, incluindo seção 5.4.
