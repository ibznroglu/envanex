# Plan: reconcile `docs/roadmap.md` with merged work (`chore/roadmap-reconciliation`)

Date: 2026-10-22. Branch: `chore/roadmap-reconciliation` (from `98b90c5`). Lane: light lane
(docs only, no schema change, no new public behavior). PR title: `docs(roadmap): reconcile with #15-#17 and record #16's runtime limitation`.

## Goal

Bring `docs/roadmap.md` in line with what is merged on `main`, and record the runtime limitation #16
left open, without changing any other open work.

## Facts verified at 98b90c5

- #15 delivered Phases 1–2 and #17 delivered Phases 3–4 of `chore(agents)` (`git log`).
- #16 `fix(infra)`: patch vulnerable System.Security.Cryptography.Pkcs and Xml packages (`4f0fa30`).
  It is not in the PR table.
- SoapCore and the Windows Service are cut (Scope section, `CLAUDE.md`), but the Stack table lists
  them as live choices. The empty shells still reference them: `src/Envanex.SoapApi/Envanex.SoapApi.csproj:6`
  (SoapCore) and `src/Envanex.Worker/Envanex.Worker.csproj:9` (`Microsoft.Extensions.Hosting.WindowsServices`).
- Line endings: `git ls-files --eol` at `98b90c5` shows 408 `i/lf w/lf`, 29 `i/none w/none`, 1 `i/-text`;
  `git grep -l -I -P '\r'` finds no file at `98b90c5`, nor at `f62c4e1`, `f6df221`, `2e8497a`, `4f0fa30`.
  The roadmap's "every file at HEAD is CRLF … nothing has ever been renormalized" is therefore false.
  An earlier check with the ANSI-C-quoted form `git grep -l -I $'\r'` gave inconsistent results across
  two runs (0 files in one, 369–430 files per commit in the other); its cause is not established, and it
  is not relied on.
  `.github/workflows/ci.yml` runs restore, `dotnet format --no-restore --verify-no-changes`, build and
  test; no step checks line endings directly. The recorded observation that `dotnet format
  --verify-no-changes` passes on CRLF files was not re-verified.
- #16's PR body, "Runtime limitation (unresolved)": Web's build output and `deps.json` contain neither the
  Pkcs nor the Xml DLL, so both are expected to come from the shared `Microsoft.AspNetCore.App`
  framework; which copy loads at run time was not observed; closing it needs a patched ASP.NET Core
  runtime wherever Web runs. The roadmap does not record this.

## Touches schema? (yes/no — db-reviewer required if yes)

No.

## Phase 1 — `docs/roadmap.md` only

1. `chore(agents)` row: status becomes `done (#15, #17)`; drop the link to the follow-up plan.
2. Add a row directly after it: `| — | \`fix(infra)\` — patch vulnerable System.Security.Cryptography.Pkcs and Xml packages | done (#16) |`.
3. Stack table: the `Integration` and `Background` rows state that SoapCore and the Worker hosted as a
   Windows Service are cut (see Scope) and that their empty shell projects remain (see the known-gaps row
   on `Envanex.SoapApi` and `Envanex.Worker`). Do not delete the rows.
4. Line-ending known-gaps row: replace the false claim. State the observed state (the index is LF at
   HEAD and at the commits above; the exceptions are binary or line-ending-free files), and keep the gap
   that remains: no CI step fails on a CRLF file, so the LF rule is unenforced. The recorded observation
   about `dotnet format` stays, marked as recorded in PR 6a and not re-verified. The disposition becomes
   `its own chore — a CI check that fails on CRLF`; the renormalization part is dropped.
5. Known gaps: add one row, linked to #16, after the row on PR 7's deploy method. It restates #16's
   runtime limitation without claiming that the current runtime is vulnerable or that the package fix
   patched the shared runtime, and closes with: establish which cryptography assemblies Web actually
   loads and, if the shared runtime supplies them, make sure the deployed runtime is patched.
   Disposition: `PR 7 — before the first deploy`. No runtime investigation or dependency change here.

## Non-goals

No other row changes. In particular, these stay as they are: the README row, the empty-shell row,
the three PR 7 blockers (rate-limit partition key, demo lifecycle, cookie revalidation / research
Decision 11), the hook-test CI row, the residual-risks row, the PR 7 paragraph, and every deferred or
"needs an owner" entry. No README, ADR, journal, CI or code change.

## Validation

`git status --short` shows only `docs/roadmap.md` (modified) and this plan (untracked until committed);
`git diff --check` is clean; both files are LF only. No build or test change is expected; the light-lane review and tester follow as usual.
