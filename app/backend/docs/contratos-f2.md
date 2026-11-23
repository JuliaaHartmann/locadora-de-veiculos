# Contratos HTTP — cadastros F2

Base: `/api/v1`. Todas as rotas abaixo exigem sessão válida. Empresa e ator vêm da sessão, não do corpo, query ou cabeçalhos selecionáveis pelo cliente.

O documento gerado `/openapi/v1.json`, disponível em Development e Testing, descreve os DTOs, respostas, padrões decimais e cabeçalhos. Não é publicado automaticamente em produção.

## Recursos

| Recurso | Operações |
| --- | --- |
| `/grupos`, `/planos`, `/veiculos`, `/clientes`, `/condutores`, `/taxas-servicos` | GET paginado, POST; `/{id}` com GET, PUT, DELETE |
| `/clientes/{id}/vinculos-pj` | GET e POST `{ clientePjId }`, condicionados à versão da raiz PF |
| `/clientes/{id}/vinculos-pj/{clientePjId}` | DELETE, retorna a lista e nova versão da raiz PF |
| `/configuracoes/seguros/{tipo}` | GET e PUT `{ precoDiario }`; tipos `Cliente`, `Terceiros` |
| `/configuracoes/combustiveis/{tipo}` | GET e PUT `{ preco }`; tipos `Gasolina`, `Gas`, `Diesel`, `Alcool` |
| `/configuracoes/combustiveis/{tipo}/historico` | GET paginado somente leitura |

POST de recurso retorna 201, estado, ETag e Location. PUT retorna 200, estado e ETag. DELETE de cadastro retorna 204 e realiza exclusão lógica. Remoção de vínculo retorna 200 com lista atualizada e ETag. Combustível acrescenta versão, sem editar versões passadas.

## Decimais e campos

Dinheiro é **string**, com ponto decimal, sem sinal, expoente ou separador de milhar: `"0"`, `"125.50"`. Até 13 dígitos inteiros e 6 casas decimais; máximo `9999999999999.999999`. Excesso de escala não é arredondado. Nulo/branco não vira zero: campo obrigatório ausente produz erro. A UI converte vírgula local por texto.

Capacidade é string positiva de `0.001` a `999999999.999`, até 3 casas. Ano e franquia são inteiros não negativos Int32.

Diário exige preço diário e por km; Controlado também exige franquia; Livre mantém somente preço diário. Documentos de cliente são específicos do tipo PF/PJ. Não há verificação documental externa, rota de foto ou regra de aluguel implementada neste contrato F2.

Limites de texto: nome/marca/modelo/cidade/bairro/rua 200; cor/estado 100; e-mail 254; telefone/RG/CNH 32; número do endereço 40; placa normalizada 16; CPF/CNPJ normalizados 11/14.

## Paginação e seleção

Query `page` padrão 1, mínimo 1; `pageSize` padrão 20, de 1 a 100. Envelope:

```json
{ "items": [], "page": 1, "pageSize": 20, "total": 0 }
```

Planos e veículos aceitam filtro `grupoId`. Frota ordena por placa/ID; listas nominais por nome/ID e histórico por vigência/ordem/ID decrescentes. Agrupamento da UI organiza **somente a página carregada**, não toda a frota. Seletores percorrem as páginas de opções ativas.

## Concorrência, erros e CORS

GET detalhe mutável e escritas retornam ETag forte entre aspas. PUT/DELETE e POST de vínculo exigem `If-Match` com a versão lida. Seguro/combustível ausente retorna `configurado: false`, preços nulos e versão inicial da Empresa; zero explícito é configuração existente.

| Status | Significado |
| --- | --- |
| 400 | Validação/contrato, com `errors` por campo |
| 401/403 | Sessão inválida/acesso negado |
| 404 | Inexistente, excluído ou de outra empresa |
| 409 | Duplicidade ou vínculo impeditivo |
| 412 | Versão divergente |
| 428 | Precondição ausente |
| 503 | Dependência SQL indisponível |

ProblemDetails contém `status`, `title`, `code`, `traceId` e, quando aplicável, `errors`. Não inclui documento pessoal completo, credencial nem detalhes SQL. Após 412, a tela preserva entrada e orienta recarregar para reavaliar; não repete a escrita automaticamente.

CORS autoriza apenas origens exatas configuradas, sem credenciais, e expõe ETag. CORS não substitui autenticação/isolamento. Escritas usam transação e lock da Empresa; mutação e auditoria confirmam ou revertem juntas.
