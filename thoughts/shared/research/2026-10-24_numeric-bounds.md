# Research: numeric bounds and the product list-price sign (branch fix/numeric-bounds)

## Question

How do `ListPriceAmount`, `ReorderPoint` and `ConversionFactor` travel from a request to SQL Server
today, what happens to values outside what their columns can store, and what facts bear on the two
design questions the human will decide?

- **A.** Where the upper bounds live: on the general `Money` / `Quantity` types, or only on the persisted
  product and unit-of-measure fields.
- **B.** Whether a bound is checked against the raw input or against the rounded value.

The human's constraints 1-6 (2026-10-23) are taken as given:
1. A conversion factor that cannot be represented at 6 places is rejected by value, never rounded.
2. Nothing reaches SQL Server outside its column.
3. The rounding stays as it is.
4. `Product` rejects a negative list price in `Create`, `Update` and `UpdatePrice`.
5. New codes go into the two tables and their tests.
6. API and SQL round-trip tests run at every bound.

PR 7 topics were not explored.

Notation used below:
- **M4** = `99999999999999.9999`, the largest value `decimal(18,4)` can store: 14 integer digits and 4 fractional.
- **M6** = `999999999999.999999`, the largest value `decimal(18,6)` can store: 12 integer digits and 6 fractional.

The negative limits are -M4 and -M6. For the range rule, see the T-SQL `decimal` doc, whose example is "decimal(10, 2) … -99999999.99 to 99999999.99".

## Relevant files

All paths are repo-relative to `C:\projects\envanex`. The layer is in brackets.

**Request entry [Web]**
- `src/Envanex.Web/Controllers/ProductsController.cs:83-89` — `Create([FromBody] CreateProductCommand command, CancellationToken ct)`. It calls `_createProductHandler.HandleAsync` and then `ToCreatedActionResult`.
- `src/Envanex.Web/Controllers/ProductsController.cs:91-107` — `Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)`. It checks the route id against the body id, then calls the handler and `ToActionResult`.
- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs:31-37` — `Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct)`.
- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs:39-45` — `GetById`. Its response carries `conversionFactor`, which makes it usable for API round-trip reads.
- `src/Envanex.Web/Program.cs:150-155` — `UseExceptionHandler("/Error", createScopeForErrors: true)` runs whenever the environment is not Development.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs:43,91` — the test host runs in the environment `"Testing"`, so the exception handler above is active in the API tests.
- `src/Envanex.Web/Extensions/ResultExtensions.cs:16-82` holds `StatusCodeMap`, a `FrozenDictionary<string,int>`:
  - `:129` exposes `MappedErrorCodes`.
  - `:141-170` turns a `ValidationError` into a 400 `ValidationProblemDetails` whose `errors[PropertyName]` holds the Turkish text.
  - `:172-187` handles any other `Error`: ProblemDetails with the mapped status, defaulting to 400.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs:7-66` holds the `Messages` table, and `:75` exposes `TranslatedErrorCodes`.

**Commands, validators, handlers [Application]**
- `src/Envanex.Application/Products/Commands/CreateProductCommand.cs:3-9` — a positional record with `decimal ListPriceAmount`, `string ListPriceCurrency` and `decimal ReorderPoint`.
- `src/Envanex.Application/Products/Commands/UpdateProductCommand.cs:3-10` — the same fields, plus `Id` and `RowVersion`.
- `src/Envanex.Application/UnitOfMeasures/Commands/CreateUnitOfMeasureCommand.cs:3-7` — `Guid? BaseUnitId` and `decimal ConversionFactor`.
- `src/Envanex.Application/Products/Validators/CreateProductCommandValidator.cs:31-33` (`ListPriceAmount` `GreaterThanOrEqualTo(0)`, code `"Product.ListPriceAmountNegative"`) and `:43-45` (`ReorderPoint` `GreaterThanOrEqualTo(0)`, code `"Product.ReorderPointNegative"`). Neither field has an upper rule.
- `src/Envanex.Application/Products/Validators/UpdateProductCommandValidator.cs:27-29` and `:39-41` — the same two rules.
- `src/Envanex.Application/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidator.cs:27-39`:
  - A base unit must have `Equal(1m)` (`"UnitOfMeasure.BaseUnitFactorMustBeOne"`).
  - A derived unit must have `GreaterThan(0m)` (`"UnitOfMeasure.ConversionFactorMustBePositive"`).
  - There is no upper rule and no precision rule.
- `src/Envanex.Application/Behaviors/ValidationDecorator.cs:20-40` runs every validator. It maps each failure to `ValidationFailure(f.PropertyName, f.ErrorCode)` and short-circuits before the handler runs.
- `src/Envanex.Application/DependencyInjection.cs:46-69` wires the decorators. Only the `ICommandHandler<,>` registration is decorated; the concrete handler types are also registered undecorated (`:36-40`).
- `src/Envanex.Application/Products/Commands/CreateProductCommandHandler.cs:46-66`. The calls are `Currency.Of` (`:46`), then `Money.Of(command.ListPriceAmount, …)` (`:52`), then `Quantity.Of(command.ReorderPoint)` (`:58`), then `Product.Create(...)` (`:64-66`). The `try/catch` at `:76-83` catches `DuplicateKeyException` only.
- `src/Envanex.Application/Products/Commands/UpdateProductCommandHandler.cs:46-64`. The calls are `Money.Of` (`:52`), `Quantity.Of` (`:58`) and `product.Update(...)` (`:64`). The catch at `:72-79` handles `ConcurrencyConflictException` only.
- `src/Envanex.Application/UnitOfMeasures/Commands/CreateUnitOfMeasureCommandHandler.cs:45-46` passes `command.ConversionFactor` to `UnitOfMeasure.Create` as typed. The catch at `:56-63` handles `DuplicateKeyException` only.

**Value objects and aggregates [Domain]**
- `src/Envanex.Domain/ValueObjects/Money.cs` (`public sealed record Money`):
  - `:7` `private const int DecimalPlaces = 4`.
  - `:18-23` `public static Result<Money> Of(decimal amount, Currency currency)` calls ThrowIfNull on the currency, then `Math.Round(amount, 4, ToEven)`. It has no bounds and accepts negatives.
  - `:25-36` `Result<Money> Add(Money other)`.
  - `:38-49` `Result<Money> Subtract(Money other)`.
  - `:51-55` `Money Multiply(decimal factor)` returns a plain `Money`.
  - `:57-61` `Zero`.
  - `:64-67` `MoneyErrors.CurrencyMismatch`.
- `src/Envanex.Domain/ValueObjects/Quantity.cs` (`public readonly record struct Quantity`):
  - `:7` `private const int DecimalPlaces = 6`.
  - `:16-25` `public static Result<Quantity> Of(decimal value)` checks the raw `value < 0` (`QuantityErrors.Negative`) **before** it rounds.
  - `:27-31` `Quantity Add(Quantity other)` returns a plain `Quantity`.
  - `:33-44` `Result<Quantity> Subtract(Quantity other)`.
  - `:46-57` `Result<Quantity> Multiply(decimal factor)`.
  - `:62-66` `QuantityErrors.Negative` and `QuantityErrors.NegativeResult`.
- `src/Envanex.Domain/Aggregates/Products/Product.cs` (`public sealed class Product : AggregateRoot<Guid>`):
  - `:8-9` `public const int CodeMaxLength = 50; public const int NameMaxLength = 200;`
  - `:14` `public Money ListPrice { get; private set; }` and `:15` `public Quantity ReorderPoint { get; private set; }`.
  - `:37-71` `public static Result<Product> Create(string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)`. It has no checks on the amount or the quantity.
  - `:73-77` `public void UpdatePrice(Money newPrice)` calls ThrowIfNull and then assigns. It has no check.
  - `:89-114` `public Result Update(string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)` validates first and only then assigns. It has no amount or quantity check.
- `src/Envanex.Domain/Aggregates/Products/ProductErrors.cs:5-22`. `ListPriceAmountNegative` (`:19`) and `ReorderPointNegative` (`:20`) are declared here but never returned by any domain code.
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs`:
  - `:7-8` `CodeMaxLength = 20` and `NameMaxLength = 200`.
  - `:13` `public decimal ConversionFactor { get; private set; }` is a raw `decimal`, not a `Quantity`.
  - `:33-80` `public static Result<UnitOfMeasure> Create(string code, string name, Guid? baseUnitId, decimal conversionFactor)`. Its factor rules are `:58-76`: a base unit needs `!= 1m`, which yields `BaseUnitFactorMustBeOne`; a derived unit needs `<= 0m`, which yields `ConversionFactorMustBePositive`. There is no upper rule and no precision rule.
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasureErrors.cs:5-18`.

**Persistence [Infrastructure]**
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs:28-41`:
  - `Properties<Quantity>().HaveConversion<QuantityConverter>().HavePrecision(18, 6)` (`:31-33`).
  - `MoneyComplexTypeConvention` is registered at `:40`.
- `src/Envanex.Infrastructure/Persistence/Conventions/MoneyComplexTypeConvention.cs:13-20` calls `SetPrecision(18)` and `SetScale(4)` on `Money.Amount` inside every complex `Money`.
- `src/Envanex.Infrastructure/Persistence/Converters/QuantityConverter.cs:8-12` writes `quantity.Value`. It reads with `Quantity.Of(value).Value`, so every read runs the `Quantity.Of` factory.
- `src/Envanex.Infrastructure/Persistence/Configurations/ProductConfiguration.cs:26` has `builder.ComplexProperty(p => p.ListPrice);`.
- `src/Envanex.Infrastructure/Persistence/Configurations/UnitOfMeasureConfiguration.cs:19` has `builder.Property(u => u.ConversionFactor).HasPrecision(18, 6);`.
- `src/Envanex.Infrastructure/Persistence/UnitOfWork.cs:17-36` translates two exceptions only:
  - `DbUpdateConcurrencyException` becomes `ConcurrencyConflictException`.
  - `DbUpdateException` whose inner `SqlException` has `Number` 2601 or 2627 becomes `DuplicateKeyException`.
  - Everything else propagates.
- `src/Envanex.Infrastructure/Persistence/Repositories/ProductReadRepository.cs:33-35,55-57` and `UnitOfMeasureReadRepository.cs:50` are the read projections of the three values.
- `src/Envanex.Infrastructure/Migrations/20260904115921_InitialSchema.cs` defines the three columns:
  - `:22` `UnitOfMeasures.ConversionFactor decimal(18,6) NOT NULL`.
  - `:60` `Products.ReorderPoint decimal(18,6) NOT NULL`.
  - `:63` `Products.ListPrice_Amount decimal(18,4) NOT NULL`.
- `EnvanexDbContextModelSnapshot.cs:45-47,61-63,95-97` matches the migration.
- Resolved packages: `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11 (`Directory.Packages.props:18`) and FluentValidation 12.1.1 (`:13`). `Microsoft.Data.SqlClient` is transitive and resolves to 6.1.6 (`src/Envanex.Infrastructure/obj/project.assets.json:473`, as of the last restore).

**Docs that constrain the decision**
- `docs/adr/0002-result-over-exceptions.md:20-34`:
  - `Quantity.Add` returns a plain `Quantity` because "two non-negative quantities always sum to a valid quantity".
  - `Money.Multiply` returns a plain `Money`.
  - "an overflow is an exception, not a domain error".
- `docs/adr/0003-ef-core-mapping-conventions.md:21-30`:
  - Type-wide facts belong in `ConfigureConventions`.
  - `:61` says EF binds `Money` through its private constructor, so `Money.Of` is not called on read.
  - `:82-85` says forgetting a per-property registration is silent.
- `docs/adr/0006-rest-error-mapping.md:22-28,119-122` covers the explicit table, the reflection tests and the two-table burden. It is a historical record and is not rewritten (constraint 5).
- `docs/roadmap.md:79-85` lists PR 8 (append-only ledger, `StockMovement`, balance projection), PR 9 (costing strategies) and PR 10.
- `CLAUDE.md` Key Patterns covers Result over exceptions, null versus invalid, aggregates owning their invariants, the append-only ledger, costing as `ICostingStrategy`, money at `decimal(18,4)` and quantity at `decimal(18,6)`.

## Current behavior

### Path of each value, request to SQL

| Field | Validator (raw) | Handler | Value object | Aggregate | EF mapping | Column |
|---|---|---|---|---|---|---|
| `ListPriceAmount` | `>= 0` only | `Money.Of` rounds to 4 places (ToEven) | no bounds; negatives valid | `Product.Create/Update/UpdatePrice`: no check | complex type, convention sets precision 18 and scale 4 | `Products.ListPrice_Amount decimal(18,4)` |
| `ReorderPoint` | `>= 0` only | `Quantity.Of`: raw `< 0` fails, then rounds to 6 | no upper bound | `Product.Create/Update`: no check | `QuantityConverter`, `HavePrecision(18,6)` | `Products.ReorderPoint decimal(18,6)` |
| `ConversionFactor` | base `== 1`, derived `> 0` | none, passed as typed | n/a (plain `decimal`) | `UnitOfMeasure.Create`: base `== 1`, derived `> 0` | `HasPrecision(18,6)` | `UnitOfMeasures.ConversionFactor decimal(18,6)` |

`Product.UpdatePrice` has no production caller. Its only callers are tests:
- `ProductTests.cs:87,97`
- `RepositoryTests.cs:61,65,106,117,271,285`
- `ConcurrencyTests.cs:48,52`

No Blazor component touches these commands; a grep of `src/Envanex.Web/**/*.razor` finds no hits.

### What reaches the parameter, and what SqlClient and SQL Server do with it

Microsoft Learn says the following:

1. **EF Core sets parameter precision and scale from the model.** It has done so since EF Core 5.0: "EF Core now sets precision and scale on parameters using the values configured for properties in the EF Core model. This means rounding now happens in SqlClient", and "SqlClient made a change to round instead of truncate decimal values". https://learn.microsoft.com/ef/core/what-is-new/ef-core-5.0/breaking-changes#medium-impact-changes
2. **The SqlClient docs disagree about rounding.**
   - `Microsoft.Data.SqlClient.SqlParameter.Scale` (v7.1 docs) says: "Use of this property to coerce data passed to the database is not supported. To round, truncate, or otherwise coerce data before passing it to the database, use the Math class…".
   - The same page says "Scale values that exceed the Decimal parameter scale are still truncated."
   - That last sentence contradicts point 1 on round versus truncate.
   - https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlparameter.scale?view=sqlclient-dotnet-7.1
3. **SqlClient throws on an invalid precision.** `SqlParameter.Precision` says "an exception is thrown if a Decimal parameter value is set with an invalid Precision". The exception type is not named. https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlparameter.precision?view=sqlclient-dotnet-7.1
4. **SQL Server rounds on conversion and raises 8115 on overflow.** The T-SQL `decimal` doc says: "By default, SQL Server uses rounding when converting a number to a decimal or numeric value with a lower precision and scale. If the value is too large for the target precision, an arithmetic overflow error (8115) occurs when SET ARITHABORT or SET ANSI_WARNINGS is ON… Loss of only precision and scale doesn't cause an error." https://learn.microsoft.com/sql/t-sql/data-types/decimal-and-numeric-transact-sql?view=sql-server-ver17#convert-decimal-and-numeric-data
5. **ToEven picks the even neighbour at a midpoint.** `MidpointRounding.ToEven`: "When a number is halfway between two others, it's rounded toward the nearest even number". https://learn.microsoft.com/dotnet/api/system.midpointrounding?view=net-10.0
6. **SaveChanges wraps save errors.** `DbContext.SaveChanges` throws `DbUpdateException` when "an error is encountered while saving to the database". https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechanges?view=efcore-10.0
7. **Version mismatch.** The SqlClient pages describe v7.1; the repo resolves 6.1.6. That their behavior is the same is UNVERIFIED.

`Money.Of` and `Quantity.Of` round before EF sees the value, so a `ListPrice` or `ReorderPoint` parameter never carries more scale than its column. For those two fields only magnitude can overflow. `ConversionFactor` is never rounded, so it can exceed the scale and the magnitude.

### Behavior today for the inputs asked about

"Code" below means behavior derived by reading the code and the documented `ToEven` semantics. Nothing was executed.

| Input (via POST) | Validator | Value object / aggregate | Reaches SQL as | Outcome |
|---|---|---|---|---|
| derived `ConversionFactor = 0.0000001` (active base unit) | passes (`> 0`) | `UnitOfMeasure.Create` passes (`> 0`) | `0.0000001`, coerced to scale 6 | Code: no layer rejects it. UNVERIFIED: both doc readings give `0.000000` and no error, so most likely 201 with a **zero factor stored** for a derived unit. The read side (`UnitOfMeasure.ConversionFactor` is a plain `decimal`, and the read projection) applies no validation. |
| derived `ConversionFactor = 0.1234567` | passes | passes | `0.1234567`, coerced | UNVERIFIED: stored as `0.123457` (EF doc: SqlClient rounds) or `0.123456` (SqlClient doc: truncated); probably 201. |
| `ListPriceAmount = 100000000000000` | passes | `Money.Of` leaves it unchanged; `Product.Create` passes | 19 digits at scale 4, above precision 18 | Code: `UnitOfWork` does not translate it and no handler catch matches, so the exception escapes the controller. UNVERIFIED: the exception type (a SqlClient precision exception versus `SqlException` 8115), whether EF wraps it in `DbUpdateException`, and the status (expected 500 via `UseExceptionHandler("/Error")` in the "Testing" host). The update path is the same. |
| `ReorderPoint = 1000000000000` | passes | `Quantity.Of` leaves it unchanged; passes | 19 digits at scale 6 | Same as the row above, UNVERIFIED. |
| `ListPriceAmount = 99999999999999.99994` | passes | `Money.Of` gives M4 (next digit 4) | M4, which fits | Code: accepted. UNVERIFIED by execution: that the stored value reads back as exactly M4, which no existing test covers. |
| `ListPriceAmount = 99999999999999.99995` | passes | `Money.Of` gives `100000000000000.0000` (midpoint; the kept digit 9 is odd, so ToEven rounds up) | out of range | Same as the `100000000000000` row, UNVERIFIED. |

Tests that would settle each UNVERIFIED claim:
- **Before the fix.** Once the domain rejects these inputs, the experiment can no longer reach SQL through the domain.
  - A persistence test in the pattern of `SelfReferencingFkTests.DerivedUnit_WithValidBaseUnit_ShouldPersist`. It saves `UnitOfMeasure.Create("GR","Gram",baseId,0.1234567m)` and `…0.0000001m` through `_fixture.CreateDbContext()`, re-reads in a fresh context, and records the stored value. This settles round versus truncate and the silent zero.
  - A persistence test that saves `Product.Create(..., Money.Of(100000000000000m, Currency.TRY).Value, ...)` and the `Quantity.Of(1000000000000m)` equivalent. It records the thrown type, the `InnerException` type, and the `SqlException.Number` if one is present. This settles the exception shape.
  - `ProductsApiTests`-style POSTs of `100000000000000m`, `99999999999999.99995m` and `ReorderPoint = 1000000000000m`, and a `UnitOfMeasuresApiTests`-style POST of a derived unit with `0.0000001m` followed by `GET /api/unit-of-measures/{id}`. These record the HTTP status and the stored factor.
- **At any time.**
  - A domain test reading `decimal.IsNegative(Money.Of(-0.00004m, Currency.TRY).Value.Amount)`. Whether `Math.Round` yields negative zero is UNVERIFIED, and it decides whether `Amount < 0m` and `decimal.IsNegative` disagree.
  - An API test posting a JSON number beyond `decimal`'s range (for example `1e30`). Whether that gives the automatic `[ApiController]` 400 is UNVERIFIED.
  - Whether System.Text.Json keeps the scale of `1.500000000`. This is irrelevant to a by-value check (constraint 1); a POST of `1.500000000m` followed by a GET of the factor settles it.

### Callers of the arithmetic methods (input to question A)

- `Money.Add`, `Money.Subtract` and `Money.Multiply` are called only from `tests/Envanex.Domain.Tests/ValueObjects/MoneyTests.cs:48,60,72,84,96,107`.
- `Quantity.Add`, `Quantity.Subtract` and `Quantity.Multiply` are called only from `tests/Envanex.Domain.Tests/ValueObjects/QuantityTests.cs:50,61,73,84,95,106`.
- LSP `findReferences` on `Money.Multiply` returns only `Money.cs:51` and `MoneyTests.cs:107`. A grep of `src/` finds no production caller of any of the six.
- The future consumers per `CLAUDE.md` and `docs/roadmap.md:83-84`:
  - The PR 8 append-only `StockMovement` ledger with a `StockBalance` projection. Balances are sums of movement quantities, which means `Quantity.Add` and `Quantity.Subtract`.
  - The PR 9 `ICostingStrategy` (moving average, then FIFO). This means `Money.Multiply` by quantities, totals of value, and division.
  - `Money` has no `Divide` today.
- The exact intermediate shapes of PR 8-9 cannot be determined from the code. None of that code exists.

## Existing patterns to follow

- **Limit constants live on the aggregate as `public const int`.**
  - Examples: `Product.CodeMaxLength`, `Product.NameMaxLength` (`Product.cs:8-9`), `UnitOfMeasure.CodeMaxLength` and `UnitOfMeasure.NameMaxLength` (`UnitOfMeasure.cs:7-8`).
  - Validators reference the constant, for example `MaximumLength(Product.CodeMaxLength)` at `CreateProductCommandValidator.cs:16`.
  - EF configurations repeat the number as a literal: `HasMaxLength(50)` at `ProductConfiguration.cs:15`, and `SetPrecision(18)` / `SetScale(4)` and `HasPrecision(18, 6)` likewise.
  - The scales 4 and 6 also exist as `private const` in `Money` and `Quantity`, which validators cannot reference.
  - Nothing links a domain constant to the EF facet. Only `SchemaPrecisionTests` pins the live columns, and only for `ListPrice_Amount` and `ReorderPoint`. No test pins `UnitOfMeasures.ConversionFactor`.
- **Error classes.**
  - `public static class ProductErrors` and `UnitOfMeasureErrors` sit in their own files next to the aggregate, as `public static readonly Error X = new("<Aggregate>.<Field><Condition>", "<English sentence>.")`.
  - Value-object errors (`MoneyErrors`, `QuantityErrors`, `CurrencyErrors`) are declared in the same file as the value object.
  - Length messages embed the literal number: "must not exceed 50 characters." The Turkish message does the same: "en fazla 50 karakter".
- **Validator and domain codes.**
  - A validator emits the same code string as the domain error for the same rule, as a string literal (`.WithErrorCode("Product.CodeTooLong")`), never `ProductErrors.CodeTooLong.Code` and never `.WithMessage` (ADR 0006).
  - Some codes exist only for validators but are still declared as `Error` fields in the domain: `Product.IdRequired`, `Product.RowVersionRequired`, `Product.ListPriceAmountNegative` and `Product.ReorderPointNegative`. Without the field, the orphan test below would fail on their table entries.
  - **Precedent for question B.** The validator already checks a raw value where the domain checks a normalized one:
    - `MaximumLength(Product.CodeMaxLength)` applies to the raw `Code` (`CreateProductCommandValidator.cs:15-17`).
    - `Product.Create` checks `code.Trim().Length` (`Product.cs:51`).
    - So the validator is stricter than the domain for padded input, and the API surface already accepts that asymmetry.
- **Aggregate method style.**
  - ThrowIfNull runs first for reference value objects (`Product.cs:39,75,91`), per the null-versus-invalid rule.
  - Each rule then returns `Result.Failure(...)` on its own line.
  - State is assigned only after every check passes (`Product.cs:108-113`).
  - `Update` returns the non-generic `Result`, and `Create` returns `Result<T>`.
- **Handler style.** Each value-object factory's failure is propagated as `Result.Failure<Guid>(x.Error)` before the aggregate call.
- **The two tables.**
  - A new code needs an entry in `ResultExtensions.StatusCodeMap` (the "400 Bad Request - Product domain", "Product validator" or "UnitOfMeasure" groups are where the neighbours live) and an entry in `TurkishErrorMessages.Messages`.
  - Product and unit-of-measure messages are written with `\u` escapes, while the Auth messages use literal characters.
  - `ListPriceAmountNegative` sits under the "Product - validator" comment groups (`ResultExtensions.cs:57-62`, `TurkishErrorMessages.cs:37-42`). Once `Product` emits it, that grouping comment is no longer accurate.
- **What the tests enforce.**
  - `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` is the reflection guard. It scans every public type in the Domain and Application assemblies for `public static readonly Error` fields, plus `"Validation.Failed"`. Its five facts:
    - `ResultMapping_Scan_ShouldReachTheApplicationAssembly`: the scan reaches Application.
    - `ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping`: every code is in `MappedErrorCodes`.
    - `ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage`: every code is in `TranslatedErrorCodes`.
    - `ResultMapping_NoErrorCode_ShouldBeEmpty`: no code is empty.
    - `ResultMapping_NoMappingEntry_ShouldBeOrphaned`: no table key exists without a declared field.
  - A new `Error` field therefore turns three facts red until both tables are updated.
  - Nothing checks that a validator's `WithErrorCode` literal is a declared code. A typo falls back silently to `"Bu alan geçersiz."` (`ResultExtensions.cs:14,147`).
  - `tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs` is **not** a reflection test. It pins four Auth strings only: InvalidCredentials exactly, the no-lockout-confirmation terms, the "veya" ordering, and PasswordRequired.
  - `tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` reflects over `AuthErrors` only.
- **Round-trip test style.**
  - `[Collection(DatabaseCollection.Name)]` with `IAsyncLifetime` and `_fixture.ResetAsync()`.
  - Seed through one `_fixture.CreateDbContext()`, then re-read in a fresh one (`MoneyPersistenceTests.cs:20-46`, `QuantityPersistenceTests.cs:20-46`).
  - API tests use `_fixture.CreateAdministratorClientAsync()` and `PostAsJsonAsync`, and read `ValidationProblemDetails` as `body.GetProperty("errors").GetProperty("<PropertyName>")[0]` (`ProductsApiTests.cs:153-176`).
- **Ignoring a `Result` already compiles under `-warnaserror`.** `ProductTests.cs:211` discards `product.Update(...)`'s `Result`. Changing `UpdatePrice` to return `Result` therefore does not break its existing callers at compile time. This is inferred from a green `main`; it was not built here.

## Data model touched

| Table | Column | Type | Source |
|---|---|---|---|
| `Products` | `ListPrice_Amount` | `decimal(18,4) NOT NULL` | `InitialSchema.cs:63`; `MoneyComplexTypeConvention.cs:18-19` |
| `Products` | `ReorderPoint` | `decimal(18,6) NOT NULL` | `InitialSchema.cs:60`; `EnvanexDbContext.cs:31-33` |
| `UnitOfMeasures` | `ConversionFactor` | `decimal(18,6) NOT NULL` | `InitialSchema.cs:22`; `UnitOfMeasureConfiguration.cs:19` |

- **No schema change is needed for A1 or A2.** Every bound is enforced in code; the column types do not change.
- **No CHECK constraint exists anywhere.** A grep for `CheckConstraint` and `CHECK (` under `src/` finds nothing, and the `db/` directory named in `CLAUDE.md` does not exist in the repo.
- **A database CHECK constraint, if chosen, is a migration.**
  - It would be a new `migrations add` against `EnvanexDbContext`, with the LF / no-BOM normalization.
  - It requires the db-reviewer (`CLAUDE.md` pipeline; `docs/ai-workflow.md:28`).
  - It would be the repo's first CHECK constraint.
  - It can enforce sign and positivity (`ListPrice_Amount >= 0`, `ReorderPoint >= 0`, `ConversionFactor > 0`, or `BaseUnitId IS NULL AND ConversionFactor = 1`). It cannot enforce magnitude beyond what the column type already rejects.
  - It does catch a factor that SqlClient coerces to `0.000000`.
  - A violation surfaces as `SqlException` 547 inside `DbUpdateException`. `UnitOfWork` does not translate it, so it surfaces as an unhandled exception (likely a 500) rather than a `Result`. It is defense in depth, not a substitute for constraint 4 or the domain bounds.
  - Whether the developer database `EnvanexDev` holds rows that would violate such a constraint is unknown.

## Risks and unknowns

- **The zero factor today.** A derived unit can very likely be stored with factor `0.000000` today (UNVERIFIED). That violates `ConversionFactorMustBePositive` in the database, and nothing detects it on read.
- **Out-of-range values give an unhandled exception today.** An out-of-range amount or quantity reaches `SaveChanges`, and the exception escapes every catch (UNVERIFIED type and status, expected 500).
- **Under A2, Product sees only rounded values.** `Product` receives `Money` and `Quantity` after the handler has rounded them, so a domain check on `ListPrice` or `ReorderPoint` is necessarily a rounded-value check. A raw-value check exists only in the validator, or before `Money.Of` / `Quantity.Of`. See B below.
- **The domain negative-price check is rounded under every option.** `Money` stays general (constraint 4), so `Product` sees `-0.00004` and `-0.00005` as `0.0000`, possibly negative zero (UNVERIFIED).
  - `Amount < 0m` accepts them, while `decimal.IsNegative(Amount)` would reject a negative zero.
  - The validator's raw `>= 0` rejects them, so they never reach `Product` through the API.
- **Undecorated paths skip the validators.** The concrete handlers are resolvable undecorated (`DependencyInjection.cs:36-40`), and tests construct them directly. Any constraint-2 guard that lives only in the validators leaves these paths open, along with `Product.UpdatePrice`, which has no use case.
- **Which layer fires decides the response shape.**
  - A validator failure gives `ValidationProblemDetails` with `errors[<Property>]`.
  - A domain failure gives a plain ProblemDetails `detail`.
  - When validators are at least as strict as the domain, the domain message for these fields is unreachable through the API, and only domain and handler tests exercise it.
- **Four copies of the column range can drift.**
  - New maximum constants would be the third copy of column-range knowledge, after the domain scale constants and the EF literals. The Turkish and English messages that embed the number would be a fourth.
  - No test links them.
- **Under A1, the read converter runs the bound.** `QuantityConverter` calls `Quantity.Of` on every read. A bound equal to the column range cannot fail on stored data, but it would couple materialization to the bound. `Money` materialization bypasses `Money.Of` (ADR 0003).
- **ConversionFactor needs its own checks under A1.** It is a raw `decimal`, so even A1 needs aggregate-level checks on `UnitOfMeasure.Create`. A1 does not remove any aggregate-level work.
- **Check order decides the message.** For a factor such as `1000000000000.0000001`, both the representability rule and the upper bound fail, and the order of the checks decides which code the user sees.
- **The SqlClient docs (v7.1) contradict the EF Core 5 breaking-change note** on round versus truncate. The repo resolves SqlClient 6.1.6.
- **ADR 0006 is out of date.** It says the reflection tests cover "every code declared in Envanex.Domain" and counts thirty-two codes. The tests now scan Domain and Application. The ADR is historical (constraint 5), so this is noted, not a defect.
- **Not explored, per instructions:** PR 7 and the datasource allowlist. `ProductsDatasourceTests.cs` filters on `ListPriceAmount` with the values `0` and `>` against seeded `100m`, and is unaffected by any option.

### Existing tests that cover these values or call `UpdatePrice`

These are the tests that would change or be extended.

**Domain**
- `tests/Envanex.Domain.Tests/Aggregates/Products/ProductTests.cs`:
  - Create tests: `:13-79`, `:121-141`, `:224-253`.
  - `UpdatePrice_ShouldChangeListPrice` (`:81-90`) and `UpdatePrice_WithNull_ShouldThrow` (`:92-98`).
  - Update tests: `:143-222`, `:255-276`.
  - No sign or bound test exists.
- `tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs`:
  - Factor tests: `:66-110` (`2m`, `12m`, `0m`, `-1m`).
  - No precision or upper test exists.
- `tests/Envanex.Domain.Tests/ValueObjects/MoneyTests.cs`:
  - `Of_WithNegativeAmount_ShouldSucceed` (`:27-34`) pins that a negative `Money` is valid. Constraint 4 requires it to stay green.
  - `Of_ShouldRoundToFourDecimalPlaces` (`:18-25`).
  - Under A1: `Multiply_ShouldScale_AndRound` (`:102-110`, `Money result = a.Multiply(3m);`) stops compiling if `Multiply` returns `Result`.
- `tests/Envanex.Domain.Tests/ValueObjects/QuantityTests.cs`:
  - `Of_WithNegative_ShouldFail` (`:26-33`) and `Of_ShouldRoundToSixDecimalPlaces` (`:35-42`).
  - Under A1: `Add_ShouldSumValues` (`:44-53`, `Quantity result = a.Add(b);`) stops compiling if `Add` returns `Result`.

**Application**
- `Products/Validators/CreateProductCommandValidatorTests.cs` (`:85-94`, `:107-116`) and `UpdateProductCommandValidatorTests.cs` (`:85-94`, `:107-116`) hold the negative rules only.
- `UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidatorTests.cs` (`:18-22` has `ValidDerivedUnitCommand` with `0.001m`, and `:86-106`) holds the factor rules.
- The three handler test classes (`CreateProductCommandHandlerTests.cs`, `UpdateProductCommandHandlerTests.cs`, `CreateUnitOfMeasureCommandHandlerTests.cs`) have no amount, quantity or factor failure tests. They are where an undecorated-path domain failure would be pinned.

**Integration: persistence**
- `MoneyPersistenceTests.cs:20-46` round-trips `149.9950`.
- `QuantityPersistenceTests.cs:20-46` round-trips `3.141593`.
- `SelfReferencingFkTests.cs:18-43` round-trips a factor of `0.001`.
- `SchemaPrecisionTests.cs:17-53` pins `ListPrice_Amount` at (18,4) and `ReorderPoint` at (18,6), but has no `ConversionFactor` test.
- `RepositoryTests.cs` and `ConcurrencyTests.cs` call `UpdatePrice` with 200, 300 and 999. They are unaffected unless the signature change is asserted.

**Integration: API**
- `ProductsApiTests.cs:131-176` holds the validation-shape tests.
- `UnitOfMeasuresApiTests.cs` has no successful derived-unit creation test; its derived cases are the 422s at `:100-124`.
- `ResultMappingTests.cs` goes red automatically for any new `Error` field until both tables are updated. Neither it nor `ResultExtensionsTests.cs` nor `TurkishErrorMessagesTests.cs` needs editing.

## Open questions for the human

### A. Where the upper bounds live

**A1: bound `Money` and `Quantity` to the column range.**
`Money` would be bounded to ±M4 and `Quantity` to [0, M6]. What it costs:
- **Signatures and ADR 0002.** `Money.Of`, `Add` and `Subtract` already return `Result`, but `Money.Multiply` (plain `Money`) and `Quantity.Add` (plain `Quantity`) would need to become `Result` or throw. That reverses ADR 0002's recorded rationale ("two non-negative quantities always sum to a valid quantity", "an overflow is an exception, not a domain error"), so **it is a new architectural decision and needs an ADR**, which the human writes.
- **Intermediates.** It bounds every intermediate `Money` and `Quantity` in the PR 8 ledger and the PR 9 costing to the column range, including values that are never persisted.
- **No aggregate work saved.**
  - `Product` still needs its own negative check (constraint 4).
  - `UnitOfMeasure.Create` still needs both factor checks, because `ConversionFactor` is a plain `decimal`.
- **Error wording.** The error raised is value-object-level (`Money.*` / `Quantity.*`), so its wording is not field-specific unless a validator catches the input first.
- **Read path.** It runs on every `ReorderPoint` read through `QuantityConverter`.
- **Tests.** Two existing tests stop compiling (listed above).
- **In its favour.** `CLAUDE.md` fixes every money column at (18,4) and every quantity column at (18,6), so a type-level bound states a type-wide fact, which matches ADR 0003's split.

**A2: bound only the persisted fields, in the domain.**
The checks would go in `Product.Create`, `Product.Update` and `Product.UpdatePrice` (void to `Result`) for `ListPrice.Amount` and `ReorderPoint.Value`, and in `UnitOfMeasure.Create` for `ConversionFactor`. Limit constants would sit on the aggregates, like `CodeMaxLength`, with new fields in `ProductErrors` and `UnitOfMeasureErrors`, mirrored by validators with the same codes. What it costs:
- **Unchanged parts.** `Money`, `Quantity` and ADR 0002 stay as they are, and intermediates stay unbounded.
- **Repetition for PR 8-9.** Every future persisted `Money` or `Quantity` field must repeat its own bound. Nothing enforces that, the same silent-omission risk ADR 0003 records for `ComplexProperty`.
- **Rounded domain check.** The domain check is necessarily on the rounded value for `ListPrice` and `ReorderPoint` (see B).
- **Smallest option.** **This is the smallest option consistent with constraints 1-6 that does not narrow intermediates.** It follows the existing `CodeMaxLength` pattern and needs no schema change.
- **ADR.** Whether to record the rule "value objects are unbounded; each persisted aggregate field carries its column bound", so that PR 8-9 follow it, is the human's call. A2 itself introduces no new mechanism.

**Options that fall short alone.**
- **Validators only** fail constraint 2 on undecorated handlers and `UpdatePrice`, and fail constraint 4.
- **An Infrastructure guard** (a converter or a SaveChanges interceptor) turns an expected failure into an exception, against `CLAUDE.md`'s Result rule.
- **A CHECK constraint** is defense in depth only (see Data model). It is a migration and requires the db-reviewer.

### B. Raw input versus rounded value

Which layer sees what:
- The validators see the raw command `decimal`.
- The product handlers round through `Money.Of` / `Quantity.Of` (`CreateProductCommandHandler.cs:52,58`; `UpdateProductCommandHandler.cs:52,58`) before `Product` sees the value.
- `UnitOfMeasure.Create` receives the factor as typed (`CreateUnitOfMeasureCommandHandler.cs:45-46`).
- `Quantity.Of` already checks the raw value for the lower bound (`Quantity.cs:18`).

These are the boundary values constraint 6 asks for. A rounded check accepts an input when `Math.Round(x, s, ToEven)` fits; a raw check accepts it when `x` fits.

**ListPrice amount, upper (scale 4)**

| Input | `Money.Of` result | Raw | Rounded |
|---|---|---|---|
| `99999999999999.9998` | same | accept | accept |
| `99999999999999.9999` (M4) | same | accept | accept |
| `99999999999999.99991` … `99999999999999.99994` | M4 | reject | **accept, stored as M4** |
| `99999999999999.99995` (midpoint; the kept 9 is odd) | `100000000000000.0000` | reject | reject |
| `100000000000000` | same | reject | reject |

The rounded check's cut-off is `x < 99999999999999.99995`.

**ListPrice amount, lower (0, constraint 4)**

| Input | `Money.Of` result | Raw | Rounded |
|---|---|---|---|
| `0.0001` | same | accept | accept |
| `0` | same | accept | accept |
| `-0.00004` | `0.0000` (negative zero UNVERIFIED) | reject | **accept as 0** |
| `-0.00005` (midpoint; the kept 0 is even) | `0.0000` | reject | **accept as 0** |
| `-0.00006` | `-0.0001` | reject | reject |
| `-0.0001` | same | reject | reject |

The rounded check accepts `[-0.00005, 0)`.

**ReorderPoint, upper (scale 6)**

| Input | `Quantity.Of` result | Raw | Rounded |
|---|---|---|---|
| `999999999999.999998` | same | accept | accept |
| `999999999999.999999` (M6) | same | accept | accept |
| `999999999999.9999994` | M6 | reject | **accept, stored as M6** |
| `999999999999.9999995` (midpoint; the kept 9 is odd) | `1000000000000.000000` | reject | reject |
| `1000000000000` | same | reject | reject |

**ReorderPoint, lower.** The semantics are already raw at both layers: the validator applies `>= 0` (`CreateProductCommandValidator.cs:43-45`), and `Quantity.Of` checks `value < 0` before rounding.
- `-0.0000004` is rejected even though it rounds to 0.
- A rounded upper check would make `Quantity` raw at the bottom and rounded at the top.

**ConversionFactor: question B does not apply.** Nothing rounds it, and constraint 1 accepts only a value equal to its own 6-place rounding, so raw equals rounded for every accepted factor. The boundary values:
- **Lower, rejected:** `0` (MustBePositive); `0.0000001`, which would round to zero; `0.0000005`, a midpoint that ToEven rounds to `0.000000`.
- **Lower, accepted:** `0.000001`; `0.0000010` (equal to `0.000001` by value); `1.500000000` (trailing zeros).
- **Rejected by constraint 1:** `0.1234567`.
- **Upper, accepted:** `999999999999.999998` and `999999999999.999999`.
- **Upper, rejected:** `999999999999.9999994`, by constraint 1 rather than by the bound; `1000000000000`, representable but over the bound.
- **Base unit:** `1` and `1.000000000` are both accepted, because `!= 1m` compares by value.

**How A × B plays out by layer**

| Option | Validator (raw) | `Money.Of` / `Quantity.Of` | `Product` (rounded) | `UnitOfMeasure.Create` (typed) | SQL |
|---|---|---|---|---|---|
| A1 + raw | raw `<= max` and `>= 0`; the same set as the value object for the upper bound | the bound is checked before rounding, so `…99994` fails | only the negative-price check; `[-0.00005, 0)` passes as 0 while the validator rejects it | constraint 1 + upper | always in range |
| A1 + rounded | raw rejects `…99994` while the value object accepts it, unless the validator replicates the rounding | the bound is checked after rounding, so `…99994` becomes M4 | as above | as above | always in range |
| A2 + raw | raw `<= max` and `>= 0` | unchanged | **cannot see the raw value**, so raw semantics exist only in the validator, and the domain is effectively rounded | constraint 1 + upper | always in range |
| A2 + rounded | either raw (stricter than the domain; precedent `Code`/`Trim`) or replicating `Math.Round(x, 4 or 6, ToEven)`, which needs the scale outside the private `DecimalPlaces` constants | unchanged | rounded checks: `…99994` accepted as M4, `…99995` rejected; `-0.00004` and `-0.00005` accepted as 0 | constraint 1 + upper | always in range |

- A raw validator rejects a superset of what a rounded domain rejects, at both ends. Through the API, then, the validator always fires first and the domain code is reachable only on undecorated paths.
- With raw semantics, the round-trip test for `…99994` / `…9999994` expects a 400. With rounded semantics, it expects a 201 and a stored M4 / M6.

### Further decisions the human owns

These touch user-facing behavior or the public contract.
1. **New codes.** The names, and whether `ConversionFactor` gets one code or two (too many decimal places, and too large). The new `Product` codes would follow the existing `<Aggregate>.<Field><Condition>` style.
2. **Turkish message wording**, including how the maximum is written (for example Turkish grouping `99.999.999.999.999,9999` versus `99999999999999.9999`). Existing messages embed the literal limit.
3. **Check order** in `UnitOfMeasure.Create` when both the precision rule and the upper bound fail, which decides the message shown.
4. **Rounded inputs below zero.** Whether the domain may accept `[-0.00005, 0)` as zero (rounded semantics) while the validator rejects it, and whether a negative-zero `Money` counts as negative.
5. **Pre-fix characterization tests.** Whether to run them, so that the UNVERIFIED SqlClient and HTTP behavior is recorded before the fix makes it unreachable.
6. **An ADR.** Whether one is wanted under A2. Under A1 one is needed.
