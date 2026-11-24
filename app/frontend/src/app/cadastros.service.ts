import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams, HttpResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';

export interface Pagina<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface RespostaComEtag<T> {
  dados: T;
  etag: string;
  status?: number;
}

@Injectable({ providedIn: 'root' })
export class CadastrosService {
  private readonly http = inject(HttpClient);

  private url(caminho: string) {
    return `${CONFIGURACAO_API.url}${caminho}`;
  }

  private etag(resposta: HttpResponse<unknown>): string {
    return (resposta.headers.get('ETag') ?? '').replace(/"/g, '');
  }

  async listar<T>(caminho: string, page = 1, pageSize = 20,
    filtros: Record<string, string | undefined> = {}): Promise<Pagina<T>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [chave, valor] of Object.entries(filtros)) {
      if (valor) params = params.set(chave, valor);
    }
    return firstValueFrom(this.http.get<Pagina<T>>(this.url(caminho), { params }));
  }

  async obter<T>(caminho: string): Promise<RespostaComEtag<T>> {
    const resposta = await firstValueFrom(this.http.get<T>(this.url(caminho), { observe: 'response' }));
    return { dados: resposta.body as T, etag: this.etag(resposta) };
  }

  async listarOpcoes<T>(caminho: string, filtros: Record<string, string | undefined> = {}): Promise<T[]> {
    const itens: T[] = [];
    let pagina = 1;
    let resposta: Pagina<T>;
    do {
      resposta = await this.listar<T>(caminho, pagina++, 100, filtros);
      itens.push(...resposta.items);
    } while (resposta.items.length > 0 && itens.length < resposta.total);
    return itens;
  }

  async criar<T>(caminho: string, corpo: unknown): Promise<RespostaComEtag<T>> {
    const resposta = await firstValueFrom(
      this.http.post<T>(this.url(caminho), corpo, { observe: 'response' }));
    return { dados: resposta.body as T, etag: this.etag(resposta), status: resposta.status };
  }

  lerFoto(id: string): Promise<Blob> {
    return firstValueFrom(this.lerFotoCancelavel(id));
  }

  lerFotoCancelavel(id: string) {
    return this.http.get(this.url(`/veiculos/${encodeURIComponent(id)}/foto`), { responseType: 'blob' });
  }

  enviarFoto(id: string, etag: string, arquivo: File): Promise<RespostaComEtag<null>> {
    const corpo = new FormData();
    corpo.append('arquivo', arquivo);
    return this.editar(`/veiculos/${encodeURIComponent(id)}/foto`, corpo, etag);
  }

  removerFoto(id: string, etag: string): Promise<RespostaComEtag<null>> {
    return this.removerComEtag(`/veiculos/${encodeURIComponent(id)}/foto`, etag);
  }

  async adicionarComEtag<T>(caminho: string, corpo: unknown, etag: string): Promise<RespostaComEtag<T>> {
    const resposta = await firstValueFrom(this.http.post<T>(this.url(caminho), corpo, {
      observe: 'response',
      headers: { 'If-Match': `"${etag}"` },
    }));
    return { dados: resposta.body as T, etag: this.etag(resposta) };
  }

  async removerComEtag<T>(caminho: string, etag: string): Promise<RespostaComEtag<T>> {
    const resposta = await firstValueFrom(this.http.delete<T>(this.url(caminho), {
      observe: 'response',
      headers: { 'If-Match': `"${etag}"` },
    }));
    return { dados: resposta.body as T, etag: this.etag(resposta) };
  }

  async editar<T>(caminho: string, corpo: unknown, etag: string): Promise<RespostaComEtag<T>> {
    const resposta = await firstValueFrom(this.http.put<T>(this.url(caminho), corpo, {
      observe: 'response',
      headers: { 'If-Match': `"${etag}"` },
    }));
    return { dados: resposta.body as T, etag: this.etag(resposta) };
  }

  async excluir(caminho: string, etag: string): Promise<void> {
    await firstValueFrom(this.http.delete(this.url(caminho), {
      observe: 'response',
      headers: { 'If-Match': `"${etag}"` },
    }));
  }

  static errosDe(erro: unknown): Record<string, string> {
    if (erro instanceof HttpErrorResponse) {
      const corpo = erro.error as { errors?: Record<string, string[]>; detail?: string } | null;
      if (corpo?.errors) {
        return Object.fromEntries(Object.entries(corpo.errors)
          .map(([campo, mensagens]) => [campo, mensagens.join(' ')]));
      }
      if (erro.status === 412 || erro.status === 428) {
        return { geral: 'O cadastro foi alterado por outra operação. Recarregue e tente novamente.' };
      }
      if (erro.status === 0 || erro.status >= 500) {
        return { geral: 'Serviço indisponível. Tente novamente.' };
      }
      if (corpo?.detail) return { geral: corpo.detail };
      if (erro.status === 404) return { geral: 'Cadastro não encontrado.' };
      if (erro.status === 409) return { geral: 'A operação entra em conflito com outro cadastro.' };
    }
    return { geral: 'Não foi possível concluir a operação.' };
  }
}

export function decimalDoFormulario(valor: string): string {
  return valor.trim().replace(',', '.');
}

export function validarLimites(dados: Record<string, string>, limites: Record<string, number>): Record<string, string> {
  const erros: Record<string, string> = {};
  for (const [campo, maximo] of Object.entries(limites)) {
    if ((dados[campo] ?? '').trim().length > maximo)
      erros[campo] = `Use no máximo ${maximo} caracteres.`;
  }
  return erros;
}

export function formatarMoeda(valor: string | null | undefined): string {
  if (valor === null || valor === undefined || valor === '') return '—';
  const [inteiro, decimal = ''] = valor.split('.');
  const comMilhar = inteiro.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return `R$ ${comMilhar},${decimal.padEnd(2, '0').slice(0, 6)}`;
}

export function formatarData(valor: string | null | undefined): string {
  if (!valor) return '—';
  const data = new Date(valor);
  return Number.isNaN(data.getTime()) ? valor
    : data.toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}
