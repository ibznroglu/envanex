Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: whole branch
Range: 98b90c55abebf61596825d9389d5a0d2cddcb858..51884b3d8ff30eb69557f4ab63e405c60ac43ba6
Head: 51884b3d8ff30eb69557f4ab63e405c60ac43ba6

## Findings

Evidence labels:
- observed: read in this worktree by this review.
- inferred: a conclusion drawn from what was observed.
- UNVERIFIED: needs a command this review cannot run. The settling command is given.

This review ran nothing. There was no `git diff`, so the pre-branch text of `docs/roadmap.md` was
reconstructed from two sources:
- the Phase 4 roadmap diff recorded at
  `thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md:4338-4406`;
- line-number citations in earlier review and plan files.

### F1. docs/roadmap.md:180: the corrected line-ending row asserts a conclusion that is not established and that repo sources contradict. Severity: Medium (blocking).

The row reads: "no step checks line endings directly, so no CI step fails on a CRLF file and the LF
rule is unenforced. PR 6a recorded that `dotnet format --verify-no-changes` passes on CRLF files;
that observation has not been re-verified."

- **The "so" does not follow.** The premise is true: `.github/workflows/ci.yml:19-29` has no
  line-ending step. But the format step reads `.editorconfig:3-5`, whose `[*]` section sets
  `end_of_line = lf`, and that section covers `.cs` files.
- **Four repo sources state the opposite, and the row cites none of them (observed):**
  - `.gitattributes:1-3`, one of the sources this review was asked to check against: "CI runs
    `dotnet format --verify-no-changes` on Linux. Without this, Windows checkouts produce CRLF and
    the format check fails."
  - `docs/journal/0001-solution-scaffolding.md:148-149` and
    `thoughts/shared/plans/2026-08-30_solution-scaffolding.md:37-38` (PR 1): CRLF from `dotnet new`
    makes `--verify-no-changes` fail.
  - `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md:277-278` and `CLAUDE.md:31-33`:
    `dotnet ef`'s "CRLF with a UTF-8 BOM" fails `dotnet format --verify-no-changes`. This is weaker
    evidence, because the BOM may be the cause.
- **The row's only support is the PR 6a observation, which the row itself marks as not re-verified.**
  - Inferred: the old row paired that observation with "every file at HEAD is CRLF". If the
    observation was made on those files, it rests on the premise this row now disproves.
  - The plan's record of a `$'\r'` run that matched 369-430 files per commit (plan:22-24) fits how
    that premise could have arisen.
  - UNVERIFIED. These settle it:
    - `git show 98b90c5:docs/roadmap.md | sed -n 179p` shows the old wording.
    - `git grep -l -I -P '\r' 83f6cefbccd6f8c739748aecb3d55d7ca43ac218` checks main right after
      PR 6a (`.git/logs/HEAD:62`).
    - The same command at `02b90855652eb06cbc7c93a56d95e467b6273e3d` checks the commit that recorded
      the gap (`.git/logs/HEAD:42`).
- **"The LF rule is unenforced" conflicts with `CLAUDE.md:169`** ("`.gitattributes` enforces this").
  - Under `* text=auto eol=lf` (`.gitattributes:4`), git converts CRLF to LF on `git add` for files
    it detects as text, unless the file is already committed with CRLF.
  - With the index all LF (per the row), the remaining gap is narrower (inferred): a commit from a
    client that ignores `.gitattributes`, or a file that git treats as binary.
  - UNVERIFIED. Settle it in a scratch clone:
    `printf 'a\r\nb\r\n' > crlf-probe.txt && git add crlf-probe.txt && git ls-files --eol crlf-probe.txt`
    (expect `i/lf w/crlf`), then `git rm -q --cached crlf-probe.txt && rm crlf-probe.txt`.
- **Whether the format step fails on a CRLF `.cs` file is UNVERIFIED.** Settle it in a clean
  worktree:
  `sed -i 's/$/\r/' src/Envanex.Domain/Common/Entity.cs && dotnet format --verify-no-changes; echo "exit=$?"; git checkout -- src/Envanex.Domain/Common/Entity.cs`.
  A non-zero exit means the PR 6a observation is wrong for C# files.
- **Cause.**
  - Plan item 4 (plan:44-48) prescribes the wording "no CI step fails on a CRLF file, so the LF rule
    is unenforced".
  - The plan's Facts (plan:25-27) mark the PR 6a observation as unverified but omit the contrary
    records.
  - The coder followed the plan, so the fix starts in the plan.

### F2. docs/roadmap.md:197: the version sentence drops the only recorded coverage qualifier, and the plan's Facts do not source it. Severity: Low (blocking, small).

The sentence: "#16 also recorded the locally installed ASP.NET Core runtime as 10.0.11 and the
reviewed Microsoft announcements as naming 10.0.13 as the patched runtime."

- **No source in the plan.** The plan's quoted record of #16's "Runtime limitation (unresolved)"
  section (plan:28-31) contains no version information.
- **The only repo source qualifies the second value (observed).** It records both values as reported:
  "10.0.11 on this machine (reported)", and "According to the reported announcements, that is 10.0.13
  for the advisories they cover"
  (`thoughts/shared/reviews/2026-10-17_fix-cryptography-vulnerabilities-code-review.md:130-131`,
  `:153-154`).
- **The risk.** Without that qualifier, "the patched runtime" can be read as 10.0.13 closing all five
  GHSAs at `thoughts/shared/plans/2026-10-16_fix-cryptography-vulnerabilities.md:18-20`.
- **Not settled here.** What the PR body itself says is UNVERIFIED. This settles it:
  `gh pr view 16 --json body --jq .body`.

### F3. docs/roadmap.md:197 vs :69-71: a fourth before-deploy item sits beside "Three blockers". Severity: Info (non-blocking).

- The disposition "PR 7 — before the first deploy" makes the row a precondition of the first deploy.
- :69-71 says "Three blockers in the known-gaps table must close first" and names only :161-163.
- This follows existing practice for unlabelled deploy items (:185, :193), and plan:57-59 keeps the
  paragraph and the blockers unchanged. No change is required.
- Whether the deployed runtime's patch level should become a labelled blocker is a human or planner
  decision for PR 7.

### F4. docs/roadmap.md:197: inconsistent code spans. Severity: nit (optional).

`Microsoft.AspNetCore.App` and `deps.json` are code spans; System.Security.Cryptography.Pkcs and Xml
are not.

### Verified: no finding

**PR table (observed).**
- :59 now reads `done (#15, #17)`, and the follow-up-plan link is gone (plan:39). The old text is at
  workflow-skills plan:4350.
- :60 matches plan:40 exactly, and matches 4f0fa30's subject "fix(infra): patch vulnerable
  System.Security.Cryptography.Pkcs and Xml packages (#16)".
- Merge order: #15 = 2e8497a "(phases 1-2)", #16 = 4f0fa30, #17 = 98b90c5 (the session git log, and
  `.git/logs/HEAD:192-219`). This matches the table.
- Nothing else depended on the removed link. `docs/ai-workflow.md:13` links the plan independently.

**Stack (observed).**
- :27-28 meet plan item 3 (plan:41-43). Both rows are kept, both say "cut (see Scope)", and both name
  the remaining shell and point to :183.
- The rows are accurate:
  - Scope (:10-14) cuts SOAP and the worker.
  - `src/Envanex.SoapApi/` holds only its csproj, with SoapCore at :6.
  - `src/Envanex.Worker/Envanex.Worker.csproj:9` references `Microsoft.Extensions.Hosting.WindowsServices`.
- "Empty" follows the established wording (CLAUDE.md Architecture, roadmap :183), although
  `src/Envanex.Worker/Program.cs:1-5` is the template host. That is acceptable.

**Line-ending row, apart from F1.**
- `.gitattributes:4` and `CLAUDE.md:169` are cited correctly.
- The ci.yml step list matches `.github/workflows/ci.yml:19-29`. The upload step is left out, which
  does not matter.
- The observed-state figures match plan:19-21:
  - 408 + 29 + 1 = 438, so "the other 30" is right.
  - `i/none` means text with no line ending, and `i/-text` means binary.
- The figures themselves are UNVERIFIED. These settle them:
  - `git switch --detach 98b90c5 && git ls-files --eol | awk '{print $1}' | sort | uniq -c`.
    `ls-files` reads the index, so it needs that checkout.
  - `for c in 98b90c5 f62c4e1 f6df221 2e8497a 4f0fa30; do echo "$c"; git grep -l -I -P '\r' "$c"; done`.
- As plan:46-48 asks, the renormalization step is gone, the disposition matches, and the PR 6a
  observation is labelled.

**#16 row (:197), apart from F2.**
- It sits directly after the PR 7 deploy-method row (:196), as plan:49 requires.
- Its closing sentence and disposition match plan:51-53.
- It does not claim that the package fix patched the shared runtime ("which the package fix does not
  change"). It does not claim that the current runtime is vulnerable.
- It agrees with the cryptography plan (:224-242) and with finding 3 of its code review (:77-160):
  - Web ships no Pkcs DLL, and Xml is pruned.
  - The framework copy is the expected source.
  - Loading was not established.
- `#16` is plain text, as in the status cells. That satisfies "linked to #16" (plan:49).

**Non-goals.** This is inferred from the structure; there was no diff, so it is UNVERIFIED.
- The file is exactly two lines longer than at 98b90c5:
  - The 2026-10-21 branch review cites the pipeline block as :206-217
    (`thoughts/shared/reviews/2026-10-21_chore-agents-workflow-skills-code-review-branch.md:114`).
    It is now at :208-219.
  - The workflow-skills plan put the line-ending and README rows at :179-180 (plan:178). They are now
    at :180-181.
- The rows shown as context in the recorded Phase 4 diff are unchanged. These are now :54-58, :61,
  :63, :195-196, :198-202 and :204-219.
- Rows with no recorded pre-image cannot be compared here: :161-179 and :181-194.
- This settles it: `git diff 98b90c5..51884b3 -- docs/roadmap.md`. Expected: :27, :28, :59 and :180
  modified, and :60 and :197 added.

**Table integrity (observed).** The changed rows have the right cell counts: 2 in the Stack and
Known-gaps tables, 3 in the PR table. No cell contains an unescaped `|`, and `'\r'` sits inside a code
span.

**Line endings of the changed files.**
- The search tool finds no CR in either changed file, and no trailing whitespace in
  `docs/roadmap.md`.
- The search tool cannot establish that there is no BOM.
- The plan's Validation step (plan:64-65) is UNVERIFIED. These settle it: `git diff --check 98b90c5..51884b3`
  and `git ls-files --eol docs/roadmap.md thoughts/shared/plans/2026-10-22_chore-roadmap-reconciliation.md`.

**Plan file.**
- The light lane fits (plan:3-4, :33-35): docs only, no schema change.
- The facts at plan:13-18 match the csproj lines and the commit subjects.

## Required changes

**R1 (F1).** The planner revises plan:25-27 and item 4 (plan:44-48). The coder then edits only the
"Remaining gap — enforcement" sentences of `docs/roadmap.md:180`, by one of two routes:

- **(a) Settle first.** The human runs the two experiments in F1, and the row records the observed
  outcomes with their commands, as its "Observed state" sentence already does.
- **(b) Without settling.** The row:
  - keeps "no CI step checks line endings directly";
  - states that the repository's records disagree on whether the format step fails on a CRLF C#
    file: `.gitattributes:1-3`, journal 0001, ADR 0007 and `CLAUDE.md:31-33` say it fails, and
    PR 6a's row said it passes;
  - states that neither has been re-verified;
  - drops "so no CI step fails on a CRLF file and the LF rule is unenforced", or marks it UNVERIFIED.

Either way:
- Keep the observed-state sentence and the disposition `its own chore — a CI check that fails on CRLF`.
- List the contrary records in the plan's Facts.
- Change no other row.

**R2 (F2).**
- The planner adds to plan:28-31 the #16 PR-body text that the version sentence relies on. The human
  gets it with `gh pr view 16 --json body --jq .body`.
- The coder then does one of the following:
  - adds the coverage qualifier: "for the advisories they cover", per the code review at :153-154;
  - names the advisories that the announcements cover;
  - if the PR body has no version record, attributes the sentence to the #16 code review as
    reported values, or removes it.
- The rest of :197 does not change.

F3 and F4 require no change.
