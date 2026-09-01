# PR 0001 — Solution Scaffolding

## Ne yaptik

Bu PR, bos bir repoyu derlenebilir ve test edilebilir bir .NET 10 solution'ina donusturdu. Alti src projesi (Domain, Application, Infrastructure, SoapApi, Web, Worker) ve uc test projesi (Domain.Tests, Application.Tests, IntegrationTests) olusturuldu. Projeler arasindaki referanslar, katmanli mimari kurallarina uygun sekilde tek yonlu olarak baglandi -- Domain hicbir projeye referans vermiyor, bagimliliklar her zaman iceriye (Domain'e dogru) isaret ediyor. Tum NuGet paketleri Central Package Management (CPM) ile tek bir `Directory.Packages.props` dosyasinda yonetiliyor. `dotnet new` sablonlarinin urettigi gereksiz dosyalar (Class1.cs, Counter.razor, Weather.razor, Worker.cs, UnitTest1.cs) silindi, yerlerine mimari kurali dogrulayan dort xUnit testi kondu. Son olarak proje adi Envanta'dan Envanex'e degistirildi.

## Yeni giren teknolojiler

### slnx (XML Solution Format)

- **Ne ise yarar:** .NET'in yeni solution dosya formati. Klasik `.sln` dosyasi GUIDs ve opak satirlardan olusan, merge conflict uretmeye meyilli, insan tarafindan okunamayan bir format. `.slnx` bunun yerine duz XML kullaniyor. Her proje bir `<Project>` elementi, her klasor bir `<Folder>` elementi -- git diff'te ne degistigini gorebiliyorsun.
- **Bu projede nerede:** `Envanex.slnx` (repo kokunde)
- **Alternatifi neydi:** Klasik `.sln` dosyasi kullanilabilirdi. slnx .NET 9'dan itibaren destekleniyor. Trade-off: eski IDE'ler ve bazi ucuncu parti araclar henuz slnx'i desteklemiyor olabilir. Burada bunu goz onune alarak yeni formati tercih ettik cunku proje yeni, geriye donuk uyumluluk sorunu yok.
- **Nerede okunur:** https://learn.microsoft.com/en-us/visualstudio/ide/reference/solution-file

### Central Package Management (CPM)

- **Ne ise yarar:** Normalde her `.csproj` dosyasinda her paketin surumu ayri ayri yazilir. On projen varsa ve hepsi xunit kulllaniyorsa, on farkli yerde surum numarasi bulunur -- birini guncellemeyi unutursan farkli surumler yan yana calisir. CPM, tum paket surumlerini tek bir `Directory.Packages.props` dosyasinda toplar. `.csproj` dosyalari paketin adini yazar (`<PackageReference Include="Shouldly" />`), surumu yazmaz. Surum sadece merkezi dosyada bir kere tanimlanir.
- **Bu projede nerede:** `Directory.Packages.props` (repo kokunde). `ManagePackageVersionsCentrally` property'si `true` yapilarak aktive ediliyor.
- **Alternatifi neydi:** Her `.csproj`'da `Version` attribute'u ile surum pinleme. Kucuk projelerde is gorur ama 9 projelik bir solution'da surum tutarsizligi riski olusturur. Ayrica `dotnet add package` her seferinde `.csproj`'a surum ekler -- CPM aktifken bunu her seferinde temizlemen gerekiyor; bu ek bir adim ama merkezi kontrolun bedeli.
- **Nerede okunur:** https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management

### Directory.Build.props

- **Ne ise yarar:** MSBuild, bir proje derlendiginde diskde yukari dogru yuruyerek `Directory.Build.props` arar ve bulursa icerigini projeye otomatik olarak uygular. Boylece `<TargetFramework>`, `<Nullable>`, `<TreatWarningsAsErrors>` gibi ortak ayarlari her `.csproj`'a ayri ayri yazmak yerine bir kere yaziyorsun. React'teki tsconfig.json'un `extends` mekanizmasina benzetebilirsin.
- **Bu projede nerede:** `Directory.Build.props` (repo kokunde). `net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended` burada tanimli.
- **Alternatifi neydi:** Ayni property'leri her `.csproj`'a kopyalamak. Calisiyor ama DRY prensibini cigniyor ve bir projeyi guncellemeyi unutmak sorun yaratir.
- **Nerede okunur:** https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build

### Scalar (OpenAPI UI)

- **Ne ise yarar:** REST API'lerini test etmek icin tarayici tabanli bir arayuz. Swagger UI'in modern bir alternatifi. Projedeki REST endpoint'lerini kesfetmeni, istek gonderip cevap gormeni saglar. Bu PR'da henuz endpoint yok, sadece altyapi hazirlandi.
- **Bu projede nerede:** `src/Envanex.Web/Program.cs` -- `AddOpenApi()`, `MapOpenApi()`, `MapScalarApiReference()` satirlari, yalnizca Development ortaminda aktif.
- **Alternatifi neydi:** Swagger UI (Swashbuckle). .NET 9'dan itibaren Microsoft, Swashbuckle'i varsayilan sablondan cikarip kendi `Microsoft.AspNetCore.OpenApi` paketini one cikartti. Scalar, bu paketin ustune oturan bir UI katmani.
- **Nerede okunur:** https://github.com/scalar/scalar/tree/main/packages/scalar.aspnetcore

### Shouldly

- **Ne ise yarar:** Assertion kutuphanesi. xUnit'in kendi `Assert.Equal(expected, actual)` syntax'i yerine `actual.ShouldBe(expected)` yazdirir. Hata mesajlari daha okunakli: "expected [X] but was [Y]" yerine gercek ifadeyi gosterir. React'teki jest'in `expect(x).toBe(y)` mantigi -- farkli sozdizimi ama ayni fikir.
- **Bu projede nerede:** Tum test projelerinde `PackageReference` olarak var. `ArchitectureTests.cs`'de `references.ShouldBeEmpty()` ve `references.ShouldBe(...)` ile kullaniliyor.
- **Alternatifi neydi:** FluentAssertions daha populer bir alternatif ama ticari lisans gerektiriyor. xUnit'in kendi Assert sinifi da is gorur ama hata mesajlari daha az bilgi veriyor.
- **Nerede okunur:** https://docs.shouldly.org/

### Testcontainers.MsSql

- **Ne ise yarar:** Entegrasyon testleri sirasinda gercek bir SQL Server 2022 instance'ini Docker container olarak ayaga kaldirir, testi calistirir, container'i yok eder. "In-memory veritabani" kullanmak yerine gercek SQL Server'a karsi test etmeni saglar -- boylece SQL Server'a ozgu davranislar (transaction isolation, collation, stored procedure) testlerde de dogrulaniyor.
- **Bu projede nerede:** `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj`'da `PackageReference` olarak var. Henuz kullanan test yok, altyapi hazirlandi.
- **Alternatifi neydi:** EF Core InMemory provider (testlerde gercek SQL davranisini simule edemez, FK constraint yok, transaction yok -- yalanci guvence verir). Ya da paylasilan bir test veritabani (testler birbirini etkiler, paralel calismaz).
- **Nerede okunur:** https://dotnet.testcontainers.org/modules/mssql/

### Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory)

- **Ne ise yarar:** Entegrasyon testlerinde ASP.NET Core uygulamasini process-ici olarak ayaga kaldirir. Gercek bir HTTP sunucusu baslatmadan tum middleware pipeline'ini, DI container'ini ve routing'i test edebilirsin. `WebApplicationFactory<Program>` sinifindan turetilen bir test factory, uygulamanin `Program.cs`'indeki konfigurasyonu kullanir ama sen istedigin servisi mock ile degistirebilirsin.
- **Bu projede nerede:** `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj`'da `PackageReference` olarak var. Uygulamadan erisim icin `src/Envanex.Web/Program.cs`'in sonuna `public partial class Program { }` eklendi.
- **Nerede okunur:** https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests

## Kavramlar

### Target Framework (net10.0)

Projenin hangi .NET surumunu hedefledigini belirten bir MSBuild property'si. `<TargetFramework>net10.0</TargetFramework>` demek "bu kod .NET 10 runtime'i uzerinde calisacak, .NET 10 API'lerini kullanabilir" demek. React'te `package.json`'daki `engines.node` alanina benzer ama burada derleyici seviyesinde zorunlu -- yanlis framework'u hedefleyen bir proje derlenmez.

### Project Reference vs Package Reference

`ProjectReference`, ayni solution icindeki bir projeye bagimlilik tanimlar -- senin kodun. `PackageReference`, NuGet'ten indirilen ucuncu parti bir kutuphanedir -- baskasinin kodu. npm'deki `dependencies` vs workspace'teki `"@myorg/shared": "workspace:*"` ayrimi gibi dusun. `ProjectReference`'lar bagimlilik grafigini olusturur ve bu PR'daki katmanli mimarinin temelini saglar.

### Layered Architecture ve Dependency Rule

Kodu sorumluluk katmanlarina ayirip bagimliliklari tek yonde akitma prensibi. Bu projede en icte Domain (is kurallari, saf C#, hicbir bagimlilik yok), sonra Application (use case'ler, sadece Domain'i bilir), sonra Infrastructure (veritabani, dis servisler, Domain + Application'i bilir), en distta Web ve Worker (kullaniciya dokunan katmanlar). Kural: ic katman dis katmani bilmez. Domain'e bir `using Envanex.Infrastructure;` eklemek mimari ihlalidir. Bu kural `.csproj` dosyalari uzerinden zorlanir -- `ProjectReference` olmadan `using` calismaz.

### Sdk Attribute (Microsoft.NET.Sdk, Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker)

Her `.csproj`'un ilk satirindaki `<Project Sdk="...">` ifadesi, projenin turunu belirler. `Microsoft.NET.Sdk` genel amacli bir class library, `Microsoft.NET.Sdk.Web` bir ASP.NET Core uygulamasi (HTTP pipeline, static assets, Kestrel sunucu gelir), `Microsoft.NET.Sdk.Worker` ise arka plan servisi (Windows Service veya Linux daemon olarak calisabilir). Her SDK, varsayilan import'lar, build hedefleri ve kullanilabilir API'ler acisindan farklidir.

### PrivateAssets ve IncludeAssets

`Microsoft.EntityFrameworkCore.Design` paketinin `.csproj`'da ozel bir tanimlanma sekli var: `<PrivateAssets>all</PrivateAssets>` ve `<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`. Bu, paketin sadece gelistirme sirasinda (migration olusturma) kullanilacagini, uretim (production) ciktisina dahil edilmeyecegini soyluyor. npm'deki `devDependencies` mantigi -- ama NuGet'te bu ayrim paket bazinda, ItemGroup bazinda degil.

### IsPackable

Test projelerindeki `<IsPackable>false</IsPackable>` property'si, bu projenin NuGet paketi olarak yayinlanmasini engeller. `dotnet pack` calistiginda bu projeyi atlar. Test projelerinin dagitilmasi anlamsiz oldugu icin varsayilan olarak kapatiliyor.

### Transitive Dependency Override

`Directory.Packages.props`'taki `System.Security.Cryptography.Xml` girisi, dogrudan kullanilan bir paket degil. SoapCore'un bagimliligi olan bu paketin 8.0.2 surumunde guvenlik acigi var (NU1903 uyarisi). CPM'de bu paketin surumunu 10.0.11'e pinleyerek, SoapCore'un kullandigi eski surumu zorla yeni surumle degistiriyoruz. npm'deki `overrides` alanina benzer.

### TreatWarningsAsErrors

`Directory.Build.props`'taki `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` her uyariyi derleme hatasina cevirir. Derleyici "bu nullable olabilir" diye uyarmak yerine derlemeyi durdurur. Sert bir kural -- `dotnet new` sablonlarinin urettigi kodda bile uyari kalmamasi gerekiyor, bu yuzden bu PR'da sablon temizligi yapildi.

### AnalysisLevel

`<AnalysisLevel>latest-recommended</AnalysisLevel>` .NET'in yerlesik kod analiz kurallarinin (CA**** numarali kurallar) en guncel ve onerilen alt kumesini aktive eder. `TreatWarningsAsErrors` ile birlikte kullanildiginda, potansiyel guvenlik aciklari, performans sorunlari ve kod kalitesi problemleri derleme hatasina donusur.

### CA1707 ve Test Method Naming

`.editorconfig`'e eklenen `[tests/**/*.cs]` blogu, test dosyalarinda CA1707 kuralini devre disi birakiyor. CA1707 "identifier'larda alt cizgi kullanma" der, ama test metodlari geleneksel olarak `Domain_ShouldNotReference_AnyProject` gibi alt cizgili isimlendirilir. Bu kural test disindaki kodda aktif kaliyor.

### BlazorDisableThrowNavigationException

`Envanex.Web.csproj`'daki bu property, Blazor Server'da sayfa navigasyonu sirasinda firlatilan `NavigationException`'i bastirir. Bu exception, sunucu tarafli render'da beklenen bir mekanizma ama `WebApplicationFactory` ile yapilan entegrasyon testlerinde sorun cikarabiliyor. Test altyapisinin duzgun calismasi icin eklendi.

### public partial class Program

`src/Envanex.Web/Program.cs`'in sonundaki bu satir, top-level statement kullanan bir `Program.cs`'nin sinifini disariya aciyor. C#'ta top-level statement'lar derleyici tarafindan gorunmez bir `Program` sinifina sarmalanir, ama bu sinif `internal`'dir -- test projesinden erisilmez. `public partial class Program { }` ile bu sinifi `public` yaparak `WebApplicationFactory<Program>` icin erisim saglaniyor.

### UseWindowsService / AddWindowsService

Worker projesindeki `builder.Services.AddWindowsService()` cagrisi, uygulamanin Windows Service olarak calisabilmesini sagliyor. Normal terminalden `dotnet run` ile calistirdiginda fark etmez ama `sc.exe create` ile Windows servis olarak kaydedildiginde, servis yasam dongusunu (start/stop/pause) dogru sekilde yonetiyor.

## Komutlar ve ne yaptiklari

### `dotnet new sln -n Envanex -o . --format slnx`
Bulunulan dizinde `Envanex.slnx` adinda yeni bir solution dosyasi olusturur. `--format slnx` yeni XML formatini secer. `-n` solution adini, `-o .` cikti dizinini belirler.

### `dotnet new classlib -n Envanex.Domain -o src/Envanex.Domain`
`src/Envanex.Domain` dizininde `Envanex.Domain.csproj` ve `Class1.cs` iceren yeni bir class library projesi olusturur. `classlib` sablonu en yalkin proje tipi -- ne web sunucusu ne de calistirilabilir bir uygulama, sadece derlenen bir kutuphane.

### `dotnet new blazor --interactivity Server -n Envanex.Web -o src/Envanex.Web`
Blazor Web App sablonuyla bir proje olusturur. `--interactivity Server` interaktif bilesenlerin sunucu tarafinda SignalR uzerinden calismasini ayarlar. Sablon; layout, routing, ornek sayfalar ve wwwroot icerigi dahil tam bir web uygulamasi iskeleti uretir.

### `dotnet new worker -n Envanex.Worker -o src/Envanex.Worker`
Arka plan servisi sablonuyla bir proje olusturur. `BackgroundService`'ten turetilen bir `Worker.cs` ve `Program.cs` iceren minimal bir host uygulamasi uretir.

### `dotnet sln add src/Envanex.Domain/Envanex.Domain.csproj --solution-folder src`
Projeyi solution'a ekler ve solution icinde `src` sanal klasorune yerlestirir. `--solution-folder` IDE'de projeleri gruplamak icin -- diskteki klasor yapisini degistirmez.

### `dotnet add src/Envanex.Application reference src/Envanex.Domain`
Application projesine Domain'e bir `ProjectReference` ekler. Bundan sonra Application icindeki kod `using Envanex.Domain;` yazabilir.

### `dotnet add src/Envanex.Web package Radzen.Blazor`
Web projesine Radzen.Blazor NuGet paketini ekler. CPM aktif olsa bile `dotnet add` komutu `.csproj`'a `Version` attribute'u yazar -- sonrasinda bu attribute'un elle cikarilip surum numarasinin `Directory.Packages.props`'a tasinmasi gerekir.

### `dotnet build -warnaserror`
Tum solution'i derler. `-warnaserror` bayragi, derleyici uyarilarini hata olarak degerlendirir. `Directory.Build.props`'taki `TreatWarningsAsErrors` ile ayni etkiyi yapar ama komut satirindan acikca belirtir.

### `dotnet format --verify-no-changes`
Kod stilini kontrol eder ama dosyalari degistirmez. `.editorconfig` kurallarini ihlal eden bir dosya varsa sifirdan farkli cikis kodu dondurur. CI pipeline'inda "format duzgun mu" kontrolu icin kullanilir.

### `dotnet test`
Tum solution'daki test projelerini bulur, derler ve calistirir. Test runner (`xunit.runner.visualstudio`) `[Fact]` attribute'u ile isaretlenmis tum metodlari kesfeder ve sonuclari raporlar.

## Dikkat edilen tuzaklar

### dotnet new sablonlarinin CPM ile catismasi
`dotnet new xunit` komutu `.csproj`'a `<PackageReference Include="xunit" Version="2.9.3" />` gibi surumlu referanslar yazar. CPM aktifken ayni paketin surumu hem `.csproj`'da hem `Directory.Packages.props`'ta tanimlanirsa NuGet restore basarisiz olur. Cozum: her `dotnet new` sonrasinda `.csproj`'daki `Version` attribute'larini cikarip `Directory.Packages.props`'a tasimak.

### dotnet new sablonlarinin CRLF uretmesi
Windows'ta `dotnet new` tum dosyalari CRLF satir sonlariyla uretir. `.editorconfig` LF zorunlu kildiginda ve `TreatWarningsAsErrors` aktif oldugunda, `dotnet format --verify-no-changes` basarisiz olur. Cozum: `dotnet format` (yazma modunda) calistirarak once normalizeasyonu saglamak, sonra `--verify-no-changes` ile dogrulamak.

### Directory.Build.props tekrari
`dotnet new` her projeye `<TargetFramework>`, `<Nullable>`, `<ImplicitUsings>` ekler. Bunlar zaten `Directory.Build.props`'ta tanimli. Cikarilmasa calisiyor gibi gorunur ama iki farkli yerde ayni ayari tutmak, birini degistirip digirini unutma riski yaratir. Her sablon dosyasi olusturulduktan sonra bu tekrarlanan property'ler silindi.

### SoapCore'un guvenlik acigi olan transitif bagimliligi
SoapCore, `System.Security.Cryptography.Xml` 8.0.2'ye bagimli ve bu surumde bilinen guvenlik aciklari var. `TreatWarningsAsErrors` aktifken NU1903 (known vulnerability) uyarisi build'i kirar. Cozum: CPM'de bu paketin surumunu 10.0.11'e pinleyerek zorla yukseltmek. Bu, SoapCore'un islevini bozmaz cunku paketin API'si geriye donuk uyumlu.

### Worker sablonunun gereksiz Worker.cs'i
`dotnet new worker` bir ornek `Worker.cs` dosyasi uretir. Bu dosya silindiginde `Program.cs`'teki `builder.Services.AddHostedService<Worker>()` satiri derleme hatasi verir. Cozum: sadece dosyayi silmek degil, `Program.cs`'i de guncellemek -- `AddHostedService` cagrisi kaldirildI ve yerine `AddWindowsService()` eklendi.

### Test projelerinden gereksiz template testlerin silinmesi zamani
Sablon `UnitTest1.cs` dosyalari silinmeden ONCE `ArchitectureTests.cs` eklendi. Boylece hicbir commit'te `dotnet test` sifir testle karsilasmadi. Eger once silseydin ve sonra yeni test ekleseydin, aradaki commit'te test sayisi sifir olurdu -- CI'da "test yok" durumu goz ardi edilebilir ve bu tehlikeli bir bosluk olusturur.

### ArchitectureTests'in .csproj'u dogrudan okumasi
Testler `GetReferencedAssemblies()` (reflection) yerine `.csproj` dosyasini diskten okuyarak proje referanslarini kontrol ediyor. Neden: C# derleyicisi, kullanilmayan bir proje referansini IL ciktisinda gorunmez kilar. Yanlis eklenmis ama henuz kullanilmayan bir referans reflection ile yakalanamaz -- test "yesil" dondurur ama kural aslinda ihlal edilmistir. Dosyayi dogrudan okumak bu optimizasyondan bagimsiz calisir.

## Farkli dusundugum yer

Worker projesindeki `Program.cs`'ten sadece `Worker.cs` referansi ve `AddHostedService<Worker>()` cagrisi kaldirildi, ama `AddWindowsService()` eklendi. Plan, `UseWindowsService()` extension method'unu kullanmayi ongoruyordu (bu, host builder uzerinde calisir). Sonucta `AddWindowsService()` (servis koleksiyonuna ekleme) secildi. Her ikisi de ayni ise yarar ama API yuzeyi farkli -- `UseWindowsService()` `IHostBuilder` uzerinde, `AddWindowsService()` `IServiceCollection` uzerinde calisir. Minimal hosting modeli (`Host.CreateApplicationBuilder`) ile `AddWindowsService()` daha dogal bir tercih.

Bir diger nokta: `Envanex.Web.csproj`'da `Envanex.Application`'a dogrudan bir `ProjectReference` yok. Web, Infrastructure uzerinden Application'a ve Domain'e transitif olarak erisiyor. Bu calisiyor, ama Web katmaninin Application'daki DTO'lari ve use case interface'lerini dogrudan kullanacagi dusunulurse, acik (explicit) bir `ProjectReference` daha net bir niyet ifade ederdi. Transitif bagimliliklara guvenmenin trade-off'u: Infrastructure'daki bir refactor, Web'in Application'a erisimini kirabiliyor ve hata mesaji kaynagi belirsiz oluyor.

## Kendini sina

1. `Directory.Build.props` ile `Directory.Packages.props` dosyalari farkli sorumluluklar tasiyor. Hangisi ne is yapar ve neden ikisi de repo kokunde bulunuyor?

2. `Envanex.Domain.csproj` dosyasi neden bu kadar bos -- icinde `<TargetFramework>`, `<Nullable>` gibi property'ler yok? Bu bilgiler nereden geliyor?

3. Test projelerindeki `.csproj` dosyalarinda `<PackageReference Include="Shouldly" />` var ama `Version` attribute'u yok. Bu paket hangi surumle restore ediliyor ve bu bilgi nerede tanimli?

4. `ArchitectureTests.cs`'deki `FindSolutionDirectory()` metodu neden `AppContext.BaseDirectory`'den baslayip yukari dogru yuruyor? Testler calistiginda `AppContext.BaseDirectory` nereyi isaret eder?

5. Neden `GetReferencedAssemblies()` gibi bir reflection yontemi yerine `.csproj` dosyasini regex ile parse etmeyi tercih ettik? Reflection kullanilsaydi hangi senaryoda test yanlis sonuc verirdi?

6. `Envanex.SoapApi.csproj`'da `System.Security.Cryptography.Xml` paketi dogrudan referans olarak eklenmis, ama bu paket projede dogrudan kullanilmiyor. Bu referans neden var ve cikarilsa ne olurdu?

7. `src/Envanex.Web/Program.cs`'in sonundaki `public partial class Program { }` satirini silseydin, hangi proje derlenmezdi ve neden?
