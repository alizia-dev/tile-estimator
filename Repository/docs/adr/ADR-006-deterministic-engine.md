# ADR-006: One deterministic calculation engine

Status: Accepted

## Context

SPEC 14 requires deterministic, non-AI calculation, and that estimates and every quick
calculator produce identical results for identical inputs.

## Decision

Pure, synchronous engines in `Application/Engines`, taking records and returning records. No
database, no HTTP, no clock, no randomness. Every caller, including the project takeoff, all five
quick calculators and the estimate service, goes through the same instance.

A quick calculator builds a **detached** `Surface` and passes it through the *same*
`TakeoffBuilder` a project uses.

Each result carries a `CalculationBreakdown` stating its own arithmetic.

## Rejected

**Calculating in the Angular client.** The formula would then exist twice and drift. The client
formats numbers; it never computes them.

**Separate quick formulas.** The obvious shortcut, and the one that guarantees the calculator and
the estimate eventually disagree.

**Reading the catalog inside the engine.** It would make results depend on hidden state and break
snapshot immutability.

## Consequences

- Exhaustively unit-testable, including the SPEC 14 reference case.
- Identical inputs give identical outputs, everywhere, always.
- Callers must gather catalog data up front, which `TakeoffBuilder` does in one pass to avoid
  N+1 queries.
