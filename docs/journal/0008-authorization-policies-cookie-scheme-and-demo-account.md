# PR 0008 — Authorization Policies, the Blazor Cookie Scheme, and the Read-Only Demo Account

## Ne yaptık

PR 6a sisteme "sen kimsin" sorusunu sormayı öğretmişti ama cevabı hiçbir yerde kullanmıyordu: her uç nokta herkese açıktı. Bu PR "neye iznin var" sorusunu ekledi. Artık her uç nokta varsayılan olarak kapalı. Açık olanlar tek tek `[AllowAnonymous]` ile işaretlendi. İzinler iki politikayla ifade ediliyor: `CanRead` ve `CanWrite`. Bu politikaları iki rol besliyor: `Administrator` ve `Viewer`. Blazor arayüzü kendi cookie şemasını aldı; statik render edilen bir giriş sayfası, bir çıkış sayfası ve `NavMenu`'de oturumdaki kullanıcının e-postası var. REST tarafı ise yalnızca bearer token kabul ediyor. Hangi şemanın devreye gireceğine isteğin yolu karar veriyor. Access token artık rolleri de taşıyor. Son olarak yapılandırmayla açılan, salt okunur bir demo hesabı eklendi. Şema değişmedi, migration yok. Test sayısı 562'den 637'ye çıktı (120 Domain + 126 Application + 391 Integration); entegrasyon suiti 58-59 saniyede, ADR 0007'nin 60 saniyelik karar çizgisine dayandı.

## Yeni giren teknolojiler

### ASP.NET Core yetkilendirme politikaları (`AddAuthorization`, `FallbackPolicy`, `AddPolicy`)
- **Ne işe yarar:** Politika, adı olan bir kural setidir: "şu rollerden biri olmalı", "oturum açmış olmalı" gibi. Uç noktalar rol adı yazmaz, politika adı yazar (`[Authorize(Policy = "CanWrite")]`). Rol ile politika arasındaki eşleme tek bir yerde durur. `FallbackPolicy` ise hiçbir yetkilendirme bilgisi taşımayan her uç noktaya uygulanan kuraldır.
- **Bu projede nerede:** `src/Envanex.Web/Program.cs` (`AddAuthorization` bloğu), `src/Envanex.Web/Authorization/EnvanexPolicies.cs`, controller'lardaki `[Authorize(Policy = ...)]` attribute'ları.
- **Alternatifi neydi:** Controller'larda doğrudan `[Authorize(Roles = "Administrator")]` yazmak. Bu yöntemle ileride eklenecek bir rol için her controller'ı tek tek düzenlemen gerekirdi. Bir de hangi rolün neye yettiğini bütün kod tabanını tarayarak bulmak zorunda kalırdın. Politikalarla bu bilgi iki satırda duruyor.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/security/authorization/policies

### Cookie kimlik doğrulaması (`AddCookie`)
- **Ne işe yarar:** Başarılı girişten sonra sunucu, kullanıcının kimliğini (claim'lerini) şifreleyip bir cookie'ye yazar. Tarayıcı bu cookie'yi aynı siteye giden her istekte kendiliğinden geri gönderir. Sonuç olarak JavaScript'in bir token tutmasına ya da bir `Authorization` başlığı eklemesine gerek kalmaz.
- **Bu projede nerede:** `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` (`.AddCookie(...)`), `src/Envanex.Web/Authentication/CookieSignInService.cs`.
- **Alternatifi neydi:** Blazor UI'ın da REST'teki gibi bir bearer token tutması. Blazor Server'da UI zaten sunucuda çalıştığı için bu, token'ın nerede saklanacağı sorusunu (localStorage mı, bellek mi?) gereksiz yere açardı. React tarafında bildiğin "token'ı nereye koysam XSS'e açık olmaz" tartışması bu yüzden burada hiç doğmuyor.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/security/authentication/cookie

### Policy scheme (`AddPolicyScheme`, `ForwardDefaultSelector`)
- **Ne işe yarar:** Kendisi kimlik doğrulaması yapmayan, gelen her isteği başka bir şemaya yönlendiren bir "santral" şemasıdır. Tüm varsayılanlar bu santrali gösterir, santral de isteğe bakıp cookie ya da bearer şemasını seçer.
- **Bu projede nerede:** `JwtAuthenticationExtensions.cs`, `EnvanexAuthenticationSchemes.Selector` (`"Envanex"`).
- **Alternatifi neydi:** Çok şemalı bir politika (`new AuthorizationPolicyBuilder(Cookie, Bearer)`). Bu yolda framework iki şemayı da dener ve sonuçları tek bir kullanıcıda birleştirir. Planlayıcı bunu önerdi, insan reddetti. Nedeni "Kavramlar" bölümünde anlatılıyor.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/security/authentication/policyschemes

### Blazor'ın yetkilendirme yüzeyi (`AuthorizeRouteView`, `AuthorizeView`, `AddCascadingAuthenticationState`)
- **Ne işe yarar:** `AddCascadingAuthenticationState` oturum bilgisini tüm bileşen ağacına dağıtır; React'teki bir `AuthContext.Provider`'ın servis düzeyindeki karşılığı gibi düşünebilirsin. `AuthorizeView` "oturum açıksa şunu, değilse bunu göster" der. `AuthorizeRouteView` ise router'ın yetkisi olmayan kullanıcıya ne göstereceğini belirler.
- **Bu projede nerede:** `src/Envanex.Web/Components/Routes.razor`, `Components/Layout/NavMenu.razor`, `Program.cs`.
- **Alternatifi neydi:** Sayfalarda `HttpContext.User`'ı elle okumak. Bu hem her sayfada tekrar demek hem de interaktif render'da `HttpContext` olmadığı için çalışmaz.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/blazor/security/

### `RevalidatingServerAuthenticationStateProvider`
- **Ne işe yarar:** Açık bir Blazor devresinin (circuit) kullanıcısını belirli aralıklarla veritabanına karşı yeniden kontrol eder. Burada aralık 30 dakika. Kullanıcı silinmiş ya da security stamp'i değişmişse devreyi anonim hale getirir.
- **Bu projede nerede:** `src/Envanex.Web/Authentication/RevalidatingIdentityAuthenticationStateProvider.cs`.
- **Alternatifi neydi:** Framework'ün düz `ServerAuthenticationStateProvider`'ı. O, devre açıldığında gördüğü kullanıcıyı devre kapanana kadar hiç değiştirmez. Bir uyarı: bu sağlayıcı bugün **hiç çalışmıyor**. Nedeni aşağıda, güvenlik incelemesi bölümünde.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/blazor/security/server/

### `RoleManager<IdentityRole<Guid>>`
- **Ne işe yarar:** Identity'nin rol tablosunu (`auth.AspNetRoles`) yöneten servis. Rol oluşturur, var mı diye bakar; kullanıcıyı role bağlama işini ise `UserManager.AddToRoleAsync` yapar.
- **Bu projede nerede:** `src/Envanex.Infrastructure/Identity/IdentityRoleSeeder.cs`.
- **Alternatifi neydi:** Rolleri migration içinde `HasData` ile tohumlamak. Bu yol rolleri şemaya gömer ve `ConcurrencyStamp`, `NormalizedName` gibi Identity alanlarını elle doldurmanı gerektirir. PR "hiç migration yok" hedefiyle yazıldığı için tohumlama host açılışına kondu.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/security/authorization/roles

### User-secrets
- **Ne işe yarar:** Geliştiricinin makinesinde, repo dışındaki bir JSON dosyasında (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`) yapılandırma değeri tutar. Yalnızca `Development` ortamında yüklenir. PR 6a'da JWT imza anahtarı için geldi; bu PR'da `Demo:Password` da aynı yolu izliyor.
- **Bu projede nerede:** `Demo:Enabled` ve `Demo:Password` yerelde buraya yazılıyor. `appsettings.json` bilerek `"Password": ""` taşıyor.
- **Alternatifi neydi:** Parolayı `appsettings.Development.json`'a yazmak. O dosya commit'lenir.
- **Nerede okunur:** https://learn.microsoft.com/aspnet/core/security/app-secrets

## Kavramlar

### Kimlik doğrulama ile yetkilendirme; 401 ile 403

Kimlik doğrulama (authentication) "sen kimsin" sorusudur, yetkilendirme (authorization) "buna iznin var mı" sorusudur. İkisinin başarısızlığı farklı kodlarla ifade edilir. **401** "seni tanımıyorum" demektir; framework buna *challenge* der ve şemaya göre farklı cevap verir: bearer şeması 401 döner, cookie şeması giriş sayfasına 302 ile yönlendirir. **403** ise "seni tanıyorum ama iznin yok" demektir; framework buna *forbid* der. Oturum açmış ama hiç rolü olmayan bir kullanıcı `/`'e gittiğinde 403 alır, 302 değil. Onu giriş sayfasına göndermek anlamsız olurdu, çünkü zaten giriş yapmış durumda.

### Varsayılan olarak kapalı: fallback politikası ve `[AllowAnonymous]`

İki strateji var. Birincisinde her uç noktaya tek tek `[Authorize]` yazarsın. İkincisinde her şeyi kapatır, açık kalması gerekenlere `[AllowAnonymous]` yazarsın. Bu PR ikincisini seçti. Gerekçe, iki stratejinin **unutma durumunda** ne yaptığına bakınca anlaşılıyor:

- Birinci stratejide unutulan bir `[Authorize]` açık bir kapıdır. Hiçbir test kırmızıya dönmez, çünkü uç nokta beklendiği gibi 200 döner. PR 8 ve PR 10 yeni uç noktalar ve ekranlar ekleyecek; bu hata tam oralarda yapılacak türden.
- İkinci stratejide unutulan bir `[AllowAnonymous]` kapalı bir kapıdır. Giriş sayfası açılmaz, biri hemen fark eder.

Yani sessiz bir güvenlik açığı yerine gürültülü bir işlev hatası üretmeyi tercih ettik. Bunun bedeli şu: framework'ün kendi uç noktaları dahil, anonim erişilmesi gereken her şeyi bulup adlandırman gerekiyor. Politika `RequireAuthenticatedUser()`'dan ibaret. Yani fallback bir rol şartı koymuyor, yalnızca oturum açılmış olmasını istiyor.

İstisna listesi (her satırın kendi kanıtlayan testi var):

| # | Ne | Mekanizma |
|---|---|---|
| 1-3 | `POST /api/auth/login`, `/refresh`, `/logout` | `AuthController` sınıfında `[AllowAnonymous]` |
| 4 | `/not-found` | `.razor` dosyasında `@attribute [AllowAnonymous]` |
| 5 | eşleşmeyen yol | istisna yok: anonim çağırana 302, oturum açmış olana 404 |
| 6 | `/Error` | `@attribute [AllowAnonymous]` |
| 7 | `/login` | `@attribute [AllowAnonymous]` |
| 8 | statik dosyalar | `app.MapStaticAssets().AllowAnonymous()` |
| 9-10 | OpenAPI ve Scalar, yalnızca Development | `.AllowAnonymous()` |
| 11 | `_framework/blazor.web.js` | 8. satırın doğal sonucu; betik asset manifest'ten sunuluyor |
| 12 | `/_blazor` uç noktaları (negotiate, transport, disconnect) | route pattern'e bakan kendi convention'ı |
| 13 | eşleşmeyen `/api/*` yolları | istisna değil, bir davranış değişikliği: artık 404 yerine gövdeli 401 |

Üç satır ayrıca açıklama istiyor:

- **Logout (satır 3):** Logout access token değil refresh token alıyor. Access token'ı süresi dolmuş bir istemci de çıkış yapabilmeli; hatta çıkışın en çok gerektiği an tam da budur. Karar 14 bunu açıkça yazdı, çünkü bir auth uç noktasındaki istisna, gerekçesi yazılmadıkça gözden kaçmış gibi okunur.
- **Satır 5:** Anonim bir kullanıcı var olmayan bir sayfaya gittiğinde 404 değil, giriş sayfasına 302 alıyor. Challenge, routing "böyle bir yol yok" diyemeden önce gerçekleşiyor. Bunun bir yan faydası da var: anonim bir çağıran hangi sayfaların var olduğunu yanıt kodlarından çıkaramaz.
- **Satır 12:** Spike C, `MapRazorComponents<App>().AllowAnonymous()` çağrısının hub uç noktalarıyla birlikte `/`'i ve **diğer her sayfayı** da açtığını gözlemledi. Kararın bütün amacı sayfaların varsayılan olarak kapalı olmasıydı, dolayısıyla bu yol kullanılamazdı. Yerine yalnızca `/_blazor` önekli route pattern'lere `AllowAnonymousAttribute` ekleyen dar bir convention yazıldı. Ön ek kullanıldı, sabit bir liste değil, çünkü bazı pattern'ler sonda `/` taşıyor (`/_blazor/disconnect/`).

`Home.razor` yalnızca oturum açmış olmayı değil `CanRead`'i de istiyor. Bir ERP'nin açılış sayfası envanter verisi gösterir; oturum açmış ama rolü olmayan bir kullanıcının bu veriyi görmesi için bir neden yok.

### Kimlik doğrulama şeması; challenge, forbid, sign-in

Şema (scheme), "kimliği nereden okuyacağım, reddedince ne yapacağım" sorularının adlandırılmış bir cevabıdır. Her şemanın dört davranışı var: *authenticate* (isteğin kimliğini okur), *challenge* (kimlik yoksa ne yapılacağı), *forbid* (kimlik var ama izin yoksa ne yapılacağı), *sign-in/sign-out* (kimliği yazmak ya da silmek). Bu PR'da üç şema var: santral (`Envanex`), cookie (`Envanex.Cookie`) ve bearer (`Bearer`). İlk dört varsayılan santrali gösteriyor; sign-in ve sign-out yalnızca cookie'yi. Bearer token'a "sign-in" yapılmaz, token login uç noktasından alınır.

### İki şema, yol önekiyle seçim ve neden `/api/*` yalnızca bearer

Santral şu kurala göre çalışıyor: yol `/api` ile başlıyorsa bearer, başlamıyorsa cookie. Bu tür santrallerde yaygın olan anahtar ise "`Authorization` başlığı var mı?" sorusudur. Araştırma o yolu açıkça reddetti. Nedenini frontend tarafından düşün: tarayıcıda oturum cookie'si olan bir kullanıcı, başlık eklemeden `fetch('/api/products')` çağırsın. Başlık varlığına bakan bir santral bu isteği cookie şemasına verir. Cookie şeması da kimliği bulamayınca giriş sayfasına **302** döner. `fetch` yönlendirmeleri varsayılan olarak sessizce takip eder, dolayısıyla React kodun `200 text/html` bir giriş sayfası alır ve `res.json()` patlar. Bir API'nin böyle cevap vermemesi gerekir.

Yol önekinin ikinci ve daha önemli sonucu şu: `/api/*` altında cookie **hiç okunmuyor**. Oturum açmış bir tarayıcı bile orada anonimdir. Bu sayede REST yüzeyinde CSRF yapısal olarak imkânsız oluyor. CSRF, tarayıcının cookie'yi başka bir sitenin tetiklediği isteğe de kendiliğinden eklemesine dayanan bir saldırıdır. Cookie'yi hiç okumayan bir uç noktaya bu yolla kimlik taşınamaz. Controller'larda antiforgery yok (`UseAntiforgery` yalnızca Blazor uç noktalarını koruyor). `SameSite=Lax` riski azaltıyor ama tamamen ortadan kaldırmıyor. Çok şemalı alternatifte ise her yazma uç noktası, token olmadan yalnızca cookie ile erişilebilir olurdu. Bu klasik CSRF biçimidir ve REST yüzeyinin tamamına antiforgery eklemeyi gerektirirdi. Karar metnindeki ifadeyle: saldırıyı ortadan kaldırmak, ona karşı savunma yapmaktan iyidir.

Bunun bir bedeli var ve kaydedildi: tarayıcıdaki bir oturum REST'i çağıramaz, `/api/products/datasource` dahil. Bugün buna ihtiyaç duyan bir istemci yok, çünkü Radzen grid'i Blazor devresinin içinde çalışıp Application katmanını doğrudan çağırıyor (Karar 10). Soru `showcase/devexpress` dalında yeniden açılacak: orada tarayıcıdaki bir DevExpress istemcisinin bearer token tutması gerekecek. Cookie şeması tam da bu sorudan kaçınmak için seçilmişti.

### Claim, rol claim'i ve claim tipi

Claim, kimlik hakkında bir önermedir: "sub = 42", "email = x@y", "role = Viewer". Claim tipi o önermenin anahtarıdır. `RequireRole` rolü, kimliğin `RoleClaimType` olarak tanımladığı tipten okur. Burada iki şema bu tipi farklı yazıyor:

- Cookie kimliğini Identity'nin claims factory'si üretiyor ve rolleri uzun URI biçimindeki `ClaimTypes.Role` altına yazıyor.
- Access token'ı biz üretiyoruz ve rolleri kısa `"role"` adıyla yazıyoruz (`EnvanexClaimTypes.Role`).

`IsInRole` her kimliğin kendi `RoleClaimType`'ına baktığı için iki kimlik aynı `RequireRole`'ü ortak bir tip paylaşmadan karşılıyor. Bu varsayılmadı, iki tarafı da testle kanıtlandı: bearer tarafını `BearerRoleClaimTests`, cookie tarafını ise `CookieAuthPipelineTests`'teki Viewer 200 ve rolsüz 403 çifti kanıtlıyor.

`MapInboundClaims = false` da aynı konuyla ilgili. JWT handler'ı varsayılan olarak gelen kısa claim adlarını uzun WS-Federation URI'lerine çevirir. Bu açık kalsaydı `"role"`, `RoleClaimType = "role"` ayarıyla hiçbir zaman eşleşmezdi ve her rol kontrolü sessizce başarısız olurdu.

### Roller token'a refresh anında yazılıyor: 30 gün değil, 15 dakika

Access token imzalı bir fotoğraftır. Sunucu her istekte veritabanına sormaz, token'da ne yazıyorsa ona inanır. Bir kullanıcının `Administrator` rolünü geri aldığında soru şudur: bu değişiklik ne zaman etkili olur?

Rolleri token'a yazmak için iki yol vardı:

1. **Rolleri girişte okuyup refresh boyunca taşımak.** Yeni token eskisinin rollerini kopyalar. Bu durumda geri alınan bir rol, refresh ailesi yaşadıkça, yani en fazla 30 gün boyunca yaşar.
2. **Her token basılırken rolleri yeniden okumak.** Bu PR bunu yaptı: login'de `UserManager.GetRolesAsync`, refresh'te `RefreshTokenService` içindeki `UserRoles`×`Roles` join'i. Geri alınan rol bir sonraki refresh'te düşer. Access token 15 dakika yaşadığı ve `ClockSkew = TimeSpan.Zero` olduğu için bu en geç 15 dakika demek.

Bu bir güvenlik özelliği ve bunu başka hiçbir yer yazmıyor; bu yüzden ADR 0008'e girmesi istendi. Bir de şuna dikkat et: bu özellik yalnızca bearer tarafında geçerli. Cookie tarafında bunun karşılığı yok (güvenlik incelemesi bölümüne bak).

Refresh'teki okumanın bir yan etkisi de önemliydi. O satır olmasaydı yenilenen token rolünü kaybederdi ve kullanıcı girişten 15 dakika sonra 403 almaya başlardı. Yalnızca taze login'i test eden hiçbir test bunu göremezdi.

Login tarafında `GetRolesAsync` yalnızca başarı yolunda ve `ResetAccessFailedCountAsync`'ten sonra çağrılıyor. Hata dallarından birinde çağrılsaydı yalnızca o dalın ödediği bir veritabanı turu olurdu. Bu da PR 6a'nın kapatmaya çalıştığı zamanlama kanallarına dördüncüsünü eklerdi. `CountingUserManager` üç hata dalında da `GetRolesCallCount.ShouldBe(0)` iddia ediyor. Bugün kodun biçimi bunu zaten sağlıyor, ama kod biçimi refactoring sırasında sessizce değişebilir. Bu yüzden iddia yazıldı.

### Statik SSR ile interaktif render

Blazor bir bileşeni iki şekilde çalıştırabilir. **Statik sunucu render'ında (static SSR)** bileşen normal bir HTTP isteği içinde HTML üretir ve iş biter. Bu modda `HttpContext` vardır, başlık ve cookie yazılabilir. **İnteraktif render'da** bileşen SignalR üzerinden açık tutulan bir devrede yaşar. Orada `HttpContext` yoktur, yanıt çoktan başlamıştır ve `Set-Cookie` yazılamaz. Giriş yapmak bir cookie yazmak demek, dolayısıyla giriş sayfası statik olmak zorunda. Radzen bileşenleri interaktif render gerektirdiği için giriş sayfasında hiç Radzen yok; sayfa düz HTML ve bir `EditForm`'dan oluşuyor. Bu PR'daki hiçbir sayfa bir render mode belirtmiyor, yani bugün her şey statik.

Bunun iki sonucu var. Birincisi, `[Authorize]` taşıyan bir sayfa bileşen render edilmeden önce, uç nokta katmanında `UseAuthorization` tarafından reddediliyor. Bu yüzden `Routes.razor`'daki `NotAuthorized` şablonuna bugün hiçbir istek ulaşamıyor. Şablon yine de eklendi, çünkü PR 7'nin interaktif bileşenlerinde gezinme devrenin içinde gerçekleşecek ve uç nokta katmanına hiç uğramayacak. Dosyadaki yorum, birinin şablonu ölü kod sanıp silmesini engellemek için orada. İkincisi, `.razor` dosyasındaki `@attribute [Authorize]` uç nokta metadata'sına ulaşıyor (Spike D → D1). Bu yüzden ayrı bir convention sınıfına gerek kalmadı.

### Spike

Spike, bir tasarım kararını etkileyen ama belgelerden kesin cevabı çıkmayan bir soruyu cevaplamak için yazılan, sonra atılan deney kodudur. Faz 0 yedi soruyu beş spike ile cevapladı. Ortaya kalıcı tek bir çıktı çıktı: ham `curl` çıktılarının yapıştırıldığı bir not (`thoughts/shared/debug/2026-09-20_pr6b-blazor-auth-spikes.md`). Faz sonunda `src/` ve `tests/` altında bu deneylerden tek bir değişiklik kalmadı. Spike'ların ne kadar gerekli olduğunu şuradan görebilirsin: soruların üçü planın tahmininden farklı cevap verdi. Hub uç noktaları kendi mekanizmasını istedi. Eşleşmeyen yol 404 değil 302 döndü. Sayfayı yalnızca GET etmek bile login limitinden bir hak harcıyordu.

### Yeniden çalıştırma kuralı: yanıt başladıysa hiçbir şey yeniden çalıştırılmaz

`UseStatusCodePagesWithReExecute("/not-found")` gövdesi boş bir 4xx gördüğünde isteği `/not-found` yolu için pipeline'ın **tamamından** yeniden geçirir. Yeniden çalıştırılan istek kendi durum kodunu da yazabilir. Spike C bunu iki kez gözlemledi:

- Anonim `/_blazor/disconnect`'in 400'ü istemciye gövdesiz bir 401 olarak döndü. Çünkü o sırada `/not-found`'un kendisi kapalıydı.
- Eşleşmeyen `/api/auth/register` yolundaki bearer 401'i istemciye `302 /login?ReturnUrl=%2Fnot-found` olarak döndü. Zincir şöyle: gövdesiz 401 → yeniden çalıştırma → `/not-found` `/api` altında olmadığı için santral isteği cookie şemasına verir → cookie şeması yönlendirir. Sonuçta bir API yolu HTML giriş sayfasına yönlenmiş oldu.

Faz 4'ün mutasyon koşusu bu yönlendirmenin **iki kusurun birlikte** bulunmasını gerektirdiğini gösterdi: challenge gövdesiz olacak ve `/not-found` kapalı olacak. Yalnızca gövde eksik olsaydı sonuç 401 olarak kalırdı ama `text/html` bir not-found sayfası taşırdı. Bir API istemcisi için bu da yanlış, ama en azından yönlendirme değil.

Çözüm iki parçalı. Birincisi, her ret yolu bir gövde yazıyor: bearer tarafında `OnChallenge` ve `OnForbidden` (ProblemDetails, `Title` İngilizce ve `ResultExtensions.GetReasonPhrase` ile aynı, `Detail` Türkçe), cookie tarafında `OnRedirectToAccessDenied` (Türkçe HTML). Gövde yazmak yanıtı başlatır, başlamış bir yanıt da yeniden çalıştırılmaz. İkincisi, `/not-found` anonime açık. Bu nezaket için değil: yeniden çalıştırma hedefi kapalı kaldığı sürece uygulamadaki her gövdesiz 4xx, `/not-found`'u reddeden şemanın ürettiği cevaba dönüşür.

### Demo hesabı

Demo hesabı herkese açık bir parolayla verilecek, salt okunur bir hesap. Beş parçası var. Her biri ayrı bir hatayı önlüyor.

**1. `Demo:Enabled` kapısı.** Açma kararı ortam adına değil yapılandırmaya bağlı. `if (env.IsProduction())` gibi bir kontrol test edilemez; `Demo:Enabled` ise bir test fabrikasında tek satırla açılıp kapanabilir. Varsayılan değeri `false`, parolanın varsayılanı da boş. `DemoAppSettingsTests`, commit'lenen `appsettings.json`'ı diskten okuyup iki değeri de doğruluyor. Birisi yerelde denemek için `"Enabled": true` yazıp commit'lerse test kırmızıya döner. Roller ise bu kapıdan bağımsız, **her açılışta** tohumlanıyor. Rolleri olmayan bir üretim host'u kimseye izin veremez.

**2. Her yazmadan önce doğrulama.** `DemoAccountSeeder.SeedAsync`'in sırası bilinçli olarak şöyle: parola kontrolü → mevcut hesabın rollerini okuma (R1) → rolleri tohumlama → kullanıcıyı oluşturma → role ekleme. Parola boşsa, `user-secrets` komutunu da içeren bir mesajla exception fırlatıyor. Parola politikaya uymuyorsa hostun `UserManager.PasswordValidators` listesindeki her doğrulayıcıdan geçiriliyor. Bu sayede kontrol edilen politika `AddEnvanexIdentity`'nin tanımladığı politikanın kendisi oluyor, bir kopyası değil. Mesajda yalnızca Identity hata kodları var (`PasswordTooShort` gibi); açıklama metni ya da parolanın kendisi hiçbir zaman yer almıyor. Testler exception'a ek olarak rol tablosunun boş ve kullanıcı sayısının 0 olduğunu da iddia ediyor. Yanlış yapılandırılmış bir host arkasında hiçbir satır bırakmıyor, roller dahil.

**3. Açılışta çökmek.** Sözleşme iki biçimli. Yanlış yapılandırma (boş ya da politikaya uymayan parola) bir `InvalidOperationException` fırlatıyor, çünkü bu bir iş sonucu değil, operatör hatası (JWT tarafındaki `JwtOptionsGuard` ile aynı mantık). Identity'nin hesabı oluşturmayı reddetmesi ise beklenen bir tohumlama sonucu olarak `Result.Failure` dönüyor. Sonra `IdentitySeedingExtensions.SeedIdentityAsync` başarısız `Result`'u host sınırında exception'a çeviriyor. Çağrı `app.Build()` ile `app.Run()` arasında, yani host hiç istek kabul etmeden önce. Gerekçe: yapılandırmanın istediğini yerine getirmeden ayağa kalkan bir host, hiç kalkmayan bir hosttan daha kötüdür. Kimsenin seçmediği bir parolayla açık bir demo hesabı dağıtmaktansa açılmamayı tercih ediyoruz. Spike E, bu konumdaki kodun `WebApplicationFactory` altında da çalıştığını ölçtü; o yüzden hosted service'e taşımaya gerek kalmadı. `Host_WhenDemoSeedingFails_ShouldNotStart` bu satırın testi.

**4. Rol değişmezi (R1).** Parolası herkese açık bir hesabın `Viewer`'dan başka bir rol taşıması, herkese açık bir parolayla yazma yetkisi demektir. Kod incelemesi bunu ilk bulgusu olarak işaretledi. Şimdi `Demo:Email`'deki hesap zaten varsa seeder önce rollerini okuyor. Hesap `Viewer` dışında bir rol taşıyorsa hiçbir şey yazmadan `DemoAccount.HasOtherRoles` dönüyor ve açılış çöküyor. Böylece biri o hesaba elle `Administrator` eklese de, `Demo:Email` gerçek bir yöneticinin adresine denk gelse de sistem sessizce devam etmiyor. Kontrol `EnsureRolesAsync`'ten önce çalıştığı için reddedilen bir açılış rol tablosuna da dokunmuyor. Test edilmemiş ve kabul edilmiş bir kenar durumu da var: rolü olmayan mevcut bir demo kullanıcısı bir sonraki açılışta `Viewer` alıyor. Bu, `CreateAsync` ile `AddToRoleAsync` arasında bir çökme olursa ortaya çıkan durum.

**5. User-secrets pin kanıtı.** `DevelopmentEndpointExemptionTests` host'u `Development` ortamında kuruyor, çünkü OpenAPI ve Scalar yalnızca orada var. `Development` ortamı da user-secrets'ı yüklüyor. Geliştirici kendi makinesinde demoyu açmışsa, test suiti o geliştiricinin gizli yapılandırmasına göre farklı davranırdı. `EnvanexWebApplicationFactory` bu yüzden `Demo:Enabled=false`'u sabitliyor (pin). M16a ve M16b mutasyonları pin'in `appsettings.json`'ı yendiğini kanıtladı. User-secrets'ı da yendiği ise elle kanıtlanana kadar DOĞRULANMAMIŞ bir iddiaydı. Kanıt şöyle yapıldı: user-secrets'a `Demo:Enabled=true` ve `Demo:Password=short` yazıldı. Pin yerindeyken üç testin üçü geçti. Pin satırı yoruma alınınca üçü de açılışta şu mesajla çöktü: "Demo:Password does not satisfy the password policy, and Demo:Enabled is true. Identity reported: PasswordTooShort, PasswordRequiresDigit, PasswordRequiresUpper". Bu tek deney üç şeyi birden gösterdi: test host'u user-secrets'ı gerçekten okuyor, onu durduran şey pin, ve hata mesajı parolayı sızdırmıyor.

### Tohumlama idempotent olmalı; "ulaşılan" ve "savunma amaçlı" catch

Seeder her host açılışında çalışır, dolayısıyla ikinci çalışması bir hata değil, hiçbir şey yapmayan bir işlem olmalı. `IdentityRoleSeeder`'ın eşzamanlılık hikâyesi öğretici. `RoleValidator` yinelenen adı yazmadan **önce** veritabanını okuyarak kontrol ediyor. İki host aynı anda okursa ikisinin doğrulayıcısı da geçer ve kaybedenin INSERT'ü `RoleNameIndex`'e çarpıp 2601/2627 hatası alır. Seeder'ın yorumu bu yarışı tolere ettiğini iddia ediyordu ama etmiyordu; bunu db-review yakaladı. Şimdi bir catch var ve `EnsureRolesAsync_RunByTwoHostsAtOnce_...` testi iki seeder'ı yirmi iterasyon boyunca yarıştırıyor. Clause silinince test her koşuda ilk saniyede kırmızıya dönüyor.

Bunu ADR 0007'deki `RotateAsync` clause'u ile yan yana koy. Catch'in biçimi aynı, kanıt ters yönde: orada clause korundu ama hiçbir yoldan ulaşılamadığı gösterilmişti, burada ulaşıldığı gösterildi. `DemoAccount.AddToRoleFailed` dalı da `RotateAsync` kategorisine giriyor: yalnızca bir mutasyon altında ulaşıldı, korundu, ama onu tetikleyen bir test yok. Demo kullanıcısının kendi ilk tohumlaması eşzamanlı iki host'a karşı güvenli değil (R3). Kaybedenin açılışı çöküyor ve yeniden başlatınca düzeliyor. Üretim tek bir App Service örneği olduğu için bu sınır bilinçli olarak kabul edildi ve kaydedildi.

## Komutlar ve ne yaptıkları

### `dotnet run --project src\Envanex.Web --launch-profile http`
`launchSettings.json`'daki `http` profilini seçer, yani yalnızca `http://localhost:5216`. Spike'ların hepsi bu profille koştu, çünkü `https` profilinde `UseHttpsRedirection` her düz HTTP isteğine 307 döner ve ölçülmek istenen cevap görünmez. Aynı sebeple `appsettings.Development.json` `Auth:Cookie:SecurePolicy`'yi `SameAsRequest` yapıyor: tarayıcı, düz HTTP üzerinden gelen `Secure` işaretli bir cookie'yi saklamaz. Üretimde bu anahtar yok ve cookie `Always` kalıyor.

### `dotnet run ... --launch-profile http -- --RateLimiting:Login:PermitLimit=2`
`--`'dan sonraki her şey `dotnet`'e değil uygulamaya gider. `WebApplication.CreateBuilder(args)` komut satırını da bir yapılandırma kaynağı olarak okur ve komut satırı `appsettings`'i ezer. Spike B login limitini bu şekilde, hiçbir dosyaya dokunmadan 2'ye indirdi.

### `curl.exe -i -c cookies.txt ...` / `-b cookies.txt` / `-s -o NUL -w "%{http_code}"`
Windows PowerShell'de `curl` bazı sürümlerde `Invoke-WebRequest`'in takma adıdır; `curl.exe` gerçek curl'ü çağırır. `-i` yanıt başlıklarını da yazdırır. `-c` gelen cookie'leri dosyaya kaydeder, `-b` o dosyadaki cookie'leri isteğe ekler; Spike A antiforgery cookie'sini bu şekilde taşıdı. `-s -o NUL -w "%{http_code}"` gövdeyi Windows'un null cihazına atar ve yalnızca durum kodunu basar. `/dev/null` Windows'ta yok, karşılığı `NUL`.

### `dotnet user-secrets set "Demo:Password" "<değer>" --project src\Envanex.Web`
Değeri projenin `UserSecretsId`'sine bağlı, repo dışındaki `secrets.json`'a yazar. Duman testinden sonra değerler `dotnet user-secrets remove` ile silindi. Bırakılsaydı sonraki her Development koşusu demoyu açık görürdü.

### `dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~AuthPipelineTests|FullyQualifiedName~CookieAuthPipelineTests"`
`~` "içerir", `|` "veya" anlamına gelir. Bir fazın dokunduğu sınıfları hızlıca koşturmak için kullanıldı. Faz sonu doğrulaması yine de her zaman filtresiz `dotnet test` ile yapıldı.

### `git status --porcelain -- src tests`
Makinenin okuyabileceği biçimde, yalnızca verilen yollardaki değişiklikleri listeler. Faz 0'ın kabul şartı bu komutun **boş** çıktı vermesiydi: spike'lar `src/` ve `tests/` altında iz bırakmamalıydı.

### `git diff --name-only 5e4fcf6~1 5a305ba -- src/Envanex.Infrastructure/Migrations`
`~1` bir commit'in ebeveyni demek. Komut, Faz 5'in tüm commit aralığında migration klasöründe değişen dosyaları listeler. Çıktının boş olması, db-reviewer'ın DOĞRULANMAMIŞ tek iddiasını ("Faz 5 migration'a dokunmadı") kanıtlanmış bir iddiaya çevirdi.

### `dotnet build -warnaserror`
Her uyarıyı hataya çevirir. Bu PR'da beklenmedik bir yan etkisi görüldü: M2 mutasyonunu derlenemez hale getirdi. Ayrıntısı aşağıda.

### `dotnet ef database update` ve `ENVANEX_CONNECTION_STRING`
EF'in design-time factory'leri bağlantı dizesini user-secrets'tan değil `ENVANEX_CONNECTION_STRING` ortam değişkeninden okuyor. Bu değişken yoksa komut başarısız oluyor. `CLAUDE.md`'deki migration komutları bunu söylemiyor; eksiklik yol haritasına eklendi.

## Dikkat edilen tuzaklar

### Login sayfasının GET'lerinin login limitini yemesi, ve bunun yanlış düzeltmesi
Blazor'da bir sayfanın GET'i ile form POST'u aynı uç noktadır. `Login.razor`'daki `[EnableRateLimiting("login")]` bu yüzden her sayfa açılışında da bir hak harcıyordu. Üretimdeki limitle (5 istek / 300 sn) beş sayfa yenilemesi girişi beş dakika kilitlerdi. İlk düzeltme POST olmayan isteklere `GetNoLimiter(partitionKey)` döndürdü; yani POST limiter'ının kullandığı adres anahtarının aynısını. Rate limiter bir partition anahtarı için limiter'ı **bir kez** kurar ve host yaşadığı sürece yeniden kullanır. Sonuç: bir adresten ilk hangi metot geldiyse o adresin limiter'ını kalıcı olarak o belirledi. Tek bir `/login` ziyareti, o adres için `POST /api/auth/login`'deki brute-force korumasını kapattı; bu, PR 6a'nın getirdiği korumaydı. Ters sıralamada da bozuktu: önce POST'lar hakları bitiriyor, ardından her `/login` GET'i 429 alıyordu. Spike B bu hatayı kaçırdı, çünkü GET ve POST dizileri arasında host'u yeniden başlatmıştı ve iki metot hiç aynı partition'ı paylaşmadı. Doğru düzeltme GET'lere ayrı ve sabit bir anahtar vermek: `"login-non-post"`. Bu PR'ın PR 6a'yı gerileten tek noktası buydu ve gerçek bir host üzerinde elle gözlenerek yakalandı.

### `TokenValidationParameters` nesnesinin tamamen yeniden atanması
`JwtAuthenticationExtensions` `TokenValidationParameters`'a yeni bir nesne atıyor. `RoleClaimType`'ı bu atamadan **önce** ayrı bir satırda set etseydin değer sessizce kaybolurdu ve test yanıltıcı bir mesajla kırmızıya dönerdi. Bu yüzden iki claim tipi aynı object initializer'ın içine yazıldı.

### `AccessDeniedPath` set etmek
`AccessDeniedPath` set etmek cookie şemasının forbid'ini bir Razor sayfasına 302'ye çevirirdi. Karar 3 gövdeli bir 403 istiyor. Bu yüzden gövdeyi `OnRedirectToAccessDenied` yazıyor. Bedeli şu: tarayıcıdaki "erişim reddedildi" ekranı bir `.razor` dosyası değil, `EnvanexAuthenticationEvents` içindeki bir string literal. Bu kod tabanında kullanıcıya dönük markup'ın Razor dışına çıktığı tek yer burası. PR 7'de 403'ü koruyan bir yeniden çalıştırmayla değiştirilecek.

### `HandleResponse()` ve `WWW-Authenticate`
`OnChallenge` gövdeyi yazdıktan sonra `context.HandleResponse()` çağırıyor. Çağırmasaydı handler olayın ardından çalışmaya devam eder ve başlamış bir yanıta kendi başlıklarını eklemeye çalışırdı. Bunun bedeli, bu uygulamadaki hiçbir 401'in `WWW-Authenticate` başlığı taşımaması. Bu RFC 7235'ten bir sapma ve bilinen bir boşluk olarak kaydedildi. Bir yan etkisi de oldu: istisna satırı 2'nin testi başlangıçta "`WWW-Authenticate` yok" diye iddia ediyordu. Artık hiçbir 401 bu başlığı taşımadığı için bu iddia istisna kaldırılsa da yeşil kalacaktı. Test, ProblemDetails'teki `Detail` metnini iddia edecek şekilde yeniden yazıldı; bu metin iki farklı 401'i birbirinden ayırt edebiliyor.

### Kayıtlı olmayan bir politika adı
`[Authorize(Policy = "CanRead")]` hiçbir `AddPolicy` ile kaydedilmemiş bir adı gösterirse yetkilendirme anında exception fırlar. Plan önce politikaları Faz 4'e koymuştu. `Home.razor` `CanRead`'i Faz 3'te istediği için politikalar da Faz 3'e taşındı: politikayı kaydeden kod ile onu adıyla kullanan attribute aynı fazda gelmek zorunda.

### Sabit `Secure` cookie ve test sunucusu
Test istemcisinin adresi `http://localhost`. `Secure` işaretli bir cookie'yi istemcinin cookie container'ı düz HTTP üzerinden geri göndermez. Fabrika bu yüzden `Auth:Cookie:SecurePolicy=SameAsRequest` set ediyor. Set etmeseydi on iki cookie testi, test ettikleri kodla hiç ilgisi olmayan bir sebeple kırmızıya dönerdi. `TestHost_CookieOptions_...` testi bu ayarı doğrudan okuyor, böylece ayar düşerse kırmızı mesaj asıl sebebi gösteriyor.

### Honour edilmeyen `ReturnUrl`
Cookie şeması `/login?ReturnUrl=/istenen-sayfa` ile yönlendiriyor ama giriş sayfası bu parametreyi yok sayıp her zaman `/`'e dönüyor. Bu bilinçli bir tercih. `ReturnUrl`'i körü körüne takip etmek bir *open redirect* açığıdır: `?ReturnUrl=https://kotu.site` içeren bir bağlantı, kullanıcıyı senin giriş sayfandan geçirdikten sonra saldırganın sitesine götürür. Doğru uygulama doğrulama ve test ister; bu işler ayrı bir chore olarak bırakıldı.

### Herkesin kilitleyebileceği demo hesabı
`Lockout.AllowedForNewUsers = true` olduğu için beş yanlış parola demo hesabını 15 dakika kilitler, istek hangi adresten gelirse gelsin. Hesabın parolası zaten herkese açık olacağı için kilit orada hiçbir şeyi korumuyor, yalnızca bir hizmet engelleme aracı oluyor. PR 7'ye bırakıldı.

## Doğrulama disiplini: bu PR'ın öğrendikleri

### Geçen bir test, koruyan bir test değildir
PR 6a bu cümleyi kurmuştu, bu PR onu uygulamaya döktü. Kural şu: bir test ancak koruduğunu iddia ettiği kod kaldırıldığında kırmızıya dönüyorsa bir şeyi koruyordur. Bunu görmenin tek yolu kodu gerçekten bozup testi koşmaktır. Buna mutasyon denir. Bu PR'da mutasyonlar üç testi yakaladı; üçü de yazıldıkları haliyle yeşildi ama hiçbir şeyi korumuyordu:

- `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits` ilk halinde yalnızca GET'leri yapıp hiçbirinin 429 almadığını iddia ediyordu. Yukarıdaki adres anahtarı hatası varken de yeşil kaldı. Güçlendirilmiş hali önce GET'leri yapıyor, sonra 200 dönmesi gereken iki POST ve 429 dönmesi gereken üçüncü bir POST gönderiyor.
- Faz 3'ün mutasyon koşusunda santral "`Authorization` başlığı var mı" sorusuna göre seçim yapacak şekilde değiştirildi; yani araştırmanın reddettiği biçime getirildi. Bütün cookie testleri yeşil kaldı. Karar 4'ü koruyan tek şey santrali doğrudan çağıran `SchemeSelectionTests`'ti. Faz 4 bunun için `ApiPath_WithASessionCookieAndNoBearerToken_ShouldReturn401AndNotARedirect` testini ekledi: oturum açmış bir cookie istemcisi, bearer token olmadan korumalı bir `/api/*` uç noktasına istek atıyor.
- İstisna satırı 2'nin `WWW-Authenticate` iddiası (yukarıda anlatıldı).

### Mutasyon kanıtları commit'lenmiş koda karşı yapılır
Plandaki tabloların başlıklarında "against 5e4fcf6" ve "against 5a305ba" yazıyor. Mutasyon, bilinen bir duruma uygulanan bir farktır. Temel durum commit'lenmemişse "M3 kırmızıya döndü" cümlesinin hangi koda göre söylendiğini kimse bilemez ve kimse tekrarlayamaz. İkinci bir sebep daha var: mutasyon geri alınırken `git checkout -- <dosya>` kullanılır. Çalışma ağacında commit'lenmemiş gerçek bir değişiklik varsa, geri alma işlemi onu da siler. Önce commit, sonra boz, koş, geri al. Commit hash'i kanıtın bir parçasıdır.

### M2 neden derlenmedi (CS0162)
M2'nin amacı `if (!options.Enabled)` dalını bozmaktı. İlk yazılan hali `if (false)` idi ve arkasından bir `return` geliyordu. Derleyici sabit `false`'u görüp altındaki kodu erişilemez kod olarak işaretledi (CS0162, bir uyarı). `TreatWarningsAsErrors` bu uyarıyı hataya çevirdi. Build başarısız oldu ve **hiçbir test koşmadı**. Bu kırmızı testten değil derleyiciden geldiği için bir şey kanıtlamaz. Düzeltilmiş hali `if (bool.Parse("false"))`: çalışma anında aynı değeri üretiyor ama derleyici içini göremiyor. Genel ders: bir mutasyon, derleyicinin değerini hesaplayabildiği bir sabit olmamalı.

### M6 neden hayatta kaldı
M6 boş parola kontrolünü atladı ve `SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrow` yeşil kaldı. Sebebi şu: boş bir parola bir sonraki kontrole, yani politika doğrulamasına düşüyor ve o da reddediyor. Politika kontrolü de `InvalidOperationException` fırlatıyor ve onun mesajı da `Demo:Password` içeriyor. Test yalnızca exception tipini ve bu kelimeyi iddia ettiği için iki kontrolü birbirinden ayırt edemedi. Başka bir deyişle, test çıktının bir özelliğini doğruluyordu ama o çıktıyı hangi kontrolün ürettiğini doğrulamıyordu. Düzeltme testteydi (`4cdf628`): artık yalnızca boş parola kontrolünün mesajında geçen `is not configured` ifadesini de iddia ediyor. Mutasyon yeniden koşturulunca test kırmızıya döndü.

### Bir kırmızı, ancak testin kendi assertion'ı başarısız olduğunda sayılır
MR5'in ilk koşusu herhangi bir assertion'a gelmeden bir Docker API hatasıyla düştü. Bu kırmızı sayılmadı ve koşu tekrarlandı. M2 ile aynı ilke: derleme hatası, altyapı çökmesi, zaman aşımı ya da fixture kurulumundaki bir exception mutasyonun yakalandığını değil, testin hiç koşmadığını gösterir. Mutasyon kanıtında aranan kırmızı, testin kendi `ShouldBe`'sinin başarısız olduğu kırmızıdır.

## Güvenlik incelemesi ne buldu

Claude Code'un yerleşik `/security-review` komutu en yüksek efor ayarıyla tüm dalı `main`'e karşı taradı. 8/10 güven eşiğini geçen bir bulgu çıkmadı. Eşiğin altında kalan iki aday şunlardı: cookie oturumları hiç yeniden doğrulanmıyor (2/10) ve demo hesabı `Demo:Enabled=false` sonrasında da yaşıyor (3/10). İncelemenin asıl katkısı bir bulgu değil, **planda bir olgu hatası** yakalamasıydı. Demo yaşam döngüsü blocker'ı, "security stamp'i döndürmek canlı cookie oturumlarını bitirir" diye yazılmıştı. Bu kurulumda bitirmiyor. `9a50d14`'e karşı yeniden kontrol edildi:

- `AddCookie`'de (`JwtAuthenticationExtensions.cs:66`) `SlidingExpiration` var ama ne `OnValidatePrincipal` ne de `ExpireTimeSpan` var. Varsayılan süre 14 gün ve her istekte uzuyor. `AddIdentityCore` cookie'ye security-stamp doğrulaması bağlamıyor; bunu yapan `AddIdentity`'dir ve PR 6a onu bilerek kullanmamıştı.
- `RevalidatingIdentityAuthenticationStateProvider` bugün hiç çalışmıyor, çünkü hiçbir `.razor` dosyası bir render mode belirtmiyor ve ortada açık bir devre yok. Çalışsaydı bile yalnızca devreyi anonim yapardı; cookie'ye hiç dokunmazdı.

Sonuç olarak bir cookie; kullanıcısı silindikten, rolü alındıktan, stamp'i değiştikten ve hesabı kilitlendikten sonra da geçerli kalıyor. Karar 11'in kilit hedefi (kilitlenmiş bir hesap açık UI oturumunu da kaybetmeli) karşılanmadı.

**Bu neden bugün zararsız, PR 7 için ise blocker?** Bugün cookie yalnızca `/`'i koruyor ve `/`'de veri yok. `/api/*` zaten cookie okumuyor. PR 7 ürün grid'ini cookie'nin arkasına koyacak ve grid Application katmanını doğrudan çağıracak. O andan itibaren silinmiş ya da rolü alınmış bir kullanıcı 14 gün boyunca gerçek veri okuyabilir. Bearer tarafındaki 15 dakikayla karşılaştır: aynı "rolü geri al" işlemi bir kapıda 15 dakikada, diğer kapıda hiçbir zaman etkili olmuyor. Demo yaşam döngüsü blocker'ı da bu açığa bağlı: `Enabled=false`'un hesabı gerçekten iptal edebilmesi için canlı cookie oturumlarını da bitirebilmesi gerekir. PR 7 bu yüzden aralıklı bir security-stamp doğrulayıcısı, açık bir süre sınırı ve canlı oturumlarda kilidin uygulanmasıyla başlamak zorunda; her birinin kendi testi olacak.

## Farklı düşündüğüm yer

**Revalidation yanlış katmana kondu.** Karar 11 "security-stamp revalidation eklenir" diyordu. Faz 3 bunu `RevalidatingIdentityAuthenticationStateProvider` ile yaptı, bir testini de yazdı ve db-reviewer'dan "30 dakikada bir, devre başına bir okuma" maliyetini onaylattı. Oysa bu sınıfın bugün çalışacağı tek bir devre bile yok. `/`'i gerçekte koruyan şey cookie ve cookie'ye hiçbir doğrulayıcı bağlanmadı. Bir kararın kâğıt üstünde yerine getirilmesiyle davranışta yerine getirilmesi arasındaki fark bu. Bu fark, `/security-review` yakalayana kadar planın içinde "stamp döndürmek oturumu bitirir" şeklinde yanlış bir cümle olarak yaşadı. Cookie şemasına `SecurityStampValidator.ValidatePrincipalAsync` ile bir `OnValidatePrincipal` ve açık bir `ExpireTimeSpan` eklemek bu PR'ın kapsamına sığardı. Test altyapısı (`CookieAuthHelper`, rolsüz kullanıcı, `UpdateSecurityStampAsync`) zaten hazırdı. Bunu PR 7'ye blocker olarak ertelemek dürüstçe yapılmış bir kayıt, ama yine de bir erteleme.

**`Demo:Enabled` adı olduğundan fazlasını vaat ediyor.** Bayrağın bugünkü gerçek anlamı "açılışta demo hesabını oluştur". `false` yapmak hesabı kapatmıyor, yeni bir `Demo:Password` da mevcut hesabın parolasını değiştirmiyor. Bir operatör bayrağın adını okuyup "demo kapalı" diye düşünür, oysa `CanRead` hâlâ `Viewer`'ı içerdiği için hesap giriş yapıp okumaya devam eder. Rollback notu bunu açıkça yazıyor ("revert'ten sonra elle sil"). Ama bu bilgi bayrağın kendisinde değil, planın içinde duruyor. PR 7'deki yaşam döngüsü işi bitene kadar ya ad dürüst olmalıydı ya da `appsettings.json`'daki bloğun üstünde bir yorum bulunmalıydı.

**`AddEnvanexJwtBearer` artık üç şema kaydediyor.** Dosyanın ve metodun adı, "çağrı yeri bu adı zaten biliyor" gerekçesiyle korundu. Yeniden adlandırmanın maliyeti tek bir çağrı yeri ve birkaç test. Yanlış ad ise uzun süre yaşayacak: PR 7'de cookie'ye doğrulayıcı eklemeye çalışan biri onu önce "JwtBearer" adlı bir dosyada arayacak. Bu gerekçe, sınıfın kendi remarks bloğunda "adı yanlış" diye yazmak zorunda kalınacak kadar zayıf.

**Plan belgesinin boyutu.** Plan yaklaşık iki bin satıra çıktı. İçinde öngörüler, düzeltmeler, düzeltmelerin düzeltmeleri, mutasyon tabloları ve karar gerekçeleri var. Her düzeltme doğru ve kaydedilmiş. Ama güvenlik incelemesinin bulduğu olgu hatası tam da bu yoğunluğun içinde saklanabildi. "Olduğu gibi" kayıtları plandan ayrı ve daha kısa bir belgede tutmak, planın bir tahmin olarak okunmasını, kaydın ise bir olgu olarak okunmasını kolaylaştırırdı.

## Kendini sına

1. `ProductsController`'a yarın bir `Delete` action'ı eklendiğini ve kimsenin üstüne yetkilendirme attribute'u yazmadığını düşün. Bu action'a anonim bir istek, `Viewer` token'lı bir istek ve `Administrator` token'lı bir istek gelirse her biri hangi durum kodunu alır? Bu PR'ın kurduğu düzen hangi hatayı gürültülü hale getiriyor, hangisini hâlâ sessiz bırakıyor?

2. `CookieSecurePolicyResolver.Resolve`, `AddCookie` callback'inin içinde değil, `AddEnvanexJwtBearer`'ın en başında çağrılıyor. Çağrı callback'in içine taşınsaydı, `Auth:Cookie:SecurePolicy` değerindeki bir yazım hatası ne zaman ve hangi belirtiyle ortaya çıkardı? Bu farkı hangi test yakalardı, hangisi yakalayamazdı?

3. `ForwardDefaultSelector` yolu `StartsWithSegments("/api", ...)` ile kontrol ediyor. Birisi bunu `context.Request.Path.Value!.StartsWith("/api")` ile değiştirirse hangi somut yollar yanlış şemaya gider? O yollara gelen anonim bir istek neyle karşılaşır?

4. `SignOut.razor`, `CanRead` değil politikasız bir `[Authorize]` taşıyor ve `AuthorizationOptions.DefaultPolicy` bilerek `RequireAuthenticatedUser()` seviyesinde bırakıldı. `DefaultPolicy`'yi `RequireRole(Administrator, Viewer)`'a yükseltmek neden cazip görünebilir? Yükseltseydin bugün kimin, hangi işlemi yapamaz hale gelirdi, ve PR 8'de düz bir `[Authorize]` yazan biri ne yanlış anlardı?

5. `CookieSignInService.SignInAsync`, `ValidateCredentialsAsync` başarılı olduktan sonra `FindByIdAsync` null dönerse yeni bir hata kodu değil `AuthErrors.InvalidCredentials` döndürüyor. Ayrıca `IIdentityService`'ten gelen hatayı hiç dönüştürmeden olduğu gibi geçiriyor. Bu metot, örneğin kilitli hesap için ayrı bir `Auth.AccountLocked` üretseydi Blazor kapısı ile REST kapısı arasında nasıl bir fark doğardı? Bu fark kimin işine yarardı?

6. `EnvanexWebApplicationFactory.ConfigureWebHost` içinde `_settingOverrides` döngüsü `builder.UseSetting("Demo:Enabled", "false")` satırından sonra geliyor. Döngü o satırın üstüne taşınsaydı `DemoAccountSeederTests`'teki hangi testler kırmızıya dönerdi, hangileri etkilenmezdi ve neden?

7. `SqlServerFixture.ExpireCachedToken`, önbellekteki token'ın sona erme zamanını geçmişe değil, otuz saniye sonrasına çekiyor. Test seam'i token'ı doğrudan geçmişe tarihleseydi, cache'in "bitimine bir dakikadan az kalmışsa yeniden bas" kuralının hangi yanlış uygulaması `AccessToken_WhenTheCachedTokenIsNearExpiry_ShouldBeRemintedRatherThanReused` testinden yeşil geçerdi?

8. `BlazorHubEndpoints_WithoutAuthentication_ShouldReturnNeither401NorARedirect` üç uç noktanın her biri için üç olumsuz iddia yapıyor: 401 değil, 302 değil, `Location` başlığı yok. Satır 12'nin convention'ı kaldırıldığında negotiate, transport ve disconnect uç noktalarının her biri reddi hangi biçimde döndürür, ve her birini üç iddiadan hangisi yakalar? Test neden "200 dönmeli" gibi pozitif bir durum kodu iddia etmiyor?
