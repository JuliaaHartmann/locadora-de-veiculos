import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { CadastrosService, RespostaComEtag } from '../cadastros.service';
import { CONFIGURACAO_API } from '../configuracao-api';
import type { ComandoDevolucao, ConfirmacaoDevolucao, DemonstrativoDevolucao, DevolucaoConcluida, PreviaDevolucao } from './contratos-devolucao';

export type ModalidadePlano = 'Diario' | 'Controlado' | 'Livre';
export type TipoSeguro = 'Cliente' | 'Terceiros';
export type Combustivel = 'Gasolina' | 'Gas' | 'Diesel' | 'Alcool';
export type ModalidadeTaxa = 'Fixa' | 'Diaria';
export type TipoCliente = 'PF' | 'PJ';

export interface PlanoContratado {
  id: string;
  modalidade: ModalidadePlano;
  precoDiario: string;
  precoKm: string | null;
  franquiaDiariaKm: number | null;
}

export interface ComponenteContratado {
  origemId: string;
  categoria: 'Taxa' | 'SeguroCliente' | 'SeguroTerceiros';
  nome: string;
  modalidade: ModalidadeTaxa;
  preco: string;
  valor: string;
}

export interface DemonstrativoEstimado {
  diarias: number;
  plano: PlanoContratado;
  valorPlano: string;
  franquiaTotalKm: number | null;
  quilometragemPendente: boolean;
  adicionais: ComponenteContratado[];
  parcelasConhecidas: string;
  caucao: string;
  incompleta: boolean;
}

export interface VeiculoLocacao {
  id: string; placa: string; marca: string; modelo: string; combustivel: Combustivel; capacidadeLitros: string;
}
export type EstadoAluguel = 'EmAberto' | 'Concluido';
interface ContratoPreservado {
  id: string; numeroExibicao: string; saida: string; retornoPrevisto: string; hodometroInicial: number;
  cliente: { id: string; nome: string; tipo: TipoCliente; documentoMascarado: string };
  condutor: { id: string; nome: string; validadeCnh: string };
  veiculo: VeiculoLocacao;
  grupo: { id: string; nome: string };
}
export type AluguelAberto = ContratoPreservado & { estado: 'EmAberto'; estimativa: DemonstrativoEstimado };
export type AluguelConcluido = ContratoPreservado & { estado: 'Concluido'; final: DemonstrativoDevolucao; oculto?: boolean };
export type AluguelDetalhe = AluguelAberto | AluguelConcluido;

interface ResumoPreservado {
  id: string; numeroExibicao: string; condutor: string; veiculoId: string; placa: string; marca: string;
  modelo: string; plano: ModalidadePlano; saida: string; retornoPrevisto: string;
}
export type AluguelResumo = ResumoPreservado &
  ({ estado: 'EmAberto'; parcelasConhecidas: string } | { estado: 'Concluido'; totalFinal: string; oculto?: boolean });
export type VeiculoHistorico = { id: string; placa: string; marca: string; modelo: string; };

export interface ComandoAluguel {
  clienteId: string; condutorId: string; veiculoId: string; planoId: string; retornoPrevisto: string;
  hodometroInicial: number; taxaIds: string[]; seguros: TipoSeguro[];
}

export interface ClienteOpcao { id: string; nome: string; tipo: TipoCliente; }
export interface CondutorOpcao { id: string; nome: string; validadeCnh: string; }
export interface VeiculoOpcao extends VeiculoLocacao { grupoId: string; grupoNome: string; }
export interface TaxaOpcao { id: string; nome: string; preco: string; modalidade: ModalidadeTaxa; }
export interface SeguroEstado { configurado: boolean; precoDiario: string | null; }

const rota = '/alugueis';

@Injectable({ providedIn: 'root' })
export class AlugueisService {
  private readonly http = inject(HttpClient);
  private readonly cadastros = inject(CadastrosService);

  listar(pagina: number, filtros: Record<string, string | undefined>) {
    return this.cadastros.listar<AluguelResumo>(rota, pagina, 20, filtros);
  }
  obter(id: string, incluirOcultos = false) {
    return this.cadastros.obter<AluguelDetalhe>(`${rota}/${id}${incluirOcultos ? '?incluirOcultos=true' : ''}`);
  }
  async veiculosDoHistorico(incluirOcultos: boolean): Promise<VeiculoHistorico[]> {
    const lista = await this.cadastros.listarOpcoes<AluguelResumo>(rota, { incluirOcultos: incluirOcultos ? 'true' : undefined });
    const unicos = new Map<string, VeiculoHistorico>();
    for (const { veiculoId, placa, marca, modelo } of lista)
      if (!unicos.has(veiculoId)) unicos.set(veiculoId, { id: veiculoId, placa, marca, modelo });
    return [...unicos.values()].sort((a, b) => a.placa.localeCompare(b.placa, 'pt-BR') || a.id.localeCompare(b.id));
  }
  ocultar(id: string, etag: string) { return this.cadastros.excluir(`${rota}/${id}`, etag); }
  clientes() { return this.cadastros.listarOpcoes<ClienteOpcao>('/clientes'); }
  condutores(clienteId: string) { return this.cadastros.listarOpcoes<CondutorOpcao>('/condutores', { clienteId }); }
  veiculos(disponiveis = false, aluguelAtualId?: string) {
    return this.cadastros.listarOpcoes<VeiculoOpcao>('/veiculos', { disponiveis: disponiveis ? 'true' : undefined, aluguelAtualId });
  }
  planos(grupoId: string) { return this.cadastros.listarOpcoes<PlanoContratado>('/planos', { grupoId }); }
  taxas() { return this.cadastros.listarOpcoes<TaxaOpcao>('/taxas-servicos'); }
  async seguro(tipo: TipoSeguro) {
    return (await this.cadastros.obter<SeguroEstado>(`/configuracoes/seguros/${tipo}`)).dados;
  }
  async estimar(comando: ComandoAluguel) {
    return (await firstValueFrom(this.http.post<{ estimativa: DemonstrativoEstimado }>(
      `${CONFIGURACAO_API.url}${rota}/estimativas`, comando))).estimativa;
  }
  async criar(comando: ComandoAluguel, chave: string): Promise<RespostaComEtag<AluguelAberto>> {
    const resposta = await firstValueFrom(this.http.post<AluguelAberto>(`${CONFIGURACAO_API.url}${rota}`, comando, {
      observe: 'response', headers: { 'Idempotency-Key': chave },
    }).pipe(timeout({ first: 15000 })));
    return { dados: resposta.body as AluguelAberto, etag: (resposta.headers.get('ETag') ?? '').replace(/"/g, '') };
  }
  editar(id: string, comando: ComandoAluguel, etag: string) {
    return this.cadastros.editar<AluguelAberto>(`${rota}/${id}`, comando, etag);
  }
  async preverDevolucao(id: string, comando: ComandoDevolucao): Promise<RespostaComEtag<PreviaDevolucao>> {
    const resposta = await firstValueFrom(this.http.post<PreviaDevolucao>(
      `${CONFIGURACAO_API.url}${rota}/${id}/devolucao/previa`, comando, { observe: 'response' }));
    return { dados: resposta.body as PreviaDevolucao, etag: (resposta.headers.get('ETag') ?? '').replace(/"/g, '') };
  }
  async confirmarDevolucao(id: string, comando: ConfirmacaoDevolucao, etag: string,
    chave: string): Promise<RespostaComEtag<DevolucaoConcluida>> {
    const resposta = await firstValueFrom(this.http.post<DevolucaoConcluida>(
      `${CONFIGURACAO_API.url}${rota}/${id}/devolucao`, comando, { observe: 'response',
        headers: { 'If-Match': `"${etag}"`, 'Idempotency-Key': chave } }).pipe(timeout({ first: 15000 })));
    return { dados: resposta.body as DevolucaoConcluida, etag: (resposta.headers.get('ETag') ?? '').replace(/"/g, '') };
  }
}

export const rotuloPlano: Record<ModalidadePlano, string> = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };
export const rotuloCombustivel: Record<Combustivel, string> = { Gasolina: 'Gasolina', Gas: 'Gás', Diesel: 'Diesel', Alcool: 'Álcool' };
export const rotuloCliente: Record<TipoCliente, string> = { PF: 'Pessoa física', PJ: 'Pessoa jurídica' };
export const rotuloSeguro: Record<TipoSeguro, string> = { Cliente: 'Seguro do cliente', Terceiros: 'Seguro de terceiros' };
