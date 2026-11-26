import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpContextToken, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';

export interface Tokens {
  accessToken: string;
  refreshToken: string;
  sessaoId: string;
  accessExpiraEm: string;
}

interface ContextoSessao {
  readonly id: symbol;
  tokens: Tokens;
  renovavel: boolean;
  renovacao?: Promise<boolean>;
  saida?: Promise<void>;
}

// Estas chamadas levam o bearer explícito do contexto reservado, sem recuperação pelo interceptor.
export const REVOGACAO_CONTROLADA = new HttpContextToken(() => false);
type FaseSaida = 'livre' | 'revogando' | 'incerta';
const tempoLimite = 15_000;
const url = `${CONFIGURACAO_API.url}/session`;

@Injectable({ providedIn: 'root' })
export class SessaoService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly atual = signal<ContextoSessao | undefined>(undefined);
  private readonly fase = signal<FaseSaida>('livre');
  readonly faseSaida = this.fase.asReadonly();
  readonly contexto = computed(() => this.atual()?.id);
  readonly autenticada = computed(() => !!this.atual());
  readonly aviso = signal('');

  definir(tokens: Tokens) {
    this.atual.set({ id: Symbol(), tokens, renovavel: true });
    this.fase.set('livre');
  }

  obterAccessToken() {
    return this.atual()?.tokens.accessToken;
  }

  podeRecuperar(origem: symbol | undefined) {
    return origem !== undefined && origem === this.contexto() && this.fase() === 'livre';
  }

  async recuperar401(origem: symbol | undefined, bearerRejeitado: string | undefined) {
    const sessao = this.atual();
    if (!sessao || !this.podeRecuperar(origem)) return undefined;
    if (!sessao.renovacao && sessao.tokens.accessToken !== bearerRejeitado) {
      return sessao.tokens.accessToken;
    }
    const renovada = await this.renovarContexto(sessao);
    if (!this.podeRecuperar(origem)) return undefined;
    if (renovada) return sessao.tokens.accessToken;
    this.encerrar(sessao, 'Sessão encerrada. Entre novamente.');
    return undefined;
  }

  private renovarContexto(sessao: ContextoSessao): Promise<boolean> {
    if (sessao.renovacao) return sessao.renovacao;
    if (this.atual() !== sessao || !sessao.renovavel) return Promise.resolve(false);
    sessao.renovacao = firstValueFrom(this.http.post<Tokens>(`${url}/refresh`, {
      refreshToken: sessao.tokens.refreshToken,
    }, { timeout: tempoLimite })).then(tokens => {
      if (this.atual() !== sessao) return false;
      if (tokens.sessaoId !== sessao.tokens.sessaoId) {
        sessao.renovavel = false;
        return false;
      }
      sessao.tokens = tokens;
      return true;
    }).catch(() => {
      // A rotação pode ter sido aplicada mesmo se a resposta não chegou.
      sessao.renovavel = false;
      return false;
    }).finally(() => sessao.renovacao = undefined);
    return sessao.renovacao;
  }

  revogar(origem: symbol) {
    const sessao = this.atual();
    if (sessao?.id !== origem) return Promise.resolve();
    if (sessao.saida) return sessao.saida;
    const novaTentativa = this.fase() === 'incerta';
    this.fase.set('revogando');
    sessao.saida = this.revogarContexto(sessao, novaTentativa).finally(() => sessao.saida = undefined);
    return sessao.saida;
  }

  private async revogarContexto(sessao: ContextoSessao, novaTentativa: boolean) {
    if (sessao.renovacao) await sessao.renovacao;
    if (this.atual() !== sessao) return;

    const bearerValido = () => Date.parse(sessao.tokens.accessExpiraEm) > Date.now();
    if (!bearerValido() && !await this.renovarContexto(sessao)) {
      this.encerrarSemRevogacao(sessao);
      return;
    }
    if (this.atual() !== sessao) return;
    if (!bearerValido()) {
      this.encerrarSemRevogacao(sessao);
      return;
    }

    if (novaTentativa && !await this.confirmarSessaoAtiva(sessao)) return;
    if (this.atual() !== sessao) return;
    try {
      const resposta = await firstValueFrom(this.http.post(`${url}/logout`, {}, {
        ...this.opcoesRevogacao(sessao), observe: 'response',
      }));
      if (resposta.status === 204) {
        this.encerrar(sessao);
        return;
      }
    } catch (erro) {
      if (this.atual() !== sessao) return;
      // Se venceu em trânsito, a renovação fica para uma nova ação explícita.
      if (erro instanceof HttpErrorResponse && erro.status === 401 && !bearerValido()) {
        this.fase.set('incerta');
        return;
      }
    }
    if (this.atual() !== sessao) return;
    await this.confirmarSessaoAtiva(sessao);
    if (this.atual() === sessao) this.fase.set('incerta');
  }

  private async confirmarSessaoAtiva(sessao: ContextoSessao) {
    try {
      const resposta = await firstValueFrom(this.http.get<{ ativa: boolean; sessaoId: string }>(
        url, this.opcoesRevogacao(sessao)));
      if (this.atual() !== sessao) return false;
      if (resposta?.ativa === true && resposta.sessaoId === sessao.tokens.sessaoId) {
        // Um 200 pode chegar antes do commit do logout anterior; não libera gravações.
        return true;
      }
      this.encerrarSemRevogacao(sessao);
    } catch (erro) {
      if (this.atual() !== sessao) return false;
      if (erro instanceof HttpErrorResponse && erro.status === 401 &&
          Date.parse(sessao.tokens.accessExpiraEm) > Date.now()) this.encerrarSemRevogacao(sessao);
      else this.fase.set('incerta');
    }
    return false;
  }

  private opcoesRevogacao(sessao: ContextoSessao) {
    return {
      context: new HttpContext().set(REVOGACAO_CONTROLADA, true),
      headers: { Authorization: `Bearer ${sessao.tokens.accessToken}` },
      timeout: tempoLimite,
    };
  }

  reautenticar(origem: symbol) {
    const sessao = this.atual();
    if (sessao?.id === origem && this.fase() === 'incerta') this.encerrarSemRevogacao(sessao);
  }

  private encerrarSemRevogacao(sessao: ContextoSessao) {
    this.encerrar(sessao, 'Não foi possível confirmar a revogação remota. Entre novamente.');
  }

  private encerrar(sessao: ContextoSessao, aviso = '') {
    if (this.atual() !== sessao) return;
    this.atual.set(undefined);
    this.fase.set('livre');
    this.aviso.set(aviso);
    void this.router.navigateByUrl('/login');
  }
}
