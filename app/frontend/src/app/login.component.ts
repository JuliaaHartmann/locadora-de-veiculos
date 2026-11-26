import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { form, FormField, required } from '@angular/forms/signals';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { SessaoService, Tokens } from './sessao.service';

@Component({
  standalone: true,
  imports: [FormField, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
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
      this.tentativa.set(false);
      this.dados.update(dados => ({ ...dados, senha: '' }));
      this.sessao.definir(tokens);
      await this.router.navigateByUrl('/');
    } catch {
      this.tentativa.set(true);
      this.dados.update(dados => ({ ...dados, senha: '' }));
      this.erro.set('Não foi possível autenticar.');
      this.resumo()?.nativeElement.focus();
    } finally { this.enviando.set(false); }
  }
}
