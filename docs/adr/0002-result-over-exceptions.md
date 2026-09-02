# 0002 — Result over exceptions for predictable failures

## Context

An ERP fails in several different ways, and they are not the same kind of failure. Adding two
amounts in different currencies is something the business rules say cannot happen — it is a
normal, expected outcome of running the system, and the caller has to do something sensible
about it. Passing `null` where a validated `Currency` is required is not that: it is a coding
mistake. A `decimal` overflow is neither — it is the runtime hitting a numeric limit.

Treating all three the same way loses information. The domain needed a way to say "this can
fail, and here is why" in the method signature itself.

## Decision

Expected business failures return `Result` or `Result<T>`. Bugs and infrastructure faults throw.

Concretely, in this PR:

- `Money.Add` and `Money.Subtract` return `Result<Money>` — the two operands may carry different
  currencies, which is a rule violation the caller must handle.
- Passing a `null` `Currency` throws `ArgumentNullException` — the caller wrote incorrect code.
- A `decimal` overflow throws `OverflowException` — the runtime hit its limit.

The rule is applied by asking whether an expected failure is actually possible, not by making
signatures look alike:

- `Quantity.Add` returns a plain `Quantity`. Two non-negative quantities always sum to a valid
  quantity, so there is nothing to report.
- `Quantity.Subtract` returns `Result<Quantity>`, because the result can go below zero and
  `Quantity` may not be negative.
- `Money.Multiply` returns a plain `Money`. It takes a `Money` and a `decimal`, so there is no
  second currency and no mismatch is possible. A negative multiplier producing negative money is
  allowed by design, and an overflow is an exception, not a domain error.

Two supporting decisions:

`Result` is deliberately minimal — `IsSuccess`, `IsFailure`, `Error`, `Value`. There is no `Bind`,
`Map` or any other combinator. The type exists to make failure explicit in a signature, not to
turn C# into a functional pipeline.

Accessing `Value` on a failed result throws `InvalidOperationException` rather than returning
`default`. A caller who forgets to check `IsSuccess` fails immediately and loudly instead of
silently continuing with a zero or a null.

## Alternatives

**Exceptions everywhere.** Rejected because the signature stops carrying information. A method
declared as returning `Money` gives the caller no indication that it can fail or why, so the
knowledge of what to catch lives in documentation and memory rather than in the type. Control
flow ends up spread across `try/catch` blocks that are easy to place at the wrong level.

**`Result` everywhere, including for bugs.** Rejected because it removes fail-fast behavior
exactly where it matters most. A `null` argument is a defect; wrapping it in a `Result` invites
the caller to handle it as an ordinary outcome and lets the defect travel further from where it
was introduced. It also forces an `IsSuccess` check on operations that can never fail, which
makes the check meaningless everywhere.

**A functional `Result` with `Bind` and `Map`.** Rejected for this codebase. The chaining reads
well in languages built for it, but in C# it produces call sites that are harder to read than
the `if` statements it replaces, and it interacts badly with `Task`. The minimal version was
chosen instead.

## Consequences

**What it costs.** The caller cannot use the value directly. Every call to a `Result`-returning
method needs an `IsSuccess` check before the value is available, and propagating a failure
upward means writing an explicit early return. In a sequence of operations this accumulates
into visible boilerplate, and the happy path becomes harder to read than it would be with
exceptions.

**What it buys.** Which operations can fail is visible in the signature and checked by the
compiler at the point of use. Business rule violations are ordinary values that flow through the
system, while genuine defects still crash immediately.

**Where this may hurt later.** `Result<T>` is a Domain type, and it will travel outward as use
cases and endpoints are built on top of it — the Application and Web layers will end up
depending on it, and mapping it to HTTP responses is work that has not been done yet. Async
signatures become `Task<Result<T>>`, which is noticeably more awkward to compose than either
piece alone. And the boilerplate cost grows with the number of chained operations, so the
pressure to add `Bind` and `Map` will return once real use cases exist. If that pressure becomes
strong enough, this decision is the one to revisit — but it should be revisited deliberately,
not by adding combinators one at a time.
