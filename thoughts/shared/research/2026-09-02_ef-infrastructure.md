# Research: EF Core + SQL Server Infrastructure Setup

## Sorulan Soru

Bu araştırma, PR 3'te uygulanacak EF Core + SQL Server altyapısı için mevcut domain tiplerinin
yapısı, existing projeler, NuGet bağımlılıklar, ve Envanex'in EF mapping stratejisi konusunda
detaylı bir haritalama yapmalıdır.

## İlgili Dosyalar

**Domain Katmanı (`src/Envanex.Domain/`):**
- `Common/Entity.cs` — `Entity<TId>` base class, `TId : notnull` constraint
- `Common/AggregateRoot.cs` — `AggregateRoot<TId> : Entity<TId>`, IDomainEvent toplama
- `Common/Result.cs` — `Result` ve `Result<T>` tipi, protected constructor
- `Common/Error.cs` — `sealed record Error(string Code, string Message)`
- `Common/IDomainEvent.cs` — marker interface, `OccurredOnUtc` property
- `ValueObjects/Money.cs` — `sealed record Money`, `decimal Amount`, `Currency Currency`
- `ValueObjects/Quantity.cs` — `readonly record struct Quantity`, `decimal Value`
- `ValueObjects/Currency.cs` — `sealed record Currency`, `string Code`

**Infrastructure Projesi (`src/Envanex.Infrastructure/`):**
- `Envanex.Infrastructure.csproj` — ProjectReference: Domain, Application; NuGet: EF Core Design 10.0.11, EF Core SqlServer 10.0.11

**Testler:**
- `tests/Envanex.Domain.Tests/Envanex.Domain.Tests.csproj` — Shouldly, xunit, Testcontainers.MsSql
- `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj` — Microsoft.AspNetCore.Mvc.Testing, Testcontainers.MsSql
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — katmanlar arası project reference kontrolü

**Web ve Application:**
- `src/Envanex.Web/Program.cs` — Blazor + OpenAPI + Scalar yapısı, DbContext registrasyonu yok
- `src/Envanex.Application/Envanex.Application.csproj` — FluentValidation reference
- `src/Envanex.Web/appsettings.json`, `appsettings.Development.json` — bağlantı string yok
- `src/Envanex.Worker/Envanex.Worker.csproj` — UserSecretsId: `dotnet-Envanex.Worker-34dce3f2-b757-4a1c-b867-30391c8fb3f6`

**Konfigürasyon:**
- `docker-compose.yml` — SQL Server 2022, localhost:1433, MSSQL_SA_PASSWORD ortam değişkeni
- `Directory.Packages.props` — NuGet versiyonları merkezi yönetim
- `global.json` — .NET 10.0.400

---

## Mevcut Davranış

### Envanex.Domain Yapısı

**Entity\<TId\> (`src/Envanex.Domain/Common/Entity.cs` satır 1-66):**
- Abstract class, `TId : notnull` generic constraint
- Constructor: `protected Entity(TId id)` (satır 8-11)
- Constructor: `protected Entity()` (satır 15-19) — EF Core materialization için, parametresiz
- Property: `public TId Id { get; private set; }` (satır 6) — private setter
- `IEquatable<Entity<TId>>` implementasyonu (satır 21-44), `GetType()` kontrolü ile runtime type comparison (satır 33-35)
- Operator overloading: `==` ve `!=` (satır 51-64)

**AggregateRoot\<TId\> (`src/Envanex.Domain/Common/AggregateRoot.cs` satır 1-31):**
- `public abstract class AggregateRoot<TId> : Entity<TId>` (satır 3)
- Constructor: `protected AggregateRoot(TId id) : base(id)` (satır 8-10)
- Constructor: `protected AggregateRoot()` (satır 14-17) — EF Core materialization için, parametresiz
- Field: `private readonly List<IDomainEvent> _domainEvents = []` (satır 6)
- Property: `public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly()` (satır 19)
- Method: `protected void RaiseDomainEvent(IDomainEvent domainEvent)` (satır 21-25)
- Method: `public void ClearDomainEvents()` (satır 27-30)

**Result ve Result\<T\> (`src/Envanex.Domain/Common/Result.cs` satır 1-56):**
- `public class Result` (satır 3) — Properties: `bool IsSuccess`, `bool IsFailure`, `Error Error`
- Constructor: `protected Result(bool isSuccess, Error error)` (satır 9-23) — protected, validasyon var
- Static factory: `Result.Success()` (satır 25), `Result.Failure(Error)` (satır 27-31)
- Generic static factory: `Result.Success<T>(T value)` (satır 33), `Result.Failure<T>(Error)` (satır 35-39)
- `public class Result<T> : Result` (satır 42) — generic variant
- Constructor: `internal Result(T value, bool isSuccess, Error error)` (satır 50-54) — internal

**Error (`src/Envanex.Domain/Common/Error.cs` satır 1-6):**
- `public sealed record Error(string Code, string Message)` (satır 3)
- Static readonly: `Error.None = new(string.Empty, string.Empty)` (satır 5)

**IDomainEvent (`src/Envanex.Domain/Common/IDomainEvent.cs` satır 1-6):**
- Marker interface: `public interface IDomainEvent` (satır 3)
- Property: `DateTime OccurredOnUtc { get; }` (satır 5)

### Value Objects Yapısı

**Money (`src/Envanex.Domain/ValueObjects/Money.cs` satır 1-62):**
- `public sealed record Money` (satır 5)
- Private constant: `const int DecimalPlaces = 4` (satır 7) — SQL Server `decimal(18,4)`
- Property: `public decimal Amount { get; }` (satır 9)
- Property: `public Currency Currency { get; }` (satır 10)
- Constructor: `private Money(decimal amount, Currency currency)` (satır 12-16) — private
- Factory: `static Result<Money> Of(decimal amount, Currency currency)` (satır 18-23)
- Method: `Result<Money> Add(Money other)` (satır 25-36) — currency mismatch kontrolü
- Method: `Result<Money> Subtract(Money other)` (satır 38-49)
- Method: `Money Multiply(decimal factor)` (satır 51-55) — düz Money döner
- Static: `Money Zero(Currency currency)` (satır 57-61)
- Error class: `MoneyErrors.CurrencyMismatch` (satır 66)

**Quantity (`src/Envanex.Domain/ValueObjects/Quantity.cs` satır 1-60):**
- `public readonly record struct Quantity` (satır 5) — stack-allocated value type
- Private constant: `const int DecimalPlaces = 6` (satır 7) — SQL Server `decimal(18,6)`
- Property: `public decimal Value { get; }` (satır 9)
- Constructor: `private Quantity(decimal value)` (satır 11-14) — private
- Factory: `static Result<Quantity> Of(decimal value)` (satır 16-25) — negative check
- Method: `Quantity Add(Quantity other)` (satır 27-31) — düz Quantity döner
- Method: `Result<Quantity> Subtract(Quantity other)` (satır 33-44)
- Method: `Result<Quantity> Multiply(decimal factor)` (satır 46-57)
- Static: `readonly Quantity Zero = new(0m)` (satır 59)
- Error class: `QuantityErrors.Negative`, `QuantityErrors.NegativeResult` (satır 62-66)

**Currency (`src/Envanex.Domain/ValueObjects/Currency.cs` satır 1-42):**
- `public sealed record Currency` (satır 5)
- Private field: `static readonly HashSet<string> SupportedCodes = ["TRY", "USD", "EUR"]` (satır 7)
- Property: `public string Code { get; }` (satır 9)
- Constructor: `private Currency(string code)` (satır 11-14) — private
- Factory: `static Result<Currency> Of(string code)` (satır 16-31) — validation, normalization
- Static singletons: `Currency.TRY`, `Currency.USD`, `Currency.EUR` (satır 33-35)
- Error class: `CurrencyErrors.InvalidCode` (satır 40)

### Infrastructure Projesi Mevcut Durumu

**`src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` (satır 1-13):**
- ProjectReference: `Envanex.Domain`, `Envanex.Application` (satır 2-5)
- PackageReference: `Microsoft.EntityFrameworkCore.Design` 10.0.11, IncludeAssets = "runtime; build; native; contentfiles; analyzers; buildtransitive" (satır 7-10)
- PackageReference: `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11 (satır 11)
- Hiçbir source file yok

### Web Program.cs Mevcut Durumu

**`src/Envanex.Web/Program.cs` (satır 1-44):**
- Blazor UI: `.AddRazorComponents().AddInteractiveServerComponents()` (satır 7-8)
- OpenAPI (dev only): `.AddOpenApi()`, `.MapOpenApi()`, `.MapScalarApiReference()` (satır 12, 27-28)
- Exception handler, HTTPS redirection, antiforgery (satır 20-34)
- DbContext registrasyonu yok
- Connection string konfigürasyonu yok
- `public partial class Program { }` (satır 43) — WebApplicationFactory için

### NuGet Bağımlılıklar (Directory.Packages.props)

**EF Core (satır 16-17):**
- `Microsoft.EntityFrameworkCore.Design` Version 10.0.11
- `Microsoft.EntityFrameworkCore.SqlServer` Version 10.0.11

**Test Infrastructure (satır 26-33):**
- `Testcontainers.MsSql` Version 4.14.0
- `Microsoft.AspNetCore.Mvc.Testing` Version 10.0.11
- `xunit` Version 2.9.3
- `Shouldly` Version 4.3.0
- `Microsoft.NET.Test.Sdk` Version 17.14.1
- `coverlet.collector` Version 6.0.4

**Application (satır 13):**
- `FluentValidation` Version 12.1.1

### Docker Compose ve Bağlantı Ayarları

**`docker-compose.yml` (satır 1-22):**
- Image: `mcr.microsoft.com/mssql/server:2022-latest` (satır 3)
- Container: `envanex-sqlserver` (satır 4)
- Env vars: `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-Erp_Local_Dev_2026!}"`, `MSSQL_PID=Developer` (satır 5-8)
- Port: `1433:1433` (satır 10)
- Volume: `envanex-mssql-data:/var/opt/mssql` (satır 11-12)
- Healthcheck: sqlcmd ile `SELECT 1` (satır 13-18), interval 10s, timeout 5s, retries 10, start_period 30s

**appsettings.json ve appsettings.Development.json:**
- Şu an sadece logging config, connection string yok
- User secrets ayrı yönetiliyor (Envanex.Worker için UserSecretsId tanımlı)

### Test Projeleri

**Envanex.Domain.Tests:**
- `ArchitectureTests.cs` — katmanlar arası project reference kontrolü
  - Domain projesinde reference yok (satır 12)
  - Application sadece Domain'e (satır 20-24)
  - Infrastructure, Domain ve Application'a (satır 28-32)
  - SoapApi, Application'a (satır 36-40)

**Envanex.IntegrationTests:**
- ProjectReference: Envanex.Web
- Hiçbir source file yok
- NuGet: Testcontainers.MsSql, Microsoft.AspNetCore.Mvc.Testing, xunit, Shouldly

**Envanex.Application.Tests:**
- Proje var, source file yok

---

## Domain Tipi Kısıtları (EF mapping için kritik)

| Tip | Tür | Parametresiz ctor | Private ctor | EF materialization |
|---|---|---|---|---|
| Entity\<TId\> | abstract class | ✅ protected | — | EF kullanabilir |
| AggregateRoot\<TId\> | abstract class | ✅ protected | — | EF kullanabilir |
| Money | sealed record | ❌ yok | private(decimal, Currency) | Value converter veya owned/complex type gerek |
| Quantity | readonly record struct | ❌ yok | private(decimal) | Value converter gerek |
| Currency | sealed record | ❌ yok | private(string) | Value converter gerek |

**Ek kısıtlar:**
- Sealed tipler → EF proxy oluşturamaz (zaten CLAUDE.md "no lazy loading" der)
- Immutable record properties → EF init-only property desteği gerekli (EF Core 5+, mevcut)
- EF, reflection ile private constructor çağırabilir

---

## EF Core Mapping Seçenekleri

### Money Value Object

**Seçenek 1: Complex Type (EF 8+)**
- Money bir complex type olarak eşlenir, tablo oluşturmaz, sütunlar parent entity'nin tablosuna düzleştirilir
- Avantaj: Owned entity'den daha hafif, navigation semantiği yok, gerçek value object davranışı
- Dezavantaj: EF 10'da complex type non-nullable zorunlu, `Money?` alanı olan aggregate yazılamaz

**Seçenek 2: Owned Entity**
- Avantaj: EF Core native, navigation properties çalışır
- Dezavantaj: Money referans tipi olduğu için null olabilir, entity olarak izlenir — value object için yanlış model

**Seçenek 3: Value Converter**
- Avantaj: Minimal, Amount saklanır
- Dezavantaj: Currency kaybolur, eksik mapping

**Seçenek 4: JSON column**
- Avantaj: Tam Money object tutulur
- Dezavantaj: Sorgulamada NVARCHAR yaklaşımı gerek, performans overhead

### Quantity Value Object

**Seçenek 1: Value Converter**
- Struct mapping için ideal, tek decimal property
- Private constructor EF reflection ile override edilmeli

**Seçenek 2: Complex Type (EF 8+)**
- EF 8+ native support
- Tek property için overkill olabilir

### Currency Value Object

**Seçenek 1: Value Converter**
- `Code` ↔ `nvarchar(3)` dönüşümü
- `Currency.Of` factory ile reconstruction

**Seçenek 2: Owned Entity**
- Ayrı satır veya flattened column
- Currency gibi basit bir value object için ağır

---

## Riskler ve Bilinmeyenler

### Risk 1: Money ve Currency EF Mapping Stratejisi
Money bir `sealed record` ve Currency de içerideki bir referans tipi. Complex type seçilirse
EF 10'da non-nullable kısıtı devreye girer — `Money?` alanı olan aggregate yazılamaz.

### Risk 2: Quantity Private Constructor ve EF Materialization
Quantity `readonly record struct` ile private constructor. EF'nin parametresiz constructor'u
çağırması gerekiyorsa struct default constructor devreye girer (`Value = 0m`). Value converter
zorunlu.

### Risk 3: RowVersion Shadow Property Konfigurasyonu
CLAUDE.md: "every aggregate root has a `RowVersion` concurrency token (EF shadow property
configured in Infrastructure)". Henüz config yok. Her `AggregateRoot<TId>` tablosunda
`rowversion` column oluşturulmalı.

### Risk 4: DomainEvents Persistence
`AggregateRoot.DomainEvents` read-only collection. Outbox pattern ile ayrı tabloda tutulacak
ama henüz implementasyon yok — bu PR'ın kapsamı dışında.

### Risk 5: FK Explicit Configuration Kuralı
CLAUDE.md: "every FK is explicitly configured". Tüm navigation properties Fluent Config'te
explicit `.HasForeignKey()` ile yapılandırılmalı.

### Risk 6: Testcontainers Docker Gereksinimi
Testcontainers Docker daemon gerektirir. CI'da (ubuntu-latest) Docker varsayılan olarak mevcut
ama doğrulanmalı.

---

## Açık Sorular

1. **Owned Entity vs Complex Type vs Value Converter:** Money ve Currency için hangi yaklaşım?
2. **DomainEvents Persistence:** Ayrı outbox table mı, bu PR'da mı yoksa sonra mı?
3. **Connection String:** user-secrets mi, appsettings.Development.json mı?
4. **Migration Strategy:** Boş migration mı, test aggregate ile gerçek tablo mu?
5. **Testcontainers Setup:** WebApplicationFactory ile mi, doğrudan DbContext ile mi?
6. **RowVersion dışında shadow property:** CreatedAt, UpdatedAt gerekli mi?
7. **Stored Procedures:** `db/` folder yapısı bu PR'da kurulacak mı?
8. **CI yml değişikliği:** Testcontainers için Docker gereksinimi CI'da karşılanıyor mu?
