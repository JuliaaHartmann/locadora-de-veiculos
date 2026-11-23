import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { CadastrosService, RespostaComEtag } from '../cadastros.service';
import { CONFIGURACAO_API } from '../configuracao-api';

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
export interface AluguelDetalhe {
  id: string; estado: 'EmAberto'; saida: string; retornoPrevisto: string; hodometroInicial: number;
  cliente: { id: string; nome: string; tipo: TipoCliente; documentoMascarado: string };
  condutor: { id: string; nome: string; validadeCnh: string };
  veiculo: VeiculoLocacao;
  grupo: { id: string; nome: string };
  estimativa: DemonstrativoEstimado;
}

export interface AluguelResumo {
  id: string; estado: 'EmAberto'; condutor: string; veiculoId: string; placa: string; marca: string;
  modelo: string; plano: ModalidadePlano; saida: string; retornoPrevisto: string; parcelasConhecidas: string;
}

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
  obter(id: string) { return this.cadastros.obter<AluguelDetalhe>(`${rota}/${id}`); }
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
  async criar(comando: ComandoAluguel, chave: string): Promise<RespostaComEtag<AluguelDetalhe>> {
    const resposta = await firstValueFrom(this.http.post<AluguelDetalhe>(`${CONFIGURACAO_API.url}${rota}`, comando, {
      observe: 'response', headers: { 'Idempotency-Key': chave },
    }).pipe(timeout({ first: 15000 })));
    return { dados: resposta.body as AluguelDetalhe, etag: (resposta.headers.get('ETag') ?? '').replace(/"/g, '') };
  }
  editar(id: string, comando: ComandoAluguel, etag: string) {
    return this.cadastros.editar<AluguelDetalhe>(`${rota}/${id}`, comando, etag);
  }
}

export const rotuloPlano: Record<ModalidadePlano, string> = { Diario: 'Diário', Controlado: 'Controlado', Livre: 'Livre' };
export const rotuloCombustivel: Record<Combustivel, string> = { Gasolina: 'Gasolina', Gas: 'Gás', Diesel: 'Diesel', Alcool: 'Álcool' };
export const rotuloCliente: Record<TipoCliente, string> = { PF: 'Pessoa física', PJ: 'Pessoa jurídica' };
export const rotuloSeguro: Record<TipoSeguro, string> = { Cliente: 'Seguro do cliente', Terceiros: 'Seguro de terceiros' };
