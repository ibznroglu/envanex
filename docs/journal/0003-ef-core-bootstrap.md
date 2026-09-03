# PR 0003 — EF Core Infrastructure Bootstrap

## Ne yaptik

Bu PR, domain katmanindaki value object'lerin (Money, Currency, Quantity) SQL Server'a nasil eslenecegini tanimlayan EF Core altyapisini kurdu. Infrastructure projesine `EnvanexDbContext` eklendi; icerisinde iki model-building convention (`AggregateRootConvention`, `MoneyComplexTypeConvention`) ve iki value converter (`CurrencyConverter`, `QuantityConverter`) kayitli. Web host'un DI konteynerine `AddInfrastructure` extension method'u ile DbContext kaydedildi; connection string user-secrets'tan geliyor, yoksa uygulama anlasilir hata mesajiyla pat diye duruyor. Testcontainers ile gercek SQL Server instance'ina karsi calistirilan entegrasyon testleri, sema precision'larini (`decimal(18,4)`, `decimal(18,6)`), `RowVersion` concurrency token'ini, Money ve Quantity round-trip persistance'ini ve concurrent update'lerde `DbUpdateConcurrencyException` firlatidigini dogruluyor.

Bu PR'da migration dosyasi yok. Sema yalnizca test tarafinda `EnsureCreated` ile ephemeral Testcontainers instance'larinda olusturuluyor. Ilk migration, PR 4'te gercek domain aggregate ile gelecek. Ama `IDesignTimeDbContextFactory` burada kuruldu, yani `dotnet ef migrations add` komutu calismaya hazir.

## Yeni giren teknolojiler

### EF Core (Entity Framework Core)

- **Ne ise yarar:** Bir ORM (Object-Relational Mapper). C# siniflarin ile SQL tablolari arasinda kopru kurar. Sen `context.TestProducts.Add(product)` yazarsin, o `INSERT INTO TestProducts (...)` uretir. Sorgu yazarken LINQ kullanirsin, o bunu SQL'e cevirir. React dnyasindaki analojisi: Prisma veya Drizzle ne ise TypeScript icin, EF Core o ise .NET icin — ama cok daha olgun ve karmasik.
- **Bu projede nerede:** `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` ana giris noktasi. Convention'lar, converter'lar ve factory hep ayni `Persistence/` klasorunde.
- **Alternatifi neydi:** Ham ADO.NET (manual `SqlConnection`, `SqlCommand`, `SqlDataReader` yazardin) veya Dapper (lightweight micro-ORM, SQL elle yazilir, mapping otomatik). ADO.NET secilmedi cunku her tablo icin yuzlerce satir boilerplate gerektirir. Dapper secilmedi cunku complex type mapping, convention sistemi ve migration toolchain'i yok; buyuk bir ERP projesi icin eksik kaliyor. EF Core'un bedeli: soyutlama katmani performans maliyeti ve ogrenim egrisi.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/

### Testcontainers

- **Ne ise yarar:** Test basladiginda Docker ile gercek bir SQL Server konteyneri ayaga kaldirir, testler bittikten sonra onu yok eder. Boylece testler fake/mock veritabani yerine gercek SQL Server'a karsi calisir. Jest testlerinde `mongodb-memory-server` kullandiysan, ayni fikrin Docker tabanli hali.
- **Bu projede nerede:** `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — `MsSqlContainer` instance'ini olusturup yonetiyor.
- **Alternatifi neydi:** SQLite in-memory (hizli ama SQL Server'dan farkli davranislari var — ornegin `decimal` precision'lari farkli calisir, `timestamp`/`rowversion` destegi yok). EF Core'un `InMemoryDatabase` provider'i (tamamen sahte, SQL bile calistirmaz, iliskisel kistlamalari yok sayar). Her ikisi de "yesil gecen ama uretimde patlayan testler" riski tasir. Testcontainers'in bedeli: Docker'in kurulu olmasi sart ve testler yavas (konteyner ayaga kalkmasi 5-15 saniye).
- **Nerede okunur:** https://dotnet.testcontainers.org/

### Shouldly

- **Ne ise yarar:** Assertion kutuphanesi. `Assert.Equal(expected, actual)` yerine `actual.ShouldBe(expected)` yazarsin. Hata mesajlari cok daha okunaklı — ne beklendigini ve ne geldigini acikca gosterir. React testlerindeki `expect(value).toBe(expected)` syntax'ina yakin.
- **Bu projede nerede:** Tum test dosyalarinda — ornegin `result.Precision.ShouldBe((byte)18)`.
- **Alternatifi neydi:** xUnit'in built-in `Assert` sinifi (calisir ama hata mesajlari daha kuru) veya FluentAssertions (cok populer ama lisans degisikligi nedeniyle riskli). Shouldly MIT lisansli ve yeterli.
- **Nerede okunur:** https://docs.shouldly.org/

### Microsoft.EntityFrameworkCore.Design

- **Ne ise yarar:** `dotnet ef` CLI aracinin calisabilmesi icin gereken design-time paket. Runtime'da kullanilmaz, sadece migration olusturma ve veritabani guncelleme komutlari icin gerekir. React'teki `@types/*` paketleri gibi dusun — uretim kodunda calismiyor ama gelistirme arac zinciri icin sart.
- **Bu projede nerede:** `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` icinde `PackageReference` olarak.
- **Alternatifi neydi:** Migration'lari elle SQL olarak yazmak. Bu da bir secenektir ve bazi ekipler bunu tercih eder, ama EF Core'un model'den otomatik migration ureten toolchain'ini kullanamaz oldun.
- **Nerede okunur:** https://learn.microsoft.com/en-us/ef/core/cli/dotnet

## Kavramlar

### DbContext

EF Core'un merkezi sinifi. Veritabani baglantisini, tablo eslemelerini, degisiklik izlemeyi (change tracking) ve transaction yonetimini tek bir yerde toplar. Her `DbSet<T>` property'si bir SQL tablosuna karsilik gelir. React'teki `useContext` ile isim benzerliginden baska ilgisi yok — bu bir veritabani oturum nesnesi. `DbContext` instance'lari kisa omurlu olmali: istek basina bir tane olusturulur, is bittikten sonra `Dispose` edilir.

### Value Converter

EF Core'a "bu C# tipini veritabanina yazarken su tipe cevir, okurken geri cevir" diyen bir donusturucu. `CurrencyConverter` orneginde: `Currency` nesnesi veritabanina `string` olarak yazilir (`currency.Code` -> `"TRY"`), okunurken `string`'den `Currency`'ye geri donusturulur (`Currency.Of("TRY").Value`). Veritabani `Currency` diye bir tip bilmez; o sadece `nvarchar(3)` gorur.

### Complex Type

EF Core 8'de gelen bir kavram. Kendi basina bir tabloya sahip olmayan, bir entity'nin icine gomulu olan deger nesnesi. `Money` complex type olarak esleniginde, `TestProducts` tablosunda `UnitPrice_Amount` ve `UnitPrice_Currency` adinda iki sutun olusur — ayri bir `Money` tablosu degil. React'teki bir component'in props'larini dusun: component'in kendi route'u yok, parent'in icinde yasiyor. Complex type da entity'nin icinde oyle yasiyor.

Onemli kisitlama: EF Core complex type'lari otomatik kesfetmez. Her aggregate konfigurasyonunda `builder.ComplexProperty(p => p.UnitPrice)` satiri elle yazilmak zorunda. `MoneyComplexTypeConvention` sadece *zaten complex property olarak kaydedilmis* `Money`'lerin ic sutunlarini (precision, converter) yapilandirir.

### Convention (Model-Building Convention)

EF Core'un model'i olustururken otomatik uygulayacagi kurallar. `IModelFinalizingConvention` implement eden siniflar, model "finalize" edilirken (yani tum konfigurasyonlar uygulandiktan sonra, DDL uretilmeden once) calisir. Proje genelinde tekrarlayan konfigurasyonlari (her aggregate'e RowVersion eklemek gibi) convention'a tasirsin; boylece her entity konfigurasyonunda ayni seyi tekrar yazmana gerek kalmaz. ESLint rule'lari gibi dusun: bir kere tanimlarsin, her yerde gecerli olur.

### Shadow Property

Entity sinifinda CLR property'si olmayan ama EF Core modelinde ve veritabani tablosunda var olan bir property. `RowVersion` boyle: `TestProduct` sinifinda `public byte[] RowVersion` diye bir property yok, ama `AggregateRootConvention` bunu EF Core modeline ekliyor ve SQL Server tablosunda `RowVersion` sutunu var. Change tracker bu degeri kendi icinde tutar. Domain siniflarini veritabani kaygalarindan temiz tutar — `TestProduct` concurrency token bilmez, infrastructure halleder.

### Concurrency Token / RowVersion

Iyimser esaramanlik kontrolu (optimistic concurrency) mekanizmasi. SQL Server'da `timestamp` (ya da `rowversion`) tipinde bir sutun, her `UPDATE` isleminde otomatik artar. EF Core, bir entity'yi guncellerken `WHERE Id = @id AND RowVersion = @oldRowVersion` seklinde sorgu uretir. Eger baska biri arada ayni satiri guncelledigse `RowVersion` degismis olur, `WHERE` kosulu eslesemez, `UPDATE` sifir satir etkiler ve EF Core `DbUpdateConcurrencyException` firlatir. Bu, iki kullanicinin ayni siparis uzerinde ayni anda calistiginda verinin sessizce ezilmesini onler.

### IDesignTimeDbContextFactory

`dotnet ef migrations add` komutu calisirken DbContext'i nasil olusturacagini bilmesi gerekir. Normal calisma zamaninda DI container bunu halleder, ama CLI araci uygulamayi tam olarak baslatmaz. `IDesignTimeDbContextFactory` tam da bu durumu cozer: "migration aracim calistiginda, DbContext'i soyle olustur" der. Bu projede connection string'i `ENVANEX_CONNECTION_STRING` ortam degiskeninden okur.

### IAsyncLifetime (xUnit)

xUnit'in "test sinifi olusturulmadan once bir seyler yap, testler bittikten sonra temizle" mekanizmasi. React Testing Library'deki `beforeEach`/`afterEach` gibi. Burada `InitializeAsync` her test sinifinin basinda `ResetAsync` cagirarak tablodaki tum verileri siliyor — boylece testler birbirinden bagmsiz calisiyor.

### Collection Fixture (xUnit)

xUnit'te `IClassFixture` ile bir fixture sinif basina bir kere olusturulur. `ICollectionFixture` ile bir fixture *koleksiyon* basina bir kere olusturulur — birden fazla test sinifi ayni fixture'i paylasir. Docker konteyneri pahalı bir kaynak oldugu icin her test sinifi icin ayri konteyner baslatmak yerine, tek konteyner baslatilip `[Collection("Database")]` etiketli tum siniflar arasinda paylasiliyor.

### User Secrets

Gelistirme ortaminda hassas verileri (connection string, API key) kaynak kodun disinda tutma mekanizmasi. `appsettings.json` repoyla birlikte commit edilir, bu yuzden oraya parola yazilmaz. `dotnet user-secrets set "ConnectionStrings:EnvanexDb" "..."` komutu ile deger kullanicinin home dizininde (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`) sifresiz ama repo disinda saklanir. Runtime'da `IConfiguration` bu degeri otomatik okur. `.env` dosyalariyla ayni mantik, ama .NET'in kendi araci.

### `decimal(18,4)` ve `decimal(18,6)`

SQL Server'da `decimal(precision, scale)` notasyonu: `precision` toplam basamak sayisi, `scale` ondalik basamak sayisi. `decimal(18,4)` demek: en fazla 18 basamakli, bunun 4'u ondalik. Para icin 4 ondalik yeterli (kurusun yuzde biri seviyesi); miktar icin 6 ondalik (gram, mililitre gibi hassas olcumler). EF Core'un varsayilani `decimal(18,2)` — bu projedeki convention'lar bunu override ediyor.

### ApplyConfigurationsFromAssembly

`OnModelCreating` icinde cagrilan bu metod, assembly icerisindeki tum `IEntityTypeConfiguration<T>` siniflarini bulur ve otomatik uygular. Her entity icin ayri `modelBuilder.ApplyConfiguration(new XxxConfiguration())` satiri yazmana gerek kalmaz. Assembly buyudukce olceklenir.

## Komutlar ve ne yaptiklari

### `dotnet user-secrets set "ConnectionStrings:EnvanexDb" "..." --project src/Envanex.Web`

Gelistirme ortaminda connection string'i user-secrets deposuna kaydeder. `--project` bayragi hangi projenin `UserSecretsId`'sini kullanacagini belirtir (bu projede `envanex-web-secrets`). Bu deger `appsettings.json`'daki bos `EnvanexDb` key'ini override eder.

### `dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web`

Migration dosyasi olusturur. `-p` (project): migration'in nerede olusacagini belirtir (Infrastructure projesi). `-s` (startup): uygulamanin giris noktasini belirtir (Web projesi — DI konfigurasyonu burada). Bu PR'da bu komut calistirilmadi, ama `IDesignTimeDbContextFactory` ile arac zinciri hazir edildi.

### `context.Database.EnsureCreatedAsync()`

Veritabaninina varsa dokunmaz, yoksa EF Core modelinden turetilen DDL ile tum tablolari olusturur. Migration kullanmaz — modelden dogrudan `CREATE TABLE` uretir. Uretim ortaminda kullanilmaz (migration'la kontrol kaybedersin), ama test ortaminda ephemeral veritabanlarinda idealdir.

### `context.Database.ExecuteSqlRawAsync("DELETE FROM TestProducts")`

Ham SQL calistirir. `ResetAsync` icinde testler arasi izolasyon icin kullaniliyor. `TRUNCATE` yerine `DELETE` kullanilmasinin nedeni: `TRUNCATE` foreign key kisitlamalari olan tablolarda calismaz; simdilik fark etmez ama ileride tablo iliskileri geldiginde sorun cikmaz.

### `context.Database.SqlQueryRaw<T>(...)`

Ham SQL calistirip sonucu bir POCO sinifina map'ler. `SchemaPrecisionTests`'te `INFORMATION_SCHEMA.COLUMNS`'dan sutun metadata'si cekmek icin kullaniliyor. Entity Framework'un kendi modeline degil, gercek SQL Server'in urettigi semalara bakmak icin — boylece "EF Core bunu dogru DDL'e cevirdi mi?" sorusunu dogrudan veritabanina soruyorsun.

## Dikkat edilen tuzaklar

### Money'de `private init` zorunlulugu

`Money` record'unda property'ler baslangicta `get;` (init-only olmayan, setter'siz) tanimliydi. EF Core complex type property'lerini materialize ederken bir setter veya `init` accessor'a ihtiyac duyar — salt okunur property'leri set edemez. PR, `Amount` ve `Currency` property'lerini `private init` yaparak bu sorunu cozdu: disaridan hala immutable, ama EF Core constructor binding ile degerlerini set edebiliyor.

### DbContext'te iki constructor

`EnvanexDbContext(DbContextOptions<EnvanexDbContext> options)` (public) DI kaydı icin, `EnvanexDbContext(DbContextOptions options)` (protected) tureten siniflar icin. Eger sadece generic constructor olsaydi, `TestDbContext` kendi `DbContextOptions<TestDbContext>`'ini base'e gecirirken compile hatasi alirdi — cunku `DbContextOptions<TestDbContext>`, `DbContextOptions<EnvanexDbContext>` degildir. Non-generic `DbContextOptions` her ikisinin de base class'i oldugu icin ikinci constructor bu sorunu cozer.

### Connection string yoksa sessiz kalma

Hem `DependencyInjection.AddInfrastructure` hem de `EnvanexDbContextFactory.CreateDbContext` connection string bos veya null ise aciklayici hata mesajiyla `InvalidOperationException` firlatiyor. Bunu yapmasalardi, uygulama baska bir yerde anlasilmaz bir `SqlException` ile patlar ve sebebini bulmak 30 dakika alirdi.

### Complex type otomatik kesfedilmez

`MoneyComplexTypeConvention` "her `Money` property'sini otomatik complex type yap" demiyor. Sadece *zaten* complex property olarak kaydedilmis `Money`'lerin ic sutunlarini (precision, converter) yapilandirir. Bu ayrim kasitli: aggregate yazarken `builder.ComplexProperty(p => p.UnitPrice)` satiri unutulursa, EF Core `Money`'yi bir navigation property gibi esler ve hata verir — boylece eksiklik sessizce yutulmaz.

### Test izolasyonu icin DELETE, TRUNCATE degil

`ResetAsync` `DELETE FROM TestProducts` kullaniyor. `TRUNCATE TABLE` daha hizli olurdu ama foreign key constraint'leri varsa calismaz. Simdi constraint yok ama ileride `TestProduct`'a iliskiler eklendiginde `TRUNCATE` sessizce patlardi.

### `AggregateRootConvention`'da mevcut property kontrolu

Convention, `RowVersion` eklemeden once `entityType.FindProperty("RowVersion") is not null` kontrolu yapiyor. Bu olmasa, bir aggregate kendi `RowVersion` property'sini tanimlasaydi duplike property hatasi alirdin.

## Farkli dusundugum yer

`MoneyComplexTypeConvention` sadece `Amount` precision'ini set ediyor ama `Currency` icin hicbir sey yapmiyor — `Currency` converter'i ve max length'i zaten `ConfigureConventions`'da global olarak kayitli oldugu icin gerekmiyor. Ancak bu, convention'in yarim is yaptigini gizliyor: `Money` complex type'inin iki parcasindan biri convention'da, digeri baska bir yerde yapilandiriliyor. Ben olsam `Currency` converter ayarini da ayni convention icine tasirdim ve `ConfigureConventions`'daki global `Currency` kaydini kaldirir, yerine convention icinden sutun bazinda ayarlardim. Bu sekilde "Money'nin veritabanina nasil eslendigini merak ediyorsan tek bir yere bak" kuralini saglardim. Mevcut yaklasimin avantaji: `Currency` baska bir entity'de tek basina (Money olmadan) kullanilirsa global kayit onu da kapsar. Ama projede `Currency`'nin `Money` disinda tek basina kullanilmasi planlanmiyor.

## Kendini sina

1. `QuantityConverter`'da `Quantity.Of(value).Value` ifadesi cagrildiginda, `Of` bir `Result<Quantity>` donduruyor. Eger veritabanindan gelen deger negatif olsaydi (ornegin biri elle veritabanina `-5` yazsaydi), bu converter ne yapardi?

2. `TestDbContext` constructor'i neden `DbContextOptions<TestDbContext>` aliyor da `DbContextOptions<EnvanexDbContext>` almiyor? `EnvanexDbContext`'in protected constructor'i olmasaydi ne olurdu?

3. `SchemaPrecisionTests`'te `INFORMATION_SCHEMA.COLUMNS` sorgulanarak `NUMERIC_PRECISION` ve `NUMERIC_SCALE` kontrol ediliyor. Bu testler neden EF Core'un `IModel` API'si uzerinden degil de dogrudan SQL Server'in metadata'sindan kontrol ediyor — ikisi arasinda ne fark var?

4. `AggregateRootConvention`'da `IsAggregateRoot` metodu `type.BaseType`'i zincirleme yukari yuruyerek `AggregateRoot<>` ariyor. `type.IsAssignableTo(typeof(AggregateRoot<>))` kullansak calismaz miydi? Neden?

5. `appsettings.json`'da `"EnvanexDb": ""` olarak bos string var. Uygulama baslarken `AddInfrastructure` bu bos string'i nasil ele aliyor ve neden `""` degeri `null` gibi davraniliyor?

6. `MoneyComplexTypeConvention` `IModelFinalizingConvention` uyguluyor ve `GetComplexProperties()` ile tum complex property'leri tarıyor. Eger bir aggregate konfigurasyonunda `builder.ComplexProperty(p => p.UnitPrice)` yazilmayi unutulursa, convention `Money` tipini hic gormez. Bu durumda EF Core `Money`'yi nasil eslemeye calisir ve ne tur bir hata alirsin?

7. Concurrency testinde iki ayri `DbContext` instance'i ayni `TestProduct`'i yukluyor. Eger ikisi de `context1` olsaydi (yani ayni DbContext'ten iki kere `SingleAsync` cagrilsaydi), `DbUpdateConcurrencyException` yine firlatilir miydi? Neden?

8. `SqlServerFixture` bir `ICollectionFixture` olarak kullaniliyor ve konteyner tum test siniflarinca paylasiliyor. Eger her test sinifi icin ayri konteyner baslatilsa (yani `IClassFixture<SqlServerFixture>` kullansak), testlerin davranisinda ne degisirdi ve hangi yeni sorunlar ortaya cikardi?
