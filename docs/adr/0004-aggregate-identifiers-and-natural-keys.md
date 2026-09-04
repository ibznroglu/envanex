# 0004 — Aggregate identifiers and natural keys

## Context

The first three aggregates — `UnitOfMeasure`, `Warehouse` and `Product` — introduced two questions
that every aggregate after them will inherit.

The first is what an identifier should be. Rows need a primary key, but the choice has
consequences that show up years later: how the clustered index behaves as the table grows, whether
an identifier can be created before the row is written, and what an identifier exposed in a URL
tells an outsider.

The second is what to do with the codes the business already uses. A product has an SKU, a
warehouse has a code, a unit of measure has a short symbol. These are real identifiers to the
people using the system, and they carry rules the database alone cannot express.

## Decision

**Aggregates use a `Guid` identifier generated with `Guid.CreateVersion7()`.** UUIDv7 puts a
timestamp in its leading bits, so values generated over time are ordered. New rows land at the end
of the clustered index instead of being scattered through it, which is the behaviour that made
random `Guid` keys expensive. The identifier is produced by the domain factory, so an aggregate is
fully formed before anything touches the database.

**Business codes are natural keys, enforced in two places.** The factory normalises the code —
trims it and upper-cases it with the invariant culture — and rejects an empty one. The database
carries a unique index on each code column. These are not duplicated checks; they answer different
questions. The factory decides whether a value is *well formed*, which it can do without any
knowledge of other rows. Only the database can decide whether a value is *unique*, because only it
sees every row and only it can settle a race between two concurrent inserts.

**The unique indexes are not filtered.** A deactivated product keeps its code reserved. Codes
appear on past invoices, orders and stock records, so letting a new product claim an old code would
make historical data ambiguous. Reactivation is also safer: `Deactivate` followed by `Activate`
brings back the same row rather than competing with something that took the code in the meantime.

**Identity comparison stays on the surrogate key.** `Entity<TId>` already compares by `Id` and
runtime type. Codes are business identifiers, not object identity.

## Alternatives

**`int identity`.** Compact — four bytes against sixteen — and naturally ordered, so index
behaviour is ideal. Rejected for two reasons. The value only exists after the insert, so an
aggregate cannot be fully constructed in the domain before it is persisted, which complicates
anything that needs the identifier early. And sequential integers exposed in URLs or API responses
let an outsider enumerate records and estimate volumes.

**`Guid.NewGuid()`.** Solves creation timing and enumeration, but the values are random, so inserts
land at arbitrary positions in the clustered index. Pages split, fragmentation grows, and write
performance degrades as the table fills. UUIDv7 keeps the benefits and drops this cost.

**A natural key as the primary key.** Making `Code` the primary key removes a column and a join
hop. Rejected because business codes change: a company reorganises its SKU scheme and every foreign
key referencing it has to change with it. A surrogate key absorbs that.

**Filtered unique indexes on active rows only.** This would let a code be reused after
deactivation, which is convenient when a record was created by mistake. Rejected because it makes
historical data ambiguous — the same code could mean two different products depending on the date.
Convenience for the rare mistake is not worth ambiguity in the permanent record.

## Consequences

**Identifiers cost four times what an integer would.** Sixteen bytes per key, repeated in every
non-clustered index and every foreign key. At master-data volumes this is invisible. On the stock
ledger, which grows without bound, it is a real cost that should be measured rather than assumed
away.

**A code, once used, is used forever.** Reserving codes permanently is the right default for a
system that keeps financial history, but it will be felt. An operator who creates a product by
mistake and deactivates it has locked that code. A customer who wants to reassign an old SKU to a
new product cannot. Data imported from another ERP will collide with codes held by inactive rows.
Each of these has an answer — a rename path, an archival scheme, an import-time mapping — but none
of them exist yet, and the first one that is actually needed should be designed then rather than
guessed at now.

**Code length is not validated in the domain.** The columns are `nvarchar(20)` and `nvarchar(50)`,
but the factories only check that a code is present and normalise it. A code longer than the column
passes every domain rule and fails at `SaveChanges` as a `DbUpdateException`, which reaches the
caller as an infrastructure error rather than a business rule violation. This is a known gap and
contradicts the principle that an aggregate owns its invariants. It closes when the application
layer arrives in PR 5, where the length becomes a validated rule alongside the rest of the input
checks.

**Nothing enforces uniqueness before the insert.** A duplicate code is discovered when the database
rejects it, not when the aggregate is built. That is the correct division — the domain cannot see
other rows — but it means the application layer has to translate a unique-index violation into a
message a user can act on, rather than letting the raw exception escape.
