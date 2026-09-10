# ADR 0006: REST Error Mapping and Concurrency Protocol

<!-- Questions this ADR should answer:
- Why are FK errors (UnitOfMeasureNotFound, UnitOfMeasureInactive) mapped to 422 instead of 400?
- Why does an unmapped Result.Failure fall back to 400, not 500?
- What is the concurrency protocol? (RowVersion as base64 in JSON, returned on GET, sent back on PUT, 409 on mismatch)
- Why do error mappings live as an explicit code table in the Web layer, not as a category on the Error type?
- When should this change? (PR 17 SOAP: consider ErrorCategory enum on Error)
- Why are user-facing messages determined in the presentation layer, not in Domain?
- Why can't datasource projection DTOs be positional records or use EF.Property shadow properties?
-->
