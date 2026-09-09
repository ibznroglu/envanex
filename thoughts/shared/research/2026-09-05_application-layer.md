# Research: Application Layer (PR 5)

## Question

What is the current state of the Envanex.Application project and the Domain surface it will
consume? What EF configurations and architectural patterns must Application layer use cases,
DTOs, and validators follow?

## Relevant files

**Application layer structure:**
- `src/Envanex.Application/Envanex.Application.csproj` — references Domain only; declares FluentValidation dependency
- `src/Envanex.Application/` — folder exists but is empty except for obj/ and csproj
- `tests/Envanex.Application.Tests/Envanex.Application.Tests.csproj` — references Application; is empty except for obj/

**Domain aggregates to be consumed:**
- `src/Envanex.Domain/Aggregates/Products/Product.cs` (line 34–74: static Create factory, UpdatePrice, Activate, Deactivate)
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs` (line 30–77: static Create factory with base unit validation, Activate, Deactivate)
- `src/Envanex.Domain/Aggregates/Warehouses/Warehouse.cs` (line 25–52: static Create factory, Activate, Deactivate)

**Value objects and factory methods:**
- `src/Envanex.Domain/ValueObjects/Money.cs` (line 18–23: Money.Of → Result\<Money\>; line 25–36, 38–49: Add/Subtract → Result\<Money\>; line 51–55: Multiply → Money directly; line 57–61: Zero factory)
- `src/Envanex.Domain/ValueObjects/Quantity.cs` (line 16–25: Quantity.Of → Result\<Quantity\>; line 27–31: Add → Quantity directly; line 33–44: Subtract/Multiply → Result\<Quantity\>; line 59: Quantity.Zero)
- `src/Envanex.Domain/ValueObjects/Currency.cs` (line 16–31: Currency.Of → Result\<Currency\>; line 33–35: static TRY, USD, EUR)

**Result\<T\> API:**
- `src/Envanex.Domain/Common/Result.cs` (line 3–40: Result and Result\<T\> classes)
- `src/Envanex.Domain/Common/Error.cs` (line 3–5: Error sealed record with Code and Message; line 5: Error.None)

**EF column configurations:**
- `src/Envanex.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` (line 15: Code HasMaxLength(50); line 18: Name HasMaxLength(200))
- `src/Envanex.Infrastructure/Persistence/Configurations/UnitOfMeasureConfiguration.cs` (line 14: Code HasMaxLength(20); line 17: Name HasMaxLength(200); line 19: ConversionFactor HasPrecision(18, 6))
- `src/Envanex.Infrastructure/Persistence/Configurations/WarehouseConfiguration.cs` (line 14: Code HasMaxLength(20); line 17: Name HasMaxLength(200))

**RowVersion shadow property:**
- `src/Envanex.Infrastructure/Persistence/Conventions/AggregateRootConvention.cs` (line 23, 28: property name "RowVersion" hardcoded; line 31: SetIsConcurrencyToken(true); line 32: SetValueGenerated(OnAddOrUpdate))

**Architecture tests:**
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` (line 7–9: expected references arrays; line 54–58: regex extracts ProjectReference only, not PackageReference)

**Integration test fixtures:**
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` (line 22–32: ResetAsync; line 34–40: InitializeAsync calls MigrateAsync; line 13–20: CreateDbContext)
- `tests/Envanex.IntegrationTests/Fixtures/DatabaseCollection.cs` (line 5–10: xUnit collection definition)
- `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj` (line 22: references Envanex.Web; line 12: Testcontainers.MsSql)

**Web project dependency chain:**
- `src/Envanex.Web/Envanex.Web.csproj` (line 4: references Infrastructure; line 5: references SoapApi)
- `src/Envanex.Web/Program.cs` (line 1: using Envanex.Infrastructure; line 11: AddInfrastructure)
- `src/Envanex.Infrastructure/DependencyInjection.cs` (line 28–29: only AddDbContext registered; no Application DI)

**Package version management:**
- `Directory.Packages.props` (line 13: FluentValidation 12.1.1; line 16–17: EF Core 10.0.11; line 20: SoapCore 1.2.1.14)

## Current behavior

### 1. Envanex.Application project

Project exists and is empty (no namespaces, no classes, no interfaces). Only contains
`Envanex.Application.csproj` with:
- ProjectReference to Envanex.Domain
- PackageReference to FluentValidation (version managed in Directory.Packages.props, 12.1.1)

No folder structure created yet.

### 2. Domain aggregate surface

**Product.Create** (`Product.cs:34`):
```
static Result<Product> Create(string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)
```
- ArgumentNullException.ThrowIfNull on listPrice (line 36)
- Returns Failure: CodeRequired (empty code), NameRequired (empty name), UnitOfMeasureRequired (Guid.Empty)
- Normalizes: code → trim + ToUpperInvariant; name → trim
- Sets IsActive = true, generates id via Guid.CreateVersion7()

**Product.UpdatePrice** (`Product.cs:60`): `void UpdatePrice(Money newPrice)` — ThrowIfNull on newPrice (line 62)

**Product.Activate** (`Product.cs:71`): `void Activate()` — sets IsActive = true

**Product.Deactivate** (`Product.cs:66`): `void Deactivate()` — sets IsActive = false

**UnitOfMeasure.Create** (`UnitOfMeasure.cs:30`):
```
static Result<UnitOfMeasure> Create(string code, string name, Guid? baseUnitId, decimal conversionFactor)
```
- Returns Failure: CodeRequired, NameRequired, BaseUnitFactorMustBeOne (null baseUnit + factor≠1), InvalidBaseUnitId (Guid.Empty), ConversionFactorMustBePositive (factor≤0)
- Normalizes: code → trim + ToUpperInvariant; name → trim
- Sets IsActive = true

**UnitOfMeasure.Activate/Deactivate**: void, same pattern as Product

**Warehouse.Create** (`Warehouse.cs:25`):
```
static Result<Warehouse> Create(string code, string name)
```
- Returns Failure: CodeRequired, NameRequired
- Normalizes: code → trim + ToUpperInvariant; name → trim
- Sets IsActive = true

**Warehouse.Activate/Deactivate**: void, same pattern

### 3. Value object factories

**Money.Of** (`Money.cs:18`): `Result<Money> Of(decimal amount, Currency currency)`
- ThrowIfNull on currency (line 20)
- Rounds amount to 4 decimal places (MidpointRounding.ToEven)
- Always returns Result.Success — never returns failure

**Quantity.Of** (`Quantity.cs:16`): `Result<Quantity> Of(decimal value)`
- Returns Failure if value < 0 (QuantityErrors.Negative)
- Rounds to 6 decimal places

**Currency.Of** (`Currency.cs:16`): `Result<Currency> Of(string code)`
- Returns Failure if code null/whitespace or length ≠ 3 or not in ["TRY", "USD", "EUR"]

**Result API** (`Result.cs`):
- `Result.Success()` → Result (line 25)
- `Result.Failure(Error error)` → Result (line 27)
- `Result.Success<T>(T value)` → Result\<T\> (line 33)
- `Result.Failure<T>(Error error)` → Result\<T\> (line 35)
- `Result.IsSuccess` (bool), `Result.IsFailure` (bool computed), `Result.Error` (Error record)
- `Result<T>.Value` — throws if IsFailure (line 46–48)

### 4. EF column constraints

| Aggregate | Property | Max Length | Unique Index | Precision |
|-----------|----------|-----------|--------------|-----------|
| Product | Code | 50 | Yes | — |
| Product | Name | 200 | No | — |
| Product | ListPrice.Amount | — | No | 18,4 |
| Product | ListPrice.Currency | 3 | No | — |
| Product | ReorderPoint | — | No | 18,6 |
| UnitOfMeasure | Code | 20 | Yes | — |
| UnitOfMeasure | Name | 200 | No | — |
| UnitOfMeasure | ConversionFactor | — | No | 18,6 |
| Warehouse | Code | 20 | Yes | — |
| Warehouse | Name | 200 | No | — |

**FK configurations:**
- Product.UnitOfMeasureId → UnitOfMeasures.Id, OnDelete(Restrict), IsRequired
- UnitOfMeasure.BaseUnitId → UnitOfMeasures.Id, OnDelete(Restrict), IsRequired(false)

### 5. AggregateRootConvention — RowVersion

`AggregateRootConvention.cs`:
- Iterates all entity types, checks if derived from `AggregateRoot<>` via recursive base class walk (line 37–51)
- Adds shadow property named `"RowVersion"` (hardcoded string, line 28), type `byte[]`
- `SetIsConcurrencyToken(true, fromDataAnnotation: false)` (line 31)
- `SetValueGenerated(ValueGenerated.OnAddOrUpdate, fromDataAnnotation: false)` (line 32)
- Applied during `EnvanexDbContext.ConfigureConventions` (`EnvanexDbContext.cs:35`)

**"RowVersion" string occurrences:** 27 across codebase (convention, migration, test assertions).
No shared constant — all hardcoded.

### 6. ArchitectureTests — what each test does

`ArchitectureTests.cs`:

1. **Domain_ShouldNotReference_AnyProject** (line 12–16): reads `Domain.csproj`, expects `GetProjectReferences` returns empty array
2. **Application_ShouldOnlyReference_Domain** (line 19–24): reads `Application.csproj`, expects ProjectReference folder names == `["Envanex.Domain"]`
3. **Infrastructure_ShouldOnlyReference_DomainAndApplication** (line 27–32): reads `Infrastructure.csproj`, expects `["Envanex.Application", "Envanex.Domain"]`
4. **SoapApi_ShouldOnlyReference_Application** (line 35–40): reads `SoapApi.csproj`, expects `["Envanex.Application"]`

**Implementation** (line 54–58): regex pattern `@"<ProjectReference\s+Include=""[^""]*[/\\]([^/\\""]+)[/\\][^/\\""]+\.csproj"""`
- Matches **only `<ProjectReference>` elements**, NOT `<PackageReference>`
- Extracts folder name from Include path, returns sorted alphabetically
- **Out of scope:** Web and Worker projects are NOT tested by ArchitectureTests

### 7. Web project references and Program.cs

**Envanex.Web.csproj:**
- Direct references: Infrastructure (line 4), SoapApi (line 5)
- Does NOT directly reference Application — access is **transitive** via Infrastructure → Application
- Packages: DevExtreme.AspNet.Data, Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore.Design, Radzen.Blazor, Scalar.AspNetCore

**Program.cs middleware/DI order:**
1. `AddRazorComponents().AddInteractiveServerComponents()` (line 8–9)
2. `AddInfrastructure(builder.Configuration)` (line 11) — registers DbContext only
3. Dev: `AddOpenApi()` (line 15)
4. Pipeline: ExceptionHandler (non-dev) → HSTS (non-dev) → OpenApi (dev) → Scalar (dev) → StatusCodePages → HttpsRedirect → Antiforgery → StaticAssets → RazorComponents
5. `public partial class Program { }` (line 46) — for WebApplicationFactory\<Program\>

No REST endpoints, no controllers, no UseControllers call yet.

### 8. Directory.Packages.props vs csproj matching

| Package | Props line | Referenced in |
|---------|-----------|---------------|
| DevExtreme.AspNet.Data | 7 | Web only |
| FluentValidation | 13 | Application only |
| Microsoft.AspNetCore.Mvc.Testing | 28 | IntegrationTests only |
| Microsoft.EntityFrameworkCore.Design | 16 | Infrastructure, Web |
| Microsoft.EntityFrameworkCore.SqlServer | 17 | Infrastructure only |
| Radzen.Blazor | 9 | Web only |
| Scalar.AspNetCore | 10 | Web only |
| SoapCore | 20 | SoapApi only |
| Shouldly | 29 | Domain.Tests, IntegrationTests |
| Testcontainers.MsSql | 30 | IntegrationTests only |
| xunit | 31 | Domain.Tests, Application.Tests, IntegrationTests |

## Existing patterns to follow

1. **Result\<T\> for expected failures.** Callers access `.Value` only after checking `.IsSuccess`.
   `ArgumentNullException.ThrowIfNull` for null reference bugs (caller bug), not Result.Failure.

2. **Static factory `Create` on aggregates.** Returns `Result<AggregateType>`. Validates business
   rules, normalizes strings, generates ID via `Guid.CreateVersion7()`, sets `IsActive = true`.

3. **Private setters on all aggregate properties.** State changes via factory or named methods only.

4. **Value object `.Of` factories return `Result<T>`.** Callers must check Result before passing
   value objects to aggregate factories.

5. **Layer boundaries.** Application → Domain only. Infrastructure → Application + Domain.
   Web → Infrastructure + SoapApi (transitively gets Application and Domain).

6. **ArchitectureTests enforce ProjectReference rules** (not PackageReference). Worker and Web
   out of scope.

7. **RowVersion is a shadow property.** Convention-based, not exposed in domain. `byte[]`,
   concurrency token, generated on add/update.

8. **EF conventions for value objects.** Money → decimal(18,4) via MoneyComplexTypeConvention.
   Quantity → decimal(18,6) via ConfigureConventions. Currency → nvarchar(3) via converter.

9. **All FKs use DeleteBehavior.Restrict.** No cascading deletes.

10. **Integration tests use Testcontainers MsSql 2022.** InitializeAsync calls MigrateAsync.
    ResetAsync deletes in FK dependency order.

## Data model touched

Three aggregate roots already in Domain, waiting for Application use cases:

1. **Product** (table: Products) — Id, Code(50, unique), Name(200), UnitOfMeasureId(FK Restrict),
   ListPrice_Amount(18,4), ListPrice_Currency(3), ReorderPoint(18,6), IsActive, RowVersion(shadow)

2. **UnitOfMeasure** (table: UnitOfMeasures) — Id, Code(20, unique), Name(200),
   BaseUnitId(FK self-ref Restrict, nullable), ConversionFactor(18,6), IsActive, RowVersion(shadow)

3. **Warehouse** (table: Warehouses) — Id, Code(20, unique), Name(200), IsActive, RowVersion(shadow)

One migration exists: `20260904115921_InitialSchema`. No further migrations.

## Risks and unknowns

1. **RowVersion hardcoded string** — "RowVersion" appears as literal string in
   AggregateRootConvention (line 23, 28) and 27 places total. No shared constant.

2. **Application layer doesn't exist yet** — no use cases, DTOs, validators, or repository
   interfaces defined.

3. **DI for Application not registered** — Infrastructure.DependencyInjection only registers
   DbContext. No Application DI method exists.

4. **Repository pattern undefined** — Domain aggregate factories don't validate FK targets exist
   (e.g., Product.Create doesn't check UnitOfMeasureId exists). Application must handle this.

5. **No error handling for concurrency at domain level** — RowVersion is opaque to domain.
   DbUpdateConcurrencyException and DbUpdateException will surface at SaveChanges, not during
   domain factory calls. Application must catch and translate.

6. **FluentValidation declared but no validators exist** — pattern for DTO validators vs. domain
   factory validation not yet demonstrated.

7. **No repository interfaces in Domain** — expected by architecture (Domain has zero references).
   Repositories must be defined as interfaces in Application, implemented in Infrastructure.

8. **Application.Tests empty** — known gap, listed in roadmap.

9. **ArchitectureTests don't cover Web or Worker** — a circular reference involving Web would
   not be caught.

10. **Web accesses Application transitively, not directly** — no direct ProjectReference from
    Web to Application. If Web needs to register Application services, it may need the reference
    added, or Infrastructure.AddInfrastructure must do it.

## Open questions for the human

1. Should Application define repository interfaces (IProductRepository, etc.) with Infrastructure
   implementing them — or a different pattern?

2. Use case naming convention: CreateProductUseCase, CreateProductCommand+Handler, or something
   else? One class per aggregate or one per operation?

3. Should use cases return `Result<T>` (mirroring domain) or a different error shape?

4. FK pre-validation: should use cases check FK targets exist before calling domain factories,
   or rely on database constraints and translate DbUpdateException?

5. RowVersion access: should use cases/repositories expose RowVersion to callers, or keep it
   internal to Infrastructure?

6. DI registration: a new `AddApplication()` method in Application/DependencyInjection.cs, or
   added to Infrastructure's `AddInfrastructure()`?

7. Should Application.Tests have unit tests mocking repositories, or only integration tests in
   IntegrationTests?

8. Scope of PR 5: all three aggregates (Product, UnitOfMeasure, Warehouse) or start with one?
   Which operations — create only, or also update/activate/deactivate?
