Verdict: NEEDS_REVISION (recipe only)
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md (Revision 2)
Range: f6df221d70faab6dc629b761b30b7479489d5b3c..9e32191bd1dd9a40649af5e81284a5b878e96642

## Verdict
NEEDS_REVISION

This is recipe-only. One real defect is left, plus two small hardening items. All three are given below as exact edits. If they are applied as written, no further review round is needed.

Plan: C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md (Revision 2)
Previous review: C:\projects\envanex\thoughts\shared\reviews\2026-09-25_chore-agents-plan-review-r2.md

## Findings

### Were r2's required changes 1-11 carried out?
All eleven are in the sections the Revision 2 table (Plan:87-99) names.

| # | Where it is now | Result |
|---|---|---|
| 1 | Plan:194-198 (sub-steps 9-11, and the tester's Head is the review-file commit); Plan:1131 (CLAUDE.md addition P) | done |
| 2 | Plan:583; tests at Plan:738-739; Plan:884 | done |
| 3 | Plan:299-303 (rules 1-4); tests at Plan:399-405; M1.8 and M1.9 at Plan:460-461; Plan:1165 | done |
| 4 | Plan:297-298 and 315-319 (`getCommand`, `extractTarget`); tests at Plan:394-396; Plan:570 and 717 | done |
| 5 | Plan:1041 | done |
| 6 | Plan:1013 | done, but see finding 1 |
| 7 | Plan:1022 and 1030-1032, Plan:1047, Plan:1135-1136, Plan:1187 | done |
| 8 | Plan:1008 (the heading at `.claude/skills/pr/SKILL.md:10` really does read "verify all four"); Plan:1005-1007 | done |
| 9 | Plan:602-615; tests at Plan:751-753 and 768-776; M2.8 at Plan:810; C2.6 at Plan:230 and 245-249; P2.5 5f at Plan:860 | done |
| 10 | Plan:173, 1164, 1189 | done |
| 11 | Plan:932 and 934-938 | done |

### Are the new specifics sound?
- **The base-check line under coder-bash-guard (traced by hand):**
  - Both `git` matches are allowed: `rev-parse`, and `merge-base` after `$(`.
  - No other line in the Phase 1-4 validation blocks names `git` outside an allowed subcommand. `rm -r .claude/skills/verify` and the `mutate-*.sh` calls contain no `git` word.
- **The redaction rules, traced against each named test:**
  - Rule 1: `export MSSQL_SA_PASSWORD='S3cretValue4'` becomes `MSSQL_SA_PASSWORD=***`. The double-quoted case and the connection-string case also come out right.
  - Rule 2: `-P "S3cretValue7"` is redacted, and `-U sa` survives. The rule matches `docker-compose.yml:16`'s real form, `-P \"$$MSSQL_SA_PASSWORD\"`.
  - Rule 3: all four user-secrets tests leave no secret, and the `&&` test keeps `&& dotnet build`.
  - M1.8 goes red: without the quoted alternatives, `[^;'"\s]+` can't start at `'`, and `S3cret` does not match rule 4's `secret`.
  - M1.9 goes red: no other rule covers `-P`.
  - Rule order is safe: rule 1 runs before rule 3, so a connection string inside a user-secrets value is covered past the first `;`.
- **The Bash target source (interpretation G):**
  - It is sound, and the extractor choice fails closed on a mis-attached hook.
  - Every allow case in Phase 2 goes through `getCommand`. A `runGuard` that ignored `extractTarget` would turn all of them red.
- **The dotnet fixed-token list:**
  - It is sound.
  - M2.8 turns all three required block tests red, because `--results-directory` and `--report` don't start with `--output`.
  - One gap, which is harmless in practice (item 3 below): a `--filter` argument may start with `/`.
- **Precondition 7:**
  - It is sound. `--is-ancestor X X` exits 0, so two files with the same Head resolve to one "newest" Head.
  - The trigger paths match the diff paths.
- **The PENDING verdict:** sound. `/pr` accepts only `SECURITY_CLEAR`. A human edit made after the commit leaves the tree dirty, so precondition 2 catches it.

### 1. BLOCKING: Plan:930 conflicts with Plan:1013 and Plan:1052, so a clean tree fails the gate
- `run_step` says: "if that output does not end with a newline, appends one". On a clean tree, `git status --short` prints nothing, and empty output does not end with a newline.
- A literal implementation, or the usual `out=$(eval …); printf '%s\n' "$out"`, therefore writes a blank line between `$ git status --short` and `exit=0 step=status kind=info`.
- Precondition 3 (Plan:1013) requires "no line between" those two lines. The tester rule (Plan:1052) returns NEEDS_FIXES whenever the status section is not empty.
- Result: every tester run and every `/pr` fails on a clean tree.
- P3.1 (Plan:1062-1069) does not check this, so the first sign would be the tester failing in P4.4.
- This is a conflict between interpretation M and required change 6. Both are new in Revision 2.

### 2. Minor: Plan:299, the redaction rules don't say they replace every occurrence
"Replaces each secret value" is the only wording. A coder who writes `.replace(/…/i, …)` without the `g` flag redacts only the first match. For example, the second password in `…Password=a… && …Password=b…` stays in the log, and no named test would catch it.

### 3. Minor: Plan:605, a `--filter` argument may start with `/`
`/p:…` is MSBuild's other switch prefix. `dotnet test` consumes the token as the filter value, so this is probably harmless. Interpretation L's "fails harmlessly" claim, for `dotnet build --filter /p:X`, is UNVERIFIED. Settle it with `dotnet build --filter /p:PreBuildEvent="echo PWNED > pwned.txt"; test -f pwned.txt; echo $?` in the C2.6 scratch project. Excluding `/` is cheaper than settling the question.

## Required changes (exact recipe; apply as written)

1. **Plan:930:** replace the sub-bullet with:
   "if that output is non-empty and does not end with a newline, appends one. Empty output appends nothing, so an empty section is the `$ <command>` line immediately followed by the `exit=` line."

   **Plan:1013:** append:
   ", meaning the line after `$ git status --short` is exactly `exit=0 step=status kind=info`"

   **Plan:1052:** after "not empty", insert:
   "(the line after `$ git status --short` is not `exit=0 step=status kind=info`)"

   **P3.1 (after Plan:1067):** add the bullet:
   "In `gates.txt`, the line after `$ git status --short` is exactly `exit=0 step=status kind=info`: `grep -A1 -x '\$ git status --short' TestResults/chore-agents-hardening/gates.txt` shows those two lines and nothing else."
2. **Plan:299:** after "with `***`.", insert:
   "Every rule replaces every occurrence (global flag)."

   **After Plan:398:** add the test:
   "`redacts every Password occurrence` — target `a Password=S3cretValue10; b Password=S3cretValue11`. The log line lacks both `S3cretValue10` and `S3cretValue11`."
3. **Plan:605:** change "does not start with `-`" to "does not start with `-` or `/`".

   **Plan:775:** change the test to:
   "`blocks dotnet test --filter -p:PreBuildEvent=x` and `blocks dotnet test --filter /p:PreBuildEvent=x` (the filter argument may not start with `-` or `/`)".

   **Plan:126:** change "must not start with `-`" to "must not start with `-` or `/`".

With these three edits the plan is ready for the coder. Nothing Revision 1 settled was reopened.

Verdict: NEEDS_REVISION
