# ADR-007: Assemblies as configurable installation systems

Status: Accepted

## Context

A tiled surface needs far more than tile: thinset, grout, backer board, waterproofing, trim,
caulk and several labor operations. Estimators should not add fifteen lines by hand each time,
and the quantities depend on coverage values that vary by product.

## Decision

An `Assembly` is an organization-owned, editable list of `AssemblyItem` rows. Each item points at
a material, a labor rate, or the surface own tile, and derives its quantity by one of four
methods: `PerArea`, `PerLinearFoot`, `PerEach`, `PerCoverage`.

Coverage comes from the material catalog row, or from a `CoverageOverride` on the item. A
`PerCoverage` item with no coverage anywhere is an **error**, not a zero.

Six assemblies are seeded per organization, and all are editable.

## Rejected

**Hard-coded bills of materials.** Contractors differ on products, coverages and what they
include. Baking that in would make the tool wrong for most of them.

**Hard-coded coverage constants.** "95 SF per bag" is a property of one product, not of the
universe.

## Consequences

- One selection expands into a complete, correctly quantified line set.
- Assemblies are catalog data, so deactivating a material removes it from *new* calculations
  while existing estimates keep their snapshots.
- A mis-configured assembly fails loudly rather than quietly producing zero bags of thinset.
