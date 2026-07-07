# Qistas — Implementation Plan

Qistas is the integration middleware between the **Balance** weighbridge system (WinForms, .NET 3.5) and **Microsoft Dynamics 365 F&O** (Ferdas project). One-way: Balance → D365FO, Sales Order loads only.

Reference: `../Balance/CLAUDE.md` (Part 2, §12–§17). Operating rules: `AGENT_INSTRUCTION.md` (same folder).

---

## Phase 1 — Foundation (Qistas solution)

### 1.1 Scaffolding
- [x] `Qistas.sln` with projects: `Qistas.Api` (Minimal API host), `Qistas.Application`, `Qistas.Domain`, `Qistas.Infrastructure` — all **.NET 8** (~~Qistas.Worker~~ removed per owner decision: no automatic background retry)
- [x] Project references: Api → Application + Infrastructure; Application → Domain; Infrastructure → Application + Domain
- [x] `.gitignore` (.NET 8: bin/obj, .vs/, *.user, appsettings.Development.json, secrets)
- [x] `appsettings.json` layout: `Qistas:D365` (per-environment BaseUrl/tenant/CompanyId/clientId), `Qistas:Retry` (maxAttempts, backoffSeconds, timeoutSeconds), `Qistas:ActiveEnvironment` (Dev/Test/Prod). **No secrets in files that get committed.**

### 1.2 Domain (`Qistas.Domain`)
- [x] D365 contract DTOs matching the JSON contracts **byte-for-byte, including typos** (`Telorence`, `VehicleLicenselId`, `Userid` request / `UserId` response, `Vehichle*` in LoadHeader) — see AGENT_INSTRUCTION.md §Contract Quirks
- [x] Entry request/response, GetLoadDetails request/response, ExitWeight request/response, shared `LoadHeader`, `LoadLine`, `DriverDetails`, `VehicleDetails`
- [x] Validation rules: license expiry (must be future), tolerance check (exit vs load lines), weight sanity (positive, entry < exit for loaded exit), KG normalization (2 decimals)
- [x] Sentinel-date handling: parse `1900-01-01T12:00:00` → null; lenient parse of non-padded dates (`2029-3-26`)

### 1.3 Infrastructure (`Qistas.Infrastructure`)
- [x] `TokenService`: Azure AD client_credentials, cache per environment, thread-safe (`SemaphoreSlim`), proactive refresh ~5 min before expiry, refresh-once-and-retry-once on 401
- [x] `D365Client` (typed `HttpClient`): setEntryWeightDetails, getLoadDetails, setExitWeightDetails; `System.Text.Json` with `CultureInfo.InvariantCulture`-safe converters; ignore `$id` members; case-insensitive `CompanyId` comparison
- [x] Polly policies: exponential backoff retry (configurable N), circuit breaker for sustained outages, per-call timeout — all async, never blocking
- [x] Outbox persistence (SQLite or SQL Server — configurable): table `Outbox` (Id, ScaleSystemReferenceId, Operation, PayloadJson, Status [Pending/Sent/Failed/Manual], Attempts, LastError, LastResponseJson, CreatedUtc, UpdatedUtc)
- [x] Request/response logging (Serilog) — full payloads, secrets redacted
- [x] Secret storage: client_secret encrypted at rest (DPAPI on Windows) — never Balance's `ClsCrypto`

### 1.4 Application (`Qistas.Application`)
- [x] Use cases: `SubmitEntryWeight`, `GetLoadForValidation`, `SubmitExitWeight`, `RetryOutboxMessage`, `MarkOutboxManual`
- [x] Idempotency: dedupe by `ScaleSystemReferenceId`; treat "already processed" D365 responses as success
- [x] Manual mapping (explicit mapper classes) domain ↔ contract DTOs — **no AutoMapper conventions near contract DTOs**

### 1.5 Api (`Qistas.Api`)
- [x] Balance-facing endpoints: `POST /api/scale/entry-weight`, `GET /api/scale/loads/{loadId}`, `POST /api/scale/exit-weight`, `GET /api/health` (D365 reachability + token status + environment)
- [x] Developer/admin endpoints: `GET /api/admin/outbox`, `POST /api/admin/outbox/{id}/retry`, `POST /api/admin/outbox/{id}/manual`, `GET /api/admin/config`, `PUT /api/admin/config`, `GET /api/admin/token-status`
- [x] Swagger: **two documents** — "Balance API" and "Qistas Developer API" — separate `SwaggerDoc` entries + separate UI endpoints, endpoints tagged via endpoint metadata group names
- [x] Show active environment (Dev/Test/Prod) in health + Swagger description (edge case #20)

### 1.6 Failure handling (revised per owner decision 2026-07-07)
- [x] ~~Worker BackgroundService auto-retry~~ REMOVED — retry is Polly INLINE only (short, configurable; the truck cannot wait); on exhaustion the message is archived (`Outbox` table, `Status=Failed`) for MANUAL employee action (Retry now / Mark manual on the review screen)
- [x] Database logging: `IntegrationLog` table (one row per D365 call attempt: request, response, outcome, duration) + `GET /api/admin/logs` — logs in DB, not only Serilog files
- [x] Startup reconciliation hook: archive status exposed via `/api/admin/outbox` so Balance can reconcile `Table_StillInside` after crash (edge case #17)

### 1.7 Phase 1 gate
- [x] Reviewer agent pass against AGENT_INSTRUCTION.md + §16 edge cases — PASS (health-probe gap + response-dispose fixed after review; wire contracts corrected to literal `_request` envelope + flat `D365Response` per APIs V2.0)
- [ ] `dotnet build` clean, tests green — **pending: no .NET SDK in this environment; run `dotnet build && dotnet test` on the Windows machine**
- [x] Git: Qistas repo initialized, committed (`ba1d390`) — GitHub push deferred, local only per owner decision

---

## Phase 2 — Balance-side changes (only after Phase 1 reviewed)

Precondition: Balance git baseline commit exists (clean "before" state).

- [x] `git init` in Balance folder + WinForms/.NET 3.5 `.gitignore` + baseline commit (`b3eba37`, 132 files, clean "before" state)
- [ ] DB schema: `LoadId`, `IntegrationStatus` on `Table_Transaction` + `Table_StillInside`; license fields on `Table_Drivers`/`Table_Trucks` (migration script, own commit)
- [ ] `frmBuyWeight.cs` Weight-In: LoadId (first field), DriverNationalId, DriverLicenseId + expiry, VehicleLicenselId + expiry, optional IsInternal/VehicleType (own commit)
- [ ] Call point 1: Weight-In save → `POST localhost/api/scale/entry-weight` (WebRequest, async, non-blocking) — Sales Order loads only (`BuySell = "مبيعات"` + LoadId present)
- [ ] Call point 2: Weight-Out open → `GET /api/scale/loads/{loadId}`, display lines, local tolerance validation
- [ ] Call point 3: Weight-Out save → `POST /api/scale/exit-weight` with `ScaleSystemReferenceId = Transaction GUID`
- [ ] Admin settings screen (environment, URLs, retry, timeouts — reads/writes Qistas config API)
- [ ] Failed-message review screen (grid over `GET /api/admin/outbox`, Retry now / Mark manual)
- [ ] Block or flag edits/deletes in `frmUpdate.cs` on integrated transactions (edge cases #18/#19)
- [ ] Each bullet = its own logical commit

---

## Phase 3 — Test & deploy

- [x] Unit tests (`Qistas.Tests`): token caching + 401 retry-once, Polly retry/backoff, outbox insert-on-exhaustion, idempotent duplicate `ScaleSystemReferenceId`, tolerance breach, license expiry, sentinel dates, **InvariantCulture serialization under `ar-LY` culture**
- [ ] Integration tests: fake D365 server (WireMock.Net) — 