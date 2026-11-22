# Frontend — F1 local

Angular 22.3, standalone, zoneless e Signal Forms, Tailwind CSS 4 via PostCSS e ícones standalone `@lucide/angular`; sessão/refresh somente em memória. Ícones são decorativos e não substituem rótulos textuais. Recarregar a página exige novo login.

## Verificar e executar

Node 24.21.0 e npm 11.19.0 foram usados na revisão. Com instalação de dependências autorizada:

```powershell
npm ci
npm run build
npm test -- --watch=false
npm audit
npm start
```

A SPA usa `http://localhost:4200` e chama diretamente `http://localhost:5000/api/v1`. Inicie o backend conforme `../backend/README.md`. Não há proxy. A configuração da API é pública, não contém segredo e pode ser substituída pelo identificador de build `LOCADORA_API_URL` (string JavaScript entre aspas). A configuração `verificacao-f1` em `angular.json` demonstra a substituição para API 5001 e é usada pelo script visual isolado.

Rotas: `/registro`, `/login` e painel mínimo protegido em `/`. O registro envia somente `username`, `email`, `password`; confirmação de senha é local e não há nome empresarial obrigatório nem autenticação automática. O sucesso encaminha ao login com aviso transitório. Erros preservam usuário/e-mail e limpam senhas.

O interceptor restringe bearer à origem e ao segmento exato da API; não o envia a login/refresh/registro ou destinos externos. Renovação é serializada na aba, sem persistência ou repetição cega de refresh. Falha de revogação no logout é exibida e não é apresentada como sucesso.

Os testes Vitest cobrem Signal Forms, confirmação, erros/foco, contrato de registro, memória, ausência de armazenamento, logout, interceptor e serialização de refresh. A avaliação visual complementar usa Playwright do ambiente; não há dependência Playwright instalada neste frontend nem alegação de certificação de acessibilidade.
