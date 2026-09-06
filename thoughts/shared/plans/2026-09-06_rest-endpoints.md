# Plan: PR 5b — REST Controllers, Hardened Grid Datasource, Integration Tests

## Goal

PR 5a'da inşa edilen Application katmanını HTTP üzerinden sunmak: Product (create, update, get by id,
activate, deactivate, datasource) ve UnitOfMeasure (create, get by id, list) için REST controller'lar,
`Result` → `IActionResult` eşlemesi ProblemDetails (RFC 9457) ile, sertleştirilmiş datasource
endpoint'i, rate limiting, güvenlik başlıkları ve integration testleri.

## Non-goals

- Blazor UI bileşenleri (PR 7)
- Authentication/authorization (PR 6)
- Warehouse endpoint'leri (PR 8'e ertelendi)
- SOAP entegrasyonu (PR 17)
- **CSP başlığı (PR 7)** — gerçek UI Radzen ile PR 7'de geliyor; şimdi yazılan bir CSP orada baştan
  yazılırdı
- `ListProductsQuery` veya `GET /api/products` — datasource tek liste yüzeyi
- UnitOfMeasure update, activate, deactivate veya datasource
- Yeni migration

## Touches schema?

No.

## ADR needed?

`docs/adr/0006-rest-error-mapping.md` — REST error mapping and concurrency protocol. Coder yalnızca
başlık satırını ve bir HTML yorumunu içeren boş dosyayı oluşturur; içeriği insan yazar.

ADR'de gerekçelendirilmesi gereken noktalar:

- FK hatalarının (`UnitOfMeasureNotFound`, `UnitOfMeasureInactive`) 400 yerine **422**'ye eşlenmesi.
  Sözdizimi geçerli, gövde iyi biçimli; reddedilme sebebi bir iş kuralı — bu ayrımın neden
  korunduğu yazılmalı.
- Eşlenmemiş bir `Result.Failure`'ın **400**'e gitmesi, 500'e değil: `Result.Failure` bir iş
  hatasıdır; 500 sunucu arızası demektir ve kullanıcı hatası yüzünden alarm çalar.
- Concurrency protokolü: `RowVersion` istemciye `byte[]` (JSON'da base64) olarak gider, güncelleme
  isteğinde geri gelir, uyuşmazlıkta 409.
- Eşlemelerin Web katmanında açık bir kod tablosu olarak yaşaması, `Error` tipinin üzerinde bir
  kategori olarak değil. Neden: Domain katmanı HTTP semantiğinden habersiz kalmalı; HTTP durum kodu
  seçimi sunumun kararıdır. Ancak PR 17 SOAP'ı ikinci tüketici olarak eklediğinde, tablo yerine
  `Error` üzerinde bir `ErrorCategory` enum'u (`NotFound`, `Conflict`, `Unprocessable`, `Validation`,
  `General`) tanımlamak ve her tüketicinin kategoriyi kendi protokolüne çevirmesi daha sürdürülebilir
  olacaktır.

---

## Phase 1 — ResultExtensions, security headers, minimal controller, WebApplicationFactory

### Files

- `src/Envanex.Web/Extensions/ResultExtensions.cs` — created
- `src/Envanex.Web/Middleware/SecurityHeadersMiddleware.cs` — created
- `src/Envanex.Web/Controllers/ProductsController.cs` — created (minimal: yalnızca `GetById`)
- `src/Envanex.Web/Program.cs` — modified — `AddControllers()`, `MapControllers()`, güvenlik
  başlıkları middleware'i. Mevcut `UseHsts()`, `UseHttpsRedirection()` ve `UseAntiforgery()`
  çağrılarının KALDIRILMADIĞI doğrulanır.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — created
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified (PR 5a'da eklenmediyse)
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs` — created (minimal)
- `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — created

### Signatures

```csharp
// ResultExtensions — IActionResult döner, IResult DEĞİL.
// MVC controller kullanıyoruz; IResult minimal API dünyasına ait.
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result);
    public static IActionResult ToCreatedActionResult<T>(
        this Result<T> result, string routeName, Func<T, object> routeValues);

    // AÇIK KOD TABLOSU — sonek veya önek eşlemesi DEĞİL.
    // Her Error.Code birebir string eşlemesiyle HTTP durum koduna çevrilir.
    // Tek istisna: "Validation." öneki çalışma zamanında özellik adıyla üretildiği için
    // önek kontrolü kullanır.
    //
    //   AÇIK EŞLEME TABLOSU:
    //   ---------------------------------------------------------------
    //   Error.Code                                   | HTTP Status
    //   ---------------------------------------------------------------
    //
    //   --- 404 Not Found ---
    //   Product.NotFound                              | 404
    //   UnitOfMeasure.NotFound                        | 404
    //
    //   --- 409 Conflict ---
    //   Product.DuplicateCode                         | 409
    //   UnitOfMeasure.DuplicateCode                   | 409
    //   Product.ConcurrencyConflict                   | 409
    //
    //   --- 422 Unprocessable Entity ---
    //   Product.UnitOfMeasureNotFound                 | 422
    //   Product.UnitOfMeasureInactive                 | 422
    //   UnitOfMeasure.BaseUnitNotFound                | 422
    //   UnitOfMeasure.BaseUnitInactive                | 422
    //
    //   --- 400 Bad Request (Product) ---
    //   Product.CodeRequired                          | 400
    //   Product.NameRequired                          | 400
    //   Product.UnitOfMeasureRequired                 | 400
    //   Product.CodeTooLong                           | 400
    //   Product.NameTooLong                           | 400
    //
    //   --- 400 Bad Request (UnitOfMeasure) ---
    //   UnitOfMeasure.CodeRequired                    | 400
    //   UnitOfMeasure.NameRequired                    | 400
    //   UnitOfMeasure.CodeTooLong                     | 400
    //   UnitOfMeasure.NameTooLong                     | 400
    //   UnitOfMeasure.InvalidBaseUnitId               | 400
    //   UnitOfMeasure.BaseUnitFactorMustBeOne         | 400
    //   UnitOfMeasure.ConversionFactorMustBePositive  | 400
    //
    //   --- 400 Bad Request (Warehouse) ---
    //   Warehouse.CodeRequired                        | 400
    //   Warehouse.NameRequired                        | 400
    //
    //   --- 400 Bad Request (Value Objects) ---
    //   Currency.InvalidCode                          | 400
    //   Money.CurrencyMismatch                        | 400
    //   Quantity.Negative                             | 400
    //   Quantity.NegativeResult                       | 400
    //
    //   --- Validation prefix (runtime-generated) ---
    //   Validation.* öneki                            | 400 + alan bazlı hata sözlüğü
    //
    //   --- Fallback (safety net) ---
    //   Tabloda olmayan her şey                       | 400 Bad Request
    //   ---------------------------------------------------------------
    //
    // "Validation.*" TEK İSTİSNA: önek kontrolü kullanır çünkü özellik adı
    // çalışma zamanında üretilir. Diğer HER eşleme tam string eşlemesidir.
    //
    // Uygulama: FrozenDictionary<string, int> veya aynı anlama gelen sabit
    // arama yapısı. "*.NotFound" gibi sonek eşlemesi YOKTUR —
    // "Product.UnitOfMeasureNotFound" ile "Product.NotFound" farklı
    // HTTP kodlarına eşlenmelidir, sonek eşlemesi bunu karıştırırdı.
    //
    // Tüm hatalar ProblemDetails (RFC 9457) gövdesiyle döner.
    // Hata kodları İngilizce, kullanıcıya giden mesajlar Türkçe.
}

// SecurityHeadersMiddleware — YALNIZCA üç başlık. CSP YOK (PR 7).
//   X-Content-Type-Options: nosniff
//   X-Frame-Options: DENY
//   Referrer-Policy: strict-origin-when-cross-origin

// ProductsController (Faz 1'de minimal)
[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{id:guid}", Name = "GetProductById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct);
}

// EnvanexWebApplicationFactory : WebApplicationFactory<Program>
// - Mevcut SqlServerFixture'ın connection string'ini alır.
// - YENİ xUnit collection AÇMAZ: ikinci bir collection ikinci bir Testcontainers konteyneri
//   başlatır, test süresini ikiye katlar ve CI'da bellek sorunu çıkarır.
//   Tüm API testleri mevcut DatabaseCollection altında kalır.
// - Testing ortamını ayarlar; rate limiting Testing'de devre dışıdır.
//
// Yaşam döngüsü: EnvanexWebApplicationFactory, SqlServerFixture içinde TEK SEFER oluşturulur
// ve fixture'ın DisposeAsync'inde dispose edilir. Koleksiyon başına tek host, test sınıfı başına
// DEĞİL. API test sınıfları factory'yi fixture üzerinden alır, kendileri oluşturmaz.
public sealed class EnvanexWebApplicationFactory : WebApplicationFactory<Program>
{
    public EnvanexWebApplicationFactory(string connectionString);
}
```

### Tests to add

`ArchitectureTests.cs`:

- `Web_ShouldOnlyReference_ApplicationInfrastructureAndSoapApi` (PR 5a'da eklendiyse tekrarlanmaz)

`ProductsApiTests.cs` (`[Collection(DatabaseCollection.Name)]`):

- `GetProductById_WithExistingProduct_ShouldReturn200WithProductDetail`
- `GetProductById_WithNonExistentId_ShouldReturn404WithProblemDetails`
- `GetProductById_ResponseHeaders_ShouldContainXContentTypeOptions`
- `GetProductById_ResponseHeaders_ShouldContainXFrameOptions`
- `GetProductById_ResponseHeaders_ShouldContainReferrerPolicy`

`ResultMappingTests.cs`:

- `ResultMapping_EveryDomainErrorCode_ShouldHaveAnExplicitMapping` — Reflection ile
  `Envanex.Domain` assembly'sini tarar. Assembly'deki TÜM public sınıflardaki (static veya
  non-static) `public static readonly Error` alanlarının `Code` değerlerini toplar ve her kodun
  `ResultExtensions`'daki açık eşleme tablosunda bir girişe sahip olduğunu doğrular. Tarama
  `*Errors` son eki ile sınırlı DEĞİLDİR — değer nesneleri (`CurrencyErrors`, `MoneyErrors`,
  `QuantityErrors`) dahil assembly'deki her `Error` alanını kapsar. Bu test, yeni hata kodlarının
  eşleme tablosu güncellenmeden eklenmesini engeller.
  **Not:** `Validation.*` öneki çalışma zamanında üretilir ve statik sınıf taramasına dahil
  değildir; test onu kapsamaz, çünkü tabloda önek kuralı olarak zaten mevcuttur.

### Validation

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Phase 2 — Full ProductsController: CRUD, activate/deactivate, hardened datasource

### Files

- `src/Envanex.Web/Controllers/ProductsController.cs` — modified — `Create`, `Update`, `Activate`,
  `Deactivate`, `GetDataSource`
- `src/Envanex.Web/DataSource/DataSourceGuard.cs` — created — tek yeniden kullanılabilir sınıf,
  controller action'ına dağılmış `if`'ler DEĞİL
- `src/Envanex.Web/DataSource/DataSourceLoadOptionsModelBinder.cs` — created
- `src/Envanex.Web/DataSource/DataSourceLoadOptionsModelBinderProvider.cs` — created
- `src/Envanex.Web/Program.cs` — modified — model binder provider kaydı
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs` — modified
- `tests/Envanex.IntegrationTests/Api/ProductsDatasourceTests.cs` — created

### Model binder — coder DOĞRULAMALI, TAHMİN ETMEMELİ

`DevExtreme.AspNet.Data` sürümü `Directory.Packages.props`'ta **5.1.0** olarak doğrulandı.
`[FromQuery] DataSourceLoadOptionsBase` DevExtreme'in sorgu formatını otomatik bağlamaz; paket bir
parser sağlar. Custom model binder şunu yapar:

1. Yeni bir `DataSourceLoadOptionsBase` oluşturur
2. `DataSourceLoadOptionsParser.Parse(options, key => valueProvider.GetValue(key).FirstValue)` ile
   doldurur
3. `bindingContext.Result = ModelBindingResult.Success(options)` ayarlar

Coder, faza başlarken kurulu 5.1.0 paketine karşı `DataSourceLoadOptionsParser`'ın public olduğunu ve
`DataSourceLoadOptionsBase`'in parametresiz constructor'ı bulunduğunu **komutla doğrulamalıdır**.
API farklıysa DUR ve bildir; uydurma.

### Signatures

```csharp
// DataSourceGuard — ihlalde 400 döner, SESSİZCE DÜZELTMEZ (clamp etmez).
// Sessiz clamp, istemciye isteğinin karşılandığı yanılgısını verir ve saldırı yüzeyini gizler.
public sealed class DataSourceGuard
{
    public DataSourceGuard(
        IReadOnlySet<string> allowedSortFields,
        IReadOnlySet<string> allowedGroupFields,
        int defaultTake,
        int maxTake);

    // Doğrulama (hepsi ihlalde Result.Failure → controller 400 döner):
    //   Take > maxTake                       → hata
    //   Sort alanı allowlist dışında         → hata
    //   Group alanı allowlist dışında        → hata
    //   RequireGroupCount istendi            → hata
    //   GroupSummary istendi                 → hata
    // Uygulama:
    //   Take belirtilmemişse defaultTake ayarlanır (sınırsız sorgu OLMAZ)
    public Result<DataSourceLoadOptionsBase> ValidateAndApply(DataSourceLoadOptionsBase options);
}

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private static readonly DataSourceGuard Guard = new(
        allowedSortFields: ["Code", "Name", "UnitOfMeasureName", "ListPriceAmount",
                            "ListPriceCurrency", "ReorderPoint", "IsActive"],
        allowedGroupFields: ["UnitOfMeasureName", "IsActive"],
        defaultTake: 20,
        maxTake: 100);
    // RowVersion allowlist'lerde YOK — sıralanabilir veya gruplanabilir bir alan değil.

    [HttpPost]                       // → 201 + Location
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct);

    [HttpPut("{id:guid}")]           // → 200
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct);

    [HttpPost("{id:guid}/activate")]   // POST, PUT DEĞİL
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateProductCommand command, CancellationToken ct);

    [HttpPost("{id:guid}/deactivate")] // POST, PUT DEĞİL
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateProductCommand command, CancellationToken ct);

    [HttpGet("datasource")]
    public async Task<IActionResult> GetDataSource(DataSourceLoadOptionsBase options, CancellationToken ct);
    // Akış: Guard.ValidateAndApply(options) → başarısızsa 400 ProblemDetails
    //     → readRepo.GetAll()  (IProductReadRepository doğrudan enjekte edilir; yalnızca iletme
    //       yapan bir handler katmanı davranış eklemez, bkz. ADR 0005)
    //     → DataSourceLoader.LoadAsync(query, options, ct) → Ok(result)
}
```

**Route/body Id kuralı — üç endpoint'in üçünde de geçerlidir.** `Update`, `Activate` ve `Deactivate`
hem route'ta `id` hem gövdede `Id` taşır. Route'taki `id` otoriterdir; gövdedeki `Id` ile
uyuşmuyorsa istek 400 ProblemDetails ile reddedilir. Sessizce birini tercih etmek yok.

### Tests to add

`ProductsApiTests.cs` (eklenir):

- `CreateProduct_WithValidPayload_ShouldReturn201WithLocationHeader`
- `CreateProduct_WithDuplicateCode_ShouldReturn409`
- `CreateProduct_WithInvalidPayload_ShouldReturn400WithProblemDetails`
- `CreateProduct_WithNonExistentUnitOfMeasure_ShouldReturn422`
- `CreateProduct_WithInactiveUnitOfMeasure_ShouldReturn422`
- `UpdateProduct_WithValidPayload_ShouldReturn200`
- `UpdateProduct_RouteIdMismatchesBodyId_ShouldReturn400`
- `UpdateProduct_WithNonExistentProduct_ShouldReturn404`
- `UpdateProduct_WithMissingRowVersion_ShouldReturn400`
- `ActivateProduct_WithExistingProduct_ShouldReturn200`
- `ActivateProduct_RouteIdMismatchesBodyId_ShouldReturn400`
- `DeactivateProduct_WithExistingProduct_ShouldReturn200`
- `DeactivateProduct_RouteIdMismatchesBodyId_ShouldReturn400`

`ProductsDatasourceTests.cs`:

- `GetDatasource_WithDefaultRequest_ShouldReturn200WithData`
- `GetDatasource_WithNoTake_ShouldDefaultTo20`
- `GetDatasource_WithTakeExceedingMax_ShouldReturn400`
- `GetDatasource_WithAllowedSortField_ShouldReturn200`
- `GetDatasource_WithDisallowedSortField_ShouldReturn400`
- `GetDatasource_WithDisallowedGroupField_ShouldReturn400`
- `GetDatasource_WithRequireGroupCount_ShouldReturn400`
- `GetDatasource_WithGroupSummary_ShouldReturn400`

### Validation

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Phase 3 — UnitOfMeasuresController, rate limiter, concurrency test, README fix, ADR stub

### Files

- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs` — created
- `src/Envanex.Web/Program.cs` — modified — rate limiter
- `src/Envanex.Web/appsettings.json` — modified — `RateLimiting` bölümü
- `README.md` — modified — stack tablosunda "Swagger" → "OpenAPI + Scalar"
  (`Program.cs` `AddOpenApi()` + `MapScalarApiReference()` kullanıyor; README bayat)
- `docs/adr/0006-rest-error-mapping.md` — created (başlık + ADR'de cevaplanacak soruları
  listeleyen HTML yorumu)
- `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs` — created
  (RateLimitedWebApplicationFactory yalnızca RateLimiterTests içinde oluşturulur ve o sınıfın
  IAsyncLifetime.DisposeAsync'inde dispose edilir. EnvanexWebApplicationFactory ile paylaşılmaz.)
- `tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs` — created
- `tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs` — created
- `tests/Envanex.IntegrationTests/Api/ConcurrencyApiTests.cs` — created

### Signatures

```csharp
[ApiController]
[Route("api/unit-of-measures")]
public sealed class UnitOfMeasuresController : ControllerBase
{
    [HttpPost]                                         // → 201 + Location
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct);

    [HttpGet("{id:guid}", Name = "GetUnitOfMeasureById")]  // → 200
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct);

    [HttpGet]                                          // → 200, düz liste
    public async Task<IActionResult> List(CancellationToken ct);
    // IUnitOfMeasureReadRepository.GetAllAsync — DataSourceLoader YOK, sertleştirme gerekmez:
    // sabit boyutlu bir lookup tablosu, istemci sorgu şekli belirlemiyor.
}
```

Rate limiter — ASP.NET Core yerleşik (`AddRateLimiter`), politika taksonomisi kurulmaz:

```
"RateLimiting": { "Enabled": true, "PermitLimit": 100, "WindowSeconds": 60 }
```

- Bir global politika + `/datasource` ve yazma endpoint'lerinde daha sıkı bir "mutations" politikası
- Limitler `appsettings`'ten okunur
- `EnvanexWebApplicationFactory` Testing ortamında rate limiting'i devre dışı bırakır. Aksi hâlde
  `ProductsApiTests` + `ProductsDatasourceTests` + `ConcurrencyApiTests` toplamı limiti aşar ve
  testler rastgele 429 almaya başlar — kendi ayağına sıkma.
- `RateLimitedWebApplicationFactory` limiti bilerek 2'ye çeker; rate limiter'ın gerçekten çalıştığını
  yalnızca bu factory doğrular.
- Not (PR 7'ye taşınacak): App Service'in arkasında IP bazlı bölümleme `ForwardedHeaders` olmadan
  yanlış çalışır. Bu PR'ın işi değil, kaydedildi.

### Tests to add

`UnitOfMeasuresApiTests.cs`:

- `CreateUnitOfMeasure_WithValidPayload_ShouldReturn201WithLocationHeader`
- `CreateUnitOfMeasure_WithDuplicateCode_ShouldReturn409`
- `CreateUnitOfMeasure_WithInvalidPayload_ShouldReturn400WithProblemDetails`
- `CreateUnitOfMeasure_WithNonExistentBaseUnit_ShouldReturn422`
- `CreateUnitOfMeasure_WithInactiveBaseUnit_ShouldReturn422`
- `GetUnitOfMeasureById_WithExistingUnit_ShouldReturn200`
- `GetUnitOfMeasureById_WithNonExistentId_ShouldReturn404`
- `ListUnitOfMeasures_ShouldReturnAllUnits`

`RateLimiterTests.cs` (`RateLimitedWebApplicationFactory`, limit = 2):

- `MutationEndpoint_ExceedingRateLimit_ShouldReturn429` — üç ardışık POST, üçüncüsü 429
- `MutationEndpoint_UnderRateLimit_ShouldReturn201` — tek POST, 201 döner. Bu vaka olmadan test,
  her isteği 429'a çeviren bozuk bir yapılandırmayı da geçerdi.

`ConcurrencyApiTests.cs` — PR 5'in en kritik testi:

- `UpdateProduct_WithStaleRowVersion_ThroughFullStack_ShouldReturn409` — POST ile ürün oluştur,
  GET ile `RowVersion` al (v1), aynı v1 ile bir PUT gönder (başarılı, satır v2 olur), ardından
  yine v1 ile ikinci bir PUT gönder → 409 beklenir.
- `ActivateProduct_WithStaleRowVersion_ShouldReturn409`

Bu testler, `SetOriginalRowVersion` çağrılmazsa veya `OriginalValue` üzerine yazılmazsa kırmızıya
döner. Concurrency token'ının dekoratif olmadığını kanıtlayan tek şey bunlardır.

### Validation

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

---

## Rollback notes

Migration yok, şema değişmiyor. Geri alma: controller'lar, `ResultExtensions`, `DataSourceGuard`,
model binder, rate limiter ve güvenlik başlıkları middleware'i kaldırılır; `Program.cs` önceki
hâline döner; `appsettings.json`'daki `RateLimiting` bölümü silinir. `docs/adr/0006` boş dosya.
PR 5b iptal edilirse PR 5a bağımsız geçerli kalır — Application katmanı ve repository'ler HTTP
yüzeyi olmadan çalışır.
