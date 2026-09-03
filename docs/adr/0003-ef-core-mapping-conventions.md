# 0003 — EF Core mapping conventions

## Context

The domain layer models money, quantities and currencies as value objects with private
constructors and validating factories. None of them can be built in an invalid state, and none of
them know that a database exists — `Envanex.Domain` references no packages at all.

Relational storage does not share that shape. It has columns, precision, nullability and
concurrency tokens, and EF Core needs to know how to turn each value object into columns and back.
The question this PR had to answer was how to express that mapping without letting persistence
concerns leak back into the domain types.

## Decision

**`Money` maps as an EF complex type.** It carries two fields — an amount and a currency — so it
cannot collapse into a single column. As a complex type it is embedded in the owner's table as
`<Property>_Amount` and `<Property>_Currency`, and EF treats it as a value rather than as a
tracked entity with its own identity.

**`Quantity` and `Currency` map through value converters.** Each wraps a single primitive, so each
fits in one column: `Quantity` as `decimal(18,6)`, `Currency` as `nvarchar(3)`. The converters are
registered type-wide in `ConfigureConventions`, which states a fact about the type itself: wherever
a `Currency` appears, it is stored this way.

**Column shape is set by conventions, not repeated in every configuration.**
`AggregateRootConvention` adds the concurrency token to every aggregate root.
`MoneyComplexTypeConvention` sets the precision of the amount inside any complex `Money`. This
splits along a clear line: `ConfigureConventions` handles what is true of a type everywhere, the
conventions handle what is specific to a shape.

**The concurrency token lives in Infrastructure, not in Domain.** `RowVersion` is not a business
rule. It exists because two transactions can race, which is a storage problem. Putting a `byte[]`
on the domain base class would push a database concern into a layer that is supposed to know
nothing about databases, so it is configured as an EF shadow property instead. A schema test
asserts the column is a real SQL Server `rowversion` and not an ordinary `varbinary`, because a
`varbinary` would pass the concurrency test while silently never detecting a conflict.

**Converters fail loudly on invalid stored data.** Reading a `Currency` calls the same validating
factory the domain uses, and an unrecognised code throws. A row that violates a domain rule means
the data is already corrupt; crashing on read surfaces that immediately instead of letting invalid
values circulate through the system.

**No migration file yet.** `EnvanexDbContext` has no entity configurations, so a migration would be
empty. The schema in this PR is created by `EnsureCreated` inside ephemeral test containers. The
tooling for migrations is in place — a design-time factory reading the connection string from an
environment variable — and the first real migration arrives with the first aggregate.

## Alternatives

**`Money` as an owned entity.** Owned entities are tracked as entities: they have identity
semantics and navigation behaviour. That is the wrong model for a value, where two instances with
the same amount and currency are simply the same thing. Complex types were introduced for exactly
this case.

**Adding a parameterless constructor to `Money`.** This was actually done and then undone, and the
way it was resolved matters more than the outcome. The implementing agent added the constructor and
reported it as required for EF materialisation; the reviewing agent said it was unnecessary. Both
claims were unverified. Rather than deciding by argument, the constructor was removed and the tests
were run — all of them passed. EF Core 10 binds complex types through an existing constructor by
matching parameter names, so `Money` needed no change at all. The domain type went back to having a
single way in, and a silent `Currency = TRY` default never reached production. The lesson is not
about EF: a claim that something is technically required is a hypothesis, and hypotheses about
compilation and runtime behaviour are cheap to test and expensive to accept.

**Moving the `Currency` converter into `MoneyComplexTypeConvention`.** This would put everything
about `Money`'s storage in one file, which reads well. It was rejected because it narrows the
guarantee: the mapping would then apply only to currencies inside a `Money`, and a `Currency` used
anywhere else would fall back to defaults. Type-level facts belong in `ConfigureConventions`.

**A lenient converter returning a default currency for unknown codes.** Rejected for the same
reason the parameterless constructor was rejected. A silent default turns corrupt data into
plausible-looking data and moves the failure far away from its cause. Returning `Result<Currency>`
would have been better than either, but a value converter's signature has no room for it.

## Consequences

**Complex types cannot be null.** No aggregate can declare a `Money?` property. Where an optional
amount is genuinely needed, `Money.Zero` has to carry that meaning, or the two columns have to be
mapped separately.

**Every `Money` property must be registered by hand.** EF does not discover complex types, so each
aggregate configuration needs its own `builder.ComplexProperty(...)` line. Forgetting it is silent:
the convention finds nothing to configure and the property maps as an ordinary type. This is the
main ongoing cost of the decision.

**Corrupt data crashes on read rather than on write.** The exception surfaces during
materialisation, which is far from where the bad value entered the database. This is accepted
because the alternative is worse, but it means a data-integrity incident will present as an
unfamiliar exception in an unexpected place.

**`EnsureCreated` cannot evolve a schema.** It creates a database or does nothing; it never applies
a change. That is fine for test containers that are thrown away after each run, and unusable
anywhere the data must survive. Migrations are the answer and they start in PR 4; nothing in this
PR should be taken as a pattern for production schema management.

**Nested complex types are not covered.** `MoneyComplexTypeConvention` only inspects complex
properties one level deep, so a `Money` nested inside another complex type would not get its
precision configured. No such structure exists yet; the convention will need to recurse when one
appears.
