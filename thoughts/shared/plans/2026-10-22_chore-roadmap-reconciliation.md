# Plan: reconcile `docs/roadmap.md` with merged work (`chore/roadmap-reconciliation`)

Date: 2026-10-22. Branch: `chore/roadmap-reconciliation` (from `98b90c5`). Lane: light lane
(docs only, no schema change, no new public behavior). PR title: `docs(roadmap): reconcile with #15-#17 and record #16's runtime limitation`.
Revised: 2026-10-22 (Revision 1, at `51884b3`).

## Revision 1

Answers `thoughts/shared/reviews/2026-10-22_chore-roadmap-reconciliation-code-review.md` (Verdict:
NEEDS_REVISION, Head `51884b3`) by applying the human's decisions. Items 1–3, committed in
`51884b3`, are unchanged.

- **F1 / R1: option (b), no settling experiments.** The Facts list the contrary records with
  file:line. Item 4 gives the exact replacement for the "Remaining gap — enforcement" sentences. The
  missing dedicated check stays, kept apart from the unknown effectiveness of `.gitattributes`
  normalization and the format step. The disagreeing records are cited as not re-verified, and "so
  no CI step fails on a CRLF file and the LF rule is unenforced" goes. The disposition keeps
  `its own chore` but no longer presumes the rule is unenforced; by the human's decision this
  departs from R1's "keep the disposition". It says "stops CRLF" where the human's example said
  "rejects CRLF", because `.gitattributes` normalization converts line endings and rejects nothing.
- **F2 / R2: remove the version numbers.** The #16 Facts bullet records PR #16's "Runtime limitation
  (unresolved)" section as the main session read it, and the only repo source of the numbers. Item 5
  deletes the version sentence, keeps the assembly-loading qualification, and words the requirement
  as "verify that the deployed runtime is patched for the advisories #16 addresses".
- **F3: one cross-reference sentence.** New item 6 adds it to the PR 7 paragraph; the three blockers
  stay. The Non-goals name it as the paragraph's only change.
- **F4: code spans in the #16 row only.** Part of item 5.
- **Human acceptance (2026-10-23):** the disposition wording "stops CRLF" and item 6's sentence are
  accepted as written. The correction runs no build, tests or runtime experiments (see Validation).

## Goal

Bring `docs/roadmap.md` in line with what is merged on `main`, and record the runtime limitation #16
left open, without changing any other open work.

## Facts

Verified at `98b90c5`. Items marked (R1) were read in the worktree at `51884b3`.

- #15 delivered Phases 1–2 and #17 delivered Phases 3–4 of `chore(agents)` (`git log`).
- #16 `fix(infra)`: patch vulnerable System.Security.Cryptography.Pkcs and Xml packages (`4f0fa30`).
  It is not in the PR table.
- SoapCore and the Windows Service are cut (Scope section, `CLAUDE.md`), but the Stack table lists
  them as live choices. The empty shells still reference them: `src/Envanex.SoapApi/Envanex.SoapApi.csproj:6`
  (SoapCore) and `src/Envanex.Worker/Envanex.Worker.csproj:9` (`Microsoft.Extensions.Hosting.WindowsServices`).
- Line endings, observed state: `git ls-files --eol` at `98b90c5` shows 408 `i/lf w/lf`, 29 `i/none w/none`, 1 `i/-text`;
  `git grep -l -I -P '\r'` finds no file at `98b90c5`, nor at `f62c4e1`, `f6df221`, `2e8497a`, `4f0fa30`.
  The roadmap's "every file at HEAD is CRLF … nothing has ever been renormalized" is therefore false.
  An earlier check with the ANSI-C-quoted form `git grep -l -I $'\r'` gave inconsistent results across
  two runs (0 files in one, 369–430 files per commit in the other); its cause is not established, and it
  is not relied on.
- Line endings, CI (R1): `.github/workflows/ci.yml:19-29` runs restore,
  `dotnet format --no-restore --verify-no-changes` (`:22-23`), build and test. No step checks line
  endings directly.
- Line endings, the records disagree on the format step (R1). None of these claims was re-verified.
  By the human's decision (R1 option (b)), no settling experiment runs in this PR.
  - It passes on CRLF: PR 6a's record in the line-ending row at `98b90c5:docs/roadmap.md:179`
    (`thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md:178` cites that row; not
    re-read in Revision 1). `51884b3` carried it into `docs/roadmap.md:180`.
  - CRLF makes it fail:
    - `.gitattributes:1-3`: "Required: .editorconfig enforces LF and CI runs
      `dotnet format --verify-no-changes` on Linux. Without this, Windows checkouts produce CRLF
      and the format check fails."
    - `docs/journal/0001-solution-scaffolding.md:148-149` (Turkish): with `.editorconfig` requiring
      LF, the CRLF that `dotnet new` writes on Windows makes `dotnet format --verify-no-changes` fail.
    - `thoughts/shared/plans/2026-08-30_solution-scaffolding.md:37-38` (Turkish, PR 1): the same.
  - `.editorconfig:3-5`: the `[*]` section, which covers `.cs` files, sets `end_of_line = lf`.
  - Weaker, because the UTF-8 BOM may be the cause, and therefore not cited in the row (the human's
    list omits them): `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md:277-278` and
    `CLAUDE.md:31-33`, where `dotnet ef`'s "CRLF with a UTF-8 BOM" fails `dotnet format`.
- Line endings, existing enforcement (R1): `.gitattributes:4` is `* text=auto eol=lf`, and
  `CLAUDE.md:169` says `.gitattributes` enforces the LF rule. Neither the normalization nor the
  format step was exercised against a CRLF file.
- #16's runtime limitation (R1, revised):
  - PR #16's "Runtime limitation (unresolved)" section, as the main session read it with
    `gh pr view 16` (its paraphrase, not a verbatim quote). Web's build output and `deps.json`
    contain neither the Pkcs nor the Xml DLL. Both are expected to come from the shared
    `Microsoft.AspNetCore.App` framework. Which copy loads, and whether the APIs run, was not
    observed. Closing the Web gap needs a patched ASP.NET Core runtime wherever Web runs. The
    roadmap did not record this at `98b90c5`.
  - The advisories #16 addresses, as its accepted research attributes them: Pkcs GHSA-846f-f764-76mx
    and GHSA-2fmv-rv2c-59x7; Xml GHSA-r955-8q5j-3h83, GHSA-7hqp-7c5m-4vq6 and GHSA-c399-9853-6jmj
    (`thoughts/shared/plans/2026-10-16_fix-cryptography-vulnerabilities.md:18-20`).
  - Runtime version numbers: this plan's record of the section names none. The only repo record
    gives both as reported values and qualifies the second: "10.0.11 on this machine (reported)"
    and "According to the reported announcements, that is 10.0.13 for the advisories they cover"
    (`thoughts/shared/reviews/2026-10-17_fix-cryptography-vulnerabilities-code-review.md:130-131`,
    `:153-154`). `51884b3` put both numbers in the row without that qualifier (F2). By the human's
    decision, the row carries no version number.
  - What stays open, in the row's wording: before the first deploy, establish which cryptography
    assemblies Web actually loads and, if the shared runtime supplies them, verify that the deployed
    runtime is patched for the advisories #16 addresses.

## Touches schema? (yes/no — db-reviewer required if yes)

No.

## ADR needed?

No. Documentation only; no architectural decision.

## Phase 1 — `docs/roadmap.md` only

Items 1–5 were applied in `51884b3`. The review verified items 1–3, and they are not redone.
Revision 1 rewrites items 4 and 5 and adds item 6. **The coder's next run is the Phase 1
correction below: items 4–6 only, as one bounded edit of `docs/roadmap.md` against its text at
`51884b3`.**

### Done in `51884b3` (unchanged)

1. `chore(agents)` row: status becomes `done (#15, #17)`; drop the link to the follow-up plan.
2. Add a row directly after it: `| — | \`fix(infra)\` — patch vulnerable System.Security.Cryptography.Pkcs and Xml packages | done (#16) |`.
3. Stack table: the `Integration` and `Background` rows state that SoapCore and the Worker hosted as a
   Windows Service are cut (see Scope) and that their empty shell projects remain (see the known-gaps row
   on `Envanex.SoapApi` and `Envanex.Worker`). Do not delete the rows.

### Phase 1 correction — the coder's next run (items 4–6)

#### Files

- `docs/roadmap.md` — modified — line 71 (the PR 7 paragraph), line 180 (the line-ending known-gaps
  row) and line 197 (the #16 known-gaps row), numbered as at `51884b3`. Item 6 adds one line, so
  rows 180 and 197 move to 181 and 198. No other line changes.

#### Item 4 — line-ending row (`docs/roadmap.md:180`)

Keep verbatim: the first sentence, the **Observed state** sentences (the `ls-files --eol` figures
and the `git grep -l -I -P '\r'` result), the label `**Remaining gap — enforcement:**`, and the
closing "Pre-existing and repo-wide; not introduced by PR 6a". Replace exactly this text:

```text
`.github/workflows/ci.yml` runs restore, `dotnet format --no-restore --verify-no-changes`, build and test, and no step checks line endings directly, so no CI step fails on a CRLF file and the LF rule is unenforced. PR 6a recorded that `dotnet format --verify-no-changes` passes on CRLF files; that observation has not been re-verified.
```

with:

```text
`.github/workflows/ci.yml` runs restore, `dotnet format --no-restore --verify-no-changes`, build and test, and no step checks line endings directly. That shows only the absence of a dedicated check; whether the existing enforcement — `.gitattributes` normalization and the format step — stops a CRLF file is not established. The repository's records disagree on whether `dotnet format --verify-no-changes` rejects CRLF: PR 6a recorded that it passes on CRLF files, while `.gitattributes:1-3`, journal 0001 and the 2026-08-30 solution-scaffolding plan record that CRLF makes it fail, and `.editorconfig` sets `end_of_line = lf`. None of these historical claims has been re-verified.
```

Then replace the disposition `its own chore — a CI check that fails on CRLF` with
`its own chore — settle whether the existing enforcement stops CRLF, and add a dedicated CI check if it does not`.
The row does not cite ADR 0007 or `CLAUDE.md:31-33` (see Facts).

#### Item 5 — #16 row (`docs/roadmap.md:197`)

Exactly three changes:

- F4: "neither the System.Security.Cryptography.Pkcs nor the Xml DLL" becomes "neither the
  `System.Security.Cryptography.Pkcs` nor the `System.Security.Cryptography.Xml` DLL". `deps.json`
  and `Microsoft.AspNetCore.App` are already code spans. "Web" and "#16" stay plain text, as
  elsewhere in the roadmap (for example `:183`).
- F2: delete the sentence "#16 also recorded the locally installed ASP.NET Core runtime as 10.0.11
  and the reviewed Microsoft announcements as naming 10.0.13 as the patched runtime." and the space
  before it.
- "make sure the deployed runtime is a patched one" becomes "verify that the deployed runtime is
  patched for the advisories #16 addresses".

The whole line then reads exactly:

```text
| The cryptography fix in #16 changed package references only. It recorded that Web's build output and `deps.json` contain neither the `System.Security.Cryptography.Pkcs` nor the `System.Security.Cryptography.Xml` DLL, so both are expected to come from the shared `Microsoft.AspNetCore.App` framework, which the package fix does not change. Which copy actually loads at run time, and whether the cryptography APIs run at all, was not observed. Establish which cryptography assemblies Web actually loads and, if the shared runtime supplies them, verify that the deployed runtime is patched for the advisories #16 addresses | PR 7 — before the first deploy |
```

The row still claims neither that the current runtime is vulnerable nor that the package fix
patched the shared runtime. The PR-table row for #16 (`:60`) keeps its title, which matches
`4f0fa30`'s subject. No runtime investigation or dependency change.

#### Item 6 — PR 7 paragraph (`docs/roadmap.md:71`)

Replace line 71:

```text
and the login rate-limit partition key behind the reverse proxy. PR 7 also starts with a design
```

with exactly these two lines:

```text
and the login rate-limit partition key behind the reverse proxy. The runtime verification row
for #16 in the same table must also close before the first deploy. PR 7 also starts with a design
```

Lines 67–70 and 72–76 stay as they are, so "Three blockers in the known-gaps table must close
first" and its three items do not change.

#### Signatures

None (documentation only).

#### Tests to add

None: documentation only, no behavior change. The Validation checks stand in; the light-lane code
review and tester follow.

#### Validation

Run from the repository root and paste the raw output.

1. `git status --short`: ` M docs/roadmap.md`. The only other entries allowed are this plan file
   and the review file named in Revision 1, if the human has not committed them yet.
2. `git diff --unified=0 51884b3 -- docs/roadmap.md`: exactly three hunks, whose headers start
   `@@ -71 +71,2 @@`, `@@ -180 +181 @@` and `@@ -197 +198 @@`. They change only what items 6, 4
   and 5 specify.
3. `git diff --check 51884b3 -- docs/roadmap.md`: no output.
4. `git ls-files --eol docs/roadmap.md`: `w/lf`.
5. `head -c 3 docs/roadmap.md | od -An -tx1`: `23 20 52` (`# R`), so no BOM.
6. Grep tool on `docs/roadmap.md`: no match for `10\.0\.1[13]`, `LF rule is unenforced`,
   `a CI check that fails on CRLF` or `is a patched one`. One match each for `Three blockers in`
   (line 69) and `must also close before the first deploy` (line 72).

No build, tests or runtime experiments run for this correction, by the human's explicit
instruction for this bounded documentation change. This departs from the general build rule in
`.claude/agents/coder.md:26`; the light-lane tester's gate run remains the build and test check.

## Non-goals

No other row changes. In particular, these stay as they are: the README row, the empty-shell row,
the three PR 7 blockers (rate-limit partition key, demo lifecycle, cookie revalidation / research
Decision 11), the hook-test CI row, the residual-risks row, the PR 7 paragraph apart from the one
sentence item 6 inserts (its three blockers and every other word stay), and every deferred or
"needs an owner" entry. No README, ADR, journal, CI or code change.

## Rollback notes

Documentation only. Reverting the correction's commit restores `docs/roadmap.md` as committed in
`51884b3`; items 1–3 do not depend on the correction.
