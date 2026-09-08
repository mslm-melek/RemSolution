# RemSolution — working notes for Claude

Read this instead of re-scanning. It records what the product **is** and the
rules the code holds itself to. Where a rule has a longer explanation, the
pointer is to the file that owns it, not a copy of it.

---

## 1. The product

A **multi-tenant SaaS for car-rental agencies**, plus a **public marketplace**
where customers search and book across agencies. Market: Tunisia first, then
Maghreb / France (`hrmaps.fr` owns it). Three UI languages ship — `fr`
(default), `en`, `ar` with full RTL — and every string goes through Transloco.

Two audiences share one codebase: agency staff living in the back-office all
day, and marketplace customers who see two screens and decide whether to trust
us with a passport scan.

### Actors

| Role | Tenant-scoped | What it is |
|---|---|---|
| `PlatformAdministrator` | **No** (never carries an `AgencyId` claim) | Runs the platform: agencies, plans, subscriptions. Cross-tenant reads only through the audited `ICrossTenantAccess` path. Can *enter* an agency workspace (`PlatformAdminImpersonationMiddleware`), every entry audited. |
| `AgencyAdministrator` | Yes | Runs one agency. Holds **every** permission implicitly. |
| `AgencyStaff` | Yes | The role only labels them staff; what they can do is their `UserPermission` claim set. No authorization is keyed on this role. |
| `Customer` | No | Self-registered marketplace customer. Gated by the `CustomerOnly` policy; browse is anonymous. |

### Capability model (two independent axes — both must pass)

- **Features** (`FeatureFlags`, 16 modules) are per-agency: a feature is on when
  the active subscription plan includes it, unless an `AgencyFeature` override
  row forces it. A feature the **plan excludes** is 403 for the whole module,
  agency administrator included, and hidden in the SPA. A **lapsed
  subscription** is a *write freeze*, not a lockout: reads pass and only writes
  are refused (`FeatureEnforcementBehaviour` +
  `SubscriptionEnforcementInterceptor` + `SubscriptionGuard`), so an agency
  behind on payment keeps sight of its own bookings — including the invoice it
  has to look at to pay.
- **Permissions** (`Permissions`, `Entity.Verb` names) are per-user, registered
  one authorization policy per permission.
- `FeatureCatalog` intersects them. The Team screen only offers permissions
  belonging to enabled modules.

### Core flow

`Car` → availability → `Reservation` (a hold) → `Renting` (the hire) →
`Payment` / `Facture` / `Contract`, with `Expense`, `ExtraService`,
`RentingFee`, `Credit`, `Chat`, `Notification`, `Statistics` around it.

Lifecycles (the enums carry the full doc comments — read them, don't guess):

- `ReservationStatus`: `PendingConfirmation → Confirmed → (Paid) → Converted`;
  off-ramps `Rejected` (reason mandatory, shown to the client), `Expired`
  (hourly Hangfire sweep), `Cancelled`. **Only Confirmed/Paid block a car** — a
  pending request does not, so several customers can ask for the same car and
  the same days; `ConfirmReservationCommand` is where the car is claimed and
  where a conflict is refused (naming what holds the period).
- `RentingState`: `NotYet → InProgress → Done`, plus `Cancelled`. Never
  physically deleted — financial record. `Done`/`Cancelled` don't block a car.
- `CarStatus`: only `Active` is bookable.

---

## 2. Architecture

Clean Architecture (Jason Taylor template, heavily replaced). `Web` is the
composition root.

```
Web  ──►  Application  ──►  Domain
 └──►  Infrastructure ──►──┘
```

- **Domain** — entities, enums, the `Money` value object, events, exceptions,
  constants. No external deps.
- **Application** — CQRS vertical slices under
  `Features/<Aggregate>/{Commands,Queries,DTOs,EventHandlers}`, one folder per
  use case. MediatR 12, FluentValidation 11, Mapster 10 (**not** AutoMapper —
  migrated away for a CVE + licence). Map a query with `ProjectToType<T>()` on
  the queryable; `x.Adapt<T>()` *inside* an EF projection compiles and runs but
  silently drops the DTO's `IRegister` mapping, so computed members come back
  null.
- **Infrastructure** — EF Core 10 / SQL Server, Identity, interceptors,
  Hangfire jobs, SkiaSharp imaging, QuestPDF documents, SMTP mail, file storage.
- **Web** — Minimal APIs grouped by `EndpointGroupBase` + reflection
  (`MapEndpoints`), NSwag (`/api`), Angular 18 SPA host.

Pipeline behaviours, in order: `UnhandledException` → `Authorization` →
`Validation` → `Performance` → `Logging`.

---

## 3. Non-negotiable conventions

Breaking one of these is a bug even when it compiles and the tests pass.

**Tenancy.** Every `ITenantEntity` carries a global EF query filter
`AgencyId == tenant.AgencyId` (composed with `!IsDeleted` where soft-deletable).
`TenantEntityInterceptor` stamps `AgencyId` on insert, never on update, and
throws `ForbiddenAccessException` on a cross-agency write. No tenant claim ⇒
tenant data matches nothing. `IgnoreQueryFilters()` is allowed **only** in the
`MarketplaceSearch` feature folder and the platform-admin referential check in
`DeleteAgencyCommand` — enforced by `TenantEnforcementTests`. Background jobs
have no HTTP context and push `AmbientTenant` instead.

**Money.** Monetary amounts are the `Money` value object (Amount + ISO-4217
Currency), never bare decimals. Currency is tenant-scoped: each `Agency` has one
`Currency` and handlers denominate in it (clients send only the amount). Mapped
as EF optional owned types via `OwnsMoney(...)`. Exposed as `MoneyDto`.
Subscription-plan prices stay decimal (platform-level, no agency).

**Currency conversion is display-only, and never touches a stored amount.** An
agency bills in one currency and its invoices, payments, credits and statistics
stay in it. `ExchangeRate` is a platform-level table of quoted pairs
(`1 From = Rate To`), one row per ordered pair — the reciprocal is **derived,
never stored**, because two rows that must agree eventually will not.
`GetDisplayRatesQuery` expands each quote into both directions for the
(anonymous) marketplace, so the browser only multiplies; chains are deliberately
not built. The SPA shows the converted figure as a **second line** beside the
agency's own price (`<app-converted-price>`), never instead of it, and no
endpoint accepts or returns a converted amount — which is what stops one being
posted back.

**Time.** All domain `DateTime` values are UTC; `UtcDateTimeConverter` (a global
convention) normalises on write and stamps `Kind = Utc` on read. Use
`TimeProvider.GetUtcNow()`, never `DateTime.Now`. Two kinds, and the SPA renders
them **differently**:

- *wall-clock* (a day/hour a person picked — `StartDate`, `EndDate`,
  `FirstCirculationDate`, `BirthDate`, `ExpenseDate`, `PayementDate`) →
  `{{ x | date:'…':'UTC' }}`
- *instant* (recorded from `GetUtcNow()` — `ExpiresAt`, `SentAt`, `SubmittedAt`,
  `IssuedAt`, `CreatedAt`) → `{{ x | date:'…' }}` (local)
- `DateTimeOffset` fields (`AgencySubscription`, `PlatformDashboardDto`) always
  render local.

Getting it backwards shifts a booking by the browser's offset. The two greps
that catch both directions are in `docs/PROJECT_OVERVIEW.md` §4 — run them after
touching a template. `agency-detail.component.html` and
`platform-dashboard.component.html` are expected hits (both `DateTimeOffset`).

**Soft delete is selective.** `Car`/`Client` are `ISoftDeletable`.
`ExpenseType`/`ExtraServicesType` use an `IsActive` flag. `Renting`/`Payment`/
`Reservation` are **never** deleted (financial records; `Client` FKs are
`Restrict`). Unique indexes are filtered (`… WHERE IsDeleted = 0`) so an
archived row frees its key.

**Locks.** `AcquireCarWriteLockAsync(carId)` for availability paths;
`AcquireTenantWriteLockAsync()` for quota paths. When both are needed the
**agency lock comes first** or two writers deadlock.

**Concurrency.** `IHasRowVersion` + `RowVersion` on the contended aggregates.

**Collation.** Name/search columns use `Latin1_General_100_CI_AI`
(`DatabaseCollations.AccentInsensitive`) so French/Arabic names match regardless
of accent or case. Chosen up front — changing a populated column later rebuilds
every index on it.

**Documents.** `Contract`/`Facture` are `INumberedDocument`, numbered
`(AgencyId, Year, SequenceNumber)` unique, append-only, never cascade-deleted.

**Tax — the daily rate an agency enters is TAX-INCLUSIVE.** `Car.DailyRate`,
`Renting.Price` and every fee are gross; the invoice divides back out to the net
amount (`TaxBreakdown.FromGross`). This is a product-wide decision with no
switch, because flipping it would silently reinterpret every stored amount. The
rate, the duty stamp and the agency's tax number live on `AgencySettings` and are
**frozen onto each `Facture`** at issue — a rate is changed by law, and a reprint
must still say what was charged. `Net + Vat == Gross` exactly: the net is
rounded and the tax is the remainder, never rounded twice.

**Availability has three sources, not two.** A car is free for `[start, end)`
only if no non-terminal `Renting`, no **confirmed or paid** `Reservation`, and no
`CarUnavailability` overlaps it. All three are one round trip in
`AvailabilityChecker` and the same predicate is repeated in
`MarketplaceCars.AvailableBetween` — if you change one, change both, or the
marketplace offers a car the booking command will refuse. `Car.Status` answers
"available now"; `CarUnavailability` answers "available on the 20th".

**Cancelling has two boundaries, not one.** `CancellationWindowHours` is the hard
cutoff — inside it neither side can cancel at all. `CancellationFreeHours` is
where it stops being *free*: between the two, a customer cancelling pays the fee
`CancellationPolicy` computes (none / fixed / percent of price, capped at the
price). That one object is the only place the arithmetic lives, so the figure
quoted before the decision is the figure charged. Only a cancellation the
customer made (`Reservation.CancelledByCustomer`) counts against their
reliability score, which is computed per agency and never across them.

**An agency that breaks a confirmed booking is scored for it — publicly.**
`Reservation.Cancel()` records `CancelledAfterConfirmation` (derived from the
status it is overwriting, so no caller can get it wrong), and
`CancelReservationCommand` refuses a confirmed hold with no reason: the customer
is shown it, and it is what the arbitrator reads later. `AgencyReliability` owns
the arithmetic for both readers — 100 less 15 per broken booking and 15 per
upheld report, **null** (not 100) for an agency with nothing on record. The
counts come from `AgencyReliabilityCounts`, whose predicates the shopfront reads
cross-tenant and the agency's own screen reads filtered; the *reportable* twin of
`WasConfirmed` is `AgencyReport.CanReport`, repeated inline wherever EF has to
translate it. The score is on the agency page only — as a correlated sub-query
per search card it would cost a page of results.

**A complaint is arbitrated by the platform, and no money moves.** An
`AgencyReport` hangs off exactly one booking the customer had (a reservation or a
renting, check-constrained), one per booking, within
`AgencyReport.ReportingWindowDays` of it ending. Like `AgencyReview` it is
**platform-level, not `ITenantEntity`** — and for a stronger reason: neither the
customer who raises it nor the platform administrator who settles it carries a
tenant claim, so agency-facing code filters on `AgencyId` by hand. What the
triage screen shows about the booking is **snapshotted onto the row**
(`BookingSummary`, `AgencyCancellationReason`), because the arbitrator cannot
read tenant data. `Uphold`/`Dismiss` happen once and both require a note shown to
both sides. Upholding costs reliability points; nothing else changes hands, which
is Malek's decision: until the platform holds the money, a debt no mechanism
collects is a row in a table, not a sanction.

**A booking can be asked for things.** On a *confirmed* hold the agency declares
what it needs before the keys change hands — money, the deposit, papers, terms,
the signed agreement — as `ReservationRequirement` rows. The customer answers each
from their own space (a file or an acceptance), the agency accepts, refuses (with
a reason the customer is shown) or waives, and `ConvertReservationCommand` refuses
while anything is outstanding unless `AcknowledgeUnmetRequirements` is set. The
money itself is still a `Payment` — a requirement is the ask, never the ledger.

**A chat thread hangs off a booking, and the booking is a hire OR a confirmed
hold.** `ChatMessage` carries a nullable `RentingId` *and* `ReservationId`;
exactly one is set and `ChatSubjectKind` says which. `ChatMessage.CanPostTo` has
one overload per kind (`NotYet`/`InProgress`, `Confirmed`/`Paid`), and the thread
lists repeat the same states inline because EF cannot translate the call. A
hold's conversation stays on the hold when it converts.

**Deposits.** A deposit's fate is recorded on the hire
(`DepositRetainedAmount` + `DepositSettledAt`, `HasUnsettledDeposit`), and the
money going back is a refund `Payment` — the client's balance is computed from
the ledger, so a deposit returned without one leaves the client looking as
though they had paid money they no longer have.

**Expired paperwork blocks a booking.** `CreateRentingCommand` refuses a hire
whose renter or second driver has a lapsed `DrivingLicenceExpiryDate`, unless
`AcknowledgeExpiredDocuments` is set. Expiry is exclusive of the day itself, and
no recorded expiry never blocks.

**Background jobs.** Hangfire on SQL Server storage. Registered only when a real
database is present (skipped for the NSwag build-time host and functional
tests). Recurring: `reservation-expiry` and `notification-sweep`, both hourly.
Dashboard at `/hangfire`, platform admins only.

**Images.** `UploadCarImageCommand` stores the original synchronously (status
`Pending`) and enqueues `CarImageProcessingJob`, which generates thumbnail
(200px) and medium (800px) `StoredFile` derivatives out of band. Client
portraits are cropped out of the CIN image by `SkiaPortraitCropper`.

**Frontend.** `docs/DESIGN_SYSTEM.md` is the source of truth for rules; the
`:root` block in `src/Web/ClientApp/src/styles.scss` is the source of truth for
values. Never edit `src/Web/ClientApp/src/app/web-api-client.ts` — NSwag
generates it; change the endpoint + DTO and regenerate. Never reorder the
stylesheet imports in `angular.json`. Never rename a Transloco key without
moving `src/assets/i18n/*` with it.

---

## 4. Commands

**First, once per machine.** The connection string is in no appsettings file, so
without it the host fails DI with `Connection string 'RemSolutionDb' not found`
and every functional test fails in `OneTimeSetUp`. It belongs in user secrets —
the Web project has a `UserSecretsId`, and the secret reaches both `dotnet run`
and the `WebApplicationFactory` test host:

```powershell
dotnet user-secrets set "ConnectionStrings:RemSolutionDb" `
  "Server=localhost;Database=RemSolutionDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" `
  --project src\Web\Web.csproj
```

Then everything else works:

```powershell
dotnet build RemSolution.sln            # Debug build also regenerates the NSwag client
dotnet test tests\Domain.UnitTests\Domain.UnitTests.csproj            # 156 tests
dotnet test tests\Application.UnitTests\Application.UnitTests.csproj  # 159 tests
dotnet test tests\Infrastructure.IntegrationTests\...csproj           #  24 tests (disk + SkiaSharp)
dotnet test tests\Application.FunctionalTests\...csproj               # 591 tests (needs SQL Server)
cd src\Web; dotnet watch run                                          # https://localhost:5001

cd src\Web\ClientApp; npx ng build --configuration production
bash scripts/check-frontend-conventions.sh   # colour literals + the UTC date split
```

Environment facts that bite:

- The solution targets **`net10.0`** (`Directory.Build.props`), SDK pinned
  `10.0.101` / `rollForward: latestFeature` (`global.json`).
  `TreatWarningsAsErrors` is on, `NU190x` excepted — so an *obsolete* API is a
  build error, which is how a package bump breaks the build here.
- `NSwag.MSBuild` 14.2.0 ships no `Net100` tool, so the Debug post-build target
  runs the `Net90` one with `DOTNET_ROLL_FORWARD=LatestMajor`. Don't remove that
  env var unless a .NET 9 runtime is guaranteed installed.
- `git` and `docker` are **not** on PATH on this machine, and `dotnet ef` needs
  `dotnet tool restore` first. The functional tests run against **local SQL
  Server** (`SqlTestDatabase`), not Testcontainers — that is what
  `TestDatabaseFactory` selects — and they use their own
  `tests/Application.FunctionalTests/appsettings.json` database
  (`RemSolutionTestDb`), separate from the app's.
- **The app creates its own database.** On a dev machine
  `Database:MigrateOnStartup` defaults on, so `dotnet run` creates
  `RemSolutionDb` if absent, applies every migration, seeds the reference data
  and platform admin, then (Development + `DemoData:Enabled`, both already set)
  loads the demo dataset. There is no separate setup step and no script to run.
  Demo logins: `admin@demo.tn` / `staff@demo.tn` / `customer@demo.tn` and the
  other agency admins, password `Demo1234!`; platform admin
  `platformadmin@localhost` / `PlatformAdmin1!`.
- Migrations are applied by the **deployment** (`dotnet ef migrations bundle`),
  not the app — except on a dev machine. See `docs/RUNBOOK_Base_De_Donnees.md`;
  the additive-migration constraint there is load-bearing. A non-nullable column
  added to an existing table lands on 0 for existing rows, NOT on the C#-side
  default — backfill it in the migration (see `AddTaxUnavailabilityAndDocumentExpiry`).

---

## 5. Where the rest lives

| Question | File |
|---|---|
| Cross-cutting technique, in detail | `docs/PROJECT_OVERVIEW.md` §4 |
| Frontend rules, tokens, copy voice | `docs/DESIGN_SYSTEM.md` |
| DB migrations / backups / restore drill | `docs/RUNBOOK_Base_De_Donnees.md` |
| Roadmap, effort estimates, open decisions | `docs/RemSolution_Plan_Developpement.md` |
| Findings not on the client's list | `docs/RemSolution_Addendum_Ameliorations.md` |

**Doc staleness, verified 2026-09-02.** `PROJECT_OVERVIEW.md` §§5–9 describe a
much earlier state (14 entities, "you cannot rent a car yet", net9.0) — §4 is
current, the rest is not. The plan and addendum are written against commit
`139750e` and their status columns are now largely history: since then §3.1
(`CarExpenseSchedule`), §3.2 (`RentingFee`), §4.1 (agency admin created with the
agency), §4.2 (migration bundle) and §4.5 (per-car lock) shipped, and on
2026-09-02 so did §2.6/§4.7 (cleanup + rate limiting), §2.5 (CI), §2.1
(integration tests), A.1 (VAT), A.2 (document expiry), A.3 (availability
indexes), A.4 (one round trip), A.5 (rate limiting), A.8
(`CarUnavailability`) and A.9 (deposit settlement). Check the code, not the
tables. **§0.1 and §8 of the plan are the exception — they were rewritten
2026-09-02 and again 2026-09-08, and are current.** The N.x series is at N.4
(display-only currency conversion) and N.8 (agency cancellation + reports), both
shipped 2026-09-08; what is left is N.10 (agency-opening wizard) and N.2's "TTC"
labels.

**Still open** (and why): a transactional Outbox (§4.6) — its stated purpose is
atomicity for online payment and push, both out of scope, and durable mail would
mean persisting temporary passwords; online payment (§2.3) needs a provider and
credentials; statistics export (§2.9); monitoring alerts (A.6); an email
provider (A.7); the second-driver insurance decision (A.10); the restore drill
and uploads backup (runbook §2–3); mobile and GPS (§3.3–3.5).
