# ElegasceStudioProjeto

Sistema completo para gestao de marcacoes da barbearia Elegance Studio.

Este repositorio e um monorepo com site publico, dashboard dos barbeiros, API .NET, base de dados PostgreSQL, Redis para tokens temporarios em producao, email transacional via Brevo e uma copia estatica do site para uso temporario sem marcacoes.

## Estado atual

- Site publico em Next.js com fluxo de marcacao.
- Dashboard em Next.js para barbeiros/admin.
- API em ASP.NET Core com PostgreSQL, JWT, SignalR, rate limiting e validacoes de marcacoes.
- Emails via Brevo:
  - email para cliente confirmar marcacao;
  - email para barbeiro com links para confirmar, remarcar no dashboard ou cancelar;
  - `replyTo` configurado para o email do cliente.
- Confirmar/cancelar atualiza o dashboard em tempo real via SignalR.
- Base de dados com migrations, indices, constraints e logs de notificacao.
- Ambiente local preparado para desenvolvimento sem Redis obrigatorio.
- Copia estatica em `Barbearia-estatica/`, sem sistema de marcacoes.

## Estrutura

```txt
Barbearia/                              Site publico Next.js
elegance-studio-dashboard/              Dashboard Next.js
api-DarioSabioni-Projeto-Final/         Solucao .NET / API
Barbearia-estatica/                     Versao estatica sem marcacoes
DEPLOY_FASE1_VERCEL.md                  Guia de deploy fase 1
RELATORIO_ESTADO_PROJETO.md             Relatorio tecnico do estado do projeto
docker-compose.yml                      PostgreSQL, Redis e apps em Docker
.env.example                            Exemplo de variaveis de ambiente
```

## Stack

- Frontend: Next.js, React, TypeScript, Tailwind CSS.
- Backend: ASP.NET Core, Entity Framework Core, PostgreSQL.
- Tempo real: SignalR.
- Tokens temporarios: Redis em producao; memoria em desenvolvimento.
- Email: Brevo Transactional Email API.
- Deploy previsto:
  - Vercel para site publico;
  - Vercel para dashboard;
  - Railway para API, PostgreSQL e Redis.

## Requisitos locais

- .NET SDK 10
- Node.js / npm
- PostgreSQL local ou Docker
- Conta Brevo com API key e remetente validado

## Configuracao local da API

Guardar segredos localmente com `dotnet user-secrets`:

```powershell
cd .\api-DarioSabioni-Projeto-Final\EleganceStudio.API

dotnet user-secrets set "Email:BrevoApiKey" "A_TUA_CHAVE_BREVO"
dotnet user-secrets set "Email:SenderEmail" "t82704366@gmail.com"
dotnet user-secrets set "Email:SenderName" "Elegance Studio"
dotnet user-secrets set "PublicLinks:ConfirmBookingBaseUrl" "http://localhost:3000/confirmar"
dotnet user-secrets set "PublicLinks:DashboardBookingBaseUrl" "http://localhost:3001/dashboard"
dotnet user-secrets set "PublicLinks:BarberActionBaseUrl" "http://localhost:5134/api/bookings/barber-action"
```

Em desenvolvimento, a API usa:

```txt
API:       http://localhost:5134
Site:      http://localhost:3000
Dashboard: http://localhost:3001
```

## Correr localmente

API:

```powershell
dotnet run --project .\api-DarioSabioni-Projeto-Final\EleganceStudio.API\EleganceStudio.API.csproj --launch-profile http
```

Site publico:

```powershell
cd .\Barbearia
npm install
npm run dev
```

Dashboard:

```powershell
cd .\elegance-studio-dashboard
npm install
npm run dev
```

## Variaveis importantes para deploy

API / Railway:

```txt
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=...
Redis__ConnectionString=...
Email__BrevoApiKey=...
Email__SenderEmail=t82704366@gmail.com
Email__SenderName=Elegance Studio
Jwt__Issuer=EleganceStudio.API
Jwt__Audience=EleganceStudio.Client
Jwt__SecretKey=uma-chave-forte-com-32-ou-mais-caracteres
PublicLinks__ConfirmBookingBaseUrl=https://dominio-do-site/confirmar
PublicLinks__DashboardBookingBaseUrl=https://dominio-do-dashboard/dashboard
PublicLinks__BarberActionBaseUrl=https://dominio-da-api/api/bookings/barber-action
Cors__AllowedOrigins__0=https://dominio-do-site
Cors__AllowedOrigins__1=https://dominio-do-dashboard
Database__ApplyMigrationsOnStartup=true
```

Vercel / site publico e dashboard:

```txt
NEXT_PUBLIC_API_URL=https://dominio-da-api
```

Depois do primeiro deploy com migrations aplicadas, recomenda-se mudar:

```txt
Database__ApplyMigrationsOnStartup=false
```

## Validacao

Comandos usados para verificar o estado atual:

```powershell
dotnet build .\api-DarioSabioni-Projeto-Final\api-DarioSabioni-Projeto-Final.slnx --no-restore

cd .\Barbearia
npm run build

cd .\elegance-studio-dashboard
npm run build
```

## Notas de producao

- Usar dominio proprio para reduzir risco de emails cairem em spam.
- Configurar DKIM/DMARC na Brevo quando existir dominio.
- Usar emails reais dos barbeiros nas variaveis `Seed__Barbers`.
- Manter PostgreSQL e Redis privados no Railway.
- Trocar todos os segredos antes de vender/usar com clientes reais.
- Fazer backup antes de aplicar migrations em dados reais.

## Documentacao adicional

- `RELATORIO_ESTADO_PROJETO.md`
- `DEPLOY_FASE1_VERCEL.md`
- `.env.example`
