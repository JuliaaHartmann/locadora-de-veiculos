import { afterNextRender, Component, ElementRef, inject, Injector, signal, viewChild } from '@angular/core';
import { email, form, FormField, minLength, required, validate, type FieldState } from '@angular/forms/signals';
import { HttpErrorResponse, HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CONFIGURACAO_API } from './configuracao-api';
import { SessaoService } from './sessao.service';
import { LucideUserPlus } from '@lucide/angular';

@Component({
  standalone: true,
  imports: [FormField, RouterLink, LucideUserPlus],
  templateUrl: './registro.component.html',
  styleUrl: './registro.component.css',
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
  private readonly injector = inject(Injector);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly sessao = inject(SessaoService);

  protected invalido(campo: FieldState<string>): boolean {
    return campo.invalid() && (this.tentativa() || campo.touched());
  }

  protected descricao(campo: FieldState<string>, id: string): string {
    return this.invalido(campo) && this.erro() ? `${id} resumo-erros-registro` : id;
  }

  protected manterFocoVisivel(evento: FocusEvent) {
    const alvo = evento.target;
    if (!(alvo instanceof HTMLElement) || alvo === this.resumo()?.nativeElement) return;
    // A mensagem do campo anterior pode deslocar o novo foco depois da rolagem nativa do Tab.
    afterNextRender(() => {
      if (alvo === alvo.ownerDocument.activeElement) {
        alvo.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      }
    }, { injector: this.injector });
  }

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
    afterNextRender({ write: () => this.resumo()?.nativeElement.focus() }, { injector: this.injector });
  }
}
