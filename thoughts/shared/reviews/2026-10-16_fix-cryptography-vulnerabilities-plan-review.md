Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-16_fix-cryptography-vulnerabilities.md
Range: 2e8497a9220ae3ec8789fbc551270dd5538c609c..2e0f200af12eec1ebff84b4420f9d0a193ef0d99
Head: 2e0f200af12eec1ebff84b4420f9d0a193ef0d99

## Findings

The technical core holds against the repository: the central package management mechanics, the
two-file scope, the expected resolution table, the stop conditions and the tester split. Three
statements claim more than the repository supports. Each needs a wording fix, and none of them changes
the two-file diff.

Evidence note: the `obj/project.assets.json` files cited below are untracked restore output in this
worktree. Their `centralPackageVersions` match `Directory.Packages.props` (for example Xml 10.0.11 at
`src/Envanex.SoapApi/obj/project.assets.json:653`), so they describe the current, unpatched sources.
When they were generated is not known.

### Verified in repository files

- CPM is on (`Directory.Packages.props:3`) and `CentralPackageTransitivePinningEnabled` is set nowhere,
  so a `PackageVersion` reaches only projects with a direct reference. That is the plan's reason for
  the Infrastructure reference. `TreatWarningsAsErrors` is set at `Directory.Build.props:7`.
- The anchors are right: `Directory.Packages.props:49-51` (Xml override block) and
  `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj:15` (EF Core SqlServer).
  `src/Envanex.SoapApi/Envanex.SoapApi.csproj:8` references Xml unversioned. Neither target file
  contains a CR.
- The dependency chain:
  - EF Core SqlServer 10.0.11 -> SqlClient 6.1.6 (`src/Envanex.Infrastructure/obj/project.assets.json:655-658`).
  - SqlClient 6.1.6 -> Pkcs 9.0.11 (`src/Envanex.Web/obj/project.assets.json:502-515`).
  - Xml 10.0.11 -> Pkcs 10.0.11 (`src/Envanex.SoapApi/obj/project.assets.json:105-108`).
  - SoapCore -> Xml 8.0.2 (same file :183).
  - Nothing else in these graphs depends on Pkcs.
- Today's resolution:
  - Infrastructure, Web and Worker: Pkcs 9.0.11.
  - SoapApi and IntegrationTests: Pkcs 10.0.11 and Xml 10.0.11.
  - Domain, Application, Domain.Tests and Application.Tests: neither package.
- The restore logs map exactly the plan's five GHSA IDs to the plan's packages, and report them as
  NU1902/NU1903 errors (SoapApi assets :952-997). Every project audits with `auditMode: all` and
  `allWarningsAsErrors: true` (SoapApi :602-612, Web :3934-3942, Worker :3248-3256).
- Web pruning:
  - Web is the only project with a direct `Microsoft.AspNetCore.App` framework reference (Web :4028-4029).
    Infrastructure :4371-4374, SoapApi :669-673 and IntegrationTests :6010-6013 list only
    `Microsoft.NETCore.App`.
  - Web prunes Xml with `(,10.0.32767]` (Web :4421). That range covers 10.0.13, so Web still prunes
    Xml after the bump.
  - Web's prune list has no Pkcs entry (:4412-4424), so Web keeps the app-local Pkcs package.
  - Infrastructure's prune list has no Pkcs entry either, so the new direct reference raises no NU1510.
- The table at plan:186-193 follows from these graphs. This is my reasoning from NuGet's rules, not a
  run.
  - Infrastructure's direct `[9.0.21, )` overrides SqlClient's 9.0.11 in Infrastructure, Web and Worker.
  - In IntegrationTests, the Infrastructure path and the Xml path are cousins, so the higher version
    (10.0.13) wins. The same shape yields 10.0.11 today without NU1605: IntegrationTests :6295-6334
    hold only NU1902/NU1903.
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs:155-161` regex-scans the raw csproj, and :92-99
  expects exactly one match. The plan's "no `<PackageReference` in the comment" rule (plan:95-97) is
  therefore needed.
- Agents and permissions:
  - The tester profile (`.claude/hooks/bash-allowlist.js:35-50`) admits none of `dotnet restore`,
    `dotnet list package`, `dotnet --list-runtimes` or `curl`. plan:207-210 is right.
  - `.claude/hooks/coder-bash-guard.js:9-10` reacts only to `git`/`gh` words, and no plan command trips
    it: neither `api.github.com` nor the GHSA IDs match.
  - `curl` is in neither `.claude/settings.json` nor `.claude/settings.local.json` (which holds no
    permissions).
  - The PostToolUse hook fires only for `.cs`/`.csproj` (`.claude/settings.json:168`), so editing the
    props file first is right.
- Remaining claims:
  - Step 7 is achievable: `obj/` and `TestResults/` (hook logs) are ignored (`.gitignore:3`, `:31`).
  - The suite needs no compose or `.env`: Testcontainers starts its own SQL Server
    (`tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs:42`).
  - The `infra` scope exists and `deps` does not (`.claude/skills/commit/SKILL.md:30`).
  - The empty-shell claim matches `docs/roadmap.md:182`.
  - There is no `packages.lock.json`.
  - Only journal 0001 also names these packages, and the plan rightly leaves it alone.

### Reported by research, not verified here

- Pkcs 9.0.21/10.0.13 and Xml 10.0.13 exist and clear all five advisories. UNVERIFIED; settled by plan
  steps 1 and 3.
- Xml 10.0.13 depends on Pkcs 10.0.13, as 10.0.11 does on 10.0.11. UNVERIFIED; settled by step 2.
- The installed ASP.NET Core runtime is 10.0.11. UNVERIFIED; settled by `dotnet --list-runtimes`
  (step 8a). `Microsoft.AspNetCore.App.Internal.Assets` 10.0.11 in Web's assets (:80) is consistent
  with that but does not prove it.
- A throwaway copy of the fix restored cleanly. UNVERIFIED; settled by step 1.
- Whether the shared frameworks contain Pkcs.dll/Xml.dll, and which copy Web loads. UNVERIFIED;
  covered by steps 8b/8d, which the plan already labels presence and inference.

### Defects

1. plan:85 — "A private reference silently reopens the vulnerability in Web and Worker" contradicts
   plan:111-112, which says any graph that reintroduces a vulnerable Pkcs or Xml fails restore. The
   repository supports plan:111-112. Web and Worker audit transitive packages with warnings as errors,
   and their current restore already fails on SqlClient's Pkcs 9.0.11 (Web :4456-4468, Worker
   :3599-3611). A private reference would leave them on 9.0.11 and fail restore loudly. The directive
   is right; the rationale is wrong and undercuts the plan's own regression-guard argument.
2. plan:114-120 — "Compatibility evidence: the full existing suite" and "the integration suite
   exercises Pkcs 10.0.13" claim more than the repository shows.
   - No file in `src/` or `tests/` references the Pkcs or Xml namespaces or their main types (SignedCms,
     EnvelopedCms, Pkcs12*, SignedXml, EncryptedXml), and nothing configures Data Protection with
     certificates. No project code path calls either assembly.
   - Whether SqlClient or ASP.NET Core internals load them during the suite is UNVERIFIED. No command
     in this plan settles it; it would need assembly-load instrumentation.
   - Pkcs 9.0.21 reaches no test process at all.
   This is the third item the human asked to keep separate, and the plan states it more strongly than
   the evidence allows.
3. plan:246-247 — The named command cannot show the removal trigger. While the override exists,
   `dotnet list package --include-transitive` reports Infrastructure's own top-level 9.0.21 (plan:188),
   never what SqlClient requests. SqlClient's own request is in the `dependencies` of its library entry
   in `obj/project.assets.json`. Today that is `src/Envanex.Web/obj/project.assets.json:502-515`:
   6.1.6 -> Pkcs 9.0.11.

### Non-blocking

- plan:98-104 — The hook (`.claude/settings.json:168`) swallows every error, so it always exits 0,
  and it pipes its output through `tail -5` inside Node's `execSync`. Whether the coder sees that
  output at all is UNVERIFIED: exit-0 PostToolUse stdout is documented as transcript-only, and
  `execSync` uses `cmd.exe` on Windows, where `tail` may be missing. Check the coder transcript after
  the csproj edit. It is harmless: plan:104 keeps steps 1-6 as the gate, and MSB4025 would fail step 1.
- plan:152 — Review files are tracked in this repository. This verdict file therefore shows as
  untracked at step 7 unless the human commits it first. That is a reported deviation, not a stop.
- plan:174-175 — Step 2's `| grep`, 8a (`dotnet --list-runtimes`) and 8b (`ls ... | grep`) are not
  allowlisted either, so they prompt as `curl` does. Whether Claude Code auto-approves read-only
  `ls`/`grep` is UNVERIFIED. If 8a or 8b is denied, record "not determined" by analogy.

## Required changes

1. plan:85 — Replace the "silently reopens" sentence with one consistent with plan:111-112. For
   example: a private reference leaves Web and Worker on SqlClient's Pkcs 9.0.11, and their restore
   fails with NU1902/NU1903.
2. plan:114-120 — Drop "exercises" and state the evidence at its real strength:
   - the suite runs with Pkcs 10.0.13 and Xml 10.0.13 in IntegrationTests' graph and output;
   - no code in `src/` or `tests/` calls a Pkcs or Xml API, so the suite is not shown to load either
     assembly at any version;
   - Pkcs 9.0.21, the version Web and Worker resolve, is covered only by restore and build.
   Keep the existing point that the tested versions differ from production.
3. plan:246-247 — Replace the removal check with one that can see SqlClient's own requirement while
   the override is in place. Either read the `dependencies` of the `Microsoft.Data.SqlClient/<version>`
   entry in `src/Envanex.Infrastructure/obj/project.assets.json`, or remove the override on a branch
   and run steps 1-3.
