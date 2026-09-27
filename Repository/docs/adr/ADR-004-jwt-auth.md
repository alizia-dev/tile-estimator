# ADR-004: JWT access tokens with rotating refresh tokens

Status: Accepted

## Context

An Angular SPA talks to a stateless API. The design should suit a later move to OIDC.

## Decision

Short-lived JWT access tokens (15 minutes) plus long-lived, rotating, revocable refresh tokens.
Only the **hash** of a refresh token is stored. Rotation revokes the presented token and records
its successor.

The JWT carries identity and the active organization only: no business data, no prices.

Presenting an already-revoked token is treated as theft. **The whole token family is revoked**
and the user must sign in again.

Permissions are re-read from the database on every request rather than trusted from the token.

## Rejected

**Long-lived access tokens.** A stolen token would stay valid for its whole life, and a
revocation could not take effect.

**Server-side sessions.** They work, but cost a lookup per request and complicate scaling out.

**Permissions baked into the token.** A role change would not take effect until the token
expired, which is precisely when it matters most.

## Consequences

- Revocation is real, and a role change is immediate.
- One membership lookup per request, indexed and cheap.
- Refresh needs request queueing on the client so six parallel requests do not rotate six times.
  The Angular interceptor does this.
