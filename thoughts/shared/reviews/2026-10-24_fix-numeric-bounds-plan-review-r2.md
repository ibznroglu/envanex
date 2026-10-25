Verdict: APPROVED
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md
Range: 56fd6d41bcdb7d1b440bb1f161b08709debc5d0a..1acaf4234b7c7c356fbabdd43124b4c0811328b6
Head: 1acaf4234b7c7c356fbabdd43124b4c0811328b6

## Findings

`plan:N` refers to line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md` at `1acaf42`. I read the revised file in full and compared each changed passage with the relayed diff. They match; I found no difference. The Grep results below are from the Grep tool, run in this review. Claims that need a build, a test run or git are marked UNVERIFIED.

### r1 findings: all five resolved

- **F1 (Grep patterns): resolved.**
  - The four `WithErrorCode` patterns are now escaped (plan:526-528, plan:915-916).
  - Phase 3's `\r` check now names its five files (plan:920).
  - The literal-code patterns are escaped too (plan:783, :792, :917).
  - Run today:
    - `"Product\.ListPriceAmountNegative"|"Product\.ReorderPointNegative"` matches once per code and validator file: `CreateProductCommandValidator.cs:33`, `:45` and `UpdateProductCommandValidator.cs:29`, `:41`.
    - `Product\.ListPriceAmountNegative` in `src/Envanex.Web/Extensions` matches exactly twice: `TurkishErrorMessages.cs:40` and `ResultExtensions.cs:60`.
    - The escaped `WithErrorCode\(UnitOfMeasureErrors\...\.Code\)` form has no match yet, because that code does not exist until Phase 1. It is escaped the same way as the r1 control that found `CreateUnitOfMeasureCommandValidator.cs:13`.
- **F2 (`git status --short`): resolved** (plan:518-522, :776-779, :908-911).
  - Each phase now names the coder's pre-commit list.
  - It states that the tester's run prints nothing on its clean tree, as `tester.md:50-53` and `gate.sh:204` require.
- **F3 (rollback note): resolved** (plan:1162-1164).
  - The note also names the second dependency the revision adds: Phase 2 appends rows to Phase 1's two theories.
- **F4 (fact count): resolved** (plan:58-62).
  - `:67` and `:81` are the two method lines in `ResultMappingTests.cs`.
- **F5 (hunk headers): resolved** (plan:993-999, :1011-1013).
  - The headers are now required to "begin with" the given text.
  - The arithmetic is right for the six-line (c):
    - (c) is `-121,2 +122,6`.
    - For (d), old line 124 becomes new line 129, the inserted lines are 130-135, and the header is `-124,0 +130,6`.
    - For (b), 196 + 1 + 4 + 6 = 207.
  - The "scratch diff" heading quoted at plan:994-995 matches git's default funcname rule, which picks `docs/roadmap.md:37`. It is UNVERIFIED by me, and "begins with" holds either way.

The r1 non-blocking notes are also answered:
- Grepping `\b(T[0-9]+|R[0-9]+)\b` now finds only U-T1 and P-T1, which are defined.
- The code points are given in lower case (plan:216-217).
- The Observed column has a named writer (plan:1054-1066).
- The 400 mapping is tested (U-M1, P-M1).
- Change (c) now states how ADR 0003's claim stands under EF Core 10 (plan:946-956).

### Changed passages and their effects

- **U-T1 and P-T1 (plan:460-472, :745-749): consistent with `TurkishErrorMessagesTests.cs`.**
  - Its class comment (`:7-11`) says "No database and no collection" and explains that it uses literal Turkish characters on purpose.
  - `Fallback` is at `:14`, and the class takes its code from `AuthErrors.InvalidCredentials.Code` (`:22`).
  - P-T1's texts equal the message table (plan:228-229) and the decoded `TurkishErrorMessages.cs:40`.
  - This closes the r1 D3 gap: a transcription error made identically in a table entry and an API-test literal now turns U-T1 or P-T1 red.
  - The exception to the D3 escape rule is explicit (plan:220-222). No Grep check touches this file's literal letters, because the ASCII checks cover only `UnitOfMeasuresApiTests.cs` (plan:535-536) and `ProductsApiTests.cs` (plan:921-922).
- **U-M1 and P-M1 (plan:474-488, :750-752): consistent with `ResultExtensionsTests.cs`.**
  - They follow the existing shape: `AuthErrorCodes` (`:16-32`), the assertions of `UnmappedErrorCode_ShouldFallBackTo400NotServerError` (`:34-53`), and the same failure construction (`:176`).
  - The reasoning at plan:474-476 is right. An unmapped code also falls back to 400 (`ResultExtensions.cs:174`), so U-M1 alone cannot tell "mapped to 400" from "missing".
  - Together with `ResultMappingTests.cs:67-78`, which proves the code is mapped, the pair pins "mapped, and to 400".
  - `BuildProblemDetails` creates the exact type `ProblemDetails` (`ResultExtensions.cs:176`), so a `ShouldBeOfType<ProblemDetails>()` assertion holds.
- **Files lists.**
  - Phase 1 now has thirteen files (plan:239-260): twelve modified and one created.
  - Phase 2 has fourteen (plan:550-570).
  - Phase 3 is unchanged at five.
  - Each count matches that phase's `git status --short` expectation.
- **Counts.**
  - Phase 1 adds 2 + 1 + 3 + 3 + 3 + 2 + 2 = 16 integration tests (U-P1, U-P2, U-S1..3, U-A1..3, U-A4, U-T1, U-M1).
  - Phase 2 adds 2 + 2 + 2 + 3 + 3 = 12 (P-P1, P-P2, P-S1..2, P-T1, P-M1). Phase 3 adds 7.
  - That totals 35, as plan:979 says. The Domain and Application deltas are unchanged.
- **Validation commands.** The new targeted runs (plan:505-506, :764-765) use single-token filters, which `bash-allowlist.js:113-144` permits. Each selects only its own class.
- **Phase 4, change (c) (plan:951-956).**
  - None of the six new lines equals old line 121 or 122, so git shows a single replace hunk.
  - The Grep expectations at plan:1006-1009 hold:
    - Today only `Complex types cannot be null in EF Core` matches (`roadmap.md:121`).
    - The new wording, "complex types cannot be null does not hold for EF Core 10", does not match it: the case and the words after "null" differ.
    - `ef-core-10\.0/whatsnew` and the three other strings have no match today, and (a)-(d) add exactly one each.
- **Write scope for the Observed cells (plan:1054-1066): permitted.**
  - `coder.md:4` gives the coder Edit, and `coder.md:7-12` attaches only a Bash hook.
  - `path-guard.js:7-8` and `:20-32` block only `bin`, `obj`, `packages`, `.vs`, `.idea`, `.git` and secret file names.
  - The format hook runs only on `.cs` and `.csproj` files (`settings.json:171`).
  - `docs/ai-workflow.md:238-239` reads: "The coder has no write scope: it writes the files its plan names, within path-guard's limits." The plan's listing satisfies `coder.md:23`.
  - Filling the cells only after every row's final clean check keeps the restore proof intact, because `mutate-clean.sh` diffs only the target files (`:113`, `:115`).
- **Mutation tables: unaffected.** The F-U and F-P filters (plan:1071-1072) select neither `TurkishErrorMessagesTests` nor `ResultExtensionsTests`.

### Non-blocking notes

- **N1 — Phases 2 and 3 have no allowance for uncommitted files.**
  - Phase 2 (plan:776-777) and Phase 3 (plan:908-909) expect `git status --short` to list "exactly" their Files.
  - Phase 1 (plan:520) and Phase 4 (plan:992) allow uncommitted plan or review files; Phases 2 and 3 do not.
  - The plan now creates such a file itself: the S1 Observed update. The human commits it "as its own docs commit" (plan:1065-1066), at a time the plan does not state.
  - If that update is still uncommitted when the Phase 2 coder runs the check, the expectation is false.
  - Either fix works: commit the update before Phase 2 starts, or copy Phase 1's allowance.
- **N2 — "The phase's changed files" is imprecise (plan:522, :779, :911).**
  - The `files` section is `git diff --name-only main...HEAD` (`gate.sh:206`), the files changed on the whole branch.
  - It includes earlier phases, the research, plan and review files, and the Observed docs commits, so it is a superset of the phase's Files.
  - No expectation depends on it; the wording is the only issue.
- **N3 — The Observed cell format is unspecified.** The plan says only "from the logs only" (plan:1054). A suggested minimum for each cell:
  - the red tests with their row counts, or "as expected";
  - any deviation from the expected column;
  - the log file name;
  - for MU7 and MP15, the observed status and exception, which their rows already require.
- **N4 — Wording at plan:994.** It calls the text after the second `@@` "a context line". It is the hunk header's section heading. The example on the same line makes the meaning clear.
- **N5 — Carried from r1.** P-VC4/P-VU4 (plan:849-850) still have no "Asserts" line. The test name says ShouldPass, which means `IsValid` is true.
- **N6 — Optional.** U-T1, U-M1, P-T1 and P-M1 have no mutation row. Two cheap proofs would be: map `UnitOfMeasure.ConversionFactorTooPrecise` to 422, which should turn U-M1 red; and change one escape in a new table entry, which should turn U-T1 red. Whether to add them is the human's call.
- **UNVERIFIED — the build accepting the new members.**
  - CA1062 should stay quiet on the new public theory methods. Their string parameters are passed on, not dereferenced, as in `ResultExtensionsTests.cs:174-176`.
  - The new `using` lines must follow `dotnet_sort_system_directives_first`. The PostToolUse format hook corrects the order after each Edit.
  - `dotnet build -warnaserror` and `dotnet format --verify-no-changes` settle both.
- **UNVERIFIED — row counts.** That xUnit runs each `TheoryData<string>` or `TheoryData<string, string>` row as its own test case is UNVERIFIED. Strings are serializable, so it should. `dotnet test tests/Envanex.IntegrationTests --list-tests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.TurkishErrorMessagesTests` settles it.

## Required changes

None.
