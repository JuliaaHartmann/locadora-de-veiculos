import { ChangeDetectorRef, Component, DestroyRef, inject, signal, viewChild, ElementRef } from '@angular/core';
import { IsActiveMatchOptions, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LucideLogOut } from '@lucide/angular';
import { IconeModuloComponent } from './icone-modulo.component';
import { modulosAutenticados } from './modulos-autenticados';
import { SaidaCoordenador } from './saida-coordenador.service';

@Component({
  selector: 'app-layout-autenticado',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, LucideLogOut, IconeModuloComponent],
  templateUrl: './layout-autenticado.component.html',
  styleUrl: './layout-autenticado.component.css',
  host: {
    '[attr.data-foco-ponteiro]': 'focoPorPonteiro() ? "" : null',
    '(document:pointerdown)': 'focoPorPonteiro.set(true)',
    '(document:keydown)': 'registrarTecla($event)',
  },
})
export class LayoutAutenticadoComponent {
  private readonly router = inject(Router);
  private readonly mudancas = inject(ChangeDetectorRef);
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');
  private resolverConfirmacao?: (confirmada: boolean) => void;
  readonly saida = inject(SaidaCoordenador);
  readonly focoPorPonteiro = signal(false);
  readonly saidaIntegrada = signal(false);
  readonly mensagem = signal('');
  readonly modulos = modulosAutenticados;
  readonly rotaAtiva: IsActiveMatchOptions = {
    paths: 'exact', matrixParams: 'ignored', queryParams: 'ignored', fragment: 'ignored',
  };

  constructor() {
    inject(DestroyRef).onDestroy(() => this.resolverConfirmacao?.(false));
  }

  sair() {
    return this.saida.solicitar(
      () => this.saidaIntegrada() && !this.router.currentNavigation(),
      mensagem => this.confirmarSaida(mensagem),
    );
  }

  registrarTecla(evento: KeyboardEvent) {
    if (!['Shift', 'Alt', 'Control', 'Meta'].includes(evento.key)) this.focoPorPonteiro.set(false);
  }

  private confirmarSaida(mensagem: string) {
    this.mensagem.set(mensagem);
    this.mudancas.detectChanges();
    const dialogo = this.dialogo().nativeElement;
    const resultado = new Promise<boolean>(resolve => this.resolverConfirmacao = resolve);
    dialogo.showModal();
    return resultado;
  }

  fecharConfirmacao(confirmada: boolean) {
    this.dialogo().nativeElement.close();
    this.resolverConfirmacao?.(confirmada);
    this.resolverConfirmacao = undefined;
  }

  conterFoco(evento: KeyboardEvent, cancelar: HTMLButtonElement, confirmar: HTMLButtonElement) {
    if (evento.key !== 'Tab') return;
    if (evento.shiftKey ? evento.target === cancelar : evento.target === confirmar) {
      evento.preventDefault();
      (evento.shiftKey ? confirmar : cancelar).focus();
    }
  }

  desativarSaida() {
    this.saidaIntegrada.set(false);
    if (this.resolverConfirmacao) this.fecharConfirmacao(false);
  }
}
