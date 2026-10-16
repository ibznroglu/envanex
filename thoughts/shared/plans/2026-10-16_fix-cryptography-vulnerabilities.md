# Plan: patch vulnerable System.Security.Cryptography.Pkcs and .Xml (`fix/cryptography-vulnerabilities`)

Date: 2026-10-16. Branch: `fix/cryptography-vulnerabilities` (from `2e8497a`). Input: the research the
human accepted in the planner prompt (no research file). Revised the same day in two correction
passes; the second applies the required changes of
`thoughts/shared/reviews/2026-10-16_fix-cryptography-vulnerabilities-plan-review.md`.

## Goal

Make restore succeed again under `TreatWarningsAsErrors` (`Directory.Build.props`), which today turns
the NuGet audit warnings NU1902/NU1903 into errors, by moving every project onto patched packages:

- `System.Security.Cryptography.Xml` 10.0.11 -> 10.0.13 (central version; SoapApi already references it).
- `System.Security.Cryptography.Pkcs` 9.0.11 -> 9.0.21 in Infrastructure, Web and Worker. SoapApi and
  IntegrationTests resolve 10.0.13 through Xml. The vulnerable version arrives through
  `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11 -> `Microsoft.Data.SqlClient` 6.1.6. It is
  overridden by a new direct reference in `Envanex.Infrastructure`.
- Advisories, as attributed in the accepted research:
  - Pkcs: GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7.
  - Xml: GHSA-r955-8q5j-3h83, GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj.

Two files change. **Package remediation alone does NOT fix Envanex.Web's shared-framework copy of
Xml**; that copy is reported as a known gap (section below), not fixed. Runtime remediation is outside
this package-only implementation.

## Non-goals

- Fixing Web's shared-framework Xml: no runtime installs, no CI (`setup-dotnet`) or deployment changes.
- Upgrading EF Core SqlServer, Microsoft.Data.SqlClient, SoapCore or any other `10.0.11` package.
- Enabling `CentralPackageTransitivePinningEnabled`, changing NuGet audit settings, or suppressing
  anything (`NuGetAuditSuppress`, `NoWarn`, `WarningsNotAsErrors`).
- Moving Infrastructure's Pkcs to 10.x, either to unify versions across projects or as a fix-forward
  after a failure. A major-version change needs its own reviewed decision and validation.
- New tests. Edits to `docs/roadmap.md`, the journal or README. The runtime gap goes only into the
  coder's report and the PR description.

## Touches schema? (yes/no — db-reviewer required if yes)

No. There is no EF model, migration or query change, and Microsoft.Data.SqlClient stays at 6.1.6.
Only its transitive Pkcs dependency moves a patch level. db-reviewer is not required.

## ADR needed? (title, or "no")

No. This is a patch-level security override that follows the pattern already used for Xml
(`Directory.Packages.props`, PR 1, journal 0001). The direct-reference-over-transitive-pinning choice
is a dependency tactic, not an architectural decision. The csproj comment explains it.

## Phase 1: Override Pkcs and Xml to patched versions

### Files

- `Directory.Packages.props` — modified — Xml `Version` 10.0.11 -> 10.0.13; new Pkcs `PackageVersion` 9.0.21.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — new unversioned Pkcs
  `PackageReference` with an XML comment.

No other file changes. `src/Envanex.SoapApi/Envanex.SoapApi.csproj` already references Xml without a
version and picks up 10.0.13 from the central file. Both files are LF without a BOM; keep them that
way. Edit the props file first, then the csproj.

### Signatures

`Directory.Packages.props`: the override block at the end of the `ItemGroup` (current lines 49-51).

```xml
<PackageVersion Include="System.Security.Cryptography.Xml" Version="10.0.13" />
<PackageVersion Include="System.Security.Cryptography.Pkcs" Version="9.0.21" />
```

- Xml: change only the version. Keep the existing comment and add one line naming
  GHSA-r955-8q5j-3h83, GHSA-7hqp-7c5m-4vq6 and GHSA-c399-9853-6jmj.
- Pkcs: a new entry directly below Xml, with its own 1-2 line comment. It says this is the override
  for Microsoft.Data.SqlClient's transitive Pkcs dependency, and that it takes effect only through
  the direct reference in `Envanex.Infrastructure.csproj`, because central transitive pinning is off.

`src/Envanex.Infrastructure/Envanex.Infrastructure.csproj`: directly after
`<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />` (current line 15).

```xml
<PackageReference Include="System.Security.Cryptography.Pkcs" />
```

- No `Version` attribute. Under central package management a version here is NU1008.
- No `PrivateAssets` or `IncludeAssets`. The reference must flow through project references so that
  Web and Worker resolve 9.0.21, as Infrastructure does, instead of SqlClient's 9.0.11.
  IntegrationTests resolves 10.0.13 because of its Xml dependency (see the table under Validation).
  A private reference would leave Web and Worker on SqlClient's Pkcs 9.0.11, so their restore fails
  with NU1902/NU1903.
- A brief XML comment directly above it states:
  1. it is a security override, and no Infrastructure code uses Pkcs;
  2. the chain: EF Core SqlServer 10.0.11 -> Microsoft.Data.SqlClient 6.1.6 -> Pkcs 9.0.11;
  3. the advisories GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7;
  4. why it is a direct reference: the central `PackageVersion` alone does not reach a transitive
     dependency;
  5. it is removed, together with its `PackageVersion`, once EF Core SqlServer brings a patched Pkcs
     on its own.
- Comment rules for both files: ASCII only, and no `--` inside a comment (invalid XML, MSB4025).
  The csproj comment must not contain the literal text `<PackageReference`.
  `ArchitectureTests.MatchPackageReferences` regex-scans the raw csproj, comments included, and
  `Infrastructure_ShouldReference_IdentityEntityFrameworkCore` expects exactly one match.
- The PostToolUse hook runs `dotnet format --no-restore --include <csproj>` after the csproj edit,
  before any restore. Inspect its output; do not dismiss it.
  - Expected at that point: a failure that only reflects the not-yet-restored graph, for example a
    message that restore is needed or that Pkcs is not in the assets file.
  - A genuine error: a failure that points at the edit itself, for example MSB4025 (malformed XML)
    or an error naming a line of the edited file. Fix the edit before continuing.
  - Either way, the phase is complete only when the later validation steps pass.

### Tests to add

None. The phase adds no behavior: no C# changes, no public API, no new code path. The coder rule "a
phase with no tests is incomplete" does not apply, so do not add a test.

- **Regression guard:** NuGet audit at restore. Under `TreatWarningsAsErrors`, any graph that
  reintroduces a vulnerable Pkcs or Xml fails restore, locally and in CI. A test that pattern-matches
  the csproj would pin text rather than resolved versions, and would duplicate that guard more weakly.
- **What the existing suite shows.** It runs in full and unchanged against the new graph. That shows
  the existing behavior still passes with the patched packages restored. It does not show that the
  patched assemblies are loaded or exercised.
  - IntegrationTests' graph and output contain Pkcs 10.0.13 and Xml 10.0.13. Its tests drive EF Core
    -> SqlClient against a real SQL Server (Testcontainers).
  - No code in `src/` or `tests/` calls a Pkcs or Xml API, so the suite is not shown to load either
    assembly at any version. Whether SqlClient or ASP.NET Core internals load them during the run is
    unverified, and no command in this plan settles it.
  - Pkcs 9.0.21, the version Web and Worker resolve, reaches no test process. Restore and build are
    its only coverage.
  - The tested versions therefore differ from production's: 10.0.13 in the test graph, 9.0.21 in Web
    and Worker.
  - `ArchitectureTests` re-reads the edited Infrastructure csproj.
  - SoapApi and Worker are empty shells (roadmap, Known gaps) and have no tests.

### Validation

The coder runs these from the repository root, after the edits, in this order, and pastes the raw
output. Prerequisites: network access to nuget.org (restore and audit) and Docker running.
Testcontainers starts its own SQL Server, so `docker compose` and `.env` are not needed for the suite.

```bash
dotnet restore                                                                # 1
dotnet list package --include-transitive | grep -E "Project '|Cryptography\.(Pkcs|Xml)"   # 2
dotnet list package --vulnerable --include-transitive                        # 3
dotnet format --verify-no-changes                                             # 4
dotnet build -warnaserror                                                     # 5
dotnet test                                                                   # 6
git status --short                                                            # 7
git diff                                                                      # 7
dotnet --list-runtimes                                                        # 8a
ls "<shared dir from 8a>/<version>" | grep -E "Cryptography\.(Pkcs|Xml)\.dll"  # 8b, once per framework
curl -s https://api.github.com/advisories/<GHSA-ID>                           # 8c, once per GHSA under Goal
```

Expected results:

1. Exit code 0, with no `NU1...` diagnostic in the output. Every NuGet warning is an error here,
   including NU1008, NU1010, NU1510, NU1605 and NU1608.
2. The output matches the table below.
3. All nine projects print "has no vulnerable packages given the current sources".
4. Exit code 0.
5. 0 warnings, 0 errors.
6. Full suite, with `Failed: 0` in `Envanex.Domain.Tests`, `Envanex.Application.Tests` and
   `Envanex.IntegrationTests`. Record the totals.
7. Exactly the two files under Files are modified, with nothing untracked.
8. **Runtime gap record.**
   - It does not block `PHASE_COMPLETE`, but `PHASE_COMPLETE` covers the package remediation only.
   - The report carries a "Runtime gap (unresolved)" section with the four fields below. Each field
     names its source and one status: observed, stated by advisory, inferred (unverified) or not
     determined.
   - The report must not state or imply that runtime exposure is fixed.

   The four fields:
   - a. **Installed versions** (observed). The highest installed `Microsoft.AspNetCore.App` and
     `Microsoft.NETCore.App` 10.0.x. A net10.0 app rolls forward to the highest installed 10.0 patch.
     This covers this machine only; CI and the deployment target are not checked.
   - b. **Assembly presence** (observed). Whether `System.Security.Cryptography.Pkcs.dll` and
     `System.Security.Cryptography.Xml.dll` exist in each of those two framework directories.
     Presence alone does not establish which copy an application loads, or whether that copy is
     vulnerable.
   - c. **Advisory applicability** (stated by advisory). For each of the five GHSAs under Goal, the
     affected and first patched .NET 10 runtime versions, if the advisory states them.
     - If a GitHub advisory does not state the patched runtime, follow only the official Microsoft
       references that advisory links (for example its dotnet/announcements issue), one request
       each, and stop there.
     - If the version is still not found, record "not stated". Do no other searching.
     - `curl` is not in the settings allowlist, so the human is prompted. If the human denies it,
       record "not determined".
   - d. **Actual runtime selection** (not verified by this phase). Which copy of each assembly Web
     loads at run time. Record only what the evidence supports, labelled as inference. For example:
     if step 2 lists no Xml package for Web and 8b finds `Xml.dll` in `Microsoft.AspNetCore.App`,
     the shared-framework copy is the expected source. That is inferred, not observed.
     - Any conclusion about Web's runtime exposure needs b, c and d together. Because d is
       unverified, such a conclusion is labelled unverified.
     - Do not run the app or investigate further.

Expected resolution for step 2 ("—" = not listed):

| Project | System.Security.Cryptography.Pkcs | System.Security.Cryptography.Xml |
|---|---|---|
| Envanex.Infrastructure | 9.0.21 (top-level) | — |
| Envanex.Web | 9.0.21 (transitive) | — (pruned: Microsoft.AspNetCore.App supplies it) |
| Envanex.Worker | 9.0.21 (transitive) | — |
| Envanex.SoapApi | 10.0.13 (transitive) | 10.0.13 (top-level) |
| Envanex.IntegrationTests | 10.0.13 (transitive) | 10.0.13 (transitive) |
| Domain, Application, Domain.Tests, Application.Tests | — | — |

**Stop conditions.** The coder stops without `PHASE_COMPLETE` and reports if:

- step 1, 3, 4, 5 or 6 fails, including any compatibility failure in build or tests;
- step 2 shows Pkcs below 9.0.21 (9.x) or 10.0.13 (10.x), or Xml below 10.0.13.

The coder never adds references, moves other versions or suppresses diagnostics to get past a
failure. In particular, it does not move Pkcs from 9.x to 10.x: a major-version change needs a
separately reviewed decision and its own validation. Any other difference from the table, for
example Web listing Xml, goes into the report and does not stop the phase.

**Tester.**

- Runs its standard steps (`dotnet format --verify-no-changes`, `dotnet build -warnaserror`,
  `dotnet test`) and no additional commands. The `tester` profile in
  `.claude/hooks/bash-allowlist.js` does not admit `dotnet restore`, `dotnet list package`,
  `dotnet --list-runtimes` or `curl`, so steps 1-3 and 8 are evidence in the coder's report.
- Its build includes a restore, so an audit error still fails it.
- Manual trace: both diffs match Signatures.
- CI's clean-runner `dotnet restore` on the PR is the independent audit.

## Known gap: Web's shared-framework System.Security.Cryptography.Xml (reported, not fixed)

**Package remediation alone does NOT fix Envanex.Web's copy of System.Security.Cryptography.Xml.**
Runtime remediation is outside this package-only implementation.

- **Where Web gets Xml.** Web prunes the Xml package because `Microsoft.AspNetCore.App` supplies the
  assembly; this comes from the research, and step 2 re-checks it. Web therefore carries no app-local
  Xml package, and the expected source at run time is the shared-framework copy. That is an
  inference, not an observation: this phase does not verify which copy Web loads (step 8d).
- **Installed runtime.** It was 10.0.11 during research; step 8a re-records it. Whether that version
  is affected comes from the advisories (step 8c), not from a file being present (step 8b).
- **What fixes it.** Only a patched ASP.NET Core runtime wherever Web runs: locally, in CI and on the
  deployment target. Nothing in this plan changes a runtime.
- **Pkcs.** If step 8b finds Pkcs in a shared-framework directory, that is presence only. Whether Web
  loads it instead of its app-local 9.0.21 package is unverified.
- **The test suite.** IntegrationTests resolves the 10.0.13 Xml package itself, so a green suite is
  no evidence about the copy a deployed Web loads.
- **Reporting.** The gap stays open and explicit in the coder's report and in the PR description.
  There is no roadmap edit in this PR, and no later PR is named or promised to close it.

## Rollback notes

- Nothing else to undo: two files, no schema, data, migration or deployment state, no
  `packages.lock.json`, and `obj/` regenerates on restore.
- Before merge, drop or revert the branch commit. After the squash merge, revert the squash commit in
  a new PR.
- A rollback brings back the vulnerable versions, so restore fails again with NU1902/NU1903. Never
  roll back by suppressing audit diagnostics.
- If compatibility fails, stop and report; the human decides the next step. This plan does not
  authorize a fix-forward from Pkcs 9.x to 10.x. That change needs a separately reviewed decision and
  its own validation.
- **Later removal.** The trigger is SqlClient's own dependency requirement, not the resolved Pkcs
  version.
  - While the override exists, `dotnet list package --include-transitive` reports Infrastructure's own
    top-level 9.0.21 and cannot show what SqlClient requests.
  - After a restore, read the `dependencies` of the `Microsoft.Data.SqlClient/<version>` entry in
    `src/Envanex.Infrastructure/obj/project.assets.json`. Today 6.1.6 requires Pkcs 9.0.11.
  - Remove the override only once that entry itself requires a patched Pkcs. Delete the Infrastructure
    `PackageReference` and its `PackageVersion` together.
  - The removal must still pass restore, the resolution check and the audit (Validation steps 1-3).

## Open questions

None. Decided in the correction pass: this PR does not edit `docs/roadmap.md`. The runtime gap is
recorded in the coder's report and the PR description, with no promised closing PR.

Note: the `/commit` scopes have no `deps`. Suggested PR title:
`fix(infra): patch vulnerable System.Security.Cryptography.Pkcs and Xml packages`.
