# Plan: PR 5a — Application Layer, Repositories and DI

## Goal

Product ve UnitOfMeasure için Application katmanını kurmak: command/query abstraction'ları, DTO'lar,
FluentValidation validator'ları, elle yazılmış handler'lar, repository arayüzleri, Infrastructure
repository implementasyonları, UnitOfWork, read projeksiyonları ve açık DI wiring. Roadmap'ten üç
known gap'i kapatır: domain uzunluk validasyonu, unique-index iş hatası ve boş Application.Tests.

## Non-goals

- Warehouse use case'leri (PR 8'e ertelendi, stok defteri gerektirdiğinde)
- REST controller'lar ve endpoint'ler (PR 5b)
- MediatR veya tarama/otomatik kayıt kütüphanesi (Scrutor dahil)
- Blazor bileşenleri
- Authentication/authorization (PR 6)
- Yeni EF migration'ları (şema değişmiyor)
- ListProductsQuery (datasource tek liste yüzeyi, PR 5b'de)

## Touches schema?

No. Migration yok, model değişikliği yok. `AggregateRootConvention`'a sabit çıkarılıyor ama üretilen
SQL özdeş.

## ADR needed?

`docs/adr/0005-application-layer-patterns.md` — Application Layer Patterns: Command/Query Handlers,
Validation Decorator, Repository Contracts. Coder yalnızca başlık satırını içeren boş dosyayı
oluşturur; içeriği insan yazar.

ADR'de gerekçelendirilmesi gereken noktalar:

- Elle yazılmış `ICommandHandler`/`IQueryHandler`, MediatR yerine; decorator zinciri açık DI kaydıyla
- Validation decorator yalnızca command'ları sarar, query'leri sarmaz
- Okuma tarafı bilerek ince: `IQueryable` yalnızca üzerine kompozisyon yapılan yerde döner
  (Product datasource), liste sorguları için geçiş handler katmanı yok
- Repository arayüzleri Application'da, implementasyonlar Infrastructure'da; `IUnitOfWork` transaction
  sınırını use case'e verir
- FK ön doğrulaması `GetActiveStatusAsync` ile; "pasif bir birime yeni referans verilemez", mevcut
  referanslar geçerli kalır
- **EF exception'larının Application tiplerine çevrilmesi Infrastructure'ın sorumluluğudur.**
  `DbUpdateException` ve `DbUpdateConcurrencyException` Application katmanında görünmez; `UnitOfWork`
  bunları `DuplicateKeyException` ve `ConcurrencyConflictException`'a çevirir. Bu, Application'ın EF'e
  hiç referans vermemesini mümkün kılar ve `Application_ShouldNotReference_EntityFrameworkPackages`
  testiyle mekanik olarak korunur.

---

## Phase 1 — Domain constants, length validation in factories, Product.Update, new errors

### Files

- `src/Envanex.Domain/Aggregates/Products/Product.cs` — modified — `CodeMaxLength = 50`,
  `NameMaxLength = 200` sabitleri. `Create`'e uzunluk kontrolleri. `Update(...)` metodu.
- `src/Envanex.Domain/Aggregates/Products/ProductErrors.cs` — modified — `CodeTooLong`, `NameTooLong`,
  `NotFound`, `ConcurrencyConflict`, `DuplicateCode`, `UnitOfMeasureNotFound`, `UnitOfMeasureInactive`
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasure.cs` — modified — `CodeMaxLength = 20`,
  `NameMaxLength = 200`. `Create`'e uzunluk kontrolleri.
- `src/Envanex.Domain/Aggregates/UnitOfMeasures/UnitOfMeasureErrors.cs` — modified — `CodeTooLong`,
  `NameTooLong`, `DuplicateCode`, `NotFound`
- `tests/Envanex.Domain.Tests/Aggregates/Products/ProductTests.cs` — modified
- `tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs` — modified

### Signatures

```csharp
// Product.cs — sabitler doğrudan aggregate üzerinde, ayrı bir Constants sınıfında DEĞİL
public const int CodeMaxLength = 50;
public const int NameMaxLength = 200;

// Product.Create — mevcut boşluk kontrollerinden sonra:
//   code.Trim().Length > CodeMaxLength → Result.Failure<Product>(ProductErrors.CodeTooLong)
//   name.Trim().Length > NameMaxLength → Result.Failure<Product>(ProductErrors.NameTooLong)

public Result Update(string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint);
// - ArgumentNullException.ThrowIfNull(listPrice)
// - name boş → NameRequired; name.Trim().Length > NameMaxLength → NameTooLong
// - unitOfMeasureId == Guid.Empty → UnitOfMeasureRequired
// - name = name.Trim()
// - Name, UnitOfMeasureId, ListPrice, ReorderPoint ayarlanır
// - Code'a DOKUNMAZ (immutable)
// - return Result.Success()

// ProductErrors.cs — yeni alanlar
public static readonly Error CodeTooLong = new("Product.CodeTooLong", "Ürün kodu en fazla 50 karakter olabilir.");
public static readonly Error NameTooLong = new("Product.NameTooLong", "Ürün adı en fazla 200 karakter olabilir.");
public static readonly Error NotFound = new("Product.NotFound", "Ürün bulunamadı.");
public static readonly Error ConcurrencyConflict = new("Product.ConcurrencyConflict",
    "Kayıt başka bir kullanıcı tarafından değiştirilmiş. Lütfen sayfayı yenileyip tekrar deneyin.");
public static readonly Error DuplicateCode = new("Product.DuplicateCode", "Bu ürün kodu zaten kullanılıyor.");
public static readonly Error UnitOfMeasureNotFound = new("Product.UnitOfMeasureNotFound", "Belirtilen ölçü birimi bulunamadı.");
public static readonly Error UnitOfMeasureInactive = new("Product.UnitOfMeasureInactive", "Pasif bir ölçü birimi atanamaz.");

// UnitOfMeasure.cs
public const int CodeMaxLength = 20;
public const int NameMaxLength = 200;
// Create'e aynı uzunluk kontrolleri

// UnitOfMeasureErrors.cs
public static readonly Error CodeTooLong = new("UnitOfMeasure.CodeTooLong", "Ölçü birimi kodu en fazla 20 karakter olabilir.");
public static readonly Error NameTooLong = new("UnitOfMeasure.NameTooLong", "Ölçü birimi adı en fazla 200 karakter olabilir.");
public static readonly Error DuplicateCode = new("UnitOfMeasure.DuplicateCode", "Bu ölçü birimi kodu zaten kullanılıyor.");
public static readonly Error NotFound = new("UnitOfMeasure.NotFound", "Ölçü birimi bulunamadı.");
public static readonly Error BaseUnitNotFound = new("UnitOfMeasure.BaseUnitNotFound", "Belirtilen temel ölçü birimi bulunamadı.");
public static readonly Error BaseUnitInactive = new("UnitOfMeasure.BaseUnitInactive", "Pasif bir temel ölçü birimi atanamaz.");
```

### Tests to add

`ProductTests.cs` (mevcut sınıfa eklenir):

- `Create_WithCodeExceedingMaxLength_ShouldFail`
- `Create_WithNameExceedingMaxLength_ShouldFail`
- `Update_WithValidInputs_ShouldSucceed`
- `Update_WithEmptyName_ShouldFail`
- `Update_WithNameExceedingMaxLength_ShouldFail`
- `Update_WithEmptyGuidUnitOfMeasureId_ShouldFail`
- `Update_ShouldTrimName`
- `Update_ShouldNotChangeCode`
- `Update_WithNullListPrice_ShouldThrow`

`UnitOfMeasureTests.cs` (mevcut sınıfa eklenir):

- `Create_WithCodeExceedingMaxLength_ShouldFail`
- `Create_WithNameExceedingMaxLength_ShouldFail`

Sabitin literal değerini doğrulayan test YAZILMAZ (`MaxCodeLength_ShouldBe50` gibi). Testler davranış
doğrular.

### Validation

```
dotnet build -warnaserror
dotnet test --filter "FullyQualifiedName~ProductTests|FullyQualifiedName~UnitOfMeasureTests"
dotnet format --verify-no-changes
```

---

## Phase 2 — Abstractions, DTOs, commands, queries, validators, ValidationDecorator

### Files

Abstractions:

- `src/Envanex.Application/Abstractions/Messaging/ICommandHandler.cs`
- `src/Envanex.Application/Abstractions/Messaging/IQueryHandler.cs`
- `src/Envanex.Application/Abstractions/Persistence/IProductRepository.cs`
- `src/Envanex.Application/Abstractions/Persistence/IProductReadRepository.cs`
- `src/Envanex.Application/Abstractions/Persistence/IUnitOfMeasureRepository.cs`
- `src/Envanex.Application/Abstractions/Persistence/IUnitOfMeasureReadRepository.cs`
- `src/Envanex.Application/Abstractions/Persistence/IUnitOfWork.cs`
- `src/Envanex.Application/Abstractions/Persistence/ConcurrencyConflictException.cs`
- `src/Envanex.Application/Abstractions/Persistence/DuplicateKeyException.cs`
- `src/Envanex.Application/Behaviors/ValidationDecorator.cs`

Product:

- `src/Envanex.Application/Products/DTOs/ProductDetailDto.cs`
- `src/Envanex.Application/Products/DTOs/ProductListDto.cs`
- `src/Envanex.Application/Products/Commands/CreateProductCommand.cs`
- `src/Envanex.Application/Products/Commands/UpdateProductCommand.cs`
- `src/Envanex.Application/Products/Commands/ActivateProductCommand.cs`
- `src/Envanex.Application/Products/Commands/DeactivateProductCommand.cs`
- `src/Envanex.Application/Products/Queries/GetProductByIdQuery.cs`
- `src/Envanex.Application/Products/Validators/CreateProductCommandValidator.cs`
- `src/Envanex.Application/Products/Validators/UpdateProductCommandValidator.cs`
- `src/Envanex.Application/Products/Validators/ActivateProductCommandValidator.cs`
- `src/Envanex.Application/Products/Validators/DeactivateProductCommandValidator.cs`

UnitOfMeasure:

- `src/Envanex.Application/UnitOfMeasures/DTOs/UnitOfMeasureDetailDto.cs`
- `src/Envanex.Application/UnitOfMeasures/DTOs/UnitOfMeasureListDto.cs`
- `src/Envanex.Application/UnitOfMeasures/Commands/CreateUnitOfMeasureCommand.cs`
- `src/Envanex.Application/UnitOfMeasures/Queries/GetUnitOfMeasureByIdQuery.cs`
- `src/Envanex.Application/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidator.cs`

Tests:

- `tests/Envanex.Application.Tests/Products/Validators/CreateProductCommandValidatorTests.cs`
- `tests/Envanex.Application.Tests/Products/Validators/UpdateProductCommandValidatorTests.cs`
- `tests/Envanex.Application.Tests/Products/Validators/ActivateProductCommandValidatorTests.cs`
- `tests/Envanex.Application.Tests/Products/Validators/DeactivateProductCommandValidatorTests.cs`
- `tests/Envanex.Application.Tests/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidatorTests.cs`
- `tests/Envanex.Application.Tests/Behaviors/ValidationDecoratorTests.cs`

Test projesine YENİ PAKET EKLENMEZ. FluentValidation, Application referansından transitif gelir.
`Directory.Packages.props`'a test için giriş AÇILMAZ.

### Signatures

```csharp
public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);

    // TUZAK: entity GetByIdAsync ile yüklendiğinde OriginalValue zaten veritabanının güncel
    // değeridir. İstemciden gelen değerle ÜZERİNE YAZILMAZSA WHERE koşulu her zaman eşleşir,
    // DbUpdateConcurrencyException hiç fırlamaz ve concurrency token dekoratif kalır.
    // Hata sessizdir: mutlu yol testleri de geçer.
    void SetOriginalRowVersion(Product product, byte[] rowVersion);
}

// IQueryable yalnızca üzerine kompozisyon yapılan yerde (datasource) döner
public interface IProductReadRepository
{
    IQueryable<ProductListDto> GetAll();
    Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(UnitOfMeasure unit, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<bool?> GetActiveStatusAsync(Guid id, CancellationToken ct = default);
    // null = yok, false = var ama pasif, true = var ve aktif
}

// UoM'un datasource endpoint'i yok — IQueryable döndürmez
public interface IUnitOfMeasureReadRepository
{
    Task<IReadOnlyList<UnitOfMeasureListDto>> GetAllAsync(CancellationToken ct = default);
    Task<UnitOfMeasureDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Application'ın kendi kalıcılık exception'ları. EF tipleri Application'da GÖRÜNMEZ.
// Infrastructure'daki UnitOfWork çeviriyi yapar (Faz 4).
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string? message = null, Exception? innerException = null);
}

public sealed class DuplicateKeyException : Exception
{
    public string? ConstraintName { get; }
    public DuplicateKeyException(string? constraintName = null, Exception? innerException = null);
}

// DTO'lar — DÜZ, Money nested değil
public sealed record ProductDetailDto(
    Guid Id, string Code, string Name, Guid UnitOfMeasureId,
    string UnitOfMeasureName, decimal ListPriceAmount,
    string ListPriceCurrency, decimal ReorderPoint,
    bool IsActive, byte[] RowVersion);

public sealed record ProductListDto(
    Guid Id, string Code, string Name, string UnitOfMeasureName,
    decimal ListPriceAmount, string ListPriceCurrency,
    decimal ReorderPoint, bool IsActive, byte[] RowVersion);

public sealed record UnitOfMeasureDetailDto(
    Guid Id, string Code, string Name, Guid? BaseUnitId,
    string? BaseUnitName, decimal ConversionFactor,
    bool IsActive, byte[] RowVersion);

public sealed record UnitOfMeasureListDto(
    Guid Id, string Code, string Name, bool IsActive);

// Commands
public sealed record CreateProductCommand(
    string Code, string Name, Guid UnitOfMeasureId,
    decimal ListPriceAmount, string ListPriceCurrency, decimal ReorderPoint);

public sealed record UpdateProductCommand(
    Guid Id, string Name, Guid UnitOfMeasureId,
    decimal ListPriceAmount, string ListPriceCurrency,
    decimal ReorderPoint, byte[] RowVersion);

public sealed record ActivateProductCommand(Guid Id, byte[] RowVersion);
public sealed record DeactivateProductCommand(Guid Id, byte[] RowVersion);

public sealed record CreateUnitOfMeasureCommand(
    string Code, string Name, Guid? BaseUnitId, decimal ConversionFactor);

// Queries
public sealed record GetProductByIdQuery(Guid Id);
public sealed record GetUnitOfMeasureByIdQuery(Guid Id);

// ValidationDecorator<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
// ctor: (ICommandHandler<TCommand, TResponse> inner, IEnumerable<IValidator<TCommand>> validators)
// Hata kodu: "Validation.<PropertyName>", mesaj FluentValidation'dan gelir.
// Validator yoksa doğrudan inner'a devreder.
```

Validator kuralları — hepsi domain sabitlerini referans alır, sayıyı tekrar yazmaz:

- `CreateProductCommandValidator`: `Code` NotEmpty + `MaximumLength(Product.CodeMaxLength)`,
  `Name` NotEmpty + `MaximumLength(Product.NameMaxLength)`, `UnitOfMeasureId` `NotEqual(Guid.Empty)`,
  `ListPriceAmount >= 0`, `ListPriceCurrency` NotEmpty + `Length(3)`, `ReorderPoint >= 0`
- `UpdateProductCommandValidator`: `Id NotEqual(Guid.Empty)`, `Name` NotEmpty + MaxLength,
  `UnitOfMeasureId NotEqual(Guid.Empty)`, `ListPriceAmount >= 0`, `ListPriceCurrency` NotEmpty +
  `Length(3)`, `ReorderPoint >= 0`, `RowVersion` NotNull + NotEmpty
- `ActivateProductCommandValidator`: `Id NotEqual(Guid.Empty)`, `RowVersion` NotNull + NotEmpty
- `DeactivateProductCommandValidator`: aynı
- `CreateUnitOfMeasureCommandValidator`: `Code`/`Name` NotEmpty + MaxLength,
  `ConversionFactor` conditional:
    - When `BaseUnitId` is null → `ConversionFactor` must equal 1 (message: "Temel birim için dönüşüm katsayısı 1 olmalıdır.")
    - When `BaseUnitId` is not null → `ConversionFactor` must be > 0 (message: "Türetilmiş birim için dönüşüm katsayısı sıfırdan büyük olmalıdır.")

### Tests to add

`CreateProductCommandValidatorTests.cs`: `Validate_WithValidCommand_ShouldPass`,
`Validate_WithEmptyCode_ShouldFail`, `Validate_WithCodeExceedingMaxLength_ShouldFail`,
`Validate_WithEmptyName_ShouldFail`, `Validate_WithNameExceedingMaxLength_ShouldFail`,
`Validate_WithEmptyGuidUnitOfMeasureId_ShouldFail`, `Validate_WithNegativeListPriceAmount_ShouldFail`,
`Validate_WithEmptyListPriceCurrency_ShouldFail`, `Validate_WithNegativeReorderPoint_ShouldFail`

`UpdateProductCommandValidatorTests.cs`: `Validate_WithValidCommand_ShouldPass`,
`Validate_WithEmptyName_ShouldFail`, `Validate_WithNameExceedingMaxLength_ShouldFail`,
`Validate_WithEmptyGuidUnitOfMeasureId_ShouldFail`, `Validate_WithNullRowVersion_ShouldFail`,
`Validate_WithEmptyRowVersion_ShouldFail`

`ActivateProductCommandValidatorTests.cs`: `Validate_WithValidCommand_ShouldPass`,
`Validate_WithEmptyGuidId_ShouldFail`, `Validate_WithNullRowVersion_ShouldFail`,
`Validate_WithEmptyRowVersion_ShouldFail`

`DeactivateProductCommandValidatorTests.cs`: aynı dört vaka

`CreateUnitOfMeasureCommandValidatorTests.cs`: `Validate_WithValidBaseUnitCommand_ShouldPass`,
`Validate_WithValidDerivedUnitCommand_ShouldPass`, `Validate_WithEmptyCode_ShouldFail`,
`Validate_WithCodeExceedingMaxLength_ShouldFail`, `Validate_WithEmptyName_ShouldFail`,
`Validate_WithNameExceedingMaxLength_ShouldFail`,
`Validate_BaseUnitWithConversionFactorNotOne_ShouldFail`,
`Validate_DerivedUnitWithZeroConversionFactor_ShouldFail`

`ValidationDecoratorTests.cs`: `HandleAsync_WithValidCommand_ShouldDelegateToInner`,
`HandleAsync_WithInvalidCommand_ShouldReturnFailure_WithoutCallingInner`,
`HandleAsync_WithNoValidators_ShouldDelegateToInner`

### Validation

```
dotnet build -warnaserror
dotnet test --filter "FullyQualifiedName~Application.Tests"
dotnet format --verify-no-changes
```

---

## Phase 3 — Command/query handlers, fakes, handler tests

### Files

Handlers:

- `src/Envanex.Application/Products/Commands/CreateProductCommandHandler.cs`
- `src/Envanex.Application/Products/Commands/UpdateProductCommandHandler.cs`
- `src/Envanex.Application/Products/Commands/ActivateProductCommandHandler.cs`
- `src/Envanex.Application/Products/Commands/DeactivateProductCommandHandler.cs`
- `src/Envanex.Application/Products/Queries/GetProductByIdQueryHandler.cs`
- `src/Envanex.Application/UnitOfMeasures/Commands/CreateUnitOfMeasureCommandHandler.cs`
- `src/Envanex.Application/UnitOfMeasures/Queries/GetUnitOfMeasureByIdQueryHandler.cs`

Fakes (elle yazılmış, mock kütüphanesi YOK):

- `tests/Envanex.Application.Tests/Fakes/FakeProductRepository.cs`
- `tests/Envanex.Application.Tests/Fakes/FakeProductReadRepository.cs`
- `tests/Envanex.Application.Tests/Fakes/FakeUnitOfMeasureRepository.cs`
- `tests/Envanex.Application.Tests/Fakes/FakeUnitOfMeasureReadRepository.cs`
- `tests/Envanex.Application.Tests/Fakes/FakeUnitOfWork.cs`

Tests:

- `tests/Envanex.Application.Tests/Products/Commands/CreateProductCommandHandlerTests.cs`
- `tests/Envanex.Application.Tests/Products/Commands/UpdateProductCommandHandlerTests.cs`
- `tests/Envanex.Application.Tests/Products/Commands/ActivateProductCommandHandlerTests.cs`
- `tests/Envanex.Application.Tests/Products/Commands/DeactivateProductCommandHandlerTests.cs`
- `tests/Envanex.Application.Tests/Products/Queries/GetProductByIdQueryHandlerTests.cs`
- `tests/Envanex.Application.Tests/UnitOfMeasures/Commands/CreateUnitOfMeasureCommandHandlerTests.cs`
- `tests/Envanex.Application.Tests/UnitOfMeasures/Queries/GetUnitOfMeasureByIdQueryHandlerTests.cs`

### Signatures

Handler'lar YALNIZCA Application'ın kendi exception tiplerini yakalar. `DbUpdateException` ve
`DbUpdateConcurrencyException` bu katmanda GÖRÜNMEZ — çeviriyi Faz 4'teki `UnitOfWork` yapar.

```csharp
// CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
// Akış: ExistsByCodeAsync → true ise DuplicateCode
//     | GetActiveStatusAsync (null → UnitOfMeasureNotFound, false → UnitOfMeasureInactive)
//     | Currency.Of → Money.Of → Quantity.Of → Product.Create
//     | AddAsync → SaveChangesAsync
//     | catch DuplicateKeyException → ProductErrors.DuplicateCode   (yarış durumu)
//     | return product.Id

// UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, Guid>
// Akış: GetByIdAsync (null → ProductErrors.NotFound)
//     | GetActiveStatusAsync (null → UnitOfMeasureNotFound, false → UnitOfMeasureInactive)
//     | Currency.Of → Money.Of → Quantity.Of → product.Update(name, uomId, listPrice, reorderPoint)
//     | SetOriginalRowVersion(product, command.RowVersion) → SaveChangesAsync
//     | catch ConcurrencyConflictException → ProductErrors.ConcurrencyConflict
//     | return product.Id

// ActivateProductCommandHandler : ICommandHandler<ActivateProductCommand, Guid>
// Akış: GetByIdAsync (null → NotFound) → product.Activate()
//     → SetOriginalRowVersion → SaveChangesAsync
//     → catch ConcurrencyConflictException → ProductErrors.ConcurrencyConflict
//     → return product.Id

// DeactivateProductCommandHandler : ICommandHandler<DeactivateProductCommand, Guid>
// Aynı akış, product.Deactivate() ile

// GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDetailDto>
// Akış: readRepo.GetByIdAsync → null → ProductErrors.NotFound | Result.Success(dto)

// CreateUnitOfMeasureCommandHandler : ICommandHandler<CreateUnitOfMeasureCommand, Guid>
// Akış: ExistsByCodeAsync → DuplicateCode
//     | BaseUnitId != null ise: GetActiveStatusAsync (aynı IUnitOfMeasureRepository üzerinde)
//       (null → UnitOfMeasureErrors.BaseUnitNotFound, false → UnitOfMeasureErrors.BaseUnitInactive)
//     | BaseUnitId == null ise: bu kontrol atlanır
//     | UnitOfMeasure.Create → AddAsync → SaveChangesAsync
//     | catch DuplicateKeyException → UnitOfMeasureErrors.DuplicateCode | return unit.Id

// GetUnitOfMeasureByIdQueryHandler : IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto>
// Akış: readRepo.GetByIdAsync → null → UnitOfMeasureErrors.NotFound | Result.Success(dto)

// Fake'ler: List<T> bazlı. FakeUnitOfWork bir SaveChangesCalled sayacı tutar ve
// testin isteğine göre ConcurrencyConflictException / DuplicateKeyException fırlatabilir.
```

### Tests to add

`CreateProductCommandHandlerTests.cs`: `HandleAsync_WithValidCommand_ShouldReturnProductId`,
`HandleAsync_WithDuplicateCode_ShouldReturnDuplicateCodeError`,
`HandleAsync_WithNonExistentUnitOfMeasure_ShouldReturnUnitOfMeasureNotFoundError`,
`HandleAsync_WithInactiveUnitOfMeasure_ShouldReturnUnitOfMeasureInactiveError`,
`HandleAsync_WithInvalidCurrency_ShouldReturnCurrencyError`,
`HandleAsync_WhenUnitOfWorkThrowsDuplicateKey_ShouldReturnDuplicateCodeError`,
`HandleAsync_ShouldCallSaveChanges`

`UpdateProductCommandHandlerTests.cs`: `HandleAsync_WithValidCommand_ShouldReturnProductId`,
`HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError`,
`HandleAsync_WithNonExistentUnitOfMeasure_ShouldReturnUnitOfMeasureNotFoundError`,
`HandleAsync_WithInactiveUnitOfMeasure_ShouldReturnUnitOfMeasureInactiveError`,
`HandleAsync_ShouldCallSetOriginalRowVersion`,
`HandleAsync_WhenUnitOfWorkThrowsConcurrencyConflict_ShouldReturnConcurrencyConflictError`

`ActivateProductCommandHandlerTests.cs`:
`HandleAsync_WithExistingProduct_ShouldActivateAndReturnProductId`,
`HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError`,
`HandleAsync_ShouldCallSetOriginalRowVersion`,
`HandleAsync_WhenUnitOfWorkThrowsConcurrencyConflict_ShouldReturnConcurrencyConflictError`

`DeactivateProductCommandHandlerTests.cs`: aynı dört vaka, deactivate için

`GetProductByIdQueryHandlerTests.cs`: `HandleAsync_WithExistingProduct_ShouldReturnProductDetail`,
`HandleAsync_WithNonExistentProduct_ShouldReturnNotFoundError`

`CreateUnitOfMeasureCommandHandlerTests.cs`:
`HandleAsync_WithValidBaseUnitCommand_ShouldReturnUnitId`,
`HandleAsync_WithDuplicateCode_ShouldReturnDuplicateCodeError`,
`HandleAsync_WhenUnitOfWorkThrowsDuplicateKey_ShouldReturnDuplicateCodeError`,
`HandleAsync_WithNonExistentBaseUnit_ShouldReturnBaseUnitNotFoundError`,
`HandleAsync_WithInactiveBaseUnit_ShouldReturnBaseUnitInactiveError`,
`HandleAsync_WithNullBaseUnit_ShouldNotCheckBaseUnit`

`GetUnitOfMeasureByIdQueryHandlerTests.cs`: `HandleAsync_WithExistingUnit_ShouldReturnUnitDetail`,
`HandleAsync_WithNonExistentUnit_ShouldReturnNotFoundError`

### Validation

```
dotnet build -warnaserror
dotnet test --filter "FullyQualifiedName~Application.Tests"
dotnet format --verify-no-changes
```

---

## Phase 4 — Infrastructure repositories, UnitOfWork translation, ColumnNames, DI wiring, architecture and integration tests

### Files

- `src/Envanex.Infrastructure/Persistence/Constants/ColumnNames.cs` — created
- `src/Envanex.Infrastructure/Persistence/Conventions/AggregateRootConvention.cs` — modified —
  `"RowVersion"` → `ColumnNames.RowVersion`. **`Migrations/` ve `ModelSnapshot`'a DOKUNMA.**
- `src/Envanex.Infrastructure/Persistence/Repositories/ProductRepository.cs` — created
- `src/Envanex.Infrastructure/Persistence/Repositories/ProductReadRepository.cs` — created
- `src/Envanex.Infrastructure/Persistence/Repositories/UnitOfMeasureRepository.cs` — created
- `src/Envanex.Infrastructure/Persistence/Repositories/UnitOfMeasureReadRepository.cs` — created
- `src/Envanex.Infrastructure/Persistence/UnitOfWork.cs` — created
- `src/Envanex.Application/DependencyInjection.cs` — created — `AddApplication()`
- `src/Envanex.Application/Envanex.Application.csproj` — modified —
  `Microsoft.Extensions.DependencyInjection.Abstractions` eklenir
- `Directory.Packages.props` — modified — DI abstractions versiyon girişi
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — repository'ler ve `UnitOfWork`
- `src/Envanex.Web/Envanex.Web.csproj` — modified — `Envanex.Application`'a doğrudan `ProjectReference`
- `src/Envanex.Web/Program.cs` — modified — `AddApplication()` eklenir (`AddInfrastructure()` öncesinde)
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified
- `tests/Envanex.IntegrationTests/Persistence/RepositoryTests.cs` — created
- `docs/adr/0005-application-layer-patterns.md` — created (yalnızca başlık satırı)

Test projelerine YENİ PAKET EKLENMEZ.

### Signatures

```csharp
internal static class ColumnNames
{
    public const string RowVersion = "RowVersion";
}
// Yalnızca elle yazılan koda uygulanır. Migrations/ ve ModelSnapshot dosyaları
// üretilmiş dosyalardır ve bu sabite bağlanmak için düzenlenmez.

// ProductRepository.SetOriginalRowVersion — TUZAK
// entity GetByIdAsync ile yüklendiğinde OriginalValue zaten veritabanının güncel değeridir.
// İstemciden gelen değerle ÜZERİNE YAZILMAZSA WHERE koşulu her zaman eşleşir,
// DbUpdateConcurrencyException hiç fırlamaz. Hata sessizdir: mutlu yol testleri de geçer.
public void SetOriginalRowVersion(Product product, byte[] rowVersion)
{
    _context.Entry(product).Property<byte[]>(ColumnNames.RowVersion).OriginalValue = rowVersion;
}

// ProductReadRepository.GetAll() — IQueryable<ProductListDto>
//   AsNoTracking, UnitOfMeasures join (UnitOfMeasureName),
//   EF.Property<byte[]>(p, ColumnNames.RowVersion) ile RowVersion yüzeye çıkar,
//   Select(...) ile sorgu Infrastructure'da BİTER.

// UnitOfMeasureRepository.GetActiveStatusAsync — tek sorgu
public async Task<bool?> GetActiveStatusAsync(Guid id, CancellationToken ct)
{
    return await _context.UnitOfMeasures
        .Where(u => u.Id == id)
        .Select(u => (bool?)u.IsActive)
        .FirstOrDefaultAsync(ct);   // null = bulunamadı
}

// UnitOfMeasureReadRepository.GetAllAsync — Task<IReadOnlyList<UnitOfMeasureListDto>>, ToListAsync

// UnitOfWork — EF exception'larını Application tiplerine ÇEVİREN tek yer.
// Bu sayede Application katmanı Microsoft.EntityFrameworkCore'a hiç referans vermez ve
// Application_ShouldNotReference_EntityFrameworkPackages testi yeşil kalır.
public async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    try
    {
        return await _context.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        throw new ConcurrencyConflictException(innerException: ex);
    }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex))
    {
        throw new DuplicateKeyException(ExtractConstraintName(ex), ex);
    }
    // Diğer her şey olduğu gibi yukarı çıkar.
}
// IsUniqueViolation: ex.InnerException is SqlException { Number: 2601 or 2627 }
// Microsoft.Data.SqlClient yalnızca Infrastructure'da görünür.
```

`AddApplication()` — açık kayıt, tarama YOK, Scrutor YOK:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    // Validators
    services.AddSingleton<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
    services.AddSingleton<IValidator<UpdateProductCommand>, UpdateProductCommandValidator>();
    services.AddSingleton<IValidator<ActivateProductCommand>, ActivateProductCommandValidator>();
    services.AddSingleton<IValidator<DeactivateProductCommand>, DeactivateProductCommandValidator>();
    services.AddSingleton<IValidator<CreateUnitOfMeasureCommand>, CreateUnitOfMeasureCommandValidator>();

    // Inner handlers (concrete types)
    services.AddScoped<CreateProductCommandHandler>();
    services.AddScoped<UpdateProductCommandHandler>();
    services.AddScoped<ActivateProductCommandHandler>();
    services.AddScoped<DeactivateProductCommandHandler>();
    services.AddScoped<CreateUnitOfMeasureCommandHandler>();

    // Decorated command handlers — ICommandHandler<TCommand, Guid>, Result<Guid> DEĞİL
    services.AddScoped<ICommandHandler<CreateProductCommand, Guid>>(sp =>
        new ValidationDecorator<CreateProductCommand, Guid>(
            sp.GetRequiredService<CreateProductCommandHandler>(),
            sp.GetServices<IValidator<CreateProductCommand>>()));

    services.AddScoped<ICommandHandler<UpdateProductCommand, Guid>>(sp =>
        new ValidationDecorator<UpdateProductCommand, Guid>(
            sp.GetRequiredService<UpdateProductCommandHandler>(),
            sp.GetServices<IValidator<UpdateProductCommand>>()));

    services.AddScoped<ICommandHandler<ActivateProductCommand, Guid>>(sp =>
        new ValidationDecorator<ActivateProductCommand, Guid>(
            sp.GetRequiredService<ActivateProductCommandHandler>(),
            sp.GetServices<IValidator<ActivateProductCommand>>()));

    services.AddScoped<ICommandHandler<DeactivateProductCommand, Guid>>(sp =>
        new ValidationDecorator<DeactivateProductCommand, Guid>(
            sp.GetRequiredService<DeactivateProductCommandHandler>(),
            sp.GetServices<IValidator<DeactivateProductCommand>>()));

    services.AddScoped<ICommandHandler<CreateUnitOfMeasureCommand, Guid>>(sp =>
        new ValidationDecorator<CreateUnitOfMeasureCommand, Guid>(
            sp.GetRequiredService<CreateUnitOfMeasureCommandHandler>(),
            sp.GetServices<IValidator<CreateUnitOfMeasureCommand>>()));

    // Query handlers — decorate edilmez
    services.AddScoped<IQueryHandler<GetProductByIdQuery, ProductDetailDto>,
        GetProductByIdQueryHandler>();
    services.AddScoped<IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto>,
        GetUnitOfMeasureByIdQueryHandler>();

    return services;
}
```

`AddInfrastructure()` içine eklenecekler:

```csharp
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<IProductReadRepository, ProductReadRepository>();
services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
services.AddScoped<IUnitOfMeasureReadRepository, UnitOfMeasureReadRepository>();
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

### Tests to add

`ArchitectureTests.cs`:

- `Application_ShouldNotReference_EntityFrameworkPackages` — `Envanex.Application.csproj` içinde
  `Microsoft.EntityFrameworkCore` içeren `PackageReference` olmamalı. Mevcut dört test yalnızca
  `ProjectReference`'a bakıyor; `IQueryable<TDto>` ve exception-çevirisi kararlarını koruyan
  mekanizma budur.
- `Web_ShouldOnlyReference_ApplicationInfrastructureAndSoapApi`

`RepositoryTests.cs` (`[Collection(DatabaseCollection.Name)]`, mevcut `SqlServerFixture` üzerinde):

- `UnitOfWork_SaveChangesAsync_WithStaleRowVersion_ShouldThrowConcurrencyConflictException`
- `UnitOfWork_SaveChangesAsync_WithDuplicateCode_ShouldThrowDuplicateKeyException`
- `ProductRepository_SetOriginalRowVersion_WithStaleValue_ShouldCauseConcurrencyConflict`
- `ProductRepository_ExistsByCodeAsync_ExistingCode_ShouldReturnTrue`
- `ProductReadRepository_GetAll_ShouldSurfaceRowVersion`
- `ProductReadRepository_GetAll_ShouldJoinUnitOfMeasureName`
- `ProductReadRepository_GetByIdAsync_ShouldReturnDetailDto`
- `UnitOfMeasureRepository_GetActiveStatusAsync_Active_ShouldReturnTrue`
- `UnitOfMeasureRepository_GetActiveStatusAsync_Inactive_ShouldReturnFalse`
- `UnitOfMeasureRepository_GetActiveStatusAsync_NonExistent_ShouldReturnNull`

Unique-index yakalamasının tek gerçek testi `UnitOfWork_SaveChangesAsync_WithDuplicateCode_...` —
ön kontrolü atlayarak doğrudan repository üzerinden aynı kodla ikinci kayıt eklenir.

### Validation

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Rollback notes

Migration yok, şema değişmiyor; geri alma veritabanı işlemi gerektirmez. Tüm değişiklikler additif.
`ColumnNames` sabiti `AggregateRootConvention`'da aynı string değerini üretir, SQL özdeş.
`Envanex.Web` → `Envanex.Application` `ProjectReference`'ı additif; kaldırmak PR 5b'yi kırar ama
PR 5a'yı kırmaz. `docs/adr/0005` boş dosya, silmek güvenli. PR 5a, HTTP yüzeyi olmadan bağımsız
geçerlidir.
