import { Component, input } from '@angular/core';
import { LucideCar, LucideDollarSign, LucideFileText, LucideLayoutGrid, LucideUsersRound } from '@lucide/angular';
import { modulosAutenticados } from './modulos-autenticados';

@Component({
  selector: 'app-icone-modulo',
  imports: [LucideCar, LucideDollarSign, LucideFileText, LucideLayoutGrid, LucideUsersRound],
  host: { 'aria-hidden': 'true' },
  styles: ':host { display:block; flex:none; width:var(--icone-tamanho,1.25rem); height:var(--icone-tamanho,1.25rem); } svg { width:100%; height:100%; stroke-width:1.75; }',
  template: `
    @switch (nome()) {
      @case ('inicio') { <svg lucideLayoutGrid focusable="false"></svg> }
      @case ('frota') { <svg lucideCar focusable="false"></svg> }
      @case ('pessoas') { <svg lucideUsersRound focusable="false"></svg> }
      @case ('precos') { <svg lucideDollarSign focusable="false"></svg> }
      @case ('alugueis') { <svg lucideFileText focusable="false"></svg> }
    }
  `,
})
export class IconeModuloComponent {
  readonly nome = input.required<'inicio' | (typeof modulosAutenticados)[number]['icone']>();
}
