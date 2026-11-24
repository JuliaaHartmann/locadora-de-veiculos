// Jornadas locais com SPA/API reais e SQL Server exclusivo de Testar-Integracao.ps1.
const assert = require('node:assert/strict');
const { mkdtemp, mkdir, rm, writeFile } = require('node:fs/promises');
const { join, resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const { tmpdir } = require('node:os');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const api = 'http://localhost:5001/api/v1';
const origens = ['http://localhost:4201', 'http://127.0.0.1:4201'];
const btn = (p, name) => p.getByRole('button', { name, exact: typeof name === 'string' });
const heading = (p, name) => p.getByRole('heading', { name, exact: typeof name === 'string' });
const esperar = (p, seletor, texto) => p.locator(seletor).filter({ hasText: texto }).waitFor();
const label = (p, name) => p.getByLabel(name, { exact: true });
const disponivel = (alvo, seletor) => alvo.and(alvo.page().locator(seletor));
const acionar = alvo => disponivel(alvo, ':is(button:enabled, a[href])').press('Enter');
const marcar = alvo => disponivel(alvo, 'input:enabled').press('Space');
async function digitar(alvo, texto) {
  await alvo.press('Control+a');
  await alvo.press('Backspace');
  await alvo.pressSequentially(texto);
}
async function selecionar(alvo, valor) {
  alvo = disponivel(alvo, 'select:enabled');
  await alvo.waitFor({ state: 'visible' });
  const posicao = await alvo.locator('option').evaluateAll((opcoes, v) => opcoes.findIndex(x => x.value === v || x.textContent.trim() === v), valor);
  assert.ok(posicao >= 0, `Opção não encontrada: ${valor}`);
  await alvo.press('Home');
  for (let i = 0; i < posicao; i++) await alvo.press('ArrowDown');
  assert.equal(await alvo.evaluate(e => e.selectedIndex), posicao);
}
function resposta(pagina, rota, metodo, status) {
  return pagina.waitForResponse(r => r.url() === api + rota && r.request().method() === metodo && r.status() === status);
}
async function nav(pagina, nome, caminho, origem) {
  await acionar(pagina.getByRole('navigation', { name: 'Navegação principal' }).getByRole('link', { name: nome, exact: true }));
  await pagina.waitForURL(`${origem}${caminho}`);
}

const formatter = new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo',
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
function dataLocal(ms) {
  const { day, month, year, hour, minute } = Object.fromEntries(formatter.formatToParts(ms).map(p => [p.type, p.value]));
  return `${day}/${month}/${year} ${hour}:${minute}`;
}

async function registrarEEntrar(p, origem, sufixo) {
  const usuario = `f4-${sufixo}-${Date.now()}`, senha = 'Senha-Sintetica-F4!';
  await p.goto(`${origem}/alugueis`, { waitUntil: 'domcontentloaded' });
  await p.waitForURL(`${origem}/login`);
  await acionar(p.getByRole('link', { name: 'Criar empresa', exact: true }));
  await p.waitForURL(`${origem}/registro`);
  const usuarioInput = label(p, 'Usuário');
  await usuarioInput.focus();
  await p.keyboard.press('Tab');
  assert.equal(await p.evaluate(() => document.activeElement.id), 'email');
  await digitar(usuarioInput, usuario);
  await digitar(label(p, 'E-mail'), `${usuario}@example.invalid`);
  await digitar(label(p, 'Senha'), senha);
  await digitar(label(p, 'Confirmar senha'), senha);
  const [registro] = await Promise.all([resposta(p, '/registro', 'POST', 201), acionar(btn(p, 'Criar empresa'))]);
  assert.deepEqual(Object.keys(await registro.json()).sort(), ['empresaId', 'usuarioId']);
  await p.waitForURL(`${origem}/login`);
  await esperar(p, '[role=status]', 'Empresa criada');
  await digitar(label(p, 'Usuário'), usuario);
  await digitar(label(p, 'Senha'), senha);
  const [login] = await Promise.all([resposta(p, '/session/login', 'POST', 200), acionar(btn(p, 'Entrar'))]);
  assert.equal((await login.request().allHeaders()).origin, origem);
  assert.equal((await login.allHeaders())['access-control-allow-origin'], origem);
  const credenciais = await login.json();
  await p.waitForURL(`${origem}/`);
  return credenciais;
}

async function prepararCadastros(p, origem, placa) {
  await nav(p, 'Frota', '/frota', origem);
  await acionar(btn(p, 'Grupos'));
  await acionar(btn(p, 'Cadastrar'));
  await digitar(label(p, 'Nome do grupo'), 'Compactos F4');
  const [g] = await Promise.all([resposta(p, '/grupos', 'POST', 201), acionar(btn(p, 'Salvar'))]);
  const grupo = await g.json();
  await acionar(btn(p, 'Planos')); await acionar(btn(p, 'Cadastrar'));
  await label(p, 'Grupo').locator(`option[value="${grupo.id}"]`).waitFor({ state: 'attached' });
  await selecionar(label(p, 'Grupo'), grupo.id); await selecionar(label(p, 'Modalidade'), 'Livre');
  await digitar(label(p, 'Preço diário (R$)'), '100');
  const [pl] = await Promise.all([resposta(p, '/planos', 'POST', 201), acionar(btn(p, 'Salvar'))]);
  const plano = await pl.json();
  await acionar(btn(p, 'Veículos')); await acionar(btn(p, 'Cadastrar'));
  await label(p, 'Grupo').locator(`option[value="${grupo.id}"]`).waitFor({ state: 'attached' });
  await selecionar(label(p, 'Grupo'), grupo.id);
  for (const [campo, valor] of [['Placa', placa], ['Marca', 'Aurora'], ['Modelo', 'Lume'], ['Cor', 'Prata'],
    ['Capacidade (litros)', '50'], ['Ano', '2026']]) await digitar(label(p, campo), valor);
  await selecionar(label(p, 'Combustível'), 'Gasolina');
  const [v] = await Promise.all([resposta(p, '/veiculos', 'POST', 201), acionar(btn(p, 'Salvar'))]);
  const veiculo = await v.json();
  await nav(p, 'Pessoas', '/pessoas', origem);
  await acionar(btn(p, 'Cadastrar'));
  await selecionar(label(p, 'Tipo'), 'PJ');
  for (const [campo, valor] of [['Nome', 'Locação Exemplo'], ['CNPJ', '11222333000181'], ['Telefone', '0000'],
    ['Estado', 'SP'], ['Cidade', 'Cidade'], ['Bairro', 'Bairro'], ['Rua', 'Rua Exemplo'], ['Número', '1']])
    await digitar(label(p, campo), valor);
  const [cl] = await Promise.all([resposta(p, '/clientes', 'POST', 201), acionar(btn(p, 'Salvar cadastro'))]);
  const cliente = await cl.json();
  await acionar(btn(p, 'Condutores')); await acionar(btn(p, 'Cadastrar'));
  await label(p, 'Cliente vinculado').locator(`option[value="${cliente.id}"]`).waitFor({ state: 'attached' });
  await selecionar(label(p, 'Cliente vinculado'), cliente.id);
  for (const [campo, valor] of [['Nome', 'Condutora Exemplo'], ['E-mail', 'condutora@example.invalid'], ['CPF', '52998224725'],
    ['CNH', 'CNH-SINTETICA'], ['Telefone', '0000']]) await digitar(label(p, campo), valor);
  const validade = label(p, 'Validade da CNH'), anoValidade = new Date().getUTCFullYear() + 2;
  // Chromium em pt-BR avança os segmentos dia/mês/ano enquanto recebe as teclas.
  await validade.focus();
  await p.keyboard.type(`3112${anoValidade}`);
  assert.equal(await validade.inputValue(), `${anoValidade}-12-31`);
  const [c] = await Promise.all([resposta(p, '/condutores', 'POST', 201), acionar(btn(p, 'Salvar cadastro'))]);
  const condutor = await c.json();
  await nav(p, 'Preços', '/precos', origem);
  const preparar = await taxa('Preparação', '30', 'Fixa');
  const suporte = await taxa('Suporte de viagem', '10', 'Diária');
  await acionar(btn(p, 'Seguros'));
  for (const [tipo, valor] of [['Cliente', '5'], ['Terceiros', '7']]) {
    const artigo = p.locator('article', { hasText: tipo === 'Cliente' ? /do cliente/i : /de terceiros/i });
    await acionar(artigo.getByRole('button', { name: 'Configurar preço' }));
    await digitar(label(p, 'Preço diário (R$)'), valor);
    await Promise.all([resposta(p, `/configuracoes/seguros/${tipo}`, 'PUT', 200), acionar(btn(p, 'Salvar'))]);
  }
  return { plano, veiculo, cliente, condutor, preparar, suporte };

  async function taxa(nome, preco, modalidade) {
    await acionar(btn(p, 'Cadastrar taxa'));
    await digitar(label(p, 'Nome'), nome); await digitar(label(p, 'Preço (R$)'), preco);
    await selecionar(label(p, 'Modalidade'), modalidade);
    const [t] = await Promise.all([resposta(p, '/taxas-servicos', 'POST', 201), acionar(btn(p, 'Salvar'))]);
    return t.json();
  }
}

async function jornada(contexto, origem, sufixo, worker) {
  const p = await contexto.newPage();
  p.setDefaultTimeout(18000);
  const erros = [];
  p.on('pageerror', erro => erros.push(erro.message));
  try {
    const sessao = await registrarEEntrar(p, origem, sufixo);
    const auth = { Authorization: `Bearer ${sessao.accessToken}` };
    const dados = await prepararCadastros(p, origem, `SIM0${sufixo}08`);
    const get = caminho => contexto.request.get(api + caminho, { headers: auth });
    const put = async (caminho, corpo) => {
      const atual = await get(caminho); assert.equal(atual.status(), 200);
      const r = await contexto.request.put(api + caminho, { data: corpo, headers: { ...auth, 'If-Match': atual.headers().etag } });
      assert.equal(r.status(), 200); return r.json();
    };
    await nav(p, 'Aluguéis', '/alugueis', origem);
    await esperar(p, '#resultado-alugueis', 'Nenhum aluguel encontrado');
    const previsto = Math.floor((Date.now() + 30 * 3600000) / 60000) * 60000;
    await acionar(btn(p, 'Nova contratação'));
    await p.locator(`#aluguel-cliente option[value="${dados.cliente.id}"]`).waitFor({ state: 'attached' });
    await selecionar(p.locator('#aluguel-cliente'), dados.cliente.id);
    await p.locator(`#aluguel-condutor option[value="${dados.condutor.id}"]`).waitFor({ state: 'attached' });
    await selecionar(p.locator('#aluguel-condutor'), dados.condutor.id);
    await selecionar(p.locator('#aluguel-veiculo'), dados.veiculo.id);
    await p.locator(`#aluguel-plano option[value="${dados.plano.id}"]`).waitFor({ state: 'attached' });
    await selecionar(p.locator('#aluguel-plano'), dados.plano.id);
    await digitar(p.locator('#aluguel-retorno'), dataLocal(previsto)); await digitar(p.locator('#aluguel-hodometro'), '21430');
    await marcar(p.getByRole('checkbox', { name: /Preparação/ }));
    await marcar(p.getByRole('checkbox', { name: /Seguro do cliente/ }));
    await marcar(p.getByRole('checkbox', { name: /Seguro de terceiros/ }));
    await acionar(btn(p, 'Calcular estimativa'));
    await esperar(p, 'app-demonstrativo-aluguel', 'R$ 254,00');
    const [criado] = await Promise.all([resposta(p, '/alugueis', 'POST', 201), acionar(btn(p, 'Confirmar contratação'))]);
    const { id } = await criado.json();
    const rota = `/alugueis/${id}`;
    await esperar(p, '#aviso-alugueis', 'Contratação confirmada');
    assert.equal((await (await get('/veiculos?disponiveis=true')).json()).total, 0);
    await acionar(btn(p, 'Editar aluguel'));
    await p.locator('#aluguel-hodometro').waitFor();
    const era = await p.locator('#aluguel-hodometro').inputValue();
    assert.equal(era, '21430');
    await Promise.all([resposta(p, rota, 'PUT', 200), acionar(btn(p, 'Salvar alterações'))]);
    await esperar(p, '#aviso-alugueis', 'Alterações salvas');
    await acionar(btn(p, 'Registrar devolução'));
    await heading(p, 'Registrar devolução').waitFor();
    const litros = label(p, 'Litros restantes (L)'), odometro = label(p, 'Hodômetro final (km)');
    const instante = label(p, 'Data e hora reais do retorno');
    await digitar(instante, dataLocal(previsto)); await digitar(odometro, '21680'); await digitar(litros, '50,0001');
    await acionar(btn(p, 'Calcular prévia'));
    await p.locator('#erro-devolucao-litrosRestantes').waitFor();
    await foco(p, '#erros-devolucao');
    assert.match(await litros.getAttribute('aria-describedby'), /erro-devolucao-litrosRestantes/);
    await contraste(p.locator('#erro-devolucao-litrosRestantes'));
    await digitar(litros, '50');
    await acionar(btn(p, 'Calcular prévia'));
    await esperar(p, '#erros-devolucao', 'Sem preço de combustível aplicável');
    await put('/configuracoes/combustiveis/Gasolina', { preco: '6' });
    await digitar(litros, '40,000');
    await marcar(p.getByRole('checkbox', { name: /Suporte de viagem/ }));
    await acionar(btn(p, 'Calcular prévia'));
    await esperar(p, 'app-demonstrativo-devolucao', 'R$ 334,00');
    await foco(p, '#titulo-calculo-devolucao');
    assert.equal(await p.getByRole('checkbox', { name: /Preparação/ }).count(), 0);
    await digitar(instante, dataLocal(previsto + 60000));
    assert.equal(await btn(p, 'Confirmar devolução').count(), 0);
    await acionar(btn(p, 'Calcular prévia'));
    await esperar(p, 'app-demonstrativo-devolucao', 'R$ 354,00');
    await contraste(btn(p, 'Confirmar devolução'));
    if (sufixo === 'A') await reflowEZoom(p, contexto, worker, origem, 'devolucao');

    const envios = [];
    await p.route(api + rota + '/devolucao', async interceptada => {
      const pedido = interceptada.request();
      if (pedido.method() !== 'POST') return interceptada.continue();
      envios.push({ chave: (await pedido.allHeaders())['idempotency-key'], corpo: pedido.postData() });
      if (envios.length === 1) {
        const real = await interceptada.fetch();
        assert.equal(real.status(), 200);
        await interceptada.abort('failed');
      } else await interceptada.continue();
    });
    await acionar(btn(p, 'Confirmar devolução'));
    await esperar(p, '#erros-devolucao', 'Resultado da devolução não confirmado');
    assert.equal(await litros.isDisabled(), true);
    await acionar(btn(p, 'Repetir confirmação'));
    await heading(p, 'Demonstrativo final').waitFor();
    await foco(p, '#aviso-alugueis');
    assert.ok(envios[0].chave); assert.deepEqual(envios, [envios[0], envios[0]]);
    assert.equal(await btn(p, 'Editar aluguel').count(), 0);
    assert.equal(await btn(p, 'Registrar devolução').count(), 0);
    const finalAntes = await p.locator('app-demonstrativo-devolucao').innerText();
    assert.match(finalAntes, /R\$ 354,00/); assert.match(finalAntes, /R\$ 1\.000,00/);
    assert.equal((await (await get('/veiculos?disponiveis=true')).json()).total, 1);
    await acionar(btn(p, 'Ocultar da operação'));
    await esperar(p, '#aviso-alugueis', 'O contrato e o demonstrativo foram preservados');
    await esperar(p, '#resultado-alugueis', 'Nenhum aluguel encontrado');
    assert.equal((await get(rota)).status(), 404);
    await selecionar(label(p, 'Situação'), 'Concluido');
    await marcar(label(p, 'Incluir aluguéis ocultos'));
    await acionar(btn(p, 'Filtrar'));
    await p.getByRole('row').filter({ hasText: id }).filter({ hasText: 'Oculto' }).waitFor();
    await foco(p, '#resultado-alugueis');
    if (sufixo === 'A') await reflowEZoom(p, contexto, worker, origem, 'historico');
    await acionar(btn(p, `Consultar aluguel ${id}`));
    await heading(p, 'Demonstrativo final').waitFor();
    assert.equal(await p.locator('app-demonstrativo-devolucao').innerText(), finalAntes);
    assert.equal(await btn(p, 'Ocultar da operação').count(), 0);
    for (const outro of jornadas)
      assert.equal((await get(`${outro.rota}?incluirOcultos=true`)).status(), 404);
    assert.equal(await p.locator('input[type=file]').count(), 0);
    assert.deepEqual(erros, []);
    console.log(`${origem}: J-01 a J-05 por teclado; empresa ${sufixo}, R$ 334/354, mesmo envio após perda, histórico e veículo liberado.`);
    return { rota, auth, p, origem };
  } catch (erro) {
    if (process.env.F4_CAPTURAS)
      await p.screenshot({ path: join(process.env.F4_CAPTURAS, `f4-erro-${sufixo}.png`), fullPage: true }).catch(() => {});
    throw erro;
  }
}

async function foco(p, seletor) {
  await p.waitForFunction(s => document.activeElement.matches(s), seletor);
  assert.ok(await p.evaluate(() => parseFloat(getComputedStyle(document.activeElement).outlineWidth) >= 2));
}

async function contraste(alvo) {
  const razao = await alvo.evaluate(elemento => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 1;
    const ctx = canvas.getContext('2d');
    const rgb = cor => {
      ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = cor; ctx.fillRect(0, 0, 1, 1);
      return [...ctx.getImageData(0, 0, 1, 1).data];
    };
    const luminancia = cor => cor.slice(0, 3).map(canal => canal / 255)
      .map(canal => canal <= .04045 ? canal / 12.92 : ((canal + .055) / 1.055) ** 2.4)
      .reduce((soma, canal, i) => soma + canal * [.2126, .7152, .0722][i], 0);
    let fundo = [255, 255, 255, 255];
    for (let no = elemento; no; no = no.parentElement) {
      const cor = rgb(getComputedStyle(no).backgroundColor);
      if (cor[3] === 255) { fundo = cor; break; }
    }
    const [menor, maior] = [luminancia(rgb(getComputedStyle(elemento).color)), luminancia(fundo)].sort((a, b) => a - b);
    return (maior + .05) / (menor + .05);
  });
  assert.ok(razao >= 4.5, `Contraste inferior a 4,5:1 (${razao.toFixed(2)}).`);
  console.log(`Contraste amostral: ${razao.toFixed(2)}:1.`);
}

async function reflowEZoom(p, contexto, worker, origem, tela) {
  const reflow = async () => {
    const medida = await p.evaluate(() => ({ conteudo: document.documentElement.scrollWidth, janela: innerWidth }));
    assert.ok(medida.conteudo <= medida.janela, `${tela}: reflow ${JSON.stringify(medida)}`);
  };
  if (process.env.F4_CAPTURAS)
    await p.screenshot({ path: join(process.env.F4_CAPTURAS, `f4-${tela}-spa.png`), fullPage: true });
  await p.setViewportSize({ width: 320, height: 800 });
  await reflow();
  if (tela === 'historico') {
    const tabela = p.getByRole('region', { name: 'Tabela de aluguéis' });
    await tabela.focus(); await foco(p, '[aria-label="Tabela de aluguéis"]');
    assert.ok(await tabela.evaluate(el => el.scrollWidth > el.clientWidth));
  }
  await p.setViewportSize({ width: 840, height: 900 });
  const cdp = await contexto.newCDPSession(p);
  await cdp.send('Emulation.clearDeviceMetricsOverride');
  const largura = await p.evaluate(() => innerWidth);
  const zoom = (fator) => worker.evaluate(async ({ url, fator }) => {
    const aba = (await chrome.tabs.query({})).find(x => x.url === url);
    await chrome.tabs.setZoom(aba.id, fator);
    return chrome.tabs.getZoom(aba.id);
  }, { url: `${origem}/alugueis`, fator });
  assert.equal(await zoom(2), 2);
  await p.waitForFunction(antes => innerWidth < antes * .65, largura);
  await reflow();
  assert.equal(await zoom(1), 1);
  await cdp.detach();
  await p.setViewportSize({ width: 1366, height: 1000 });
  console.log(`${tela}: reflow a 320 px e zoom nativo a 200%.`);
}

const jornadas = [];
async function executar() {
  const raiz = process.env.LOCALAPPDATA ? join(process.env.LOCALAPPDATA, 'Temp', 'opencode') : tmpdir();
  const temporario = await mkdtemp(join(raiz, 'fase4-navegador-'));
  let contexto;
  try {
    const extensao = join(temporario, 'zoom');
    await mkdir(extensao);
    await writeFile(join(extensao, 'manifest.json'), JSON.stringify({
      manifest_version: 3, name: 'Verificação sintética de zoom F4', version: '1.0.0',
      permissions: ['tabs'], background: { service_worker: 'worker.js' },
    }));
    await writeFile(join(extensao, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
    contexto = await chromium.launchPersistentContext(join(temporario, 'perfil'), {
      channel: 'chromium', executablePath: process.env.PLAYWRIGHT_EXECUTABLE || undefined,
      headless: true, locale: 'pt-BR', viewport: { width: 1366, height: 1000 },
      // As URLs/Origin permanecem distintas. Apenas a resolução de rede da instância de teste é fixada no loopback IPv4.
      args: [`--disable-extensions-except=${extensao}`, `--load-extension=${extensao}`, '--host-resolver-rules=MAP localhost 127.0.0.1'],
    });
    const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker', { timeout: 15000 });
    if (process.env.F4_CAPTURAS) {
      await mkdir(process.env.F4_CAPTURAS, { recursive: true });
      const prototipo = await contexto.newPage();
      const base = resolve(__dirname, '../../openspec/changes/archive/2026-11-23-fase-4-devolucao-operacao-local/prototypes');
      for (const nome of ['devolucao', 'historico']) {
        await prototipo.goto(pathToFileURL(join(base, `${nome}.html`)).href);
        if (nome === 'devolucao') {
          await acionar(btn(prototipo, 'Registrar devolução'));
          await digitar(prototipo.locator('#returned'), '24/11/2026 16:01');
          await digitar(prototipo.locator('#odometer'), '21680'); await digitar(prototipo.locator('#fuel'), '40');
          await marcar(prototipo.locator('#support'));
          await acionar(btn(prototipo, 'Calcular prévia'));
          await esperar(prototipo, '#statement-body', 'R$ 354,00');
        } else {
          await acionar(btn(prototipo, 'Consultar aluguel AL-2408'));
          await acionar(btn(prototipo, 'Ocultar da operação'));
          await marcar(label(prototipo, 'Incluir aluguéis ocultos'));
          await selecionar(label(prototipo, 'Situação'), 'done');
          await acionar(btn(prototipo, 'Filtrar'));
          await prototipo.getByRole('row').filter({ hasText: 'AL-2408' }).filter({ hasText: 'Oculto' }).waitFor();
        }
        await prototipo.screenshot({ path: join(process.env.F4_CAPTURAS, `f4-${nome}-prototipo.png`), fullPage: true });
      }
      await prototipo.close();
    }
    for (const [i, origem] of origens.entries()) jornadas.push(await jornada(contexto, origem, i ? 'B' : 'A', worker));
    for (const j of jornadas) {
      const outra = jornadas.find(x => x !== j);
      assert.equal((await contexto.request.get(`${api}${outra.rota}?incluirOcultos=true`, { headers: j.auth })).status(), 404);
      await nav(j.p, 'Painel', '/', j.origem);
      await Promise.all([resposta(j.p, '/session/logout', 'POST', 204), acionar(btn(j.p, 'Sair'))]);
      await j.p.waitForURL(`${j.origem}/login`);
      assert.equal((await contexto.request.get(`${api}${j.rota}?incluirOcultos=true`, { headers: j.auth })).status(), 401);
    }
    console.log('Isolamento A/B em ambos os sentidos e acesso após revogação verificados.');
  } finally {
    await contexto?.close();
    await rm(temporario, { recursive: true, force: true });
  }
}

executar().catch(erro => { console.error(erro.stack); process.exitCode = 1; });
