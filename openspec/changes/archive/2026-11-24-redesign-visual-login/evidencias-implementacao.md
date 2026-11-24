# Verificações da implementação do login

**Data:** 2026-11-24. **Branch:** `feat/redesign-visual`. **Escopo:** `/login` para desktop e notebook. Os resultados técnicos e a avaliação relatada pela usuária são registrados separadamente.

| Tarefa | Verificação executada | Resultado |
| --- | --- | --- |
| 1.1 — Recursos visuais | SHA-256 do PNG em `public/login/` e da cópia no build; carregamento local; comparação em navegador da geometria dos SVGs com o HTML aprovado. | Os dois PNGs coincidiram com o SHA-256 do design; a paisagem mantém os seis caminhos aprovados, sem faixas ou substitutos. Os ícones preservam a geometria e são decorativos. |
| 1.2 — Apresentação isolada | Comparação da tela Angular compilada com o HTML aprovado no Chromium 159.0.8154.6. A normalização da referência para a comparação retirou os elementos de avaliação apenas no navegador, sem alterar o arquivo. | Painéis, logotipo, paisagem, cartão, título, campos, botão e link coincidiram em medidas dentro de 1 px CSS nos tamanhos `1920×1080`, `1440×900`, `1366×768`, `1024×768` e `683×600`. Cores e estilos tipográficos comparados coincidiram. Capturas de `1440×900` e `683×600` inspecionadas. Nenhum controle de avaliação, novo campo ou fluxo entrou na SPA. |
| 1.3 — Regressão do componente | `npm test -- --watch=false`, em `app/frontend`. Comparação do corpo de `LoginComponent` com o Git antes da mudança. | **89 testes passaram** em 11 arquivos. Cenários adicionados ou reforçados cobrem validação, foco, contrato exato, bloqueio de reenvio, falha genérica, limpeza de senha, sessão em memória, aviso e navegação. O corpo da classe (incluindo `entrar()`) é idêntico ao anterior. |
| 2.1 — Paridade e acessibilidade | Comparação dos cenários da delta com o HTML e a SPA; Tab e Enter, associação de rótulos/erros, foco visível, espaçamento e exclusão dos ícones da ordem de foco; estado ocupado e resultados controlados no navegador. Zoom nativo com `chrome.tabs.setZoom(2)` no Chromium, confirmado pela leitura do fator. | A SPA preservou os estados existentes de vazio, aviso, espera, falha e sucesso. Nenhuma rolagem horizontal nos tamanhos verificados. Com janela de `1366×768` e `1024×768` a 200%, a largura útil foi `683` e `512` px CSS; ambos os painéis e a paisagem permaneceram presentes, com controles operáveis por teclado. |
| 2.1 — Contraste | Cálculo de pares de cores da implementação, incluindo composição dos fundos semitransparentes. | Texto principal `11,31:1`, botão `5,70:1`, espera `7,35:1`, erro `6,39:1`, aviso `8,20:1`, foco `9,06:1` e borda externa do campo `3,63:1`. São verificações dos pares selecionados, não auditoria completa de acessibilidade. |
| 2.2 — Integração e jornada F1 | `pwsh -NoProfile -File ./app/backend/scripts/Testar-Integracao.ps1 -Visual -FiltroIntegracao 'FullyQualifiedName~AutenticacaoTests'`; Playwright já disponível, executando `app/e2e/fase1.playwright.js` na SPA `localhost:4201` e API `localhost:5001` com SQL Server isolado e dados fictícios. | **40 testes de integração de autenticação passaram.** A jornada passou com registro, aviso, senha incorreta e correta, acesso ao registro via teclado, recarga sem sessão persistida, falha remota de logout e revogação após logout confirmado. O ambiente temporário foi encerrado. |
| 2.2 — Isolamento visual | Comparação de dois builds (antes/depois), CSS global e estilos computados em Registro e, com respostas controladas, Painel, Frota, Pessoas, Preços e Aluguéis. | CSS global compilado idêntico byte a byte; estilos comparados da navegação, títulos e controles de registro não mudaram. O layout exclusivo do login não aparece nas telas autenticadas. |
| 2.3 — Build e escopo | `npm run build` em `app/frontend`; `openspec validate redesign-visual-login --type change --strict`; revisão do diff e SHA-256 do HTML aprovado. | Build e validação passaram. Alterações restritas ao login, a seus testes e à change; o hash do protótipo permaneceu `9fa109ea5fcba8e164ccf95a05ea0093d9e7ac75b28adf4c4ed037e9380e2412`. |

## Revisão pontual — cor da estrada

- **Decisão autorizada:** substituir `#B96645` por `#BA7C52` somente nos dois contornos da estrada no protótipo e no template Angular. O tom novo é uma variação decorativa baseada no traço predominante do logotipo; o terracota oficial `#B96645` e o oliva oficial `#606B45` permanecem.
- **Escopo conferido:** os hashes dos dois HTMLs após a edição correspondem exatamente à substituição desses dois preenchimentos sobre as versões anteriores. Nenhuma geometria, gradiente, espaçamento ou outra cor mudou. O PNG servido mantém o hash do arquivo aprovado. O novo SHA-256 do protótipo consta no [design](design.md#prototype); o hash da tabela acima é histórico.
- **Paridade em execução:** Chromium 159.0.8154.6, janela `1440×900`, comparou os seis caminhos SVG do login e do protótipo. Os dois contornos em ambos exibiram `rgb(186, 124, 82)` com opacidade integral e sem filtro; o painel permaneceu `rgb(96, 107, 69)`.
- **Regressão:** `npm run build`, `npm test -- --watch=false` (**89 testes aprovados**) e `openspec validate redesign-visual-login --type change --strict` passaram. A jornada integrada com SQL Server, registrada acima, não foi repetida nesta revisão exclusivamente cromática.

## Revisão final — associação dos erros e aprovação do protótipo

- **Correção:** o template preserva a descrição individual de cada campo e inclui o resumo em `aria-describedby` somente quando o campo está inválido e o resumo tem conteúdo. Permanecem os rótulos, as mensagens, a região de alerta e o foco existentes.
- **Testes do frontend:** `npm test -- --watch=false` — **91 testes aprovados em 11 arquivos**. Verificados formulário intacto, envio vazio sem requisição, cada campo isoladamente inválido, remoção do vínculo ao corrigir o campo e falha de autenticação com a senha limpa. O resumo mantém o foco nos cenários de erro, e o campo válido mantém apenas a referência individual.
- **Build e especificação:** `npm run build`, `openspec validate redesign-visual-login --type change --strict` e `git diff --check` passaram.
- **Aprovação visual nesta rodada:** a usuária aprovou o protótipo HTML com os contornos em `#BA7C52`, sem alteração do arquivo. A confirmação posterior da implementação e o relato de acessibilidade constam na seção abaixo.
- **Backend:** a suíte completa não foi reexecutada nesta etapa e permanece inconclusiva, conforme o histórico abaixo.

## Validação final pela usuária

A usuária confirmou a **aprovação visual definitiva** do protótipo HTML e do login implementado no sistema, incluindo a harmonização dos contornos da estrada em `#BA7C52`.

A avaliação manual de acessibilidade foi **realizada pela usuária**, com o **Narrador do Windows**, no login implementado. Segundo seu relato, os seguintes cenários tiveram resultados satisfatórios:

- Navegação por teclado e identificação dos campos Usuário e Senha, do botão Entrar e do link Criar empresa.
- Leitura dos erros ao enviar o formulário com os campos vazios.
- Identificação do erro quando somente a senha está ausente.
- Anúncio da falha de autenticação.
- Autenticação com credenciais corretas.
- Acesso ao link Criar empresa pelo teclado.

**Não observados com o Narrador:** anúncio do estado de envio e aviso de retorno do cadastro. Navegador e versões não foram informados no relato. A avaliação não constitui certificação de acessibilidade.

## Diferenças e limites

- O HTML aprovado contém faixa, atalho, painel e avisos de simulação, ausentes na aplicação conforme o design. Na SPA, o envio e a navegação usam a API e as rotas existentes. Após uma falha, a SPA continua mostrando **Informe a senha.** no campo limpo além do resumo genérico: é o comportamento já existente de Signal Forms, que a simulação do protótipo não reproduz.
- A primeira tentativa do executor de backend sem filtro atingiu o limite de 240 s depois de **104 testes unitários aprovados**. A suíte de integração completa não tem resultado nessa tentativa. Os recursos daquele ensaio foram removidos, e a rodada isolada direcionada à autenticação e a jornada F1 concluíram com sucesso.
- A inspeção técnica anterior em Chromium não utilizou leitor de tela; ela não substitui nem amplia a avaliação manual relatada acima. Não foram executados ensaios para celular ou tablet, fora do escopo.
