# Data Model: مركز التقارير المتقدمة

## Persistent entities

### ReportDefinition

تعريف شخصي محفوظ؛ الموجود حالياً في الشجرة يحتاج mapping/migration واختبارات.

| Field | Type | Rules |
|---|---|---|
| Id | Guid | PK |
| OwnerUserId | Guid | FK Users، required، جزء من كل query |
| Name | string(120) | trim، 1..120، unique per owner case-insensitive بين غير المحذوف |
| Domain | string(64) | catalog key allowlist |
| ConfigurationJson | jsonb | canonical validated configuration فقط، حد 64KB |
| SchemaVersion | int | `>=1`، الإصدار الحالي من catalog/config |
| Version | uint/xmin أو long | optimistic concurrency |
| CreatedAt/UpdatedAt | DateTime UTC | BaseEntity |

**Indexes**: `(OwnerUserId, UpdatedAt desc)`, unique `(OwnerUserId, lower(Name))` إن لم يدعم soft delete؛ `(Domain)` ليس ضرورياً في v1.

**Relationship**: User 1—N ReportDefinition. لا يجوز للأدمن رؤية definitions الشخصية لمستخدم آخر في v1 إلا عبر تدقيق النظام.

### ReportExport

| Field | Type | Rules |
|---|---|---|
| Id | Guid | PK |
| OwnerUserId | Guid | FK Users، required |
| ReportDefinitionId | Guid? | FK nullable، export ad-hoc مسموح |
| Domain | string(64) | normalized domain |
| Format | enum | `Xlsx`, `Pdf` |
| Status | enum | `Queued`, `Running`, `Completed`, `Failed`, `Expired` |
| SnapshotJson | jsonb | normalized authorized request + scope fingerprint، لا أسرار خام |
| SnapshotHash | string(64) | SHA-256 hex، immutable |
| ScopeFingerprint | string(64) | hash actor/role/teacher/membership version |
| SnapshotDataKey | string(240) | private immutable streamed row spool؛ random key |
| RequestedAt | DateTime UTC | required |
| StartedAt/CompletedAt | DateTime? UTC | lifecycle |
| ExpiresAt | DateTime UTC | default +24h |
| RowCount | int? | frozen count؛ 0..50000 XLSX و0..5000 PDF |
| FileKey | string(240)? | private relative/object key، random، no user filename path |
| FileName | string(180)? | sanitized display name |
| ContentType | string(100)? | fixed allowlist |
| FileSizeBytes | long? | bounded |
| FailureCode | string(80)? | safe machine code |
| FailureMessage | string(500)? | لا stack trace/PII |
| AttemptCount | int | bounded retries |
| CreatedAt/UpdatedAt | DateTime UTC | BaseEntity |

**Indexes**: `(Status, RequestedAt)` للـ worker؛ `(OwnerUserId, RequestedAt desc)`؛ `(ExpiresAt)` للتنظيف. `SnapshotDataKey` و`FileKey` لا يخرجان في API أو audit.

**State transitions**:

```text
Queued -> Running -> Completed -> Expired
   |         |
   +-------> Failed
```

- لا يعود Completed إلى Running.
- retry ينشئ محاولة مضبوطة أو يعيد Failed إلى Queued فقط بأمر صريح وبنفس owner.
- download مسموح فقط في Completed وقبل ExpiresAt وبعد إعادة authorization.

## Value objects / contracts (not persisted as executable data)

### ReportConfiguration

| Field | Type | Rules |
|---|---|---|
| schemaVersion | int | catalog version |
| domain | string | allowed for effective actor |
| presetKey | string? | allowlist |
| filters | FilterGroup | required root؛ default `all` empty |
| columns | string[] | 1..30، allowed/visible fields only |
| sort | SortClause[] | 0..3 recommended، max 10 contract limit |
| chart | ChartRequest | allowed dimension/measure/interval |
| timezone | string | v1 `Africa/Cairo` |

### FilterGroup

| Field | Type | Rules |
|---|---|---|
| combinator | enum | `all` أو `any` |
| conditions | FilterCondition[] | root total max 20 |
| groups | FilterGroup[] | depth max 3 |

Empty root means no user filters but never removes server scope. Empty nested groups are rejected.

### FilterCondition

| Field | Type | Rules |
|---|---|---|
| field | string | catalog allowlist |
| operator | enum | constrained by field type |
| value | scalar? | for equals/comparison/text/date/bool |
| values | scalar[]? | for in/notIn/between |

Operators:

- string: `equals`, `notEquals`, `contains`, `startsWith`, `in`, `notIn`, `isEmpty`, `isNotEmpty`
- number/money/duration: `equals`, `gt`, `gte`, `lt`, `lte`, `between`, `in`
- date/datetime: `on`, `before`, `after`, `between`, `relativeDays`
- enum/reference: `equals`, `notEquals`, `in`, `notIn`
- bool: `isTrue`, `isFalse`
- derived state: catalog-specific enum only؛ لا expression حر

### EffectiveReportScope

| Field | Meaning |
|---|---|
| ActorUserId | authenticated subject |
| ActorKind | Admin / DelegatedAdmin / Teacher / TeacherStaff |
| TeacherId | required for teacher surfaces |
| AllowedDomains | server-derived set |
| AllowedFieldClassifications | e.g. academic, student-contact, teacher-finance |
| CanViewTeacherFinance | explicit permission |
| ScopeVersion | role/security stamp/membership update version |

لا يرسل العميل هذا الكائن ولا يعاد كاملاً له.

### ReportResult

- `snapshot`: hash, `asOfUtc`, timezone، applied filter summary، scope label.
- `availability`: status/reasonCode/message.
- `summary`: typed metric cards (key, label, value, format).
- `chart`: type, dimension, series، accessible table.
- `table`: columns metadata، rows dictionaries ذات types ثابتة، page metadata.
- `warnings`: truncation، partial data، unavailable metrics.

## Domain catalog

كل field يسجل: `key`, Arabic label, dataType, operators, filterable, sortable, selectable, sensitivity, allowedActors, lookup endpoint/values, null semantics.

### students

- identity/profile: studentId, fullName, phoneNumber, studentCode, activeStatus, registrationDate, birthDate, gender, nationality, governorate, district, address, secondaryPhone.
- parent/school: parentPhone, secondaryParentPhone, motherPhone, schoolName/type, educationStage, gradeLevel, studyTrack.
- activity: lastLoginAt إن وجد مصدر موثوق، lastWatchAt, watchedSeconds, completionPercent, inactivityDays, commitmentStatus, warningSeverity.
- commercial relation: purchaseCount, spentOnScopedContent, currentScopedBalance (teacher-safe derived only), accessStatus.
- forbidden to teacher/all reports: PasswordHash, refresh tokens, device fingerprints/IP, raw ParentTrackingCode.

### purchases_access

student, teacher, subject/package/term/lesson/video/exam target، source (`balance`, `code`, `gift`, `recharge`, `admin`)، purchase/access timestamps، expiry، active/expired/revoked/refunded state، gross/discount/paid/promo amounts.

### codes

group, code type/serial/status, teacher, target, price, created/expires/activated, student, use duration, purchase summary. يمنع plaintext/hash بالكامل.

### balance_recharge

student، transaction type/amount/balanceAfter/reference/date، recharge amount/wallet/sender phone/proof present/status/teacher scope/resolver/date/rejection category. Screenshot URL لا يظهر كعمود افتراضي للمدرس.

### content

teacher, subject, package, term, section, lesson, video/exam/homework، published/active، price، وعدد العناصر. لا تخلط metrics المشاهدة هنا.

### engagement

student، teacher/content hierarchy، first/last watch، watched seconds، duration، completion percent، watch count، progress/locked/override state، inactivity days، committed/at-risk state. session tokens وأسرار playback ممنوعة.

### assessments

student، teacher/content hierarchy، assessment type، attempt/submission status، started/submitted/graded، score/max/percentage/pass، question type/tag/error rate؛ feedback النصي ليس عموداً افتراضياً.

### teachers_finance

teacher، source/target، gross/discount/paid/promo، teacher/platform shares، allocation/review/payout status، payout dates. Teacher يرى صفوف TeacherId الخاص به فقط.

### staff_operations (admin only)

employee/role، attendance، tasks، CRM outcomes، media pipeline، payroll aggregate. تفاصيل راتب فردي تتطلب `finance.manage` بالإضافة إلى `reports.manage`.

### support (admin only)

conversation status/channel، queue/assignment، response/resolution duration، category، rating، staff؛ message bodies/attachments ليست أعمدة تقرير v1.

### comments_community

teacher/content، author student، item type/status، created/reviewed، response present/time، likes/poll aggregates. body optional sensitive selectable للأدمن/teacher scoped فقط وليس في exports الافتراضية.

### parent_tracking (admin only)

student، parent contact، hasTrackingCode، device token count، academic summary availability. raw tracking code/token ممنوع؛ open-count يظهر unavailable حتى وجود telemetry موثوق.

### security_audit (admin only)

action/entity/actor/time/IP masked/correlation، login/device/session aggregates. token/password/old-new sensitive JSON تحتاج redaction ولا تكون selectable افتراضياً.

## Presets

- `buyers_not_started`: access/purchase exists AND no watch event.
- `eligible_non_buyers`: eligible cohort AND no purchase source at asOf.
- `stopped_7_days`: last activity before Cairo now - 7 days.
- `bought_term_not_next`: bought selected term AND not next selected term.
- `watched_not_attempted`: watched target threshold AND no exam attempt.
- `failed_assessment`: completed attempt below pass threshold.
- `codes_by_status`: code lifecycle grouping.
- `recharge_by_sender_status`: admin only unless teacher scoped recharge.

كل preset يترجم إلى نفس FilterGroup المرئي ويمكن تعديله؛ لا يوجد منطق سري مختلف عن الجدول.
