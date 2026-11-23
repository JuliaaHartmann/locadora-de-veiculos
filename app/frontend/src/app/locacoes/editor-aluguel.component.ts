import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, DestroyRef, ElementRef, Injector, OnInit, inject, input, output, signal } from '@angular/core';
import { disabled, form, FormField, required } from '@angular/forms/signals';
import { CadastrosService, formatarMoeda, RespostaComEtag } from '../cadastros.service';
import {
  AluguelDetalhe, AlugueisService, ClienteOpcao, ComandoAluguel, CondutorOpcao, DemonstrativoEstimado,
  PlanoContratado, rotuloCliente, rotuloPlano, rotuloSeguro, SeguroEstado, TaxaOpcao, TipoSeguro, VeiculoOpcao,
} from './alugueis.service';
import { horaDoContrato, partesDaData, retornoDaTela } from './horario-locacao';
import { DemonstrativoAluguelComponent } from './demonstrativo.component';

const vazio = {
  clienteId: '', condutorId: '', veiculoId: '', planoId: '', retornoPrevisto: '', hodometroInicial: '',
  seguroCliente: false, seguroTerceiros: false,
};
const camposObrigatorios = {
  clienteId: 'Selecione o cliente contratante.', condutorId: 'Selecione o condutor vinculado ao cliente.',
  veiculoId: 'Selecione um veículo disponível.', planoId: 'Selecione um plano do grupo do veículo.',
  retornoPrevisto: 'Informe o retorno previsto.', hodometroInicial: 'Informe o hodômetro inicial.',
} as const;
const tiposSeguro = ['Cliente', 'Terceiros'] as const;

@Component({
  standalone: true,
  selector: 'app-editor-aluguel',
  imports: [FormField, DemonstrativoAluguelComponent],
  templateUrl: './editor-aluguel.component.html',
})
export class EditorAluguelComponent implements OnInit {
  private readonly api = inject(AlugueisService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private ativo = true;
  private requisicaoCondutor = 0;
  private requisicaoPlano = 0;
  private envio: { comando: ComandoAluguel; chave: string } | null = null;
  private previaComando: ComandoAluguel | null = null;
  private retornoOriginal = '';

  readonly original = input<RespostaComEtag<AluguelDetalhe> | null>(null);
  readonly voltar = output<void>();
  readonly gravado = output<RespostaComEtag<AluguelDetalhe>>();
  readonly revisaoCarregada = output<RespostaComEtag<AluguelDetalhe>>();
  readonly atual = signal<RespostaComEtag<AluguelDetalhe> | null>(null);
  readonly modelo = signal({ ...vazio });
  readonly calculando = signal(false);
  readonly gravando = signal(false);
  readonly incerto = signal(false);
  readonly desatualizado = signal(false);
  readonly bloqueado = computed(() => this.calculando() || this.gravando() || this.incerto());
  readonly carregando = signal(true);
  readonly carregandoCondutores = signal(false);
  readonly carregandoPlanos = signal(false);
  readonly falhaCondutores = signal(false);
  readonly falhaPlanos = signal(false);
  readonly erroCarregar = signal('');
  readonly erros = signal<Record<string, string>>({});
  readonly mensagens = computed(() => [...new Set(Object.values(this.erros()))]);
  readonly aviso = signal('');
  readonly previa = signal<DemonstrativoEstimado | null>(null);
  readonly clientes = signal<ClienteOpcao[]>([]);
  readonly condutores = signal<CondutorOpcao[]>([]);
  readonly veiculos = signal<VeiculoOpcao[]>([]);
  readonly planos = signal<PlanoContratado[]>([]);
  readonly taxas = signal<TaxaOpcao[]>([]);
  readonly selecionadas = signal<ReadonlySet<string>>(new Set());
  readonly seguros = signal<SeguroEstado[]>([]);
  readonly grupo = computed(() => this.veiculos().find(x => x.id === this.modelo().veiculoId));
  readonly tiposSeguro = tiposSeguro;
  readonly moeda = formatarMoeda;
  readonly rotuloCliente = rotuloCliente;
  readonly rotuloPlano = rotuloPlano;
  readonly rotuloSeguro = rotuloSeguro;
  readonly horario = horaDoContrato;

  readonly formulario = form(this.modelo, caminho => {
    for (const campo of Object.keys(camposObrigatorios) as (keyof typeof camposObrigatorios)[])
      required(caminho[campo], { message: camposObrigatorios[campo] });
    disabled(caminho, () => this.bloqueado());
    disabled(caminho.condutorId, () => this.carregandoCondutores() || !this.condutores().length);
    disabled(caminho.planoId, () => this.carregandoPlanos() || !this.planos().length);
  });

  constructor() { inject(DestroyRef).onDestroy(() => this.ativo = false); }

  ngOnInit() { void this.carregar(this.original()); }

  private focar(seletor: string) {
    if (this.ativo)
      afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(seletor)?.focus(), { injector: this.injector });
  }

  async carregar(revisao: RespostaComEtag<AluguelDetalhe> | null = this.atual()) {
    this.atual.set(revisao); this.carregando.set(true); this.erroCarregar.set('');
    const contrato = revisao?.dados;
    try {
      const [clientes, veiculos, taxas, ...seguros] = await Promise.all([
        this.api.clientes(), this.api.veiculos(true, contrato?.id), this.api.taxas(),
        ...tiposSeguro.map(tipo => this.api.seguro(tipo)),
      ] as const);
      if (!this.ativo) return;
      this.clientes.set(clientes); this.veiculos.set(veiculos); this.taxas.set(taxas); this.seguros.set(seguros);
      const dataHora = contrato ? partesDaData(contrato.retornoPrevisto) : null;
      this.retornoOriginal = dataHora ? `${dataHora.data} ${dataHora.hora}` : '';
      const adicionais = contrato?.estimativa.adicionais ?? [];
      this.modelo.set(contrato ? {
        clienteId: contrato.cliente.id, condutorId: contrato.condutor.id, veiculoId: contrato.veiculo.id,
        planoId: contrato.estimativa.plano.id, retornoPrevisto: this.retornoOriginal,
        hodometroInicial: String(contrato.hodometroInicial), seguroCliente: adicionais.some(x => x.categoria === 'SeguroCliente'),
        seguroTerceiros: adicionais.some(x => x.categoria === 'SeguroTerceiros'),
      } : { ...vazio });
      this.selecionadas.set(new Set(adicionais.filter(x => x.categoria === 'Taxa').map(x => x.origemId)));
      await Promise.all([this.buscarCondutores(), this.buscarPlanos()]);
    } catch {
      if (this.ativo) this.erroCarregar.set('Não foi possível consultar os dados da contratação.');
    } finally {
      if (this.ativo) this.carregando.set(false);
    }
  }

  private async buscarCondutores() {
    const clienteId = this.modelo().clienteId, requisicao = ++this.requisicaoCondutor;
    this.falhaCondutores.set(false);
    this.condutores.set([]);
    if (!clienteId) { this.carregandoCondutores.set(false); return; }
    this.carregandoCondutores.set(true);
    try {
      const resultado = await this.api.condutores(clienteId);
      if (this.ativo && requisicao === this.requisicaoCondutor) this.condutores.set(resultado);
    } catch {
      if (this.ativo && requisicao === this.requisicaoCondutor) {
        this.falhaCondutores.set(true);
        this.erro({ ...this.erros(), condutorId: 'Não foi possível consultar os condutores vinculados.' });
      }
    } finally {
      if (this.ativo && requisicao === this.requisicaoCondutor) this.carregandoCondutores.set(false);
    }
  }

  tentarCondutores() { this.limparErro('condutorId'); void this.buscarCondutores(); }
  tentarPlanos() { this.limparErro('planoId'); void this.buscarPlanos(); }
  private limparErro(campo: string) {
    const erros = { ...this.erros() }; delete erros[campo]; this.erros.set(erros);
  }

  private async buscarPlanos() {
    const grupo = this.grupo(), requisicao = ++this.requisicaoPlano;
    this.falhaPlanos.set(false);
    this.planos.set([]);
    if (!grupo) { this.carregandoPlanos.set(false); return; }
    this.carregandoPlanos.set(true);
    try {
      const resultado = await this.api.planos(grupo.grupoId);
      if (this.ativo && requisicao === this.requisicaoPlano) {
        this.planos.set(resultado);
        if (!resultado.some(x => x.id === this.modelo().planoId))
          this.modelo.update(v => ({ ...v, planoId: '' }));
      }
    } catch {
      if (this.ativo && requisicao === this.requisicaoPlano) {
        this.falhaPlanos.set(true);
        this.erro({ ...this.erros(), planoId: 'Não foi possível consultar os planos do grupo.' });
      }
    } finally {
      if (this.ativo && requisicao === this.requisicaoPlano) this.carregandoPlanos.set(false);
    }
  }

  alterarCliente(id: string) {
    this.modelo.update(v => ({ ...v, clienteId: id, condutorId: '' })); this.alterar(); void this.buscarCondutores();
  }

  alterarVeiculo(id: string) {
    this.modelo.update(v => ({ ...v, veiculoId: id })); this.alterar(); void this.buscarPlanos();
  }

  alterar() {
    this.previaComando = null; this.previa.set(null); this.aviso.set('');
    if (!this.desatualizado()) this.erros.set({});
  }

  alternarTaxa(id: string, evento: Event) {
    const marcado = (evento.target as HTMLInputElement).checked;
    const nova = new Set(this.selecionadas());
    if (marcado) nova.add(id); else nova.delete(id);
    this.selecionadas.set(nova); this.alterar();
  }

  precoTaxa(taxa: TaxaOpcao) {
    return this.atual()?.dados.estimativa.adicionais.find(x => x.categoria === 'Taxa' && x.origemId === taxa.id)?.preco ?? taxa.preco;
  }

  precoSeguro(tipo: TipoSeguro) {
    const cat = tipo === 'Cliente' ? 'SeguroCliente' : 'SeguroTerceiros';
    return this.atual()?.dados.estimativa.adicionais.find(x => x.categoria === cat)?.preco ??
      this.seguros()[tiposSeguro.indexOf(tipo)]?.precoDiario;
  }

  private erro(erros: Record<string, string>) { this.erros.set(erros); this.focar('#erros-contratacao'); }

  private comando(): ComandoAluguel | null {
    const modelo = this.modelo();
    const erros: Record<string, string> = {};
    for (const campo of Object.keys(camposObrigatorios) as (keyof typeof camposObrigatorios)[]) {
      if (!modelo[campo].trim()) erros[campo] = camposObrigatorios[campo];
    }
    if (!this.condutores().some(x => x.id === modelo.condutorId)) erros['condutorId'] = camposObrigatorios.condutorId;
    if (!this.planos().some(x => x.id === modelo.planoId)) erros['planoId'] = camposObrigatorios.planoId;
    const retorno = this.atual() && modelo.retornoPrevisto === this.retornoOriginal
      ? { iso: this.atual()!.dados.retornoPrevisto } : retornoDaTela(modelo.retornoPrevisto);
    if ('erro' in retorno) erros['retornoPrevisto'] = retorno.erro;
    const hodometro = Number(modelo.hodometroInicial);
    if (!/^[0-9]+$/.test(modelo.hodometroInicial.trim()) || !Number.isSafeInteger(hodometro) || hodometro > 2147483647)
      erros['hodometroInicial'] = 'Informe o hodômetro inicial em quilômetros inteiros e não negativos.';
    if (Object.keys(erros).length || 'erro' in retorno) { this.erro(erros); return null; }
    const seguros = tiposSeguro.filter(t => t === 'Cliente' ? modelo.seguroCliente : modelo.seguroTerceiros);
    return { clienteId: modelo.clienteId, condutorId: modelo.condutorId, veiculoId: modelo.veiculoId, planoId: modelo.planoId,
      retornoPrevisto: retorno.iso, hodometroInicial: hodometro, taxaIds: [...this.selecionadas()], seguros };
  }

  submeter(evento: Event) {
    evento.preventDefault();
    const confirmar = (evento as SubmitEvent).submitter?.id === 'confirmar-aluguel';
    if (this.bloqueado() || this.desatualizado()) return;
    if (confirmar) { void this.confirmar(); return; }
    const comando = this.comando();
    if (comando) void (this.atual() ? this.editar(comando) : this.estimar(comando));
  }

  private async estimar(comando: ComandoAluguel) {
    this.calculando.set(true); this.erros.set({});
    try {
      const previa = await this.api.estimar(comando);
      if (!this.ativo) return;
      this.previaComando = comando; this.previa.set(previa);
      this.aviso.set('Estimativa calculada. Nenhum veículo foi ocupado.');
      this.focar('#titulo-demonstrativo');
    } catch (erro) {
      if (this.ativo) await this.tratarFalha(erro);
    } finally { if (this.ativo) this.calculando.set(false); }
  }

  async confirmar() {
    if (this.gravando() || this.calculando()) return;
    if (!this.envio && this.previaComando) {
      this.envio = { comando: this.previaComando, chave: crypto.randomUUID() };
    }
    const envio = this.envio;
    if (!envio) return;
    this.gravando.set(true); this.erros.set({});
    try {
      const resposta = await this.api.criar(envio.comando, envio.chave);
      if (this.ativo) { this.envio = null; this.incerto.set(false); this.gravado.emit(resposta); }
    } catch (erro) {
      if (!this.ativo) return;
      if (!(erro instanceof HttpErrorResponse) || erro.status === 0 || erro.status >= 500) {
        this.incerto.set(true); this.aviso.set('');
        this.erro({ geral: 'Não recebemos a confirmação da gravação. Repita o mesmo envio sem criar outro aluguel.' });
      } else {
        this.envio = null; this.incerto.set(false); await this.tratarFalha(erro);
      }
    } finally { if (this.ativo) this.gravando.set(false); }
  }

  private async editar(comando: ComandoAluguel) {
    const atual = this.atual();
    if (!atual) return;
    this.gravando.set(true); this.erros.set({});
    try {
      const resposta = await this.api.editar(atual.dados.id, comando, atual.etag);
      if (this.ativo) this.gravado.emit(resposta);
    } catch (erro) {
      if (!this.ativo) return;
      if (erro instanceof HttpErrorResponse && [412, 428].includes(erro.status)) {
        this.desatualizado.set(true);
        this.erro({ geral: 'O contrato foi atualizado. Recarregue a versão atual antes de revisar os dados novamente.' });
      } else await this.tratarFalha(erro);
    } finally { if (this.ativo) this.gravando.set(false); }
  }

  async recarregar() {
    const atual = this.atual();
    if (!atual || this.gravando()) return;
    this.gravando.set(true);
    try {
      const revisao = await this.api.obter(atual.dados.id);
      if (!this.ativo) return;
      this.desatualizado.set(false); this.erros.set({});
      this.revisaoCarregada.emit(revisao);
      await this.carregar(revisao);
      this.focar('#aluguel-cliente');
    } catch {
      if (this.ativo) this.erro({ geral: 'Não foi possível recarregar o contrato. Tente novamente.' });
    } finally { if (this.ativo) this.gravando.set(false); }
  }

  private async tratarFalha(erro: unknown) {
    const falhas = CadastrosService.errosDe(erro);
    this.aviso.set(''); this.previa.set(null); this.previaComando = null;
    if (erro instanceof HttpErrorResponse && erro.status === 409 && falhas['veiculoId']) {
      this.modelo.update(v => ({ ...v, veiculoId: '' })); this.planos.set([]);
      try { const veiculos = await this.api.veiculos(true, this.atual()?.dados.id); if (this.ativo) this.veiculos.set(veiculos); }
      catch { falhas['geral'] = 'Não foi possível atualizar os veículos disponíveis.'; }
    }
    if (this.ativo) this.erro(falhas);
  }
}
