# Proposta — redesign visual do login

## Why

Aplicar a identidade visual aprovada à tela de entrada do sistema em desktops e notebooks. Começar pelo login permite avaliar a linguagem visual sem afetar outras telas ou fluxos.

## What Changes

- Preservar a composição de `/login` com painel de identidade e formulário lado a lado em larguras usuais de desktop e notebook. Quando a janela ou o zoom reduzir a largura útil, reorganizar os dois painéis completos sem ocultar a paisagem e sem comprometer legibilidade, foco, acesso por teclado ou leitura dos estados do formulário.
- Usar `logo-transparente.png` sem alterações, sobre o verde oliva oficial `#606B45`. `simbolo-transparente.png` permanece como referência auxiliar, sem substituir o logotipo completo.
- Manter a estrada e a paisagem decorativas, sem faixas de sinalização nem substitutos. Os dois contornos da estrada usam `#BA7C52` como variação decorativa; `#B96645` permanece o terracota oficial e `#606B45`, o verde oliva oficial.
- Reduzir levemente o título Entrar e adicionar ícones decorativos de usuário e cadeado à esquerda dos respectivos campos, sem substituir os rótulos visíveis.
- Preservar os campos Usuário e Senha, o botão Entrar, o link Criar empresa, os avisos, as mensagens, a validação, o foco e o bloqueio de novo envio durante a requisição. Não acrescentar controles, como revelar senha ou recuperar acesso.
- Restringir esta change à apresentação do login. Registro, painel, demais módulos, rotas, API, sessão, regras de negócio e funcionalidades não mudam. “LocaCar” permanece provisório e não será propagado como nome do sistema.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `account-access`: acrescentar o contrato observável de apresentação responsiva da tela de login e os cenários de preservação necessários para avaliá-la. Os requisitos existentes de autenticação, sessão e limites não são modificados.

## Impact

- A implementação está restrita à apresentação de `/login` e às verificações pertinentes no frontend.
- Diretriz para as sete telas e suas subtelas nas changes posteriores: escopo visual exclusivo de desktops e notebooks, sem variantes, protótipos, tarefas ou testes específicos para celular ou tablet. Preservar a adaptação a resoluções, janelas e zoom no desktop e manter os requisitos de acessibilidade, incluindo contraste, legibilidade, foco e navegação por teclado. Nenhuma outra tela é alterada nesta change.
- Rastreabilidade: PRD **RF-02**, **J-01**, **RNF-04**, **CA-01** e **CA-11**; TDD **DT-01**, **DT-02**, **DT-05**, **DT-06**, **DT-14**, **DT-19** e **DT-21**. A navegação preserva o cenário **Navegação entre acesso e registro** de `registro-empresa`.
- O protótipo não autentica nem se conecta à API. Design e tarefas estão concluídos. A usuária aprovou visualmente o protótipo e o login implementado, incluindo os contornos em `#BA7C52`. A avaliação manual da usuária com o Narrador do Windows e seus limites constam nas [evidências da implementação](evidencias-implementacao.md#validação-final-pela-usuária).
