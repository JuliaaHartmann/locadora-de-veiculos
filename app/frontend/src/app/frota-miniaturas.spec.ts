import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from './api.interceptor';
import { CadastrosService } from './cadastros.service';
import { CONFIGURACAO_API } from './configuracao-api';
import { FrotaComponent } from './frota.component';
import { SessaoService } from './sessao.service';

const veiculo = (n: number, grupoId = 'g1') => ({
  id: `id-${n}`, placa: `FIC00${n}`, grupoId, grupoNome: grupoId === 'g1' ? 'Grupo A' : 'Grupo B',
  marca: 'Exemplo', modelo: 'Lume', cor: 'Azul', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50',
});
const rota = (n: number) => `${CONFIGURACAO_API.url}/veiculos/id-${n}/foto`;
const imagem = new Blob(['imagem sintética'], { type: 'image/png' });

class ObservadorDeTeste {
  static readonly itens = new Map<Element, ObservadorDeTeste>();
  alvo?: Element;
  constructor(private readonly callback: IntersectionObserverCallback) {}
  observe(alvo: Element) {
    this.alvo = alvo;
    ObservadorDeTeste.itens.set(alvo, this);
  }
  disconnect() {
    if (this.alvo) ObservadorDeTeste.itens.delete(this.alvo);
  }
  avisar(isIntersecting: boolean) {
    this.callback([{ isIntersecting, target: this.alvo } as IntersectionObserverEntry],
      this as unknown as IntersectionObserver);
  }
}

describe('Frota — miniaturas autenticadas', () => {
  const autenticada = signal(true);
  let dados: ReturnType<typeof veiculo>[];
  let http: HttpTestingController;
  let f: ComponentFixture<FrotaComponent>;
  let criarUrl: ReturnType<typeof vi.fn>;
  let revogarUrl: ReturnType<typeof vi.fn>;
  const descritores = Object.getOwnPropertyDescriptors(URL);

  beforeEach(() => {
    autenticada.set(true);
    dados = [];
    vi.stubGlobal('IntersectionObserver', ObservadorDeTeste);
    criarUrl = vi.fn(() => 'blob:https://app.invalid/miniatura');
    revogarUrl = vi.fn();
    Object.defineProperties(URL, {
      createObjectURL: { configurable: true, value: criarUrl },
      revokeObjectURL: { configurable: true, value: revogarUrl },
    });
    TestBed.configureTestingModule({ providers: [
      provideRouter([]), provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting(),
      { provide: SessaoService, useValue: { autenticada, obterAccessToken: () => 'sessao-sintetica' } },
    ] });
    http = TestBed.inject(HttpTestingController);
    const cadastros = TestBed.inject(CadastrosService);
    vi.spyOn(cadastros, 'listar').mockImplementation(async (caminho, page, pageSize, filtros) => {
      const itens = caminho === '/veiculos'
        ? dados.filter(v => !filtros?.['grupoId'] || filtros['grupoId'] === v.grupoId) : [];
      return { items: itens as never[], page: page ?? 1, pageSize: pageSize ?? 20, total: itens.length };
    });
    vi.spyOn(cadastros, 'listarOpcoes').mockResolvedValue([{ id: 'g1', nome: 'Grupo A' }, { id: 'g2', nome: 'Grupo B' }]);
  });

  afterEach(() => {
    f?.destroy();
    http.verify();
    ObservadorDeTeste.itens.clear();
    vi.unstubAllGlobals();
    for (const chave of ['createObjectURL', 'revokeObjectURL'] as const) {
      if (descritores[chave]) Object.defineProperty(URL, chave, descritores[chave]);
      else Reflect.deleteProperty(URL, chave);
    }
  });

  async function montar() {
    f = TestBed.createComponent(FrotaComponent);
    await f.whenStable();
    f.componentInstance.trocarAba('veiculos');
    await f.whenStable();
    f.detectChanges();
  }

  const vista = () => f.nativeElement as HTMLElement;
  const linha = (n: number) => [...vista().querySelectorAll('tbody tr')].find(tr => tr.textContent?.includes(`FIC00${n}`))!;
  const caixa = (n: number) => linha(n).querySelector('app-foto-miniatura')!;
  const foto = (n: number) => http.expectOne({ method: 'GET', url: rota(n) });
  const avancarFila = async () => { await Promise.resolve(); };
  const avistar = (n: number, visivel = true) => ObservadorDeTeste.itens.get(caixa(n))!.avisar(visivel);

  it('só busca linhas próximas e mantém tabela e ações disponíveis em todos os estados', async () => {
    dados = [veiculo(1), veiculo(2), veiculo(3)];
    await montar();
    expect(vista().querySelector('thead th')?.textContent).toBe('Foto');
    expect(linha(1).textContent).toContain('Editar');
    expect(caixa(1).textContent).toContain('Carregando…');
    avistar(1, false);
    await avancarFila();
    http.expectNone(rota(1));
    [1, 2, 3].forEach(n => avistar(n));
    await avancarFila();
    const leitura = foto(1);
    expect(leitura.request.headers.get('Authorization')).toBe('Bearer sessao-sintetica');
    expect(leitura.request.responseType).toBe('blob');
    leitura.flush(imagem);
    foto(2).flush(null, { status: 404, statusText: 'Not Found' });
    foto(3).flush(null, { status: 503, statusText: 'Unavailable' });
    f.detectChanges();
    const img = caixa(1).querySelector('img')!;
    expect(img.getAttribute('src')).toBe('blob:https://app.invalid/miniatura');
    expect(img.alt).toBe('Foto do veículo FIC001');
    expect(caixa(2).textContent).toContain('Sem foto');
    expect(caixa(2).querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
    expect(caixa(3).textContent).toContain('Foto indisponível');
    expect(linha(3).textContent).toContain('Editar');
    expect(vista().querySelector('[role=alert]:not([hidden])')).toBeNull();
    img.dispatchEvent(new Event('error'));
    f.detectChanges();
    expect(revogarUrl).toHaveBeenCalledExactlyOnceWith('blob:https://app.invalid/miniatura');
    expect(caixa(1).textContent).toContain('Foto indisponível');
    http.expectNone(rota(1));
  });

  it('limita a quatro leituras e cancela ativas e enfileiradas ao filtrar a lista', async () => {
    dados = [1, 2, 3, 4, 5, 6, 7].map(n => veiculo(n));
    dados.push(veiculo(8, 'g2'));
    await montar();
    [1, 2, 3, 4, 5, 6, 7].forEach(n => avistar(n));
    await avancarFila();
    const primeiras = [1, 2, 3, 4].map(foto);
    http.expectNone(rota(5));
    primeiras[0].flush(imagem);
    await avancarFila();
    const quinta = foto(5);
    f.detectChanges();
    expect(caixa(1).querySelector('img')).not.toBeNull();

    const carregar = vi.spyOn(f.componentInstance, 'carregar');
    const seletor = vista().querySelector<HTMLSelectElement>('#filtro-grupo')!;
    seletor.value = 'g2';
    seletor.dispatchEvent(new Event('change'));
    expect(f.componentInstance.filtroGrupo()).toBe('g2');
    f.detectChanges();
    expect(primeiras.slice(1).every(req => req.cancelled)).toBe(true);
    expect(quinta.cancelled).toBe(true);
    expect(revogarUrl).toHaveBeenCalledExactlyOnceWith('blob:https://app.invalid/miniatura');
    await carregar.mock.results[0].value;
    f.detectChanges();
    expect(f.componentInstance.veiculos().map(v => v.id)).toEqual(['id-8']);
    http.expectNone(rota(6));
    http.expectNone(rota(7));
    expect(linha(1)).toBeUndefined();
    avistar(8);
    await avancarFila();
    const oitava = foto(8);
    f.destroy();
    expect(oitava.cancelled).toBe(true);
  });

  it('funciona na tabela agrupada e limpa URLs e leituras ao encerrar sessão', async () => {
    dados = [veiculo(1), veiculo(2, 'g2')];
    await montar();
    const guardar = vi.spyOn(Storage.prototype, 'setItem');
    vista().querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    f.detectChanges();
    expect(vista().querySelectorAll('th[scope=rowgroup][colspan="8"]')).toHaveLength(2);
    [1, 2].forEach(n => avistar(n));
    await avancarFila();
    foto(1).flush(imagem);
    const pendente = foto(2);
    f.detectChanges();
    expect(caixa(1).querySelector('img')?.alt).toBe('Foto do veículo FIC001');
    autenticada.set(false);
    f.detectChanges();
    await f.whenStable();
    expect(pendente.cancelled).toBe(true);
    expect(revogarUrl).toHaveBeenCalledExactlyOnceWith('blob:https://app.invalid/miniatura');
    expect(caixa(1).querySelector('img')).toBeNull();
    expect(guardar).not.toHaveBeenCalled();
    guardar.mockRestore();
  });
});
