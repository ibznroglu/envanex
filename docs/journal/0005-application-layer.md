# PR 0005 — Application Layer

## Ne yaptik

Bu PR, Domain katmanindaki aggregate'leri kullanan use case altyapisini sifirdan kurdu. Onceki PR'larda Product, UnitOfMeasure ve Warehouse entity'leri vardi ama bunlari "olustur", "guncelle", "aktive et" gibi islemlere donusturecek bir katman yoktu. Simdi var: `ICommandHandler`/`IQueryHandler` arayuzleri, FluentValidation tabanli validator'lar, bu validator'lari handler'in onune koyan bir `ValidationDecorator`, repository arayuzleri (Application'da) ve implementasyonlari (Infrastructure'da), EF Core exception'larini Application tiplerine ceviren bir `UnitOfWork`. Domain'deki `Error` tipinin `sealed` olmaktan cikarilip `ValidationError` alt tipinin eklenmesi de bu PR'da yapildi. PR oncesi toplam 102 test vardi (90 Domain + 12 Integration), Application.Tests bostu. Simdi toplam 222 (112 Domain + 79 Application + 31 Integration). Mimari testlere iki yeni kural eklendi: Application'in EF Core paketine referans vermemesi ve Web projesinin sadece Application, Infrastructure ve SoapApi'ye referans vermesi.

## Yeni giren teknolojiler

### FluentValidation

- **Ne ise yarar:** Dogrulama kurallarini akici (fluent) bir API ile tanimlamani saglar: `RuleFor(x => x.Name).NotEmpty().MaximumLength(200)`. React dunyasindaki Zod veya Yup'a benzer ama derleme zamaninda tip guvenligi saglar. Her validator bir `AbstractValidator<T>` siniftan turetilir ve command nesnesi uzerinde kurallar tanimlar. Kosula bagli kurallar da yazabilirsin: `When(x => x.BaseUnitId is not null, () => ...)` gibi.
- **Bu projede nerede:** `src/Envanex.Application/Products/Validators/` ve `src/Envanex.Application/UnitOfMeasures/Validators/` altindaki siniflar. `Directory.Packages.props`'ta versiyon kaydi.
- **Alternatifi neydi:** Data Annotations (`[Required]`, `[MaxLength]`) kullanilabilirdi ama bunlar entity siniflarinin uzerine yazilir -- domain katmanini altyapi attribute'larina baglar. Ayrica kosula bagli validasyonlar (orn. "base unit degilse conversionFactor zorunlu") attribute'larla zordur. Bir diger alternatif MediatR'in `IPipelineBehavior`'u idi ama MediatR'a bagimlilik eklememek icin secilmedi.
- **Nerede okunur:** https://docs.fluentvalidation.net/

## Kavramlar

### Decorator Pattern

Bir nesneyi ayni arayuzu implement eden baska bir nesneyle sarmalama teknigi. `ValidationDecorator<TCommand, TResponse>` bir `ICommandHandler`'dir ama icinde gercek handler'i (`_inner`) tutar. Once validasyonu calistirir, hata yoksa `_inner.HandleAsync`'e devreder. React'teki Higher-Order Component (HOC) mantigi: `withAuth(MyComponent)` nasil sarar ve once yetki kontrolu yaparsa, decorator da oyle calisir. Fark: HOC composition JSX'te gizlidir, decorator DI kaydinda acikca gorunur (`src/Envanex.Application/DependencyInjection.cs` satir 37-60). MediatR'in pipeline behavior'larindan farki: decorator her handler icin ayri kaydedilir, hangi handler'in hangi decorator ile sarildigini DI dosyasinda gorebilirsin. MediatR'da pipeline behavior'lar tum handler'lara global uygulanir ve hangisinin hangisine uygulandigini kafadan takip etmen gerekir.

### CQRS-lite (Command Query Responsibility Segregation)

Yazma ve okuma islemlerini farkli arayuzlerle ayirma yaklasimi. Tam CQRS ayri veritabanlari ve event sourcing icerir. Bu projede hafif versiyonu var: `IProductRepository` (yazma -- entity doner) ve `IProductReadRepository` (okuma -- DTO doner). Ayni veritabani, ayni DbContext, ama sorumluluklar ayri. Yazma tarafi aggregate metodlarini cagirmak icin entity'ye ihtiyac duyar; okuma tarafi sadece gosterim amacli veri ister ve entity'nin is kurallarindan gecirmesine gerek yok. Bu ayrim handler'lara da yansir: `ICommandHandler` yan etki uretir ve `Result<TResponse>` doner; `IQueryHandler` yan etki uretmez, sadece okur.

### IUnitOfWork (Birim Is Kalibi)

Birden fazla repository islemini tek transaction icinde gruplama mekanizmasi. `SaveChangesAsync` cagrilana kadar hicbir sey veritabanina yazilmaz -- EF Core tum degisiklikleri change tracker'da biriktirir. Handler, repository'ye `AddAsync` der (bellege yazar), en sonda `IUnitOfWork.SaveChangesAsync` der (veritabanina yazar). SaveChanges'i repository icine koymama sebebi: bir handler birden fazla repository kullanabilir ve hepsinin ayni transaction'da olmasini ister. Birinde hata olursa hepsi geri alinsin. Arayuz Application'da (`src/Envanex.Application/Abstractions/Persistence/IUnitOfWork.cs`), implementasyon Infrastructure'da (`src/Envanex.Infrastructure/Persistence/UnitOfWork.cs`). Frontend'den bakarsan: birden fazla API cagrisini tek bir `Promise.all` ile yapmak gibi dusun, ama burada veritabani garanti veriyor -- ya hepsi basarili ya hicbiri.

### ValidationError : Error

Onceki PR'larda `Error` tipi `sealed record` idi. Tek bir hata kodu ve mesaj tasiyordu. Ama bir form gonderiminde birden fazla alan ayni anda hatali olabilir: isim bos, fiyat negatif, para birimi gecersiz. Kullaniciya "bir dogrulama hatasi olustu" demek yetersiz -- hangi alanin hangi hatasi oldugunu gostermen gerekiyor. `ValidationError`, `Error`'dan turetilmis ve icinde `IReadOnlyList<ValidationFailure>` tasiyor -- her `ValidationFailure` bir `PropertyName` ve `ErrorCode` iceriyor. `Error`'un `sealed` olmaktan cikarilmasi bu kalitimi mumkun kildi. `Result.Failure<T>(new ValidationError(...))` ifadesi calisir cunku `ValidationError` bir `Error`'dur -- `Result<T>` hala `Error` tipini bekler. TypeScript'ten geliyorsan: bir discriminated union'a yeni bir varyant eklemek gibi dusun, ama C#'ta bunu kalitimla yapiyorsun (`src/Envanex.Domain/Common/ValidationError.cs`). `ValidationError` Domain'de tanimlandi cunku `Result<T>` de Domain'de -- Application'da tanimlasan Result onu bilemezdi.

### IQueryable vs IReadOnlyList (okuma arayuzlerinde)

`IProductReadRepository.GetAll()` metodu `IQueryable<ProductListDto>` doner. `IUnitOfMeasureReadRepository.GetAllAsync()` ise `Task<IReadOnlyList<UnitOfMeasureListDto>>` doner. Fark: Product'in ileride bir datasource endpoint'i olacak -- grid bileseni filtreleme, siralama ve sayfalama parametrelerini gonderecek ve `DevExtreme.AspNet.Data` kutuphanesi `IQueryable` uzerinde calisarak bu parametreleri SQL'e cevirecek. `IReadOnlyList` olsa tum veriyi bellege yuklemen gerekirdi -- 10.000 urunle bu felaket olur. UnitOfMeasure az sayida kayit (dropdown icin) oldugu icin duz liste yeterli. Trade-off: `IQueryable`'i Application'a tasimak soyutlamayi zayiflatir -- "IQueryable'i kim materialize ediyor" sorusu Application sinirinin disina cikiyor. Ama tum veriyi bellege yuklemenin performans maliyeti bu soyutlama kaybindan daha kotu.

### Fake (Test Double)

Mock kutuphanesi (Moq, NSubstitute) kullanmadan elle yazilmis test nesnesi. `FakeProductRepository` gercek arayuzu implement eder ama veritabani yerine bellekteki `List<Product>` kullanir. Mock'tan farki: davranis test bazinda degil, genel amacli yazilir ve refactoring'e daha dayaniklidir. Mock'ta arayuz degisince her test konfigurasyonunu guncellemelisin; fake'te tek sinifi guncellersen tum testler calisir. Bu projede mock kutuphanesi yok -- `tests/Envanex.Application.Tests/Fakes/` altinda bes fake sinif var. `FakeUnitOfWork` ozellikle ilginc: icinde `RowVersionWasSetBeforeSave` gibi test-icin-assertion mekanizmalari barindirir.

### InternalsVisibleTo

C# derleyici direktifi. `internal` erisim duzenleyicisine sahip tiplerin baska bir assembly'den gorunmesini saglar. `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` icinde `<InternalsVisibleTo Include="Envanex.IntegrationTests" />` var -- boylece `internal sealed class ProductRepository` gibi siniflar integration testlerinden dogrudan erisilebilir hale geliyor. Bunun trade-off'u: test projesi Infrastructure'in ic detaylarini bilir ve soyutlama delinir. Alternatif, testlerin sadece Application arayuzleri uzerinden calismasi olurdu ama o zaman repository implementasyonunun dogru calismasi dogrudan test edilemezdi.

### WithErrorCode (FluentValidation)

Validator kurallarina hata kodu atama mekanizmasi. `.WithMessage()` yerine `.WithErrorCode("Product.CodeRequired")` kullaniliyor. Neden: domain error mesajlari Ingilizce, kullaniciya gosterilecek mesajlar Turkce. Mesaj uretimi Web katmanina birakildi; validator sadece kod uretir. FluentValidation'in varsayilan mesajlari ("must not be empty" gibi) boylece hicbir yerde gorulmez -- her hata kodu `ProductErrors` veya `UnitOfMeasureErrors` siniflarindaki sabitlerle eslesir. Web katmaninda `result.Error is ValidationError ve` pattern match ile coklu hatalari ayristirip her alana kendi mesajini gosterebilirsin.

### Exception Ceviri (Exception Translation)

Infrastructure katmaninda EF Core exception'larini Application'in bildigi tiplere donusturme mekanizmasi. `UnitOfWork.SaveChangesAsync` icinde `DbUpdateConcurrencyException` -> `ConcurrencyConflictException`, unique violation (SqlException 2601/2627) -> `DuplicateKeyException` cevirisi yapilir. Bu sayede handler `catch (DuplicateKeyException)` yazabilir -- EF Core namespace'ini bilmesine gerek kalmaz. `ConcurrencyConflictException` ve `DuplicateKeyException` Application katmaninda tanimli (`src/Envanex.Application/Abstractions/Persistence/`). FK violation (547) bilerek cevrilmez -- farkli bir hata turu, farkli handle edilmeli.

### Scoped vs Singleton Lifetime

DI container'da bir servisin ne kadar yasayacagini belirleyen ayar. `Scoped` servisler HTTP istegi basina bir kere olusturulur ve istek bitince yok edilir -- `DbContext` ve repository'ler bu kategoride. `Singleton` tum uygulama boyunca tek bir instance olarak yasarr -- validator'lar bu kategoride cunku durumsuzdurlar (stateless), her cagri ayni kurallari uygular. Tehlike: bir singleton servis icine scoped bir bagimlilik inject edilirse, scoped olan nesne hic yok edilmez ve eski veriyle calisir (captive dependency problemi).

## Komutlar ve ne yaptiklari

### `dotnet build -warnaserror`
Tum solution'i derler. `-warnaserror` bayragi her uyariyi hata olarak degerlendirir. Bu PR'da yeni eklenen 50+ dosyanin mevcut kodla uyumunu dogrular.

### `dotnet test`
Tum test projelerini calistirir. PR sonrasinda 222 test var: Domain.Tests, Application.Tests (132 yeni) ve IntegrationTests.

### `dotnet test --filter "FullyQualifiedName~Application.Tests"`
Sadece Application.Tests iceren testleri calistirir. `~` operatoru "contains" demek. Faz bazli calisirken tum suite yerine ilgili alt kumeyi calistirmak icin kullanildi.

### `dotnet format --verify-no-changes`
Kodun `.editorconfig` kurallarina uygun olup olmadigini kontrol eder. Dosyalari degistirmez; uygunsuzluk varsa sifirdan farkli cikis kodu doner.

## Dikkat edilen tuzaklar

### SetOriginalRowVersion sira tuzagi

Shadow property kavrami 0003'te anlatildi. Bu PR'daki kritik tuzak: handler entity'yi `GetByIdAsync` ile yukleyince EF Core, change tracker'a guncel RowVersion'i "OriginalValue" olarak yazar. `UPDATE ... WHERE RowVersion = @OriginalValue` her zaman eslesir cunku OriginalValue zaten veritabaninin son degeri -- baska biri arada satiri degistirmis olsa bile. Concurrency control sessizce devre disi kalir. Cozum: istemciden gelen eski RowVersion'i `SetOriginalRowVersion` ile OriginalValue'nun uzerine yazmak -- ve bunu `SaveChangesAsync`'ten ONCE yapmak. `FakeUnitOfWork.RowVersionWasSetBeforeSave` property'si bu sirayi dogrulayan test mekanizmasi. Integration testlerde ise `SetOriginalRowVersion` bilerek CAGIRILMADAN save yapilarak sessiz basarinin kaniti gosteriliyor.

### Application'a EF Core paketi eklenmemesi

`IQueryable` kullanmak icin `Microsoft.EntityFrameworkCore` paketini eklemek cazip -- `ToListAsync` extension metodu orada. Ama bu mimari siniri cigner. `Application_ShouldNotReference_EntityFrameworkPackages` mimari testi `Envanex.Application.csproj` dosyasini okuyarak bu kurali korur. `IQueryable` zaten `System.Linq`'te, EF Core'a gerek yok.

### DuplicateKeyException iki katmanli savunma

Handler once `ExistsByCodeAsync` ile kontrol eder (hizli cevap), sonra `catch (DuplicateKeyException)` ile veritabani kisitlamasini yakalar (race condition korumasi). Sadece on kontrol yetmez cunku iki istek ayni anda ayni kodu kontrol edip ikisi de "yok" alabilir. Sadece veritabani hatasini yakalamak da yetmez cunku kullaniciya gecikmeli ve teknik bir mesaj gosterirsin. Iki katman birlikte: normal durumda hizli ve anlamli cevap, race condition'da da veri butunlugu korunuyor.

### Validator'larda domain sabiti kullanmak

`.MaximumLength(50)` yerine `.MaximumLength(Product.CodeMaxLength)` yazmak. Sabit degisirse validator otomatik guncellenir. Hardcoded sayi kullansan, sabit degistiginde validator yanlis uzunlugu kontrol eder ve bu tutarsizlik test olmadan fark edilmez. Boundary testleri (`CodeMaxLength + 1` uzunlugunda string) bu uyumsuzlugu korur.

### DI kayit testleri

Elle DI kaydi yaptiginda bir handler veya decorator'u unutmak kolay. `DependencyInjectionTests.AllRegisteredServices_ShouldResolve` her servisi container'dan cozumlemeye calisir; eksik kayit `InvalidOperationException` firlatir. `CommandHandlers_ShouldBeWrappedWithValidationDecorator` ise resolve edilen nesnenin `ValidationDecorator` tipinde oldugunu dogrular -- decorator olmadan kaydedilmis handler validasyonu sessizce atlar. Bu iki test, MediatR kullanmaminin bedelini (elle kayit) guvenli hale getiren mekanizma.

### Query handler'lara decorator uygulanmamasi

`DependencyInjection.cs`'te query handler'lar dogrudan kayitli -- `ValidationDecorator` yok. Query'ler yan etki yaratmaz, dolayisiyla validasyon katmaninin maliyeti gereksiz. Bir `GetProductByIdQuery` sadece bir `Guid` tasir -- bu deger zaten handler icinde null kontrolunden gecer. MediatR'da bunu saglamak icin pipeline behavior'da tip kontrolu yapman veya ayri pipeline'lar tanimlaman gerekirdi; burada DI kaydinda decorator'u koymamak yeterli.

## Farkli dusundugum yer

Validator'lardaki `.WithErrorCode()` stratejisi dogru ama `.WithMessage()` kullanmamak, FluentValidation'in varsayilan hata mesajlarinin tamamen atilmasi anlamina geliyor. Gelistirici bir test basarisizligi gordugunde `"Product.CodeRequired"` hata kodunu elle aramak zorunda -- `"'Code' must not be empty."` mesaji daha hizli anlasilir. REST endpoint'leri (PR 5b) ve TurkishErrorMessages tablosuna kadar developer experience biraz zorlasiyor.

Ayrica handler sayisi 5'ten 20-30'a ciktiginda `DependencyInjection.cs` dosyasi cok buyuyecek. Scrutor gibi bir assembly-tarama kutuphanesi bu tekrari ortadan kaldirir ama acik kayit gorunurlugunun kaybedilmesi trade-off. Su an bes handler icin explicit kayit makul, ama bu kararin yeniden degerlendirilmesi gereken bir esik var.

## Kendini sina

1. `IProductReadRepository.GetAll()` neden `IQueryable<ProductListDto>` donuyor da `Task<IReadOnlyList<ProductListDto>>` degil? `IUnitOfMeasureReadRepository` icin neden tersi gecerli?

2. `ValidationDecorator`'in `HandleAsync` metodunda validator listesi bossa neden dogrudan `_inner.HandleAsync`'e devrediliyor? Bu kontrol olmasa performansta ne degisirdi?

3. `UnitOfWork.IsUniqueViolation` metodunda neden `SqlException { Number: 2601 or 2627 }` kontrol ediliyor -- tek biri yetmez miydi? Bu iki numara arasindaki fark ne?

4. `DependencyInjection.cs`'te validator'lar `Singleton`, handler'lar `Scoped`. Bir validator `IProductRepository`'ye bagimli olsa (ornegin "kod veritabaninda var mi" kontrolu), bu lifetime farki ne sorun yaratir?

5. `FakeUnitOfWork`'teki `RowVersionWasSetBeforeSave` neden `bool?` tipinde? `null`, `true` ve `false` degerlerinin her birinin anlami ne?

6. Handler icinde `ExistsByCodeAsync` kontrolunu tamamen kaldirip sadece `catch (DuplicateKeyException)`'a dayansak ne degisir? Kullanici deneyimi nasil etkilenir?

7. `Error` tipinden `sealed` kaldirildi ve `ValidationError` turetildi. Yarin birisi `NotFoundError : Error` diye baska bir alt tip eklese, `Result<T>` mekanizmasi kirilir mi? Bu esnekligin riski ne?

8. `ProductReadRepository.GetAll()` icinde `EF.Property<byte[]>(p, ColumnNames.RowVersion)` kullaniliyor ama `Product` sinifinda `RowVersion` property'si yok. Birisi `Product`'a `public byte[] RowVersion { get; private set; }` eklese shadow property mekanizmasi ne olur?
