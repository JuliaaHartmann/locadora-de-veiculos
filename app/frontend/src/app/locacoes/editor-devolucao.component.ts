import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, Injector, input, OnInit, output, signal } from '@angular/core';
import { disabled, form, FormField, required } from '@angular/forms/signals';
import { CadastrosService, decimalDoFormulario, formatarMoeda, RespostaComEtag } from '../cadastros.service';
import { AluguelAberto, AluguelDetalhe, AlugueisService, rotuloCombustivel, rotuloPlano, TaxaOpcao } from './alugueis.service';
import type { ComandoDevolucao, ConfirmacaoDevolucao, PreviaDevolucao } from './contratos-devolucao';
import { DemonstrativoDevolucaoComponent } from './demonstrativo-devolucao.component';
import { antesDe, aplicarMascaraData, horarioPreciso, retornoDaTela } from './horario-locacao';
import { SaidaCoordenador } from '../saida-coordenador.service';

const litrosEmMilesimos = (litros: string) => {
  const [inteira, fracao = ''] = litros.split('.');
  return BigInt(inteira) * 1000n + BigInt(fracao.padEnd(3, '0'));
};
@Component({
  standalone: true,
  selector: 'app-editor-devolucao',
  imports: [FormField, DemonstrativoDevolucaoComponent],
  templateUrl: './editor-devolucao.component.html',
  styleUrls: ['../quadro-operacional.css', './alugueis-cartoes.css'],
})
export class EditorDevolucaoComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly saida = inject(SaidaCoordenador);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private ativo = true;

  private previaComando: ComandoDevolucao | null = null;
  private envio: { id: string; comando: ConfirmacaoDevolucao; etag: string; chave: string } | null = null;

  readonly original = input.required<RespostaComEtag<AluguelAberto>>();
  readonly voltar = output<void>();
  readonly revisaoCarregada = output<RespostaComEtag<AluguelAberto>>();
  readonly concluido = output<{ resposta: RespostaComEtag<AluguelDetalhe>; confirmado: boolean }>();
  readonly atual = signal<RespostaComEtag<AluguelAberto> | null>(null);
  readonly contrato = computed(() => (this.atual() ?? this.original()).dados);
  readonly modelo = signal({ retornoReal: '', hodometroFinal: '', litrosRestantes: '' });
  readonly taxas = signal<TaxaOpcao[]>([]);
  readonly selecionadas = signal<ReadonlySet<string>>(new Set());
  readonly elegiveis = computed(() => {
    const contratadas = new Set(this.contrato().estimativa.adicionais.filter(x => x.categoria === 'Taxa').map(x => x.origemId));
    return this.taxas().filter(x => !contratadas.has(x.id));
  });
  readonly carregandoTaxas = signal(true);
  readonly falhaTaxas = signal(false);
  readonly calculando = signal(false);
  readonly gravando = signal(false);
  readonly recarregando = signal(false);
  readonly incerto = signal(false);
  readonly desatualizado = signal(false);
  readonly saidaBloqueia = computed(() => this.saida.fase() !== 'livre');
  readonly bloqueado = computed(() => this.calculando() || this.gravando() || this.recarregando() ||
    this.incerto() || this.desatualizado() || this.saidaBloqueia());
  readonly previa = signal<RespostaComEtag<PreviaDevolucao> | null>(null);
  readonly invalidada = signal(false);
  readonly erros = signal<Record<string, string>>({});
  readonly mensagens = computed(() => [...new Set(Object.values(this.erros()))]);
  readonly tituloErro = signal('Revise os dados do retorno.');
  readonly horario = horarioPreciso;
  readonly numero = new Intl.NumberFormat('pt-BR');
  readonly moeda = formatarMoeda;
  readonly combustiveis = rotuloCombustivel;
  readonly planos = rotuloPlano;
  readonly campos = [
    { chave: 'retornoReal', rotulo: 'Data e hora reais do retorno', dica: 'Formato: dia/mês/ano, hora e minuto (horário de São Paulo).',
      exemplo: '10/07/2027 14:30', modo: 'text' },
    { chave: 'hodometroFinal', rotulo: 'Hodômetro final (km)', dica: '', exemplo: 'Ex.: 21680', modo: 'numeric' },
    { chave: 'litrosRestantes', rotulo: 'Litros restantes (L)', dica: '', exemplo: 'Ex.: 40', modo: 'decimal' },
  ] as const;
  readonly formulario = form(this.modelo, campos => {
    required(campos.retornoReal);
    required(campos.hodometroFinal);
    required(campos.litrosRestantes);
    disabled(campos, () => this.bloqueado());
  });

  constructor() { inject(DestroyRef).onDestroy(() => this.ativo = false); }

  estadoSaida() {
    return {
      rascunhoAlterado: Object.values(this.modelo()).some(valor => valor.trim() !== ''),
      selecaoPendente: this.selecionadas().size > 0,
      tentativaIncerta: this.incerto(),
    };
  }
  ngOnInit() { void this.buscarTaxas(); }

  private focar(seletor: string) {
    if (this.ativo)
      afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(seletor)?.focus(), { injector: this.injector });
  }

  async buscarTaxas() {
    this.carregandoTaxas.set(true); this.falhaTaxas.set(false);
    try {
      const taxas = await this.api.taxas();
      if (this.ativo) {
        this.taxas.set(taxas);
        const idsElegiveis = new Set(this.elegiveis().map(x => x.id));
        if ([...this.selecionadas()].some(id => !idsElegiveis.has(id))) {
          this.selecionadas.update(ids => new Set([...ids].filter(id => idsElegiveis.has(id))));
          this.erros.update(erros => ({ ...erros, taxaIds: 'Uma taxa selecionada deixou de estar elegível. Confira a seleção antes de calcular.' }));
        }
      }
    } catch {
      if (this.ativo) this.falhaTaxas.set(true);
    } finally {
      if (this.ativo) this.carregandoTaxas.set(false);
    }
  }

  alterar() {
    if (this.previa()) this.invalidada.set(true);
    this.previa.set(null);
    this.previaComando = null;
    this.erros.set({});
  }

  alterarRetorno(evento: Event) {
    const retornoReal = aplicarMascaraData(evento, true);
    this.modelo.update(modelo => ({ ...modelo, retornoReal }));
    this.alterar();
  }

  alternarTaxa(id: string, evento: Event) {
    const marcadas = new Set(this.selecionadas());
    if ((evento.target as HTMLInputElement).checked) marcadas.add(id); else marcadas.delete(id);
    this.selecionadas.set(marcadas);
    this.alterar();
  }

  dica(campo: typeof this.campos[number]) {
    if (campo.chave === 'hodometroFinal')
      return `Quilometragem mínima: ${this.numero.format(this.contrato().hodometroInicial)} km.`;
    if (campo.chave === 'litrosRestantes')
      return `De 0 a ${this.contrato().veiculo.capacidadeLitros.replace('.', ',')} L, com até três casas decimais.`;
    return campo.dica;
  }

  private feedback(erros: Record<string, string>, titulo = 'Revise os dados do retorno.') {
    this.tituloErro.set(titulo);
    this.erros.set(erros);
    this.focar('#erros-devolucao');
  }

  private comando(): ComandoDevolucao | null {
    const modelo = this.modelo(), contrato = this.contrato();
    const erros: Record<string, string> = {};
    const momento = retornoDaTela(modelo.retornoReal, true);
    if ('erro' in momento) erros['retornoReal'] = momento.erro;
    else if (antesDe(momento.iso, contrato.saida)) erros['retornoReal'] = 'O retorno real não pode preceder a saída.';

    const hodometro = Number(modelo.hodometroFinal);
    if (!modelo.hodometroFinal.trim()) erros['hodometroFinal'] = 'Informe o hodômetro final.';
    else if (!/^\d+$/.test(modelo.hodometroFinal.trim()) || !Number.isSafeInteger(hodometro) || hodometro > 2147483647)
      erros['hodometroFinal'] = 'Informe o hodômetro final em quilômetros inteiros, sem casas decimais, até 2.147.483.647 km.';
    else if (hodometro < contrato.hodometroInicial)
      erros['hodometroFinal'] = `O hodômetro final não pode ser inferior a ${this.numero.format(contrato.hodometroInicial)} km.`;

    const litros = decimalDoFormulario(modelo.litrosRestantes);
    const capacidade = contrato.veiculo.capacidadeLitros;
    if (!modelo.litrosRestantes.trim()) erros['litrosRestantes'] = 'Informe quantos litros restam no tanque.';
    else if (!/^\d{1,9}(\.\d{1,3})?$/.test(litros))
      erros['litrosRestantes'] = 'Informe a quantidade de litros com até três casas decimais, como 40,125.';
    else if (litrosEmMilesimos(litros) > litrosEmMilesimos(capacidade))
      erros['litrosRestantes'] = `A quantidade restante não pode passar de ${capacidade.replace('.', ',')} L.`;
    if (Object.keys(erros).length || 'erro' in momento) { this.feedback(erros); return null; }
    return { retornoReal: momento.iso, hodometroFinal: hodometro, litrosRestantes: litros, taxaIds: [...this.selecionadas()] };
  }

  async calcular(evento: Event) {
    evento.preventDefault();
    if (this.bloqueado() || this.carregandoTaxas() || this.falhaTaxas()) return;
    this.alterar();
    const comando = this.comando();
    if (!comando) return;
    this.calculando.set(true);
    try {
      const previa = await this.api.preverDevolucao(this.contrato().id, comando);
      if (!this.ativo) return;
      if (previa.etag !== (this.atual() ?? this.original()).etag) { this.exigirReleitura(); return; }
      this.previaComando = comando;
      this.previa.set(previa); this.invalidada.set(false);
      this.focar('#titulo-calculo-devolucao');
    } catch (erro) {
      if (!this.ativo) return;
      const falhas = CadastrosService.errosDe(erro);
      if (falhas['estado']) { this.exigirReleitura(); return; }
      if (falhas['combustivel']) { this.feedback(falhas, 'Sem preço de combustível aplicável.'); return; }
      this.feedback(erro instanceof HttpErrorResponse && erro.status === 404 && comando.taxaIds.length
        ? { taxaIds: 'Uma taxa selecionada não está mais disponível. Revise as taxas ou consulte o contrato novamente.' }
        : falhas, 'Não foi possível calcular a devolução.');
    } finally { if (this.ativo) this.calculando.set(false); }
  }

  async confirmar() {
    if (this.gravando() || this.calculando() || this.desatualizado()) return;
    const previa = this.previa();
    if (!this.envio && !(previa && this.previaComando)) return;
    const liberar = this.saida.iniciarMutacao();
    if (!liberar) return;
    if (!this.envio && previa && this.previaComando) {
      this.envio = { id: this.contrato().id, etag: previa.etag, chave: crypto.randomUUID(),
        comando: { ...this.previaComando, versaoAceita: previa.dados.versaoAceita } };
    }
    const envio = this.envio;
    if (!envio) { liberar(); return; }
    this.gravando.set(true);
    this.erros.set({});
    try {
      const { dados, etag } = await this.api.confirmarDevolucao(envio.id, envio.comando, envio.etag, envio.chave);
      if (!this.ativo) return;
      const { estimativa: _estimativa, ...preservado } = this.contrato();
      this.envio = null;
      this.incerto.set(false);
      this.gravando.set(false);
      this.concluido.emit({ resposta: { dados: { ...preservado, ...dados }, etag }, confirmado: true });
    } catch (erro) {
      if (!this.ativo) return;
      const falhas = CadastrosService.errosDe(erro);
      if (erro instanceof HttpErrorResponse && erro.error?.code === 'devolucao_nao_gravada' && !this.incerto()) {
        this.envio = null;
        this.feedback({ geral: 'O aluguel continua em aberto e o veículo permanece ocupado. Os dados foram mantidos.' },
          'Não foi possível gravar a devolução.');
      } else if (!(erro instanceof HttpErrorResponse) || erro.status === 0 || erro.status >= 500 ||
          (this.incerto() && [401, 403].includes(erro.status))) {
        this.incerto.set(true);
        this.feedback({ geral: 'Repita o mesmo envio para recuperar o resultado, sem registrar outra devolução.' },
          'Resultado da devolução não confirmado.');
      } else {
        this.envio = null; this.incerto.set(false);
        this.alterar();
        if (erro.status === 412 || erro.status === 428 || falhas['estado']) this.exigirReleitura();
        else if (falhas['versaoAceita']) {
          this.feedback({ geral: 'Nenhum fechamento foi gravado. Calcule e aceite uma nova prévia com os preços atualizados.' },
            'O cálculo aceito mudou.');
          await this.buscarTaxas();
        } else this.feedback(falhas, 'Não foi possível gravar a devolução.');
      }
    } finally { liberar(); if (this.ativo) this.gravando.set(false); }
  }

  private exigirReleitura() {
    this.alterar(); this.desatualizado.set(true);
    this.feedback({ geral: 'A devolução não foi gravada por este envio. Recarregue o contrato e confira os dados antes de continuar.' },
      'O contrato mudou.');
  }

  async recarregar() {
    if (this.recarregando() || this.saidaBloqueia()) return;
    this.recarregando.set(true);
    try {
      const { dados, etag } = await this.api.obter(this.contrato().id);
      if (!this.ativo) return;
      if (dados.estado === 'Concluido') {
        this.concluido.emit({ resposta: { dados, etag }, confirmado: false });
        return;
      }
      const atual = { dados, etag };
      this.atual.set(atual);
      this.revisaoCarregada.emit(atual);
      this.erros.set({});
      await this.buscarTaxas();
      if (!this.ativo) return;
      this.desatualizado.set(false);
      this.focar('#devolucao-retornoReal');
    } catch {
      if (this.ativo) this.feedback({ geral: 'Não foi possível recarregar o contrato. Tente novamente.' }, 'O contrato mudou.');
    } finally { if (this.ativo) this.recarregando.set(false); }
  }

}
