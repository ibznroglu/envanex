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
