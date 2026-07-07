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
  Qistas.Infrastructure/        # TokenService, D365Client (Polly inline resilience), SQLite
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
short so trucks are never held), `Outbox` (SQLite path).

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

One SQLite database (`Qistas:Outbox:SqlitePath`, default `qistas-outbox.db` relative to
the Api's working directory -- use an absolute path in production) holds two tables,
both created automatically on first use:

- `Outbox` -- failed-message archive awaiting employee action (review screen).
- `IntegrationLog` -- one row per D365 call att