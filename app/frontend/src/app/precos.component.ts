import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormField, form, required } from '@angular/forms/signals';
import { CadastrosService, decimalDoFormulario, formatarData, formatarMoeda, validarLimites } from './cadastros.service';

type Aba = 'taxas' | 'seguros' | 'combustiveis';
type ModalidadeTaxa = 'Fixa' | 'Diaria';
type TipoSeguro = 'Cliente' | 'Terceiros';
type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';
type Modo = 'lista' | 'taxa' | 'seguro' | 'combustivel';

interface Taxa { id: string; nome: string; preco: string; modalidade: ModalidadeTaxa; }
interface SeguroEstado { tipo: TipoSeguro; configurado: boolean; precoDiario: string | null; }
interface CombustivelEstado {
  tipo: Combustivel; configurado: boolean; id: string | null; preco: string | null;
  inicioVigenciaUtc: string | null; ordem: number | null;
}
interface Versao { id: string; tipo: Combustivel; preco: string; inicioVigenciaUtc: string; ordem: number; }
interface TaxaModelo { nome: string; preco: string; modalidade: ModalidadeTaxa; }
interface SeguroModelo { precoDiario: string; }
interface CombustivelModelo { preco: string; }

@Component({
  standalone: true,
  imports: [FormField],
  template: `
    <main class="max-w-6xl">
      <p class="text-sm text-slate-600">Cadastros / Preços operacionais</p>
      <h1>Preços operacionais</h1>
      <p class="text-slate-600">Configurações independentes e valores versionados da empresa.</p>

      <nav aria-label="Preços operacionais" class="my-4 flex flex-wrap gap-2">
        @for (item of abas; track item.valor) {
          <button type="button" (click)="trocarAba(item.valor)"
            [attr.aria-current]="aba() === item.valor ? 'page' : null"
            [class]="aba() === item.valor ? 'bg-blue-700 text-white border-blue-700' : 'bg-white text-slate-900 border border-slate-400'">
            {{ item.rotulo }}
          </button>
        }
      </nav>

      <p role="status" class="font-medium text-slate-700">{{ aviso() }}</p>
      <div #resumo tabindex="-1" role="alert" class="my-3 rounded-lg border border-red-300 bg-red-50 p-4"
        [hidden]="!temErros()">
        <strong>Revise os dados informados.</strong>
        <ul class="my-1 list-disc pl-5">
          @for (mensagem of mensagensErro(); track mensagem) { <li>{{ mensagem }}</li> }
        </ul>
      </div>

      @if (modo() === 'lista') {
        @if (aba() === 'seguros') {
          <section class="overflow-hidden rounded-xl border border-slate-300 bg-white shadow-sm">
            <div class="border-b border-slate-200 p-4"><h2 class="text-xl font-semibold">Preços diários independentes</h2></div>
            @if (carregando()) {
              <p class="p-6" role="status">Carregando configurações…</p>
            } @else if (erroConsulta()) {
              <div class="p-6" role="alert">
                <p>{{ erroConsulta() }}</p>
                <button type="button" (click)="carregar()">Tentar novamente</button>
              </div>
            } @else {
              @for (tipo of tiposSeguro; track tipo) {
                <article class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4">
                  <div>
                    <strong>Seguro {{ tipo === 'Cliente' ? 'do cliente' : 'de terceiros' }}</strong>
                    <p>{{ estadoSeguro(tipo)?.configurado
                      ? formatarMoeda(estadoSeguro(tipo)?.precoDiario) + ' por dia'
                      : 'Preço não configurado' }}</p>
                  </div>
                  <button type="button" (click)="configurarSeguro(tipo)">Configurar preço</button>
                </article>
              }
            }
          </section>
        } @else {
          <section class="overflow-hidden rounded-xl border border-slate-300 bg-white shadow-sm">
            <div class="flex flex-wrap items-center justify-between gap-3 p-4">
              <h2 class="text-xl font-semibold">{{ aba() === 'taxas' ? 'Taxas cadastradas' : 'Vigências de combustível — somente leitura' }}</h2>
              <button type="button" [disabled]="carregando() || !!erroConsulta()" (click)="aba() === 'taxas' ? novaTaxa() : adicionarPreco(historicoTipo())">
                {{ aba() === 'taxas' ? 'Cadastrar taxa' : 'Adicionar preço' }}
              </button>
            </div>

            @if (carregando()) {
              <p class="p-6" role="status">Carregando configurações…</p>
            } @else if (erroConsulta()) {
              <div class="p-6" role="alert">
                <p>{{ erroConsulta() }}</p>
                <button type="button" (click)="carregar()">Tentar novamente</button>
              </div>
            } @else {
              @if (aba() === 'combustiveis') {
                <div class="grid gap-3 border-t border-slate-200 p-4 sm:grid-cols-2 lg:grid-cols-4">
                  @for (tipo of tiposCombustivel; track tipo) {
                    <div class="rounded-lg border border-slate-200 p-3">
                      <strong>{{ rotuloCombustivel(tipo) }}</strong>
                      <p>{{ estadoCombustivel(tipo)?.configurado
                        ? formatarMoeda(estadoCombustivel(tipo)?.preco)
                        : 'Preço não configurado' }}</p>
                      <button type="button" (click)="adicionarPreco(tipo)">Adicionar preço</button>
                      <button type="button" (click)="verHistorico(tipo)">Histórico</button>
                    </div>
                  }
                </div>
                <div class="flex flex-wrap items-center gap-3 border-t border-slate-200 p-4">
                  <label for="historico-tipo">Histórico do combustível</label>
                  <select id="historico-tipo" [value]="historicoTipo()" (change)="mudarHistorico($event)">
                    @for (tipo of tiposCombustivel; track tipo) {
                      <option [value]="tipo">{{ rotuloCombustivel(tipo) }}</option>
                    }
                  </select>
                </div>
                <div class="overflow-auto">
                  <table class="w-full min-w-[700px] border-collapse text-left">
                    <thead><tr>
                      <th scope="col">ID</th><th scope="col">Combustível</th><th scope="col">Preço</th>
                      <th scope="col">Início da vigência</th><th scope="col">Ordem</th><th scope="col">Situação</th>
                    </tr></thead>
                    <tbody>
                      @for (versao of historico(); track versao.id; let primeiro = $first) {
                        <tr>
                          <td>{{ versao.id }}</td>
                          <td>{{ rotuloCombustivel(versao.tipo) }}</td>
                          <td>{{ formatarMoeda(versao.preco) }}</td>
                          <td>{{ formatarData(versao.inicioVigenciaUtc) }}</td>
                          <td>{{ versao.ordem }}</td>
                          <td>{{ versao.id === estadoCombustivel(versao.tipo)?.id ? 'Vigente' : 'Versão anterior' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (historicoTotal() === 0) { <p class="p-6 text-center">Nenhuma vigência registrada.</p> }
                <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4">
                  <span>Página {{ historicoPagina() }} · {{ historicoTotal() }} registros · até 20 por página</span>
                  <div class="flex gap-2">
                    <button type="button" (click)="paginarHistorico(-1)" [disabled]="historicoPagina() <= 1">Anterior</button>
                    <button type="button" (click)="paginarHistorico(1)" [disabled]="historicoPagina() * 20 >= historicoTotal()">Próxima</button>
                  </div>
                </div>
              } @else {
                <div class="overflow-auto">
                  <table class="w-full min-w-[700px] border-collapse text-left">
                    <thead><tr>
                      <th scope="col">ID</th><th scope="col">Nome</th><th scope="col">Preço</th>
                      <th scope="col">Modalidade</th><th scope="col">Ações</th>
                    </tr></thead>
                    <tbody>
                      @for (taxa of taxas(); track taxa.id) {
                        <tr>
                          <td>{{ taxa.id }}</td>
                          <td>{{ taxa.nome }}</td>
                          <td>{{ formatarMoeda(taxa.preco) }}</td>
                          <td>{{ taxa.modalidade === 'Diaria' ? 'Diária' : 'Fixa' }}</td>
                          <td class="whitespace-nowrap">
                            <button type="button" (click)="editarTaxa(taxa.id)">Editar</button>
                            <button type="button" (click)="excluirTaxa(taxa)">Excluir</button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (total() === 0) { <p class="p-6 text-center">Nenhuma taxa cadastrada.</p> }
                <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4">
                  <span>Página {{ pagina() }} · {{ total() }} registros · até 20 por página</span>
                  <div class="flex gap-2">
                    <button type="button" (click)="paginar(-1)" [disabled]="pagina() <= 1">Anterior</button>
                    <button type="button" (click)="paginar(1)" [disabled]="pagina() * 20 >= total()">Próxima</button>
                  </div>
                </div>
              }
            }
          </section>
        }
      }

      @if (modo() === 'taxa') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">{{ editandoId() ? 'Editar taxa' : 'Cadastrar taxa' }}</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarTaxa()" novalidate>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1">
                <label for="taxa-nome">Nome</label>
                <input id="taxa-nome" [formField]="taxaForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" aria-describedby="taxa-nome-erro">
                <small id="taxa-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="taxa-preco">Preço (R$)</label>
                <input id="taxa-preco" inputmode="decimal" [formField]="taxaForm.preco"
                  [attr.aria-invalid]="erros()['preco'] ? 'true' : null" aria-describedby="taxa-preco-erro">
                <small id="taxa-preco-erro" class="text-red-800">{{ erros()['preco'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="taxa-modalidade">Modalidade</label>
                <select id="taxa-modalidade" [formField]="taxaForm.modalidade"
                  [attr.aria-invalid]="erros()['modalidade'] ? 'true' : null" aria-describedby="taxa-modalidade-erro">
                  <option value="Fixa">Fixa</option>
                  <option value="Diaria">Diária</option>
                </select>
                <small id="taxa-modalidade-erro" class="text-red-800">{{ erros()['modalidade'] }}</small>
              </div>
            </div>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'seguro') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">Seguro {{ seguroTipo() === 'Cliente' ? 'do cliente' : 'de terceiros' }}</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarSeguro()" novalidate>
            <div class="grid gap-1">
              <label for="seguro-preco">Preço diário (R$)</label>
              <input id="seguro-preco" inputmode="decimal" [formField]="seguroForm.precoDiario"
                [attr.aria-invalid]="erros()['precoDiario'] ? 'true' : null" aria-describedby="seguro-preco-erro">
              <small id="seguro-preco-erro" class="text-red-800">{{ erros()['precoDiario'] }}</small>
            </div>
            <p class="text-slate-600">Preço zero é uma configuração existente. Deixe em branco apenas para não salvar.</p>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'combustivel') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">Adicionar preço — {{ rotuloCombustivel(combustivelTipo()) }}</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarCombustivel()" novalidate>
            <div class="grid gap-1">
              <label for="combustivel-preco">Preço por litro (R$)</label>
              <input id="combustivel-preco" inputmode="decimal" [formField]="combustivelForm.preco"
                [attr.aria-invalid]="erros()['preco'] ? 'true' : null" aria-describedby="combustivel-preco-erro">
              <small id="combustivel-preco-erro" class="text-red-800">{{ erros()['preco'] }}</small>
            </div>
            <p class="text-slate-600">A vigência é atribuída pelo servidor. Preços anteriores não são editados.</p>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar</button>
            </div>
          </form>
        </section>
      }
    </main>
  `,
})
export class PrecosComponent {
  private consultaAtual = 0;
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');

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
  readonly erroConsulta = signal('');
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

  readonly taxaModelo = signal<TaxaModelo>({ nome: '', preco: '', modalidade: 'Fixa' });
  readonly taxaForm = form(this.taxaModelo, caminho => {
    required(caminho.nome, { message: 'Informe o nome.' });
    required(caminho.preco, { message: 'Informe o preço.' });
  });
  readonly seguroModelo = signal<SeguroModelo>({ precoDiario: '' });
  readonly seguroForm = form(this.seguroModelo, caminho => {
    required(caminho.precoDiario, { message: 'Informe o preço diário.' });
  });
  readonly combustivelModelo = signal<CombustivelModelo>({ preco: '' });
  readonly combustivelForm = form(this.combustivelModelo, caminho => {
    required(caminho.preco, { message: 'Informe o preço.' });
  });

  constructor() {
    void this.carregar();
  }

  trocarAba(aba: Aba) {
    this.aba.set(aba);
    this.cancelar();
    this.pagina.set(1);
    this.aviso.set('');
    void this.carregar();
  }

  async carregar() {
    const consulta = ++this.consultaAtual;
    this.carregando.set(true);
    this.erroConsulta.set('');
    try {
      if (this.aba() === 'taxas') {
        const pagina = await this.servico.listar<Taxa>('/taxas-servicos', this.pagina());
        if (consulta !== this.consultaAtual) return;
        this.taxas.set(pagina.items);
        this.total.set(pagina.total);
      } else if (this.aba() === 'seguros') {
        const resultados = await Promise.all(this.tiposSeguro.map(async tipo => {
          const resposta = await this.servico.obter<SeguroEstado>(`/configuracoes/seguros/${tipo}`);
          return [tipo, { estado: resposta.dados, etag: resposta.etag }] as const;
        }));
        if (consulta !== this.consultaAtual) return;
        this.seguros.set(Object.fromEntries(resultados));
      } else {
        const resultados = await Promise.all(this.tiposCombustivel.map(async tipo => {
          const resposta = await this.servico.obter<CombustivelEstado>(`/configuracoes/combustiveis/${tipo}`);
          return [tipo, { estado: resposta.dados, etag: resposta.etag }] as const;
        }));
        if (consulta !== this.consultaAtual) return;
        this.combustiveis.set(Object.fromEntries(resultados));
        await this.carregarHistorico();
      }
    } catch {
      if (consulta !== this.consultaAtual) return;
      this.erroConsulta.set('Não foi possível consultar as configurações. Verifique a conexão e tente novamente.');
    } finally {
      if (consulta === this.consultaAtual) this.carregando.set(false);
    }
  }

  private async carregarHistorico() {
    const tipo = this.historicoTipo();
    const numeroPagina = this.historicoPagina();
    const pagina = await this.servico.listar<Versao>(
      `/configuracoes/combustiveis/${tipo}/historico`, numeroPagina);
    if (tipo !== this.historicoTipo() || numeroPagina !== this.historicoPagina()) return;
    this.historico.set(pagina.items);
    this.historicoTotal.set(pagina.total);
  }

  paginar(delta: number) {
    this.pagina.update(x => Math.max(1, x + delta));
    void this.carregar();
  }

  paginarHistorico(delta: number) {
    this.historicoPagina.update(x => Math.max(1, x + delta));
    void this.carregar();
  }

  mudarHistorico(evento: Event) {
    this.historicoTipo.set((evento.target as HTMLSelectElement).value as Combustivel);
    this.historicoPagina.set(1);
    void this.carregar();
  }

  verHistorico(tipo: Combustivel) {
    this.historicoTipo.set(tipo);
    this.historicoPagina.set(1);
    void this.carregar();
  }

  estadoSeguro(tipo: TipoSeguro): SeguroEstado | undefined {
    return this.seguros()[tipo]?.estado;
  }

  estadoCombustivel(tipo: Combustivel): CombustivelEstado | undefined {
    return this.combustiveis()[tipo]?.estado;
  }

  novaTaxa() {
    this.modo.set('taxa');
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.taxaModelo.set({ nome: '', preco: '', modalidade: 'Fixa' });
  }

  async editarTaxa(id: string) {
    try {
      const resposta = await this.servico.obter<Taxa>(`/taxas-servicos/${id}`);
      this.taxaModelo.set({
        nome: resposta.dados.nome, preco: resposta.dados.preco, modalidade: resposta.dados.modalidade,
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.modo.set('taxa');
    } catch (erro: unknown) { this.falhar(erro); }
  }

  async configurarSeguro(tipo: TipoSeguro) {
    try {
      const resposta = await this.servico.obter<SeguroEstado>(`/configuracoes/seguros/${tipo}`);
      this.seguros.update(x => ({ ...x, [tipo]: { estado: resposta.dados, etag: resposta.etag } }));
    } catch (erro: unknown) { this.falhar(erro); return; }
    this.modo.set('seguro');
    this.seguroTipo.set(tipo);
    this.etag.set(this.seguros()[tipo]?.etag ?? '');
    this.erros.set({});
    this.seguroModelo.set({ precoDiario: this.estadoSeguro(tipo)?.precoDiario ?? '' });
  }

  async adicionarPreco(tipo: Combustivel) {
    try {
      const resposta = await this.servico.obter<CombustivelEstado>(`/configuracoes/combustiveis/${tipo}`);
      this.combustiveis.update(x => ({ ...x, [tipo]: { estado: resposta.dados, etag: resposta.etag } }));
    } catch (erro: unknown) { this.falhar(erro); return; }
    this.modo.set('combustivel');
    this.combustivelTipo.set(tipo);
    this.etag.set(this.combustiveis()[tipo]?.etag ?? '');
    this.erros.set({});
    this.combustivelModelo.set({ preco: this.estadoCombustivel(tipo)?.preco ?? '' });
  }

  async salvarTaxa() {
    if (this.salvando()) return;
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
    });
  }

  async salvarSeguro() {
    if (this.salvando()) return;
    const preco = decimalDoFormulario(this.seguroModelo().precoDiario);
    if (!preco) { this.falharCampos({ precoDiario: 'Informe o preço diário.' }); return; }
    if (!/^\d{1,13}(\.\d{1,6})?$/.test(preco)) {
      this.falharCampos({ precoDiario: 'Informe um valor não negativo com até 6 casas.' });
      return;
    }
    await this.executarSalvar(async () => {
      await this.servico.editar(`/configuracoes/seguros/${this.seguroTipo()}`,
        { precoDiario: preco }, this.etag());
    });
  }

  async salvarCombustivel() {
    if (this.salvando()) return;
    const preco = decimalDoFormulario(this.combustivelModelo().preco);
    if (!preco) { this.falharCampos({ preco: 'Informe o preço.' }); return; }
    if (!/^\d{1,13}(\.\d{1,6})?$/.test(preco)) {
      this.falharCampos({ preco: 'Informe um valor não negativo com até 6 casas.' });
      return;
    }
    await this.executarSalvar(async () => {
      await this.servico.editar(`/configuracoes/combustiveis/${this.combustivelTipo()}`,
        { preco }, this.etag());
    });
  }

  async excluirTaxa(taxa: Taxa) {
    try {
      const resposta = await this.servico.obter<unknown>(`/taxas-servicos/${taxa.id}`);
      await this.servico.excluir(`/taxas-servicos/${taxa.id}`, resposta.etag);
      this.aviso.set('Taxa excluída logicamente.');
      this.erros.set({});
      await this.carregar();
    } catch (erro: unknown) { this.falhar(erro); }
  }

  cancelar() {
    this.modo.set('lista');
    this.editandoId.set(null);
    this.erros.set({});
  }

  rotuloCombustivel(tipo: Combustivel): string {
    return tipo === 'Gas' ? 'Gás' : tipo === 'Alcool' ? 'Álcool' : tipo;
  }

  formatarMoeda = formatarMoeda;
  formatarData = formatarData;

  private async executarSalvar(operacao: () => Promise<unknown>) {
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      await operacao();
      this.aviso.set('Configuração salva.');
      this.modo.set('lista');
      this.editandoId.set(null);
      await this.carregar();
    } catch (erro: unknown) {
      this.falhar(erro);
    } finally {
      this.salvando.set(false);
    }
  }

  private falhar(erro: unknown) {
    this.falharCampos(CadastrosService.errosDe(erro));
  }

  private falharCampos(erros: Record<string, string>) {
    this.erros.set(erros);
    afterNextRender(() => this.resumo()?.nativeElement.focus(), { injector: this.injector });
  }
}
