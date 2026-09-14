# Phase 5 — refunds

- Refunds are source-bound to `SalesFinancialEffect` and limited by the paid remainder.
- Student-balance and cash methods are explicit; cash requires a treasury account.
- Platform and teacher portions are separate journal dimensions, and posted refunds can be reversed with an audit reason.
- Contract coverage: refund workflow, teacher amount validation, and endpoint authorization tests.
