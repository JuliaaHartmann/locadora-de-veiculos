import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CadastrosService } from './cadastros.service';
import { PrecosComponent } from './precos.component';
import { SessaoService } from './sessao.service';
import { SaidaCoordenador } from './saida-coordenador.service';

describe('Preços operacionais F2', () => {
  const listar = vi.fn();
  const obter = vi.fn();
  const criar = vi.fn();
  const editar = vi.fn();
  const excluir = vi.fn();
  const taxa = { id: 't', nome: 'Taxa', preco: '15.25', modalidade: 'Diaria' as const };
  beforeEach(() => {
    listar.mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    obter.mockReset().mockImplementation(async (caminho: string) => ({
      dados: caminho.includes('/seguros/')
        ? { tipo: caminho.split('/').at(-1), configurado: caminho.endsWith('Cliente'), precoDiario: caminho.endsWith('Cliente') ? '0' : null }
        : { tipo: caminho.split('/').at(-1), configurado: true, id: 'vigente', preco: '5', ordem: 22 },
      etag: 'versao',
    }));
    editar.mockReset().mockResolvedValue({ dados: {}, etag: 'nova' });
    criar.mockReset().mockResolvedValue({});
    excluir.mockReset().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: CadastrosService, useValue: { listar, obter, criar, editar, excluir } }],
    });
    TestBed.inject(SessaoService).definir({
      sessaoId: 'teste', accessToken: 'teste', refreshToken: 'teste', accessExpiraEm: '2099-01-01T00:00:00Z',
    });
  });

  it('apresenta zero explícito e ausência independentemente e preenche a edição', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    fixture.componentInstance.aba.set('seguros');
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('R$ 0,00 por dia');
    expect(fixture.nativeElement.textContent).toContain('Preço não configurado');
    await fixture.componentInstance.configurarSeguro('Cliente');
    fixture.detectChanges();
    expect(fixture.componentInstance.seguroModelo().precoDiario).toBe('0');
    expect(fixture.componentInstance.etag()).toBe('versao');
    expect(fixture.nativeElement.querySelector('#seguro-preco').value).toBe('0');
  });

  it('rejeita preço negativo, preserva a entrada e foca o resumo', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    fixture.componentInstance.novaTaxa();
    fixture.componentInstance.taxaModelo.set({ nome: 'Taxa', preco: '-1', modalidade: 'Fixa' });
    fixture.detectChanges();
    await fixture.componentInstance.salvarTaxa();
    fixture.detectChanges();
    expect(editar).not.toHaveBeenCalled();
    expect(fixture.componentInstance.taxaModelo().preco).toBe('-1');
    const campo = fixture.nativeElement.querySelector('#taxa-preco');
    expect(campo.getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
  });

  it('não identifica o primeiro item de página posterior como versão vigente', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    listar.mockResolvedValue({
      items: [{ id: 'anterior', tipo: 'Gasolina', preco: '4', inicioVigenciaUtc: '2026-11-22T12:00:00Z', ordem: 1 }],
      page: 2, pageSize: 20, total: 21,
    });
    fixture.componentInstance.aba.set('combustiveis');
    fixture.componentInstance.historicoPagina.set(2);
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('tbody').textContent).toContain('Versão anterior');
    expect(fixture.nativeElement.querySelector('tbody').textContent).not.toContain('Vigente');
    expect(fixture.componentInstance.estadoCombustivel('Gasolina')?.preco).toBe('5');
  });

  it('busca o ETag antes de abrir combustível mesmo com estado ainda não carregado', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    expect(fixture.componentInstance.combustiveis()).toEqual({});
    await fixture.componentInstance.adicionarPreco('Gasolina');
    expect(obter).toHaveBeenCalledWith('/configuracoes/combustiveis/Gasolina');
    expect(fixture.componentInstance.etag()).toBe('versao');
    expect(fixture.componentInstance.modo()).toBe('combustivel');
    fixture.componentInstance.combustivelModelo.set({ preco: '6' });
    await fixture.componentInstance.salvarCombustivel();
    expect(editar).toHaveBeenCalledWith('/configuracoes/combustiveis/Gasolina', { preco: '6' }, 'versao');
  });

  it('preenche edição de taxa e salva com versão lida', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    obter.mockResolvedValue({ dados: taxa, etag: 'vTaxa' });
    await fixture.componentInstance.editarTaxa('t');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#taxa-nome').value).toBe('Taxa');
    expect(fixture.nativeElement.querySelector('#taxa-preco').value).toBe('15.25');
    await fixture.componentInstance.salvarTaxa();
    expect(editar).toHaveBeenCalledWith('/taxas-servicos/t',
      { nome: 'Taxa', preco: '15.25', modalidade: 'Diaria' }, 'vTaxa');
  });

  it('distingue falha de consulta de ausência e permite recuperar', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    fixture.componentInstance.aba.set('seguros');
    obter.mockRejectedValueOnce(new Error('Indisponível'));
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
    expect(fixture.nativeElement.textContent).not.toContain('Preço não configurado');
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Preço não configurado');
  });

  it('mantém a lista quando uma taxa não pode ser editada por aluguel em aberto', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    const mensagem = 'Esta taxa está vinculada a um aluguel em aberto e não pode ser editada.';
    obter.mockRejectedValueOnce(new HttpErrorResponse({ status: 409, error: { detail: mensagem } }));
    await fixture.componentInstance.editarTaxa('t');
    fixture.detectChanges();
    expect(obter).toHaveBeenCalledWith('/taxas-servicos/t?paraEdicao=true');
    expect(fixture.componentInstance.modo()).toBe('lista');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
    expect(document.activeElement?.textContent).toContain(mensagem);
  });

  it('só exclui depois de confirmar e restaura o foco ao cancelar', async () => {
    listar.mockResolvedValue({ items: [taxa], page: 1, pageSize: 20, total: 1 });
    obter.mockResolvedValue({ dados: taxa, etag: 'vTaxa' });
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    const origem = fixture.nativeElement.querySelector('button[aria-label="Excluir taxa Taxa"]');
    const dialogo = fixture.nativeElement.querySelector('dialog');
    const cancelar = dialogo.querySelector('[autofocus]');
    const confirmar = dialogo.querySelector('.excluir');
    dialogo.showModal = vi.fn(() => {
      dialogo.setAttribute('open', '');
      cancelar.focus();
    });
    dialogo.close = vi.fn(() => dialogo.removeAttribute('open'));
    origem.focus();
    origem.click();
    fixture.detectChanges();
    expect(dialogo.showModal).toHaveBeenCalledOnce();
    expect(document.activeElement).toBe(cancelar);
    expect(obter).not.toHaveBeenCalled();
    cancelar.click();
    expect(document.activeElement).toBe(origem);
    expect(excluir).not.toHaveBeenCalled();
    origem.click();
    fixture.detectChanges();
    confirmar.click();
    await fixture.whenStable();
    expect(excluir).toHaveBeenCalledExactlyOnceWith('/taxas-servicos/t', 'vTaxa');
    expect(fixture.nativeElement.querySelector('#aviso-precos').textContent).toContain('Taxa excluída: Taxa');
  });

  it('bloqueia Sair e envio duplicado em gravação e preserva rascunho no cancelamento', async () => {
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    const componente = fixture.componentInstance;
    const saida = TestBed.inject(SaidaCoordenador);
    componente.novaTaxa();
    componente.taxaModelo.set({ nome: 'Rascunho', preco: '1', modalidade: 'Fixa' });
    const confirmar = vi.fn().mockResolvedValue(false);
    await saida.solicitar(() => true, confirmar);
    expect(confirmar).toHaveBeenCalledExactlyOnceWith(expect.stringContaining('alterações não salvas'));
    TestBed.inject(HttpTestingController).expectNone(() => true);
    expect(componente.taxaModelo().nome).toBe('Rascunho');

    let liberar!: () => void;
    criar.mockReturnValueOnce(new Promise<void>(resolve => liberar = resolve));
    const operacao = componente.salvarTaxa();
    await componente.salvarTaxa();
    expect(criar).toHaveBeenCalledOnce();
    expect(saida.ocupada()).toBe(true);
    expect(componente.podeNavegar()).toBe(false);
    await saida.solicitar(() => true, confirmar);
    expect(confirmar).toHaveBeenCalledOnce();
    TestBed.inject(HttpTestingController).expectNone(() => true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#taxa-preco').disabled).toBe(true);
    liberar();
    await operacao;
    expect(saida.ocupada()).toBe(false);
  });

  it('preserva a entrada num conflito e consulta a versão sem repetir a gravação', async () => {
    obter.mockResolvedValue({ dados: taxa, etag: 'v1' });
    editar.mockRejectedValueOnce(new HttpErrorResponse({ status: 412 }));
    const fixture = TestBed.createComponent(PrecosComponent);
    await fixture.whenStable();
    const componente = fixture.componentInstance;
    await componente.editarTaxa(taxa.id);
    componente.taxaModelo.update(modelo => ({ ...modelo, preco: '2' }));
    await componente.salvarTaxa();
    expect(componente.conflito()).toBe(true);
    await componente.salvarTaxa();
    expect(editar).toHaveBeenCalledOnce();
    obter.mockResolvedValue({ dados: { ...taxa, preco: '3' }, etag: 'v2' });
    await componente.recarregarVersao();
    expect(componente.taxaModelo().preco).toBe('2');
    expect(editar).toHaveBeenCalledOnce();
    await componente.salvarTaxa();
    expect(editar).toHaveBeenLastCalledWith('/taxas-servicos/t', { nome: 'Taxa', preco: '2', modalidade: 'Diaria' }, 'v2');
  });
});
