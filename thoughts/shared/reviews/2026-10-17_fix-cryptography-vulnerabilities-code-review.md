Verdict: APPROVED
Reviewer: code-reviewer
Scope: cryptography package fix, Phase 1
Range: 9cb08995fba989b76a87254da03363b3adf64265..75b9e39d51fae2ae213bc389de2bdc24dab3ed98
Head: 75b9e39d51fae2ae213bc389de2bdc24dab3ed98

## Findings

Evidence labels:
- observed: read in this worktree by this review.
- reported: from the coder's handback, not observed.
- inferred: a conclusion drawn from evidence.
- UNVERIFIED: not established; the command that settles it is given.

This review ran nothing.

### 1. The implementation matches Phase 1. No defects. Severity: none.

- `Directory.Packages.props:52`: Xml is 10.0.13 (plan:65). Lines 49-50 keep the existing comment,
  and line 51 adds only the three GHSA IDs (plan:69-70).
- `Directory.Packages.props:53-55`: the Pkcs 9.0.21 entry sits directly below Xml. Its 2-line comment
  makes both statements that plan:71-73 requires.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj:21`: the reference is unversioned and
  self-closing, has no `PrivateAssets`/`IncludeAssets`, and sits directly after EF Core SqlServer (:15)
  (plan:75-87).
  - Propagation, observed: Infrastructure `obj/project.assets.json:4333-4337` records the dependency
    with no `include`/`suppressParent`. Compare EF Core Design at :4306-4312, which has
    `suppressParent: All`.
  - The Infrastructure project entry carries Pkcs 9.0.21 into Web (Web assets :1411) and Worker
    (Worker deps.json :960).
- `Envanex.Infrastructure.csproj:16-20`: the comment covers all five points of plan:88-95. The chain
  it states matches Infrastructure assets :655-658 (EF Core SqlServer 10.0.11 -> SqlClient 6.1.6) and
  :473-487 (SqlClient 6.1.6 -> Pkcs 9.0.11).
- XML validity and ArchitectureTests:
  - Observed in both files: `--` occurs only in comment delimiters, and there are no non-ASCII or CR
    characters.
  - The comment contains neither `<PackageReference` nor `<ProjectReference`. The regexes at
    `tests/Envanex.Domain.Tests/ArchitectureTests.cs:158` and `:183` therefore gain no match, and
    `Infrastructure_ShouldReference_IdentityEntityFrameworkCore` (:92-99) still sees exactly one.
  - BOM absence cannot be established with the search tool. The passing format check is reported.
- No suppressions or extra pins:
  - No `*.props`, `*.targets` or `*.csproj` file contains `NuGetAuditSuppress`,
    `WarningsNotAsErrors`, `CentralPackageTransitivePinningEnabled` or `VersionOverride`.
  - `Directory.Build.props:11` `NoWarn` holds only the pre-existing CS1591.
- No unrelated changes: the supplied diff touches only the two planned files. The clean `git status`
  is reported.
- Restore resolution matches the table at plan:195-202 (observed, assets files):
  - Infrastructure :1464 Pkcs 9.0.21.
  - Web :1224 Pkcs 9.0.21; Xml is pruned (:4423).
  - Worker :1271 Pkcs 9.0.21.
  - SoapApi :83/:105 and IntegrationTests :2158/:2196: Pkcs and Xml 10.0.13.
  - None of the five assets files has a `logs` section, so no restore warning or error is recorded.
  - The audit, the build and the 638 passing tests are reported.
- The "output" half of the r2 plan review's non-blocking point (plan:119) is now observed.
  `tests/Envanex.IntegrationTests/bin/Debug/net10.0/` contains both 10.0.13 DLLs, with deps.json
  entries at :1507-1512 and :1537-1542. Loading and execution are not established.

### 2. Removal comment: acceptable, not a defect. Severity: none (an optional wording improvement only).

- The wording: `Envanex.Infrastructure.csproj:19-20` repeats plan:94-95 almost word for word.
- The plan's criterion: the Rollback notes (plan:255-263) trigger removal on SqlClient's own declared
  Pkcs dependency, read from the `Microsoft.Data.SqlClient/<version>` entry in Infrastructure's assets
  file. Today that entry declares 9.0.11 (:487).
- Why they agree:
  - "Brings a patched Pkcs on its own" means "without this override".
  - EF Core SqlServer brings Pkcs only through SqlClient (:658, :487), so this is the same criterion
    as the Rollback notes.
  - The phrase also rules out the misleading signal. The resolved 9.0.21 that `dotnet list package`
    shows is the override's own effect.
- What the comment omits is the procedure, that is, where to read the declared requirement. That is
  an omission, not a contradiction.
- A premature removal fails closed: an unpatched transitive Pkcs fails restore with NU1902/NU1903
  under `TreatWarningsAsErrors` (plan:250-251, 263).
- Optional, not required: name the `Microsoft.Data.SqlClient` entry in `obj/project.assets.json` in
  the comment.

### 3. Pkcs conflict concern: the override does not reach Web's build output. Severity: not an implementation defect. It extends the known runtime gap to Pkcs for Web.

**Restore resolution (observed).**

- Web `obj/project.assets.json:1224-1245` resolves Pkcs 9.0.21 with `runtime` and `runtimeTargets`
  assets. It arrives through Infrastructure (:1411).
- Pkcs is not in Web's `packagesToPrune`. Xml is (:4423).
- Restore pruning is a separate mechanism. It is what removes Configuration.Binder and
  Options.ConfigurationExtensions: both are absent from Web's Infrastructure entry (:1402-1412), even
  though the csproj references them.

**Build-output selection (observed).**

- No `System.Security.Cryptography.Pkcs.dll` exists in `src/Envanex.Web/bin/Debug/net10.0/` or its
  `runtimes/` subtree.
- `obj/Debug/net10.0/Envanex.Web.csproj.FileListAbsolute.txt` lists no Pkcs DLL. It does list SqlClient
  (:28), Microsoft.Bcl.Cryptography (:20) and ProtectedData (:63).
- `Envanex.Web.deps.json` contains no Pkcs string at all. It is absent from SqlClient's dependencies
  (:404-416) and from `Envanex.Infrastructure/1.0.0` (:1046-1053).
- Worker's deps.json keeps Pkcs in both places (:113, :960). Its library entry (:887-898) has
  assemblyVersion 9.0.0.0 and fileVersion 9.0.2126.50404, which settles the coder's "probably
  9.0.0.0".

How the package fares in each project's build output:

| Project | Frameworks | Pkcs package | Assembly version | In build output |
|---|---|---|---|---|
| Worker | NETCore.App only (assets :3316-3320) | 9.0.21 | 9.0.0.0 | Yes, including `runtimes/win/lib/net9.0` |
| IntegrationTests | inherits Microsoft.AspNetCore.App (assets :259-260) | 10.0.13 | 10.0.0.0 | Yes |
| Web | Microsoft.AspNetCore.App (assets :4030-4036) | 9.0.21 | 9.0.0.0 | No |

Freshness:
- The Worker and IntegrationTests outputs are demonstrably post-change.
- Web's timestamps are not readable here (UNVERIFIED). This settles it:
  `Get-Item src/Envanex.Web/bin/Debug/net10.0/Envanex.Web.deps.json, src/Envanex.Web/obj/project.assets.json | Select-Object Name, LastWriteTime`.
- The conclusion does not depend on freshness:
  - Web's deps.json carries the SqlClient 6.1.6 chain but no Pkcs at any version.
  - Pkcs was not pruned before the change either: the r2 review records Web's restore failing on
    transitive Pkcs 9.0.11.

**Mechanism (UNVERIFIED).**

- The evidence fits the SDK's package-vs-platform conflict resolution choosing a
  Microsoft.AspNetCore.App Pkcs with the higher assembly version (10.0.0.0 over 9.0.0.0). That is the
  coder's hypothesis.
- This review did not observe the platform manifest entry. These settle it:
  - `Select-String -Path "C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.*\data\PlatformManifest.txt" -Pattern "Cryptography"`.
  - `dotnet build src/Envanex.Web/Envanex.Web.csproj -v:d`, then search the log for the
    conflict-resolution message that names Pkcs.

**Runtime loading (UNVERIFIED).**

- `Envanex.Web.runtimeconfig.json:4-13` is framework-dependent on NETCore.App and AspNetCore.App
  10.0.0, with no `rollForward`. It therefore runs on the highest installed 10.0 patch, which is
  10.0.11 on this machine (reported).
- Web has no app-local Pkcs and no deps.json entry for it, so the expected source is a framework copy
  (inferred).
- Whether anything loads Pkcs at all is not established. These settle it:
  - Selection: run Web with `COREHOST_TRACE=1` and `COREHOST_TRACEFILE=<path>`, then read the TPA
    entry for `System.Security.Cryptography.Pkcs.dll`.
  - Loading: inspect the process modules after exercising a SqlClient path.

**What this means for the package fix.**

- It meets its stated goal (plan:10-11).
- For Web, the override acts on the restore graph only. It removes SqlClient's 9.0.11 node so the
  audit passes, but it changes no byte that Web ships.
- By the same mechanism, Web did not ship 9.0.11 before the change either (inferred). The NU1903 that
  blocked Web's restore therefore flagged a package that was not in Web's output.
- The override is still required: without it, Web's restore fails.
- For Worker, it does change the shipped bits: 9.0.21 is observed in the output.

**What this means for the runtime gap.**

- Web's Pkcs comes from the shared framework, as its Xml does. The known gap therefore covers both
  for Web.
- Only a patched ASP.NET Core runtime closes it. According to the reported announcements, that is
  10.0.13 for the advisories they cover.
- Where the plan's wording falls short:
  - plan:14 ("9.0.21 in Infrastructure, Web and Worker") holds for restore only.
  - The Known gap names only Xml (plan:224, :226).
  - The Pkcs bullet assumes "its app-local 9.0.21 package" (plan:237-238), which the artifacts
    contradict.
- After this change, no build step flags Web's Pkcs. Only the runtime-gap record covers it.

**Why this is not a defect of this phase.**

- The coder implemented exactly what the plan specified.
- Within the plan's non-goals, no package change alters what Web selects.
- An app-local 10.x Pkcs would be expected to win the conflict (UNVERIFIED), as IntegrationTests
  suggests. That is the 9.x -> 10.x move that plan:32-33 excludes; it needs its own decision.
- Reporting consequence, non-blocking for the code:
  - The PR description's "Runtime gap (unresolved)" section should name Pkcs alongside Xml for Web.
  - It must not say that Web runs Pkcs 9.0.21 (plan:167).

### 4. `Directory.Packages.props:51` says "Advisories addressed". Severity: non-blocking.

- The wording applies to the package version.
- For Web, the Xml `PackageVersion` has no effect, because Xml is pruned (Web assets :4423).
- On a `PackageVersion` line, read together with the plan's Known gap, it is acceptable. No change is
  required.

## Required changes

None.
