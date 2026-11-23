import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { routes } from './app.routes';
import { SessaoService } from './sessao.service';
import { App } from './app';

describe('Navegação protegida dos cadastros', () => {
  const autenticada = signal(false);
  beforeEach(() => {
    autenticada.set(false);
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), { provide: SessaoService, useValue: { autenticada } }],
    });
  });
  it.each(['frota', 'pessoas', 'precos', 'alugueis'])('protege a rota %s com a sessão em memória', caminho => {
    expect(routes.find(x => x.path === caminho)?.canActivate).toEqual([authGuard]);
    const negado = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    expect(negado instanceof UrlTree).toBe(true);
    autenticada.set(true);
    expect(TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot))).toBe(true);
  });
  it('oferece navegação principal somente com sessão autenticada', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('nav')).toBeNull();
    autenticada.set(true);
    fixture.detectChanges();
    const links = [...fixture.nativeElement.querySelectorAll('nav a')] as HTMLAnchorElement[];
    expect(links.map(x => x.getAttribute('href'))).toEqual(['/', '/frota', '/pessoas', '/precos', '/alugueis']);
  });
});
