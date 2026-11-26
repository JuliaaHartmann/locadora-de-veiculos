const fuso = 'America/Sao_Paulo';
const painel = new Intl.DateTimeFormat('pt-BR', {
  timeZone: fuso, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
});
const deslocamento = new Intl.DateTimeFormat('en-GB', { timeZone: fuso, timeZoneName: 'longOffset' });

function isoData(dia: string, mes: string, ano: string) {
  const iso = `${ano}-${mes}-${dia}`;
  const teste = new Date(`${iso}T00:00:00Z`);
  return Number.isFinite(teste.valueOf()) && teste.toISOString().slice(0, 10) === iso ? iso : null;
}

export function dataDoFiltro(valor: string): string | null {
  const texto = valor.trim();
  if (!texto) return '';
  const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(texto);
  return partes ? isoData(partes[1], partes[2], partes[3]) : null;
}

export function retornoDaTela(valor: string, preciso = false): { iso: string } | { erro: string } {
  const partes = /^(\d{2})\/(\d{2})\/(\d{4})\s+([01]\d|2[0-3]):([0-5]\d)(?::([0-5]\d)(?:[,.](\d{1,7}))?)?(?:\s+([+-]\d{2}:\d{2}))?$/.exec(valor.trim());
  const data = partes && isoData(partes[1], partes[2], partes[3]);
  if (!partes || !data || (!preciso && partes[6])) return { erro: 'Informe uma data e hora válidas, como 10/07/2027 14:30.' };
  const local = `${data}T${partes[4]}:${partes[5]}:${partes[6] ?? '00'}${partes[7] ? '.' + partes[7] : ''}`;
  const centro = Date.parse(`${local}Z`);
  const candidatos = new Set<string>();
  // Apenas offsets que reproduzam o horário local digitado são válidos; o fuso do dispositivo não é usado.
  for (const dias of [-1, 0, 1]) {
    const instante = new Date(centro + dias * 86400000);
    const offset = deslocamento.formatToParts(instante).find(x => x.type === 'timeZoneName')?.value.slice(3);
    if (!offset || !/^[+-]\d{2}:\d{2}$/.test(offset)) continue;
    const comOffset = new Date(local + offset);
    const p = partesDaData(comOffset);
    if (p.data === `${partes[1]}/${partes[2]}/${partes[3]}` && p.hora === `${partes[4]}:${partes[5]}`) candidatos.add(offset);
  }
  if (!candidatos.size) return { erro: 'Esse horário não existe no horário de São Paulo. Escolha outro horário.' };
  if (!partes[8] && candidatos.size > 1)
    return { erro: `Esse horário se repete. Acrescente ${[...candidatos].join(' ou ')} após a hora para informar o deslocamento.` };
  const offset = partes[8] ?? [...candidatos][0];
  return candidatos.has(offset) ? { iso: local + offset } : { erro: 'O deslocamento não corresponde ao horário de São Paulo.' };
}

export function partesDaData(instante: string | Date) {
  const partes = Object.fromEntries(painel.formatToParts(new Date(instante)).map(x => [x.type, x.value]));
  return { data: `${partes['day']}/${partes['month']}/${partes['year']}`, hora: `${partes['hour']}:${partes['minute']}` };
}

export function horaDoContrato(instante: string) {
  const {data, hora} = partesDaData(instante);
  return `${data} às ${hora}`;
}

export function horarioPreciso(instante: string) {
  const { data, hora } = partesDaData(instante);
  const offset = /[+-]\d{2}:\d{2}$/.exec(instante)?.[0];
  const ambigua = 'erro' in retornoDaTela(`${data} ${hora}`, true);
  return `${data} às ${hora}${ambigua && offset ? ' ' + offset : ''}`;
}

/** Formata a parte digitada sem descartar precisão, offset ou caracteres inválidos colados. */
export function formatarEntradaData(valor: string, comHora: boolean) {
  const limite = comHora ? 12 : 8;
  if (!/^[\d/ :]*$/.test(valor)) return valor;
  const digitos = valor.replace(/\D/g, '');
  if (digitos.length > limite || (comHora && valor.split(':').length > 2) || / {2,}/.test(valor)) return valor;
  // Um separador final digitado manualmente continua disponível para completar ou corrigir o campo.
  if (/[/ :]$/.test(valor) || (!digitos && valor)) return valor;
  const grupos = [[0, 2, ''], [2, 4, '/'], [4, 8, '/'], [8, 10, ' '], [10, 12, ':']] as const;
  return grupos.map(([inicio, fim, separador]) => digitos.length > inicio
    ? separador + digitos.slice(inicio, fim) : '').join('');
}

export function aplicarMascaraData(evento: Event, comHora = false) {
  const campo = evento.target as HTMLInputElement;
  const antes = campo.value;
  const cursor = campo.selectionStart;
  if ((evento as InputEvent).isComposing) return antes;
  const valor = formatarEntradaData(antes, comHora);
  if (valor !== antes) {
    const digitosAntes = antes.slice(0, cursor ?? antes.length).replace(/\D/g, '').length;
    let i = 0, lidos = 0;
    while (i < valor.length && lidos < digitosAntes) if (/\d/.test(valor[i++])) lidos++;
    campo.value = valor;
    campo.setSelectionRange(i, i);
  }
  return valor;
}

/** Compara as frações que Date.parse perderia abaixo de 1 ms. */
export function antesDe(instante: string, referencia: string) {
  const fracao = (iso: string) => (/T\d{2}:\d{2}:\d{2}\.(\d{1,7})/.exec(iso)?.[1] ?? '').padEnd(7, '0').slice(3);
  const ms = Date.parse(instante) - Date.parse(referencia);
  return ms < 0 || (ms === 0 && fracao(instante) < fracao(referencia));
}

export function dataDeCadastro(dataIso: string) { return dataIso.split('-').reverse().join('/'); }
