# Research Document: watch-requests-refinements-and-repurchases

## 1. Context and Findings
- **Egypt Timezone**: Appending `Z` to DB ISO strings in `admin-utils.ts` ensures Cairo local timezone (UTC+3) is parsed correctly relative to UTC in browser-side date formatting functions.
- **Modified Decisions**: We removed `Pending` constraints in `ApproveWatchRequestCommand` and `RejectWatchRequestCommand` allowing the status of watch requests to be altered dynamically (e.g. from Approved to Rejected).
- **Custom Extra Views**: `ApproveWatchRequestCommand` now accepts an `AddedViews` parameter which increments `CustomMaxWatchCount` dynamically rather than hardcoding it to 1. Rejection decrements the count by 1 to revert previous approval.
- **Student Lesson Repurchase**: Under `PurchaseContentCommand`, lessons already purchased can be repurchased if one or more video watch counts are exhausted or locked, or if they have rejected requests. This deducts the price from the student's balance and resets all video watch limits of the lesson to 0.

## 2. Alternatives Investigated
- **Reversing Decisions by Deleting Records**: Considered deleting requests, but keeping them as `Approved` or `Rejected` is better for historical audits.
- **Deducting Views dynamically on Rejected**: Decrementing the limit by 1 is the safest approach to prevent lock state errors and preserve historical increments.
