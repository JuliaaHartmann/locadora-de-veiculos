import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injectable, afterRenderEffect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, Subject, asapScheduler, catchError, defer, mergeMap, subscribeOn, takeUntil, tap, timeout } from 'rxjs';
import type { Subscriber, Subscription } from 'rxjs';
import { CadastrosService } from './cadastros.service';
import { SessaoService } from './sessao.service';

interface ConsultaMiniatura {
  id: string;
  saida: Subscriber<Blob>;
  cancelar: Subject<void>;
}

@Injectable()
export class LeiturasMiniaturasFrota {
  private readonly cadastros = inject(CadastrosService);
  private readonly sessao = inject(SessaoService);
  private readonly consultas = new Subject<ConsultaMiniatura>();

  constructor() {
    this.consultas.pipe(
      mergeMap(({ id, saida, cancelar }) =>
        defer(() => saida.closed || !this.sessao.autenticada()
          ? EMPTY : this.cadastros.lerFotoCancelavel(id).pipe(timeout(15_000))).pipe(
          // O próximo item só começa depois da desmontagem síncrona das linhas da lista anterior.
          subscribeOn(asapScheduler),
          takeUntil(cancelar),
          tap({ next: blob => saida.next(blob), error: erro => saida.error(erro), complete: () => saida.complete() }),
          catchError(() => EMPTY),
        ), 4),
      takeUntilDestroyed(),
    ).subscribe();
  }

  ler(id: string): Observable<Blob> {
    return new Observable(saida => {
      const cancelar = new Subject<void>();
      this.consultas.next({ id, saida, cancelar });
      return () => {
        cancelar.next();
        cancelar.complete();
      };
    });
  }
}

@Component({
  selector: 'app-foto-miniatura',
  host: { class: 'block' },
  template: `
    <div class="grid h-16 w-[5.5rem] place-content-center justify-items-center gap-0.5
      overflow-hidden rounded-lg border border-slate-300 bg-slate-50 text-center text-xs text-slate-600">
      @if (url(); as foto) {
        <img [src]="foto" [alt]="'Foto do veículo ' + placa()" width="84" height="60"
          class="h-[3.75rem] w-[5.25rem] object-contain" decoding="async" (error)="falhaNaImagem(foto)">
      } @else {
        <svg aria-hidden="true" focusable="false" class="size-5" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="m5 10 1.5-4.5h11L19 10M5 10h14l1.5 2v5.5h-17V12L5 10Zm0 7.5V20m14-2.5V20M7 14h2m6 0h2"/>
        </svg>
        <span>{{ mensagem() }}</span>
      }
    </div>
  `,
})
export class FotoMiniaturaComponent {
  readonly veiculoId = input.required<string>();
  readonly placa = input.required<string>();
  protected readonly url = signal('');
  protected readonly mensagem = signal('Carregando…');
  private urlAtual = '';

  constructor() {
    const leituras = inject(LeiturasMiniaturasFrota);
    const sessao = inject(SessaoService);
    const elemento = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

    afterRenderEffect({ read: limpar => {
      const id = this.veiculoId();
      if (!sessao.autenticada()) {
        this.mensagem.set('Foto indisponível');
        return;
      }
      this.mensagem.set('Carregando…');
      let atual = true;
      let assinatura: Subscription | undefined;
      const iniciar = () => {
        if (!atual || assinatura || !sessao.autenticada()) return;
        assinatura = leituras.ler(id).subscribe({
          next: blob => {
            if (!atual || !sessao.autenticada()) return;
            if (!blob?.size) {
              this.mensagem.set('Foto indisponível');
              return;
            }
            try {
              this.urlAtual = URL.createObjectURL(blob);
              this.url.set(this.urlAtual);
            } catch { this.mensagem.set('Foto indisponível'); }
          },
          error: erro => {
            if (atual) this.mensagem.set(erro instanceof HttpErrorResponse && erro.status === 404
              ? 'Sem foto' : 'Foto indisponível');
          },
        });
      };
      const observador = typeof IntersectionObserver === 'undefined' ? null : new IntersectionObserver(entradas => {
        if (entradas.some(entrada => entrada.isIntersecting)) {
          observador?.disconnect();
          iniciar();
        }
      }, { rootMargin: '180px' });
      if (observador) observador.observe(elemento);
      else iniciar();

      limpar(() => {
        atual = false;
        observador?.disconnect();
        assinatura?.unsubscribe();
        this.liberarUrl();
      });
    } });
  }

  protected falhaNaImagem(url: string) {
    if (url !== this.urlAtual) return;
    this.liberarUrl();
    this.mensagem.set('Foto indisponível');
  }

  private liberarUrl() {
    if (this.urlAtual) URL.revokeObjectURL(this.urlAtual);
    this.urlAtual = '';
    this.url.set('');
  }
}
