Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md
Range: 56fd6d41bcdb7d1b440bb1f161b08709debc5d0a..63c2c3777b509ed48f86a51752f037c36f106ccf
Head: 63c2c3777b509ed48f86a51752f037c36f106ccf

## Findings

Plan lines are cited as `plan:N`, meaning `thoughts/shared/plans/2026-10-24_numeric-bounds.md`. I read every file the plan cites. The Grep results below come from the Grep tool, run during this review. I could not compile, run tests or run git, so every claim about those is marked UNVERIFIED.

### Blocking

**F1 — plan:475-477 and plan:836-837 — four Grep checks cannot pass as written.**

- **The patterns.**
  - `WithErrorCode(UnitOfMeasureErrors.ConversionFactorTooPrecise.Code)`
  - `WithErrorCode(UnitOfMeasureErrors.ConversionFactorTooLarge.Code)`
  - `WithErrorCode(ProductErrors.ListPriceAmountTooLarge.Code)`
  - `WithErrorCode(ProductErrors.ReorderPointTooLarge.Code)`
- **Why they fail.** The Grep tool reads each pattern as a regex. The parentheses form a group, so each pattern matches only the text with the parentheses removed.
- **Demonstrated on existing code.**
  - `CreateUnitOfMeasureCommandValidator.cs:13` contains `.WithErrorCode("UnitOfMeasure.CodeRequired");`.
  - The pattern `WithErrorCode("UnitOfMeasure.CodeRequired")` gives "No matches found" on that file.
  - The escaped pattern `WithErrorCode\("UnitOfMeasure.CodeRequired"\)` finds line 13.
- **Consequence.** On correct code these four checks return no match, while the plan expects one match each. The coder or tester either reports a false failure or has to guess a fixed pattern.
- **Why this looks like an oversight.** The plan escapes its other regexes, for example `\.WithMessage\(` at plan:478 and :840.
- **Related.** Phase 3's `\r` check (plan:841) does not name the files it covers.

**F2 — the `git status --short` expectations in Phases 1-3 describe the coder's tree, but the tester runs the command too.**

- **Where.** plan:461 with :470-471, plan:694 with :702-703, and plan:824 with :832.
- **What the plan expects.** The expectations describe the uncommitted tree, for example "exactly the eleven Files above: ten ` M` and one `??`".
- **What the tester sees.**
  - The tester runs "the plan's other validation commands that your allowlist permits, each exactly as the plan writes it" (`.claude/agents/tester.md:58-59`).
  - Its allowlist permits `git status` (`.claude/hooks/bash-allowlist.js:37`).
  - It accepts only a clean tree (`tester.md:50-53`, and `gate.sh:204` records `git status --short`). So its `git status --short` prints nothing, which contradicts the plan's expected list.
- **Consequence.** The tester must either return NEEDS_FIXES or decide on its own that the expectation does not apply.
- **Precedent in the plan.** Phase 4 already separates the coder's and the tester's checks (plan:907, plan:927-928). Phases 1-3 do not.

### Inaccurate statements

These are small, but they sit in sections that a coder or reviewer relies on.

**F3 — plan:1064 — "Phase 1 is independent of Phases 2-3" is wrong.**

- Phase 2 reuses Phase 1's `ColumnMaximum` helper (plan:588-589). P-S1 and P-S2 call it (plan:675-680).
- Reverting Phase 1 alone therefore leaves P-S1 and P-S2 without the helper, and the Phase 2 test build breaks.

**F4 — plan:58-60 — a new `Error` field turns two facts red, not three.**

- These two go red:
  - `ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping` (`ResultMappingTests.cs:67-78`);
  - `ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage` (`ResultMappingTests.cs:81-92`).
- `ResultMapping_NoMappingEntry_ShouldBeOrphaned` (`:109-123`) goes red only for a table key that has no field.
- `ResultMapping_Scan_ShouldReachTheApplicationAssembly` and `ResultMapping_NoErrorCode_ShouldBeEmpty` are unaffected.
- The plan's conclusion still holds: every phase that adds a field also adds both table entries.
- The research has the same miscount (research:229).

**F5 — plan:911-917 and :928 — the hunk headers are given as whole lines, but git will probably append a section heading.** UNVERIFIED.

- **Why git adds text.** `.gitattributes` defines no diff driver for `*.md`. Git therefore uses its default rule: after the second `@@` it prints the nearest earlier line that starts with a letter, `_` or `$`.
- **The likely headers.** By that rule they would read:
  - `@@ -60,0 +61 @@ in the README. That branch is never merged.` (from `docs/roadmap.md:37`);
  - `@@ -121,2 +122,5 @@ ADR.` and `@@ -124,0 +129,6 @@ ADR.` (from `docs/roadmap.md:111`);
  - `@@ -196 +206 @@ Tracked deliberately rather than hidden. …` (from `docs/roadmap.md:158`).
- **Consequence.** A tester that compares whole lines would report a mismatch on correct output.
- **The arithmetic is right.** The line offsets are +1 after (a), +3 after (c) and +6 after (d).
- **How to settle it.** Run `git diff --unified=0 HEAD -- docs/roadmap.md` once (a)-(d) are applied.

### The four areas the caller asked about

**1. Mutation tables (plan:1008-1044): correct.**

I traced all 25 rows against the named tests and the code. Every expected red follows from its mutation, and every listed green holds. Every red fails on the test's own Shouldly assertion:

- **No exception on success.** `Result.Error` returns `Error.None` on success instead of throwing (`Result.cs:7`, `:33`), and `Error` is a record (`Error.cs:3`). So under MU3 and MU4, U-D2, U-D3 and U-D4 fail on `ShouldBe`, even without an `IsFailure` line.
- **Cases the domain answers.** These cases pass the helper's status assertion and fail on `title` ("Bad Request", `ResultExtensions.cs:172-187`, `:199`):
  - U-A1 and U-A2 under MU1 and MU2;
  - P-A1, P-A3, P-A4 and P-A5 under MP1-MP4.
- **Cases that are created.** P-A2 under MP1 and MP15 fails on its status assertion, because the response is 201.
- **MU7 and MP15.** U-A1 and P-A1 fail on the status assertion, whatever status the run observes. That fits D5.
- **MP20.** P-VC1's M4 row and P-A6 go red.
- **Filters.** F-U and F-P select the intended classes. Their trailing dots exclude `ProductsDatasourceTests`.
- **Run count.** 27 runs (10 + 17) is correct.

**2. First use of the mutation scripts in dotnet mode and with two files (plan:73-77, :930-989): consistent with the scripts.**

- `mutate-run.sh:56-64` builds `dotnet test [<project>] --filter "<filter>"`. Its argument order (`:7`) matches the MU8 example at plan:986.
- The log is opened for appending (`:83`). The second call of a two-file row therefore adds the second diff and the second run to the same `<ID>.txt`.
- `:75` requires the target file to be tracked, and all five targets are.
- `mutate-clean.sh` accepts several files (`:54-56`, `:113`, `:115`).
- `coder-bash-guard.js:9` and `:123-134` inspect only the words `git` and `gh`. No filter or path in the plan contains `git` as a word, so F-U and F-P pass the coder's guard despite their `|`.
- `.claude/settings.json:50-51` allows both scripts.
- `TestResults/` is gitignored (`.gitignore:31`), so the logs leave the tree clean for `gate.sh`.
- **Still UNVERIFIED.** It is not known how dotnet mode and the two-file clean behave when run (`2026-10-19_chore-agents-workflow-skills-code-review-phase3.md:187-192`). The first S1 rows will show it: MU1, then MU7.

**3. Grep checks: apart from F1 and Phase 3's unscoped `\r` check, each one runs and has a single expected result.**

I ran each of these:

- `"(Product|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]` has no match in `TurkishErrorMessages.cs`. The same pattern with `Auth` matches `:16-19`, `:21` and `:23`, which shows the pattern works.
- `[^\x00-\x7F]` has no match in `UnitOfMeasuresApiTests.cs` or in `ProductsApiTests.cs`.
- `\.WithMessage\(` has no match in `src/Envanex.Application`.
- `\r` works as a pattern. It matches the CRLF files under `tests/Envanex.Domain.Tests/obj` and has no match in `src/Envanex.Web/Extensions`.
- `Product.ListPriceAmountNegative` has exactly two matches in `src/Envanex.Web/Extensions`: `TurkishErrorMessages.cs:40` and `ResultExtensions.cs:60`.
- The Phase 4 strings contain no regex metacharacter that changes their meaning. Each expected count holds for the planned text.

**4. D3 source form (plan:209-223): precise enough, because it yields exactly one source string per message.**

- I transcribed all four texts. They use only ı, ş, ö, ü and ç, with the code points the plan gives.
- No capital non-ASCII letter occurs.
- `\u` always takes exactly four hex digits, so a following letter cannot be misread.
- The existing escapes use lower case: `\\u[0-9a-f]{0,3}[A-F]` has no match in `TurkishErrorMessages.cs` today.
- **Two weaknesses, not blocking:**
  - plan:211-212 prints the code points in upper case, in the same paragraph that requires lower case (plan:213-214).
  - Nothing checks the hex case. The tests compare compiled strings, and the table entry and the test literal come from the same transcription. A transcription mistake made in both places would therefore pass.
- **Optional improvement.** Give the four escaped strings verbatim, and add a Grep for `\\u[0-9a-f]{0,3}[A-F]` (expected: no match) on `TurkishErrorMessages.cs`, `UnitOfMeasuresApiTests.cs` and `ProductsApiTests.cs`.

### Decisions D1-D5 and the correction

The plan implements D1-D5 and the human's correction completely.

- **The correction is accurate.** `docs/adr/0006-rest-error-mapping.md:41-45` requires `.WithErrorCode(...)`, forbids `.WithMessage(...)`, and says nothing about string literals.
- **Change (c)'s EF Core 10 statement checks out on Microsoft Learn.** "What's New in EF Core 10" says EF 10 "adds support for optional types", and that optional complex types "currently require at least one required property".

Two points are for the human only. They are not required changes.

- **D4 and CLAUDE.md.** CLAUDE.md requires an ADR for "every PR that makes an architectural decision". Change (d) (plan:877-888) binds PR 8 and PR 9 to a rule. There is a precedent for this in `docs/roadmap.md:108-111`, which records such rules under "Decisions carried forward".
- **Change (c) and ADR 0003.** Change (c) ends with "See ADR 0003" (plan:874), but `docs/adr/0003-ef-core-mapping-conventions.md:78` still reads "Complex types cannot be null." The plan keeps ADR 0003 unedited (plan:150-151), so a reader who follows the pointer finds the opposite statement.

### Checklist results that hold

I verified each of these by reading the code.

- **Facts.** The file:line facts at plan:31-92 match HEAD, except for F4.
- **Names and signatures.** Namespaces, type names and method signatures match.
- **Dependency rule.** The rule holds. Application references only Domain constants and errors, and no file under `src/Envanex.Infrastructure/` changes.
- **Existing patterns.**
  - `Result` carries the expected failures.
  - `ThrowIfNull` stays first.
  - Every check runs before any assignment.
  - Setters stay private.
- **Buildability.** Each phase builds and tests on its own state.
- **Files lists.** They are complete. The only dependency between phases is the helper described in F3.
- **Branch coverage.** Every branch the signatures require has a named test:
  - `UnitOfMeasure.Create` checks 1-4, and the base-unit branch;
  - `Product.Create` and `Product.Update` checks 1-3;
  - `UpdatePrice` checks 1-2;
  - every new validator rule;
  - both undecorated handlers.
- **State written by other code paths.** No test can pass on it:
  - each test reads in a fresh context, or by the id the write returned;
  - `ResetAsync` runs for every test case;
  - the fakes are created per test instance;
  - P-A7 starts from the seeded 100/10.
- **User-secrets.** They cannot reach the test hosts. The host runs as "Testing" (`EnvanexWebApplicationFactory.cs:43`, `:91`), and the plan adds no Development host.
- **Validation commands.**
  - They use the VSTest filter syntax. The repo runs xunit 2.9.3 with xunit.runner.visualstudio 3.1.4 (`Directory.Packages.props:46-47`).
  - The targeted test commands pass the tester's allowlist (`bash-allowlist.js:113-144`).
- **Count deltas.** They add up: +14/+15/+12, +21/+6/+6 and +22/+7. That makes 25 new integration tests.
- **EF and db-reviewer.**
  - No EF configuration changes.
  - Leaving out db-reviewer is consistent with `.claude/agents/db-reviewer.md:3` and `.claude/skills/pr/SKILL.md:61-63`.

### Non-blocking notes

- **Undefined labels.** The plan uses T3, T4, T7 and R1 without defining them (plan:61, :133, :405, :506, :512-513, :635, :1025, :1065). "R1" at plan:1025 has no referent at all. The MP IDs also jump from MP15 to MP20.
- **P-VC4/P-VU4 (plan:773-774).** They list rows but have no "Asserts" line. The intended assertion is presumably `IsValid` true, as in P-V*1.
- **The mutation tables' "Observed" column (plan:968).** No one is named to fill it in.
  - `coder.md:23` forbids the coder to touch files outside the plan's Files lists.
  - The PR 6b plan recorded its observed results in a plan revision (`2026-09-20_pr6b-authorization-and-blazor-auth.md:1550-1570`).
- **Status mapping of the four new codes (D3).** No test without a mutation reaches the codes' 400 status. Through the API these fields always fail in the validator first, and that response is always a 400 (`ResultExtensions.cs:159-169`). A wrong map value such as 422 would therefore stay green. The existing domain codes have the same gap. `ResultExtensionsTests.cs:172-185` shows a test pattern if one is wanted.
- **UNVERIFIED claims.** A build or a test run settles each one:
  - **`UpdatePrice` overload binding.** Once `UpdatePrice` returns `Result`, `Should.Throw<ArgumentNullException>(() => product.UpdatePrice(null!))` must still bind to an overload. The precedent is `ProductTests.cs:221`. `dotnet build -warnaserror` settles it.
  - **CA1861.** It must stay silent on `ShouldBe([...])` under `latest-recommended`. The precedent is `ArchitectureTests.cs:87`. The same build settles it.
  - **xUnit theory rows.** xUnit should list each `TheoryData<decimal>` row as its own test and keep the scale of `0.0000010m` and `1.500000000m`. Settle it with `dotnet test tests/Envanex.Domain.Tests --list-tests --filter FullyQualifiedName~Envanex.Domain.Tests.Aggregates.UnitOfMeasures.UnitOfMeasureTests`. plan:203-204 already says to record the totals either way.
  - **SqlClient.** It should accept `1.500000000m` for a `decimal(18,6)` parameter. The stop condition at plan:489-490 covers this case.

## Required changes

1. **Escape the four `WithErrorCode(...)` Grep patterns (F1)** at plan:475-477 and plan:836-837. For example, write `WithErrorCode\(UnitOfMeasureErrors\.ConversionFactorTooPrecise\.Code\)`. Keep the expected result of one match each per file. At plan:841, name the files the `\r` check covers: each of Phase 3's five Files.
2. **Separate the coder's and the tester's `git status --short` expectations in Phases 1-3 (F2)**, at plan:461/:470-471, plan:694/:702-703 and plan:824/:832, the way Phase 4 does:
   - mark the command and its file list as the coder's check, run before the human commits;
   - state the tester's expected result: empty output, with the changed files read from the `files` section of `gates.txt`.
3. **Correct plan:1064 (F3).** Phase 1 cannot be reverted alone while Phase 2 stays, because P-S1 and P-S2 call Phase 1's `ColumnMaximum`.
4. **Correct plan:58-60 (F4).** A new `Error` field turns two facts red, not three. Name the two facts.
5. **Make the Phase 4 hunk checks unambiguous (F5)** at plan:911-917 and plan:928. State that each hunk header line *begins with* the given `@@ … @@` text, and that git may append a section heading after it.
