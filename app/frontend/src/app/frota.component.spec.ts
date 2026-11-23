import { TestBed } from '@angular/core/testing';
import { CadastrosService } from './cadastros.service';
import { FrotaComponent } from './frota.component';

describe('Frota F2', () => {
  const listar = vi.fn();
  const obter = vi.fn();
  const criar = vi.fn();
  const editar = vi.fn();
  const excluir = vi.fn();
  const grupo = { id: 'g1', nome: 'Grupo', planos: [] };
  beforeEach(() => {
    listar.mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    obter.mockReset();
    criar.mockReset().mockResolvedValue({});
    editar.mockReset().mockResolvedValue({});
    excluir.mockReset().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [{ provide: CadastrosService, useValue: {
        listar, obter, criar, editar, excluir, listarOpcoes: async () => [grupo],
      } }],
    });
  });

  it('mostra vazio, falha com tentar novamente e recupera a consulta', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Nenhum registro encontrado.');
    listar.mockRejectedValueOnce(new Error('Indisponível'));
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
    expect(fixture.nativeElement.textContent).not.toContain('Nenhum registro encontrado.');
    await fixture.componentInstance.carregar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nenhum registro encontrado.');
  });

  it.each(['Diario', 'Controlado', 'Livre'] as const)('solicita somente parâmetros de %s', async modalidade => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    fixture.componentInstance.aba.set('planos');
    fixture.componentInstance.novo();
    fixture.componentInstance.planoModelo.update(x => ({ ...x, modalidade }));
    fixture.detectChanges();
    expect(!!fixture.nativeElement.querySelector('#plano-km')).toBe(modalidade !== 'Livre');
    expect(!!fixture.nativeElement.querySelector('#plano-franquia')).toBe(modalidade === 'Controlado');
    expect(fixture.nativeElement.querySelector('#plano-diario')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[type=file]')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Relógio sintético');
  });

  it('preenche edição de plano e mantém grupo/modalidade', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    fixture.componentInstance.aba.set('planos');
    obter.mockResolvedValueOnce({ dados: {
      id: 'p1', grupoId: 'g1', grupoNome: 'Grupo', modalidade: 'Controlado',
      precoDiario: '100', precoKm: '2.5', franquiaDiariaKm: 200,
    }, etag: 'versao' });
    await fixture.componentInstance.editarPlano('p1');
    fixture.detectChanges();
    expect(fixture.componentInstance.planoModelo().franquiaDiariaKm).toBe('200');
    expect(fixture.nativeElement.querySelector('#plano-km').value).toBe('2.5');
    expect(fixture.nativeElement.querySelector('select#plano-grupo')).toBeNull();
    expect(fixture.nativeElement.querySelector('select#plano-modalidade')).toBeNull();
    await fixture.componentInstance.salvarPlano();
    expect(editar).toHaveBeenCalledWith('/planos/p1',
      { precoDiario: '100', precoKm: '2.5', franquiaDiariaKm: 200 }, 'versao');
  });

  it('associa erro obrigatório, preserva dados e foca resumo', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    fixture.componentInstance.novo();
    fixture.detectChanges();
    await fixture.componentInstance.salvarGrupo();
    fixture.detectChanges();
    expect(criar).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('#grupo-nome').getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
  });

  it('preenche todos os campos de veículo, aplica filtro e agrupa apenas a página', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    fixture.componentInstance.aba.set('veiculos');
    const veiculo = { id: 'v1', grupoId: 'g1', grupoNome: 'Grupo', placa: 'FIC1A23', marca: 'Marca',
      modelo: 'Modelo', cor: 'Azul', ano: 2020, combustivel: 'Gasolina', capacidadeLitros: '50.5' };
    obter.mockResolvedValueOnce({ dados: veiculo, etag: 'v' });
    await fixture.componentInstance.editarVeiculo('v1');
    fixture.detectChanges();
    for (const [id, valor] of Object.entries({ placa: 'FIC1A23', marca: 'Marca', modelo: 'Modelo',
      cor: 'Azul', ano: '2020', capacidade: '50.5' }))
      expect(fixture.nativeElement.querySelector(`#veiculo-${id}`).value).toBe(valor);
    fixture.componentInstance.cancelar();
    fixture.componentInstance.filtroGrupo.set('g1');
    listar.mockResolvedValueOnce({ items: [veiculo], page: 1, pageSize: 20, total: 30 });
    await fixture.componentInstance.carregar();
    fixture.componentInstance.agrupar.set(true);
    fixture.detectChanges();
    expect(listar).toHaveBeenLastCalledWith('/veiculos', 1, 20, { grupoId: 'g1' });
    expect(fixture.componentInstance.veiculosAgrupados()[0].itens.length).toBe(1);
    expect(fixture.nativeElement.querySelector('th[scope=rowgroup]').textContent).toContain('Grupo');
    expect(fixture.nativeElement.textContent).toContain('30 registros');
  });

  it('exclui com a versão lida e atualiza a listagem', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    obter.mockResolvedValueOnce({ dados: grupo, etag: 'v1' });
    await fixture.componentInstance.excluirGrupo(grupo);
    expect(excluir).toHaveBeenCalledWith('/grupos/g1', 'v1');
    expect(fixture.componentInstance.aviso()).toContain('excluído');
  });

  it('ignora resposta de aba anterior que chega depois da consulta atual', async () => {
    const fixture = TestBed.createComponent(FrotaComponent);
    await fixture.whenStable();
    let resolver!: (valor: unknown) => void;
    listar.mockImplementationOnce(() => new Promise(resolve => { resolver = resolve; }));
    const antiga = fixture.componentInstance.carregar();
    fixture.componentInstance.aba.set('veiculos');
    listar.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 2 });
    await fixture.componentInstance.carregar();
    resolver({ items: [grupo], page: 1, pageSize: 20, total: 999 });
    await antiga;
    expect(fixture.componentInstance.total()).toBe(2);
    expect(fixture.componentInstance.carregando()).toBe(false);
  });
});
