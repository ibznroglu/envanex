# PR 0002 — Shared Kernel Primitives

## Ne yaptik

Bu PR, `Envanex.Domain` projesine gelecek aggregate'lerin uzerine insa edilecegi temel yapi taslarini ekledi. Uc katman halinde dusunebilirsin: (1) hata yonetimi icin `Result`, `Result<T>` ve `Error` tipleri, (2) entity kimlik esitligi icin `Entity<TId>` ve domain event toplama icin `AggregateRoot<TId>` taban siniflari, (3) is alani degerlerini temsil eden `Currency`, `Money` ve `Quantity` value object'leri. Hicbir NuGet paketi eklenmedi -- Domain projesinin sifir bagimlilik kurali korundu. Ayrica CLAUDE.md'deki uc yanlis ifade duzeltildi ve ADR 0002 stub dosyasi olusturuldu. PR'in sonunda Domain projesinde 5 kaynak dosya (`Common/` altinda) ve 3 kaynak dosya (`ValueObjects/` altinda) var, hepsi 34 unit test ile dogrulanmis durumda.

## Yeni giren teknolojilerf

Bu PR hicbir yeni NuGet paketi veya framework bilesenini projeye eklemiyor. Tum yeni kod sifir bagimlilikla, sadece .NET BCL (Base Class Library) tipleri kullanilarak yazildi. Ancak PR'da kullanilan bircok desen ve C# dil ozelligini tanimanin faydasi var:

### Record tipi (C# 9+)

- **Ne ise yarar:** `record`, C#'ta deger semantigi (value semantics) tasiyan referans tipi tanimlamanin kisa yolu. Derleyici senin icin `Equals`, `GetHashCode`, `ToString` ve `with` ifadesini otomatik uretir. Yani iki record instance'i ayni property degerlerine sahipse, `==` ile karsilastirdiginda `true` doner -- normal `class`'larda bu referans karsilastirmasi yapar, farkli nesneler her zaman farkli gorulur. React'teki shallow equality kavrami sana yakin gelecektir: record'lar da benzer sekilde icerige gore esitlik saglar.
- **Bu projede nerede:** `Error` (`Common/Error.cs`), `Currency` (`ValueObjects/Currency.cs`), `Money` (`ValueObjects/Money.cs`) sealed record olarak, `Quantity` (`ValueObjects/Quantity.cs`) ise `readonly record struct` olarak tanimlanmis.
- **Alternatifi neydi:** Normal `class` yazip `Equals`/`GetHashCode`'u elle override etmek. Calisir ama boilerplate kod uretir ve bir property eklediginde override'lari guncellemeyi unutma riski vardir. Record bu riski ortadan kaldirir.
- **Nerede okunur:** https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record

### `readonly record struct` (C# 10+)

- **Ne ise yarar:** `record struct`, deger tipi (value type) olarak stack uzerinde yasayan bir record. `readonly` eklenince tum field'lar degistirilemez hale gelir. Normal `record` (sinif) heap'te yasarken, `record struct` stack'te yasayabilir -- bu da kucuk, sik kullanilan degerler icin bellek ve performans avantaji saglar. Onemli fark: `default(Quantity)` gecerli bir deger uretir (`Value = 0m`), oysa `default(Money)` referans tipi oldugu icin `null` uretir.
- **Bu projede nerede:** `Quantity` (`ValueObjects/Quantity.cs`).
- **Alternatifi neydi:** `Quantity`'yi de `sealed record` (referans tipi) yapmak. Ama sifir miktar gecerli bir is degeri oldugu icin (stokta sifir urun olmasi normal), struct'in default degerinin gecerli olmasi bir avantaj. Ayrica `Quantity` cok sik kullanilacak bir tip -- her stok hareketi, her siparis kalemi icin en az bir tane olusturulacak -- stack allocation burada anlam kazaniyor.
- **Nerede okunur:** https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#record-struct

### Factory method pattern (private constructor + static `Of`)

- **Ne ise yarar:** Constructor'i `private` yapip, nesne olusturmayi static bir metoda (burada `Of`) tasimak. Bu sayede nesne olusturulmadan once validasyon yapabilirsin ve gecersiz durumda exception atmak yerine `Result<T>` donebilirsin. Constructor'lar sadece `new` ile cagirilir ve donen tip her zaman o sinifin kendisidir -- basarisizligi ifade edemezsin. Factory method ise `Result<T>` donebildigi icin cagiranin hataya hazirlenmasi gerektigini tip sistemiyle zorunlu kilar.
- **Bu projede nerede:** `Currency.Of(string code)`, `Money.Of(decimal amount, Currency currency)`, `Quantity.Of(decimal value)`.
- **Alternatifi neydi:** Public constructor ile validasyon ve exception firlatmak. Bu, cagiranin try-catch yazmasi ya da hatayi gormezden gelmesi anlamina gelirdi. `Result<T>` ile calisan factory, hatanin tip seviyesinde gorunur olmasini sagliyor.
- **Nerede okunur:** https://refactoring.guru/design-patterns/factory-method

## Kavramlar

### Value Object

Domain-Driven Design'da (DDD) iki tur nesne vardir: Entity ve Value Object. Entity'nin bir kimligi vardir -- iki musteri ayni ada sahip olsa bile farkli musterilerdir cunku farkli ID'leri vardir. Value Object'in ise kimligi yoktur, tamamen icerigine gore tanimlanir -- 100 TL ile 100 TL ayni seydir, hangisinin hangi degiskenle olusturuldugu onemli degildir. Bu PR'daki `Currency`, `Money` ve `Quantity` value object'tir. C#'taki `record` tipi, value object icin ideal bir altyapi saglar cunku esitligi otomatik olarak icerige gore yapar.

### Entity ve Identity Equality

Entity, kimligi olan bir domain nesnesidir. Iki entity ayni tipteyse ve ayni `Id`'ye sahipse esittir -- diger property'leri farkli olsa bile. Bu PR'daki `Entity<TId>` bunu `IEquatable<Entity<TId>>` uygulayarak ve `Equals`/`GetHashCode`'u ID bazli override ederek sagliyor. React'teki `key` prop'una benzetebilirsin: React bir listedeki elemanlari `key`'e gore tanimlar, entity de `Id`'ye gore tanimlanir.

### Aggregate Root

Aggregate, birbiriyle tutarli olmasi gereken entity ve value object'lerin bir arada yonetildigi kume. Aggregate Root, bu kumenin dis dunyayla tek iletisim noktasi. Disaridan sadece root'a erisilebilir, icerdeki nesnelere dogrudan erisim yoktur. Bu PR'daki `AggregateRoot<TId>` sinifi `Entity<TId>`'dan turetilir ve ek olarak domain event toplama yetenegi ekler. Ileride `Product`, `PurchaseOrder` gibi tipler bu siniftan turetilecek.

### Domain Event

Sistemde "bir sey oldu" bilgisini tasiyan nesne. Ornegin, "urun stoga girdi" veya "siparis onaylandi" gibi olaylar. Bu PR'da sadece marker interface (`IDomainEvent`) tanimlandi -- event'lerin ne zaman ve nasil dispatch edilecegi (outbox pattern ile) sonraki PR'lara birakildi. `OccurredOnUtc` property'si her event'in ne zaman gerceklestigini UTC olarak kaydeder.

### Result pattern

Exception'lara alternatif bir hata yonetim yaklasimi. Bir metodun basarili veya basarisiz olabilecegi durumlarda, exception atmak yerine `Result<T>` donulur. Cagiranin `IsSuccess` veya `IsFailure` kontrol etmesi gerekir -- hatanin fark edilmemesi zorlasir. Exception'lar ise control flow icin kullanildiginda gorunmez hale gelir: metodun imzasinda hangi hatalari firlatabilecegin yazmaz (C#'ta checked exception yoktur, Java'dan farkli olarak), dolayisiyla cagiran hatanin olabilecegini bilmeyebilir. `Result<T>` bunu derleme zamaninda gorunur kilar.

### Banker's rounding (MidpointRounding.ToEven)

Normalde 0.5'i yukariya yuvarlarsin (okullarda ogretilen yontem). Ama bu, buyuk veri setlerinde sistematik olarak yukari yonlu sapma yaratir -- binlerce finansal islemde toplam surekli oldugundan fazla cikar. Banker's rounding, 0.5'i en yakin cift sayiya yuvarlar: 10.125 -> 10.12 (2 cift), ama 10.135 -> 10.14 (4 cift). Bu, sapmayi istatistiksel olarak dengeler. Finansal yazilimlarda standart yaklasimdir. Bu projede `Money` 4 ondalik basamaga, `Quantity` 6 ondalik basamaga bu yontemle yuvarlanir.

### Sealed sinif/record

`sealed` anahtar kelimesi, bir sinifin veya record'un baska bir tip tarafindan kalitim (inheritance) yoluyla genisletilmesini yasaklar. Value object'ler icin onemlidir cunku kalitim, esitlik semantigini kirar. Ornegin `SpecialMoney : Money` diye bir alt sinif olsaydi, `SpecialMoney(100, TRY) == Money(100, TRY)` sorusu cevaplanamaz bir karmasikliga donusurdu. `sealed` bunu en basindan engeller.

### Generic type constraint (`where TId : notnull`)

`Entity<TId>` taniminda `where TId : notnull` kisitlamasi, `TId` yerine sadece nullable olmayan tiplerin kullanilabilecegini belirtir. Bu olmadan biri `Entity<int?>` yazabilir ve `Id` degeri `null` olabilirdi -- kimligi olmayan bir entity anlamsizdir. TypeScript'teki generic constraint'lere (`<T extends Something>`) cok benzer.

### `IEquatable<T>` interface'i

.NET'te `Equals(object)` metodu boxing (value type'larin object'e cevrimi) ve tip kontrolu gerektirdigi icin yavas olabilir. `IEquatable<T>` strongly-typed bir `Equals(T)` metodu tanimlar, boxing'i ortadan kaldirir ve `Dictionary`, `HashSet` gibi koleksiyonlarin performansli calismasini saglar. `Entity<TId>` bu interface'i implemente ederek entity karsilastirmalarini optimize ediyor.

### Protected ve internal erisim belirleyicileri

`protected`, bir uye'nin (member) sadece tanimlayan sinif ve ondan turetilen siniflar tarafindan erisilebilecegini belirtir. `Entity<TId>`'deki parametresiz constructor `protected` cunku sadece EF Core materialization sirasinda alt siniflar uzerinden cagirilmali, disaridan `new Entity<Guid>()` yazmak anlamsiz. `internal` ise uye'nin sadece ayni proje (assembly) icinden erisilebilecegini belirtir. `Result<T>`'nin constructor'i `internal` cunku sadece `Result` sinifinin factory metodlari onu olusturmali -- disaridan biri gecersiz bir `Result<T>` uretememeli.

### Analyzer ve `.editorconfig` scope'u

.NET analyzerlari kodu derlerken ek kurallar uygular -- eslint'e benzetebilirsin. `CA1716` kuralı, C# anahtar kelimelerine benzeyen tip isimlerini yasaklar (`Error` bunlardan biri). Bu projede `Error` tipinin adi DDD konvansiyonuna uygun oldugu icin kural, `.editorconfig`'te sadece o dosya icin kapatiildi: `[src/Envanex.Domain/Common/Error.cs]` bloguyla scope'lanmis. Ayni sekilde `CA1707` (metod adinda alt cizgi yasagi) test dosyalari icin `[tests/**/*.cs]` bloguyla kapatilmis, cunku test metod adlari `Should_DoSomething_When_Condition` formatinda yaziliyor.

### EF Core materialization

EF Core, veritabanindan okunan satirlari C# nesnelerine cevirirken "materialization" adi verilen bir surec uygular. Bu surecte parametresiz constructor'i cagirarak bos bir nesne olusturur, sonra property'leri reflection ile doldurur. Bu yuzden `Entity<TId>` ve `AggregateRoot<TId>` siniflarinda `protected` parametresiz constructor var. Bu constructor'da `Id = default!` yazilmis -- `default!` null-forgiving operator'u ile nullable uyarisini bastirir cunku EF Core aninda uzerine gercek degeri yazacaktir.

## Komutlar ve ne yaptiklari

Bu PR'da yeni komut eklenmedi. Mevcut komutlar kullanildi:

```bash
dotnet build src/Envanex.Domain -warnaserror
```
Domain projesini derler. `-warnaserror` bayragi tum uyarilari hata olarak ele alir -- tek bir uyari bile derlemeyi basarisiz kilar. Bu, analyzer kurallarinin (CA1062, CS8618 vb.) enforcing mekanizmasidir.

```bash
dotnet test tests/Envanex.Domain.Tests --filter "FullyQualifiedName~ResultTests"
```
Sadece ismi `ResultTests` iceren test siniflarini calistirir. `--filter` bayragi xUnit'in test filtreleme mekanizmasini kullanir. `~` operatoru "iceriyor" anlamina gelir (tam eslesme degil). Faz faz calisirken sadece ilgili testleri calistirmak icin kullanildi.

```bash
dotnet format --verify-no-changes
```
Kodun `.editorconfig` kuralarina uygun olup olmadigini kontrol eder. Uygunsuzluk varsa sifir olmayan cikis kodu doner ama dosyalari degistirmez (`--verify-no-changes` sayesinde). CI pipeline'da stil tutarliligi zorunlu kilmak icin kullanilir.

## Dikkat edilen tuzaklar

### `DomainEvents` donus tipinde downcast korunmasi

`AggregateRoot<TId>.DomainEvents` property'si `IReadOnlyCollection<IDomainEvent>` donuyor ama arka planda `_domainEvents.AsReadOnly()` kullaniliyor, dogrudan `_domainEvents` listesini donmuyor. Eger `_domainEvents`'i dogrudan `IReadOnlyCollection<IDomainEvent>` olarak donseydin, cagiran onu `(List<IDomainEvent>)aggregate.DomainEvents` seklinde downcast edip listeye eleman ekleyebilirdi. `.AsReadOnly()` bir `ReadOnlyCollection<T>` wrapper'i doner ve bu downcast'i kirar. Testte de bu dogrudan kontrol ediliyor: `(aggregate.DomainEvents as List<IDomainEvent>).ShouldBeNull()`.

### Farkli tiplerin ayni ID ile esit gorunmesi

`Entity<TId>.Equals` icinde `GetType() != other.GetType()` kontrolu var. Bu olmadan, `Entity<Guid>` taban tipinde iki farkli alt sinif (ornegin `Product` ve `Supplier`) ayni GUID'e sahip olursa esit gorunurdu. Bir urun ve bir tedarikci ayni ID'ye sahip olabilir (farkli tablolarda) ama bunlar ayni entity degil -- `GetType()` kontrolu bunu engeller.

### `Money`'de negatif tutara izin verilmesi

`Money.Of` negatifi reddetmiyor -- bu bilerek yapilmis. Alacak dekontlari, iadeler ve muhasebe duzeltmeleri negatif para degerleri gerektirir. Isaret kisitlamasi (bu tutar pozitif olmali) aggregate seviyesinde uygulanir, value object seviyesinde degil. Eger `Money` negatifi yasaklasaydi, her iade islemi icin ayri bir tip veya workaround gerekecekti.

### `Quantity.Add`'in `Result<Quantity>` yerine `Quantity` donmesi

Iki negatif olmayan miktar toplaminin sonucu her zaman negatif olmayan olacagi icin `Add` metodu basarisiz olamaz. Bu yuzden `Result<Quantity>` yerine dogrudan `Quantity` doner. Gereksiz yere `Result` sarmak, cagiranin `IsSuccess` kontrolu yapmak zorunda kalmasi demek olurdu -- hem gereksiz kod, hem yanlis sinyal (basarisizlik mumkunmus gibi). Ama `Subtract` ve `Multiply` basarisiz olabilir (sonuc negatife dusebilir) bu yuzden onlar `Result<Quantity>` donuyor.

### `Money.Of`'un `Result<Money>` donmesi ama hep basarili olmasi

Simdilik `Money.Of` her zaman basarili donuyor (null currency exception atiyor, gecersiz amount durumu yok). Ama donus tipi `Result<Money>` cunku ileride ek validasyonlar gelebilir (ornegin tutara ust sinir). API'yi simdi `Money` dondurseydin ve sonra validasyon eklesen, tum cagiran kodlari degistirmen gerekirdi. Bu, API'nin ileriye donuk uyumlulugunun korunmasi.

### `decimal` precision'in domain'de kontrol edilmesi

`Money` 4 ondalik, `Quantity` 6 ondalik basamaga factory'de yuvarliyor. Bu, veritabani ile domain arasindaki tutarsizligi onler. Veritabani `decimal(18,4)` olarak tanimlandiginda 4'ten fazla ondalik basamagi sessizce keser. Eger domain tarafinda 10.12345 degerini saklayip veritabanina 10.1235 olarak yazilsaydi, read-after-write tutarsizligi olusurdu. Factory'de yuvarlayarak, domain'deki deger her zaman veritabanindaki degerle ayni olur.

### `Error` tipinin CA1716 ile catismasi

`Error`, C# ve Visual Basic'te anahtar kelime olarak kullanildigi icin CA1716 analyzer kuralini tetikler. Tipi `DomainError` olarak adlandirmak kuraldan kurtarirdi ama DDD literaturunde ve ekosisteminde `Error` standart isimdir. Kural, `.editorconfig`'te sadece o dosya icin kapatildi -- proje genelinde degil. Bu, kuraldan kacinmanin en dar scope'lu yolu.

## Farkli dusundugum yer

`Money.Multiply` metodu `Money` donuyor ama `Money.Add` ve `Money.Subtract` `Result<Money>` donuyor. Asimetri mantikli (carpma para birimi uyusmazligi uretemiyor) ama cagiranin "bu metod neden duz deger donuyor, digerleri neden Result donuyor" diye durmasi gerekiyor. Bir alternatif, carpmanin da `Result<Money>` donmesiydi -- teknik olarak gereksiz ama API tutarliligi acisindan daha az surpriz. `Quantity`'de de benzer bir asimetri var: `Add` duz `Quantity` donuyor, `Subtract` ve `Multiply` `Result<Quantity>` donuyor. Burada matematiksel garanti daha guclu (iki pozitifin toplami pozitiftir) o yuzden kabul edilebilir, ama `Money.Multiply` icin "carpim overflow yapabilir mi" sorusu acik kaliyor -- `decimal` cok buyuk degerlerle overflow verir ve bu simdiki haliyle `OverflowException` olarak patlar, `Result` olarak donmez.

## Kendini sina

1. `Currency` neden `sealed record` olarak tanimlandi? `sealed` kaldirilsa ne degisir?

2. `Quantity.Of(-1m)` cagirildiginda ne olur? Ayni cagriyi `Money.Of(-1m, Currency.TRY)` ile yapsan sonuc nasil farklidir ve bu fark neden var?

3. `Entity<TId>.Equals` metodundaki `GetType()` kontrolu kaldirilirsa, hangi somut senaryo yanlis sonuc uretir?

4. `AggregateRoot.DomainEvents` property'si neden `_domainEvents`'i dogrudan `IReadOnlyCollection<IDomainEvent>` olarak dondurmek yerine `.AsReadOnly()` kullaniyor?

5. `Quantity` neden `readonly record struct` ama `Money` neden `sealed record` (sinif)? Ikisi de value object olduguna gore neden ikisi de ayni sekilde tanimlanmadi?

6. `Money.Of` simdi her zaman basarili donuyor, o zaman neden `Result<Money>` donuyor da dogrudan `Money` donmuyor? Bu kararin trade-off'u nedir?

7. `Result<T>` constructor'i `internal` olarak isaretlenmis. Bu `public` yapilsaydi hangi garanti kirilirdi ve bu kirilmayi gosteren bir kod ornegi yazabilir misin?

8. `Money.Multiply(decimal factor)` `decimal.MaxValue` yakininda bir degerle cagirilsa ne olur? Bu davranis `Result<T>` pattern'inin amacina uygun mu?
