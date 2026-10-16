Verdict: APPROVED
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-16_fix-cryptography-vulnerabilities.md
Range: 2e8497a9220ae3ec8789fbc551270dd5538c609c..a61f62bc32bb23d029889bd0ad61f279e18e8a5a
Head: a61f62bc32bb23d029889bd0ad61f279e18e8a5a

## Findings

This focused re-review covers two things:
- the three required changes in
  `thoughts/shared/reviews/2026-10-16_fix-cryptography-vulnerabilities-plan-review.md`;
- the passages changed between 2e0f200 and a61f62b.

The plan file in the worktree matches the diff the caller supplied.

### Required findings

1. Closed: the private-reference explanation (plan:86-87). It now says a private reference leaves Web
   and Worker on SqlClient's Pkcs 9.0.11, so their restore fails with NU1902/NU1903. That agrees with
   the regression guard at plan:113-114. The verified basis is unchanged:
   - Web and Worker restore with `auditMode: all` and `allWarningsAsErrors: true` (Web assets
     :3934-3942, Worker :3248-3256).
   - Their current restore already fails on the transitive Pkcs 9.0.11 (Web :4456-4468, Worker
     :3599-3611).
2. Closed: the strength of the test evidence (plan:116-129).
   - "Compatibility evidence" and "exercises" are gone. The only remaining match is the negation at
     plan:118, and no other passage refers to the removed wording.
   - The passage now says the suite does not show that the patched assemblies are loaded or
     exercised.
   - It says no code in `src/` or `tests/` calls a Pkcs or Xml API, which matches the repository search
     in the previous review.
   - It marks loading by SqlClient or ASP.NET Core internals as unverified and not settled by any
     command.
   - It says Pkcs 9.0.21 reaches no test process, and that restore and build are its only coverage.
   - The Xml side stays covered by the Known gap's test-suite bullet (plan:239-240).
3. Closed: the override-removal trigger (plan:255-263).
   - The trigger is now SqlClient's own dependency requirement. It is read from the
     `Microsoft.Data.SqlClient/<version>` entry in
     `src/Envanex.Infrastructure/obj/project.assets.json`.
   - Verified: that entry declares `System.Security.Cryptography.Pkcs` 9.0.11 today
     (`src/Envanex.Infrastructure/obj/project.assets.json:473-487`). "Today 6.1.6 requires Pkcs
     9.0.11" is accurate.
   - Verified: the check still works while an override is in place. The same entry declares
     `Microsoft.IdentityModel.JsonWebTokens` 7.7.1 (:482), while Infrastructure's own direct
     reference resolves 8.19.2 (:1105). An entry's `dependencies` therefore record the package's
     declared minimum, not the overridden resolution.
   - The passage stays consistent with the csproj comment rule at plan:94-95.

### Regressions

None found in the changed passages (plan:3-6, 86-87, 116-129, 255-263).

### Non-blocking

- plan:119: the "output" half of "IntegrationTests' graph and output contain Pkcs 10.0.13 and Xml
  10.0.13" is UNVERIFIED.
  - This worktree has no IntegrationTests build output.
  - Whether the Xml package DLL is copied may depend on the SDK's conflict resolution against the
    transitive `Microsoft.AspNetCore.App` reference (UNVERIFIED).
  - The wording came from the previous review's suggestion, and the bullet's conclusion does not
    depend on it.
  - To settle it after step 5, look for both DLLs, or their `Envanex.IntegrationTests.deps.json`
    entries, under `tests/Envanex.IntegrationTests/bin/Debug/net10.0/`.
- The previous review's three non-blocking points (plan:100-106, plan:161, plan:183-184) are unchanged
  and stay non-blocking. This r2 file will also show as untracked at step 7 unless the human commits
  it first.

## Required changes

None.
