## Surec boyunca yakalanan hatalar

### 1. Error tipinin sealed olmasi alan bazli validasyonu engelliyor

**Sorun:** Plan incelemesi sirasinda fark edildi ki, tek `Error(Code, Message)` record'u birden fazla alan hatasini tasiyamaz. PR 7'de Blazor form'lari her alan icin ayri hata mesaji bekliyacek.

**Nasil yakalandi:** Plan-reviewer fazinda, `ValidationDecorator`'in nasil coklu hata dondurecegi soruldugunda.

**Yakalanmasaydi:** `ValidationDecorator` ilk hatada durup tek hata donecekti, veya hatalari birlestirip tek mesaj yapacakti. PR 7'de form UI'i yazilirken alan bazli hata gosterimi icin buyuk bir refactoring gerekecekti.

### 2. ExtractConstraintName tum mesaji donuyordu

**Sorun:** `UnitOfWork.ExtractConstraintName` metodunun ilk versiyonu `SqlException.Message`'in tamamini `ConstraintName` olarak donuyordu. Ornek: `"Cannot insert duplicate key row in object 'dbo.Products' with unique index 'IX_Products_Code'. The duplicate key value is (PRD001)."` -- bu string ConstraintName olarak anlamsiz.

**Nasil yakalandi:** Code review sirasinda, integration test yazildiktan sonra constraint name'in bosluk icerdiginin gorulmesiyle. `UnitOfWork_SaveChangesAsync_WithDuplicateCode_ShouldExtractIndexNameNotFullMessage` testi eklendi: `ex.ConstraintName.ShouldNotContain(" ")`.

**Yakalanmasaydi:** `ConstraintName` diagnostik amaclidir ve su an akis kontrolunde kullanilmiyor. Ama gelecekte loglarda veya hata raporlarinda anlamsiz uzun string'ler gorulurdu; daha kotusu, birisi `ConstraintName`'e dayali is mantigi yazarsa yanlis sonuc verirdi.

### 3. Suffix eslestirme belirsizligi handler kayitlarinda

**Sorun:** Plan ilk versiyonunda handler'lari DI'a kaydetmek icin "sinif adinin sonuna bak, `Handler` ile bitiyorsa kaydet" gibi convention-based yaklasim dusunuldu. Ama `ActivateProductCommandHandler` ve `DeactivateProductCommandHandler` gibi isimler suffix eslestirmede belirsizlik yaratir.

**Nasil yakalandi:** Plan-reviewer fazinda, otomatik kayit mekanizmasinin `ICommandHandler<TCommand, Guid>` generic tip parametrelerini dogru esleyip esleyemeyecegi sorgulandiginda.

**Yakalanmasaydi:** Yanlis handler'in yanlis komuta baglanmasi -- activate komutu deactivate handler'a gidebilirdi. Hata runtime'da ortaya cikar ve teshisi cok zor olurdu. Explicit kayit bu riski ortadan kaldirir.

### 4. Eksik boundary ve validator testleri

**Sorun:** Faz 2'nin ilk versiyonunda bazi validator testleri eksikti -- ornegin `ListPriceCurrency` icin `Length(3)` kuralinin testi yoktu, `UpdateProductCommandValidator`'daki `RowVersion` null/empty kontrolleri test edilmemisti.

**Nasil yakalandi:** Code review sirasinda, her validator kuralinin bir teste sahip olup olmadigi sistematik olarak kontrol edildigi anda. `fix(app): add missing validator tests, boundary tests, and fix DuplicateKeyException message` commit'i bunu duzeltmek icin yazildi.

**Yakalanmasaydi:** Validator kurallari varmis gibi gorunur ama test edilmemis kurallar sessizce bozulabilirdi. Ornegin biri `Length(3)` kuralini `MaximumLength(3)` ile degistirirse (bos string kabul eder), test olmadigi icin fark edilmezdi.

### 5. FakeUnitOfWork'teki yorum hatasi ve RowVersion sira kontrolunun eksikligi

**Sorun:** `FakeUnitOfWork`'un ilk versiyonunda `RowVersionWasSetBeforeSave` mekanizmasi yoktu. Handler `SetOriginalRowVersion`'i `SaveChangesAsync`'ten sonra da cagirabilirdi ve hicbir test bunu yakalayamazdi.

**Nasil yakalandi:** `test(app): assert RowVersion is set before SaveChanges, fix fake comment` commit'inde. `FakeUnitOfWork`'e `FakeProductRepository` referansi eklendi ve `SaveChangesAsync` icinde sira kontrolu yapildi.

**Yakalanmasaydi:** Handler kodu dogru gorunur ama `SetOriginalRowVersion` ve `SaveChangesAsync` siralari bir refactoring sirasinda yer degistirirse, concurrency control sessizce devre disi kalir -- cunku OriginalValue uzerine yazma SaveChanges'tan SONRA olur ve artik etkisi yoktur.

## ADR 0005'te cevaplanmasi gereken sorular

1. MediatR yerine elle yazilmis handler arayuzlerini secmek, handler sayisi 20-30'u gectiginde hala surdurulebilir mi? Hangi esik degerinde assembly-tarama kutuphanesine (Scrutor) gecis degerlendirilmeli?

2. ValidationDecorator yalnizca command'lari sariyor. Query handler'lar icin validasyon gerekirse (ornegin sayfalama parametrelerinin sinir kontrolu) bu karari degistirmek mi yoksa query-specific bir mekanizma mi olusturmak gerekir?

3. `IQueryable<TDto>` Application'da gorunuyor ama `ToListAsync` veya `FirstOrDefaultAsync` gibi materialize islemleri Infrastructure'da degil, controller'da yapilacak. Bu, controller'in EF Core uzantisina (EntityFrameworkQueryableExtensions) dolayili bagimliligini dogurur -- bu kabul edilebilir mi?

4. UnitOfWork exception ceviri katmani sadece unique violation ve concurrency conflict'i ceviriyor. FK violation, deadlock veya timeout gibi diger veritabani hatalari icin ne yapilmali? Her birinin Application tipine cevirilmesi mi, yoksa oldugu gibi firlatilmasi mi dogru?

5. Repository arayuzleri Application'da tanimlandi ama `InternalsVisibleTo` ile integration test projesi Infrastructure'in internal siniflarini gorebiliyor. Bu, integration testlerinin Infrastructure implementasyon detaylarini test etmesi anlamina gelir -- bu kabul edilebilir mi, yoksa testler sadece Application arayuzleri uzerinden mi calismaliydi?

6. `RowVersion` shadow property'sinin handler'a kadar tasinma zinciri (DTO -> Command -> Handler -> Repository -> EF) uzun ve hataya acik. Bunun icin daha guvenli bir mekanizma var mi, yoksa `SetOriginalRowVersion` + sira testi yeterli mi?

7. Validator'lar singleton olarak kayitli ama handler'lar scoped. FluentValidation'da stateful validator'lar (injected service kullananlar) olusursa bu lifetime uyumsuzlugu sorun cikarir mi?
