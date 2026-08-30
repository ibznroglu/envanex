# Plan — PR 1: Solution Scaffolding

**Branch:** `chore/solution-scaffolding`
**Goal:** .NET 10 solution iskeleti, proje referansları, smoke test, yeşil CI.
**Domain kodu YAZILMAYACAK.**

---

## Mevcut Durum

Repo'da zaten var:

- `Directory.Build.props` — net10.0, LangVersion=latest, Nullable, ImplicitUsings, TreatWarningsAsErrors, EnforceCodeStyleInBuild, AnalysisLevel=latest-recommended, GenerateDocumentationFile (CS1591 suppressed)
- `global.json` — SDK 10.0.400
- `.editorconfig` — LF, indent kuralları, CA/CS diagnostik seviyeleri
- `.gitattributes` — `* text=auto eol=lf`, `*.sln text eol=crlf`
- `docker-compose.yml` — SQL Server 2022
- `.github/workflows/ci.yml` — restore → format check → build → test
- `docs/adr/.gitkeep`
- `docs/journal/.gitkeep`

Henüz yok: `.sln` / `.slnx`, `src/`, `tests/`, hiçbir C# projesi.

---

## Genel Kural: Her Fazın Doğrulama Adımları

Her fazın SON adımı şu sırayla çalıştırılacak:

```bash
dotnet format                        # CRLF → LF normalizasyonu + stil düzeltmeleri (yazma modu)
dotnet format --verify-no-changes    # hiçbir değişiklik kalmadığını doğrula
dotnet build -warnaserror            # uyarı = hata
dotnet test                          # en az 1 test geçmeli (Faz 1'den itibaren)
```

`dotnet new` Windows'ta CRLF üretir, `.editorconfig` LF zorunlu kılar. Her fazda `dotnet format`
yazma modunda çalıştırılarak normalizasyon sağlanır; aksi halde `--verify-no-changes` düşer.

**PHASE_COMPLETE koşulu:** Dört komut da başarılı (exit code 0).

---

## Phase 1 — Solution + Central Package Management + Boş Projeler

### 1.1 Solution oluştur

Önce XML tabanlı slnx formatını dene (.NET 10, merge conflict üretmez):

```bash
dotnet new sln -n Envanex -o . --format slnx
```

Eğer `--format slnx` desteklenmiyorsa klasik formata düş:

```bash
dotnet new sln -n Envanex -o .
```

Sonraki adımlara etkisi:

- **slnx seçildiyse:** Dosya adı `Envanex.slnx`. `.gitattributes`'teki `*.sln text eol=crlf` satırı slnx'i kapsamaz — slnx varsayılan LF kuralına tabi olacak, ek bir `.gitattributes` değişikliği gerekmez.
- **Klasik .sln seçildiyse:** Dosya adı `Envanex.sln`. `.gitattributes`'teki `*.sln text eol=crlf` satırı zaten mevcut.

Hangi format seçildiği Phase 4.1'deki ArchitectureTests kodunu belirler — coder bunu Phase 1'de karar verip not edecek.

### 1.2 Central Package Management

Repo köküne `Directory.Packages.props` oluştur:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Phase 3'te doldurulacak -->
  </ItemGroup>
</Project>
```

### 1.3 src/ projeleri oluştur

| Proje | Şablon | Konum |
|---|---|---|
| Envanex.Domain | `classlib` | `src/Envanex.Domain` |
| Envanex.Application | `classlib` | `src/Envanex.Application` |
| Envanex.Infrastructure | `classlib` | `src/Envanex.Infrastructure` |
| Envanex.SoapApi | `classlib` | `src/Envanex.SoapApi` |
| Envanex.Web | `blazor --interactivity Server` | `src/Envanex.Web` |
| Envanex.Worker | `worker` | `src/Envanex.Worker` |

> `dotnet new blazor` .NET 9+ şablon adı. Eğer .NET 10'da değiştiyse `dotnet new list` ile kontrol et.
> `--interactivity Server` InteractiveServer render mode'u ayarlar. HTTPS açık kalacak (`--no-https` YOK).

### 1.4 tests/ projeleri oluştur

| Proje | Şablon | Konum |
|---|---|---|
| Envanex.Domain.Tests | `xunit` | `tests/Envanex.Domain.Tests` |
| Envanex.Application.Tests | `xunit` | `tests/Envanex.Application.Tests` |
| Envanex.IntegrationTests | `xunit` | `tests/Envanex.IntegrationTests` |

### 1.5 xUnit sürüm tespiti

Şablonlar oluşturulduktan sonra test projelerinin `.csproj` dosyalarını oku. .NET 10 xUnit şablonu
xunit.v3 (Microsoft Testing Platform tabanlı) üretebilir — bu durumda:

- `coverlet.collector` paketi gelmeyebilir
- `dotnet test` davranışı ve CI'daki `--no-build` bayrağı etkilenebilir
- `Microsoft.NET.Test.Sdk` yerine farklı paket referansları olabilir

Yapılacak:

1. Üretilen `.csproj`'u oku, hangi xunit sürümünün ve hangi test altyapı paketlerinin geldiğini tespit et.
2. CPM girişlerini buna göre hazırla (Phase 1.7'de).
3. Gerekiyorsa `.github/workflows/ci.yml`'deki `dotnet test` satırını uyarla.

### 1.6 Tüm projeleri solution'a ekle

```bash
# src
dotnet sln add src/Envanex.Domain/Envanex.Domain.csproj --solution-folder src
dotnet sln add src/Envanex.Application/Envanex.Application.csproj --solution-folder src
dotnet sln add src/Envanex.Infrastructure/Envanex.Infrastructure.csproj --solution-folder src
dotnet sln add src/Envanex.SoapApi/Envanex.SoapApi.csproj --solution-folder src
dotnet sln add src/Envanex.Web/Envanex.Web.csproj --solution-folder src
dotnet sln add src/Envanex.Worker/Envanex.Worker.csproj --solution-folder src

# tests
dotnet sln add tests/Envanex.Domain.Tests/Envanex.Domain.Tests.csproj --solution-folder tests
dotnet sln add tests/Envanex.Application.Tests/Envanex.Application.Tests.csproj --solution-folder tests
dotnet sln add tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj --solution-folder tests
```

> slnx formatında `--solution-folder` desteği olmayabilir — coder `dotnet sln add --help` ile
> doğrulasın. Desteklenmiyorsa projeleri düz ekle.

### 1.7 .csproj temizliği

`dotnet new` her `.csproj`'a `<TargetFramework>`, `<Nullable>`, `<ImplicitUsings>` ekler — bunlar
`Directory.Build.props`'ta zaten tanımlı. Şablonların ürettiği bu property'leri tüm `.csproj`
dosyalarından sil.

Ek olarak xUnit şablonları `<PackageReference Include="..." Version="...">` ile paket ekler. CPM
aktif olduğu için `.csproj`'lardaki `Version` attribute'larını kaldır ve karşılık gelen
`<PackageVersion>` girişlerini `Directory.Packages.props`'a ekle. Bu adım atlanırsa CPM restore
hatası verir.

> Hangi paketlerin geldiği 1.5'teki tespite bağlıdır. xunit v2 ise: `Microsoft.NET.Test.Sdk`,
> `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`. xunit v3 ise farklı paket seti olacak.

### 1.8 Phase 1 doğrulaması

Genel kurala göre. Bu noktada `dotnet test` şablonların ürettiği `UnitTest1.cs` testlerini
çalıştırır (3 test projesi × 1 test = 3 test). Sıfır test olmayacak.

---

## Phase 2 — Proje Referansları

### 2.1 Referansları ekle

| Proje | Referans |
|---|---|
| Envanex.Application | → Domain |
| Envanex.Infrastructure | → Domain, Application |
| Envanex.SoapApi | → Application |
| Envanex.Web | → Infrastructure, SoapApi |
| Envanex.Worker | → Infrastructure |
| Envanex.Domain.Tests | → Domain |
| Envanex.Application.Tests | → Application |
| Envanex.IntegrationTests | → Web |

```bash
dotnet add src/Envanex.Application reference src/Envanex.Domain
dotnet add src/Envanex.Infrastructure reference src/Envanex.Domain
dotnet add src/Envanex.Infrastructure reference src/Envanex.Application
dotnet add src/Envanex.SoapApi reference src/Envanex.Application
dotnet add src/Envanex.Web reference src/Envanex.Infrastructure
dotnet add src/Envanex.Web reference src/Envanex.SoapApi
dotnet add src/Envanex.Worker reference src/Envanex.Infrastructure
dotnet add tests/Envanex.Domain.Tests reference src/Envanex.Domain
dotnet add tests/Envanex.Application.Tests reference src/Envanex.Application
dotnet add tests/Envanex.IntegrationTests reference src/Envanex.Web
```

**KRİTİK:** `Envanex.Domain` hiçbir Envanex projesine referans VERMEZ. Bu kural Phase 4'teki
architecture test ile kalıcı olarak korunacak.

### 2.2 Phase 2 doğrulaması

Genel kurala göre.

---

## Phase 3 — NuGet Paketleri (CPM)

### 3.1 Paket sürümlerini belirle

Coder, paketlerin sürümlerini `dotnet add` sonrasındaki restore çıktısından veya
`dotnet list package` ile belirleyip `Directory.Packages.props`'a pinleyecek. "latest" veya
sürümsüz bırakılması YASAKTIR.

### 3.2 Paketleri ekle

```bash
# Web
dotnet add src/Envanex.Web package Radzen.Blazor
dotnet add src/Envanex.Web package DevExtreme.AspNet.Data
dotnet add src/Envanex.Web package Microsoft.AspNetCore.OpenApi
dotnet add src/Envanex.Web package Scalar.AspNetCore

# Infrastructure
dotnet add src/Envanex.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/Envanex.Infrastructure package Microsoft.EntityFrameworkCore.Design

# Application
dotnet add src/Envanex.Application package FluentValidation

# SoapApi
dotnet add src/Envanex.SoapApi package SoapCore

# Worker
dotnet add src/Envanex.Worker package Microsoft.Extensions.Hosting.WindowsServices

# Test projeleri
dotnet add tests/Envanex.Domain.Tests package Shouldly
dotnet add tests/Envanex.Application.Tests package Shouldly
dotnet add tests/Envanex.IntegrationTests package Shouldly
dotnet add tests/Envanex.IntegrationTests package Testcontainers.MsSql
dotnet add tests/Envanex.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
```

### 3.3 CPM'e taşı

Her `.csproj`'da `dotnet add` tarafından eklenen `Version="X.Y.Z"` attribute'larını kaldır. Tüm
sürümleri `Directory.Packages.props`'taki `<PackageVersion>` elemanlarına taşı.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Test infrastructure (packages depend on 1.5 discovery) -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.x.x" />
    <PackageVersion Include="xunit" Version="2.x.x" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.x.x" />
    <PackageVersion Include="coverlet.collector" Version="6.x.x" />
    <PackageVersion Include="Shouldly" Version="4.x.x" />
    <PackageVersion Include="Testcontainers.MsSql" Version="4.x.x" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.x.x" />

    <!-- Application -->
    <PackageVersion Include="FluentValidation" Version="11.x.x" />

    <!-- Infrastructure -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.x.x" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.x.x" />

    <!-- SoapApi -->
    <PackageVersion Include="SoapCore" Version="1.x.x" />

    <!-- Web -->
    <PackageVersion Include="Radzen.Blazor" Version="7.x.x" />
    <PackageVersion Include="DevExtreme.AspNet.Data" Version="3.x.x" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.x.x" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.x.x" />

    <!-- Worker -->
    <PackageVersion Include="Microsoft.Extensions.Hosting.WindowsServices" Version="10.x.x" />
  </ItemGroup>
</Project>
```

> Sürümler placeholder'dır. Coder gerçek sürümleri pinleyecek.

`.csproj`'larda sadece şu kalacak (Version attribute'u YOK):

```xml
<PackageReference Include="Shouldly" />
```

### 3.4 Phase 3 doğrulaması

Genel kurala göre.

---

## Phase 4 — Şablon Temizliği + Architecture Tests + ADR + Journal

### 4.1 Architecture tests ekle (ÖNCE)

Şablon dosyaları silinmeden ÖNCE `tests/Envanex.Domain.Tests/ArchitectureTests.cs` dosyasını
oluştur. Böylece hiçbir commit'te `dotnet test` sıfır testle karşılaşmaz.

Dört ayrı `[Fact]` — her biri `.csproj` dosyasını diskten okuyarak bağımlılık kuralını doğrular.
Ortak mantık tek bir private helper'da toplanır.

```csharp
using Shouldly;

namespace Envanex.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_ShouldNotReference_AnyProject()
    {
        var references = GetProjectReferences("Envanex.Domain");

        references.ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldOnlyReference_Domain()
    {
        var references = GetProjectReferences("Envanex.Application");

        references.ShouldBe(new[] { "Envanex.Domain" });
    }

    [Fact]
    public void Infrastructure_ShouldOnlyReference_DomainAndApplication()
    {
        var references = GetProjectReferences("Envanex.Infrastructure");

        references.ShouldBe(new[] { "Envanex.Application", "Envanex.Domain" });
    }

    [Fact]
    public void SoapApi_ShouldOnlyReference_Application()
    {
        var references = GetProjectReferences("Envanex.SoapApi");

        references.ShouldBe(new[] { "Envanex.Application" });
    }

    /// <summary>
    /// Reads the .csproj file from disk and extracts project names (directory names)
    /// from ProjectReference elements, returning them in alphabetical order.
    /// </summary>
    private static string[] GetProjectReferences(string projectName)
    {
        var solutionDir = FindSolutionDirectory();
        var csprojPath = Path.Combine(solutionDir, "src", projectName, $"{projectName}.csproj");

        var csprojContent = File.ReadAllText(csprojPath);

        return System.Text.RegularExpressions.Regex
            .Matches(csprojContent, @"<ProjectReference\s+Include=""[^""]*[/\\]([^/\\""]+)[/\\][^/\\""]+\.csproj""")
            .Select(m => m.Groups[1].Value)
            .Order()
            .ToArray();
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (dir.GetFiles("Envanex.slnx").Length > 0
                || dir.GetFiles("Envanex.sln").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Envanex.sln or Envanex.slnx not found. The test must run from within the solution directory.");
    }
}
```

**Neden `GetReferencedAssemblies()` kullanılmıyor:** Derleyici, kullanılmayan proje referanslarını
IL'den çıkarır. Yanlışlıkla eklenen ama henüz kullanılmayan bir referans testte yakalanmaz —
yanlış yeşil verir. `.csproj` dosyasını doğrudan okumak, derleme optimizasyonlarından bağımsız
olarak bağımlılık kuralını korur.

**Yol bulma stratejisi:** `AppContext.BaseDirectory` test binary'sinin çalıştığı dizini verir
(örn. `tests/Envanex.Domain.Tests/bin/Debug/net10.0/`). Buradan yukarı doğru solution dosyası
aranarak repo köküne ulaşılır. Hem slnx hem sln aranır. Bu, hem `dotnet test` hem de IDE'den
çalıştırma senaryolarını kapsar.

> Coder notu: Shouldly'nin `ShouldBe` overload imzası sürüme göre değişebilir. Derleme hatası
> alırsan en yakın uygun overload'a geç, assertion kütüphanesini değiştirme.

### 4.2 Şablon dosyalarını sil

- `src/Envanex.Domain/Class1.cs`
- `src/Envanex.Application/Class1.cs`
- `src/Envanex.Infrastructure/Class1.cs`
- `src/Envanex.SoapApi/Class1.cs`
- `src/Envanex.Web/Components/Pages/Counter.razor`
- `src/Envanex.Web/Components/Pages/Weather.razor`
- `src/Envanex.Worker/Worker.cs`
- `tests/Envanex.Domain.Tests/UnitTest1.cs`
- `tests/Envanex.Application.Tests/UnitTest1.cs`
- `tests/Envanex.IntegrationTests/UnitTest1.cs`

> Silme sırasında `dotnet build` kırmızı kalabilir — beklenen davranış. 4.3'teki güncellemeler
> tamamlandığında build tekrar yeşile döner.

### 4.3 Şablon kodunu güncelle

**`src/Envanex.Web/Program.cs`:** Şablon sample servislerini kaldır, minimal çalışır hale getir.
Blazor InteractiveServer render mode aktif kalsın. OpenAPI ve Scalar kaydını ekle (sadece
Development ortamında):

```csharp
// Builder configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```

> Bu PR'da endpoint yok; bu kayıtlar sadece altyapıyı hazırlar. Endpoint'ler PR 5'te gelecek.

Dosyanın sonuna `WebApplicationFactory<Program>` erişimi için ekle:

```csharp
// Required for WebApplicationFactory<Program> access
public partial class Program { }
```

**`src/Envanex.Web/Components/Pages/Home.razor`:** Sadece basit bir "Envanex ERP" başlığı.

**`src/Envanex.Web/Components/Layout/NavMenu.razor`:** Counter ve Weather nav link'lerini kaldır.

**`src/Envanex.Worker/Program.cs`:** Şablonun ürettiği builder kalıbını koru (değiştirme, dayatma).
Sadece örnek Worker servis kaydını kaldır ve `UseWindowsService()` ekle.

> Blazor şablonu `wwwroot/`, `Components/Layout/`, `Components/Routes.razor`,
> `Components/App.razor`, `_Imports.razor` gibi dosyalar üretir — bunlar KALACAK.

### 4.4 Blazor şablonunda ek temizlik

Blazor şablonu `Components/Pages/Error.razor` ve/veya `Home.razor` içinde `HttpContext`'e erişen
kod üretebilir. Bu kod `AnalysisLevel=latest-recommended` altında uyarı üretebilir. Kontrol et ve
gerekirse düzelt.

### 4.5 TreatWarningsAsErrors uyumluluğu

Strateji:

1. Önce `dotnet build -warnaserror` çalıştır — hataları tespit et.
2. Kodu düzelt (tercih edilen yol): kullanılmayan using'leri kaldır, nullable uyarılarını düzelt.
3. Son çare: `.editorconfig`'de kural gevşet — sadece gerçekten düzeltilemeyecek şablon kaynaklı
   uyarılar için. Hangi kuralın neden gevşetildiği commit mesajında belirtilir.

Beklenen sorunlar:

- **CS8618** (non-nullable field not initialized): Blazor component'lerde `[Parameter]`
  property'leri — `= default!;` ile çözülür.
- **CA1062** (validate public arguments): Çıkarsa method'a null check eklenir.
- Kullanılmayan using statement'ları: `dotnet format` halleder.

### 4.6 ADR 0001 stub

`docs/adr/0001-layered-architecture-and-dependency-rule.md`:

```markdown
# 0001 — Layered architecture and dependency rule

## Context

## Decision

## Alternatives

## Consequences
```

### 4.7 Phase 4 doğrulaması

Genel kurala göre. `dotnet test` en az 4 test geçirir (ArchitectureTests'teki 4 Fact).

---

## Risk ve Notlar

| Risk | Etki | Mitigasyon |
|---|---|---|
| `dotnet new blazor` şablon adı .NET 10'da değişmiş olabilir | Proje oluşturulamaz | `dotnet new list` ile doğrula, gerekirse `webapp` veya `blazorserver` dene |
| `dotnet new sln --format slnx` desteklenmeyebilir | slnx oluşturulamaz | Klasik `.sln`'e düş; ArchitectureTests zaten her iki formatı da arıyor |
| Radzen.Blazor net10.0 desteklemiyor olabilir | Restore başarısız | Pre-release dene (`--prerelease`), veya paketi bu PR'dan çıkarıp sonraya bırak |
| DevExtreme.AspNet.Data net10.0 desteklemiyor olabilir | Restore başarısız | Aynı strateji |
| SoapCore net10.0 desteği | Restore başarısız | Pre-release dene |
| Scalar.AspNetCore net10.0 desteği | Restore başarısız | Pre-release dene; son çare olarak OpenAPI kaydını bu PR'dan çıkar |
| xUnit şablon paketleri CPM ile çakışabilir | Restore hatası | Phase 1.7'de Version attribute'larını kaldır ve sürümleri CPM'e taşı |
| .NET 10 xunit şablonu xunit.v3 + Microsoft Testing Platform üretebilir | coverlet.collector gelmeyebilir, `dotnet test` davranışı ve CI'daki `--no-build` bayrağı etkilenebilir | Phase 1.5'te üretilen `.csproj`'u oku, CPM girişlerini ve gerekiyorsa `ci.yml`'deki `dotnet test` satırını uyarla |
| Blazor şablonu beklenenden farklı dosya yapısı üretebilir | Temizlik adımları uyumsuz | Şablon çalıştıktan sonra `ls -R` ile kontrol et, temizlik adımlarını buna göre ayarla |

---

## Dosya Listesi (beklenen son durum)

> Bu liste beklenen son durumdur. Burada listelenen hiçbir dosya silinmez — Faz 4.2'deki silme
> listesi bağlayıcıdır, bu liste değildir.

```
Envanex.slnx (veya Envanex.sln)
Directory.Packages.props
src/
  Envanex.Domain/
    Envanex.Domain.csproj
  Envanex.Application/
    Envanex.Application.csproj
  Envanex.Infrastructure/
    Envanex.Infrastructure.csproj
  Envanex.SoapApi/
    Envanex.SoapApi.csproj
  Envanex.Web/
    Envanex.Web.csproj
    Program.cs
    Components/
      App.razor
      Routes.razor
      _Imports.razor
      Layout/
        MainLayout.razor
        NavMenu.razor
      Pages/
        Home.razor
        Error.razor
    appsettings.json
    appsettings.Development.json
    Properties/
      launchSettings.json
    wwwroot/
      ...
  Envanex.Worker/
    Envanex.Worker.csproj
    Program.cs
    appsettings.json
    appsettings.Development.json
    Properties/
      launchSettings.json
tests/
  Envanex.Domain.Tests/
    Envanex.Domain.Tests.csproj
    ArchitectureTests.cs
  Envanex.Application.Tests/
    Envanex.Application.Tests.csproj
  Envanex.IntegrationTests/
    Envanex.IntegrationTests.csproj
docs/
  adr/
    0001-layered-architecture-and-dependency-rule.md
  journal/
    .gitkeep
```
