import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CadastrosService } from './cadastros.service';
import { PessoasComponent } from './pessoas.component';
import { SaidaCoordenador } from './saida-coordenador.service';
import { SessaoService } from './sessao.service';

describe('Pessoas F2', () => {
  const listar = vi.fn();
  const obter = vi.fn();
  const criar = vi.fn();
  const editar = vi.fn();
  const adicionar = vi.fn();
  const remover = vi.fn();
  const excluir = vi.fn();
  const pf = { id: 'pf', numeroExibicao: '000001', tipo: 'PF' as const, nome: 'Pessoa', cpf: '52998224725', rg: 'RG',
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
    excluir.mockReset().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: CadastrosService, useValue: {
        listar, obter, criar, editar, excluir, listarOpcoes: async () => [pf, pj],
        adicionarComEtag: adicionar, removerComEtag: remover,
      } }],
    });
    TestBed.inject(SessaoService).definir({
      sessaoId: 'teste', accessToken: 'teste', refreshToken: 'teste', accessExpiraEm: '2099-01-01T00:00:00Z',
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

  it('reaproveita dados PF e exige validade, mas aceita condutor sem e-mail', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    await fixture.componentInstance.usarComoCondutor(pf);
    fixture.detectChanges();
    expect(fixture.componentInstance.condutorModelo()).toMatchObject({
      clienteId: 'pf', nome: 'Pessoa', cpf: pf.cpf, cnh: 'CNH', telefone: 'Tel',
    });
    await fixture.componentInstance.salvarCondutor();
    fixture.detectChanges();
    expect(criar).not.toHaveBeenCalled();
    expect(fixture.componentInstance.erros()['email']).toBeUndefined();
    expect(fixture.componentInstance.erros()['validadeCnh']).toBeTruthy();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role=alert]'));
    fixture.componentInstance.condutorModelo.update(x => ({ ...x, validadeCnh: '2030-01-01', email: 'inválido' }));
    await fixture.componentInstance.salvarCondutor();
    expect(fixture.componentInstance.erros()['email']).toBeTruthy();
    expect(criar).not.toHaveBeenCalled();
    fixture.componentInstance.condutorModelo.update(x => ({ ...x, email: '  ' }));
    await fixture.componentInstance.salvarCondutor();
    expect(criar).toHaveBeenCalledWith('/condutores', expect.objectContaining({ email: null }));
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
    expect(fixture.nativeElement.querySelector('#condutor-email').value).toBe('pessoa@example.invalid');
    fixture.componentInstance.condutorModelo.update(x => ({ ...x, email: '' }));
    await fixture.componentInstance.salvarCondutor();
    expect(editar).toHaveBeenCalledWith('/condutores/c',
      expect.objectContaining({ validadeCnh: '2020-01-01', clienteId: 'pj', email: null }), 'v');
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

  it('preserva seleção no cancelamento da saída e repete apenas o vínculo que faltou', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    const pessoas = fixture.componentInstance;
    const saida = TestBed.inject(SaidaCoordenador);
    const confirmar = vi.fn().mockResolvedValue(false);
    await fixture.whenStable();
    obter.mockResolvedValueOnce({ dados: [{ id: 'anterior' }], etag: 'v1' });
    await pessoas.abrirVinculos(pf);
    pessoas.vinculosSelecionados.set(new Set(['pj']));
    adicionar.mockResolvedValueOnce({ dados: [{ id: 'anterior' }, { id: 'pj' }], etag: 'v2' });
    let rejeitar!: (erro: Error) => void;
    remover.mockImplementationOnce(() => new Promise((_, reject) => { rejeitar = reject; }));
    const operacao = pessoas.salvarVinculos();
    await vi.waitFor(() => expect(remover).toHaveBeenCalledOnce());
    expect(saida.ocupada()).toBe(true);
    await saida.solicitar(() => true, confirmar);
    expect(confirmar).not.toHaveBeenCalled();
    rejeitar(new Error('Falha de teste'));
    await operacao;
    expect(pessoas.erros()['parcial']).toContain('Aplicadas: Vincular Empresa. Pendentes: Remover vínculo');
    expect(pessoas.aviso()).toBe('');
    await saida.solicitar(() => true, confirmar);
    expect(confirmar).toHaveBeenCalledWith(expect.stringContaining('seleção pendente'));
    expect(pessoas.selecionado('pj')).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(req => req.url.endsWith('/logout'));
    await pessoas.salvarVinculos();
    expect(adicionar).toHaveBeenCalledOnce();
    expect(remover).toHaveBeenLastCalledWith('/clientes/pf/vinculos-pj/anterior', 'v2');
  });

  it('reconhece apenas alterações efetivas e preserva o rascunho ao cancelar Sair', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    const pessoas = fixture.componentInstance;
    pessoas.novoCliente();
    pessoas.clienteModelo.update(x => ({ ...x, nome: 'Rascunho' }));
    const confirmar = vi.fn().mockResolvedValue(false);
    await TestBed.inject(SaidaCoordenador).solicitar(() => true, confirmar);
    expect(confirmar).toHaveBeenCalledWith(expect.stringContaining('alterações não salvas'));
    expect(pessoas.clienteModelo().nome).toBe('Rascunho');
    expect(pessoas.modo()).toBe('cliente');
    TestBed.inject(HttpTestingController).expectNone(req => req.url.endsWith('/logout'));
  });

  it('mantém a paginação nas listas e volta à página anterior quando a última fica vazia', async () => {
    listar.mockResolvedValueOnce({ items: [pf], page: 1, pageSize: 20, total: 21 });
    const fixture = TestBed.createComponent(PessoasComponent);
    const pessoas = fixture.componentInstance;
    await fixture.whenStable();
    listar.mockResolvedValueOnce({ items: [pj], page: 2, pageSize: 20, total: 21 });
    await pessoas.paginar(1);
    fixture.detectChanges();
    expect(pessoas.pagina()).toBe(2);
    expect(listar).toHaveBeenLastCalledWith('/clientes', 2);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.regiao-tabela'));
    listar.mockResolvedValueOnce({ items: [], page: 2, pageSize: 20, total: 20 })
      .mockResolvedValueOnce({ items: [pf], page: 1, pageSize: 20, total: 20 });
    await pessoas.carregar();
    expect(listar).toHaveBeenLastCalledWith('/clientes', 1);
    expect(pessoas.pagina()).toBe(1);
    expect(pessoas.clientes()).toEqual([pf]);
  });

  it('limpa apenas a associação de erro do campo corrigido', async () => {
    const fixture = TestBed.createComponent(PessoasComponent);
    await fixture.whenStable();
    fixture.componentInstance.novoCliente();
    await fixture.componentInstance.salvarCliente();
    fixture.detectChanges();
    const nome = fixture.nativeElement.querySelector('#cliente-nome') as HTMLInputElement;
    const cpf = fixture.nativeElement.querySelector('#cliente-cpf') as HTMLInputElement;
    expect(nome.getAttribute('aria-describedby')).toBe('cliente-nome-erro resumo-pessoas');
    nome.value = 'Cliente de teste';
    nome.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
    expect(nome.hasAttribute('aria-invalid')).toBe(false);
    expect(nome.hasAttribute('aria-describedby')).toBe(false);
    expect(cpf.getAttribute('aria-invalid')).toBe('true');
  });
});
