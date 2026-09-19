# PR 0007 — Authentication Infrastructure

## Ne yaptik

Bu PR sisteme kimlik dogrulamayi getirdi: ASP.NET Core Identity kendi `DbContext`'i ve kendi `auth` sematinde, 15 dakikalik JWT access token, ve donduruler (rotated) opak refresh token'lar. `POST /api/auth/login`, `/refresh` ve `/logout` uc noktalari calisiyor. Onceki PR'larda sistemin hicbir yerinde "kullanici" diye bir kavram yoktu; simdi `auth.AspNetUsers` ve `auth.RefreshTokens` tablolari, `IIdentityService` / `IRefreshTokenService` / `IAccessTokenIssuer` soyutlamalari ve bu soyutlamalari kullanan uc command handler var. Bir sey bilerek yapilmadi: hicbir uc nokta hala korumali degil -- `[Authorize]`, yetkilendirme politikalari ve Blazor cookie semasi PR 6b'ye birakildi. Yani bu PR "kim oldugunu dogrulama" isini kurdu, "neye izin verilecegi" isini degil. Test sayisi 299'dan (113 Domain + 79 Application + 107 Integration) 562'ye cikti (120 + 126 + 316); entegrasyon suiti son olcumde 44 saniye surdu.

## Kavramlar

### Access token ve opak refresh token ikilisi

Access token kisa omurlu (15 dakika) ve her istekte tasinir; sizarsa zarari omruyle sinirlidir. Refresh token uzun omurludur ve sadece "bana yeni bir access token ver" demek icin kullanilir. Ayrim, sik kullanilan sirri kisa omurlu, nadiren kullanilan sirri iptal edilebilir yapar. Refresh token bir JWT degil: 32 baytlik CSPRNG ciktisinin Base64Url hali -- icinde hicbir bilgi yok, "opak" demek bu. Anlami yalnizca veritabanindaki satirda oldugu icin iptali gercek; JWT olsaydi iptal icin yine bir kara liste gerekirdi, yani JWT'nin tek avantaji olan durum tutmama ozelligi kaybolurdu.

### Rotation, aile ve yeniden kullanim tespiti

Her refresh kullaniminda token tuketilir ve yerine yenisi verilir -- rotation budur. Bir girisle baslayan zincirin tamamina **aile** denir; her satir ayni `FamilyId`'yi tasir. r1 -> r2 -> r3 zincirinde biri gelip r1'i tekrar sunarsa bu ya bir saldiri ya da bir ag tekraridir, ve ikisi disaridan ayirt edilemez. RFC 9700 bolum 4.14.2 bu durumda sadece sunulan token'in degil **tum ailenin** iptalini ister.

### Tuketilen satir neden silinmiyor

Tuketilen satiri silmek tespiti bir kusaktan sonra oldurur: r1'in satiri silinmisse saldirgan r1'i sundugunda sorgu hicbir seye eslesmez, sistem "gecersiz token" der, oysa r3 hala canlidir ve calismaya devam eder. Dogrusu satiri tutup damgalamaktir (`RotatedAt`, `ReplacedByTokenId`) -- boylece herhangi bir kusagin tekrari yine ailesine cozulur. Bedeli tablonun budanmadan buyumesi; pruning PR 18'de.

### Idle window ve absolute cap

Iki son kullanma tarihi var. `ExpiresAt` bos durma penceresidir, her rotation'da `now + 7 gun` olarak yeniden hesaplanir. `FamilyExpiresAt` ailenin mutlak tavanidir: ilk giriste `now + 30 gun` konur ve her cocuga **degismeden** kopyalanir. Tavanin kopyalanmasi dogrulamayi tek satirlik bir okuma olarak tutar; aksi halde her kontrolde ailenin kokunu bulman gerekirdi. Anlami: aktif kullanici 7 gunde bir yeniledigi surece oturumda kalir, ama en fazla 30 gun.

### Sistemin ilk "simdi" kavrami

Bu PR'dan once `src/` altinda `TimeProvider`, `ISystemClock`, `IClock`, `DateTime.` veya `DateTimeOffset.` gecen tek bir satir yoktu: access token suresi, bos durma penceresi ve mutlak tavan, hepsi sisteme "simdi" kavramini bu PR'da soktu. Zaman `TimeProvider` uzerinden aliniyor ve testte `FakeTimeProvider` ileri sariliyor, boylece sekiz gunluk bir sona erme testi uyumadan calisiyor. Elle bir `IClock` yazilmadi -- BCL'de tip varken ikinci bir kavram uretmenin anlami yok.

### Refresh token neden SHA-256 ile hash'leniyor, parola gibi degil

Parolalar PBKDF2 gibi **kasitli yavas** algoritmalarla hash'lenir, cunku insan parolalari dusuk entropilidir ve yavaslik denemeyi pahalilastirir. Refresh token ise 256 bitlik rastgelelik -- ona karsi denenecek bir sozluk yok. Is faktoru her yenilemeye ~100 ms ekler ve hicbir sey kazandirmaz; salt eklemek ise `IX_RefreshTokens_TokenHash` uzerindeki seek'i imkansiz kilar. Bu yuzden duz SHA-256 ve `binary(32)`, karsilastirma da `FixedTimeEquals` ile: `==` ilk farkli baytta durur, bu durma ani olculebilir ve deger bayt bayt bulunabilir.

### Hesap numaralandirma ve zamanlama kanali

Bir giris ekrani "boyle bir kullanici yok" ile "parola yanlis" arasinda ayrim yaparsa, saldirgan hangi e-postalarin kayitli oldugunu ogrenir. Burada yanlis parola, bilinmeyen e-posta ve kilitli hesap **ayni** `AuthErrors.InvalidCredentials` ornegini doner; govde, durum kodu ve `Content-Type` bayt bayt aynidir. `Auth.UserLockedOut` diye bir kod hic yaratilmadi -- iki ayri kod ayni duruma eslense bile farkli `detail` metinleri tasir ve ileride birinin "yardimci olmak icin" degistirmesini davet eder.

Ikinci kanal zamandir. Kilitli hesapta parola kontrolunu atlarsan yanit birden hizlanir; bu hizlanma hem hesabin var oldugunu hem kilit esiginin asildigini soyler. Bu yuzden once `CheckPasswordAsync`, sonra `IsLockedOutAsync` cagriliyor. Kilitliyse ne `AccessFailedAsync` ne `ResetAccessFailedCountAsync` cagrilir -- ilki saldirganin kurbanin kilidini sonsuza uzatmasina izin verirdi. Kalan iki acik kaydedildi: kilitli hesap da PBKDF2 maliyetini oduyor, bilinmeyen e-posta hala hizli cevap veriyor.

Kullaniciya gosterilen tek mesaj politika hakkinda konusur, bu hesap hakkinda degil: "E-posta veya parola hatali. Arka arkaya birkac basarisiz denemeden sonra hesap bir sureligine kilitlenir." Genis zaman ("kilitlenir") tasiyicidir; "kilitlendi" bu hesabin durumunu **onaylar**, ve bunu yasaklayan adi konmus bir test var. Kilit ayarlari (5 deneme, 15 dakika) yanitla hic gorulmedigi icin tek gozlemlenebildikleri yer `IOptions<IdentityOptions>`. Bir not: `UserManager`'in kilit kontrolu sistem saatini okur, enjekte edilen `TimeProvider`'i degil -- sahte saati 16 dakika ileri sarmak bir kilidi acmaz.

### Iki `DbContext`, iki `__EFMigrationsHistory`

EF Core uygulanan migration'lari `__EFMigrationsHistory` tablosunda tutar, ve iki `DbContext` ayni veritabanina baktiginda **ikisi de varsayilan olarak ayni tabloyu** okur: her biri digerinin kayitlarini kendi gecmisi sanar. Bu sessiz bir hatadir -- hicbir sey patlamaz, sadece bir migration atlanir veya tekrar uygulanir ve bunu haftalar sonra fark edersin. Cozum `MigrationsHistoryTable("__EFMigrationsHistory", "auth")`; uc cagri yeri (DI, design-time factory, test fixture) tek bir uzanti metodundan geciyor, yoksa birinde yazip digerinde unutmak isten bile degil. `auth` semasinin asil isi budur, duzen ikincil.

### Es zamanlilik: filtreli indeks, `RowVersion` ve sinirli yeniden deneme

`IX_RefreshTokens_FamilyId_Live`, `FamilyId` uzerinde `UNIQUE` ama filtresi `[RotatedAt] IS NULL AND [RevokedAt] IS NULL`: bir ailede en fazla bir **canli** token olabilir. Ayni kontrol uygulama kodunda da yapilabilirdi, ama iki es zamanli istek arasinda uygulama yaris kaybeder; veritabani kaybetmez. Ayni sutunda filtresiz ikinci bir indeks daha var, cunku aile sorgulari dondurulmus-ama-iptal-edilmemis satirlari da gormek zorunda. Cluster ise PK yerine `(CreatedAt, Id)` uzerinde: rastgele GUID'e gore siralanan bir tabloda her yeni satir ortalara girip sayfalari bolerdi.

Rotation ebeveyni damgalar ve cocugu ekler -- ikisi de **tek** bir `SaveChangesAsync` icinde. Kaybeden ya `DbUpdateConcurrencyException` ile (`RowVersion` degismis) ya da canli indeks uzerinde 2601/2627 ihlaliyle kaybeder; ikisi de ayni olguyu bildirdigi icin tek bir cikista `Auth.InvalidRefreshToken`'a esleniyor, boylece EF'in UPDATE/INSERT sirasi ne olursa olsun sonuc degismez. Bir aileyi iptal etmek de tek gecisle bitmeyebilir: okuma ile yazma arasinda commit eden bir rotation, ilk gecisin hic gormedigi yeni bir canli cocuk birakir. Iptal bu yuzden en fazla uc kez donen sinirli bir dongude yapiliyor; her gecis kesinlikle daha yeni bir kusak gordugu icin sonlanir, ve dongu bittikten sonraki tek fazladan okuma Faz 4 db-review'unun A1 tavsiyesiyle geldi.

### Middleware sirasi ve login rate limit politikasi

`UseAuthentication()` istegin kimligini cozer, `UseAuthorization()` politika kontrolunu yapar. Ikisi de `UseHttpsRedirection()`den **sonra** ve `UseAntiforgery()` ile uc nokta eslemelerinden **once** durur -- antiforgery, routing, Blazor devresi ve MVC filtreleri hepsi `HttpContext.User`i okur. Rate limiter tarafinda global limiterin yanina yalnizca login icin adlandirilmis bir politika eklendi: uretimde 5 istek / 300 saniye, kovalari istemcinin IP adresine gore ayrilmis, adres yoksa sabit `"unknown"`.

## Komutlar ve ne yaptiklari

Iki `DbContext` yuzunden `dotnet ef` komutlari artik `--context` istiyor, ve Identity zinciri `--output-dir Migrations/Identity` ile ayri bir klasorde duruyor; ayni klasore karisirlarsa iki zincir birbirine girer. Dort komutun tam hali `CLAUDE.md`de. Ogretici olan ikisi:

### `dotnet ef migrations list` -- `--context` olmadan, bilerek
Hata vermesi beklenerek calistirildi: "More than one DbContext was found". Planin notu netti -- bu komut beklenmedik sekilde basarili olursa iki-context varsayimi yanlis demektir ve durup bildirilmelidir. Bir varsayimi kanitlamak icin basarisiz olmasi beklenen komutu calistirmak, bu projede standart bir dogrulama bicimi.

### `dotnet list ... package --include-transitive`
Dogrudan ve **transitif** paketleri cozulmus surumleriyle listeler; `Microsoft.IdentityModel.JsonWebTokens` surumu tahmin edilmeyip buradan okundu, cunku cozulen surumden dusuk pinlemek NU1605 uretirdi. Identity ve JwtBearer `10.0.11` olarak pinlendi -- solution'daki diger ASP.NET Core ve EF Core paketleriyle ayni satir; `Microsoft.IdentityModel.JsonWebTokens` ise `8.19.2`. Ailenin bolunmesi NU1605 uretir ve `TreatWarningsAsErrors` onu build hatasina cevirir.

## Dikkat edilen tuzaklar

### `AddIdentity` bir sinif kutuphanesine framework referansi zorluyor
`AddIdentity` cookie semalarini ve `SignInManager`'i da kurar; `SignInManager` paylasilan ASP.NET Core framework'unde yasadigi icin `Envanex.Infrastructure` gibi bir sinif kutuphanesine `Microsoft.AspNetCore.App` framework referansi eklemeyi zorlar. Giris akisi zaten elle yazildigi icin `AddIdentityCore` yeterli oldu.

### `ApplyConfigurationsFromAssembly`'nin fazla genis olmasi
`EnvanexDbContext` kendi assembly'sindeki tum konfigurasyonlari otomatik uyguluyordu; `RefreshTokenConfiguration` ayni assembly'ye girince is context'i de onu bulur ve bir sonraki is migration'inda ikinci bir `dbo.RefreshTokens` uretirdi. Cozum iki tarafli: tarama bir namespace yuklemine daraltildi, konfigurasyon da bilerek o yuklemin disina kondu. Gercek bekci `EnvanexDbContext_ShouldNotMapRefreshToken` testidir.

### Adsiz `HasIndex` cagrisinin ilk indeksi yeniden adlandirmasi
EF adsiz bir indeksi **ozellik kumesine gore** tanir: ayni sutun uzerindeki ikinci cagri yeni bir indeks eklemedi, birincisini yeniden adlandirdi ve migration'da tek indeks cikti. Ikisi de adli overload'a gecirildi.

### Uretilen migration'da CA1861
Bilesik clustered indeks satir ici bir dizi argumani olarak uretiliyor; CA1861 bunu uyariya, `TreatWarningsAsErrors` hataya ceviriyor. Uretilen migration elle duzeltilemeyecegi icin cozum `.editorconfig`'te migration klasorune kapsamli bir bastirma oldu.

### "Identity" kelimesini iceren migration adina bagli bekciler
Faz 1'in uc migration bekcisi, migration adinda `Identity` alt dizesi olup olmamasina bakiyordu. Faz 2'nin migration'i `AddRefreshTokens` adini tasiyinca biri hemen kirmizi oldu, digerleri ise kacan bir migration'i sessizce yakalayamaz hale gelecekti. Ucu de ada bagli olmaktan cikarilip kume uyeligi kontroluyle yeniden yazildi.

### Bir satiri iki kez damgalamak
`MarkRotated` ve `Revoke` ikinci cagriya karsi korumasizdi. Ikinci bir `Revoke`, `Reuse` sebebiyle iptal edilmis bir satiri `Logout` olarak damgalayabilirdi -- ki `RevokedReason` bu tasarimin urettigi tek adli kanittir; ikinci bir `MarkRotated` ise aile zincirini kirardi. Ikisi de artik firlatiyor: uc ayri cagri yeri var ve dogruluk "her cagiran dikkatli olsun"a birakilamaz.

### `RevokedReason`'in tanimsiz bir enum degeri almasi
Sebep `reason.ToString()` ile yaziliyordu; menzil disi bir cast sutuna `"99"` yazardi. Artik `Enum.IsDefined` kontrolu var ve **zaten-iptal-edilmis** kontrolunden once geliyor: arguman dogrulamasi durum dogrulamasindan once.

### Basarisiz save'den sonra izlenen varliklar
Rotation'in kaybeden cikisi `ChangeTracker.Clear()` cagiriyor. Basarisiz save cocugu `Added`, ebeveyni `Modified` birakir; bu context `UserManager`'in da kullandigi scoped context oldugu icin ayni istekteki bir sonraki save iki yazmayi tekrar denerdi.

### Paylasilan test fabrikasinda login limiti
`WebApplicationFactory` uzerinden gelen isteklerin uzak adresi yoktur, yani hepsi tek bir `"unknown"` kovasina duser; ustelik fabrika tum test koleksiyonu boyunca yasar. `RateLimiting:Login:Enabled=false` iki paylasilan fabrikaya da yazilmasaydi siniflar arasi 429'lar rastgele testleri kirardi. Bu, plan incelemesinin bir numarali bulgusuydu ve `Login_EightConsecutiveAttempts_ShouldNeverReturn429` ile kilitlendi -- uretim limiti 5 oldugu icin ayar dusse test kirmizi olur.

### Adi gecen ama kayitli olmayan politika
`[EnableRateLimiting("login")]` middleware'in gormedigi bir politikayi isaret ederse **uc nokta insa edilirken** exception firlar ve sadece login degil tum uygulama coker. Bu yuzden `AddRateLimiter` ve `UseRateLimiter` kosulsuz hale getirildi; yalnizca `GlobalLimiter` atamasi `RateLimiting:Enabled` icinde kaldi.

### Govdesi bos 401'in not-found sayfasina donusmesi
`UseStatusCodePagesWithReExecute("/not-found")` govdesi bos 4xx yanitlarini yakalar (PR 0006'daki 429 sorununun aynisi). Kimlik dogrulama middleware'i bu sarmalayicinin **icine** kondu; 6a'da bu guvenli, cunku her 401 ProblemDetails govdesiyle uretiliyor. Govdesi bos challenge 401'i ilk `[Authorize]` ile, yani 6b'de dogacak. Bugunku durumu `AuthPipelineTests` kilitliyor: durum 401, `Content-Type` `application/problem+json`, govdede `<html` yok.

### `ClockSkew`
JwtBearer varsayilan olarak 5 dakikalik saat kaymasi toleransi uygular. 15 dakikalik bir access token fiilen 20 dakika yasardi ve sahte saatle yazilan her sona erme testi yalan soylerdi. `ClockSkew = TimeSpan.Zero`.

### Test kullanicisi kurmanin iki tuzagi
`UserManager.CreateAsync` basarisizligi exception ile degil basarisiz bir `IdentityResult` ile bildirir; sonucu kontrol etmeyen bir seeder hicbir kullanici olusturmaz ama basarili raporlar, ardindan gelen test alakasiz bir yerde patlar. Ayrica Faz 2'nin sema testleri bir FK icin kullanici satiri istiyordu ama `UserManager` yolu Faz 4'te geliyordu: cozum `PasswordHash`'i **null** birakan dar bir istisna oldu -- parolasi olmayan bir satirla giris yapilamaz, dolayisiyla bir hash'leme hatasini gizleyemez. Istisnanin dar kalmasini bir yorum degil, `IdentitySeedingScopeTests` sagliyor.

## Surecten cikan dersler

Bu PR'da ogretici olan sey yalnizca kod degil, kodun nasil dogrulandigi.

### Basarisiz olamayan testler: yazilanlar ve hic yazilmayanlar

Iki ayri olay var ve ayri dersler veriyorlar. Birincisi: **dort test yazildi, korudugunu iddia ettigi seyi kaldirsan bile yesil kalacak haldeydi, ve dordu de yazildiktan sonraki incelemede yakalandi.**

- `EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse` yalnizca entity tiplerinin bulundugunu dogruluyordu -- EF bunlari `DbSet<T>` property'lerinden convention ile zaten kesfeder, yani tum Fluent konfigurasyonlar dusse bile yesil kalabilirdi.
- `EnvanexIdentityDbContext_ShouldNotDeclareRowVersionShadowProperty`, convention kayitli olsun ya da olmasin yesildi: hicbir Identity entity'si `AggregateRoot<>`'tan turemiyor.
- `EmailIndex_ShouldBeUnique` yalnizca `is_unique` bakiyordu; indeks filtresini kaybetse veya baska bir sutuna tasinsa yine yesildi.
- Canli indeks testi "filtre metninde `RotatedAt` ve `RevokedAt` geciyor mu" diye bakiyordu, operatoru ve yonu sabitlemiyordu.

Ikincisi, ve farkli bir ders: **plan incelemesi, henuz yazilmamis iki testi yazilmadan once reddetti** -- var olmayan bir "Sifre -> Parola" degisikligini koruyan test, ve `EnvanexUser`'i hicbir fazda hicbir konfigurasyonu olmadigi icin bos yere kontrol eden test. Birinci olay "incelemeler yazilani yakaliyor" der, ikincisi "plan incelemesi yazilacak olani yakaliyor". Ikisini tek sayiya toplamak her ikisini de zayiflatir: biri kodun uzerinden gecmeyi savunur, digeri plani okumayi.

Buradan cikan pratik: son fazlarda her yeni test **once mutasyonla kirmizi gorulup** sonra kabul edildi. "Gecen bir test, koruyan bir test degildir."

### Komut ciktisini anlatmak ile yapistirmak

Bu proje ajan ozetine degil ham komut ciktisina bakar -- cunku bir ajanin "calistirdim, gecti" demesi ile komutun gercekten ne yazdigi ayni sey degil. Bu PR sirasinda da iki ajan, calistirdigini soyledigi komutun sonucunu gerceginden farkli bildirdi. Plan bu yuzden yer yer ciktinin **birebir yapistirilmasini** sart kosuyor: paket cozumleme listesi, uretilen migration'in `Up` metodu, rotation sirasindaki UPDATE/INSERT sirasini gosteren SQL logu, aile sorgusunun `SHOWPLAN` ciktisi. Arastirma dosyasi ayni kurali kendine de uyguluyor: her satir numarasi ve sayim, ikinci elden bildirilmek yerine calisma agacina karsi kontrol edildi.

### Kabugu olmayan bir inceleyici

Kod inceleyicinin shell'i yok; iddialari okuyarak uretiyor. Iki kez oncullerden **dogru** akil yurutup **yanlis** sonuca vardi:

1. `docs/journal/note-2026-09-18-configuration-predicate-guard.md`'de kayitli olay: "testin assert'leri predicate'e bagli degil" dogruydu, "o yuzden predicate bozulsa test yesil kalir" yanlisti -- arada `MigrateAsync` ve `PendingModelChangesWarning` patliyordu. Koruma vardi, ama iddia edilen yerde degil.
2. Plan incelemesinin 2 numarali bulgusu: es zamanlilik testlerinin serilestirilmis bir sirada kirmizi olacagi tespiti **dogruydu**, ama onerdigi duzeltme ("aileye dogrudan ikinci bir canli satir ekle, sonra `RotateAsync` cagir") **imkansizdi** -- ikinci canli satir tam da `IX_RefreshTokens_FamilyId_Live`'in yasakladigi seydir, `RotateAsync` cagrilmadan insert patlar.

Ikinci olayda planlayici geri itti ve hakliydi: ayni analiz 2601/2627 dalinin `RotateAsync` uzerinden hic ulasilabilir olmadigini da gosterdi. Sonuc: uc kati kaybeden testi silindi, yerine her sira icin gecerli olan iki iddia birakildi, deterministik kanit ise gercekten deterministik oldugu yere -- Faz 2'nin sema testlerine -- tasindi.

### Planin kanitla uc kez ezilmesi

Faz 2'de uygulayan taraf planin uc ayrintisini **calistirdigi icin** duzeltti: adsiz `HasIndex` cagrisinin ikinci bir indeks uretecegi varsayimi (uretmedi), uretilen migration'in CA1861 ile build'i kiracagi (plan bunu ongormuyordu), ve `Identity` alt dizesine dayanan bekcilerin yeni migration adiyla bozulacagi. Plan bir sozlesme degil, en iyi tahmindir; tahmini bozan sey komutun ciktisidir.

## Farkli dusundugum yer

**Logout'un basarisizligi kullaniciya yanlis seyi soyluyor.** `RevokeFamilyAsync` uc denemede ve son okumada da aileyi kapatamazsa `AuthErrors.InvalidRefreshToken` donuyor; bu kod 401'e ve "Oturum bilgisi gecersiz. Lutfen tekrar giris yapin." mesajina esleniyor. Oysa gerceklesen sey token'in gecersiz olmasi degil, sunucunun cekismeden dolayi iptali tamamlayamamasidir -- ayni anda `Error` seviyesinde bir log atiliyor ve bir operator bundan cagri aliyor. Istemci "token'im gecersizmis" diye yorumlar, gercekte oturumu hala canlidir. Bunun 5xx ailesine ait bir kodu veya en azindan kendi hata kodu olmasi gerekirdi; `Result` sozlesmesi bunu engellemiyor.

**Faz 6'nin elle duman testi adimlarinin atlanmasi.** Gerekce makul (kullanici tohumlama yuzeyi yok) ve entegrasyon testleri ayni senaryolari gercek SQL Server'a karsi kapsiyor. Ama o adimlarin var olma sebebi tam da test fabrikasinin kurdugu duzenin disinda, `dotnet run` ile ayaga kalkan gercek host'ta bir seylerin farkli davranabilmesiydi. Test icin bir tohumlama yuzeyi icat etmemek dogru karardi; adimi tamamen atlamak yerine "bu PR'da calistirilamaz, PR 6b'de demo hesapla calistirilacak" diye yol haritasina baglamak daha iyi olurdu.

**`.editorconfig`'teki cift bolum.** Migration klasoru icin iki ayri bastirma bolumu var; yorum bir glob biciminin her motorda tutarli eslenmedigini **gozlemledigini** soyluyor ama bu gozlemin kaydi repoda yok. Projenin kendi kurali "dogrulanmamis iddia bulgu degildir" -- bu yorum tam da o kategoride, ve ileride birinin "gereksiz" diye silmesini engelleyen tek sey bir cumle. Ya deney kaydedilmeli ya da tek bolume inilmeliydi.

## Kendini sina

1. `RefreshTokenGenerator.CreateToken` Base64Url uretiyor. Duz Base64 kullansaydin, token'i bir URL'de veya bir HTTP basliginda tasiyan istemcide ne kirilirdi?

2. `JwtOptionsGuard.ThrowIfInvalid` iki ayri yerden cagriliyor: `AddEnvanexIdentity` ve `AddEnvanexJwtBearer`. Birini silsen hangi somut yanlis yapilandirma fark edilmeden gecer?

3. Kilitli bir hesaba **dogru** parola ile giris denendiginde `ResetAccessFailedCountAsync` bilerek cagrilmiyor. Cagrilsaydi saldirgan icin ne mumkun olurdu?

4. `RefreshToken.IsUsableAt` ile `IX_RefreshTokens_FamilyId_Live`'in filtresi ayni kosullari tasiyor. Biri degisip digeri degismezse sistem hangi anda bozulur -- once uygulama mi sikayet eder, veritabani mi? Hangi test bunu yakalar?

5. `auth.RefreshTokens` append-only ve bugun hic budanmiyor. PR 18'de gece calisan bir pruning isi eklendiginde `(CreatedAt, Id)` uzerindeki clustered indeks bu isi kolaylastirir mi zorlastirir mi? Silme islemi hangi indeksleri nasil etkiler?

6. `RevokeLiveFamilyRowsWithRetryAsync` dongu bittikten sonra aileyi bir kez daha okuyor. Bu son okuma silinseydi hangi durumda logout basarisiz raporlanirdi, ve bunun operasyonel maliyeti ne olurdu?

7. Refresh token, cookie olarak degil yanit govdesinde donuyor. `HttpOnly` cookie secilseydi PR 6b'deki Blazor cookie semasi ve mevcut REST istemcisi acisindan ne degisirdi? XSS ve CSRF risklerini nasil kaydirirdi?

8. `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`, PR 7 sorunu `UseForwardedHeaders` ile cozerse yesil kalacak -- cunku `GetKey`'i elle kurulmus bir `DefaultHttpContext` ile dogrudan cagiriyor. Bu tripwire'i gercekten kirmizi olacak sekilde nasil yazardin, ve boyle yazmanin bedeli ne olurdu?
