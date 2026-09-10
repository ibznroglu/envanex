# PR 0006 — REST Endpoints

## Ne yaptik

Bu PR, onceki PR'da insa edilen Application katmanini HTTP uzerinden erisime acti. Product (create, update, get by id, activate, deactivate, datasource) ve UnitOfMeasure (create, get by id, list) icin REST controller'lar eklendi. `Result<T>` donus tiplerini RFC 9457 uyumlu ProblemDetails yanitlarina ceviren bir `ResultExtensions` katmani ve hata kodlarini Turkce kullanici mesajlarina esleyen `TurkishErrorMessages` tablosu olusturuldu. Grid endpoint'i icin `DataSourceGuard` ile sertlestirilmis datasource korumasi, fixed-window rate limiter ve guvenlik basliklari middleware'i eklendi. PR sonunda 8 yeni kaynak dosya, 2 yeni controller ve toplam 70+ yeni entegrasyon testi var.

## Yeni giren teknolojiler

### ProblemDetails (RFC 9457)

- **Ne ise yarar:** HTTP API'lerinden hata dondurmek icin standartlastirilmis bir JSON formati. `status`, `title`, `detail`, `type` alanlari tasir. Boylece her API kendi hata formatini icat etmek yerine, istemciler her yerden ayni yapiyi bekleyebilir. ASP.NET Core'un `ProblemDetails` ve `ValidationProblemDetails` siniflari bunu dogrudan destekler.
- **Bu projede nerede:** `src/Envanex.Web/Extensions/ResultExtensions.cs` -- tum hata yanitlari bu sinif uzerinden ProblemDetails formatinda uretiliyor. Rate limiter'in 429 yaniti da ayni formati elle kullaniyor (`Program.cs`).
- **Alternatifi neydi:** Kendi hata JSON formatimizi tanimlamak (ornegin `{ "error": "...", "code": "..." }`). Calisir ama her istemci icin ayri dokumantasyon gerektirir ve standart kutuphaneler otomatik parse edemez.
- **Nerede okunur:** https://www.rfc-editor.org/rfc/rfc9457

### FrozenDictionary / FrozenSet

- **Ne ise yarar:** .NET 8+ ile gelen, olusturulduktan sonra degistirilemez ve okuma erisimi icin optimize edilmis koleksiyon tipleri. Normal `Dictionary`'den farki: build-time'da ic yapiyi optimize eder, sonrasinda her okuma daha hizli olur. Surekli okunan ama hic degismeyen arama tablolari icin ideal.
- **Bu projede nerede:** `ResultExtensions.cs`'deki `StatusCodeMap` (`FrozenDictionary<string, int>`), `TurkishErrorMessages.cs`'deki `Messages` (`FrozenDictionary<string, string>`), `DataSourceGuard.cs`'deki `_allowedFields` ve `_allowedGroupFields` (`FrozenSet<string>`).
- **Alternatifi neydi:** Normal `Dictionary` veya `HashSet`. Fonksiyonel olarak ayni is gorur ama uygulama boyunca degismeyecek bir tabloda `FrozenDictionary` niyet belirtir: "bu koleksiyon sabittir, degistirmeye calisma."
- **Nerede okunur:** https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen

### FixedWindowRateLimiter

- **Ne ise yarar:** Sabit zaman penceresi icinde izin verilen istek sayisini sinirlar. Ornegin "60 saniyede 100 istek" -- pencere dolunca yeni istekler 429 ile reddedilir, pencere sifirlaninca sayac sifirlanir. ASP.NET Core'un `AddRateLimiter` middleware'i bunu yerlesik olarak saglar.
- **Bu projede nerede:** `src/Envanex.Web/Program.cs` -- `PartitionedRateLimiter.Create` ile global bir limiter tanimlaniyor. Ayarlar `appsettings.json`'daki `RateLimiting` bolumunden okunuyor.
- **Alternatifi neydi:** Sliding window (pencereleri kaydirir, daha yumusak gecis saglar ama karmasik), token bucket (firtina trafigine daha toleransli), veya reverse proxy'de (nginx, Azure Front Door) rate limiting. Bu PR'da basit bir baslangic noktasi olarak fixed window secildi.
- **Nerede okunur:** https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit

### IModelBinder / IModelBinderProvider

- **Ne ise yarar:** ASP.NET Core MVC'de gelen HTTP isteginin parametrelerini C# nesnelerine ceviren mekanizma. Varsayilan model binder sorgu parametrelerini property isimlerine gore esler. Custom model binder, DevExtreme'in ozel sorgu formatini `DataSourceLoadOptionsBase`'e donusturmek icin yazildi.
- **Bu projede nerede:** `src/Envanex.Web/DataSource/DataSourceLoadOptionsModelBinder.cs` ve `DataSourceLoadOptionsModelBinderProvider.cs`. Provider, `Program.cs`'te controller options'a kaydediliyor.
- **Alternatifi neydi:** `[FromQuery]` attribute'u ile otomatik binding. Ama DevExtreme'in sorgu formati (ozellikle `sort` ve `filter` parametreleri JSON dizileri olarak gelir) varsayilan binder tarafindan parse edilemez. `DataSourceLoadOptionsParser` kutuphanenin kendi parser'idir ve custom binder onu sariyor.
- **Nerede okunur:** https://learn.microsoft.com/en-us/aspnet/core/mvc/advanced/custom-model-binding

## Kavramlar

### Error Code -> HTTP Status Mapping (Kod Tablosu)

Domain katmanindaki hata kodlari (`Product.NotFound`, `Product.DuplicateCode`) HTTP semantigi bilmez -- bunlar saf is alani hatalaridir. Web katmani her hata kodunu bir HTTP durum koduna esleyen acik bir tablo tutar (`ResultExtensions.StatusCodeMap`). Bu tablo sonek eslesmesi (suffix matching) KULLANMAZ: `Product.UnitOfMeasureNotFound` 422'ye eslenirken `Product.NotFound` 404'e eslenir. Sonekle eslesen bir sistem bu ikisini karistirirdi. Tabloda olmayan bir hata kodu 400'e gider (500 degil), cunku `Result.Failure` bir is hatasidir, sunucu arizasi degildir.

### 422 Unprocessable Entity vs 400 Bad Request

400, istek soz dizimi veya formati bozuk oldugunda kullanilir: bos alan, negatif sayi, format hatasi. 422, istek formati dogru ama is kurali ihlali varsa kullanilir. Ornegin `Product.UnitOfMeasureNotFound` demek "gonderdigin ID gecerli bir GUID ama veritabaninda boyle bir olcu birimi yok" -- soz dizimi dogru, anlam yanlis. Bu ayrim istemcinin hangi tur duzeltme yapmasi gerektigini belirtir.

### DataSourceGuard

DevExtreme grid bileseni sort, filter, group ve sayfalama parametrelerini sunucuya gonderir. `DataSourceGuard` bu parametreleri bir allowlist'e karsi dogrular. Ihlalde sessizce duzeltmez (clamp), 400 doner. Sessiz clamp tehlikeli cunku istemciye isteginin karsilandigini soyler ama aslinda farkli bir sorgu calistirir. Filter dogrulamasi rekursiftir: DevExtreme filter'lari ic ice `[["Code","=","X"],"and",["Name","=","Y"]]` seklinde olabilir ve her seviyedeki alan adi kontrol edilir.

### Positional Record vs Sealed Class (DTO Projeksiyonu)

EF Core'un LINQ-to-SQL cevirici, `new ProductListDto(p.Id, p.Code, ...)` seklindeki constructor-based projeksiyon (positional record) ustune DevExtreme'in `DataSourceLoader`'inin eklediyi `OrderBy` ifadesini SQL'e ceviremiyor. Sorun, EF Core'un `NewExpression` ustune member access'i desteklememesi. Sonuc: tum tablo bellege cekilir (client-side evaluation). Cozum: positional record yerine `required init` property'li sealed class kullanmak. Ayni sekilde `EF.Property<byte[]>(p, "RowVersion")` shadow property erisimi de bu kompozisyonda calismiyor, bu yuzden `ProductListDto` RowVersion icermiyor.

### UseStatusCodePagesWithReExecute ve Bos Govde Sorunu

ASP.NET Core'un `UseStatusCodePagesWithReExecute` middleware'i, govdesi bos olan 4xx yanitlari yakalatip baska bir sayfaya yonlendirir (bu projede `/not-found`). Rate limiter varsayilan olarak bos govdeyle 429 doner -- middleware bunu yakalar ve HTML sayfasi dondurur. API istemcisi JSON beklerken HTML alir. Cozum: `OnRejected` callback'inde ProblemDetails JSON govdesi yazarak middleware'in araya girmesini onlemek.

### WebApplicationFactory Yasam Dongusu

`WebApplicationFactory<Program>` entegrasyon testlerinde ASP.NET Core uygulamasini process-ici olarak baslatir. Bu projede `SqlServerFixture` icinde TEK SEFER olusturuluyor ve tum API test siniflari bu instance'i paylasiyor. Her test sinifi icin ayri factory baslatmak hem yavas hem de rate limiter gibi global state'i bozar. Istisnasi `RateLimitedWebApplicationFactory`: rate limiter testleri icin limiti bilerek 2'ye cekilen ayri bir factory -- bu factory kendi test sinifinda olusturulup dispose ediliyor.

## Komutlar ve ne yaptiklari

Bu PR'da yeni CLI komutu eklenmedi. Mevcut dogrulama komutlari (`dotnet build -warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`) her fazin sonunda calistirildi.

## Dikkat edilen tuzaklar

### Sonek eslesmesinin belirsizligi

`Product.UnitOfMeasureNotFound` ve `Product.NotFound` farkli HTTP kodlarina eslenmelidir (422 vs 404). Sonek tabanlı esleme (`*.NotFound` -> 404) bunu karistirirdi. Tam string eslesmesi bu riski ortadan kaldirir ama her yeni hata kodu icin tabloya bir satir eklenmesini gerektirir. `ResultMappingTests` bunu korur: Domain'e yeni bir `Error` alani eklenir ve tabloda karsiligi yoksa test kirilir.

### ValidationFailure fallback'inin ham hata kodu gostermesi

`ValidationFailure` sadece `PropertyName` ve `ErrorCode` tasir, `Message` alani yoktur. `TurkishErrorMessages` tablosunda karsiligi olmayan bir hata kodu kullaniciya ham kod olarak gosterilirdi (ornegin `"Product.CodeRequired"`). Cozum: `GenericValidationFallback` sabiti (`"Bu alan gecersiz."`) -- cevirisi olmayan kodlar icin genel bir Turkce mesaj. `ResultExtensionsTests.ValidationFailure_WithUnmappedErrorCode_ShouldUseGenericTurkishFallback` testi bunu dogruluyor.

### DataSource'un tum tabloyu bellege cekmesi

`ProductListDto` positional record olarak tanimlansa veya icerisinde `EF.Property` shadow property erisimi olsa, `DataSourceLoader.LoadAsync` sorgusu EF Core tarafindan SQL'e cevrilemez ve client-side evaluation'a duser -- tum tablo bellege yuklenir. `ArchitectureTests.DataSourceListDtos_ShouldNotBePositionalRecords` mimari testi tum `*ListDto.cs` dosyalarini tarayarak positional record kullanilmadigini garanti eder.

### Negatif Take/Skip ve sinirsiz offset

DevExtreme istemcisi `take=-1` veya `skip=-1` gondermez ama bir saldirgan gonderebilir. Negatif take/skip bazi LINQ provider'larda beklenmedik davranislara neden olabilir. `DataSourceGuard` bunlari acikca reddeder. `maxSkip` limiti (varsayilan 10.000) asiri OFFSET sorgularini onler -- buyuk OFFSET SQL Server'da pahali bir table scan'a donusur.

### Bozuk sorgu dizesinde 500

`DataSourceLoadOptionsParser.Parse` bozuk bir filter JSON'u aldiginda exception firlatir. Model binder bunu try/catch ile sarar ve `ModelBindingResult.Failed()` doner. `[ApiController]` attribute'u basarisiz model binding'i otomatik olarak 400 ProblemDetails'a cevirir. Bu sarma olmasaydi exception middleware'e kadar cikar ve 500 donurdu.

### 429'un bos govdeyle StatusCodePages'e yakalanmasi

Rate limiter varsayilan davranisla bos govdeli 429 doner. `UseStatusCodePagesWithReExecute("/not-found")` govdesi bos olan 4xx yanitlari yakalar ve HTML sayfasina yonlendirir. API istemcisi ProblemDetails JSON beklerken HTML alir. `OnRejected` callback ile ProblemDetails govdesi yazarak bu yakalanmayi onledik. Test, yanitin HTML icermedigini acikca dogruluyor: `body.ShouldNotContain("<html")`.

## Farkli dusundugum yer

`Retry-After` basligi icin kod yazildi (`OnRejected` callback'inde `context.Lease.TryGetMetadata(MetadataName.RetryAfter, ...)`) ama `FixedWindowRateLimiter` bu metadata'yi saglamiyor -- yani baslik hicbir zaman eklenmeyecek. Test de bunu dogrulamamis. Bu ya kaldirilmali ya da sabit bir degerle (ornegin pencere suresi) yazilmali. Simdiki haliyle olu koddur.

## Kendini sina

1. `ResultExtensions.StatusCodeMap`'te tabloda olmayan bir hata kodu icin neden 500 degil de 400 donuluyor? 500 dondursek ne kotu olurdu?

2. `ProductListDto` neden positional record degil de `required init` property'li sealed class? `record ProductListDto(Guid Id, string Code, ...)` yazsaydin ne olurdu?

3. `DataSourceGuard` ihlallerde neden sessizce duzeltme (clamp) yapmak yerine 400 donuyor? Clamp yapsaydi istemci acisindan ne degisirdi?

4. `EnvanexWebApplicationFactory` rate limiting'i neden devre disi birakiyor? Birakmasaydi hangi testler rastgele basarisiz olurdu ve neden?

5. `TurkishErrorMessages` tablosunda `Product.UnitOfMeasureNotFound` ile `Product.NotFound` farkli mesajlara sahip. Bunlarin ikisi de 404'e eslense ne yanlisligi olusurdu?

6. `ResultMappingTests.ResultMapping_NoMappingEntry_ShouldBeOrphaned` testi ne kontrol ediyor? Bu test olmasaydi hangi bakım hatasi sessizce birikirdi?

7. Model binder'daki try/catch blogu kaldirilsa ve `DataSourceLoadOptionsParser.Parse` exception firlatsa, `[ApiController]` attribute'u bu hatanin 500 donmesini onler miydi? Neden?

8. `RateLimiterTests` neden `EnvanexWebApplicationFactory` yerine ayri bir `RateLimitedWebApplicationFactory` kullaniyor? Ayni factory'yi rate limiting acik olarak kullansaydi test suitinde ne olurdu?
