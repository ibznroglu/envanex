Verdict: SECURITY_CLEAR
Reviewer: /security-review
Scope: whole branch
Range: 56fd6d41bcdb7d1b440bb1f161b08709debc5d0a..85ac3e69904c32a2b9b6df41b70d4580ab2a19b3
Head: 85ac3e69904c32a2b9b6df41b70d4580ab2a19b3

# Security review: fix/numeric-bounds (56fd6d4..85ac3e6)

No vulnerabilities found at HIGH or MEDIUM severity with confidence of 8 or more. With no candidate findings, there was nothing for the false-positive filter step to check.

## What was reviewed

The branch only adds input checks. It removes and weakens none. It adds upper limits for the list price, the reorder point and the conversion factor, and a 6-decimal-place limit for the conversion factor. Each limit is checked both in the validators and in the domain. The branch also adds error codes for these limits and maps them to HTTP 400 responses.

- **Validators**
  - Files: `CreateProductCommandValidator.cs`, `UpdateProductCommandValidator.cs` and `CreateUnitOfMeasureCommandValidator.cs`.
  - The new rules only add conditions: `LessThanOrEqualTo` and `Must` against constant limits.
  - The precision check uses `Math.Round` and has no injection surface.
- **Domain**
  - Files: `Product.cs` and `UnitOfMeasure.cs`.
  - The checks before `SaveChanges` are stricter than before. The existing non-negative rules remain, and the domain now enforces them too.
  - `UpdatePrice` now returns `Result` instead of `void`. It has no callers in `src`, so the change opens no unchecked path.
  - The update handler goes through `Product.Update`, which now checks the limits.
- **Error messages**
  - Files: `ProductErrors.cs` and `UnitOfMeasureErrors.cs`.
  - The new messages are fixed strings. They contain no user input, exception text or internal details.
- **Web layer**
  - Files: `ResultExtensions.cs` and `TurkishErrorMessages.cs`.
  - The new codes map to 400.
  - `ListPriceAmountNegative` only moved from the validator group to the domain group. Its status and message are unchanged.
  - Responses are built through `ProblemDetails` or `ValidationProblemDetails`, with the Turkish message looked up by error code. No internal detail leaks out and no user input is echoed back, which also rules out XSS.
- **Other categories**
  - The branch adds no raw SQL, deserialization, file or path handling, authentication or authorization code, cryptography or logging.
  - The rest of the diff is tests, `docs/` and `thoughts/`, which are excluded from scope.
