import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CONFIGURACAO_API } from '../configuracao-api';
import { AlugueisComponent } from './alugueis.component';
import { AluguelDetalhe, AlugueisService, DemonstrativoEstimado, VeiculoOpcao } from './alugueis.service';
import { formatarEntradaData, retornoDaTela } from './horario-locacao';
import { SessaoService } from '../sessao.service';
import { SaidaCoordenador } from '../saida-coordenador.service';

const veiculo: VeiculoOpcao = { id: 'v1', grupoId: 'g1', grupoNome: 'Compactos', placa: 'FICTICIO', marca: 'Aurora', modelo: 'Lume',
  combustivel: 'Gasolina', capacidadeLitros: '50' };
const plano = { id: 'p1', modalidade: 'Controlado', precoDiario: '145.00', precoKm: '2.50', franquiaDiariaKm: 100 } as const;
const estimativa: DemonstrativoEstimado = { diarias: 2, plano, valorPlano: '290.00', franquiaTotalKm: 200,
  quilometragemPendente: true, adicionais: [], parcelasConhecidas: '290.00', caucao: '1000.00', incompleta: true };
const contrato: AluguelDetalhe = {
  id: 'a1', numeroExibicao: '0001', estado: 'EmAberto', saida: '2026-11-23T09:00:00-03:00', retornoPrevisto: '2026-11-24T09:01:00-03:00',
  hodometroInicial: 500, cliente: { id: 'c1', nome: 'Cliente Exemplo', tipo: 'PF', documentoMascarado: 'CPF •••' },
  condutor: { id: 'co1', nome: 'Condutor Exemplo', validadeCnh: '2026-11-24' },
  veiculo, grupo: { id: 'g1', nome: 'Compactos' }, estimativa,
};
const resumo = {
  id: 'a1', numeroExibicao: '0001', estado: 'EmAberto', condutor: contrato.condutor.nome, veiculoId: 'v1', placa: veiculo.placa,
  marca: veiculo.marca, modelo: veiculo.modelo, plano: plano.modalidade, saida: contrato.saida,
  retornoPrevisto: contrato.retornoPrevisto, parcelasConhecidas: '290.00',
};

describe('Aluguéis F3', () => {
  const api = {
    listar: vi.fn(), obter: vi.fn(), clientes: vi.fn(), condutores: vi.fn(), veiculos: vi.fn(), planos: vi.fn(),
    taxas: vi.fn(), seguro: vi.fn(), estimar: vi.fn(), editar: vi.fn(), criar: vi.fn(),
    preverDevolucao: vi.fn(), confirmarDevolucao: vi.fn(), veiculosDoHistorico: vi.fn(), ocultar: vi.fn(),
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
    const botao = [...el.querySelectorAll('button')].find(x =>
      !x.closest('dialog:not([open])') && x.textContent?.trim() === rotulo);
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
  function simularDialogo(seletor = 'dialog[aria-labelledby="titulo-ocultar"]') {
    const dialogo = el.querySelector<HTMLDialogElement>(seletor)!;
    let origem: HTMLElement | undefined;
    // jsdom não implementa o modal nativo; o foco real é verificado no Chromium.
    dialogo.showModal = () => {
      origem = document.activeElement as HTMLElement;
      dialogo.open = true;
      dialogo.querySelector<HTMLElement>('[autofocus]')?.focus();
    };
    dialogo.close = () => { dialogo.open = false; origem?.focus(); };
    return dialogo;
  }
  beforeEach(async () => {
    for (const mock of Object.values(api)) mock.mockReset();
    api.listar.mockResolvedValue({ items: [resumo], page: 1, pageSize: 20, total: 21 });
    api.obter.mockResolvedValue(detalhe);
    api.clientes.mockResolvedValue([contrato.cliente]); api.condutores.mockResolvedValue([contrato.condutor]);
    api.veiculos.mockResolvedValue([veiculo]); api.planos.mockResolvedValue([plano]);
    api.veiculosDoHistorico.mockResolvedValue([veiculo]); api.ocultar.mockResolvedValue(undefined);
    api.taxas.mockResolvedValue([]); api.seguro.mockResolvedValue({ precoDiario: null, configurado: false });
    api.estimar.mockResolvedValue(estimativa);
    api.editar.mockResolvedValue(detalhe); api.criar.mockResolvedValue(detalhe);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: AlugueisService, useValue: api }] });
    TestBed.inject(SessaoService).definir({ sessaoId: 'sessao', accessToken: 'bearer',
      refreshToken: 'refresh', accessExpiraEm: '2099-01-01T00:00:00Z' });
    fixture = TestBed.createComponent(AlugueisComponent);
    el = fixture.nativeElement;
  });

  it('identifica o detalhe pelo nome completo preservado e pelo código secundário', async () => {
    const nome = 'Empresa de Locação e Serviços de Transporte Rodoviário do Vale Exemplo';
    api.obter.mockResolvedValueOnce({ ...detalhe, dados: { ...contrato, cliente: { ...contrato.cliente, nome } } });
    await render(); clicar('Consultar'); await render();
    expect(el.querySelector('h1')?.textContent?.trim()).toBe(`Aluguel de ${nome}`);
    expect(el.querySelector('.referencia-contrato')?.textContent?.trim()).toBe('Código do contrato: 0001');
    expect(document.activeElement?.id).toBe('titulo-alugueis');
  });

  it('anuncia revogação incerta e oferece reautenticação sem desbloquear gravações', async () => {
    await render();
    const sessao = TestBed.inject(SessaoService);
    const http = TestBed.inject(HttpTestingController);
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const rota = `${CONFIGURACAO_API.url}/session`;
    const tentativa = sessao.revogar(sessao.contexto()!);
    http.expectOne({ method: 'POST', url: rota + '/logout' }).error(new ProgressEvent('error'));
    await vi.waitFor(() => http.expectOne({ method: 'GET', url: rota }).flush({ ativa: true, sessaoId: 'sessao' }));
    await tentativa;
    await render();

    expect(el.querySelector('.saida-incerta[role="alert"]')?.textContent).toContain('Não foi possível confirmar');
    expect([...el.querySelectorAll('button')].find(b => b.textContent?.trim() === 'Nova contratação')?.disabled).toBe(true);
    expect(navegar).not.toHaveBeenCalled();
    clicar('Entrar novamente');
    expect(sessao.autenticada()).toBe(false);
    expect(sessao.aviso()).toContain('Não foi possível confirmar a revogação remota');
    expect(navegar).toHaveBeenCalledExactlyOnceWith('/login');
    http.verify();
  });

  it('distingue consulta, erro, vazio e recuperação, e filtra paginação', async () => {
    api.listar.mockRejectedValueOnce(new Error('indisponível'));
    fixture.detectChanges();
    expect(el.textContent).toContain('Carregando aluguéis');
    await render();
    expect(el.textContent).toContain('Não foi possível consultar os aluguéis');
    api.listar.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 });
    clicar('Tentar novamente'); await render();
    expect(el.textContent).toContain('Nenhum aluguel encontrado');
    await campo('filtro-veiculo', 'v1'); await campo('filtro-de', '23/11/2026'); clicar('Filtrar'); await render();
    expect(api.listar).toHaveBeenLastCalledWith(1, { estado: '', veiculoId: 'v1', saidaDe: '2026-11-23', saidaAte: '', incluirOcultos: undefined });
    clicar('Próxima'); await render();
    expect(api.listar).toHaveBeenLastCalledWith(2, { estado: '', veiculoId: 'v1', saidaDe: '2026-11-23', saidaAte: '', incluirOcultos: undefined });
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
    let rejeitar!: (erro: unknown) => void;
    api.criar.mockImplementationOnce(() => new Promise((_resolve, reject) => rejeitar = reject));
    clicar('Confirmar contratação'); await render();
    const saida = TestBed.inject(SaidaCoordenador);
    const confirmarSaida = vi.fn(async () => false);
    const revogar = vi.spyOn(TestBed.inject(SessaoService), 'revogar');
    expect(saida.ocupada()).toBe(true);
    await saida.solicitar(() => true, confirmarSaida);
    expect(confirmarSaida).not.toHaveBeenCalled();
    rejeitar(new HttpErrorResponse({ status: 503 }));
    await render();
    expect(el.textContent).toContain('Resultado da contratação não confirmado');
    expect(document.activeElement?.id).toBe('erros-contratacao');
    await saida.solicitar(() => true, confirmarSaida);
    expect(confirmarSaida).toHaveBeenCalledWith(expect.stringContaining('repetir a mesma tentativa'));
    expect(revogar).not.toHaveBeenCalled();
    clicar('Repetir confirmação'); await render();
    expect(api.criar.mock.calls).toHaveLength(2);
    expect(api.criar.mock.calls[0]).toEqual(api.criar.mock.calls[1]);
    expect(api.criar.mock.calls[0][0]).toMatchObject({ hodometroInicial: 600, retornoPrevisto: '2026-11-24T09:01:00-03:00' });
    expect(el.textContent).toContain('Contratação do aluguel 0001 confirmada');
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
    const dialogo = simularDialogo('dialog[aria-labelledby="titulo-descarte"]');
    clicar('Recarregar contrato'); await render();
    expect(dialogo.open).toBe(true);
    clicar('Descartar e continuar'); await render();
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

  it('mostra o final confirmado e não oferece edição nem outra devolução', async () => {
    const final = {
      retornoReal: '2026-11-24T09:01:00-03:00', hodometroFinal: 600, litrosRestantes: '40',
      diarias: 2, kmRodados: 100, total: '354.00', caucao: '1000.00',
      plano: { parametros: plano, franquiaTotalKm: 200, kmCobrados: 0, subtotal: { valor: '290.00' } },
      adicionais: [], combustivel: { tipo: 'Gasolina', litrosARepor: '10', precoPorLitro: '6', parcela: { valor: '60.00' } },
    };
    api.preverDevolucao.mockResolvedValue({ etag: 'v1', dados: { versaoAceita: 'p1', demonstrativo: final } });
    api.confirmarDevolucao.mockResolvedValue({ etag: 'v2', dados: { id: 'a1', numeroExibicao: '0001', estado: 'Concluido', final } });
    await render(); clicar('Consultar'); await render(); clicar('Registrar devolução'); await render();
    await campo('devolucao-retornoReal', '24/11/2026 09:01');
    await campo('devolucao-hodometroFinal', '600'); await campo('devolucao-litrosRestantes', '40');
    clicar('Calcular prévia'); await render(); clicar('Confirmar devolução'); await render();
    expect(el.textContent).toContain('Demonstrativo final');
    expect(el.textContent).toContain('Retorno registrado');
    expect(el.textContent).toContain('R$ 354,00');
    expect(el.textContent).not.toContain('Editar aluguel');
    expect(el.textContent).not.toContain('Registrar devolução');
    expect(document.activeElement?.id).toBe('titulo-alugueis');
    expect(el.querySelector('#anuncio-alugueis[role="status"]')?.textContent).toContain('Devolução do aluguel 0001 concluída.');
  });

  it('oculta com ETag, recupera o mesmo final com inclusão explícita e não permite ocultar de novo', async () => {
    const resumoFinal = { ...resumo, id: 'a2', numeroExibicao: '000002', estado: 'Concluido', totalFinal: '354.00' };
    const final = {
      retornoReal: '2026-11-24T09:01:00-03:00', hodometroFinal: 600, litrosRestantes: '40',
      diarias: 2, kmRodados: 100, total: '354.00', caucao: '1000.00',
      plano: { parametros: plano, franquiaTotalKm: 200, kmCobrados: 0, subtotal: { valor: '290.00' } },
      adicionais: [], combustivel: { tipo: 'Gasolina', litrosARepor: '10', precoPorLitro: '6', parcela: { valor: '60.00' } },
    };
    const concluido = { ...contrato, id: 'a2', numeroExibicao: '000002', estado: 'Concluido', final };
    api.listar.mockImplementation(async (_pagina, filtros) => ({
      items: filtros.incluirOcultos ? [{ ...resumoFinal, oculto: true }] :
        api.ocultar.mock.calls.length ? [resumo] : [resumo, resumoFinal],
      page: 1, pageSize: 20, total: filtros.incluirOcultos || api.ocultar.mock.calls.length ? 1 : 2,
    }));
    api.obter.mockImplementation(async (id, incluir) => id === 'a2'
      ? { dados: { ...concluido, oculto: incluir }, etag: incluir ? 'v3' : 'v2' } : detalhe);
    await render();
    expect(el.textContent).toContain('Estimativa');
    expect(el.textContent).toContain('Total final');
    el.querySelector<HTMLButtonElement>('[data-consultar="a2"]')!.click(); await render();
    expect(el.textContent).toContain('R$ 354,00');
    expect(el.textContent).not.toContain('Editar aluguel');
    const acionador = el.querySelector<HTMLButtonElement>('button.perigo')!;
    const dialogo = simularDialogo();
    acionador.focus();
    acionador.click(); await render();
    expect(dialogo.open).toBe(true);
    expect(dialogo.textContent).toContain('O aluguel 000002');
    expect(document.activeElement?.textContent?.trim()).toBe('Cancelar');
    dialogo.dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(dialogo.open).toBe(false);
    expect(api.ocultar).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(acionador);
    acionador.click(); await render();
    dialogo.querySelector<HTMLButtonElement>('button.excluir')!.click(); await render();
    expect(api.ocultar).toHaveBeenCalledWith('a2', 'v2');
    expect(document.activeElement?.id).toBe('titulo-alugueis');
    expect(el.querySelector('#anuncio-alugueis')?.textContent).toContain('Aluguel 000002 ocultado');
    expect(el.querySelector('[data-consultar="a2"]')).toBeNull();
    await campo('filtro-estado', 'Concluido'); await campo('filtro-de', '23/11/2026');
    el.querySelector<HTMLInputElement>('#filtro-ocultos')!.click();
    clicar('Filtrar'); await render();
    expect(api.listar).toHaveBeenLastCalledWith(1, expect.objectContaining({ estado: 'Concluido', incluirOcultos: 'true' }));
    expect(document.activeElement?.id).toBe('resultado-alugueis');
    expect(el.textContent).toContain('Oculto');
    clicar('Consultar'); await render();
    expect(api.obter).toHaveBeenLastCalledWith('a2', true);
    expect(el.textContent).toContain('R$ 354,00');
    expect(el.querySelector('button.perigo')).toBeNull();
    expect(el.textContent).not.toContain('Registrar devolução');
  });

  it('focaliza o erro do filtro ou ETag da ocultação sem oferecer ação indevida', async () => {
    await render(); await campo('filtro-ate', '31/02/2026'); clicar('Filtrar'); await render();
    expect(document.activeElement?.id).toBe('erro-filtro-alugueis');
    expect(el.querySelector('#filtro-ate')?.getAttribute('aria-describedby')).toContain('erro-filtro-ate');
    clicar('Consultar'); await render();
    expect(el.querySelector('button.perigo')).toBeNull();
    fixture.componentInstance.detalhe.set({ etag: 'v2', dados: {
      ...contrato, estado: 'Concluido',
      final: { retornoReal: contrato.retornoPrevisto, hodometroFinal: 500, litrosRestantes: '50',
        diarias: 2, kmRodados: 0,
        plano: { parametros: plano, kmCobrados: 0, franquiaTotalKm: 200, diariasBruto: '290', quilometragemBruto: '0',
          subtotal: { bruto: '290', valor: '290.00' } },
        combustivel: { tipo: 'Gasolina', litrosARepor: '0', precoPorLitro: '6', versaoId: 'c', ordem: 1,
          inicioVigenciaUtc: contrato.saida, parcela: { bruto: '0', valor: '0.00' } },
        versaoRegra: 'RN/v1', total: '290.00', caucao: '1000.00', adicionais: [], multa: null,
      },
    } });
    api.ocultar.mockRejectedValueOnce(new HttpErrorResponse({ status: 412 }));
    await render(); simularDialogo(); clicar('Ocultar aluguel'); await render();
    el.querySelector<HTMLButtonElement>('dialog button.excluir')!.click(); await render();
    expect(document.activeElement?.id).toBe('erro-ocultar-aluguel');
    expect(el.textContent).toContain('Volte à lista e consulte novamente');
    expect(el.querySelector<HTMLButtonElement>('button.perigo')?.disabled).toBe(true);
    expect(el.textContent).toContain('Demonstrativo final');
  });

  it('protege rascunho de edição no menu e em Voltar, mas não protege valores restaurados', async () => {
    await render(); clicar('Consultar'); await render(); clicar('Editar aluguel'); await render();
    const dialogo = simularDialogo('dialog[aria-labelledby="titulo-descarte"]');
    expect(fixture.componentInstance.podeSair()).toBe(true);
    await campo('aluguel-hodometro', '67');
    const saida = TestBed.inject(SaidaCoordenador);
    const cancelar = vi.fn(async () => false);
    const revogar = vi.spyOn(TestBed.inject(SessaoService), 'revogar');
    await saida.solicitar(() => true, cancelar);
    expect(cancelar).toHaveBeenCalledWith(expect.stringContaining('alterações não salvas'));
    expect(revogar).not.toHaveBeenCalled();
    clicar('Voltar ao contrato'); await render();
    expect(dialogo.open).toBe(true);
    expect(document.activeElement?.textContent?.trim()).toBe('Cancelar');
    dialogo.dispatchEvent(new Event('cancel', { cancelable: true }));
    await render();
    expect(el.querySelector<HTMLInputElement>('#aluguel-hodometro')?.value).toBe('67');
    await campo('aluguel-hodometro', '500');
    clicar('Voltar ao contrato'); await render();
    expect(dialogo.open).toBe(false);
    expect(el.querySelector('#aluguel-hodometro')).toBeNull();
  });
});

describe('Horário local da locação', () => {
  it('formata data e hora compactas sem descartar precisão ou offset colados', () => {
    expect(formatarEntradaData('10072027', false)).toBe('10/07/2027');
    expect(formatarEntradaData('100720271430', true)).toBe('10/07/2027 14:30');
    expect(formatarEntradaData('10/07/2027 14:30:00,1234567', true)).toBe('10/07/2027 14:30:00,1234567');
    expect(formatarEntradaData('16/02/2019 23:30 -02:00', true)).toBe('16/02/2019 23:30 -02:00');
    expect(formatarEntradaData('1a', false)).toBe('1a');
  });
  it('rejeita horário inexistente e exige offset quando o horário se repete', () => {
    expect(retornoDaTela('04/11/2018 00:30')).toMatchObject({ erro: expect.stringContaining('não existe') });
    expect(retornoDaTela('16/02/2019 23:30')).toMatchObject({ erro: expect.stringContaining('se repete') });
    expect(retornoDaTela('16/02/2019 23:30 -02:00')).toEqual({ iso: '2019-02-16T23:30:00-02:00' });
  });
});
