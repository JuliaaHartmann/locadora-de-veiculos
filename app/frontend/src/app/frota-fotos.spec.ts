import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from './api.interceptor';
import { CadastrosService } from './cadastros.service';
import { CONFIGURACAO_API } from './configuracao-api';
import { FrotaComponent } from './frota.component';
import { SessaoService } from './sessao.service';
import { SaidaCoordenador } from './saida-coordenador.service';

const veiculo = { id: 'identidade-1', placa: 'FIC01', grupoId: 'g1', marca: 'Marca', modelo: 'Modelo',
  cor: 'Cinza', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50' };
const arquivo = new File(['imagem sintética'], 'foto.png', { type: 'image/png' });
const erro = (status: number, body?: unknown) => new HttpErrorResponse({ status, error: body });

describe('Frota — fotos da F5', () => {
  const contexto = Symbol();
  const revogarSessao = vi.fn();
  const lerFoto = vi.fn();
  const enviarFoto = vi.fn();
  const removerFoto = vi.fn();
  const criar = vi.fn();
  const obter = vi.fn();
  const sessao = signal(true);
  const formaOriginal = Object.getOwnPropertyDescriptors(URL);
  const revogar = vi.fn();
  const url = vi.fn();

  beforeEach(() => {
    sessao.set(true);
    vi.clearAllMocks();
    revogar.mockReset();
    url.mockReset().mockReturnValue('blob:https://app.invalid/a');
    Object.defineProperties(URL, {
      createObjectURL: { configurable: true, value: url },
      revokeObjectURL: { configurable: true, value: revogar },
    });
    lerFoto.mockReset().mockRejectedValue(erro(404));
    enviarFoto.mockReset().mockResolvedValue({ etag: 'nova' });
    removerFoto.mockReset().mockResolvedValue({ etag: 'sem-foto' });
    criar.mockReset().mockResolvedValue({ dados: veiculo, etag: 'retornada', status: 201 });
    obter.mockReset().mockResolvedValue({ dados: veiculo, etag: 'retornada' });
    TestBed.configureTestingModule({ providers: [
      { provide: SessaoService, useValue: { autenticada: sessao, contexto: () => contexto, faseSaida: signal('livre'), revogar: revogarSessao } },
      { provide: CadastrosService, useValue: { obter, criar, lerFoto, enviarFoto, removerFoto,
        listar: async () => ({ items: [], page: 1, pageSize: 20, total: 0 }),
        listarOpcoes: async () => [{ id: 'g1', nome: 'Grupo' }] } },
    ] });
  });
  afterEach(() => {
    for (const chave of ['createObjectURL', 'revokeObjectURL'] as const) {
      if (formaOriginal[chave]) Object.defineProperty(URL, chave, formaOriginal[chave]);
      else Reflect.deleteProperty(URL, chave);
    }
  });

  async function montar(novo = true) {
    const f = TestBed.createComponent(FrotaComponent);
    await f.whenStable();
    const sut = f.componentInstance;
    sut.aba.set('veiculos');
    if (novo) {
      sut.novo();
      sut.veiculoModelo.set({ ...veiculo, combustivel: 'Gasolina', ano: '2026' });
    } else await sut.editarVeiculo(veiculo.id);
    f.detectChanges();
    return { f, sut };
  }

  function selecionar(sut: FrotaComponent) {
    sut.selecionarFoto({ target: { files: [arquivo] } } as unknown as Event);
  }

  it('sem foto, cria veículo sem enviar imagem e retorna à lista', async () => {
    const { f, sut } = await montar();
    expect(f.nativeElement.querySelector('input[type=file]')).toBeTruthy();
    await sut.salvarVeiculo();
    expect(criar).toHaveBeenCalledTimes(1);
    expect(enviarFoto).not.toHaveBeenCalled();
    expect(sut.aviso()).toContain('Veículo cadastrado sem foto.');
    expect(sut.editando()).toBe(false);
  });

  it('só envia após confirmar criação, usando exatamente o ID e ETag recebidos', async () => {
    const { sut } = await montar();
    selecionar(sut);
    let confirmar!: (resp: unknown) => void;
    criar.mockImplementationOnce(() => new Promise(resolve => { confirmar = resolve; }));
    const salvar = sut.salvarVeiculo();
    expect(enviarFoto).not.toHaveBeenCalled();
    expect(criar.mock.calls[0][1]).not.toHaveProperty('arquivo');
    confirmar({ dados: veiculo, etag: 'retornada', status: 201 });
    await salvar;
    expect(enviarFoto).toHaveBeenCalledWith('identidade-1', 'retornada', arquivo);
    expect(sut.aviso()).toContain('Veículo cadastrado e foto salva.');
  });

  it('não envia após erro do cadastro e conserva o arquivo', async () => {
    const { sut } = await montar();
    selecionar(sut);
    criar.mockRejectedValueOnce(erro(409));
    await sut.salvarVeiculo();
    expect(enviarFoto).not.toHaveBeenCalled();
    expect(sut.arquivoFoto()).toBe(arquivo);
    expect(sut.erros()['foto']).toContain('não foi enviada');
    expect(sut.editando()).toBe(true);
  });

  it.each([{ dados: {}, etag: 'retornada' }, { dados: veiculo, etag: '' }])(
    'não envia nem permite duplicar cadastro se a resposta confirmada não identifica a versão', async retorno => {
      const { sut } = await montar();
      selecionar(sut);
      criar.mockResolvedValueOnce({ ...retorno, status: 201 });
      await sut.salvarVeiculo();
      await sut.salvarVeiculo();
      await sut.enviarFoto();
      expect(criar).toHaveBeenCalledTimes(1);
      expect(enviarFoto).not.toHaveBeenCalled();
      expect(sut.erroFoto()).toContain('O veículo foi cadastrado');
    });

  it('mantém o cadastro, foca o erro parcial e repete só o upload', async () => {
    const { f, sut } = await montar();
    selecionar(sut);
    enviarFoto.mockRejectedValueOnce(erro(503, { code: 'foto_nao_gravada', detail: 'Não foi possível salvar a foto.' }));
    await sut.salvarVeiculo();
    f.detectChanges();
    expect(f.nativeElement.querySelector('button[type=submit]')).toBeNull();
    expect(document.activeElement?.id).toBe('erro-foto');
    expect(sut.erroFoto()).toContain('O veículo foi cadastrado');
    expect(f.nativeElement.textContent).toContain('Tentar enviar a foto novamente');
    revogarSessao.mockClear();
    const saida = TestBed.inject(SaidaCoordenador);
    const confirmar = vi.fn().mockResolvedValue(false);
    await saida.solicitar(() => true, confirmar);
    expect(confirmar).toHaveBeenCalledWith(expect.stringContaining('O veículo já foi cadastrado'));
    expect(confirmar.mock.calls[0][0]).not.toContain('alterações não salvas');
    expect(sut.arquivoFoto()).toBe(arquivo);
    expect(revogarSessao).not.toHaveBeenCalled();
    await sut.enviarFoto();
    expect(criar).toHaveBeenCalledTimes(1);
    expect(enviarFoto.mock.calls).toEqual([[veiculo.id, 'retornada', arquivo], [veiculo.id, 'retornada', arquivo]]);
    expect(sut.aviso()).toContain('Veículo cadastrado e foto salva.');
  });

  it.each([412, 409, 0])('preserva o cadastro e não repete automaticamente o envio após %i', async status => {
    const { sut } = await montar();
    selecionar(sut);
    enviarFoto.mockRejectedValueOnce(erro(status, { detail: 'Veículo com aluguel em aberto.' }));
    await sut.salvarVeiculo();
    expect(sut.cadastroConfirmado()).toBe(true);
    expect(criar).toHaveBeenCalledTimes(1);
    expect(enviarFoto).toHaveBeenCalledTimes(1);
    if (status === 412) {
      await sut.enviarFoto();
      expect(enviarFoto).toHaveBeenCalledTimes(1);
      expect(sut.erroFoto()).toContain('Consulte o cadastro');
    }
    if (status === 0) {
      expect(lerFoto).toHaveBeenCalledWith(veiculo.id);
      expect(sut.erroFoto()).toContain('Não foi possível confirmar');
    }
    if (status === 409) expect(sut.erroFoto()).toContain('aluguel em aberto');
  });

  it('só revoga a foto anterior quando a substituição é confirmada, e remove com a nova versão', async () => {
    lerFoto.mockResolvedValue(new Blob(['foto'], { type: 'image/png' }));
    url.mockReturnValueOnce('blob:https://app.invalid/antiga').mockReturnValueOnce('blob:https://app.invalid/nova');
    const { sut } = await montar(false);
    const cor = 'Azul';
    sut.veiculoModelo.update(x => ({ ...x, cor }));
    selecionar(sut);
    enviarFoto.mockRejectedValueOnce(erro(400, { errors: { arquivo: ['Imagem inválida.'] } }));
    await sut.enviarFoto();
    expect(sut.fotoUrl()).toContain('antiga');
    expect(revogar).not.toHaveBeenCalled();
    expect(sut.erroArquivoFoto()).toBe(true);
    await sut.enviarFoto();
    expect(revogar).toHaveBeenCalledWith('blob:https://app.invalid/antiga');
    expect(sut.fotoUrl()).toContain('nova');
    expect(sut.veiculoModelo().cor).toBe(cor);
    await sut.removerFoto();
    expect(removerFoto).toHaveBeenCalledWith(veiculo.id, 'nova');
    expect(revogar).toHaveBeenCalledWith('blob:https://app.invalid/nova');
    expect(sut.estadoFoto()).toBe('ausente');
  });

  it('mostra indisponibilidade sem bloquear o formulário, e descarta a resposta que chega após sair', async () => {
    lerFoto.mockRejectedValueOnce(erro(503));
    const { f, sut } = await montar(false);
    expect(sut.estadoFoto()).toBe('indisponivel');
    expect(f.nativeElement.querySelector('button[type=submit]').disabled).toBe(false);
    let responder!: (blob: Blob) => void;
    lerFoto.mockImplementationOnce(() => new Promise(resolve => { responder = resolve; }));
    await sut.editarVeiculo(veiculo.id);
    sut.cancelar();
    responder(new Blob(['não exibir']));
    await Promise.resolve();
    expect(url).not.toHaveBeenCalled();
  });

  it('revoga URL ao trocar de veículo e no logout sem gravar no navegador', async () => {
    lerFoto.mockResolvedValue(new Blob(['imagem'], { type: 'image/png' }));
    url.mockReturnValueOnce('blob:https://app.invalid/1').mockReturnValueOnce('blob:https://app.invalid/2');
    const guardar = vi.spyOn(Storage.prototype, 'setItem');
    const { f, sut } = await montar(false);
    await sut.editarVeiculo('outro');
    expect(revogar).toHaveBeenCalledWith('blob:https://app.invalid/1');
    sessao.set(false);
    f.detectChanges();
    expect(revogar).toHaveBeenCalledWith('blob:https://app.invalid/2');
    expect(sut.fotoUrl()).toBe('');
    expect(guardar).not.toHaveBeenCalled();
    guardar.mockRestore();
  });
});

describe('Contrato cliente de foto', () => {
  it('envia multipart com versão e bearer apenas à API; lê a imagem como blob', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting(),
      { provide: SessaoService, useValue: { obterAccessToken: () => 'sessao-sintetica', contexto: () => undefined } },
    ] });
    const api = TestBed.inject(CadastrosService);
    const http = TestBed.inject(HttpTestingController);
    const rota = CONFIGURACAO_API.url + '/veiculos/id/foto';
    const envio = api.enviarFoto('id', 'v1', arquivo);
    const put = http.expectOne({ method: 'PUT', url: rota });
    expect(put.request.headers.get('If-Match')).toBe('"v1"');
    expect(put.request.headers.get('Authorization')).toBe('Bearer sessao-sintetica');
    expect(put.request.headers.has('Content-Type')).toBe(false);
    expect((put.request.body as FormData).get('arquivo')).toEqual(arquivo);
    put.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v2"' } });
    expect((await envio).etag).toBe('v2');
    const leitura = api.lerFoto('id');
    const get = http.expectOne({ method: 'GET', url: rota });
    expect(get.request.responseType).toBe('blob');
    expect(get.request.headers.get('Authorization')).toBe('Bearer sessao-sintetica');
    const blob = new Blob(['imagem'], { type: 'image/png' });
    get.flush(blob);
    expect(await leitura).toBe(blob);
    http.verify();
  });
});
