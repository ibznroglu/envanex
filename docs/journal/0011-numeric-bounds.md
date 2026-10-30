# 0011 — fix(domain): kalıcı sayısal alanlara sütun sınırı ve negatif liste fiyatının reddi

Dal `fix/numeric-bounds`, HEAD `751053c`. Plan: `thoughts/shared/plans/2026-10-24_numeric-bounds.md`.
Bu PR'ın ADR'ı yok (planın D4 kararı); kural `docs/roadmap.md`'de.

## Ne yaptık

Bu PR'dan önce `Products.ListPrice_Amount` (`decimal(18,4)`), `Products.ReorderPoint` ve
`UnitOfMeasures.ConversionFactor` (ikisi de `decimal(18,6)`) sütunlarına yazılacak değerler için
hiçbir katmanda üst sınır yoktu; türetilmiş bir birimin dönüşüm katsayısı 6'dan fazla ondalık
basamak taşıyabiliyordu ve negatif bir liste fiyatı yalnızca validator'a takılıyor, domain'den
geçebiliyordu. Böyle bir değer `SaveChanges`'a ulaştığında SqlClient'ın ve SQL Server'ın ne
yaptığını araştırma belirleyemedi (UNVERIFIED) ve bu PR bunu ölçmedi (D5). Artık `Product` ve
`UnitOfMeasure` her sütunun en büyük değerini public bir sabit olarak taşıyor; aldığı değeri
`Create`'te, `Update`'te ve artık `Result` döndüren `UpdatePrice`'ta, hiçbir atamadan önce kontrol
ediyor ve sınır dışı bir değeri `Result.Failure` ile reddediyor. Validator'lar aynı sınırları ham
istek değeri üzerinde, dört yeni hata kodu ve Türkçe mesajlarıyla, alan düzeyinde bir 400 olarak
uyguluyor; şema testleri de sabitleri canlı sütunun precision ve scale'ine bağlıyor. Şema,
migration ve EF konfigürasyonu değişmedi, `src/Envanex.Infrastructure/` altında tek dosya
değişmedi ve kural bir ADR yerine `docs/roadmap.md`'ye yazıldı.

## Yeni giren teknolojiler

Bu PR'a yeni bir paket girmedi; `Directory.Packages.props` değişmedi. Aşağıdaki ikisi zaten kurulu
paketlerin bu repoda ilk kez kullanılan parçaları.

### xUnit `TheoryData<decimal>` ve `[MemberData]`

- **Ne işe yarar:** Bir `[Theory]` testinin girdi satırlarını bir attribute'tan değil, sınıftaki
  `public static` bir property'den alır. Jest'teki `test.each([...])` ile aynı fikir: tek test
  gövdesi, birçok girdi. Plan her decimal satırın ayrı bir test vakası olarak sayılacağını varsaydı
  ve gözlenen toplamların kaydedilmesini istedi.
- **Bu projede nerede:** `tests/Envanex.Domain.Tests/Aggregates/UnitOfMeasures/UnitOfMeasureTests.cs`,
  `tests/Envanex.Application.Tests/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidatorTests.cs`,
  `tests/Envanex.Application.Tests/Products/Validators/CreateProductCommandValidatorTests.cs`,
  `tests/Envanex.Application.Tests/Products/Validators/UpdateProductCommandValidatorTests.cs`,
  `tests/Envanex.IntegrationTests/Persistence/ConversionFactorPersistenceTests.cs`,
  `MoneyPersistenceTests.cs`, `QuantityPersistenceTests.cs` ve
  `tests/Envanex.IntegrationTests/Api/UnitOfMeasuresApiTests.cs`. Repodaki bütün
  `TheoryData<decimal>` property'leri bu PR'dan geliyor; `[MemberData]`'nın kendisi önceden vardı
  (`ResultExtensionsTests`, `ProductsDatasourceTests`).
- **Alternatifi neydi:** `[InlineData(...)]`. C#'ta bir attribute argümanı yalnızca belirli sabit
  türlerden olabilir ve `decimal` bunlardan biri değil; `[InlineData(999999999999.999999m)]`
  derlenmez. Değeri `double` olarak yazıp testte `decimal`'a çevirmek derlenir, ama `double` ikili
  (binary) bir kayan noktalı sayıdır ve yaklaşık 15-17 anlamlı basamak taşır: 18 anlamlı basamaklı
  `999999999999.999999`'u tam taşıyamaz, yani test sınırın kendisini değil yakınındaki bir değeri
  sınardı. Değeri string verip testte `decimal.Parse` çağırmak da çalışırdı; her teste bir parse
  adımı ve bir kültür (`CultureInfo`) sorusu eklerdi. Plan repodaki `[MemberData]` örneğini izledi.
  Bilinen bir sınır var: xUnit v2'nin bir theory satırındaki decimal'ın scale'ini koruyup
  korumadığı bu PR'da doğrulanmadı ("Dikkat edilen tuzaklar"a bak).
- **Nerede okunur:** https://learn.microsoft.com/dotnet/core/testing/unit-testing-csharp-with-xunit
  ve attribute argüman türleri için
  https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/attributes

### FluentValidation `Must` (predicate validator) ve `LessThanOrEqualTo`

- **Ne işe yarar:** `Must(value => ...)` bir alana keyfi bir `bool` koşulu bağlar; koşul `false`
  dönerse o kural bir hata üretir. `LessThanOrEqualTo(x)` hazır bir karşılaştırma kuralıdır. İkisi
  de `.WithErrorCode(...)` ile projenin hata koduna bağlanır. Zod'daki `.refine(...)` ve `.max(...)`
  ikilisine benzer.
- **Bu projede nerede:** `src/Envanex.Application/UnitOfMeasures/Validators/CreateUnitOfMeasureCommandValidator.cs`
  (`Must` ile 6 basamak kontrolü, `LessThanOrEqualTo` ile üst sınır),
  `src/Envanex.Application/Products/Validators/CreateProductCommandValidator.cs` ve
  `UpdateProductCommandValidator.cs` (`LessThanOrEqualTo`). `src/` içindeki tek `.Must(` bu PR'ın.
- **Alternatifi neydi:** Koşulu ayrı bir helper metoda ya da özel bir `PropertyValidator` sınıfına
  koymak. Plan lambda'yı satır içinde istedi: kuralı silen mutasyon (MU1) geride kullanılmayan bir
  üye bırakmasın. Bir başka aday FluentValidation'ın hazır `PrecisionScale(precision, scale,
  ignoreTrailingZeros)` validator'ıydı; araştırma onu değerlendirmedi ve FluentValidation 12.1.1'deki
  tam davranışını ben de doğrulamadım. Tek bir kural olduğu için tek bir kod üretirdi; plan ise iki
  ayrı kod (`TooPrecise`, `TooLarge`) ve iki mesajın birlikte gösterilmesini istiyordu. Ayrıca
  domain'deki `Math.Round` kontrolüyle her girdide aynı kararı verdiğinin ayrıca kanıtlanması
  gerekirdi.
- **Nerede okunur:** https://docs.fluentvalidation.net/en/latest/built-in-validators.html

## Kavramlar

### C# `decimal` ve scale

`decimal` 128 bitlik, **onluk** (base-10) bir sayı türü: `0.1`'i tam olarak tutar. JavaScript'in
`number`'ı ise ikili bir `double`'dır ve tutamaz; `0.1 + 0.2 !== 0.3` oradan gelir. Bir `decimal`
değerin yanında bir de **scale** (virgülden sonraki basamak sayısı) taşınır: `1.5m` ile
`1.500000000m` değerce eşittir (`==` true döner) ama ilkinin scale'i 1, ikincisininki 9'dur. Bu
PR'daki bütün kontroller ve assertion'lar değeri karşılaştırır, scale'e bakmaz.

### SQL Server `decimal(p, s)`: precision ve scale

`decimal(18,4)` toplam 18 basamak demek, bunların 4'ü virgülden sonra; virgülden önce en fazla 14
basamak kalır. Bu yüzden `ListPrice_Amount`'un en büyük değeri `99999999999999.9999` (planda M4),
`decimal(18,6)` sütunlarınınki `999999999999.999999` (M6). `SchemaPrecisionTests.ColumnMaximum` bu
sayıyı sütunun precision ve scale'inden, yalnızca `decimal` aritmetiğiyle hesaplıyor.

### Kalıcı alan ve ara değer

Kalıcı alan (persisted field), bir sütuna yazılan aggregate property'si: `Product.ListPrice.Amount`,
`Product.ReorderPoint.Value`, `UnitOfMeasure.ConversionFactor`. Ara değer ise bir hesaplamanın
içinde yaşayan ve hiçbir sütuna yazılmayan bir `Money` ya da `Quantity`. Bu PR sınırı yalnızca
kalıcı alanlara koydu (D1, araştırmadaki A2 seçeneği); `Money` ve `Quantity` olduğu gibi kaldı.

### Validator katmanı, domain katmanı ve "undecorated" handler

Bir HTTP isteği controller'dan bir `ICommandHandler<...>`'a gider. DI'da bu arayüz bir
`ValidationDecorator` ile sarılı: önce FluentValidation validator'ları çalışır, hepsi geçerse asıl
handler çağrılır (`src/Envanex.Application/DependencyInjection.cs`). Aynı dosya somut handler'ları
da (`CreateProductCommandHandler` vb.) kendi başlarına kaydediyor; somut türü doğrudan çözen ya da
testte `new` ile kuran kod validator'ı atlar. Planın "undecorated path" dediği bu. `Product.UpdatePrice`'ın
ise hiç handler'ı ya da endpoint'i yok. Domain kontrolleri bu yolları da kapsasın diye aggregate'e
kondu.

### Ham değer ve yuvarlanmış değer (D2)

Validator, komuttaki `decimal`'ı istemcinin gönderdiği haliyle (ham) görür. Product handler'ları
değeri `Money.Of` (4 basamak) ve `Quantity.Of` (6 basamak) ile yuvarlar ve `Product`'a öyle verir;
yani `Product` ham değeri hiç görmez. `UnitOfMeasure.Create` ise faktörü yazıldığı gibi alır,
arada yuvarlama yok.

### Banker's rounding: `MidpointRounding.ToEven`

`Math.Round(x, 4, MidpointRounding.ToEven)` x'i 4 basamağa yuvarlar ve tam ortadaki (atılan kısmı
tam 5 olan) bir değeri, son tutulan basamağı **çift** yapan komşuya götürür. JavaScript'in
`Math.round`'u (tamsayıya yuvarlar) ortadaki değeri her zaman +∞ yönüne götürür; bu başka bir
kural. Bu PR'ın kritik örneği: `99999999999999.99995`'te son tutulan basamak 9 (tek), bu yüzden
değer `100000000000000.0000`'a çıkar ve sütun sınırını aşar. `99999999999999.99994` ise M4'e iner.

### Temsil edilebilirlik kontrolü

`conversionFactor != Math.Round(conversionFactor, 6, MidpointRounding.ToEven)` şunu sorar: bu
sayının 6. ondalık basamaktan sonra sıfırdan farklı bir basamağı var mı? Karşılaştırma değer
üzerinden olduğu için `1.500000000` geçer, çünkü yuvarlanmış hali `1.5`'e değerce eşit. Kontrol
değeri değiştirmez, yalnızca karar verir; faktör kurucuya yazıldığı gibi gider. Aynı ifade
`UnitOfMeasure.cs`'te ve validator'ın `Must` lambda'sında ayrı ayrı yazılı.

### Negatif sıfır

`decimal` sıfır değerinde bile bir işaret biti taşıyabilir; bu "negatif sıfır" sayısal olarak
sıfıra eşittir. `listPrice.Amount < 0m` sayısal bir karşılaştırma olduğu için onu sıfır sayar;
`decimal.IsNegative` ise işaret bitine bakar. `Math.Round`'un bu PR'daki girdilerde negatif sıfır
üretip üretmediği UNVERIFIED; plan bunu araştırmadı ve D2 bir işaret biti kuralı istemedi.

### Kontrol sırası: ilk hata kazanır

Bir aggregate metodu kontrollerini sırayla çalıştırır ve ilk başarısız kontrolde
`Result.Failure` ile döner; çağıran yalnızca o tek hatayı görür. İki kural birden ihlal edildiğinde
hangi kodun döneceğini bu yüzden sıra belirler. D2 türetilmiş faktör için sırayı sabitledi:
pozitiflik (`ConversionFactorMustBePositive`), sonra 6 basamakta temsil edilebilirlik
(`ConversionFactorTooPrecise`), sonra üst sınır (`ConversionFactorTooLarge`). Product'ta sıra:
negatif fiyat, fiyat üst sınırı, sipariş noktası üst sınırı.

### Validator'ın bütün hataları raporlaması

`src/` içinde hiçbir FluentValidation cascade mode ayarı yok ve validator bir alan için ihlal edilen
bütün kuralları tanım sırasıyla döndürüyor. Domain ilk hatada dururken validator bir liste
döndürür: U-V4 `1000000000000.0000001m` için iki kod beklerken, U-D4 aynı değer için tek kod
bekliyor. Okuma: https://docs.fluentvalidation.net/en/latest/cascade.html

### `ValidationProblemDetails` ve düz `ProblemDetails`

İkisi de RFC 9457 hata gövdesi. Bir validator hatası `title: "Validation Failed"` ve alan adına
göre gruplanmış bir `errors` nesnesiyle döner; domain'den gelen bir `Result.Failure` ise
`title: "Bad Request"` ve tek bir `detail` ile. Hangi katmanın önce cevap verdiği, istemcinin
göreceği gövdenin biçimini belirler. `ReadFieldValidationMessagesAsync` helper'ı bu yüzden önce
status'u, sonra `title`'ı, sonra `errors[alan]`'ı kontrol ediyor; status'u gövdeyi okumadan önce
kontrol ettiği için JSON olmayan bir 500, bir parse hatasıyla değil okunur bir Shouldly
assertion'ıyla kırılır.

### Kısmi güncelleme (partial update)

Bir `Update` metodu önce `Name`'i atayıp sonra fiyat kontrolünde başarısız olursa, `Result.Failure`
dönse bile nesne yarı değişmiş kalır; EF'nin change tracker'ı aynı `DbContext`'te o değişikliği
görmeye devam eder. Bu yüzden `Create`, `Update` ve `UpdatePrice`'ta bütün kontroller bütün
atamalardan önce. `ProductTests.ShouldBeUnchanged`, başarısız bir çağrıdan sonra sekiz property'nin
hiçbirinin değişmediğini doğruluyor; başarısız `Update` testleri diğer her argümanı da farklı
veriyor ki yarım kalmış bir atama gözden kaçmasın.

### `void`'den `Result`'a

`UpdatePrice` önceden `void`'di; artık sınır ihlalinde `Result.Failure`, başarıda
`Result.Success()` döndürüyor. C# bir dönüş değerinin görmezden gelinmesine izin verir; araştırma
`-warnaserror` altında da derlemenin kırılmayacağını yeşil `main`'den çıkarsadı (derleyerek
değil). Yani derleyici çağıranları sonucu kontrol etmeye zorlamıyor. Bu yüzden test tarafındaki
çağrılar elle güncellendi: `ProductTests.UpdatePrice_ShouldChangeListPrice`, `RepositoryTests`'teki
altı ve `ConcurrencyTests`'teki iki çağrı artık `IsSuccess`'i doğruluyor.

### `INFORMATION_SCHEMA.COLUMNS` ve şema testi

`INFORMATION_SCHEMA.COLUMNS`, SQL Server'ın her sütunun türünü, precision'ını ve scale'ini
listeleyen standart meta veri görünümü. `SchemaPrecisionTests` onu `SqlQueryRaw<ColumnInfo>` ile,
Testcontainers'ta migration'larla kurulmuş gerçek veritabanından okuyor ve domain sabitlerini
(`UnitOfMeasure.ConversionFactorMaxValue`, `ConversionFactorDecimalPlaces`,
`Product.ListPriceAmountMaxValue`, `ReorderPointMaxValue`) o sütundan hesaplanan değerle
karşılaştırıyor. `Envanex.Domain` hiçbir projeye referans veremediği için sabit EF
konfigürasyonundan okunamaz; aradaki bağı ancak bir test kurabilir. Okuma:
https://learn.microsoft.com/sql/relational-databases/system-information-schema-views/columns-transact-sql

### Yeni hata kodları ve iki tablo

Her hata kodunun Web katmanında iki karşılığı var: `ResultExtensions.StatusCodeMap`'te bir HTTP
status'u ve `TurkishErrorMessages.Messages`'ta kullanıcıya gösterilen metin. Bu PR'ın dört yeni
kodu, hepsi 400:

| Kod | Kullanıcıya gösterilen |
|---|---|
| `UnitOfMeasure.ConversionFactorTooPrecise` | Dönüşüm katsayısı en fazla 6 ondalık basamak içerebilir; sondaki sıfırlar sayılmaz. |
| `UnitOfMeasure.ConversionFactorTooLarge` | Dönüşüm katsayısı en fazla 999.999.999.999,999999 olabilir. |
| `Product.ListPriceAmountTooLarge` | Liste fiyatı en fazla 99.999.999.999.999,9999 olabilir. |
| `Product.ReorderPointTooLarge` | Yeniden sipariş noktası en fazla 999.999.999.999,999999 olabilir. |

`ResultMappingTests`, Domain ve Application assembly'lerindeki her `public static readonly Error`
alanını yansıma (reflection) ile bulur ve her kodun iki tabloda da olduğunu kontrol eder; yeni bir
`Error` alanı iki tabloya da satır eklenene kadar onu kırar. Bu yüzden her faz kodu ve iki tablo
satırını birlikte ekledi. `ResultMappingTests` kodun **eşlendiğini** sabitler, **hangi status'a**
eşlendiğini değil; tabloda olmayan bir kod da zaten 400'e düşüyor. Tam 400'ü U-M1 ve P-M1
(`ResultExtensionsTests`) sabitliyor. Validator kuralları kodu `.WithErrorCode(...)` ile veriyor;
ADR 0006 `.WithMessage(...)`'ı yasaklıyor ama string literal şart koşmuyor (insanın araştırmaya
düzeltmesi), bu yüzden yeni kurallar `ProductErrors.ListPriceAmountTooLarge.Code` biçiminde, eski
kurallar ise literal olarak kaldı.

### `\uXXXX` kaçışı ve kaynak biçimi

C# string literal'inde `\u0131` derleyicinin `ı`'ya çevirdiği bir kaçış dizisi: dosyada ASCII
durur, çalışma zamanında harf olur. Plan (D3), `TurkishErrorMessages.cs`'teki Product ve
UnitOfMeasure mesajlarının ve onları sabitleyen API test literal'lerinin bu biçimde, küçük harfli
hex ile yazılmasını şart koştu. `TurkishErrorMessagesTests` bilerek tersini yapıyor: beklenen
metni gerçek Türkçe harflerle yazıyor, böylece assertion'ı gönderilen kaçışların doğru harflere
çözüldüğünü kanıtlıyor.

### Mutasyon testi ve maskeleme

Mutasyon testi, üretim koduna bilerek bir hata sokup (bir `if` bloğunu silmek, bir sabiti
değiştirmek, iki kontrolün yerini değiştirmek) en az bir testin kendi Shouldly assertion'ında
kırmızıya döndüğünü görmektir; "geçen test" ile "koruyan test" arasındaki farkı ölçer. Maskeleme,
bir katmandaki hatanın başka bir katman tarafından gizlenmesidir: API üzerinden validator domain'den
önce cevap verdiği için, bir domain kontrolü silinse de API testleri yeşil kalır.

### EF Core complex type ve EF Core 10'un opsiyonel complex type'ları

`Money`, EF Core'da ayrı bir tablo değil, `Products` tablosunun içinde `ListPrice_Amount` gibi
sütunlara eşlenen bir complex type. Roadmap'teki eski cümle "complex type'lar EF Core'da null
olamaz" diyordu; Microsoft Learn'ün "What's New in EF Core 10" sayfası ise EF 10'un, complex type'ın
en az bir zorunlu property'si olması şartıyla, opsiyonel complex type desteklediğini söylüyor.
Faz 4 roadmap'i buna göre düzeltti; `Money`'nin zorunlu kalması kararı değişmedi ve ADR 0003
tarihsel kayıt olarak düzenlenmedi. Okuma:
https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew

## Komutlar ve ne yaptıkları

| Komut | Ne yapar |
|---|---|
| `dotnet build -warnaserror` | Solution'ı derler; `-warnaserror` her uyarıyı hataya çevirir. `Directory.Build.props` de `TreatWarningsAsErrors` açıyor. |
| `dotnet test tests/Envanex.Domain.Tests --filter FullyQualifiedName~Envanex.Domain.Tests.Aggregates.UnitOfMeasures.UnitOfMeasureTests` | Yalnızca o test projesini çalıştırır ve yalnızca tam adı (namespace + sınıf + metot) verilen metni **içeren** testleri seçer. Filtrede `~` "içerir", `=` "tam eşit", `\|` "veya", `&` "ve" demek. |
| `dotnet test` | Repo kökünde üç test projesinin hepsini çalıştırır. Entegrasyon testleri Docker ister: Testcontainers bir SQL Server 2022 container'ı açar. Her proje bir özet satırı basar (`Passed! - Failed: 0, Passed: N, ... Duration: ... - Envanex.IntegrationTests.dll`); suite süresi ölçümleri bu satırdaki süreyi kullanıyor. |
| `dotnet format --verify-no-changes` | Kodu `.editorconfig`'e göre kontrol eder ama değiştirmez; bir şeyi değiştirmesi gerekseydi sıfırdan farklı bir çıkış koduyla biter. CRLF satır sonlarını da yakalar. |
| `git status --short` | İki sütunlu kısa durum: ` M` çalışma ağacında değişmiş (stage'lenmemiş), `??` izlenmeyen dosya. Coder her fazın dosya listesini bununla doğruladı. |
| `git diff --unified=0 HEAD -- docs/roadmap.md` | Bağlam satırı olmadan diff. `@@ -60,0 +61 @@` başlığı "eski 60. satırdan sonra 0 satır silindi, yeni 61. satır eklendi" demek. Faz 4 tam dört hunk bekledi. |
| `git diff --check HEAD -- docs/roadmap.md` | Satır sonu boşluklarını ve conflict işaretlerini raporlar; çıktı yoksa temiz. |
| `git ls-files --eol docs/roadmap.md` | Dosyanın index'teki (`i/`) ve çalışma ağacındaki (`w/`) satır sonunu gösterir; `w/lf` beklenir. |
| `head -c 3 docs/roadmap.md \| od -An -tx1` | İlk üç baytı hex olarak basar. UTF-8 BOM `ef bb bf` olurdu; `23 20 52` (`# R`) BOM olmadığını gösterir. |
| `bash .claude/skills/mutate/scripts/mutate-clean.sh <ID> <dosya>...` | Verilen dosyaların hem çalışma ağacında hem index'te `HEAD` ile aynı olduğunu kontrol eder ve kaydı `TestResults/fix-numeric-bounds/mutations/<ID>.txt`'ye ekler. Çıkış 0: temiz. Her mutasyondan önce ve geri alındıktan sonra çalışır. |
| `bash .claude/skills/mutate/scripts/mutate-run.sh <ID> <dosya> dotnet "<filtre>" [<proje>]` | Önce `git diff HEAD -- <dosya>`'yı (mutasyonun kendisini), sonra testin çıktısını ve `exit=` satırını aynı log'a yazar. Beşinci argüman çalıştırmayı tek projeye daraltır; yoksa repo kökünde üç projede koşar. Çağrı başına tek dosyanın diff'ini kaydettiği için iki dosyalı satırlar (MU7, MP15) aynı çift mutasyonlu ağaçta iki kez çağrıldı. Betiğin `dotnet` modu ve `mutate-clean.sh`'ın çok dosyalı kullanımı bu PR'da ilk kez kullanıldı. |
| Grep: `"(Product\|UnitOfMeasure)\.[^"]*"\] = "[^"]*[^\x00-\x7F]` | `TurkishErrorMessages.cs`'te anahtarı `Product.` ya da `UnitOfMeasure.` ile başlayan ve değeri ASCII dışı bir karakter içeren girdiyi bulur. `[^\x00-\x7F]` "ASCII olmayan herhangi bir karakter" demek. Beklenen sonuç: eşleşme yok. |
| `bash .claude/skills/gate/scripts/gate.sh` | Gate betiği. On adımı sırayla çalıştırır: araç kontrolü, `git status --short`, base-check (`main` ve `origin/main`'in `HEAD` ile aynı merge-base'i vermesi), `git diff --name-only main...HEAD`, `git log --oneline main..HEAD`, `docker compose ps`, hook testleri (`node --test`), `dotnet format --verify-no-changes`, `dotnet build -warnaserror` ve `dotnet test`. Her adımın ham çıktısını ve `exit=` satırını `TestResults/fix-numeric-bounds/gates.txt`'ye yazar; son satır `gate-exit=0 failed=` ise zorunlu adımların hepsi geçmiştir. Stdout'u bu kayıttan hesaplanan bir özettir. Tester her fazda bu komutu doğrudan çalıştırdı. `/gate` aynı betiği çalıştıran skill'dir; PR sonunda onu insan çağırır. |

## Dikkat edilen tuzaklar

### Sınırı yalnızca validator'a koymak

Araştırma "yalnızca validator" seçeneğinin yetmediğini gösterdi: somut handler'lar ve `UpdatePrice`
validator'dan geçmiyor. Bu yüzden her sınır iki yerde var, domain'de ve validator'da. Handler
testleri (U-H1, U-H2, P-HC1..P-HC3, P-HU1..P-HU3) handler'ı dekoratörsüz kurup hatanın döndüğünü ve
`SaveChangesCalled`'ın 0 kaldığını doğruluyor. Bedeli: aynı kural iki kez yazılı ve validator her
zaman en az domain kadar katı olduğu için, bu alanlar için domain'in kodu API'den kullanıcıya hiç
ulaşmıyor.

### Yuvarlamanın sınırı aşması ve bilerek bırakılan asimetri

`Product` yuvarlanmış değeri gördüğü için, yukarı yuvarlanıp `100000000000000.0000` olan
`99999999999999.99995` domain'de `ListPriceAmountTooLarge` ile reddediliyor; `99999999999999.99994`
ise M4 olarak kabul ediliyor ve P-P1 o değerin veritabanından M4 olarak geri okunduğunu gösteriyor.
Validator ikisini de ham haliyle reddediyor (P-VC2, P-VU2). Aynı ikilik alt sınırda da var: domain
`[-0.00005, 0)` aralığındaki bir fiyatı sıfır olarak kabul ediyor (P-D5, P-D13, P-D21), validator'ın
ham `>= 0` kuralı reddediyor (P-VC3, P-VU3). Plan bunu bilerek seçti (D2) ve `ReorderPoint`'e
taşımadı, çünkü `Quantity.Of` negatif bir değeri yuvarlamadan önce reddediyor. Verilen şey: domain
kuralı aslında "liste fiyatı ≥ 0" değil, "yuvarlanmış liste fiyatı ≥ 0".

### Kontrol sırasını şansa bırakmak

D2 sırayı sabitledi ve iki test onu kilitliyor: U-D4 (hem 6 basamağı aşan hem sınırın üstündeki
değer `TooPrecise` döner) ve U-D5 (negatif ve 6 basamağı aşan değer `MustBePositive` döner).
Planın Observed sütununa göre MU5 (üst sınır kontrolünü temsil edilebilirlik kontrolünün üstüne
taşımak) yalnızca U-D4'ü, MU6 (temsil edilebilirliği pozitifliğin üstüne taşımak) yalnızca U-D5'i
kırdı. Bu testler olmasa, kontrolleri "toparlayan" biri kullanıcıya gösterilen mesajı fark
etmeden değiştirebilirdi.

### Maskeleme: "API testleri yeşil, demek ki korunuyoruz"

Planın Observed sütununa göre (coder'ın mutasyon log'larından doldurduğu kayıt) MU3 ve MU4
(UnitOfMeasure'daki yeni domain kontrollerini silmek) ile MP5-MP10 (`Product.Create` ve
`Product.Update`'teki yeni kontrolleri tek tek silmek) yalnızca domain ve handler testlerini kırdı;
hiçbir API testi kırılmadı. Plan bunu öyle bekliyordu, çünkü API'de validator önce cevap veriyor.
Uç örnek MP11 ve MP12: `UpdatePrice`'ın handler'ı ve endpoint'i yok, kontrollerini yalnızca P-D19
ve P-D20 koruyor.

### Düzeltme öncesi davranışı olgu gibi yazmak (D5)

Araştırma, sınır dışı bir değerin SqlClient'ta yuvarlanacağını mı, kesileceğini mi, yoksa bir
istisnaya mı yol açacağını belirleyemedi: EF Core 5'in breaking-change notu ile SqlClient'ın
`SqlParameter` dokümanı yuvarlama ve kesme konusunda birbiriyle çelişiyordu, ve doküman repoda
çözülen 6.1.6'yı değil 7.1 sürümünü anlatıyordu. Plan bir karakterizasyon fazı koymadı. Bunun
bedeli: düzeltme bu yolları kapattığı için eski davranış artık güncel kodda ölçülemez; ölçmek için
düzeltmeden önceki bir commit'e, örneğin `56fd6d4`'e dönmek gerekir. İki mutasyon satırı HTTP
üzerinden bir şey gözledi; ama ikisi de kontrolleri silinmiş **düzeltme sonrası** kodda koştu ve
`main`'in karakterizasyonu değiller:

- MU7 mutasyonu altında gözlendi: validator'daki ve domain'deki `TooPrecise` kontrolleri birlikte
  silinince U-A1 (`0.0000001m`) `BadRequest` beklerken `Created` aldı; log'da Shouldly
  assertion'larından başka istisna yoktu.
- MP15 mutasyonu altında gözlendi: create validator'ındaki ve `Product.Create`'teki liste fiyatı üst
  sınırı birlikte silinince P-A1 (`100000000000000m`) `BadRequest` beklerken `InternalServerError`,
  P-A2 (`99999999999999.99994m`) `Created` aldı; log'da Shouldly assertion'larından başka istisna
  yoktu.

Bu gözlemlerin nedeni bu PR'da araştırılmadı.

### Sondaki sıfırlar ve theory satırları

D2 sondaki sıfırları olan bir faktörü (`1.500000000`) kabul ediyor, çünkü kontrol değer
karşılaştırıyor. U-D1 ve U-V1'de `0.0000010m` ve `1.500000000m`, U-A4'te yalnızca `1.500000000m` satırı var; ama xUnit'in
bir theory satırındaki decimal'ın scale'ini test metoduna koruyarak taşıyıp taşımadığı Faz 1 code
review'ında N1 olarak UNVERIFIED kaldı ve çözülmedi. Bu yüzden o satırları sondaki sıfırlar
hakkında kanıt sayma. Böyle bir faktörün kalıcı olup olmadığı konusunda bu PR'ın tek gözlemi U-P2:
bir `[Fact]`, `1.500000000m` literal'ini `UnitOfMeasure.Create` ve bir `DbContext` ile kaydediyor,
üçüncü ve taze bir context'te okuyor ve okunan değerin `1.5m`'ye değerce eşit olduğunu iddia
ediyor. U-P2 tester'ın gate koşusundaki `dotnet test`'in parçası; ben testleri çalıştırmadım. Bu,
kaydın hata vermediğini ve değerin değerce korunduğunu gösterir; scale hakkında bir şey söylemez.

### Sabitin sütundan kayması

Yeni sabitler elle yazıldı; domain onları EF konfigürasyonundan okuyamaz. Biri sabiti değiştirir de
sütunu değiştirmezse, domain sütunun kabul edemeyeceği bir değeri kabul eder ya da kabul
edebileceğini reddeder. Bağı şema testleri kuruyor (U-S1..U-S3, P-S1, P-S2). Planın Observed
sütununa göre MU8 (`ConversionFactorMaxValue`'yu `999999999999.99999m` yapmak) U-S2'yi, MU9
(`ConversionFactorDecimalPlaces`'i 5 yapmak) U-S3'ü kırdı.

### Türkçe harflerin kaynakta kaçışsız kalması

D3: `TurkishErrorMessages.cs`'teki Product ve UnitOfMeasure girdilerinde ve onları sabitleyen API
test literal'lerinde her Türkçe harf `\u` artı dört küçük hex basamak olarak yazılır. Plan her fazda
bunun için Grep kontrolleri tanımladı (ASCII dışı karakter arayan desenler). İnsanın gözlemi (ham
çıktısı repoda değil): Faz 1-3'te, düzenleme denemelerinden sonra, D3'ün kaçış istediği kaynak
satırlarında gerçek Türkçe harfler görüldü. Harflerin hangi aşamada ortaya çıktığı doğrulanmadı.
Planın Grep kontrolleri her vakayı yakaladı; son kaynak biçimi onarıldı ve kontrol edildi. Faz 1-3
code review'ları son durumu satır satır, çapalı (anchored) Grep desenleriyle de karşılaştırdı.

### `ListPriceAmountNegative`'i taşırken metni bozmak

`Product` artık `ListPriceAmountNegative`'i kendisi döndürdüğü için bu kod, iki tabloda da "Product
validator" grubundan "Product domain" grubuna taşındı. Plan Türkçe girdinin kaynakta bayt bayt aynı
kalmasını istedi: bir Grep kontrolü kaynağı, P-T1'in `ListPriceAmountNegative` satırı da çalışma
zamanındaki metni sabitliyor. Bu metni sabitleyen başka bir test yok.

### Entegrasyon suite'inin süresi

ADR 0007 entegrasyon suite'i için 60 saniyelik bir karar çizgisi koyuyor; plana göre Faz 1-3 toplam
35 entegrasyon testi ekledi. Roadmap'in Faz 4'te güncellenen satırı 2026-10-23'te `56fd6d4`'te
yapılan ölçümü kaydediyor (`1 m 2 s`, `55 s`, `52 s`; medyan 55 s) ve bir takip kuralı koyuyor: bir
`/gate` kaydı suite'i `1 m` ya da üstünde gösterirse üç koşunun medyanı yeniden ölçülür, medyan 60 s
ya da üstündeyse container'ın açılış süresi ayrıca ölçülür. İnsanın gözlemi (ham çıktısı repoda
değil): Faz 3'ün gate kaydı `Envanex.IntegrationTests.dll` için `1 m 6 s` gösterdi. 2026-10-29'da,
`316f2ba`'da, repo kökünde üç `dotnet test` koşusu bu suite için `1 m 1 s`, `55 s` ve `52 s` verdi,
her birinde 427 test: medyan 55 s, 60 s çizgisinin altında. Bu ölçüm roadmap'te yok; roadmap satırı
yalnızca 2026-10-23 ölçümünü taşıyor.

### Satır sonları ve BOM

Repo yalnızca LF istiyor (`.gitattributes`). Faz 1-3, değiştirdiği dosyalarda `\r` arayan bir Grep
kontrolü taşıdı; Faz 4 satır sonlarını `git diff --check` ve `git ls-files --eol` ile denetledi.
Planın tek BOM kontrolü `head -c 3 docs/roadmap.md | od -An -tx1`. Bu dalda oluşturulan tek
kaynak dosya `ConversionFactorPersistenceTests.cs` için plan bir BOM kontrolü tanımlamadı; Faz 1
code review'ı bunun için `head -c 3` önerdi, ama bu bir plan adımı değildi. 2026-10-30'da bu
dosyada çalıştırılan `head -c 3 <dosya> | od -An -tx1` `75 73 69` verdi, yani dosyada BOM yok.
`.gitattributes`'taki açıklamaya göre bir C# kaynak dosyasındaki CRLF, kod hiç değişmemiş olsa
bile `dotnet format --verify-no-changes`'i ve dolayısıyla gate'i kırar; BOM'u yakalayıp
yakalamadığı doğrulanmadı. `dotnet format` solution'daki projelerin dosyalarını denetler,
`docs/` altındaki Markdown dosyalarını görmez; roadmap için `git ls-files --eol` ve
`head -c 3 | od` kontrolleri bu boşluğu kapatıyor.

## Farklı düşündüğüm yer

- **ADR yazılmaması (D4).** Roadmap'e eklenen madde "PR 8 ve PR 9 her yeni kalıcı alan için buna
  uyar" diyor; bu, gelecekteki PR'ları bağlayan bir mimari karar ve `CLAUDE.md` mimari karar veren
  her PR'ın `docs/adr/` altına bir dosya eklemesini istiyor. Roadmap maddesi bir sonraki roadmap
  güncellemesinde kolayca kaybolur; A1 ile A2 arasındaki tartışma ise yalnızca `thoughts/` altındaki
  araştırma dosyasında. Kuralı zorlayan bir test de yok: PR 8'de sınırsız eklenen yeni bir kalıcı
  `Quantity` alanı hiçbir testi kırmaz. Ben kısa bir ADR isterdim; onu insan yazar.
- **Temsil edilebilirlik ifadesi iki kopyada.** `UnitOfMeasure.Create` ve validator'ın `Must`
  lambda'sı aynı `Math.Round(..., 6, MidpointRounding.ToEven)` karşılaştırmasını ayrı ayrı yazıyor.
  Planın gerekçesi, MU1'in validator'da kullanılmayan bir helper bırakmamasıydı; ama `UnitOfMeasure`
  üzerinde public static bir yüklem (ör. `IsRepresentableConversionFactor(decimal)`) MU1'den sonra
  da domain tarafından kullanılmaya devam ederdi ve kural tek yerde tanımlı olurdu. Bedeli domain'in
  public yüzeyine bir metot eklemek; ben onu öderdim.
- **Şema testlerinde tekrar.** `SchemaPrecisionTests`'e eklenen beş test aynı on beş satırlık sorgu
  bloğunu tekrar ediyor, üçü aynı sütunu (`UnitOfMeasures.ConversionFactor`) okuyor. Plan dosyanın
  mevcut stilini (her testin kendi satır içi sorgusu) istedi. Ben `ReadColumnAsync(string table,
  string column)` gibi private bir helper'a çekerdim: PR 8 ve PR 9 her yeni kalıcı alan için bu
  dosyaya bir test daha ekleyecek ve tekrar onlarla büyüyecek.

## Kendini sına

1. `CreateUnitOfMeasureCommandValidator`, türetilmiş bir birim için `ConversionFactor = 0m`
   aldığında `ConversionFactor` alanı için hangi hata kodlarını, hangi sırayla döndürür? Aynı değeri
   `UnitOfMeasure.Create` aldığında ne döner? Üç kuralın her birini bu değerle tek tek çalıştırarak
   gerekçelendir.
2. `CreateProductCommand`'da `ListPriceAmount` `-0.00006m` ve `-0.00015m` olarak gelseydi validator
   ne derdi? Validator'ı atlayan bir yolda `Money.Of` bu iki değeri neye çevirir ve `Product.Create`
   her biri için ne döndürür?
3. Biri `UnitOfMeasure.Create`'in base-unit dalına (`baseUnitId is null`) de `TooPrecise` kontrolünü
   eklese, `1.0000001m` için hangi kod döner? Cevap, yeni kontrolün `conversionFactor != 1m`
   kontrolünden önce mi sonra mı konduğuna göre değişir mi; değişiyorsa bu PR'ın hangi testi hangi
   durumda kırılır?
4. `Money.Of` bir tutarı 4 basamağa yuvarlayıp kabul ediyor; `UnitOfMeasure.Create` ise 6 basamağı
   aşan bir faktörü yuvarlamak yerine reddediyor. Faktör de yuvarlanıp kabul edilseydi `0.0000004m`
   ve `0.0000005m` gibi girdilere ne olurdu, ve bu sonuç türetilmiş bir birim için neden sorun
   olurdu? Yuvarlamanın pozitiflik kontrolünden önce mi sonra mı yapıldığı sonucu değiştirir mi?
5. Validator'daki yeni kurallar kodu `.WithErrorCode(ProductErrors.ListPriceAmountTooLarge.Code)` ile
   veriyor, ama validator testleri (U-V*, P-VC*, P-VU*) beklenen kodları
   `"Product.ListPriceAmountTooLarge"` gibi literal string'lerle karşılaştırıyor. Bu iki tercih ayrı
   ayrı hangi hatayı yakalamak için? Testler de `.Code` kullansaydı hangi değişiklik hiçbir testi
   kırmadan geçerdi?
6. `CreateUnitOfMeasureCommandValidator`'a `RuleLevelCascadeMode = CascadeMode.Stop` eklenseydi
   U-V4, U-V5 ve U-A3'e ne olurdu? Peki `ClassLevelCascadeMode = CascadeMode.Stop` eklenseydi? İki
   cevabın, her yeni kontrolün ayrı bir `RuleFor` ifadesi olmasıyla ilgisi ne?
7. Araştırma iki seçenek sundu: A1, üst sınırı `Money` ve `Quantity`'nin kendisine koymak; A2,
   yalnızca kalıcı alanlara, aggregate'lerde. İnsan A2'yi seçti (D1). A1 seçilseydi
   `Money.Multiply`, `Quantity.Add`, `QuantityConverter` ve PR 8-9'daki stok ve maliyet
   hesaplarının ara değerleri nasıl etkilenirdi, ve bu neden yeni bir ADR gerektirirdi?
8. Bir gün bir migration `Products.ListPrice_Amount`'u `decimal(19,4)`'e çıkarırsa, bu PR'ın
   eklediği ve önceden var olan testlerden hangileri kırılır, hangileri yeşil kalır? Hiçbir test
   kırılmadan eski sınırı taşımaya devam eden yerler hangileri, ve bunlardan hangisi kullanıcıya
   doğrudan görünür?
