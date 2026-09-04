# PR 0004 — Master Data Aggregates

## Ne yaptik

Bu PR ile projenin ilk gercek domain aggregate'lerini olusturduk: `UnitOfMeasure`, `Warehouse` ve `Product`. Bunlar bir ERP sisteminin en temel master data'sidir -- urun tanimlamadan, stok hareketi kaydetmeden, fatura kesmeden once bu kayitlarin var olmasi gerekir. Her uc aggregate kendi invariant'larini korur: code'lar normalize edilir (trim + upper invariant), bos deger kabul edilmez, iliskili ID'ler `Guid.Empty` olamaz. Product, onceki PR'da olusturulan `Money` ve `Quantity` value object'lerini tasir ve bu sayede complex type eslesmesi gercek production kodunda ilk kez calisir.

Infrastructure katmaninda uc `IEntityTypeConfiguration` dosyasi eklendi, `EnvanexDbContext`'e uc `DbSet` property'si yazildi ve `dotnet ef migrations add InitialSchema` ile projenin ilk migration'i uretildi. SQL Server'da uc tablo (`UnitOfMeasures`, `Warehouses`, `Products`) olusturuluyor; her tabloda unique index, FK constraint ve `RowVersion` concurrency token var.

Integration testler onceki PR'daki gecici `TestProduct` altyapisindan gercek aggregate'lere tasindi. `SqlServerFixture` artik `EnvanexDbContext` kullaniyor ve `EnsureCreatedAsync` yerine `MigrateAsync` cagiriyor -- boylece migration dosyasinin gercekten uygulanabilir oldugu her test kosusunda dogrulaniyor. Uc yeni integration test eklendi: unique index violation, self-referencing FK ve migration zinciri butunlugu. Gecici `TestProduct`, `TestDbContext` ve `TestProductConfiguration` dosyalari silindi.

## Yeni giren teknolojiler

### IEntityTypeConfiguration<T>
- **Ne ise yarar:** EF Core'da bir entity'nin veritabanindaki tabloya nasil eslenecegini tanimlayan bir arayuz. Kolon tipleri, uzunluklari, precision'lari, index'ler, foreign key'ler ve tablo adi gibi ayarlarin hepsi burada yazilir. Bu konfigurasyonu `OnModelCreating` icinde inline yazmak yerine ayri bir sinifa cikarmak, her entity'nin esleme kurallarini izole ve test edilebilir tutar.
- **Bu projede nerede:** `src/Envanex.Infrastructure/Persistence/Configurations/` altinda uc dosya: `UnitOfMeasureConfiguration.cs`, `WarehouseConfiguration.cs`, `ProductConfiguration.cs`.
- **Alternatifi neydi:** Data annotation'lar (entity sinifinin uzerine `[MaxLength(200)]` gibi attribute'lar yazmak). Onu secmedik cunku domain entity'leri infrastructure teknolojisinden habersiz olmali -- `Domain` projesi EF Core'a referans vermiyor ve vermemeli. Fluent API + ayri konfigurayon dosyasi, esleme bilgisini infrastructure'da tutar.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration

### ComplexProperty (EF Core Complex Types)
- **Ne ise yarar:** EF Core 8+ ile gelen bir ozellik. Bir value object'i kendi tablosu olmadan, sahibi olan entity'nin tablosuna yatay olarak gomme imkani verir. `Money` gibi bir nesneyi `ListPrice_Amount` ve `ListPrice_Currency` seklinde iki kolon olarak Products tablosuna yazdirir. Owned type'lardan farki: complex type'larin kimlik (identity) kavrami yoktur, null olamazlar ve EF Core onlari farkli bir change-tracking mekanizmasiyla izler.
- **Bu projede nerede:** `ProductConfiguration.cs` satir 26: `builder.ComplexProperty(p => p.ListPrice)`. Precision (`decimal(18,4)`) ve currency uzunlugu (`nvarchar(3)`) convention'lar tarafindan otomatik uygulanir, ama `ComplexProperty` cagrisinin kendisi explicit olmak zorunda.
- **Alternatifi neydi:** Owned type (`OwnsOne`). Complex type'i sectik cunku Money asla null olmamali ve kendi tablosuna ayrilmayi hak etmiyor -- iki property'lik bir deger. ADR 0003 bu karari dokumante ediyor.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/modeling/complex-types

### Self-referencing Foreign Key
- **Ne ise yarar:** Bir tablonun kendi primary key'ine FK veren bir kolona sahip olmasi. `UnitOfMeasures` tablosunda `BaseUnitId` kolonu yine `UnitOfMeasures.Id`'ye isaret eder. Boylece "Gram'in base unit'i Kilogram'dir" gibi hiyerarsik iliskiler ayni tablo icinde modellenebilir.
- **Bu projede nerede:** `UnitOfMeasureConfiguration.cs` satir 21-25. `HasOne<UnitOfMeasure>().WithMany().HasForeignKey(u => u.BaseUnitId)` -- dikkat: navigation property olmadan generic type ile FK tanimlaniyor.
- **Alternatifi neydi:** Ayri bir `UnitConversion` tablosu. Onu secmedik cunku bir birim ya base'dir ya da tam bir tane parent'i vardir -- bu one-to-many degil, basit bir parent pointer. Ayri tablo gereksiz karmasiklik olurdu.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many#without-navigation-to-principal

### Database.MigrateAsync()
- **Ne ise yarar:** EF Core'un migration pipeline'ini calistirarak veritabanini `Migrations/` klasorundeki dosyalara gore olusturur veya gunceller. Bekleyen (pending) migration'lari sirasiyla uygular. `EnsureCreatedAsync`'den temel farki: `EnsureCreated` modelden dogrudan DDL uretir ve migration dosyalarini tamamen atlar -- bu yuzden migration'daki bir hata (yanlis kolon, eksik index) hicbir zaman test edilmis olmaz.
- **Bu projede nerede:** `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` satir 39. Testcontainers her testte temiz bir SQL Server baslatir, `MigrateAsync` sifirdan tum migration zincirini uygular.
- **Alternatifi neydi:** `EnsureCreatedAsync` -- onceki PR'da kullaniliyordu. Artik gercek migration dosyasi oldugu icin onu test etmek istiyoruz. `EnsureCreated` migration pipeline'ini bypass ettigi icin, migration dosyasindaki bir bug ancak production'da veya elle `dotnet ef database update` calistirinca ortaya cikardi.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime

## Kavramlar

### Aggregate Root
Domain-Driven Design'dan gelen bir kavram. Bir aggregate, birbiriyle tutarli olmasi gereken entity'ler ve value object'ler grubudur; aggregate root, bu grubun tek giris noktasidir. Disaridan aggregate'in ic elemanlarini dogrudan degistirmezsin -- her sey root uzerindeki metodlarla olur. Bu projede `UnitOfMeasure`, `Warehouse` ve `Product` birer aggregate root. Hepsi `AggregateRoot<Guid>` base class'indan turetilmis, tum setter'lar `private set`, olusturma `Create` factory metodu uzerinden yapilir.

### Factory Method (static Create)
Constructor yerine `public static Result<T> Create(...)` kullaniyoruz. Neden: bir constructor basarisiz olamaz (exception disinda) ama bir is kurali ihlalinde exception firlatmak istemiyoruz -- exception'lar bug ve altyapi hatalari icindir. Factory method, validasyon basarisizsa `Result.Failure` donebilir; boylece cagiran kod explicit olarak basariyi veya hatayi handle etmek zorunda kalir. Constructor `private` oldugu icin kimse bu korumaya ragmen invalid bir nesne olusturamaz.

### null guard vs Result.Failure ayrimi
Bu PR'da CLAUDE.md'ye de eklenen onemli bir kural: `null` referans gecmek bir "caller bug"dur, is kurali ihlali degildir. `Product.Create` icinde `listPrice` null gelirse `ArgumentNullException.ThrowIfNull` ile patlatiriz -- cunku Money tipinde bir parametrenin null olmasi, cagiran kodun hatali olmasi demektir. Ama `unitOfMeasureId == Guid.Empty` bir is kurali ihlalidir -- deger var ama gecersiz. O yuzden `Result.Failure` donuyoruz. Sinirlari karistirmak tehlikelidir: null'u Result.Failure ile donersen, cagiran kod "null gecmem normalmis" diye dusunur; gecersiz degeri exception ile patlatirsan, kullaniciya anlasilmaz bir 500 hatasi gosterirsin.

### Conversion Factor
Olcu birimi donusumu icin bir carpan. "1 Kilogram = 1000 Gram" demek yerine, Gram'in `ConversionFactor`'u `0.001` (yani 1 Gram = 0.001 Kilogram). Base unit her zaman factor 1'dir -- cunku kendisini kendisine donusturmek 1:1'dir. Derived unit'lerin factor'u 0'dan buyuk olmali.

### DeleteBehavior.Restrict
EF Core'da bir FK iliskisinde parent kayit silindiginde child kayitlara ne olacagini belirler. `Cascade` secersen parent ile birlikte child'lar da silinir. `Restrict` secersen parent'i silmeye calistigin anda veritabani hata verir -- yani "once bu kayda bagli seyleri temizle" der. Bu projede hem self-referencing FK'da (bir base unit, kendisine bagli derived unit'ler varken silinemez) hem de Product -> UnitOfMeasure FK'sinda Restrict kullaniliyor. ERP'de cascade silme genellikle tehlikelidir: bir olcu birimini sildigin icin butun urunlerin ucup gitmesini istemezsin.

### Unique Index
Bir kolondaki (veya kolon kombinasyonundaki) degerlerin tekrarlanmasini engelleyen veritabani kisitlamasi. `HasIndex(u => u.Code).IsUnique()` ile Code kolonuna unique index koyuyoruz. Bu demek ki ayni code ile iki kayit ekleyemezsin -- ikinci `SaveChangesAsync` cagrisinda `DbUpdateException` alinir. Uygulamada genellikle "bu kod zaten var" seklinde kullaniciya anlamli bir hata donustururuz ama bu PR'da henuz application katmani yok.

### Migration (EF Core Migrations)
Veritabani semasini kod olarak versiyonlama sistemi. `dotnet ef migrations add InitialSchema` komutu, EF Core'un mevcut model snapshot'i ile DbContext'teki modeli karsilastirir, farktan bir C# dosyasi uretir. Bu dosya `Up()` ve `Down()` metodlari icerir -- `Up` degisikligi uygular, `Down` geri alir. Migration dosyalari kaynak kodla birlikte versiyonlanir, boylece ekipteki herkes ayni sema degisikliklerini uygulayabilir. React'teki TypeScript "`interface`" gibi dusun -- veritabani semasinin "type definition"i.

### RowVersion (Concurrency Token)
SQL Server'in `timestamp`/`rowversion` veri tipi. Her satirda otomatik artan 8 byte'lik bir binary deger. Bir satir her guncellendiginde SQL Server bu degeri otomatik arttirir. EF Core bunu optimistic concurrency icin kullanir: "bu satiri okudugumda RowVersion 42 idi, guncellerken hala 42 ise guncelle, degilse baskasi araya girdi demektir" seklinde `WHERE RowVersion = @old` kosuluyla UPDATE yapar. Uyumsuzlukta `DbUpdateConcurrencyException` firlatilir. Bu projede `AggregateRootConvention` tum aggregate root'lara otomatik RowVersion ekler.

### Guid.CreateVersion7()
.NET 9+ ile gelen bir metod. Klasik `Guid.NewGuid()` rastgele bir GUID uretir ve veritabaninda clustered index'e eklendiginde page split'lere neden olur cunku degerler rastgele dagilir. Version 7 GUID'leri ise zamana dayali (timestamp-based) uretilir -- sonraki GUID her zaman oncekinden buyuktur. Bu, clustered index'e eklemenin her zaman sona ekleme (append) olmasi demektir ve yazma performansini onemli olcude iyilestirir.

### ApplyConfigurationsFromAssembly
`OnModelCreating` icinde tek tek `builder.ApplyConfiguration(new ProductConfiguration())` yazmak yerine, `ApplyConfigurationsFromAssembly` bir assembly'deki tum `IEntityTypeConfiguration<T>` siniflarini otomatik bulur ve uygular. Yeni bir entity eklediginde konfigurasyonunu yazmak yeterli -- DbContext'e dokunmana gerek kalmaz (DbSet property'si haric). Convention-over-configuration yaklasimi.

## Komutlar ve ne yaptiklari

```bash
dotnet ef migrations add InitialSchema -p src/Envanex.Infrastructure -s src/Envanex.Web
```
EF Core CLI araci. `migrations add` yeni bir migration dosyasi uretir. `-p` (project) migration'in hangi projede olusturulacagini belirtir -- DbContext ve konfigurasyonlar Infrastructure'da. `-s` (startup project) ise `IDesignTimeDbContextFactory`'yi bulmak icin kullanilir -- bu factory Web projesinde kayitli. Komut calistiginda EF Core modeli analiz eder, mevcut snapshot ile karsilastirir ve farki C# koduna cevirir.

```bash
dotnet build -warnaserror
```
Tum solution'i build eder ve her warning'i error olarak degerlendirir. Bu projede `TreatWarningsAsErrors` acik oldugu icin CS8618 ("non-nullable field not initialized") gibi uyarilar build'i durdurur. EF Core'un parameterless constructor'inda `Code = default!;` yazmamizin sebebi bu -- null-forgiving operator (`!`) ile compiler'a "biliyorum, EF Core dolduracak" diyoruz.

```bash
dotnet test --filter "FullyQualifiedName~UnitOfMeasureTests"
```
Sadece ismi `UnitOfMeasureTests` iceren test siniflarini calistirir. `--filter` parametresi buyuk bir test suite'inde belirli testleri secmek icin kullanilir. `~` operatoru "contains" anlamina gelir; `=` olsa tam eslesme isterdi.

```bash
dotnet format --verify-no-changes
```
Kodun `.editorconfig` kurallarinda tanimlanmis formata uygun olup olmadigini kontrol eder. Bir dosya formata uymuyorsa sifirdan farkli exit code doner ve CI pipeline'inda build kirilir. `--verify-no-changes` bayragi dosyalari degistirmez, sadece kontrol eder.

## Dikkat edilen tuzaklar

### Self-referencing FK'da DELETE sirasi
`ResetAsync` metodu testler arasinda veriyi temizler. Naive yaklasim `DELETE FROM UnitOfMeasures` yazmak olurdu ama bu, derived unit'ler (BaseUnitId != NULL olan satirlar) base unit'lere FK ile baglidigi icin patlar. Cozum iki asamali silme: once `WHERE BaseUnitId IS NOT NULL` (derived unit'ler), sonra `WHERE BaseUnitId IS NULL` (base unit'ler). Bu bug gercekten yasandi -- `fix(infra): delete self-referencing units before their base units` commit'i bunu duzeltmek icin yazildi. FK constraint'lerini dusunmeden toptan DELETE yazmak, testlerin rastgele basarisiz olmasina yol acar.

### Products'i UnitOfMeasures'tan once silme
Ayni FK mantigi tablolar arasi da gecerli. Products tablosunda `UnitOfMeasureId` FK'si var -- once Products silinmezse UnitOfMeasures silinemez. `ResetAsync`'teki DELETE sirasi: (1) Products, (2) derived UnitOfMeasures, (3) base UnitOfMeasures, (4) Warehouses.

### EnsureCreated yerine MigrateAsync kullanmak
`EnsureCreated` modelden dogrudan DDL uretir ve migration dosyalarini hic okumaz. Bu, migration dosyasindaki bir hatanin (yanlis precision, eksik index, bozuk FK) hicbir test tarafindan yakalanamayacagi anlamina gelir. `MigrateAsync` ise migration dosyalarini sirasiyla uygular, yani testler migration'in kendisini de test etmis olur. Testcontainers zaten her kosuda temiz bir veritabani baslatir, dolayisiyla `MigrateAsync`'in "zaten var olan tablolara migration uygulamaya calisir" riski yoktur.

### EF Core parameterless constructor ve default!
EF Core entity'leri veritabanindan yuklerken parameterless constructor kullanir ve property'leri reflection ile doldurur. Ama `TreatWarningsAsErrors` acikken, non-nullable `string` property'yi initialize etmezsen CS8618 uyarisi (simdi hata) alinir. `Code = default!;` yazmak compiler'a "bu deger daha sonra set edilecek" sinyali verir. Constructor'u `private` yapmak onemli -- disaridan kimse bu yarim-baslangic-halli nesneyi olusturmasin.

### Migration dosyalarinda CA1062 bastirma
`dotnet ef migrations add` ile uretilen C# dosyalari `migrationBuilder` parametresini null-check yapmadan kullanir. `TreatWarningsAsErrors` acikken CA1062 ("validate parameter") uyarisi build'i kirar. Cozum: `.editorconfig`'te `[src/Envanex.Infrastructure/Migrations/*.cs]` scope'unda `CA1062.severity = none` yazmak. Bu, sadece uretilen migration dosyalari icin gecerlidir -- production kodda CA1062 hala aktif.

### OnDelete(DeleteBehavior.Restrict) secimi
Varsayilan EF Core davranisi genellikle `Cascade`'dir -- parent silindiginde child da silinir. Bir ERP'de bu felaket olabilir: bir olcu birimini silersen ona bagli tum urunler kaybolur. `Restrict` secmek, "once iliskili kayitlari temizle" demeye es degerdir ve veri kaybini onler. Bunun trade-off'u: silme islemi daha fazla adim gerektirir, kullaniciya "bu kayit kullanimda" gibi anlamli bir hata donusturmek gerekir.

### ComplexProperty'yi explicit yazmak
`MoneyComplexTypeConvention` Money tipinin precision'ini otomatik ayarlar ama EF Core'un bir tipi complex type olarak tanimasi icin ya convention'da ya da konfigurasyonda `ComplexProperty` cagrisinin yapilmasi gerekir. Bu projede convention `ComplexProperty` olarak isaretlenmis tiplerde precision ayarlar, ama isaretleme kendisi `ProductConfiguration`'da explicit yapilir. Bunu yazmayi unutursan EF Core Money'yi owned type veya hatta ayri bir entity olarak modellemeye calisabilir ve beklenmedik sema degisiklikleri olusur.

## Kendini sina

1. `UnitOfMeasure.Create` metodunda `baseUnitId` `null` gelirse `conversionFactor` neden tam olarak `1m` olmak zorunda? `0.5m` veya `2m` olsa ne olurdu?

2. `Product.Create` icinde `listPrice` parametresi icin `ArgumentNullException.ThrowIfNull` kullanilirken, `unitOfMeasureId == Guid.Empty` icin neden `Result.Failure` donuluyor? Ikisini de ayni mekanizmayla handle etsek ne kotu olurdu?

3. `SqlServerFixture.ResetAsync`'te UnitOfMeasures tablosu neden tek bir `DELETE FROM UnitOfMeasures` yerine iki ayri sorgu (`WHERE BaseUnitId IS NOT NULL` ve `WHERE BaseUnitId IS NULL`) ile temizleniyor? Siralarini degistirsen (once NULL, sonra NOT NULL) ne olur?

4. `ProductConfiguration`'da `builder.ComplexProperty(p => p.ListPrice)` satiri kaldirilirsa ama `MoneyComplexTypeConvention` hala mevcutsa ne olur? Migration yeniden uretildiginde sema nasil degisir?

5. `UnitOfMeasureConfiguration`'daki self-referencing FK neden `HasOne<UnitOfMeasure>()` seklinde generic tip ile yazilmis, neden bir navigation property kullanilmamis? Navigation property eklesek domain katmaninda ne degisirdi?

6. `SqlServerFixture`'da `EnsureCreatedAsync` yerine `MigrateAsync` kullanmaya gecildi. Eger migration dosyasinda bir kolon `decimal(18,2)` olarak yazilmis ama konfigurasyonda `decimal(18,4)` olarak tanimlanmis olsaydi, `EnsureCreated` ile calisan testler bu hatayi yakalar miydi? Neden?

7. Migration dosyasinin `Down()` metodu tablolari `Products -> Warehouses -> UnitOfMeasures` sirasinda siliyor. Bu siranin `UnitOfMeasures -> Warehouses -> Products` olarak degistirilmesi migration rollback'ini nasil etkiler?

8. `ResetAsync`'te Warehouses tablosu en son siliniyor ama su an hicbir tablo Warehouses'a FK ile baglanmiyor. Bu siralama neden yine de dogru, ve gelecekte hangi tablonun eklenmesi bu sirayi degistirmeyi zorunlu kilar?
