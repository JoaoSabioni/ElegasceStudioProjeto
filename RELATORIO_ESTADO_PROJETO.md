# Relatório de estado - Elegance Studio

Data da análise: 21 maio 2026

## Atualizacao - inicio da Fase 1

Foram iniciadas as correcoes de base para preparar a aplicacao para uso real:

- disponibilidade passa a aceitar varios servicos e calcula a duracao total;
- slots que colidem a meio de um servico longo deixam de aparecer como disponiveis;
- `/api/services` passa a devolver `DurationMinutes`;
- site publico envia `serviceIds` para disponibilidade;
- dashboard tambem verifica disponibilidade real ao criar marcacoes manuais;
- CORS passou a aceitar `https://try-barbearia.vercel.app` e tambem pode ser configurado por `Cors:AllowedOrigins`;
- politica de privacidade do site publico foi alinhada com recolha real de dados de marcacao;
- login e base visual do dashboard foram simplificados para uma experiencia mais operacional;
- `.env.example` foi criado e `.env` passou a estar no `.gitignore` para proximos commits.

## Atualizacao - revisao de logica geral

Foi feita uma segunda passagem de simplificacao da logica:

- estados de marcacao foram centralizados em `BookingStatus`;
- calculo de slots ocupados foi centralizado em `BookingSlotCalculator`;
- `AvailabilityService` e `BookingsController` passaram a usar a mesma regra de slots;
- confirmacao por token agora suporta conjuntos de marcacoes criadas por varios servicos;
- tokens antigos de confirmacao com apenas um id continuam suportados;
- apagar uma marcacao agora liberta slots em tempo real e emite `BookingDeleted` para o dashboard;
- dashboard remove marcacoes apagadas via SignalR;
- modal de nova marcacao no dashboard deixou de gerar disponibilidade localmente e passou a tratar a API como fonte de verdade;
- erros de conflito no dashboard usam codigo interno `BOOKING_CONFLICT`, evitando depender de texto traduzido.

## Resumo executivo

O projeto já tem uma base forte para uma solução vendável: site público em Next.js, dashboard para barbeiros/admin, API .NET, PostgreSQL, Redis, SignalR, autenticação JWT, rate limiting e Docker Compose.

Ainda não está pronto para "lançar no ar e vender" sem uma fase curta de endurecimento. Os maiores riscos atuais são: credenciais e seeds de desenvolvimento no repositório, configuração de produção incompleta, fluxo de marcações com múltiplos serviços ainda inconsistente, SMS ainda simulado via Mountebank, política de privacidade desalinhada com o backend real de marcações, e ausência de gestão operacional de horários/folgas/serviços pelo dashboard.

## Estado atual

### Site público `Barbearia`

- Next.js com páginas de início, serviços, galeria, política de privacidade e marcação.
- Boa presença visual: fotografias reais, marca consistente e experiência mobile/desktop razoável.
- O fluxo de marcação escolhe barbeiro, serviços, data, hora e dados do cliente.
- A versão atual depende da API para barbeiros, serviços, disponibilidade e criação de marcações.
- Build de produção validado com sucesso.

### Dashboard `elegance-studio-dashboard`

- Login com JWT.
- Vista diária/semanal para barbeiros e admin.
- Lista e timeline de marcações.
- Confirmação e remoção de marcações.
- Criação manual de marcação por barbeiro.
- Atualização em tempo real via SignalR.
- Build de produção validado com sucesso.

### API `EleganceStudio.API`

- .NET 10, Entity Framework Core, PostgreSQL.
- Redis para tokens/cache.
- JWT com roles `Admin` e `Barber`.
- Rate limiting em marcações, disponibilidade, lookup e login.
- Soft delete de marcações.
- Arquivo automático de marcações antigas.
- SignalR para novas marcações e alterações.
- A pasta de testes foi removida por decisão de produto nesta fase.

## Pontos fortes

- Arquitetura separada entre site público, dashboard e API.
- Boa fundação para uso real por uma barbearia: agenda, barbeiros, serviços, estados e notificações.
- Uso de transações e locks no fluxo de criação/edição de marcações.
- Índices em marcações e telefone, o que ajuda performance.
- Rate limiting já pensado para pontos sensíveis.
- Dashboard funcional para operação diária.
- Docker Compose facilita demonstração local e ambiente de staging.

## Riscos e problemas a corrigir antes de vender

### Crítico

1. Credenciais de desenvolvimento no repositório
   - `.env` contém `POSTGRES_PASSWORD` e `JWT_SECRET`.
   - `appsettings.Development.json` também contém password e secret.
   - `DbSeeder` cria utilizadores com passwords previsíveis como `admin123`, `edi123`, etc.
   - Antes de produção: rodar todos os segredos, usar secrets do hosting e remover credenciais reais do Git.

2. SMS ainda não é fornecedor real
   - O `SmsService` aponta para Mountebank/mock.
   - Para venda: integrar Twilio, MessageBird, Vonage, E-goi, BulkSMS ou outro fornecedor real, com logs, retries e custo previsto.

3. Fluxo de múltiplos serviços tem inconsistência de disponibilidade
   - O site permite selecionar vários serviços.
   - A disponibilidade consulta apenas o primeiro `serviceId`.
   - A API cria várias marcações sequenciais, mas o frontend pode mostrar horários que não comportam a duração total.
   - Recomendação: endpoint de disponibilidade deve receber `serviceIds` ou `totalDuration` validado no backend.

4. Política de privacidade desatualizada para a versão com marcações
   - A página atual diz que o site não recolhe nem armazena dados pessoais.
   - A API real guarda nome, telefone, barbeiro, serviço, data e hora.
   - Para produção: atualizar RGPD, base legal, retenção, direitos, contacto, subcontratantes e política de SMS.

### Alto

5. CORS só permite localhost
   - `Program.cs` permite apenas `http://localhost:3000` e `http://localhost:3001`.
   - Produção precisa de domínios reais do site e dashboard.

6. Docker/API tem dois Dockerfiles com alvos diferentes
   - Existe Dockerfile Linux na raiz da API e Dockerfile Windows/NanoServer dentro do projeto.
   - O `docker-compose.yml` usa o Dockerfile da raiz, mas convém remover ambiguidade antes de deploy.

7. Configuração de HTTPS/proxy/hosting incompleta
   - `UseHttpsRedirection` existe, mas faltam instruções claras para reverse proxy, TLS, headers e domínio.
   - Para produção: Nginx/Caddy/Traefik ou plataforma gerida, certificados automáticos e health checks.

8. Gestão de horários ainda hardcoded
   - Horário global 09:00-19:00 em configuração.
   - Não há folgas, férias, pausas de almoço, dias fechados, horários por barbeiro ou exceções.
   - Isto é essencial para vender a barbearias reais.

9. Dashboard ainda não gere catálogo
   - Serviços, preços, duração, barbeiros e dados de contacto estão essencialmente seeded/hardcoded.
   - Para produto vendável, o dono precisa gerir isto sem mexer em código.

10. Tokens em `localStorage`
   - Funciona, mas aumenta impacto de XSS.
   - Melhor para produção: cookie HttpOnly/SameSite ou, no mínimo, CSP forte, sanitização e proteção de sessão.

### Médio

11. Cobertura automatizada removida
   - A pasta `tests` foi removida.
   - Antes de vender em escala, recomenda-se voltar a criar testes focados nos fluxos críticos: disponibilidade, conflitos, confirmação, cancelamento e autenticação.

12. Serviços retornam dados incompletos
   - `ServicesController.GetAll` não devolve `DurationMinutes`, mas os frontends tipam/esperam duração.
   - Corrigir para evitar cálculos errados no cliente.

13. Estados de marcação são strings soltas
   - `Pending`, `Confirmed`, `Cancelled` estão como string.
   - Preferível enum/constantes centralizadas e constraints.

14. Confirmação por SMS só confirma a primeira marcação de um conjunto
   - Em múltiplos serviços, o token aponta para a primeira booking.
   - É preciso decidir se o conjunto deve ter um `BookingGroupId` e confirmar/cancelar em bloco.

15. Lookup por telefone expõe histórico sem verificação forte
   - Rate limited e cacheado, mas qualquer pessoa com o telefone consegue ver marcações.
   - Melhor: OTP por SMS ou token temporário.

16. Logs, auditoria e observabilidade mínimos
   - Falta logging estruturado, métricas, alertas, dashboards e tracking de erros.

17. Backups e recuperação não documentados
   - Para vender, é obrigatório ter backup automático da BD e plano de restore testado.

## Recomendações para lançar em produção

### Fase 1 - Preparar uma demo segura

- Criar ambiente de staging com domínio temporário.
- Remover segredos do Git e usar variáveis de ambiente.
- Trocar passwords seeded por criação manual inicial ou script seguro.
- Atualizar CORS para os domínios reais.
- Atualizar política de privacidade para o fluxo real.
- Corrigir retorno de `DurationMinutes` em `/api/services`.
- Corrigir disponibilidade para múltiplos serviços.
- Integrar SMS real ou desativar a promessa de SMS até estar pronto.

### Fase 2 - Tornar vendável para a barbearia

- Dashboard para gerir:
  - barbeiros ativos/inativos;
  - serviços, preços e durações;
  - horários por barbeiro;
  - pausas, folgas e férias;
  - cancelamentos/reagendamentos.
- Histórico e estatísticas:
  - receita estimada;
  - serviços mais pedidos;
  - marcações por barbeiro;
  - no-shows/cancelamentos.
- Confirmação/cancelamento pelo cliente com link seguro.
- Notificações reais por SMS/WhatsApp/email.
- Exportação simples para CSV.
- Página pública com SEO e contactos atualizados.

### Fase 3 - Operação comercial

- Definir plano de preço:
  - setup inicial;
  - mensalidade de hosting/manutenção;
  - custos de SMS por utilização;
  - suporte e alterações.
- Criar contrato/termos de serviço.
- Criar processo de onboarding:
  - recolha de serviços, preços, horários, contactos, logo/fotos;
  - configuração de domínio;
  - formação dos barbeiros;
  - teste com 10 marcações reais antes do go-live.
- Definir SLA simples:
  - backups;
  - tempo de resposta;
  - quem altera horários/preços;
  - responsabilidade em falhas de SMS.

## Checklist antes de ir "mesmo no ar"

- Domínio final comprado/configurado.
- HTTPS ativo.
- API e frontends em produção/staging.
- PostgreSQL gerido ou servidor com backup automático.
- Redis configurado.
- Secrets fora do repositório.
- CORS com domínios reais.
- SMS real configurado e testado.
- Política de privacidade e termos revistos.
- Admin inicial criado com password forte.
- Logs e alertas básicos.
- Backup e restore testados.
- Testes de criação, conflito, confirmação, cancelamento e dashboard em mobile.
- Plano de suporte/comercial acordado com a barbearia.

## Cópia estática criada

Foi criada uma versão temporária em `Barbearia-estatica/`.

Esta cópia:

- não usa Next.js;
- não usa API;
- não tem página nem formulário de marcação;
- inclui início, serviços, galeria, contactos e política de privacidade;
- pode ser aberta diretamente pelo ficheiro `Barbearia-estatica/index.html`.

É adequada para os barbeiros usarem já como presença online simples enquanto o sistema de marcações é endurecido.

## Validação feita

- `dotnet build api-DarioSabioni-Projeto-Final\api-DarioSabioni-Projeto-Final.slnx --no-restore`
  - Resultado: build passou sem warnings ou erros.
- `npm.cmd run build` em `Barbearia`
  - Resultado: build passou.
  - Aviso: Next.js detetou múltiplos lockfiles.
- `npm.cmd run build` em `elegance-studio-dashboard`
  - Resultado: build passou.
  - Avisos: múltiplos lockfiles e `@import` de Google Fonts depois de regras CSS.
- Validação da cópia estática
  - Resultado: todos os links internos e assets referenciados existem.
  - A pasta estática não contém chamadas `fetch`, `api/`, `NEXT_PUBLIC` nem links para `/marcar`.
