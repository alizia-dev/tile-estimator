# Pricing engine

Implements SPEC §14's pricing half. Answers "what do we charge?".

---

## Order of operations

This order is the decision the whole page rests on, because discount-then-tax and
tax-then-discount give different totals.

```
1. Total cost   = direct cost + overhead                 (from the cost engine)
2. Subtotal     = cost with markup / margin / fixed markup applied
3. Discount     = percentage of, or fixed amount off, the subtotal
4. Tax          = taxable base × tax rate
5. Grand total  = discounted subtotal + tax
```

Step 4's base depends on `DiscountBeforeTax`, which is configurable per organization:

- **`true` (default)** — tax is charged on the discounted subtotal. The customer is taxed on what
  they actually pay.
- **`false`** — tax is charged on the full subtotal and the discount comes off afterwards.

A unit test pins both paths: on $1,000 with a 10% discount and 10% tax, discounting first gives
$990 and discounting last gives $1,000.

---

## Markup and margin are not the same thing

```
Markup: Price = Cost × (1 + Markup%)
Margin: Price = Cost ÷ (1 − Margin%)
```

On $1,000 of cost, a "25%" that means markup gives $1,250, and a "25%" that means margin gives
$1,333.33. Confusing them is a real and expensive mistake, so:

- the strategy actually used is **stored on the estimate**, not inferred;
- the estimate response reports the achieved `GrossMarginPercentage` back, so an estimator using
  markup can see what margin it actually produced;
- a margin of **100% or more is rejected** rather than clamped. The divisor would be zero or
  negative, and there is no sensible price to return.

`FixedMarkup` adds a flat amount, for jobs priced as "cost plus $2,000".

---

## Discounts

| Type | Behaviour |
|---|---|
| `None` | no discount |
| `Percentage` | rejected above 100% |
| `FixedAmount` | capped at the subtotal, so a discount never produces a negative price |

---

## Tax basis

US states differ on whether labor is taxable, so the basis is configurable per organization:

| Basis | Taxable portion |
|---|---|
| `MaterialsAndLabor` | everything |
| `MaterialsOnly` | the material share |
| `LaborOnly` | the labor share |

**How the share is derived.** The engine apportions the *priced* subtotal by the material/labor
mix of the *cost*. Taxing the cost directly would under-charge tax, because the customer is not
billed cost; apportioning the price keeps the tax proportional to what they are actually charged.

With $600 material and $400 labor cost, a materials-only basis taxes 60% of the price.

When there is no cost to apportion, everything is treated as taxable — tax is never silently
skipped.

---

## What the result reports

```
TotalCost                  what the job costs
MarkupAmount               subtotal − cost
Subtotal                   priced, before discount
DiscountAmount
TaxableAmount              the portion tax was charged on
TaxAmount
GrandTotal
GrossProfit                measured after the discount, not before
GrossMarginPercentage
EffectiveMarkupPercentage
Breakdown                  the arithmetic, in one readable line
```

`GrossProfit` is deliberately measured **after** the discount. A 50% markup followed by a 20%
discount leaves $200 of profit on $1,000 of cost, not $500, and the estimator needs to see the
$200.

---

## Snapshots (§15)

An estimate stores its whole pricing configuration — strategy, percentages, discount, tax rate,
tax basis, discount ordering — at the moment it was created. Changing the organization's defaults
afterwards does not move an estimate already in progress, and re-opening a finalized estimate
reproduces exactly the numbers that were quoted.

The same applies one level down: each estimate line stores the unit cost, unit price, waste
percentage and formula it was calculated with, so a catalog price change never rewrites history.
This is verified end to end: the workflow test raises a tile from $2.50 to $99.00 per square foot
and asserts the finalized estimate's total does not move.
