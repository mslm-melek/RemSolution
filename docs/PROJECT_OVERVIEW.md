# RemSolution — Full Project Description

## 1. What it is

**RemSolution** is a **car-rental management system** (backend API + Angular SPA). It is built on the **Ardalis / Jason Taylor "Clean Architecture" solution template** for .NET, with the template's sample content (Todo / WeatherForecast) being stripped out and replaced by a real rental domain: brands, car models, cars, clients, rentings, reservations, payments, expenses, and extra services.

The domain vocabulary and field names (Matricule, CIN, Passeport, Deliverance) indicate a **French / North-African business context** (consistent with the `hrmaps.fr` owner and the French build output).

---

## 2. Architecture

Classic **Clean Architecture** — four projects, with dependencies pointing inward:

```
Web  ──►  Application  ──►  Domain
 └──►  Infrastructure ──►──┘
```

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Domain** | `RemSolution.Domain` | Entities, enums, value objects, domain events, exceptions. No external dependencies. |
| **Application** | `RemSolution.Application` | Use cases (CQRS commands / queries), DTOs, validation, mapping, pipeline behaviours, interfaces. |
| **Infrastructure** | `RemSolution.Infrastructure` | EF Core `DbContext`, entity configurations, migrations, Identity, interceptors, external services. |
| **Web** | `RemSolution.Web` | Minimal-API endpoints, host wiring, Swagger, Angular SPA host. |

The **Web layer is the composition root**: `Program.cs` calls `AddApplicationServices()`, `AddInfrastructureServices()`, and `AddWebServices()`.

---

## 3. Technology stack

### Backend
- **.NET 9** (`Directory.Build.props` targets `net9.0`), building with **SDK 10.0.101** (`global.json`, roll-forward) — *a version mismatch worth reconciling*.
- **ASP.NET Core Minimal APIs** — endpoints grouped via a custom `EndpointGroupBase` and reflection (`MapEndpoints`).
- **Entity Framework Core 9** on **SQL Server** (LocalDB in development).
- **ASP.NET Core Identity** for authentication (users, roles, policies).
- **NSwag** for OpenAPI / Swagger generation (served at `/api`); it also generates the Angular TypeScript client.
- **Azure Key Vault** integration (optional, via `DefaultAzureCredential`).
- **Health checks** at `/health` (includes a DbContext check).

### Application patterns / libraries
- **MediatR 12** — CQRS mediator.
- **FluentValidation 11** — request validation.
- **Mapster 10** — object mapping (**migrated from AutoMapper** to remove a CVE and a licensing concern).
- **Ardalis.GuardClauses** — guard helpers.

### Frontend
- **Angular 18** (module-based, not standalone) with **TypeScript 5.4**.
- **Angular Material 18** + **ngx-bootstrap** + **Bootstrap 5** (two UI systems present).
- **RxJS**, plus the auto-generated **NSwag TypeScript API client** (`web-api-client.ts`).

### Build / DevOps
- **Central Package Management** (`Directory.Packages.props`).
- **Cake** build script (`build.cake`), **Azure Developer CLI** (`azure.yaml` + Bicep `infra/`), **.devcontainer**, and **GitHub** workflows.

### Testing (scaffolding present)
- `Domain.UnitTests`, `Application.UnitTests` (NUnit + FluentAssertions + Moq), `Application.FunctionalTests` (Testcontainers / Respawn), `Infrastructure.IntegrationTests`, `Web.AcceptanceTests` (SpecFlow + Playwright).

---

## 4. Cross-cutting techniques

- **MediatR pipeline behaviours**: `UnhandledException` → `Authorization` → `Validation` → `Performance` → `Logging`.
- **EF Core interceptors**:
  - `AuditableEntityInterceptor` — auto-sets Created / LastModified and by-whom on `BaseAuditableEntity`.
  - `TenantEntityInterceptor` — write-side tenant enforcement: stamps `AgencyId` from the current
    tenant on inserted `ITenantEntity` rows, never writes `AgencyId` on update (rows cannot move
    between agencies), and throws `ForbiddenAccessException` (403) when a request updates or
    deletes a row belonging to another agency.
  - `DispatchDomainEventsInterceptor` — dispatches domain events on `SaveChanges`.
- **Money value object**: monetary amounts are a `Money` value object (Amount + ISO-4217 Currency),
  not bare decimals. Currency is tenant-scoped — each `Agency` has a single `Currency`, and handlers
  denominate amounts in it (clients send only the amount). Mapped as EF optional owned types via
  `OwnsMoney(...)`: the amount keeps its original column, the currency lands in a new
  `<name>Currency` column, both null together when absent. Exposed to the API as `MoneyDto`.
  Applies to `Car.DailyRate`, `Renting.Price`, `Reservation.Price`/`PayedPrice`,
  `Payment.PayementAmount`, `ExtraService.TotalAmount`, `RentingHistory.Price`. Subscription-plan
  prices stay decimal (platform-level, no agency).
- **Time (UTC at the persistence boundary)**: all domain `DateTime` values are UTC. A global EF
  value-conversion convention (`UtcDateTimeConverter`, applied in
  `ApplicationDbContext.ConfigureConventions`) normalises them on write (a `Local` value is
  converted to UTC; an `Unspecified` value is assumed already-UTC and its `Kind` stamped, since the
  API edge deserializes inbound values as UTC) and stamps `Kind = Utc` on read (SQL Server
  `datetime2` keeps no offset). Audit stamps stay `DateTimeOffset` (already unambiguous). Callers
  should use `TimeProvider.GetUtcNow()`; never `DateTime.Now`.
- **Multi-tenancy (agency isolation)**: `ITenantProvider` is resolved per-request from the
  authenticated user's `AgencyId` claim (emitted by `ApplicationUserClaimsPrincipalFactory` from
  `ApplicationUser.AgencyId`). Every `ITenantEntity` gets an EF global query filter
  (`AgencyId == tenant.AgencyId`) applied in `ApplicationDbContext.OnModelCreating`, so handlers
  cannot leak cross-tenant data by forgetting a WHERE; no tenant claim ⇒ tenant data matches
  nothing. Cross-tenant reads via `IgnoreQueryFilters()` are allowed only in the Phase 6
  MarketplaceSearch feature folder (plus the platform-admin referential check in
  `DeleteAgencyCommand`) — enforced by the `TenantEnforcementTests` convention test.
- **Car image pipeline (async derivatives)**: a car has many `CarImage` (SortOrder, one IsPrimary),
  each keeping its full-resolution original plus generated thumbnail (200px) and medium (800px)
  derivatives — all `StoredFile` rows. `UploadCarImageCommand` stores the original synchronously
  (status `Pending`) and enqueues via `IImageProcessingQueue`; the Hangfire-backed implementation
  schedules `CarImageProcessingJob`, which resizes via `IImageProcessor` (SkiaSharp) out of band so
  upload latency is flat. Hangfire resolves the job in its own DI scope; the job has no HTTP context,
  so it pushes the agency onto `AmbientTenant` (an `AsyncLocal` that `CurrentTenant` consults before
  the claim) — tenant filters and the write-stamp then behave as in a request. A failed job marks the
  image `Failed` (the original stays usable) and rethrows so Hangfire records/retries it.
- **Background jobs (Hangfire, SQL Server storage)**: the single job infrastructure (car-image
  derivatives today; reservation expiry, emails, PII purge, statistics next). Registered only when a
  real database is present — skipped for the NSwag build-time host (placeholder connection) and
  functional tests (`Hangfire:Enabled=false`), where the enqueue seam is a no-op / recording fake.
  Dashboard at `/hangfire`, restricted to platform administrators.
- **Selective soft delete**: only where an archive is wanted. `Car`/`Client` are `ISoftDeletable`
  (`IsDeleted`/`DeletedAt`/`DeletedBy`) — `SoftDeleteInterceptor` turns a `Remove()` into the flag
  update (history preserved, files/FK references kept), and the global filter composes
  `!IsDeleted && AgencyId == tenant`. `ExpenseType`/`ExtraServicesType` use an `IsActive` flag
  (deactivation, not deletion). `Renting`/`Payment`/`Reservation` are never deleted — financial
  records; their `Client` FKs are `Restrict`, so a physical client delete fails. Unique indexes are
  filtered (`IX_Cars_AgencyId_Matricule WHERE IsDeleted = 0`) so an archived row's key frees for reuse.
- **Collation (accent/case-insensitive names)**: name/search columns (client first/last name, agency /
  branch / brand / model / expense-type / extra-service-type names) use `Latin1_General_100_CI_AI`
  (`DatabaseCollations.AccentInsensitive`) so French/Arabic names match regardless of accent/case.
  Chosen up front — changing a populated column's collation later rebuilds every index touching it.
- **Domain events** via `BaseEntity.AddDomainEvent(...)`.
- **CQRS vertical slices** — each feature folder has Commands / Queries / DTOs / EventHandlers.
- **Mapster projection** — `ProjectToType<T>()` produces efficient SQL projections (no over-fetching).
- **Global `RequireAuthorization()`** on all endpoint groups; policy-based authorization (`CanPurge` → Administrator).
- **Custom exception handling** (`CustomExceptionHandler`) → RFC-7807 problem details.

---

## 5. Domain model (14 entities)

All entities inherit `BaseAuditableEntity` (Id + audit fields).

- **Brand** → has many **ModelCar** → has many **Car**
- **Car** — matricule, first-circulation date, color, power, `FuelType` (Gasoline / Diesel), image; has Expenses and Rentings
- **Client** — rich identity record: name, birth info, **CIN**, **Passeport**, **Driving Licence** (each with number / date / place / country + image), multiple `Country` foreign keys
- **Country** — reference lookup
- **Renting** — car + primary/secondary client, dates, mileage, price, `RentingState` (Done / InProgress / NotYet); has ExtraServices, RentingHistories, Reservations
- **Reservation** — client, dates, price, payed price, links to Renting
- **RentingHistory** — snapshot / audit of renting periods
- **Payment** — client payments
- **Expense** + **ExpenseType** — per-car expenses (with mileage / month-based notification thresholds)
- **ExtraService** + **ExtraServicesType** — add-ons on a renting
- **User** — a Domain user entity that *duplicates* Identity's `ApplicationUser` (a known redundancy)

Enums: `FuelType`, `RentingState`.

---

## 6. What is actually done (feature matrix)

| Feature | Create | Update | Delete | GetById | List | Paginated | Endpoint | Angular UI |
|---------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| **Car** | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| **ModelCar** | Yes | No | Yes | Yes | Yes | Yes | Yes | Yes |
| **Brand** | Yes | No | Yes | Yes | Yes | No | Yes | Yes |
| **Country** | Yes | No | Yes | Yes | Yes | No | Yes | No |
| Client, Renting, Reservation, Payment, Expense(Type), ExtraService(Type), User | No | No | No | No | No | No | No | No |

**Infrastructure done:** DbContext with all 14 entities + EF configurations, one migration (`InitialCreate`, includes every table), Identity, seeding (admin user + Administrator role), interceptors, health checks, Swagger, NSwag client generation.

**Frontend done:** Angular 18 shell with nav-menu, home, and CRUD components for **brand / model-car / car**; Material table / paginator wired; authorize HTTP interceptor.

**Verified working:** solution builds clean (0 warnings / 0 errors); all 10 unit tests pass; Mapster mappings validated.

---

## 7. What is partial / not done

- **Core business flow is missing** — Renting, Reservation, Payment, and Client have entities + DB tables but **no commands / queries / endpoints / UI**. You cannot actually rent a car yet.
- **Incomplete CRUD** — Brand / ModelCar / Country lack Update commands.
- **Duplicate user identity** — `Domain.User` vs Identity `ApplicationUser` (unresolved source of truth).
- **Hardcoded connection string** in `Infrastructure/DependencyInjection.cs` (the configuration line is commented out).
- **Model drift** — `PendingModelChangesWarning` is suppressed and the migration still contains Todo tables; only one migration exists.
- **No tests for the new domain** — the old Todo tests were deleted; the test projects are empty scaffolding.
- **Version inconsistencies** — net9 vs SDK 10, plus stale `obj/net8.0` build artifacts.
- **Two UI frameworks** (Material + Bootstrap) loaded together.

---

## 8. Recent changes (applied)

1. Added `Id` to `CarDto` / `CountryDto` / `ModelCarDto`.
2. Fixed the wrong `NotFoundException` entity name in the Country **and** ModelCar GetById queries.
3. Renamed three mis-named files (`CountryDto.cs`, `GetCountryByIdQuery.cs`, `ExpenseConfiguration.cs`).
4. **Migrated AutoMapper → Mapster** (removed CVE-2026-32933 and the new commercial-license issue); ported DI registration, four DTO mappings, ten query handlers, the mapping helper, and the mapping test.
5. **Removed template-leftover domain events** — deleted `CarCompletedEvent` (wrongly raised on create), the never-consumed `Car`/`ModelCar` Deleted & Completed events, and the log-only handlers; also deleted the unused `PriorityLevel` enum. The `DispatchDomainEventsInterceptor` infrastructure is retained; events will be reintroduced when `Renting` has a real consumer (e.g. `RentingCompletedEvent → write RentingHistory`).

---

## 9. Summary

A well-architected **.NET 9 Clean Architecture + Angular 18** car-rental application with a solid, complete foundation and a rich data model — but only about **30% of the use cases are implemented**, and the central "renting" workflow is still just data tables without behavior.
