Verdict: APPROVED
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md phase 3
Range: 8c34f8f134d1295db79d7fa15e3ee9ac42da8fad..0f23453076a47667e28009c0e57c08a62bcd0829
Head: 0f23453076a47667e28009c0e57c08a62bcd0829

## Findings

How this review was done:

- `plan:N` is line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md`.
- What I read, with the Read tool:
  - from the plan: Phase 3 (plan:797-924), the message table and its source-form rule (plan:211-231), the Phase 1 helper specification (plan:338-343), and the S3 rows (plan:1118-1142);
  - the Phase 2 code review, and the plan review r2's notes on Phase 3;
  - all five changed files, in full;
  - the code they depend on: `UnitOfMeasuresApiTests.cs:1-90`, `ResultExtensions.cs:120-199`, both product command handlers, and the command and DTO records.
- Where the evidence comes from:
  - character, digit, placement and line-ending claims: the Grep tool, run in this review;
  - symbol resolution: LSP `goToDefinition`, run in this review.
- How backslashes are written:
  - This file is ASCII-only, and it contains no backslash-u sequence on purpose. A non-ASCII letter is written `[U+XXXX]`.
  - "Escaped form" means a backslash, the letter u, and four hexadecimal digits.
  - In the patterns quoted below, `\x5c` stands for one backslash. I ran some of them with an escaped backslash instead; both match the same byte.
- I have no shell. Every build, test, count, hash and git claim below is the coder's or the caller's. It is UNVERIFIED unless I say how I checked it.

No blocking finding.

### F1 - relayed diff against the files - info, no difference

- **No difference found.** Every hunk of the relayed diff matches the files as read at `0f23453`.
  - The relay kept the five new message literals in the escaped form, and the file holds the same form (F2). There is no difference to record.
- **The stat is consistent with the files:** 5 files, 320 insertions, 0 deletions.
  - The 5 files are exactly Phase 3's Files (plan:803-807). None is under `src/Envanex.Infrastructure/`.
  - By the hunk headers, each file's line count at HEAD is its base count plus its insertions:

    | File | Base | Added | HEAD |
    |---|---|---|---|
    | Create validator | 47 | 8 | 55 |
    | Update validator | 51 | 8 | 59 |
    | Create tests | 139 | 92 | 231 |
    | Update tests | 128 | 92 | 220 |
    | `ProductsApiTests.cs` | 401 | 120 | 521 |
  - `ProductsApiTests.cs` has two hunks: +13 (the helper, `:74-86`) and +107 (P-A1..P-A7, `:414-520`). Their context lines match the file.
  - UNVERIFIED by me. `git diff --numstat 8c34f8f..0f23453` settles it. The expected rows are `8 0`, `8 0`, `92 0`, `92 0` and `120 0`.
- **Nothing unplanned was implemented.**

### F2 - D3 and the message literals in `ProductsApiTests.cs` - info, no defect

I checked the final state only, as asked.

- **ASCII-only.** `[^\x00-\x7F]` has no match in the file. This is the plan's check (plan:921-922).
- **The escapes.** A pattern for a backslash, `u` and four hexadecimal digits matches 11 lines:
  - the six pre-existing lines `:156`, `:163`, `:186`, `:187`, `:188` and `:204`;
  - the five new lines `:424`, `:436`, `:448`, `:465` and `:482`.

  The five new lines also match `fiyat\x5cu0131 en fazla|sipari\x5cu015f noktas\x5cu0131 en fazla`.
- **Lower-case hexadecimal digits.** `\x5cu[0-9a-fA-F]{0,3}[A-F]` has no match anywhere in the file.
- **Nothing is left from the sed and perl passes:**
  - no code-point digits glued to a word (the `fiyat0131` shape);
  - no doubled backslash, no backslash-U, and no literal `x5c` text;
  - no carriage return (count 0).
- **Each literal is pinned by one anchored full-line Grep.** Every character in it is literal, the 8-space indent included:
  - **P-A1 `:424`, P-A2 `:436` and P-A4 `:465`:** `Liste fiyat`, then the escaped form of U+0131 (digits `0131`), then ` en fazla 99.999.999.999.999,9999 olabilir.`
  - **P-A3 `:448` and P-A5 `:482`:** `Yeniden sipari`, then the escaped form of U+015F (digits `015f`), then ` noktas`, then the escaped form of U+0131, then ` en fazla 999.999.999.999,999999 olabilir.`
  - Each test reads the field its literal belongs to: `ListPriceAmount` at `:423`, `:435` and `:464`, and `ReorderPoint` at `:447` and `:481`.
- **The plan's message table.** An anchored Grep with the code points `\x{0131}` and `\x{015f}` matches plan:228 and plan:229 with exactly these texts:
  - `Liste fiyat[U+0131] en fazla 99.999.999.999.999,9999 olabilir.`
  - `Yeniden sipari[U+015F] noktas[U+0131] en fazla 999.999.999.999,999999 olabilir.`

  So each decoded literal equals its table text, character by character.
- **The shipped source.** The same anchored form matches `src/Envanex.Web/Extensions/TurkishErrorMessages.cs:32` and `:33`.
  - So the test literals are byte for byte the shipped literals.
  - P-T1 already pins those shipped literals to the decoded text.
- **No existing line changed.**
  - The relayed diff has no deletion.
  - The six pre-existing escaped lines still match their escaped form.
- **BOM: UNVERIFIED.**
  - `head -c 3 tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs | od -An -tx1` settles it. The expected output is `75 73 69`.
  - A BOM added by the perl pass would change line 1, and the relayed diff has no hunk there.

### F3 - the API tests' shape - info, no defect

- **The helper** (`ProductsApiTests.cs:74-85`) is identical, line for line, to `UnitOfMeasuresApiTests.cs:63-74`, comment included.
  - It is `private static`, with the signature of plan:831-832.
  - It asserts `HttpStatusCode.BadRequest` (`:77`) before it reads the body (`:79`). Then it asserts the `title` `Validation Failed`, the `errors` property, and the field.
  - It needs no new `using`. The file already imports `System.Net`, `System.Net.Http.Json`, `System.Text.Json` and Shouldly.
- **The body it reads** comes from `ResultExtensions.cs:145-174`.
  - Each failure is keyed by its `PropertyName`. Its message is `TurkishErrorMessages.GetMessage(failure.ErrorCode, GenericValidationFallback)`, so a missing entry gives the fallback and fails the literal.
  - A domain failure goes through `BuildProblemDetails` (`:176-191`) instead. Its title is `GetReasonPhrase(400)`, which is `"Bad Request"` (`:201-203`), so it fails the title assertion.
- **P-A1..P-A5** follow plan:858-872:
  - they seed through the existing helpers;
  - POST uses `BOUND-001` and `Bound Product`, and PUT uses `Updated`;
  - the defaults are `100m` and `10m`;
  - each asserts the exact one-element list.
- **P-A6** (`:485-500`) asserts `Created` and reads the id with `ReadFromJsonAsync<Guid>()`. It then calls `GetProductDetailAsync` and compares with `GetDecimal()`: `ShouldBe(99999999999999.9999m)` and `ShouldBe(999999999999.999999m)`.
- **P-A7** (`:502-520`) asserts `HttpStatusCode.OK` (`:515`), then makes the same two by-value comparisons on a fresh GET.
  - `ProductDetailDto.cs:9` and `:11` declare `decimal ListPriceAmount` and `decimal ReorderPoint`. So `listPriceAmount` and `reorderPoint` are camel-cased, like `rowVersion` at `:102`.
- **Line references.** These citations are base line numbers. Phase 3's 13-line helper moved each down by 13. Info only:
  - plan:887 cites `ProductsApiTests.cs:270` for the `OK` assertion. That assertion is now at `:283`, and `:270` is now the method's declaration.
  - plan:212 and plan:218 cite `:150` and `:175`. They are now `:163` and `:188`.
- **A validator-free request reaches the domain**, as plan:855-856 requires.
  - Both handlers check the code or the product, then the unit of measure, then the currency. Only then do they call `Money.Of`, `Quantity.Of` and the aggregate (`CreateProductCommandHandler.cs:29-66`, `UpdateProductCommandHandler.cs:29-64`).
  - The tests seed an active unit and use a fresh code, so no 409 or 422 answers first.
- **Every large literal has its planned digit count.** Two patterns return the same matches:
  - a generic pattern for decimal literals with 12 or more integer digits;
  - a word-bounded union of the planned forms, with each digit count pinned.

  They return 13 matches in `ProductsApiTests.cs` and 22 in the two validator test files.

### F4 - the validator rules - info, no defect

- **Placement.** Each new rule is its own `RuleFor` statement, directly after the existing rule for the same field:
  - `CreateProductCommandValidator.cs:35-37` after `:31-33`, and `:51-53` after `:47-49`;
  - `UpdateProductCommandValidator.cs:31-33` after `:27-29`, and `:47-49` after `:43-45`.
- **What they check.** Each checks the raw command value with `LessThanOrEqualTo` and the declared error's `.Code`. Both command records declare the two fields as `decimal` (`CreateProductCommand.cs:7`, `:9`; `UpdateProductCommand.cs:7`, `:9`).
- **Symbol resolution.** LSP `goToDefinition` pairs each field with its own constant and error:

  | Reference | Lines | Resolves to |
  |---|---|---|
  | `ListPriceAmountMaxValue` | `Create:36`, `Update:32` | `Product.cs:10:26` |
  | `ReorderPointMaxValue` | `Create:52`, `Update:48` | `Product.cs:11:26` |
  | `ListPriceAmountTooLarge` | `Create:37`, `Update:33` | `ProductErrors.cs:20:34` |
  | `ReorderPointTooLarge` | `Create:53`, `Update:49` | `ProductErrors.cs:22:34` |
- **The existing literal rules are unchanged.** Each literal appears once per file, and the relayed diff deletes nothing:
  - `"Product.ListPriceAmountNegative"` at `Create:33` and `Update:29`;
  - `"Product.ReorderPointNegative"` at `Create:49` and `Update:45`.
- **ADR 0006.** There is no `.WithMessage(` and no cascade mode anywhere in `src/`, so each rule reports independently (plan:52-53).
- **Dependency rule: holds.** The only new references are to Domain types, whose namespace is already imported at line 2.

### F5 - the validator tests - info, no defect

- **The planned tests exist.** P-VC1..P-VC5 and P-VU1..P-VU5 have the plan's names, attributes, data members and rows (plan:826-853), built as `ValidCommand with { ... }`.
- **The failure tests** compare the field's exact ordered code list with literal strings, as Phase 1 does (`CreateUnitOfMeasureCommandValidatorTests.cs:172-175`).
- **P-VC4/P-VU4** assert that `IsValid` is true. The plan gives them no Asserts line (plan review r2, N5), and their ShouldPass name means exactly that.
- **P-VC3/P-VU3 hold by construction.** `-0.00004m` fails `GreaterThanOrEqualTo(0)` and passes the new rule. So the field's list is exactly `["Product.ListPriceAmountNegative"]`.
- **Static count:**
  - `CreateProductCommandValidatorTests`: 11 facts, plus 1 fact, plus 2 + 3 + 2 + 3 theory rows = 22 cases;
  - `UpdateProductCommandValidatorTests`: 10 + 1 + 10 = 21;
  - `ActivateProductCommandValidatorTests` and `DeactivateProductCommandValidatorTests`: 4 facts each.

  That is 51 in all, the total of the coder's filtered run. The two classes gain 22, as plan:890 says. `ProductsApiTests` has 27 test attributes, 20 + 7. That the runs passed is UNVERIFIED.

### F6 - LF and the plan's Grep checks - info, no defect

- **LF only.** `\r` has no match in the five files.
  - A positive control, `^namespace ` over the same glob, hits all five. So the search covered them.
- **ASCII.** The two validator test files contain no non-ASCII character.
- **The plan's checks at plan:915-922 all hold,** as F2 and F4 show.

### Non-blocking notes

- **N1 - S3 readiness.** I walked these rows statically and executed none of them. Recording the observed reds is S3's job.
  - **MP1.** P-A1 should go red on the helper's title assertion, because the domain answers with a plain ProblemDetails. P-A2 should go red on its status assertion, because `99999999999999.99994` rounds to M4 and the product is created.
  - **MP20.** P-VC1's M4 row should go red on `IsValid`, and P-A6 on `Created`. The `...9998` row stays green.
  - **MP2-MP4 and MP15.** The tests have the shapes these rows assume.
- **N2 - carried from Phase 2 (its N3).**
  - P-A1..P-A5 now also pin the two TooLarge texts at runtime, through the validator path. A wrong escape at `TurkishErrorMessages.cs:32` or `:33` now turns them red, as well as P-T1.
  - P-M1's 400 mapping still has no mutation row. Whether to add one is the human's call.
- **N3 - UNVERIFIED: the coder's runtime claims.** These commands settle them:
  - `dotnet build -warnaserror`;
  - `dotnet test`, and the plan's targeted filters (plan:894-901);
  - `dotnet format --verify-no-changes`. The perl pass ran outside the format hook, so this run is the only format check of that edit;
  - the BOM check in F2.
- **N4 - trimmed output.** The coder trimmed some `dotnet` output with `tail` or `grep`, and said so. The tester's gate run is the raw evidence of record for this phase.

## Required changes

None.
