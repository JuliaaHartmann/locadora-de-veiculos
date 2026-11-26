import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';
import type { AlugueisComponent } from './locacoes/alugueis.component';
import { inject } from '@angular/core';
import { SessaoService } from './sessao.service';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./login.component').then(m => m.LoginComponent) },
  { path: 'registro', loadComponent: () => import('./registro.component').then(m => m.RegistroComponent) },
  {
    path: '',
    canActivateChild: [authGuard],
    loadComponent: () => import('./layout-autenticado.component').then(m => m.LayoutAutenticadoComponent),
    children: [
      {
        path: '', pathMatch: 'full', data: { saidaIntegrada: true },
        // Impede alcançar editores legados enquanto a revogação puder produzir efeitos.
        canDeactivate: [() => inject(SessaoService).faseSaida() === 'livre'],
        loadComponent: () => import('./painel.component').then(m => m.PainelComponent),
      },
      {
        path: 'frota',
        loadComponent: () => import('./frota.component').then(m => m.FrotaComponent),
      },
      {
        path: 'pessoas',
        loadComponent: () => import('./pessoas.component').then(m => m.PessoasComponent),
      },
      {
        path: 'precos',
        loadComponent: () => import('./precos.component').then(m => m.PrecosComponent),
      },
      {
        path: 'alugueis',
        canDeactivate: [(componente: AlugueisComponent) => componente.podeSair()],
        loadComponent: () => import('./locacoes/alugueis.component').then(m => m.AlugueisComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
