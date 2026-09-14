# Quickstart: Phase 1 Sales and Content Completion

## Prerequisites

- Existing Spec 151 migration/content identity is applied.
- Existing Spec 152 migration/gifts and promotional balance is applied.
- Admin account and one delegated staff account.
- At least two teachers, subjects, packages, lessons, videos with video types, and question-bank items.
- Docker daemon and PostgreSQL available for live migration/integration checks.

## Build and Migrate

```bash
docker compose config -q
make up
make migrate
make ps
```

Open:

- Admin sales: `http://localhost:8740/admin/sales/coupons`
- Admin printable codes: `http://localhost:8740/admin/sales/printable-codes`
- Admin public exams: `http://localhost:8740/admin/public-exams`
- Student public exams: `http://localhost:8739/student/public-exams`

## Automated Verification

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~Phase1SalesContentTests|FullyQualifiedName~PublicExamProductTests"
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore
dotnet ef migrations has-pending-model-changes --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API --no-build
(cd frontend && npm run lint)
(cd frontend && npx tsc --noEmit)
(cd frontend && npm run build)
(cd frontend && npx playwright test tests/e2e/admin-sales.spec.ts tests/e2e/public-exams.spec.ts --project=chromium)
```

## SQL Invariants

Run against PostgreSQL after scenarios:

```sql
select "NormalizedCode", count(*)
from "sales_coupons"
group by "NormalizedCode"
having count(*) > 1;

select "SerialNumber", count(*)
from "printable_sales_codes"
group by "SerialNumber"
having count(*) > 1;

select id
from "sales_financial_effects"
where "GrossAmount" < 0
   or "CouponDiscountAmount" < 0
   or "PrintableCodeDiscountAmount" < 0
   or "PromotionalAmount" < 0
   or "PaidAmount" < 0
   or "GrossAmount" <> "CouponDiscountAmount" + "PrintableCodeDiscountAmount" + "PromotionalAmount" + "PaidAmount";

select u.id
from "sales_coupon_usages" u
left join "sales_financial_effects" f on f."PurchaseOperationId" = u."PurchaseOperationId"
where f.id is null;

select r.id
from "printable_code_redemptions" r
left join "printable_sales_codes" c on c.id = r."PrintableCodeId"
where c.id is null;
```

Every query must return zero rows.

## Owner Manual QA

Record status beside every item. Default status is `pending` until the product owner performs it.

1. `pending` - Admin creates percentage coupon for one teacher-owned package and verifies only eligible purchase accepts it.
2. `pending` - Admin creates fixed-value coupon for platform-wide purchases and verifies out-of-scope purchase rejects it.
3. `pending` - Admin configures stacking policy and verifies allowed and blocked coupon/printed-code combinations.
4. `pending` - Student attempts expired/disabled/over-limit coupon and sees clear rejection with no purchase/access change.
5. `pending` - Admin creates printable template with QR/code/serial, previews it, and confirms missing QR/code template is blocked.
6. `pending` - Admin generates printable batch, redeems one code as student, and duplicate redemption is rejected.
7. `pending` - Admin publishes free public exam; student starts/submits it without payment and result appears in public-exam report.
8. `pending` - Admin publishes paid public exam; student buys it, starts/submits it, and access is limited to that exam.
9. `pending` - Admin disables a public exam; new purchase/start is blocked, previous result remains visible.
10. `pending` - Delegated staff without new permissions cannot create/disable coupons, codes, templates, or public exams.
11. `pending` - Existing legacy CodeGroup redemption still works for package/lesson/video/exam/balance codes.
12. `pending` - Existing gift/promotional purchase scenario from Spec 152 still works.

## Completion Notes

- Manual QA remains pending until the owner updates this checklist.
- Docker/PostgreSQL verification remains pending if Docker daemon is unavailable.
