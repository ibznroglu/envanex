# Plan: EF Core + SQL Server Infrastructure Setup

## Goal

`EnvanexDbContext`'i value-object eşlemeleriyle (Money → complex type, Quantity → value converter, Currency → value converter), model-building konvansiyonlarıyla (RowVersion shadow property, decimal precision, lazy loading kapalı), Web'de DI kaydıyla (connection string user-secrets'tan), `IDesignTimeDbContextFactory` ile migration araç zinciriyle, ve Testcontainers tabanlı entegrasyon test altyapısıyla kur. Test-only bir aggregate ile konvansiyonları gerçek SQL Server'a karşı doğrula.

## Non-goals

- Gerçek domain aggregate'leri (PR 4).
- **Migration dosyası.** Bu PR'da `dotnet ef migrations add` çalıştırılmayacak. İlk migration PR 4'te ilk gerçek aggregate ile gelecek. Gerekçe: `EnvanexDbContext`'te sıfır entity konfigürasyonu var — migration üretilse boş olur. Test tarafındaki `TestDbContext` ise üretim kodunun parçası değil, onun şeması `EnsureCreated` ile ephemeral Testcontainers instance'larında oluşturulur. Bu PR migration **araç zincirini** kurar (`IDesignTimeDbContextFactory`), migration **dosyası** üretmez.
- Outbox tablosu veya dispatching.
- Stored procedure'lar veya `db/` klasör içeriği.
- CreatedAt / UpdatedAt shadow property'leri (gelecek PR).
- WebApplicationFactory tabanlı entegrasyon testleri (bu PR doğrudan DbContext'i Testcontainers'a karşı test eder; WAF testleri endpoint geldiğinde).

## Touches schema? Yes — db-reviewer required

Test altyapısı `EnsureCreated` ile gerçek SQL Server şeması oluşturur. Migration dosyası commit edilmez ama `OnModelCreating` pipeline'ı DDL üretir.

## ADR needed? 0003 — EF Core Mapping Conventions

İçeriği insan yazar. Coder sadece boş stub dosyası oluşturur.

---

## Phase 1: EnvanexDbContext, conventions, value-object converters, design-time factory

### Özet

`EnvanexDbContext`'i tüm model-building konvansiyonları, value converter'lar ve Money complex-type konfigürasyonuyla oluştur. `IDesignTimeDbContextFactory<EnvanexDbContext>` ekleyerek `dotnet ef` araç zincirini hazır et.

### Dosyalar

| Yol | Eylem | Ne değişir |
|-----|-------|-----------|
| `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` | oluştur | `DbContext`, iki constructor (generic + non-generic protected), `OnModelCreating`'de `ApplyConfigurationsFromAssembly`, `ConfigureConventions`'da konvansiyonlar ve converter'lar |
| `src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs` | oluştur | `public sealed` `IDesignTimeDbContextFactory<EnvanexDbContext>`: connection string'i `ENVANEX_CONNECTION_STRING` ortam değişkeninden okur, yoksa açıklayıcı `InvalidOperationException` fırlatır |
| `src/Envanex.Infrastructure/Persistence/Conventions/AggregateRootConvention.cs` | oluştur | `IModelFinalizingConvention`: `AggregateRoot<>`'dan türeyen her entity type'a `RowVersion` shadow property (`byte[]`, `IsRowVersion`) ekler |
| `src/Envanex.Infrastructure/Persistence/Conventions/MoneyComplexTypeConvention.cs` | oluştur | `IModelFinalizingConvention`: CLR tipi `Money` olan her complex property'nin **iç sütunlarını** yapılandırır — `Amount` → `decimal(18,4)`, `Currency` → `CurrencyConverter` + `nvarchar(3)`. **Bu konvansiyon complex property'nin kendisini oluşturmaz** — her aggregate konfigürasyonunda `builder.ComplexProperty(...)` satırı elle yazılmak zorundadır (aşağıdaki "Açık kısıtlar" bölümüne bkz.) |
| `src/Envanex.Infrastructure/Persistence/Converters/CurrencyConverter.cs` | oluştur | `ValueConverter<Currency, string>` |
| `src/Envanex.Infrastructure/Persistence/Converters/QuantityConverter.cs` | oluştur | `ValueConverter<Quantity, decimal>` |
| `docs/adr/0003-ef-core-mapping-conventions.md` | oluştur | ADR stub — başlık + boş bölümler |

### İmzalar

```csharp
// src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs
namespace Envanex.Infrastructure.Persistence;

public class EnvanexDbContext : DbContext
{
    public EnvanexDbContext(DbContextOptions<EnvanexDbContext> options) : base(options) { }
    protected EnvanexDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) { /* ... */ }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) { /* ... */ }
}
```

**İki constructor gerekçesi:** Generic constructor DI kaydı için (`AddDbContext<EnvanexDbContext>`). Non-generic protected constructor, türetilmiş sınıfların (`TestDbContext`) kendi generic options'ını base'e geçirmesi için. EF Core, `DbContextOptions<TDerived>` → `DbContextOptions` dönüşümünü bu yolla destekler.

`ConfigureConventions` içinde:
- `configurationBuilder.Properties<Quantity>().HaveConversion<QuantityConverter>().HavePrecision(18, 6);`
- `configurationBuilder.Properties<Currency>().HaveConversion<CurrencyConverter>().HaveMaxLength(3);`
- `configurationBuilder.Conventions.Add(_ => new AggregateRootConvention());`
- `configurationBuilder.Conventions.Add(_ => new MoneyComplexTypeConvention());`

`OnModelCreating` içinde:
- `modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnvanexDbContext).Assembly);`

```csharp
// src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs
namespace Envanex.Infrastructure.Persistence;

public sealed class EnvanexDbContextFactory : IDesignTimeDbContextFactory<EnvanexDbContext>
{
    public EnvanexDbContext CreateDbContext(string[] args) { /* ... */ }
}
```

`CreateDbContext` body: `ArgumentNullException.ThrowIfNull(args)` ile CA1062 karşılanır. Ardından `Environment.GetEnvironmentVariable("ENVANEX_CONNECTION_STRING")` okur. Null veya boş ise:

```csharp
throw new InvalidOperationException(
    "ENVANEX_CONNECTION_STRING environment variable is not set. " +
    "Example: set ENVANEX_CONNECTION_STRING=Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True");
```

**Migration üretirken kullanım (PR 4'te, bu PR'da migration üretilmeyecek):**

```bash
# Bash
export ENVANEX_CONNECTION_STRING="Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=Erp_Local_Dev_2026!;TrustServerCertificate=True"
dotnet ef migrations add InitialCreate -p src/Envanex.Infrastructure -s src/Envanex.Web

# PowerShell
$env:ENVANEX_CONNECTION_STRING="Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=Erp_Local_Dev_2026!;TrustServerCertificate=True"
dotnet ef migrations add InitialCreate -p src/Envanex.Infrastructure -s src/Envanex.Web
```

```csharp
// src/Envanex.Infrastructure/Persistence/Conventions/AggregateRootConvention.cs
namespace Envanex.Infrastructure.Persistence.Conventions;

internal sealed class AggregateRootConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context) { /* ... */ }
}
```

```csharp
// src/Envanex.Infrastructure/Persistence/Conventions/MoneyComplexTypeConvention.cs
namespace Envanex.Infrastructure.Persistence.Conventions;

internal sealed class MoneyComplexTypeConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context) { /* ... */ }
}
```

```csharp
// src/Envanex.Infrastructure/Persistence/Converters/CurrencyConverter.cs
namespace Envanex.Infrastructure.Persistence.Converters;

internal sealed class CurrencyConverter : ValueConverter<Currency, string>
{
    public CurrencyConverter() : base(
        currency => currency.Code,
        code => Currency.Of(code).Value) { }
}
```

```csharp
// src/Envanex.Infrastructure/Persistence/Converters/QuantityConverter.cs
namespace Envanex.Infrastructure.Persistence.Converters;

internal sealed class QuantityConverter : ValueConverter<Quantity, decimal>
{
    public QuantityConverter() : base(
        quantity => quantity.Value,
        value => Quantity.Of(value).Value) { }
}
```

**DomainEvents hakkında:** `AggregateRoot<TId>.DomainEvents` (`IReadOnlyCollection<IDomainEvent>`) EF Core tarafından otomatik olarak yoksayılır çünkü `IDomainEvent` bir entity type değil. Build sırasında uyarı çıkarsa convention'da explicit ignore eklenecek.

**ADR 0003 stub içeriği:**

```markdown
# 0003 — EF Core mapping conventions

## Context

## Decision

## Alternatives

## Consequences
```

### Testler

Yok — DbContext'te entity yok, veritabanına karşı test edilecek bir şey yok.

### Doğrulama

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Phase 2: DI registration in Web and user-secrets setup

### Özet

`EnvanexDbContext`'i Web host'un DI konteynerine kaydet. `Envanex.Web.csproj`'ye `UserSecretsId` ekle. `appsettings.json`'a boş connection string key'i ekle (parola YAZILMAYACAK). Infrastructure'da `DependencyInjection` extension method'u oluştur.

### Dosyalar

| Yol | Eylem | Ne değişir |
|-----|-------|-----------|
| `src/Envanex.Infrastructure/DependencyInjection.cs` | oluştur | `AddInfrastructure(this IServiceCollection, IConfiguration)` extension method'u |
| `src/Envanex.Web/Program.cs` | değiştir | `builder.Services.AddInfrastructure(builder.Configuration);` ekle |
| `src/Envanex.Web/Envanex.Web.csproj` | değiştir | `<UserSecretsId>` ekle |
| `src/Envanex.Web/appsettings.json` | değiştir | `"ConnectionStrings": { "EnvanexDb": "" }` ekle |

### İmzalar

```csharp
// src/Envanex.Infrastructure/DependencyInjection.cs
namespace Envanex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) { /* ... */ }
}
```

Method body: Önce `ArgumentNullException.ThrowIfNull(services)` ve `ArgumentNullException.ThrowIfNull(configuration)` ile CA1062 karşılanır. Ardından `configuration.GetConnectionString("EnvanexDb")` okur, `services.AddDbContext<EnvanexDbContext>(options => options.UseSqlServer(connectionString))` çağırır.

### Testler

Yok — DI kaydı veritabanı olmadan anlamlı test edilemez.

### Doğrulama

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

Kullanıcı ayrıca şunu çalıştırmalı (coder yapmaz):
```bash
dotnet user-secrets set "ConnectionStrings:EnvanexDb" \
  "Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=Erp_Local_Dev_2026!;TrustServerCertificate=True" \
  --project src/Envanex.Web
```

---

## Phase 3: Test aggregate and TestDbContext in IntegrationTests

### Özet

IntegrationTests projesinde test-only `TestProduct` aggregate'i oluştur. `EnvanexDbContext`'ten türeyen `TestDbContext`'i `DbContextOptions<TestDbContext>` alacak şekilde oluştur, `TestProduct` entity konfigürasyonunu ekle. Test aggregate: `Guid` Id, `string Name`, `Money UnitPrice`, `Quantity StockQuantity` — üç value-object eşlemesini ve RowVersion konvansiyonunu doğrular.

### Dosyalar

| Yol | Eylem | Ne değişir |
|-----|-------|-----------|
| `tests/Envanex.IntegrationTests/TestAggregates/TestProduct.cs` | oluştur | Money, Quantity ve string property'li aggregate root |
| `tests/Envanex.IntegrationTests/Persistence/TestDbContext.cs` | oluştur | `EnvanexDbContext`'ten türer, `DbContextOptions<TestDbContext>` alır, `DbSet<TestProduct>` ekler |
| `tests/Envanex.IntegrationTests/Persistence/TestProductConfiguration.cs` | oluştur | `IEntityTypeConfiguration<TestProduct>` — `builder.ComplexProperty(p => p.UnitPrice)` **elle yazılır** |

### İmzalar

```csharp
// tests/Envanex.IntegrationTests/TestAggregates/TestProduct.cs
namespace Envanex.IntegrationTests.TestAggregates;

internal sealed class TestProduct : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public Money UnitPrice { get; private set; }
    public Quantity StockQuantity { get; private set; }

    private TestProduct()
    {
        Name = default!;
        UnitPrice = default!;
    } // EF Core materialization — Quantity struct olduğu için default(Quantity) geçerli, initialize gerekmez

    public static TestProduct Create(string name, Money unitPrice, Quantity stockQuantity) { /* ... */ }
    public void UpdatePrice(Money newPrice) { /* ... */ }
}
```

**CS8618 gerekçesi:** `Name` (`string`) ve `UnitPrice` (`Money`, sealed record — referans tipi) non-nullable olduğu için parametresiz constructor'da initialize edilmezse `CS8618` uyarısı → `TreatWarningsAsErrors=true` ile build hatası. `default!` ataması bu uyarıyı bastırır. `Entity<TId>` aynı deseni kullanır (`Id = default!`, Entity.cs:18). `StockQuantity` (`Quantity`, readonly record struct — değer tipi) struct olduğu için `default(Quantity)` otomatik olarak `Value = 0m` üretir, bu geçerli bir `Quantity`'dir — initialize gerekmez.

```csharp
// tests/Envanex.IntegrationTests/Persistence/TestDbContext.cs
namespace Envanex.IntegrationTests.Persistence;

internal sealed class TestDbContext : EnvanexDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    public DbSet<TestProduct> TestProducts => Set<TestProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // tüm konvansiyonlar uygulanır
        modelBuilder.ApplyConfiguration(new TestProductConfiguration());
    }
}
```

**Constructor zinciri:** `TestDbContext(DbContextOptions<TestDbContext>)` → `EnvanexDbContext(DbContextOptions)` (protected non-generic constructor). EF Core, `DbContextOptions<TestDbContext>`'i `DbContextOptions`'a implicit cast eder. Generic `DbContextOptions<EnvanexDbContext>` geçilseydi runtime'da `InvalidOperationException` fırlatırdı.

```csharp
// tests/Envanex.IntegrationTests/Persistence/TestProductConfiguration.cs
namespace Envanex.IntegrationTests.Persistence;

internal sealed class TestProductConfiguration : IEntityTypeConfiguration<TestProduct>
{
    public void Configure(EntityTypeBuilder<TestProduct> builder)
    {
        builder.ToTable("TestProducts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.ComplexProperty(p => p.UnitPrice);
        // Money'nin iç sütunları (Amount precision, Currency converter) MoneyComplexTypeConvention tarafından otomatik yapılandırılır.
        // Quantity converter ConfigureConventions'dan otomatik uygulanır.
        // RowVersion shadow property AggregateRootConvention tarafından otomatik eklenir.
    }
}
```

**`builder.ComplexProperty(p => p.UnitPrice)` neden elle yazılmak zorunda:** EF Core complex type'ları otomatik keşfetmez — bir property'nin complex type olduğunu bilmesi için konfigürasyonda explicit `ComplexProperty` çağrısı gerekir. `MoneyComplexTypeConvention` yalnızca **zaten complex property olarak kaydedilmiş** bir `Money` property'sinin iç sütunlarını yapılandırır (Amount → `decimal(18,4)`, Currency → `CurrencyConverter` + `nvarchar(3)`). Konvansiyon, complex property'nin kendisini oluşturmaz. Bu iki katmanlı yapı:

1. **Aggregate konfigürasyonu** (elle): `builder.ComplexProperty(p => p.UnitPrice)` — "bu property bir complex type'tır" der
2. **MoneyComplexTypeConvention** (otomatik): "complex type olarak kaydedilmiş her Money'nin iç sütunlarını standart şekilde yapılandır" der

Bu ayrım ADR 0003'te belirtilecek bir kısıttır.

### Testler

Yok — bu faz Phase 4 için altyapı kurar.

### Doğrulama

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Phase 4: Testcontainers fixture and all integration tests

### Özet

`SqlServerFixture` oluştur: `MsSqlContainer` başlatır, `EnsureCreated` ile şema oluşturur, test-scoped `TestDbContext` instance'ları sağlar, testler arası izolasyon için `ResetAsync` metodu sunar. Altı entegrasyon testi yaz.

### Dosyalar

| Yol | Eylem | Ne değişir |
|-----|-------|-----------|
| `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` | oluştur | `IAsyncLifetime`: `MsSqlContainer` başlatır, `EnsureCreated` çağırır, `CreateDbContext` ve `ResetAsync` sağlar |
| `tests/Envanex.IntegrationTests/Fixtures/DatabaseCollection.cs` | oluştur | `[CollectionDefinition]` xUnit collection fixture paylaşımı için |
| `tests/Envanex.IntegrationTests/Persistence/SchemaCreationTests.cs` | oluştur | 1 test |
| `tests/Envanex.IntegrationTests/Persistence/MoneyPersistenceTests.cs` | oluştur | 1 test |
| `tests/Envanex.IntegrationTests/Persistence/QuantityPersistenceTests.cs` | oluştur | 1 test |
| `tests/Envanex.IntegrationTests/Persistence/SchemaPrecisionTests.cs` | oluştur | 2 test |
| `tests/Envanex.IntegrationTests/Persistence/ConcurrencyTests.cs` | oluştur | 1 test |

### İmzalar

```csharp
// tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs
namespace Envanex.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer _container = null!;

    public string ConnectionString => _container.GetConnectionString();
    public TestDbContext CreateDbContext() { /* ... */ }
    public Task ResetAsync() { /* ... */ }
    public Task InitializeAsync() { /* ... */ }
    public Task DisposeAsync() { /* ... */ }
}
```

**`ResetAsync` mekanizması:** `TestProducts` tablosundaki tüm satırları siler (`DELETE FROM TestProducts`). Collection fixture konteyner yaşam döngüsünü yönetir (`InitializeAsync`/`DisposeAsync`), `ResetAsync` ise test sınıflarının `IAsyncLifetime.InitializeAsync`'inde çağrılarak testler arası izolasyon sağlar.

```csharp
// tests/Envanex.IntegrationTests/Fixtures/DatabaseCollection.cs
namespace Envanex.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
```

**Test sınıfı izolasyon deseni** (tüm test sınıfları bu deseni uygular):

```csharp
[Collection(DatabaseCollection.Name)]
public sealed class XxxTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    public XxxTests(SqlServerFixture fixture) => _fixture = fixture;
    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
```

### Testler

| Sınıf | Test metodu | Ne doğrular |
|-------|------------|-------------|
| `SchemaCreationTests` | `EnsureCreated_OnCleanDatabase_ShouldCreateSchema` | Yeni bir `TestDbContext`'te `EnsureCreated` çağırır, `CanConnect()` true döner, `TestProducts` tablosu var |
| `MoneyPersistenceTests` | `SaveAndLoad_ShouldPreserveAmountAndCurrency` | `Money.Of(149.9950m, Currency.TRY)` ile `TestProduct` oluşturur, kaydeder, yeni DbContext scope'unda yükler, `Amount` ve `Currency.Code` aynı |
| `QuantityPersistenceTests` | `SaveAndLoad_ShouldPreserveRoundedValue` | `Quantity.Of(3.141593m)` (6 ondalık) ile oluşturur, kaydeder, yükler, `Value` tam `3.141593m` |
| `SchemaPrecisionTests` | `MoneyColumn_ShouldBeDecimal18_4` | `INFORMATION_SCHEMA.COLUMNS`'da Money Amount sütunu: `NUMERIC_PRECISION = 18`, `NUMERIC_SCALE = 4` |
| `SchemaPrecisionTests` | `QuantityColumn_ShouldBeDecimal18_6` | `INFORMATION_SCHEMA.COLUMNS`'da Quantity sütunu: `NUMERIC_PRECISION = 18`, `NUMERIC_SCALE = 6` |
| `ConcurrencyTests` | `ConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException` | Aynı `TestProduct`'ı iki ayrı DbContext'te yükler, birinde günceller ve kaydeder, diğerinde günceller ve kaydeder → `DbUpdateConcurrencyException` |

### CI notu

GitHub Actions `ubuntu-latest` runner'larında Docker önceden kurulu. Testcontainers out of the box çalışır. CI workflow değişikliği gerekmiyor.

### Doğrulama

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

6 entegrasyon testi geçmeli. Docker çalışıyor olmalı.

---

## Faz özeti

| Faz | Oluşturulan | Değiştirilen | Test | db-reviewer? |
|-----|------------|-------------|------|-------------|
| 1 | 7 dosya (DbContext, factory, 2 convention, 2 converter, ADR stub) | 0 | 0 | Hayır |
| 2 | 1 dosya (DependencyInjection.cs) | 3 dosya (Program.cs, Web.csproj, appsettings.json) | 0 | Hayır |
| 3 | 3 dosya (TestProduct, TestDbContext, TestProductConfiguration) | 0 | 0 | Hayır |
| 4 | 7 dosya (fixture, collection, 5 test sınıfı) | 0 | 6 | Evet |

## Açık kısıtlar

1. **Money non-nullable.** Complex type'lar EF Core'da nullable olamaz. Opsiyonel para alanı gerekirse `Money.Zero(currency)` kullanılacak. ADR'de belirtilecek.
2. **Money complex property elle kaydedilir.** EF Core complex type'ları otomatik keşfetmez. Her aggregate konfigürasyonunda ilgili Money alanı için `builder.ComplexProperty(...)` satırı elle yazılmak zorundadır. `MoneyComplexTypeConvention` yalnızca zaten complex property olarak kaydedilmiş bir Money'nin iç sütunlarını (Amount precision, Currency converter) yapılandırır — complex property'nin kendisini oluşturmaz. ADR'de belirtilecek.
3. **Domain'de EF paketi yok.** Value converter'lar ve complex type konfigürasyonları yalnızca Infrastructure'da yaşar.
4. **Lazy loading yok.** `Microsoft.EntityFrameworkCore.Proxies` paketi asla referans edilmeyecek.
5. **DomainEvents eşlenmez.** `_domainEvents` alanı ve `DomainEvents` property'si EF Core'a görünmez olmalı.
6. **PR 3'te migration dosyası yok.** İlk migration PR 4'te ilk gerçek aggregate ile gelecek. Bu PR `IDesignTimeDbContextFactory`'yi kurarak araç zincirini hazır eder. Factory, connection string'i `ENVANEX_CONNECTION_STRING` ortam değişkeninden okur — değişken yoksa açıklayıcı hata fırlatır, varsayılan string'e düşmez.

## Analyzer uyarı stratejisi

- `TreatWarningsAsErrors=true` aktif. Tüm yeni sınıflar `internal sealed` (kullanılmayan public API uyarısı olmaz).
- **Üç istisna public olmalıdır:**
  - `DependencyInjection.cs` — `public static` (extension method gereksinimi).
  - `EnvanexDbContextFactory` — `public sealed` (`dotnet ef` design-time araçları factory'yi reflection ile arar ve public olmayan tipi bulamaz).
  - `EnvanexDbContext` — `public class` (non-sealed). Public çünkü `DependencyInjection.AddInfrastructure` içinde `AddDbContext<EnvanexDbContext>()` çağrısı ve design-time factory cross-assembly erişim gerektiriyor. Non-sealed çünkü `TestDbContext` (IntegrationTests'te) ondan türüyor.
- CA1062: Tüm public method'larda referans tipi parametreler için `ArgumentNullException.ThrowIfNull` çağrılır — `AddInfrastructure(services, configuration)` ve `CreateDbContext(args)`.
- CS8618: EF Core materialization için kullanılan parametresiz constructor'larda non-nullable referans tipi property'ler `default!` ile initialize edilir (`Entity<TId>` deseni).
- Value converter'lardaki lambda expression'lar `Result<T>.Value` erişir — failed result durumu veritabanından gelen veriyle oluşmaz (yazma sırasında valide edilmiş), ama `InvalidOperationException` fırlatacağı için sessiz hata olmaz.

## Rollback notları

- Migration dosyası commit edilmez. Tek şema oluşturma ephemeral Testcontainers instance'larında `EnsureCreated` ile gerçekleşir. Rollback = branch commit'lerini revert.
- `UserSecretsId` user-secrets set edilmediyse no-op.
- ADR stub tek satırlık dosya — PR terk edilirse revert.
