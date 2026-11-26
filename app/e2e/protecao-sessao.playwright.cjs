// Executar somente via Testar-Integracao.ps1 -JornadaSessao, com dados e SQL exclusivos.
const assert = require('node:assert/strict');
const { execFile, spawn } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const { promisify } = require('node:util');
const { setTimeout: esperar } = require('node:timers/promises');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const api = 'http://localhost:5001/api/v1';
const origem = 'http://127.0.0.1:4201';
const container = process.env.LOCADORA_SQL_JORNADA_SESSAO;
const aviso = 'Não foi possível confirmar a revogação remota.';
const sqlArgs = ['exec', '-i', container, 'sh', '-c',
  'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -d LocadoraTeste_Base "$@"', 'sqlcmd'];

async function sql(comando) {
  const { stdout } = await promisify(execFile)('docker', [...sqlArgs, '-Q', 'SET NOCOUNT ON; ' + comando], { timeout: 5000 });
  return stdout.trim();
}

function diferida() {
  let resolver;
  const promessa = new Promise(resolve => { resolver = resolve; });
  return { promessa, resolver };
}

async function aguardar(condicao, descricao) {
  for (let i = 0; i < 40; i++) {
    if (await condicao()) return;
    await esperar(100);
  }
  throw new Error(descricao);
}

function dataLocal(instante) {
  const partes = new Intl.DateTimeFormat('pt-BR', {
    timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(instante);
  const { day, month, year, hour, minute } = Object.fromEntries(partes.map(p => [p.type, p.value]));
  return `${day}${month}${year}${hour}${minute}`;
}

async function verificar() {
  assert.match(container ?? '', /^locadora-f1-testes-[0-9a-f]{32}-sqlserver-1$/);
  const browser = await chromium.launch();
  try {
    const contexto = await browser.newContext({ locale: 'pt-BR' });
    const p = await contexto.newPage();
    p.setDefaultTimeout(10000);
    const erros = [];
    p.on('pageerror', erro => erros.push(erro.message));
    const pedidos = [];
    p.on('request', r => {
      if (r.url().startsWith(api + '/')) pedidos.push({ rota: new URL(r.url()).pathname.slice('/api/v1'.length), metodo: r.method() });
    });
    const contagem = (rota, metodo = 'POST') => pedidos.filter(r => r.rota === rota && r.metodo === metodo).length;
    const btn = (name, alvo = p) => alvo.getByRole('button', { name, exact: true });
    async function modulo(name) {
      const link = p.getByRole('link', { name, exact: true });
      const destino = new URL(await link.getAttribute('href'), origem);
      await link.click();
      await p.waitForURL(destino.href);
    }
    const dialogo = p.getByRole('dialog', { name: 'Sair do sistema?' });
    const foco = seletor => p.waitForFunction(s => document.activeElement?.matches(s), seletor);
    const status = (rota, metodo, codigo) => p.waitForResponse(r => r.url() === api + rota &&
      r.request().method() === metodo && r.status() === codigo);
    async function clicar(nome, rota, codigo, metodo = 'POST') {
      const [resposta] = await Promise.all([p.waitForResponse(r => r.url() === api + rota &&
        r.request().method() === metodo), btn(nome).click()]);
      assert.equal(resposta.status(), codigo, `${metodo} ${rota}`);
      return resposta;
    }
    let sessao;
    async function enviar(rota, metodo, codigo, data, extras = {}) {
      const r = await contexto.request.fetch(api + rota, { method: metodo, data,
        headers: { ...(sessao ? { Authorization: `Bearer ${sessao.accessToken}` } : {}), ...extras } });
      assert.equal(r.status(), codigo, `${metodo} ${rota}`);
      return r;
    }
    const criar = (rota, data) => enviar(rota, 'POST', 201, data).then(r => r.json());
    const username = `sessao-${randomUUID().slice(0, 8)}`;
    const password = randomUUID();
    await criar('/registro', { username, email: `${username}@example.invalid`, password });
    await p.goto(origem + '/login');
    async function entrar() {
      await p.getByLabel('Usuário', { exact: true }).fill(username);
      await p.getByLabel('Senha', { exact: true }).fill(password);
      sessao = await (await clicar('Entrar', '/session/login', 200)).json();
      await p.waitForURL(origem + '/');
    }
    await entrar();
    const empresa = JSON.parse(Buffer.from(sessao.accessToken.split('.')[1], 'base64url')).empresaId;
    assert.match(empresa, /^[0-9a-f-]{36}$/i);
    const empresaSql = `EmpresaId = '${empresa}'`;
    const grupo = await criar('/grupos', { nome: 'Compactos' });
    const plano = await criar('/planos', { grupoId: grupo.id, modalidade: 'Livre', precoDiario: '100' });
    const veiculo = await criar('/veiculos', { grupoId: grupo.id, placa: 'SIM0A08', marca: 'Aurora', modelo: 'Lume',
      cor: 'Prata', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50' });
    const cliente = await criar('/clientes', { tipo: 'PJ', nome: 'Empresa Exemplo', cnpj: '11222333000181',
      telefone: '11912345678', endereco: { estado: 'SP', cidade: 'Cidade', bairro: 'Bairro', rua: 'Rua Exemplo', numero: '1' } });
    const condutor = await criar('/condutores', { clienteId: cliente.id, nome: 'Condutor Exemplo', cpf: '52998224725',
      email: 'condutor@example.invalid', cnh: 'CNH-SIM', validadeCnh: '2040-01-01', telefone: '0000' });
    const combustivel = '/configuracoes/combustiveis/Gasolina';
    const preco = await enviar(combustivel, 'GET', 200);
    await enviar(combustivel, 'PUT', 200, { preco: '6' }, { 'If-Match': preco.headers().etag });

    async function cancelarSaida(texto) {
      const antes = pedidos.length;
      // Dois acionamentos na mesma pilha, antes de o diálogo adquirir foco.
      await p.locator('.botao-sair').evaluate(b => { b.focus(); b.click(); b.click(); });
      await dialogo.waitFor();
      assert.match(await dialogo.locator('#mensagem-saida').innerText(), texto);
      assert.match(await dialogo.ariaSnapshot(), /dialog "Sair do sistema\?"/);
      await foco('#titulo-saida ~ div button[autofocus]');
      await p.keyboard.press('Escape');
      await dialogo.waitFor({ state: 'hidden' });
      await foco('.botao-sair');
      assert.deepEqual(pedidos.slice(antes).filter(r => r.rota.startsWith('/session')), []);
    }

    async function bloquearDurante(rota, acao, codigo, metodo = 'POST', perderResposta = false) {
      const chegada = diferida();
      const liberar = diferida();
      const resultado = diferida();
      const manipulador = async route => {
        if (route.request().method() !== metodo) return route.continue();
        chegada.resolver();
        await liberar.promessa;
        try {
          const r = await route.fetch({ maxRetries: 0 });
          resultado.resolver(r.status());
          if (perderResposta) await route.abort('failed');
          else await route.fulfill({ response: r });
        } catch (erro) {
          resultado.resolver(erro);
        }
      };
      await p.route(api + rota, manipulador);
      try {
        await btn(acao).click();
        await Promise.race([chegada.promessa, esperar(5000).then(() => { throw new Error(`Sem ${metodo} ${rota}`); })]);
        await p.locator('.botao-sair[aria-disabled="true"]').waitFor();
        await p.locator('#situacao-saida').filter({ hasText: 'Aguarde a operação em andamento.' }).waitFor();
        assert.match(await p.locator('#situacao-saida').ariaSnapshot(), /status: Aguarde a operação em andamento/);
        const antes = contagem('/session/logout');
        await p.locator('.botao-sair').evaluate(b => b.click());
        assert.equal(await dialogo.isVisible(), false);
        assert.equal(contagem('/session/logout'), antes);
      } finally {
        liberar.resolver();
      }
      assert.equal(await resultado.promessa, codigo, `${metodo} ${rota} na API`);
      await p.unroute(api + rota, manipulador);
      await p.waitForFunction(() => !document.querySelector('.botao-sair[aria-disabled]'));
    }

    await modulo('Frota');
    await btn('Grupos').click();
    await btn('Cadastrar').click();
    await p.locator('#grupo-nome').fill('Grupo da etapa 8');
    await cancelarSaida(/alterações não salvas/);
    assert.equal(await p.locator('#grupo-nome').inputValue(), 'Grupo da etapa 8');
    await bloquearDurante('/grupos', 'Salvar', 201);
    await btn('Veículos').click();
    await btn('Editar SIM0A08').click();
    const foto = p.locator('input[type=file]');
    const png = await p.evaluate(() => {
      const tela = document.createElement('canvas');
      tela.width = tela.height = 16;
      tela.getContext('2d').fillRect(0, 0, 16, 16);
      return tela.toDataURL('image/png').split(',')[1];
    });
    await foto.setInputFiles({ name: 'sintetica.png', mimeType: 'image/png',
      buffer: Buffer.from(png, 'base64') });
    await cancelarSaida(/seleção pendente/);
    await p.getByRole('status').filter({ hasText: 'Um arquivo selecionado.' }).waitFor();
    await bloquearDurante(`/veiculos/${veiculo.id}/foto`, 'Enviar foto', 204, 'PUT');

    await modulo('Pessoas');
    await btn('Editar Empresa Exemplo').click();
    await p.locator('#cliente-nome').fill('Empresa em edição');
    await cancelarSaida(/alterações não salvas/);
    assert.equal(await p.locator('#cliente-nome').inputValue(), 'Empresa em edição');
    await bloquearDurante(`/clientes/${cliente.id}`, 'Salvar cadastro', 200, 'PUT');

    await modulo('Preços');
    await p.locator('#nova-taxa').click();
    await p.locator('#taxa-nome').fill('Taxa da etapa 8');
    await p.locator('#taxa-preco').fill('12');
    await cancelarSaida(/alterações não salvas/);
    assert.equal(await p.locator('#taxa-nome').inputValue(), 'Taxa da etapa 8');
    await bloquearDurante('/taxas-servicos', 'Salvar', 201);
    console.log('Frota, Pessoas e Preços: cancelamento sem chamada de sessão, foco restaurado; Sair bloqueado durante gravação e foto reais.');

    await modulo('Aluguéis');
    await btn('Nova contratação').click();
    for (const [campo, valor] of Object.entries({ cliente: cliente.id, condutor: condutor.id, veiculo: veiculo.id, plano: plano.id }))
      await p.locator(`#aluguel-${campo}`).selectOption(valor);
    const previsto = dataLocal(Date.now() + 30 * 3600000);
    await p.locator('#aluguel-retorno').fill(previsto);
    await p.locator('#aluguel-hodometro').fill('66');
    await clicar('Calcular estimativa', '/alugueis/estimativas', 200);

    async function perderERepetir(rota, acao, codigo, comEtag) {
      const primeiro = p.waitForRequest(r => r.url() === api + rota && r.method() === 'POST');
      // A API confirma o commit ao proxy de teste; a SPA recebe apenas erro de rede.
      await bloquearDurante(rota, acao, codigo, 'POST', true);
      const repeticao = btn('Repetir confirmação');
      await repeticao.waitFor();
      const total = contagem(rota);
      await cancelarSaida(/mesma tentativa nesta tela/);
      assert.equal(contagem(rota), total);
      const resposta = await clicar('Repetir confirmação', rota, codigo);
      const a = await primeiro;
      const b = resposta.request();
      assert.equal(b.postData(), a.postData());
      const chave = await a.headerValue('idempotency-key');
      assert.ok(chave);
      assert.equal(await b.headerValue('idempotency-key'), chave);
      const etag = await a.headerValue('if-match');
      assert.equal(Boolean(etag), comEtag);
      assert.equal(await b.headerValue('if-match'), etag);
      assert.equal(contagem(rota), total + 1);
      return resposta.json();
    }
    const criado = await perderERepetir('/alugueis', 'Confirmar contratação', 201, false);
    await p.locator('#anuncio-alugueis').filter({ hasText: 'confirmada' }).waitFor({ state: 'attached' });
    assert.match(await p.locator('#anuncio-alugueis').ariaSnapshot(), /status: Contratação/);
    const rota = '/alugueis/' + criado.id;
    await btn('Registrar devolução').click();
    await p.locator('#devolucao-retornoReal').fill(previsto);
    await p.locator('#devolucao-hodometroFinal').fill('100');
    await p.locator('#devolucao-litrosRestantes').fill('45');
    await clicar('Calcular prévia', rota + '/devolucao/previa', 200);
    const final = await perderERepetir(rota + '/devolucao', 'Confirmar devolução', 200, true);
    assert.equal(final.id, criado.id);
    assert.equal(final.estado, 'Concluido');
    const contadores = await sql(`SELECT CONCAT(
      (SELECT COUNT(*) FROM Alugueis WHERE ${empresaSql}), ':',
      (SELECT COUNT(*) FROM FechamentosAlugueis WHERE ${empresaSql}), ':',
      (SELECT COUNT(*) FROM IdempotenciaAlugueis WHERE ${empresaSql}), ':',
      (SELECT COUNT(*) FROM Auditorias WHERE ${empresaSql} AND Evento = 'aluguel.criado'), ':',
      (SELECT COUNT(*) FROM Auditorias WHERE ${empresaSql} AND Evento = 'aluguel.devolvido'));`);
    assert.equal(contadores, '1:1:2:1:1');
    console.log('Aluguéis: perda após commit em contratação e devolução; cancelamento preservou comando, chave e ETag; SQL 1 contrato, 1 fechamento, 2 entradas de idempotência e 1 auditoria por operação.');

    await btn('Voltar aos aluguéis').click();
    // Mesma trava UPDLOCK usada por RevogarAsync. Aguardamos o SQL confirmar que
    // o POST verdadeiro está bloqueado; o GET não lê a linha travada de Empresas.
    const trava = spawn('docker', sqlArgs, { stdio: ['pipe', 'pipe', 'pipe'] });
    const fechada = new Promise(resolve => trava.on('close', resolve));
    let saidaSql = '';
    trava.stdout.on('data', chunk => { saidaSql += chunk; });
    trava.stderr.on('data', chunk => { saidaSql += chunk; });
    trava.stdin.on('error', () => {});
    trava.stdin.write(`SET NOCOUNT ON;
BEGIN TRAN;
SELECT Id FROM Empresas WITH (UPDLOCK, HOLDLOCK) WHERE Id = '${empresa}';
RAISERROR('TRAVA_ETAPA8_%d', 10, 1, @@SPID) WITH NOWAIT;
GO
`);
    try {
      await aguardar(() => /TRAVA_ETAPA8_\d+/.test(saidaSql), 'Trava isolada não iniciou.');
      const spid = saidaSql.match(/TRAVA_ETAPA8_(\d+)/)[1];
      const emVoo = diferida();
      const recebido = diferida();
      await p.route(api + '/session/logout', async route => {
        // Manter o HTTP do proxy em voo mesmo após simular a perda no navegador.
        try {
          const real = route.fetch({ maxRetries: 0, timeout: 25000 }).then(r => recebido.resolver(r.status()), recebido.resolver);
          await aguardar(async () => await sql(`SELECT COUNT(*) FROM sys.dm_exec_requests
            WHERE database_id = DB_ID() AND blocking_session_id = ${spid} AND wait_type LIKE 'LCK[_]M[_]%';`) === '1',
          'Revogação não ficou bloqueada no SQL.');
          emVoo.resolver();
          await route.abort('failed');
          await real;
        } catch (erro) { emVoo.resolver(erro); }
      });
      const consulta = status('/session', 'GET', 200);
      const antes = contagem('/session/logout');
      await p.locator('.botao-sair').evaluate(b => { b.click(); b.click(); });
      assert.equal(await emVoo.promessa, undefined);
      const ativa = await (await consulta).json();
      assert.deepEqual({ ativa: ativa.ativa, sessaoId: ativa.sessaoId }, { ativa: true, sessaoId: sessao.sessaoId });
      assert.equal(contagem('/session/logout'), antes + 1);
      const alerta = p.getByRole('alert').filter({ hasText: aviso });
      await alerta.waitFor();
      assert.match(await alerta.ariaSnapshot(), /alert: Não foi possível/);
      assert.equal(await btn('Nova contratação').isDisabled(), true);
      await btn('Entrar novamente').click();
      await p.waitForURL(origem + '/login');
      await p.getByRole('status').filter({ hasText: aviso }).waitFor();
      trava.stdin.end('ROLLBACK;\nGO\n');
      assert.equal(await fechado(fechada), 0);
      assert.equal(await recebido.promessa, 204);
      await enviar('/session', 'GET', 401);
      await p.unroute(api + '/session/logout');
    } finally {
      if (!trava.stdin.destroyed && !trava.stdin.writableEnded) trava.stdin.end('ROLLBACK;\nGO\n');
      await fechado(fechada);
    }
    console.log('Logout em disputa: um POST real bloqueado pelo SQL; GET 200 da mesma sessão não liberou gravações; reautenticação explícita retornou ao Login antes do commit.');

    await entrar();
    let revogacoes = 0;
    await p.route(api + '/session/logout', async route => {
      const real = await route.fetch({ maxRetries: 0 });
      revogacoes += Number(real.status() === 204);
      await route.abort('failed');
    });
    const rejeitada = status('/session', 'GET', 401);
    await btn('Sair').click();
    await rejeitada;
    await p.waitForURL(origem + '/login');
    await p.getByRole('status').filter({ hasText: aviso }).waitFor();
    assert.equal(revogacoes, 1);
    await enviar('/session/refresh', 'POST', 401, { refreshToken: sessao.refreshToken }, { Authorization: '' });
    assert.equal(await sql(`SELECT COUNT(*) FROM Auditorias WHERE ${empresaSql} AND Evento = 'Logout';`), '2');
    console.log('Início: 204 perdido após commit, GET 401 real, refresh revogado e Login com aviso de revogação não confirmada.');
    await p.unroute(api + '/session/logout');

    await entrar();
    const rotaTaxas = /\/api\/v1\/taxas-servicos(?:\?|$)/;
    const refreshPronto = diferida();
    const entregarRefresh = diferida();
    // Forçar o gatilho de 401 sem esperar o TTL do bearer real. A rotação e o
    // logout chamados pelo cliente continuam sendo HTTP real com SQL Server.
    const cors = { 'access-control-allow-origin': origem };
    await p.route(rotaTaxas, route => route.request().method() === 'GET'
      ? route.fulfill({ status: 401, headers: cors })
      : route.continue());
    await p.route(api + '/session/refresh', async route => {
      const real = await route.fetch({ maxRetries: 0 });
      refreshPronto.resolver(await real.json());
      assert.equal(real.status(), 200);
      await entregarRefresh.promessa;
      await route.fulfill({ response: real });
    });
    const inicio = pedidos.length;
    try {
      await modulo('Preços');
      const renovada = await Promise.race([refreshPronto.promessa, esperar(5000).then(() => { throw new Error('Sem refresh concorrente.'); })]);
      assert.equal(renovada.sessaoId, sessao.sessaoId);
      const confirmada = status('/session/logout', 'POST', 204);
      await btn('Sair').click();
      await p.getByRole('status').filter({ hasText: 'Confirmando a revogação' }).waitFor();
      assert.equal(pedidos.slice(inicio).filter(r => r.rota === '/session/logout').length, 0);
      entregarRefresh.resolver();
      const resposta = await confirmada;
      assert.ok(await resposta.request().headerValue('authorization') === `Bearer ${renovada.accessToken}`,
        'Logout não usou o contexto renovado.');
      await p.waitForURL(origem + '/login');
      const novos = pedidos.slice(inicio);
      for (const rota of ['/taxas-servicos', '/session/refresh', '/session/logout'])
        assert.equal(novos.filter(r => r.rota === rota && r.metodo !== 'OPTIONS').length, 1, rota);
      await enviar('/session', 'GET', 401, undefined, { Authorization: `Bearer ${renovada.accessToken}` });
    } finally {
      entregarRefresh.resolver();
    }
    assert.deepEqual(erros, []);
    console.log('Preços: Sair aguardou refresh real, usou o novo bearer, não repetiu a leitura e retornou ao Login após um único logout 204.');
  } finally {
    await browser.close();
  }
}

async function fechado(processo) {
  return Promise.race([processo, esperar(5000).then(() => { throw new Error('sqlcmd não encerrou.'); })]);
}

verificar().catch(erro => { console.error(erro); process.exitCode = 1; });
