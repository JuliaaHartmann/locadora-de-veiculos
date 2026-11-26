import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { FormField, form, required } from '@angular/forms/signals';
import { LucideCheck, LucidePencil, LucideTrash2, LucideX } from '@lucide/angular';
import { CadastrosService, validarLimites } from './cadastros.service';
import { SaidaCoordenador } from './saida-coordenador.service';
import { SessaoService } from './sessao.service';

type Aba = 'clientes' | 'condutores';
type TipoCliente = 'PF' | 'PJ';
type Modo = 'lista' | 'cliente' | 'condutor' | 'vinculos';

interface Cliente {
  id: string;
  numeroExibicao: string;
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
interface Exclusao { caminho: string; entidade: 'cliente' | 'condutor'; nome: string; }

interface Condutor {
  id: string;
  numeroExibicao: string;
  clienteId: string;
  clienteNome: string;
  nome: string;
  email: string | null;
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
  imports: [FormField, LucideCheck, LucidePencil, LucideTrash2, LucideX],
  styleUrls: ['./quadro-operacional.css', './pessoas.component.css'],
  template: `
    <main class="quadro-operacional pessoas">
      <p class="sobretitulo">Área administrativa</p>
      <h1>Pessoas</h1>
      <hr class="regua" aria-hidden="true">
      <p class="introducao">Clientes PF/PJ, vínculos e condutores da empresa.</p>

      <div role="tablist" aria-label="Cadastros de pessoas" class="abas flex flex-wrap" (keydown)="navegarAbas($event)">
        @for (item of abas; track item.valor) {
          <button type="button" role="tab" [id]="'aba-' + item.valor" aria-controls="painel-pessoas"
            [attr.aria-selected]="aba() === item.valor" [attr.tabindex]="aba() === item.valor ? 0 : -1"
            [attr.aria-disabled]="bloqueado() || null" (click)="trocarAba(item.valor)">
            {{ item.rotulo }}
          </button>
        }
      </div>

      @if (faseSaida() === 'incerta') {
        <div class="saida-incerta" role="alert">Não foi possível confirmar a revogação remota. Tente sair novamente ou entre de novo.</div>
        <button type="button" class="compacto mb-4" (click)="reautenticar()">Entrar novamente</button>
      }
      <div class="aviso" [class.visivel]="aviso()">
        @if (aviso()) { <svg lucideCheck aria-hidden="true" class="size-5 shrink-0"></svg> }
        <p #avisoElemento class="foco-transicao" role="status" tabindex="-1">{{ aviso() }}</p>
        @if (aviso()) {
          <button type="button" class="acao" aria-label="Dispensar mensagem" (click)="dispensarAviso()"><svg lucideX aria-hidden="true"></svg></button>
        }
      </div>
      <div #resumo id="resumo-pessoas" tabindex="-1" role="alert" class="resumo"
        [hidden]="!temErros()">
        <strong>Revise os dados informados.</strong>
        <ul class="my-1 list-disc pl-5">
          @for (mensagem of mensagensErro(); track $index) { <li>{{ mensagem }}</li> }
        </ul>
      </div>

      <div id="painel-pessoas" role="tabpanel" [attr.aria-labelledby]="'aba-' + aba()">
      @if (modo() === 'lista') {
        <section class="quadro">
          <div class="barra">
            <h2 class="text-xl font-semibold">{{ aba() === 'clientes' ? 'Clientes' : 'Condutores' }} cadastrados</h2>
            <button id="pessoas-cadastrar" type="button" class="primario" [disabled]="bloqueado()"
              (click)="aba() === 'clientes' ? novoCliente() : novoCondutor()">Cadastrar</button>
          </div>

          @if (carregando()) {
            <p class="p-6" role="status">Carregando cadastros…</p>
          } @else if (erroConsulta()) {
            <div class="p-6" role="alert">
              <p>{{ erroConsulta() }}</p>
              <button type="button" id="pessoas-tentar-consulta" [disabled]="bloqueado()" (click)="tentarConsulta()">Tentar novamente</button>
            </div>
          } @else {
            @if (aba() === 'clientes') {
              <div class="regiao-tabela" tabindex="0" role="region" aria-label="Tabela de clientes">
                <table class="w-full border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Código</th><th scope="col">Nome / tipo</th><th scope="col">Telefone</th>
                    <th scope="col">Documentos</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (cliente of clientes(); track cliente.id) {
                      <tr>
                        <td>{{ cliente.numeroExibicao }}</td>
                         <td class="nome-pessoa"><strong>{{ cliente.nome }}</strong><span class="tipo-cliente">{{ cliente.tipo }}</span></td>
                         <td class="whitespace-nowrap">{{ formatarTelefone(cliente.telefone) }}</td>
                        <td class="documentos-cliente">
                          @for (documento of documentos(cliente); track $index) { <span class="block">{{ documento }}</span> }
                        </td>
                         <td><div class="acoes-tabela whitespace-nowrap">
                           <button type="button" class="acao dica-editar" [id]="'editar-' + cliente.id" [disabled]="bloqueado()"
                             [attr.aria-label]="'Editar ' + cliente.nome" (click)="editarCliente(cliente.id)"><svg lucidePencil aria-hidden="true"></svg></button>
                           <button type="button" class="acao perigo dica-excluir" [disabled]="bloqueado()"
                             [attr.aria-label]="'Excluir ' + cliente.nome" (click)="pedirExclusao('cliente', cliente, $event)"><svg lucideTrash2 aria-hidden="true"></svg></button>
                          @if (cliente.tipo === 'PF') {
                             <button type="button" class="compacto" [id]="'vinculos-' + cliente.id" [disabled]="bloqueado()"
                               [attr.aria-label]="'Vínculos PJ de ' + cliente.nome" (click)="abrirVinculos(cliente)">Vínculos PJ</button>
                             <button type="button" class="compacto" [id]="'usar-' + cliente.id" [disabled]="bloqueado()"
                               [attr.aria-label]="'Usar como condutor: ' + cliente.nome" (click)="usarComoCondutor(cliente)">Usar como condutor</button>
                          }
                         </div></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <div class="regiao-tabela" tabindex="0" role="region" aria-label="Tabela de condutores">
                <table class="tabela-acoes-duplas w-full border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Código</th><th scope="col">Condutor</th><th scope="col">Cliente</th>
                    <th scope="col">CPF</th><th scope="col">CNH</th><th scope="col">Validade</th>
                    <th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (condutor of condutores(); track condutor.id) {
                      <tr>
                        <td>{{ condutor.numeroExibicao }}</td>
                        <td class="nome-pessoa">{{ condutor.nome }}</td>
                        <td class="nome-pessoa">{{ condutor.clienteNome }}</td>
                        <td class="dados-condutor">{{ formatarDocumento(condutor.cpf) }}</td>
                        <td class="dados-condutor">{{ condutor.cnh }}</td>
                        <td class="dados-condutor">{{ condutor.validadeCnh.split('-').reverse().join('/') }}</td>
                         <td><div class="acoes-tabela">
                           <button type="button" class="acao dica-editar" [id]="'editar-' + condutor.id" [disabled]="bloqueado()"
                             [attr.aria-label]="'Editar ' + condutor.nome" (click)="editarCondutor(condutor.id)"><svg lucidePencil aria-hidden="true"></svg></button>
                           <button type="button" class="acao perigo dica-excluir" [disabled]="bloqueado()"
                             [attr.aria-label]="'Excluir ' + condutor.nome" (click)="pedirExclusao('condutor', condutor, $event)"><svg lucideTrash2 aria-hidden="true"></svg></button>
                         </div></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (total() === 0) { <p class="barra" role="status">Nenhum registro encontrado.</p> }

            <div class="barra paginacao">
              <span>Página {{ pagina() }} · {{ total() }} {{ total() === 1 ? 'registro' : 'registros' }} · até 20 por página</span>
              <div class="flex gap-2">
                 <button type="button" id="pagina-anterior" (click)="paginar(-1)" [disabled]="bloqueado() || pagina() <= 1">Anterior</button>
                 <button type="button" id="pagina-proxima" (click)="paginar(1)" [disabled]="bloqueado() || pagina() * 20 >= total()">Próxima</button>
              </div>
            </div>
          }
        </section>
      }

      @if (modo() === 'cliente') {
        <section class="quadro editor" aria-labelledby="titulo-editor">
          <div class="barra">
            <h2 id="titulo-editor" class="foco-transicao" tabindex="-1">{{ editandoId() ? 'Editar' : 'Cadastrar' }} — cliente</h2>
          </div>
          <form class="grid gap-4 p-4" (input)="corrigirCampo($event)" (submit)="$event.preventDefault(); salvarCliente()" novalidate>
            <fieldset [disabled]="bloqueado()"><legend>Identificação</legend><div class="campos">
              <div class="grid gap-1">
                <label for="cliente-tipo">Tipo</label>
                <select id="cliente-tipo" [formField]="clienteForm.tipo" (change)="limparErro('cpf', 'rg', 'cnh', 'cnpj')">
                  <option value="PF">PF</option>
                  <option value="PJ">PJ</option>
                </select>
              </div>
              <div class="grid gap-1">
                <label for="cliente-nome">Nome</label>
                <input id="cliente-nome" [formField]="clienteForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('nome', 'cliente-nome')">
                <small id="cliente-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              @if (clienteModelo().tipo === 'PF') {
                <div class="grid gap-1">
                  <label for="cliente-cpf">CPF</label>
                  <input id="cliente-cpf" [formField]="clienteForm.cpf"
                    inputmode="numeric" [attr.aria-invalid]="erros()['cpf'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('cpf', 'cliente-cpf')">
                  <small id="cliente-cpf-erro" class="text-red-800">{{ erros()['cpf'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="cliente-rg">RG</label>
                  <input id="cliente-rg" [formField]="clienteForm.rg"
                    [attr.aria-invalid]="erros()['rg'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('rg', 'cliente-rg')">
                  <small id="cliente-rg-erro" class="text-red-800">{{ erros()['rg'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="cliente-cnh">CNH</label>
                  <input id="cliente-cnh" [formField]="clienteForm.cnh"
                    [attr.aria-invalid]="erros()['cnh'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('cnh', 'cliente-cnh')">
                  <small id="cliente-cnh-erro" class="text-red-800">{{ erros()['cnh'] }}</small>
                </div>
              } @else {
                <div class="grid gap-1">
                  <label for="cliente-cnpj">CNPJ</label>
                  <input id="cliente-cnpj" [formField]="clienteForm.cnpj"
                    inputmode="numeric" [attr.aria-invalid]="erros()['cnpj'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('cnpj', 'cliente-cnpj')">
                  <small id="cliente-cnpj-erro" class="text-red-800">{{ erros()['cnpj'] }}</small>
                </div>
              }
              </div></fieldset>
              <fieldset [disabled]="bloqueado()"><legend>Contato</legend><div class="campos">
              <div class="grid gap-1">
                <label for="cliente-telefone">Telefone</label>
                <input id="cliente-telefone" [formField]="clienteForm.telefone"
                  [attr.aria-invalid]="erros()['telefone'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('telefone', 'cliente-telefone')">
                <small id="cliente-telefone-erro" class="text-red-800">{{ erros()['telefone'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-email">E-mail (opcional)</label>
                <input id="cliente-email" type="email" [formField]="clienteForm.email"
                  [attr.aria-invalid]="erros()['email'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('email', 'cliente-email')">
                <small id="cliente-email-erro" class="text-red-800">{{ erros()['email'] }}</small>
              </div>
              </div></fieldset>
              <fieldset [disabled]="bloqueado()"><legend>Endereço</legend><div class="campos">
              <div class="grid gap-1">
                <label for="cliente-estado">Estado</label>
                <input id="cliente-estado" [formField]="clienteForm.estado"
                  [attr.aria-invalid]="erros()['endereco.estado'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('endereco.estado', 'cliente-estado')">
                <small id="cliente-estado-erro" class="text-red-800">{{ erros()['endereco.estado'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-cidade">Cidade</label>
                <input id="cliente-cidade" [formField]="clienteForm.cidade"
                  [attr.aria-invalid]="erros()['endereco.cidade'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('endereco.cidade', 'cliente-cidade')">
                <small id="cliente-cidade-erro" class="text-red-800">{{ erros()['endereco.cidade'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-bairro">Bairro</label>
                <input id="cliente-bairro" [formField]="clienteForm.bairro"
                  [attr.aria-invalid]="erros()['endereco.bairro'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('endereco.bairro', 'cliente-bairro')">
                <small id="cliente-bairro-erro" class="text-red-800">{{ erros()['endereco.bairro'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-rua">Rua</label>
                <input id="cliente-rua" [formField]="clienteForm.rua"
                  [attr.aria-invalid]="erros()['endereco.rua'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('endereco.rua', 'cliente-rua')">
                <small id="cliente-rua-erro" class="text-red-800">{{ erros()['endereco.rua'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="cliente-numero">Número</label>
                <input id="cliente-numero" [formField]="clienteForm.numero"
                  [attr.aria-invalid]="erros()['endereco.numero'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('endereco.numero', 'cliente-numero')">
                <small id="cliente-numero-erro" class="text-red-800">{{ erros()['endereco.numero'] }}</small>
              </div>
            </div></fieldset>
            <p class="text-slate-600">Nenhuma consulta oficial de documentos é realizada.</p>
            <div class="acoes-editor">
              <button type="button" [disabled]="bloqueado()" (click)="cancelar()">Cancelar</button>
              <button type="submit" class="primario" [disabled]="bloqueado()">Salvar cadastro</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'condutor') {
        <section class="quadro editor" aria-labelledby="titulo-editor">
          <div class="barra">
            <h2 id="titulo-editor" class="foco-transicao" tabindex="-1">{{ editandoId() ? 'Editar' : 'Cadastrar' }} — condutor</h2>
          </div>
          <form class="grid gap-4 p-4" (input)="corrigirCampo($event)" (submit)="$event.preventDefault(); salvarCondutor()" novalidate>
            <fieldset [disabled]="bloqueado()"><legend>Vínculo</legend><div class="campos">
              <div class="grid gap-1">
                <label for="condutor-cliente">Cliente vinculado</label>
                <select id="condutor-cliente" [formField]="condutorForm.clienteId"
                  [attr.aria-invalid]="erros()['clienteId'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('clienteId', 'condutor-cliente')">
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
              </div></fieldset>
              <fieldset [disabled]="bloqueado()"><legend>Identificação</legend><div class="campos">
              <div class="grid gap-1">
                <label for="condutor-nome">Nome</label>
                <input id="condutor-nome" [formField]="condutorForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('nome', 'condutor-nome')">
                <small id="condutor-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-cpf">CPF</label>
                <input id="condutor-cpf" [formField]="condutorForm.cpf"
                  inputmode="numeric" [attr.aria-invalid]="erros()['cpf'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('cpf', 'condutor-cpf')">
                <small id="condutor-cpf-erro" class="text-red-800">{{ erros()['cpf'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-cnh">CNH</label>
                <input id="condutor-cnh" [formField]="condutorForm.cnh"
                  [attr.aria-invalid]="erros()['cnh'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('cnh', 'condutor-cnh')">
                <small id="condutor-cnh-erro" class="text-red-800">{{ erros()['cnh'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-validade">Validade da CNH</label>
                <input id="condutor-validade" type="date" [formField]="condutorForm.validadeCnh"
                  [attr.aria-invalid]="erros()['validadeCnh'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('validadeCnh', 'condutor-validade')">
                <small id="condutor-validade-erro" class="text-red-800">{{ erros()['validadeCnh'] }}</small>
              </div>
              </div></fieldset>
              <fieldset [disabled]="bloqueado()"><legend>Contato</legend><div class="campos">
              <div class="grid gap-1">
                <label for="condutor-email">E-mail (opcional)</label>
                <input id="condutor-email" type="email" [formField]="condutorForm.email"
                  [attr.aria-invalid]="erros()['email'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('email', 'condutor-email')">
                <small id="condutor-email-erro" class="text-red-800">{{ erros()['email'] }}</small>
              </div>
              <div class="grid gap-1">
                <label for="condutor-telefone">Telefone</label>
                <input id="condutor-telefone" [formField]="condutorForm.telefone"
                  [attr.aria-invalid]="erros()['telefone'] ? 'true' : null" [attr.aria-describedby]="descricaoErro('telefone', 'condutor-telefone')">
                <small id="condutor-telefone-erro" class="text-red-800">{{ erros()['telefone'] }}</small>
              </div>
            </div></fieldset>
            <p class="text-slate-600">Validade vencida não impede o cadastro; a elegibilidade é verificada na contratação.</p>
            <div class="acoes-editor">
              <button type="button" [disabled]="bloqueado()" (click)="cancelar()">Cancelar</button>
              <button type="submit" class="primario" [disabled]="bloqueado()">Salvar cadastro</button>
            </div>
          </form>
        </section>
      }

      @if (modo() === 'vinculos') {
        <section class="quadro editor vinculos" aria-labelledby="titulo-editor">
          <div class="barra">
            <h2 id="titulo-editor" class="foco-transicao" tabindex="-1">Vínculos PJ — {{ nomeVinculo() }}</h2>
          </div>
          <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarVinculos()">
            <fieldset [disabled]="bloqueado()"><legend>Clientes PJ</legend>
            <div class="campos">
              @for (pj of pjsOpcoes(); track pj.id) {
                <label class="opcao-pj">
                  <input type="checkbox" [checked]="selecionado(pj.id)" (change)="alternarVinculo(pj.id, $event)">
                  <span>{{ pj.nome }}</span>
                </label>
              }
            </div>
            @if (pjsOpcoes().length === 0) { <p>Nenhum cliente PJ cadastrado.</p> }
            </fieldset>
            <p class="text-slate-600">Desmarque um vínculo para removê-lo. Isto não altera o vínculo direto do condutor com o contratante.</p>
            <div class="acoes-editor">
              <button type="button" [disabled]="bloqueado()" (click)="cancelar()">Cancelar</button>
              <button type="submit" class="primario" [disabled]="bloqueado()">Salvar vínculos</button>
            </div>
          </form>
        </section>
      }
      </div>
      <dialog #dialogo aria-labelledby="titulo-exclusao-pessoas" aria-describedby="registro-exclusao-pessoas historico-exclusao-pessoas"
        (cancel)="$event.preventDefault(); fecharExclusao(false)" (keydown)="conterFoco($event, cancelarExclusao, confirmarExclusao)">
        <h2 id="titulo-exclusao-pessoas">Excluir {{ exclusao()?.entidade }}</h2>
        <p id="registro-exclusao-pessoas">Deseja excluir {{ exclusao()?.nome }}?</p>
        <p id="historico-exclusao-pessoas">O histórico será preservado.</p>
        <div class="acoes-editor">
          <button #cancelarExclusao type="button" autofocus (click)="fecharExclusao(false)">Cancelar</button>
          <button #confirmarExclusao type="button" class="excluir" (click)="fecharExclusao(true)">Excluir</button>
        </div>
      </dialog>
    </main>
  `,
})
export class PessoasComponent {
  private consultaAtual = 0;
  private consultaEditor = 0;
  private referenciaEditor = '';
  private retornoEditor = 'pessoas-cadastrar';
  private origemExclusao?: HTMLElement;
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destruicao = inject(DestroyRef);
  private readonly sessao = inject(SessaoService);
  private readonly saida = inject(SaidaCoordenador);
  private readonly contexto = this.sessao.contexto();
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');
  private readonly avisoElemento = viewChild<ElementRef<HTMLElement>>('avisoElemento');
  private readonly dialogo = viewChild<ElementRef<HTMLDialogElement>>('dialogo');
  readonly exclusao = signal<Exclusao | null>(null);
  readonly faseSaida = this.saida.fase;

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'clientes', rotulo: 'Clientes' },
    { valor: 'condutores', rotulo: 'Condutores' },
  ];
  readonly aba = signal<Aba>('clientes');
  readonly modo = signal<Modo>('lista');
  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly bloqueado = computed(() => this.salvando() || this.faseSaida() !== 'livre');
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
    required(caminho.cpf, { message: 'Informe o CPF.' });
    required(caminho.cnh, { message: 'Informe a CNH.' });
    required(caminho.validadeCnh, { message: 'Informe a validade.' });
    required(caminho.telefone, { message: 'Informe o telefone.' });
  });

  constructor() {
    this.destruicao.onDestroy(this.saida.registrar({ estado: () => ({
      rascunhoAlterado: (this.modo() === 'cliente' || this.modo() === 'condutor') &&
        this.snapshotEditor() !== this.referenciaEditor,
      selecaoPendente: this.modo() === 'vinculos' && this.alteracoesVinculos().length > 0,
      operacaoEmCurso: this.salvando(),
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
    const { cpf, cnh, rg, cnpj, ...cliente } = this.clienteModelo();
    const modelo = this.modo() === 'condutor' ? this.condutorModelo() :
      { ...cliente, ...(cliente.tipo === 'PF' ? { cpf, cnh, rg } : { cnpj }) };
    return JSON.stringify(Object.values(modelo).map(valor => valor.trim()));
  }

  private focar(id: string, alternativa = '.regiao-tabela') {
    afterNextRender(() => {
      if (!this.contextoAtual()) return;
      const host = this.host.nativeElement;
      (host.querySelector<HTMLElement>(`#${CSS.escape(id)}:not(:disabled)`) ??
        host.querySelector<HTMLElement>(alternativa))?.focus();
    }, { injector: this.injector });
  }

  private abrirEditor(modo: Exclude<Modo, 'lista'>, retorno: string) {
    ++this.consultaEditor;
    this.aviso.set('');
    this.erros.set({});
    this.retornoEditor = retorno;
    this.modo.set(modo);
    this.referenciaEditor = this.snapshotEditor();
    this.focar('titulo-editor');
  }

  navegarAbas(evento: KeyboardEvent) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(evento.key)) return;
    evento.preventDefault();
    if (this.bloqueado()) return;
    const destino = evento.key === 'Home' ? 'clientes' : evento.key === 'End' ? 'condutores' :
      this.aba() === 'clientes' ? 'condutores' : 'clientes';
    this.trocarAba(destino);
    this.focar('aba-' + destino);
  }

  trocarAba(aba: Aba) {
    if (this.bloqueado() || this.aba() === aba) return;
    this.fecharEditor();
    this.aba.set(aba);
    this.pagina.set(1);
    this.aviso.set('');
    void this.carregar();
  }

  async carregar() {
    ++this.consultaEditor;
    const consulta = ++this.consultaAtual;
    this.carregando.set(true);
    this.erroConsulta.set('');
    try {
      if (this.aba() === 'clientes') {
        const pagina = await this.servico.listar<Cliente>('/clientes', this.pagina());
        if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
        this.clientes.set(pagina.items);
        this.total.set(pagina.total);
      } else {
        const pagina = await this.servico.listar<Condutor>('/condutores', this.pagina());
        if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
        this.condutores.set(pagina.items);
        this.total.set(pagina.total);
      }
      const ultima = Math.max(1, Math.ceil(this.total() / 20));
      if (this.pagina() > ultima) {
        this.pagina.set(ultima);
        await this.carregar();
      }
    } catch {
      if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
      this.erroConsulta.set('Não foi possível consultar os cadastros. Verifique a conexão e tente novamente.');
    } finally {
      if (consulta === this.consultaAtual) this.carregando.set(false);
    }
  }

  async paginar(delta: number) {
    if (this.bloqueado() || this.carregando() || (delta < 0 ? this.pagina() <= 1 : this.pagina() * 20 >= this.total())) return;
    this.pagina.update(x => Math.max(1, x + delta));
    const consulta = this.consultaAtual + 1;
    await this.carregar();
    if (consulta === this.consultaAtual && this.modo() === 'lista')
      this.focar(this.erroConsulta() ? 'pessoas-tentar-consulta' : delta > 0 ? 'pagina-proxima' : 'pagina-anterior');
  }

  async tentarConsulta() {
    if (this.bloqueado() || this.carregando()) return;
    const consulta = this.consultaAtual + 1;
    await this.carregar();
    if (consulta === this.consultaAtual && this.modo() === 'lista')
      this.focar(this.erroConsulta() ? 'pessoas-tentar-consulta' : 'pessoas-cadastrar');
  }

  novoCliente() {
    if (this.bloqueado()) return;
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.clienteModelo.set({
      tipo: 'PF', nome: '', cpf: '', rg: '', cnh: '', cnpj: '', telefone: '', email: '',
      estado: '', cidade: '', bairro: '', rua: '', numero: '',
    });
    this.abrirEditor('cliente', 'pessoas-cadastrar');
  }

  async editarCliente(id: string) {
    if (this.bloqueado()) return;
    const consulta = ++this.consultaEditor;
    try {
      const resposta = await this.servico.obter<Cliente & { endereco: Record<string, string> }>(`/clientes/${id}`);
      if (!this.editorAtual(consulta)) return;
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
      this.abrirEditor('cliente', 'editar-' + id);
    } catch (erro: unknown) { if (this.editorAtual(consulta)) this.falhar(erro); }
  }

  async novoCondutor(cliente?: Cliente) {
    if (this.bloqueado()) return;
    const consulta = ++this.consultaEditor;
    try {
      const opcoes = await this.servico.listarOpcoes<Cliente>('/clientes');
      if (!this.editorAtual(consulta)) return;
      this.clientesOpcoes.set(opcoes);
    } catch (erro: unknown) {
      if (this.editorAtual(consulta)) this.falhar(erro);
      return;
    }
    this.clienteOriginal.set(null);
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.condutorModelo.set({
      clienteId: cliente?.id ?? '', nome: cliente?.nome ?? '', email: cliente?.email ?? '',
      cpf: cliente?.cpf ?? '', cnh: cliente?.cnh ?? '', validadeCnh: '', telefone: cliente?.telefone ?? '',
    });
    this.abrirEditor('condutor', cliente ? 'usar-' + cliente.id : 'pessoas-cadastrar');
  }

  async editarCondutor(id: string) {
    if (this.bloqueado()) return;
    const consulta = ++this.consultaEditor;
    try {
      const [resposta, opcoes] = await Promise.all([
        this.servico.obter<Condutor>(`/condutores/${id}`),
        this.servico.listarOpcoes<Cliente>('/clientes'),
      ]);
      if (!this.editorAtual(consulta)) return;
      this.clientesOpcoes.set(opcoes);
      const condutor = resposta.dados;
      this.clienteOriginal.set({ id: condutor.clienteId, nome: condutor.clienteNome });
      this.condutorModelo.set({
        clienteId: condutor.clienteId, nome: condutor.nome, email: condutor.email ?? '',
        cpf: condutor.cpf, cnh: condutor.cnh, validadeCnh: condutor.validadeCnh,
        telefone: condutor.telefone,
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.abrirEditor('condutor', 'editar-' + id);
    } catch (erro: unknown) { if (this.editorAtual(consulta)) this.falhar(erro); }
  }

  usarComoCondutor(cliente: Cliente) {
    return this.novoCondutor(cliente);
  }

  async abrirVinculos(cliente: Cliente) {
    if (this.bloqueado()) return;
    const consulta = ++this.consultaEditor;
    try {
      const [resposta, opcoes] = await Promise.all([
        this.servico.obter<ClienteVinculado[]>(`/clientes/${cliente.id}/vinculos-pj`),
        this.servico.listarOpcoes<Cliente>('/clientes'),
      ]);
      if (!this.editorAtual(consulta)) return;
      this.pjsOpcoes.set(opcoes.filter(item => item.tipo === 'PJ'));
      this.vinculosAtuais.set(resposta.dados.map(item => item.id));
      this.vinculosSelecionados.set(new Set(resposta.dados.map(item => item.id)));
      this.vinculoPf.set(cliente);
      this.vinculoEtag.set(resposta.etag);
      this.erros.set({});
      this.abrirEditor('vinculos', 'vinculos-' + cliente.id);
    } catch (erro: unknown) { if (this.editorAtual(consulta)) this.falhar(erro); }
  }

  private editorAtual(consulta: number) {
    return consulta === this.consultaEditor && this.contextoAtual() && !this.bloqueado() && !this.exclusao();
  }

  selecionado(id: string): boolean {
    return this.vinculosSelecionados().has(id);
  }

  alternarVinculo(id: string, evento: Event) {
    if (this.bloqueado()) return;
    const selecionados = new Set(this.vinculosSelecionados());
    if ((evento.target as HTMLInputElement).checked) selecionados.add(id);
    else selecionados.delete(id);
    this.vinculosSelecionados.set(selecionados);
  }

  private validarCliente(modelo: ClienteModelo) {
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
    return erros;
  }

  async salvarCliente() {
    if (this.bloqueado() || this.modo() !== 'cliente') return;
    const modelo = this.clienteModelo();
    const erros = this.validarCliente(modelo);
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
    }, modelo.nome);
  }

  private validarCondutor(modelo: CondutorModelo) {
    const erros = validarLimites({ ...modelo, cpf: modelo.cpf.replace(/\D/g, '') },
      { nome: 200, email: 254, cpf: 11, cnh: 32, telefone: 32 });
    if (!modelo.clienteId) erros['clienteId'] = 'Selecione o cliente.';
    if (!modelo.nome.trim()) erros['nome'] = 'Informe o nome.';
    if (modelo.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(modelo.email.trim()))
      erros['email'] = 'Informe um e-mail válido.';
    if (!modelo.cpf.trim()) erros['cpf'] = 'Informe o CPF.';
    if (!modelo.cnh.trim()) erros['cnh'] = 'Informe a CNH.';
    if (!modelo.validadeCnh) erros['validadeCnh'] = 'Informe a validade.';
    if (!modelo.telefone.trim()) erros['telefone'] = 'Informe o telefone.';
    return erros;
  }

  async salvarCondutor() {
    if (this.bloqueado() || this.modo() !== 'condutor') return;
    const modelo = this.condutorModelo();
    const erros = this.validarCondutor(modelo);
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    const corpo = { ...modelo, email: modelo.email.trim() || null };
    await this.executarSalvar(async () => {
      if (this.editandoId()) {
        await this.servico.editar(`/condutores/${this.editandoId()}`, corpo, this.etag());
      } else {
        await this.servico.criar('/condutores', corpo);
      }
    }, modelo.nome);
  }

  private alteracoesVinculos() {
    const atuais = new Set(this.vinculosAtuais());
    const selecionados = this.vinculosSelecionados();
    return [
      ...[...selecionados].filter(id => !atuais.has(id)).map(id => ({ id, adicionar: true })),
      ...[...atuais].filter(id => !selecionados.has(id)).map(id => ({ id, adicionar: false })),
    ];
  }

  async salvarVinculos() {
    if (this.bloqueado() || this.modo() !== 'vinculos') return;
    const pf = this.vinculoPf();
    if (!pf) return;
    const alteracoes = this.alteracoesVinculos();
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    let aplicadas = 0;
    try {
      const caminho = `/clientes/${pf.id}/vinculos-pj`;
      for (const { id, adicionar } of alteracoes) {
        const resposta = adicionar ?
          await this.servico.adicionarComEtag<ClienteVinculado[]>(caminho, { clientePjId: id }, this.vinculoEtag()) :
          await this.servico.removerComEtag<ClienteVinculado[]>(`${caminho}/${id}`, this.vinculoEtag());
        if (!this.contextoAtual()) return;
        ++aplicadas;
        this.vinculoEtag.set(resposta.etag);
        this.vinculosAtuais.set(resposta.dados.map(item => item.id));
      }
      this.fecharEditor();
      this.anunciar(alteracoes.length ? `Vínculos atualizados: ${pf.nome}.` : 'Nenhum vínculo foi alterado.');
    } catch (erro: unknown) {
      if (this.contextoAtual()) {
        const descrever = (lista: typeof alteracoes) => lista.map(({ id, adicionar }) => {
          const nome = this.pjsOpcoes().find(pj => pj.id === id)?.nome ?? 'Cliente PJ';
          return `${adicionar ? 'Vincular' : 'Remover vínculo com'} ${nome}`;
        }).join('; ');
        this.falharCampos({
          ...(aplicadas ? { parcial: `Vínculos atualizados parcialmente. Aplicadas: ${descrever(alteracoes.slice(0, aplicadas))}. Pendentes: ${descrever(this.alteracoesVinculos())}. A seleção foi preservada.` } : {}),
          ...CadastrosService.errosDe(erro),
        });
      }
    } finally {
      this.salvando.set(false);
      liberar();
    }
  }

  pedirExclusao(entidade: Exclusao['entidade'], registro: Cliente | Condutor, evento: Event) {
    if (this.bloqueado() || this.exclusao()) return;
    ++this.consultaEditor;
    this.origemExclusao = evento.currentTarget as HTMLElement;
    const exclusao = { entidade, nome: registro.nome,
      caminho: `/${entidade === 'cliente' ? 'clientes' : 'condutores'}/${registro.id}` };
    this.exclusao.set(exclusao);
    afterNextRender(() => {
      if (this.contextoAtual() && this.exclusao() === exclusao) this.dialogo()?.nativeElement.showModal();
    }, { injector: this.injector });
  }

  async fecharExclusao(confirmada: boolean) {
    const exclusao = this.exclusao();
    const origem = this.origemExclusao;
    this.dialogo()?.nativeElement.close();
    this.exclusao.set(null);
    this.origemExclusao = undefined;
    if (confirmada && exclusao) await this.executarExclusao(exclusao);
    else origem?.focus();
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
    this.focar(this.retornoEditor);
  }

  private fecharEditor() {
    ++this.consultaEditor;
    this.modo.set('lista');
    this.editandoId.set(null);
    this.erros.set({});
  }

  documentos(cliente: Cliente): string[] {
    if (cliente.tipo === 'PF') return [`CPF ${this.formatarDocumento(cliente.cpf)}`, `RG ${cliente.rg ?? '—'}`, `CNH ${cliente.cnh ?? '—'}`];
    return [`CNPJ ${this.formatarDocumento(cliente.cnpj)}`];
  }

  formatarDocumento(valor: string | null) {
    return (valor ?? '—')
      .replace(/^(\d{3})(\d{3})(\d{3})(\d{2})$/, '$1.$2.$3-$4')
      .replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, '$1.$2.$3/$4-$5');
  }

  formatarTelefone(valor: string) {
    if (!/^[\d().\s-]+$/.test(valor)) return valor;
    const digitos = valor.replace(/\D/g, '');
    if (digitos.length < 8 || digitos.length > 11) return valor;
    const ddd = digitos.length >= 10 ? `(${digitos.slice(0, 2)}) ` : '';
    const numero = ddd ? digitos.slice(2) : digitos;
    return `${ddd}${numero.slice(0, -4)}-${numero.slice(-4)}`;
  }

  private iniciarMutacao() {
    return this.contextoAtual() ? this.saida.iniciarMutacao() : undefined;
  }

  limparErro(...campos: string[]) {
    const erros = { ...this.erros() };
    for (const campo of campos) delete erros[campo];
    this.erros.set(erros);
  }

  descricaoErro(campo: string, id: string) {
    return this.erros()[campo] ? `${id}-erro resumo-pessoas` : null;
  }

  corrigirCampo(evento: Event) {
    const controle = evento.target as HTMLInputElement | HTMLSelectElement;
    const campo = controle.id.split('-')[1];
    if (!campo || campo === 'tipo') return;
    const chave = ['estado', 'cidade', 'bairro', 'rua', 'numero'].includes(campo) ? `endereco.${campo}` :
      campo === 'cliente' ? 'clienteId' : campo === 'validade' ? 'validadeCnh' : campo;
    if (!this.erros()[chave]) return;
    const condutor = this.modo() === 'condutor';
    const propriedade = chave.replace('endereco.', '');
    const errosLocais = condutor ?
      this.validarCondutor({ ...this.condutorModelo(), [propriedade]: controle.value }) :
      this.validarCliente({ ...this.clienteModelo(), [propriedade]: controle.value });
    if (errosLocais[chave]) this.erros.update(erros => ({ ...erros, [chave]: errosLocais[chave] }));
    else this.limparErro(chave);
  }

  dispensarAviso() {
    this.aviso.set('');
    this.focar('pessoas-cadastrar');
  }

  private anunciar(texto: string) {
    this.aviso.set(texto);
    afterNextRender(() => {
      if (this.contextoAtual()) this.avisoElemento()?.nativeElement.focus();
    }, { injector: this.injector });
  }

  private async executarExclusao({ caminho, nome }: Exclusao) {
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    this.salvando.set(true);
    this.aviso.set('');
    this.erros.set({});
    try {
      const resposta = await this.servico.obter<unknown>(caminho);
      if (!this.contextoAtual()) return;
      await this.servico.excluir(caminho, resposta.etag);
      if (!this.contextoAtual()) return;
      this.anunciar(`${nome}: cadastro excluído.`);
      await this.carregar();
    } catch (erro: unknown) {
      if (this.contextoAtual()) this.falhar(erro);
    } finally {
      this.salvando.set(false);
      liberar();
    }
  }

  private async executarSalvar(operacao: () => Promise<unknown>, nome: string) {
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      await operacao();
      if (!this.contextoAtual()) return;
      this.fecharEditor();
      this.anunciar(`Cadastro salvo: ${nome}.`);
      await this.carregar();
    } catch (erro: unknown) {
      if (this.contextoAtual()) this.falhar(erro);
    } finally {
      this.salvando.set(false);
      liberar();
    }
  }

  private falhar(erro: unknown) {
    this.falharCampos(CadastrosService.errosDe(erro));
  }

  private falharCampos(erros: Record<string, string>) {
    this.erros.set(erros);
    afterNextRender(() => {
      if (this.contextoAtual()) this.resumo()?.nativeElement.focus();
    }, { injector: this.injector });
  }
}
