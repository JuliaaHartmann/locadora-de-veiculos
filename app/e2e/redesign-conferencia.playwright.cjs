// Verificação somente de interface: SPA local (:4200), HTTP controlado e nenhum dado persistido.
const assert = require('node:assert/strict');
const { mkdtemp, mkdir, rm, writeFile } = require('node:fs/promises');
const { join } = require('node:path');
const { tmpdir } = require('node:os');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

async function conferir() {
  const pasta = await mkdtemp(join(process.env.LOCALAPPDATA
    ? join(process.env.LOCALAPPDATA, 'Temp', 'opencode') : tmpdir(), 'redesign-conferencia-'));
  let contexto;
  try {
    const ext = join(pasta, 'ext');
    await mkdir(ext);
    await writeFile(join(ext, 'manifest.json'), JSON.stringify({
      name: 'Zoom da conferência', version: '1.0', manifest_version: 3, permissions: ['tabs'],
      background: { service_worker: 'worker.js' },
    }));
    await writeFile(join(ext, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
    contexto = await chromium.launchPersistentContext(join(pasta, 'perfil'), {
      executablePath: chromium.executablePath(), headless: true, viewport: null, locale: 'pt-BR',
      args: ['--window-size=1366,768', `--disable-extensions-except=${ext}`, `--load-extension=${ext}`],
    });
    const p = contexto.pages()[0] ?? await contexto.newPage();
    p.setDefaultTimeout(7000);
    const erros = [];
    p.on('pageerror', erro => erros.push(erro.message));
    const origem = 'http://localhost:4200';
    const pf = { id: 'pf', nome: 'Pessoa de conferência', tipo: 'PF', cpf: '12345678909', rg: 'SIM',
      cnh: 'CNH-SIM', telefone: '11900000000', numeroExibicao: '0001' };
    const pj = { id: 'pj', nome: 'X'.repeat(200), tipo: 'PJ', cnpj: '11222333000181', numeroExibicao: '0002' };
    const condutor = { id: 'condutor', nome: 'A'.repeat(200), clienteNome: pj.nome, cpf: pf.cpf,
      cnh: 'CNH-SEM-QUEBRA', validadeCnh: '2031-01-31', numeroExibicao: '0001' };
    const aluguel = { id: 'aluguel', numeroExibicao: '0001', estado: 'EmAberto', condutor: condutor.nome,
      placa: 'SIM0A01', marca: 'Aurora', modelo: 'Lume', plano: 'Controlado', parcelasConhecidas: '90',
      saida: '2026-11-26T09:00:00-03:00', retornoPrevisto: '2026-11-28T09:00:00-03:00' };
    const grupos = Array.from({ length: 15 }, (_, i) => ({
      id: `grupo-${i}`, numeroExibicao: String(i + 1).padStart(4, '0'), nome: `Grupo ${i + 1}`, planos: [],
    }));
    const pagina = (items = []) => ({ items, total: items.length, page: 1, pageSize: 20 });
    await contexto.route('**/api/v1/**', async r => {
      const caminho = new URL(r.request().url()).pathname;
      const headers = { etag: '"v"', 'access-control-allow-origin': origem, 'access-control-expose-headers': 'ETag' };
      if (caminho.endsWith('/logout')) return r.fulfill({ status: 503, headers, json: {} });
      let json = pagina();
      if (caminho.endsWith('/login')) json = { sessaoId: 'conferencia', accessToken: 'simulado',
        refreshToken: 'simulado', accessExpiraEm: '2099-01-01T00:00:00Z' };
      else if (caminho.endsWith('/session')) json = { ativa: true, sessaoId: 'conferencia' };
      else if (caminho.endsWith('/clientes')) json = pagina([pf, pj]);
      else if (caminho.endsWith('/vinculos-pj')) json = [];
      else if (caminho.endsWith('/condutores')) json = pagina([condutor]);
      else if (caminho.endsWith('/alugueis')) json = pagina([aluguel]);
      else if (caminho.endsWith('/grupos')) json = pagina(grupos);
      else if (caminho.endsWith('/planos')) json = pagina([{ id: 'plano', numeroExibicao: '0001',
        grupoNome: 'Grupo 1', modalidade: 'Livre', precoDiario: '100' }]);
      else if (caminho.endsWith('/veiculos')) json = pagina([{ id: 'veiculo', numeroExibicao: '0001',
        grupoNome: 'Grupo 1', placa: 'SIM0A01', marca: 'Aurora', modelo: 'Lume', cor: 'Cinza', combustivel: 'Gasolina' }]);
      else if (caminho.endsWith('/taxas-servicos')) json = pagina([
        { id: 'taxa', numeroExibicao: '0001', nome: 'Lavagem', preco: '25', modalidade: 'Fixa' },
      ]);
      await r.fulfill({ headers, json });
    });
    const papel = (role, name) => p.getByRole(role, { name, exact: true });
    const medidas = () => p.evaluate(() => ({ largura: document.documentElement.clientWidth,
      altura: innerHeight, conteudo: document.documentElement.scrollWidth }));
    const semOverflow = async () => {
      const m = await medidas();
      assert.equal(m.conteudo, m.largura, p.url());
      return m;
    };
    const contrasteTexto = async alvo => {
      const razao = await alvo.evaluate(e => {
        const luminancia = cor => cor.match(/\d+/g).slice(0, 3).map(n => Number(n) / 255)
          .map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4)
          .reduce((total, n, i) => total + n * [.2126, .7152, .0722][i], 0);
        const s = getComputedStyle(e);
        const [clara, escura] = [luminancia(s.color), luminancia(s.backgroundColor)].sort((a, b) => b - a);
        return (clara + .05) / (escura + .05);
      });
      assert.ok(razao >= 4.5, `${alvo}: ${razao}`);
      return razao.toFixed(2);
    };
    const focoVisivel = async alvo => {
      await p.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      const resultado = await alvo.evaluate(e => {
        const r = e.getBoundingClientRect();
        const s = getComputedStyle(e);
        return { foco: document.activeElement === e, atual: document.activeElement?.textContent?.trim(),
          contorno: s.outlineStyle, espessura: parseFloat(s.outlineWidth),
          limites: [r.top, r.bottom, r.left, r.right], janela: [innerHeight, innerWidth] };
      });
      const { foco, contorno, espessura, limites: [topo, base, esquerda, direita], janela: [altura, largura] } = resultado;
      assert.ok(foco && contorno === 'solid' && espessura > 0 && topo >= -1 && base <= altura + 1 &&
        esquerda >= -1 && direita <= largura + 1, `${alvo}: ${JSON.stringify(resultado)}`);
    };
    const autofill = async (seletor, autocomplete) => {
      const cdp = await contexto.newCDPSession(p);
      try {
        await cdp.send('DOM.enable');
        await cdp.send('CSS.enable');
        const { root } = await cdp.send('DOM.getDocument');
        const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector: seletor });
        assert.ok(nodeId, seletor);
        const campo = p.locator(seletor);
        const antes = await campo.evaluate(e => ({ valor: e.value, borda: getComputedStyle(e).borderColor }));
        if (autocomplete) assert.equal(await campo.getAttribute('autocomplete'), autocomplete);
        await cdp.send('CSS.forcePseudoState', { nodeId, forcedPseudoClasses: ['autofill'] });
        const depois = await campo.evaluate(e => ({ valor: e.value, borda: getComputedStyle(e).borderColor,
          sombra: getComputedStyle(e).boxShadow }));
        assert.match(depois.sombra, /rgb\(255, 255, 255\).*100px inset/);
        assert.deepEqual({ valor: depois.valor, borda: depois.borda }, antes);
      } finally { await cdp.detach(); }
    };
    const dadosInteiros = async (regiao, seletores) => {
      for (const seletor of seletores) {
        const dados = regiao.locator(seletor);
        assert.ok(await dados.count() > 0, seletor);
        assert.ok(await dados.evaluateAll(es => es.every(e => getComputedStyle(e).whiteSpace === 'nowrap')), seletor);
      }
    };
    await p.goto(`${origem}/login`);
    if (process.argv.includes('--acoes-duplas')) {
      await p.locator('#usuario').fill('conferencia-local');
      await p.locator('#senha').fill('somente-simulacao');
      await papel('button', 'Entrar').click();
      await papel('heading', 'Início').waitFor();
      const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker');
      for (const zoom of [1, 2]) {
        assert.equal(await worker.evaluate(async fator => {
          const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
          await chrome.tabs.setZoom(tab.id, fator);
          return chrome.tabs.getZoom(tab.id);
        }, zoom), zoom);
        const resultados = {};
        const medir = async nome => {
          const regiao = papel('region', nome);
          const botoes = regiao.locator('tbody .acoes-tabela').first().getByRole('button');
          await botoes.last().waitFor();
          const direita = async nomeBotao => papel('button', nomeBotao).evaluate(e => e.getBoundingClientRect().right);
          await semOverflow();
          const m = await regiao.evaluate(e => {
            e.scrollLeft = e.scrollWidth;
            const tabela = e.querySelector('table').getBoundingClientRect();
            const coluna = e.querySelector('thead th:last-child').getBoundingClientRect();
            const [editar, excluir] = [...e.querySelector('tbody .acoes-tabela').children].map(b => b.getBoundingClientRect());
            return { largura: coluna.width, recuo: tabela.right - excluir.right, excluir: excluir.right,
              botoes: [editar.width, excluir.width], intervalo: excluir.left - editar.right,
              desvio: Math.abs((coluna.left + coluna.right - editar.left - excluir.right) / 2),
              tabela: [e.clientWidth, e.scrollWidth] };
          });
          const bordas = await Promise.all(['Cadastrar', 'Próxima'].map(direita));
          assert.ok(bordas.every(x => Math.abs(x - m.excluir) < .5), JSON.stringify({ m, bordas }));
          assert.ok(m.desvio < .5 && Math.abs(m.recuo - 20) < .5);
          assert.deepEqual(m.botoes, [36, 36]);
          assert.ok(Math.abs(m.intervalo - 4.8) < .1);
          resultados[nome] = m;
        };
        await papel('link', 'Frota').click();
        await medir('Tabela de grupos');
        await papel('button', 'Planos').click();
        await medir('Tabela de planos');
        await papel('button', 'Veículos').click();
        await medir('Tabela de veículos');
        await papel('link', 'Pessoas').click();
        await papel('tab', 'Condutores').click();
        await medir('Tabela de condutores');
        await papel('link', 'Preços').click();
        await medir('Tabela de taxas cadastradas');
        console.log(JSON.stringify({ zoom, janela: await medidas(), resultados }));
      }
      assert.deepEqual(erros, []);
      return;
    }
    await papel('heading', 'Entrar').waitFor();
    assert.equal(await p.title(), 'LocaCar');
    assert.equal(await p.locator('link[rel="icon"]').getAttribute('href'), 'locacar-monograma.svg');
    await autofill('#usuario', 'username');
    await p.goto(`${origem}/registro`);
    await p.locator('input[type=email]').waitFor();
    await autofill('input[type=email]', 'email');
    await p.goto(`${origem}/login`);
    await p.locator('#usuario').fill('conferencia-local');
    await p.locator('#senha').fill('somente-simulacao');
    await papel('button', 'Entrar').click();
    await papel('heading', 'Início').waitFor();
    const antes = await semOverflow();
    const sair = papel('button', 'Sair');
    const posicaoSair = () => sair.evaluate(e => {
      const r = e.getBoundingClientRect();
      return { topo: r.top, base: r.bottom, altura: innerHeight, pagina: scrollY,
        posicao: getComputedStyle(e.closest('aside')).position };
    });
    const acoes = tabela => tabela.evaluate(e => {
      const coluna = e.querySelector('thead th:last-child').getBoundingClientRect();
      const botoes = Array.from(e.querySelector('tbody tr:first-child .acoes-tabela').children);
      const inicio = botoes[0].getBoundingClientRect();
      const fim = botoes.at(-1).getBoundingClientRect();
      return { recuo: e.getBoundingClientRect().right - fim.right,
        desvio: Math.abs((coluna.left + coluna.right - inicio.left - fim.right) / 2) };
    });
    await papel('link', 'Frota').click();
    const tabelaFrota = papel('region', 'Tabela de grupos');
    await tabelaFrota.waitFor();
    const acoesFrota = await acoes(tabelaFrota);
    const sairAntes = await posicaoSair();
    await p.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
    await p.waitForFunction(() => scrollY > 0);
    const sairDepois = await posicaoSair();
    assert.equal(sairDepois.posicao, 'sticky');
    assert.ok(Math.abs(sairDepois.topo - sairAntes.topo) < 1);
    assert.ok(sairDepois.topo >= 0 && sairDepois.base <= sairDepois.altura);
    await sair.focus();
    await p.keyboard.press('Shift+Tab');
    await p.keyboard.press('Tab');
    await focoVisivel(sair);
    await papel('link', 'Preços').click();
    const tabelaTaxas = papel('region', 'Tabela de taxas cadastradas');
    await tabelaTaxas.waitFor();
    const acoesPrecos = await acoes(tabelaTaxas);
    assert.ok(acoesPrecos.desvio < 1 && acoesFrota.desvio < 1);
    console.log(JSON.stringify({ bloco: 'Desktop', sairAntes, sairDepois, acoesFrota, acoesPrecos }));
    const worker = contexto.serviceWorkers()[0] ?? await contexto.waitForEvent('serviceworker');
    const zoom = await worker.evaluate(async () => {
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      await chrome.tabs.setZoom(tab.id, 2);
      return chrome.tabs.getZoom(tab.id);
    });
    assert.equal(zoom, 2);
    await p.waitForFunction(w => document.documentElement.clientWidth < w, antes.largura);

    await papel('link', 'Pessoas').click();
    const tabela = papel('region', 'Tabela de clientes');
    await tabela.waitFor();
    const pessoas = await semOverflow();
    assert.equal((await posicaoSair()).posicao, 'static');
    const rolagem = await tabela.evaluate(e => [e.clientWidth, e.scrollWidth, e.tabIndex]);
    assert.ok(rolagem[0] < rolagem[1] && rolagem[2] === 0);
    const abaCondutores = papel('tab', 'Condutores');
    await abaCondutores.click();
    assert.equal(await abaCondutores.evaluate(e => getComputedStyle(e).outlineStyle), 'none');
    const tabelaCondutores = papel('region', 'Tabela de condutores');
    await tabelaCondutores.waitFor();
    await dadosInteiros(tabelaCondutores, ['thead th', '.dados-condutor', '.acoes-tabela button']);
    assert.equal(await tabelaCondutores.getByRole('cell', { name: condutor.nome, exact: true }).count(), 1);
    await semOverflow();
    await p.keyboard.press('ArrowLeft');
    await focoVisivel(papel('tab', 'Clientes'));
    const contrasteAba = await contrasteTexto(papel('tab', 'Clientes'));
    assert.match(await tabela.ariaSnapshot(), /columnheader "Código"[\s\S]*0001/);
    assert.equal(await p.getByText('(11) 90000-0000', { exact: true }).count(), 1);
    const vinculos = papel('button', `Vínculos PJ de ${pf.nome}`);
    await vinculos.press('Enter');
    const titulo = papel('heading', `Vínculos PJ — ${pf.nome}`);
    await titulo.waitFor();
    assert.equal(await titulo.evaluate(e => e === document.activeElement), true);
    await p.keyboard.press('Tab');
    const escolha = papel('checkbox', pj.nome);
    await focoVisivel(escolha);
    await p.keyboard.press('Space');
    assert.equal(await escolha.isChecked(), true);
    await semOverflow();
    await papel('button', 'Cancelar').press('Enter');
    assert.equal(await vinculos.evaluate(e => e === document.activeElement), true);
    await tabela.getByRole('button', { name: `Usar como condutor: ${pf.nome}` }).click();
    await p.locator('#condutor-email').waitFor();
    await autofill('#condutor-email');
    assert.equal(await p.locator('#condutor-email').getAttribute('required'), null);
    console.log(JSON.stringify({ bloco: 'Pessoas', zoom, antes, pessoas, rolagem, contrasteAba }));

    await papel('link', 'Aluguéis').click();
    const nova = papel('button', 'Nova contratação');
    await nova.waitFor();
    const tabelaAlugueis = papel('region', 'Tabela de aluguéis');
    await tabelaAlugueis.waitFor();
    await dadosInteiros(tabelaAlugueis, ['thead th', '.codigo-aluguel', 'td:nth-child(3) strong',
      'td:nth-child(4)', 'td:nth-child(5)', 'td:nth-child(6)', '.acoes-tabela button']);
    assert.equal(await tabelaAlugueis.getByRole('cell', { name: condutor.nome, exact: true }).count(), 1);
    await papel('button', 'Sair').press('Enter');
    const aviso = p.getByRole('alert').filter({ hasText: 'Não foi possível confirmar a revogação remota' });
    await aviso.waitFor();
    const contrasteAlerta = await contrasteTexto(aviso);
    assert.equal(await nova.isDisabled(), true);
    const recuperar = papel('button', 'Entrar novamente');
    // O link de salto é a primeira parada depois do menu.
    await p.keyboard.press('Tab');
    await focoVisivel(papel('link', 'Ir para o conteúdo de Aluguéis'));
    await p.keyboard.press('Tab');
    await focoVisivel(recuperar);
    const alugueis = await semOverflow();
    await p.keyboard.press('Enter');
    await p.waitForURL(`${origem}/login`);
    assert.deepEqual(erros, []);
    console.log(JSON.stringify({ bloco: 'Aluguéis', zoom, alugueis, contrasteAlerta, destino: '/login' }));
  } finally {
    await contexto?.close();
    await rm(pasta, { recursive: true, force: true });
  }
}

conferir().catch(erro => { console.error(erro); process.exitCode = 1; });
