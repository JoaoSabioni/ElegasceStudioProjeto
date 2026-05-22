# Deploy Fase 1 - Elegance Studio

Dominio atual do site publico:

- `https://try-barbearia.vercel.app/main`

## Ponto importante

O Vercel aloja bem o frontend Next.js, mas a API .NET, PostgreSQL e Redis precisam de outro ambiente de producao.

Enquanto `NEXT_PUBLIC_API_URL` apontar para `http://localhost:5134`, o site publicado na Vercel nao consegue criar marcacoes reais, porque o browser do cliente tentara chamar o localhost do proprio cliente.

## Variaveis necessarias no Vercel

No projeto Vercel do site `Barbearia`:

```txt
NEXT_PUBLIC_API_URL=https://api-do-teu-dominio.pt
```

No projeto Vercel do dashboard, se tambem for publicado la:

```txt
NEXT_PUBLIC_API_URL=https://api-do-teu-dominio.pt
```

Depois de alterar variaveis no Vercel, e necessario fazer redeploy.

## Backend necessario para producao

A API precisa de:

- hosting para .NET;
- PostgreSQL;
- Redis;
- `Jwt__SecretKey` forte;
- `ConnectionStrings__DefaultConnection`;
- `Redis__ConnectionString`;
- `Email__BrevoApiKey`;
- `Email__SenderEmail=t82704366@gmail.com` ou outro remetente validado na Brevo;
- `Cors__AllowedOrigins__0=https://try-barbearia.vercel.app`;
- dominio HTTPS para a API.

## Opcoes de deploy recomendadas

### Recomendacao para MVP: Render

E a opcao mais simples para este projeto nesta fase: consegue alojar a API .NET, PostgreSQL e Redis/Key Value no mesmo painel, com HTTPS e dominio proprio.

Tem plano gratuito para experimentar, mas nao deve ser usado como producao real:

- web service gratuito adormece apos 15 minutos sem trafego;
- o arranque apos dormir pode demorar cerca de 1 minuto;
- PostgreSQL gratuito expira ao fim de 30 dias;
- os proprios docs da Render dizem para nao usar free instances em producao.

Uso recomendado:

- gratuito apenas para demonstracao/testes;
- plano pago pequeno quando a barbearia comecar a usar diariamente.

Fonte oficial: https://render.com/docs/free

### Alternativa facil: Railway

E muito bom para MVP e deploy rapido, mas o gratuito atual e basicamente trial/credito pequeno:

- trial novo inclui $5 por ate 30 dias;
- depois passa para plano Free com $1/mes de credito;
- pode ter restricoes se a conta nao for verificada.

Uso recomendado:

- bom para testar depressa;
- menos previsivel como solucao gratis permanente.

Fonte oficial: https://docs.railway.com/pricing/free-trial

### Alternativa tecnica: Fly.io

E forte tecnicamente e pode sair barato com maquinas pequenas, mas nao e propriamente um plano gratis.

Os docs atuais dizem explicitamente que nao existe "free account/free tier" na Fly.io. Tambem avisam que allowances/creditos nao colocam teto automatico na fatura.

Uso recomendado:

- bom quando quisermos Docker/deploy mais controlado;
- nao e a minha primeira escolha para vender rapido a uma barbearia.

Fonte oficial: https://fly.io/docs/about/cost-management/

### Azure

E a opcao mais profissional/empresarial, e suporta bem .NET. Tem App Service com planos Free/Basic/Premium, mas para uma app real com base de dados, Redis, dominio e operacao simples tende a ficar mais caro e mais trabalhoso.

Uso recomendado:

- bom se o cliente exigir Microsoft/Azure;
- provavelmente exagerado para a fase 1.

Fonte oficial: https://azure.microsoft.com/en-us/pricing/details/app-service/

## Escolha pratica agora

Para esta fase eu recomendo:

1. Manter site publico e dashboard na Vercel.
2. Como ja tens Railway, publicar a API + PostgreSQL + Redis no Railway.
3. Usar plano gratuito apenas para validar fluxo real.
4. Antes de vender/usar em producao, passar para plano/creditos suficientes para nao parar o servico.

## Variaveis Railway obrigatorias

Na API, configurar pelo menos:

```txt
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=...
Redis__ConnectionString=...
Email__BrevoApiKey=...
Email__SenderEmail=t82704366@gmail.com
Email__SenderName=Elegance Studio
Jwt__Issuer=EleganceStudio.API
Jwt__Audience=EleganceStudio.Client
Jwt__SecretKey=uma-chave-aleatoria-com-32-ou-mais-caracteres
PublicLinks__ConfirmBookingBaseUrl=https://try-barbearia.vercel.app/confirmar
PublicLinks__DashboardBookingBaseUrl=https://teu-dashboard.vercel.app/dashboard
PublicLinks__BarberActionBaseUrl=https://api-do-teu-dominio.pt/api/bookings/barber-action
Cors__AllowedOrigins__0=https://try-barbearia.vercel.app
Database__ApplyMigrationsOnStartup=true
Seed__Users__0__Username=admin
Seed__Users__0__Password=password-forte-unica
Seed__Users__0__Role=Admin
Seed__Users__1__Username=edi
Seed__Users__1__Password=password-forte-unica
Seed__Users__1__Role=Barber
Seed__Users__1__BarberId=a1a1a1a1-0000-0000-0000-000000000001
Seed__Barbers__0__Id=a1a1a1a1-0000-0000-0000-000000000001
Seed__Barbers__0__Email=email-do-edi@exemplo.pt
Seed__Barbers__1__Id=a1a1a1a1-0000-0000-0000-000000000002
Seed__Barbers__1__Email=email-do-tomas@exemplo.pt
Seed__Barbers__2__Id=a1a1a1a1-0000-0000-0000-000000000003
Seed__Barbers__2__Email=email-do-abreu@exemplo.pt
```

Depois do primeiro deploy bem sucedido, recomenda-se mudar:

```txt
Database__ApplyMigrationsOnStartup=false
```

Assim a app deixa de ter de alterar schema automaticamente em cada arranque.

## Antes de ligar clientes reais

- trocar todos os segredos atuais;
- remover passwords previsiveis;
- configurar Brevo/transacional email e validar o remetente;
- testar criar, confirmar, cancelar e consultar marcacoes no dominio final;
- testar em telemovel com dados moveis, nao apenas no computador local.

## Reavaliacao pre-deploy da logica

Antes de avancar para Railway, foram corrigidos pontos que podiam criar problemas em producao:

- criacao e confirmacao de marcacoes passaram para `BookingService`;
- historico de barbeiro passou a usar a claim correta `barberId`;
- disponibilidade, SignalR e criacao de marcacoes passam a rejeitar barbeiros inativos;
- criacao/edicao valida passado, limite de 60 dias, intervalos e horario de expediente;
- conflitos de horario usam a duracao total dos servicos selecionados;
- confirmacao e lookup publico de marcacoes passaram a usar email com link/codigo temporario;
- tentativas de email ficam registadas em `NotificationLogs` com estado `Sent`, `Skipped` ou `Failed`;
- seed de utilizadores passou a vir de variaveis de ambiente, sem passwords default de producao;
- modelo EF ganhou limites, indices unicos e check constraints;
- API, site publico e dashboard compilam.

## Cuidados de base de dados

- Fazer backup/snapshot antes de aplicar migrations em dados reais.
- Usar um utilizador de BD apenas para a API, sem partilhar credenciais publicamente.
- Manter PostgreSQL e Redis privados dentro do Railway sempre que possivel.
- Nao deixar `Database__ApplyMigrationsOnStartup=true` permanentemente em producao.
- Guardar as passwords iniciais fora do codigo e trocar se forem partilhadas.
