# Qistas

Integration middleware between **Balance** (legacy WinForms weighbridge app, .NET
Framework 3.5) and **Microsoft Dynamics 365 F&O** (Ferdas project). Direction is
strictly one-way: Balance -> D365FO, Sales Order loads only. See
`AGENT_INSTRUCTION.md` and `PLAN.md` for the full operating rules and roadmap; this
file is just "how do I run it."

## Solution layout

```
Qistas.sln
Directory.Build.props          # net8.0, nullable enable, implicit usings, shared across all projects
src/
  Qistas.Domain/                # D365 contract DTOs (with the required typos), domain models,
                                 # validation rules, JSON converters -- no dependencies
  Qistas.Application/           # Use cases, manual (non-AutoMapper) mappers, archive/log abstractions
  Qistas.Infrastructure/        # TokenService, D365Client (Polly inline resilience), SQL Server
                                 # failed-message archive + IntegrationLog, DPAPI secret protector,
                                 # DI wiring (ServiceCollectionExtensions)
  Qistas.Api/                   # ASP.NET Core Minimal API host -- Balance-facing + admin endpoints
```

## Failure handling (owner decision)

When D365 is down, Polly retries **inline** per configuration (`Qistas:Retry` -- keep it
short, the truck is on the scale). If still failing, the message is **archived in the
database** with `Status=Failed`; the truck proceeds (the transaction is already saved in
Balance). An **employee** then handles it from the review screen: "Retry now" re-sends to
D365, or "Mark manual" records that it was entered in D365 by hand. There is **no
automatic background re-sender**.

## Running

Requires the .NET 8 SDK (not available in the environment that generated this code --
build/run on a machine with it installed).

```powershell
# Api (Swagger UI at https://localhost:<port>/swagger)
dotnet run --project src/Qistas.Api
```

## Swagger

The Api exposes **two** Swagger documents, split by `.WithGroupName(...)` on each
endpoint group:

- `Balance API` (`/swagger/balance/swagger.json`) -- the three D365 call points
  (`POST /api/scale/entry-weight`, `GET /api/scale/loads/{loadId}`,
  `POST /api/scale/exit-weight`) plus `GET /api/health`.
- `Qistas Developer API` (`/swagger/admin/swagger.json`) -- archived-message review/
  retry/manual, `GET /api/admin/logs` (database log), `GET /api/admin/config`,
  `GET /api/admin/token-status`.

Both documents show the currently active D365 environment (Dev/Test/Prod) in their
description banner, so it's never ambiguous which environment a developer is looking at
(a Dev/Prod mix-up is one of the explicit edge cases this project guards against).

## Configuration

`appsettings.json` in `Qistas.Api` defines the `Qistas:` section: `ActiveEnvironment`,
`Tenant`, `Environments:{Dev,Test,Prod}` (BaseUrl/CompanyId/ClientId/
ClientSecretProtected), `Retry` (Polly inline retry settings -- keep attempts/timeout
short so trucks are never held), `ConnectionStrings:QistasLogDb` (SQL Server connection string for the Outbox/IntegrationLog/Serilog database).

**No real credentials are committed.** `ClientId` and `ClientSecretProtected` are empty
strings in the checked-in `appsettings.json` for every environment. The Dev/Test
client_id/secret that were shared in the Ferdas documents are treated as **compromised**
and must be rotated before use; configure real values via:

- an untracked `appsettings.Development.json` (already in `.gitignore`), or
- `dotnet user-secrets`, or
- environment variables (`Qistas__Environments__Dev__ClientId`, etc.), or
- the planned admin settings screen (Balance-side, Phase 2) once it can write through to
  a persisted config store.

`ClientSecretProtected` must contain a value produced by `ISecretProtector.Protect(...)`
(DPAPI on Windows, CurrentUser scope) -- never a plaintext secret.

## Archive + integration log database

One SQL Server database (`ConnectionStrings:QistasLogDb`) holds two tables, both created
automatically on first use:

- `Outbox` -- failed-message archive awaiting employee action (review screen).
- `IntegrationLog` -- one row per D365 call attempt: timestamp, environment, operation,
  request JSON, response JSON, HTTP status, error, duration. Queryable via
  `GET /api/admin/logs?operation=&success=&take=`. Kept in Qistas's own DB (not
  BalanceSAHEL_New) so large JSON payloads never bloat the production weighbridge
  database or its backups.

## Notes on contract fidelity

Every D365 contract DTO in `Qistas.Domain.Contracts` preserves the documented typos
exactly (`Telorence`, `VehicleLicenselId`, `Userid` request / `UserId` response,
`Vehichle*` in `LoadHeader`). Do not "fix" these -- see `AGENT_INSTRUCTION.md` section 2.

## getLoadDetails: Driver/Vehicle/Location surfacing (owner decision)

`getLoadDetails`'s D365 response includes `Context.DriverDetails`, `Context.VehicleDetails`,
and each line's `LocationId`/`LocationName` -- these used to be silently dropped by
`LoadDetailsMapper`. They are now mapped through end-to-end:

```
D365Response.Context.DriverDetails/VehicleDetails (wire, Qistas.Domain.Contracts)
  -> LoadDetailsMapper.ToDomainResult
  -> LoadValidationResult.Driver / .Vehicle (Qistas.Domain.Models, clean OUTPUT read-shape --
     distinct from DriverInfo/VehicleInfo, which is the INPUT shape sent on entry-weight)
  -> LoadValidationResultDto.Driver / .Vehicle (Qistas.Api.Contracts)
  -> JSON body of GET /api/scale/loads/{loadId}
```

`LoadLineInfo` similarly now carries `LocationId`/`LocationName` per line.

Balance uses this to (a) auto-fill the Weight-Out materials grid from D365 line items
instead of manual re-entry, and (b) sync `Table_Trucks` (driver/vehicle master) with D365's
canonical values, matched by `DriverName`+`TruckNo` -- **D365 is treated as source of truth**
for this sync; see `Balance/CLAUDE.md` section 14 item 5 for the Balance-side behavior.

Date parsing for `DriverLicenseExpiryDate`/`VehicleLicenseExpiryDate` reuses the same
"1900-01-01 (any time) = null" sentinel rule as `LoadLine.BatchExpirationDate`, via
`LenientNullableDateTimeConverter.TryParseLenientDate` (shared helper, refactored out of the
JSON converter so the mapper can call it directly without going through JSON deserialization).
