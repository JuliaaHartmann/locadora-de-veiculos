import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, map, switchMap, throwError } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { REVOGACAO_CONTROLADA, SessaoService } from './sessao.service';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!pertenceAApi(req.url) || req.context.get(REVOGACAO_CONTROLADA)) return next(req);
  const rota = new URL(req.url).pathname.slice(new URL(CONFIGURACAO_API.url).pathname.length);
  if (['/session/login', '/session/refresh', '/registro'].includes(rota)) return next(req);
  const sessao = inject(SessaoService);
  const origem = sessao.contexto();
  const token = sessao.obterAccessToken();
  const comBearer = (bearer: string | undefined) =>
    bearer ? req.clone({ setHeaders: { Authorization: `Bearer ${bearer}` } }) : req;
  const erroContexto = () => new HttpErrorResponse({ status: 0, statusText: 'Contexto de sessão substituído', url: req.url });
  const enviar = (bearer: string | undefined) => next(comBearer(bearer)).pipe(map(evento => {
    if (sessao.contexto() !== origem) throw erroContexto();
    return evento;
  }), catchError(erro => throwError(() => sessao.contexto() !== origem ? erroContexto() : erro)));

  return enviar(token).pipe(catchError(erro => {
    if (erro.status !== 401 || rota === '/session/logout' || !sessao.podeRecuperar(origem)) {
      return throwError(() => erro);
    }
    return from(sessao.recuperar401(origem, token)).pipe(switchMap(novoToken => {
      if (novoToken && sessao.podeRecuperar(origem)) return enviar(novoToken);
      return throwError(() => sessao.contexto() !== origem ? erroContexto() : erro);
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
