import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RegistroComponent } from './registro.component';
import { LoginComponent } from './login.component';
import { SessaoService, Tokens } from './sessao.service';
import { apiInterceptor, pertenceAApi } from './api.interceptor';
import { CONFIGURACAO_API } from './configuracao-api';

const api = CONFIGURACAO_API.url;
const tokens: Tokens = { accessToken: 'acesso-sintetico', refreshToken: 'refresh-sintetico', sessaoId: 'sessao', accessExpiraEm: '2026-11-22T20:00:00Z' };

describe('Acesso da F1', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });
  afterEach(() => http.verify());

  it('no cadastro impede envio vazio, associa erros e foca o resumo', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg[lucideUserPlus]').getAttribute('aria-hidden')).toBe('true');
    await fixture.componentInstance.registrar();
    fixture.detectChanges();
    http.expectNone(`${api}/registro`);
    const tela: HTMLElement = fixture.nativeElement;
    const resumo = tela.querySelector('#resumo-erros-registro[role=alert]');
    expect(resumo?.textContent).toBe('Revise os campos indicados.');
    for (const id of ['usuario', 'email', 'senha', 'confirmacao']) {
      const campo = tela.querySelector<HTMLInputElement>(`#${id}`)!;
      expect(campo.getAttribute('aria-invalid')).toBe('true');
      expect(campo.getAttribute('aria-describedby')).toBe(`erro-${id} resumo-erros-registro`);
      expect(tela.querySelector(`#erro-${id}`)?.textContent?.trim()).not.toBe('');
    }
    expect(document.activeElement).toBe(resumo);
    const usuario = tela.querySelector<HTMLInputElement>('#usuario')!;
    usuario.value = 'admin';
    usuario.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
    expect(usuario.getAttribute('aria-invalid')).toBe('false');
    expect(usuario.getAttribute('aria-describedby')).toBe('erro-usuario');
    expect(tela.querySelector('#erro-usuario')?.textContent?.trim()).toBe('');
    expect(tela.querySelector('#email')?.getAttribute('aria-describedby')).toBe('erro-email resumo-erros-registro');
  });

  it('no cadastro mantém a descrição individual ao sair do campo inválido com resumo vazio', () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    const tela: HTMLElement = fixture.nativeElement;
    const email = tela.querySelector<HTMLInputElement>('#email')!;
    email.dispatchEvent(new FocusEvent('blur'));
    fixture.detectChanges();
    expect(email.getAttribute('aria-invalid')).toBe('true');
    expect(email.getAttribute('aria-describedby')).toBe('erro-email');
    expect(tela.querySelector('#resumo-erros-registro')?.textContent).toBe('');
  });

  it('no cadastro valida espaços, e-mail, senha curta e confirmação antes de enviar', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: '   ', email: 'invalido', senha: 'curta', confirmacaoSenha: 'diferente' });
    await fixture.componentInstance.registrar();
    const formulario = fixture.componentInstance.formulario;
    for (const campo of [formulario.usuario, formulario.email, formulario.senha, formulario.confirmacaoSenha]) {
      expect(campo().invalid()).toBe(true);
    }
    http.expectNone(`${api}/registro`);
  });

  it('no cadastro envia o contrato uma vez, limpa os dados e retorna ao login sem autenticar', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', email: 'admin@example.test', senha: '12345678', confirmacaoSenha: '12345678' });
    const envio = fixture.componentInstance.registrar();
    const pedido = http.expectOne({ method: 'POST', url: `${api}/registro` });
    expect(pedido.request.body).toEqual({ username: 'admin', email: 'admin@example.test', password: '12345678' });
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    fixture.detectChanges();
    const botao: HTMLButtonElement = fixture.nativeElement.querySelector('button[type=submit]');
    expect(botao.disabled).toBe(true);
    expect(botao.textContent).toContain('Criando conta...');
    await fixture.componentInstance.registrar();
    http.expectNone(`${api}/registro`);
    pedido.flush({ empresaId: 'empresa', usuarioId: 'usuario' }, { status: 201, statusText: 'Created' });
    await envio;
    expect(fixture.componentInstance.dados()).toEqual({ usuario: '', email: '', senha: '', confirmacaoSenha: '' });
    http.expectNone(`${api}/session/login`);
    expect(TestBed.inject(SessaoService).autenticada()).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/login');
    expect(TestBed.inject(SessaoService).aviso()).toContain('Empresa criada');
  });

  it.each([
    [409, 'Usuário indisponível. Revise os dados.'],
    [429, 'Muitas tentativas. Aguarde alguns minutos.'],
    [503, 'Serviço indisponível. Tente mais tarde.'],
    [400, 'Revise os dados informados.'],
  ])('no cadastro exibe erro %s com foco e preserva dados não sensíveis', async (status, mensagem) => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', email: 'admin@example.test', senha: '12345678', confirmacaoSenha: '12345678' });
    const envio = fixture.componentInstance.registrar();
    http.expectOne(`${api}/registro`).flush({ detail: 'SQL segredo' }, { status, statusText: 'Erro' });
    await envio;
    fixture.detectChanges();
    expect(fixture.componentInstance.dados()).toEqual({ usuario: 'admin', email: 'admin@example.test', senha: '', confirmacaoSenha: '' });
    const tela: HTMLElement = fixture.nativeElement;
    const resumo = tela.querySelector('#resumo-erros-registro[role=alert]');
    expect(resumo?.textContent).toBe(mensagem);
    expect(document.activeElement).toBe(resumo);
    expect(tela.querySelector('#email')?.getAttribute('aria-describedby')).toBe('erro-email');
    expect(tela.querySelector('#senha')?.getAttribute('aria-describedby')).toBe('erro-senha resumo-erros-registro');
    expect(tela.querySelector<HTMLButtonElement>('button[type=submit]')?.disabled).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).not.toHaveBeenCalled();
  });

  it('impede login vazio, associa os erros aos campos e foca o resumo', async () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const tela: HTMLElement = fixture.nativeElement;
    for (const id of ['usuario', 'senha']) {
      const campo = tela.querySelector<HTMLInputElement>(`#${id}`)!;
      expect(campo.getAttribute('aria-invalid')).toBe('false');
      expect(campo.getAttribute('aria-describedby')).toBe(`erro-${id}`);
    }

    await fixture.componentInstance.entrar();
    fixture.detectChanges();

    http.expectNone(`${api}/session/login`);
    const resumo = tela.querySelector('#resumo-erros-login[role=alert]');
    expect(resumo?.textContent).toBe('Informe usuário e senha.');
    for (const [id, rotulo] of [['usuario', 'Usuário'], ['senha', 'Senha']]) {
      const campo = tela.querySelector<HTMLInputElement>(`#${id}`)!;
      expect(campo.labels?.[0].textContent).toBe(rotulo);
      expect(campo.getAttribute('aria-invalid')).toBe('true');
      expect(campo.getAttribute('aria-describedby')).toBe(`erro-${id} resumo-erros-login`);
      expect(tela.querySelector(`#erro-${id}`)?.textContent?.trim()).not.toBe('');
    }
    expect(document.activeElement).toBe(resumo);
  });

  it.each([
    ['usuario', { usuario: '', senha: '12345678' }, 'senha'],
    ['senha', { usuario: 'admin', senha: '' }, 'usuario'],
  ] as const)('associa apenas %s inválido ao resumo e remove a referência após a correção', async (
    invalido, dados, valido,
  ) => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.componentInstance.dados.set(dados);
    fixture.detectChanges();

    await fixture.componentInstance.entrar();
    fixture.detectChanges();
    http.expectNone(`${api}/session/login`);

    const tela: HTMLElement = fixture.nativeElement;
    const campoInvalido = tela.querySelector<HTMLInputElement>(`#${invalido}`)!;
    const campoValido = tela.querySelector<HTMLInputElement>(`#${valido}`)!;
    expect(campoInvalido.getAttribute('aria-invalid')).toBe('true');
    expect(campoInvalido.getAttribute('aria-describedby')).toBe(`erro-${invalido} resumo-erros-login`);
    expect(campoValido.getAttribute('aria-invalid')).toBe('false');
    expect(campoValido.getAttribute('aria-describedby')).toBe(`erro-${valido}`);

    campoInvalido.value = invalido === 'usuario' ? 'admin' : '12345678';
    campoInvalido.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();

    expect(campoInvalido.getAttribute('aria-invalid')).toBe('false');
    expect(campoInvalido.getAttribute('aria-describedby')).toBe(`erro-${invalido}`);
    expect(tela.querySelector(`#erro-${invalido}`)?.textContent?.trim()).toBe('');
  });

  it('autentica por login, bloqueia reenvio e mantém tokens somente em memória', async () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const local = vi.spyOn(Storage.prototype, 'setItem');
    try {
      fixture.componentInstance.dados.set({ usuario: 'admin', senha: '12345678' });
      const envio = fixture.componentInstance.entrar();
      const pedido = http.expectOne({ method: 'POST', url: `${api}/session/login` });
      expect(pedido.request.body).toEqual({ username: 'admin', password: '12345678' });
      expect(pedido.request.headers.has('Authorization')).toBe(false);
      fixture.detectChanges();
      const botao: HTMLButtonElement = fixture.nativeElement.querySelector('button[type=submit]');
      expect(botao.disabled).toBe(true);
      const tela: HTMLElement = fixture.nativeElement;
      expect(tela.querySelector('#usuario')?.getAttribute('aria-describedby')).toBe('erro-usuario');
      expect(tela.querySelector('#senha')?.getAttribute('aria-describedby')).toBe('erro-senha');
      await fixture.componentInstance.entrar();
      http.expectNone(`${api}/session/login`);
      pedido.flush(tokens);
      await envio;
      expect(TestBed.inject(SessaoService).autenticada()).toBe(true);
      expect(fixture.componentInstance.dados()).toEqual({ usuario: 'admin', senha: '' });
      expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/');
      expect(local).not.toHaveBeenCalled();
    } finally {
      local.mockRestore();
    }
    const novaSessao = TestBed.runInInjectionContext(() => new SessaoService());
    expect(novaSessao.autenticada()).toBe(false);
    expect(novaSessao.obterAccessToken()).toBeUndefined();
  });

  it('no login aceito limpa a senha sem exibir validação enquanto aguarda a navegação', async () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    let concluirNavegacao!: (resultado: boolean) => void;
    vi.mocked(TestBed.inject(Router).navigateByUrl).mockImplementationOnce(() =>
      new Promise(resolve => { concluirNavegacao = resolve; }));
    fixture.componentInstance.dados.set({ usuario: 'admin', senha: '12345678' });
    const envio = fixture.componentInstance.entrar();
    http.expectOne(`${api}/session/login`).flush(tokens);
    await Promise.resolve();
    try {
      fixture.detectChanges();
      const tela: HTMLElement = fixture.nativeElement;
      expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/');
      expect(fixture.componentInstance.enviando()).toBe(true);
      expect(tela.querySelector<HTMLInputElement>('#senha')?.value).toBe('');
      expect(tela.querySelector('#senha')?.getAttribute('aria-invalid')).toBe('false');
      expect(tela.querySelector('#erro-senha')?.textContent?.trim()).toBe('');
      expect(tela.querySelector('#resumo-erros-login')?.textContent?.trim()).toBe('');
    } finally {
      concluirNavegacao?.(true);
      await envio;
    }
  });

  it.each([401, 429, 503])('mostra erro genérico de login para %s e recupera o foco', async status => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', senha: '12345678' });
    const envio = fixture.componentInstance.entrar();
    http.expectOne(`${api}/session/login`).flush({ detail: 'Detalhe interno' }, { status, statusText: 'Erro' });
    await envio;
    fixture.detectChanges();

    const tela: HTMLElement = fixture.nativeElement;
    const resumo = tela.querySelector('#resumo-erros-login[role=alert]');
    expect(resumo?.textContent).toBe('Não foi possível autenticar.');
    expect(document.activeElement).toBe(resumo);
    expect(tela.querySelector('#usuario')?.getAttribute('aria-invalid')).toBe('false');
    expect(tela.querySelector('#usuario')?.getAttribute('aria-describedby')).toBe('erro-usuario');
    expect(tela.querySelector('#senha')?.getAttribute('aria-invalid')).toBe('true');
    expect(tela.querySelector('#senha')?.getAttribute('aria-describedby')).toBe('erro-senha resumo-erros-login');
    expect(tela.querySelector('#erro-senha')?.textContent?.trim()).toBe('Informe a senha.');
    expect(tela.querySelector<HTMLButtonElement>('button[type=submit]')?.disabled).toBe(false);
    expect(fixture.componentInstance.dados()).toEqual({ usuario: 'admin', senha: '' });
    expect(TestBed.inject(SessaoService).autenticada()).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).not.toHaveBeenCalled();
  });

  it('mostra o aviso após o cadastro e preserva o link de registro', () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.aviso.set('Empresa criada. Entre com seu usuário e senha.');
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    const tela: HTMLElement = fixture.nativeElement;
    expect(tela.querySelector('[role=status]')?.textContent).toBe('Empresa criada. Entre com seu usuário e senha.');
    expect(tela.querySelector('a[href="/registro"]')?.textContent).toBe('Criar empresa');
    expect(sessao.aviso()).toBe('');
    expect(sessao.autenticada()).toBe(false);
  });

  it('serializa duas renovações e não envia access token ao endpoint público', async () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const a = sessao.recuperar401(sessao.contexto(), tokens.accessToken);
    const b = sessao.recuperar401(sessao.contexto(), tokens.accessToken);
    const pedido = http.expectOne(`${api}/session/refresh`);
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    expect(pedido.request.withCredentials).toBe(false);
    pedido.flush({ ...tokens, accessToken: 'novo-acesso' });
    expect(await Promise.all([a, b])).toEqual(['novo-acesso', 'novo-acesso']);
    expect(sessao.obterAccessToken()).toBe('novo-acesso');
  });

  it('não repete refresh se resposta se perde e exige reautenticação', async () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const envio = sessao.recuperar401(sessao.contexto(), tokens.accessToken);
    http.expectOne(`${api}/session/refresh`).error(new ProgressEvent('error'));
    expect(await envio).toBeUndefined();
    expect(sessao.autenticada()).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledExactlyOnceWith('/login');
    http.expectNone(`${api}/session/refresh`);
  });

  it.each(['https://externo.example.test/api/v1/dados', `${api}malicioso/dados`, 'http://localhost:5000/api/v10/dados'])(
    'nunca envia bearer a destino fora da API: %s', destino => {
      TestBed.inject(SessaoService).definir(tokens);
      TestBed.inject(HttpClient).get(destino).subscribe();
      const pedido = http.expectOne(destino);
      expect(pedido.request.headers.has('Authorization')).toBe(false);
      pedido.flush({});
      expect(pertenceAApi(destino)).toBe(false);
    });

  it('renova uma requisição protegida após 401 e a repete com o novo bearer', async () => {
    TestBed.inject(SessaoService).definir(tokens);
    TestBed.inject(HttpClient).get(`${api}/session`).subscribe();
    const original = http.expectOne(`${api}/session`);
    expect(original.request.headers.get('Authorization')).toBe(`Bearer ${tokens.accessToken}`);
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(`${api}/session/refresh`).flush({ ...tokens, accessToken: 'renovado' });
    await vi.waitFor(() => {
      const repetido = http.expectOne(`${api}/session`);
      expect(repetido.request.headers.get('Authorization')).toBe('Bearer renovado');
      repetido.flush({});
    });
  });
});
