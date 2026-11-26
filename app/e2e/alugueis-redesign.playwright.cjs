// Rodar com SPA :4200, API :5000 e SQL Server de desenvolvimento.
// Cria uma empresa sintética isolada, que permanece no banco local para conferência.
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { mkdtemp, mkdir, rm, writeFile } = require('node:fs/promises');
const { join } = require('node:path');
const { tmpdir } = require('node:os');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const api = 'http://localhost:5000/api/v1';
const origem = 'http://localhost:4200';
const mensagemCnh = 'A CNH vence antes da data do retorno previsto.';
function dataLocal(instante) {
  const partes = new Intl.DateTimeFormat('pt-BR', {
    timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(instante);
  const { day, month, year, hour, minute } = Object.fromEntries(partes.map(p => [p.type, p.value]));
  return `${day}/${month}/${year} ${hour}:${minute}`;
}

async function verificar() {
  const pasta = process.env.ZOOM_NATIVO ? await mkdtemp(join(process.env.LOCALAPPDATA
    ? join(process.env.LOCALAPPDATA, 'Temp', 'opencode') : tmpdir(), 'alugueis-zoom-')) : undefined;
  let contexto, browser;
  try {
    if (pasta) {
      const extensao = join(pasta, 'ext');
      await mkdir(extensao);
      await writeFile(join(extensao, 'manifest.json'), JSON.stringify({
        name: 'Zoom do ensaio local', version: '1.0', manifest_version: 3, permissions: ['tabs'],
        background: { service_worker: 'worker.js' },
      }));
      await writeFile(join(extensao, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
      contexto = await chromium.launchPersistentContext(join(pasta, 'perfil'), {
        executablePath: chromium.executablePath(), headless: true, viewport: null, locale: 'pt-BR',
        args: ['--window-size=1440,900', `--disable-extensions-except=${extensao}`, `--load-extension=${extensao}`],
      });
    } else {
      browser = await chromium.launch();
      contexto = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'pt-BR' });
    }
    const p = contexto.pages()[0] ?? await contexto.newPage();
    p.setDefaultTimeout(10000);
    const erros = [];
    p.on('pageerror', erro => erros.push(erro.message));
    const btn = name => p.getByRole('button', { name, exact: true });
    const texto = (seletor, hasText) => p.locator(seletor).filter({ hasText }).waitFor();
    const foco = seletor => p.waitForFunction(s => document.activeElement?.matches(s), seletor);
    const contorno = (alvo, esperado) => alvo.evaluate(e => getComputedStyle(e).outlineStyle).then(estilo =>
      assert.equal(estilo, esperado, 'contorno de ' + alvo));
    async function contraste(alvo) {
      const razao = await alvo.evaluate(e => {
        const cs = getComputedStyle(e);
        const luminancia = cor => cor.match(/\d+/g).map(Number).slice(0, 3).map(n => n / 255)
          .map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4)
          .reduce((l, n, i) => l + n * [.2126, .7152, .0722][i], 0);
        const cores = [luminancia(cs.color), luminancia(cs.backgroundColor)].sort((a, b) => b - a);
        return (cores[0] + .05) / (cores[1] + .05);
      });
      assert.ok(razao >= 4.5, `contraste insuficiente: ${razao}`);
      return razao.toFixed(2);
    }
    const resposta = (rota, metodo, status) => p.waitForResponse(r => r.url() === api + rota &&
      r.request().method() === metodo && r.status() === status);
    const clicar = (rota, metodo, status, botao) =>
      Promise.all([resposta(rota, metodo, status), botao.click()]).then(([r]) => r);

    const username = `redesign-alugueis-${randomUUID().slice(0, 8)}`;
    const password = randomUUID();
    let token;
    async function enviar(rota, method, status, data, extras = {}) {
      const headers = { ...extras, ...(token ? { Authorization: `Bearer ${token}` } : {}) };
      const r = await contexto.request.fetch(api + rota, { method, headers, data });
      assert.equal(r.status(), status, `${method} ${rota}`);
      return r;
    }
    const post = (rota, data) => enviar(rota, 'POST', 201, data).then(r => r.json());
    await post('/registro', { username, email: `${username}@example.invalid`, password });
    await p.goto(`${origem}/login`);
    await p.getByLabel('Usuário', { exact: true }).fill(username);
    await p.getByLabel('Senha', { exact: true }).fill(password);
    token = (await (await clicar('/session/login', 'POST', 200, btn('Entrar'))).json()).accessToken;
    await p.waitForURL(`${origem}/`);
    const { id: grupoId } = await post('/grupos', { nome: 'Compactos de conferência' });
    const plano = await post('/planos', { grupoId, modalidade: 'Livre', precoDiario: '100' });
    const veiculo = await post('/veiculos', { grupoId, placa: 'SIM0A08', marca: 'Aurora', modelo: 'Lume',
      cor: 'Prata', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50' });
    const endereco = { estado: 'SP', cidade: 'Cidade', bairro: 'Bairro', rua: 'Rua Exemplo', numero: '1' };
    const cliente = await post('/clientes', { tipo: 'PJ', nome: 'Empresa Exemplo', cnpj: '11222333000181', telefone: '0000', endereco });
    await post('/clientes', { tipo: 'PF', nome: 'Pessoa Exemplo', cpf: '12345678909', rg: 'RG-SIM', cnh: 'CNH-SIM',
      telefone: '0000', endereco });
    const condutor = (nome, cpf, validadeCnh) => post('/condutores', { clienteId: cliente.id, nome,
      email: `${cpf}@example.invalid`, cpf, cnh: `CNH-${nome}`, validadeCnh, telefone: '0000' });
    const vencido = await condutor('Vencida', '52998224725', '2000-01-01');
    const valido = await condutor('Válida', '11144477735', `${new Date().getUTCFullYear() + 2}-12-31`);
    const combustivel = '/configuracoes/combustiveis/Gasolina';
    const versao = await enviar(combustivel, 'GET', 200);
    await enviar(combustivel, 'PUT', 200, { preco: '6' }, { 'If-Match': versao.headers().etag });
    await p.getByRole('link', { name: 'Pessoas', exact: true }).click();
    const documentosPf = p.getByRole('row').filter({ hasText: 'Pessoa Exemplo' }).locator('.documentos-cliente span');
    await documentosPf.last().waitFor();
    assert.deepEqual(await documentosPf.allTextContents(), ['CPF 123.456.789-09', 'RG RG-SIM', 'CNH CNH-SIM']);
    assert.deepEqual(await documentosPf.evaluateAll(linhas => linhas.map(x => getComputedStyle(x).display)), ['block', 'block', 'block']);
    await p.getByRole('link', { name: 'Aluguéis', exact: true }).click();
    await texto('#resultado-alugueis', 'Nenhum aluguel encontrado');
    await contorno(p.locator('#titulo-alugueis'), 'none');
    await p.locator('#filtro-de').pressSequentially('10072027');
    assert.equal(await p.locator('#filtro-de').inputValue(), '10/07/2027');
    await btn('Filtrar').click();
    await foco('#resultado-alugueis');
    await contorno(p.locator('#resultado-alugueis'), 'none');
    await p.locator('#filtro-de').fill('');
    await btn('Filtrar').press('Enter');
    await foco('#resultado-alugueis');
    await contorno(p.locator('#resultado-alugueis'), 'solid');
    await btn('Nova contratação').click();
    for (const [campo, valor] of [['cliente', cliente.id], ['condutor', vencido.id],
      ['veiculo', veiculo.id], ['plano', plano.id]])
      await p.locator(`#aluguel-${campo}`).selectOption(valor);
    const previsto = dataLocal(Date.now() + 30 * 3600000);
    await p.locator('#aluguel-retorno').pressSequentially(previsto.replace(/\D/g, ''));
    assert.equal(await p.locator('#aluguel-retorno').inputValue(), previsto);
    await p.locator('#aluguel-hodometro').fill('66');
    await p.locator('#titulo-alugueis').click();
    await contorno(p.locator('#titulo-alugueis'), 'none');
    await clicar('/alugueis/estimativas', 'POST', 400, btn('Calcular estimativa'));
    await texto('#erro-aluguel-condutor', mensagemCnh);
    await foco('#erros-contratacao');
    assert.match(await p.locator('#erros-contratacao').ariaSnapshot(), /alert:[\s\S]*retorno previsto/);
    assert.equal(await btn('Confirmar contratação').isDisabled(), true);
    assert.equal((await (await enviar('/alugueis', 'GET', 200)).json()).total, 0);
    console.log('CNH: cadastro 201, estimativa 400, erro associado ao condutor, foco no resumo, zero aluguéis.');

    await p.locator('#aluguel-condutor').selectOption(valido.id);
    await clicar('/alugueis/estimativas', 'POST', 200, btn('Calcular estimativa'));
    await foco('#titulo-demonstrativo');
    await contorno(p.locator('#titulo-demonstrativo'), 'none');
    await p.locator('#aluguel-hodometro').fill('67');
    assert.equal(await btn('Confirmar contratação').isDisabled(), true);
    await btn('Sair').click();
    const sair = p.getByRole('dialog', { name: 'Sair do sistema?' });
    await sair.waitFor();
    await foco('#titulo-saida ~ div button[autofocus]');
    await contorno(sair.getByRole('button', { name: 'Cancelar' }), 'none');
    assert.match(await sair.ariaSnapshot(), /dialog "Sair do sistema\?"/);
    await p.keyboard.press('Escape');
    assert.equal(await p.locator('#aluguel-hodometro').inputValue(), '67');
    await foco('.botao-sair');
    await clicar('/alugueis/estimativas', 'POST', 200, btn('Calcular estimativa'));
    let encaminhar;
    const espera = new Promise(resolve => { encaminhar = resolve; });
    await p.route(api + '/alugueis', async route => {
      if (route.request().method() === 'POST') await espera;
      await route.continue();
    });
    const pedido = clicar('/alugueis', 'POST', 201, btn('Confirmar contratação'));
    try {
      await p.locator('.botao-sair[aria-disabled="true"]').waitFor();
      await texto('#situacao-saida', 'Aguarde a operação em andamento.');
    } finally { encaminhar(); }
    const criado = await (await pedido).json();
    await p.unroute(api + '/alugueis');
    await texto('#anuncio-alugueis', 'confirmada.');
    assert.match(await p.locator('#anuncio-alugueis').ariaSnapshot(), /status: Contratação/);
    await foco('#titulo-alugueis');
    await contorno(p.locator('#titulo-alugueis'), 'none');
    const contrasteAberto = await contraste(p.locator('.situacao-aluguel'));
    const rota = '/alugueis/' + criado.id;
    const rotaPlano = '/planos/' + plano.id;
    const atualPlano = await enviar(rotaPlano, 'GET', 200);
    await enviar(rotaPlano, 'PUT', 200, { precoDiario: '150' }, { 'If-Match': atualPlano.headers().etag });
    await btn('Editar aluguel').click();
    await texto('app-demonstrativo-aluguel', 'R$ 100,00');
    await p.locator('#aluguel-hodometro').fill('68');
    const descarte = p.getByRole('dialog', { name: 'Descartar alterações?' });
    await btn('Voltar ao contrato').press('Enter');
    await descarte.waitFor();
    await contorno(descarte.getByRole('button', { name: 'Cancelar' }), 'solid');
    await p.keyboard.press('Escape');
    assert.equal(await p.locator('#aluguel-hodometro').inputValue(), '68');
    await p.getByRole('link', { name: 'Início', exact: true }).click();
    await descarte.waitFor();
    await p.keyboard.press('Escape');
    await p.waitForURL(`${origem}/alugueis`);
    assert.equal(await p.locator('#aluguel-hodometro').inputValue(), '68');
    await p.locator('#aluguel-hodometro').fill('67');
    const editado = await (await clicar(rota, 'PUT', 200, btn('Salvar alterações'))).json();
    assert.equal(editado.saida, criado.saida);
    assert.equal(editado.estimativa.plano.precoDiario, criado.estimativa.plano.precoDiario);
    await btn('Registrar devolução').click();
    await p.locator('#devolucao-retornoReal').fill(previsto.replace(/\D/g, ''));
    assert.equal(await p.locator('#devolucao-retornoReal').inputValue(), previsto);
    await p.locator('#devolucao-hodometroFinal').fill('66');
    await p.locator('#devolucao-litrosRestantes').fill('45');
    await texto('#ajuda-devolucao-hodometroFinal', 'Quilometragem mínima: 67 km.');
    await btn('Calcular prévia').click();
    await texto('#erro-devolucao-hodometroFinal', 'O hodômetro final não pode ser inferior a 67 km.');
    assert.equal(await p.locator('#ajuda-devolucao-hodometroFinal').count(), 0);
    assert.equal(await p.locator('#devolucao-hodometroFinal').getAttribute('aria-describedby'), 'erro-devolucao-hodometroFinal');
    await p.locator('#devolucao-hodometroFinal').fill('100');
    await clicar(`${rota}/devolucao/previa`, 'POST', 200, btn('Calcular prévia'));
    await foco('#titulo-calculo-devolucao');
    await contorno(p.locator('#titulo-calculo-devolucao'), 'none');
    await p.locator('#devolucao-litrosRestantes').fill('44');
    assert.equal(await btn('Confirmar devolução').count(), 0);
    await clicar(`${rota}/devolucao/previa`, 'POST', 200, btn('Calcular prévia'));
    await clicar(`${rota}/devolucao`, 'POST', 200, btn('Confirmar devolução'));
    await texto('app-demonstrativo-devolucao', 'Demonstrativo final');
    const antes = await (await enviar(rota, 'GET', 200)).json();
    const contrasteConcluido = await contraste(p.locator('.situacao-aluguel.concluido'));
    console.log('Contratação 201, edição 200 e devolução 200; prévias invalidadas ao mudar entradas; diária e saída preservadas.');

    if (pasta) {
      const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker');
      const largura = await p.evaluate(() => innerWidth);
      const zoom = await worker.evaluate(async () => {
        const [aba] = await chrome.tabs.query({ active: true, currentWindow: true });
        await chrome.tabs.setZoom(aba.id, 2);
        return chrome.tabs.getZoom(aba.id);
      });
      assert.equal(zoom, 2);
      await p.waitForFunction(w => innerWidth <= w / 2 + 1, largura);
      console.log(`Zoom nativo: 200%, área CSS ${await p.evaluate(() => `${innerWidth}×${innerHeight}`)}.`);
    } else await p.setViewportSize({ width: 504, height: 300 });
    const ocultar = btn('Ocultar aluguel');
    await ocultar.click();
    const dialogo = p.getByRole('dialog', { name: 'Ocultar aluguel' });
    await dialogo.waitFor();
    await foco('#historico-ocultar ~ div button[autofocus]');
    await contorno(dialogo.getByRole('button', { name: 'Cancelar' }), 'none');
    await p.keyboard.press('Shift+Tab');
    await foco('#historico-ocultar ~ div button.excluir');
    await p.keyboard.press('Escape');
    await foco('button.perigo');
    assert.notEqual((await (await enviar(rota, 'GET', 200)).json()).oculto, true);
    await ocultar.click();
    await clicar(rota, 'DELETE', 204, dialogo.getByRole('button', { name: 'Ocultar aluguel' }));
    await texto('#resultado-alugueis', 'Nenhum aluguel encontrado');
    await p.getByRole('checkbox', { name: 'Incluir aluguéis ocultos' }).check();
    await btn('Filtrar').click();
    const consultar = btn('Consultar aluguel ' + criado.numeroExibicao);
    await consultar.waitFor();
    const medidas = await p.locator('.regiao-tabela').evaluate(e => ({
      pagina: document.documentElement.scrollWidth, viewport: document.documentElement.clientWidth,
      tabela: e.scrollWidth, regiao: e.clientWidth,
    }));
    assert.equal(medidas.pagina, medidas.viewport);
    assert.ok(medidas.tabela > medidas.regiao);
    await consultar.click();
    await texto('app-demonstrativo-devolucao', 'Demonstrativo final');
    const historico = await (await enviar(`${rota}?incluirOcultos=true`, 'GET', 200)).json();
    assert.equal(historico.oculto, true);
    assert.deepEqual(historico.final, antes.final);
    assert.deepEqual(erros, []);
    const contrasteOculto = await contraste(p.locator('.situacao-aluguel.oculto'));
    console.log(`Ocultação 204, Esc preservou dados e foco. Rolagem local: ${JSON.stringify(medidas)}. Contraste aberto/concluído/oculto: ${contrasteAberto}/${contrasteConcluido}/${contrasteOculto}:1.`);
  } finally {
    if (browser) await browser.close();
    else await contexto?.close();
    if (pasta) await rm(pasta, { recursive: true, force: true });
  }
}

verificar().catch(erro => { console.error(erro); process.exitCode = 1; });
