# Review Report: Gifts and Free Access

## Scope Reviewed

- Admin gifts ledger, issue form, details page, revoke flow, shell navigation, and `gifts.manage` route/API protection.
- Gift issuance, recipient outcomes, direct grants, promotional balance allocation/usage/revocation/expiry, and student purchase funding integration.
- Student balance display and purchase preview integration for promotional versus paid balance.
- Migration/model consistency and focused automated tests.

## Architectural Review

- Gift issuance is modeled as an auditable aggregate with recipient-level outcomes, keeping partial success visible without hiding failed or already-entitled recipients.
- Direct content gifts reuse access grants and add gift-specific use caps, avoiding duplicate entitlement paths.
- Promotional value is separate from paid balance and uses allocation rows plus usage rows, preserving conservation of value: original amount equals available, consumed, expired, and revoked amounts.
- Purchase funding consumes promotional value inside the purchase transaction before paid balance, so content access and funding changes commit together.
- Teacher-restricted promotional balance resolves the authoritative content teacher server-side and does not trust frontend price or teacher input.
- Revocation removes only future/unspent value and disables only gift-linked grants, preserving historical activity.

## Code Guard Findings

- **Fixed:** Admin Shell now protects direct video-types as built-in Admin only while gifts are permission-based through `gifts.manage`.
- **Fixed:** Gift issue retries keep the same `requestId` after a failed submit and reset only after success, preserving idempotency.
- **Fixed:** Gift list service no longer sends empty enum query values for status or target type.
- **Fixed:** Quickstart SQL now matches actual migration table names.
- **Accepted residual risk:** PostgreSQL check constraints are covered by migration/model verification but live constraint execution is pending because Docker/PostgreSQL is unavailable in this environment.

## Test Guard Findings

- Focused tests cover permission denial, idempotent gift issue, partial recipients, video-only use consumption, promotional purchase funding order/conservation, teacher restriction, and idempotent revocation.
- E2E tests mock the gifts API and verify the Admin Shell, ledger/details/revoke interaction, and teacher-restricted promotional issue payload.
- Manual QA remains pending by definition until the product owner performs the scenario list in `quickstart.md`.

## UI/UX Review

- Gifts live as an operational admin workspace, not a marketing page.
- The issue form exposes target type, target lookup, teacher restriction, student selection, expiry/use limits, and reason without relying on hidden state.
- Ledger/details separate issuance-level state from recipient-level outcomes, making partial success reviewable.
- Revoke requires an explicit reason before submit.
