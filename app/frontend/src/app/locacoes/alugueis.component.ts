import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, ChangeDetectorRef, Component, computed, DestroyRef, ElementRef, Injector, OnInit, inject, signal, viewChild } from '@angular/core';
import { LucideCircleCheck, LucideX } from '@lucide/angular';
import { CadastrosService, formatarMoeda, Pagina, RespostaComEtag } from '../cadastros.service';
import { AluguelAberto, AluguelDetalhe, AlugueisService, AluguelResumo, rotuloCliente, rotuloCombustivel, rotuloPlano, VeiculoHistorico } from './alugueis.service';
import { aplicarMascaraData, dataDeCadastro, dataDoFiltro, horaDoContrato, horarioPreciso, partesDaData } from './horario-locacao';
import { DemonstrativoAluguelComponent } from './demonstrativo.component';
import { EditorAluguelComponent } from './editor-aluguel.component';
import { EditorDevolucaoComponent } from './editor-devolucao.component';
import { DemonstrativoDevolucaoComponent } from './demonstrativo-devolucao.component';
import { SaidaCoordenador } from '../saida-coordenador.service';
import { SessaoService } from '../sessao.service';

type Tela = 'lista' | 'detalhe' | 'contratacao' | 'edicao' | 'devolucao';

@Component({
  standalone: true,
  imports: [DemonstrativoAluguelComponent, EditorAluguelComponent, EditorDevolucaoComponent, DemonstrativoDevolucaoComponent, LucideCircleCheck, LucideX],
  templateUrl: './alugueis.component.html',
  styleUrls: ['../quadro-operacional.css', './alugueis-cartoes.css', './alugueis.component.css'],
  host: { '(window:beforeunload)': 'avisarRecarregamento($event)' },
})
export class AlugueisComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly saida = inject(SaidaCoordenador);
  private readonly sessao = inject(SessaoService);
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoOcultar');
  private readonly dialogoDescarte = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoDescarte');
  readonly faseSaida = this.saida.fase;
  private readonly injector = inject(Injector);
  private readonly mudancas = inject(ChangeDetectorRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly titulo = viewChild<ElementRef<HTMLElement>>('titulo');
  private readonly editorDevolucao = viewChild(EditorDevolucaoComponent);
  private readonly editorAluguel = viewChild(EditorAluguelComponent);
  private readonly estadoEditor = computed(() => this.editorAluguel()?.estadoSaida() ?? this.editorDevolucao()?.estadoSaida());
  private confirmacaoDescarte?: { resultado: Promise<boolean>; resolver: (confirmado: boolean) => void };
  readonly autorizarDescarte = () => this.podeSair();
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
  readonly anuncio = signal('');
  readonly mensagemDescarte = signal('');
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

  constructor() {
    const destruir = inject(DestroyRef);
    destruir.onDestroy(this.saida.registrar({ estado: () => this.estadoEditor() ?? {} }));
    destruir.onDestroy(() => { this.leitura++; this.confirmacaoDescarte?.resolver(false); });
  }

  ngOnInit() {
    this.focar('#titulo-alugueis');
    void this.carregarLista();
  }

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
    this.tela.set(tela); this.limparAviso(); this.erroConsulta.set('');
    this.erroOcultar.set(''); this.ocultacaoObsoleta.set(false);
    afterNextRender(() => this.titulo()?.nativeElement.focus(), { injector: this.injector });
  }

  limparAviso() { this.aviso.set(''); this.anuncio.set(''); }

  reautenticar() {
    const contexto = this.sessao.contexto();
    if (contexto !== undefined) this.sessao.reautenticar(contexto);
  }

  private publicarSucesso(mensagem: string) {
    this.aviso.set(mensagem);
    // A região viva já existe; muda depois que o foco chegou ao título da nova tela.
    afterNextRender(() => {
      if (this.aviso() === mensagem) this.anuncio.set(mensagem);
    }, { injector: this.injector });
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
    this.limparAviso();
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
  alterarDataFiltro(campo: 'de' | 'ate', evento: Event) {
    aplicarMascaraData(evento);
    if (this.errosDatas()[campo]) this.errosDatas.update(erros => ({ ...erros, [campo]: '' }));
    this.erroFiltro.set('');
  }
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
  async voltarDoEditor() {
    if (await this.podeSair()) {
      if (this.tela() === 'contratacao') this.voltarLista();
      else this.voltarDetalhe();
    }
  }

  podeSair() {
    // Após o encerramento da sessão, a confirmação de Sair já foi resolvida.
    if (this.sessao.contexto() === undefined) return true;
    if (this.faseSaida() !== 'livre' || this.saida.ocupada()) return false;
    const estado = this.estadoEditor();
    if (!estado?.tentativaIncerta && !estado?.rascunhoAlterado && !estado?.selecaoPendente) return true;
    if (this.confirmacaoDescarte) return this.confirmacaoDescarte.resultado;
    let resolver!: (confirmado: boolean) => void;
    const resultado = new Promise<boolean>(r => resolver = r);
    this.confirmacaoDescarte = { resultado, resolver };
    const dialogo = this.dialogoDescarte().nativeElement;
    this.mensagemDescarte.set(estado.tentativaIncerta
      ? 'A gravação ainda não foi confirmada. Se você voltar ou trocar de tela, não poderá repetir o mesmo envio aqui.'
      : 'Você tem alterações não salvas. Se continuar, os dados em edição serão descartados.');
    this.mudancas.detectChanges();
    dialogo.showModal();
    return resultado;
  }

  fecharDescarte(confirmado: boolean) {
    const pendente = this.confirmacaoDescarte;
    this.confirmacaoDescarte = undefined;
    const dialogo = this.dialogoDescarte().nativeElement;
    if (dialogo.open) dialogo.close();
    pendente?.resolver(confirmado && this.faseSaida() === 'livre' && !this.saida.ocupada());
  }

  avisarRecarregamento(evento: BeforeUnloadEvent) {
    if (!this.sessao.contexto()) return;
    const estado = this.estadoEditor();
    if (estado?.rascunhoAlterado || estado?.selecaoPendente || estado?.tentativaIncerta || this.saida.ocupada()) {
      evento.preventDefault();
      evento.returnValue = true;
    }
  }
  contratoAberto(): RespostaComEtag<AluguelAberto> | null {
    const atual = this.detalhe();
    return atual?.dados.estado === 'EmAberto' ? { etag: atual.etag, dados: atual.dados } : null;
  }

  devolvido(evento: { resposta: RespostaComEtag<AluguelDetalhe>; confirmado: boolean }) {
    this.detalhe.set(evento.resposta);
    this.mudarTela('detalhe');
    if (evento.confirmado) {
      this.publicarSucesso(`Devolução do aluguel ${evento.resposta.dados.numeroExibicao} concluída. O veículo está disponível para nova contratação.`);
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

  pedirOcultacao() {
    const contrato = this.detalhe()?.dados;
    if (contrato?.estado === 'Concluido' && !contrato.oculto &&
        !this.ocultando() && !this.ocultacaoObsoleta() && this.faseSaida() === 'livre')
      this.dialogo().nativeElement.showModal();
  }

  fecharOcultacao() { this.dialogo().nativeElement.close(); }

  conterFoco(evento: KeyboardEvent, cancelar: HTMLButtonElement, confirmar: HTMLButtonElement) {
    if (evento.key !== 'Tab' || evento.target !== (evento.shiftKey ? cancelar : confirmar)) return;
    evento.preventDefault();
    (evento.shiftKey ? confirmar : cancelar).focus();
  }

  async ocultar() {
    if (!this.dialogo().nativeElement.open) return;
    this.fecharOcultacao();
    const detalhe = this.detalhe();
    if (!detalhe || detalhe.dados.estado !== 'Concluido' || detalhe.dados.oculto ||
        this.ocultando() || this.ocultacaoObsoleta()) return;
    const liberar = this.saida.iniciarMutacao();
    if (!liberar) return;
    this.ocultando.set(true);
    this.erroOcultar.set('');
    const leitura = this.leitura;
    try {
      await this.api.ocultar(detalhe.dados.id, detalhe.etag);
      if (leitura !== this.leitura) return;
      this.mudarTela('lista');
      this.publicarSucesso(`Aluguel ${detalhe.dados.numeroExibicao} ocultado da operação. O contrato e o demonstrativo foram preservados.`);
      void this.carregarLista();
    } catch (erro) {
      if (leitura !== this.leitura) return;
      const obsoleto = erro instanceof HttpErrorResponse && [404, 412, 428].includes(erro.status);
      this.ocultacaoObsoleta.set(obsoleto);
      this.erroOcultar.set(obsoleto ? 'O aluguel não foi ocultado por este envio porque a versão mudou. Volte à lista e consulte novamente.'
        : 'Não foi possível confirmar a ocultação do aluguel. Consulte novamente ou tente repetir a operação.');
      this.focar('#erro-ocultar-aluguel');
    } finally { liberar(); this.ocultando.set(false); }
  }

  gravado(resultado: RespostaComEtag<AluguelDetalhe>) {
    const editando = this.tela() === 'edicao';
    this.detalhe.set(resultado); this.carregando.set(false); this.mudarTela('detalhe');
    this.publicarSucesso(editando ? `Alterações do aluguel ${resultado.dados.numeroExibicao} salvas. A revisão atual está disponível.`
      : `Contratação do aluguel ${resultado.dados.numeroExibicao} confirmada. O veículo está ocupado por este aluguel.`);
  }
}
