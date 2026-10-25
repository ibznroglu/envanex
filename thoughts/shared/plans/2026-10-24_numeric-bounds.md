# Plan: numeric bounds and the product list-price sign (`fix/numeric-bounds`)

Date: 2026-10-24. Branch: `fix/numeric-bounds` (HEAD `cb4b079`). Input:
`thoughts/shared/research/2026-10-24_numeric-bounds.md`, whose file:line references were taken at
`56fd6d4`. `cb4b079` only adds that research file, so they still hold, and this plan re-read every
file it cites at `cb4b079`. Suggested PR title:
`fix(domain): bound the persisted numeric fields and reject a negative list price`.

## Decisions this plan implements (the human's; not reopened)

- **D1:** the bounds live on the persisted fields, in `Product` and `UnitOfMeasure` (research
  option A2). `Money` and `Quantity` are unchanged. No schema change, no migration, and no change
  to an EF configuration, convention or converter.
- **D2:** validators check the raw command value. `Product` checks the value it receives, which the
  handlers have already rounded with `Money.Of` / `Quantity.Of`. `UpdatePrice` returns `Result`.
  Every check runs before any assignment. The negative check is the numeric `< 0m`, with no sign-bit
  rule. `UnitOfMeasure.Create` checks a derived factor as typed, in this order: positivity, then
  6-place representability by value, then the upper bound. The base-unit rule is unchanged. The
  maxima and the factor's decimal places are public constants on the aggregates.
- **D3:** four new codes with fixed Turkish texts (Signatures below). Each gets an `Error` field, a
  400 entry and a Turkish entry. `Product.ListPriceAmountNegative` moves out of the "validator"
  groups. New validator rules use the declared error's `.Code`; existing literals stay as they are.
- **D4:** no ADR. `docs/roadmap.md` records the rule instead (Phase 4, change (d)).
- **D5:** no characterization phase. How SqlClient, SQL Server and HTTP behaved before the fix is
  still UNVERIFIED and is never stated as fact. A mutation row records only what its own run
  observes.
- **Correction to the research (human):** ADR 0006 requires `.WithErrorCode(...)` and forbids
  `.WithMessage(...)` (`docs/adr/0006-rest-error-mapping.md:41-45`). It does not require string
  literals, so writing `ProductErrors.ListPriceAmountTooLarge.Code` complies with it.

## Facts (verified at `cb4b079`)

- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs:7-8` holds `CodeMaxLength` and
  `NameMaxLength`. The factor rules are at `:58-76`:
  - base unit: `!= 1m` gives `BaseUnitFactorMustBeOne`;
  - derived unit: `InvalidBaseUnitId` first, then `<= 0m` gives `ConversionFactorMustBePositive`.
- `UnitOfMeasureErrors.cs:9-10` declares `BaseUnitFactorMustBeOne` and
  `ConversionFactorMustBePositive`.
- `src/Envanex.Domain/Aggregates/Products/Product.cs`:
  - `:8-9` holds the constants.
  - `Create` is at `:37-71`: `ThrowIfNull` at `:39`, the unit-of-measure check at `:61-64`,
    normalization at `:66-67`.
  - `UpdatePrice` is `void`, at `:73-77`.
  - `Update` is at `:89-114`: checks at `:93-106`, assignments at `:108-111`.
- `ProductErrors.cs:19-20` declares `ListPriceAmountNegative` and `ReorderPointNegative`. No domain
  code returns either today.
- Raw rules today:
  - `CreateProductCommandValidator.cs:31-33` (`>= 0`, `"Product.ListPriceAmountNegative"`) and
    `:43-45` (`"Product.ReorderPointNegative"`);
  - `UpdateProductCommandValidator.cs:27-29` and `:39-41`, the same two rules;
  - `CreateUnitOfMeasureCommandValidator.cs:27-39`, two `When` blocks.
  - Each rule is its own `RuleFor` statement. No cascade mode is set anywhere in `src/`, so every
    failure is reported.
- Status map in `src/Envanex.Web/Extensions/ResultExtensions.cs`: "Product domain" at `:50-55`,
  "Product validator" at `:57-62`, "UnitOfMeasure" at `:64-71`.
- `TurkishErrorMessages.cs`: "Product - domain" at `:25-35`, "Product - validator" at `:37-42`,
  "UnitOfMeasure" at `:44-55`. All three groups use `\u` escapes.
- `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs:37-123`: a new `public static readonly
  Error` field turns two facts red until both tables have the code:
  `ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping` (`:67`) and
  `ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage` (`:81`). So every phase that adds a
  field also adds both table entries.
- The fakes already expose everything the undecorated-handler tests (U-H1, U-H2, P-HC1..P-HC3,
  P-HU1..P-HU3) need:
  - `FakeUnitOfWork.SaveChangesCalled` (`FakeUnitOfWork.cs:9`);
  - `FakeProductRepository.Products` (`:11`) and `SetOriginalRowVersionCallCount` (`:18`);
  - `FakeUnitOfMeasureRepository.Units` (`:11`) and `SeedActiveStatus` (`:24`).
- `src/Envanex.Application/DependencyInjection.cs:36-40` registers the concrete handlers
  undecorated.
- `UpdatePrice` has no caller in `src/` (grep). Its test callers:
  - `ProductTests.cs:87` and `:97`;
  - `RepositoryTests.cs:61`, `:65`, `:106`, `:117`, `:271` and `:285`;
  - `ConcurrencyTests.cs:48` and `:52`.
- `SchemaPrecisionTests.cs:17-53` pins only `Products.ListPrice_Amount` (18,4) and
  `Products.ReorderPoint` (18,6). Its `ColumnInfo` (`:94-98`) has `byte Precision` and `int Scale`.
- Mutation scripts:
  - `mutate-run.sh` records `git diff HEAD -- <file>` for **one** `<file>` (`:7`, `:118-124`).
  - `mutate-clean.sh` takes several files (`:6`, `:54-56`).
  - Neither has been used in `dotnet` mode or with several files before (code review, Phase 3,
    O4). This plan is the first such use.
- `.claude/settings.json:165-174`: a PostToolUse hook runs
  `dotnet format --no-restore --include <file>` after every `.cs` Edit or Write.
- The tester's `bash-allowlist.js:176-177` splits a command on every `|`, ignoring quotes. The
  tester's targeted commands below therefore use single-token filters without `|`. The `|` filters
  are coder-only, and appear only in the mutation rows.
- Lines in `docs/roadmap.md`:
  - `:60` is the `fix(infra)` row and `:61` the PR 7 row;
  - `:121-122` is "Money is not nullable";
  - `:123-124` is "Every `Money` property needs an explicit `ComplexProperty` line";
  - `:196` is the integration-suite duration row.
- EF Core 10 optional complex types, from Microsoft Learn:
  - "What's New in EF Core 10" says EF 10 "adds support for **optional** types", and that
    "optional complex types currently require at least one required property to be defined on the
    complex type".
  - Source: https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew

## Goal

Every persisted amount, quantity and factor is checked against its column's range before it can
reach `SaveChanges`. Today no layer has an upper bound for these values. A derived factor can carry
more than 6 decimal places, and a negative list price passes the domain. What SqlClient and
SQL Server do with such a value today is UNVERIFIED (research, D5).

After the fix there are two layers, each tested:

- **The domain.** `Product` and `UnitOfMeasure` reject an out-of-range value they receive with a
  `Result` failure. This also holds on paths without a validator: the undecorated handlers, and
  `UpdatePrice`.
- **The validators.** They reject the raw request value with a field-level 400 and the Turkish
  message.

### Behaviour after the fix (decided by D2; each row is tested)

M4 = `99999999999999.9999` (`decimal(18,4)`); M6 = `999999999999.999999` (`decimal(18,6)`).

| Field: input | Validator (raw) | Handler | Domain (value received) | Tests |
|---|---|---|---|---|
| ListPrice: M4 | accept | `Money.Of` keeps it | accept | P-VC1, P-D1, P-P1, P-A6, P-A7 |
| ListPrice: `99999999999999.99994` | reject `ListPriceAmountTooLarge` | `Money.Of` gives M4 | accept; SQL stores M4 | P-VC2, P-A2, P-D2, P-P1 |
| ListPrice: `99999999999999.99995` | reject `ListPriceAmountTooLarge` | `Money.Of` gives `100000000000000.0000` | reject `ListPriceAmountTooLarge` | P-VC2, P-D3, P-HC1 |
| ListPrice: `-0.0001` | reject `ListPriceAmountNegative` (existing rule) | unchanged | reject `ListPriceAmountNegative` | P-D4, P-HC2 |
| ListPrice: `-0.00004` | reject `ListPriceAmountNegative` (existing raw `>= 0`) | `Money.Of` gives a zero | accept as zero, numerically | P-VC3, P-D5 |
| ReorderPoint: M6 | accept | `Quantity.Of` keeps it | accept | P-VC4, P-D6, P-P2, P-A6, P-A7 |
| ReorderPoint: `999999999999.9999994` | reject `ReorderPointTooLarge` | `Quantity.Of` gives M6 | accept; SQL stores M6 | P-VC5, P-D7, P-P2 |
| ReorderPoint: `999999999999.9999995` | reject `ReorderPointTooLarge` | `Quantity.Of` gives `1000000000000.000000` | reject `ReorderPointTooLarge` | P-VC5, P-D8, P-HC3 |
| ReorderPoint: negative | unchanged (`ReorderPointNegative`) | unchanged: `Quantity.Of` rejects the raw negative | not reached | existing |
| Derived factor: `0.000001`, `0.0000010`, `1.500000000` | accept | passed as typed | accept, stored without changing its numeric value | U-V1, U-D1, U-P1, U-P2, U-A4 |
| Derived factor: `0.0000001`, `0.0000005`, `0.1234567` | reject `ConversionFactorTooPrecise` | as typed | reject `ConversionFactorTooPrecise` | U-V2, U-D2, U-H1, U-A1 |
| Derived factor: M6 | accept | as typed | accept; SQL stores M6 | U-V1, U-D1, U-P1, U-A4 |
| Derived factor: `1000000000000` | reject `ConversionFactorTooLarge` | as typed | reject `ConversionFactorTooLarge` | U-V3, U-D3, U-H2, U-A2 |
| Derived factor: `1000000000000.0000001` | reject `TooPrecise` and `TooLarge`, in that order | as typed | reject `ConversionFactorTooPrecise` (checked first) | U-V4, U-D4, U-A3 |
| Derived factor: `-0.0000001` | reject `MustBePositive` and `TooPrecise` | as typed | reject `ConversionFactorMustBePositive` (checked first) | U-V5, U-D5 |
| Base factor: `1.000000000` | accept (`Equal(1m)` compares by value) | as typed | accept | U-V6, U-D6 |

Through the API, every validator rule is at least as strict as the matching domain check. A domain
code for these fields therefore reaches a caller only on an undecorated path, which the handler
tests U-H1, U-H2, P-HC1..P-HC3 and P-HU1..P-HU3 cover. The domain's
`ListPriceAmountNegative` accepts `[-0.00005, 0)` as zero. That asymmetry is deliberate (D2) and
does not extend to `ReorderPoint`.

## Non-goals

- No change to `Money` or `Quantity`: sign, rounding and arithmetic stay as they are. ADR 0002 is
  untouched. `MoneyTests.Of_WithNegativeAmount_ShouldSucceed` stays green.
- No schema, migration, CHECK constraint, EF configuration, convention or converter change, and no
  file under `src/Envanex.Infrastructure/` changes. If an implementation step seems to need one,
  the coder stops and reports. The human decides, and db-reviewer would be required.
- No sign-bit rule and no `decimal.IsNegative` in production code. The domain adds no normalization
  step for a factor, and an accepted factor is stored without changing its numeric value.
- No change to the existing raw `>= 0` rules, their literal codes, or the `ReorderPoint` negative
  path.
- No pre-fix characterization (D5). No test of JSON numbers beyond `decimal`'s range; that stays
  UNVERIFIED (research).
- No ADR (D4). ADR 0003 ("Complex types cannot be null") and ADR 0006 are historical and are not
  edited.
- No new fakes or shared test infrastructure. The helpers named below are private to their class.
- No Warehouse, datasource, PR 7 or `UnitOfWork` exception-translation change.

## Touches schema? (yes/no — db-reviewer required if yes)

No. No phase changes the schema, a migration, an EF configuration, convention or converter, or a
production query. Phases 1 and 2 add test-side SQL only, in the existing style:

- `SqlQueryRaw` reads of `INFORMATION_SCHEMA.COLUMNS` in `SchemaPrecisionTests`;
- `DbContext` round-trips in the persistence tests.

db-reviewer is not required:

- `.claude/agents/db-reviewer.md` reviews schema, migrations, EF configuration and query shape
  (`:3`). Its query checks (`:37-42`: indexes, `AsNoTracking`, `IQueryable` leaving the repository,
  `DataSourceLoader` translation) apply to the application's data access.
- `/pr` precondition 7 (`.claude/skills/pr/SKILL.md:61-63`) triggers only on "Touches schema? yes"
  or on a changed path under `db/`, `src/Envanex.Infrastructure/Migrations/` or
  `src/Envanex.Infrastructure/Persistence/`. This PR changes none of those.
- Its only SQL is in tests (the `INFORMATION_SCHEMA` reads and `DbContext` round-trips above), and
  the code-reviewer covers it.

## ADR needed? (title, or "no")

No (D4). Phase 4 records the rule "persisted numeric fields carry their column's bound in their
aggregate" under "Decisions carried forward" in `docs/roadmap.md`, as change (d).

## Pipeline and order

1. Plan review.
2. Phase 1: coder, human commit, code-reviewer, human commits the review, tester. Then mutation
   step **S1** against Phase 1's code commit (rows MU1-MU9).
3. Phase 2, the same cycle. Its domain checks are mutation-tested in S3.
4. Phase 3, the same cycle. Then mutation step **S3** against Phase 3's code commit (rows
   MP1-MP15 and MP20), so that one run per row sees the domain, handler and API tests together.
5. Phase 4 (docs only, a separate docs commit), the same cycle.
6. Explainer, then the PR-end sequence. `/security-review` is not skipped, because `src/` changed.

Suggested commit subjects. The human writes the commits.

- Phase 1: `fix(domain): bound the unit-of-measure conversion factor's precision and size`
- Phase 2: `fix(domain): bound the product list price and reorder point and reject a negative price`
- Phase 3: `fix(app): check the product list price and reorder point maxima in the validators`
- Phase 4: `docs(roadmap): record the numeric-bounds fix, the suite measurement and two decisions`

Test IDs: `U-` marks unit-of-measure tests and `P-` product tests. Then `D` is domain, `V` the
validator (`VC`/`VU` for the create/update product validators), `H` the handler (`HC`/`HU`), `P`
persistence, `S` schema, `A` API, `T` the Turkish message text (`TurkishErrorMessagesTests`) and
`M` the status mapping (`ResultExtensionsTests`). The mutation table uses these IDs.

Theory data: `decimal` cannot appear in `[InlineData]`, so every decimal theory uses
`[MemberData(nameof(X))]` over a `public static TheoryData<decimal> X` property. Precedent:
`ResultExtensionsTests.cs:16`, `ProductsDatasourceTests.cs:219`. The count deltas below assume
xUnit enumerates each decimal row as its own case. Record the observed totals either way.

Expected Turkish messages. Tests compare against the escaped literal, in the style of
`ProductsApiTests.cs:150`.

**Source form of these messages (D3).** In `TurkishErrorMessages.cs`, and in every API-test literal
that pins one of these messages, each non-ASCII letter is written as a backslash, the letter u, and
four hexadecimal digits. The letters these messages use are ı (0131), ş (015f), ö (00f6),
ü (00fc) and ç (00e7). Every other character is written as itself. The existing Product and
UnitOfMeasure entries, and `ProductsApiTests.cs:150` and `:175`, already follow this rule, with the
hexadecimal digits in lower case; the new entries and test literals do the same.
`TurkishErrorMessagesTests` is the one exception: its expected texts use literal Turkish letters on
purpose (its class comment), so that its assertion proves the shipped escapes decode to the
intended characters (U-T1, P-T1).

| Code | Shown to the user | English `Error.Message` |
|---|---|---|
| `UnitOfMeasure.ConversionFactorTooPrecise` | Dönüşüm katsayısı en fazla 6 ondalık basamak içerebilir; sondaki sıfırlar sayılmaz. | `"A derived unit's conversion factor must have at most 6 decimal places, not counting trailing zeros."` |
| `UnitOfMeasure.ConversionFactorTooLarge` | Dönüşüm katsayısı en fazla 999.999.999.999,999999 olabilir. | `"A derived unit's conversion factor must not exceed 999999999999.999999."` |
| `Product.ListPriceAmountTooLarge` | Liste fiyatı en fazla 99.999.999.999.999,9999 olabilir. | `"List price amount must not exceed 99999999999999.9999."` |
| `Product.ReorderPointTooLarge` | Yeniden sipariş noktası en fazla 999.999.999.999,999999 olabilir. | `"Reorder point must not exceed 999999999999.999999."` |

Source form: in `TurkishErrorMessages.cs` and in the API tests, each message above is written as the rule above gives it, and the test placeholders `<TooPrecise literal>`, `<TooLarge literal>` and `<literal>` stand for that form.

## Phase 1: UnitOfMeasure — the conversion factor's precision and upper bound

Schema, migration, EF configuration and production queries: none. db-reviewer: not required.

### Files

- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs` — modified — two constants, and two
  checks in `Create`'s derived branch.
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasureErrors.cs` — modified — two errors.
- `src/Envanex.Application/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidator.cs` —
  modified — two rules in the derived-unit `When` block.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — two 400 entries.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — modified — two messages.
- `tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs` — modified — U-D1..U-D7.
- `tests/Envanex.Application.Tests/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidatorTests.cs`
  — modified — U-V1..U-V6.
- `tests/Envanex.Application.Tests/UnitOfMeasures/Commands/CreateUnitOfMeasureCommandHandlerTests.cs`
  — modified — U-H1, U-H2.
- `tests/Envanex.IntegrationTests/Persistence/ConversionFactorPersistenceTests.cs` — **created** —
  U-P1, U-P2.
- `tests/Envanex.IntegrationTests/Persistence/SchemaPrecisionTests.cs` — modified — U-S1..U-S3 and
  two private helpers.
- `tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs` — modified — U-A1..U-A4 and one
  private helper.
- `tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs` — modified — U-T1: a theory and
  its data member, with the two UnitOfMeasure rows.
- `tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified — U-M1: a theory and its
  data member, with the two UnitOfMeasure rows.

### Signatures

`UnitOfMeasure` — directly after `NameMaxLength` (line 8), with no comment, as for
`CodeMaxLength`. The domain does not name a column.

```csharp
public const decimal ConversionFactorMaxValue = 999999999999.999999m;
public const int ConversionFactorDecimalPlaces = 6;

public static Result<UnitOfMeasure> Create(string code, string name, Guid? baseUnitId, decimal conversionFactor); // unchanged
```

`Create`'s derived branch (the `else` block, lines 65-76) after this phase. Each check is its own
`if` returning `Result.Failure<UnitOfMeasure>(...)`, like its neighbours, in this order:

| # | Rejects when | Error |
|---|---|---|
| 1 | `baseUnitId.Value == Guid.Empty` (unchanged) | `InvalidBaseUnitId` |
| 2 | `conversionFactor <= 0m` (unchanged) | `ConversionFactorMustBePositive` |
| 3 | `conversionFactor` differs by value from `Math.Round(conversionFactor, ConversionFactorDecimalPlaces, MidpointRounding.ToEven)` | `ConversionFactorTooPrecise` |
| 4 | `conversionFactor > ConversionFactorMaxValue` | `ConversionFactorTooLarge` |

The base branch (`conversionFactor != 1m`) is unchanged and gets neither new check. The factor is
passed to the constructor as received: no rounding and no scale normalization. Check 3 compares
values, so trailing zeros (`1.500000000`) pass. The midpoint mode only keeps the style of
`Money`/`Quantity`; it cannot change the outcome.

`UnitOfMeasureErrors` — directly after `ConversionFactorMustBePositive` (line 10), in this order:

```csharp
public static readonly Error ConversionFactorTooPrecise = new("UnitOfMeasure.ConversionFactorTooPrecise", "A derived unit's conversion factor must have at most 6 decimal places, not counting trailing zeros.");
public static readonly Error ConversionFactorTooLarge = new("UnitOfMeasure.ConversionFactorTooLarge", "A derived unit's conversion factor must not exceed 999999999999.999999.");
```

`CreateUnitOfMeasureCommandValidator`: inside the existing `When(x => x.BaseUnitId is not null, ...)`
block, after the `GreaterThan(0m)` rule, add two separate `RuleFor(x => x.ConversionFactor)`
statements:

1. `.Must(...)` with an inline lambda, and no helper method, so that deleting the rule in MU1 leaves
   no unused member. The lambda is true when the value equals
   `Math.Round(value, UnitOfMeasure.ConversionFactorDecimalPlaces, MidpointRounding.ToEven)`.
   Then `.WithErrorCode(UnitOfMeasureErrors.ConversionFactorTooPrecise.Code)`.
2. `.LessThanOrEqualTo(UnitOfMeasure.ConversionFactorMaxValue)`, then
   `.WithErrorCode(UnitOfMeasureErrors.ConversionFactorTooLarge.Code)`.

No `.WithMessage`, and the base-unit `When` block is unchanged.

Tables. Add each entry directly after `"UnitOfMeasure.ConversionFactorMustBePositive"`, TooPrecise
first:

- `ResultExtensions.StatusCodeMap` (after line 71), as `= StatusCodes.Status400BadRequest`.
- `TurkishErrorMessages.Messages` (after line 49), with the texts of the message table above, in
  its source form.

Test-side signatures:

- `UnitOfMeasureTests`:
  - `public static TheoryData<decimal> RepresentableFactorsWithinMaximum`
  - `public static TheoryData<decimal> FactorsBeyondSixDecimalPlaces`
  - `public static TheoryData<decimal> FactorsTooPreciseAndAboveMaximum`
- `CreateUnitOfMeasureCommandValidatorTests`: the same three members.
- `ConversionFactorPersistenceTests`:
  - `[Collection(DatabaseCollection.Name)] public sealed class ConversionFactorPersistenceTests : IAsyncLifetime`,
    shaped like `MoneyPersistenceTests.cs:10-18`;
  - `public static TheoryData<decimal> BoundaryFactors`;
  - `private async Task<decimal> SaveDerivedUnitAndReadFactorAsync(decimal factor)`. It seeds base
    unit `KG` in one context and saves `UnitOfMeasure.Create("GR", "Gram", baseId, factor).Value` in
    a second. It returns `ConversionFactor` as read in a third, fresh context.
- `SchemaPrecisionTests`:
  - `private static decimal PowerOfTen(int exponent)`: multiplies `1m` by `10m` `exponent` times.
    No `double`, no `Math.Pow`.
  - `private static decimal ColumnMaximum(int precision, int scale)`: returns
    `(PowerOfTen(precision) - 1m) / PowerOfTen(scale)`, which is exact in `decimal`.
  - Add `using Envanex.Domain.Aggregates.UnitOfMeasures;`.
- `UnitOfMeasuresApiTests`:
  - `public static TheoryData<decimal> ValidDerivedFactors`;
  - `private static async Task<string[]> ReadFieldValidationMessagesAsync(HttpResponseMessage response, string fieldName)`.
    It asserts, in this order: the status is `HttpStatusCode.BadRequest`; then it reads the body as
    `JsonElement`; `title` is `"Validation Failed"`; `TryGetProperty("errors", ...)` is true;
    `errors.TryGetProperty(fieldName, ...)` is true. It returns the array's strings. The status
    assertion comes before any JSON read, so a non-JSON 500 under a mutation fails on a Shouldly
    assertion, not on a parse.
- `TurkishErrorMessagesTests`: `public static TheoryData<string, string> NumericFieldMessages`; add
  `using Envanex.Domain.Aggregates.UnitOfMeasures;`.
- `ResultExtensionsTests`: `public static TheoryData<string> NumericFieldErrorCodes`; add
  `using Envanex.Domain.Aggregates.UnitOfMeasures;`.

### Tests to add

Assertions on error codes compare the exact ordered list for the field:
`result.Errors.Where(e => e.PropertyName == "ConversionFactor").Select(e => e.ErrorCode).ToArray()`.
Use `ShouldBe([...])` with literal code strings, as in `ArchitectureTests.cs:87`. Use string
literals, not `.Code`, so the test pins the contract value.

`UnitOfMeasureTests` (Domain.Tests). Every derived case uses `Guid.CreateVersion7()` as the base id.

- **U-D1** `[Theory]` `Create_DerivedUnit_WithRepresentableFactorWithinMaximum_ShouldSucceed(decimal factor)`.
  - Rows: `0.000001m`, `0.0000010m`, `1.500000000m`, `999999999999.999998m`, `999999999999.999999m`.
  - Asserts: success, and `ConversionFactor.ShouldBe(factor)`.
- **U-D2** `[Theory]` `Create_DerivedUnit_WithFactorBeyondSixDecimalPlaces_ShouldFailWithTooPrecise(decimal factor)`.
  - Rows: `0.0000001m`, `0.0000005m`, `0.1234567m`.
  - Asserts: `Error` is `UnitOfMeasureErrors.ConversionFactorTooPrecise`.
- **U-D3** `[Fact]` `Create_DerivedUnit_WithFactorAboveMaximum_ShouldFailWithTooLarge`.
  - Input: `1000000000000.000000m`.
  - Asserts: `ConversionFactorTooLarge`.
- **U-D4** `[Theory]` `Create_DerivedUnit_WithFactorTooPreciseAndAboveMaximum_ShouldFailWithTooPrecise(decimal factor)`.
  - Rows: `1000000000000.0000001m`, `999999999999.9999994m`.
  - Asserts: `ConversionFactorTooPrecise`. This is the check-order case.
- **U-D5** `[Fact]` `Create_DerivedUnit_WithNegativeFactorBeyondSixDecimalPlaces_ShouldFailWithMustBePositive`.
  - Input: `-0.0000001m`.
  - Asserts: `ConversionFactorMustBePositive`, because positivity is checked first.
- **U-D6** `[Fact]` `Create_BaseUnit_WithOneAndTrailingZeros_ShouldSucceed`.
  - Input: `null` base, `1.000000000m`.
  - Asserts: success, and `ConversionFactor.ShouldBe(1m)`.
- **U-D7** `[Fact]` `Create_BaseUnit_WithFactorBeyondSixDecimalPlaces_ShouldFailWithBaseUnitFactorMustBeOne`.
  - Input: `null` base, `1.0000001m`.
  - Asserts: `BaseUnitFactorMustBeOne`. The base branch keeps only its own rule.

`CreateUnitOfMeasureCommandValidatorTests` (Application.Tests). Derived cases use
`ValidDerivedUnitCommand with { ConversionFactor = ... }`.

- **U-V1** `[Theory]` `Validate_DerivedUnitWithRepresentableFactorWithinMaximum_ShouldPass(decimal factor)`.
  - Rows: as U-D1. These cover just below and at the maximum.
  - Asserts: `IsValid` is true.
- **U-V2** `[Theory]` `Validate_DerivedUnitWithFactorBeyondSixDecimalPlaces_ShouldFailWithTooPrecise(decimal factor)`.
  - Rows: as U-D2.
  - Asserts: the field's codes are exactly `["UnitOfMeasure.ConversionFactorTooPrecise"]`.
- **U-V3** `[Fact]` `Validate_DerivedUnitWithFactorAboveMaximum_ShouldFailWithTooLarge`.
  - Input: `1000000000000.000000m`, just above the maximum.
  - Asserts: exactly `["UnitOfMeasure.ConversionFactorTooLarge"]`.
- **U-V4** `[Theory]` `Validate_DerivedUnitWithFactorTooPreciseAndAboveMaximum_ShouldReportBothFailures(decimal factor)`.
  - Rows: as U-D4.
  - Asserts: exactly `["UnitOfMeasure.ConversionFactorTooPrecise", "UnitOfMeasure.ConversionFactorTooLarge"]`.
- **U-V5** `[Fact]` `Validate_DerivedUnitWithNegativeFactorBeyondSixDecimalPlaces_ShouldReportPositivityAndPrecision`.
  - Input: `-0.0000001m`.
  - Asserts: exactly `["UnitOfMeasure.ConversionFactorMustBePositive", "UnitOfMeasure.ConversionFactorTooPrecise"]`.
- **U-V6** `[Fact]` `Validate_BaseUnitWithOneAndTrailingZeros_ShouldPass`.
  - Input: `ValidBaseUnitCommand with { ConversionFactor = 1.000000000m }`.
  - Asserts: `IsValid` is true.

`CreateUnitOfMeasureCommandHandlerTests` (Application.Tests, the undecorated handler). Each test
calls `_repository.SeedActiveStatus(baseId, true)` and sends
`new CreateUnitOfMeasureCommand("GR", "Gram", baseId, <factor>)`. Each asserts:
`IsFailure`; the `Error`; `_unitOfWork.SaveChangesCalled.ShouldBe(0)`;
`_repository.Units.Count.ShouldBe(0)`.

- **U-H1** `HandleAsync_WithDerivedFactorBeyondSixDecimalPlaces_ShouldReturnTooPreciseWithoutSaving`
  (`0.0000001m`; `UnitOfMeasureErrors.ConversionFactorTooPrecise`).
- **U-H2** `HandleAsync_WithDerivedFactorAboveMaximum_ShouldReturnTooLargeWithoutSaving`
  (`1000000000000m`; `UnitOfMeasureErrors.ConversionFactorTooLarge`).

`ConversionFactorPersistenceTests` (IntegrationTests, through the domain and a `DbContext`):

- **U-P1** `[Theory]` `DerivedUnit_WithBoundaryFactor_ShouldRoundTripExactly(decimal factor)`.
  - Rows: `0.000001m`, `999999999999.999999m`.
  - Asserts: the re-read value `ShouldBe(factor)`.
- **U-P2** `[Fact]` `DerivedUnit_WithFactorWithTrailingZeros_ShouldRoundTripByValue`.
  - Input: `1.500000000m`.
  - Asserts: the re-read value `ShouldBe(1.5m)`.
  - Added beyond the required persistence cases (U-P1, P-P1, P-P2): the domain accepts trailing
    zeros (D2), and no other test shows that such a factor persists.

`SchemaPrecisionTests` (IntegrationTests). Each test runs its own inline query, in the style of
`:22-30`: `NUMERIC_PRECISION AS Precision, NUMERIC_SCALE AS Scale` with literal `TABLE_NAME` and
`COLUMN_NAME`. The existing four tests are unchanged.

- **U-S1** `ConversionFactorColumn_ShouldBeDecimal18_6`.
  - Reads `UnitOfMeasures.ConversionFactor`.
  - Asserts: `Precision` is `(byte)18` and `Scale` is `6`.
- **U-S2** `ConversionFactorMaxValue_ShouldEqualTheColumnMaximum`.
  - Reads the same column.
  - Asserts: `UnitOfMeasure.ConversionFactorMaxValue.ShouldBe(ColumnMaximum(result.Precision, result.Scale))`.
- **U-S3** `ConversionFactorDecimalPlaces_ShouldEqualTheColumnScale`.
  - Asserts: `UnitOfMeasure.ConversionFactorDecimalPlaces.ShouldBe(result.Scale)`.

`UnitOfMeasuresApiTests` (IntegrationTests). Each test first calls `SeedBaseUnitViaApiAsync()`, so
a validator-free request would reach the domain rather than the 422 base-unit checks. Each then
POSTs `new CreateUnitOfMeasureCommand("GR", "Gram", baseId, <factor>)` to `/api/unit-of-measures`.

- **U-A1** `CreateUnitOfMeasure_WithDerivedFactorBeyondSixDecimalPlaces_ShouldReturn400WithTurkishMessage`.
  - Input: `0.0000001m`.
  - Asserts: `ReadFieldValidationMessagesAsync(response, "ConversionFactor")` returns exactly
    `[<TooPrecise literal>]`.
- **U-A2** `CreateUnitOfMeasure_WithDerivedFactorAboveMaximum_ShouldReturn400WithTurkishMessage`.
  - Input: `1000000000000m`.
  - Asserts: exactly `[<TooLarge literal>]`.
- **U-A3** `CreateUnitOfMeasure_WithDerivedFactorTooPreciseAndAboveMaximum_ShouldReturn400WithBothTurkishMessages`.
  - Input: `1000000000000.0000001m`.
  - Asserts: exactly `[<TooPrecise literal>, <TooLarge literal>]`.
- **U-A4** `[Theory]` `CreateUnitOfMeasure_WithValidDerivedFactor_ShouldReturn201AndGetShouldReturnIt(decimal factor)`.
  - Rows: `0.000001m`, `999999999999.999999m`, `1.500000000m`.
  - Asserts:
    - the POST is `HttpStatusCode.Created`;
    - `GET /api/unit-of-measures/{id}` is `OK`;
    - `conversionFactor` read with `GetDecimal()` `ShouldBe(factor)`, by value;
    - `baseUnitId` read with `GetGuid()` `ShouldBe(baseId)`.
  - This is the first API test that creates a derived unit successfully.

`TurkishErrorMessagesTests` (IntegrationTests, in memory, no collection). This is the transcription
guard: the expected texts use literal Turkish letters, so the assertion proves that the shipped
escapes decode to the intended characters.

- **U-T1** `[Theory]` `NumericFieldMessage_ShouldBeExactlyTheShownText(string errorCode, string expected)`,
  in the class's style.
  - Data: `NumericFieldMessages`. Each row carries two strings, so xUnit enumerates every row as
    its own test case: the code, taken from its declared `Error` field as the class does with
    `AuthErrors.InvalidCredentials.Code`, and the exact "Shown to the user" text from the message
    table, written with literal Turkish letters.
  - Phase 1 rows: `UnitOfMeasureErrors.ConversionFactorTooPrecise.Code` and
    `UnitOfMeasureErrors.ConversionFactorTooLarge.Code`, each with its text.
  - Asserts: `TurkishErrorMessages.GetMessage(errorCode, Fallback).ShouldBe(expected)`.

`ResultExtensionsTests` (IntegrationTests, in memory). `ResultMappingTests` pins that each code is
mapped, not to which status: a wrong value such as 422 or 500 would pass it, and the API tests stop
at the validator before any of these codes is mapped. This theory pins the 400.

- **U-M1** `[Theory]` `ToActionResult_NumericFieldErrorCode_ShouldMapTo400(string errorCode)`, in the
  style of `UnmappedErrorCode_ShouldFallBackTo400NotServerError`.
  - Data: `NumericFieldErrorCodes`. Rows carry the codes as strings, as `AuthErrorCodes` does, so
    each row is its own test case. Each code is taken from its declared `Error` field, not retyped.
  - Phase 1 rows: `UnitOfMeasureErrors.ConversionFactorTooPrecise.Code` and
    `UnitOfMeasureErrors.ConversionFactorTooLarge.Code`.
  - It maps `Result.Failure<Guid>(new Error(errorCode, "English fallback message."))` through
    `ToActionResult()`, as `ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401` builds its
    failure.
  - Asserts: the result is an `ObjectResult` whose `StatusCode` is 400, and its `Value` is a
    `ProblemDetails` whose `Status` is 400.

Count deltas against the previous full run: Domain.Tests +14, Application.Tests +15,
IntegrationTests +16.

### Validation

Prerequisite: Docker running (Testcontainers). Run from the repository root and paste the raw
output.

```bash
dotnet build -warnaserror
dotnet test tests/Envanex.Domain.Tests --filter FullyQualifiedName~Envanex.Domain.Tests.Aggregates.UnitOfMeasures.UnitOfMeasureTests
dotnet test tests/Envanex.Application.Tests --filter FullyQualifiedName~Envanex.Application.Tests.UnitOfMeasures
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.UnitOfMeasuresApiTests
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Persistence
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.ResultMappingTests
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.TurkishErrorMessagesTests
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.ResultExtensionsTests
dotnet format --verify-no-changes
dotnet test
git status --short
```

Expected:

- The build has 0 warnings and 0 errors.
- Every `dotnet test` reports `Failed: 0`. The full run shows the deltas above. Record the
  `Envanex.IntegrationTests.dll` duration line.
- `dotnet format` exits 0.
- `git status --short` is the coder's check, run before the human's commit. It lists exactly the
  thirteen Files above: twelve ` M` and one `??`. Nothing under `src/Envanex.Infrastructure/`
  appears. Plan or review files may also appear if not yet committed.
- The tester's run of `git status --short` prints nothing, because the tester runs on a clean tree.
  The phase's changed files come from the `files` section of `gates.txt`.

Grep tool checks:

- `WithErrorCode\(UnitOfMeasureErrors\.ConversionFactorTooPrecise\.Code\)` and
  `WithErrorCode\(UnitOfMeasureErrors\.ConversionFactorTooLarge\.Code\)`: one match each, in
  `CreateUnitOfMeasureCommandValidator.cs`.
- `\.WithMessage\(` in `src/Envanex.Application`: no match.
- `\r` in every changed and created file: no match (LF only).
- In `src/Envanex.Web/Extensions/TurkishErrorMessages.cs`, no Product or UnitOfMeasure entry
  contains a non-ASCII character. The pattern
  `"(Product|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]`
  has no match. Today it has none; only the Auth entries contain literal Turkish letters.
- `[^\x00-\x7F]` in `tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs`: no match. The
  file is ASCII-only today.

Stop conditions. The coder stops without `PHASE_COMPLETE` and reports in each of these cases:

- U-P1, U-P2 or the `1.500000000m` row of U-A4 fails at `SaveChanges` or reads back a different
  value. The coder reports what it observed without prejudging the cause, and the human decides.
- Any step seems to need an Infrastructure, EF or migration change (D1).

## Phase 2: Product — domain bounds, the list-price sign, `UpdatePrice` returns `Result`

Schema, migration, EF configuration and production queries: none. db-reviewer: not required.

### Files

- `src/Envanex.Domain/Aggregates/Products/Product.cs` — modified — two constants. Three checks
  each in `Create` and `Update`, two in `UpdatePrice`. `UpdatePrice` returns `Result`.
- `src/Envanex.Domain/Aggregates/Products/ProductErrors.cs` — modified — two errors.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — two entries added, and
  `ListPriceAmountNegative` moved.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — modified — the same.
- `tests/Envanex.Domain.Tests/Aggregates/Products/ProductTests.cs` — modified — P-D1..P-D21, one
  private helper, and the `UpdatePrice` success assertions.
- `tests/Envanex.Application.Tests/Products/Commands/CreateProductCommandHandlerTests.cs` — modified — P-HC1..P-HC3.
- `tests/Envanex.Application.Tests/Products/Commands/UpdateProductCommandHandlerTests.cs` — modified — P-HU1..P-HU3.
- `tests/Envanex.IntegrationTests/Persistence/MoneyPersistenceTests.cs` — modified — P-P1.
- `tests/Envanex.IntegrationTests/Persistence/QuantityPersistenceTests.cs` — modified — P-P2.
- `tests/Envanex.IntegrationTests/Persistence/SchemaPrecisionTests.cs` — modified — P-S1, P-S2.
- `tests/Envanex.IntegrationTests/Persistence/RepositoryTests.cs` — modified — the `UpdatePrice`
  success assertions, six calls.
- `tests/Envanex.IntegrationTests/Persistence/ConcurrencyTests.cs` — modified — the `UpdatePrice`
  success assertions, two calls.
- `tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs` — modified — P-T1: three Product
  rows in Phase 1's theory.
- `tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified — P-M1: three Product rows
  in Phase 1's theory.

### Signatures

`Product` — directly after `NameMaxLength` (line 9), with no comment:

```csharp
public const decimal ListPriceAmountMaxValue = 99999999999999.9999m;
public const decimal ReorderPointMaxValue = 999999999999.999999m;

public static Result<Product> Create(string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint); // unchanged
public Result Update(string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint);                            // unchanged
public Result UpdatePrice(Money newPrice);                                                                                   // was: public void UpdatePrice(Money newPrice)
```

New checks. Each is its own `if` returning a failure, like its neighbours, in this order:

| # | Rejects when | Error | In |
|---|---|---|---|
| 1 | `<money>.Amount < 0m` (numeric; no `decimal.IsNegative`) | `ProductErrors.ListPriceAmountNegative` | `Create`, `Update`, `UpdatePrice` |
| 2 | `<money>.Amount > ListPriceAmountMaxValue` | `ProductErrors.ListPriceAmountTooLarge` | `Create`, `Update`, `UpdatePrice` |
| 3 | `reorderPoint.Value > ReorderPointMaxValue` | `ProductErrors.ReorderPointTooLarge` | `Create`, `Update` |

Placement:

- **`Create`:** after the `unitOfMeasureId == Guid.Empty` check (lines 61-64), before
  `normalizedCode` (line 66). It returns `Result.Failure<Product>(...)`.
- **`Update`:** after its `unitOfMeasureId == Guid.Empty` check (lines 103-106), before
  `Name = name.Trim();` (line 108). It returns `Result.Failure(...)`. All four assignments stay
  after every check.
- **`UpdatePrice`:**
  1. `ArgumentNullException.ThrowIfNull(newPrice);` stays first.
  2. Checks 1 and 2, on `newPrice.Amount`.
  3. `ListPrice = newPrice;`.
  4. `return Result.Success();`.

`ProductErrors`. Insert `ListPriceAmountTooLarge` directly after `ListPriceAmountNegative`
(line 19), and `ReorderPointTooLarge` directly after `ReorderPointNegative`:

```csharp
public static readonly Error ListPriceAmountTooLarge = new("Product.ListPriceAmountTooLarge", "List price amount must not exceed 99999999999999.9999.");
public static readonly Error ReorderPointTooLarge = new("Product.ReorderPointTooLarge", "Reorder point must not exceed 999999999999.999999.");
```

Tables, `ResultExtensions.StatusCodeMap`:

- The "400 Bad Request - Product domain" group becomes `CodeRequired`, `NameRequired`,
  `UnitOfMeasureRequired`, `CodeTooLong`, `NameTooLong`, **`ListPriceAmountNegative`** (moved),
  **`ListPriceAmountTooLarge`**, **`ReorderPointTooLarge`**.
- The "400 Bad Request - Product validator" group becomes `IdRequired`, `RowVersionRequired`,
  `ReorderPointNegative`, `ListPriceCurrencyRequired`.
- All entries are `StatusCodes.Status400BadRequest`.

Tables, `TurkishErrorMessages.Messages`:

- In "Product - domain", directly after `Product.NameTooLong` (line 30), insert three entries before
  `Product.NotFound`:
  - `Product.ListPriceAmountNegative`, moved byte for byte. Its source stays as it is today at
    `TurkishErrorMessages.cs:40`: `"Liste fiyat`, the escape for ı (0131), then a space and
    `negatif olamaz."`. The Grep check below keeps its source byte for byte, and P-T1's
    `Product.ListPriceAmountNegative` row pins its runtime text.
  - then `Product.ListPriceAmountTooLarge`;
  - then `Product.ReorderPointTooLarge`, with the texts of the message table, in its source form.
- "Product - validator" loses the `ListPriceAmountNegative` line. Its other entries are unchanged.

Test-side signatures:

- `ProductTests`: `private static void ShouldBeUnchanged(Product product, Guid originalId)`. It
  asserts each property of a product made by `Product.Create("SKU001", "Widget",
  ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint)`:
  - `Id == originalId`, `Code == "SKU001"`, `Name == "Widget"`;
  - `UnitOfMeasureId == ValidUnitOfMeasureId`;
  - `ListPrice == DefaultPrice` and `ReorderPoint == DefaultReorderPoint`, by record equality;
  - `IsActive` is true, and `DomainEvents` is empty.
- `MoneyPersistenceTests`: `public static TheoryData<decimal> AmountsAtOrRoundingToTheMaximum`.
- `QuantityPersistenceTests`: `public static TheoryData<decimal> ReorderPointsAtOrRoundingToTheMaximum`.
- `SchemaPrecisionTests`: add `using Envanex.Domain.Aggregates.Products;`. Reuse `ColumnMaximum`
  from Phase 1.
- `TurkishErrorMessagesTests` and `ResultExtensionsTests`: add
  `using Envanex.Domain.Aggregates.Products;`.

### Tests to add

`ProductTests` (Domain.Tests). Prices are `Money.Of(x, Currency.TRY).Value` and quantities
`Quantity.Of(x).Value`, already rounded as the handlers produce them. Accepted-value tests assert
the stored value against a literal. Zero is asserted with `ShouldBe(0m)`, which is numeric
equality. Never assert `decimal.IsNegative`, `ToString()`, `decimal.GetBits` or `Scale`.

| ID | Test | Input | Asserts |
|---|---|---|---|
| P-D1 | `Create_WithListPriceAmountAtMaximum_ShouldSucceed` | `Money.Of(99999999999999.9999m)` | success; `ListPrice.Amount` is `99999999999999.9999m` |
| P-D2 | `Create_WithListPriceAmountRoundedDownToMaximum_ShouldSucceedWithMaximum` | `Money.Of(99999999999999.99994m)` | success; Amount is `99999999999999.9999m` |
| P-D3 | `Create_WithListPriceAmountRoundedUpAboveMaximum_ShouldFail` | `Money.Of(99999999999999.99995m)` | `ListPriceAmountTooLarge` |
| P-D4 | `Create_WithNegativeListPriceAmount_ShouldFail` | `Money.Of(-0.0001m)` | `ListPriceAmountNegative` |
| P-D5 | `Create_WithNegativeListPriceAmountRoundedToZero_ShouldSucceedWithZero` | `Money.Of(-0.00004m)` | success; Amount `ShouldBe(0m)` |
| P-D6 | `Create_WithReorderPointAtMaximum_ShouldSucceed` | `Quantity.Of(999999999999.999999m)` | success; `ReorderPoint.Value` is `999999999999.999999m` |
| P-D7 | `Create_WithReorderPointRoundedDownToMaximum_ShouldSucceedWithMaximum` | `Quantity.Of(999999999999.9999994m)` | success; Value is `999999999999.999999m` |
| P-D8 | `Create_WithReorderPointRoundedUpAboveMaximum_ShouldFail` | `Quantity.Of(999999999999.9999995m)` | `ReorderPointTooLarge` |
| P-D9 | `Update_WithListPriceAmountAtMaximum_ShouldSucceed` | as P-D1 | success; Amount is M4 |
| P-D10 | `Update_WithListPriceAmountRoundedDownToMaximum_ShouldSucceedWithMaximum` | as P-D2 | success; Amount is M4 |
| P-D11 | `Update_WithListPriceAmountRoundedUpAboveMaximum_ShouldFailAndChangeNothing` | name `"Changed Name"`, a new `Guid.CreateVersion7()`, price as P-D3, `Quantity.Of(20m)` | `ListPriceAmountTooLarge`; `ShouldBeUnchanged` |
| P-D12 | `Update_WithNegativeListPriceAmount_ShouldFailAndChangeNothing` | as P-D11, price `Money.Of(-0.0001m)` | `ListPriceAmountNegative`; `ShouldBeUnchanged` |
| P-D13 | `Update_WithNegativeListPriceAmountRoundedToZero_ShouldSucceedWithZero` | as P-D5 | success; Amount `ShouldBe(0m)` |
| P-D14 | `Update_WithReorderPointAtMaximum_ShouldSucceed` | as P-D6 | success; Value is M6 |
| P-D15 | `Update_WithReorderPointRoundedDownToMaximum_ShouldSucceedWithMaximum` | as P-D7 | success; Value is M6 |
| P-D16 | `Update_WithReorderPointRoundedUpAboveMaximum_ShouldFailAndChangeNothing` | name `"Changed Name"`, a new id, `Money.Of(200.75m)`, quantity as P-D8 | `ReorderPointTooLarge`; `ShouldBeUnchanged` |
| P-D17 | `UpdatePrice_WithListPriceAmountAtMaximum_ShouldSucceed` | as P-D1 | success; Amount is M4 |
| P-D18 | `UpdatePrice_WithListPriceAmountRoundedDownToMaximum_ShouldSucceedWithMaximum` | as P-D2 | success; Amount is M4 |
| P-D19 | `UpdatePrice_WithListPriceAmountRoundedUpAboveMaximum_ShouldFailAndChangeNothing` | as P-D3 | `ListPriceAmountTooLarge`; `ShouldBeUnchanged` |
| P-D20 | `UpdatePrice_WithNegativeListPriceAmount_ShouldFailAndChangeNothing` | `Money.Of(-0.0001m)` | `ListPriceAmountNegative`; `ShouldBeUnchanged` |
| P-D21 | `UpdatePrice_WithNegativeListPriceAmountRoundedToZero_ShouldSucceedWithZero` | as P-D5 | success; Amount `ShouldBe(0m)` |

Notes on these tests:

- **Call shapes.** Each test changes only the argument under test and keeps the class's defaults
  for the rest.
  - `Create`: `Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, <price or DefaultPrice>, <quantity or DefaultReorderPoint>)`.
  - Successful `Update`, list-price cases: `product.Update("Widget", ValidUnitOfMeasureId, <price>, DefaultReorderPoint)`.
  - Successful `Update`, reorder-point cases: `product.Update("Widget", ValidUnitOfMeasureId, DefaultPrice, <quantity>)`.
- Each failing `Update` passes a value that differs from the current one for every other argument,
  so a partial update is caught by `ShouldBeUnchanged`.
- The failure tests also assert `IsFailure` before they assert the `Error`.
- `Update` and `UpdatePrice` tests start from `Product.Create("SKU001", "Widget",
  ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value` and capture `product.Id` first.

The `UpdatePrice` success assertions (the existing callers now check the returned `Result`):

- **`ProductTests.UpdatePrice_ShouldChangeListPrice`:** keep the returned `Result` and assert
  `IsSuccess.ShouldBeTrue()` before the existing `ListPrice` assertion.
- **`UpdatePrice_WithNull_ShouldThrow`:** unchanged. The lambda now returns `Result` and binds to
  Shouldly's `Func<object?>` overload, as `Update_WithNullListPrice_ShouldThrow` already does.
- **`RepositoryTests` and `ConcurrencyTests`:** each of the eight `UpdatePrice(...)` calls (lines
  above) becomes `<call>.IsSuccess.ShouldBeTrue();`. Both files already import Shouldly.

Handler tests (Application.Tests, undecorated handlers). Each asserts: `IsFailure`; the `Error`;
`_unitOfWork.SaveChangesCalled.ShouldBe(0)`; and the `Products.Count`.

- **`CreateProductCommandHandlerTests`.** The command is `ValidCommand() with { ... }`, and the test
  asserts `_productRepository.Products.Count.ShouldBe(0)`.
  - **P-HC1** `HandleAsync_WithListPriceAmountRoundingAboveMaximum_ShouldReturnListPriceAmountTooLargeWithoutSaving`
    (`ListPriceAmount = 99999999999999.99995m`).
  - **P-HC2** `HandleAsync_WithNegativeListPriceAmount_ShouldReturnListPriceAmountNegativeWithoutSaving`
    (`-0.0001m`).
  - **P-HC3** `HandleAsync_WithReorderPointRoundingAboveMaximum_ShouldReturnReorderPointTooLargeWithoutSaving`
    (`ReorderPoint = 999999999999.9999995m`).
- **`UpdateProductCommandHandlerTests`** (P-HU1..P-HU3).
  - Same three names and inputs, after `SeedProduct()`, via
    `new UpdateProductCommand(product.Id, "Updated Name", ActiveUnitOfMeasureId, <amount>, "TRY", <reorder>, RowVersion)`.
  - The valid defaults are `200m` and `20m`.
  - Each also asserts `_productRepository.Products.Count.ShouldBe(1)` and
    `_productRepository.SetOriginalRowVersionCallCount.ShouldBe(0)`.

Persistence (IntegrationTests, through `Product.Create` and a `DbContext`, never a validator; in the
style of the existing test in each class):

- **P-P1** `MoneyPersistenceTests` `[Theory]` `SaveAndLoad_WithAmountAtOrRoundingToTheMaximum_ShouldStoreTheMaximum(decimal amount)`.
  - Rows: `99999999999999.9999m`, `99999999999999.99994m`.
  - The product is built with `Money.Of(amount, Currency.TRY).Value`.
  - Asserts: the re-read `ListPrice.Amount` is `99999999999999.9999m`.
- **P-P2** `QuantityPersistenceTests` `[Theory]` `SaveAndLoad_WithReorderPointAtOrRoundingToTheMaximum_ShouldStoreTheMaximum(decimal value)`.
  - Rows: `999999999999.999999m`, `999999999999.9999994m`.
  - Asserts: the re-read `ReorderPoint.Value` is `999999999999.999999m`.

Schema (`SchemaPrecisionTests`, inline queries as in Phase 1):

- **P-S1** `ListPriceAmountMaxValue_ShouldEqualTheColumnMaximum`.
  - Reads `Products.ListPrice_Amount`.
  - Asserts: `Product.ListPriceAmountMaxValue.ShouldBe(ColumnMaximum(...))`.
- **P-S2** `ReorderPointMaxValue_ShouldEqualTheColumnMaximum`.
  - Reads `Products.ReorderPoint`.
  - Asserts: `Product.ReorderPointMaxValue.ShouldBe(ColumnMaximum(...))`.

Phase 1's two theories get the Product rows. Codes are taken from their declared `Error` fields;
the expected texts are written with literal Turkish letters.

- **P-T1** (`TurkishErrorMessagesTests.NumericFieldMessages`), three rows:
  - `ProductErrors.ListPriceAmountTooLarge.Code` with "Liste fiyatı en fazla 99.999.999.999.999,9999 olabilir.";
  - `ProductErrors.ReorderPointTooLarge.Code` with "Yeniden sipariş noktası en fazla 999.999.999.999,999999 olabilir.";
  - `ProductErrors.ListPriceAmountNegative.Code` with "Liste fiyatı negatif olamaz.", the moved
    entry's runtime text.
- **P-M1** (`ResultExtensionsTests.NumericFieldErrorCodes`), three rows:
  `ProductErrors.ListPriceAmountTooLarge.Code`, `ProductErrors.ReorderPointTooLarge.Code` and
  `ProductErrors.ListPriceAmountNegative.Code`.

Count deltas: Domain.Tests +21, Application.Tests +6, IntegrationTests +12.

### Validation

```bash
dotnet build -warnaserror
dotnet test tests/Envanex.Domain.Tests --filter FullyQualifiedName~Envanex.Domain.Tests.Aggregates.Products.ProductTests
dotnet test tests/Envanex.Application.Tests --filter FullyQualifiedName~Envanex.Application.Tests.Products.Commands
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Persistence
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.ResultMappingTests
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.TurkishErrorMessagesTests
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.ResultExtensionsTests
dotnet format --verify-no-changes
dotnet test
git status --short
```

Expected:

- The build has 0 warnings and 0 errors.
- Every `dotnet test` reports `Failed: 0`. The full run shows the deltas. Record the
  integration-suite duration.
- `git status --short` is the coder's check, run before the human's commit. It lists exactly the
  fourteen Files, all ` M`, and nothing under `src/Envanex.Infrastructure/`.
- The tester's run of `git status --short` prints nothing, because the tester runs on a clean tree.
  The phase's changed files come from the `files` section of `gates.txt`.

Grep tool checks:

- `Product\.ListPriceAmountNegative` in `src/Envanex.Web/Extensions`: exactly two matches, one per
  file. Each lies between the "Product domain" / "Product - domain" comment and the
  "Product validator" / "Product - validator" comment.
- `void UpdatePrice` in `src/`: no match.
- `\r` in every changed file: no match.
- In `src/Envanex.Web/Extensions/TurkishErrorMessages.cs`, no Product or UnitOfMeasure entry
  contains a non-ASCII character. The pattern
  `"(Product|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]`
  has no match, as in Phase 1.
- In the same file, exactly one line matches `Product\.ListPriceAmountNegative`. Its value reads
  `"Liste fiyat`, the escape for ı (0131), then a space and `negatif olamaz."`, as at `:40` today.

Stop condition: P-P1 or P-P2 does not read back the maximum exactly.

## Phase 3: Product — validator bounds and the API contract

Schema, migration, EF configuration and production queries: none. db-reviewer: not required.

### Files

- `src/Envanex.Application/Products/Validators/CreateProductCommandValidator.cs` — modified — two rules.
- `src/Envanex.Application/Products/Validators/UpdateProductCommandValidator.cs` — modified — two rules.
- `tests/Envanex.Application.Tests/Products/Validators/CreateProductCommandValidatorTests.cs` — modified — P-VC1..P-VC5.
- `tests/Envanex.Application.Tests/Products/Validators/UpdateProductCommandValidatorTests.cs` — modified — P-VU1..P-VU5.
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs` — modified — P-A1..P-A7 and one private helper.

### Signatures

Both validators get two new rules. Each is its own `RuleFor` statement. The existing literal rules
are unchanged.

- **The list-price rule.** It goes directly after the existing `ListPriceAmount` rule:
  `CreateProductCommandValidator.cs:31-33`, `UpdateProductCommandValidator.cs:27-29`.
  - `RuleFor(x => x.ListPriceAmount).LessThanOrEqualTo(Product.ListPriceAmountMaxValue)`
  - `.WithErrorCode(ProductErrors.ListPriceAmountTooLarge.Code)`
- **The reorder-point rule.** It goes directly after the existing `ReorderPoint` rule: `:43-45`
  and `:39-41`.
  - `RuleFor(x => x.ReorderPoint).LessThanOrEqualTo(Product.ReorderPointMaxValue)`
  - `.WithErrorCode(ProductErrors.ReorderPointTooLarge.Code)`
- Both files already import `Envanex.Domain.Aggregates.Products`.

Test-side signatures:

- In both validator test classes:
  - `public static TheoryData<decimal> ListPriceAmountsAtOrJustBelowMaximum`
  - `public static TheoryData<decimal> ListPriceAmountsAboveMaximum`
  - `public static TheoryData<decimal> ReorderPointsAtOrJustBelowMaximum`
  - `public static TheoryData<decimal> ReorderPointsAboveMaximum`
- `ProductsApiTests`: `private static async Task<string[]> ReadFieldValidationMessagesAsync(HttpResponseMessage response, string fieldName)`,
  exactly as specified for `UnitOfMeasuresApiTests` in Phase 1.

### Tests to add

The same five tests in `CreateProductCommandValidatorTests` (P-VC*) and
`UpdateProductCommandValidatorTests` (P-VU*), on raw values, as `ValidCommand with { ... }`. Codes
are compared as in Phase 1: the exact ordered list for the field, with literal strings.

- **P-VC1/P-VU1** `[Theory]` `Validate_WithListPriceAmountAtOrJustBelowMaximum_ShouldPass(decimal amount)`.
  - Rows: `99999999999999.9998m`, `99999999999999.9999m`.
  - Asserts: `IsValid` is true.
- **P-VC2/P-VU2** `[Theory]` `Validate_WithListPriceAmountAboveMaximum_ShouldFailWithListPriceAmountTooLarge(decimal amount)`.
  - Rows: `100000000000000.0000m` (just above), `99999999999999.99994m`, `99999999999999.99995m`.
  - Asserts: the `ListPriceAmount` codes are exactly `["Product.ListPriceAmountTooLarge"]`.
- **P-VC3/P-VU3** `[Fact]` `Validate_WithNegativeListPriceAmountThatRoundsToZero_ShouldFailWithListPriceAmountNegative`.
  - Input: `-0.00004m`.
  - Asserts: exactly `["Product.ListPriceAmountNegative"]`. This pins the raw `>= 0` side of D2.
- **P-VC4/P-VU4** `[Theory]` `Validate_WithReorderPointAtOrJustBelowMaximum_ShouldPass(decimal value)`.
  - Rows: `999999999999.999998m`, `999999999999.999999m`.
- **P-VC5/P-VU5** `[Theory]` `Validate_WithReorderPointAboveMaximum_ShouldFailWithReorderPointTooLarge(decimal value)`.
  - Rows: `1000000000000.000000m`, `999999999999.9999994m`, `999999999999.9999995m`.
  - Asserts: the `ReorderPoint` codes are exactly `["Product.ReorderPointTooLarge"]`.

`ProductsApiTests`. Each test seeds through the existing helpers, so that a validator-free request
would reach the domain.

- **POST tests:** `SeedUnitOfMeasureAsync()`, then POST `/api/products` with
  `new CreateProductCommand("BOUND-001", "Bound Product", uomId, <amount>, "TRY", <reorder>)`.
- **PUT tests:**
  1. `SeedUnitOfMeasureAsync()`, `SeedProductViaApiAsync(uomId)` and `GetProductDetailAsync`.
  2. PUT `/api/products/{id}` with `new UpdateProductCommand(productId, "Updated", uomId, <amount>, "TRY", <reorder>, Convert.FromBase64String(rowVersion))`.
- The valid defaults are `100m` and `10m`. Each test in the table below asserts
  `ReadFieldValidationMessagesAsync(response, <field>)` equals exactly `[<literal>]`.

| ID | Test | Request | Field |
|---|---|---|---|
| P-A1 | `CreateProduct_WithListPriceAmountAboveMaximum_ShouldReturn400WithTurkishMessage` | POST, `ListPriceAmount = 100000000000000m` | `ListPriceAmount` |
| P-A2 | `CreateProduct_WithListPriceAmountRoundingToTheMaximum_ShouldReturn400WithTurkishMessage` | POST, `99999999999999.99994m` | `ListPriceAmount` |
| P-A3 | `CreateProduct_WithReorderPointAboveMaximum_ShouldReturn400WithTurkishMessage` | POST, `ReorderPoint = 1000000000000m` | `ReorderPoint` |
| P-A4 | `UpdateProduct_WithListPriceAmountAboveMaximum_ShouldReturn400WithTurkishMessage` | PUT, `100000000000000m` | `ListPriceAmount` |
| P-A5 | `UpdateProduct_WithReorderPointAboveMaximum_ShouldReturn400WithTurkishMessage` | PUT, `ReorderPoint = 1000000000000m` | `ReorderPoint` |

Two acceptance tests at the maxima, each with one request that carries both maxima. Both read the
stored values back with `GetProductDetailAsync(productId)`, take `listPriceAmount` and
`reorderPoint` with `GetDecimal()`, and compare them by value with `ShouldBe(99999999999999.9999m)`
and `ShouldBe(999999999999.999999m)`. No further combinations are added.

- **P-A6** `CreateProduct_WithListPriceAmountAndReorderPointAtMaximum_ShouldReturn201AndGetShouldReturnThem`.
  - The POST setup above, with `ListPriceAmount = 99999999999999.9999m` and
    `ReorderPoint = 999999999999.999999m`.
  - Asserts: the POST is `HttpStatusCode.Created`; the id is read with `ReadFromJsonAsync<Guid>()`;
    the GET returns both values exactly.
- **P-A7** `UpdateProduct_WithListPriceAmountAndReorderPointAtMaximum_ShouldSucceedAndGetShouldReturnThem`.
  - The PUT setup above, with the same two maxima.
  - Asserts: the PUT is `HttpStatusCode.OK`, the success status that
    `UpdateProduct_WithValidPayload_ShouldReturn200` asserts (`ProductsApiTests.cs:270`); the GET
    returns both values exactly.

Count deltas: Application.Tests +22, IntegrationTests +7.

### Validation

```bash
dotnet build -warnaserror
dotnet test tests/Envanex.Application.Tests --filter FullyQualifiedName~Envanex.Application.Tests.Products.Validators
dotnet test tests/Envanex.IntegrationTests --filter FullyQualifiedName~Envanex.IntegrationTests.Api.ProductsApiTests
dotnet format --verify-no-changes
dotnet test
git status --short
```

Expected:

- The build has 0 warnings and 0 errors.
- Every test run reports `Failed: 0`, with the deltas. The `ProductsApiTests` run includes P-A1 to
  P-A7. Record the integration-suite duration.
- `git status --short` is the coder's check, run before the human's commit. It lists exactly the
  five Files.
- The tester's run of `git status --short` prints nothing, because the tester runs on a clean tree.
  The phase's changed files come from the `files` section of `gates.txt`.

Grep tool checks:

- `WithErrorCode\(ProductErrors\.ListPriceAmountTooLarge\.Code\)` and
  `WithErrorCode\(ProductErrors\.ReorderPointTooLarge\.Code\)`: one match each per validator file.
- `"Product\.ListPriceAmountNegative"` and `"Product\.ReorderPointNegative"`: still literal, once per
  validator file.
- `\.WithMessage\(` in `src/Envanex.Application`: no match.
- `\r` in each of Phase 3's five Files: no match (LF only).
- `[^\x00-\x7F]` in `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs`: no match. The file is
  ASCII-only today.

Stop condition: P-A6 or P-A7 does not read back both maxima exactly.

## Phase 4: docs(roadmap) — the fix row, the measured duration, two decisions

A separate docs commit after the code phases. Exactly four changes to `docs/roadmap.md`, numbered
as at `cb4b079`. Nothing else changes.

### Files

- `docs/roadmap.md` — modified — changes (a)-(d) below.

### Signatures

None (documentation only). The exact text follows.

**(a)** Insert one line after line 60 (the `fix(infra)` row), before the PR 7 row. The status cell
is left empty; a later roadmap update fills in the PR number:

```text
| — | `fix(domain)` — bound the persisted numeric fields and reject a negative list price | |
```

**(c)** Replace lines 121-122 (the "Money is not nullable" bullet) with exactly these six lines.
They state the technical distinction: ADR 0003's general statement does not hold for EF Core 10,
and the project's decision is unchanged. ADR 0003 itself is not edited:

```text
- **Money is not nullable.** EF Core 10 supports optional complex types, provided the complex type
  has at least one required property
  (https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew), so ADR 0003's general
  statement that complex types cannot be null does not hold for EF Core 10. The project's
  decision to keep `Money` required is unchanged: an optional amount is still expressed as
  `Money.Zero` or as separate columns. See ADR 0003.
```

**(d)** Insert these six lines after line 124, the end of the "Every `Money` property needs an
explicit `ComplexProperty` line" bullet. The new decision sits beside the other `Money` mapping
decisions:

```text
- **Persisted numeric fields carry their column's bound in their aggregate.** `Money` and
  `Quantity` get no extra bound tied to a column range; their existing sign and rounding rules
  stay. Each persisted amount, quantity or factor states its maximum as a public constant on its
  aggregate and checks it there, on the value the aggregate receives. Its validator checks the
  same maximum on the raw request value, and a schema test links the constant to the live column.
  PR 8 and PR 9 follow this for every new persisted field.
```

**(b)** Replace line 196 (the row starting `| The integration suite ran at 53–59 s`) with exactly
this one line. The table keeps its two columns:

```text
| The integration suite's duration against the 60 s decision line in ADR 0007. **Measured on 2026-10-23 at `56fd6d4`:** three runs of `dotnet test` at the repository root, in Debug, gave `1 m 2 s`, `55 s` and `52 s` on the `Envanex.IntegrationTests.dll` summary line: a median of 55 s, the same metric as the earlier records. The raw output is in the measurement turn of Claude Code session `3e1d4467-7a9c-42b7-b757-526d3617ea84`. **The condition this row set is only partly met:** the SQL Server container's start-up was not separated out. No timestamped Testcontainers log lines were observed in these three runs, so their output gives no start-up time. No change is made while the median is below the line. **Follow-up:** when a `/gate` record shows the integration suite at `1 m` or more, repeat the three-run median. If the median is 60 s or more, measure the container's start-up directly, in a step approved separately. Then decide between fewer `ResetIdentityAsync` calls (the first suspect, weakens nothing), fewer `ConcurrencyIterations`, parallel test collections (which break `DatabaseCollection`'s serialisation), or a higher line (which revises ADR 0007). | when a `/gate` record reaches `1 m`; the start-up separation stays open until then |
```

Note on the suite's duration. Phases 1-3 add 35 integration tests. If a gate record during this PR
shows the integration suite at `1 m` or more, row (b)'s follow-up is triggered. This plan does not
schedule it, and the human decides. It is not a phase failure.

### Tests to add

None: documentation only, with no behavior change. The checks below stand in for tests.

### Validation

The coder runs these before the human commits:

1. `dotnet build -warnaserror` (`coder.md:26`). 0 warnings, 0 errors.
2. `git status --short`: ` M docs/roadmap.md` only, plus any uncommitted review or plan files.
3. `git diff --unified=0 HEAD -- docs/roadmap.md`: exactly four hunks. Each hunk header line
   begins with the text below. Git appends a context line after the second `@@`; a scratch diff of
   `docs/roadmap.md` showed `@@ -60,0 +61 @@ in the README. That branch is never merged.`.
   - `@@ -60,0 +61 @@`
   - `@@ -121,2 +122,6 @@`
   - `@@ -124,0 +130,6 @@`
   - `@@ -196 +207 @@`

   They change only (a), (c), (d) and (b).
4. `git diff --check HEAD -- docs/roadmap.md`: no output.
5. `git ls-files --eol docs/roadmap.md`: `w/lf`.
6. Coder only: `head -c 3 docs/roadmap.md | od -An -tx1` gives `23 20 52` (no BOM).
7. Grep tool on `docs/roadmap.md`:
   - no match for `Complex types cannot be null in EF Core` or
     `The integration suite ran at 53–59 s`;
   - exactly one match each for `bound the persisted numeric fields`, `Measured on 2026-10-23 at`,
     `Persisted numeric fields carry their column's bound` and `ef-core-10\.0/whatsnew`.

The tester, after the commit: `git log --oneline -3 -- docs/roadmap.md`, then
`git show --unified=0 <that commit> -- docs/roadmap.md`. It expects the same four hunks, each header
line beginning with the same text.

## Mutation proofs

They run against committed code, through the existing scripts, in a coder turn the human starts for
that purpose (the coder never invokes `/mutate`), or by the human through `/mutate`.

- Labels are the row IDs, and each log is `TestResults/fix-numeric-bounds/mutations/<ID>.txt`.
- Docker must be running, except for MU5, MU6, MP13 and MP14, which run Domain.Tests only.
- Every `mutate-run.sh` call uses the Bash tool's `timeout` of 600000.
- 27 runs in total: 10 in S1 and 17 in S3. Each two-file row runs twice.

**One-file row:**

1. `bash .claude/skills/mutate/scripts/mutate-clean.sh <ID> <file>` must exit 0.
2. Apply the Edit.
3. `bash .claude/skills/mutate/scripts/mutate-run.sh <ID> <file> dotnet "<filter>" [<project>]`.
4. Apply the inverse Edit.
5. `mutate-clean.sh <ID> <file>` must exit 0. If it does not, stop and tell the human.

**Two-file row (MU7, MP15):**

1. `mutate-clean.sh <ID> <fileA> <fileB>` must exit 0.
2. Edit A, then Edit B.
3. `mutate-run.sh <ID> <fileA> dotnet "<filter>"`, then `mutate-run.sh <ID> <fileB> dotnet "<filter>"`.
   The script records the diff of one `<file>` per call, so two calls put both diffs in the log.
   Both runs execute the same tests on the same doubly mutated tree, and must show the same reds.
4. Inverse B, then inverse A.
5. `mutate-clean.sh <ID> <fileA> <fileB>` must exit 0.

**Rules** (`.claude/skills/mutate/SKILL.md`):

- A mutation deletes or moves a block, or edits a condition or a constant. It never adds a constant
  the compiler folds (CS0162).
- No `--no-build`.
- A red counts only when the test's own Shouldly assertion fails. Report that assertion for every
  red. An infrastructure failure is re-run and logged as `<ID>-infra-N`.
- The PostToolUse format hook runs after each Edit; the final `mutate-clean.sh` proves the restore.
- If a row's observed reds differ from its expected reds, record the observation, do not edit any
  test, and report to the human.

**Recording the results.** The coder fills the Observed column, from the logs only. Until then each
cell stays `pending`.

- **Allowed write.** In steps S1 and S3 the coder may edit this plan file,
  `thoughts/shared/plans/2026-10-24_numeric-bounds.md`, limited to the Observed cells of that
  step's rows. `coder.md:23` forbids modifying any file the plan does not list; this is the listing.
- **Order.** Each row completes the procedure above before the next row starts: the initial
  `mutate-clean.sh` check, only that row's mutation, its test run, its inverse edits, and its final
  `mutate-clean.sh` check. Only after every row of the step has completed and passed its final
  check does the coder fill that step's Observed cells from the logs. This keeps the proof that the
  code is restored separate from the documentation change.
- **Nothing else changes.** No other line of the plan changes. The human commits the update as its
  own docs commit.

Filters:

```text
F-U = FullyQualifiedName~Envanex.Domain.Tests.Aggregates.UnitOfMeasures.UnitOfMeasureTests.|FullyQualifiedName~Envanex.Application.Tests.UnitOfMeasures.|FullyQualifiedName~Envanex.IntegrationTests.Api.UnitOfMeasuresApiTests.
F-P = FullyQualifiedName~Envanex.Domain.Tests.Aggregates.Products.ProductTests.|FullyQualifiedName~Envanex.Application.Tests.Products.|FullyQualifiedName~Envanex.IntegrationTests.Api.ProductsApiTests.
F-S = FullyQualifiedName~Envanex.IntegrationTests.Persistence.SchemaPrecisionTests
F-UD = FullyQualifiedName~Envanex.Domain.Tests.Aggregates.UnitOfMeasures.UnitOfMeasureTests
F-PD = FullyQualifiedName~Envanex.Domain.Tests.Aggregates.Products.ProductTests
```

How each filter is passed:

- **F-U and F-P** contain `|` inside double quotes, so they are coder-only. Rows that use them pass
  no `<project>` argument and run at the solution root across the three test projects.
- **F-S** rows (MU8, MU9) pass `tests/Envanex.IntegrationTests` as the fifth argument.
  For example:
  `bash .claude/skills/mutate/scripts/mutate-run.sh MU8 src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs dotnet "FullyQualifiedName~Envanex.IntegrationTests.Persistence.SchemaPrecisionTests" tests/Envanex.IntegrationTests`.
- **F-UD** rows (MU5, MU6) and **F-PD** rows (MP13, MP14) pass `tests/Envanex.Domain.Tests` as the
  fifth argument, because only domain tests can change.

Files:

- `uom` = `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs`
- `uomV` = `src/Envanex.Application/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidator.cs`
- `prod` = `src/Envanex.Domain/Aggregates/Products/Product.cs`
- `cV` = `src/Envanex.Application/Products/Validators/CreateProductCommandValidator.cs`
- `uV` = `src/Envanex.Application/Products/Validators/UpdateProductCommandValidator.cs`

"Green" means every other test the filter selects stays green. The cell names the tests that matter
for the row.

### Step S1, against Phase 1's code commit

Required: MU1-MU4 and MU7. Risk-based:

- MU5 and MU6: the decided check order (D2), which only U-D4 and U-D5 pin.
- MU8 and MU9: the schema link for the factor's maximum and for its scale.

| ID | File: mutation | Filter | Expected red | Expected green (notable) | Why | Observed |
|---|---|---|---|---|---|---|
| MU1 | `uomV`: delete the TooPrecise `RuleFor` | F-U | U-V2 (3 rows), U-V4 (2), U-V5, U-A1, U-A3 | U-D*, U-H*, U-V1, U-V3, U-A2, U-A4 | Risk: a raw factor with more than 6 decimal places gets past the validator. The domain still rejects it, but as a plain ProblemDetails (title `Bad Request`, no `errors`), and U-A3 gets only the TooLarge message. | U-V2 (3 rows), U-V4 (2), U-V5, U-A1, U-A3; as expected; log MU1.txt |
| MU2 | `uomV`: delete the TooLarge `RuleFor` | F-U | U-V3, U-V4 (2), U-A2, U-A3 | U-D*, U-H*, U-V1, U-V2, U-V5, U-A1, U-A4 | Risk: a raw factor above M6 gets past the validator. The domain still rejects it as a plain ProblemDetails, and U-A3 gets only the TooPrecise message. | U-V3, U-V4 (2), U-A2, U-A3; as expected; log MU2.txt |
| MU3 | `uom`: delete check 3 (TooPrecise) | F-U | U-D2 (3), U-D4 (2: they now return TooLarge), U-H1 | U-D5 (positivity first), every U-V and U-A | Risk: an undecorated path accepts a factor with more than 6 decimal places. **Masking:** the validator answers first, so every API test stays green, and only the domain and handler tests protect the check. | U-D2 (3), U-D4 (2: they now return TooLarge), U-H1; as expected; log MU3.txt |
| MU4 | `uom`: delete check 4 (TooLarge) | F-U | U-D3, U-H2 | U-D4 (precision answers first), every U-V and U-A | Risk: an undecorated path passes a factor above M6 to `SaveChanges`. Masked through the API, as in MU3. | U-D3, U-H2; as expected; log MU4.txt |
| MU5 | `uom`: move check 4 above check 3 | F-UD | U-D4 (2) | every other `UnitOfMeasureTests` test | Risk: a factor that fails both rules reports TooLarge instead of the decided TooPrecise (D2's order of checks 3 and 4). | U-D4 (2); as expected; log MU5.txt |
| MU6 | `uom`: move check 3 above check 2 | F-UD | U-D5 | every other `UnitOfMeasureTests` test, including the existing zero and `-1` factor tests (integers pass check 3) | Risk: a negative factor with more than 6 decimal places reports TooPrecise instead of the decided MustBePositive (D2's order of checks 2 and 3). | U-D5; as expected; log MU6.txt |
| MU7 | MU1's edit and MU3's edit together (`uomV`, `uom`) | F-U | U-V2, U-V4, U-V5, U-D2, U-D4, U-H1, U-A1, U-A3 | U-V1, U-V3, U-D1, U-D3, U-D5, U-H2, U-A2, U-A4 | Risk: with both layers removed, a factor with more than 6 decimal places reaches `SaveChanges` through the API; U-A1 protects the end-to-end contract. Expected red on its status assertion. The status and any exception are recorded from the run, not predicted (D5). | U-V2, U-V4, U-V5, U-D2, U-D4, U-H1, U-A1, U-A3; as expected; U-A1 red on its status assertion, observed status Created where BadRequest was expected; no exception in the log besides the Shouldly assertions; both runs showed the same reds (14 each); log MU7.txt |
| MU8 | `uom`: `ConversionFactorMaxValue` becomes `999999999999.99999m` | F-S | U-S2 | U-S1, U-S3, the four existing schema tests | Risk: the maximum constant drifts from the live column's range. U-S2 recomputes the maximum from the column's precision and scale. | U-S2; as expected; log MU8.txt |
| MU9 | `uom`: `ConversionFactorDecimalPlaces` becomes `5` | F-S | U-S3 | U-S1, U-S2, the existing schema tests | Risk: the decimal-places constant drifts from the live column's scale. U-S3 compares it with that scale. | U-S3; as expected; log MU9.txt |

### Step S3, against Phase 3's code commit

Required: MP1-MP12 and MP15. Risk-based:

- MP13 and MP14: no partial update in `Update` and `UpdatePrice`, the human's acceptance criterion.
- MP20: an exclusive bound at the maximum, which P-VC1 and P-A6 pin.

| ID | File: mutation | Filter | Expected red | Expected green (notable) | Why | Observed |
|---|---|---|---|---|---|---|
| MP1 | `cV`: delete the ListPriceAmount TooLarge `RuleFor` | F-P | P-VC2 (3), P-A1, P-A2 | P-VU*, P-D*, P-HC*, P-HU*, P-A3..P-A5 | Risk: POST takes a raw list price above M4 past the validator. P-A1: the domain still rejects it as a plain ProblemDetails. P-A2: `…99994` rounds to M4 and is created. | pending |
| MP2 | `cV`: delete the ReorderPoint TooLarge `RuleFor` | F-P | P-VC5 (3), P-A3 | P-A1, P-A2, P-A4, P-A5 and the rest | Risk: POST takes a raw reorder point above M6 past the validator; the domain answers with a plain ProblemDetails. | pending |
| MP3 | `uV`: delete the ListPriceAmount TooLarge `RuleFor` | F-P | P-VU2 (3), P-A4 | P-VC*, P-A1..P-A3, P-A5 | Risk: PUT takes a raw list price above M4 past the validator; the domain answers with a plain ProblemDetails. | pending |
| MP4 | `uV`: delete the ReorderPoint TooLarge `RuleFor` | F-P | P-VU5 (3), P-A5 | P-VC*, P-A1..P-A4 | Risk: PUT takes a raw reorder point above M6 past the validator; the domain answers with a plain ProblemDetails. | pending |
| MP5 | `prod`: delete check 1 in `Create` | F-P | P-D4, P-HC2 | P-D5, every P-V and P-A | Risk: `Create` accepts a negative list price on an undecorated path. Masking: the raw `>= 0` rule answers first through the API. | pending |
| MP6 | `prod`: delete check 2 in `Create` | F-P | P-D3, P-HC1 | **P-A1, P-A2** (masked by the validator) | Risk: `Create` passes a list price above M4 to `SaveChanges` on an undecorated path. Masked through the API. | pending |
| MP7 | `prod`: delete check 3 in `Create` | F-P | P-D8, P-HC3 | **P-A3** (masked) | Risk: `Create` passes a reorder point above M6 to `SaveChanges` on an undecorated path. Masked through the API. | pending |
| MP8 | `prod`: delete check 1 in `Update` | F-P | P-D12, P-HU2 | P-D13, every P-A | Risk: `Update` accepts a negative list price on an undecorated path. Masked through the API by the raw `>= 0` rule. | pending |
| MP9 | `prod`: delete check 2 in `Update` | F-P | P-D11, P-HU1 | **P-A4** (masked) | Risk: `Update` passes a list price above M4 to `SaveChanges` on an undecorated path. Masked through the API. | pending |
| MP10 | `prod`: delete check 3 in `Update` | F-P | P-D16, P-HU3 | **P-A5** (masked) | Risk: `Update` passes a reorder point above M6 to `SaveChanges` on an undecorated path. Masked through the API. | pending |
| MP11 | `prod`: delete check 1 in `UpdatePrice` | F-P | P-D20 | P-D21 and the rest | Risk: `UpdatePrice` accepts a negative list price. It has no handler and no endpoint, so only P-D20 protects it. | pending |
| MP12 | `prod`: delete check 2 in `UpdatePrice` | F-P | P-D19 | the rest | Risk: `UpdatePrice` accepts a list price above M4. Only P-D19 protects it. | pending |
| MP13 | `prod`: move `Name = name.Trim();` in `Update` above the new checks | F-PD | P-D11, P-D12, P-D16 | every other `ProductTests` test | Risk: a failed `Update` leaves a partial update behind. `ShouldBeUnchanged` catches the changed `Name`. | pending |
| MP14 | `prod`: move `ListPrice = newPrice;` in `UpdatePrice` above the checks | F-PD | P-D19, P-D20 | every other `ProductTests` test | Risk: a failed `UpdatePrice` still changes the price. `ShouldBeUnchanged` catches it. | pending |
| MP15 | MP1's edit and MP6's edit together (`cV`, `prod`) | F-P | P-VC2, P-D3, P-HC1, P-A1, P-A2 | P-VU*, P-D11, P-HU1, P-A3..P-A5 | Risk: with both layers removed, a list price above M4 reaches `SaveChanges` through POST; P-A1 and P-A2 protect the end-to-end contract. P-A1's observed status is recorded, not predicted (D5). P-A2 is created (201). | pending |
| MP20 | `cV`: the list price's `LessThanOrEqualTo(Product.ListPriceAmountMaxValue)` becomes `LessThan(Product.ListPriceAmountMaxValue)` | F-P | P-VC1's `99999999999999.9999m` row, P-A6 | the rest | Risk: an exclusive bound at the maximum, so M4 itself is rejected. Caught both at the validator and through the API. | pending |

## Notes for the journal and the PR text

- How out-of-range values behaved before the fix in SqlClient, SQL Server and HTTP is UNVERIFIED
  (D5). Describe it only as what the research could not establish, never as fact.
- MU7 and MP15 run post-fix code with checks removed; they are not characterizations of `main`.
  Quote their observations only as "observed under mutation <ID>".
- The research's UNVERIFIED question whether a trailing-zero factor persists may be cited only as far
  as the `1.500000000` rows (U-P2, U-A4) actually observed it. Whether `Math.Round` yields a
  negative zero stays UNVERIFIED; this PR does not investigate it, because D2 has no sign-bit rule.

## Rollback notes

- No schema, migration or data change. Rolling back restores code behavior only. Rows saved while
  the bounds were active stay valid, because the bounds only narrow what is accepted.
- **Revert order:** Phase 4 (docs), then 3, then 2, then 1.
  - Phase 3 alone can be reverted; the domain still guards.
  - Phase 2 cannot be reverted while Phase 3 stays, because Phase 3's rules reference
    `Product.ListPriceAmountMaxValue`, `ReorderPointMaxValue` and Phase 2's errors.
  - Phase 1 cannot be reverted alone while Phase 2 stays: Phase 2's P-S1 and P-S2 use Phase 1's
    `ColumnMaximum` helper, and Phase 2 adds its rows to the two theories Phase 1 creates in
    `TurkishErrorMessagesTests` and `ResultExtensionsTests`.
- Reverting Phase 2 brings back `void UpdatePrice`. Its `UpdatePrice` success assertions go in the
  same commit.
- Reads are unaffected in either direction:
  - EF materializes through constructors and setters, not `Create`;
  - `Money` bypasses `Money.Of` (ADR 0003);
  - `QuantityConverter`'s `Quantity.Of` has no upper bound.
- Stored values cannot exceed the column maxima, which equal the new constants, and a stored factor
  cannot carry more than 6 decimal places.
- What may conflict with the new domain rules is a negative list price or a zero conversion factor.
  Whether any such row exists in a developer database is unknown.
- Such rows still load, because EF materializes through constructors and setters rather than
  `Create`. The new checks run only on values passed to `Create`, `Update` and `UpdatePrice`.
- After the squash merge, roll back by reverting the squash commit in a new PR.

## Open questions

None.
