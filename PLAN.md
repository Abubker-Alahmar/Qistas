# Qistas — Implementation Plan

Qistas is the integration middleware between the **Balance** weighbridge system (WinForms, .NET 3.5) and **Microsoft Dynamics 365 F&O** (Ferdas project). One-way: Balance → D365FO, Sales Order loads only.

Reference: `../Balance/CLAUDE.md` (Part 2, §12–§17). Operating rules: `AGENT_INSTRUCTION.md` (same folder).

---

## Phase 1 — Foundation (Qistas solution)

### 1.1 Scaffolding
- [x] `Qistas.sln` with projects: `Qistas.Api` (Minimal API host), `Qistas.Application`, `Qistas.Domain`, `Qistas.Infrastructure`, `Qistas.Worker` — all **.NET 8**
- [x] Project references: Api → Application + Infrastructure; Application → Domain; Infrastructure → Application + Domain; Worker → Application + Infrastructure
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

### 1.6 Worker (`Qistas.Worker`)
- [x] `BackgroundService` polling Outbox for Pending/Failed below max attempts; retries with backoff; moves to `Failed` (manual review) on exhaustion
- [x] Startup reconciliation hook: expose outbox status so Balance can reconcile `Table_StillInside` after crash (edge case #17)

### 1.7 Phase 1 gate
- [ ] Reviewer agent pass against AGENT_INSTRUCTION.md + §16 edge cases
- [ ] `dotnet build` clean, tests green (see Phase 3 unit tests, written alongside)
- [ ] Git: Qistas repo initialized, committed (GitHub push deferred — local only per owner decision)

---

## Phase 2 — Balance-side changes (only after Phase 1 reviewed)

Precondition: Balance git baseline commit exists (clean "before" state).

- [ ] `git init` in Balance folder + WinForms/.NET 3.5 `.gitignore` + baseline commit
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
- [ ] Integration tests: fake D365 server (WireMock.Net) — ghost-success/timeout-then-duplicate, Status=false paths, `$id` metadata parsing, `Bell` vs `BELL`
- [ ] Load tests: **skipped unless coder+reviewer flag concurrency risk** in token cache/outbox worker — decision recorded here: skipped — token cache and outbox covered by unit tests; no concurrency risk flagged by reviewer
- [ ] Postman parity on Dev environment (compare Qistas request bytes vs Alsahl Collection)
- [ ] UAT on `alsahl-test.sandbox...`, then Prod cutover
- [ ] Rotate client secrets before Production (Dev/Test secrets in shared docs are compromised); confirm Prod credentials with Ferdas
- [ ] Confirm with Ferdas: token call is standard PO