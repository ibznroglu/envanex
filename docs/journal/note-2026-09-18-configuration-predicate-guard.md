# Not (2026-09-18) — Namespace predicate'ini gercekte ne koruyor?

PR 6a, Faz 1 kod incelemesinden cikan bir iddianin deneyle cozulmesi. Tam PR gunlugu degil,
tek bir bulgunun kaydi.

## Iddia

Kod incelemesi (code-reviewer) soyle dedi: `EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse`
testi yalnizca entity tiplerinin bulundugunu dogruluyor. EF entity tiplerini `DbSet<T>` property'lerinden
convention ile kesfettigi icin, `ApplyConfigurationsFromAssembly` predicate'i fazla dar yazilsa ve tum
Fluent konfigurasyonlar sessizce dusse bile bu test YESIL kalirdi.

Incelemeyi yapan ajanin shell'i yok; iddia calistirilarak degil, okunarak uretildi.

## Deney

**1. Predicate `_ => false` yapildi (tum konfigurasyonlar dusuruldu).**

Test yesil kalmadi, kirmizi oldu -- ama kendi assert'lerine hic ulasmadan:

```
System.InvalidOperationException : The entity type 'Money' requires a primary key to be defined.
   at Envanex.IntegrationTests.Fixtures.SqlServerFixture.InitializeAsync() ... SqlServerFixture.cs:line 71
```

**2. Gercekci daraltma: yalnizca `WarehouseConfiguration` disarida birakildi.**

Yine kirmizi, yine fixture kurulumunda:

```
System.InvalidOperationException : ... 'Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning':
The model for context 'EnvanexDbContext' has pending changes.
   at Envanex.IntegrationTests.Fixtures.SqlServerFixture.InitializeAsync() ... SqlServerFixture.cs:line 71
```

Iki denemede de hata testin assert satirlarindan ONCE, `SqlServerFixture.InitializeAsync` icinde patladi.

## Sonuc

Predicate'i asil koruyan sey o test degil: fixture'in `MigrateAsync` cagrisi ve `PendingModelChangesWarning`.
Modelde herhangi bir kayma olursa entegrasyon suiti tek bir assert calismadan, acilista comekiyor.
Yani koruma var -- ama iddia edilen yerde degil.

Test yine de yeniden yazildi (`EnvanexDbContext_ShouldStillApplyProductUnitOfMeasureAndWarehouseConfigurations`):
artik yalnizca Fluent ile gelen eslemeleri dogruluyor (`Product.Code` max length 50, `UnitOfMeasure.Code` 20,
`Warehouse.Code` 20 ve `Warehouse.Code` uzerindeki unique index). Cunku predicate'in KONTROL ETTIGI bir seyi
dogrulamak, `DbSet` convention'inin zaten sagladigi bir seyi dogrulamaktan iyidir -- testin tek basina
kirmizi olabildigini gosteremesek bile.

## Asil ders

Shell'i olmayan bir inceleyici oncullerinden DOGRU akil yurutup YANLIS sonuca vardi:
"assert'ler predicate'e bagli degil" dogruydu, "o yuzden test yesil kalir" yanlisti -- arada baska bir sey
once patliyordu. Iki adim da makul gorundugu icin fark ancak calistirinca goruldu.

Bu yuzden dogrulanmamis iddia bulgu degildir: bir sey calistirilarak cozulur.
Bkz. `docs/roadmap.md` -> "An unverified claim is not a finding" ve "Settle disputes by experiment".
