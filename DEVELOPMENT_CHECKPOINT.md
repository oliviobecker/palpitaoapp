# DEVELOPMENT_CHECKPOINT.md

> Última atualização: 2026-06-17 — multi-grupo + SuperAdmin + flags por grupo + **tipo de certame
> (Palpitão England × FIFA World Cup)** com pontuação/clássicos, Regra Flávio, import/resultados da Copa
> via **OneFootball**, multiplicador da Copa no texto copiável, **branding por idioma (FanPicks/Palpitão)**,
> **validação via FluentValidation localizada (PT/EN)** e **PWA `manifest.webmanifest`**.
> Build/testes: **backend 317/317**, **frontend build OK + Vitest 35/35 + Prettier OK + Playwright 25/25**.

## 0. Tipo de certame (Copa), branding e validação (recente)

Cada grupo escolhe um **`TournamentType`** na criação: `PalpitaoEngland` (regras inglesas atuais,
intactas) ou `FifaWorldCup`. O tipo fica em `Group`; cada `Round` resolve o tipo via `Group`.

- **Competições/fases por tipo** (`Services/Tournaments/TournamentRules`): England → PL/FA Cup/
  Championship/League One + fases atuais; World Cup → competição única `FifaWorldCup` + fases
  `WorldCup*` (grupos, 16-avos…final). Validado **no backend** (criar/importar jogo rejeita o que não
  pertence ao tipo).
- **Pontuação da Copa** (`ScoringService`): mesmas categorias de placar; multiplicador por fase
  (grupos x1; R32/R16 x2; quartas+ x3) e **clássico** (ambas campeãs mundiais, só no mata-mata) que
  **dobra** o multiplicador. Override manual continua prevalecendo.
- **Seleções**: `Team` ganhou `TeamType`/`CountryCode`/`FifaCode`/`WorldCupTitles`; 7 campeãs semeadas
  (Brasil 5, Alemanha 4, Argentina 3, França 2, Uruguai 2, Espanha 1, Inglaterra 1). Import da Copa
  cria seleções (`NationalTeam`).
- **Regra Flávio por tipo** (`FlavioRuleService`): England a partir da rodada 16 (inalterado); World
  Cup a partir das **quartas** (por fase). Alvo (líder pré-rodada) e aplicabilidade **persistidos na
  publicação** (`Round.FlavioRuleTargetUserId/FlavioRuleApplies`); prazo 24h/12h reaproveitado;
  punição = metade dos pontos (arredonda p/ baixo); ausência continua ausência.
- **Import/resultados via OneFootball** (mesmo site das competições inglesas): `OneFootballFixtureProvider`
  ganhou o slug `fifa-world-cup-12` + inferência de fase (grupos/16-avos…final) a partir do texto de
  stage do card; novo `OneFootballResultsProvider` lê placar/status ao vivo e final das abas
  `fixtures`/`results`, casando pelo `onefootball-{matchId}`. Selecionado por
  `ResultsProvider:Provider="OneFootball"` (`Enabled=false` por padrão = no-op seguro). `refresh-results`
  e o `ResultsRefreshBackgroundService` (config `ResultsRefresh`, desligado por padrão) atualizam
  rodadas ativas e **nunca** finalizam a rodada.
- **Frontend**: cards de tipo obrigatórios em criar grupo; filtro de competição/fase por tipo no form
  de jogos; avisos da Regra Flávio e dos prints assinados na tela de palpites; i18n PT/EN das fases e
  textos da Copa; `GroupContext.tournamentType`/`isWorldCup`. **Texto copiável** (`match.util`) reflete
  o multiplicador da Copa (fase × clássico campeãs-mundiais) e os rótulos de fase.
- **Branding por idioma**: o nome do produto agora é a chave i18n **`app.name`** — **FanPicks** em
  `en-US`, **Palpitão** em `pt-BR` — usada no header do shell e nas telas login/registro/criar grupo.
  O `<title>` do documento acompanha o idioma (`LanguageService.use` → `app.name`); `index.html` default
  = `FanPicks`. O **texto copiável** (round/closing message) passou a usar o **nome do grupo/temporada
  atual** (`GroupContextService.groupName`) no cabeçalho, em vez do fixo "Palpitão England 2025/2026".
  Nomes como "England 2025/2026" são **exemplos de grupo/temporada, não o nome do app**; o grupo padrão
  semeado mantém "Palpitão England 2025/2026" (é um grupo). **Backend não renomeado** (namespaces/
  entidades/seed intactos). README retitulado **Palpitão / FanPicks** com seção de branding. e2e
  `branding.e2e` cobre FanPicks (en) e Palpitão (pt) no login + `<title>`.
- **Validação via FluentValidation (PT/EN)**: as DataAnnotations hardcoded em português saíram dos DTOs.
  Os validators (`Validation/RequestValidators.cs`) emitem **chaves** do `DomainMessages`; o
  `ValidationActionFilter` global roda os validators e lança `ValidationException` → middleware devolve
  **400 localizado** pelo `Accept-Language`. Chaves `validation.*` (PT+EN) adicionadas (e
  `tournamentType.required`, que faltava). O 400 automático do ModelState foi suprimido.
- **PWA**: `frontend/public/manifest.webmanifest` (name/short_name **FanPicks**, standalone, theme-color,
  ícones favicon) + `<link rel="manifest">`/`<meta theme-color>` no `index.html`. Faltam ícones PNG
  192/512 para instalação "completa" (hoje aponta para o favicon).
- **Limitações**: o provider externo fica `Enabled=false` por padrão (sem credenciais commitadas); os
  parsers do OneFootball seguem as chaves de JSON mais comuns (stage/score) — convém validar contra o
  payload real da Copa e ajustar nomes de campos se necessário (degrada com mensagem amigável e mantém
  o fluxo manual). UI dedicada de badges x4/x6 e de busca de jogos da Copa reusa o fluxo de fixtures.

## 1. Visão geral

**Palpitão** (pt) / **FanPicks** (en) é uma plataforma de bolão de futebol **multi-grupo
(multi-tenant)**: cada **grupo** é um bolão independente (ex.: _Palpitão England 2025/2026_,
_Palpitão Copa do Mundo_, _World Cup 2026_, _Friends League_) com seu próprio tipo de certame,
administradores, participantes, rodadas, jogos, palpites, classificações, solicitações de cadastro,
importações OCR e auditoria — **com isolamento total entre grupos**. Nomes como "England 2025/2026"
são **grupos/temporadas**, não o nome do app.

Monorepo: `/backend` (.NET 10 Web API + EF Core + PostgreSQL) e `/frontend` (Angular 21 + TypeScript +
Bootstrap 5.3 + ngx-translate).

## 2. Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 Web API (controllers), EF Core 10 (code-first), PostgreSQL, solução `.slnx` |
| Auth | JWT (Bearer); papel **por grupo** via header `X-Group-Id` |
| Testes backend | xUnit + SQLite in-memory (`EnsureCreated`) — **317 testes** |
| Frontend | Angular 21 standalone + signals, Reactive Forms, Bootstrap 5.3, ngx-translate (PT-BR/EN-US) |
| Testes frontend | Vitest (unit, **35**) + Playwright (e2e, **25**, API mockada) |
| CI/CD | GitHub Actions: `ci.yml` (ubuntu, build+test) e `deploy-iis.yml` (self-hosted Windows → IIS) |
| Observabilidade | Sentry (backend), tag `group_id` |

## 3. Funcionalidades implementadas

### Núcleo do bolão (pré-multi-grupo)
- Rodadas (ciclo Draft → Published → Locked → Scored/Cancelled), jogos, palpites, espelho.
- Pontuação automática, multiplicadores, ausências/penalidades, **Regra Flávio**, eliminação.
- Classificação geral + **classificação temporária** (resultados ao vivo, provider plugável).
- Importação de jogos por período (providers externos) e **importação de palpites por OCR** (Tesseract).
- Palpites manuais pelo admin; catálogo completo de clubes 2025/2026; Scout por partida; mensagem de
  encerramento (copiável); i18n PT-BR/EN-US (Accept-Language).

### Multi-grupo (multi-tenant) — concluído
- **Entidades** `Group` e `GroupUser` (enums `GroupRole` {GroupAdmin, Participant}, `GroupUserStatus`
  {PendingApproval, Approved, Rejected, Inactive}).
- **Coluna `GroupId`** nas raízes de tenant (`Season`, `Round`, `Standing`, `RoundParticipantResult`,
  `AuditLog`); demais entidades por-rodada derivam o grupo pelo pai.
- **`CurrentGroupService`**: lê `X-Group-Id`, valida membership `Approved`, expõe `GroupId`/`Role`;
  filtros `[RequireGroupAdmin]` / `[RequireGroupParticipant]`; 403 quando inválido.
- **Endpoints**: `GET /public/groups`, `POST /auth/create-group`, `POST /auth/register` (com grupo),
  `GET /auth/my-groups`; solicitações por `GroupUser` filtradas pelo grupo.
- **Roster por grupo**: quem pontua/falta vem da associação ao grupo (`GroupQueries`), não do papel global.
- **Frontend**: `GroupContextService` + interceptor `X-Group-Id`; telas `/create-group`, `/select-group`,
  `/register` (com seletor de grupos ativos); roteamento pós-login (0/1/vários grupos); guards por papel
  no grupo; header com grupo atual + "Trocar grupo"; i18n.
- **Migration** `AddGroupsAndTenancy`: cria tabelas, backfill dos dados atuais para o grupo padrão
  _Palpitão England 2025/2026_, admin semeado como `GroupAdmin/Approved`.
- **SuperAdmin de plataforma** (vê/gerencia todos os grupos): reutiliza o papel global existente
  `UserRole.Admin` (sem novo enum/coluna/migration). O `CurrentGroupService` concede acesso
  `GroupAdmin` a **qualquer grupo existente** selecionado pelo header `X-Group-Id`, mesmo sem
  `GroupUser`; `GET /auth/my-groups` retorna **todos** os grupos para o SuperAdmin (alimenta o
  seletor `/select-group`). Fluxos públicos (`register`/`create-group`) sempre criam `Participant`,
  então ninguém se auto-promove. Frontend: badge "Super Admin" no header + aviso em `/select-group`.
- **Ativação/eliminação por grupo**: `GroupUser.IsActive` e `GroupUser.IsEliminated` (migration
  `MovePerGroupFlagsToGroupUser` com backfill dos antigos flags globais). Desativar/eliminar um
  participante afeta **apenas o grupo atual**; `User.IsActive` permanece só como chave-mestra de
  conta (login). `GroupQueries`/scoring/ausências/mirror/standings/Flávio usam os flags do grupo.
- **Sentry**: tag `group_id`. **README**: seção §26 (multi-grupo).

## 4. Funcionalidades pendentes

- `create-group` público exige **e-mail novo** (usuário existente criando outro grupo fica para depois).
- `AdminSentryController` (diagnóstico) ainda usa papel global (`[Authorize(Roles="Admin")]`).
- Provedor de resultados **OneFootball** implementado (jogos + placares), porém `Enabled=false` por
  padrão e os parsers ainda não validados contra o payload real da Copa.
- Notificação (e-mail) ao aprovar/rejeitar cadastro; refresh token.
- Teste de integração HTTP real dos gates `[RequireGroupAdmin]` (WebApplicationFactory).
- Finalização **parcial** de rodada não suportada; pontos fracionados ainda inteiros.
- Tag Sentry `group_role` (evitada para não custar query por request).
- API key de provider ainda em `appsettings.json` (a remover).

## 5. Arquivos principais alterados (multi-grupo)

**Backend (novos):** `Entities/{Group,GroupUser}.cs`, `Enums/{GroupRole,GroupUserStatus}.cs`,
`Services/Groups/{ICurrentGroupService,CurrentGroupService,IGroupService,GroupService,GroupQueries}.cs`,
`Auth/RequireGroup{Admin,Participant}Attribute.cs`, `Common/ForbiddenException.cs`,
`Controllers/GroupsController.cs`, `DTOs/Groups/GroupDtos.cs`,
`Migrations/*_AddGroupsAndTenancy.cs`.

**Backend (alterados):** `Data/AppDbContext.cs`, `Entities/{Season,Round,Standing,RoundParticipantResult,AuditLog}.cs`,
`Services/Auth/AuthService.cs` (+DTOs/Interface), `Services/Audit/AuditService.cs`,
`Services/Registrations/RegistrationRequestService.cs`, e os services escopados por grupo
(Rounds, Scoring, Standings, TemporaryStandings, Predictions, Absences, Seasons, Users, AdminPredictions,
PredictionImport/Ocr, Results, Scouts, Fixtures), todos os controllers admin/participante (troca de
atributos), `Program.cs`, `Middlewares/{ExceptionHandlingMiddleware,SentryUserContextMiddleware}.cs`.

**Frontend (novos):** `core/services/{group-context,groups}.service.ts`,
`core/interceptors/group.interceptor.ts`, `features/auth/create-group.ts`,
`features/groups/select-group.ts` (+ specs e `e2e/groups.e2e.ts`).

**Frontend (alterados):** `core/auth/{auth.service,auth.guard}.ts`, `app.{routes,config}.ts`,
`features/auth/{login.*,register.ts}`, `layout/shell.*`, `core/models/{models,enums}.ts`,
`public/i18n/{pt-BR,en-US}.json`, `e2e/support.ts`, `e2e/registration.e2e.ts`.

**CI:** `.github/workflows/deploy-iis.yml` (correção do runner self-hosted; ver §6 Decisões).

## 6. Decisões técnicas importantes

- **Rotas de controller SEM prefixo `api/`** (IIS monta em `/api`).
- **Escoamento de `GroupId`**: coluna só nas raízes de tenant; entidades por-rodada derivam o grupo pelo
  pai (`Round`/`Season`), e toda query valida que a rodada/temporada pertence ao grupo atual. **`Team`
  permanece global** (catálogo de clubes reais). Isolamento garantido pela validação no backend (403).
- **Roster por grupo** via `GroupQueries.ActiveParticipants/AllParticipants` (associação aprovada), não
  pelo `User.Role` global.
- **Grupo atual via header `X-Group-Id`** (não no JWT); o backend **sempre revalida** a membership.
- **Seed determinístico**: clubes recebem GUID derivado do nome (MD5) para o snapshot ficar estável.
- **Auto-migration no startup** (`db.Database.Migrate()`); testes usam SQLite `EnsureCreated`.
- **CI self-hosted (IIS):** o runner ficou com ambiente poluído por Visual Studio após configurar um 2º
  runner, **e um `global.json` (adicionado por engano) forçava o SDK 10.0.301 cujo resolver estava
  quebrado**. Correção: **remover o `global.json`** (volta ao SDK default 10.0.100 que funciona). NÃO
  reintroduzir `global.json` sem necessidade. `ci.yml` (ubuntu) usa `setup-dotnet 10.0.x`.

## 7. Regras de negócio principais

- **Ciclo da rodada:** Draft → Published → Locked → Scored/Cancelled. Pontuar exige `Locked` + todos os
  placares preenchidos.
- **Pontuação:** categorias por acerto (coluna/placar exato etc.) × multiplicador (Big Seven, fase,
  competição; override manual justificado). League One e clássicos têm multiplicadores especiais.
- **Ausências:** participante que não palpitou tudo fica ausente; 3ª/4ª ausência = −20; 5ª = eliminação.
- **Regra Flávio:** a partir da rodada 16, líder(es) com prazo especial têm pontos da rodada pela metade
  se palpitarem após o prazo.
- **Acesso por grupo:** participante só vê o grupo onde `GroupUser.Status = Approved`; admin gerencia só
  o próprio grupo; header `X-Group-Id` sempre validado (403 caso contrário).

## 8. Comandos

```bash
# Banco (dev)
docker compose up -d            # PostgreSQL

# Backend
cd backend
dotnet build Palpitao.slnx -c Release
dotnet test  Palpitao.slnx -c Release          # 317 testes
dotnet run --project src/Palpitao.Api          # aplica migrations no startup

# Frontend
cd frontend
npm ci
npm run build                                  # ng build
npm test -- --watch=false                      # Vitest (35)
npm run e2e                                    # Playwright (23)
npx prettier --check "src/app/**/*.{ts,html}"  # gate do CI
```

## 9. Próximos passos recomendados

1. Validar os parsers do `OneFootball*Provider` contra o payload real da Copa e ligar
   `ResultsProvider:Enabled` (e opcionalmente `ResultsRefresh:Enabled`) quando confirmado.
2. Notificação por e-mail ao aprovar/rejeitar; refresh token; teste de integração dos gates de grupo.
3. Remover a API key do `appsettings.json` quando sair de testes.
4. (Opcional) Tela dedicada de administração da plataforma para o SuperAdmin (listar/gerenciar grupos,
   ativar/desativar, transferir owner) — hoje o SuperAdmin entra em qualquer grupo via `/select-group`.
5. (Opcional) UI dedicada de badges de multiplicador x4/x6 e de busca de jogos da Copa.
6. (Opcional) Ícones PNG 192/512 + completar o `manifest.webmanifest` para PWA instalável.
