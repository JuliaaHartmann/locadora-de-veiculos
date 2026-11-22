import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { SessaoService } from './sessao.service';
import { Router } from '@angular/router';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!pertenceAApi(req.url)) return next(req);
  const sessao = inject(SessaoService);
  const router = inject(Router);
  const token = sessao.obterAccessToken();
  const publico = ['/session/login', '/session/refresh', '/registro']
    .some(rota => new URL(req.url).pathname === new URL(CONFIGURACAO_API.url).pathname + rota);
  const requisicao = token && !publico ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(requisicao).pipe(catchError(erro => {
    if (erro.status !== 401 || publico) {
      return throwError(() => erro);
    }
    return from(sessao.renovar()).pipe(switchMap(renovado => {
      if (!renovado) {
        sessao.aviso.set('Sessão encerrada. Entre novamente.');
        void router.navigateByUrl('/login');
        return throwError(() => erro);
      }
      const novoToken = sessao.obterAccessToken();
      return next(novoToken ? req.clone({ setHeaders: { Authorization: `Bearer ${novoToken}` } }) : req);
    }));
  }));
};

export function pertenceAApi(destino: string): boolean {
  try {
    const url = new URL(destino);
    const api = new URL(CONFIGURACAO_API.url);
    return url.origin === api.origin && (url.pathname === api.pathname || url.pathname.startsWith(api.pathname + '/'));
  } catch { return false; }
}
