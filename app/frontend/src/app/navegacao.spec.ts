import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { apiInterceptor } from './api.interceptor';
import { routes } from './app.routes';
import { CONFIGURACAO_API } from './configuracao-api';
import { AlugueisComponent } from './locacoes/alugueis.component';
import { LayoutAutenticadoComponent } from './layout-autenticado.component';
import { SessaoService } from './sessao.service';

const modulos = [
  ['/frota', 'Frota'], ['/pessoas', 'Pessoas'], ['/precos', 'Preços'], ['/alugueis', 'Aluguéis'],
];
const sessaoUrl = `${CONFIGURACAO_API.url}/session`;

describe('Layout e navegação autenticados', () => {
  let router: Router;
  let sessao: SessaoService;

  function autenticar() {
    sessao.definir({ sessaoId: 'sessao', accessToken: 'bearer', refreshToken: 'refresh', accessExpiraEm: '2099-01-01T00:00:00Z' });
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting()],
    });
    router = TestBed.inject(Router);
    sessao = TestBed.inject(SessaoService);
  });

  it.each(['/', ...modulos.map(([caminho]) => caminho)])('protege %s sem renderizar o layout público', async caminho => {
    const harness = await RouterTestingHarness.create(caminho);
    expect(router.url).toBe('/login');
    expect(harness.routeNativeElement?.querySelector('nav[aria-label="Navegação principal"]')).toBeNull();
  });

  it.each(['/login', '/registro'])('mantém %s fora do layout mesmo com sessão em memória', async caminho => {
    autenticar();
    const harness = await RouterTestingHarness.create(caminho);
    expect(router.url).toBe(caminho);
    expect(harness.routeNativeElement?.querySelector('nav[aria-label="Navegação principal"]')).toBeNull();
  });

  it.each(modulos)('abre %s pelo cartão, transfere aria-current e só libera Sair quando integrado', async (caminho, nome) => {
    autenticar();
    const harness = await RouterTestingHarness.create('/');
    const el = harness.routeNativeElement!;
    expect(el.querySelector('main h1')?.textContent).toBe('Início');
    const principal = el.querySelector('nav[aria-label="Navegação principal"]')!;
    expect(principal.querySelector('[aria-current="page"]')?.getAttribute('href')).toBe('/');
    expect(el.querySelector('aside button')?.textContent?.trim()).toBe('Sair');
    const cartoes = [...el.querySelectorAll<HTMLAnchorElement>('main nav a')];
    expect(cartoes.map(a => a.getAttribute('href'))).toEqual(modulos.map(([rota]) => rota));
    const alvo = cartoes.find(a => a.getAttribute('href') === caminho)!;
    expect(alvo.textContent).toContain(nome);
    alvo.click();
    await vi.waitFor(() => expect(router.url).toBe(caminho));
    harness.detectChanges();
    const ativo = principal.querySelector('[aria-current="page"]');
    expect(ativo?.getAttribute('href')).toBe(caminho);
    expect(ativo?.classList.contains('ativo')).toBe(true);
    expect(el.querySelector('aside button')?.textContent?.trim()).toBe('Sair');
    await harness.navigateByUrl(caminho + '?origem=inicio');
    expect(principal.querySelector('[aria-current="page"]')?.getAttribute('href')).toBe(caminho);
  });

  it('preserva a decisão do guarda da devolução em navegação comum', async () => {
    autenticar();
    await RouterTestingHarness.create('/alugueis');
    const podeSair = vi.spyOn(AlugueisComponent.prototype, 'podeSair').mockReturnValue(false);
    expect(await router.navigateByUrl('/')).toBe(false);
    expect(router.url).toBe('/alugueis');
    expect(podeSair).toHaveBeenCalledOnce();
  });

  it('retém Início durante revogação e incerteza; deixa sair para reautenticar', async () => {
    autenticar();
    const harness = await RouterTestingHarness.create();
    const layout = await harness.navigateByUrl('/', LayoutAutenticadoComponent);
    const http = TestBed.inject(HttpTestingController);
    const tentativa = layout.sair();
    await vi.waitFor(() => expect(sessao.faseSaida()).toBe('revogando'));
    const pedido = http.expectOne(`${sessaoUrl}/logout`);
    expect(await router.navigateByUrl('/frota')).toBe(false);
    pedido.error(new ProgressEvent('error'));
    await vi.waitFor(() => http.expectOne(sessaoUrl).flush({ ativa: true, sessaoId: 'sessao' }));
    await tentativa;
    expect(await router.navigateByUrl('/frota')).toBe(false);
    harness.detectChanges();
    const painel = harness.routeNativeElement!;
    expect(painel.querySelector('[role=alert]')?.textContent).toContain('Não foi possível confirmar');
    [...painel.querySelectorAll('button')].find(b => b.textContent?.trim() === 'Entrar novamente')!.click();
    await vi.waitFor(() => expect(router.url).toBe('/login'));
    expect(harness.routeNativeElement?.querySelector('[role=status]')?.textContent).toContain('Não foi possível confirmar');
    http.verify();
  });
});
