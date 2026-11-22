import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RegistroComponent } from './registro.component';
import { LoginComponent } from './login.component';
import { PainelComponent } from './painel.component';
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

  it('impede envio inválido, associa erros e foca o resumo', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg[lucideUserPlus]').getAttribute('aria-hidden')).toBe('true');
    await fixture.componentInstance.registrar();
    fixture.detectChanges();
    http.expectNone(`${api}/registro`);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('#senha');
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toBe('erro-senha');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
  });

  it('valida confirmação de senha pelo Signal Form antes de enviar', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', email: 'admin@example.test', senha: '12345678', confirmacaoSenha: 'diferente' });
    await fixture.componentInstance.registrar();
    expect(fixture.componentInstance.formulario.confirmacaoSenha().invalid()).toBe(true);
    http.expectNone(`${api}/registro`);
  });

  it('envia somente o contrato, limpa credenciais e navega para login sem autenticar', async () => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', email: 'admin@example.test', senha: '12345678', confirmacaoSenha: '12345678' });
    const envio = fixture.componentInstance.registrar();
    const pedido = http.expectOne(`${api}/registro`);
    expect(pedido.request.body).toEqual({ username: 'admin', email: 'admin@example.test', password: '12345678' });
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    pedido.flush({ empresaId: 'empresa', usuarioId: 'usuario' }, { status: 201, statusText: 'Created' });
    await envio;
    expect(fixture.componentInstance.dados().senha).toBe('');
    expect(TestBed.inject(SessaoService).autenticada()).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/login');
    expect(TestBed.inject(SessaoService).aviso()).toContain('Empresa criada');
  });

  it.each([409, 429, 503])('exibe erro %s sem perder dados não sensíveis ou refletir texto da API', async status => {
    const fixture = TestBed.createComponent(RegistroComponent);
    fixture.detectChanges();
    fixture.componentInstance.dados.set({ usuario: 'admin', email: 'admin@example.test', senha: '12345678', confirmacaoSenha: '12345678' });
    const envio = fixture.componentInstance.registrar();
    http.expectOne(`${api}/registro`).flush({ detail: 'SQL segredo' }, { status, statusText: 'Erro' });
    await envio;
    expect(fixture.componentInstance.dados()).toEqual({ usuario: 'admin', email: 'admin@example.test', senha: '', confirmacaoSenha: '' });
    expect(fixture.componentInstance.erro()).not.toContain('SQL');
    expect(TestBed.inject(Router).navigateByUrl).not.toHaveBeenCalled();
  });

  it('autentica por login e mantém tokens somente no serviço em memória', async () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const local = vi.spyOn(Storage.prototype, 'setItem');
    fixture.componentInstance.dados.set({ usuario: 'admin', senha: '12345678' });
    const envio = fixture.componentInstance.entrar();
    http.expectOne(`${api}/session/login`).flush(tokens);
    await envio;
    expect(TestBed.inject(SessaoService).autenticada()).toBe(true);
    expect(fixture.componentInstance.dados().senha).toBe('');
    expect(local).not.toHaveBeenCalled();
    local.mockRestore();
    const novaSessao = new SessaoService(TestBed.inject(HttpClient));
    expect(novaSessao.autenticada()).toBe(false);
    expect(novaSessao.obterAccessToken()).toBeUndefined();
  });

  it('não confirma logout quando falha a revogação remota', async () => {
    const fixture = TestBed.createComponent(PainelComponent);
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const envio = fixture.componentInstance.sair();
    http.expectOne(`${api}/session/logout`).flush({}, { status: 503, statusText: 'Indisponível' });
    await envio;
    expect(sessao.autenticada()).toBe(true);
    expect(fixture.componentInstance.erro()).toContain('revogação remota');
    expect(TestBed.inject(Router).navigateByUrl).not.toHaveBeenCalled();
  });

  it('limpa memória somente depois do logout confirmado', async () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const envio = sessao.sair();
    expect(sessao.autenticada()).toBe(true);
    http.expectOne(`${api}/session/logout`).flush(null, { status: 204, statusText: 'No Content' });
    await envio;
    expect(sessao.autenticada()).toBe(false);
  });

  it('serializa duas renovações e não envia access token ao endpoint público', async () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const a = sessao.renovar();
    const b = sessao.renovar();
    const pedido = http.expectOne(`${api}/session/refresh`);
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    expect(pedido.request.withCredentials).toBe(false);
    pedido.flush({ ...tokens, accessToken: 'novo-acesso' });
    expect(await Promise.all([a, b])).toEqual([true, true]);
    expect(sessao.obterAccessToken()).toBe('novo-acesso');
  });

  it('não repete refresh se resposta se perde e exige reautenticação', async () => {
    const sessao = TestBed.inject(SessaoService);
    sessao.definir(tokens);
    const envio = sessao.renovar();
    http.expectOne(`${api}/session/refresh`).error(new ProgressEvent('error'));
    expect(await envio).toBe(false);
    expect(sessao.autenticada()).toBe(false);
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
