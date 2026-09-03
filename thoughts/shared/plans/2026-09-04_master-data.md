# Plan: Master Data Aggregates (Product, UnitOfMeasure, Warehouse)

## Goal

Ilk gercek domain aggregate'lerini (UnitOfMeasure, Warehouse, Product) olusturmak, EF Core konfigurasyonlarini yazmak, ilk migration'i uretmek ve mevcut TestProduct'a dayali integration testleri gercek Product aggregate'ine gecirmek. PR sonunda uc tablo SQL Server'da var olacak, tum konvansiyonlar (RowVersion, Money precision, Quantity precision, Currency converter) gercek production kodunda dogrulanmis olacak.

## Non-goals

- Application layer'a hicbir sey eklenmeyecek (repository interface, use case, DTO yok — bunlar PR 5).
- Blazor UI, REST endpoint, SOAP contract yok.
- Envanex.Application.Tests bos kalacak — bilinen bosluk.
- Domain event tanimlanmayacak (aggregate'ler event raise etmiyor henuz).

## Touches schema? Yes — db-reviewer required

Uc yeni tablo: `UnitOfMeasures`, `Warehouses`, `Products`. Ilk migration uretilecek. Self-referencing FK (UnitOfMeasure.BaseUnitId), cross-table FK (Product.UnitOfMeasureId), unique index'ler (Code kolonlari).

## ADR needed? 0004 — Aggregate identifiers and natural keys

Sadece stub dosya olusturulacak, icerigi insan yazacak.

---

## Phase 1: UnitOfMeasure aggregate + domain tests

### Ozet

Ilk ve en basit aggregate. Self-referencing FK mantigi (base unit vs derived unit) domain'de dogrulanir. Bu fazda sadece Domain projesine dokunulur, Infrastructure'a dokunulmaz.

### Files

| Path | Action | Description |
|------|--------|-------------|
| `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs` | created | Aggregate root |
| `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasureErrors.cs` | created | Error sabitleri |
| `tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs` | created | Unit testleri |

### Signatures

```csharp
// UnitOfMeasure.cs
namespace Envanex.Domain.Aggregates.UnitOfMeasures;

public sealed class UnitOfMeasure : AggregateRoot<Guid>
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid? BaseUnitId { get; private set; }
    public decimal ConversionFactor { get; private set; }
    public bool IsActive { get; private set; }

    private UnitOfMeasure() : base() { /* EF Core */ }
    private UnitOfMeasure(Guid id, string code, string name, Guid? baseUnitId, decimal conversionFactor) : base(id) { }

    public static Result<UnitOfMeasure> Create(string code, string name, Guid? baseUnitId, decimal conversionFactor);
    public void Deactivate();
    public void Activate();
}
```

Fabrika kurallari:
- `code` bos/whitespace ise -> `UnitOfMeasureErrors.CodeRequired`
- `code` normalize edilir: `code.Trim().ToUpperInvariant()`
- `name` bos/whitespace ise -> `UnitOfMeasureErrors.NameRequired`
- `name` normalize edilir: `name.Trim()`
- Base unit (baseUnitId == null) ise conversionFactor 1 olmali -> `UnitOfMeasureErrors.BaseUnitFactorMustBeOne`
- Derived unit (baseUnitId != null) ise conversionFactor > 0 olmali -> `UnitOfMeasureErrors.ConversionFactorMustBePositive`
- Derived unit icin baseUnitId == Guid.Empty ise -> `UnitOfMeasureErrors.InvalidBaseUnitId`
- Id: `Guid.CreateVersion7()`
- IsActive: `true` (varsayilan)

```csharp
// UnitOfMeasureErrors.cs
namespace Envanex.Domain.Aggregates.UnitOfMeasures;

public static class UnitOfMeasureErrors
{
    public static readonly Error CodeRequired;
    public static readonly Error NameRequired;
    public static readonly Error BaseUnitFactorMustBeOne;
    public static readonly Error ConversionFactorMustBePositive;
    public static readonly Error InvalidBaseUnitId;
}
```

### Tests to add

**`tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `Create_BaseUnit_WithValidInputs_ShouldSucceed` | code/name set, baseUnitId null, factor 1, isActive true |
| `Create_ShouldNormalizeCode_TrimAndUpperInvariant` | `" adet "` -> `"ADET"` |
| `Create_ShouldTrimName` | `" Kilogram "` -> `"Kilogram"` |
| `Create_WithEmptyCode_ShouldFail` | empty string -> CodeRequired |
| `Create_WithWhitespaceCode_ShouldFail` | whitespace -> CodeRequired |
| `Create_WithEmptyName_ShouldFail` | empty string -> NameRequired |
| `Create_BaseUnit_WithNonOneConversionFactor_ShouldFail` | baseUnitId null + factor 2 -> BaseUnitFactorMustBeOne |
| `Create_DerivedUnit_WithValidInputs_ShouldSucceed` | baseUnitId set, factor > 0, success |
| `Create_DerivedUnit_WithZeroFactor_ShouldFail` | factor 0 -> ConversionFactorMustBePositive |
| `Create_DerivedUnit_WithNegativeFactor_ShouldFail` | factor -1 -> ConversionFactorMustBePositive |
| `Create_DerivedUnit_WithEmptyGuidBaseUnitId_ShouldFail` | Guid.Empty -> InvalidBaseUnitId |
| `Deactivate_ShouldSetIsActiveFalse` | IsActive false olur |
| `Activate_ShouldSetIsActiveTrue` | Deactivate sonrasi Activate -> true |
| `Create_ShouldGenerateVersion7Id` | Id != Guid.Empty |

### Validation

```bash
dotnet build -warnaserror
dotnet test --filter "FullyQualifiedName~UnitOfMeasureTests"
```

---

## Phase 2: Warehouse + Product aggregates + domain tests

### Ozet

Kalan iki aggregate'i ekler. Product, Money ve Quantity value object'lerini icerir, UnitOfMeasureId FK'sine sahiptir. Bu faz hala sadece Domain katmanina dokunur.

### Files

| Path | Action | Description |
|------|--------|-------------|
| `src/Envanex.Domain/Aggregates/Warehouses/Warehouse.cs` | created | Aggregate root |
| `src/Envanex.Domain/Aggregates/Warehouses/WarehouseErrors.cs` | created | Error sabitleri |
| `src/Envanex.Domain/Aggregates/Products/Product.cs` | created | Aggregate root, Money + Quantity tasiyor |
| `src/Envanex.Domain/Aggregates/Products/ProductErrors.cs` | created | Error sabitleri |
| `tests/Envanex.Domain.Tests/Aggregates/Warehouses/WarehouseTests.cs` | created | Unit testleri |
| `tests/Envanex.Domain.Tests/Aggregates/Products/ProductTests.cs` | created | Unit testleri |

### Signatures

```csharp
// Warehouse.cs
namespace Envanex.Domain.Aggregates.Warehouses;

public sealed class Warehouse : AggregateRoot<Guid>
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse() : base() { /* EF Core */ }
    private Warehouse(Guid id, string code, string name) : base(id) { }

    public static Result<Warehouse> Create(string code, string name);
    public void Deactivate();
    public void Activate();
}
```

```csharp
// WarehouseErrors.cs
namespace Envanex.Domain.Aggregates.Warehouses;

public static class WarehouseErrors
{
    public static readonly Error CodeRequired;
    public static readonly Error NameRequired;
}
```

```csharp
// Product.cs
namespace Envanex.Domain.Aggregates.Products;

public sealed class Product : AggregateRoot<Guid>
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid UnitOfMeasureId { get; private set; }
    public Money ListPrice { get; private set; }
    public Quantity ReorderPoint { get; private set; }
    public bool IsActive { get; private set; }

    private Product() : base() { /* EF Core */ }
    private Product(Guid id, string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint) : base(id) { }

    public static Result<Product> Create(string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint);
    public void UpdatePrice(Money newPrice);
    public void Deactivate();
    public void Activate();
}
```

Fabrika kurallari:
- Product: code ve name ayni normalizasyon (trim + upper invariant for code, trim for name)
- `unitOfMeasureId == Guid.Empty` -> `ProductErrors.UnitOfMeasureRequired`
- `listPrice` null check -> `ArgumentNullException.ThrowIfNull` (Money record, non-null required)
- Id: `Guid.CreateVersion7()`
- IsActive: `true`
- UpdatePrice: `ArgumentNullException.ThrowIfNull(newPrice)` ve atama

```csharp
// ProductErrors.cs
namespace Envanex.Domain.Aggregates.Products;

public static class ProductErrors
{
    public static readonly Error CodeRequired;
    public static readonly Error NameRequired;
    public static readonly Error UnitOfMeasureRequired;
}
```

### Tests to add

**`tests/Envanex.Domain.Tests/Aggregates/Warehouses/WarehouseTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `Create_WithValidInputs_ShouldSucceed` | code/name set, isActive true |
| `Create_ShouldNormalizeCode_TrimAndUpperInvariant` | `" dpo1 "` -> `"DPO1"` |
| `Create_ShouldTrimName` | `" Ana Depo "` -> `"Ana Depo"` |
| `Create_WithEmptyCode_ShouldFail` | CodeRequired |
| `Create_WithEmptyName_ShouldFail` | NameRequired |
| `Deactivate_ShouldSetIsActiveFalse` | IsActive false |
| `Activate_ShouldSetIsActiveTrue` | Deactivate sonrasi Activate -> true |
| `Create_ShouldGenerateVersion7Id` | Id != Guid.Empty |

**`tests/Envanex.Domain.Tests/Aggregates/Products/ProductTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `Create_WithValidInputs_ShouldSucceed` | tum property'ler set, isActive true |
| `Create_ShouldNormalizeCode_TrimAndUpperInvariant` | `" sku-001 "` -> `"SKU-001"` |
| `Create_ShouldTrimName` | `" Widget "` -> `"Widget"` |
| `Create_WithEmptyCode_ShouldFail` | CodeRequired |
| `Create_WithEmptyName_ShouldFail` | NameRequired |
| `Create_WithEmptyGuidUnitOfMeasureId_ShouldFail` | UnitOfMeasureRequired |
| `Create_ShouldGenerateVersion7Id` | Id != Guid.Empty |
| `UpdatePrice_ShouldChangeListPrice` | yeni fiyat atanir |
| `UpdatePrice_WithNull_ShouldThrow` | ArgumentNullException |
| `Deactivate_ShouldSetIsActiveFalse` | IsActive false |
| `Activate_ShouldSetIsActiveTrue` | Deactivate sonrasi Activate -> true |

### Validation

```bash
dotnet build -warnaserror
dotnet test --filter "FullyQualifiedName~WarehouseTests|FullyQualifiedName~ProductTests"
dotnet test  # full suite — Phase 1 tests still pass
```

---

## Phase 3: EF Core configurations + DbContext DbSets + migration

### Ozet

Uc aggregate icin IEntityTypeConfiguration dosyalari olusturulur, EnvanexDbContext'e DbSet property'leri eklenir, ilk migration uretilir. TestProduct dosyalari henuz silinmez (integration testler hala onlara bagli). Bu faz schema'ya dokunur — db-reviewer gerekli.

### Files

| Path | Action | Description |
|------|--------|-------------|
| `src/Envanex.Infrastructure/Persistence/Configurations/UnitOfMeasureConfiguration.cs` | created | IEntityTypeConfiguration |
| `src/Envanex.Infrastructure/Persistence/Configurations/WarehouseConfiguration.cs` | created | IEntityTypeConfiguration |
| `src/Envanex.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` | created | IEntityTypeConfiguration |
| `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` | modified | 3 DbSet property eklenir |
| `src/Envanex.Infrastructure/Migrations/*.cs` | created | `dotnet ef migrations add` ile uretilir |
| `docs/adr/0004-aggregate-identifiers-and-natural-keys.md` | created | Sadece baslik — insan icerik yazar |

### Signatures

```csharp
// UnitOfMeasureConfiguration.cs
namespace Envanex.Infrastructure.Persistence.Configurations;

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder);
}
```

Konfigurasyondaki kurallar:
- `builder.ToTable("UnitOfMeasures")`
- `builder.HasKey(u => u.Id)`
- `builder.Property(u => u.Code).IsRequired().HasMaxLength(20)`
- `builder.HasIndex(u => u.Code).IsUnique()`
- `builder.Property(u => u.Name).IsRequired().HasMaxLength(200)`
- `builder.Property(u => u.ConversionFactor).HasPrecision(18, 6)` (conversion factor needs fine precision)
- Self-referencing FK: `builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(u => u.BaseUnitId).OnDelete(DeleteBehavior.Restrict).IsRequired(false)`
- `builder.Property(u => u.IsActive).IsRequired()`

```csharp
// WarehouseConfiguration.cs
namespace Envanex.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder);
}
```

Konfigurasyondaki kurallar:
- `builder.ToTable("Warehouses")`
- `builder.HasKey(w => w.Id)`
- `builder.Property(w => w.Code).IsRequired().HasMaxLength(20)`
- `builder.HasIndex(w => w.Code).IsUnique()`
- `builder.Property(w => w.Name).IsRequired().HasMaxLength(200)`
- `builder.Property(w => w.IsActive).IsRequired()`

```csharp
// ProductConfiguration.cs
namespace Envanex.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder);
}
```

Konfigurasyondaki kurallar:
- `builder.ToTable("Products")`
- `builder.HasKey(p => p.Id)`
- `builder.Property(p => p.Code).IsRequired().HasMaxLength(50)`
- `builder.HasIndex(p => p.Code).IsUnique()`
- `builder.Property(p => p.Name).IsRequired().HasMaxLength(200)`
- FK: `builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(p => p.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict).IsRequired()`
- `builder.ComplexProperty(p => p.ListPrice)` — ADR 0003 gereksinimi, explicit olmali
- ReorderPoint: Quantity converter convention tarafindan handle edilir, ek konfigurasyon gerekmez
- `builder.Property(p => p.IsActive).IsRequired()`

```csharp
// EnvanexDbContext.cs (modified — new lines only)
public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
public DbSet<Warehouse> Warehouses => Set<Warehouse>();
public DbSet<Product> Products => Set<Product>();
```

### Migration command

```bash
# Docker compose must be running for SQL Server
# Environment variable required by EnvanexDbContextFactory:
export ENVANEX_CONNECTION_STRING="Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=YourStr0ngP@ssword;TrustServerCertificate=True"

dotnet ef migrations add InitialSchema -p src/Envanex.Infrastructure -s src/Envanex.Web
```

Migration ismi: `InitialSchema`

### ADR stub content

```markdown
# 0004 — Aggregate identifiers and natural keys

<!-- Human writes content -->
```

### Tests to add

Bu fazda yeni test eklenmez. Mevcut tum testler gecmeye devam etmeli (TestProduct dosyalari henuz silinmedi, TestDbContext hala calisiyor).

### Validation

```bash
dotnet build -warnaserror
dotnet test   # tum mevcut testler gecer (TestProduct-bazli integration testler dahil)
dotnet format --verify-no-changes
```

**db-reviewer bu fazdan sonra calistirilmali** — migration dosyasini, konfigurasyonlari ve tablo yapisi dogrulamali.

---

## Phase 4: Integration testleri Product'a gecir + TestProduct temizligi

### Ozet

Mevcut 8 integration testi (SchemaCreationTests, SchemaPrecisionTests, MoneyPersistenceTests, QuantityPersistenceTests, ConcurrencyTests) TestProduct'tan gercek Product aggregate'ine tasinir. SqlServerFixture artik EnvanexDbContext kullanir (TestDbContext degil) ve `EnsureCreatedAsync` yerine `Database.MigrateAsync()` kullanir. TestProduct, TestDbContext ve TestProductConfiguration dosyalari silinir. Uc yeni integration test eklenir: unique index violation, self-referencing FK ve migration zinciri dogrulama.

### Files

| Path | Action | Description |
|------|--------|-------------|
| `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` | modified | TestDbContext -> EnvanexDbContext |
| `tests/Envanex.IntegrationTests/Persistence/SchemaCreationTests.cs` | modified | Products tablosuna yonlendirilir |
| `tests/Envanex.IntegrationTests/Persistence/SchemaPrecisionTests.cs` | modified | Products tablo ve kolon adlari |
| `tests/Envanex.IntegrationTests/Persistence/MoneyPersistenceTests.cs` | modified | Product.Create kullanir |
| `tests/Envanex.IntegrationTests/Persistence/QuantityPersistenceTests.cs` | modified | Product.Create kullanir |
| `tests/Envanex.IntegrationTests/Persistence/ConcurrencyTests.cs` | modified | Product.Create + UpdatePrice kullanir |
| `tests/Envanex.IntegrationTests/Persistence/UniqueIndexTests.cs` | created | Unique index violation testi |
| `tests/Envanex.IntegrationTests/Persistence/SelfReferencingFkTests.cs` | created | UnitOfMeasure self-ref FK testi |
| `tests/Envanex.IntegrationTests/Persistence/MigrationTests.cs` | created | Migration zinciri dogrulama testi |
| `tests/Envanex.IntegrationTests/TestAggregates/TestProduct.cs` | deleted | Artik gercek Product var |
| `tests/Envanex.IntegrationTests/Persistence/TestDbContext.cs` | deleted | EnvanexDbContext yeterli |
| `tests/Envanex.IntegrationTests/Persistence/TestProductConfiguration.cs` | deleted | Gercek config var |

### SqlServerFixture degisiklikleri

```csharp
// SqlServerFixture.cs — artik EnvanexDbContext kullanir
public sealed class SqlServerFixture : IAsyncLifetime
{
    public EnvanexDbContext CreateDbContext();
    // options: DbContextOptionsBuilder<EnvanexDbContext>.UseSqlServer(ConnectionString)

    public async Task ResetAsync();
    // FK dependency order:
    // 1. DELETE FROM Products
    // 2. DELETE FROM UnitOfMeasures
    // 3. DELETE FROM Warehouses

    public async Task InitializeAsync();
    // MigrateAsync with EnvanexDbContext (EnsureCreated DEGIL)
}
```

### EnsureCreated → MigrateAsync gecisi

**Gerekce:** Phase 3'te ilk gercek migration uretiliyor ama `EnsureCreated` migration pipeline'ini tamamen bypass eder — modelden dogrudan DDL uretir. Bu durumda migration dosyasindaki bir hata (yanlis kolon adi, eksik index, bozuk FK) hicbir testte yakalanmaz ve ancak production'da veya elle `dotnet ef database update` calistirildiginda ortaya cikar. `MigrateAsync` ise migration zincirini sirasiyla uygular: her test kosusu migration'in gercek SQL Server'a uygulanabildigini dogrular. Bu, ADR 0003'un Consequences bolumundeki "`EnsureCreated` cannot evolve a schema" notunu da kapatir — artik test altyapisi migration-aware'dir.

**`InitializeAsync` degisikligi:**

```csharp
// SqlServerFixture.cs — InitializeAsync
public async Task InitializeAsync()
{
    // container start...
    await using var context = CreateDbContext();
    await context.Database.MigrateAsync();  // EnsureCreatedAsync yerine
}
```

**Etki:** `EnsureCreated` model snapshot'indan DDL uretirdi, `MigrateAsync` ise `Migrations/` klasorundeki dosyalari sirasiyla uygular. Testcontainers her kosuda temiz bir veritabani baslattigi icin tum migration'lar sifirdan uygulanir — bu, migration zincirinin butunlugunu her CI kosusunda dogrular.

### Mevcut testlerin gecis detaylari

**SchemaCreationTests** — `Database_ShouldBeConnectableAndTableShouldExist`:
- `context.TestProducts` -> `context.Products`
- Tablo adi `TestProducts` -> `Products`

**SchemaPrecisionTests** — 4 test:
- `MoneyColumn_ShouldBeDecimal18_4`: `TABLE_NAME = 'TestProducts'` -> `'Products'`, `COLUMN_NAME = 'UnitPrice_Amount'` -> `'ListPrice_Amount'`
- `QuantityColumn_ShouldBeDecimal18_6`: `TABLE_NAME = 'TestProducts'` -> `'Products'`, `COLUMN_NAME = 'StockQuantity'` -> `'ReorderPoint'`
- `RowVersionColumn_ShouldBeRowVersionType`: `TABLE_NAME = 'TestProducts'` -> `'Products'`
- `CurrencyColumn_ShouldBeNVarChar3`: `TABLE_NAME = 'TestProducts'` -> `'Products'`, `COLUMN_NAME = 'UnitPrice_Currency'` -> `'ListPrice_Currency'`

**MoneyPersistenceTests** — `SaveAndLoad_ShouldPreserveAmountAndCurrency`:
- `TestProduct.Create(...)` -> `Product.Create(code, name, unitOfMeasureId, listPrice, reorderPoint).Value`
- Test icinde once bir UnitOfMeasure persist edilmeli (FK gerekliligi)
- `context.TestProducts` -> `context.Products`
- Assert: `loaded.ListPrice.Amount`, `loaded.ListPrice.Currency.Code`

**QuantityPersistenceTests** — `SaveAndLoad_ShouldPreserveRoundedValue`:
- Ayni FK gerekliligi — once UnitOfMeasure persist et
- `context.TestProducts` -> `context.Products`
- Assert: `loaded.ReorderPoint.Value`

**ConcurrencyTests** — `ConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException`:
- Once UnitOfMeasure persist et
- `TestProduct.Create(...)` -> `Product.Create(...).Value`
- `product1.UpdatePrice(...)` / `product2.UpdatePrice(...)` ayni kalir
- `context.TestProducts` -> `context.Products`

### Tests to add (new)

**`tests/Envanex.IntegrationTests/Persistence/UniqueIndexTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `InsertDuplicateProductCode_ShouldThrowDbUpdateException` | Ayni code ile ikinci Product eklemek unique violation atar |

Test detayi: once UnitOfMeasure olustur, sonra ayni code ile iki Product kaydet, ikinci SaveChangesAsync DbUpdateException firlatmali.

**`tests/Envanex.IntegrationTests/Persistence/SelfReferencingFkTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `DerivedUnit_WithValidBaseUnit_ShouldPersist` | BaseUnitId ile kayit yapilabilir ve reload'da deger korunur |
| `DerivedUnit_WithNonExistentBaseUnit_ShouldThrowDbUpdateException` | Var olmayan BaseUnitId ile kayit FK violation atar |

**`tests/Envanex.IntegrationTests/Persistence/MigrationTests.cs`**

| Test method | What it verifies |
|-------------|-----------------|
| `Migrations_ShouldApplyToCleanDatabase` | Temiz veritabanina tum migration'lar uygulanabilir ve `GetPendingMigrationsAsync()` bos doner |

Test deseni: Bu test `IAsyncLifetime` implement etmez — `ResetAsync` cagirmaz cunku veri yazmiyor, sadece migration durumunu sorgular. `SqlServerFixture.InitializeAsync` zaten `MigrateAsync` cagirdigi icin, bu test fixture'in migration'lari basariyla uyguladigini ve hicbir migration'in beklemede kalmadigini dogrular.

```csharp
[Collection(DatabaseCollection.Name)]
public sealed class MigrationTests
{
    private readonly SqlServerFixture _fixture;
    public MigrationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_ShouldApplyToCleanDatabase()
    {
        await using var context = _fixture.CreateDbContext();
        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.ShouldBeEmpty();
    }
}
```

### Validation

```bash
dotnet build -warnaserror
dotnet test   # tum testler — 8 migrated + 4 new (UniqueIndex, 2x SelfReferencingFK, MigrationTests) + tum domain testleri
dotnet format --verify-no-changes
```

**db-reviewer bu fazdan sonra da calistirilmali** — sorgularin dogru tablo/kolon adlarini kullandigini dogrulamali.

---

## Constraints (tum fazlar icin)

1. Domain projesi SIFIR NuGet bagimliligina sahip kalir — `Envanex.Domain.csproj` degismez.
2. ArchitectureTests'deki 4 kural kiyrilmaz (Domain -> nothing, Application -> Domain, Infrastructure -> Domain+Application, SoapApi -> Application).
3. `TreatWarningsAsErrors=true` — CS8618 icin EF parameterless constructor'da `default!` kullanilir, CA1062 icin `ArgumentNullException.ThrowIfNull` kullanilir.
4. Money non-nullable complex type. Product.ListPrice required.
5. Her aggregate configuration'da explicit `ComplexProperty` satiri olmali (ADR 0003 gereksinimi) — sadece ProductConfiguration'da ListPrice icin gecerli.
6. Aggregate factory'ler `Guid.CreateVersion7()` kullanir.
7. Tum setter'lar `private set`.
8. Dosyalar LF-only.

## Rollback notes

- Fazlar sirasinda geri almak gerekirse: branch'teki commit'ler atomic, her faz kendi commit'inde.
- Migration geri alma: `dotnet ef migrations remove -p src/Envanex.Infrastructure -s src/Envanex.Web` — migration dosyalarini siler.
- Phase 4 rollback'i en riskli: TestProduct silindikten sonra geri almak icin git restore kullanilmali.
- Full rollback: branch silinir, `feat/ef-core-bootstrap` zaten merge edilmis main'e donulur.
