import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { apiInterceptor } from './api.interceptor';
import { CONFIGURACAO_API } from './configuracao-api';
import { EditorSaida, EstadoEditorSaida, SaidaCoordenador } from './saida-coordenador.service';
import { SessaoService, Tokens } from './sessao.service';

const sessaoUrl = `${CONFIGURACAO_API.url}/session`;
const operacaoUrl = `${CONFIGURACAO_API.url}/operacao-sintetica`;
const negado = { status: 401, statusText: 'Unauthorized' };
const concluido = { status: 204, statusText: 'No Content' };
const tokens: Tokens = {
  accessToken: 'bearer-original', refreshToken: 'refresh-original', sessaoId: 'original',
  accessExpiraEm: '2099-01-01T00:00:00Z',
};
const renovada = { ...tokens, accessToken: 'bearer-renovado' };
const nova = { ...tokens, accessToken: 'bearer-substituto', sessaoId: 'substituta' };
const ativa = { ativa: true, sessaoId: tokens.sessaoId };

describe('DT-22 no recorte de Início', () => {
  let sessao: SessaoService;
  let saida: SaidaCoordenador;
  let http: HttpTestingController;
  let router: Router;
  const permitir = () => true;
  const sair = (confirmar = vi.fn(async (_mensagem: string) => true)) => saida.solicitar(permitir, confirmar);
  const logout = () => http.expectOne({ method: 'POST', url: `${sessaoUrl}/logout` });
  const consulta = () => http.expectOne({ method: 'GET', url: sessaoUrl });
  const refresh = () => http.expectOne({ method: 'POST', url: `${sessaoUrl}/refresh` });
  const noLogout = () => http.expectNone(`${sessaoUrl}/logout`);

  function editorSimulado(estado: EstadoEditorSaida) {
    const dados = signal(estado);
    const editor: EditorSaida = { estado: dados };
    saida.registrar(editor);
    return dados;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting()],
    });
    sessao = TestBed.inject(SessaoService);
    saida = TestBed.inject(SaidaCoordenador);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    sessao.definir(tokens);
  });
  afterEach(() => http.verify());

  it('reúne cliques, reserva antes do envio e encerra somente com 204', async () => {
    const a = sair();
    const b = sair();
    expect(a).toBe(b);
    await Promise.resolve();
    const pedido = logout();
    expect(pedido.request.headers.get('Authorization')).toBe('Bearer bearer-original');
    expect(sessao.autenticada()).toBe(true);
    expect(saida.fase()).toBe('revogando');
    expect(saida.iniciarMutacao()).toBeUndefined();
    expect(sair()).toBe(a);
    pedido.flush(null, concluido);
    await a;
    expect(sessao.autenticada()).toBe(false);
    expect(sessao.obterAccessToken()).toBeUndefined();
    expect(sessao.aviso()).toBe('');
    expect(router.navigateByUrl).toHaveBeenCalledExactlyOnceWith('/login');
  });

  it('nega saída não integrada e bloqueia enquanto a mutação ou foto está em curso', async () => {
    const confirmar = vi.fn(async () => true);
    await saida.solicitar(() => false, confirmar);
    const editor = editorSimulado({ operacaoEmCurso: true, rascunhoAlterado: true });
    await sair(confirmar);
    editor.set({ rascunhoAlterado: true });
    const liberar = saida.iniciarMutacao()!;
    expect(liberar).toBeTypeOf('function');
    await sair(confirmar);
    expect(confirmar).not.toHaveBeenCalled();
    noLogout();
    liberar();
    expect(saida.ocupada()).toBe(false);
  });

  it('distingue seleções e tentativa incerta; cancelar não faz HTTP nem altera o editor', async () => {
    const dados = { fotoPendenteAposCadastro: true, tentativaIncerta: true };
    const editor = editorSimulado(dados);
    const confirmar = vi.fn(async (_mensagem: string) => false);
    const a = sair(confirmar);
    expect(sair(confirmar)).toBe(a);
    await a;
    expect(confirmar).toHaveBeenCalledExactlyOnceWith(expect.stringMatching(/já foi cadastrado.*mesma tentativa/));
    expect(editor()).toBe(dados);
    expect(saida.fase()).toBe('livre');
    expect(sessao.autenticada()).toBe(true);
    noLogout();
  });

  it('reavalia uma mutação que venceu a disputa durante a confirmação', async () => {
    editorSimulado({ rascunhoAlterado: true });
    let resolver!: (value: boolean) => void;
    const confirmar = vi.fn(() => new Promise<boolean>(resolve => resolver = resolve));
    const tentativa = sair(confirmar);
    await Promise.resolve();
    const liberar = saida.iniciarMutacao()!;
    expect(liberar).toBeTypeOf('function');
    resolver(true);
    await tentativa;
    noLogout();
    expect(saida.fase()).toBe('livre');
    liberar();
  });

  it('pede confirmação para nova proteção surgida no diálogo', async () => {
    const editor = editorSimulado({ rascunhoAlterado: true });
    const confirmar = vi.fn(async (_mensagem: string) => false).mockImplementationOnce(async () => {
      editor.set({ tentativaIncerta: true });
      return true;
    });
    await sair(confirmar);
    expect(confirmar).toHaveBeenNthCalledWith(1, expect.stringContaining('alterações não salvas'));
    expect(confirmar).toHaveBeenNthCalledWith(2, expect.stringContaining('mesma tentativa'));
    noLogout();
  });

  it('mantém trava após resposta perdida e GET 200, até nova revogação explícita', async () => {
    const primeira = sair();
    await Promise.resolve();
    logout().error(new ProgressEvent('error'));
    await vi.waitFor(() => consulta().flush(ativa));
    await primeira;
    expect(saida.fase()).toBe('incerta');
    expect(saida.iniciarMutacao()).toBeUndefined();
    expect(sessao.autenticada()).toBe(true);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    noLogout();
    const segunda = sair();
    await Promise.resolve();
    consulta().flush(ativa);
    await vi.waitFor(() => logout().flush(null, concluido));
    await segunda;
    expect(router.navigateByUrl).toHaveBeenCalledExactlyOnceWith('/login');
  });

  it('não confunde 401 com revogação confirmada e não renova a consulta de recuperação', async () => {
    const tentativa = sair();
    await Promise.resolve();
    logout().flush(null, negado);
    await vi.waitFor(() => consulta().flush(null, negado));
    await tentativa;
    expect(sessao.autenticada()).toBe(false);
    expect(sessao.aviso()).toContain('Não foi possível confirmar');
    expect(router.navigateByUrl).toHaveBeenCalledExactlyOnceWith('/login');
    http.expectNone(`${sessaoUrl}/refresh`);
    noLogout();
  });

  it('exige reautenticação explícita se a consulta ficar indisponível, sem alegar revogação', async () => {
    const tentativa = sair();
    await Promise.resolve();
    logout().flush(null, { status: 503, statusText: 'Unavailable' });
    await vi.waitFor(() => consulta().error(new ProgressEvent('error')));
    await tentativa;
    expect(saida.fase()).toBe('incerta');
    expect(saida.iniciarMutacao()).toBeUndefined();
    sessao.reautenticar(sessao.contexto()!);
    expect(sessao.aviso()).toContain('Não foi possível confirmar');
    expect(router.navigateByUrl).toHaveBeenCalledExactlyOnceWith('/login');
  });

  it.each([true, false])('renova no máximo uma vez para revogar com bearer vencido (refresh responde: %s)', async sucesso => {
    sessao.definir({ ...tokens, accessExpiraEm: '2000-01-01T00:00:00Z' });
    const tentativa = sair();
    await Promise.resolve();
    noLogout();
    const pedido = refresh();
    expect(pedido.request.headers.has('Authorization')).toBe(false);
    if (sucesso) {
      pedido.flush(renovada);
      await vi.waitFor(() => {
        const revogacao = logout();
        expect(revogacao.request.headers.get('Authorization')).toBe('Bearer bearer-renovado');
        revogacao.flush(null, concluido);
      });
    } else {
      pedido.error(new ProgressEvent('error'));
    }
    await tentativa;
    http.expectNone(`${sessaoUrl}/refresh`);
    noLogout();
    expect(sessao.autenticada()).toBe(false);
    expect(!!sessao.aviso()).toBe(!sucesso);
  });

  it('aguarda refresh em andamento e não repete o comando que o iniciou', async () => {
    const cliente = TestBed.inject(HttpClient);
    const erro = vi.fn();
    cliente.post(operacaoUrl, {}).subscribe({ error: erro });
    http.expectOne(operacaoUrl).flush(null, negado);
    const renovacao = refresh();
    const tentativa = sair();
    await Promise.resolve();
    noLogout();
    renovacao.flush(renovada);
    await vi.waitFor(() => {
      const revogacao = logout();
      expect(revogacao.request.headers.get('Authorization')).toBe('Bearer bearer-renovado');
      revogacao.flush(null, concluido);
    });
    await tentativa;
    expect(erro).toHaveBeenCalledTimes(1);
    http.expectNone(operacaoUrl);
    http.expectNone(`${sessaoUrl}/refresh`);
  });

  it.each(['GET', 'POST'])('arbitra 401 tardio de %s durante a saída e após troca de sessão', async method => {
    const cliente = TestBed.inject(HttpClient);
    const efetuar = () => {
      cliente.request(method, operacaoUrl).subscribe({ error: () => {} });
      return http.expectOne({ method, url: operacaoUrl });
    };
    const durante = efetuar();
    const incerta = efetuar();
    const substituida = efetuar();
    const origem = sessao.contexto();
    const tentativa = sair();
    await Promise.resolve();
    const revogacao = logout();
    durante.flush(null, negado);
    expect(await sessao.recuperar401(origem, tokens.accessToken)).toBeUndefined();
    revogacao.error(new ProgressEvent('error'));
    await vi.waitFor(() => consulta().flush(ativa));
    await tentativa;
    incerta.flush(null, negado);
    expect(await sessao.recuperar401(origem, tokens.accessToken)).toBeUndefined();
    sessao.definir(nova);
    substituida.flush(null, negado);
    expect(await sessao.recuperar401(origem, tokens.accessToken)).toBeUndefined();
    expect(sessao.obterAccessToken()).toBe(nova.accessToken);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    http.expectNone(operacaoUrl);
    http.expectNone(`${sessaoUrl}/refresh`);
  });

  it.each(['refresh', 'logout', 'consulta'])('ignora sucesso tardio de %s após substituição', async etapa => {
    let tentativa: Promise<unknown>;
    if (etapa === 'refresh') {
      tentativa = sessao.recuperar401(sessao.contexto(), tokens.accessToken);
      const pedido = refresh();
      sessao.definir(nova);
      pedido.flush(renovada);
    } else {
      tentativa = sair();
      await Promise.resolve();
      const pedido = logout();
      if (etapa === 'logout') {
        sessao.definir(nova);
        pedido.flush(null, concluido);
      } else {
        pedido.error(new ProgressEvent('error'));
        await vi.waitFor(() => {
          const busca = consulta();
          sessao.definir(nova);
          busca.flush(ativa);
        });
      }
    }
    await tentativa;
    expect(sessao.obterAccessToken()).toBe(nova.accessToken);
    expect(saida.fase()).toBe('livre');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('não entrega dados protegidos de um contexto substituído', () => {
    const receber = vi.fn();
    const erro = vi.fn();
    TestBed.inject(HttpClient).get(operacaoUrl).subscribe({ next: receber, error: erro });
    const pedido = http.expectOne(operacaoUrl);
    sessao.definir(nova);
    pedido.flush({});
    expect(receber).not.toHaveBeenCalled();
    expect(erro).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({ status: 0 }));
  });
});
