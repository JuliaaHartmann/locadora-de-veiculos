declare const LOCADORA_API_URL: string;

export const CONFIGURACAO_API = {
  url: typeof LOCADORA_API_URL === 'string' ? LOCADORA_API_URL : 'http://localhost:5000/api/v1',
};
