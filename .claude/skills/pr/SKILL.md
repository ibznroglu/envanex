---
name: pr
description: Push the current feature branch, open a pull request, wait for CI, squash-merge it, and return to an updated main. Invoked manually via /pr — never runs automatically.
disable-model-invocation: true
effort: low
allowed-tools: Read, Glob, Grep, Write, Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff *), Bash(git rev-parse *), Bash(git merge-base *), Bash(git push *), Bash(git switch main), Bash(git pull --ff-only), Bash(git branch --show-current), Bash(gh pr *), Bash(gh run *)
---

# Open and squash-merge a PR

`$ARGUMENTS` names the active plan file. Without it, ask for the path and STOP.

## Definitions

- `<slug>` is `git branch --show-current` with `/` replaced by `-`.
- A review file belongs to this branch when its name matches `thoughts/shared/reviews/*_<slug>-*.md`.
- A review file's kind is its name without the leading `YYYY-MM-DD_<slug>-` and without a trailing
  `-r<N>` before `.md`: for example `code-review-branch`, `code-review-phase3`, `security-review`.
  Within one kind only the file with the highest N counts; a file without `-r<N>` has N = 1. Two
  files of one kind with the same N fail every precondition that reads that kind.
- A file's header is its first five lines: `Verdict:`, `Reviewer:`, `Scope:`, `Range:`, `Head:`.
- **Head rule R:**
  1. `git rev-parse --verify --quiet "<Head>^{commit}"` exits 0. An unknown or mistyped SHA fails here.
  2. The file's `Head:` equals `git rev-parse HEAD`, or `git diff --name-only <Head> HEAD` exits 0
     and every path it prints is under `thoughts/shared/reviews/`.
- **Git errors:** any non-zero exit from `git rev-parse`, `git diff` or `git merge-base` during a
  precondition check fails that precondition. The output of a failed command is never read as an
  empty list. The one exception is `git merge-base --is-ancestor`, where exit 1 means "not an
  ancestor"; that also fails the precondition.

## Preconditions — verify all seven, STOP if any fails

1. The current branch is NOT `main`.
2. The working tree is clean (`git status --short` is empty).
3. `TestResults/<slug>/gates.txt` meets three conditions:
   - the `head=` value on its first line equals `git rev-parse HEAD`
   - its `status` section is empty: the line after `$ git status --short` is exactly
     `exit=0 step=status kind=info`
   - its last line is exactly `gate-exit=0 failed=`
4. `TestResults/<slug>/tester-verdict.md` has `Verdict: READY_TO_PUSH`, and its `Head:` equals
   `git rev-parse HEAD`. An in-session verdict alone does not suffice.
5. **Branch code review:** a review file of this branch whose header has `Reviewer: code-reviewer`
   and `Verdict: APPROVED`, satisfying rule R, that either is of kind `code-review-branch`, or has
   a `Range:` whose base equals `git merge-base main HEAD` (the light lane).
6. **Security review:** required unless every path in the `files` section of `gates.txt` starts
   with `docs/` or `thoughts/`. It needs the file of kind `security-review` with
   `Verdict: SECURITY_CLEAR`, satisfying rule R. Its first five lines are exact; the `Verdict:`
   line carries exactly one of the three values:

   ```
   Verdict: PENDING | SECURITY_CLEAR | SECURITY_FINDINGS
   Reviewer: /security-review
   Scope: whole branch
   Range: <git merge-base main HEAD>..<head>
   Head: <sha>
   ```

   The verbatim `/security-review` output follows the header. The main session writes
   `Verdict: PENDING` when it saves the file and never writes any other value. Only the human
   changes the line, after reading the output. `PENDING` and `SECURITY_FINDINGS` both stop `/pr`.
7. **DB review:**
   - required when the plan's `Touches schema?` answer is yes, or the `files` section has a path
     under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/`
   - needs at least one file of a `db-review-*` kind, and every such file (highest N per kind) has
     `Verdict: DB_APPROVED`
   - each such file's `Head:` passes step 1 of rule R and is an ancestor of HEAD
     (`git merge-base --is-ancestor <Head> HEAD` exits 0)
   - the newest db-review Head is the one every other db-review Head is an ancestor of
     (`git merge-base --is-ancestor <other> <this>` exits 0); if no single such Head exists, this fails
   - no path under `db/`, `src/Envanex.Infrastructure/Migrations/` or
     `src/Envanex.Infrastructure/Persistence/` appears in `git diff --name-only <newest db-review Head> HEAD`

## Procedure

1. Record the validated head: `git rev-parse HEAD`. Call it V. Every later step refers to V.
2. `git push -u origin <branch>`.
3. Compose the PR **title** as a Conventional Commit: `type(scope): summary`. This is the single
   commit that will land on `main` after squash — it matters more than any commit inside the branch.
4. Write the PR **body** to `TestResults/PR_BODY.md` with the Write tool (Read the file first if it
   exists):

   ```
   ## What
   (one paragraph)

   ## Why
   (link the plan: thoughts/shared/plans/YYYY-MM-DD_topic.md)

   ## Phases included
   ## Schema changes
   (migration name, or "none")
   ## Verdicts
   - <review file path> — <its Verdict: line> — <its Head: line>   (one per file preconditions 5-7 used)
   - TestResults/<slug>/tester-verdict.md — <Verdict:> — <Head:> — <Gates-sha256:>
   - <plan-review file path> — <its Verdict: line> (cited, not gated)
   ## Validation
   (the stdout of the last `/gate` run, verbatim)
   ## ADR
   (docs/adr/NNNN-title.md — or "none")
   ```

5. `gh pr create --title "<title>" --body-file TestResults/PR_BODY.md --base main`.
6. `gh pr checks --watch`. If CI fails, report the failure with file:line and STOP — the human
   routes the fix back to the coder. Do not fix it yourself.
7. Recheck before merging, and STOP if either check fails:
   - `gh pr view --json headRefOid --jq .headRefOid` prints V
   - `gh pr checks` exits 0, with every check passing
8. `gh pr merge --squash --delete-branch --match-head-commit V`.
9. `git switch main`, then `git pull --ff-only` (two calls).
10. Report the PR number, the squashed commit hash on `main`, and `git status --short`.

## Rules

- NEVER merge with red or pending CI.
- NEVER force-push, and NEVER use `gh pr merge --admin`. The deny list also blocks `git push`
  with `--force`, `-f` or a `+refspec`.
- Merge only with `--match-head-commit` set to V, the head the preconditions validated.
- Update `main` only with `git pull --ff-only`.
- NEVER merge a PR whose body cites a verdict that no file satisfying preconditions 3-7 carries.
- Evidence and doc commits land before the PR-end sequence. A commit after the tester ran
  invalidates its verdict (precondition 4). Re-run the tester.
- No agent writes `SECURITY_CLEAR` or `SECURITY_FINDINGS`. The saved security-review file starts as
  `Verdict: PENDING`, and only the human changes it.
- If the ADR file listed in the plan does not exist yet, STOP and tell the user to write it.
  The agents do not write ADRs.
