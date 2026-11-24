const assert = require('node:assert/strict');
const { mkdtemp, mkdir, rm, writeFile } = require('node:fs/promises');
const { join, resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const { tmpdir } = require('node:os');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const api = 'http://localhost:5001/api/v1';
const origens = ['http://localhost:4201', 'http://127.0.0.1:4201'];
const botao = (p, name) => p.getByRole('button', { name, exact: true });
const campo = (p, name) => p.getByLabel(name, { exact: true });
const acionar = a => a.and(a.page().locator(':is(button:enabled, a[href])')).press('Enter');
const mensagem = (p, seletor, texto) => p.locator(seletor).filter({ hasText: texto }).waitFor();
const resposta = (p, metodo, rota, status) => p.waitForResponse(r =>
  r.url().startsWith(api) && rota.test(new URL(r.url()).pathname) && r.request().method() === metodo && r.status() === status);
const frota = /\/veiculos$/;
const foto = /\/veiculos\/[^/]+\/foto$/;

async function digitar(a, v) { await a.press('Control+a'); await a.press('Backspace'); await a.pressSequentially(v); }
async function selecionar(a, valor) {
  // A lista pode reabrir enquanto o cadastro anterior ainda termina sua atualização.
  a = a.and(a.page().locator('select:enabled'));
  await a.waitFor({ state: 'visible' });
  const pos = await a.locator('option').evaluateAll((itens, v) => itens.findIndex(i => i.value === v || i.text.trim() === v), valor);
  assert.ok(pos >= 0);
  await a.press('Home');
  for (let i = 0; i < pos; i++) await a.press('ArrowDown');
  assert.equal(await a.evaluate(e => e.selectedIndex), pos);
}

async function entrar(p, origem, letra) {
  const usuario = `f5-${letra}-${Date.now()}`;
  const senha = 'Senha-Sintetica-F5!';
  await p.goto(origem + '/registro');
  await digitar(campo(p, 'Usuário'), usuario);
  await digitar(campo(p, 'E-mail'), `${usuario}@example.invalid`);
  await digitar(campo(p, 'Senha'), senha);
  await digitar(campo(p, 'Confirmar senha'), senha);
  await Promise.all([resposta(p, 'POST', /\/registro$/, 201), acionar(botao(p, 'Criar empresa'))]);
  await p.waitForURL(origem + '/login');
  await digitar(campo(p, 'Usuário'), usuario);
  await digitar(campo(p, 'Senha'), senha);
  const [login] = await Promise.all([resposta(p, 'POST', /\/session\/login$/, 200), acionar(botao(p, 'Entrar'))]);
  await p.waitForURL(origem + '/');
  assert.equal((await login.allHeaders())['access-control-allow-origin'], origem);
  const { accessToken } = await login.json();
  await acionar(p.getByRole('navigation', { name: 'Navegação principal' }).getByRole('link', { name: 'Frota', exact: true }));
  await p.waitForURL(origem + '/frota');
  await acionar(botao(p, 'Cadastrar'));
  await digitar(campo(p, 'Nome do grupo'), 'Grupo sintético');
  const [novo] = await Promise.all([resposta(p, 'POST', /\/grupos$/, 201), acionar(botao(p, 'Salvar'))]);
  const grupo = await novo.json();
  await acionar(botao(p, 'Veículos'));
  return { grupoId: grupo.id, auth: { Authorization: `Bearer ${accessToken}` } };
}

async function abrirNovo(p, grupoId, placa) {
  await acionar(botao(p, 'Cadastrar'));
  await p.waitForFunction(() => document.activeElement?.matches('h2[tabindex="-1"]') &&
    document.activeElement.textContent.includes('Cadastrar'));
  const grupo = campo(p, 'Grupo');
  await grupo.locator(`option[value="${grupoId}"]`).waitFor({ state: 'attached' });
  await selecionar(grupo, grupoId);
  for (const [nome, valor] of [['Placa', placa], ['Marca', 'Exemplo'], ['Modelo', 'Lume'],
    ['Cor', 'Azul'], ['Ano', '2026'], ['Capacidade (litros)', '50']])
    await digitar(campo(p, nome), valor);
}

async function png(p, cor) {
  const base64 = await p.evaluate(cor => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 16;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = cor;
    ctx.fillRect(0, 0, 16, 16);
    return canvas.toDataURL('image/png').split(',')[1];
  }, cor);
  return { name: 'veiculo-sintetico.png', mimeType: 'image/png', buffer: Buffer.from(base64, 'base64') };
}

async function conferirAcessibilidade(p, origem, worker, contexto) {
  const seletor = campo(p, 'Selecionar arquivo');
  await seletor.focus();
  assert.ok(await seletor.evaluate(e => parseFloat(getComputedStyle(e.parentElement).outlineWidth) >= 3));
  const razao = await p.locator('#ajuda-foto').evaluate(e => {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
    const ctx = canvas.getContext('2d');
    const cor = valor => { ctx.fillStyle = valor; ctx.fillRect(0, 0, 1, 1); return ctx.getImageData(0, 0, 1, 1).data; };
    const luminancia = c => [0.2126, 0.7152, 0.0722].reduce((n, peso, i) => {
      const x = c[i] / 255; return n + peso * (x <= 0.04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4);
    }, 0);
    return (luminancia(cor('#fff')) + .05) / (luminancia(cor(getComputedStyle(e).color)) + .05);
  });
  assert.ok(razao >= 4.5);
  const reflow = () => p.evaluate(() => document.documentElement.scrollWidth <= innerWidth);
  await p.setViewportSize({ width: 320, height: 820 });
  assert.ok(await reflow());
  await p.setViewportSize({ width: 840, height: 900 });
  const cdp = await contexto.newCDPSession(p);
  await cdp.send('Emulation.clearDeviceMetricsOverride');
  const largura = await p.evaluate(() => innerWidth);
  const zoom = fator => worker.evaluate(async ({ url, fator }) => {
    const aba = (await chrome.tabs.query({})).find(x => x.url === url);
    await chrome.tabs.setZoom(aba.id, fator);
    return chrome.tabs.getZoom(aba.id);
  }, { url: origem + '/frota', fator });
  assert.equal(await zoom(2), 2);
  await p.waitForFunction(antes => innerWidth < antes * .65, largura);
  assert.ok(await reflow());
  assert.equal(await zoom(1), 1);
  await cdp.detach();
  await p.setViewportSize({ width: 1366, height: 980 });
  console.log(`Seção de foto: foco, contraste amostral ${razao.toFixed(2)}:1, reflow a 320 px e zoom nativo de 200%.`);
}

async function jornada(contexto, origem, letra, worker) {
  const p = await contexto.newPage();
  const erros = [];
  p.on('pageerror', erro => erros.push(erro.message));
  const { auth, grupoId } = await entrar(p, origem, letra);
  const fotoAzul = await png(p, '#0369a1'), fotoVerde = await png(p, '#166534');
  const prefixo = `F5${letra}`;
  const regiao = p.getByRole('region', { name: 'Tabela de veículos' });
  const linha = placa => regiao.getByRole('row').filter({ hasText: placa });
  const miniatura = placa => linha(placa).getByRole('img', { name: `Foto do veículo ${placa}`, exact: true });
  const semFoto = placa => linha(placa).getByText('Sem foto', { exact: true });
  const placaFoto = `${prefixo}002`;
  const roteiros = [];
  p.on('request', r => {
    if (r.url().startsWith(api) && (r.method() === 'POST' && frota.test(new URL(r.url()).pathname) ||
      r.method() === 'PUT' && foto.test(new URL(r.url()).pathname))) roteiros.push(r.method());
  });
  // Cadastro sem foto.
  await abrirNovo(p, grupoId, `${prefixo}001`);
  await Promise.all([resposta(p, 'POST', frota, 201), acionar(botao(p, 'Salvar'))]);
  await mensagem(p, '[role=status]', 'Veículo cadastrado sem foto.');
  assert.deepEqual(roteiros.splice(0), ['POST']);
  await regiao.getByRole('columnheader', { name: 'Foto', exact: true }).waitFor();
  await semFoto(`${prefixo}001`).waitFor();

  // Cadastro rejeitado pelo SQL mesmo com arquivo selecionado.
  await abrirNovo(p, grupoId, `${prefixo}001`);
  await campo(p, 'Selecionar arquivo').setInputFiles(fotoAzul);
  await Promise.all([resposta(p, 'POST', frota, 409), acionar(botao(p, 'Salvar'))]);
  await mensagem(p, '[role=alert]', 'A foto não foi enviada');
  assert.deepEqual(roteiros.splice(0), ['POST']);
  assert.match(await campo(p, 'Selecionar arquivo').inputValue(), /veiculo-sintetico/);
  await acionar(botao(p, 'Cancelar'));

  // Sucesso parcial induzido antes de o primeiro PUT alcançar a API; retry usa API e volume reais.
  await abrirNovo(p, grupoId, `${prefixo}002`);
  const escolha = campo(p, 'Selecionar arquivo');
  const seletor = p.waitForEvent('filechooser');
  await escolha.press('Enter');
  await (await seletor).setFiles(fotoAzul);
  const modoFalha = async route => {
    if (route.request().method() !== 'PUT') return route.continue();
    await route.fulfill({ status: 503, json: {
      code: 'foto_nao_gravada', detail: 'Não foi possível salvar a foto.',
    }, headers: { 'access-control-allow-origin': origem } });
  };
  await p.route('**/veiculos/*/foto', modoFalha);
  const [criou, falhou] = await Promise.all([resposta(p, 'POST', frota, 201), resposta(p, 'PUT', foto, 503), acionar(botao(p, 'Salvar'))]);
  const id = (await criou.json()).id;
  const etag = (await criou.allHeaders()).etag;
  assert.ok(falhou.url().endsWith(`/${id}/foto`));
  assert.equal((await falhou.request().allHeaders())['if-match'], etag);
  await mensagem(p, '#erro-foto', 'O veículo foi cadastrado');
  await p.waitForFunction(() => document.activeElement?.id === 'erro-foto');
  assert.equal(await botao(p, 'Salvar').count(), 0);
  await p.unroute('**/veiculos/*/foto', modoFalha);
  const [recuperada] = await Promise.all([resposta(p, 'PUT', foto, 204), acionar(botao(p, 'Tentar enviar a foto novamente'))]);
  assert.ok(recuperada.url().endsWith(`/${id}/foto`));
  assert.equal((await recuperada.request().allHeaders())['if-match'], etag);
  await mensagem(p, '[role=status]', 'Veículo cadastrado e foto salva.');
  assert.deepEqual(roteiros.splice(0), ['POST', 'PUT', 'PUT']);
  await miniatura(placaFoto).waitFor();
  assert.equal(await linha(placaFoto).count(), 1);
  assert.match(await miniatura(placaFoto).getAttribute('src'), /^blob:/);

  // Os dois passos bem-sucedidos no primeiro envio.
  await abrirNovo(p, grupoId, `${prefixo}003`);
  await campo(p, 'Selecionar arquivo').setInputFiles(fotoVerde);
  await Promise.all([resposta(p, 'POST', frota, 201), resposta(p, 'PUT', foto, 204), acionar(botao(p, 'Salvar'))]);
  await mensagem(p, '[role=status]', 'Veículo cadastrado e foto salva.');
  assert.deepEqual(roteiros.splice(0), ['POST', 'PUT']);
  await miniatura(`${prefixo}003`).waitFor();
  await miniatura(placaFoto).evaluate(i => i.decode());
  if (letra === 'A') {
    if (process.env.F5_CAPTURAS) {
      await mkdir(process.env.F5_CAPTURAS, { recursive: true });
      await p.screenshot({ path: join(process.env.F5_CAPTURAS, 'f5-lista.png'), fullPage: true });
    }
    await p.setViewportSize({ width: 320, height: 820 });
    assert.ok(await p.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    assert.ok(await regiao.evaluate(e => e.scrollWidth > e.clientWidth));
    await regiao.press('ArrowRight');
    await p.waitForFunction(() => document.querySelector('[aria-label="Tabela de veículos"]')?.scrollLeft > 0);
    await regiao.evaluate(e => { e.scrollLeft = 0; });
    if (process.env.F5_CAPTURAS)
      await p.screenshot({ path: join(process.env.F5_CAPTURAS, 'f5-lista-320.png'), fullPage: true });
    await p.setViewportSize({ width: 1366, height: 980 });
  }

  // Uma leitura indisponível na lista agrupada não afeta outras fotos nem as ações.
  const falharLeitura = route => route.request().method() === 'GET'
    ? route.fulfill({ status: 503, json: { code: 'foto_indisponivel' }, headers: { 'access-control-allow-origin': origem } })
    : route.continue();
  const rotaFoto = `**/veiculos/${id}/foto`;
  const agrupar = p.getByRole('checkbox', { name: 'Agrupar por grupo' });
  await p.route(rotaFoto, falharLeitura);
  await agrupar.press('Space');
  await linha(placaFoto).getByText('Foto indisponível', { exact: true }).waitFor();
  await semFoto(`${prefixo}001`).waitFor();
  await miniatura(`${prefixo}003`).waitFor();
  assert.equal(await regiao.locator('th[scope=rowgroup][colspan="8"]').count(), 1);
  assert.ok(await botao(linha(placaFoto), 'Editar').isEnabled());
  await p.unroute(rotaFoto, falharLeitura);
  await agrupar.press('Space');
  await miniatura(placaFoto).waitFor();

  await acionar(botao(linha(placaFoto), 'Editar'));
  const img = p.getByRole('img', { name: `Foto do veículo ${placaFoto}` }).and(p.locator('img:not(app-foto-miniatura img)'));
  await img.waitFor();
  assert.match(await img.getAttribute('src'), /^blob:/);
  if (letra === 'A') {
    await conferirAcessibilidade(p, origem, worker, contexto);
    if (process.env.F5_CAPTURAS) {
      await mkdir(process.env.F5_CAPTURAS, { recursive: true });
      await p.screenshot({ path: join(process.env.F5_CAPTURAS, 'f5-edicao.png'), fullPage: true });
      await p.setViewportSize({ width: 320, height: 820 });
      await p.screenshot({ path: join(process.env.F5_CAPTURAS, 'f5-edicao-320.png'), fullPage: true });
      await p.setViewportSize({ width: 1366, height: 980 });
    }
  }
  const urlAnterior = await img.getAttribute('src');
  await campo(p, 'Selecionar arquivo').setInputFiles(fotoVerde);
  await Promise.all([resposta(p, 'PUT', foto, 204), acionar(botao(p, 'Substituir foto'))]);
  await mensagem(p, '[role=status]', 'Foto substituída.');
  await img.waitFor();
  assert.notEqual(await img.getAttribute('src'), urlAnterior);
  await acionar(botao(p, 'Cancelar'));
  const cor = await miniatura(placaFoto).evaluate(async i => {
    await i.decode();
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 1;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(i, 0, 0, 1, 1);
    return [...ctx.getImageData(0, 0, 1, 1).data];
  });
  assert.deepEqual(cor, [22, 101, 52, 255]);
  await acionar(botao(linha(placaFoto), 'Editar'));
  await img.waitFor();
  await Promise.all([resposta(p, 'DELETE', foto, 204), acionar(botao(p, 'Remover foto'))]);
  await mensagem(p, '[role=status]', 'Veículo sem foto.');
  await acionar(botao(p, 'Cancelar'));
  await semFoto(placaFoto).waitFor();
  await acionar(botao(linha(placaFoto), 'Editar'));
  await campo(p, 'Selecionar arquivo').setInputFiles(fotoAzul);
  await Promise.all([resposta(p, 'PUT', foto, 204), acionar(botao(p, 'Enviar foto'))]);
  await img.waitFor();
  await acionar(botao(p, 'Cancelar'));
  await miniatura(placaFoto).waitFor();
  assert.deepEqual(erros, []);
  console.log(`${origem}: J-02 por teclado; quatro resultados do cadastro, ID/ETag, miniaturas ativa/ausente/indisponível, lista agrupada e atualizada após edição.`);
  return { rota: `/veiculos/${id}/foto`, auth, p };
}

async function executar() {
  const raiz = process.env.LOCALAPPDATA ? join(process.env.LOCALAPPDATA, 'Temp', 'opencode') : tmpdir();
  const temporario = await mkdtemp(join(raiz, 'fase5-navegador-'));
  let contexto;
  try {
    const extensao = join(temporario, 'zoom');
    await mkdir(extensao);
    await writeFile(join(extensao, 'manifest.json'), JSON.stringify({
      manifest_version: 3, name: 'Verificação de zoom da Fase 5', version: '1.0.0',
      permissions: ['tabs'], background: { service_worker: 'worker.js' },
    }));
    await writeFile(join(extensao, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
    contexto = await chromium.launchPersistentContext(join(temporario, 'perfil'), {
      channel: 'chromium', executablePath: process.env.PLAYWRIGHT_EXECUTABLE || undefined,
      headless: true, locale: 'pt-BR', viewport: { width: 1366, height: 980 },
      args: [`--disable-extensions-except=${extensao}`, `--load-extension=${extensao}`, '--host-resolver-rules=MAP localhost 127.0.0.1'],
    });
    const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker', { timeout: 15000 });
    const a = await jornada(contexto, origens[0], 'A', worker);
    const b = await jornada(contexto, origens[1], 'B', worker);
    for (const [propria, outra] of [[a, b], [b, a]]) {
      assert.equal((await contexto.request.get(api + outra.rota, { headers: propria.auth })).status(), 404);
      assert.equal((await contexto.request.get(api + propria.rota, { headers: propria.auth })).status(), 200);
      await acionar(propria.p.getByRole('navigation', { name: 'Navegação principal' }).getByRole('link', { name: 'Painel', exact: true }));
      await acionar(botao(propria.p, 'Sair'));
      await propria.p.waitForURL('**/login');
      assert.equal((await contexto.request.get(api + propria.rota, { headers: propria.auth })).status(), 401);
    }
    const prototipo = await contexto.newPage();
    await prototipo.goto(pathToFileURL(resolve(__dirname, '../../openspec/changes/archive/2026-11-24-fase-5-fotos-veiculos/prototypes/frota-foto.html')).href);
    await prototipo.getByRole('columnheader', { name: 'Foto', exact: true }).waitFor();
    await prototipo.getByRole('img', { name: 'Foto do veículo FICTÍCIO01' }).waitFor();
    await prototipo.getByText('Sem foto', { exact: true }).waitFor();
    await acionar(botao(prototipo, 'Cadastrar'));
    await campo(prototipo, 'Selecionar arquivo').waitFor();
    const texto = 'JPEG ou PNG estático, até 16 megapixels.';
    await mensagem(prototipo, '#photo-help', texto);
    console.log('Isolamento A/B e revogação no proxy; miniaturas, seção e ajuda do protótipo conferidas.');
  } finally {
    await contexto?.close();
    await rm(temporario, { recursive: true, force: true });
  }
}
executar().catch(erro => { console.error(erro.stack); process.exitCode = 1; });
