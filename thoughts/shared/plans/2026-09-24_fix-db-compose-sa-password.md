# Plan: stop publishing the local SQL Server SA password (`fix(db)`)

Date: 2026-09-24. Branch: `fix/compose-sa-password`. Roadmap: the `fix(db)` chore before PR 7.

## Why

- `docker-compose.yml` falls back to a committed default SA password (`${MSSQL_SA_PASSWORD:-…}`) in a public repository, and it publishes 1433 on every interface. While the container runs, anyone on the same network can try `sa` with that password.
- The same literal appears in `thoughts/shared/plans/2026-09-02_ef-infrastructure.md`. That plan is a historical record and stays as written; rotating the password makes the literal inert.

## Changes

1. `docker-compose.yml`:
   - `MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD in .env - see .env.example}"`. There is no default, and compose refuses to start without the value.
   - `ports: ["127.0.0.1:1433:1433"]`, so the port is bound to loopback only.
   - A one-line comment on each change saying why.
2. `.env.example` (new): the key with an empty value, plus comments on SQL Server's password policy and on why a new password needs a new volume.
3. `.gitignore`: `!.env.example` after the `.env.*` rule, which ignores the example file today.
4. `CLAUDE.md`, Commands: the `docker compose up -d` line says it needs `.env`, created from `.env.example`.

Out of scope: pinning the SQL Server image tag in both compose and `SqlServerFixture`. That is about reproducibility, not security. The tests use Testcontainers, which sets its own password, so they are unaffected.

## Verification (human, on the branch)

- Without the variable, `docker compose --env-file <an empty file> config` fails and names `.env.example`.
- With `.env`, `docker compose config` shows the port bound to `127.0.0.1`.
- After the rotation below:
  - the container is healthy
  - `netstat` shows 1433 listening on loopback only
  - both `dotnet ef database update` commands succeed
  - the app boots
- The gates are unchanged: 638 tests.

## Rotation (human, once)

SQL Server applies `MSSQL_SA_PASSWORD` only when it creates the data volume, so the existing volume keeps the old password. The local database holds development data only, so the volume is recreated:

1. Write a new password to `.env`.
2. Run `docker compose down -v`, then `docker compose up -d`, and wait until the container is healthy.
3. Put the new password in the `ConnectionStrings:EnvanexDb` user-secret.
4. Run both migrations with `ENVANEX_CONNECTION_STRING` set.

No ADR: this is a configuration fix, not a decision with alternatives worth recording.

## As built (2026-09-24)

- The coder changed `docker-compose.yml`, `.gitignore` and `CLAUDE.md`.
- The PreToolUse path guard blocked `.env.example`. Its `\.env($|\.)` rule matches the template as well as real secrets, so the human wrote the file from the coder's draft. `chore(agents)` should allowlist `.env.example` when it fixes the guard. The block also shows that the guard's suffix rules fire on Windows paths; its directory rules are still unverified.
- With an empty env file, `docker compose config` fails with "required variable MSSQL_SA_PASSWORD is missing a value: Set MSSQL_SA_PASSWORD in .env - see .env.example". With a value, the port is published on `host_ip: 127.0.0.1`.
- Gates: 638 green. The integration suite took 61 s, one sample over ADR 0007's 60 s line. The decision stays scheduled before PR 7 adds tests, with the median-of-three measurement.

## Finding during verification (2026-09-24)

- After the rotation, both `dotnet ef database update` commands timed out (SqlClient error 258). The connection strings used `Server=localhost,1433`. Windows resolves `localhost` to `::1` first, and the new binding listens on IPv4 loopback only. `Test-NetConnection` confirmed it: `127.0.0.1 -> True`, `::1 -> False`. The old `"1433:1433"` binding also listened on IPv6, which hid the dependency.
- With `Server=127.0.0.1,1433`, the migrations applied.
- Decision: connect to `127.0.0.1`, not `localhost`. The compose comment, `CLAUDE.md` and both design-time factories' example connection strings now say so. Also binding `[::1]` was rejected: it depends on Docker Desktop's IPv6 port publishing, and it keeps an ambiguous hostname in the setup.

## Code review (2026-09-24)

code-reviewer, on Opus, over the whole branch: **NEEDS_REVISION**, with one required change.
- Fixed: `.env.example` now names all three places that hold the password: `.env`, the `ConnectionStrings:EnvanexDb` user-secret, and `ENVANEX_CONNECTION_STRING`. It gives the connection-string shape with `127.0.0.1`, and it warns against `$`, `#`, `;` and quotes in the password. The human wrote the file, because the path guard blocks the coder from writing `.env.example`.
- Decided: this PR also rewrites README's "Running locally". The change made `docker compose up -d` require `.env`, and the old steps also lacked the JWT signing-key and connection-string secrets the app needs to boot. The rest of README stays for PR 7.
- Decided: this PR removes the roadmap's `docker-compose.yml` row, which it closes. The PR table's status for `fix(db)` is filled in by the next PR that touches the roadmap, once the GitHub number is known.
- Settled: `.env.example` is tracked; `git ls-tree` on the branch lists it.

## Focused re-review (2026-09-24)

code-reviewer, on Opus, over the commits after 3972595: **NEEDS_REVISION** on README's "Running locally". `.env.example`, the roadmap and this plan passed. Decided and done:
- The EF Core CLI is a local tool, pinned in `.config/dotnet-tools.json` to the EF Core version the solution uses. `dotnet tool restore` is the first setup step, so a fresh machine no longer fails at `dotnet ef`.
- `docker compose up -d --wait` waits for the healthcheck before the migrations run.
- The connection string is single-quoted. Inside double quotes Git Bash expands `!`, backticks and `\`, and `!` is common in SQL Server passwords.
- The signing key uses `openssl rand -base64 48`, 64 characters on one line. The earlier `-base64 64` wrapped, which put a newline inside the key.
- One sentence says the demo steps are how to sign in locally; with the demo off, no account exists.

Notes for `chore(agents)`:
- `/commit` ran `git commit` through Windows PowerShell 5.1, which split a message at its double quotes. It recovered by soft-resetting its own unpushed commit and committing with `-F`. The skill should always commit with `-F <file>`.
- "How the work is run" in the roadmap still says the code-reviewer runs on Sonnet. This PR's reviews ran on Opus through the per-invocation override.

## Second focused re-review and gate (2026-09-24)

- code-reviewer, on Opus: **APPROVED**, with four optional README items. Three are applied:
  - the SDK floor from `global.json` (10.0.400)
  - `openssl rand -hex 32` for the signing key, because base64 output can start with `/`, which Git Bash rewrites into a Windows path for a native program
  - the demo password policy, spelled out
- Declined: moving the demo commands into the code block. README's structure is PR 7's to decide.
- The first tester run returned NEEDS_FIXES, because the gate prompt listed only the plan's original files and not README.md, docs/roadmap.md and .config/dotnet-tools.json. The change was right and the list was stale; the tester reran with the full list.
