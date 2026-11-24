import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, OnInit, inject, signal, viewChild } from '@angular/core';
import { CadastrosService, formatarMoeda, Pagina, RespostaComEtag } from '../cadastros.service';
import { AluguelAberto, AluguelDetalhe, AlugueisService, AluguelResumo, rotuloCliente, rotuloCombustivel, rotuloPlano, VeiculoHistorico } from './alugueis.service';
import { dataDeCadastro, dataDoFiltro, horaDoContrato, horarioPreciso, partesDaData } from './horario-locacao';
import { DemonstrativoAluguelComponent } from './demonstrativo.component';
import { EditorAluguelComponent } from './editor-aluguel.component';
import { EditorDevolucaoComponent } from './editor-devolucao.component';
import { DemonstrativoDevolucaoComponent } from './demonstrativo-devolucao.component';

type Tela = 'lista' | 'detalhe' | 'contratacao' | 'edicao' | 'devolucao';

@Component({
  standalone: true,
  imports: [DemonstrativoAluguelComponent, EditorAluguelComponent, EditorDevolucaoComponent, DemonstrativoDevolucaoComponent],
  templateUrl: './alugueis.component.html',
})
export class AlugueisComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly titulo = viewChild<ElementRef<HTMLElement>>('titulo');
  private readonly editorDevolucao = viewChild(EditorDevolucaoComponent);
  private leitura = 0;
  private filtros: Record<string, string | undefined> = {};
  private incluirOcultos = false;
  private pagina = 1;
  private ultimoConsultado = '';

  readonly tela = signal<Tela>('lista');
  readonly carregando = signal(true);
  readonly erroConsulta = signal('');
  readonly erroFiltro = signal('');
  readonly errosDatas = signal({ de: '', ate: '' });
  readonly filtroNaTela = signal({ estado: '', veiculo: '', de: '', ate: '', incluir: false });
  readonly aviso = signal('');
  readonly lista = signal<Pagina<AluguelResumo> | null>(null);
  readonly detalhe = signal<RespostaComEtag<AluguelDetalhe> | null>(null);
  readonly veiculos = signal<VeiculoHistorico[]>([]);
  readonly ocultando = signal(false);
  readonly erroOcultar = signal('');
  readonly ocultacaoObsoleta = signal(false);
  readonly planos = rotuloPlano;
  readonly combustiveis = rotuloCombustivel;
  readonly clientes = rotuloCliente;
  readonly moeda = formatarMoeda;
  readonly horario = horaDoContrato;
  readonly horarioPreciso = horarioPreciso;
  readonly dataCadastro = dataDeCadastro;
  readonly dataHora = partesDaData;
  readonly numero = new Intl.NumberFormat('pt-BR');
  readonly arredondarParaCima = Math.ceil;
  private ultimaConsulta: () => Promise<void> = () => this.carregarLista();

  constructor() { inject(DestroyRef).onDestroy(() => this.leitura++); }

  ngOnInit() { void this.carregarLista(); }

  focar(seletor: string) {
    afterNextRender(() => {
      const alvo = seletor === 'consulta-anterior'
        ? [...this.host.nativeElement.querySelectorAll<HTMLElement>('[data-consultar]')]
            .find(botao => botao.dataset['consultar'] === this.ultimoConsultado) ??
          this.host.nativeElement.querySelector<HTMLElement>('#resultado-alugueis')
        : this.host.nativeElement.querySelector<HTMLElement>(seletor);
      alvo?.focus();
    }, { injector: this.injector });
  }

  private mudarTela(tela: Tela) {
    this.tela.set(tela); this.aviso.set(''); this.erroConsulta.set('');
    this.erroOcultar.set(''); this.ocultacaoObsoleta.set(false);
    afterNextRender(() => this.titulo()?.nativeElement.focus(), { injector: this.injector });
  }

  async carregarLista(foco = ''): Promise<void> {
    const leitura = ++this.leitura;
    this.carregando.set(true); this.erroConsulta.set('');
    this.ultimaConsulta = () => this.carregarLista('#resultado-alugueis');
    try {
      const [lista, veiculos] = await Promise.all([
        this.api.listar(this.pagina, this.filtros), this.api.veiculosDoHistorico(this.incluirOcultos),
      ]);
      if (leitura !== this.leitura) return;
      if (this.filtros['veiculoId'] && !veiculos.some(x => x.id === this.filtros['veiculoId'])) {
        this.filtros = { ...this.filtros, veiculoId: '' };
        this.filtroNaTela.update(f => ({ ...f, veiculo: '' }));
        this.pagina = 1;
        return await this.carregarLista(foco);
      }
      const ultimaPagina = Math.max(1, Math.ceil(lista.total / lista.pageSize));
      if (this.pagina > ultimaPagina) {
        this.pagina = ultimaPagina;
        return await this.carregarLista(foco);
      }
      this.lista.set(lista); this.veiculos.set(veiculos);
    } catch {
      if (leitura === this.leitura) this.erroConsulta.set('Não foi possível consultar os aluguéis.');
    } finally {
      if (leitura === this.leitura) {
        this.carregando.set(false);
        if (foco) this.focar(foco);
      }
    }
  }

  filtrar(evento: Event, estado: string, veiculoId: string, de: string, ate: string, incluir: boolean) {
    evento.preventDefault();
    this.aviso.set('');
    this.filtroNaTela.set({ estado, veiculo: veiculoId, de, ate, incluir });
    const saidaDe = dataDoFiltro(de), saidaAte = dataDoFiltro(ate);
    const faixaInvertida = !!saidaDe && !!saidaAte && saidaAte < saidaDe;
    this.errosDatas.set({ de: saidaDe === null ? 'Informe uma data válida.' : '',
      ate: saidaAte === null ? 'Informe uma data válida.' : faixaInvertida ? 'A data final não pode preceder a data inicial.' : '' });
    if (saidaDe === null || saidaAte === null || faixaInvertida) {
      this.erroFiltro.set(saidaDe === null || saidaAte === null ? 'Informe datas válidas no formato dia/mês/ano.'
        : 'A data final não pode preceder a data inicial.');
      this.focar('#erro-filtro-alugueis'); return;
    }
    this.erroFiltro.set('');
    if (!incluir && this.incluirOcultos) this.veiculos.set([]);
    this.incluirOcultos = incluir;
    this.filtros = { estado, veiculoId, saidaDe, saidaAte, incluirOcultos: incluir ? 'true' : undefined };
    this.pagina = 1;
    void this.carregarLista('#resultado-alugueis');
  }

  paginar(sentido: number) { this.pagina += sentido; void this.carregarLista('#resultado-alugueis'); }
  tentarNovamente() { void this.ultimaConsulta(); }
  nova() { this.leitura++; this.detalhe.set(null); this.mudarTela('contratacao'); }
  voltarLista() {
    this.erroFiltro.set(''); this.errosDatas.set({ de: '', ate: '' });
    this.mudarTela('lista');
    void this.carregarLista(this.ultimoConsultado ? 'consulta-anterior' : '');
  }
  editar() { if (this.detalhe()?.dados.estado === 'EmAberto') this.mudarTela('edicao'); }
  devolver() { if (this.detalhe()?.dados.estado === 'EmAberto') this.mudarTela('devolucao'); }
  voltarDetalhe() { this.mudarTela('detalhe'); }
  podeSair() { return this.editorDevolucao()?.podeSair() ?? true; }
  contratoAberto(): RespostaComEtag<AluguelAberto> | null {
    const atual = this.detalhe();
    return atual?.dados.estado === 'EmAberto' ? { etag: atual.etag, dados: atual.dados } : null;
  }

  devolvido(evento: { resposta: RespostaComEtag<AluguelDetalhe>; confirmado: boolean }) {
    this.detalhe.set(evento.resposta);
    this.mudarTela('detalhe');
    if (evento.confirmado) {
      this.aviso.set('Devolução concluída. O veículo está disponível para nova contratação.');
      this.focar('#aviso-alugueis');
    }
  }

  async abrir(id: string) {
    this.ultimoConsultado = id;
    this.detalhe.set(null); this.mudarTela('detalhe');
    const leitura = ++this.leitura;
    this.ultimaConsulta = () => this.abrir(id);
    this.carregando.set(true);
    try {
      const detalhe = await this.api.obter(id, this.incluirOcultos);
      if (leitura === this.leitura) this.detalhe.set(detalhe);
    } catch (erro) {
      if (leitura === this.leitura)
        this.erroConsulta.set(CadastrosService.errosDe(erro)['geral'] ?? 'Não foi possível consultar o aluguel.');
    } finally {
      if (leitura === this.leitura) {
        this.carregando.set(false);
        if (this.erroConsulta()) this.focar('#erro-detalhe-aluguel');
        else afterNextRender(() => this.titulo()?.nativeElement.focus(), { injector: this.injector });
      }
    }
  }

  async ocultar() {
    const detalhe = this.detalhe();
    if (!detalhe || detalhe.dados.estado !== 'Concluido' || detalhe.dados.oculto ||
        this.ocultando() || this.ocultacaoObsoleta()) return;
    this.ocultando.set(true);
    this.erroOcultar.set('');
    const leitura = this.leitura;
    try {
      await this.api.ocultar(detalhe.dados.id, detalhe.etag);
      if (leitura !== this.leitura) return;
      this.mudarTela('lista');
      this.aviso.set(`Aluguel ${detalhe.dados.id} ocultado da operação. O contrato e o demonstrativo foram preservados.`);
      this.focar('#aviso-alugueis');
      void this.carregarLista();
    } catch (erro) {
      if (leitura !== this.leitura) return;
      const obsoleto = erro instanceof HttpErrorResponse && [404, 412, 428].includes(erro.status);
      this.ocultacaoObsoleta.set(obsoleto);
      this.erroOcultar.set(obsoleto ? 'O aluguel não foi ocultado por este envio porque a versão mudou. Volte à lista e consulte novamente.'
        : 'Não foi possível confirmar a ocultação do aluguel. Consulte novamente ou tente repetir a operação.');
      this.focar('#erro-ocultar-aluguel');
    } finally { this.ocultando.set(false); }
  }

  gravado(resultado: RespostaComEtag<AluguelDetalhe>) {
    const editando = this.tela() === 'edicao';
    this.detalhe.set(resultado); this.carregando.set(false); this.mudarTela('detalhe');
    this.aviso.set(editando ? 'Alterações salvas. A revisão atual do contrato está disponível.'
      : 'Contratação confirmada. O veículo está ocupado por este aluguel.');
  }
}
