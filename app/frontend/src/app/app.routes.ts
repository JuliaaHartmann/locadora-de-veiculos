import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./login.component').then(m => m.LoginComponent) },
  { path: 'registro', loadComponent: () => import('./registro.component').then(m => m.RegistroComponent) },
  { path: '', canActivate: [authGuard], loadComponent: () => import('./painel.component').then(m => m.PainelComponent) },
  { path: '**', redirectTo: '' },
];
