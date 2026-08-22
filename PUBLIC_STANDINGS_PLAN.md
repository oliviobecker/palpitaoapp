# Classificação pública por chave de temporada (com auditoria de pontos)

_Plano aprovado em 2026-08-22. **Implementado por completo** (fases 1–6). Documentado no [README §28](README.md). Pendente de verificação: aplicar a migration num Postgres real._

## Contexto

Hoje **toda** leitura de classificação exige login + `X-Group-Id` revalidado no servidor. Quem não é
do bolão — ou o participante que só quer conferir uma conta no celular sem entrar no app — não tem
como ver nada. E quando alguém contesta um placar ("por que o Flávio levou 6 nesse jogo?"), a única
resposta possível hoje é um admin abrir `/admin/rounds/:id/audit` e narrar o resultado.

Esta feature dá a cada temporada uma **chave pública auto-gerada** (inclusive para as já existentes,
regenerável pelo admin) e uma **tela pública de classificação** acessível por essa chave — por path
ou querystring — que serve como **auditoria**: escolher rodada e participante e ver, jogo a jogo,
quantos pontos saíram e **por qual critério** (categoria do placar, pontos-base, multiplicador,
clássico, override manual, Regra do Flávio, ausência).

O resultado pretendido: um link colável no grupo do WhatsApp que explica sozinho a pontuação, sem
conta, sem app, sem admin no meio.

---

## Decisões

Confirmadas com o usuário:

| # | Decisão |
|---|---|
| 1 | A tela tem **duas abas**: *Geral* (classificação oficial da temporada) e *Rodada* (temporária ao vivo). A auditoria jogo-a-jogo está nas duas. |
| 2 | **Só rodadas fechadas** (`Locked`/`Scored`) aparecem no link. `Draft`/`Published`/`Cancelled` nunca. |
| 3 | Chave = **código curto auto-gerado**, e "alterável" = botão **Gerar nova chave** (invalida a anterior). |
| 4 | Participantes identificados pelo **mesmo nome exibido no app**. |

Tomadas neste plano (com a justificativa):

| # | Decisão | Por quê |
|---|---|---|
| 5 | Nova flag `Season.PublicStandingsEnabled`, **default `false`**. A chave existe para todas as temporadas, mas **só resolve quando o admin liga**. | Sem isto, o deploy geraria chave para toda temporada existente e a feature passaria a expor dados sem ninguém ter pedido. Seguro por padrão; o admin liga quando quiser. |
| 6 | O DTO público é **próprio** (`Public*Dto`); `MatchScoreDto`/`RoundResultsDto` ficam intactos. | O palpite alheio precisa aparecer na auditoria. Adicioná-lo ao DTO compartilhado vazaria palpites em `GET /rounds/{id}/results`, que participantes acessam — regressão direta do README §27. |
| 7 | Chave = **12 caracteres hex maiúsculos**, exibida em 3 grupos (`A7C3-9F2E-4BD8`), guardada sem hífens. | 16¹² ≈ 2,8×10¹⁴ combinações. Hex não contém as letras ambíguas (O, I, L, S), então é ditável por voz. E permite backfill trivial em SQL puro com o **mesmo formato** que o gerador C# — um formato só em toda a base. |
| 8 | Resposta pública envia `X-Robots-Tag: noindex, nofollow`. | Nome de participante em página pública não deve cair no Google. |

⚠️ **Consequência a comunicar ao admin na própria tela:** ligar a publicação torna os palpites das
rodadas fechadas visíveis a **qualquer pessoa com o link**, independentemente de
`AllowParticipantsToViewOthersPredictions` (que hoje mantém o espelho admin-only por padrão). Isso é
intencional — auditoria sem o palpite ao lado não explica nada — mas precisa ser dito com todas as
letras no card do admin, não escondido num tooltip.

---

## Design da tela (UX/UI)

### Princípio

A auditoria **não é uma tela separada**. Obrigar o leitor a escolher rodada → escolher participante →
navegar → voltar para comparar o próximo é o desenho que mata esse tipo de tela. Aqui, **a linha da
classificação expande no lugar** e revela o detalhe. O contexto (quem está em volta, quantos pontos
de diferença) nunca sai da vista.

### Layout — mobile-first

```
┌────────────────────────────────────────────┐
│ ⚽ Palpitão · Turma do Zé          PT|EN ☾ │  header próprio, sem nav do app
│ England 2025/26 · consulta pública         │
├────────────────────────────────────────────┤
│ ┌────────┬─────────┐                       │
│ │ Geral  │ Rodada  │   ← btn-group          │
│ └────────┴─────────┘                       │
│                                            │
│  [ Rodada 18 · 12–14 mai        ▾ ]        │  só na aba Rodada
│  [ Todos os participantes       ▾ ]        │  filtro opcional
├────────────────────────────────────────────┤
│   🥇 Flávio    🥈 Ana    🥉 Zé             │  pódio (aba Geral, ≥3)
│    187          181       174              │
├────────────────────────────────────────────┤
│  1  Flávio Barros              187      ⌄  │  ← linha inteira é o botão
│  2  Ana Prado                  181      ⌄  │
│  3  Zé Carlos          eliminado 174    ⌄  │
└────────────────────────────────────────────┘
```

### Linha expandida — o miolo da auditoria

```
│  1  Flávio Barros              187      ⌃  │
│  ────────────────────────────────────────  │
│  Arsenal 2 × 1 Chelsea                     │
│  [PL] [×2] [Clássico]                      │
│  Palpite 2 × 1 · Placar exato, Tradicional │
│                        3 × 2 = +6          │
│  ────────────────────────────────────────  │
│  Fulham 0 × 0 Brentford                    │
│  [PL]                                      │
│  Palpite 1 × 0 · Errou                     │
│                        0 × 1 =  0          │
│  ────────────────────────────────────────  │
│  Bruto 24 · Regra do Flávio −12 · Total 12 │  rodapé só com o que se aplica
└────────────────────────────────────────────┘
```

Cada jogo mostra **a conta inteira, explícita**: `pontos-base × multiplicador = pontos`. É isso que
transforma a tela em auditoria e não em placar. Os badges (`[PL]`, `[×2]`, `[Clássico]`,
`[Mult. manual]`, fase) são os mesmos já usados em `admin-round-audit.ts` — reuso direto de
`app-competition-badge` e `app-multiplier-badge`.

O rodapé só imprime a linha que existe: sem Flávio e sem ausência, mostra apenas o total. Nada de
`Penalidade: 0`.

### Estados especiais

| Situação | Tratamento |
|---|---|
| Participante ausente na rodada | Badge *Ausente*, jogos colapsados, rodapé `Ausência · rodada vale 0` |
| Regra do Flávio aplicada | Badge *Regra do Flávio*, rodapé `Bruto 24 · Flávio −12 · Total 12` |
| Eliminado | Badge *Eliminado* na linha da aba Geral (igual ao app) |
| Rodada `Locked` (ainda não pontuada) | Banner `Parcial — resultados ainda chegando`, contagem `9 de 10 jogos computados`; ausências, Flávio e eliminação **não** se aplicam (README §25) |
| Jogo sem resultado | Linha em cinza, `—` no lugar da conta |

### Rodapé — "Como se pontua"

Bloco colapsável, fechado por padrão, alimentado pelo **ruleset da temporada** (`GetRuleSetAsync`),
nunca hardcoded — o admin pode ter editado os valores em `/admin/scoring`:

```
▸ Como se pontua
   Coluna certa .......... 1     Incomum ............  7
   Tradicional ........... 3     Extra-incomum ...... 10
   Mediano ............... 5     Errou ..............  0
   Os pontos do jogo são multiplicados pelo peso da partida.
```

### Aba Geral vs. aba Rodada

- **Geral** — classificação oficial acumulada. Expandir mostra o **histórico por rodada** (uma linha
  por rodada: número, pontos, badges de ausência/Flávio). Clicar numa rodada leva à aba *Rodada* já
  com aquele participante aberto.
- **Rodada** — seletor de rodada + participantes ordenados pelos pontos daquela rodada. Expandir dá o
  breakdown jogo a jogo. Este é o caminho principal da auditoria.

### Deep-link

Uma auditoria só é útil se o recorte for compartilhável. A URL carrega o estado inteiro:

| URL | Efeito |
|---|---|
| `/p/A7C3-9F2E-4BD8` | abre a aba Geral |
| `/p?key=A7C39F2E4BD8` | idem (querystring, com ou sem hífens) |
| `/p/A7C3-9F2E-4BD8?rodada=18` | aba Rodada, rodada 18 |
| `/p/A7C3-9F2E-4BD8?rodada=18&participante=<userId>` | rodada 18 com aquele participante já expandido |

Cada interação faz `router.navigate(..., { queryParams, replaceUrl: true })` — voltar no browser não
vira um labirinto de estados.

### Desktop

Mesmo conteúdo, tabela em vez de cards — exatamente o switch já usado em
[standings.html:36](frontend/src/app/features/standings/standings.html:36):
`d-md-none` para os cards, `table-responsive d-none d-md-block` para a tabela. Nada de media query
nova.

### Acessibilidade

Acordeão como `<button>` real com `aria-expanded`/`aria-controls` (o
[admin-round-audit.ts:56](frontend/src/app/features/admin/admin-round-audit.ts:56) já usa botão, mas
sem os aria — corrigir na versão pública). Contraste dos badges herda os tokens do tema. Animações já
respeitam `prefers-reduced-motion` via `styles.scss`.

---

## Backend

### 1. Modelo — `Entities/Season.cs`

```csharp
/// <summary>Chave curta e opaca do link público de classificação (12 hex, sem hífens).</summary>
public string PublicKey { get; set; } = string.Empty;

/// <summary>Quando false (padrão), a chave não resolve: o link responde 404.</summary>
public bool PublicStandingsEnabled { get; set; }
```

Configuração em `Data/AppDbContext.cs`, bloco `Season` (linhas 179-194), seguindo o padrão de
`Group.Slug` (linha 152):

```csharp
e.Property(x => x.PublicKey).HasMaxLength(32).IsRequired();
e.HasIndex(x => x.PublicKey).IsUnique();
```

### 2. Gerador — `Common/PublicKeyGenerator.cs` (novo)

Static, `RandomNumberGenerator.GetBytes` → hex maiúsculo de 12 chars. Mesmo lugar e estilo de
`Common/PasswordPolicy.cs`. Duas funções: `Generate()` e `Normalize(string)` (remove hífens/espaços,
`ToUpperInvariant`) — a normalização é usada tanto no lookup quanto na validação do admin.

### 3. Migration

`dotnet ef migrations add AddSeasonPublicKey --project src/Palpitao.Api`

Template: [20260802165419_AddSeasonFaCupEnabled.cs](backend/src/Palpitao.Api/Migrations/20260802165419_AddSeasonFaCupEnabled.cs)
(coluna + backfill + `DROP DEFAULT`) combinado com o `migrationBuilder.Sql` de
`20260617204931_MoveTournamentTypeToSeason.cs:25-27`. Editar a migration gerada para:

1. `AddColumn<string>("PublicKey", nullable: true)` e `AddColumn<bool>("PublicStandingsEnabled", defaultValue: false)`.
2. Backfill — **um valor distinto por linha** (nenhuma migration existente faz isso hoje; é o único
   ponto sem precedente direto):
   ```sql
   UPDATE "Seasons"
      SET "PublicKey" = upper(substr(md5(random()::text || clock_timestamp()::text || "Id"::text), 1, 12))
    WHERE "PublicKey" IS NULL;
   ```
3. `AlterColumn` para `nullable: false`, `DROP DEFAULT` do bool, e `CreateIndex(..., unique: true)`.

O mesmo formato do gerador C#, então a base fica com um único padrão de chave.

### 4. Serviço — `Services/Standings/PublicStandingsService.cs` (novo)

⚠️ **Não injeta `ICurrentGroupService`.** Todos os serviços de leitura existentes
(`StandingsService.cs:109`, `RoundScoringService.cs:304`, `TemporaryStandingsService.cs:29`) chamam
`_current.GetGroupIdAsync` na primeira linha e estouram 403 sem sessão — nenhum deles é reutilizável
como está.

⚠️ **Toda query usa `.IgnoreQueryFilters()` + filtro explícito.** O filtro global
(`AppDbContext.cs:73-81`) é `CurrentGroupId == null || e.GroupId == CurrentGroupId`: sem header ele
**abre todos os grupos** (não protege nada — ver `TenantQueryFilterTests.cs:65-76`), e se o browser
mandar `X-Group-Id` de outro grupo ele **esconde** a temporada e produz um 404 espúrio. A proteção
real aqui é a própria chave, que resolve para exatamente uma `Season`.

Fluxo comum a todos os métodos:

```
Normalize(key) → Season (IgnoreQueryFilters, PublicStandingsEnabled == true)
              → seasonId + groupId derivados da própria season
              → todas as queries seguintes filtram por esses dois valores, explicitamente
              → rounds restritos a Status ∈ { Locked, Scored }
```

Chave inexistente, publicação desligada ou rodada não-fechada → `NotFoundException` (mesma resposta,
para não vazar por diferença de erro qual chave existe).

Três métodos:

| Método | Conteúdo |
|---|---|
| `GetSeasonAsync(key)` | nome do grupo e da temporada, tipo de certame, lista de rodadas visíveis, ruleset resumido para a legenda |
| `GetStandingsAsync(key)` | classificação oficial — mesma projeção de `StandingsService.GetStandingsAsync:124-144`, com `groupId` explícito no lugar do `_current` |
| `GetRoundAsync(key, roundNumber)` | partidas + participantes + breakdown por jogo |

`GetRoundAsync` tem **duas fontes**, conforme o estado da rodada:

- **`Scored`** → lê a verdade persistida: `PredictionScores` (BasePoints, Multiplier, FinalPoints,
  ScoreCategory, IsExactScore, IsCorrectColumn) + `RoundParticipantResults` (GrossPoints, FinalPoints,
  PenaltyPoints, WasAbsent, WasEliminated, FlavioRuleApplied). **Nada é recalculado.**
- **`Locked`** → calcula ao vivo, reusando `IScoringService` exatamente como
  [TemporaryStandingsService.cs:97-105](backend/src/Palpitao.Api/Services/Standings/TemporaryStandingsService.cs:97)
  (`GetCategory` → `GetBasePoints` → `ManualMultiplierOverride ?? GetMultiplier`), sem ausências, sem
  Flávio, sem eliminação (README §25). Resposta marca `isPartial: true`.

O placar palpitado vem de `Predictions` (`PredictedHomeScore`/`PredictedAwayScore`) — o único campo da
auditoria que não está em `PredictionScore`.

### 5. DTOs — `DTOs/Public/PublicStandingsDtos.cs` (novo)

`PublicSeasonDto`, `PublicRoundSummaryDto`, `PublicRoundDto`, `PublicParticipantScoreDto`,
`PublicMatchScoreDto` (o breakdown + `predictedHomeScore`/`predictedAwayScore`), `PublicRulesetDto`.

Sem `email`, sem `predictionId`, sem `ManualMultiplierJustification` (texto administrativo interno —
expor só o booleano `isManualMultiplier`).

### 6. Controller — `Controllers/PublicStandingsController.cs` (novo)

```csharp
[ApiController]
[Route("public/seasons")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class PublicStandingsController : ControllerBase
```

Espelha [GroupsController.cs:9-11](backend/src/Palpitao.Api/Controllers/GroupsController.cs:9).

⚠️ **Controller separado é obrigatório, não preferência.** `[RequireGroupAdmin]` e
`[RequireGroupParticipant]` (`Auth/RequireGroup*Attribute.cs`) são **action filters**, não
authorization filters: `[AllowAnonymous]` num método de `SeasonsController` desligaria o `[Authorize]`
mas **não** o filtro, que ainda chamaria `RequireApprovedMemberAsync` e devolveria 403.

Rotas: `GET /public/seasons/{key}`, `/{key}/standings`, `/{key}/rounds/{number:int}`.
Middleware simples no controller adiciona `X-Robots-Tag: noindex, nofollow`.

### 7. Rate limit — `Program.cs`

Nova policy `"public"` no bloco 109-168, particionada por IP igual à policy `"auth"` (linhas 112-120),
lida de `RateLimiting:Public` com default mais generoso (60/min — é leitura barata e um grupo inteiro
abre o link ao mesmo tempo no fim da rodada). Seção correspondente em `appsettings.json`.

### 8. Admin — chave na temporada

- `SeasonDto` ganha `PublicKey` + `PublicStandingsEnabled`; `ProjectExpr` e `Map` em
  [SeasonService.cs:45-57](backend/src/Palpitao.Api/Services/Seasons/SeasonService.cs:45).
- `CreateAsync` gera a chave junto com o `Guid.NewGuid()` (linha 67).
- `SeasonRequest` ganha `PublicStandingsEnabled` (o toggle); a chave **não** vem do request.
- Novo `POST /seasons/{id}/public-key/regenerate` em `SeasonsController` com `[RequireGroupAdmin]`,
  seguindo o formato de `activate` (linhas 56-59).
- Auditar com `_audit.Add(...)`, eventos `SeasonPublicKeyRegenerated` e `SeasonPublicStandingsToggled`
  — mesmo padrão de `SeasonCreated` (linhas 84-91).

---

## Frontend

### 1. Rota — `app.routes.ts`

Entrada de topo, **antes** do bloco `{ path: '', … Shell }` (linha 31) e do `**` (linha 170), sem
`canActivate` — mesma posição de `login`/`register`:

```ts
{ path: 'p', loadComponent: () => import('./features/public/public-standings').then(m => m.PublicStandings) },
{ path: 'p/:key', loadComponent: () => import('./features/public/public-standings').then(m => m.PublicStandings) },
```

Nova pasta `features/public/`. Fica fora do Shell — sem navbar, sem bottom nav, sem
`GroupContextService`.

### 2. Interceptors — o opt-out que não existe hoje

⚠️ `group.interceptor.ts:10-16` e `auth.interceptor.ts:6-12` anexam `X-Group-Id` e `Authorization` em
**toda** requisição, sem nenhuma exclusão por URL ou context. Um admin logado no grupo A abrindo o
link do grupo B mandaria o header errado.

Seguir o mecanismo já estabelecido em
[http-context.ts](frontend/src/app/core/interceptors/http-context.ts) (`SKIP_ERROR_TOAST`,
`SKIP_AUTH_REFRESH`):

```ts
/** Rotas públicas por chave: não devem carregar sessão nem tenant do usuário logado. */
export const SKIP_TENANT_HEADERS = new HttpContextToken<boolean>(() => false);
```

Guarda no topo dos dois interceptors: `if (req.context.get(SKIP_TENANT_HEADERS)) return next(req);`

Estender [group.interceptor.spec.ts](frontend/src/app/core/interceptors/group.interceptor.spec.ts)
(que já testa o header) com o caso do skip.

### 3. Serviço — `core/services/public-standings.service.ts` (novo)

Padrão idêntico a
[scoring-config.service.ts](frontend/src/app/core/services/scoring-config.service.ts) (21 linhas):
`inject(HttpClient)`, `base = ${environment.apiBaseUrl}/public/seasons`, métodos `Observable<T>`.
Todos passam `{ context: new HttpContext().set(SKIP_TENANT_HEADERS, true) }`.

### 4. Componente — `features/public/public-standings.ts`

Standalone + signals + OnPush. Reusa `EmptyState`, `ErrorState`, `SkeletonList`, `Icon`,
`CompetitionBadge`, `MultiplierBadge`, `phaseLabel` — e **a estrutura de acordeão de
[admin-round-audit.ts](frontend/src/app/features/admin/admin-round-audit.ts)** (`toggle`/`isOpen` com
`Set<string>`, `score(userId, matchId)`, `categoryKey`), que é literalmente esta tela sem os seletores
e sem o gate de admin.

Ordem obrigatória `loading → error → empty → content` (CLAUDE.md). Erro de chave inválida usa
`app-empty-state` com mensagem própria — não um toast de erro genérico.

Header próprio da página com o toggle PT/EN no padrão de
[register.ts:38-55](frontend/src/app/features/auth/register.ts:38) (`btn-group btn-group-sm`) e o
toggle de tema. Abas e seletores: `btn-group` e `<select class="form-select">` — **não existem**
componentes de tabs/segmented/select no `shared/`, e criar um só para esta tela seria inventar padrão
novo.

Estilos: manter dentro do budget de 4 kB/componente; o que for compartilhado (pódio já existe como
`.podium` inline em `standings.ts:28-107`) vai para `styles.scss`.

### 5. Admin — card da chave pública

Em `features/admin/admin-seasons.*`, junto dos toggles de temporada já existentes:

```
┌──────────────────────────────────────────────┐
│ Classificação pública            [ ● ]  ligado│
│                                              │
│  A7C3-9F2E-4BD8              [copiar] [↻]    │
│  palpitao.app/p/A7C3-9F2E-4BD8   [copiar]    │
│                                              │
│  ⚠ Com a publicação ligada, qualquer pessoa  │
│    com o link vê a classificação e os        │
│    palpites das rodadas já fechadas.         │
└──────────────────────────────────────────────┘
```

"Gerar nova chave" passa por `ConfirmService` (o link antigo morre na hora). Copiar usa
`shared/utils/clipboard`.

### 6. i18n

Novo bloco de nível 1 `publicStandings` em **ambos** `pt-BR.json` e `en-US.json` (a estrutura é
exatamente 2 níveis). Reusar as chaves que já existem: `category.*`, `results.absent`,
`results.flavioApplied`, `results.manualMultiplier`, `standings.*`, `temporaryStandings.*`.

Ícones novos usados (`link`, `copy`, `refresh-cw`, …) precisam entrar em `provideLucideIcons(...)` em
`app.config.ts` — import sem registro é erro de lint.

Verificar paridade com o comando do CLAUDE.md antes de fechar.

---

## Testes

**Backend** (`tests/Palpitao.Api.Tests/`, xUnit + SQLite in-memory):

- `Standings/PublicStandingsServiceTests.cs` — chave válida resolve; chave normalizada com hífens
  resolve; chave inexistente → 404; `PublicStandingsEnabled = false` → 404; rodada `Draft`/`Published`
  **não** aparece; rodada `Scored` traz o breakdown persistido; rodada `Locked` calcula ao vivo e marca
  `isPartial`; **temporada de outro grupo nunca vaza** (dois grupos no seed, igual a
  `Groups/TenantQueryFilterTests.cs`).
- `Public/PublicStandingsControllerTests.cs` — atributos por reflexão, no padrão de
  [HealthControllerTests.cs:89-94](backend/tests/Palpitao.Api.Tests/Health/HealthControllerTests.cs:89):
  tem `[AllowAnonymous]`, **não** tem `[RequireGroupParticipant]`/`[RequireGroupAdmin]`.
- `Seasons/…` — chave gerada na criação; regenerar troca o valor e audita; unicidade.
- `Common/PublicKeyGeneratorTests.cs` — formato, tamanho, normalização.

**Frontend:**

- `core/interceptors/group.interceptor.spec.ts` — estender: com `SKIP_TENANT_HEADERS`, não clona.
- `e2e/public-standings.e2e.ts` — seguir
  [registration.e2e.ts](frontend/e2e/registration.e2e.ts), que é o único e2e **sem** `seedAuth`:
  mockar `GET /public/seasons/*`, abrir `/p/CHAVE`, trocar de aba, expandir participante, conferir a
  conta `3 × 2 = +6` na tela, e **asserir que a requisição não levou `x-group-id`** (`req.headers()`).

**Verificação manual:**

```bash
cd backend && dotnet build Palpitao.slnx && dotnet test tests/Palpitao.Api.Tests/Palpitao.Api.Tests.csproj
```
```bash
cd frontend && npm run lint && npm run format:check && npm test -- --watch=false && npm run e2e
```

Ponta a ponta: `docker compose up -d` → `dotnet ef database update` (confirmar que as temporadas
existentes ganharam chave distinta) → ligar a publicação em `/admin/seasons` → abrir `/p/<chave>` numa
**janela anônima** (prova que funciona sem sessão) → conferir uma rodada `Scored` contra
`/admin/rounds/:id/audit` (os números têm de bater exatamente) → regenerar a chave e confirmar que o
link antigo dá 404.

---

## Ordem de execução

1. ~~**Modelo + migration + gerador** — coluna, flag, backfill, índice único.~~ **Feito.**
   `Common/PublicKeyGenerator.cs`, os dois campos em `Season`, o índice único e a migration
   `20260822165521_AddSeasonPublicKey` (backfill com valor distinto por linha). O invariante da
   chave é garantido em `AppDbContext.SaveChanges` (`StampPublicKeys`), não só no `SeasonService` —
   a coluna é única e obrigatória, então qualquer caminho de escrita que esquecesse a chave
   quebraria na segunda temporada. 21 testes novos.
   ⚠️ **Pendente:** aplicar a migration num Postgres real e conferir que cada temporada existente
   recebeu uma chave distinta. O SQL gerado foi inspecionado (`dotnet ef migrations script`), mas
   não executado — não havia Docker no ambiente.
2. ~~**Serviço público + DTOs + controller + rate limit.**~~ **Feito.**
   `PublicStandingsService` (resolve a chave, deriva o tenant da season, `IgnoreQueryFilters()` +
   filtro explícito em toda query), `DTOs/Public/`, `PublicStandingsController` sob `public/seasons`
   e a policy de rate limit `public` (60/min por IP).
   Acrescentado ao plano original: o atributo **`[IgnoreRequestGroup]`**, lido por
   `RequestGroupContext`. Sem ele, um `X-Group-Id` de outro grupo — que qualquer browser logado
   envia — faria o filtro global *esconder* a temporada e devolver 404. O atributo resolve isso
   para todo serviço reusado no caminho público, incluindo `GetRuleSetAsync`.
   25 testes: resolução de chave, indistinguibilidade de chave inexistente vs. não publicada,
   isolamento entre dois grupos, invisibilidade de rodada aberta, breakdown persistido vs. ao vivo,
   override manual de multiplicador, e os atributos do controller por reflexão.
3. ~~**Admin** — `SeasonDto`, toggle, regenerar, auditoria.~~ **Feito.**
   Backend: `PublicKey`/`PublicStandingsEnabled` no `SeasonDto`, o toggle no `SeasonRequest`,
   `POST /seasons/{id}/public-key/regenerate` e os eventos de auditoria.
   Frontend: toggle com o aviso de exposição no formulário, e o bloco do link em cada temporada
   publicada (chave formatada `XXXX-XXXX-XXXX`, copiar link, gerar nova chave via `ConfirmService`).
   9 chaves i18n novas nos dois idiomas (paridade verificada). 6 testes novos.
4. ~~**Frontend público**~~ **Feito.** `SKIP_TENANT_HEADERS` em `http-context.ts` com guarda nos
   interceptors de auth e group, `PublicStandingsService`, o componente `features/public/`, os
   modelos TS, a rota `/p` + `/p/:key` fora do Shell, e o bloco i18n `publicStandings` nos dois
   idiomas.
5. ~~**Polimento**~~ **Feito.** Deep-link (`?rodada=`, `?participante=`, `?key=`) com `replaceUrl`,
   legenda "Como se pontua" vinda do ruleset da temporada, e 5 e2e — incluindo um que prova que a
   página **não** envia `x-group-id` nem `authorization` mesmo com sessão de outro grupo.
6. ~~**Documentação**~~ **README §28** ("Public standings link"), inserida após a §27 que ela toca;
   as seções seguintes foram renumeradas (nenhuma referência cruzada apontava para elas).
   `DEVELOPMENT_CHECKPOINT.md` deixado intocado — tem edições locais não commitadas.

---

## Riscos

| Risco | Mitigação |
|---|---|
| **Isolamento multi-tenant.** O filtro global do EF não protege caminho anônimo — ele abre tudo. | Filtro explícito por `seasonId`/`groupId` derivados da própria chave, em toda query. Teste com dois grupos no seed. |
| **Vazamento de palpites.** A tela mostra palpites alheios, que o app mantém privados por padrão (README §27). | Gate duplo: `PublicStandingsEnabled` (default off) + só rodadas `Locked`/`Scored`. Aviso explícito no card do admin. |
| **`[AllowAnonymous]` não desliga os filtros de grupo.** | Controller separado. Teste por reflexão garante que ninguém acrescente os atributos depois. |
| **Recalcular temporada não é idempotente** (`DEVELOPMENT_CHECKPOINT.md` §7a.1: `RecalculateSeasonCoreAsync` não limpa `Standings`, e o Flávio lê essa tabela). Uma tela pública que promete explicar cada ponto torna essa incoerência visível ao grupo inteiro. | Fora do escopo desta feature, mas **registrar como bug conhecido** antes de divulgar o link. Vale corrigir antes de anunciar a feature ao grupo. |
| **Enumeração de chaves.** | 16¹² combinações + rate limit por IP + 404 idêntico para chave inexistente e publicação desligada. |

---

## Segunda rodada — avaliação de UX e o que mudou

Uma revisão da funcionalidade **inteira** (não só da tela) apontou que o valor não estava chegando
ao leitor: o link só existia numa tela de configuração, o preview no WhatsApp saía em inglês e sem
imagem, a tela pública era visualmente mais pobre que a interna, e as duas perguntas que o grupo
realmente faz não tinham resposta. Tudo abaixo está **implementado**.

### Distribuição

- A **mensagem de fechamento** (`buildClosingMessage`) termina com um deep link para a auditoria da
  rodada que acabou de fechar — `…/p/<chave>?rodada=N` — quando a publicação está ligada. O link vai
  numa linha própria, sem `*…*`: o negrito do WhatsApp quebra o autolink.
- `shared/utils/public-link.util.ts` passa a ser o dono de `formatPublicKey` e `publicStandingsUrl`,
  usado pelo admin de temporadas e pelo detalhe de rodada.
- O card do admin mostra a **URL completa** e ganhou **Pré-visualizar** — o admin vê o que está
  publicando antes de divulgar.
- `index.html` ganhou **Open Graph** com `public/og-cover.jpg` (1200×630). Conteúdo genérico de
  produto, sem nome de grupo nem de participante: o crawler não roda JS e nunca veria a temporada,
  e um card com nomes entregaria justamente o que o `noindex` protege.

### Aba Geral

Pódio, avatar com iniciais e **colunas no desktop** (rodadas, ausências, diferença, pontos), busca
por nome, diferença para o líder e para quem está acima, e um botão **"Sou eu"** que marca a linha
do leitor — guardado no `localStorage` daquele navegador, nunca enviado a lugar nenhum. Abrir uma
linha revela o **histórico por rodada**: um chip por rodada pontuada, com marcadores de ausência e
Regra do Flávio, e cada chip é um deep link para aquela rodada com o participante já aberto.

### Aba Rodada

Alternância **Por participante | Por jogo**. O corte por jogo mostra, para uma partida, o palpite,
o critério e os pontos de todo mundo, do maior para o menor — a resposta direta a *"quem acertou o
Arsenal?"*. É um pivô 100% no cliente: a resposta da rodada já traz os dois lados. Somou-se também
**Expandir/recolher todos** (um print da rodada inteira numa ação) e jogo sem resultado passa a
aparecer esmaecido com *"Aguardando"* em vez de um `—` que se confundia com "errou".

### Backend

`GET /public/seasons/{key}/standings` passou a devolver `PublicStandingRowDto`, que espelha o
`StandingDto` do app e acrescenta `Rounds` (número, pontos, ausência, Flávio) — **só rodadas
`Scored`**, numa única query com o mesmo `IgnoreQueryFilters()` + filtro explícito por
`GroupId`/`SeasonId` das demais. 4 testes novos, incluindo o de vazamento entre grupos com o mesmo
número de rodada nos dois.

### Acessibilidade e acabamento

`aria-controls` nos painéis expansíveis, feedback de hover/foco no botão do acordeão (era
`bg-transparent border-0`, sem nenhum sinal de que era clicável), `aria-label` do seletor de idioma
traduzido, foco levado ao seletor de rodada ao pular de aba, datas da rodada no `<select>` (já
vinham na resposta e eram ignoradas), aviso quando o link aponta para uma rodada não publicada,
URL preservando todos os participantes abertos, e um rodapé com "Conheça o Palpitão" — a página é
a melhor superfície de aquisição do produto e não levava a lugar nenhum.

### Duplicações removidas no caminho

`initials()`/`avatarColor()` existiam em `standings.ts` **e** `dashboard.ts`, com luminosidade
diferente (42% vs 45%) — a mesma pessoa tinha cor diferente em cada tela. E `.rank-avatar` estava
definido duas vezes, em tamanhos diferentes. Hoje: `shared/utils/avatar.util.ts` e uma regra só em
`styles.scss`, junto com `.podium*`.

**Verificação:** 764 testes de backend, 127 unitários, 59 e2e, lint limpo, build de produção OK,
paridade de i18n confirmada.
