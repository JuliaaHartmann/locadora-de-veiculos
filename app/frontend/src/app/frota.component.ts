import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, effect, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormField, form, required } from '@angular/forms/signals';
import { CadastrosService, decimalDoFormulario, formatarMoeda, validarLimites } from './cadastros.service';
import { FotoMiniaturaComponent, LeiturasMiniaturasFrota } from './foto-miniatura.component';
import { SessaoService } from './sessao.service';

type Aba = 'grupos' | 'planos' | 'veiculos';
type Modalidade = 'Diario' | 'Controlado' | 'Livre';
type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';

interface Plano {
  id: string;
  grupoId: string;
  grupoNome: string;
  modalidade: Modalidade;
  precoDiario: string;
  precoKm: string | null;
  franquiaDiariaKm: number | null;
}

interface Grupo {
  id: string;
  nome: string;
  planos: Plano[];
}

interface Veiculo {
  id: string;
  grupoId: string;
  grupoNome: string;
  placa: string;
  marca: string;
  modelo: string;
  cor: string;
  ano: number;
  combustivel: Combustivel;
  capacidadeLitros: string;
}

interface GrupoModelo { nome: string; }
interface PlanoModelo {
  grupoId: string; modalidade: Modalidade; precoDiario: string; precoKm: string; franquiaDiariaKm: string;
}
interface VeiculoModelo {
  grupoId: string; placa: string; marca: string; modelo: string; cor: string;
  ano: string; combustivel: Combustivel; capacidadeLitros: string;
}

@Component({
  standalone: true,
  imports: [FormField, FotoMiniaturaComponent],
  providers: [LeiturasMiniaturasFrota],
  template: `
    <main class="max-w-6xl">
      <p class="text-sm text-slate-600">Cadastros / Frota e preços</p>
      <h1>Frota</h1>
      <p class="text-slate-600">Prepare os grupos, as modalidades e os dados da frota.</p>

      <nav aria-label="Cadastros da frota" class="my-4 flex flex-wrap gap-2">
        @for (item of abas; track item.valor) {
          <button type="button" (click)="trocarAba(item.valor)" [disabled]="bloquearNavegacaoFoto()"
            [attr.aria-current]="aba() === item.valor ? 'page' : null"
            [class]="aba() === item.valor ? 'bg-blue-700 text-white border-blue-700' : 'bg-white text-slate-900 border border-slate-400'">
            {{ item.rotulo }}
          </button>
        }
      </nav>

      <p #avisoElemento role="status" tabindex="-1" class="font-medium text-slate-700">{{ aviso() }}</p>
      <div #resumo tabindex="-1" role="alert" class="my-3 rounded-lg border border-red-300 bg-red-50 p-4"
        [hidden]="!temErros()">
        <strong>Revise os dados informados.</strong>
        <ul class="my-1 list-disc pl-5">
          @for (mensagem of mensagensErro(); track mensagem) { <li>{{ mensagem }}</li> }
        </ul>
      </div>

      @if (!editando()) {
        <section class="overflow-hidden rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="flex flex-wrap items-center justify-between gap-3 p-4">
            <h2 class="text-xl font-semibold">{{ tituloLista() }} cadastrados</h2>
            <button type="button" (click)="novo()">Cadastrar</button>
          </div>

          @if (aba() === 'veiculos') {
            <div class="flex flex-wrap items-center gap-4 border-t border-slate-200 p-4">
              <label for="filtro-grupo">Filtrar por grupo
                <select id="filtro-grupo" [value]="filtroGrupo()" (change)="filtrarGrupo($event)">
                  <option value="">Todos os grupos</option>
                  @for (grupo of gruposOpcoes(); track grupo.id) {
                    <option [value]="grupo.id">{{ grupo.nome }}</option>
                  }
                </select>
              </label>
              <label class="flex items-center gap-2">
                <input type="checkbox" [checked]="agrupar()" (change)="alternarAgrupamento($event)">
                Agrupar por grupo
              </label>
            </div>
          }

          @if (carregando()) {
            <p class="p-6" role="status">Carregando cadastros…</p>
          } @else if (erroConsulta()) {
            <div class="p-6" role="alert">
              <p>{{ erroConsulta() }}</p>
              <button type="button" (click)="carregar()">Tentar novamente</button>
            </div>
          } @else {
            @if (aba() === 'grupos') {
              <div class="overflow-auto">
                <table class="w-full min-w-[700px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">ID</th><th scope="col">Nome</th>
                    <th scope="col">Planos e valores</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (grupo of grupos(); track grupo.id) {
                      <tr>
                        <td>{{ grupo.id }}</td>
                        <td>{{ grupo.nome }}</td>
                        <td>{{ resumoPlanos(grupo) }}</td>
                        <td class="whitespace-nowrap">
                          <button type="button" (click)="editarGrupo(grupo.id)">Editar</button>
                          <button type="button" (click)="excluirGrupo(grupo)">Excluir</button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (aba() === 'planos') {
              <div class="overflow-auto">
                <table class="w-full min-w-[700px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">ID</th><th scope="col">Grupo</th><th scope="col">Modalidade</th>
                    <th scope="col">Parâmetros</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (plano of planos(); track plano.id) {
                      <tr>
                        <td>{{ plano.id }}</td>
                        <td>{{ plano.grupoNome }}</td>
                        <td>{{ rotuloModalidade(plano.modalidade) }}</td>
                        <td>{{ parametrosPlano(plano) }}</td>
                        <td class="whitespace-nowrap">
                          <button type="button" (click)="editarPlano(plano.id)">Editar</button>
                          <button type="button" (click)="excluirPlano(plano)">Excluir</button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (aba() === 'veiculos') {
              <div class="overflow-auto focus-visible:outline-3 focus-visible:-outline-offset-3 focus-visible:outline-blue-700"
                role="region" aria-label="Tabela de veículos" tabindex="0">
                <table class="w-full min-w-[850px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Foto</th><th scope="col">ID</th><th scope="col">Placa</th><th scope="col">Marca / modelo</th>
                    <th scope="col">Cor</th><th scope="col">Combustível</th><th scope="col">Grupo</th>
                    <th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @if (agrupar()) {
                      @for (bloco of veiculosAgrupados(); track bloco.grupoId) {
                        <tr><th colspan="8" scope="rowgroup">{{ bloco.grupoNome }}</th></tr>
                        @for (veiculo of bloco.itens; track veiculo.id) {
                          <tr>
                            <td><app-foto-miniatura [veiculoId]="veiculo.id" [placa]="veiculo.placa" /></td>
                            <td>{{ veiculo.id }}</td>
                            <td>{{ veiculo.placa }}</td>
                            <td>{{ veiculo.marca }} / {{ veiculo.modelo }}</td>
                            <td>{{ veiculo.cor }}</td>
                            <td>{{ rotuloCombustivel(veiculo.combustivel) }}</td>
                            <td>{{ veiculo.grupoNome }}</td>
                            <td class="whitespace-nowrap">
                              <button type="button" (click)="editarVeiculo(veiculo.id)">Editar</button>
                              <button type="button" (click)="excluirVeiculo(veiculo)">Excluir</button>
                            </td>
                          </tr>
                        }
                      }
                    } @else {
                      @for (veiculo of veiculos(); track veiculo.id) {
                        <tr>
                          <td><app-foto-miniatura [veiculoId]="veiculo.id" [placa]="veiculo.placa" /></td>
                          <td>{{ veiculo.id }}</td>
                          <td>{{ veiculo.placa }}</td>
                          <td>{{ veiculo.marca }} / {{ veiculo.modelo }}</td>
                          <td>{{ veiculo.cor }}</td>
                          <td>{{ rotuloCombustivel(veiculo.combustivel) }}</td>
                          <td>{{ veiculo.grupoNome }}</td>
                          <td class="whitespace-nowrap">
                            <button type="button" (click)="editarVeiculo(veiculo.id)">Editar</button>
                            <button type="button" (click)="excluirVeiculo(veiculo)">Excluir</button>
                          </td>
                        </tr>
                      }
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
      } @else {
        <section class="rounded-xl border border-slate-300 bg-white shadow-sm">
          <div class="border-b border-slate-200 p-4">
            <h2 #tituloEditorElemento tabindex="-1" class="text-xl font-semibold">{{ cadastroConfirmado() ? 'Veículo cadastrado' : (editandoId() ? 'Editar' : 'Cadastrar') + ' — ' + tituloEditor() }}</h2>
          </div>

          @if (aba() === 'grupos') {
            <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarGrupo()" novalidate>
              <div class="grid gap-1">
                <label for="grupo-nome">Nome do grupo</label>
                <input id="grupo-nome" [formField]="grupoForm.nome"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" aria-describedby="grupo-nome-erro">
                <small id="grupo-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </div>
              <div class="flex gap-2">
                <button type="button" (click)="cancelar()">Cancelar</button>
                <button type="submit" [disabled]="salvando()">Salvar</button>
              </div>
            </form>
          }

          @if (aba() === 'planos') {
            <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarPlano()" novalidate>
              <div class="grid gap-4 sm:grid-cols-2">
                <div class="grid gap-1">
                  <label for="plano-grupo">Grupo</label>
                  @if (editandoId()) {
                    <p id="plano-grupo" class="rounded-md border border-slate-300 bg-slate-100 px-3 py-2">
                      {{ nomeGrupo(planoModelo().grupoId) }}
                    </p>
                    <small class="text-slate-600">O grupo não muda depois do cadastro.</small>
                  } @else {
                    <select id="plano-grupo" [formField]="planoForm.grupoId"
                      [attr.aria-invalid]="erros()['grupoId'] ? 'true' : null" aria-describedby="plano-grupo-erro">
                      <option value="">Selecione</option>
                      @for (grupo of gruposOpcoes(); track grupo.id) {
                        <option [value]="grupo.id">{{ grupo.nome }}</option>
                      }
                    </select>
                    <small id="plano-grupo-erro" class="text-red-800">{{ erros()['grupoId'] }}</small>
                  }
                </div>
                <div class="grid gap-1">
                  <label for="plano-modalidade">Modalidade</label>
                  @if (editandoId()) {
                    <p id="plano-modalidade" class="rounded-md border border-slate-300 bg-slate-100 px-3 py-2">
                      {{ rotuloModalidade(planoModelo().modalidade) }}
                    </p>
                    <small class="text-slate-600">A modalidade não muda depois do cadastro.</small>
                  } @else {
                    <select id="plano-modalidade" [formField]="planoForm.modalidade"
                      [attr.aria-invalid]="erros()['modalidade'] ? 'true' : null" aria-describedby="plano-modalidade-erro">
                      <option value="Diario">Diário</option>
                      <option value="Controlado">Controlado</option>
                      <option value="Livre">Livre</option>
                    </select>
                    <small id="plano-modalidade-erro" class="text-red-800">{{ erros()['modalidade'] }}</small>
                  }
                </div>
                <div class="grid gap-1">
                  <label for="plano-diario">Preço diário (R$)</label>
                  <input id="plano-diario" inputmode="decimal" [formField]="planoForm.precoDiario"
                    [attr.aria-invalid]="erros()['precoDiario'] ? 'true' : null" aria-describedby="plano-diario-erro">
                  <small id="plano-diario-erro" class="text-red-800">{{ erros()['precoDiario'] }}</small>
                </div>
                @if (planoModelo().modalidade !== 'Livre') {
                  <div class="grid gap-1">
                    <label for="plano-km">{{ planoModelo().modalidade === 'Controlado'
                      ? 'Preço por quilômetro excedente (R$)' : 'Preço por quilômetro (R$)' }}</label>
                    <input id="plano-km" inputmode="decimal" [formField]="planoForm.precoKm"
                      [attr.aria-invalid]="erros()['precoKm'] ? 'true' : null" aria-describedby="plano-km-erro">
                    <small id="plano-km-erro" class="text-red-800">{{ erros()['precoKm'] }}</small>
                  </div>
                }
                @if (planoModelo().modalidade === 'Controlado') {
                  <div class="grid gap-1">
                    <label for="plano-franquia">Franquia diária (km)</label>
                    <input id="plano-franquia" inputmode="numeric" [formField]="planoForm.franquiaDiariaKm"
                      [attr.aria-invalid]="erros()['franquiaDiariaKm'] ? 'true' : null"
                      aria-describedby="plano-franquia-erro">
                    <small id="plano-franquia-erro" class="text-red-800">{{ erros()['franquiaDiariaKm'] }}</small>
                  </div>
                }
              </div>
              <p class="text-slate-600">Novos preços valem para novas contratações. Valores já contratados são preservados.</p>
              <div class="flex gap-2">
                <button type="button" (click)="cancelar()">Cancelar</button>
                <button type="submit" [disabled]="salvando()">Salvar</button>
              </div>
            </form>
          }

          @if (aba() === 'veiculos') {
            <div class="grid items-start gap-4 p-4 min-[840px]:grid-cols-[minmax(0,1.4fr)_minmax(17rem,1fr)]">
            <form class="m-0 grid min-w-0 gap-4 rounded-xl border border-slate-300 p-4" (submit)="$event.preventDefault(); salvarVeiculo()" novalidate>
              <h3 class="font-semibold">Dados do veículo</h3>
              <fieldset class="grid min-w-0 gap-4 sm:grid-cols-2" [disabled]="salvando() || cadastroConfirmado() || operacaoFoto() !== ''">
                <div class="grid gap-1">
                  <label for="veiculo-grupo">Grupo</label>
                  <select id="veiculo-grupo" [formField]="veiculoForm.grupoId"
                    [attr.aria-invalid]="erros()['grupoId'] ? 'true' : null" aria-describedby="veiculo-grupo-erro">
                    <option value="">Selecione</option>
                    @for (grupo of gruposOpcoes(); track grupo.id) {
                      <option [value]="grupo.id">{{ grupo.nome }}</option>
                    }
                  </select>
                  <small id="veiculo-grupo-erro" class="text-red-800">{{ erros()['grupoId'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-placa">Placa</label>
                  <input id="veiculo-placa" [formField]="veiculoForm.placa"
                    [attr.aria-invalid]="erros()['placa'] ? 'true' : null" aria-describedby="veiculo-placa-erro">
                  <small id="veiculo-placa-erro" class="text-red-800">{{ erros()['placa'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-marca">Marca</label>
                  <input id="veiculo-marca" [formField]="veiculoForm.marca"
                    [attr.aria-invalid]="erros()['marca'] ? 'true' : null" aria-describedby="veiculo-marca-erro">
                  <small id="veiculo-marca-erro" class="text-red-800">{{ erros()['marca'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-modelo">Modelo</label>
                  <input id="veiculo-modelo" [formField]="veiculoForm.modelo"
                    [attr.aria-invalid]="erros()['modelo'] ? 'true' : null" aria-describedby="veiculo-modelo-erro">
                  <small id="veiculo-modelo-erro" class="text-red-800">{{ erros()['modelo'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-cor">Cor</label>
                  <input id="veiculo-cor" [formField]="veiculoForm.cor"
                    [attr.aria-invalid]="erros()['cor'] ? 'true' : null" aria-describedby="veiculo-cor-erro">
                  <small id="veiculo-cor-erro" class="text-red-800">{{ erros()['cor'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-combustivel">Combustível</label>
                  <select id="veiculo-combustivel" [formField]="veiculoForm.combustivel"
                    [attr.aria-invalid]="erros()['combustivel'] ? 'true' : null" aria-describedby="veiculo-combustivel-erro">
                    <option value="Gasolina">Gasolina</option>
                    <option value="Gas">Gás</option>
                    <option value="Diesel">Diesel</option>
                    <option value="Alcool">Álcool</option>
                  </select>
                  <small id="veiculo-combustivel-erro" class="text-red-800">{{ erros()['combustivel'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-capacidade">Capacidade (litros)</label>
                  <input id="veiculo-capacidade" inputmode="decimal" [formField]="veiculoForm.capacidadeLitros"
                    [attr.aria-invalid]="erros()['capacidadeLitros'] ? 'true' : null"
                    aria-describedby="veiculo-capacidade-erro">
                  <small id="veiculo-capacidade-erro" class="text-red-800">{{ erros()['capacidadeLitros'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-ano">Ano</label>
                  <input id="veiculo-ano" inputmode="numeric" [formField]="veiculoForm.ano"
                    [attr.aria-invalid]="erros()['ano'] ? 'true' : null" aria-describedby="veiculo-ano-erro">
                  <small id="veiculo-ano-erro" class="text-red-800">{{ erros()['ano'] }}</small>
                </div>
              </fieldset>
              <div class="flex gap-2">
                <button type="button" class="border border-slate-500 bg-white text-slate-900" (click)="voltarVeiculos()"
                  [disabled]="bloquearNavegacaoFoto()">{{ cadastroConfirmado() ? 'Voltar aos veículos' : 'Cancelar' }}</button>
                @if (!cadastroConfirmado()) {
                  <button type="submit" [disabled]="salvando() || operacaoFoto() !== ''">{{ salvando() ? 'Salvando veículo…' : 'Salvar' }}</button>
                }
              </div>
            </form>
            <section aria-labelledby="titulo-foto" class="grid min-w-0 gap-3 rounded-xl border border-slate-300 p-4 shadow-sm">
              <h3 id="titulo-foto" #tituloFoto tabindex="-1" class="font-semibold">Foto do veículo <span class="text-sm text-slate-600">· Opcional</span></h3>
              @if (editandoId() || cadastroConfirmado()) {
                <div class="grid min-h-36 place-items-center rounded-lg border border-slate-300 bg-slate-50 p-2 text-center">
                  @if (fotoUrl(); as url) {
                    <img [src]="url" [alt]="descricaoFoto()" class="max-h-60 w-full object-contain">
                  } @else {
                    <p role="status">{{ estadoFoto() === 'carregando' ? 'Carregando foto…' :
                      estadoFoto() === 'indisponivel' ? 'Foto temporariamente indisponível.' : 'Veículo sem foto.' }}</p>
                  }
                </div>
              } @else {
                <p class="m-0 text-slate-600">Se você selecionar uma foto, ela será enviada depois que o veículo for cadastrado.</p>
              }
              <p class="m-0" role="status">{{ operacaoFoto() || avisoFoto() }}</p>
              @if (erroFoto()) {
                <p id="erro-foto" #resumoFoto tabindex="-1" role="alert" class="m-0 rounded-lg border border-red-300 bg-red-50 p-3">{{ erroFoto() }}</p>
              }
              <p id="ajuda-foto" class="m-0 text-sm text-slate-600">JPEG ou PNG estático, até 16 megapixels. O envio completo, com o arquivo, não pode passar de 5 MiB.</p>
              <label class="relative w-fit max-w-full rounded-md border border-slate-500 bg-white px-3 py-2
                has-focus-visible:outline-3 has-focus-visible:outline-blue-700 has-focus-visible:outline-offset-3">
                Selecionar arquivo
                <input type="file" #entradaFoto class="sr-only" accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                  [disabled]="salvando() || operacaoFoto() !== ''" (change)="selecionarFoto($event)"
                  [attr.aria-invalid]="erroArquivoFoto() ? 'true' : null"
                  [attr.aria-describedby]="erroArquivoFoto() ? 'ajuda-foto erro-foto' : 'ajuda-foto'">
              </label>
              <p class="m-0 text-sm text-slate-600" role="status">{{ arquivoFoto() ? 'Um arquivo selecionado.' : 'Nenhum arquivo selecionado.' }}</p>
              @if (editandoId()) {
                <div class="flex flex-wrap gap-2">
                  <button type="button" [disabled]="!arquivoFoto() || operacaoFoto() !== '' || salvando() || !etag() || versaoFotoVencida() || estadoFoto() === 'carregando'"
                    (click)="enviarFoto()">{{ cadastroConfirmado() ? 'Tentar enviar a foto novamente' : (fotoConhecida() ? 'Substituir foto' : 'Enviar foto') }}</button>
                  @if (fotoConhecida() && !cadastroConfirmado()) {
                    <button type="button" class="border border-slate-500 bg-white text-slate-900"
                      [disabled]="operacaoFoto() !== '' || salvando() || !etag() || versaoFotoVencida()"
                      (click)="removerFoto()">Remover foto</button>
                  }
                </div>
              }
            </section>
            </div>
          }
        </section>
      }
    </main>
  `,
})
export class FrotaComponent {
  private consultaAtual = 0;
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');
  private readonly sessao = inject(SessaoService);
  private readonly resumoFoto = viewChild<ElementRef<HTMLElement>>('resumoFoto');
  private readonly tituloFoto = viewChild<ElementRef<HTMLElement>>('tituloFoto');
  private readonly avisoElemento = viewChild<ElementRef<HTMLElement>>('avisoElemento');
  private readonly tituloEditorElemento = viewChild<ElementRef<HTMLElement>>('tituloEditorElemento');
  private readonly entradaFoto = viewChild<ElementRef<HTMLInputElement>>('entradaFoto');
  private geracaoFoto = 0;
  private leituraFoto = 0;

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'grupos', rotulo: 'Grupos' },
    { valor: 'planos', rotulo: 'Planos' },
    { valor: 'veiculos', rotulo: 'Veículos' },
  ];
  readonly aba = signal<Aba>('grupos');
  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly erroConsulta = signal('');
  readonly aviso = signal('');
  readonly erros = signal<Record<string, string>>({});
  readonly pagina = signal(1);
  readonly total = signal(0);
  readonly editando = signal(false);
  readonly editandoId = signal<string | null>(null);
  readonly etag = signal('');
  readonly grupos = signal<Grupo[]>([]);
  readonly planos = signal<Plano[]>([]);
  readonly veiculos = signal<Veiculo[]>([]);
  readonly gruposOpcoes = signal<Grupo[]>([]);
  readonly filtroGrupo = signal('');
  readonly agrupar = signal(false);
  readonly arquivoFoto = signal<File | null>(null);
  readonly fotoUrl = signal('');
  readonly estadoFoto = signal<'ausente' | 'carregando' | 'ativa' | 'indisponivel'>('ausente');
  readonly fotoConhecida = signal(false);
  readonly avisoFoto = signal('');
  readonly erroFoto = signal('');
  readonly erroArquivoFoto = signal(false);
  readonly versaoFotoVencida = signal(false);
  readonly operacaoFoto = signal('');
  readonly cadastroConfirmado = signal(false);
  readonly descricaoFoto = signal('Foto do veículo');
  readonly bloquearNavegacaoFoto = computed(() => this.aba() === 'veiculos' && (this.salvando() || this.operacaoFoto() !== ''));

  readonly temErros = computed(() => Object.keys(this.erros()).length > 0);
  readonly mensagensErro = computed(() => Object.values(this.erros()));
  readonly tituloLista = computed(() => this.abas.find(x => x.valor === this.aba())?.rotulo ?? '');
  readonly tituloEditor = computed(() => this.aba() === 'grupos' ? 'grupo'
    : this.aba() === 'planos' ? 'plano' : 'veículo');
  readonly veiculosAgrupados = computed(() => {
    const blocos: { grupoId: string; grupoNome: string; itens: Veiculo[] }[] = [];
    for (const veiculo of this.veiculos()) {
      let bloco = blocos.find(x => x.grupoId === veiculo.grupoId);
      if (!bloco) {
        bloco = { grupoId: veiculo.grupoId, grupoNome: veiculo.grupoNome, itens: [] };
        blocos.push(bloco);
      }
      bloco.itens.push(veiculo);
    }
    return blocos;
  });

  readonly grupoModelo = signal<GrupoModelo>({ nome: '' });
  readonly grupoForm = form(this.grupoModelo, caminho => {
    required(caminho.nome, { message: 'Informe o nome.' });
  });

  readonly planoModelo = signal<PlanoModelo>({
    grupoId: '', modalidade: 'Diario', precoDiario: '', precoKm: '', franquiaDiariaKm: '',
  });
  readonly planoForm = form(this.planoModelo, caminho => {
    required(caminho.grupoId, { message: 'Selecione o grupo.' });
    required(caminho.precoDiario, { message: 'Informe o preço diário.' });
  });

  readonly veiculoModelo = signal<VeiculoModelo>({
    grupoId: '', placa: '', marca: '', modelo: '', cor: '', ano: '',
    combustivel: 'Gasolina', capacidadeLitros: '',
  });
  readonly veiculoForm = form(this.veiculoModelo, caminho => {
    required(caminho.grupoId, { message: 'Selecione o grupo.' });
    required(caminho.placa, { message: 'Informe a placa.' });
    required(caminho.marca, { message: 'Informe a marca.' });
    required(caminho.modelo, { message: 'Informe o modelo.' });
    required(caminho.cor, { message: 'Informe a cor.' });
    required(caminho.ano, { message: 'Informe o ano.' });
    required(caminho.capacidadeLitros, { message: 'Informe a capacidade.' });
  });

  constructor() {
    effect(() => {
      if (!this.sessao.autenticada()) this.limparFoto();
    });
    inject(DestroyRef).onDestroy(() => this.limparFoto());
    void this.carregar();
  }

  trocarAba(aba: Aba) {
    if (this.bloquearNavegacaoFoto()) return;
    this.aba.set(aba);
    this.cancelar();
    this.pagina.set(1);
    this.filtroGrupo.set('');
    this.aviso.set('');
    void this.carregar();
  }

  async carregar() {
    const consulta = ++this.consultaAtual;
    this.carregando.set(true);
    this.erroConsulta.set('');
    try {
      if (this.aba() === 'grupos') {
        const pagina = await this.servico.listar<Grupo>('/grupos', this.pagina());
        if (consulta !== this.consultaAtual) return;
        this.grupos.set(pagina.items);
        this.total.set(pagina.total);
      } else if (this.aba() === 'planos') {
        const pagina = await this.servico.listar<Plano>('/planos', this.pagina());
        if (consulta !== this.consultaAtual) return;
        this.planos.set(pagina.items);
        this.total.set(pagina.total);
      } else {
        const pagina = await this.servico.listar<Veiculo>('/veiculos', this.pagina(), 20,
          { grupoId: this.filtroGrupo() || undefined });
        if (consulta !== this.consultaAtual) return;
        this.veiculos.set(pagina.items);
        this.total.set(pagina.total);
      }
      await this.carregarGruposOpcoes();
    } catch (erro: unknown) {
      if (consulta !== this.consultaAtual) return;
      this.erroConsulta.set('Não foi possível consultar os cadastros. Verifique a conexão e tente novamente.');
    } finally {
      if (consulta === this.consultaAtual) this.carregando.set(false);
    }
  }

  private async carregarGruposOpcoes() {
    this.gruposOpcoes.set(await this.servico.listarOpcoes<Grupo>('/grupos'));
  }

  paginar(delta: number) {
    this.pagina.update(x => Math.max(1, x + delta));
    void this.carregar();
  }

  filtrarGrupo(evento: Event) {
    this.filtroGrupo.set((evento.target as HTMLSelectElement).value);
    this.pagina.set(1);
    void this.carregar();
  }

  alternarAgrupamento(evento: Event) {
    this.agrupar.set((evento.target as HTMLInputElement).checked);
  }

  novo() {
    this.limparFoto();
    this.editando.set(true);
    this.editandoId.set(null);
    this.etag.set('');
    this.erros.set({});
    this.aviso.set('');
    if (this.aba() === 'grupos') this.grupoModelo.set({ nome: '' });
    if (this.aba() === 'planos') this.planoModelo.set({
      grupoId: '', modalidade: 'Diario', precoDiario: '', precoKm: '', franquiaDiariaKm: '',
    });
    if (this.aba() === 'veiculos') this.veiculoModelo.set({
      grupoId: this.filtroGrupo(), placa: '', marca: '', modelo: '', cor: '', ano: '',
      combustivel: 'Gasolina', capacidadeLitros: '',
    });
    if (this.aba() === 'veiculos') this.focarEditorVeiculo();
  }

  async editarGrupo(id: string) {
    try {
      const resposta = await this.servico.obter<Grupo>(`/grupos/${id}`);
      this.grupoModelo.set({ nome: resposta.dados.nome });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.editando.set(true);
    } catch (erro: unknown) { this.falhar(erro); }
  }

  async editarPlano(id: string) {
    try {
      const resposta = await this.servico.obter<Plano>(`/planos/${id}`);
      this.planoModelo.set({
        grupoId: resposta.dados.grupoId,
        modalidade: resposta.dados.modalidade,
        precoDiario: resposta.dados.precoDiario,
        precoKm: resposta.dados.precoKm ?? '',
        franquiaDiariaKm: resposta.dados.franquiaDiariaKm?.toString() ?? '',
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.editando.set(true);
    } catch (erro: unknown) { this.falhar(erro); }
  }

  async editarVeiculo(id: string) {
    this.limparFoto();
    const operacao = this.geracaoFoto;
    try {
      const resposta = await this.servico.obter<Veiculo>(`/veiculos/${id}?paraEdicao=true`);
      if (operacao !== this.geracaoFoto) return;
      this.veiculoModelo.set({
        grupoId: resposta.dados.grupoId,
        placa: resposta.dados.placa,
        marca: resposta.dados.marca,
        modelo: resposta.dados.modelo,
        cor: resposta.dados.cor,
        ano: resposta.dados.ano.toString(),
        combustivel: resposta.dados.combustivel,
        capacidadeLitros: resposta.dados.capacidadeLitros,
      });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.aviso.set('');
      this.editando.set(true);
      this.descricaoFoto.set(`Foto do veículo ${resposta.dados.placa}`);
      this.focarEditorVeiculo();
      void this.carregarFoto(id);
    } catch (erro: unknown) { if (operacao === this.geracaoFoto) this.falhar(erro); }
  }

  cancelar() {
    this.limparFoto();
    this.editando.set(false);
    this.editandoId.set(null);
    this.erros.set({});
  }

  async salvarGrupo() {
    if (this.salvando()) return;
    const nome = this.grupoModelo().nome.trim();
    if (!nome) { this.falharCampos({ nome: 'Informe o nome do grupo.' }); return; }
    if (nome.length > 200) { this.falharCampos({ nome: 'Use no máximo 200 caracteres.' }); return; }
    await this.executarSalvar(async () => {
      if (this.editandoId()) {
        await this.servico.editar(`/grupos/${this.editandoId()}`, { nome }, this.etag());
      } else {
        await this.servico.criar('/grupos', { nome });
      }
    });
  }

  async salvarPlano() {
    if (this.salvando()) return;
    const modelo = this.planoModelo();
    const erros: Record<string, string> = {};
    if (!modelo.grupoId) erros['grupoId'] = 'Selecione o grupo.';
    const precoDiario = decimalDoFormulario(modelo.precoDiario);
    if (!precoDiario) erros['precoDiario'] = 'Informe o preço diário.';
    else if (!/^\d{1,13}(\.\d{1,6})?$/.test(precoDiario)) erros['precoDiario'] = 'Informe um valor não negativo com até 6 casas.';
    const precoKm = decimalDoFormulario(modelo.precoKm);
    if (modelo.modalidade !== 'Livre') {
      if (!precoKm) erros['precoKm'] = 'Informe o preço por quilômetro.';
      else if (!/^\d{1,13}(\.\d{1,6})?$/.test(precoKm)) erros['precoKm'] = 'Informe um valor não negativo com até 6 casas.';
    }
    let franquia: number | null = null;
    if (modelo.modalidade === 'Controlado') {
      if (!/^\d+$/.test(modelo.franquiaDiariaKm)) erros['franquiaDiariaKm'] = 'Informe a franquia inteira não negativa.';
      else if (Number(modelo.franquiaDiariaKm) > 2147483647)
        erros['franquiaDiariaKm'] = 'Informe um inteiro até 2147483647.';
      else franquia = Number(modelo.franquiaDiariaKm);
    }
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    await this.executarSalvar(async () => {
      const parametros = {
        precoDiario,
        precoKm: modelo.modalidade === 'Livre' ? null : precoKm,
        franquiaDiariaKm: franquia,
      };
      if (this.editandoId()) {
        await this.servico.editar(`/planos/${this.editandoId()}`, parametros, this.etag());
      } else {
        await this.servico.criar('/planos', {
          grupoId: modelo.grupoId, modalidade: modelo.modalidade, ...parametros,
        });
      }
    });
  }

  async salvarVeiculo() {
    if (this.salvando() || this.operacaoFoto() || this.cadastroConfirmado()) return;
    const modelo = this.veiculoModelo();
    const erros = validarLimites({ ...modelo, placa: modelo.placa.replace(/[^\p{L}\p{Nd}]/gu, '') },
      { placa: 16, marca: 200, modelo: 200, cor: 100 });
    if (!modelo.grupoId) erros['grupoId'] = 'Selecione o grupo.';
    if (!modelo.placa.trim()) erros['placa'] = 'Informe a placa.';
    if (!modelo.marca.trim()) erros['marca'] = 'Informe a marca.';
    if (!modelo.modelo.trim()) erros['modelo'] = 'Informe o modelo.';
    if (!modelo.cor.trim()) erros['cor'] = 'Informe a cor.';
    if (!/^\d+$/.test(modelo.ano)) erros['ano'] = 'Informe o ano como inteiro não negativo.';
    else if (Number(modelo.ano) > 2147483647) erros['ano'] = 'Informe um inteiro até 2147483647.';
    const capacidade = decimalDoFormulario(modelo.capacidadeLitros);
    if (!capacidade) erros['capacidadeLitros'] = 'Informe a capacidade.';
    else if (!/^\d{1,9}(\.\d{1,3})?$/.test(capacidade) || Number(capacidade) <= 0)
      erros['capacidadeLitros'] = 'Informe capacidade positiva com até 3 casas.';
    if (Object.keys(erros).length > 0) { this.falharCampos(erros); return; }
    const corpo = {
        grupoId: modelo.grupoId,
        placa: modelo.placa,
        marca: modelo.marca,
        modelo: modelo.modelo,
        cor: modelo.cor,
        ano: Number(modelo.ano),
        combustivel: modelo.combustivel,
        capacidadeLitros: capacidade,
    };
    const id = this.editandoId();
    if (id) {
      await this.executarSalvar(() => this.servico.editar(`/veiculos/${id}`, corpo, this.etag()));
      return;
    }
    const foto = this.arquivoFoto();
    const operacao = this.geracaoFoto;
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      const resposta = await this.servico.criar<Veiculo>('/veiculos', corpo);
      if (operacao !== this.geracaoFoto) return;
      if (foto && resposta.status !== 201) {
        this.falharCampos({ geral: 'Não foi possível confirmar o cadastro. A foto não foi enviada. Consulte a lista antes de tentar novamente.' });
        return;
      }
      if (!foto) {
        await this.encerrarVeiculo('Veículo cadastrado sem foto.');
        return;
      }
      this.cadastroConfirmado.set(true);
      this.editandoId.set(resposta.dados?.id || null);
      this.etag.set(resposta.etag);
      this.descricaoFoto.set(`Foto do veículo ${resposta.dados?.placa || modelo.placa}`);
      this.aviso.set('Veículo cadastrado.');
      if (!this.editandoId() || !this.etag()) {
        this.falharFoto('O veículo foi cadastrado, mas faltam dados para enviar a foto. Consulte o mesmo veículo na lista antes de tentar novamente.');
        return;
      }
      await this.mutarFoto(false, foto);
    } catch (erro: unknown) {
      if (operacao === this.geracaoFoto) {
        const errosCadastro = CadastrosService.errosDe(erro);
        if (foto) errosCadastro['foto'] = 'A foto não foi enviada. Consulte a lista se o cadastro não puder ser confirmado.';
        this.falharCampos(errosCadastro);
      }
    } finally { this.salvando.set(false); }
  }

  selecionarFoto(evento: Event) {
    this.arquivoFoto.set((evento.target as HTMLInputElement).files?.[0] ?? null);
    this.erroFoto.set('');
    this.erroArquivoFoto.set(false);
    this.avisoFoto.set('');
  }

  private async carregarFoto(id: string) {
    const geracao = this.geracaoFoto;
    const leitura = ++this.leituraFoto;
    const atual = () => geracao === this.geracaoFoto && leitura === this.leituraFoto && this.sessao.autenticada();
    this.estadoFoto.set('carregando');
    try {
      const blob = await this.servico.lerFoto(id);
      if (!atual()) return;
      this.trocarUrlFoto(URL.createObjectURL(blob));
      this.fotoConhecida.set(true);
      this.estadoFoto.set('ativa');
    } catch (erro) {
      if (!atual()) return;
      if (erro instanceof HttpErrorResponse && erro.status === 404) {
        this.trocarUrlFoto('');
        this.fotoConhecida.set(false);
        this.estadoFoto.set('ausente');
      } else {
        if (!this.fotoUrl()) this.estadoFoto.set('indisponivel');
        if (erro instanceof HttpErrorResponse && erro.status === 503 && erro.error instanceof Blob) {
          try {
            const problema = JSON.parse(await erro.error.text());
            if (atual() && problema.code === 'foto_indisponivel') this.fotoConhecida.set(true);
          } catch { /* Indisponibilidade não é ausência de foto. */ }
        }
      }
    }
  }

  enviarFoto() { return this.mutarFoto(false, this.arquivoFoto()); }
  removerFoto() { return this.mutarFoto(true, null); }

  private async mutarFoto(remover: boolean, arquivo: File | null) {
    const id = this.editandoId(), versao = this.etag();
    const geracao = this.geracaoFoto;
    if (!id || !versao || this.versaoFotoVencida() || this.operacaoFoto() || (!remover && !arquivo)) return;
    this.erroFoto.set('');
    this.erroArquivoFoto.set(false);
    this.avisoFoto.set('');
    const parcial = this.cadastroConfirmado() ? 'O veículo foi cadastrado. ' : '';
    if (arquivo && (!arquivo.size || arquivo.size >= 5 * 1024 * 1024)) {
      this.falharFoto(`${parcial}Selecione um JPEG ou PNG estático dentro dos limites informados.`, true);
      return;
    }
    this.operacaoFoto.set(remover ? 'Removendo foto…' : 'Enviando foto…');
    try {
      const resposta = remover ? await this.servico.removerFoto(id, versao) : await this.servico.enviarFoto(id, versao, arquivo!);
      if (geracao !== this.geracaoFoto) return;
      this.etag.set(resposta.etag);
      this.arquivoFoto.set(null);
      const entrada = this.entradaFoto()?.nativeElement;
      if (entrada) entrada.value = '';
      if (this.cadastroConfirmado()) {
        await this.encerrarVeiculo('Veículo cadastrado e foto salva.');
      } else {
        const substituiu = this.fotoConhecida();
        this.leituraFoto++;
        this.trocarUrlFoto('');
        this.fotoConhecida.set(!remover);
        this.estadoFoto.set('ausente');
        this.avisoFoto.set(remover ? 'Foto removida.' : substituiu ? 'Foto substituída.' : 'Foto adicionada.');
        afterNextRender(() => this.tituloFoto()?.nativeElement.focus(), { injector: this.injector });
        if (!remover) void this.carregarFoto(id);
      }
    } catch (erro) {
      if (geracao !== this.geracaoFoto) return;
      const http = erro instanceof HttpErrorResponse ? erro : null;
      if (http?.status === 412 || http?.status === 428) {
        this.versaoFotoVencida.set(true);
        this.falharFoto(`${parcial}O veículo mudou. Consulte o cadastro na lista antes de tentar novamente.`);
      } else if (!http || http.status === 0 || (http.status >= 500 &&
          !['foto_nao_gravada', 'foto_processamento_indisponivel'].includes(http.error?.code))) {
        this.falharFoto(`${parcial}Não foi possível confirmar ${remover ? 'a remoção' : 'o envio'} da foto. Confira a foto atual antes de tentar novamente.`);
        await this.carregarFoto(id);
      } else {
        const mensagens = CadastrosService.errosDe(erro);
        const campo = mensagens['arquivo'] || (http.status === 413 ? 'O envio completo da foto deve ter até 5 MiB.' : '');
        const mensagem = campo || http.error?.detail || Object.values(mensagens)[0];
        this.falharFoto(`${parcial}${mensagem}${parcial && !remover ? ' Tente novamente apenas o envio da foto.' : ''}`, !!campo);
      }
    } finally { if (geracao === this.geracaoFoto) this.operacaoFoto.set(''); }
  }

  voltarVeiculos() {
    if (this.bloquearNavegacaoFoto()) return;
    if (this.cadastroConfirmado()) void this.encerrarVeiculo('Veículo cadastrado.');
    else this.cancelar();
  }

  private async encerrarVeiculo(mensagem: string) {
    this.cancelar();
    this.aviso.set(mensagem);
    afterNextRender(() => this.avisoElemento()?.nativeElement.focus(), { injector: this.injector });
    await this.carregar();
  }

  private falharFoto(mensagem: string, arquivo = false) {
    this.erroFoto.set(mensagem);
    this.erroArquivoFoto.set(arquivo);
    afterNextRender(() => this.resumoFoto()?.nativeElement.focus(), { injector: this.injector });
  }

  private focarEditorVeiculo() {
    afterNextRender(() => this.tituloEditorElemento()?.nativeElement.focus(), { injector: this.injector });
  }

  private trocarUrlFoto(url: string) {
    const anterior = this.fotoUrl();
    if (anterior) URL.revokeObjectURL(anterior);
    this.fotoUrl.set(url);
  }

  private limparFoto() {
    this.geracaoFoto++;
    this.leituraFoto++;
    this.trocarUrlFoto('');
    this.arquivoFoto.set(null);
    this.estadoFoto.set('ausente');
    this.fotoConhecida.set(false);
    this.avisoFoto.set('');
    this.erroFoto.set('');
    this.erroArquivoFoto.set(false);
    this.versaoFotoVencida.set(false);
    this.operacaoFoto.set('');
    this.cadastroConfirmado.set(false);
  }

  async excluirGrupo(grupo: Grupo) {
    await this.executarExclusao(`/grupos/${grupo.id}`);
  }

  async excluirPlano(plano: Plano) {
    await this.executarExclusao(`/planos/${plano.id}`);
  }

  async excluirVeiculo(veiculo: Veiculo) {
    await this.executarExclusao(`/veiculos/${veiculo.id}`);
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
      this.limparFoto();
      this.aviso.set('Cadastro salvo.');
      this.editando.set(false);
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

  resumoPlanos(grupo: Grupo): string {
    if (grupo.planos.length === 0) return 'Sem planos';
    return grupo.planos.map(plano => this.parametrosPlano(plano)).join('; ');
  }

  parametrosPlano(plano: Plano): string {
    const diaria = `${formatarMoeda(plano.precoDiario)}/dia`;
    if (plano.modalidade === 'Diario') return `${this.rotuloModalidade(plano.modalidade)}: ${diaria} · ${formatarMoeda(plano.precoKm)}/km`;
    if (plano.modalidade === 'Controlado') {
      return `${this.rotuloModalidade(plano.modalidade)}: ${diaria} · ${plano.franquiaDiariaKm} km/dia · ${formatarMoeda(plano.precoKm)}/km excedente`;
    }
    return `${this.rotuloModalidade(plano.modalidade)}: ${diaria}`;
  }

  nomeGrupo(grupoId: string): string {
    return this.gruposOpcoes().find(grupo => grupo.id === grupoId)?.nome ?? '—';
  }

  rotuloModalidade(modalidade: Modalidade): string {
    return modalidade === 'Diario' ? 'Diário' : modalidade === 'Controlado' ? 'Controlado' : 'Livre';
  }

  rotuloCombustivel(combustivel: Combustivel): string {
    return combustivel === 'Gas' ? 'Gás' : combustivel === 'Alcool' ? 'Álcool' : combustivel;
  }
}
