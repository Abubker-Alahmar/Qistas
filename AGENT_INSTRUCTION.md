# AGENT_INSTRUCTION.md — Standing rules for working on Qistas

Read this before touching any code in this repo. These rules override convenience, conventions, and "obvious fixes".

## 1. What Qistas is

Middleware between **Balance** (legacy WinForms weighbridge app, .NET 3.5) and **Microsoft Dynamics 365 F&O**.

- **Data direction is ONE-WAY: Balance → D365FO.** D365FO is a listener. There is no update/delete API. Never add reverse sync.
- **Scope: Sales Order loads only.** Purchases, returns, waste, free samples are out of scope — Qistas must never be called for them.
- Balance calls Qistas over localhost HTTP; Qistas owns TLS 1.2, Azure AD auth, retries, and the outbox.
- Full system analysis: `../Balance/CLAUDE.md` (Part 2, §12–§17). Edge-case list: §16.

## 2. Contract quirks — NEVER "fix" these

The D365 JSON contract contains intentional-by-now typos and quirks. Matching them exactly is a hard requirement; "correcting" them breaks the integration silently.

| Quirk | Rule |
|---|---|
| `Telorence` | NOT `Tolerance`. Exit-weight request field name. |
| `VehicleLicenselId` | NOT `VehicleLicenseId`. Note the extra `l`. |
| `Userid` (request) vs `UserId` (response) | Different casing per direction. Keep both. |
| `Vehichle*` (`VehichleNetWeight`, `VehichleGrossWeight`) | LoadHeader response fields. Keep misspelling. |
| `$id` members | DataContract serializer metadata in responses. Ignore unknown members; never fail on them. |
| `CompanyId` casing | Request `Bell`, response may be `BELL`. Compare case-insensitively. |
| `1900-01-01T12:00:00` | Sentinel = empty date. Parse → null. |
| Non-padded dates (`2029-3-26`) | Parse leniently. Send ISO `yyyy-MM-dd`. |
| Weights | Decimal **numbers** with 2 fraction digits. NEVER strings. Booleans literal `true`/`false`. |

Contract DTOs live in `Qistas.Domain` with explicit `[JsonPropertyName]` on every property. No property may rely on convention-based naming.

## 3. Culture — mandatory

`CultureInfo.InvariantCulture` on ALL serialization, parsing, and formatting. Scale PCs run Arabic Windows: default culture emits `٫` decimal separators and Arabic-Indic digits, which corrupts payloads. Unit tests must run under `ar-LY` to prove safety. Never call `.ToString()` on a number/date destined for a payload without an invariant format.

## 4. Token rules

- Client-credentials token per environment; cache it. Token from one environment must never be sent to another (cache key = environment).
- Thread-safe acquisition (`SemaphoreSlim`). Never request a token per call, never in a loop.
- Proactive refresh ~5 min before `expires_in` (3599s).
- On `401`: refresh once, retry the call once. Then fail into normal retry/outbox flow.

## 5. Retry / outbox policy

- Polly: exponential backoff, configurable max attempts, circuit breaker for sustained outage, per-call timeout. All calls async — Qistas must never block a caller while a truck waits.
- On exhaustion: write the message to the `Outbox` table with `Status=Failed` — never drop it.
- `Qistas.Worker` retries Pending/Failed periodically; humans resolve the rest via the admin/review endpoints ("Retry now" / "Mark manual").
- Log every request/response pair (Serilog), secrets redacted.

## 6. Idempotency

`ScaleSystemReferenceId` (= Balance `Transaction GUID`) is the dedupe key for exit-weight. A timeout does NOT mean D365 didn't save (ghost success): on retry, treat "already processed"/duplicate responses as success. Never submit the same reference twice as two logical transactions.

## 7. Secrets

- client_secret: encrypted at rest (DPAPI/cert). Never in committed appsettings, never in code, never in logs.
- **Never reuse Balance's `ClsCrypto`** (MD5-derived key, static IV, hardcoded key — see Balance/CLAUDE.md §6.6).
- The Dev/Test client_id/secret in the shared Ferdas docs are treated as **compromised** — must be rotated before Production.

## 8. Mapping policy

Manual mapping (explicit mapper classes / extension methods) between domain models and contract DTOs. **No AutoMapper default conventions anywhere near the contract DTOs** — convention mapping silently "fixes" the intentional typos. Mapster with fully explicit config is acceptable for a specific DTO only with a code comment justifying it.

## 9. Sub-agent setup (how work is split)

All implementation agents run on **Sonnet**; this file plus `PLAN.md` is their briefing.

| Agent | Runs when | Responsibility | Hand-off |
|---|---|---|---|
| **Coder** | Per PLAN.md phase | Implements tasks strictly under these rules (esp. §2 typos, §3 culture) | Diff summary → Reviewer |
| **Reviewer** | After each coder batch | Checks diffs against this file + `../Balance/CLAUDE.md` §16 edge cases (ghost success, env mix-up, expired licenses, clock skew…) | Approve → tick PLAN.md boxes; else → back to Coder |
| **Test-writer** | Alongside/after coder | Unit + integration tests: token caching, retry/backoff, outbox-on-exhaustion, duplicate `ScaleSystemReferenceId`, tolerance breach, `ar-LY` serialization | Failing tests → Coder |
| **Load-test** | Only if Coder+Reviewer flag concurrency risk (token cache / outbox worker) | Concurrency validation | Otherwise skipped — reason recorded in PLAN.md Phase 3 |

Nothing is "done" until the Reviewer has passed it.

## 10. Before you touch this code — checklist

- [ ] Read this file end-to-end (especially §2 and §3).
- [ ] Read `PLAN.md` — work only on the current phase; don't skip ahead (Balance WinForms is off-limits until Phase 1 is reviewed and Balance has its git baseline commit).
- [ ] Skim `../Balance/CLAUDE.md` Part 2 (§12–§17); §16 is the edge-case authority.
- [ ] Confirm which environment (Dev/Test/Prod) any manual test targets — never point tests at Production.
- [ ] Never commit secrets; check your diff for tokens/secrets before every commit.
- [ ] Preserve contract typos in any new DTO/serialization code — grep for `Telorence` and `VehicleLicenselId` as a sanity check that you matched existing patterns.

---

*v1.0 — 2026-07-07*
