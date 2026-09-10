## Surec boyunca yakalanan hatalar

### 1. Sonek eslesmesinin belirsizligi (suffix matching ambiguity)

**Sorun:** Plan ilk versiyonunda hata kodu -> HTTP status eslesmesi icin sonek tabanlı yaklasim dusunuldu: `*.NotFound` -> 404 gibi. Ama `Product.UnitOfMeasureNotFound` (FK iliskili kayit bulunamadi, 422 olmali) ile `Product.NotFound` (birincil kayit bulunamadi, 404 olmali) ayni soneki paylasiyor.

**Nasil yakalandi:** Plan inceleme fazinda, esleme tablosu tasarlanirken hata kodlarinin tam listesi cikarildiginda. Sonek eslesmesinin iki farkli anlami karistirdigi goruldu.

**Yakalanmasaydi:** `Product.UnitOfMeasureNotFound` 404 olarak donurdu. Istemci "urun bulunamadi" sanirken aslinda "olcu birimi bulunamadi" demek isteniyor. Kullanici yanlis bir hata mesaji gorur, gecersiz olcu birimi ID'sini duzeltmek yerine urunun varolmadigini dusunurdu.

### 2. Error.None sentinel'i

**Sorun:** `Error` tipinde `Error.None` sentinel degeri var (`Code = ""`, `Message = ""`). Reflection taramasi tum `public static readonly Error` alanlarini toplarken `Error.None` da esleme tablosunda bir giris beklerdi -- ama bu bir hata kodu degil, bir sentinel.

**Nasil yakalandi:** `ResultMappingTests` yazilirken, tarama mantigi `typeof(Error)` ve `typeof(ValidationError)` uzerindeki alanlari tip bazli dislamak uzere tasarlandi. Ayrica `ResultMapping_NoDomainErrorCode_ShouldBeEmpty` testi, bu dislamanin disinda kalan alanlarin bos `Code` degeri tasimadigini dogruluyor -- yani disklama mekanizmasinin gercek bir hatayi gizlemediginden emin oluyor.

**Yakalanmasaydi:** Iki olasilik: (a) `Error.None` tabloya eklenir ve bos kod icin bir HTTP status ve Turkce mesaj uydurulur -- anlamsiz, (b) test kirilir ve gelistirici neden kirildigini anlayamaz. Tip bazli disklama ikisini de onler.

### 3. ValidationFailure fallback'inin ham hata kodu gostermesi

**Sorun:** `ValidationFailure` sadece `PropertyName` ve `ErrorCode` tasir, `Message` alani yoktur. `TurkishErrorMessages` tablosunda karsiligi olmayan bir hata kodu icin fallback mekanizmasi yoksa, kullaniciya `"Product.CodeRequired"` gibi ham kod gosterilirdi.

**Nasil yakalandi:** `fix(web): consistent ValidationFailure fallback and close tester mutation gaps` commit'i. Kod inceleme sirasinda, yeni bir hata kodu eklendiginde Turkce cevirisi unutulursa ne olacagi soruldugunda. Bir `ValidationFailure` icin `Error.Message` gibi bir fallback degeri yok cunku `ValidationFailure`'in `Message` alani bulunmuyor.

**Yakalanmasaydi:** Kullanici form gonderdiginde alan hatalari `"Product.CodeRequired"` gibi Ingilizce teknik kodlar olarak gorunurdu. Bu hem kotu kullanici deneyimi hem de ic uygulama detaylarinin disariya sizmasi demek. `GenericValidationFallback` (`"Bu alan gecersiz."`) bu riski ortadan kaldirir. `ResultExtensionsTests.ValidationFailure_WithUnmappedErrorCode_ShouldUseGenericTurkishFallback` testi bunu dogruluyor.

### 4. DataSource'un tum tabloyu bellege cekmesi

**Sorun:** `ProductListDto` baslangicta positional record olarak tanimlandi: `record ProductListDto(Guid Id, string Code, ...)`. EF Core'un LINQ-to-SQL cevirici, `DataSourceLoader`'in `NewExpression` uzerine ekledigi `OrderBy` member-access ifadesini SQL'e ceviremiyor. Sonuc: `DataSourceLoader.LoadAsync` sorgusu client-side evaluation'a duser ve tum tablo bellege cekilir.

**Nasil yakalandi:** Datasource endpoint'i ilk kez calistirildiginda, sort parametresi eklenince EF Core uyari verdi (veya exception firlatti). `ProductsDatasourceTests.GetDatasource_LoadAsync_OnIQueryable_ShouldNotThrow` regresyon testi eklendi: RowVersion veya positional record geri eklense test kirilir.

**Yakalanmasaydi:** Uretimde 10.000 urunluk bir tabloda her datasource istegi tum tabloyu bellege yukler. Sunucu bellegi sisir, yanit suresi saniyelerden dakikalara cikar, sonunda `OutOfMemoryException` ile uygulama coker. `ArchitectureTests.DataSourceListDtos_ShouldNotBePositionalRecords` mimari testi bunu kalici olarak korur.

### 5. Positional record projeksiyonunun cevrilememesi (NewExpression translation failure)

**Sorun:** Ayni sorunun ikinci boyutu: sadece positional record degil, `EF.Property<byte[]>(p, "RowVersion")` shadow property erisimi de Join-projected query icinde `DataSourceLoader` kompozisyonunu bozuyor. `ProductListDto`'ya `RowVersion` eklenmis olsaydi, sort veya filter islemi yapildiginda EF Core exception firlatirdi.

**Nasil yakalandi:** `ProductReadRepository.GetAll()` projeksiyon yazilirken, `RowVersion` shadow property'sinin `GetAll`'dan cikarilmasiyla. `GetByIdAsync` ise `ProductDetailDto` doner (positional record) ve `DataSourceLoader` ile kullanilmadigi icin orada sorun yok.

**Yakalanmasaydi:** `ProductListDto`'da `RowVersion` olsa, datasource endpoint'i her sort/filter isteginde patlardi. Concurrency token grid listesinde zaten gosterilmiyor -- sadece update/activate/deactivate islemlerinde gerekli ve o zaman `GetByIdAsync` uzerinden aliniyor.

### 6. Negatif Take/Skip ve sinirsiz offset

**Sorun:** DevExtreme istemcisi normalde negatif `take` veya `skip` gondermez, ama sorgu parametreleri uzerinden `take=-1` veya `skip=-1` gonderilebilir. Bazi LINQ provider'lar negatif degerleri `0` olarak yorumlar, bazilari exception firlatir, bazilari da beklenmedik sonuclar uretir. `skip=100000` gibi asiri offset degerleri de SQL Server'da pahali table scan'lara neden olur.

**Nasil yakalandi:** `fix(api): close datasource guard gaps for negative take/skip, unbounded offset, and malformed query strings` commit'i. Guard'in ilk versiyonu sadece `take > maxTake`'i kontrol ediyordu, negatif degerler ve asiri offset gozden kacinmisti.

**Yakalanmasaydi:** Bir saldirgan `skip=999999999` gonderip SQL Server'a pahali bir OFFSET sorgusu yaptirabilirdi (DoS vektoru). Negatif `take` veya `skip` ise provider'a gore farkli davranir -- deterministic olmayan davranis guvenlik acigi olabilir.

### 7. Bozuk sorgu dizesinde 500

**Sorun:** `DataSourceLoadOptionsParser.Parse` bozuk bir filter JSON'u aldiginda (ornegin `filter=[[[invalid`) exception firlatir. Model binder bu exception'i yakalamazsa, ASP.NET Core exception middleware'e kadar yayilir ve 500 Internal Server Error doner.

**Nasil yakalandi:** `fix(api): close datasource guard gaps for negative take/skip, unbounded offset, and malformed query strings` commit'i. Model binder'a try/catch eklendi, catch blogu `ModelState.AddModelError` ile hata kaydeder ve `ModelBindingResult.Failed()` doner. `[ApiController]` basarisiz model binding'i otomatik 400 ProblemDetails'a cevirir.

**Yakalanmasaydi:** Istemci bozuk bir sorgu gonderdiyinde 500 donurdu. 500, sunucu arizasi demektir ve monitoring sistemleri alarm verir. Kullanici hatasi yuzunden sahte alarm olusur, gercek sunucu arizalari alarm yorgunlugu icinde kaybolur.

### 8. Filter alanlarinin allowlist disi kalmasi

**Sorun:** `DataSourceGuard`'in ilk versiyonu sort ve group alanlarini allowlist'e karsi dogruluyor ama filter alanlarini kontrol etmiyordu. DevExtreme filter parametresi `filter=["RowVersion","=","abc"]` seklinde gonderilebilir ve `RowVersion` shadow property'si LINQ sorgusunda exception firlatir (500 donurdu).

**Nasil yakalandi:** `fix(api): add recursive filter field allowlist to DataSourceGuard and unify sort/group/filter validation` commit'i. Code review sirasinda "sort kontrol ediliyor, filter neden kontrol edilmiyor?" sorusu soruldu. Filter yapisi ic ice olabilecegi icin (`[cond1, "and", cond2]`) rekursif dogrulama eklendi.

**Yakalanmasaydi:** Bir saldirgan `filter=["RowVersion","=","abc"]` gonderip 500 hatasi uretebilirdi. Daha kotusu, filter uzerinden var olmayan alanlar denenerek veritabani semasi hakkinda bilgi cikarilabilirdi (information disclosure).

### 9. 429'un bos govdeyle donup StatusCodePages tarafindan yakalanabilmesi

**Sorun:** Rate limiter varsayilan davranisla bos govdeli 429 doner. `UseStatusCodePagesWithReExecute("/not-found")` middleware'i govdesi bos olan 4xx yanitlari yakalar ve `/not-found` sayfasina yonlendirir. Sonuc: API istemcisi ProblemDetails JSON beklerken HTML sayfa alir.

**Nasil yakalandi:** `fix(api): write ProblemDetails body on 429 to prevent StatusCodePages interception` commit'i. Rate limiter testi yazilirken, 429 yanitinin govdesinin HTML oldugu goruldu. Middleware sirasinda `UseStatusCodePagesWithReExecute` rate limiter'dan sonra calisiyordu ve bos govdeli yanitlari yakaliyordu.

**Yakalanmasaydi:** API istemcisi (ornegin bir mobil uygulama) 429 aldiginda govdeyi JSON olarak parse etmeye calisir ve deserializasyon hatasi alirdi. Istemci tarafinda rate limiting'in dogru handle edilmesi imkansizlasirdi. Test artik `body.ShouldNotContain("<html")` ile bunu acikca dogruluyor.

## ADR 0006'da cevaplanmasi gereken sorular

1. FK hatalari (UnitOfMeasureNotFound, UnitOfMeasureInactive) neden 400 yerine 422'ye esleniyor?

2. Eslenmemis bir Result.Failure neden 500 yerine 400'e donuyor?

3. Concurrency protokolu nedir? (RowVersion JSON'da base64, GET'te doner, PUT'ta geri gonder, uyusmazlikta 409)

4. Hata eslemeleri neden Error tipi uzerinde bir kategori olarak degil de Web katmaninda acik bir kod tablosu olarak yasiyor?

5. Bu ne zaman degismeli? (PR 17 SOAP: Error uzerinde ErrorCategory enum'u dusunulmeli)

6. Kullaniciya gosterilecek mesajlar neden Domain'de degil de sunum katmaninda belirleniyor?

7. Datasource projeksiyon DTO'lari neden positional record olamaz ve EF.Property shadow property iceremiyor?

8. Rate limiter neden bos 429 donmek yerine ProblemDetails govdesi yaziyor?

9. Retry-After basligi kodu yazildi ama FixedWindowRateLimiter bu metadata'yi saglamiyor, dolayisiyla baslik hicbir zaman eklenmiyor ve test bunu iddia etmiyor -- bu olu kod kaldirilmali mi, sabit bir degerle mi yazilmali?
