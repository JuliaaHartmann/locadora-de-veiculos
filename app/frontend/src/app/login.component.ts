import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { form, FormField, required } from '@angular/forms/signals';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { SessaoService, Tokens } from './sessao.service';
import { LucideLogIn } from '@lucide/angular';

@Component({
  standalone: true,
  imports: [FormField, RouterLink, LucideLogIn],
  template: `<main><h1>Entrar</h1><p role="status">{{ aviso }}</p>
    <form (submit)="$event.preventDefault(); entrar()" novalidate>
      <label for="usuario">Usuário</label>
      <input id="usuario" autocomplete="username" [formField]="formulario.usuario"
        [attr.aria-invalid]="tentativa() && formulario.usuario().invalid()" aria-describedby="erro-usuario">
      <span id="erro-usuario">@if (tentativa() && formulario.usuario().invalid()) { Informe o usuário. }</span>
      <label for="senha">Senha</label>
      <input id="senha" type="password" autocomplete="current-password" [formField]="formulario.senha"
        [attr.aria-invalid]="tentativa() && formulario.senha().invalid()" aria-describedby="erro-senha">
      <span id="erro-senha">@if (tentativa() && formulario.senha().invalid()) { Informe a senha. }</span>
      <button type="submit" [disabled]="enviando()">
        <svg lucideLogIn aria-hidden="true" focusable="false" class="h-5 w-5"></svg> Entrar
      </button>
      <p #resumo tabindex="-1" role="alert">{{ erro() }}</p>
    </form><p>Primeiro acesso? <a routerLink="/registro">Criar empresa</a></p></main>`,
})
export class LoginComponent {
  readonly dados = signal({ usuario: '', senha: '' });
  readonly formulario = form(this.dados, caminho => {
    required(caminho.usuario);
    required(caminho.senha);
  });
  readonly erro = signal('');
  readonly enviando = signal(false);
  readonly tentativa = signal(false);
  private readonly http = inject(HttpClient);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');
  readonly aviso = this.sessao.aviso();
  constructor() { this.sessao.aviso.set(''); }

  async entrar() {
    if (this.enviando()) return;
    this.tentativa.set(true);
    if (this.formulario().invalid()) {
      this.erro.set('Informe usuário e senha.');
      this.resumo()?.nativeElement.focus();
      return;
    }
    this.erro.set('');
    this.enviando.set(true);
    try {
      const { usuario, senha } = this.dados();
      const tokens = await firstValueFrom(this.http.post<Tokens>(`${CONFIGURACAO_API.url}/session/login`, { username: usuario, password: senha }));
      this.dados.update(dados => ({ ...dados, senha: '' }));
      this.sessao.definir(tokens);
      await this.router.navigateByUrl('/');
    } catch {
      this.dados.update(dados => ({ ...dados, senha: '' }));
      this.erro.set('Não foi possível autenticar.');
      this.resumo()?.nativeElement.focus();
    } finally { this.enviando.set(false); }
  }
}
