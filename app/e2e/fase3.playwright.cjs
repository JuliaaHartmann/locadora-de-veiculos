// Executa contra a SPA e a API reais com o SQL isolado do script Testar-Integracao.ps1.
const assert = require('node:assert/strict');
const { mkdtemp, mkdir, rm, writeFile } = require('node:fs/promises');
const { join } = require('node:path');
const { tmpdir } = require('node:os');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const origem = 'http://localhost:4201';
const api = 'http://localhost:5001/api/v1';
const text = (p, s) => p.locator(s).innerText();
const btn = (p, name) => p.getByRole('button', { name, exact: true });
const partesRetorno = Object.fromEntries(new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo',
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
  .formatToParts(new Date(Date.now() + 26 * 3600000)).map(p => [p.type, p.value]));
const retorno = `${partesRetorno.day}/${partesRetorno.month}/${partesRetorno.year} ${partesRetorno.hour}:${partesRetorno.minute}`;

async function esperou(p, seletor, esperado) {
  await p.locator(seletor).filter({ hasText: esperado }).waitFor();
}

async function contraste(alvo) {
  const razao = await alvo.evaluate(el => {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
    const ctx = canvas.getContext('2d');
    function canais(cor) {
      ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = cor; ctx.fillRect(0, 0, 1, 1);
      return [...ctx.getImageData(0, 0, 1, 1).data];
    }
    const lum = ([r, g, b]) => [r, g, b].map(x => x / 255)
      .map(x => x <= .04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4)
      .reduce((a, x, i) => a + x * [.2126, .7152, .0722][i], 0);
    let fundo = [255, 255, 255, 255];
    for (let no = el; no; no = no.parentElement) {
      const cor = canais(getComputedStyle(no).backgroundColor);
      if (cor[3] === 255) { fundo = cor; break; }
    }
    const [escuro, claro] = [lum(canais(getComputedStyle(el).color)), lum(fundo)].sort((a, b) => a - b);
    return (claro + .05) / (escuro + .05);
  });
  assert.ok(razao >= 4.5, `Contraste observado abaixo de 4,5:1 (${razao.toFixed(2)}).`);
}

async function jornada(contexto, extensao) {
  const page = await contexto.newPage();
  page.setDefaultTimeout(12000);
  const erros = [];
  page.on('pageerror', e => erros.push(e.message));
  const dados = await preparar(contexto);
  const { veiculo, segundo, cliente, condutor, plano, diario, livre, taxa, requisitar, condutorCorpo } = dados;
  await page.goto(`${origem}/alugueis`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForURL(`${origem}/login`);
  await page.locator('#usuario').fill(dados.usuario);
  await page.locator('#senha').fill(dados.senha);
  const [login] = await Promise.all([
    page.waitForResponse(r => r.url() === `${api}/session/login` && r.request().method() === 'POST'),
    btn(page, 'Entrar').click(),
  ]);
  assert.equal(login.status(), 200);
  await page.waitForURL(`${origem}/`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('link', { name: 'Aluguéis', exact: true }).first().focus();
  await page.keyboard.press('Enter');
  await page.waitForURL(`${origem}/alugueis`);
  await esperou(page, '#resultado-alugueis', 'Nenhum aluguel em aberto encontrado');

  await btn(page, 'Nova contratação').click();
  await page.locator('#aluguel-cliente').selectOption(cliente.id);
  await page.locator('#aluguel-condutor').selectOption(condutor.id);
  await page.locator('#aluguel-veiculo').selectOption(veiculo.id);
  await page.locator('#aluguel-plano').selectOption(plano.id);
  await page.locator('#aluguel-retorno').fill('01/01/2000 12:00');
  await page.locator('#aluguel-hodometro').fill('21430');
  await btn(page, 'Calcular estimativa').click();
  await esperou(page, '#erro-aluguel-retorno', 'posterior à saída');
  assert.equal(await page.evaluate(() => document.activeElement.id), 'erros-contratacao');
  await contraste(page.locator('#erro-aluguel-retorno'));
  await page.locator('#aluguel-retorno').fill(retorno);
  await page.getByRole('checkbox', { name: /Higienização/ }).check();
  await page.getByRole('checkbox', { name: /Seguro do cliente/ }).check();
  await page.getByRole('checkbox', { name: /Seguro de terceiros/ }).check();
  await btn(page, 'Calcular estimativa').click();
  await page.getByRole('alert').filter({ hasText: 'Configure o preço de seguro de terceiros' }).waitFor();
  await page.getByRole('checkbox', { name: /Seguro de terceiros/ }).uncheck();
  await page.locator('#aluguel-plano').selectOption(diario.id);
  await btn(page, 'Calcular estimativa').click();
  await page.getByText('Pendente', { exact: true }).waitFor();
  assert.match(await text(page, 'app-demonstrativo-aluguel'), /Plano Diário/);
  await page.locator('#aluguel-plano').selectOption(livre.id);
  await btn(page, 'Calcular estimativa').click();
  await page.getByText('Plano Livre', { exact: false }).waitFor();
  assert.doesNotMatch(await text(page, 'app-demonstrativo-aluguel'), /Cobrança por quilômetro/);
  await page.locator('#aluguel-plano').selectOption(plano.id);
  await btn(page, 'Calcular estimativa').click();
  await page.getByRole('heading', { name: 'Estimativa da contratação' }).waitFor();
  await page.getByText('Pendente', { exact: true }).waitFor();
  assert.equal(await page.evaluate(() => document.activeElement.id), 'titulo-demonstrativo');
  assert.match(await text(page, 'app-demonstrativo-aluguel'), /R\$ 1\.000,00/);
  assert.match(await text(page, 'app-demonstrativo-aluguel'), /200 km de franquia/);
  await contraste(btn(page, 'Confirmar contratação'));
  await contraste(page.getByText('Pendente', { exact: true }));
  if (process.env.F3_CAPTURAS)
    await page.screenshot({ path: join(process.env.F3_CAPTURAS, 'fase3-estimativa-spa.png'), fullPage: true });
  assert.equal((await requisitar.get('/alugueis')).total, 0);

  const chaves = [];
  await page.route('**/api/v1/alugueis', async route => {
    if (route.request().method() !== 'POST') { await route.continue(); return; }
    chaves.push((await route.request().allHeaders())['idempotency-key']);
    if (chaves.length === 1) {
      const real = await route.fetch();
      assert.equal(real.status(), 201);
      await route.abort('failed');
    } else await route.continue();
  });
  await btn(page, 'Confirmar contratação').click();
  await esperou(page, '#erros-contratacao', 'Resultado da contratação não confirmado');
  await btn(page, 'Repetir confirmação').click();
  await page.getByRole('heading', { name: 'Demonstrativo estimado' }).waitFor();
  assert.ok(chaves[0]); assert.deepEqual(chaves, [chaves[0], chaves[0]]);
  await page.unroute('**/api/v1/alugueis');
  const lista = await requisitar.get('/alugueis');
  assert.equal(lista.total, 1);
  const id = lista.items[0].id;
  assert.equal((await requisitar.get('/veiculos?disponiveis=true')).total, 1);

  await btn(page, 'Editar aluguel').click();
  await page.locator('#aluguel-hodometro').waitFor();
  const saida = lista.items[0].saida;
  const anterior = await requisitar.detalhe(`/alugueis/${id}`);
  await requisitar.put(`/alugueis/${id}`, {
    clienteId: cliente.id, condutorId: condutor.id, veiculoId: veiculo.id, planoId: plano.id,
    retornoPrevisto: anterior.corpo.retornoPrevisto, hodometroInicial: 21500, taxaIds: [taxa.id], seguros: ['Cliente'],
  }, anterior.etag);
  await page.locator('#aluguel-hodometro').fill('21600');
  await btn(page, 'Salvar alterações').click();
  await esperou(page, '#erros-contratacao', 'A edição não foi gravada');
  assert.equal(await page.locator('#aluguel-hodometro').inputValue(), '21600');
  await btn(page, 'Recarregar contrato').click();
  await page.waitForFunction(() => document.querySelector('#aluguel-hodometro')?.value === '21500');
  assert.equal(await page.locator('#aluguel-hodometro').inputValue(), '21500');
  await page.locator('#aluguel-veiculo').selectOption(segundo.id);
  await btn(page, 'Salvar alterações').click();
  await page.getByRole('heading', { name: 'Demonstrativo estimado' }).waitFor();
  const revisado = (await requisitar.detalhe(`/alugueis/${id}`)).corpo;
  assert.equal(revisado.saida, saida); assert.equal(revisado.veiculo.id, segundo.id);
  assert.equal((await requisitar.get('/veiculos?disponiveis=true')).items[0].id, veiculo.id);

  const verificarReflow = async () => assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await page.setViewportSize({ width: 320, height: 780 });
  await verificarReflow();
  await btn(page, 'Voltar aos aluguéis').click();
  await page.getByRole('region', { name: 'Tabela de aluguéis em aberto' }).waitFor();
  await verificarReflow();
  await page.getByRole('region', { name: 'Tabela de aluguéis em aberto' }).focus();
  assert.ok(await page.evaluate(() => parseFloat(getComputedStyle(document.activeElement).outlineWidth) >= 2));
  if (extensao) {
    await page.setViewportSize({ width: 840, height: 900 });
    const cdp = await contexto.newCDPSession(page);
    await cdp.send('Emulation.clearDeviceMetricsOverride');
    const larguraSemZoom = await page.evaluate(() => innerWidth);
    const zoom = await extensao.evaluate(async url => {
      const aba = (await chrome.tabs.query({})).find(x => x.url === url);
      await chrome.tabs.setZoom(aba.id, 2);
      return chrome.tabs.getZoom(aba.id);
    }, `${origem}/alugueis`);
    assert.equal(zoom, 2);
    await page.waitForFunction(largura => innerWidth < largura * .65, larguraSemZoom);
    await verificarReflow();
    await page.getByRole('button', { name: `Consultar aluguel ${id}` }).click();
    await page.getByRole('heading', { name: 'Demonstrativo estimado' }).waitFor();
    await verificarReflow();
    await btn(page, 'Editar aluguel').click();
    await page.locator('#aluguel-hodometro').waitFor();
    await verificarReflow();
    await extensao.evaluate(async url => {
      const aba = (await chrome.tabs.query({})).find(x => x.url === url);
      await chrome.tabs.setZoom(aba.id, 1);
    }, `${origem}/alugueis`);
    await cdp.detach();
  }
  await page.setViewportSize({ width: 1366, height: 1000 });
  await page.getByRole('link', { name: 'Frota', exact: true }).click();
  await btn(page, 'Veículos').click();
  const linhaVeiculo = page.getByRole('row').filter({ hasText: segundo.placa });
  await linhaVeiculo.getByRole('button', { name: 'Editar' }).click();
  await page.getByRole('alert').filter({ hasText: 'não pode ser editado' }).waitFor();
  await linhaVeiculo.getByRole('button', { name: 'Excluir' }).click();
  await page.getByRole('alert').filter({ hasText: 'não pode ser excluído' }).waitFor();
  assert.equal(await page.evaluate(() => document.activeElement.getAttribute('role')), 'alert');

  await page.getByRole('link', { name: 'Pessoas', exact: true }).click();
  await btn(page, 'Condutores').click();
  await page.getByRole('row').filter({ hasText: condutorCorpo.nome }).getByRole('button', { name: 'Editar' }).click();
  await page.getByLabel('CNH', { exact: true }).fill('CNH-NOVA-FICTICIA');
  await btn(page, 'Salvar cadastro').click();
  await page.getByRole('alert').filter({ hasText: 'enquanto houver aluguel em aberto' }).waitFor();
  assert.equal(await page.getByLabel('CNH', { exact: true }).inputValue(), 'CNH-NOVA-FICTICIA');

  await page.getByRole('link', { name: 'Preços', exact: true }).click();
  const linhaTaxa = page.getByRole('row').filter({ hasText: taxa.nome });
  await linhaTaxa.getByRole('button', { name: 'Editar' }).click();
  await page.getByRole('alert').filter({ hasText: 'não pode ser editada' }).waitFor();
  await linhaTaxa.getByRole('button', { name: 'Excluir' }).click();
  await page.getByRole('alert').filter({ hasText: 'não pode ser excluída' }).waitFor();
  assert.deepEqual(erros, []);
  console.log(`J-03: prévia sem ocupação, replay idempotente, edição/ETag, troca, bloqueios em três módulos, teclado, contraste amostral e reflow a 320 px${extensao ? ', zoom nativo 200%' : ''}.`);
}

async function preparar(contexto) {
  const usuario = `f3-${Date.now()}`, senha = 'exemplo-sintetico';
  const corpo = { username: usuario, email: `${usuario}@example.invalid`, password: senha };
  const reg = await contexto.request.post(`${api}/registro`, { data: corpo });
  assert.equal(reg.status(), 201);
  const login = await contexto.request.post(`${api}/session/login`, { data: corpo });
  assert.equal(login.status(), 200);
  const autorizacao = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  async function requisicao(metodo, caminho, esperado, data, etag) {
    const resposta = await contexto.request.fetch(`${api}${caminho}`, {
      method: metodo, data, headers: { ...autorizacao, ...(etag ? { 'If-Match': etag } : {}) },
    });
    assert.equal(resposta.status(), esperado, `${metodo} ${caminho}: ${await resposta.text()}`);
    return { corpo: await resposta.json(), etag: resposta.headers().etag };
  }
  const criar = async (rota, corpo) => (await requisicao('POST', rota, 201, corpo)).corpo;
  const grupo = await criar('/grupos', { nome: 'Compactos de teste' });
  const plano = await criar('/planos', { grupoId: grupo.id, modalidade: 'Controlado', precoDiario: '145',
    precoKm: '2.5', franquiaDiariaKm: 100 });
  const diario = await criar('/planos', { grupoId: grupo.id, modalidade: 'Diario', precoDiario: '120', precoKm: '2' });
  const livre = await criar('/planos', { grupoId: grupo.id, modalidade: 'Livre', precoDiario: '180' });
  const automovel = { grupoId: grupo.id, marca: 'Aurora', modelo: 'Lume', cor: 'Branca', ano: 2026,
    combustivel: 'Gasolina', capacidadeLitros: '50' };
  const veiculo = await criar('/veiculos', { ...automovel, placa: 'FICTESTE1' });
  const segundo = await criar('/veiculos', { ...automovel, placa: 'FICTESTE2' });
  const cliente = await criar('/clientes', { tipo: 'PJ', nome: 'Empresa Exemplo', cnpj: '11222333000181',
    telefone: '(00) 90000-0000', endereco: { estado: 'SP', cidade: 'Cidade', bairro: 'Bairro', rua: 'Rua', numero: '1' } });
  const condutorCorpo = { clienteId: cliente.id, nome: 'Condutor Exemplo', email: 'condutor@example.invalid',
    cpf: '52998224725', cnh: 'CNH-EXEMPLO', validadeCnh: `${new Date().getUTCFullYear() + 2}-12-31`, telefone: '(00) 90000-0000' };
  const condutor = await criar('/condutores', condutorCorpo);
  const taxa = await criar('/taxas-servicos', { nome: 'Higienização', modalidade: 'Fixa', preco: '30' });
  const seguro = await requisicao('GET', '/configuracoes/seguros/Cliente', 200);
  await requisicao('PUT', '/configuracoes/seguros/Cliente', 200, { precoDiario: '0' }, seguro.etag);
  const requisitar = {
    detalhe: rota => requisicao('GET', rota, 200),
    get: async rota => (await requisicao('GET', rota, 200)).corpo,
    put: (rota, dados, etag) => requisicao('PUT', rota, 200, dados, etag),
  };
  return { usuario, senha, cliente, condutor, condutorCorpo, veiculo, segundo, plano, diario, livre, taxa, requisitar };
}

async function executar() {
  const raiz = process.env.LOCALAPPDATA ? join(process.env.LOCALAPPDATA, 'Temp', 'opencode') : tmpdir();
  const temporario = await mkdtemp(join(raiz, 'fase3-navegador-'));
  let contexto;
  try {
    const pasta = join(temporario, 'zoom');
    await mkdir(pasta);
    await writeFile(join(pasta, 'manifest.json'), JSON.stringify({
      manifest_version: 3, name: 'Verificação sintética de zoom', version: '1.0.0',
      permissions: ['tabs'], background: { service_worker: 'worker.js' },
    }));
    await writeFile(join(pasta, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
    contexto = await chromium.launchPersistentContext(join(temporario, 'perfil'), {
      channel: 'chromium',
      executablePath: process.env.PLAYWRIGHT_EXECUTABLE || undefined,
      headless: true, locale: 'en-US', viewport: { width: 1366, height: 1000 },
      args: [`--disable-extensions-except=${pasta}`, `--load-extension=${pasta}`],
    });
    const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker', { timeout: 12000 });
    await jornada(contexto, worker);
  } finally {
    await contexto?.close();
    await rm(temporario, { recursive: true, force: true });
  }
}

executar().catch(error => { console.error(error.stack); process.exitCode = 1; });
