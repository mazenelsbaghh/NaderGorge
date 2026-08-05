# Phase 6 — live source and teacher relationship

- Recharge adapters preserve `TeacherId = null` for general balance and require a teacher for scoped balance.
- Sales, teacher settlement, and payroll adapters post balanced entries with deterministic idempotency keys.
- Teacher summary reports gross sales, platform share, teacher share, refunds, paid, and outstanding amounts from posted journal dimensions.
- Existing recharge command already carries the immutable teacher scope; new adapter tests protect it from becoming a generic wallet movement.
