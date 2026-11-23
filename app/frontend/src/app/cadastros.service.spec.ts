import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CadastrosService, decimalDoFormulario, formatarData, formatarMoeda } from './cadastros.service';
import { CONFIGURACAO_API } from './configuracao-api';

describe('Contratos de cadastros F2', () => {
  let http: HttpTestingController;
  let servico: CadastrosService;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    servico = TestBed.inject(CadastrosService);
  });
  afterEach(() => http.verify());

  it('obtém detalhe e preserva o ETag para edição condicional', async () => {
    const consulta = servico.obter('/grupos/1');
    http.expectOne(`${CONFIGURACAO_API.url}/grupos/1`)
      .flush({ id: '1', nome: 'Grupo' }, { headers: { ETag: '"versao"' } });
    const detalhe = await consulta;
    expect(detalhe.etag).toBe('versao');
    const envio = servico.editar('/grupos/1', { nome: 'Outro' }, detalhe.etag);
    const pedido = http.expectOne(`${CONFIGURACAO_API.url}/grupos/1`);
    expect(pedido.request.headers.get('If-Match')).toBe('"versao"');
    expect(pedido.request.body).toEqual({ nome: 'Outro' });
    pedido.flush({ id: '1', nome: 'Outro' }, { headers: { ETag: '"nova"' } });
    expect((await envio).etag).toBe('nova');
  });

  it('envia paginação e filtro sem identificador de empresa', async () => {
    const consulta = servico.listar('/veiculos', 2, 20, { grupoId: 'grupo' });
    const pedido = http.expectOne(r => r.url.endsWith('/veiculos'));
    expect(pedido.request.params.get('page')).toBe('2');
    expect(pedido.request.params.get('grupoId')).toBe('grupo');
    expect(pedido.request.params.has('empresaId')).toBe(false);
    pedido.flush({ items: [], page: 2, pageSize: 20, total: 0 });
    expect((await consulta).items).toEqual([]);
  });

  it('converte vírgula por texto e distingue ausência de zero', () => {
    expect(decimalDoFormulario(' 9999999999999,999999 ')).toBe('9999999999999.999999');
    expect(formatarMoeda(null)).toBe('—');
    expect(formatarMoeda('0')).toBe('R$ 0,00');
    expect(formatarMoeda('1.123456')).toBe('R$ 1,123456');
  });

  it('exibe vigências no fuso de São Paulo, inclusive no horário de verão histórico', () => {
    expect(formatarData('2026-11-23T01:30:00Z')).toBe('22/11/2026, 22:30:00');
    expect(formatarData('2018-12-01T02:30:00Z')).toBe('01/12/2018, 00:30:00');
    expect(formatarData(null)).toBe('—');
    expect(formatarData('data-invalida')).toBe('data-invalida');
  });

  it('não limita seletores aos primeiros cem registros', async () => {
    const consulta = servico.listarOpcoes<{ id: string }>('/clientes');
    const primeiros = Array.from({ length: 100 }, (_, id) => ({ id: `${id}` }));
    http.expectOne(r => r.url.endsWith('/clientes') && r.params.get('page') === '1')
      .flush({ items: primeiros, page: 1, pageSize: 100, total: 101 });
    await vi.waitFor(() => {
      http.expectOne(r => r.url.endsWith('/clientes') && r.params.get('page') === '2')
        .flush({ items: [{ id: '100' }], page: 2, pageSize: 100, total: 101 });
    });
    expect((await consulta).length).toBe(101);
  });

  it('mantém o filtro de disponibilidade em todas as páginas do seletor', async () => {
    const consulta = servico.listarOpcoes<{ id: string }>('/veiculos',
      { disponiveis: 'true', aluguelAtualId: 'aluguel' });
    const primeira = http.expectOne(r => r.url.endsWith('/veiculos') && r.params.get('page') === '1');
    expect(primeira.request.params.get('disponiveis')).toBe('true');
    expect(primeira.request.params.get('aluguelAtualId')).toBe('aluguel');
    primeira.flush({ items: [{ id: 'v1' }], page: 1, pageSize: 100, total: 2 });
    await vi.waitFor(() => {
      const segunda = http.expectOne(r => r.url.endsWith('/veiculos') && r.params.get('page') === '2');
      expect(segunda.request.params.get('disponiveis')).toBe('true');
      expect(segunda.request.params.get('aluguelAtualId')).toBe('aluguel');
      segunda.flush({ items: [{ id: 'v2' }], page: 2, pageSize: 100, total: 2 });
    });
    expect(await consulta).toEqual([{ id: 'v1' }, { id: 'v2' }]);
  });
});
