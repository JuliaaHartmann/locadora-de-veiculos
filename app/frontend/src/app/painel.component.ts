import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SessaoService } from './sessao.service';
import { LucideLogOut } from '@lucide/angular';

@Component({
  standalone: true,
  imports: [LucideLogOut],
  template: `<main><h1>Painel</h1><p>Sessão autenticada em memória.</p>
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
