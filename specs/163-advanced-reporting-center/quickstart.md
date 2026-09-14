# Quickstart and Verification: مركز التقارير المتقدمة

## Prerequisites

- PostgreSQL test database منفصلة ومهاجرة.
- حسابات: Admin بصلاحية `reports.manage`، Supervisor بها/بدون الصلاحية، Teacher A/Teacher B، staff A بصلاحيات `reports` و`reports.finance`، Student A/B لكل مدرس.
- بيانات ثابتة تشمل شراء برصيد/كود/هدية، grant فعال ومنتهي، مشاهدة/عدم مشاهدة، امتحان/واجب، recharge، comment، support، audit.
- كل timestamps مخزنة UTC وتتضمن حالات حول منتصف ليل القاهرة وتغيير DST.

## Automated commands

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter Reporting
ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=nader_reporting_test;Username=postgres;Password=postgres' dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter Reporting
cd frontend && npm run lint && npm run typecheck && npm run build
docker compose config -q
make verify
make verify-e2e
```

لا يستخدم EF InMemory كدليل وحيد على صحة providers؛ اختبارات الفلاتر والـ aggregates يجب أن تمر على PostgreSQL.

## Test matrix

### A. Authorization and data isolation

| ID | Actor | Action | Expected |
|---|---|---|---|
| A01 | Guest | GET catalog | 401 |
| A02 | Student | POST query | 403 |
| A03 | Supervisor without reports.manage | admin query | 403 |
| A04 | Admin/reports.manage | all 13 domains catalog | visible including availability |
| A05 | Teacher A | catalog | only teacher-safe domains |
| A06 | Teacher A | send Teacher B id/unknown scope property | 400; no query executed |
| A07 | Teacher A | students query | only students linked to A content؛ summary/chart/table all exclude B |
| A07b | Teacher A | package/term lookup | only A-scoped values and totals; B identifiers undiscoverable |
| A08 | Teacher A | support/security/staff query direct | 403 with no counts/metadata |
| A09 | Teacher A | student contacts | allowed within scoped students |
| A10 | Teacher A | tokens/devices/raw parent code | field absent from catalog and 403/400 direct |
| A10b | Teacher A | parent_tracking domain direct | 403؛ parent contact remains available only through scoped students |
| A11 | Staff A reports | non-financial teacher domain | allowed and A-scoped |
| A12 | Staff A without reports.finance | teacher finance | 403 |
| A13 | revoke staff membership after save | run saved definition | 403/404 owner-safe |
| A14 | Owner B definition/export id guessed by A | get/update/download/delete | 404; no existence leak |
| A15 | permission revoked after export completes | download | 403, even if file exists |

### B. Filter semantics and validation

| ID | Case | Expected |
|---|---|---|
| B01 | empty root filters | valid; server scope remains |
| B02 | `(package A OR B) AND noWatch AND July` | exact seeded rows |
| B03 | `in` multiple teachers as Admin | union within group |
| B04 | nested all/any depth 3 | valid and exact |
| B05 | depth 4 / 21 conditions / 101 values | 400 before DB query |
| B06 | invalid field for domain | 400 field path |
| B07 | valid field but forbidden for actor | 403, no metadata leak |
| B08 | invalid operator for type | 400 |
| B09 | invalid UUID/enum/date/range | 400 localized message |
| B10 | empty nested group | 400 |
| B11 | reversed date between | 400 |
| B12 | null semantics isEmpty/isNotEmpty | correct SQL null handling |
| B13 | text contains special `%_` | treated as text, not wildcard injection |
| B14 | sort forbidden/nonselectable field | 400/403 |
| B15 | page size 201 | 400 or clamp explicitly per contract; preferred 400 |

### C. Business definitions

| ID | Case | Expected |
|---|---|---|
| C01 | bought active access, no watch | buyers_not_started includes |
| C02 | bought expired access | purchase=true, access=expired; not categorized notPurchased |
| C03 | gift access | source gift distinct |
| C04 | code access | source code + serial, no plaintext |
| C05 | refunded/revoked | separate state excluded/included by explicit filters |
| C06 | eligible student no access/purchase | eligible_non_buyers includes |
| C07 | student not academically eligible | eligible_non_buyers excludes |
| C08 | unpublished content | non-buyer cohort excludes by default |
| C09 | watched video but no exam attempt | watched_not_attempted includes |
| C10 | inactive >7 Cairo days | stopped preset boundary correct |
| C11 | scoped recharge teacher A | visible A; global/B recharge hidden |
| C12 | shared package allocations | visible only proportional/content ownership rules documented in provider |

### D. Result consistency and availability

| ID | Case | Expected |
|---|---|---|
| D01 | query result | summary total == matching table total == chart category sum where additive |
| D02 | pagination pages | stable ordering with Id tie-breaker; no duplicate/missing rows |
| D03 | same request | same snapshot hash normalization despite JSON property order |
| D04 | database changes after export acceptance | current UI can refresh; export rows remain identical to the frozen private spool |
| D05 | partial parent telemetry | availability partial + warning; unsupported metric unavailable, not 0 |
| D06 | empty legitimate result | available with total 0 and explicit empty state |
| D07 | provider failure | safe error/correlation id; no partial fake totals |
| D08 | unsupported chart dimension/measure | 422 |

### E. Saved definitions

| ID | Case | Expected |
|---|---|---|
| E01 | create valid | 201 canonical config, audit written |
| E02 | duplicate name owner | 409/validation message |
| E03 | 64KB+ config | 413/400 |
| E04 | update with current version | success/version increments |
| E05 | two concurrent updates | one succeeds, stale one 409 |
| E06 | copy | new id/name, same canonical allowed config |
| E07 | schemaVersion old supported | migrated with warning |
| E08 | schemaVersion unknown future | safe 422, never executes |
| E09 | field permission removed | open strips/flags; run cannot use it |
| E10 | delete | 204 + audit, subsequent 404 |

### F. Excel/PDF export

| ID | Case | Expected |
|---|---|---|
| F01 | request XLSX | 202 Queued, immutable hash |
| F02 | worker lifecycle | Queued→Running→Completed |
| F03 | XLSX parse | real workbook; Summary/Data sheets; typed numbers/dates; Arabic headers; exact rows |
| F04 | PDF parse/render | `%PDF` valid; Arabic shaped/RTL; title, filters, Cairo timestamp, summary/table |
| F05 | export current filters/columns/sort | exact authorized query match |
| F06 | >50k XLSX or >5k PDF rows | rejected before queued conversion; no silent partial file |
| F07 | 31 columns | 400 |
| F08 | two same idempotency keys | same export id/no duplicate job |
| F09 | download while queued | 409 |
| F10 | download completed owner | correct MIME/content-disposition/no-store |
| F11 | expired | 410 and file deleted/record expired |
| F12 | generation crash/restart | durable Failed/retry bounded, no stuck Running forever |
| F13 | malicious report name | sanitized filename; no path traversal/header injection |
| F14 | PII audit | no phone/filter raw value/file path in AuditLog |
| F15 | mutate/delete matching source rows after 202 | generated files still equal frozen accepted rows |
| F16 | spool cleanup | private snapshot spool deleted with completed/failed/expired export policy |

### G. Cairo time

| ID | Case | Expected |
|---|---|---|
| G01 | date-only 2026-07-22 | boundaries calculated in Africa/Cairo then UTC |
| G02 | event at local 00:00 minus 1ms | previous day |
| G03 | event at local 23:59:59.999 | included |
| G04 | DST transition | grouping uses timezone rules, not fixed +02/+03 |
| G05 | Excel/PDF | displayed timestamp and grouped dates match UI Cairo |

### H. Performance/resilience

| ID | Case | Expected |
|---|---|---|
| H01 | representative 100k+ fact rows | interactive p95 <5s |
| H02 | heavy presets | PostgreSQL plan uses intended indexes; no unbounded seq scan without documented reason |
| H03 | N+1 detector/log | bounded SQL query count per request |
| H04 | five active exports | sixth rejected/queued per limit |
| H05 | cancellation/client disconnect | DB query cancellation observed |
| H06 | rate limit exceeded | 429 with retry guidance |
| H07 | two export workers | job claimed once via database lock/idempotency |

### I. Frontend/accessibility

| ID | Case | Expected |
|---|---|---|
| I01 | mobile admin/teacher | no horizontal viewport overflow; table controlled scroll |
| I02 | keyboard only | add/remove group, choose field, run/save/export usable |
| I03 | chart | accessible data table/labels present |
| I04 | loading/empty/error/403/unavailable | distinct Arabic states |
| I05 | remove selected field | dependent invalid operator/value reset visibly |
| I06 | saved definition restore | filters/columns/sort/chart restored after revalidation |
| I07 | export polling | progress, failure retry, expiry/download state |
| I08 | teacher navigation | Reports visible only when authorized |

## Manual smoke flow

1. سجّل Admin، افتح التقارير، اختر `المبيعات والوصول`.
2. أنشئ مجموعتي شروط: `(Package A أو B)` و`لم يشاهد`، وحدد فترة القاهرة.
3. تحقق من summary/chart/table، غيّر الصفحة والفرز، احفظ التقرير.
4. صدّر XLSX وPDF، افتح الملفين وقارن 10 صفوف عشوائية والعدد الكلي.
5. سجّل Teacher A، افتح التقرير ذاته ضمن محتواه؛ تأكد من غياب مدرس B.
6. عدل body يدوياً لإرسال teacherId/field support؛ تحقق من الرفض.
7. اسحب permission من staff أثناء وجود report محفوظ/export مكتمل؛ تحقق من منع run/download.

## Docker close-out

```bash
docker compose config -q
make up
make migrate
docker compose ps
curl -fsS http://localhost:5000/health
```

أرفق في تقرير الإغلاق: commit، migration، أعداد الاختبارات، p95، query-plan notes، ملفات export samples غير محتوية PII حقيقية، نتيجة health، ومخاطر/استثناءات مع قرار go/no-go.
