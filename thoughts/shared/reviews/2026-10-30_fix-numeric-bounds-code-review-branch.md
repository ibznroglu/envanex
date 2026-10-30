Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md whole branch
Range: 56fd6d41bcdb7d1b440bb1f161b08709debc5d0a..ecad1613663fe6f69c9a1dcbfef70907090a3f97
Head: ecad1613663fe6f69c9a1dcbfef70907090a3f97

## Findings

How this review was done:

- `plan:N` is line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md`. `journal:N` is line N of `docs/journal/0011-numeric-bounds.md`.
- What I read, with the Read tool:
  - the plan in full, including the filled Observed cells (plan:1108-1142);
  - the diff file `TestResults/fix-numeric-bounds/branch-review-ecad161.diff`, lines 1-1917;
  - at the head, in full: all nine changed `src/` files, `TurkishErrorMessagesTests.cs`, `ResultMappingTests.cs`, the journal, and the four phase code reviews;
  - the neighbours: `Money.cs`, `Quantity.cs`, `Result.cs`, `DependencyInjection.cs`, `UpdateProductCommandHandler.cs`, `CreateUnitOfMeasureCommandHandler.cs`, `Directory.Packages.props`, `Directory.Build.props`, `gate.sh`, `mutate-run.sh` and `mutate-clean.sh`;
  - the tester's four gate records and the MU7 and MP15 logs under `TestResults/fix-numeric-bounds/` (gitignored, `.gitignore:31`).
- Where the evidence comes from:
  - character, placement and line-ending claims: the Grep tool, run in this review;
  - the EF Core 10 claim: the Microsoft Learn tools, run in this review;
  - the research and the plan reviews: Grep for the passages cited.
- How backslashes are written:
  - This file is ASCII-only and contains no backslash, on purpose. A non-ASCII letter is written `[U+XXXX]`.
  - "Escaped form" means a backslash, the letter u, and four hexadecimal digits.
  - In every Grep pattern I ran, a backslash in the target text was written as the hexadecimal escape for code point 5C. So no backslash-u sequence passed through a tool parameter.
- The first run of this review ended on an HTTP 429 before any file was written. On resuming, I re-ran every check that a finding below relies on, against the working tree. The caller reports that tree at `ecad161` and clean.
- I have no shell. Every build, test, git and hash claim below is the caller's or the tester's record. It is UNVERIFIED by me unless I say how I checked it. I did not check the diff file's sha256.

The code, the tests and the roadmap have no defect. The journal has two factual errors (F7). They are the only required changes.

### F1 - the diff against the head - info, no difference

- **The stat** (diff lines 1-29) lists 28 files: 9 under `src/`, 18 under `tests/`, and `docs/roadmap.md`.
  - None is under `src/Envanex.Infrastructure/` or `db/`, and no migration or EF configuration changed. So db-reviewer is not required (plan:159-176).
  - The 15 deletions are exactly the planned replacements: `void UpdatePrice` (1), the two moved table lines (2), the bare `UpdatePrice` call in `ProductTests` (1), the eight integration-test `UpdatePrice` calls (8), and three replaced roadmap lines, two for (c) and one for (b) (3).
- **The hunks match the files.** Every `src/` hunk matches its file as read at the head. For the test files I compared the lines this review cites.
- **Nothing unplanned was implemented.** Beyond the plan's wording there are only:
  - `IsFailure` assertions before each `Error` assertion, in the classes' existing style;
  - the one-line comment in each `ReadFieldValidationMessagesAsync`.

### F2 - the seams between phases (caller's point 1) - info, consistent

- **Phase 2 reuses Phase 1's `ColumnMaximum`.**
  - `SchemaPrecisionTests.cs:197-200` defines it over `PowerOfTen` (`:185-195`), in `decimal` only.
  - It is called by U-S2 (`:128`), P-S1 (`:164`) and P-S2 (`:182`).
  - Both domain namespaces are imported at `:1-2`. `ColumnInfo` (`:204-208`) has `byte Precision` and `int Scale`, as plan:75 states.
- **Phase 2 adds its rows to Phase 1's two theories.**
  - `ResultExtensionsTests.cs:36-43` (`NumericFieldErrorCodes`): two UnitOfMeasure rows (`:38-39`), then three Product rows (`:40-42`).
  - `TurkishErrorMessagesTests.cs:21-28` (`NumericFieldMessages`): two UnitOfMeasure rows (`:23-24`), then three Product rows (`:25-27`).
  - Every code is taken from its declared `Error` field. Both files import both domain namespaces.
- **Phase 3's validator rules use Phase 2's constants and error codes.** Each rule is `LessThanOrEqualTo(Product.<Field>MaxValue)` with `WithErrorCode(ProductErrors.<Field>TooLarge.Code)`:
  - `CreateProductCommandValidator.cs:35-37` and `:51-53`;
  - `UpdateProductCommandValidator.cs:31-33` and `:47-49`.
- **The move of `Product.ListPriceAmountNegative`.**
  - `TurkishErrorMessages.cs:31` lies between the "Product - domain" comment (`:25`) and the "Product - validator" comment (`:40`).
  - `ResultExtensions.cs:56` lies between `:50` and `:60`.
  - Neither validator group lists it any more (`TurkishErrorMessages.cs:41-44`, `ResultExtensions.cs:61-64`).
  - The validators keep the literal code: `CreateProductCommandValidator.cs:33` and `UpdateProductCommandValidator.cs:29`. The `ReorderPointNegative` literals at `:49` and `:45` are unchanged too.
- **No leftover.**
  - Outside `thoughts/`, `TestResults/` and `tests/`, the code appears only in four kinds of place: the three returns in `Product.cs` (`:70`, `:96`, `:140`), the two validator literals, the two table lines, and `ProductErrors.cs:19`. The journal mentions it too.
  - No comment, doc or ADR still calls it a validator-only code.
- **The two `ReadFieldValidationMessagesAsync` helpers are identical.** They are at `UnitOfMeasuresApiTests.cs:63-74` and `ProductsApiTests.cs:74-85`, comment included. Each asserts the status before it reads the body. Phase 3 copied Phase 1's helper, as plan:831-832 requires.
- **Phase 4's rule describes what Phases 1-3 built.** The rule is (d), at `docs/roadmap.md:130-135`. The fields it covers are the only persisted `decimal` fields in `src/Envanex.Domain/Aggregates`:
  - `Product.ListPrice`;
  - `Product.ReorderPoint`;
  - `UnitOfMeasure.ConversionFactor`.

  `Warehouse` has none.

### F3 - the D3 source form at the head (caller's point 2) - info, no defect

- **`src/Envanex.Web/Extensions/TurkishErrorMessages.cs:31`, `:32`, `:33`, `:52` and `:53`** are all in the escaped form, with lower-case hexadecimal digits.
  - Each line matched one anchored full-line Grep, from the 8-space indent to the trailing comma.
  - The letters, in order:
    - `:31` and `:32`: one [U+0131] each;
    - `:33`: [U+015F], then [U+0131];
    - `:52`: eleven letters;
    - `:53`: six letters.
- **File-wide checks on the same file.**
  - The plan's pattern (plan:533, plan:790) has no match.
  - Non-ASCII bytes occur only on the pre-existing Auth lines 16-19, 21 and 23.
  - There is no upper-case hexadecimal digit in an escape, and no backslash-U or backslash-x escape.
- **The API test literals are all in the escaped form.**
  - `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs`:
    - `:424` (P-A1), `:436` (P-A2) and `:465` (P-A4): the ListPriceAmountTooLarge text;
    - `:448` (P-A3) and `:482` (P-A5): the ReorderPointTooLarge text.
  - `tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs`:
    - `:195` (U-A1): the TooPrecise text;
    - `:207` (U-A2): the TooLarge text;
    - `:221-222` (U-A3): both texts.
  - Neither file has a non-ASCII byte, an upper-case hexadecimal digit in an escape, or a backslash-U or backslash-x escape.
- **The one deliberate exception.** `TurkishErrorMessagesTests.cs:23-27` write the expected texts with literal letters, matched by code point: [U+00F6], [U+00FC], [U+015F], [U+0131] and [U+00E7]. The file contains no backslash at all, as its class comment (`:9-13`) intends.
- **So the chain is closed.** The plan's table (plan:226-229) equals the literal test rows, which equal the decoded table entries, which equal the API literals.
- That these tests pass is the tester's record: `gates-4-tester-751053c-1.txt` shows 155, 169 and 427 passed and `gate-exit=0 failed=`. ecad161 adds only the journal.

### F4 - production code at the head - info, no defect

- **`Product.cs`.**
  - `Create`: checks at `:68`, `:73` and `:78`, before the normalization (`:83-84`) and the constructor call (`:86`).
  - `UpdatePrice`: `ThrowIfNull` (`:92`), the checks at `:94` and `:99`, the assignment (`:104`), then `Result.Success()` (`:106`).
  - `Update`: checks at `:138`, `:143` and `:148`, before all four assignments (`:153-156`).
  - The sign check is the numeric `< 0m`. `src/` has no `IsNegative`, no `GetBits` and no `.WithMessage(`.
- **`UnitOfMeasure.cs`.**
  - The base branch is unchanged (`:62`).
  - The derived branch checks in D2's order: `:69` InvalidBaseUnitId, `:74` MustBePositive, `:79` TooPrecise, `:84` TooLarge.
  - The factor reaches the constructor as typed (`:90`).
- **Null versus invalid.**
  - `Money` is a sealed record (`Money.cs:5`), guarded by `ThrowIfNull` on all three paths (`Product.cs:41`, `:92`, `:121`).
  - `Quantity` is a readonly record struct (`Quantity.cs:5`), so `reorderPoint.Value` cannot throw a null reference.
- **The plan's table rows hold.** `Money.Of` and `Quantity.Of` round with `MidpointRounding.ToEven` (`Money.cs:21`, `Quantity.cs:23`).
  - At `...99995` and `...9999995` the kept digit 9 is odd, so they round up to `100000000000000.0000` and `1000000000000.000000`. The domain rejects both.
  - `...99994` and `...9999994` round down to M4 and M6, which the domain accepts.
  - `Quantity.Of` rejects a raw negative before it rounds (`Quantity.cs:18`). So the accepted range `[-0.00005, 0)` exists for the list price only, as plan:138-139 says.
- **The validator is never less strict than the domain.**
  - Rounding to 4 or 6 places is monotonic, and M4 and M6 are representable at those scales. So a raw value in `[0, M]` rounds into `[0, M]`, and every product rule is at least as strict as its domain check.
  - The three factor rules equal the factor's domain checks.
  - So a domain code for these fields reaches a caller only on an undecorated path: `DependencyInjection.cs:36-43` registers the concrete handlers.
- **The handlers check before they write.** Each returns the aggregate's failure before `AddAsync`, `SetOriginalRowVersion` or `SaveChangesAsync`:
  - `UpdateProductCommandHandler.cs:64-70`;
  - `CreateUnitOfMeasureCommandHandler.cs:45-58`;
  - for `CreateProductCommandHandler.cs`, the Phase 2 review checked the same (its F7).
- **Other checks.**
  - `UpdatePrice` has no caller in `src/`.
  - The dependency rule holds: the only new references are to Domain types, and no Infrastructure type appears.
  - There is no new production async code.

### F5 - tests at the head - info, no defect

- **Every planned test exists, with the plan's names, rows and assertions.** I read each in the diff:
  - U-D1..U-D7, U-V1..U-V6, U-H1..U-H2, U-P1..U-P2, U-S1..U-S3, U-A1..U-A4, U-T1 and U-M1;
  - P-D1..P-D21, P-HC1..P-HC3, P-HU1..P-HU3, P-P1..P-P2, P-S1..P-S2, P-T1 and P-M1;
  - P-VC1..P-VC5, P-VU1..P-VU5 and P-A1..P-A7.
- **No test asserts a decimal's sign bit or scale.** `IsNegative` and `GetBits` do not occur in `tests/`. The `Scale` assertions in `SchemaPrecisionTests.cs` (`:35`, `:54`, `:110`) read the column's scale from `INFORMATION_SCHEMA`.
- **The counts match the plan.** These are the tester's gate records, read directly; each ends with `gate-exit=0 failed=`. Totals are Domain/Application/Integration:

  | Phase | Commit | Totals | Delta |
  |---|---|---|---|
  | 1 | `624fe5f` | 134 / 141 / 408 | +14 / +15 / +16 |
  | 2 | `8c34f8f` | 155 / 147 / 420 | +21 / +6 / +12 |
  | 3 | `316f2ba` | 155 / 169 / 427 | 0 / +22 / +7 |
  | 4 | `751053c` | 155 / 169 / 427 | 0 / 0 / 0 |

  - The deltas equal plan:490-491, plan:754 and plan:890.
- **The mutation logs.** 25 logs exist, one per row: MU1-MU9, MP1-MP15 and MP20. I spot-checked two against the plan's Observed cells:
  - `MU7.txt:248-254`: U-A1 expected `BadRequest` and observed `Created`;
  - `MP15.txt:126-165` and `:301-340`: P-A2 observed `Created` and P-A1 observed `InternalServerError`, in both runs.

  Both match their Observed cells and the journal.

### F6 - the roadmap entries Phase 4 changed (caller's point 3) - info, no defect

- **The text.** (a) `docs/roadmap.md:61`, (c) `:122-127`, (d) `:130-135` and (b) `:207` carry the plan's text (plan:943, :951-956, :964-969, :976).
  - The Phase 4 review matched each line with an anchored pattern. I re-confirmed the markers, and that (b) is one two-cell row ending in the planned "when" cell.
  - The two removed texts have no match.
- **(c)'s EF Core claim, re-checked on Microsoft Learn.**
  - "What's New in EF Core 10" says "EF 10 adds support for **optional** types" and "optional complex types currently require at least one required property to be defined on the complex type".
  - The Complex Types modeling page says the same.
- **(d)** matches the code, as F2 and F4 show.
- **(b) records only the 2026-10-23 measurement.**
  - Its trigger has fired: Phase 3's gate shows `1 m 6 s` (`gates-3-tester-316f2ba-1.txt:336`).
  - The journal records the 2026-10-29 re-measurement (journal:366-370).
  - The Phase 4 review (its F6) records the caller's instruction: that follow-up goes into the journal and the PR text, not the row. So the row's omission is a decision, not a defect. See N2.

### F7 - the journal's factual statements (caller's point 3) - two errors

I checked every factual statement outside the opinion section that starts at journal:381. Per the caller, that section is not a defect. Two statements are wrong.

- **F7.1 - `docs/journal/0011-numeric-bounds.md:326` - low.**
  - The sentence says that U-D1, U-V1 and U-A4 have the `0.0000010m` and `1.500000000m` rows.
  - `0.0000010m` exists only in U-D1 (`UnitOfMeasureTests.cs:11`) and U-V1 (`CreateUnitOfMeasureCommandValidatorTests.cs:27`).
  - U-A4's rows (`UnitOfMeasuresApiTests.cs:35-40`) are `0.000001m`, `999999999999.999999m` and `1.500000000m`.
  - The wording follows the Phase 1 review's N1, which has the same ambiguity. The paragraph's conclusion is unaffected.
- **F7.2 - `docs/journal/0011-numeric-bounds.md:374-376` - low to medium.** Two statements about the plan's checks are wrong.
  - **The carriage-return check.** It says every phase carried a Grep for a carriage return in the files it changed.
    - That holds for Phases 1-3 only (plan:530, plan:787, plan:920).
    - Phase 4's line-ending checks are `git diff --check` and `git ls-files --eol` (plan:1002-1003).
  - **The BOM check.** It says the plan defined the `head -c 3 | od` check for newly created files and for the roadmap.
    - The plan defines it once, for `docs/roadmap.md` (plan:1004).
    - It defines no BOM check for the branch's only created source file, `tests/Envanex.IntegrationTests/Persistence/ConversionFactorPersistenceTests.cs`.
    - A `head -c 3` check on that file appears only as an UNVERIFIED suggestion in the Phase 1 review (its N2). The same holds for the awk- and perl-edited files in the Phase 2 review (its N4) and the Phase 3 review (its F2 and N3).
  - **Why it matters.** The paragraph tells the reader which guards the plan defined. As written, it credits the plan with a BOM check on a created C# file that no planned step ran.
  - **The rest of the paragraph is consistent.** Its later sentences agree with CLAUDE.md and `.editorconfig:3-6` (`charset = utf-8`, `end_of_line = lf`). That `dotnet format` flags a BOM is UNVERIFIED by me.

Every other factual statement I checked holds:

- **Packages and build.**
  - xUnit is 2.9.3, so v2 (`Directory.Packages.props:46`; journal:49). FluentValidation is 12.1.1 (`:13`; journal:68).
  - `TreatWarningsAsErrors` is set (`Directory.Build.props:7`; journal:248).
  - No package changed (journal:24).
- **The test-data claims.**
  - All 18 `TheoryData<decimal>` members are in the eight files journal:33-39 lists, and all come from this branch.
  - `[MemberData]` existed before, in `ResultExtensionsTests` and `ProductsDatasourceTests` (journal:40-41). `DependencyInjectionTests` also used it.
- **The code claims.**
  - `src/` has one `.Must(` (journal:64) and no cascade mode (journal:150).
  - `ValidationDecorator` and the undecorated handlers are as described (`DependencyInjection.cs:36-84`; journal:101-107).
  - `ResultMappingTests` scans the Domain and Application assemblies (`:20-24`, `:37-52`; journal:207-209).
  - P-T1 is the only test that pins the moved text (journal:358).
- **The tooling claims.**
  - The fixture's image is SQL Server 2022 (`SqlServerFixture.cs:42`; journal:250).
  - The descriptions of `gate.sh`, `mutate-run.sh` and `mutate-clean.sh` (journal:257-258, :260) match the scripts, including gate.sh's ten steps and its final line.
  - The roadmap starts with `# R`, which is `23 20 52` (journal:256).
- **The research attributions.** The research never mentions `PrecisionScale` (journal:67-68). It records SqlClient 6.1.6 against the v7.1 docs, and their contradiction with the EF Core 5 note (research:109, :145-155, :277; journal:304-307). It records the `-warnaserror` inference (research:237; journal:177-179).
- **The review and Observed-column claims.**
  - The Phase 1 review's N1 exists (journal:327-328), and the phase reviews' anchored comparisons exist (journal:350-351).
  - The journal's account of the Observed cells is accurate: MU3-MU6, MU8, MU9 and MP5-MP12, plus MU7 and MP15, which I also checked against the logs (journal:288-300, :313-319, :339-341).
- **The suite measurement.** Phases 1-3 add 35 integration tests (plan:979). The 2026-10-23 numbers are those of roadmap:207. Phase 3's gate shows `1 m 6 s`. The journal says that raw output is "not in the repository", and `TestResults/` is gitignored, so that holds (journal:362-370).
- **Two more details.**
  - journal:219 holds the six-character escape sequence, as intended, not a literal letter.
  - The EF Core 10 statement (journal:236-242) matches Microsoft Learn.

### F8 - the plan's decisions (caller's point 4) - info

D1-D5, including D4 (no ADR), are the human's and are not reopened here. The journal's disagreement with D4, in the opinion section, is an opinion.

### Non-blocking notes

- **N1 - carried, the human's call.**
  - P-HU1..P-HU3's `Products.Count.ShouldBe(1)` cannot go red (Phase 2 review, N1).
  - U-T1, U-M1, P-T1 and P-M1 have no mutation row (plan review r2, N6, and the phase reviews' N3).
  - Whether xUnit v2 keeps a theory row's decimal scale stays UNVERIFIED (Phase 1 review, N1). `dotnet test tests/Envanex.Domain.Tests --list-tests` settles it.
- **N2 - the PR text.** The 2026-10-29 re-measurement belongs in the PR text, per the Phase 4 review's F6. That is for `/pr`, not for this branch.
- **N3 - the re-review's file name.**
  - `.claude/skills/pr/SKILL.md:19-20` counts a review file without a `-r<N>` suffix as N = 1, and fails when two files of one kind share an N.
  - So the next branch review of this kind needs `-r2` or higher. The plan review's re-review used `-r2`.
- **N4 - UNVERIFIED runtime claims.** Each of these commands settles one:
  - `dotnet build -warnaserror`;
  - `dotnet test`;
  - `dotnet format --verify-no-changes`.

  The tester's record at `751053c` shows all three passing, and `ecad161` adds only the journal.

## Required changes

Only `docs/journal/0011-numeric-bounds.md` changes. `src/`, `tests/`, `docs/roadmap.md`, the plan and the reviews need no change. The journal is the human's study material, in Turkish, so the wording of each correction is the human's.

1. **journal:326.** Attribute the `0.0000010m` row to U-D1 and U-V1 only. U-A4's only trailing-zero row is `1.500000000m` (`UnitOfMeasuresApiTests.cs:35-40`). The sentence's conclusion stays: these rows are no evidence about trailing zeros, and U-P2 is the only persistence observation.
2. **journal:374-376.** Make the two statements match the plan:
   - The Grep for a carriage return is a check of Phases 1-3 (plan:530, plan:787, plan:920). Phase 4 checks line endings with `git diff --check` and `git ls-files --eol` (plan:1002-1003).
   - The plan's only BOM check is `head -c 3 docs/roadmap.md | od -An -tx1` (plan:1004). Remove the claim that the plan defined one for newly created files. If the paragraph names a guard for the created test file, name only what applied:
     - the gate's `dotnet format --verify-no-changes`, under `.editorconfig:3-6`. Whether it flags a BOM is UNVERIFIED.
     - the code reviews' `head -c 3` commands, which were UNVERIFIED suggestions, not plan checks.

A re-review settles it. That review must confirm that the new head changes only `docs/journal/0011-numeric-bounds.md` against `ecad161`, and must re-check the two passages.
