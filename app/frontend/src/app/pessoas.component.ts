import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormField, form, required } from '@angular/forms/signals';
import { CadastrosService, validarLimites } from './cadastros.service';

type Aba = 'clientes' | 'condutores';
type TipoCliente = 'PF' | 'PJ';
type Modo = 'lista' | 'cliente' | 'condutor' | 'vinculos';

interface Cliente {
  id: string;
  tipo: TipoCliente;
  nome: string;
  telefone: string;
  email: string | null;
  cpf: string | null;
  rg: string | null;
  cnh: string | null;
  cnpj: string | null;
}

interface ClienteVinculado { id: string; nome: string; cnpj: string | null; }

interface Condutor {
  id: string;
  clienteId: string;
  clienteNome: string;
  nome: string;
  email: string;
  cpf: string;
  cnh: string;
  validadeCnh: string;
  telefone: string;
}

interface ClienteModelo {
  tipo: TipoCliente; nome: string; cpf: string; rg: string; cnh: string; cnpj: string;
  telefone: string; email: string; estado: string; cidade: string; bairro: string; rua: string; numero: string;
}

interface CondutorModelo {
  clienteId: string; nome: string; email: string; cpf: string; cnh: string; validadeCnh: string; telefone: string;
}

@Component({
  standalone: true,
  imports: [FormField],
  template: `
    <main class="max-w-6xl">
      <p class="text-sm text-slate-600">Cadastros / Pessoas</p>
      <h1>Pessoas</h1>
      <p class="text-slate-600">Clientes PF/PJ, vínculos e condutores da empresa.</p>

      <nav aria-label="Pessoas" class="my-4 flex flex-wrap gap-2">
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
        <section class="overflow-hidden rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="flex flex-wrap items-center justify-between gap-3 p-4">
            <h2 class="text-xl font-semibold">{{ aba() === 'clientes' ? 'Clientes' : 'Condutores' }} cadastrados</h2>
            <button type="button" (click)="aba() === 'clientes' ? novoCliente() : novoCondutor()">Cadastrar</button>
          </div>

          @if (carregando()) {
            <p class="p-6" role="status">Carregando cadastros…</p>
          } @else if (erroConsulta()) {
            <div class="p-6" role="alert">
              <p>{{ erroConsulta() }}</p>
              <button type="button" (click)="carregar()">Tentar novamente</button>
            </div>
          } @else {
            @if (aba() === 'clientes') {
              <div class="overflow-auto">
                <table class="w-full min-w-[850px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">ID</th><th scope="col">Nome / tipo</th><th scope="col">Telefone</th>
                    <th scope="col">Documentos</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (cliente of clientes(); track cliente.id) {
                      <tr>
                        <td>{{ cliente.id }}</td>
                        <td>{{ cliente.nome }} / {{ cliente.tipo }}</td>
                        <td>{{ cliente.telefone }}</td>
                        <td>{{ documentos(cliente) }}</td>
                        <td class="whitespace-nowrap">
                          <button type="button" (click)="editarCliente(cliente.id)">Editar</button>
                          <button type="button" (click)="excluirCliente(cliente)">Excluir</button>
                          @if (cliente.tipo === 'PF') {
                            <button type="button" (click)="abrirVinculos(cliente)">Vínculos PJ</button>
                            <button type="button" (click)="usarComoCondutor(cliente)">Usar como condutor</button>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <div class="overflow-auto">
                <table class="w-full min-w-[850px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">ID</th><th scope="col">Condutor</th><th scope="col">Cliente</th>
                    <th scope="col">CPF</th><th scope="col">CNH</th><th scope="col">Validade</th>
                    <th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (condutor of condutores(); track condutor.id) {
                      <tr>
                        <td>{{ condutor.id }}</td>
                        <td>{{ condutor.nome }}</td>
                        <td>{{ condutor.clienteNome }}</td>
                        <td>{{ condutor.cpf }}</td>
                        <td>{{ condutor.cnh }}</td>
                        <td>{{ condutor.validadeCnh }}</td>
                        <td class="whitespace-nowrap">
                          <button type="button" (click)="editarCondutor(condutor.id)">Editar</button>
                          <button type="button" (click)="excluirCondutor(condutor)">Excluir</button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (total() === 0) { <p class="p-6 text-center">Nenhum registro encontrado.</p> }

            <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4">
              <span>Página {{ pagina() }} · {{ total() }} registros · até 20 por página</span>
              <div class="flex gap-2">
                <button type="button" (click)="paginar(-1)" [disabled]="pagina() <= 1">Anterior</button>
                <button type="button" (click)="paginar(1)" [disabled]="pagina() * 20 >= total()">Próxima</button>
              </div>
            </div>
          }
        </section>
      }

      @if (modo() === 'cliente') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">{{ editandoId() ? 'Editar' : 'Cadastrar' }} — cliente</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarCliente()" novalidate>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1">
                <label for="cliente-tipo">Tipo</label>
                <select id="cliente-tipo" [formField]="clienteForm.tipo">
                  <option value="PF">PF</option>
                  <option value="PJ">PJ</option>
                </select>
              </div>
              <div class="grid gap-1">
                <label for="cliente-nome">Nome</label>
                <input id="cliente-nome" [formField]="clienteForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" aria-describedby="cliente-nome-erro">
                <small id="cliente-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              @if (clienteModelo().tipo === 'PF') {
                <div class="grid gap-1">
                  <label for="cliente-cpf">CPF</label>
                  <input id="cliente-cpf" [formField]="clienteForm.cpf"
                    [attr.aria-invalid]="erros()['cpf'] ? 'true' : null" aria-describedby="cliente-cpf-erro">
                  <small id="cliente-cpf-erro" class="text-red-800">{{ erros()['cpf'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="cliente-rg">RG</label>
                  <input id="cliente-rg" [formField]="clienteForm.rg"
                    [attr.aria-invalid]="erros()['rg'] ? 'true' : null" aria-describedby="cliente-rg-erro">
                  <small id="cliente-rg-erro" class="text-red-800">{{ erros()['rg'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="cliente-cnh">CNH</label>
                  <input id="cliente-cnh" [formField]="clienteForm.cnh"
                    [attr.aria-invalid]="erros()['cnh'] ? 'true' : null" aria-describedby="cliente-cnh-erro">
                  <small id="cliente-cnh-erro" class="text-red-800">{{ erros()['cnh'] }}</small>
                </div>
              } @else {
                <div class="grid gap-1">
                  <label for="cliente-cnpj">CNPJ</label>
                  <input id="cliente-cnpj" [formField]="clienteForm.cnpj"
                    [attr.aria-invalid]="erros()['cnpj'] ? 'true' : null" aria-describedby="cliente-cnpj-erro">
                  <small id="cliente-cnpj-erro" class="text-red-800">{{ erros()['cnpj'] }}</small>
                </div>
              }
              <div class="grid gap-1">
                <label for="cliente-telefone">Telefone</label>
                <input id="cliente-telefone" [formField]="clienteForm.telefone"
                  [attr.aria-invalid]="erros()['telefone'] ? 'true' : null" aria-describedby="cliente-telefone-erro">
                <small id="cliente-telefone-erro" class="text-red-800">{{ erros()['telefone'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-email">E-mail (opcional)</label>
                <input id="cliente-email" type="email" [formField]="clienteForm.email"
                  [attr.aria-invalid]="erros()['email'] ? 'true' : null" aria-describedby="cliente-email-erro">
                <small id="cliente-email-erro" class="text-red-800">{{ erros()['email'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-estado">Estado</label>
                <input id="cliente-estado" [formField]="clienteForm.estado"
                  [attr.aria-invalid]="erros()['endereco.estado'] ? 'true' : null" aria-describedby="cliente-estado-erro">
                <small id="cliente-estado-erro" class="text-red-800">{{ erros()['endereco.estado'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-cidade">Cidade</label>
                <input id="cliente-cidade" [formField]="clienteForm.cidade"
                  [attr.aria-invalid]="erros()['endereco.cidade'] ? 'true' : null" aria-describedby="cliente-cidade-erro">
                <small id="cliente-cidade-erro" class="text-red-800">{{ erros()['endereco.cidade'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-bairro">Bairro</label>
                <input id="cliente-bairro" [formField]="clienteForm.bairro"
                  [attr.aria-invalid]="erros()['endereco.bairro'] ? 'true' : null" aria-describedby="cliente-bairro-erro">
                <small id="cliente-bairro-erro" class="text-red-800">{{ erros()['endereco.bairro'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-rua">Rua</label>
                <input id="cliente-rua" [formField]="clienteForm.rua"
                  [attr.aria-invalid]="erros()['endereco.rua'] ? 'true' : null" aria-describedby="cliente-rua-erro">
                <small id="cliente-rua-erro" class="text-red-800">{{ erros()['endereco.rua'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-numero">Número</label>
                <input id="cliente-numero" [formField]="clienteForm.numero"
                  [attr.aria-invalid]="erros()['endereco.numero'] ? 'true' : null" aria-describedby="cliente-numero-erro">
                <small id="cliente-numero-erro" class="text-red-800">{{ erros()['endereco.numero'] }}</small>
              </div>
            </div>
            <p class="text-slate-600">Nenhuma consulta oficial de documentos é realizada.</p>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar cadastro</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'condutor') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">{{ editandoId() ? 'Editar' : 'Cadastrar' }} — condutor</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarCondutor()" novalidate>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="grid gap-1">
                <label for="condutor-cliente">Cliente vinculado</label>
                <select id="condutor-cliente" [formField]="condutorForm.clienteId"
                  [attr.aria-invalid]="erros()['clienteId'] ? 'true' : null" aria-describedby="condutor-cliente-erro">
                  <option value="">Selecione</option>
                  @if (clienteOriginal() && !clienteOriginalAtivo()) {
                    <option [value]="clienteOriginal()!.id">{{ clienteOriginal()!.nome }} (excluído)</option>
                  }
                  @for (cliente of clientesOpcoes(); track cliente.id) {
                    <option [value]="cliente.id">{{ cliente.nome }} ({{ cliente.tipo }})</option>
                  }
                </select>
                <small id="condutor-cliente-erro" class="text-red-800">{{ erros()['clienteId'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-nome">Nome</label>
                <input id="condutor-nome" [formField]="condutorForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" aria-describedby="condutor-nome-erro">
                <small id="condutor-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-email">E-mail</label>
                <input id="condutor-email" type="email" [formField]="condutorForm.email"
                  [attr.aria-invalid]="erros()['email'] ? 'true' : null" aria-describedby="condutor-email-erro">
                <small id="condutor-email-erro" class="text-red-800">{{ erros()['email'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-cpf">CPF</label>
                <input id="condutor-cpf" [formField]="condutorForm.cpf"
                  [attr.aria-invalid]="erros()['cpf'] ? 'true' : null" aria-describedby="condutor-cpf-erro">
                <small id="condutor-cpf-erro" class="text-red-800">{{ erros()['cpf'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-cnh">CNH</label>
                <input id="condutor-cnh" [formField]="condutorForm.cnh"
                  [attr.aria-invalid]="erros()['cnh'] ? 'true' : null" aria-describedby="condutor-cnh-erro">
                <small id="condutor-cnh-erro" class="text-red-800">{{ erros()['cnh'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-validade">Validade da CNH</label>
                <input id="condutor-validade" type="date" [formField]="condutorForm.validadeCnh"
                  [attr.aria-invalid]="erros()['validadeCnh'] ? 'true' : null" aria-describedby="condutor-validade-erro">
                <small id="condutor-validade-erro" class="text-red-800">{{ erros()['validadeCnh'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-telefone">Telefone</label>
                <input id="condutor-telefone" [formField]="condutorForm.telefone"
                  [attr.aria-invalid]="erros()['telefone'] ? 'true' : null" aria-describedby="condutor-telefone-erro">
                <small id="condutor-telefone-erro" class="text-red-800">{{ erros()['telefone'] }}</small>
              </div>
            </div>
            <p class="text-slate-600">Validade vencida não impede o cadastro; a elegibilidade é verificada na contratação.</p>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar cadastro</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'vinculos') {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 class="text-xl font-semibold">Vínculos PJ — {{ nomeVinculo() }}</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarVinculos()">
            <div class="grid gap-2 sm:grid-cols-2">
              @for (pj of pjsOpcoes(); track pj.id) {
                <label class="flex items-center gap-2">
                  <input type="checkbox" [checked]="selecionado(pj.id)" (change)="alternarVinculo(pj.id, $event)">
                  {{ pj.nome }}
                </label>
              }
            </div>
            @if (pjsOpcoes().length === 0) { <p>Nenhum cliente PJ cadastrado.</p> }
            <p class="text-slate-600">Desmarque um vínculo para removê-lo. Isto não altera o vínculo direto do condutor com o contratante.</p>
            <div class="flex gap-2">
              <button type="button" (click)="cancelar()">Cancelar</button>
              <button type="submit" [disabled]="salvando()">Salvar vínculos</button>
            </div>
          </form>
        </section>
      }
    </main>
  `,
})
export class PessoasComponent {
  private consultaAtual = 0;
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'clientes', rotulo: 'Clientes' },
    { valor: 'condutores', rotulo: 'Condutores' },
  ];
  readonly aba = signal<Aba>('clientes');
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
  readonly clientes = signal<Cliente[]>([]);
  readonly condutores = signal<Condutor[]>([]);
  readonly clientesOpcoes = signal<Cliente[]>([]);
  readonly clienteOriginal = signal<{ id: string; nome: string } | null>(null);
  readonly clienteOriginalAtivo = computed(() =>
    this.clientesOpcoes().some(x => x.id === this.clienteOriginal()?.id));
  readonly pjsOpcoes = signal<Cliente[]>([]);
  readonly vinculosAtuais = signal<string[]>([]);
  readonly vinculosSelecionados = signal<Set<string>>(new Set());
  readonly vinculoPf = signal<Cliente | null>(null);
  readonly vinculoEtag = signal('');
  readonly nomeVinculo = computed(() => this.vinculoPf()?.nome ?? '');

  readonly temErros = computed(() => Object.keys(this.erros()).length > 0);
  readonly mensagensErro = computed(() => Object.values(this.erros()));

  readonly clienteModelo = signal<ClienteModelo>({
    tipo: 'PF', nome: '', cpf: '', rg: '', cnh: '', cnpj: '', telefone: '', email: '',
    estado: '', cidade: '', bairro: '', rua: '', numero: '',
  });
  readonly clienteForm = form(this.clienteModelo, caminho => {
    required(caminho.nome, { message: 'Informe o nome.' });
    required(caminho.telefone, { message: 'Informe o telefone.' });
    required(caminho.estado, { message: 'Informe o estado.' });
    required(caminho.cidade, { message: 'Informe a cidade.' });
    required(caminho.bairro, { message: 'Informe o bairro.' });
    required(caminho.rua, { message: 'Informe a rua.' });
    required(caminho.numero, { message: 'Informe o número.' });
  });

  readonly condutorModelo = signal<CondutorModelo>({
    clienteId: '', nome: '', email: '', cpf: '', cnh: '', validadeCnh: '', telefone: '',
  });
  readonly condutorForm = form(this.condutorModelo, caminho => {
    required(caminho.clienteId, { message: 'Selecione o cliente.' });
    required(caminho.nome, { message: 'Informe o nome.' });
    required(caminho.email, { message: 'Informe o e-mail.' });
    required(caminho.cpf, { message: 'Informe o CPF.' });
    required(caminho.cnh, { message: 'Informe a CNH.' });
    required(caminho.validadeCnh, { message: 'Informe a validade.' });
    required(caminho.telefone, { message: 'Informe o telefone.' });
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
      if (this.aba() === 'clientes') {
        const pagina = await this.servico.listar<Cliente>('/clientes', this.pagina());
        if (consulta !== this.consultaAtual) return;
        this.clientes.set(pagina.items);
        this.total.set(pagina.total);
      } else {
        const pagina = await this.servico.listar<Condutor>('/condutores', this.pagina());
        if (consulta !== this.consultaAtual) return;
        this.condutores.set(pagina.items);
        this.total.set(pagina.total);
      }
      const opcoes = await this.servico.listarOpcoes<Cliente>('/clientes');
      if (consulta !== this.consultaAtual) return;
      this.clientesOpcoes.set(opcoes);
      this.pjsOpcoes.set(opcoes.filter(cliente => cliente.tipo === 'PJ'));
    } catch {
      if (consulta !== this.consultaAtual) return;
      this.erroConsulta.set('Não foi possível consultar os cadastros. Verifique a conexão e tente novamente.');
    } finally {
      if (consulta === this.consultaAtual) this.carregando.set(false);
    }
  }

  paginar(delta: number) {
    this.pagina.update(x => Math.max(1, x + delta));
    void this.carregar();
  }

  novoCliente() {
    this.modo.set('cliente');
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.clienteModelo.set({
      tipo: 'PF', nome: '', cpf: '', rg: '', cnh: '', cnpj: '', telefone: '', email: '',
      estado: '', cidade: '', bairro: '', rua: '', numero: '',
    });
  }

  async editarCliente(id: string) {
    try {
      const resposta = await this.servico.obter<Cliente & { endereco: Record<string, string> }>(`/clientes/${id}`);
      const cliente = resposta.dados;
      this.clienteModelo.set({
        tipo: cliente.tipo, nome: cliente.nome, cpf: cliente.cpf ?? '', rg: cliente.rg ?? '',
        cnh: cliente.cnh ?? '', cnpj: cliente.cnpj ?? '', telefone: cliente.telefone,
        email: cliente.email ?? '', estado: cliente.endereco['estado'] ?? '',
        cidade: cliente.endereco['cidade'] ?? '', bairro: cliente.endereco['bairro'] ?? '',
        rua: cliente.endereco['rua'] ?? '', numero: cliente.endereco['numero'] ?? '',
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.modo.set('cliente');
    } catch (erro: unknown) { this.falhar(erro); }
  }

  novoCondutor() {
    this.clienteOriginal.set(null);
    this.modo.set('condutor');
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.condutorModelo.set({
      clienteId: '', nome: '', email: '', cpf: '', cnh: '', validadeCnh: '', telefone: '',
    });
  }

  async editarCondutor(id: string) {
    try {
      const resposta = await this.servico.obter<Condutor>(`/condutores/${id}`);
      const condutor = resposta.dados;
      this.clienteOriginal.set({ id: condutor.clienteId, nome: condutor.clienteNome });
      this.condutorModelo.set({
        clienteId: condutor.clienteId, nome: condutor.nome, email: condutor.email,
        cpf: condutor.cpf, cnh: condutor.cnh, validadeCnh: condutor.validadeCnh,
        telefone: condutor.telefone,
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.modo.set('condutor');
    } catch (erro: unknown) { this.falhar(erro); }
  }

  usarComoCondutor(cliente: Cliente) {
    this.clienteOriginal.set(null);
    this.modo.set('condutor');
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.condutorModelo.set({
      clienteId: cliente.id, nome: cliente.nome, email: cliente.email ?? '',
      cpf: cliente.cpf ?? '', cnh: cliente.cnh ?? '', validadeCnh: '', telefone: cliente.telefone,
    });
  }

  async abrirVinculos(cliente: Cliente) {
    try {
      const resposta = await this.servico.obter<ClienteVinculado[]>(`/clientes/${cliente.id}/vinculos-pj`);
      const opcoes = await this.servico.listarOpcoes<Cliente>('/clientes');
      this.pjsOpcoes.set(opcoes.filter(item => item.tipo === 'PJ'));
      this.vinculosAtuais.set(resposta.dados.map(item => item.id));
      this.vinculosSelecionados.set(new Set(resposta.dados.map(item => item.id)));
      this.vinculoPf.set(cliente);
      this.vinculoEtag.set(resposta.etag);
      this.erros.set({});
      this.modo.set('vinculos');
    } catch (erro: unknown) { this.falhar(erro); }
  }

  selecionado(id: string): boolean {
    return this.vinculosSelecionados().has(id);
  }

  alternarVinculo(id: string, evento: Event) {
    const selecionados = new Set(this.vinculosSelecionados());
    if ((evento.target as HTMLInputElement).checked) selecionados.add(id);
    else selecionados.delete(id);
    this.vinculosSelecionados.set(selecionados);
  }

  async salvarCliente() {
    if (this.salvando()) return;
    const modelo = this.clienteModelo();
    const erros = validarLimites({ nome: modelo.nome, telefone: modelo.telefone, email: modelo.email,
      'endereco.estado': modelo.estado, 'endereco.cidade': modelo.cidade,
      'endereco.bairro': modelo.bairro, 'endereco.rua': modelo.rua, 'endereco.numero': modelo.numero,
      ...(modelo.tipo === 'PF' ? { rg: modelo.rg, cnh: modelo.cnh, cpf: modelo.cpf.replace(/\D/g, '') }
        : { cnpj: modelo.cnpj.replace(/\D/g, '') }) },
    { nome: 200, telefone: 32, email: 254, rg: 32, cnh: 32, cpf: 11, cnpj: 14,
      'endereco.estado': 100, 'endereco.cidade': 200, 'endereco.bairro': 200, 'endereco.rua': 200, 'endereco.numero': 40 });
    if (!modelo.nome.trim()) erros['nome'] = 'Informe o nome.';
    if (!modelo.telefone.trim()) erros['telefone'] = 'Informe o telefone.';
    if (!modelo.estado.trim()) erros['endereco.estado'] = 'Informe o estado.';
    if (!modelo.cidade.trim()) erros['endereco.cidade'] = 'Informe a cidade.';
    if (!modelo.bairro.trim()) erros['endereco.bairro'] = 'Informe o bairro.';
    if (!modelo.rua.trim()) erros['endereco.rua'] = 'Informe a rua.';
    if (!modelo.numero.trim()) erros['endereco.numero'] = 'Informe o número.';
    if (modelo.tipo === 'PF') {
      if (!modelo.cpf.trim()) erros['cpf'] = 'Informe o CPF.';
      if (!modelo.rg.trim()) erros['rg'] = 'Informe o RG.';
      if (!modelo.cnh.trim()) erros['cnh'] = 'Informe a CNH.';
    } else if (!modelo.cnpj.trim()) {
      erros['cnpj'] = 'Informe o CNPJ.';
    }
    if (modelo.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(modelo.email.trim()))
      erros['email'] = 'Informe um e-mail válido.';
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    const corpo = {
      tipo: modelo.tipo,
      nome: modelo.nome,
      cpf: modelo.tipo === 'PF' ? modelo.cpf : null,
      rg: modelo.tipo === 'PF' ? modelo.rg : null,
      cnh: modelo.tipo === 'PF' ? modelo.cnh : null,
      cnpj: modelo.tipo === 'PJ' ? modelo.cnpj : null,
      telefone: modelo.telefone,
      email: modelo.email.trim() || null,
      endereco: {
        estado: modelo.estado, cidade: modelo.cidade, bairro: modelo.bairro,
        rua: modelo.rua, numero: modelo.numero,
      },
    };
    await this.executarSalvar(async () => {
      if (this.editandoId()) {
        await this.servico.editar(`/clientes/${this.editandoId()}`, corpo, this.etag());
      } else {
        await this.servico.criar('/clientes', corpo);
      }
    });
  }

  async salvarCondutor() {
    if (this.salvando()) return;
    const modelo = this.condutorModelo();
    const erros = validarLimites({ ...modelo, cpf: modelo.cpf.replace(/\D/g, '') },
      { nome: 200, email: 254, cpf: 11, cnh: 32, telefone: 32 });
    if (!modelo.clienteId) erros['clienteId'] = 'Selecione o cliente.';
    if (!modelo.nome.trim()) erros['nome'] = 'Informe o nome.';
    if (!modelo.email.trim()) erros['email'] = 'Informe o e-mail.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(modelo.email.trim())) erros['email'] = 'Informe um e-mail válido.';
    if (!modelo.cpf.trim()) erros['cpf'] = 'Informe o CPF.';
    if (!modelo.cnh.trim()) erros['cnh'] = 'Informe a CNH.';
    if (!modelo.validadeCnh) erros['validadeCnh'] = 'Informe a validade.';
    if (!modelo.telefone.trim()) erros['telefone'] = 'Informe o telefone.';
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    const corpo = { ...modelo };
    await this.executarSalvar(async () => {
      if (this.editandoId()) {
        await this.servico.editar(`/condutores/${this.editandoId()}`, corpo, this.etag());
      } else {
        await this.servico.criar('/condutores', corpo);
      }
    });
  }

  async salvarVinculos() {
    if (this.salvando()) return;
    const pf = this.vinculoPf();
    if (!pf) return;
    const atuais = new Set(this.vinculosAtuais());
    const selecionados = this.vinculosSelecionados();
    const adicionar = [...selecionados].filter(id => !atuais.has(id));
    const remover = [...atuais].filter(id => !selecionados.has(id));
    this.salvando.set(true);
    this.erros.set({});
    try {
      let etag = this.vinculoEtag();
      for (const id of adicionar) {
        const resposta = await this.servico.adicionarComEtag<ClienteVinculado[]>(
          `/clientes/${pf.id}/vinculos-pj`, { clientePjId: id }, etag);
        etag = resposta.etag;
        this.vinculoEtag.set(etag);
        this.vinculosAtuais.set(resposta.dados.map(item => item.id));
      }
      for (const id of remover) {
        const resposta = await this.servico.removerComEtag<ClienteVinculado[]>(
          `/clientes/${pf.id}/vinculos-pj/${id}`, etag);
        etag = resposta.etag;
        this.vinculoEtag.set(etag);
        this.vinculosAtuais.set(resposta.dados.map(item => item.id));
      }
      this.aviso.set('Vínculos atualizados.');
      this.modo.set('lista');
      await this.carregar();
    } catch (erro: unknown) {
      this.falhar(erro);
    } finally {
      this.salvando.set(false);
    }
  }

  async excluirCliente(cliente: Cliente) {
    await this.executarExclusao(`/clientes/${cliente.id}`);
  }

  async excluirCondutor(condutor: Condutor) {
    await this.executarExclusao(`/condutores/${condutor.id}`);
  }

  cancelar() {
    this.modo.set('lista');
    this.editandoId.set(null);
    this.erros.set({});
  }

  documentos(cliente: Cliente): string {
    if (cliente.tipo === 'PF') return `CPF ${cliente.cpf} · RG ${cliente.rg} · CNH ${cliente.cnh}`;
    return `CNPJ ${cliente.cnpj}`;
  }

  private async executarExclusao(caminho: string) {
    try {
      const resposta = await this.servico.obter<unknown>(caminho);
      await this.servico.excluir(caminho, resposta.etag);
      this.aviso.set('Cadastro excluído logicamente.');
      this.erros.set({});
      await this.carregar();
    } catch (erro: unknown) { this.falhar(erro); }
  }

  private async executarSalvar(operacao: () => Promise<unknown>) {
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      await operacao();
      this.aviso.set('Cadastro salvo. CNH da mesma pessoa mantida coerente.');
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
