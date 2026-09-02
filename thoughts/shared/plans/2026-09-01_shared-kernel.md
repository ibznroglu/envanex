# Plan: Shared Kernel Primitives

## Goal

Add the foundational domain building blocks to `Envanex.Domain`: `Result<T>` for predictable failures, `Entity<TId>` and `AggregateRoot<TId>` base types, `IDomainEvent` marker interface, and three value objects (`Money`, `Quantity`, `Currency`). No aggregates, no business rules, no EF configuration — only the primitives that future aggregates will build on.

## Non-goals

- No aggregate implementations (Product, StockMovement, etc.)
- No EF Core entity configuration or migrations
- No outbox, no domain event dispatcher
- No cross-currency arithmetic or conversion
- No RowVersion / concurrency token (deferred to Infrastructure, separate PR)
- No Application-layer changes

## Touches schema? No

## ADR needed? 0002 — Result over exceptions for predictable failures

## Rounding Policy

Money uses 4 decimal places (`decimal(18,4)` in EF). All Money arithmetic rounds to 4 decimal places using `MidpointRounding.ToEven` (banker's rounding). The `Money.Of` factory truncates/rounds the input to 4 decimal places before storing.

Quantity uses 6 decimal places (`decimal(18,6)` in EF). All Quantity arithmetic rounds to 6 decimal places using `MidpointRounding.ToEven`. The `Quantity.Of` factory truncates/rounds the input to 6 decimal places before storing.

This ensures that values stored in Domain types are always within the precision bounds that EF will persist, preventing silent truncation at the database layer.

## Analyzer Compliance Notes

- `TreatWarningsAsErrors=true` and `AnalysisLevel=latest-recommended` are active.
- All non-nullable fields/properties must be initialized. `Entity<TId>.Id` gets initialized via constructor parameter.
- `CA1062` (validate public arguments): public methods on value objects that accept reference types must null-check. For `Currency`, the `code` string parameter must be validated.
- `CA1707` (underscores): suppressed in tests via `.editorconfig` (`tests/**/*.cs`).
- `readonly` fields enforced as warning — all backing fields that don't change after construction must be `readonly`.
- All types get XML doc comments suppressed globally via `CS1591` in `Directory.Build.props` — no per-file pragmas needed.

## Value Object Type Strategy

`Money` and `Currency` are **sealed records** (reference types). `Quantity` is a **readonly record struct** (value type). The asymmetry is intentional: `default(Quantity)` produces `Value = 0m`, which is a valid quantity (zero stock is meaningful), so the struct default does not violate the type's invariants. By contrast, `default(Money)` would produce `Amount = 0m` with a `null` `Currency`, silently violating the invariant that every `Money` has a valid currency — making it a reference type means `default` is `null`, which the compiler and nullable analysis catch at call sites.

## Folder Structure (end state)

```
src/Envanex.Domain/
  Common/
    IDomainEvent.cs
    Entity.cs
    AggregateRoot.cs
    Result.cs
    Error.cs
  ValueObjects/
    Currency.cs
    Money.cs
    Quantity.cs
tests/Envanex.Domain.Tests/
  ArchitectureTests.cs          (existing, unchanged)
  Common/
    ResultTests.cs
    EntityTests.cs
    AggregateRootTests.cs
  ValueObjects/
    CurrencyTests.cs
    MoneyTests.cs
    QuantityTests.cs
```

---

## Phase 1: Result, Error, and IDomainEvent

### What

Introduce the `Error` record, `Result` (non-generic), `Result<T>` (generic), and the `IDomainEvent` marker interface. These have no dependencies on any other new type, so they form the foundation layer.

### Files

| Path | Action | What changes |
|---|---|---|
| `src/Envanex.Domain/Common/Error.cs` | create | `Error` sealed record with `Code` and `Message` properties |
| `src/Envanex.Domain/Common/Result.cs` | create | `Result` and `Result<T>` types |
| `src/Envanex.Domain/Common/IDomainEvent.cs` | create | Marker interface for domain events |
| `tests/Envanex.Domain.Tests/Common/ResultTests.cs` | create | Tests for Result and Result<T> |

### Signatures

```csharp
// src/Envanex.Domain/Common/Error.cs
namespace Envanex.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

```csharp
// src/Envanex.Domain/Common/Result.cs
namespace Envanex.Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Error Error { get; }

    // Protected constructor — only Result itself and Result<T> can instantiate
    protected Result(bool isSuccess, Error error);

    public static Result Success();
    public static Result Failure(Error error);
    public static Result<T> Success<T>(T value);
    public static Result<T> Failure<T>(Error error);
}

public class Result<T> : Result
{
    public T Value { get; }

    // Internal constructor — called by Result.Success<T> and Result.Failure<T>
    internal Result(T value, bool isSuccess, Error error);
}
```

```csharp
// src/Envanex.Domain/Common/IDomainEvent.cs
namespace Envanex.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
```

### Tests

**`tests/Envanex.Domain.Tests/Common/ResultTests.cs`** — class `ResultTests`:

| Test method | What it verifies |
|---|---|
| `Success_ShouldHaveIsSuccessTrue` | `Result.Success()` sets `IsSuccess = true`, `IsFailure = false` |
| `Success_ShouldHaveNoneError` | `Result.Success()` sets `Error` to `Error.None` |
| `Failure_ShouldHaveIsFailureTrueAndContainError` | `Result.Failure(error)` sets `IsFailure = true`, `Error` matches |
| `Failure_WithNullError_ShouldThrow` | `Result.Failure(null!)` throws `ArgumentNullException` |
| `GenericSuccess_ShouldContainValue` | `Result.Success<int>(42).Value` equals 42 |
| `GenericFailure_AccessingValue_ShouldThrow` | Accessing `Value` on a failed `Result<T>` throws `InvalidOperationException` |
| `GenericFailure_ShouldContainError` | `Result.Failure<int>(error).Error` matches the provided error |

### Validation

```bash
dotnet build src/Envanex.Domain -warnaserror
dotnet build tests/Envanex.Domain.Tests -warnaserror
dotnet test tests/Envanex.Domain.Tests --filter "FullyQualifiedName~ResultTests"
dotnet test tests/Envanex.Domain.Tests
dotnet format --verify-no-changes
```

---

## Phase 2: Entity and AggregateRoot

### What

Introduce `Entity<TId>` with identity-based equality and `AggregateRoot<TId>` with domain event collection. These depend on `IDomainEvent` from Phase 1.

### Files

| Path | Action | What changes |
|---|---|---|
| `src/Envanex.Domain/Common/Entity.cs` | create | `Entity<TId>` abstract base class |
| `src/Envanex.Domain/Common/AggregateRoot.cs` | create | `AggregateRoot<TId>` abstract class extending Entity |
| `tests/Envanex.Domain.Tests/Common/EntityTests.cs` | create | Tests for Entity identity equality |
| `tests/Envanex.Domain.Tests/Common/AggregateRootTests.cs` | create | Tests for AggregateRoot event collection |

### Signatures

```csharp
// src/Envanex.Domain/Common/Entity.cs
namespace Envanex.Domain.Common;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; private set; }

    protected Entity(TId id);

    // Protected parameterless constructor for EF Core materialization
    protected Entity();

    public bool Equals(Entity<TId>? other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right);
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right);
}
```

```csharp
// src/Envanex.Domain/Common/AggregateRoot.cs
namespace Envanex.Domain.Common;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id);

    // Protected parameterless constructor for EF Core materialization
    protected AggregateRoot();

    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    protected void RaiseDomainEvent(IDomainEvent domainEvent);
    public void ClearDomainEvents();
}
```

**EF Core parameterless constructor note:** `Entity<TId>.Id` is non-nullable, so the parameterless constructor sets `Id = default!`. EF Core immediately overwrites the value during materialization. No `#pragma` needed — `default!` satisfies the nullable analyzer.

### Tests

**`tests/Envanex.Domain.Tests/Common/EntityTests.cs`** — class `EntityTests`:

(Uses a private `TestEntity : Entity<Guid>` inside the test class)

| Test method | What it verifies |
|---|---|
| `Entities_WithSameId_ShouldBeEqual` | Two instances with the same `Guid` are `Equals` |
| `Entities_WithDifferentIds_ShouldNotBeEqual` | Two instances with different `Guid`s are not `Equals` |
| `Entity_ComparedToNull_ShouldNotBeEqual` | `entity.Equals(null)` returns false |
| `Entities_WithSameId_ShouldHaveSameHashCode` | `GetHashCode()` matches for same-id entities |
| `EqualityOperator_WithSameId_ShouldReturnTrue` | `==` operator works |
| `InequalityOperator_WithDifferentId_ShouldReturnTrue` | `!=` operator works |

**`tests/Envanex.Domain.Tests/Common/AggregateRootTests.cs`** — class `AggregateRootTests`:

(Uses a private `TestAggregate : AggregateRoot<Guid>` and `TestEvent : IDomainEvent`)

| Test method | What it verifies |
|---|---|
| `RaiseDomainEvent_ShouldAddEventToCollection` | After raising, `DomainEvents` contains the event |
| `ClearDomainEvents_ShouldEmptyCollection` | After clearing, `DomainEvents` is empty |
| `DomainEvents_ShouldReturnReadOnlyCollection` | Returns `IReadOnlyCollection`, not mutable list |
| `NewAggregate_ShouldHaveEmptyDomainEvents` | Freshly created aggregate has zero domain events |

### Validation

```bash
dotnet build src/Envanex.Domain -warnaserror
dotnet build tests/Envanex.Domain.Tests -warnaserror
dotnet test tests/Envanex.Domain.Tests --filter "FullyQualifiedName~EntityTests|FullyQualifiedName~AggregateRootTests"
dotnet test tests/Envanex.Domain.Tests
dotnet format --verify-no-changes
```

---

## Phase 3: Currency, Money, and Quantity Value Objects

### What

Introduce the three value objects. `Currency` and `Money` are **sealed records** (reference types) — their `default` is `null`, which nullable analysis catches, preventing invalid states. `Quantity` is a **readonly record struct** (value type) — its `default` is zero, which is a valid quantity. `Currency` validates ISO 4217 codes. `Money` carries amount + currency with arithmetic. `Quantity` is a non-negative decimal with arithmetic. All factories return `Result<T>`.

### Files

| Path | Action | What changes |
|---|---|---|
| `src/Envanex.Domain/ValueObjects/Currency.cs` | create | `Currency` sealed record |
| `src/Envanex.Domain/ValueObjects/Money.cs` | create | `Money` sealed record |
| `src/Envanex.Domain/ValueObjects/Quantity.cs` | create | `Quantity` readonly record struct |
| `tests/Envanex.Domain.Tests/ValueObjects/CurrencyTests.cs` | create | Tests for Currency |
| `tests/Envanex.Domain.Tests/ValueObjects/MoneyTests.cs` | create | Tests for Money |
| `tests/Envanex.Domain.Tests/ValueObjects/QuantityTests.cs` | create | Tests for Quantity |

### Signatures

```csharp
// src/Envanex.Domain/ValueObjects/Currency.cs
namespace Envanex.Domain.ValueObjects;

public sealed record Currency
{
    public string Code { get; }

    private Currency(string code);

    // Factory: validates 3-letter uppercase ISO 4217 code against a known set
    public static Result<Currency> Of(string code);

    // Well-known currencies (static readonly)
    public static readonly Currency TRY;
    public static readonly Currency USD;
    public static readonly Currency EUR;
}
```

**Currency validation:** `Of` accepts a string, trims and uppercases it, then checks against a `HashSet<string>` of supported ISO 4217 codes. Initial set: TRY, USD, EUR. Unrecognized codes return `Result.Failure<Currency>(CurrencyErrors.InvalidCode)`.

```csharp
// src/Envanex.Domain/ValueObjects/Money.cs
namespace Envanex.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency);

    // Factory: rounds to 4 decimal places (banker's rounding), allows negative
    public static Result<Money> Of(decimal amount, Currency currency);

    // Arithmetic — returns Result to signal currency mismatch
    public Result<Money> Add(Money other);
    public Result<Money> Subtract(Money other);

    // Scalar multiply — always succeeds, rounds result to 4 decimal places
    public Money Multiply(decimal factor);

    // Zero value for a given currency
    public static Money Zero(Currency currency);
}
```

**Money allows negative amounts** — credit notes, refunds, adjustments need negative money. Sign constraints are aggregate-level rules.

```csharp
// src/Envanex.Domain/ValueObjects/Quantity.cs
namespace Envanex.Domain.ValueObjects;

public readonly record struct Quantity
{
    public decimal Value { get; }

    private Quantity(decimal value);

    // Factory: rejects negative, rounds to 6 decimal places (banker's rounding)
    public static Result<Quantity> Of(decimal value);

    // Arithmetic — returns Result for underflow
    public Result<Quantity> Add(Quantity other);
    public Result<Quantity> Subtract(Quantity other);

    // Scalar multiply — rejects negative result
    public Result<Quantity> Multiply(decimal factor);

    public static readonly Quantity Zero;
}
```

**Error constants** (co-located in each value object's file):

```csharp
public static class CurrencyErrors
{
    public static readonly Error InvalidCode = new("Currency.InvalidCode", "The currency code is not a valid ISO 4217 code.");
}

public static class MoneyErrors
{
    public static readonly Error CurrencyMismatch = new("Money.CurrencyMismatch", "Cannot perform arithmetic on Money with different currencies.");
}

public static class QuantityErrors
{
    public static readonly Error Negative = new("Quantity.Negative", "Quantity cannot be negative.");
    public static readonly Error NegativeResult = new("Quantity.NegativeResult", "The operation would result in a negative quantity.");
}
```

### Tests

**`tests/Envanex.Domain.Tests/ValueObjects/CurrencyTests.cs`** — class `CurrencyTests`:

| Test method | What it verifies |
|---|---|
| `Of_WithValidCode_ShouldSucceed` | `Currency.Of("TRY")` returns success |
| `Of_WithLowercaseCode_ShouldNormalize` | `Currency.Of("try")` returns success with `Code == "TRY"` |
| `Of_WithInvalidCode_ShouldFail` | `Currency.Of("XYZ")` returns failure with `CurrencyErrors.InvalidCode` |
| `Of_WithEmptyString_ShouldFail` | `Currency.Of("")` returns failure |
| `Of_WithNull_ShouldFail` | `Currency.Of(null!)` returns failure |
| `Of_WithWrongLength_ShouldFail` | `Currency.Of("US")` returns failure |
| `WellKnown_TRY_ShouldHaveCorrectCode` | `Currency.TRY.Code` equals `"TRY"` |
| `Currencies_WithSameCode_ShouldBeEqual` | Structural equality from record |
| `Currencies_WithDifferentCode_ShouldNotBeEqual` | Structural inequality from record |

**`tests/Envanex.Domain.Tests/ValueObjects/MoneyTests.cs`** — class `MoneyTests`:

| Test method | What it verifies |
|---|---|
| `Of_WithValidAmountAndCurrency_ShouldSucceed` | `Money.Of(100.50m, Currency.TRY)` succeeds |
| `Of_ShouldRoundToFourDecimalPlaces` | `Money.Of(10.12345m, Currency.TRY)` rounds to `10.1235m` (banker's) |
| `Of_WithNegativeAmount_ShouldSucceed` | Negative amounts are allowed |
| `Of_WithNullCurrency_ShouldThrow` | `Money.Of(10m, null!)` throws `ArgumentNullException` |
| `Add_SameCurrency_ShouldSucceed` | `10 TRY + 5 TRY = 15 TRY` |
| `Add_DifferentCurrency_ShouldFail` | `10 TRY + 5 USD` returns `MoneyErrors.CurrencyMismatch` |
| `Subtract_SameCurrency_ShouldSucceed` | `10 TRY - 3 TRY = 7 TRY` |
| `Subtract_DifferentCurrency_ShouldFail` | `10 TRY - 3 USD` returns failure |
| `Multiply_ShouldScale_AndRound` | `10.1234 TRY * 3 = 30.3702 TRY` (rounded to 4 decimals) |
| `Zero_ShouldHaveZeroAmount` | `Money.Zero(Currency.TRY).Amount == 0m` |
| `Money_WithSameAmountAndCurrency_ShouldBeEqual` | Structural equality |
| `Money_WithDifferentAmount_ShouldNotBeEqual` | Structural inequality |
| `Money_WithDifferentCurrency_ShouldNotBeEqual` | Structural inequality |

**`tests/Envanex.Domain.Tests/ValueObjects/QuantityTests.cs`** — class `QuantityTests`:

| Test method | What it verifies |
|---|---|
| `Of_WithPositiveValue_ShouldSucceed` | `Quantity.Of(10.5m)` succeeds |
| `Of_WithZero_ShouldSucceed` | `Quantity.Of(0m)` succeeds |
| `Of_WithNegative_ShouldFail` | `Quantity.Of(-1m)` returns `QuantityErrors.Negative` |
| `Of_ShouldRoundToSixDecimalPlaces` | `Quantity.Of(1.1234567m)` rounds to `1.123457m` |
| `Add_ShouldSumValues` | `Quantity(10) + Quantity(5) = Quantity(15)` |
| `Subtract_WithSufficientAmount_ShouldSucceed` | `Quantity(10) - Quantity(3) = Quantity(7)` |
| `Subtract_WouldGoNegative_ShouldFail` | `Quantity(3) - Quantity(5)` returns `QuantityErrors.NegativeResult` |
| `Multiply_WithPositiveFactor_ShouldSucceed` | `Quantity(10) * 2.5 = Quantity(25)` |
| `Multiply_WithNegativeFactor_ShouldFail` | `Quantity(10) * -1` returns failure |
| `Multiply_ShouldRoundToSixDecimalPlaces` | Result rounded correctly |
| `Zero_ShouldHaveZeroValue` | `Quantity.Zero.Value == 0m` |
| `Quantities_WithSameValue_ShouldBeEqual` | Structural equality |

### Validation

```bash
dotnet build src/Envanex.Domain -warnaserror
dotnet build tests/Envanex.Domain.Tests -warnaserror
dotnet test tests/Envanex.Domain.Tests --filter "FullyQualifiedName~CurrencyTests|FullyQualifiedName~MoneyTests|FullyQualifiedName~QuantityTests"
dotnet test tests/Envanex.Domain.Tests
dotnet format --verify-no-changes
```

---

## Phase 4: CLAUDE.md Corrections and ADR Stub

### What

Fix inaccurate statements in CLAUDE.md and create the ADR 0002 empty stub. Documentation-only phase, no .cs changes.

### Files

| Path | Action | What changes |
|---|---|---|
| `CLAUDE.md` | modify | Three corrections in Key Patterns / Architecture sections |
| `docs/adr/0002-result-over-exceptions.md` | create | Empty ADR stub (human fills content) |

### CLAUDE.md Changes

**Change 1 — Result<T> location (Key Patterns section):**

Current:
```
- **Result<T>, not exceptions**, for expected failures (validation, business rule violations).
  Exceptions are for bugs and infrastructure faults only.
```

Replace with:
```
- **Result<T>, not exceptions**, for expected failures (validation, business rule violations).
  `Result<T>` and `Error` live in `Envanex.Domain.Common` so that aggregates can return
  domain errors directly. Exceptions are for bugs and infrastructure faults only.
```

**Change 2 — Application description (Architecture section):**

Current:
```
  Envanex.Application     use cases, DTOs, validators, Result<T>. References Domain only.
```

Replace with:
```
  Envanex.Application     use cases, DTOs, validators. References Domain only.
```

**Change 3 — RowVersion clarification (EF Core conventions):**

Current:
```
- **EF Core conventions:** money is `decimal(18,4)`, quantity is `decimal(18,6)`, every aggregate
  root has a `RowVersion` concurrency token, every FK is explicitly configured, no lazy loading.
```

Replace with:
```
- **EF Core conventions:** money is `decimal(18,4)`, quantity is `decimal(18,6)`, every aggregate
  root has a `RowVersion` concurrency token (EF shadow property configured in Infrastructure),
  every FK is explicitly configured, no lazy loading.
```

### ADR 0002 Stub

```markdown
# 0002 — Result over exceptions for predictable failures

## Context

## Decision

## Alternatives

## Consequences
```

### Validation

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Rollback Notes

- All new code is additive — no existing files are modified except `CLAUDE.md` (Phase 4).
- Rollback: `git checkout main -- CLAUDE.md` plus deleting the new files/folders.
- No NuGet packages added to Domain — zero-dependency invariant preserved.
- No schema changes, no migrations, no database impact.
- Revert order (if needed): Phase 4, 3, 2, 1 (Phase 3 depends on Phase 1 for `Result<T>`, Phase 2 depends on Phase 1 for `IDomainEvent`).
