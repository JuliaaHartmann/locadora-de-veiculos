import { afterNextRender, Component, DestroyRef, ElementRef, Injector, OnInit, inject, signal, viewChild } from '@angular/core';
import { CadastrosService, formatarMoeda, Pagina, RespostaComEtag } from '../cadastros.service';
import { AluguelDetalhe, AlugueisService, AluguelResumo, rotuloCliente, rotuloCombustivel, rotuloPlano, VeiculoOpcao } from './alugueis.service';
import { dataDeCadastro, dataDoFiltro, horaDoContrato, partesDaData } from './horario-locacao';
import { DemonstrativoAluguelComponent } from './demonstrativo.component';
import { EditorAluguelComponent } from './editor-aluguel.component';

type Tela = 'lista' | 'detalhe' | 'contratacao' | 'edicao';

@Component({
  standalone: true,
  imports: [DemonstrativoAluguelComponent, EditorAluguelComponent],
  templateUrl: './alugueis.component.html',
})
export class AlugueisComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly titulo = viewChild<ElementRef<HTMLElement>>('titulo');
  private leitura = 0;
  private filtros: Record<string, string | undefined> = {};
  private pagina = 1;

  readonly tela = signal<Tela>('lista');
  readonly carregando = signal(true);
  readonly erroConsulta = signal('');
  readonly erroFiltro = signal('');
  readonly filtroNaTela = signal({ veiculo: '', de: '', ate: '' });
  readonly aviso = signal('');
  readonly lista = signal<Pagina<AluguelResumo> | null>(null);
  readonly detalhe = signal<RespostaComEtag<AluguelDetalhe> | null>(null);
  readonly veiculos = signal<VeiculoOpcao[]>([]);
  readonly planos = rotuloPlano;
  readonly combustiveis = rotuloCombustivel;
  readonly clientes = rotuloCliente;
  readonly moeda = formatarMoeda;
  readonly horario = horaDoContrato;
  readonly dataCadastro = dataDeCadastro;
  readonly dataHora = partesDaData;
  readonly numero = new Intl.NumberFormat('pt-BR');
  private ultimaConsulta: () => Promise<void> = () => this.carregarLista();

  constructor() { inject(DestroyRef).onDestroy(() => this.leitura++); }

  ngOnInit() { void this.carregarLista(); }

  focar(seletor: string) {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(seletor)?.focus(), { injector: this.injector });
  }

  private mudarTela(tela: Tela) {
    this.tela.set(tela); this.aviso.set(''); this.erroConsulta.set('');
    afterNextRender(() => this.titulo()?.nativeElement.focus(), { injector: this.injector });
  }

  async carregarLista(focar = false) {
    const leitura = ++this.leitura;
    this.carregando.set(true); this.erroConsulta.set('');
    this.ultimaConsulta = () => this.carregarLista(true);
    try {
      const [lista, veiculos] = await Promise.all([this.api.listar(this.pagina, this.filtros), this.api.veiculos()]);
      if (leitura !== this.leitura) return;
      this.lista.set(lista); this.veiculos.set(veiculos);
    } catch {
      if (leitura === this.leitura) this.erroConsulta.set('Não foi possível consultar os aluguéis.');
    } finally {
      if (leitura === this.leitura) {
        this.carregando.set(false);
        if (focar) this.focar('#resultado-alugueis');
      }
    }
  }

  filtrar(evento: Event, veiculoId: string, de: string, ate: string) {
    evento.preventDefault();
    this.filtroNaTela.set({ veiculo: veiculoId, de, ate });
    const saidaDe = dataDoFiltro(de), saidaAte = dataDoFiltro(ate);
    if (saidaDe === null || saidaAte === null || (saidaDe && saidaAte && saidaAte < saidaDe)) {
      this.erroFiltro.set(saidaDe === null || saidaAte === null ? 'Informe datas válidas no formato dia/mês/ano.'
        : 'A data final não pode preceder a data inicial.');
      this.focar('#erro-filtro-alugueis'); return;
    }
    this.erroFiltro.set(''); this.filtros = { veiculoId, saidaDe, saidaAte }; this.pagina = 1;
    void this.carregarLista();
  }

  paginar(sentido: number) { this.pagina += sentido; void this.carregarLista(true); }
  tentarNovamente() { void this.ultimaConsulta(); }
  nova() { this.leitura++; this.detalhe.set(null); this.mudarTela('contratacao'); }
  voltarLista() { this.erroFiltro.set(''); this.mudarTela('lista'); void this.carregarLista(); }
  editar() { if (this.detalhe()) this.mudarTela('edicao'); }
  voltarDetalhe() { this.mudarTela('detalhe'); }

  async abrir(id: string) {
    this.detalhe.set(null); this.mudarTela('detalhe');
    const leitura = ++this.leitura;
    this.ultimaConsulta = () => this.abrir(id);
    this.carregando.set(true);
    try {
      const detalhe = await this.api.obter(id);
      if (leitura === this.leitura) this.detalhe.set(detalhe);
    } catch (erro) {
      if (leitura === this.leitura)
        this.erroConsulta.set(CadastrosService.errosDe(erro)['geral'] ?? 'Não foi possível consultar o aluguel.');
    } finally {
      if (leitura === this.leitura) this.carregando.set(false);
    }
  }

  gravado(resultado: RespostaComEtag<AluguelDetalhe>) {
    const editando = this.tela() === 'edicao';
    this.detalhe.set(resultado); this.carregando.set(false); this.mudarTela('detalhe');
    this.aviso.set(editando ? 'Alterações salvas. A revisão atual do contrato está disponível.'
      : 'Contratação confirmada. O veículo está ocupado por este aluguel.');
  }
}
