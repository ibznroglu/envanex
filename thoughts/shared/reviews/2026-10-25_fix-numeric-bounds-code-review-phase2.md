Verdict: APPROVED
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md phase 2
Range: df6542d45eb69eb20a09a9826d0653b98b5a3656..56711934e58579ffc706b53b7f3abb6097153afd
Head: 56711934e58579ffc706b53b7f3abb6097153afd

## Findings

How this review was done:

- `plan:N` is line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md`.
- What I read, with the Read tool:
  - Phase 2 (plan:544-795), the S3 rows that will mutate this code (plan:1118-1142) and the Phase 1 code review;
  - all fourteen changed files, in full;
  - the neighbours they depend on: `Money.cs`, `Quantity.cs`, both product command handlers, `ResultMappingTests.cs` and `ProductsApiTests.cs:135-176`.
- Where the evidence comes from:
  - character, digit and placement claims: the Grep tool, run in this review;
  - symbol resolution: LSP `goToDefinition`, run in this review.
- How backslashes are written:
  - In every Grep pattern a backslash is written as `\x5c`, so no backslash-u sequence passed through a tool parameter.
  - This file is ASCII-only on purpose. A non-ASCII letter is written `[U+XXXX]`.
  - "Escaped form" means a backslash, the letter u, and four hexadecimal digits.
- The review was interrupted (HTTP 429) and resumed. The caller reports HEAD unchanged and a clean tree. Before writing, I re-ran the escaped-entry check of F2 against the file on disk; it still matches.
- I have no shell. Every build, test, count, hash and git claim below is the coder's or the caller's, and is UNVERIFIED unless I say how I checked it.

No blocking finding.

### F1 - relayed diff against the files - info, no difference

- **No difference found.** Every hunk of the re-run diff matches the file as read at `5671193`, and the re-run diff matches the copy in the original prompt hunk for hunk.
  - The relay kept the escaped forms in `TurkishErrorMessages.cs` as escapes, and the literal letters in `TurkishErrorMessagesTests.cs` as letters. That is what the two files hold.
- **The stat is consistent with the files:** 14 files, 532 insertions, 12 deletions.
  - The 14 files are exactly Phase 2's Files (plan:550-570). None is under `src/Envanex.Infrastructure/`.
  - The 12 deletions are the 12 lines the plan replaces or moves: `void UpdatePrice` (1), the two moved table lines (2), the bare `UpdatePrice` call in `ProductTests` (1), and the eight integration-test calls (8).
  - I recounted `Product.cs` from its hunks: 46 insertions and 1 deletion, as its `47` in the stat says.
  - The relayed `--summary` lists no mode change, create, delete or rename.
  - UNVERIFIED by me; `git diff --stat=200 --summary df6542d..5671193` settles it.
- **Nothing unplanned was implemented.**

### F2 - D3 and the message texts - info, no defect

The three texts. ASCII characters appear as themselves.

- **ListPriceAmountTooLarge** has 1 non-ASCII letter:
  `Liste fiyat[U+0131] en fazla 99.999.999.999.999,9999 olabilir.`
  Its number is two nines, then four dot-separated groups of three nines (14 nines in all), a comma, and four nines.
- **ReorderPointTooLarge** has 2 non-ASCII letters:
  `Yeniden sipari[U+015F] noktas[U+0131] en fazla 999.999.999.999,999999 olabilir.`
  Its number is four dot-separated groups of three nines, a comma, and six nines.
- **ListPriceAmountNegative** (moved) has 1 non-ASCII letter:
  `Liste fiyat[U+0131] negatif olamaz.`

Each source below was compared with these texts by one anchored full-line Grep. Every character is literal, the 8-space indent included. A wrong code point, a combining sequence, a non-breaking space, or any extra or missing character would fail the match.

- **The plan.** All of these match, with literal letters of exactly these code points:
  - plan:228 and plan:229, the message table;
  - plan:746-748, P-T1's rows;
  - plan:628 and plan:793, the moved entry's "escape for [U+0131] (0131)".
  - plan:216-217 pair each letter with its code: [U+0131] 0131, [U+015F] 015f, [U+00F6] 00f6, [U+00FC] 00fc, [U+00E7] 00e7.
- **`src/Envanex.Web/Extensions/TurkishErrorMessages.cs:31`, `:32` and `:33`.** All three match in the escaped form, with the hexadecimal digits in lower case.
  - `:31` and `:32` each carry the escaped form of U+0131 once. `:33` carries the escaped form of U+015F, then that of U+0131.
  - `\x5cu[0-9a-fA-F]{0,3}[A-F]|\x5cU|\x5cx` has no match in the file. There is no upper-case digit, no backslash-U escape and no backslash-x escape anywhere in it.
  - `[^\x00-\x7F]` matches only the pre-existing Auth lines 16-19, 21 and 23.
  - The plan's pattern `"(Product|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]` has no match.
- **The moved line is unchanged.** `:31` matches the line plan:627-629 describes, and it is textually identical to the removed line as relayed (old `:40`). The coder's md5 claim is UNVERIFIED by me; the anchored match covers the final state.
- **The file's diff holds only the planned change.**
  - The relayed diff has two hunks for this file: +3 after `Product.NameTooLong`, and -1 in "Product - validator". The stat says `4 +-`.
  - Independently, the lines the awk pass should not have touched still match their full-line patterns:
    - the Auth lines 16-19, 21 and 23, with their literal letters [U+0131], [U+00E7], [U+015F], [U+00FC] and [U+011F]. So the pass did not re-encode the UTF-8.
    - `:52` and `:53`, the Phase 1 entries, by the escaped patterns of the Phase 1 review. They have moved down by 2 lines.
  - The file has 80 lines, as the coder says, and no carriage return.
  - UNVERIFIED by me; `git diff df6542d..5671193 -- src/Envanex.Web/Extensions/TurkishErrorMessages.cs` settles it.
- **`tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs:25`, `:26` and `:27`.** All three match, with literal letters of exactly these code points. The file contains no backslash at all.
- **The English `Error.Message` texts, `src/Envanex.Domain/Aggregates/Products/ProductErrors.cs:20` and `:22`.** Both match plan:610-611 character for character. Their digit counts are pinned by `9{14}\.9{4}\.` and `9{12}\.9{6}\.`.

The D3 chain is closed: the plan table equals the P-T1 literals, which equal the decoded table entries.

### F3 - wiring of P-T1 and P-M1 - info, no defect

- **P-T1** (`TurkishErrorMessagesTests.cs:25-27`, theory at `:89-96`).
  - The rows take their codes from the declared fields `ProductErrors.ListPriceAmountTooLarge.Code`, `ProductErrors.ReorderPointTooLarge.Code` and `ProductErrors.ListPriceAmountNegative.Code`. LSP resolves `:26` to `ProductErrors.cs:22:34`.
  - The expected texts are string literals with literal letters. They are not computed from `TurkishErrorMessages`.
  - The test asserts `TurkishErrorMessages.GetMessage(errorCode, Fallback).ShouldBe(expected)`. A missing entry would return `"fallback"` and fail.
  - The `ListPriceAmountNegative` row is the moved entry's only runtime pin, as plan:629-630 says. No API test asserts that text: `ProductsApiTests.cs:150` is CodeRequired, and `:175` is ReorderPointNegative.
- **P-M1** (`ResultExtensionsTests.cs:40-42`, theory at `:198-211`).
  - The rows take their codes from the same three declared fields.
  - The failure is a plain `Error`. It goes through `BuildProblemDetails`, the path a domain failure takes.
  - It asserts both statuses: `objectResult.StatusCode.ShouldBe(400)` (`:207`), then `Value.ShouldBeOfType<ProblemDetails>()` (`:209`), then `problemDetails.Status.ShouldBe(400)` (`:210`).
  - As in Phase 1, an unmapped code also falls back to 400 (`ResultExtensions.cs:178`). So P-M1 pins the status, and `ResultMappingTests.cs:67` and `:81` pin that each code is in both tables.
- **The reported totals match a static count:**
  - `TurkishErrorMessagesTests`: 4 facts + 5 rows = 9;
  - `ResultExtensionsTests`: 9 facts + 8 auth rows (`AuthErrors.cs` declares 8) + 5 rows = 22.

  That the runs themselves passed is UNVERIFIED.

### F4 - "capture product.Id first" (plan:692-693) - info; the coder's reading meets the intent

- **What the code does.** `Guid originalId = product.Id;` appears in exactly the five tests that call `ShouldBeUnchanged`:
  - P-D11 (`ProductTests.cs:395`), P-D12 (`:409`), P-D16 (`:459`), P-D19 (`:498`) and P-D20 (`:512`).
  - In each, it is the first statement after `Product.Create(...)`, before the input is built and before the call under test.
  - The helper is called at `:402`, `:416`, `:467`, `:505` and `:519`, and nowhere else.
  - The eight success tests do not capture the id: P-D9, P-D10, P-D13, P-D14, P-D15, P-D17, P-D18 and P-D21.
- **Why this meets the intent.**
  - The captured id exists to feed `ShouldBeUnchanged(Product product, Guid originalId)` (plan:637-640). The plan's table assigns that helper to these five rows only (plan:670-679).
  - The success rows assert only success and the stored value. A capture there would be a local that no assertion reads: dead code.
  - Whether such a local would fail the build is UNVERIFIED and does not matter, because dead code is unwanted either way. `Directory.Build.props:7-9` sets `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and `AnalysisLevel` `latest-recommended`; `.editorconfig` sets nothing for IDE0059.
  - The sentence's other half holds for all 13 `Update` and `UpdatePrice` tests. Each starts from `Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value`.
- No change is needed.

### F5 - the language-server diagnostic - info; cause UNVERIFIED

- **Definitions.** `ProductErrors.cs:20` (`ListPriceAmountTooLarge`) and `:22` (`ReorderPointTooLarge`) are `public static readonly Error`.
  - They are in `namespace Envanex.Domain.Aggregates.Products` (`ProductErrors.cs:3`), the same namespace as `Product.cs:4`.
  - The repository has one `class ProductErrors`.
- **References.** A search for `class ProductErrors` and the two names over all `*.cs` files finds 29 lines in 9 files. They are the class declaration, the two field declarations, and 26 uses, all where the plan puts them. The uses include the five cited lines `Product.cs:75`, `:80`, `:101`, `:145` and `:150`.
- **No hidden characters.** The Domain folder has no non-ASCII byte, so no homoglyph or invisible character sits in an identifier.
- **The language server now.** LSP `goToDefinition` resolves:
  - `Product.cs:75`, `:101` and `:145` to `ProductErrors.cs:20:34`;
  - `Product.cs:80` and `:150` to `ProductErrors.cs:22:34`.

  Two of these five calls first failed because the server was still starting; on retry, all five resolved.
- **The cause** of the diagnostic reported during the commit is UNVERIFIED, as in Phase 1 (its F4). The tester's `dotnet build -warnaserror` settles whether the code compiles.

### F6 - production code against Phase 2 - info, no defect

- **`Product.cs:10-11`: the two constants.** They sit directly after `NameMaxLength`, with no comment.
  - Their digit counts are pinned by the word-bounded `9{14}\.9{4}m` and `9{12}\.9{6}m`.
  - LSP resolves `SchemaPrecisionTests.cs:164` to `Product.cs:10:26`.
- **`Create` (`:68-81`) and `Update` (`:138-151`): checks 1-3.**
  - They follow the unit-of-measure check, each as its own `if`, in the planned order: negative, too large, reorder point too large.
  - In `Create` they come before normalization (`:83`). In `Update` they come before all four assignments (`:153-156`).
- **`UpdatePrice` (`:90-107`).** It returns `Result`: `ThrowIfNull` first, then checks 1 and 2, then the assignment, then `Result.Success()`.
- **The sign check.** Check 1 is the numeric `< 0m`. There is no `IsNegative` anywhere in `src/`, and no `void UpdatePrice`.
- **Null versus invalid.**
  - `Money` is a `sealed record` (`Money.cs:5`). It stays guarded by `ThrowIfNull` on all three paths.
  - `Quantity` is a `readonly record struct` (`Quantity.cs:5`). So `reorderPoint.Value` cannot throw a null reference, and no guard is needed.
- **The plan's table rows hold.** `Money.Of` and `Quantity.Of` round with `MidpointRounding.ToEven` (`Money.cs:21`, `Quantity.cs:23`).
  - At `...99995` and `...9999995` the even neighbour is the carried value. So the domain receives `100000000000000.0000` and `1000000000000.000000`, and rejects them.
  - `...99994` and `...9999994` round down to the maxima and are accepted.
  - `-0.00004` rounds to a zero, which `< 0m` accepts. `-0.0001` is rejected.
- **Tables.** `ResultExtensions.cs:50-64` and `TurkishErrorMessages.cs:25-44` hold exactly the groups of plan:616-633.
  - The plan's Grep check `Product\.ListPriceAmountNegative` gives two matches, one per file: `TurkishErrorMessages.cs:31`, between the comments at `:25` and `:40`, and `ResultExtensions.cs:56`, between `:50` and `:60`.
- **`UpdatePrice` callers.** It has no caller in `src/`. Every test caller checks the result or the throw:
  - `ProductTests.cs:87`, `:98` and the five new tests;
  - `RepositoryTests.cs:61`, `:65`, `:106`, `:117`, `:271` and `:285`;
  - `ConcurrencyTests.cs:48` and `:52`.
- **Dependency rule: holds.** The only new references in Domain are to Domain types.
- **LF only.** A carriage-return search over a glob of the 14 files finds none. The same glob with `^namespace ` hits all 14 files, so the search covered them all.

### F7 - tests against Phase 2 - info, no defect

- **All planned tests exist**, with the plan's names, inputs and assertions: P-D1..P-D21, P-HC1..P-HC3, P-HU1..P-HU3, P-P1, P-P2, P-S1, P-S2, P-T1 (3 rows) and P-M1 (3 rows).
  - `UpdatePrice_ShouldChangeListPrice` now asserts `IsSuccess` first (`ProductTests.cs:87-90`).
  - `UpdatePrice_WithNull_ShouldThrow` is unchanged.
- **No partial update can hide.**
  - Every failing `Update` changes every other argument: `"Changed Name"`, a new id, and a different price or `Quantity.Of(20m)` (plan:689-690).
  - Every failure test asserts `IsFailure` before `Error`.
  - `ShouldBeUnchanged` (`:534-544`) asserts the eight properties of plan:637-643. `DomainEvents` exists on `AggregateRoot` (`AggregateRoot.cs:19`).
- **Every decimal literal has its planned digit count.** I checked with word-bounded, count-pinning patterns.
  - In `ProductTests.cs` they account for all 31 literals of the forms `9...9.9...m`, `-0.0001m` and `-0.00004m`, so no wrong-length literal hides among them.
  - The same holds for the 3 literals in each of the two handler test files, and in each of the two persistence test files.
- **The handler tests protect what they claim.** Both handlers call the aggregate before `AddAsync`, `SetOriginalRowVersion` and `SaveChangesAsync`:
  - `CreateProductCommandHandler.cs:64-78`;
  - `UpdateProductCommandHandler.cs:64-74`.

  So `SaveChangesCalled.ShouldBe(0)`, `Products.Count.ShouldBe(0)` (create) and `SetOriginalRowVersionCallCount.ShouldBe(0)` (update) go red when a domain check is removed.
- **The S3 mutation rows MP5-MP14** apply to this code shape: each check is its own deletable `if`, and the moves of MP13 and MP14 are possible. Walked through statically, each expected red fails on a Shouldly assertion. None was executed.
- **The reported totals match a static count of the attributes:**
  - `ProductTests`: 25 + 21 = 46;
  - `Products.Commands`: 10 + 10 + 5 + 5 = 30;
  - the deltas +21 / +6 / +12 match plan:754.

  That the runs themselves passed is UNVERIFIED.
- **The new bounds break no existing test, by construction.**
  - No test outside `ProductTests` builds a product from a negative `Money`. The `-1m` rows at `CreateProductCommandValidatorTests.cs:88` and `UpdateProductCommandValidatorTests.cs:88` reach only the validator.
  - Outside Phase 2's own files, the only decimal literals with 12 or more integer digits are the Phase 1 unit-of-measure rows. No product test elsewhere sends an amount near the maxima.

### Non-blocking notes

- **N1 - a product-count assertion that cannot go red.** P-HU1..P-HU3 assert `_productRepository.Products.Count.ShouldBe(1)` (`UpdateProductCommandHandlerTests.cs:144`, `:160`, `:176`).
  - No `Product.cs` mutation can turn it red, because the update handler never adds or removes a product.
  - It is planned (plan:719) and harmless. The assertions that protect are `SaveChangesCalled` and `SetOriginalRowVersionCallCount`.
- **N2 - the API between Phase 2 and Phase 3.** Until Phase 3's validator rules land, a raw list price or reorder point that rounds above its maximum passes the validator, and the domain rejects it.
  - The caller then gets a plain 400 ProblemDetails: title `Bad Request`, a Turkish detail, and no `errors`.
  - This is the designed interim state (plan:1159). Phase 3's P-A1..P-A5 replace it with field-level 400s.
- **N3 - carried from Phase 1 (its N3).** P-T1 and P-M1 still have no mutation row. Two cheap rows would cover them in S3:
  - map `Product.ListPriceAmountTooLarge` to 422: P-M1 should go red;
  - change one escape at `TurkishErrorMessages.cs:32`: P-T1 should go red.

  Whether to add them is the human's call.
- **N4 - UNVERIFIED: the coder's runtime claims.** These commands settle them:
  - `dotnet build -warnaserror`;
  - `dotnet test`, and the plan's targeted filters (plan:758-769);
  - `dotnet format --verify-no-changes`. The format hook did not run on the awk-edited file, so this run is that file's only format check.
  - No BOM after the awk pass: `head -c 3 src/Envanex.Web/Extensions/TurkishErrorMessages.cs | od -An -tx1`, expected `75 73 69`. The relayed diff has no hunk at line 1 or at the end of the file, and the relayed `--summary` shows no mode change.
- **N5 - trimmed output.** The coder trimmed some `dotnet` output with `tail` or `grep`, and said so for each block. The tester's gate run is the raw evidence of record for this phase.

## Required changes

None.
