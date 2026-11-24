import type { Combustivel, ComponenteContratado, PlanoContratado } from './alugueis.service';

export interface ComandoDevolucao {
  retornoReal: string; hodometroFinal: number; litrosRestantes: string; taxaIds: string[];
}

export interface ParcelaMonetaria { bruto: string; valor: string; }
export interface AdicionalFinal extends Omit<ComponenteContratado, 'valor'> {
  incluidoNoRetorno: boolean; quantidade: number; parcela: ParcelaMonetaria;
}
export interface DemonstrativoDevolucao {
  versaoRegra: string; retornoReal: string; hodometroFinal: number; litrosRestantes: string;
  diarias: number; kmRodados: number;
  plano: {
    parametros: PlanoContratado; franquiaTotalKm: number | null; kmCobrados: number;
    diariasBruto: string; quilometragemBruto: string; subtotal: ParcelaMonetaria;
  };
  adicionais: AdicionalFinal[];
  combustivel: {
    tipo: Combustivel; versaoId: string; inicioVigenciaUtc: string; ordem: number;
    litrosARepor: string; precoPorLitro: string; parcela: ParcelaMonetaria;
  };
  multa: ParcelaMonetaria | null; total: string; caucao: string;
}
export interface PreviaDevolucao { versaoAceita: string; demonstrativo: DemonstrativoDevolucao; }
export interface ConfirmacaoDevolucao extends ComandoDevolucao { versaoAceita: string; }
export interface DevolucaoConcluida { id: string; estado: 'Concluido'; final: DemonstrativoDevolucao; }
