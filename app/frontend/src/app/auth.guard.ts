import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { SessaoService } from './sessao.service';

export const authGuard: CanActivateFn = () =>
  inject(SessaoService).autenticada() || inject(Router).createUrlTree(['/login']);
