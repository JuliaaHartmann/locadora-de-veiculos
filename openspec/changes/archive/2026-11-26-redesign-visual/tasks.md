# Tarefas — redesign visual

Seguir o [design aprovado](design.md), as deltas e a [matriz de paridade](paridade.md). Os sete [protótipos e SHA-256 aprovados](evidencias.md#aprovação-da-revisão-visual-global) são referências imutáveis; não repetir testes dos protótipos.

**Ponto de aprovação por módulo:** conferir paridade nos dois sentidos (cenários aprovados cobertos e mudanças observáveis rastreáveis), contraste, teclado, foco, estados acessíveis, janela reduzida e zoom nativo de 200% conforme o fluxo. Validar manualmente com o Narrador do Windows **na aplicação**, com participação da usuária em cada módulo. Registrar em [evidências](evidencias.md) testes, resultados, limitações, divergências e aprovação explícita pelo sistema de questões antes de avançar. Marcar cada tarefa somente após execução, validação e aprovação correspondentes; tarefas de uma mesma etapa podem ser aprovadas em conjunto. Reutilizar evidências válidas; reexecutar testes só com justificativa técnica registrada.

## 1. Login — preservação da implementação existente

- [x] 1.1 Usar o [protótipo](prototypes/login.html) e o [histórico aprovado](historico/login/) como baseline. Não editar componente, estilos ou assets nem reaplicar a delta histórica. Conferir a apresentação e os fluxos já verificados, incluindo foco em erros e navegação ao Cadastro; executar apenas as verificações da aplicação ainda necessárias ao ponto de aprovação, incluindo o Narrador com a usuária. Registrar a aprovação da preservação; na etapa 3, verificar apenas regressões afetadas.

## 2. Cadastro de Empresa

- [x] 2.1 Aplicar composição, rolagem e associação condicional de erros conforme o [protótipo](prototypes/cadastro-empresa.html) e a [delta](specs/registro-empresa/spec.md). Testar envio único, falha com foco no resumo e retorno ao Login sem autenticação automática; conferir rolagem com erros e zoom e cumprir o ponto de aprovação antes de Início.

## 3. Estrutura compartilhada, Início e identidade

- [x] 3.1 Introduzir o layout autenticado e implementar [Início](prototypes/painel.html) conforme a [delta de acesso](specs/account-access/spec.md), preservando URLs, guardas e carregamento sob demanda. Usar [Frota](prototypes/frota.html) como referência operacional, preservando particularidades dos módulos. Compilar Tailwind 4.3.5 no build existente, sem importar o CSS compilado dos protótipos. Testar quatro acessos, rota ativa, navegação protegida e separação das rotas públicas.
- [x] 3.2 Integrar o coordenador de Sair e a interface dos editores conforme [DT-22](../../../../docs/tdd.md#dt-22--coordenação-da-saída-compartilhada-redesign-visual). Testar revogação e recuperação em Início; exercitar a trava de mutações e as corridas em testes controlados do coordenador, com editor simulado. Verificar tentativa única, renovação controlada e arbitragem no serviço e no interceptor de `401` tardio de leitura e mutação, durante a saída e após troca de sessão, sem renovação, repetição ou redirecionamento indevidos. Não liberar Sair em rota com mutações sem integrar suas proteções nas etapas 4 a 7.
- [x] 3.3 Manter o título **LocaCar** e integrar o favicon com o monograma aprovado. Verificar o SVG servido pelo build e a legibilidade a `16×16` e `32×32`. Verificar regressões afetadas de Login e Cadastro e cumprir o ponto de aprovação de Início, registrando os limites da cobertura de DT-22.

## 4. Frota

- [x] 4.1 Aplicar o [protótipo](prototypes/frota.html) e as deltas de [Frota](specs/grupos-planos-veiculos/spec.md) e [Fotos](specs/fotos-veiculos/spec.md). Testar confirmações de exclusão, filtros, paginação, miniaturas privadas e sucesso parcial com nova tentativa apenas da foto.
- [x] 4.2 Integrar rascunhos, seleção e operações de foto ao coordenador. Testar cancelamento sem descarte, bloqueio em operação e distinção entre cadastro confirmado e foto pendente. Conferir rolagem local e foco em diálogos e cumprir o ponto de aprovação antes de Pessoas.

## 5. Pessoas

- [x] 5.1 Aplicar o [protótipo](prototypes/pessoas.html) e a [delta](specs/pessoas/spec.md). Integrar rascunhos, vínculos e gravações ao coordenador. Testar PF/PJ, elegibilidade, confirmação de exclusão, falha parcial de vínculos e preservação ao cancelar Sair. Conferir teclado nas abas e foco em diálogos e cumprir o ponto de aprovação antes de Preços.

## 6. Preços

- [x] 6.1 Aplicar o [protótipo](prototypes/precos.html) e a [delta](specs/precos-operacionais/spec.md), mantendo planos em Frota. Integrar rascunhos e mutações ao coordenador. Testar confirmação de exclusão, ausência versus zero, unidade e histórico de combustível e bloqueio de Sair durante gravação. Conferir teclado nas abas, foco e adaptação dos cartões e cumprir o ponto de aprovação antes de Aluguéis.

## 7. Aluguéis

- [x] 7.1 Aplicar o [protótipo](prototypes/alugueis.html) e a [delta](specs/alugueis/spec.md) a consulta, contratação, edição, devolução, demonstrativos e ocultação. Testar confirmação de ocultação, consulta explícita do histórico oculto, invalidação de prévia e preservação dos valores contratados, sem mudar cálculos ou contratos.
- [x] 7.2 Integrar contratação, devolução e demais mutações ao coordenador, mantendo a proteção de navegação da devolução. Testar bloqueio durante gravação e cancelamento de Sair com resultado incerto, preservando comando, chave e ETag aplicável. Conferir foco em erros e diálogos e leitura dos demonstrativos; cumprir o ponto de aprovação antes da validação integrada.

**Validação de 7.1 (RF-10, CA-04):** rejeição da contratação por CNH vencida, mensagem e ausência de gravação comprovadas com API e SQL Server reais nas [evidências de Aluguéis](evidencias.md#etapa-7--aluguéis-para-conferência).

**Rodada de correções da avaliação manual (7.1/7.2):** foco por modalidade, anúncios, paleta, máscaras e validação, confirmação ao sair ou voltar com alterações; transversalmente, exibição de documentos PF em linhas separadas em Pessoas. Preservar os protótipos e o gate de aprovação.

**Ajuste transversal da etapa 7 (RF-20/DT-23):** implementação e verificações registradas em [Códigos permanentes](evidencias.md#códigos-permanentes--ajuste-transversal-aprovado). A [aprovação dos refinamentos finais](evidencias.md#aprovação-final-da-etapa-7) encerra 7.1/7.2, mas não substitui o gate de DT-22.

## 8. Validação integrada — gate de DT-22

- [x] 8.1 Validar Sair em **todos os módulos autenticados**: cancelamento sem HTTP, disputa com gravação ou foto, solicitações simultâneas, renovação concorrente, `401` tardio de leitura e mutação e respostas de sessão substituída sem efeitos indevidos. Com SQL Server real, cobrir perda de resposta após commit e `GET /api/v1/session` ativo antes do commit do logout: o `200` isolado não libera gravações; sem confirmação segura, verificar nova tentativa explícita ou reautenticação. Em contratação e devolução, cancelar Sair após resultado incerto e repetir a mesma intenção com comando, chave e ETag quando aplicável, comprovando ausência de efeito duplicado. Usar SQL Server real também onde isolamento ou repetição dependam do banco. Verificar foco, anúncios e retorno ao Login.
- [x] 8.2 Registrar evidências integradas por proteção, rastreadas à [delta de acesso](specs/account-access/spec.md) e a DT-22, e obter aprovação da usuária. Só considerar Sair concluído após comprovar a integração e a validação das proteções de todos os módulos; isso não equivale à homologação da F6.

## 9. Conferência final da change

**Pontos para a revisão global:** foco por mouse nas abas de Preços e concordância na paginação de Pessoas foram tratados na rodada de refinamentos da etapa 7. Usar as evidências dirigidas e repetir somente se houver regressão ou cobertura insuficiente.

**Refinamentos finais de 9.1:** ver [design](design.md#refinamentos-da-conferência-manual-da-etapa-9) e [delta de Pessoas](specs/pessoas/spec.md). Validar apenas preenchimento automático e foco afetados, geometria de tabelas, menu em desktop e 200%, e contrato de e-mail de condutor com migração em SQL Server isolado. Manter a etapa aberta para aprovação.

- [x] 9.1 Consolidar a paridade global nos dois sentidos, as aprovações por módulo e o gate de DT-22. Conferir regressões com as evidências existentes e executar apenas as verificações afetadas ou ainda sem cobertura, registrando o motivo. Executar o build da aplicação e registrar o resultado.
- [x] 9.2 Comparar os SHA-256 dos sete protótipos com a [aprovação global](evidencias.md#aprovação-da-revisão-visual-global), executar `openspec validate redesign-visual --type change --strict` e `git diff --check`, registrar resultados e pendências e obter aprovação final explícita da usuária.

Encerramento: [aprovação manual geral e consolidação final](evidencias.md#aprovação-final-da-etapa-9).
