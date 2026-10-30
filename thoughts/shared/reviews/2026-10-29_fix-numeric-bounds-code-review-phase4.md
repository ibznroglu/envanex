Verdict: APPROVED
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-24_numeric-bounds.md phase 4
Range: 79923fe2ffa663ea58d38471447db2b2e5b1fe4f..a33681f12e96425eddcc468a65fbde9720889176
Head: a33681f12e96425eddcc468a65fbde9720889176

## Findings

How this review was done:

- `plan:N` is line N of `thoughts/shared/plans/2026-10-24_numeric-bounds.md`.
- `roadmap:N` is line N of `docs/roadmap.md`, read at `a33681f`. The caller reports a clean tree.
- What I read, with the Read tool:
  - plan:1-200 and plan:900-1049;
  - `docs/roadmap.md` and ADR 0003, in full;
  - the plan review r2 and the Phase 3 code review;
  - `Product.cs`, `UnitOfMeasure.cs` and `SchemaPrecisionTests.cs`, in full.
- Where the evidence comes from:
  - character, placement and line-ending claims: the Grep tool, run in this review;
  - the EF Core claim: the Microsoft Learn tools, run in this review.
- I have no shell. Every git, build and byte-level claim below is the caller's or the coder's. It is UNVERIFIED unless I say how I checked it, and each one names the command that settles it.

No blocking finding.

### F1 - (a)-(d) against the plan text - info, identical

- **Method.** I wrote one pattern for each of the 14 new lines.
  - Each pattern is anchored with `^` and `$` and escapes every regex metacharacter, so it matches only that exact line.
  - I ran each pattern with the Grep tool on both files.
- **Result.** Every pattern matched exactly one line in each file:

  | Change | roadmap | plan |
  |---|---|---|
  | (a) | :61 | :943 |
  | (c), six lines | :122-127 | :951-956 |
  | (d), six lines | :130-135 | :964-969 |
  | (b) | :207 | :976 |

  For (b), the full-line pattern counts 1 in each file, and its opening words match only roadmap:207.
- **What the match also shows.**
  - These lines have no trailing whitespace and no carriage return, because `$` follows the last visible character.
  - The two dashes in (a) are U+2014 in both files. A second run that wrote them as `\x{2014}` matched roadmap:61 and plan:943.
  - Every apostrophe in (b), (c) and (d) is U+0027 in both files, because one ASCII pattern matched both.

### F2 - nothing else in docs/roadmap.md changed - info, consistent

- **Line counts.** The relayed stat is 1 file, 14 insertions and 3 deletions. That is exactly (a) +1, (c) -2/+6, (d) +6 and (b) -1/+1.
- **The relayed hunks match the file.**
  - The headers are `@@ -58,6 +58,7 @@`, `@@ -118,10 +119,20 @@` and `@@ -193,7 +204,7 @@`.
  - Their context lines are roadmap:58-64, :119-138 and :204-210.
  - The old and new counts add up:

    | Hunk | Old | New |
    |---|---|---|
    | 1 | 3+3 | 3+1+3 |
    | 2 | 3+2+2+3 | 3+6+2+6+3 |
    | 3 | 3+1+3 | 3+1+3 |
- **The `--unified=0` headers.** In that form the same changes give the plan's four headers (plan:996-999): `-60,0 +61`, `-121,2 +122,6`, `-124,0 +130,6` and `-196 +207`.
  - (c) and (d) share one default hunk, because only two unchanged lines separate them (old :123-124).
- **The old line numbers.** The relayed old-side line numbers agree with the plan's line facts (plan:86-90), which were taken at `cb4b079`.
- **No unplanned work.** The relayed change touches only `docs/roadmap.md`. Nothing outside (a)-(d) was implemented.
- **UNVERIFIED.** These commands settle it:
  - `git diff --stat 79923fe..a33681f`: one file;
  - `git diff --unified=0 79923fe..a33681f -- docs/roadmap.md`: the four headers above;
  - `git diff --check 79923fe..a33681f -- docs/roadmap.md`: no output.

### F3 - placement and the surrounding text - info, reads correctly

- **(a), roadmap:61.**
  - Position: between the `fix(infra)` row (:60) and the PR 7 row (:62).
  - Shape: the same `| [U+2014] | type(scope) [U+2014] summary | status |` shape as the three rows above it. A Grep for the row prefix matches :58-61.
  - Status cell: empty, like PR 7's row.
  - The rule at :64-66 holds. Only a completed row carries a PR number, and this row is not merged yet. Rows between roadmap PRs are numbered with the dash.
  - Title: after its type and scope, it equals the plan's suggested PR title (plan:6-7).
- **(c), roadmap:122-127, and (d), roadmap:130-135.**
  - Position: they sit on either side of the `ComplexProperty` bullet (:128-129), before the grid bullet (:136). This puts the `Money` mapping decisions together, as plan:959-961 asks.
  - Form: both keep the section's `**Heading.** text` form, and their bold and code-span markers are balanced.
  - Reading: (c)'s heading still states the project's decision. Its body separates the EF Core fact from that decision.
- **(b), roadmap:207, in the two-column Known gaps table (header :171).**
  - The anchored pattern `^\| The integration suite's duration[^|]*\|[^|]*\|$` matches once. So the row holds exactly three pipes, which make two cells.
  - Its markers are balanced: three bold pairs and 14 code spans.
- **No other doc repeats the old claim.**
  - Outside `thoughts/`, a case-insensitive Grep for "complex types cannot be null" matches only two places:
    - ADR 0003:78, which plan:154 keeps unedited;
    - the new roadmap:125.
  - A Grep of every `*.md` file for `Money?`, "optional complex", "nullable complex" and "Money is not nullable" finds nothing else outside `thoughts/`.
  - So CLAUDE.md, the README and the journal say nothing that (c) now contradicts.

### F4 - (c)'s EF Core 10 claim - info, verified on Microsoft Learn

- **The cited page.** I fetched https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew with `microsoft_docs_fetch`. It returned "What's New in EF Core 10" for that URL as written.
  - Under Complex types, then Table splitting, the page says:
    - "While the above was already possible since EF 8, EF 10 adds support for **optional** types", with the example `public Address? BillingAddress { get; set; }`;
    - "Note that optional complex types currently require at least one required property to be defined on the complex type."
  - The JSON section's example table has `[BillingAddress] json NULL`.
- **A second source.** `microsoft_docs_search` also returned the Complex Types modeling page (https://learn.microsoft.com/ef/core/modeling/complex-types). Its section "Optional complex types" says:
  - "Starting with EF Core 10, a complex property can be made optional by declaring it as nullable";
  - the same note, with its reason: "EF needs at least one non-nullable column".
- **ADR 0003.** ADR 0003:78 reads "**Complex types cannot be null.** No aggregate can declare a `Money?` property."
  - So "ADR 0003's general statement that complex types cannot be null does not hold for EF Core 10" describes the ADR accurately.
  - The EF Core 10 half of the sentence matches both pages.
- **Two nuances. Neither needs a change.**
  - The source says "currently require", and roadmap:122 says "provided". The sentence is scoped to EF Core 10, so leaving out "currently" does not make it wrong.
  - The modeling page offers a fallback: if the complex type has no required property, configure a discriminator. EF creates it as a required shadow property, so that case still falls under "at least one required property".

### F5 - (d) against the code that Phases 1-3 built - info, consistent

- **The persisted numeric fields.** `src/Envanex.Domain/Aggregates` has exactly three:
  - `Product.ListPrice` (`Product.cs:16`);
  - `Product.ReorderPoint` (`:17`);
  - `UnitOfMeasure.ConversionFactor` (`UnitOfMeasure.cs:15`).
- **"States its maximum as a public constant on its aggregate."**
  - The constants are `Product.cs:10-11` and `UnitOfMeasure.cs:9`. The decimal places are at `UnitOfMeasure.cs:10`.
  - `Money` and `Quantity` have no such bound. A Grep for `MaxValue` in `src/Envanex.Domain` matches only `Product.cs` and `UnitOfMeasure.cs`.
- **"Checks it there, on the value the aggregate receives."**
  - `Product.cs:73` and `:78` are in `Create`, `:99` is in `UpdatePrice`, and `:143` and `:148` are in `Update`.
  - `UnitOfMeasure.cs:84` is in `Create`.
- **Every assignment to these fields comes after its method's checks.**
  - `Product.cs:34-35` is in the private constructor, which only `Create` calls (`:86`).
  - `Product.cs:104` and `:155-156` come after their checks.
  - `UnitOfMeasure.cs:31` is in the private constructor, which only `Create` calls (`:90`).
- **"Its validator checks the same maximum on the raw request value."** Each of these calls `LessThanOrEqualTo` with the aggregate's constant:
  - `CreateProductCommandValidator.cs:36` and `:52`;
  - `UpdateProductCommandValidator.cs:32` and `:48`;
  - `CreateUnitOfMeasureCommandValidator.cs:45`.
- **"A schema test links the constant to the live column."**
  - `SchemaPrecisionTests.cs:113-129`, `:149-165` and `:167-183` each compare a `MaxValue` constant with `(10^precision - 1) / 10^scale`. The precision and scale are read from `INFORMATION_SCHEMA.COLUMNS` (`:197-200`).
  - `:131-147` ties the decimal places to the column's scale.
  - That these tests pass is UNVERIFIED by me. The tester's gate run is the record.

### F6 - row (b) - info, no mismatch

- **The text.** Row (b) is identical to plan:976 (F1). It records the 2026-10-23 measurement at `56fd6d4`: `1 m 2 s`, `55 s` and `52 s`, with a median of 55 s.
- **The 2026-10-29 follow-up.** As the caller instructed, I did not treat it as a mismatch. It ran at `316f2ba` and gave 61, 55 and 52 s with 427 tests, a median of 55 s.
  - The plan keeps row (b) as written, and the follow-up goes into the PR text and the journal.
  - The test count is consistent with the plan: 427 is the measurement's 392 tests plus the 35 that plan:979 says Phases 1-3 add.
  - The row already covers a 55 s median: "No change is made while the median is below the line."
  - plan:979-981 leaves the follow-up to the human and says it is not a phase failure.
- **UNVERIFIED: the measured values themselves.**
  - In `thoughts/`, the session ID and `1 m 2 s` occur only at plan:976 (Grep).
  - The raw output lives in a Claude Code session transcript outside the repository, which I cannot read.
  - Two ways settle it: open that session (`claude --resume 3e1d4467-7a9c-42b7-b757-526d3617ea84`), or repeat the three runs of `dotnet test` at `56fd6d4`.

### F7 - line endings, BOM and the plan's own checks (plan:1002-1009) - info

- **The plan's Grep checks (plan:1005-1009) hold on `docs/roadmap.md`.**
  - The two removed texts have no match.
  - A positive control ran the same pattern on the plan file and matched plan:972, :1006 and :1007.
  - Each of these four has exactly one match: `bound the persisted numeric fields`, `Measured on 2026-10-23 at`, `Persisted numeric fields carry their column's bound` and `ef-core-10\.0/whatsnew`.
- **LF.** `\r` has no match in `docs/roadmap.md`. The same file answers the other patterns, so the search covered it.
- **UNVERIFIED.** These commands settle the rest:
  - `git ls-files --eol docs/roadmap.md`: `i/lf w/lf`.
  - `head -c 3 docs/roadmap.md | od -An -tx1`: `23 20 52`.
    - The relayed diff has no hunk at line 1, so this commit did not change the file's first bytes.
  - `dotnet build -warnaserror`: 0 warnings, 0 errors. This is the coder's claim, and a docs-only change cannot affect it.
  - `git log -1 --format=%s a33681f`: `docs(roadmap): record the numeric-bounds fix, the suite measurement and two decisions`.
    - That subject equals plan:199, and the session's git status snapshot shows the same subject.

### Non-blocking notes

- **N1 - the link at roadmap:124 (UNVERIFIED).**
  - The URL is bare and sits inside parentheses.
  - Under GFM's extended-autolink rules, the trailing `)` and `,` fall outside the link, so the target should be exactly the cited URL.
  - How GitHub actually renders it is UNVERIFIED. Viewing `docs/roadmap.md` on GitHub after the push settles it.
  - No change is needed, because the text is the plan's.

## Required changes

None.
