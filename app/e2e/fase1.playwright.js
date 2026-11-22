// Função executável pelo Playwright do harness, sem dependência adicionada à aplicação.
// Pré-requisito: scripts/Testar-Integracao.ps1 -Visual ativo em SQL exclusivo.
async (page) => {
  const origem = 'http://localhost:4201';
  const api = 'http://localhost:5001/api/v1';
  const usuario = `e2e-${Date.now()}`;
  const senha = '12345678'; // Dado sintético de teste; não é credencial de ambiente.
  const verificar = (condicao, mensagem) => { if (!condicao) throw new Error(mensagem); };
  const resultados = {};
  let envios = 0;
  const observar = pedido => {
    if (pedido.url() === `${api}/registro` && pedido.method() === 'POST') envios++;
  };
  page.on('request', observar);
  await page.goto(`${origem}/registro`);
  verificar(await page.locator('html').getAttribute('lang') === 'pt-BR', 'Idioma incorreto.');
  await page.getByRole('button', { name: 'Criar empresa', exact: true }).click();
  verificar(envios === 0, 'Formulário vazio enviou requisição.');
  verificar(await page.evaluate(() => document.activeElement?.getAttribute('role')) === 'alert', 'Resumo sem foco.');
  await page.locator('#usuario').fill(usuario);
  await page.locator('#usuario').focus();
  await page.keyboard.press('Tab');
  verificar(await page.evaluate(() => document.activeElement?.id) === 'email', 'Ordem de teclado incorreta.');
  verificar(await page.evaluate(() => getComputedStyle(document.activeElement).outlineWidth) !== '0px', 'Foco invisível.');
  await page.locator('#email').fill(`${usuario}@example.test`);
  await page.locator('#senha').fill(senha);
  await page.locator('#confirmacao').fill('diferente');
  await page.getByRole('button', { name: 'Criar empresa', exact: true }).click();
  verificar(envios === 0, 'Confirmação divergente enviou requisição.');
  await page.locator('#confirmacao').fill(senha);
  const [registro] = await Promise.all([
    page.waitForResponse(resposta => resposta.url() === `${api}/registro` && resposta.request().method() === 'POST'),
    page.getByRole('button', { name: 'Criar empresa', exact: true }).click(),
  ]);
  verificar(registro.status() === 201, 'Registro não retornou 201.');
  verificar(!('accessToken' in await registro.json()), 'Registro autenticou automaticamente.');
  await page.waitForURL(`${origem}/login`);
  verificar((await page.getByRole('status').innerText()).includes('Empresa criada'), 'Aviso de sucesso ausente.');
  resultados.registro = '201, sem tokens, validações sem envio e foco por teclado';
  page.off('request', observar);

  await page.locator('#usuario').fill(usuario);
  await page.locator('#senha').fill('incorreta');
  const [negado] = await Promise.all([
    page.waitForResponse(resposta => resposta.url() === `${api}/session/login`),
    page.getByRole('button', { name: 'Entrar', exact: true }).click(),
  ]);
  verificar(negado.status() === 401, 'Senha incorreta autenticou.');
  verificar((await page.getByRole('alert').innerText()).includes('Não foi possível'), 'Erro de login ausente.');

  const entrar = async () => {
    await page.locator('#usuario').fill(usuario);
    await page.locator('#senha').fill(senha);
    const [resposta] = await Promise.all([
      page.waitForResponse(resposta => resposta.url() === `${api}/session/login`),
      page.getByRole('button', { name: 'Entrar', exact: true }).click(),
    ]);
    verificar(resposta.status() === 200, 'Login válido falhou.');
    const tokens = await resposta.json();
    await page.waitForURL(`${origem}/`);
    return tokens; // Somente memória deste teste; nunca retornar tokens no relatório.
  };
  await entrar();
  const armazenamento = await page.evaluate(async () => ({
    local: localStorage.length, sessao: sessionStorage.length, cookies: document.cookie.length,
    indexedDB: (await indexedDB.databases()).length,
  }));
  verificar(Object.values(armazenamento).every(valor => valor === 0), 'Sessão persistida no navegador.');
  await page.reload();
  await page.waitForURL(`${origem}/login`);
  resultados.recarga = 'exige login; localStorage, sessionStorage, cookies e IndexedDB vazios';
  const tokens = await entrar();
  const superficie = await page.evaluate(async ({ api, access }) =>
    (await fetch(`${api}/teste/tenant/historico`, { headers: { Authorization: `Bearer ${access}` } })).status,
    { api, access: tokens.accessToken });
  verificar(superficie === 404, 'Endpoint sintético foi publicado na API real.');
  await page.route('**/session/logout', rota => rota.abort('failed'));
  await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'revogação remota' }).waitFor();
  verificar(page.url() === `${origem}/`, 'Logout com falha foi apresentado como sucesso.');
  await page.unroute('**/session/logout');
  await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await page.waitForURL(`${origem}/login`);
  const reuso = await page.evaluate(async ({ api, access, refresh }) => ({
    access: (await fetch(`${api}/session`, { headers: { Authorization: `Bearer ${access}` } })).status,
    refresh: (await fetch(`${api}/session/refresh`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: refresh }),
    })).status,
  }), { api, access: tokens.accessToken, refresh: tokens.refreshToken });
  verificar(reuso.access === 401 && reuso.refresh === 401, 'Logout não revogou access/refresh.');
  resultados.logout = { falhaRemotaInformada: true, reuso };

  await page.getByRole('link', { name: 'Criar empresa' }).focus();
  await page.keyboard.press('Enter');
  await page.waitForURL(`${origem}/registro`);
  await page.locator('#usuario').fill(usuario);
  await page.locator('#email').fill(`${usuario}@example.test`);
  await page.locator('#senha').fill(senha);
  await page.locator('#confirmacao').fill(senha);
  const [conflito] = await Promise.all([
    page.waitForResponse(resposta => resposta.url() === `${api}/registro` && resposta.request().method() === 'POST'),
    page.getByRole('button', { name: 'Criar empresa', exact: true }).click(),
  ]);
  verificar(conflito.status() === 409, 'Duplicado não retornou conflito.');
  await page.getByRole('alert').filter({ hasText: 'Usuário indisponível' }).waitFor();
  verificar(await page.locator('#usuario').inputValue() === usuario, 'Perdeu dado não sensível.');
  verificar(await page.locator('#senha').inputValue() === '', 'Senha não foi limpa após erro.');
  resultados.erroApi = '409 real; mensagem compreensível, usuário preservado e senha limpa';
  resultados.origensDistintas = { spa: origem, api };
  return resultados;
}
