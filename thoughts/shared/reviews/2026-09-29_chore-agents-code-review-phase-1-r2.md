Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: Phase 1 amendments (Revision 4) of thoughts/shared/plans/2026-09-25_chore-agents-hardening.md
Range: 3d26eedb5f533084889c592cd2dfc1ac5258411d..b8c796f5e9d955801accee82afcb1dda02989963

## Verdict
NEEDS_REVISION

Scope: round 2 of the Phase 1 review, range 3d26eed..HEAD. Checked against "Revision 4" and "Phase 1 amendments (Revision 4)" in C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md. I have no Bash tool, so I ran nothing. Every runtime claim below comes from reading the code and is marked UNVERIFIED, with the command that would settle it.

**What holds**
- **A1-A7, B1 and B2 match the plan.**
  - A2 is at guard-common.js:283, A4/A5 at :199-214 and :218, A7 at :124-195.
  - The fixture, the 28 new tests (50 + 35 = 85) and the `after` guard for an undefined logDir are in place, and `CLAUDE_PROJECT_DIR` is saved and restored.
  - settings.json:7-50 is B1 entry for entry. settings.json:92-145 adds the 27 B2 patterns in order, as Bash/PowerShell pairs: 94 entries, 47 patterns in total.
  - The coder's absolute-check-before-strip deviation is an accepted ruling.
- **All 13 first-review findings are where Revision 4's table puts them**, including the Phase 2 exact test-file entries (plan:2535), Phase 3 `/pr` `allowed-tools` (plan:2978) and the Phase 4 residual entries (plan:3150-3167, 3189-3193).
- **C1.7 and CR checks:** a Grep over C:\projects\envanex\.claude\hooks finds no `LIVEPROBE` and no `\r` in any file.
- **Evidence:** every probe row in the Summary (C1.9 and P1.11-P1.15) rests on a raw tool result recorded above it. The timestamps and session order are consistent, and nothing was recorded before it existed. Exceptions are in finding 4.

## Findings

1. **C:\projects\envanex\.claude\hooks\tests\guard-common.test.js:233-242 — Medium.** A7 made `redacts before truncating to 300 characters` vacuous.
   - The test's comment (:234-235) says truncating first would leave "an unterminated quote that no rule redacts". Since A7, VALUE's `'[^']*(?:'|$)` does redact an unterminated quote.
   - Traced by hand: if guard-common.js:219 truncated before redacting, the target would cut to `…Password='S3cretVal`. Rule 1 then turns that into `Password=***`, and both assertions stay green.
   - No test and no mutation proof (M1.1-M1.27) now guards the redact-then-truncate order, and Phase 2 starts logging whole commands.
   - UNVERIFIED. To settle it, change :219 to `redactSecrets(String(entry.target ?? '').slice(0, TARGET_LOG_LIMIT))` and run `node --test .claude/hooks/tests/guard-common.test.js`. I expect it to stay green.

2. **C:\projects\envanex\.claude\hooks\lib\guard-common.js:136-139 (rule 5) — Low.** Rule 5's key prefix `[A-Za-z0-9_.:-]*` has no bound and can start at every character.
   - On a long run of those characters with no key word, a backtracking engine does roughly quadratic work.
   - A7 added this rule. Phase 4's "Hook timeouts and regex cost" entry (plan:3165-3167) names only rule 3, so the amendments changed a named residual risk without recording it.
   - This doesn't matter in Phase 1: paths split on `/`, so no run is long. It does matter in Phase 2.
   - UNVERIFIED. To settle it: `node -e "const {redactSecrets}=require('./.claude/hooks/lib/guard-common');for(const n of [1e3,1e4,1e5]){const s='a'.repeat(n);const t=Date.now();redactSecrets(s);console.log(n,Date.now()-t,'ms')}"`. Faster-than-linear growth confirms it.

3. **C:\projects\envanex\.claude\hooks\lib\guard-common.js:255 — Info, not blocking.** `block` redacts the message after appending `\n`, so rule 0 (new in A7) joins a trailing `\` or `` ` `` in the target with that newline. The stderr text then loses the last character and the newline. This is cosmetic; the exit code is unaffected. Redacting before appending `\n` avoids it.

4. **Plan evidence — Low.**
   - **(a) conclusion overreaches (plan:2329-2331 and Summary plan:2483).** The reference calls show that a rule denial produces wording W, but not that an auto-mode classifier denial does not. So "rules denied those items" is an inference; the reference calls prove it only for P1.11a and P1.12a. UNVERIFIED either way. It is settled if a raw classifier denial with different wording is recorded, or if the remaining items are run in manual mode. P1.11's and P1.12's PASS still stand on the plan's three-part evidence; only the (a) sentence needs narrowing.
   - **Count block with no command (plan:1944-1948).** The count output under "(a) denial wording" doesn't record the command that produced it, and its `<cmd>` normalisation can't be reproduced. The claim itself is supported by the 54 raw denials at plan:1590-1892.
   - **85/85 claim rests on prose (plan:2495).** "85 passed at baseline and after the last restore" rests on the coder's prose (plan:1377, 1407), not on raw output. P1.13f (plan:2099-2194, HEAD cfdd893 per plan:1927) is the raw 85/85 result to cite.

## Required changes

1. **(planner → coder, finding 1)** Rewrite `redacts before truncating to 300 characters` so that order matters under the A7 rules, and fix its comment. One form that works is URL userinfo whose `@` falls past character 300:
   - target: `'x'.repeat(270) + ' git clone https://u:S3cretValue27@example.test/x'`, so the secret starts at 291 and `@` is at 304
   - truncating first leaves `https://u:S3cretVal` with no `@`, so rule 9 misses it and no other rule matches

   Add a mutation proof M1.28 that swaps the order at guard-common.js:219 and expects this test red.
2. **(planner, finding 2)** Add rule 5 to Phase 4's regex-cost residual (plan:3165-3167) with the settling command above. Alternatively, bound the key-prefix quantifier in A7 and prove the bound with a test.
3. **(main session / human, finding 4)** Evidence text only:
   - narrow the (a) conclusion at plan:2329-2331 and plan:2483 to what the reference calls prove
   - record the command behind plan:1944-1948, or mark that block as not reproducible
   - cite P1.13f for the 85/85 gate at plan:2495

Finding 3 is optional.

Files reviewed:
- C:\projects\envanex\.claude\hooks\lib\guard-common.js
- C:\projects\envanex\.claude\hooks\tests\fixtures\async-decide-guard.js
- C:\projects\envanex\.claude\hooks\tests\guard-common.test.js
- C:\projects\envanex\.claude\hooks\tests\path-guard.test.js
- C:\projects\envanex\.claude\hooks\tests\run-hook.js (unchanged, read for context)
- C:\projects\envanex\.claude\hooks\path-guard.js (unchanged, read for context)
- C:\projects\envanex\.claude\settings.json
- C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md (Revision 4, Phase 1 amendments, As built — Phase 1 amendments, Phase 4 residual list)
- C:\projects\envanex\thoughts\shared\reviews\2026-09-26_chore-agents-code-review-phase-1.md

NEEDS_REVISION
