# Tasks

Caixas marcadas representam implementação e verificações registradas em `evidencias.md`; caixas abertas continuam pendentes. Aprovar a SDD ou revisar HTML não implementa requisitos. Escopo de evidência e dependências F3/F4/F5: `proposal.md`, seção Recorte de comprovação.

## 1. Gates e contratos transversais

- [x] 1.1 Obter validação final explícita da SDD revisada via question antes de alterar app/; verificar registro da aprovação e `openspec validate fase-2-cadastros --strict`.
- [x] 1.2 Verificar documentação oficial das APIs Angular Signal Forms, EF Core SQL Server/concorrência/filtros e ASP.NET Core CORS/ETag efetivamente usadas; registrar versões, fontes e compatibilidade com manifests/lockfiles existentes, sem instalar dependências sem autorização.
- [x] 1.3 Implementar tipos/validação de strings decimais, paginação e erros por campo conforme design, com testes MSTest de escala, limite, formato, ausência e zero; documentar contratos/OpenAPI no mesmo incremento.
- [x] 1.4 Implementar portas de ator/empresa, unidade transacional e lock SQL da Empresa sem acoplar Application/Domain ao EF/HTTP; verificar testes de orquestração, rollback e serialização em SQL Server isolado.
- [x] 1.5 Estender auditoria aditivamente para entidade/correlação/campos, sem dados pessoais; verificar migration da F1 e testes de commit conjunto e falha de auditoria revertendo a mutação, documentando resultados reais.
- [x] 1.6 Implementar ETag/If-Match, mapeamento 400/404/409/412/428/503 e CORS expondo ETag; verificar testes HTTP/preflight em duas origens e regressão da autenticação F1.

## 2. Grupos e planos — F2-01

- [x] 2.1 Implementar entidades e casos de uso de grupos/planos com invariantes da modalidade, identidade estável e exclusão por vínculos cadastrais; verificar MSTest com parâmetros inválidos, preços não negativos e modalidade duplicada.
- [x] 2.2 Implementar mapeamentos/migrations de grupos/planos, índices ativos, FKs compostas, rowversion e guardas tenant; verificar SQL Server real, incluindo anexação maliciosa, referências cruzadas, exclusão/recriação e concorrência.
- [x] 2.3 Implementar endpoints CRUD/paginação/grupoId com ETag e auditoria; verificar contratos HTTP de criação, edição, erro por campo, bloqueio de grupo vinculado e exclusão de plano nunca usado; atualizar OpenAPI.
- [x] 2.4 Implementar UI de grupos/planos com Signal Forms conforme prototypes/frota.html, sem controles exclusivos de revisão; verificar Vitest de campos condicionais, preenchimento da edição, estados, feedback, teclado e ausência de controles extras.

## 3. Veículos — F2-02 e F2-05

- [x] 3.1 Implementar entidade/casos de uso de veículo, capacidade/ano/campos e normalização de placa sem flag redundante de disponibilidade; verificar MSTest de capacidade, obrigatórios e escala, documentando limites.
- [x] 3.2 Implementar persistência com placa única entre ativos, tenant/FKs, exclusão e ETag; verificar SQL Server real com duas empresas, placa reaproveitada em novo ID e rollback de alteração inválida.
- [x] 3.3 Implementar CRUD HTTP de veículos com filtro de grupo e paginação, sem rota de foto; verificar integração de referências inválidas, isolamento, placa duplicada, precondição e ausência de foto no OpenAPI.
- [x] 3.4 Implementar formulário e frota conforme prototypes/frota.html, filtro/agrupamento da página, edição completa e exclusão permitida; verificar Vitest e documentar o alcance paginado do agrupamento, sem prometer disponibilidade contratual nesta fase.

## 4. Clientes e vínculos — F2-03 e F2-05

- [x] 4.1 Implementar CPF/CNPJ por verificadores e cliente PF/PJ/endereço, e-mail opcional e limites; verificar MSTest de entradas sintéticas válidas/inválidas, sequências repetidas e dados específicos de cada tipo.
- [x] 4.2 Implementar persistência/CRUD de clientes com unicidade ativa, tenant, ETag e exclusão lógica; verificar SQL Server/HTTP para duplicidade, reutilização com novo ID, referências preservadas, corpo de outra empresa e OpenAPI.
- [x] 4.3 Implementar consulta/inclusão/remoção PF–PJ com ETag da raiz PF, FKs e auditoria, sem criar requisito adicional para condutor de PJ; verificar SQL Server/HTTP de relação duplicada, tipos incompatíveis, concorrência e duas empresas.
- [x] 4.4 Implementar UI de clientes e vínculos conforme prototypes/pessoas.html com Signal Forms, PF/PJ, endereço, edição preenchida e remoção de vínculo; verificar Vitest, estados/erros/foco e ausência de campos extras.

## 5. Condutores e CNH — F2-03 e F2-05

- [x] 5.1 Implementar cadastro/edição de condutor e vínculo direto obrigatório ao contratante, sem exigir cliente PF prévio para PJ; verificar MSTest de CPF/validade e CNH vencida permitida no cadastro, sem confundir elegibilidade futura.
- [x] 5.2 Implementar coerência de CNH em criação e atualização coordenada nos dois sentidos cliente/condutor; verificar MSTest de incompatibilidade e integração SQL de versões/auditoria atômicas, inclusive falha parcial.
- [x] 5.3 Implementar persistência/CRUD HTTP, CPF ativo único, exclusão lógica, ETag e isolamento; verificar SQL Server real/HTTP de reutilização com novo ID, referência excluída preservada e seleção nova somente de ativos; atualizar OpenAPI.
- [x] 5.4 Implementar UI de condutores e opção do próprio cliente PF conforme prototypes/pessoas.html; verificar Vitest de dados reaproveitados, obrigatórios ausentes, listagem e edição, sem alegar consulta documental oficial.

## 6. Taxas, seguros e combustíveis — F2-04

- [x] 6.1 Implementar CRUD de taxas fixa/diária em Domain/Application, SQL e HTTP com ETag/auditoria; verificar unidade/SQL/HTTP de preços inválidos, exclusão permitida e referências preservadas; atualizar OpenAPI.
- [x] 6.2 Implementar seguros independentes ausente/zero e primeira escrita condicional, com auditoria; verificar unidade/SQL/HTTP incluindo duas escritas concorrentes, ETag inicial e preço não configurado sem zero implícito; atualizar OpenAPI.
- [x] 6.3 Implementar vigências append-only com relógio de servidor e ordem serial de combustível, mapeamento SQL e histórico paginado somente leitura; verificar unidade/SQL com mesmo instante, concorrência e preservação de versões, sem data editável; atualizar OpenAPI.
- [x] 6.4 Implementar telas Signal Forms conforme prototypes/precos.html com CRUD de taxas, seguros separados e combustível atual/histórico; verificar Vitest de ausência/zero, erro negativo, edição preenchida, versões e estados acessíveis; não copiar relógio sintético ou mensagens de auditoria executada.

## 7. Integração, paridade e entrega — F2-06

- [x] 7.1 Integrar navegação/rotas protegidas dos cadastros, usando Tailwind 4.3.5 e sessão em memória existentes; verificar Vitest de guard/interceptor e ausência de armazenamento persistente ou recurso extra de produto, registrando divergência intencional do CSS inline dos protótipos.
- [x] 7.2 Executar builds, MSTest/unidade/aplicação/HTTP, integração em SQL Server real isolado e Vitest; registrar comandos, ambiente, resultados e limitações reais em evidências, sem usar banco de Development ou User Secrets pessoais.
- [x] 7.3 Executar Playwright J-02 em duas origens e duas empresas, cobrindo cadastros, erros, exclusão/reuso, logout e isolamento; verificar teclado/foco/rótulos/zoom com inspeção manual complementar, sem alegar certificação de acessibilidade.
- [x] 7.4 Revisar specs ↔ protótipos ↔ implementação/testes nos dois sentidos; verificar que não faltam ações CRUD, estados ou mensagens e não existem filtros/controles/fotos extras; registrar cenários contratuais ainda não executáveis sem fingir aprovação deles.
- [x] 7.5 Atualizar TDD/checklist F2 somente com evidências executadas, registrar estado efetivo do schema já configurado e rastrear pendências F3/F4/F5; verificar diff completo, ausência de segredos e consistência dos RF/DT/CA antes de propor entrega.

## Workflow follow-up

- F3: integrar as restrições RF-04–11 ligadas a aluguel e os testes de corrida entre cadastro/contratação, plano utilizado, preço mantido e CNH até o retorno. Não substituir consulta real por booleano ou porta que sempre permita/bloqueie.
- F4: comprovar snapshots/demonstrativos estáveis, combustível retroativo e CA-12; F5: fotos privadas. Estes critérios não se tornam atendidos pela F2.
- Após implementação verificada, preparar commits atômicos/PR Ready for review conforme AGENTS.md, template, labels existentes e rastreabilidade; merge só com autorização específica.
- Sincronizar specs e arquivar apenas mediante solicitação de fechamento e validações aplicáveis.
