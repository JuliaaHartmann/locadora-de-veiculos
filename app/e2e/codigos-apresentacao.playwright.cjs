// Recorte visual dos códigos; usar -JornadaCodigos para API e SQL Server exclusivos.
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

async function verificar() {
  const browser = await chromium.launch();
  try {
    const contexto = await browser.newContext({ locale: 'pt-BR' });
    const pagina = await contexto.newPage();
    pagina.setDefaultTimeout(10000);
    const erros = [];
    pagina.on('pageerror', erro => erros.push(erro.message));
    let token;
    const api = 'http://localhost:5001/api/v1';
    async function enviar(rota, method, status, data, adicionais = {}) {
      const resposta = await contexto.request.fetch(api + rota, {
        method, data, headers: { ...adicionais, ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      });
      assert.equal(resposta.status(), status, `${method} ${rota}: ${resposta.status() === status ? '' : await resposta.text()}`);
      return resposta;
    }
    async function criar(rota, dados, codigo = '0001', headers = {}) {
      const resposta = await enviar(rota, 'POST', 201, dados, headers);
      const corpo = await resposta.json();
      assert.equal(corpo.numeroExibicao, codigo);
      assert.match(corpo.id, /^[0-9a-f-]{36}$/i);
      return { ...corpo, etag: resposta.headers().etag };
    }
    const nome = `codigos-${randomUUID().slice(0, 8)}`;
    const senha = randomUUID();
    await enviar('/registro', 'POST', 201, { username: nome, email: `${nome}@example.invalid`, password: senha });
    await pagina.goto('http://127.0.0.1:4201/login');
    await pagina.getByLabel('Usuário', { exact: true }).fill(nome);
    await pagina.getByLabel('Senha', { exact: true }).fill(senha);
    const [login] = await Promise.all([
      pagina.waitForResponse(r => r.url() === api + '/session/login' && r.status() === 200),
      pagina.getByRole('button', { name: 'Entrar', exact: true }).click(),
    ]);
    token = (await login.json()).accessToken;

    const grupo = await criar('/grupos', { nome: 'Compactos' });
    const plano = await criar('/planos', { grupoId: grupo.id, modalidade: 'Livre', precoDiario: '100' });
    const veiculo = await criar('/veiculos', { grupoId: grupo.id, placa: 'SIM0A08', marca: 'Aurora', modelo: 'Lume',
      cor: 'Prata', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50' });
    const cliente = await criar('/clientes', { tipo: 'PJ', nome: 'Empresa Exemplo', cnpj: '11222333000181',
      telefone: '11912345678', endereco: { estado: 'SP', cidade: 'Cidade', bairro: 'Bairro', rua: 'Rua Exemplo', numero: '1' } });
    const condutor = await criar('/condutores', { clienteId: cliente.id, nome: 'Condutor Exemplo', cpf: '52998224725',
      email: 'condutor@example.invalid', cnh: 'CNH-SIM', validadeCnh: '2040-01-01', telefone: '0000' });
    const taxa = await criar('/taxas-servicos', { nome: 'Taxa Exemplo', preco: '3', modalidade: 'Fixa' });
    async function preco(tipo, codigo) {
      const rota = `/configuracoes/combustiveis/${tipo}`;
      const atual = await enviar(rota, 'GET', 200);
      const corpo = await (await enviar(rota, 'PUT', 200, { preco: '6' }, { 'If-Match': atual.headers().etag })).json();
      assert.equal(corpo.numeroExibicao, codigo);
      return corpo;
    }
    const gasolina = await preco('Gasolina', '0001');
    await preco('Diesel', '0002');
    const retorno = new Date(Date.now() + 30 * 3600000 - 3 * 3600000).toISOString().replace('Z', '-03:00');
    const aluguel = await criar('/alugueis', { clienteId: cliente.id, condutorId: condutor.id, veiculoId: veiculo.id,
      planoId: plano.id, retornoPrevisto: retorno, hodometroInicial: 500, taxaIds: [taxa.id], seguros: [] },
      '0001', { 'Idempotency-Key': 'codigo-aluguel' });

    const modulo = nome => pagina.getByRole('navigation', { name: 'Navegação principal' })
      .getByRole('link', { name: nome, exact: true }).click();
    const botao = nome => pagina.getByRole('button', { name: nome, exact: true });
    const aba = nome => pagina.getByRole('tab', { name: nome, exact: true }).click();
    async function coluna(registro) {
      await pagina.getByRole('columnheader', { name: 'Código', exact: true }).waitFor();
      const celula = pagina.getByRole('table').getByText(registro.numeroExibicao, { exact: true }).first();
      await celula.waitFor();
      assert.equal(await pagina.getByText(registro.id, { exact: true }).count(), 0);
    }
    await modulo('Frota');
    await botao('Grupos').click(); await coluna(grupo);
    await botao('Planos').click(); await coluna(plano);
    await botao('Veículos').click(); await coluna(veiculo);
    await pagina.getByLabel('Agrupar por grupo').check(); await coluna(veiculo);
    await modulo('Pessoas'); await coluna(cliente);
    await pagina.getByText('(11) 91234-5678', { exact: true }).waitFor();
    await pagina.getByText('Página 1 · 1 registro · até 20 por página', { exact: true }).waitFor();
    await aba('Condutores'); await coluna(condutor);
    await modulo('Preços'); await coluna(taxa);
    await aba('Combustíveis'); await coluna(gasolina);
    const combustiveis = pagina.getByRole('tab', { name: 'Combustíveis', exact: true });
    assert.equal(await combustiveis.evaluate(e => getComputedStyle(e).outlineStyle), 'none');
    await combustiveis.press('ArrowLeft');
    const seguros = pagina.getByRole('tab', { name: 'Seguros', exact: true });
    await pagina.waitForFunction(() => document.activeElement?.id === 'aba-seguros');
    assert.equal(await seguros.evaluate(e => getComputedStyle(e).outlineStyle), 'solid');
    await modulo('Aluguéis'); await coluna(aluguel);
    const filtro = pagina.getByRole('button', { name: 'Filtrar', exact: true });
    const ocultos = pagina.getByLabel('Incluir aluguéis ocultos');
    const [f, o] = await Promise.all([filtro.boundingBox(), ocultos.boundingBox()]);
    assert.ok(f.x > o.x && Math.abs(f.y + f.height / 2 - o.y - o.height / 2) < 3);
    assert.equal(await ocultos.evaluate(e => getComputedStyle(e).accentColor), 'rgb(96, 107, 69)');
    const consulta = botao('Consultar aluguel 0001');
    assert.equal(await consulta.getAttribute('data-consultar'), aluguel.id);
    await consulta.click();
    await pagina.getByRole('heading', { name: 'Aluguel de Empresa Exemplo', exact: true }).waitFor();
    await pagina.getByText('Código do contrato: 0001', { exact: true }).waitFor();
    await botao('Editar aluguel').click();
    await pagina.getByRole('heading', { name: 'Editar aluguel 0001', exact: true }).waitFor();
    await botao('Voltar ao contrato').click();
    await botao('Registrar devolução').click();
    await pagina.getByText('Aluguel 0001 · Grupo Compactos', { exact: true }).waitFor();
    assert.deepEqual(erros, []);
    console.log('Oito códigos na API e na SPA (incluindo veículo agrupado); títulos, rótulo de consulta e referência de devolução usam código; navegação preserva UUID.');
  } finally {
    await browser.close();
  }
}

verificar().catch(erro => { console.error(erro); process.exitCode = 1; });
