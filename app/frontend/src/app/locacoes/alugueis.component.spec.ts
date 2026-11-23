import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlugueisComponent } from './alugueis.component';
import { AluguelDetalhe, AlugueisService, DemonstrativoEstimado, VeiculoOpcao } from './alugueis.service';
import { retornoDaTela } from './horario-locacao';

const veiculo: VeiculoOpcao = { id: 'v1', grupoId: 'g1', grupoNome: 'Compactos', placa: 'FICTICIO', marca: 'Aurora', modelo: 'Lume',
  combustivel: 'Gasolina', capacidadeLitros: '50' };
const plano = { id: 'p1', modalidade: 'Controlado', precoDiario: '145.00', precoKm: '2.50', franquiaDiariaKm: 100 } as const;
const estimativa: DemonstrativoEstimado = { diarias: 2, plano, valorPlano: '290.00', franquiaTotalKm: 200,
  quilometragemPendente: true, adicionais: [], parcelasConhecidas: '290.00', caucao: '1000.00', incompleta: true };
const contrato: AluguelDetalhe = {
  id: 'a1', estado: 'EmAberto', saida: '2026-11-23T09:00:00-03:00', retornoPrevisto: '2026-11-24T09:01:00-03:00',
  hodometroInicial: 500, cliente: { id: 'c1', nome: 'Cliente Exemplo', tipo: 'PF', documentoMascarado: 'CPF •••' },
  condutor: { id: 'co1', nome: 'Condutor Exemplo', validadeCnh: '2026-11-24' },
  veiculo, grupo: { id: 'g1', nome: 'Compactos' }, estimativa,
};
const resumo = {
  id: 'a1', estado: 'EmAberto', condutor: contrato.condutor.nome, veiculoId: 'v1', placa: veiculo.placa,
  marca: veiculo.marca, modelo: veiculo.modelo, plano: plano.modalidade, saida: contrato.saida,
  retornoPrevisto: contrato.retornoPrevisto, parcelasConhecidas: '290.00',
};

describe('Aluguéis F3', () => {
  const api = {
    listar: vi.fn(), obter: vi.fn(), clientes: vi.fn(), condutores: vi.fn(), veiculos: vi.fn(), planos: vi.fn(),
    taxas: vi.fn(), seguro: vi.fn(), estimar: vi.fn(), editar: vi.fn(), criar: vi.fn(),
  };
  let fixture: ComponentFixture<AlugueisComponent>;
  let el: HTMLElement;
  const detalhe = { dados: contrato, etag: 'v1' };

  async function render() {
    fixture.detectChanges();
    // As promises dos doubles não são rastreadas pelo HttpClient no modo sem Zone.js.
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable(); fixture.detectChanges();
  }
  function clicar(rotulo: string) {
    const botao = [...el.querySelectorAll('button')].find(x => x.textContent?.trim() === rotulo);
    expect(botao, rotulo).toBeTruthy();
    botao!.click();
  }
  async function campo(id: string, valor: string) {
    const input = el.querySelector<HTMLInputElement | HTMLSelectElement>(`#${id}`)!;
    input.value = valor;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
    await render();
  }
  beforeEach(async () => {
    for (const mock of Object.values(api)) mock.mockReset();
    api.listar.mockResolvedValue({ items: [resumo], page: 1, pageSize: 20, total: 21 });
    api.obter.mockResolvedValue(detalhe);
    api.clientes.mockResolvedValue([contrato.cliente]); api.condutores.mockResolvedValue([contrato.condutor]);
    api.veiculos.mockResolvedValue([veiculo]); api.planos.mockResolvedValue([plano]);
    api.taxas.mockResolvedValue([]); api.seguro.mockResolvedValue({ precoDiario: null, configurado: false });
    api.estimar.mockResolvedValue(estimativa);
    api.editar.mockResolvedValue(detalhe); api.criar.mockResolvedValue(detalhe);
    TestBed.configureTestingModule({ providers: [{ provide: AlugueisService, useValue: api }] });
    fixture = TestBed.createComponent(AlugueisComponent);
    el = fixture.nativeElement;
  });

  it('distingue consulta, erro, vazio e recuperação, e filtra paginação', async () => {
    api.listar.mockRejectedValueOnce(new Error('indisponível'));
    fixture.detectChanges();
    expect(el.textContent).toContain('Carregando aluguéis');
    await render();
    expect(el.textContent).toContain('Não foi possível consultar os aluguéis');
    api.listar.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 });
    clicar('Tentar novamente'); await render();
    expect(el.textContent).toContain('Nenhum aluguel em aberto encontrado');
    await campo('filtro-veiculo', 'v1'); await campo('filtro-de', '23/11/2026'); clicar('Filtrar'); await render();
    expect(api.listar).toHaveBeenLastCalledWith(1, { veiculoId: 'v1', saidaDe: '2026-11-23', saidaAte: '' });
    clicar('Próxima'); await render();
    expect(api.listar).toHaveBeenLastCalledWith(2, { veiculoId: 'v1', saidaDe: '2026-11-23', saidaAte: '' });
    clicar('Consultar'); await render();
    expect(el.textContent).toContain('Demonstrativo estimado');
    expect(el.textContent).toContain('Pendente');
    expect(el.textContent).toContain('R$ 1.000,00');
    expect(el.textContent).toContain('23/11/2026 às 09:00');
  });

  it('carrega seleções vinculadas, invalida a prévia e repete a mesma confirmação', async () => {
    await render(); clicar('Nova contratação'); await render();
    await campo('aluguel-cliente', 'c1'); await campo('aluguel-condutor', 'co1');
    await campo('aluguel-veiculo', 'v1'); await campo('aluguel-plano', 'p1');
    await campo('aluguel-retorno', '24/11/2026 09:01'); await campo('aluguel-hodometro', '500');
    expect(api.condutores).toHaveBeenCalledWith('c1');
    expect(api.planos).toHaveBeenCalledWith('g1');
    expect(api.veiculos).toHaveBeenCalledWith(true, undefined);
    clicar('Calcular estimativa'); await render();
    expect(el.textContent).toContain('R$ 290,00');
    expect(document.activeElement?.id).toBe('titulo-demonstrativo');
    await campo('aluguel-hodometro', '600');
    expect(el.querySelector<HTMLButtonElement>('#confirmar-aluguel')!.disabled).toBe(true);
    clicar('Calcular estimativa'); await render();
    api.criar.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    clicar('Confirmar contratação'); await render();
    expect(el.textContent).toContain('Resultado da contratação não confirmado');
    expect(document.activeElement?.id).toBe('erros-contratacao');
    clicar('Repetir confirmação'); await render();
    expect(api.criar.mock.calls).toHaveLength(2);
    expect(api.criar.mock.calls[0]).toEqual(api.criar.mock.calls[1]);
    expect(api.criar.mock.calls[0][0]).toMatchObject({ hodometroInicial: 600, retornoPrevisto: '2026-11-24T09:01:00-03:00' });
    expect(el.textContent).toContain('Contratação confirmada');
  });

  it('conserva dados da edição até recarregar após ETag divergente', async () => {
    await render(); clicar('Consultar'); await render(); clicar('Editar aluguel'); await render();
    await campo('aluguel-hodometro', '600');
    api.editar.mockRejectedValueOnce(new HttpErrorResponse({ status: 412 }));
    clicar('Salvar alterações'); await render();
    expect(el.textContent).toContain('A edição não foi gravada');
    expect(el.querySelector<HTMLInputElement>('#aluguel-hodometro')!.value).toBe('600');
    expect(document.activeElement?.id).toBe('erros-contratacao');
    api.obter.mockResolvedValueOnce({ ...detalhe, etag: 'v2' });
    clicar('Recarregar contrato'); await render();
    expect(el.querySelector<HTMLInputElement>('#aluguel-hodometro')!.value).toBe('500');
    expect(api.editar.mock.calls).toHaveLength(1);
    clicar('Salvar alterações'); await render();
    expect(api.editar).toHaveBeenLastCalledWith('a1', expect.objectContaining({ hodometroInicial: 500 }), 'v2');
  });

  it('retira veículo ocupado após conflito sem apagar outros dados', async () => {
    await render(); clicar('Nova contratação'); await render();
    await campo('aluguel-cliente', 'c1'); await campo('aluguel-condutor', 'co1');
    await campo('aluguel-veiculo', 'v1'); await campo('aluguel-plano', 'p1');
    await campo('aluguel-retorno', '24/11/2026 09:01'); await campo('aluguel-hodometro', '500');
    clicar('Calcular estimativa'); await render();
    api.veiculos.mockResolvedValueOnce([]);
    api.criar.mockRejectedValueOnce(new HttpErrorResponse({ status: 409,
      error: { errors: { veiculoId: ['O veículo não está mais disponível.'] } } }));
    clicar('Confirmar contratação'); await render();
    expect(el.querySelector('#erro-aluguel-veiculo')?.textContent).toContain('não está mais disponível');
    expect(el.querySelector<HTMLInputElement>('#aluguel-hodometro')!.value).toBe('500');
    expect(el.querySelector<HTMLInputElement>('#aluguel-retorno')!.value).toBe('24/11/2026 09:01');
    expect(el.querySelector<HTMLSelectElement>('#aluguel-veiculo')!.value).toBe('');
    expect(el.querySelector<HTMLButtonElement>('#confirmar-aluguel')!.disabled).toBe(true);
    expect(document.activeElement?.id).toBe('erros-contratacao');
  });
});

describe('Horário local da locação', () => {
  it('rejeita horário inexistente e exige offset quando o horário se repete', () => {
    expect(retornoDaTela('04/11/2018 00:30')).toMatchObject({ erro: expect.stringContaining('não existe') });
    expect(retornoDaTela('16/02/2019 23:30')).toMatchObject({ erro: expect.stringContaining('se repete') });
    expect(retornoDaTela('16/02/2019 23:30 -02:00')).toEqual({ iso: '2019-02-16T23:30:00-02:00' });
  });
});
