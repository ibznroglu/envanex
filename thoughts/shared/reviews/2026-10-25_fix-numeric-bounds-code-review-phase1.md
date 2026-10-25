Verdict: APPROVED
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md phase 1
Range: 5edd8d27fb5be377dbb45543eae37a808fa3d72d..5d1c77c234fa18bc1872ad54563960895273ed8a
Head: 5d1c77c234fa18bc1872ad54563960895273ed8a

## Findings

How this review was done:

- `plan:N` is line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md`.
- What I read, with the Read tool:
  - Phase 1 (plan:233-543), the S1 mutation rows (plan:1099-1116) and the approved plan review;
  - all thirteen changed files, in full;
  - the neighbouring files the plan cites.
- Where the evidence comes from:
  - character and digit claims: the Grep tool, run in this review;
  - symbol resolution: LSP `goToDefinition`, run in this review.
- How backslashes are written:
  - In every Grep pattern a backslash is written as `\x5c`, so no backslash-u sequence passed through a tool parameter.
  - This file is ASCII-only on purpose. A non-ASCII letter is written `[U+XXXX]`.
  - "Escaped form" means a backslash, the letter u, and four hexadecimal digits.
- I have no shell. Every build, test, count and git claim below is the coder's or the caller's, and is UNVERIFIED.

No blocking finding.

### F1 - relayed diff against the files - info

- **No difference found.** Every hunk of the relayed diff matches the file as read at `5d1c77c`. This includes the escaped forms in `TurkishErrorMessages.cs` and `UnitOfMeasuresApiTests.cs`; the relay kept them as escapes.
- **The relayed stat is consistent with the files:** 13 files, 512 insertions, 0 deletions.
  - All 13 files are in Phase 1's Files list (plan:239-260).
  - No pre-existing line changed.
  - UNVERIFIED by me; `git diff --stat=200 5edd8d2..5d1c77c` settles it.
- **Nothing unplanned was implemented.** Beyond the plan's wording there are only two additions:
  - an `IsFailure.ShouldBeTrue()` before each domain `Error` assertion, in the class's existing style;
  - one comment, at `UnitOfMeasuresApiTests.cs:65`.

### F2 - D3 and the message texts - info, no defect

The two messages, character by character. ASCII characters appear as themselves.

- **TooPrecise** has 11 non-ASCII letters:
  `D[U+00F6]n[U+00FC][U+015F][U+00FC]m katsay[U+0131]s[U+0131] en fazla 6 ondal[U+0131]k basamak i[U+00E7]erebilir; sondaki s[U+0131]f[U+0131]rlar say[U+0131]lmaz.`
- **TooLarge** has 6 non-ASCII letters:
  `D[U+00F6]n[U+00FC][U+015F][U+00FC]m katsay[U+0131]s[U+0131] en fazla 999.999.999.999,999999 olabilir.`
  Its number is four dot-separated groups of three nines, a comma, and six nines.

Each source below was compared with these two texts by one anchored full-line Grep. A wrong code point, a combining sequence, a non-breaking space, or any extra or missing character would fail the match.

- **The plan's table (plan:226, plan:227).** Both rows match. They use literal letters `\x{00f6}`, `\x{00fc}`, `\x{015f}`, `\x{0131}` and `\x{00e7}`, the code points plan:216-217 names.
- **`src/Envanex.Web/Extensions/TurkishErrorMessages.cs:50` and `:51`.** Both match in the escaped form, with the hexadecimal digits in lower case.
  - `\x5cu[0-9a-fA-F]{0,3}[A-F]|\x5cU|\x5cx` has no match in the file. There is no upper-case digit, no backslash-U escape and no backslash-x escape.
  - The backslash-x point matters. A backslash-u escape has exactly four digits, so two escapes followed by a hex letter decode correctly: `[U+0131]` before `f` in `s[U+0131]f[U+0131]rlar`, and `[U+00E7]` before `e` in `i[U+00E7]erebilir`. A variable-length backslash-x escape would misread both.
  - `[^\x00-\x7F]` matches only the pre-existing Auth lines 16-23.
  - The plan's pattern `"(Product|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]` has no match.
- **`tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs:195`, `:207`, `:221` and `:222`.** All four match in the same escaped form.
  - The file has no non-ASCII byte. The upper-case/backslash-U/backslash-x pattern has no match.
  - Backslashes occur on these four lines only: 11, 6, 11 and 6 of them. Those are exactly the letter counts of the two messages, so the final state has no stray escape elsewhere.
  - As asked, I did not try to check the sed's own effect.
- **`tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs:22` and `:23`.** Both match, with literal letters of exactly these code points.
- **The English `Error.Message` texts, `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasureErrors.cs:11-12`.** Both lines match plan:292-293 in full. The Domain folder has no non-ASCII byte, so the apostrophe in "unit's" is U+0027.

### F3 - wiring of U-T1 and U-M1 - info, no defect

- **U-T1** (`TurkishErrorMessagesTests.cs:20-24`, test at `:85-92`).
  - The rows take their codes from the declared fields: `UnitOfMeasureErrors.ConversionFactorTooPrecise.Code` and `UnitOfMeasureErrors.ConversionFactorTooLarge.Code`.
  - The expected texts are string literals with literal letters. They are not computed from `TurkishErrorMessages` or from the escaped entries.
  - The test asserts `TurkishErrorMessages.GetMessage(errorCode, Fallback).ShouldBe(expected)`. A missing entry would return `"fallback"` and fail.
  - The D3 chain is therefore closed: the plan table equals the U-T1 literals, which equal the decoded table entries, which equal the U-A literals.
- **U-M1** (`ResultExtensionsTests.cs:35-39`, test at `:196-207`).
  - The rows take their codes from the same two declared fields.
  - The failure is built as at `:183`.
  - It asserts `objectResult.StatusCode.ShouldBe(400)` (`:203`), then `Value.ShouldBeOfType<ProblemDetails>()` (`:205`), then `problemDetails.Status.ShouldBe(400)` (`:206`).
  - An unmapped code also falls back to 400 (`ResultExtensions.cs:176`). So U-M1 pins the status and `ResultMappingTests.cs:67` pins that the code is mapped, as plan:474-476 intends.

### F4 - the language-server diagnostic - info; cause UNVERIFIED

- **Definitions.** `UnitOfMeasureErrors.cs:11` (`ConversionFactorTooPrecise`) and `:12` (`ConversionFactorTooLarge`) are `public static readonly Error`.
  - They are in `namespace Envanex.Domain.Aggregates.UnitOfMeasures` (`:3`), the same namespace as `UnitOfMeasure.cs:3`.
  - The repository has one `class UnitOfMeasureErrors` and one `class UnitOfMeasure`.
- **References.** `ConversionFactorToo\w*` over all `*.cs` files finds 26 occurrences in 10 files, each spelled exactly `ConversionFactorTooPrecise` or `ConversionFactorTooLarge`. They are in:
  - `UnitOfMeasure.cs:81` and `:86`;
  - `CreateUnitOfMeasureCommandValidator.cs:42` and `:46`;
  - `ResultExtensions.cs:72-73` and `TurkishErrorMessages.cs:50-51`;
  - the five test files that use them.
- **No hidden characters.** Both Domain files are ASCII-only, so no homoglyph or invisible character sits in an identifier.
- **The language server now.** LSP `goToDefinition` resolves:
  - `UnitOfMeasure.cs:81` to `UnitOfMeasureErrors.cs:11:34`;
  - `UnitOfMeasure.cs:86` to `UnitOfMeasureErrors.cs:12:34`;
  - `CreateUnitOfMeasureCommandValidator.cs:42` and `:46` to the same two fields.
- **The cause** of the diagnostic reported during the commit is UNVERIFIED. The tester's `dotnet build -warnaserror` settles whether the code compiles.

### F5 - production code against Phase 1 - info, no defect

- **`UnitOfMeasure.cs:9-10`: the two constants.** They sit directly after `NameMaxLength`, with no comment. The word-bounded `9{12}\.9{6}m` matches `:9` and the four test rows that use the maximum.
- **`UnitOfMeasure.cs:79-87`: checks 3 and 4.** They follow the positivity check (`:74`), each as its own `if`.
  - The base branch (`:60-66`) is unchanged. The factor reaches the constructor as received (`:90`).
  - Check 3 compares `Math.Round(x, 6, ToEven)` with `!=`, which compares values, so trailing zeros pass. At 6 places, `Math.Round` cannot throw or overflow for any `decimal`.
  - Check 4 is inclusive at the maximum.
  - I walked every row of plan:127-133, and each gives the planned outcome. Both D2 orders hold: TooPrecise comes before TooLarge, and MustBePositive before TooPrecise.
- **`CreateUnitOfMeasureCommandValidator.cs:40-46`: the two rules.**
  - The first is an inline `.Must` lambda with no helper, so deleting it in MU1 leaves no unused member. The second is `.LessThanOrEqualTo(UnitOfMeasure.ConversionFactorMaxValue)`.
  - Each has `.WithErrorCode(<field>.Code)`. The plan's two patterns match once each, at `:42` and `:46`.
  - `\.WithMessage\(` has no match in `src/Envanex.Application`, and the base-unit block is unchanged.
  - The rules are exactly as strict as domain checks 2-4.
- **Tables.** `ResultExtensions.cs:72-73` and `TurkishErrorMessages.cs:50-51` sit directly after `ConversionFactorMustBePositive`, TooPrecise first.
- **Dependency rule: holds.**
  - `Math` and `MidpointRounding` come from `System`, through `ImplicitUsings` (`Directory.Build.props:6`).
  - The validator's Domain using predates this change (`:2`).
- **LF only.** A carriage-return search over a glob of the 13 files finds none. The same glob with `^namespace ` hits all 13 files, so the search covered them all.

### F6 - tests against Phase 1 - info, no defect

- **The 26 planned methods all exist**, with the plan's names, rows and assertions: U-D1..7, U-V1..6, U-H1..2, U-P1..2, U-S1..3, U-A1..4, U-T1, U-M1.
- **Every decimal literal has its planned digit count.** I checked with word-bounded, count-pinning patterns, and each matches only the lines the plan assigns:
  - near the maximum: `9{12}\.9{5}8m`, `9{12}\.9{6}4m`;
  - above the maximum: `10{12}\.0{6}1m`, `10{12}\.0{6}m`, `10{12}m`;
  - small factors: `0\.0{5}1m`, `0\.0{5}10m`, `0\.0{6}1m`, `0\.0{6}5m`;
  - trailing zeros and base units: `1\.50{8}m`, `1\.0{9}m`, `1\.0{6}1m`.
- **Class and helper shapes.**
  - `ConversionFactorPersistenceTests.cs:8-16` mirrors `MoneyPersistenceTests.cs:10-18`.
  - Its helper (`:41-64`) uses three contexts.
  - The `SchemaPrecisionTests.cs:148-163` helpers use `decimal` only.
- **`UnitOfMeasuresApiTests.cs:63-74`: `ReadFieldValidationMessagesAsync`.** It asserts the status before any JSON read. Its PascalCase key matches the existing use at `ProductsApiTests.cs:146` and `:168-175`.
- **U-H1 and U-H2** reach `UnitOfMeasure.Create` (`CreateUnitOfMeasureCommandHandler.cs:45-51`) before any `AddAsync` or `SaveChangesAsync`.
- **U-A4** reads properties that exist (`UnitOfMeasureDetailDto.cs:7`, `:9`).
- **The S1 mutation rows MU1-MU9** all apply to this code shape. Walked through statically, each expected red fails on a Shouldly assertion. None was executed.
- **The reported totals match a static count of the attributes and rows:**
  - Domain filter: 18 + 14 = 32;
  - Application filter: 23 + 2 + 8 = 33;
  - `UnitOfMeasuresApiTests`: 8 + 6 = 14;
  - `TurkishErrorMessagesTests`: 4 + 2 = 6.

  That the runs themselves passed is UNVERIFIED.

### Non-blocking notes

- **N1 - UNVERIFIED: the decimal scale in theory rows.**
  - The `0.0000010m` and `1.500000000m` rows of U-D1, U-V1 and U-A4 test trailing zeros only if xUnit v2 keeps each row's scale when it serializes theory data.
  - The reported +14/+15 deltas, with no row lost as a duplicate, are consistent with that.
  - U-D6, U-V6 and U-P2 test scale 9 with literals regardless.
  - Evidence: `dotnet test tests/Envanex.Domain.Tests --list-tests` should list `0.000001` and `0.0000010` as separate rows.
- **N2 - UNVERIFIED: the coder's runtime claims.** These commands settle them:
  - `dotnet build -warnaserror`;
  - `dotnet test`, and the plan's targeted filters (plan:499-508);
  - `dotnet format --verify-no-changes`;
  - for the created file, LF and no BOM:
    - `git ls-files --eol tests/Envanex.IntegrationTests/Persistence/ConversionFactorPersistenceTests.cs`, expected `i/lf w/lf`;
    - `head -c 3 <that file> | od -An -tx1`, expected `75 73 69`.
- **N3 - carried from the plan review (its N6).** U-T1 and U-M1 still have no mutation row. Two cheap rows would cover them:
  - map `UnitOfMeasure.ConversionFactorTooPrecise` to 422: U-M1 should go red;
  - alter one escape at `TurkishErrorMessages.cs:50`: U-T1 should go red.

  Whether to add them to S1 is the human's call.

## Required changes

None.
