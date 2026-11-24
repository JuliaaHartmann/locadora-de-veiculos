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
  if (!partes || !data || (!preciso && partes[6])) return { erro: 'Informe uma data e hora válidas no formato dia/mês/ano e hora de 24 h.' };
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
  if (!candidatos.size) return { erro: 'Esse horário local não existe no fuso de São Paulo.' };
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
  const precisao = /T\d{2}:\d{2}:(\d{2})(?:\.(\d{1,7}))?(Z|[+-]\d{2}:\d{2})$/.exec(instante);
  const [, segundos, fracao, offset] = precisao ?? [];
  const fracaoSignificativa = fracao?.replace(/0+$/, '');
  const sufixo = segundos && (segundos !== '00' || fracaoSignificativa)
    ? `:${segundos}${fracaoSignificativa ? ',' + fracaoSignificativa : ''}` : '';
  const ambigua = 'erro' in retornoDaTela(`${data} ${hora}${sufixo}`, true);
  return `${data} às ${hora}${sufixo}${ambigua && offset && offset !== 'Z' ? ' ' + offset : ''}`;
}

/** Compara as frações que Date.parse perderia abaixo de 1 ms. */
export function antesDe(instante: string, referencia: string) {
  const fracao = (iso: string) => (/T\d{2}:\d{2}:\d{2}\.(\d{1,7})/.exec(iso)?.[1] ?? '').padEnd(7, '0').slice(3);
  const ms = Date.parse(instante) - Date.parse(referencia);
  return ms < 0 || (ms === 0 && fracao(instante) < fracao(referencia));
}

export function dataDeCadastro(dataIso: string) { return dataIso.split('-').reverse().join('/'); }
