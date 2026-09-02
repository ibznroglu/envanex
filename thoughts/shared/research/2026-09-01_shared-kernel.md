# Research: Shared Kernel Primitives for PR 2

## Question

What is the current state of the Envanex codebase regarding the Domain layer and shared kernel patterns? What patterns, constraints, and configurations must PR 2's shared kernel implementation follow?

## Relevant files

- `src/Envanex.Domain/Envanex.Domain.csproj` — Domain project, currently empty except generated files
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — enforces layered architecture, uses xUnit + Shouldly
- `tests/Envanex.Domain.Tests/Envanex.Domain.Tests.csproj` — test project configuration
- `Directory.Build.props` — enforces net10.0, nullable=enable, TreatWarningsAsErrors=true, GenerateDocumentationFile=true (CS1591 suppressed)
- `.editorconfig` — LF line endings, indent=4 spaces for .cs, test method underscores allowed (CA1707), readonly fields enforced
- `Directory.Packages.props` — Central Package Management (CPM): all paket versions pinned in one file
- `CLAUDE.md` — Key Patterns section, architectural decisions, result pattern, aggregate patterns, EF conventions
- `docs/adr/0001-layered-architecture-and-dependency-rule.md` — architectural decision record
- `Envanex.slnx` — solution structure (XML format, .NET 10+, merge-friendly)
- `.gitattributes` — enforces LF (text=auto eol=lf), CRLF only for *.bat/*.cmd/*.ps1

## Current behavior

**Envanex.Domain project state:**
- Empty except for generated files (GlobalUsings.g.cs, .NETCoreApp AssemblyAttributes, AssemblyInfo)
- No hand-written .cs files exist yet
- Directory structure: `src/Envanex.Domain/Envanex.Domain.csproj` (3 lines only)

**Test infrastructure:**
- `Envanex.Domain.Tests` exists with only `ArchitectureTests.cs` (no business tests yet)
- Test framework: **xUnit 2.9.3** (via Directory.Packages.props)
- Assertion library: **Shouldly 4.3.0** (fluent syntax: `actual.ShouldBe(expected)`)
- Global using: xUnit is implicitly available (`<Using Include="Xunit" />`)
- ArchitectureTests validates project reference constraints at test time by parsing `.csproj` files directly (regex on XML, not reflection on compiled assemblies)
- Namespace convention: `Envanex.Domain.Tests` with `[Fact]` attribute for xUnit

**Compiler and analyzer settings (from Directory.Build.props):**
- Target Framework: `net10.0`
- C# Version: `latest`
- Nullable: `enable` (strict null safety)
- Implicit Usings: `enable` (System.* auto-included)
- Treat Warnings As Errors: `true` (any compiler/analyzer warning → build failure)
- Enforce Code Style In Build: `true`
- Analysis Level: `latest-recommended` (CA**** analyzer rules at latest safe level)
- Generate Documentation File: `true` (XML doc generation for IntelliSense)
- NoWarn: `CS1591` (missing XML doc comment warnings suppressed)

**.editorconfig rules (relevant to Domain):**
- Line ending: LF only (enforced via git)
- Indent: 4 spaces for `.cs` files
- CA1062 (validate public arguments): warning level
- CS8618 (non-nullable field uninitialized): error level
- CS8602 (possible null dereference): error level
- CA1707 (underscores in identifiers): warning in prod code, none in tests
- var for built-in types: false (explicit types preferred)
- Expression-bodied methods: when on single line (suggestion)
- Braces required: true (warning)
- Readonly fields enforced: true (warning)

**Solution structure (Envanex.slnx):**
- 6 src projects: Domain (innermost), Application, Infrastructure, SoapApi, Web, Worker
- 3 test projects: Domain.Tests, Application.Tests, IntegrationTests
- Dependency rule: `Domain` → (zero references), `Application` → Domain only, `Infrastructure` → Application + Domain, `SoapApi` → Application only
- Architecture tests (4 [Fact] methods) enforce dependency rule by reading `.csproj` XML

**NuGet packages for Domain ecosystem (Directory.Packages.props):**
- **Test infrastructure:** Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4, coverlet.collector 6.0.4, Shouldly 4.3.0
- **Application (Domain's client):** FluentValidation 12.1.1
- No packages currently declared as dependencies of Domain itself (Domain is pure C#, zero external deps)

## Existing patterns to follow

### Result<T> for Predictable Failures
- Expected failures (validation, business rule violations) → wrap in `Result<T>`, not exceptions
- Exceptions reserved for bugs and infrastructure faults only

### Aggregate Patterns
- Aggregates own their invariants
- State changes go through methods on the aggregate root
- **All setters are `private set`** (immutability at boundary)
- Aggregate roots have `RowVersion` concurrency token (EF Core optimistic concurrency)

### EF Core Conventions (Critical for Domain entities)
- Money: `decimal(18,4)` precision
- Quantity: `decimal(18,6)` precision
- Every aggregate root: `RowVersion` (byte[]) concurrency token
- Every foreign key: explicitly configured
- No lazy loading

### Costing Strategy Pattern
- Strategy interface: `ICostingStrategy`
- Default: moving average, second: FIFO
- Never inline costing formula into use case

### Append-Only Stock Ledger
- `StockMovement` rows never updated or deleted
- `StockBalance` is a projection
- Any change to stock = new movement row

## Data model touched

**Zero schema changes in PR 2.** Shared kernel creates domain value objects and base types only. No EF migrations needed.

## Risks and unknowns

### Risk 1: Result<T> Location
- CLAUDE.md states "Result<T>, not exceptions" but doesn't specify where the type lives
- Likely in Application (use case concern) but could be Domain

### Risk 2: Aggregate Base Class or Interface?
- Domain is empty — no existing base type to follow
- Need to decide: `AggregateRoot` base class with `RowVersion` vs. pure convention

### Risk 3: Value Object Pattern
- Common DDD: `ValueObject` base with `GetEqualityComponents()` for value equality
- Not explicitly mentioned in CLAUDE.md

### Risk 4: TreatWarningsAsErrors Strictness
- All types must be very clean: no unused variables, uninitialized non-nullable fields
- `string Name { get; private set; }` → CS8618 error without initializer

### Risk 5: Domain Layer Isolation
- Domain references zero other Envanex projects
- ArchitectureTests catch violations at test time

### Risk 6: Nullable Reference Types
- `Nullable=enable` — every field/property must declare nullability intent
- Non-nullable fields must be initialized

## Open questions for the human

1. **Where does `Result<T>` live?** Domain (unlikely) or Application? If not yet defined, where should it go?
2. **Aggregate root pattern:** Base class with `RowVersion` and domain events, or convention only?
3. **Value object base class:** Define `ValueObject` base with value equality, or standalone types?
4. **Concurrency token:** `RowVersion` as a domain property or EF shadow property only?
5. **Domain event pattern:** Establish `DomainEvent` base + `IHaveDomainEvents` in PR 2, or defer?
6. **Entity configuration:** Shared kernel establish EF mapping base, or Infrastructure owns all mapping?
