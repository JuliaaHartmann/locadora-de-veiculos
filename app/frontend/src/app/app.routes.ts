import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./login.component').then(m => m.LoginComponent) },
  { path: 'registro', loadComponent: () => import('./registro.component').then(m => m.RegistroComponent) },
  {
    path: 'frota',
    canActivate: [authGuard],
    loadComponent: () => import('./frota.component').then(m => m.FrotaComponent),
  },
  {
    path: 'pessoas',
    canActivate: [authGuard],
    loadComponent: () => import('./pessoas.component').then(m => m.PessoasComponent),
  },
  {
    path: 'precos',
    canActivate: [authGuard],
    loadComponent: () => import('./precos.component').then(m => m.PrecosComponent),
  },
  {
    path: 'alugueis',
    canActivate: [authGuard],
    loadComponent: () => import('./locacoes/alugueis.component').then(m => m.AlugueisComponent),
  },
  { path: '', canActivate: [authGuard], loadComponent: () => import('./painel.component').then(m => m.PainelComponent) },
  { path: '**', redirectTo: '' },
];
