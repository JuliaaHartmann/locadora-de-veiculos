import { TestBed } from '@angular/core/testing';
import { CadastrosService } from './cadastros.service';
import { PessoasComponent } from './pessoas.component';

describe('Pessoas F2', () => {
  const listar = vi.fn();
  const obter = vi.fn();
  const criar = vi.fn();
  const editar = vi.fn();
  const adicionar = vi.fn();
  const remover = vi.fn();
  const pf = { id: 'pf', tipo: 'PF' as const, nome: 'Pessoa', cpf: '52998224725', rg: 'RG',
    cnh: 'CNH', telefone: 'Tel', email: null, cnpj: null };
  const pj = { ...pf, id: 'pj', tipo: 'PJ' as const, nome: 'Empresa', cpf: null, rg: null,
    cnh: null, cnpj: '11222333000181' };
  beforeEach(() => {
    listar.mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 });
    obter.mockReset();
    criar.mockReset().mockResolvedValue({});
    editar.mockReset().mockResolvedValue({});
    adicionar.mockReset().mockResolvedValue({ dados: [], etag: 'v2' });
    remover.mockReset().mockResolvedValue({ dados: [], etag: 'v3' });
    TestBed.configureTestingModule({
      providers: [{ provide: CadastrosService, useValue: {
        listar, obter, criar, editar, listarOpcoes: async () => [pf, pj],
        adicionarComEtag: adicionar, removerComEtag: remover,
      } }],
    });
  });

  it.each(['PF', 'PJ'] as const)('apresenta os campos específicos de %s e endereço completo', async tipo => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    fixture.componentInstance.novoCliente();
    fixture.componentInstance.clienteModelo.update(x => ({ ...x, tipo }));
    fixture.detectChanges();
    expect(!!fixture.nativeElement.querySelector('#cliente-cpf')).toBe(tipo === 'PF');
    expect(!!fixture.nativeElement.querySelector('#cliente-cnpj')).toBe(tipo === 'PJ');
    for (const campo of ['estado', 'cidade', 'bairro', 'rua', 'numero'])
      expect(fixture.nativeElement.querySelector(`#cliente-${campo}`)).toBeTruthy();
  });

  it('preenche edição e permite salvar PJ sem e-mail ou documentos PF', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    obter.mockResolvedValueOnce({ dados: { ...pj,
      endereco: { estado: 'Estado', cidade: 'Cidade', bairro: 'Bairro', rua: 'Rua', numero: '12 A' } }, etag: 'v' });
    await fixture.componentInstance.editarCliente('pj');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#cliente-numero').value).toBe('12 A');
    await fixture.componentInstance.salvarCliente();
    expect(editar).toHaveBeenCalledWith('/clientes/pj', expect.objectContaining({
      email: null, cpf: null, cnh: null, cnpj: '11222333000181',
    }), 'v');
  });

  it('reaproveita dados PF e exige e-mail/validade ausentes do condutor', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    fixture.componentInstance.usarComoCondutor(pf);
    fixture.detectChanges();
    expect(fixture.componentInstance.condutorModelo()).toMatchObject({
      clienteId: 'pf', nome: 'Pessoa', cpf: pf.cpf, cnh: 'CNH', telefone: 'Tel',
    });
    await fixture.componentInstance.salvarCondutor();
    fixture.detectChanges();
    expect(criar).not.toHaveBeenCalled();
    expect(fixture.componentInstance.erros()['email']).toBeTruthy();
    expect(fixture.componentInstance.erros()['validadeCnh']).toBeTruthy();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
  });

  it('preenche e edita condutor com validade vencida sem bloquear cadastro', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    obter.mockResolvedValueOnce({ dados: { id: 'c', clienteId: 'pj', clienteNome: 'Empresa',
      nome: 'Condutor', email: 'pessoa@example.invalid', cpf: pf.cpf, cnh: 'CNH',
      validadeCnh: '2020-01-01', telefone: 'Tel' }, etag: 'v' });
    await fixture.componentInstance.editarCondutor('c');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#condutor-validade').value).toBe('2020-01-01');
    await fixture.componentInstance.salvarCondutor();
    expect(editar).toHaveBeenCalledWith('/condutores/c',
      expect.objectContaining({ validadeCnh: '2020-01-01', clienteId: 'pj' }), 'v');
  });

  it('remove vínculo desmarcado usando ETag da raiz PF', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    obter.mockResolvedValueOnce({ dados: [{ id: 'pj', nome: 'Empresa', cnpj: pj.cnpj }], etag: 'v1' });
    await fixture.componentInstance.abrirVinculos(pf);
    fixture.componentInstance.vinculosSelecionados.set(new Set());
    await fixture.componentInstance.salvarVinculos();
    expect(remover).toHaveBeenCalledWith('/clientes/pf/vinculos-pj/pj', 'v1');
    expect(fixture.componentInstance.aviso()).toContain('Vínculos atualizados');
  });

  it('não confunde falha de consulta com lista vazia', async () => {
    listar.mockRejectedValue(new Error('Indisponível'));
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
    expect(fixture.nativeElement.textContent).not.toContain('Nenhum registro encontrado.');
  });
});
