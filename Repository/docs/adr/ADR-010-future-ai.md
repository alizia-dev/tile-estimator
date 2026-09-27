# ADR-010: AI as a future input provider only

Status: Accepted

## Context

SPEC 1 requires a 100% deterministic, non-AI MVP, while SPEC 20 requires that AI document takeoff
can be added in V4 without significant change to the domain, engines, database or public API.

## Decision

Declare the extension-point interfaces now, namely `IDocumentTakeoffProvider`,
`ITakeoffExtractionProvider` and `IExternalPricingProvider`, and register explicit
"not available" implementations. No AI, LLM or ML package is referenced anywhere, and an
architecture test scans every project references and fails if one appears.

The intended V4 pipeline:

```
PDF/Image -> Document Parser -> AI Takeoff Provider -> Structured Takeoff
-> Validation -> Human Review -> Takeoff Engine -> Cost Engine -> Pricing Engine -> Estimate
```

AI is an **input provider**. It proposes measurements a human reviews. It never produces money
and never bypasses the deterministic engines, which remain the source of truth.

## Rejected

**No interfaces at all.** Retrofitting would touch the takeoff service and the project API.

**Building a stub pipeline now.** Unused code that has never run against a real provider is
usually wrong, and it invites accidental use.

## Consequences

- The DI graph and the call sites are already shaped for V4.
- The MVP stays provably deterministic, and CI keeps it that way.
- The candidate DTOs carry a confidence score and a source reference, because human review is
  part of the design, not an afterthought.
