import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';

export interface Tokens {
  accessToken: string;
  refreshToken: string;
  sessaoId: string;
  accessExpiraEm: string;
}

@Injectable({ providedIn: 'root' })
export class SessaoService {
  private tokens?: Tokens;
  private renovacaoEmAndamento?: Promise<boolean>;
  readonly autenticada = signal(false);
  readonly aviso = signal('');

  constructor(private readonly http: HttpClient) {}

  definir(tokens: Tokens) {
    this.tokens = tokens;
    this.autenticada.set(true);
  }

  obterAccessToken() {
    return this.tokens?.accessToken;
  }

  async renovar(): Promise<boolean> {
    if (this.renovacaoEmAndamento) return this.renovacaoEmAndamento;
    if (!this.tokens) return false;
    this.renovacaoEmAndamento = firstValueFrom(this.http.post<Tokens>(`${CONFIGURACAO_API.url}/session/refresh`, {
      refreshToken: this.tokens.refreshToken,
    })).then(tokens => {
      this.definir(tokens);
      return true;
    }).catch(() => {
      this.limpar();
      return false;
    }).finally(() => this.renovacaoEmAndamento = undefined);
    return this.renovacaoEmAndamento;
  }

  async sair() {
    if (this.tokens) {
      await firstValueFrom(this.http.post(`${CONFIGURACAO_API.url}/session/logout`, {}));
    }
    this.limpar();
  }

  limpar() {
    this.tokens = undefined;
    this.autenticada.set(false);
  }
}
