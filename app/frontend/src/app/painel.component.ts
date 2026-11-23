import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { SessaoService } from './sessao.service';
import { LucideLogOut } from '@lucide/angular';

@Component({
  standalone: true,
  imports: [RouterLink, LucideLogOut],
  template: `<main class="max-w-4xl"><h1>Painel</h1><p>Sessão autenticada em memória.</p>
    <nav aria-label="Cadastros" class="my-4 flex flex-wrap gap-2">
      <a routerLink="/frota" class="rounded-md border border-slate-400 bg-white px-4 py-2 no-underline">Frota</a>
      <a routerLink="/pessoas" class="rounded-md border border-slate-400 bg-white px-4 py-2 no-underline">Pessoas</a>
      <a routerLink="/precos" class="rounded-md border border-slate-400 bg-white px-4 py-2 no-underline">Preços</a>
    </nav>
    <button (click)="sair()"><svg lucideLogOut aria-hidden="true" focusable="false" class="h-5 w-5"></svg> Sair</button>
    <p role="alert">{{ erro() }}</p></main>`,
})
export class PainelComponent {
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  readonly erro = signal('');
  async sair() {
    try { await this.sessao.sair(); await this.router.navigateByUrl('/login'); }
    catch { this.erro.set('Não foi possível confirmar a revogação remota. Tente sair novamente.'); }
  }
}
