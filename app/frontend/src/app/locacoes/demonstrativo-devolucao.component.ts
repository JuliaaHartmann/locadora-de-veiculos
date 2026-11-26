import { Component, input } from '@angular/core';
import { formatarMoeda } from '../cadastros.service';
import { rotuloCombustivel, rotuloPlano } from './alugueis.service';
import type { DemonstrativoDevolucao } from './contratos-devolucao';

@Component({
  standalone: true,
  selector: 'app-demonstrativo-devolucao',
  styleUrl: './alugueis-cartoes.css',
  template: `
    <section class="cartao-aluguel" aria-labelledby="titulo-calculo-devolucao">
      <header class="border-b border-slate-200 p-4">
        <h2 id="titulo-calculo-devolucao" tabindex="-1" data-foco-gerenciado class="text-lg font-semibold">{{ definitivo() ? 'Demonstrativo final' : 'Prévia do cálculo final' }}</h2>
        <small class="text-slate-600">{{ definitivo() ? 'Valores registrados · Somente leitura' : 'Valores propostos · Não conclui nem libera o veículo' }}</small>
      </header>
      <div class="grid gap-4 p-4">
        @if (dados(); as d) {
          <p class="m-0 text-sm text-slate-600">{{ periodo(d.diarias) }} de 24 h · {{ numero.format(d.kmRodados) }} km rodados</p>
          <dl class="grid gap-3 [&>div]:flex [&>div]:flex-wrap [&>div]:justify-between [&>div]:gap-x-3 [&>div]:border-b [&>div]:border-slate-200 [&>div]:pb-2 [&_dt]:basis-32 [&_dt]:grow [&_dd]:font-semibold [&_dd]:tabular-nums [&_small]:block [&_small]:text-slate-600">
            <div><dt>Plano {{ planos[d.plano.parametros.modalidade] }}
              <small>{{ periodo(d.diarias) }} × {{ moeda(d.plano.parametros.precoDiario) }}
                @switch (d.plano.parametros.modalidade) {
                  @case ('Livre') { · sem cobrança por km }
                  @case ('Controlado') { · franquia de {{ numero.format(d.plano.franquiaTotalKm ?? 0) }} km · {{ numero.format(d.plano.kmCobrados) }} km excedentes × {{ moeda(d.plano.parametros.precoKm) }} }
                  @case ('Diario') { · {{ numero.format(d.plano.kmCobrados) }} km × {{ moeda(d.plano.parametros.precoKm) }} }
                }
              </small></dt><dd>{{ moeda(d.plano.subtotal.valor) }}</dd></div>
            @for (item of d.adicionais; track item.categoria + item.origemId) {
              <div><dt>{{ item.nome }}
                <small>
                  @if (item.incluidoNoRetorno) { Taxa incluída no retorno · }
                  @else if (item.categoria === 'Taxa') { {{ item.modalidade === 'Fixa' ? 'Taxa fixa contratada' : 'Taxa diária contratada' }} · }
                  @if (item.modalidade === 'Diaria') { {{ periodo(item.quantidade) }} × }
                  {{ moeda(item.preco) }}
                </small></dt><dd>{{ moeda(item.parcela.valor) }}</dd></div>
            }
            <div><dt>Reposição de combustível
              <small>{{ combustiveis[d.combustivel.tipo] }} · {{ litros(d.combustivel.litrosARepor) }} L × {{ moeda(d.combustivel.precoPorLitro) }} por litro</small>
              </dt><dd>{{ moeda(d.combustivel.parcela.valor) }}</dd></div>
            @if (d.multa) {
              <div><dt>Multa por atraso<small>10% somente sobre o subtotal do plano</small></dt><dd>{{ moeda(d.multa.valor) }}</dd></div>
            }
          </dl>
          <dl class="total-aluguel flex flex-wrap items-baseline justify-between gap-x-3 rounded-lg p-3">
            <dt class="font-semibold">{{ definitivo() ? 'Total final' : 'Total proposto' }}</dt>
            <dd class="text-xl font-bold tabular-nums">{{ moeda(d.total) }}</dd>
          </dl>
          <ng-content />
        } @else {
          <p class="informacao-aluguel m-0 rounded-lg p-3" role="status">
            {{ invalidada() ? 'A prévia anterior não pode mais ser confirmada. Calcule uma nova prévia.'
              : 'Preencha os dados do retorno e calcule a prévia para conferir os valores.' }}
          </p>
        }
      </div>
      <div class="caucao-aluguel rounded-b-xl border-t border-slate-200 p-4">
        <dl class="flex flex-wrap justify-between gap-2 font-semibold"><dt>Caução</dt><dd>{{ moeda(dados()?.caucao ?? '1000.00') }}</dd></dl>
        <small class="text-slate-600">Garantia separada, não incluída no custo.</small>
      </div>
    </section>
  `,
})
export class DemonstrativoDevolucaoComponent {
  readonly dados = input<DemonstrativoDevolucao | null>(null);
  readonly definitivo = input(false);
  readonly invalidada = input(false);
  readonly planos = rotuloPlano;
  readonly combustiveis = rotuloCombustivel;
  readonly numero = new Intl.NumberFormat('pt-BR');
  readonly moeda = formatarMoeda;
  periodo(n: number) { return `${n} ${n === 1 ? 'diária' : 'diárias'}`; }
  litros(n: string) { return n.replace('.', ','); }
}
