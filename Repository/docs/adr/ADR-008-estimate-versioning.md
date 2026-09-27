# ADR-008: Finalized estimates are immutable; edits create a version

Status: Accepted

## Context

An estimate becomes a quote, and a quote becomes a contract. What was quoted must remain
inspectable forever, while estimators still need to revise pricing as a job changes.

## Decision

An estimate is editable in `Draft` or `InReview`. `Finalize` makes it immutable: any mutating
endpoint then returns **409**.

Revising a finalized estimate calls `CreateNextVersion`, which copies it and its lines to
`EST-1001 v2` and marks v1 `Superseded`. Both remain readable, and the unique index is on
`(OrganizationId, EstimateNumber, Version)`.

A finalized estimate cannot be deleted. Financial records are never hard-deleted.

## Rejected

**Editing in place with an audit trail.** The audit shows *that* a number changed, but the
document itself no longer says what the customer agreed to.

**Locking on send instead of finalize.** Finalize is the deliberate act; send is logistics, and a
quote can legitimately be sent more than once.

## Consequences

- The version chain is the price history.
- Optimistic concurrency (`rowversion`) stops two estimators overwriting each other inside a
  draft, returning a 409 the UI explains.
- Slightly more clicks to revise a finalized estimate, which is the intended friction.
