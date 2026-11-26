import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideArrowRight } from '@lucide/angular';
import { IconeModuloComponent } from './icone-modulo.component';
import { modulosAutenticados } from './modulos-autenticados';
import { SessaoService } from './sessao.service';

@Component({
  imports: [RouterLink, LucideArrowRight, IconeModuloComponent],
  templateUrl: './painel.component.html',
  styleUrl: './painel.component.css',
})
export class PainelComponent {
  private readonly sessao = inject(SessaoService);
  readonly faseSaida = this.sessao.faseSaida;
  readonly modulos = modulosAutenticados;

  reautenticar() {
    const contexto = this.sessao.contexto();
    if (contexto !== undefined) this.sessao.reautenticar(contexto);
  }
}
