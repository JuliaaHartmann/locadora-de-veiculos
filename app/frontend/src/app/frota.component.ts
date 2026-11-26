import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, effect, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { NgTemplateOutlet } from '@angular/common';
import { FormField, form, required } from '@angular/forms/signals';
import { LucideCheck, LucidePencil, LucideTrash2, LucideX } from '@lucide/angular';
import { CadastrosService, decimalDoFormulario, formatarMoeda, validarLimites } from './cadastros.service';
import { FotoMiniaturaComponent, LeiturasMiniaturasFrota } from './foto-miniatura.component';
import { SessaoService } from './sessao.service';
import { SaidaCoordenador } from './saida-coordenador.service';
import { IconeModuloComponent } from './icone-modulo.component';

type Aba = 'grupos' | 'planos' | 'veiculos';
type Modalidade = 'Diario' | 'Controlado' | 'Livre';
type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';

interface Plano {
  id: string;
  numeroExibicao: string;
  grupoId: string;
  grupoNome: string;
  modalidade: Modalidade;
  precoDiario: string;
  precoKm: string | null;
  franquiaDiariaKm: number | null;
}

interface Grupo {
  id: string;
  numeroExibicao: string;
  nome: string;
  planos: Plano[];
}

interface Veiculo {
  id: string;
  numeroExibicao: string;
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

interface Exclusao { caminho: string; entidade: 'grupo' | 'plano' | 'veículo'; nome: string; }

@Component({
  standalone: true,
  imports: [FormField, NgTemplateOutlet, FotoMiniaturaComponent, IconeModuloComponent, LucideCheck, LucidePencil, LucideTrash2, LucideX],
  providers: [LeiturasMiniaturasFrota],
  styleUrls: ['./quadro-operacional.css', './frota.component.css'],
  template: `
    <main class="frota quadro-operacional">
      <p class="sobretitulo">Área administrativa</p>
      <h1>Frota</h1>
      <hr class="regua" aria-hidden="true">
      <p class="introducao">Prepare os grupos, as modalidades e os dados da frota.</p>

      <nav aria-label="Cadastros da frota" class="abas flex flex-wrap gap-2">
        @for (item of abas; track item.valor) {
          <button type="button" (click)="trocarAba(item.valor)" [disabled]="bloquearNavegacaoFoto()"
            [attr.aria-current]="aba() === item.valor ? 'page' : null">
            {{ item.rotulo }}
          </button>
        }
      </nav>

      <div class="saida-incerta" role="alert">@if (faseSaida() === 'incerta') {
        Não foi possível confirmar a revogação remota. Tente sair novamente ou entre de novo.
      }</div>
      @if (faseSaida() === 'incerta') {
        <button type="button" class="compacto mb-4" (click)="reautenticar()">Entrar novamente</button>
      }

      <div class="aviso" [class.visivel]="aviso()">
        @if (aviso()) { <svg lucideCheck aria-hidden="true" class="size-5 shrink-0"></svg> }
        <p #avisoElemento class="foco-transicao" role="status" tabindex="-1">{{ aviso() }}</p>
        @if (aviso()) {
          <button type="button" class="acao" aria-label="Dispensar mensagem" title="Dispensar mensagem" (click)="dispensarAviso()">
            <svg lucideX aria-hidden="true"></svg>
          </button>
        }
      </div>
      <div #resumo id="resumo-frota" tabindex="-1" role="alert" class="resumo"
        [hidden]="!temErros()">
        <strong>Revise os dados informados.</strong>
        <ul class="my-1 list-disc pl-5">
          @for (mensagem of mensagensErro(); track mensagem) { <li>{{ mensagem }}</li> }
        </ul>
      </div>

      @if (!editando()) {
        <section class="quadro">
          <div class="barra">
            <h2 class="text-xl font-semibold">{{ tituloLista() }} cadastrados</h2>
            <button #cadastrar type="button" class="primario" (click)="novo()" [disabled]="bloquearNavegacaoFoto()">Cadastrar</button>
          </div>

          @if (aba() === 'veiculos') {
            <div class="barra filtros">
              <label class="flex flex-wrap items-center gap-2" for="filtro-grupo">Filtrar por grupo
                <select id="filtro-grupo" [value]="filtroGrupo()" (change)="filtrarGrupo($event)" [disabled]="bloquearNavegacaoFoto()">
                  <option value="">Todos os grupos</option>
                  @for (grupo of gruposOpcoes(); track grupo.id) {
                    <option [value]="grupo.id">{{ grupo.nome }}</option>
                  }
                </select>
              </label>
              <label class="flex items-center gap-2">
                <input type="checkbox" [checked]="agrupar()" (change)="alternarAgrupamento($event)" [disabled]="bloquearNavegacaoFoto()">
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
              <div class="regiao-tabela" role="region" aria-label="Tabela de grupos" tabindex="0">
                <table class="tabela-acoes-duplas w-full min-w-[700px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Código</th><th scope="col">Nome</th>
                    <th scope="col">Planos e valores</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (grupo of grupos(); track grupo.id) {
                      <tr>
                        <td>{{ grupo.numeroExibicao }}</td>
                        <td>{{ grupo.nome }}</td>
                        <td>{{ resumoPlanos(grupo) }}</td>
                        <td><ng-container [ngTemplateOutlet]="acoes" [ngTemplateOutletContext]="{
                          nome: grupo.nome, editar: editarGrupo, id: grupo.id, entidade: 'grupo', caminho: '/grupos'
                        }" /></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (aba() === 'planos') {
              <div class="regiao-tabela" role="region" aria-label="Tabela de planos" tabindex="0">
                <table class="tabela-acoes-duplas w-full min-w-[700px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Código</th><th scope="col">Grupo</th><th scope="col">Modalidade</th>
                    <th scope="col">Parâmetros</th><th scope="col">Ações</th>
                  </tr></thead>
                  <tbody>
                    @for (plano of planos(); track plano.id) {
                      <tr>
                        <td>{{ plano.numeroExibicao }}</td>
                        <td>{{ plano.grupoNome }}</td>
                        <td class="whitespace-nowrap">{{ rotuloModalidade(plano.modalidade) }}</td>
                        <td>{{ parametrosPlano(plano) }}</td>
                        <td><ng-container [ngTemplateOutlet]="acoes" [ngTemplateOutletContext]="{
                          nome: identificarPlano(plano), editar: editarPlano, id: plano.id, entidade: 'plano', caminho: '/planos'
                        }" /></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (aba() === 'veiculos') {
              <div class="regiao-tabela"
                role="region" aria-label="Tabela de veículos" tabindex="0">
                <table class="tabela-acoes-duplas w-full min-w-[850px] border-collapse text-left">
                  <thead><tr>
                    <th scope="col">Foto</th><th scope="col">Código</th><th scope="col">Placa</th><th scope="col">Marca / modelo</th>
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
                            <td>{{ veiculo.numeroExibicao }}</td>
                            <td class="whitespace-nowrap">{{ veiculo.placa }}</td>
                            <td>{{ veiculo.marca }} / {{ veiculo.modelo }}</td>
                            <td>{{ veiculo.cor }}</td>
                            <td>{{ rotuloCombustivel(veiculo.combustivel) }}</td>
                            <td>{{ veiculo.grupoNome }}</td>
                            <td><ng-container [ngTemplateOutlet]="acoes" [ngTemplateOutletContext]="{
                              nome: veiculo.placa, editar: editarVeiculo, id: veiculo.id, entidade: 'veículo', caminho: '/veiculos'
                            }" /></td>
                          </tr>
                        }
                      }
                    } @else {
                      @for (veiculo of veiculos(); track veiculo.id) {
                        <tr>
                          <td><app-foto-miniatura [veiculoId]="veiculo.id" [placa]="veiculo.placa" /></td>
                          <td>{{ veiculo.numeroExibicao }}</td>
                          <td class="whitespace-nowrap">{{ veiculo.placa }}</td>
                          <td>{{ veiculo.marca }} / {{ veiculo.modelo }}</td>
                          <td>{{ veiculo.cor }}</td>
                          <td>{{ rotuloCombustivel(veiculo.combustivel) }}</td>
                          <td>{{ veiculo.grupoNome }}</td>
                          <td><ng-container [ngTemplateOutlet]="acoes" [ngTemplateOutletContext]="{
                            nome: veiculo.placa, editar: editarVeiculo, id: veiculo.id, entidade: 'veículo', caminho: '/veiculos'
                          }" /></td>
                        </tr>
                      }
                    }
                  </tbody>
                </table>
              </div>
            }

            @if (total() === 0) { <p class="p-6 text-center">Nenhum registro encontrado.</p> }

            <div class="barra paginacao">
              <span>Página {{ pagina() }} · {{ total() }} {{ total() === 1 ? 'registro' : 'registros' }} · até 20 por página</span>
              <div class="flex gap-2">
                <button type="button" (click)="paginar(-1)" [disabled]="bloquearNavegacaoFoto() || pagina() <= 1">Anterior</button>
                <button type="button" (click)="paginar(1)" [disabled]="bloquearNavegacaoFoto() || pagina() * 20 >= total()">Próxima</button>
              </div>
            </div>
          }
        </section>
      } @else {
        <section class="quadro editor" [attr.data-editor]="aba()">
          <div class="barra">
            <h2 #tituloEditorElemento tabindex="-1" class="foco-transicao text-xl font-semibold">{{ cadastroConfirmado() ? 'Veículo cadastrado' : (editandoId() ? 'Editar' : 'Cadastrar') + ' — ' + tituloEditor() }}</h2>
          </div>

          @if (aba() === 'grupos') {
            <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarGrupo()" novalidate>
              <fieldset class="grid gap-1" [disabled]="bloquearNavegacaoFoto()">
                <label for="grupo-nome">Nome do grupo</label>
                  <input id="grupo-nome" [formField]="grupoForm.nome" (input)="limparErroCampo('nome')"
                  [attr.aria-invalid]="erros()['nome'] ? 'true' : null" aria-describedby="grupo-nome-erro">
                <small id="grupo-nome-erro" class="text-red-800">{{ erros()['nome'] }}</small>
              </fieldset>
              <div class="acoes-editor">
                <button type="button" (click)="cancelar(true)" [disabled]="bloquearNavegacaoFoto()">Cancelar</button>
                <button type="submit" class="primario" [disabled]="bloquearNavegacaoFoto()">Salvar</button>
              </div>
            </form>
          }

          @if (aba() === 'planos') {
            <form class="grid gap-4 p-4" (submit)="$event.preventDefault(); salvarPlano()" novalidate>
              <fieldset class="campos" [disabled]="bloquearNavegacaoFoto()">
                <div class="grid gap-1">
                  <label for="plano-grupo">Grupo</label>
                  @if (editandoId()) {
                    <p id="plano-grupo" class="rounded-md border border-slate-300 bg-slate-100 px-3 py-2">
                      {{ nomeGrupo(planoModelo().grupoId) }}
                    </p>
                    <small class="text-slate-600">O grupo não muda depois do cadastro.</small>
                  } @else {
                    <select id="plano-grupo" [formField]="planoForm.grupoId" (change)="limparErroCampo('grupoId')"
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
                    <select id="plano-modalidade" [formField]="planoForm.modalidade" (change)="limparErroCampo('modalidade', 'precoKm', 'franquiaDiariaKm')"
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
                  <input id="plano-diario" inputmode="decimal" [formField]="planoForm.precoDiario" (input)="limparErroCampo('precoDiario')"
                    [attr.aria-invalid]="erros()['precoDiario'] ? 'true' : null" aria-describedby="plano-diario-erro">
                  <small id="plano-diario-erro" class="text-red-800">{{ erros()['precoDiario'] }}</small>
                </div>
                @if (planoModelo().modalidade !== 'Livre') {
                  <div class="grid gap-1">
                    <label for="plano-km">{{ planoModelo().modalidade === 'Controlado'
                      ? 'Preço por quilômetro excedente (R$)' : 'Preço por quilômetro (R$)' }}</label>
                    <input id="plano-km" inputmode="decimal" [formField]="planoForm.precoKm" (input)="limparErroCampo('precoKm')"
                      [attr.aria-invalid]="erros()['precoKm'] ? 'true' : null" aria-describedby="plano-km-erro">
                    <small id="plano-km-erro" class="text-red-800">{{ erros()['precoKm'] }}</small>
                  </div>
                }
                @if (planoModelo().modalidade === 'Controlado') {
                  <div class="grid gap-1">
                    <label for="plano-franquia">Franquia diária (km)</label>
                    <input id="plano-franquia" inputmode="numeric" [formField]="planoForm.franquiaDiariaKm" (input)="limparErroCampo('franquiaDiariaKm')"
                      [attr.aria-invalid]="erros()['franquiaDiariaKm'] ? 'true' : null"
                      aria-describedby="plano-franquia-erro">
                    <small id="plano-franquia-erro" class="text-red-800">{{ erros()['franquiaDiariaKm'] }}</small>
                  </div>
                }
              </fieldset>
              <p class="text-slate-600">Novos preços valem para novas contratações. Valores já contratados são preservados.</p>
              <div class="acoes-editor">
                <button type="button" (click)="cancelar(true)" [disabled]="bloquearNavegacaoFoto()">Cancelar</button>
                <button type="submit" class="primario" [disabled]="bloquearNavegacaoFoto()">Salvar</button>
              </div>
            </form>
          }

          @if (aba() === 'veiculos') {
            <form class="grid min-w-0 gap-4 p-4" (submit)="$event.preventDefault(); salvarVeiculo()" novalidate>
              <h3 class="font-semibold">Dados do veículo</h3>
              <fieldset class="campos" [disabled]="bloquearNavegacaoFoto() || cadastroConfirmado()">
                <div class="grid gap-1">
                  <label for="veiculo-grupo">Grupo</label>
                  <select id="veiculo-grupo" [formField]="veiculoForm.grupoId" (change)="limparErroCampo('grupoId')"
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
                  <input id="veiculo-placa" [formField]="veiculoForm.placa" (input)="limparErroCampo('placa')"
                    [attr.aria-invalid]="erros()['placa'] ? 'true' : null" aria-describedby="veiculo-placa-erro">
                  <small id="veiculo-placa-erro" class="text-red-800">{{ erros()['placa'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-marca">Marca</label>
                  <input id="veiculo-marca" [formField]="veiculoForm.marca" (input)="limparErroCampo('marca')"
                    [attr.aria-invalid]="erros()['marca'] ? 'true' : null" aria-describedby="veiculo-marca-erro">
                  <small id="veiculo-marca-erro" class="text-red-800">{{ erros()['marca'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-modelo">Modelo</label>
                  <input id="veiculo-modelo" [formField]="veiculoForm.modelo" (input)="limparErroCampo('modelo')"
                    [attr.aria-invalid]="erros()['modelo'] ? 'true' : null" aria-describedby="veiculo-modelo-erro">
                  <small id="veiculo-modelo-erro" class="text-red-800">{{ erros()['modelo'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-cor">Cor</label>
                  <input id="veiculo-cor" [formField]="veiculoForm.cor" (input)="limparErroCampo('cor')"
                    [attr.aria-invalid]="erros()['cor'] ? 'true' : null" aria-describedby="veiculo-cor-erro">
                  <small id="veiculo-cor-erro" class="text-red-800">{{ erros()['cor'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-combustivel">Combustível</label>
                  <select id="veiculo-combustivel" [formField]="veiculoForm.combustivel" (change)="limparErroCampo('combustivel')"
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
                  <input id="veiculo-capacidade" inputmode="decimal" [formField]="veiculoForm.capacidadeLitros" (input)="limparErroCampo('capacidadeLitros')"
                    [attr.aria-invalid]="erros()['capacidadeLitros'] ? 'true' : null"
                    aria-describedby="veiculo-capacidade-erro">
                  <small id="veiculo-capacidade-erro" class="text-red-800">{{ erros()['capacidadeLitros'] }}</small>
                </div>
                <div class="grid gap-1">
                  <label for="veiculo-ano">Ano</label>
                  <input id="veiculo-ano" inputmode="numeric" [formField]="veiculoForm.ano" (input)="limparErroCampo('ano')"
                    [attr.aria-invalid]="erros()['ano'] ? 'true' : null" aria-describedby="veiculo-ano-erro">
                  <small id="veiculo-ano-erro" class="text-red-800">{{ erros()['ano'] }}</small>
                </div>
              </fieldset>
            <section aria-labelledby="titulo-foto" class="secao-foto">
              <h3 id="titulo-foto" #tituloFoto tabindex="-1" class="foco-transicao font-semibold">Foto do veículo <span class="text-sm text-slate-600">· Opcional</span></h3>
              <div class="layout-foto">
                <div class="previsualizacao">
                  @if (fotoUrl(); as url) {
                    <img [src]="url" [alt]="descricaoFoto()" class="max-h-60 w-full object-contain">
                  } @else if (estadoFoto() === 'ausente') {
                    <div class="sem-foto" role="status"><app-icone-modulo nome="frota" /> Veículo sem foto</div>
                  } @else {
                    <p role="status">{{ estadoFoto() === 'carregando' ? 'Carregando foto…' :
                      'Foto temporariamente indisponível.' }}</p>
                  }
                </div>
              <div class="detalhes-foto">
              @if (!editandoId() && !cadastroConfirmado()) {
                <p>Se você selecionar uma foto, ela será enviada depois que o veículo for cadastrado.</p>
              }
              <p class="m-0" role="status">{{ operacaoFoto() || avisoFoto() }}</p>
              @if (erroFoto()) {
                <p id="erro-foto" #resumoFoto tabindex="-1" role="alert" class="m-0 rounded-lg border border-red-300 bg-red-50 p-3">{{ erroFoto() }}</p>
              }
              <p id="ajuda-foto" class="m-0 text-sm text-slate-600">JPEG ou PNG estático, até 16 megapixels. O envio completo, com o arquivo, não pode passar de 5 MiB.</p>
              <label class="selecionar-arquivo">
                Selecionar arquivo
                <input type="file" #entradaFoto class="sr-only" accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                  [disabled]="bloquearNavegacaoFoto()" (change)="selecionarFoto($event)"
                  [attr.aria-invalid]="erroArquivoFoto() ? 'true' : null"
                  [attr.aria-describedby]="erroArquivoFoto() ? 'ajuda-foto erro-foto' : 'ajuda-foto'">
              </label>
              <p class="m-0 text-sm text-slate-600" role="status">{{ arquivoFoto() ? 'Um arquivo selecionado.' : 'Nenhum arquivo selecionado.' }}</p>
              @if (editandoId()) {
                <div class="flex flex-wrap gap-2">
                  <button type="button" class="compacto" [disabled]="!arquivoFoto() || bloquearNavegacaoFoto() || !etag() || versaoFotoVencida() || estadoFoto() === 'carregando'"
                    (click)="enviarFoto()">{{ cadastroConfirmado() ? 'Tentar enviar a foto novamente' : (fotoConhecida() ? 'Substituir foto' : 'Enviar foto') }}</button>
                  @if (fotoConhecida() && !cadastroConfirmado()) {
                    <button type="button" class="compacto perigo"
                      [disabled]="bloquearNavegacaoFoto() || !etag() || versaoFotoVencida()"
                      (click)="removerFoto()">Remover foto</button>
                  }
                </div>
              }
              </div>
              </div>
            </section>
              <div class="acoes-editor">
                <button type="button" (click)="voltarVeiculos()"
                  [disabled]="bloquearNavegacaoFoto()">{{ cadastroConfirmado() ? 'Voltar aos veículos' : 'Cancelar' }}</button>
                @if (!cadastroConfirmado()) {
                  <button type="submit" class="primario" [disabled]="bloquearNavegacaoFoto()">{{ salvando() ? 'Salvando veículo…' : 'Salvar' }}</button>
                }
              </div>
            </form>
          }
        </section>
      }
      <dialog #dialogoExclusao aria-labelledby="titulo-exclusao" aria-describedby="registro-exclusao historico-exclusao"
        (cancel)="$event.preventDefault(); fecharExclusao(false)" (keydown)="conterFoco($event, cancelarExclusao, confirmarExclusao)">
        <h2 id="titulo-exclusao">Excluir {{ exclusao()?.entidade }}</h2>
        <p id="registro-exclusao">Deseja excluir {{ exclusao()?.nome }}?</p>
        <p id="historico-exclusao">O histórico será preservado.</p>
        <div class="acoes-editor">
          <button #cancelarExclusao type="button" autofocus (click)="fecharExclusao(false)">Cancelar</button>
          <button #confirmarExclusao type="button" class="excluir" (click)="fecharExclusao(true)">Excluir</button>
        </div>
      </dialog>
      <ng-template #acoes let-nome="nome" let-editar="editar" let-id="id" let-entidade="entidade" let-caminho="caminho">
        <div class="acoes-tabela">
          <button type="button" class="acao dica-editar" [id]="'frota-editar-' + id" [attr.aria-label]="'Editar ' + nome"
            [disabled]="bloquearNavegacaoFoto()" (click)="editar(id)">
            <svg lucidePencil aria-hidden="true"></svg>
          </button>
          <button type="button" class="acao perigo dica-excluir" [attr.aria-label]="'Excluir ' + nome"
            [disabled]="bloquearNavegacaoFoto()" (click)="pedirExclusao({ caminho: caminho + '/' + id, entidade, nome }, $event)">
            <svg lucideTrash2 aria-hidden="true"></svg>
          </button>
        </div>
      </ng-template>
    </main>
  `,
})
export class FrotaComponent {
  private consultaAtual = 0;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destruicao = inject(DestroyRef);
  private readonly servico = inject(CadastrosService);
  private readonly injector = inject(Injector);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');
  private readonly sessao = inject(SessaoService);
  private readonly saida = inject(SaidaCoordenador);
  private readonly contexto = this.sessao.contexto();
  private readonly dialogoExclusao = viewChild<ElementRef<HTMLDialogElement>>('dialogoExclusao');
  private readonly cadastrar = viewChild<ElementRef<HTMLButtonElement>>('cadastrar');
  private readonly resumoFoto = viewChild<ElementRef<HTMLElement>>('resumoFoto');
  private readonly tituloFoto = viewChild<ElementRef<HTMLElement>>('tituloFoto');
  private readonly avisoElemento = viewChild<ElementRef<HTMLElement>>('avisoElemento');
  private readonly tituloEditorElemento = viewChild<ElementRef<HTMLElement>>('tituloEditorElemento');
  private readonly entradaFoto = viewChild<ElementRef<HTMLInputElement>>('entradaFoto');
  private geracaoFoto = 0;
  private leituraFoto = 0;
  private origemExclusao?: HTMLElement;
  private readonly referenciaEditor = signal('');

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'grupos', rotulo: 'Grupos' },
    { valor: 'planos', rotulo: 'Planos' },
    { valor: 'veiculos', rotulo: 'Veículos' },
  ];
  readonly aba = signal<Aba>('grupos');
  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly excluindo = signal(false);
  readonly exclusao = signal<Exclusao | null>(null);
  readonly erroConsulta = signal('');
  readonly aviso = signal('');
  readonly faseSaida = this.saida.fase;
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
  readonly bloquearNavegacaoFoto = computed(() => this.salvando() || this.excluindo() ||
    !!this.operacaoFoto() || this.saida.fase() !== 'livre');

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
    const removerEditor = this.saida.registrar({ estado: () => ({
      rascunhoAlterado: this.editando() && !this.cadastroConfirmado() && this.snapshotEditor() !== this.referenciaEditor(),
      selecaoPendente: !!this.arquivoFoto(),
      fotoPendenteAposCadastro: this.cadastroConfirmado() && !!this.arquivoFoto(),
      operacaoEmCurso: this.salvando() || this.excluindo() || !!this.operacaoFoto(),
    }) });
    effect(() => {
      if (!this.sessao.autenticada()) this.limparFoto();
    });
    this.destruicao.onDestroy(() => {
      removerEditor();
      this.limparFoto();
    });
    void this.carregar();
  }

  private contextoAtual() {
    return !this.destruicao.destroyed && this.sessao.contexto() === this.contexto;
  }

  reautenticar() {
    if (this.contexto !== undefined && this.contextoAtual()) this.sessao.reautenticar(this.contexto);
  }

  podeNavegar() {
    return this.faseSaida() === 'livre' && !this.saida.ocupada();
  }

  private snapshotEditor() {
    if (this.aba() === 'grupos') return this.grupoModelo().nome.trim();
    if (this.aba() === 'veiculos') return JSON.stringify(this.veiculoModelo());
    const plano = this.planoModelo();
    return JSON.stringify({
      ...plano,
      precoKm: plano.modalidade === 'Livre' ? '' : plano.precoKm,
      franquiaDiariaKm: plano.modalidade === 'Controlado' ? plano.franquiaDiariaKm : '',
    });
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
        if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
        this.grupos.set(pagina.items);
        this.total.set(pagina.total);
      } else if (this.aba() === 'planos') {
        const pagina = await this.servico.listar<Plano>('/planos', this.pagina());
        if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
        this.planos.set(pagina.items);
        this.total.set(pagina.total);
      } else {
        const pagina = await this.servico.listar<Veiculo>('/veiculos', this.pagina(), 20,
          { grupoId: this.filtroGrupo() || undefined });
        if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
        this.veiculos.set(pagina.items);
        this.total.set(pagina.total);
      }
      if (this.total() > 0 && this.pagina() > Math.ceil(this.total() / 20)) {
        this.pagina.set(Math.ceil(this.total() / 20));
        await this.carregar();
        return;
      }
      if (this.total() === 0) this.pagina.set(1);
      const opcoes = await this.servico.listarOpcoes<Grupo>('/grupos');
      if (consulta === this.consultaAtual && this.contextoAtual()) this.gruposOpcoes.set(opcoes);
    } catch (erro: unknown) {
      if (consulta !== this.consultaAtual || !this.contextoAtual()) return;
      this.erroConsulta.set('Não foi possível consultar os cadastros. Verifique a conexão e tente novamente.');
    } finally {
      if (consulta === this.consultaAtual) this.carregando.set(false);
    }
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
    if (this.bloquearNavegacaoFoto()) return;
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
    this.prepararEditor();
  }

  editarGrupo = async (id: string) => {
    if (this.bloquearNavegacaoFoto()) return;
    const geracao = ++this.geracaoFoto;
    try {
      const resposta = await this.servico.obter<Grupo>(`/grupos/${id}`);
      if (!this.abrirEdicao(geracao)) return;
      this.grupoModelo.set({ nome: resposta.dados.nome });
      this.etag.set(resposta.etag);
      this.editandoId.set(id);
      this.erros.set({});
      this.prepararEditor();
    } catch (erro: unknown) { if (geracao === this.geracaoFoto && this.contextoAtual()) this.falhar(erro); }
  };

  editarPlano = async (id: string) => {
    if (this.bloquearNavegacaoFoto()) return;
    const geracao = ++this.geracaoFoto;
    try {
      const resposta = await this.servico.obter<Plano>(`/planos/${id}`);
      if (!this.abrirEdicao(geracao)) return;
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
      this.prepararEditor();
    } catch (erro: unknown) { if (geracao === this.geracaoFoto && this.contextoAtual()) this.falhar(erro); }
  };

  editarVeiculo = async (id: string) => {
    if (this.bloquearNavegacaoFoto()) return;
    this.limparFoto();
    const operacao = this.geracaoFoto;
    try {
      const resposta = await this.servico.obter<Veiculo>(`/veiculos/${id}?paraEdicao=true`);
      if (!this.abrirEdicao(operacao)) return;
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
      this.descricaoFoto.set(`Foto do veículo ${resposta.dados.placa}`);
      this.prepararEditor();
      void this.carregarFoto(id);
    } catch (erro: unknown) { if (operacao === this.geracaoFoto && this.contextoAtual()) this.falhar(erro); }
  };

  private abrirEdicao(geracao: number) {
    if (geracao !== this.geracaoFoto || !this.contextoAtual() || this.bloquearNavegacaoFoto() || this.exclusao()) return false;
    this.aviso.set('');
    return true;
  }

  private prepararEditor() {
    this.referenciaEditor.set(this.snapshotEditor());
    this.editando.set(true);
    afterNextRender(() => this.tituloEditorElemento()?.nativeElement.focus(), { injector: this.injector });
  }

  cancelar(restaurarFoco = false) {
    const id = this.editandoId();
    this.limparFoto();
    this.editando.set(false);
    this.editandoId.set(null);
    this.erros.set({});
    if (restaurarFoco) afterNextRender(() => {
      const origem = id ? this.host.nativeElement.querySelector<HTMLElement>(`#${CSS.escape(`frota-editar-${id}`)}`) : null;
      (origem ?? this.cadastrar()?.nativeElement)?.focus();
    }, { injector: this.injector });
  }

  async salvarGrupo() {
    if (this.bloquearNavegacaoFoto()) return;
    const nome = this.grupoModelo().nome.trim();
    if (!nome) { this.falharCampos({ nome: 'Informe o nome do grupo.' }); return; }
    if (nome.length > 200) { this.falharCampos({ nome: 'Use no máximo 200 caracteres.' }); return; }
    await this.executarSalvar(async () => {
      if (this.editandoId()) {
        await this.servico.editar(`/grupos/${this.editandoId()}`, { nome }, this.etag());
      } else {
        await this.servico.criar('/grupos', { nome });
      }
    }, nome);
  }

  async salvarPlano() {
    if (this.bloquearNavegacaoFoto()) return;
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
    }, `${this.nomeGrupo(modelo.grupoId)} — ${this.rotuloModalidade(modelo.modalidade)}`);
  }

  async salvarVeiculo() {
    if (this.bloquearNavegacaoFoto() || this.cadastroConfirmado()) return;
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
      await this.executarSalvar(() => this.servico.editar(`/veiculos/${id}`, corpo, this.etag()), modelo.placa);
      return;
    }
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    const foto = this.arquivoFoto();
    const operacao = this.geracaoFoto;
    this.salvando.set(true);
    this.erros.set({});
    this.aviso.set('');
    try {
      const resposta = await this.servico.criar<Veiculo>('/veiculos', corpo);
      if (operacao !== this.geracaoFoto || !this.contextoAtual()) return;
      if (foto && resposta.status !== 201) {
        this.falharCampos({ geral: 'Não foi possível confirmar o cadastro. A foto não foi enviada. Consulte a lista antes de tentar novamente.' });
        return;
      }
      if (!foto) {
        await this.encerrarVeiculo(`Veículo cadastrado sem foto. ${resposta.dados?.placa || modelo.placa}`);
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
      if (operacao === this.geracaoFoto && this.contextoAtual()) {
        const errosCadastro = CadastrosService.errosDe(erro);
        if (foto) errosCadastro['foto'] = 'A foto não foi enviada. Consulte a lista se o cadastro não puder ser confirmado.';
        this.falharCampos(errosCadastro);
      }
    } finally {
      this.salvando.set(false);
      liberar();
    }
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
    const atual = () => geracao === this.geracaoFoto && leitura === this.leituraFoto && this.contextoAtual() && this.sessao.autenticada();
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

  enviarFoto() { return this.executarFoto(false); }
  removerFoto() { return this.executarFoto(true); }

  private async executarFoto(remover: boolean) {
    if (this.bloquearNavegacaoFoto() || (!remover && this.estadoFoto() === 'carregando')) return;
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    try {
      await this.mutarFoto(remover, remover ? null : this.arquivoFoto());
    } finally { liberar(); }
  }

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
      if (geracao !== this.geracaoFoto || !this.contextoAtual()) return;
      this.etag.set(resposta.etag);
      this.arquivoFoto.set(null);
      const entrada = this.entradaFoto()?.nativeElement;
      if (entrada) entrada.value = '';
      if (this.cadastroConfirmado()) {
        await this.encerrarVeiculo(`Veículo cadastrado e foto salva. ${this.veiculoModelo().placa}`);
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
      if (geracao !== this.geracaoFoto || !this.contextoAtual()) return;
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
    if (this.cadastroConfirmado()) void this.encerrarVeiculo(`Veículo cadastrado. ${this.veiculoModelo().placa}`);
    else this.cancelar(true);
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

  pedirExclusao(exclusao: Exclusao, evento: Event) {
    if (this.bloquearNavegacaoFoto() || this.exclusao()) return;
    this.origemExclusao = evento.currentTarget as HTMLElement;
    this.exclusao.set(exclusao);
    afterNextRender(() => {
      if (this.exclusao() !== exclusao) return;
      this.dialogoExclusao()?.nativeElement.showModal();
    }, { injector: this.injector });
  }

  async fecharExclusao(confirmada: boolean) {
    const exclusao = this.exclusao();
    const origem = this.origemExclusao;
    this.dialogoExclusao()?.nativeElement.close();
    this.exclusao.set(null);
    this.origemExclusao = undefined;
    if (confirmada && exclusao) await this.executarExclusao(exclusao);
    else origem?.focus();
  }

  conterFoco(evento: KeyboardEvent, cancelar: HTMLButtonElement, confirmar: HTMLButtonElement) {
    if (evento.key !== 'Tab') return;
    if (evento.shiftKey ? evento.target === cancelar : evento.target === confirmar) {
      evento.preventDefault();
      (evento.shiftKey ? confirmar : cancelar).focus();
    }
  }

  dispensarAviso() {
    this.aviso.set('');
    (this.host.nativeElement.querySelector<HTMLElement>('.regiao-tabela') ??
      this.host.nativeElement.querySelector<HTMLElement>('.abas [aria-current="page"]'))?.focus();
  }

  private iniciarMutacao() {
    return this.contextoAtual() ? this.saida.iniciarMutacao() : undefined;
  }

  private async executarExclusao({ caminho, nome }: Exclusao) {
    const liberar = this.iniciarMutacao();
    if (!liberar) return;
    this.excluindo.set(true);
    this.aviso.set('');
    this.erros.set({});
    try {
      const resposta = await this.servico.obter<unknown>(caminho);
      if (!this.contextoAtual()) return;
      await this.servico.excluir(caminho, resposta.etag);
      if (!this.contextoAtual()) return;
      this.aviso.set(`${nome}: cadastro excluído.`);
      afterNextRender(() => this.avisoElemento()?.nativeElement.focus(), { injector: this.injector });
      await this.carregar();
    } catch (erro: unknown) {
      if (this.contextoAtual()) this.falhar(erro);
    } finally {
      this.excluindo.set(false);
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
      if (this.contextoAtual()) await this.encerrarVeiculo(`Cadastro salvo. ${nome}`);
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

  limparErroCampo(...campos: string[]) {
    if (!campos.some(campo => !!this.erros()[campo])) return;
    this.erros.update(atuais => {
      const restantes = { ...atuais };
      for (const campo of campos) delete restantes[campo];
      return restantes;
    });
  }

  identificarPlano(plano: Plano): string {
    return `${plano.grupoNome} — ${this.rotuloModalidade(plano.modalidade)}`;
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
