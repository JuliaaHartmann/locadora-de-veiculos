import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormField, disabled, form, required } from '@angular/forms/signals';
import { LucideCheck, LucidePencil, LucideTrash2, LucideX } from '@lucide/angular';
import { CadastrosService, decimalDoFormulario, formatarData, formatarMoeda, validarLimites } from './cadastros.service';
import { SaidaCoordenador } from './saida-coordenador.service';
import { SessaoService } from './sessao.service';

type Aba = 'taxas' | 'seguros' | 'combustiveis';
type ModalidadeTaxa = 'Fixa' | 'Diaria';
type TipoSeguro = 'Cliente' | 'Terceiros';
type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';
type Modo = 'lista' | 'taxa' | 'seguro' | 'combustivel';

interface Taxa { id: string; numeroExibicao: string; nome: string; preco: string; modalidade: ModalidadeTaxa; }
interface SeguroEstado { tipo: TipoSeguro; configurado: boolean; precoDiario: string | null; }
interface CombustivelEstado {
  tipo: Combustivel; configurado: boolean; id: string | null; preco: string | null;
  inicioVigenciaUtc: string | null; ordem: number | null;
}
interface Versao { id: string; numeroExibicao: string; tipo: Combustivel; preco: string; inicioVigenciaUtc: string; ordem: number; }
interface TaxaModelo { nome: string; preco: string; modalidade: ModalidadeTaxa; }
interface SeguroModelo { precoDiario: string; }
interface CombustivelModelo { preco: string; }

@Component({
  standalone: true,
  imports: [FormField, LucideCheck, LucidePencil, LucideTrash2, LucideX],
  templateUrl: './precos.component.html',
  styleUrls: ['./quadro-operacional.css', './precos.component.css'],
})
export class PrecosComponent {
  private consultaAtual = 0;
  private consultaEditor = 0;
  private consultaHistorico = 0;
  private referenciaEditor = '';
  private retornoEditor = '';
  private origemExclusao?: HTMLElement;
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destruicao = inject(DestroyRef);
  private readonly saida = inject(SaidaCoordenador);
  private readonly sessao = inject(SessaoService);
  private readonly contexto = this.sessao.contexto();
  private readonly dialogo = viewChild<ElementRef<HTMLDialogElement>>('dialogo');

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'taxas', rotulo: 'Taxas e serviços' },
    { valor: 'seguros', rotulo: 'Seguros' },
    { valor: 'combustiveis', rotulo: 'Combustíveis' },
  ];
  readonly tiposSeguro: TipoSeguro[] = ['Cliente', 'Terceiros'];
  readonly tiposCombustivel: Combustivel[] = ['Gasolina', 'Gas', 'Diesel', 'Alcool'];

  readonly aba = signal<Aba>('taxas');
  readonly modo = signal<Modo>('lista');
  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly faseSaida = this.saida.fase;
  readonly bloqueado = computed(() => this.salvando() || this.faseSaida() !== 'livre');
  readonly consultandoEditor = signal(false);
  readonly listaBloqueada = computed(() => this.bloqueado() || this.consultandoEditor() ||
    this.carregando() || !!this.erroConsulta());
  readonly exclusao = signal<Taxa | null>(null);
  readonly conflito = signal(false);
  readonly erroConsulta = signal('');
  readonly carregandoHistorico = signal(false);
  readonly erroHistorico = signal('');
  readonly aviso = signal('');
  readonly erros = signal<Record<string, string>>({});
  readonly pagina = signal(1);
  readonly total = signal(0);
  readonly editandoId = signal<string | null>(null);
  readonly etag = signal('');
  readonly taxas = signal<Taxa[]>([]);
  readonly seguros = signal<Partial<Record<TipoSeguro, { estado: SeguroEstado; etag: string }>>>({});
  readonly combustiveis = signal<Partial<Record<Combustivel, { estado: CombustivelEstado; etag: string }>>>({});
  readonly seguroTipo = signal<TipoSeguro>('Cliente');
  readonly combustivelTipo = signal<Combustivel>('Gasolina');
  readonly historico = signal<Versao[]>([]);
  readonly historicoTipo = signal<Combustivel>('Gasolina');
  readonly historicoPagina = signal(1);
  readonly historicoTotal = signal(0);

  readonly temErros = computed(() => Object.keys(this.erros()).length > 0);
  readonly mensagensErro = computed(() => Object.values(this.erros()));
  readonly tituloEditor = computed(() => this.modo() === 'taxa'
    ? (this.editandoId() ? 'Editar taxa' : 'Cadastrar taxa')
    : this.modo() === 'seguro' ? this.rotuloSeguro(this.seguroTipo())
      : `Adicionar preço — ${this.rotuloCombustivel(this.combustivelTipo())}`);

  readonly taxaModelo = signal<TaxaModelo>({ nome: '', preco: '', modalidade: 'Fixa' });
  readonly taxaForm = form(this.taxaModelo, caminho => {
    required(caminho.nome, { message: 'Informe o nome.' });
    required(caminho.preco, { message: 'Informe o preço.' });
    disabled(caminho, { when: () => this.bloqueado() || this.consultandoEditor() });
  });
  readonly seguroModelo = signal<SeguroModelo>({ precoDiario: '' });
  readonly seguroForm = form(this.seguroModelo, caminho => {
    required(caminho.precoDiario, { message: 'Informe o preço diário.' });
    disabled(caminho, { when: () => this.bloqueado() || this.consultandoEditor() });
  });
  readonly combustivelModelo = signal<CombustivelModelo>({ preco: '' });
  readonly combustivelForm = form(this.combustivelModelo, caminho => {
    required(caminho.preco, { message: 'Informe o preço.' });
    disabled(caminho, { when: () => this.bloqueado() || this.consultandoEditor() });
  });

  constructor() {
    this.destruicao.onDestroy(this.saida.registrar({ estado: () => ({
      rascunhoAlterado: this.modo() !== 'lista' && this.snapshotEditor() !== this.referenciaEditor,
    }) }));
    void this.carregar();
  }

  private contextoAtual() {
    return !this.destruicao.destroyed && this.contexto === this.sessao.contexto();
  }

  podeNavegar() {
    return this.faseSaida() === 'livre' && !this.saida.ocupada();
  }

  reautenticar() {
    if (this.contexto !== undefined && this.contextoAtual()) this.sessao.reautenticar(this.contexto);
  }

  private snapshotEditor() {
    return this.modo() === 'taxa' ? this.snapshotTaxa(this.taxaModelo())
      : this.snapshotPreco(this.modo() === 'seguro' ? this.seguroModelo().precoDiario : this.combustivelModelo().preco);
  }

  private snapshotTaxa(taxa: TaxaModelo) {
    return JSON.stringify([taxa.nome.trim(), this.snapshotPreco(taxa.preco), taxa.modalidade]);
  }

  private snapshotPreco(valor: string) {
    const decimal = decimalDoFormulario(valor);
    // Mantém entradas inválidas distintas e evita arredondamento de ponto flutuante.
    if (!/^\d{1,13}(\.\d{1,6})?$/.test(decimal)) return decimal;
    return decimal.replace(/^0+(?=\d)/, '').replace(/(\.\d*?)0+$/, '$1').replace(/\.$/, '');
  }

  navegarAbas(evento: KeyboardEvent) {
    const indice = this.abas.findIndex(item => item.valor === this.aba());
    const total = this.abas.length;
    const destino = evento.key === 'Home' ? 0 : evento.key === 'End' ? total - 1
      : evento.key === 'ArrowLeft' ? (indice + total - 1) % total
        : evento.key === 'ArrowRight' ? (indice + 1) % total : -1;
    if (destino < 0) return;
    evento.preventDefault();
    if (this.bloqueado()) return;
    const aba = this.abas[destino].valor;
    this.trocarAba(aba);
    this.focar('aba-' + aba);
  }

  trocarAba(aba: Aba) {
    if (this.bloqueado() || this.aba() === aba) return;
    this.fecharEditor();
    ++this.consultaHistorico;
    this.aba.set(aba);
    this.pagina.set(1);
    this.aviso.set('');
    void this.carregar();
  }

  async carregar() {
    const consulta = ++this.consultaAtual;
    const atual = () => this.contextoAtual() && consulta === this.consultaAtual;
    this.carregando.set(true);
    this.erroConsulta.set('');
    try {
      if (this.aba() === 'taxas') {
        let pagina = await this.servico.listar<Taxa>('/taxas-servicos', this.pagina());
        if (!atual()) return;
        const ultima = Math.max(1, Math.ceil(pagina.total / pagina.pageSize));
        if (this.pagina() > ultima) {
          this.pagina.set(ultima);
          pagina = await this.servico.listar<Taxa>('/taxas-servicos', ultima);
          if (!atual()) return;
        }
        this.taxas.set(pagina.items);
        this.total.set(pagina.total);
      } else if (this.aba() === 'seguros') {
        const resultados = await Promise.all(this.tiposSeguro.map(async tipo => {
          const resposta = await this.servico.obter<SeguroEstado>(`/configuracoes/seguros/${tipo}`);
          return [tipo, { estado: resposta.dados, etag: resposta.etag }] as const;
        }));
        if (!atual()) return;
        this.seguros.set(Object.fromEntries(resultados));
      } else {
        const resultados = await Promise.all(this.tiposCombustivel.map(async tipo => {
          const resposta = await this.servico.obter<CombustivelEstado>(`/configuracoes/combustiveis/${tipo}`);
          return [tipo, { estado: resposta.dados, etag: resposta.etag }] as const;
        }));
        if (!atual()) return;
        this.combustiveis.set(Object.fromEntries(resultados));
        this.carregando.set(false);
        await this.carregarHistorico();
      }
    } catch {
      if (!atual()) return;
      this.erroConsulta.set('Não foi possível consultar as configurações. Verifique a conexão e tente novamente.');
    } finally {
      if (atual()) this.carregando.set(false);
    }
  }

  private async carregarHistorico() {
    const consulta = ++this.consultaHistorico;
    const atual = () => this.contextoAtual() && consulta === this.consultaHistorico;
    const tipo = this.historicoTipo();
    this.carregandoHistorico.set(true);
    this.erroHistorico.set('');
    try {
      const pagina = await this.servico.listar<Versao>(
        `/configuracoes/combustiveis/${tipo}/historico`, this.historicoPagina());
      if (!atual()) return;
      this.historico.set(pagina.items);
      this.historicoTotal.set(pagina.total);
    } catch {
      if (atual()) this.erroHistorico.set('Não foi possível consultar o histórico. Verifique a conexão e tente novamente.');
    } finally {
      if (atual()) this.carregandoHistorico.set(false);
    }
  }

  tentarConsulta() {
    if (!this.bloqueado()) void this.carregar();
  }

  tentarHistorico() {
    if (!this.bloqueado()) void this.carregarHistorico();
  }

  paginar(delta: number) {
    if (this.listaBloqueada()) return;
    this.pagina.update(x => Math.max(1, x + delta));
    void this.carregar();
  }

  paginarHistorico(delta: number) {
    if (this.bloqueado() || this.carregandoHistorico()) return;
    this.historicoPagina.update(x => Math.max(1, x + delta));
    void this.carregarHistorico();
  }

  mudarHistorico(evento: Event) {
    this.selecionarHistorico((evento.target as HTMLSelectElement).value as Combustivel);
  }

  verHistorico(tipo: Combustivel) {
    if (this.listaBloqueada()) return;
    this.selecionarHistorico(tipo);
    this.focar('titulo-historico');
  }

  private selecionarHistorico(tipo: Combustivel) {
    if (this.bloqueado()) return;
    this.historicoTipo.set(tipo);
    this.historicoPagina.set(1);
    void this.carregarHistorico();
  }

  estadoSeguro(tipo: TipoSeguro): SeguroEstado | undefined {
    return this.seguros()[tipo]?.estado;
  }

  estadoCombustivel(tipo: Combustivel): CombustivelEstado | undefined {
    return this.combustiveis()[tipo]?.estado;
  }

  novaTaxa() {
    if (this.listaBloqueada()) return;
    this.editandoId.set(null);
    this.etag.set('');
    this.taxaModelo.set({ nome: '', preco: '', modalidade: 'Fixa' });
    this.abrirEditor('taxa', 'nova-taxa');
  }

  async editarTaxa(id: string) {
    await this.consultarEditor<Taxa>(`/taxas-servicos/${id}?paraEdicao=true`, resposta => {
      this.taxaModelo.set({
        nome: resposta.dados.nome, preco: resposta.dados.preco, modalidade: resposta.dados.modalidade,
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.abrirEditor('taxa', 'editar-taxa-' + id);
    });
  }

  async configurarSeguro(tipo: TipoSeguro) {
    await this.consultarEditor<SeguroEstado>(`/configuracoes/seguros/${tipo}`, resposta => {
      this.seguros.update(x => ({ ...x, [tipo]: { estado: resposta.dados, etag: resposta.etag } }));
      this.seguroTipo.set(tipo);
      this.etag.set(resposta.etag);
      this.seguroModelo.set({ precoDiario: resposta.dados.precoDiario ?? '' });
      this.abrirEditor('seguro', 'seguro-' + tipo);
    });
  }

  async adicionarPreco(tipo: Combustivel) {
    await this.consultarEditor<CombustivelEstado>(`/configuracoes/combustiveis/${tipo}`, resposta => {
      this.combustiveis.update(x => ({ ...x, [tipo]: { estado: resposta.dados, etag: resposta.etag } }));
      this.combustivelTipo.set(tipo);
      this.etag.set(resposta.etag);
      this.combustivelModelo.set({ preco: resposta.dados.preco ?? '' });
      this.abrirEditor('combustivel', 'combustivel-' + tipo);
    });
  }

  private async consultarEditor<T>(caminho: string, aplicar: (resposta: { dados: T; etag: string }) => void) {
    if (this.listaBloqueada()) return;
    const consulta = ++this.consultaEditor;
    const atual = () => consulta === this.consultaEditor && this.contextoAtual();
    this.consultandoEditor.set(true);
    this.erros.set({});
    try {
      const resposta = await this.servico.obter<T>(caminho);
      if (atual() && !this.bloqueado()) aplicar(resposta);
    } catch (erro: unknown) {
      if (atual() && !this.bloqueado()) this.falhar(erro);
    } finally {
      if (atual()) this.consultandoEditor.set(false);
    }
  }

  private abrirEditor(modo: Exclude<Modo, 'lista'>, retorno: string) {
    this.modo.set(modo);
    this.referenciaEditor = this.snapshotEditor();
    this.retornoEditor = retorno;
    this.aviso.set('');
    this.focar('titulo-editor');
  }

  async recarregarVersao() {
    if (!this.conflito() || this.bloqueado()) return;
    const modo = this.modo();
    const caminho = modo === 'taxa' ? `/taxas-servicos/${this.editandoId()}?paraEdicao=true`
      : modo === 'seguro' ? `/configuracoes/seguros/${this.seguroTipo()}`
        : `/configuracoes/combustiveis/${this.combustivelTipo()}`;
    await this.consultarEditor<Taxa | SeguroEstado | CombustivelEstado>(caminho, ({ dados, etag }) => {
      // Atualiza a referência confirmada, mas mantém a entrada para revisão e envio explícito.
      this.referenciaEditor = 'modalidade' in dados ? this.snapshotTaxa(dados)
        : this.snapshotPreco(('precoDiario' in dados ? dados.precoDiario : dados.preco) ?? '');
      this.etag.set(etag);
      this.conflito.set(false);
      this.falharCampos({ geral: 'Versão atual consultada. Revise os dados mantidos antes de salvar novamente.' });
    });
  }

  salvarEditor() {
    switch (this.modo()) {
      case 'taxa': return this.salvarTaxa();
      case 'seguro': return this.salvarSeguro();
      case 'combustivel': return this.salvarCombustivel();
      default: return;
    }
  }

  async salvarTaxa() {
    if (!this.podeSalvar('taxa')) return;
    const modelo = this.taxaModelo();
    const preco = decimalDoFormulario(modelo.preco);
    const erros = validarLimites({ nome: modelo.nome }, { nome: 200 });
    if (!modelo.nome.trim()) erros['nome'] = 'Informe o nome.';
    if (!preco) erros['preco'] = 'Informe o preço.';
    else if (!/^\d{1,13}(\.\d{1,6})?$/.test(preco)) erros['preco'] = 'Informe um valor não negativo com até 6 casas.';
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    await this.executarSalvar(async () => {
      const corpo = { nome: modelo.nome, preco, modalidade: modelo.modalidade };
      if (this.editandoId()) {
        await this.servico.editar(`/taxas-servicos/${this.editandoId()}`, corpo, this.etag());
      } else {
        await this.servico.criar('/taxas-servicos', corpo);
      }
    }, `Taxa salva: ${modelo.nome.trim()}.`);
  }

  async salvarSeguro() {
    if (!this.podeSalvar('seguro')) return;
    const preco = decimalDoFormulario(this.seguroModelo().precoDiario);
    if (!preco) { this.falharCampos({ precoDiario: 'Informe o preço diário.' }); return; }
    if (!/^\d{1,13}(\.\d{1,6})?$/.test(preco)) {
      this.falharCampos({ precoDiario: 'Informe um valor não negativo com até 6 casas.' });
      return;
    }
    await this.executarSalvar(async () => {
      await this.servico.editar(`/configuracoes/seguros/${this.seguroTipo()}`,
        { precoDiario: preco }, this.etag());
    }, `${this.rotuloSeguro(this.seguroTipo())}: preço salvo.`);
  }

  async salvarCombustivel() {
    if (!this.podeSalvar('combustivel')) return;
    const preco = decimalDoFormulario(this.combustivelModelo().preco);
    if (!preco) { this.falharCampos({ preco: 'Informe o preço.' }); return; }
    if (!/^\d{1,13}(\.\d{1,6})?$/.test(preco)) {
      this.falharCampos({ preco: 'Informe um valor não negativo com até 6 casas.' });
      return;
    }
    const tipo = this.combustivelTipo();
    await this.executarSalvar(async () => {
      await this.servico.editar(`/configuracoes/combustiveis/${tipo}`, { preco }, this.etag());
      if (!this.contextoAtual()) return;
      this.historicoTipo.set(tipo);
      this.historicoPagina.set(1);
    }, `${this.rotuloCombustivel(tipo)}: preço adicionado.`);
  }

  solicitarExclusao(taxa: Taxa, evento: Event) {
    if (this.listaBloqueada() || this.exclusao()) return;
    this.exclusao.set(taxa);
    this.origemExclusao = evento.currentTarget as HTMLElement;
    afterNextRender(() => {
      if (this.contextoAtual() && this.exclusao() === taxa) this.dialogo()?.nativeElement.showModal();
    }, { injector: this.injector });
  }

  fecharExclusao(confirmada: boolean) {
    const taxa = this.exclusao();
    const origem = this.origemExclusao;
    this.dialogo()?.nativeElement.close();
    this.exclusao.set(null);
    this.origemExclusao = undefined;
    if (confirmada && taxa) {
      const caminho = `/taxas-servicos/${taxa.id}`;
      return this.executarSalvar(async () => {
        const resposta = await this.servico.obter<unknown>(caminho);
        if (this.contextoAtual()) await this.servico.excluir(caminho, resposta.etag);
      }, `Taxa excluída: ${taxa.nome}.`);
    }
    origem?.focus();
    return;
  }

  conterFoco(evento: KeyboardEvent, cancelar: HTMLButtonElement, excluir: HTMLButtonElement) {
    if (evento.key === 'Tab' && (evento.shiftKey ? evento.target === cancelar : evento.target === excluir)) {
      evento.preventDefault();
      (evento.shiftKey ? excluir : cancelar).focus();
    }
  }

  cancelar() {
    if (this.bloqueado()) return;
    this.fecharEditor();
    this.focar(this.retornoEditor, '.regiao-tabela');
  }

  private fecharEditor() {
    ++this.consultaEditor;
    this.consultandoEditor.set(false);
    this.modo.set('lista');
    this.editandoId.set(null);
    this.erros.set({});
    this.conflito.set(false);
  }

  rotuloSeguro(tipo: TipoSeguro) {
    return tipo === 'Cliente' ? 'Seguro do cliente' : 'Seguro de terceiros';
  }

  rotuloCombustivel(tipo: Combustivel): string {
    return tipo === 'Gas' ? 'Gás' : tipo === 'Alcool' ? 'Álcool' : tipo;
  }

  formatarMoeda = formatarMoeda;
  formatarData = formatarData;

  private podeSalvar(modo: Modo) {
    return this.modo() === modo && !this.bloqueado() && !this.consultandoEditor() && !this.conflito();
  }

  private async executarSalvar(operacao: () => Promise<unknown>, mensagem: string) {
    const liberar = this.contextoAtual() && this.saida.iniciarMutacao();
    if (!liberar) return;
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      await operacao();
      if (!this.contextoAtual()) return;
      this.fecharEditor();
      this.aviso.set(mensagem);
      this.focar('aviso-precos');
      await this.carregar();
    } catch (erro: unknown) {
      if (this.contextoAtual()) {
        this.conflito.set(this.modo() !== 'lista' && erro instanceof HttpErrorResponse && erro.status === 412);
        this.falhar(erro);
      }
    } finally {
      this.salvando.set(false);
      liberar();
    }
  }

  dispensarAviso() {
    this.aviso.set('');
    this.focar('aba-' + this.aba());
  }

  descricaoErro(campo: string, id: string) {
    return this.erros()[campo] ? `${id}-erro resumo-precos` : null;
  }

  corrigirCampo(campo: string, evento: Event) {
    if (!this.erros()[campo]) return;
    const valor = (evento.target as HTMLInputElement | HTMLSelectElement).value.trim();
    const valido = campo === 'nome' ? valor.length > 0 && valor.length <= 200
      : campo === 'modalidade' ? valor === 'Fixa' || valor === 'Diaria'
        : /^\d{1,13}(\.\d{1,6})?$/.test(decimalDoFormulario(valor));
    if (!valido) return;
    const erros = { ...this.erros() };
    delete erros[campo];
    this.erros.set(erros);
  }

  private falhar(erro: unknown) {
    this.falharCampos(CadastrosService.errosDe(erro));
  }

  private falharCampos(erros: Record<string, string>) {
    this.erros.set(erros);
    this.focar('resumo-precos');
  }

  private focar(id: string, alternativa?: string) {
    afterNextRender(() => {
      if (!this.contextoAtual()) return;
      const host = this.host.nativeElement;
      (host.querySelector<HTMLElement>(`#${CSS.escape(id)}:not(:disabled)`) ??
        (alternativa ? host.querySelector<HTMLElement>(alternativa) : null))?.focus();
    }, { injector: this.injector });
  }
}
