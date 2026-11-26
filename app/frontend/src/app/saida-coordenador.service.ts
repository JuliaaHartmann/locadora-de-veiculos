import { Injectable, computed, inject, signal } from '@angular/core';
import { SessaoService } from './sessao.service';

export interface EstadoEditorSaida {
  rascunhoAlterado?: boolean;
  selecaoPendente?: boolean;
  fotoPendenteAposCadastro?: boolean;
  tentativaIncerta?: boolean;
  operacaoEmCurso?: boolean;
}

export interface EditorSaida {
  // Lê sinais do editor para atualizar o menu; não descarta dados antecipadamente.
  estado(): EstadoEditorSaida;
}

@Injectable({ providedIn: 'root' })
export class SaidaCoordenador {
  private readonly sessao = inject(SessaoService);
  private readonly editores = signal(new Map<EditorSaida, symbol>());
  private readonly mutacoes = signal<ReadonlySet<symbol>>(new Set());
  private tentativa?: { contexto: symbol; resultado: Promise<void> };
  readonly fase = this.sessao.faseSaida;
  readonly ocupada = computed(() => this.temMutacao() ||
    this.lerEditores().some(({ estado }) => estado.operacaoEmCurso));

  private temMutacao() {
    const contexto = this.sessao.contexto();
    return contexto !== undefined && this.mutacoes().has(contexto);
  }

  private lerEditores() {
    return [...this.editores()].filter(([, contexto]) => contexto === this.sessao.contexto())
      .map(([editor]) => ({ editor, estado: editor.estado() }));
  }

  registrar(editor: EditorSaida) {
    const contexto = this.sessao.contexto();
    if (contexto === undefined) return () => {};
    this.editores.update(atuais => new Map(atuais).set(editor, contexto));
    return () => this.editores.update(atuais => {
      if (atuais.get(editor) !== contexto) return atuais;
      const restantes = new Map(atuais);
      restantes.delete(editor);
      return restantes;
    });
  }

  // Deve ser adquirida antes do primeiro HTTP e liberada ao resolver a operação inteira.
  iniciarMutacao(): (() => void) | undefined {
    const contexto = this.sessao.contexto();
    if (contexto === undefined || this.fase() !== 'livre' || this.mutacoes().has(contexto)) return;
    this.mutacoes.update(atuais => new Set(atuais).add(contexto));
    let ativa = true;
    return () => {
      if (!ativa) return;
      ativa = false;
      this.mutacoes.update(atuais => {
        const restantes = new Set(atuais);
        restantes.delete(contexto);
        return restantes;
      });
    };
  }

  solicitar(permitida: () => boolean, confirmar: (mensagem: string) => Promise<boolean>) {
    const contexto = this.sessao.contexto();
    if (contexto === undefined || !permitida()) return Promise.resolve();
    if (this.tentativa?.contexto === contexto) return this.tentativa.resultado;
    if (this.ocupada()) return Promise.resolve();
    const tentativa = { contexto, resultado: Promise.resolve() };
    tentativa.resultado = Promise.resolve()
      .then(() => this.executar(contexto, permitida, confirmar))
      .finally(() => { if (this.tentativa === tentativa) this.tentativa = undefined; });
    this.tentativa = tentativa;
    return tentativa.resultado;
  }

  private async executar(contexto: symbol, permitida: () => boolean, confirmar: (mensagem: string) => Promise<boolean>) {
    const confirmados = new Map<EditorSaida, string>();
    while (this.sessao.contexto() === contexto && permitida()) {
      const estados = this.lerEditores();
      if (this.temMutacao() || estados.some(({ estado }) => estado.operacaoEmCurso)) return;
      const pendentes = estados.map(({ editor, estado }) => ({
        editor,
        mensagem: [
          estado.rascunhoAlterado ? 'Existem alterações não salvas. Se você sair agora, essas alterações serão perdidas.' : '',
          estado.fotoPendenteAposCadastro ? 'O veículo já foi cadastrado. Se você sair, a seleção pendente da foto será perdida.' :
            estado.selecaoPendente ? 'Se você sair, a seleção pendente será perdida.' : '',
          estado.tentativaIncerta ? 'Se você sair, perderá a possibilidade de repetir a mesma tentativa nesta tela.' : '',
        ].filter(Boolean).join(' '),
      })).filter(({ editor, mensagem }) => mensagem && confirmados.get(editor) !== mensagem);
      if (!pendentes.length) {
        // Sem await entre rever as proteções e reservar a saída: a mutação usa a mesma fase.
        return this.sessao.revogar(contexto);
      }
      if (!await confirmar([...new Set(pendentes.map(p => p.mensagem))].join(' '))) return;
      pendentes.forEach(({ editor, mensagem }) => confirmados.set(editor, mensagem));
    }
  }
}
