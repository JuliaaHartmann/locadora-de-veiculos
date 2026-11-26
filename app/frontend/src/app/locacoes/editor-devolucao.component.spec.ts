import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AluguelAberto, AlugueisService, PlanoContratado } from './alugueis.service';
import type { DemonstrativoDevolucao } from './contratos-devolucao';
import { EditorDevolucaoComponent } from './editor-devolucao.component';
import { SessaoService } from '../sessao.service';
import { SaidaCoordenador } from '../saida-coordenador.service';

const plano: PlanoContratado = { id: 'p1', modalidade: 'Livre', precoDiario: '100', precoKm: null, franquiaDiariaKm: null };
const vinculada = { id: 'fixa', nome: 'Preparação', modalidade: 'Fixa', preco: '30' } as const;
const suporte = { id: 'extra', nome: 'Suporte de viagem', modalidade: 'Diaria', preco: '10' } as const;
const contrato: AluguelAberto = {
  id: 'a1', numeroExibicao: '000001', estado: 'EmAberto', saida: '2026-11-23T10:00:25.1234567-03:00',
  retornoPrevisto: '2026-11-24T16:00:00-03:00', hodometroInicial: 21430,
  cliente: { id: 'p1', nome: 'Contratante Exemplo', tipo: 'PJ', documentoMascarado: 'CNPJ •••' },
  condutor: { id: 'c1', nome: 'Condutora Exemplo', validadeCnh: '2026-11-24' },
  veiculo: { id: 'v1', placa: 'SIM0A08', marca: 'Aurora', modelo: 'Lume', combustivel: 'Gasolina', capacidadeLitros: '50' },
  grupo: { id: 'g1', nome: 'Compactos' },
  estimativa: { diarias: 2, plano, valorPlano: '200.00', franquiaTotalKm: null, quilometragemPendente: false,
    adicionais: [{ origemId: vinculada.id, categoria: 'Taxa', nome: vinculada.nome, modalidade: vinculada.modalidade,
      preco: vinculada.preco, valor: '30.00' }], parcelasConhecidas: '230.00', caucao: '1000.00', incompleta: true },
};

const demonstrativo: DemonstrativoDevolucao = {
  versaoRegra: 'RN-01-09/v1', retornoReal: '2026-11-24T16:00:00-03:00', hodometroFinal: 21680, litrosRestantes: '40',
  diarias: 2, kmRodados: 250,
  plano: { parametros: plano, franquiaTotalKm: null, kmCobrados: 0, diariasBruto: '200', quilometragemBruto: '0',
    subtotal: { bruto: '200', valor: '200.00' } },
  adicionais: [{ origemId: suporte.id, nome: suporte.nome, categoria: 'Taxa', modalidade: 'Diaria', incluidoNoRetorno: true,
    quantidade: 2, preco: '10', parcela: { bruto: '20', valor: '20.00' } }],
  combustivel: { tipo: 'Gasolina', versaoId: '1', inicioVigenciaUtc: '2026-11-23T00:00:00+00:00',
    ordem: 1, litrosARepor: '10', precoPorLitro: '6', parcela: { bruto: '60', valor: '60.00' } },
  multa: null, total: '334.00', caucao: '1000.00',
};

describe('Editor de devolução F4', () => {
  const api = { taxas: vi.fn(), preverDevolucao: vi.fn(), confirmarDevolucao: vi.fn(), obter: vi.fn() };
  let fixture: ComponentFixture<EditorDevolucaoComponent>;
  let el: HTMLElement;
  const original = { dados: contrato, etag: 'r1' };

  async function render() {
    fixture.detectChanges();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable(); fixture.detectChanges();
  }

  function campo(nome: string) { return el.querySelector<HTMLInputElement>(`#devolucao-${nome}`)!; }
  async function digitar(nome: string, valor: string) {
    const input = campo(nome);
    input.value = valor;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await render();
  }
  function botao(rotulo: string) { return [...el.querySelectorAll('button')].find(b => b.textContent?.trim() === rotulo)!; }
  async function calcular() { botao('Calcular prévia').click(); await render(); }
  async function preencher(data = '24/11/2026 16:00', litros = '40,125') {
    await digitar('retornoReal', data); await digitar('hodometroFinal', '21680'); await digitar('litrosRestantes', litros);
  }
  beforeEach(() => {
    api.taxas.mockReset().mockResolvedValue([vinculada, suporte]);
    api.preverDevolucao.mockReset().mockResolvedValue({ dados: { versaoAceita: 'p1', demonstrativo }, etag: 'r1' });
    api.confirmarDevolucao.mockReset().mockResolvedValue({ dados: { id: 'a1', estado: 'Concluido', final: demonstrativo }, etag: 'r2' });
    api.obter.mockReset().mockResolvedValue(original);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), { provide: AlugueisService, useValue: api }] });
    TestBed.inject(SessaoService).definir({ sessaoId: 'sessao', accessToken: 'bearer',
      refreshToken: 'refresh', accessExpiraEm: '2099-01-01T00:00:00Z' });
    fixture = TestBed.createComponent(EditorDevolucaoComponent);
    fixture.componentRef.setInput('original', original);
    const liberar = TestBed.inject(SaidaCoordenador).registrar({ estado: () => fixture.componentInstance.estadoSaida() });
    fixture.componentRef.onDestroy(liberar);
    el = fixture.nativeElement;
  });

  it('mostra as referências preservadas, a opção elegível e demonstra a prévia canônica sem gravar', async () => {
    fixture.detectChanges();
    expect(el.textContent).toContain('Carregando taxas');
    await render();
    for (const nome of ['retornoReal', 'hodometroFinal', 'litrosRestantes'])
      expect(el.querySelector(`label[for="devolucao-${nome}"]`)).toBeTruthy();
    expect(el.textContent).toContain('23/11/2026 às 10:00');
    expect(el.textContent).not.toContain('10:00:25,1234567');
    expect(el.textContent).toContain('Tanque cheio (50 L)');
    expect(el.querySelectorAll('input[type="checkbox"]')).toHaveLength(1);
    expect(el.textContent).toContain('Suporte de viagem');
    expect(el.textContent).not.toContain(vinculada.nome);
    await preencher();
    el.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    await calcular();
    expect(api.preverDevolucao).toHaveBeenCalledWith('a1', {
      retornoReal: '2026-11-24T16:00:00-03:00', hodometroFinal: 21680, litrosRestantes: '40.125', taxaIds: ['extra'],
    });
    expect(el.textContent).toContain('Total proposto');
    expect(el.textContent).toContain('R$ 334,00');
    expect(el.textContent).toContain('R$ 1.000,00');
    expect(document.activeElement?.id).toBe('titulo-calculo-devolucao');
    await digitar('litrosRestantes', '41');
    expect(el.textContent).toContain('A prévia anterior não pode mais ser confirmada');
    expect(el.textContent).not.toContain('Total proposto');
  });

  it('rejeita medições inválidas e associa os erros sem apagar a entrada', async () => {
    await render();
    await preencher('04/11/2018 00:30', '50,0001');
    await digitar('hodometroFinal', '21429');
    await calcular();
    expect(api.preverDevolucao).not.toHaveBeenCalled();
    expect(document.activeElement?.id).toBe('erros-devolucao');
    for (const nome of ['retornoReal', 'hodometroFinal', 'litrosRestantes']) {
      expect(campo(nome).getAttribute('aria-invalid')).toBe('true');
      expect(campo(nome).getAttribute('aria-describedby')).toContain(`erro-devolucao-${nome}`);
    }
    expect(el.querySelector('#erro-devolucao-hodometroFinal')?.textContent).toBe('O hodômetro final não pode ser inferior a 21.430 km.');
    expect(el.querySelector('#ajuda-devolucao-hodometroFinal')).toBeNull();
    await digitar('hodometroFinal', '21680');
    expect(el.querySelector('#ajuda-devolucao-hodometroFinal')?.textContent).toBe('Quilometragem mínima: 21.430 km.');
    expect(campo('litrosRestantes').value).toBe('50,0001');
  });

  it('aceita a saída exata e mantém o tanque cheio quando falta preço', async () => {
    await render();
    await preencher('23/11/2026 10:00:25,1234567', '50');
    api.preverDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status: 400,
      error: { errors: { combustivel: ['Não há preço de Gasolina vigente no retorno real.'] } } }));
    await calcular();
    expect(api.preverDevolucao.mock.calls[0][1].retornoReal).toBe(contrato.saida);
    expect(el.textContent).toContain('Não há preço de Gasolina');
    expect(campo('litrosRestantes').value).toBe('50');
    expect(el.textContent).not.toContain('Total proposto');
  });

  it('distingue falha de opções e recupera a lista sem apagar o formulário', async () => {
    api.taxas.mockRejectedValueOnce(new Error('indisponível'));
    await render();
    await preencher();
    expect(botao('Calcular prévia').disabled).toBe(true);
    expect(el.textContent).toContain('Não foi possível consultar as taxas elegíveis');
    botao('Tentar novamente').click(); await render();
    expect(botao('Calcular prévia').disabled).toBe(false);
    expect(campo('litrosRestantes').value).toBe('40,125');
  });

  it('bloqueia edição na resposta incerta e recupera o final com a mesma intenção', async () => {
    const concluido = vi.fn();
    fixture.componentInstance.concluido.subscribe(concluido);
    await render(); await preencher(); await calcular();
    let rejeitar!: (erro: unknown) => void;
    api.confirmarDevolucao.mockImplementationOnce(() => new Promise((_resolve, reject) => rejeitar = reject));
    botao('Confirmar devolução').click(); await render();
    const saida = TestBed.inject(SaidaCoordenador);
    const cancelar = vi.fn(async () => false);
    const revogar = vi.spyOn(TestBed.inject(SessaoService), 'revogar');
    expect(saida.ocupada()).toBe(true);
    await saida.solicitar(() => true, cancelar);
    expect(cancelar).not.toHaveBeenCalled();
    rejeitar(new HttpErrorResponse({ status: 0 }));
    await render();
    expect(campo('litrosRestantes').disabled).toBe(true);
    expect(botao('Voltar ao contrato').disabled).toBe(true);
    expect(document.activeElement?.id).toBe('erros-devolucao');
    await saida.solicitar(() => true, cancelar);
    expect(cancelar).toHaveBeenCalledWith(expect.stringContaining('repetir a mesma tentativa'));
    expect(revogar).not.toHaveBeenCalled();
    botao('Repetir confirmação').click(); await render();
    expect(api.confirmarDevolucao.mock.calls).toHaveLength(2);
    expect(api.confirmarDevolucao.mock.calls[0]).toEqual(api.confirmarDevolucao.mock.calls[1]);
    expect(api.confirmarDevolucao.mock.calls[0][2]).toBe('r1');
    expect(concluido).toHaveBeenCalledWith({ confirmado: true, resposta: expect.objectContaining({
      etag: 'r2', dados: expect.objectContaining({ estado: 'Concluido', final: demonstrativo }),
    }) });
  });

  it('invalida preço alterado e requer aceitar uma nova prévia', async () => {
    await render(); await preencher(); await calcular();
    api.confirmarDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status: 409,
      error: { errors: { versaoAceita: ['mudou'] } } }));
    botao('Confirmar devolução').click(); await render();
    expect(el.textContent).toContain('Calcule e aceite uma nova prévia');
    expect(campo('litrosRestantes').value).toBe('40,125');
    expect(botao('Confirmar devolução')).toBeUndefined();
    api.preverDevolucao.mockResolvedValueOnce({ dados: { versaoAceita: 'p2', demonstrativo: { ...demonstrativo, total: '354.00' } }, etag: 'r1' });
    await calcular();
    expect(el.textContent).toContain('R$ 354,00');
    expect(api.confirmarDevolucao).toHaveBeenCalledTimes(1);
    botao('Confirmar devolução').click(); await render();
    expect(api.confirmarDevolucao.mock.calls[1][1]).toMatchObject({ versaoAceita: 'p2' });
    expect(api.confirmarDevolucao.mock.calls[1][3]).not.toBe(api.confirmarDevolucao.mock.calls[0][3]);
  });

  it('recarrega revisão explicitamente, preserva os dados digitados e focaliza o retorno', async () => {
    await render(); await preencher(); await calcular();
    api.confirmarDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status: 412 }));
    botao('Confirmar devolução').click(); await render();
    expect(campo('hodometroFinal').value).toBe('21680');
    expect(botao('Calcular prévia').disabled).toBe(true);
    api.obter.mockResolvedValueOnce({ dados: { ...contrato, hodometroInicial: 21500 }, etag: 'r2' });
    botao('Recarregar contrato').click(); await render();
    expect(el.textContent).toContain('21.500 km');
    expect(campo('hodometroFinal').value).toBe('21680');
    expect(document.activeElement?.id).toBe('devolucao-retornoReal');
    expect(botao('Calcular prévia').disabled).toBe(false);
    expect(botao('Confirmar devolução')).toBeUndefined();
  });

  it('distingue falha comprovadamente anterior ao commit de uma resposta incerta', async () => {
    await render(); await preencher(); await calcular();
    api.confirmarDevolucao.mockRejectedValueOnce(new HttpErrorResponse({ status: 503,
      error: { code: 'devolucao_nao_gravada' } }));
    botao('Confirmar devolução').click(); await render();
    expect(el.textContent).toContain('O aluguel continua em aberto');
    expect(botao('Repetir confirmação')).toBeUndefined();
    expect(botao('Confirmar devolução').disabled).toBe(false);
    expect(campo('litrosRestantes').value).toBe('40,125');
  });
});
