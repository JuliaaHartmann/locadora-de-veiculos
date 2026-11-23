import { Component, input } from '@angular/core';
import { formatarMoeda } from '../cadastros.service';
import { ComponenteContratado, DemonstrativoEstimado, rotuloPlano } from './alugueis.service';

@Component({
  standalone: true,
  selector: 'app-demonstrativo-aluguel',
  template: `
    <section class="min-w-0 rounded-xl border border-slate-300 bg-white shadow-sm" aria-labelledby="titulo-demonstrativo">
      <header class="border-b border-slate-200 p-4">
        <h2 id="titulo-demonstrativo" tabindex="-1" class="text-lg font-semibold">{{ titulo() }}</h2>
        <small class="text-slate-600">
          {{ estimativa() ? periodo(estimativa()!.diarias) + ' de 24 h · Horário de São Paulo' : 'Parcelas conhecidas da contratação.' }}
        </small>
      </header>
      <div class="grid gap-4 p-4">
        @if (estimativa(); as dados) {
          <dl class="grid gap-3">
            <div class="flex justify-between gap-3">
              <dt>Plano {{ planos[dados.plano.modalidade] }}
                <small class="block text-slate-600">{{ periodo(dados.diarias) }} × {{ moeda(dados.plano.precoDiario) }}</small>
              </dt>
              <dd class="shrink-0 font-semibold tabular-nums">{{ moeda(dados.valorPlano) }}</dd>
            </div>
            @if (dados.quilometragemPendente) {
              <div class="flex justify-between gap-3">
                <dt>Cobrança por quilômetro
                  <small class="block text-slate-600">
                    @if (dados.plano.modalidade === 'Controlado') {
                      {{ numero.format(dados.plano.franquiaDiariaKm ?? 0) }} km/dia ·
                      {{ numero.format(dados.franquiaTotalKm ?? 0) }} km de franquia no período ·
                      {{ moeda(dados.plano.precoKm) }} por km excedente
                    } @else { {{ moeda(dados.plano.precoKm) }} por km rodado }
                  </small>
                </dt>
                <dd class="shrink-0 font-semibold">Pendente</dd>
              </div>
            }
            @for (item of dados.adicionais; track item.categoria + item.origemId) {
              <div class="flex justify-between gap-3">
                <dt>{{ item.nome }}
                  <small class="block text-slate-600">{{ rotulo(item) }} ·
                    @if (item.modalidade === 'Diaria') { {{ periodo(dados.diarias) }} × }
                    {{ moeda(item.preco) }}
                  </small>
                </dt>
                <dd class="shrink-0 font-semibold tabular-nums">{{ moeda(item.valor) }}</dd>
              </div>
            }
          </dl>
          <dl class="flex items-center justify-between gap-3 rounded-lg bg-blue-50 p-3">
            <dt class="font-semibold">Parcelas conhecidas</dt>
            <dd class="shrink-0 text-lg font-bold tabular-nums">{{ moeda(dados.parcelasConhecidas) }}</dd>
          </dl>
          <small class="text-slate-600">
            @if (criacao()) { A confirmação recalcula a estimativa com a saída e os preços do momento. A prévia não ocupa o veículo. }
            @else { A estimativa usa os preços desta contratação. }
            Não é o valor final da devolução.
          </small>
        } @else {
          <p class="m-0 text-slate-600">Preencha os dados e calcule a estimativa para conferir os valores.</p>
        }
      </div>
      <div class="border-t border-slate-200 bg-slate-50 p-4" [class.rounded-b-xl]="!criacao()">
        <dl class="flex justify-between gap-3"><dt>Caução</dt>
          <dd class="font-semibold tabular-nums">{{ moeda(estimativa()?.caucao ?? '1000.00') }}</dd></dl>
        <small class="text-slate-600">Garantia separada, não incluída no custo do aluguel.</small>
      </div>
      <ng-content />
    </section>
  `,
})
export class DemonstrativoAluguelComponent {
  readonly titulo = input.required<string>();
  readonly estimativa = input<DemonstrativoEstimado | null>(null);
  readonly criacao = input(false);
  readonly moeda = formatarMoeda;
  readonly planos = rotuloPlano;
  readonly numero = new Intl.NumberFormat('pt-BR');
  periodo(n: number) { return `${n} ${n === 1 ? 'diária' : 'diárias'}`; }
  rotulo(item: ComponenteContratado) {
    return item.categoria !== 'Taxa' ? 'Seguro diário' : item.modalidade === 'Fixa' ? 'Taxa fixa' : 'Taxa diária';
  }
}
