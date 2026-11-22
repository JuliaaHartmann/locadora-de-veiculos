import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { email, form, FormField, minLength, required, validate } from '@angular/forms/signals';
import { HttpErrorResponse, HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { SessaoService } from './sessao.service';
import { LucideUserPlus } from '@lucide/angular';

@Component({
  standalone: true,
  imports: [FormField, RouterLink, LucideUserPlus],
  template: `
    <main>
      <h1>Crie sua empresa</h1>
      <p>Cadastre sua única conta administrativa. Nenhum dado empresarial adicional é necessário.</p>
      <form (submit)="$event.preventDefault(); registrar()" novalidate>
        <label for="usuario">Usuário</label>
        <input id="usuario" autocomplete="username" [formField]="formulario.usuario"
          aria-describedby="erro-usuario" [attr.aria-invalid]="formulario.usuario().invalid() && (tentativa() || formulario.usuario().touched())">
        <span id="erro-usuario">@if (tentativa() || formulario.usuario().touched()) {
          @for (erro of formulario.usuario().errors(); track erro.kind) { {{ erro.message }} }
        }</span>
        <label for="email">E-mail</label>
        <input id="email" type="email" autocomplete="email" [formField]="formulario.email"
          aria-describedby="erro-email" [attr.aria-invalid]="formulario.email().invalid() && (tentativa() || formulario.email().touched())">
        <span id="erro-email">@if (tentativa() || formulario.email().touched()) {
          @for (erro of formulario.email().errors(); track erro.kind) { {{ erro.message }} }
        }</span>
        <label for="senha">Senha</label>
        <input id="senha" type="password" autocomplete="new-password" [formField]="formulario.senha"
          aria-describedby="erro-senha" [attr.aria-invalid]="formulario.senha().invalid() && (tentativa() || formulario.senha().touched())">
        <span id="erro-senha">@if (tentativa() || formulario.senha().touched()) {
          @for (erro of formulario.senha().errors(); track erro.kind) { {{ erro.message }} }
        }</span>
        <label for="confirmacao">Confirmar senha</label>
        <input id="confirmacao" type="password" autocomplete="new-password" [formField]="formulario.confirmacaoSenha"
          aria-describedby="erro-confirmacao" [attr.aria-invalid]="formulario.confirmacaoSenha().invalid() && (tentativa() || formulario.confirmacaoSenha().touched())">
        <span id="erro-confirmacao">@if (tentativa() || formulario.confirmacaoSenha().touched()) {
          @for (erro of formulario.confirmacaoSenha().errors(); track erro.kind) { {{ erro.message }} }
        }</span>
        <p #resumo tabindex="-1" role="alert">{{ erro() }}</p>
        <button type="submit" [disabled]="enviando()">
          <svg lucideUserPlus aria-hidden="true" focusable="false" class="h-5 w-5"></svg>
          {{ enviando() ? 'Criando conta...' : 'Criar empresa' }}
        </button>
      </form>
      <p>Já tem uma conta? <a routerLink="/login">Entrar</a></p>
    </main>
  `,
})
export class RegistroComponent {
  readonly dados = signal({ usuario: '', email: '', senha: '', confirmacaoSenha: '' });
  readonly formulario = form(this.dados, caminho => {
    required(caminho.usuario, { message: 'Informe o usuário.' });
    validate(caminho.usuario, ({ value }) => value().trim() ? undefined : { kind: 'vazio', message: 'Informe o usuário.' });
    required(caminho.email, { message: 'Informe o e-mail.' });
    email(caminho.email, { message: 'Informe um e-mail válido.' });
    required(caminho.senha, { message: 'Informe a senha.' });
    minLength(caminho.senha, 8, { message: 'A senha deve ter pelo menos oito caracteres.' });
    required(caminho.confirmacaoSenha, { message: 'Confirme a senha.' });
    validate(caminho.confirmacaoSenha, ({ value, valueOf }) => value() === valueOf(caminho.senha)
      ? undefined : { kind: 'confirmacao', message: 'As senhas precisam ser iguais.' });
  });
  readonly tentativa = signal(false);
  readonly erro = signal('');
  readonly enviando = signal(false);
  private readonly resumo = viewChild<ElementRef<HTMLElement>>('resumo');
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly sessao = inject(SessaoService);

  async registrar() {
    if (this.enviando()) return;
    this.tentativa.set(true);
    if (this.formulario().invalid()) { this.falhar('Revise os campos indicados.'); return; }
    this.erro.set('');
    this.enviando.set(true);
    try {
      const { usuario, email, senha } = this.dados();
      await firstValueFrom(this.http.post(`${CONFIGURACAO_API.url}/registro`, { username: usuario, email, password: senha }));
      this.dados.set({ usuario: '', email: '', senha: '', confirmacaoSenha: '' });
      this.sessao.aviso.set('Empresa criada. Entre com seu usuário e senha.');
      await this.router.navigateByUrl('/login');
    } catch (erro: unknown) {
      this.dados.update(dados => ({ ...dados, senha: '', confirmacaoSenha: '' }));
      const status = erro instanceof HttpErrorResponse ? erro.status : 0;
      this.falhar(status === 409 ? 'Usuário indisponível. Revise os dados.'
        : status === 429 ? 'Muitas tentativas. Aguarde alguns minutos.'
        : status >= 500 || status === 0 ? 'Serviço indisponível. Tente mais tarde.'
        : 'Revise os dados informados.');
    } finally { this.enviando.set(false); }
  }

  private falhar(mensagem: string) {
    this.erro.set(mensagem);
    this.resumo()?.nativeElement.focus();
  }
}
