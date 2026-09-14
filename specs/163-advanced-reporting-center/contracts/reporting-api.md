# Reporting API Contract

**Base path**: `/api/reports`  
**Authentication**: JWT required  
**Timezone v1**: `Africa/Cairo`  
**Content types**: JSON except download

## Authorization

- Admin: role Admin bypass or actor with `reports.manage` on admin surface.
- Teacher: active Teacher owner; active TeacherStaff membership requires key `reports`; financial domain/columns additionally require owner or `reports.finance`.
- Student/guest/assistant without a scoped membership: 403.
- Server resolves scope. Any client `teacherId`, `ownerId`, `scope` fields are rejected as unknown fields (400), not ignored.
- Saved definitions and exports are owner-only and are reauthorized on read/run/download.

## Common errors

```json
{
  "success": false,
  "error": {
    "code": "REPORT_FIELD_NOT_ALLOWED",
    "message": "هذا الحقل غير متاح في التقرير المحدد.",
    "details": [{ "path": "filters.conditions[0].field", "code": "not_allowed" }],
    "correlationId": "..."
  }
}
```

Status mapping: 400 invalid schema/operator/value؛ 401 unauthenticated؛ 403 domain/field/scope denied؛ 404 owned resource absent (لا يكشف وجود مورد الغير)؛ 409 version conflict؛ 413 export/definition limits؛ 422 valid schema but unsupported combination؛ 429 rate/complexity limit؛ 500 safe generic.

## GET `/catalog`

يعيد الكتالوج المصرح به فقط. لا يقبل actor/teacher parameters.

```json
{
  "success": true,
  "data": {
    "schemaVersion": 1,
    "timezone": "Africa/Cairo",
    "limits": { "maxDepth": 3, "maxConditions": 20, "maxValues": 100, "maxPageSize": 200, "maxExportRows": 50000 },
    "domains": [{
      "key": "purchases_access",
      "label": "الشراء والوصول",
      "availability": "available",
      "reasonCode": null,
      "fields": [{
        "key": "accessStatus",
        "label": "حالة الوصول",
        "dataType": "enum",
        "operators": ["equals", "in", "notIn"],
        "filterable": true,
        "selectable": true,
        "sortable": true,
        "values": [{ "value": "active", "label": "فعال" }]
      }],
      "metrics": [{ "key": "students", "label": "الطلاب", "format": "integer" }],
      "dimensions": [{ "key": "purchaseSource", "label": "مصدر الشراء" }],
      "presets": [{ "key": "buyers_not_started", "label": "اشترى ولم يبدأ" }]
    }]
  }
}
```

كل المجالات تظهر للأدمن حتى لو `partial/unavailable`. المجالات الممنوعة للمدرس لا تظهر أصلاً؛ الاستدعاء المباشر لها يعيد 403.

## POST `/query`

### Request

```json
{
  "schemaVersion": 1,
  "domain": "purchases_access",
  "presetKey": null,
  "filters": {
    "combinator": "all",
    "conditions": [
      { "field": "accessStatus", "operator": "in", "values": ["active", "expired"] }
    ],
    "groups": [{
      "combinator": "any",
      "conditions": [
        { "field": "packageId", "operator": "in", "values": ["uuid-a", "uuid-b"] },
        { "field": "termId", "operator": "equals", "value": "uuid-c" }
      ],
      "groups": []
    }]
  },
  "columns": ["studentName", "studentPhone", "packageName", "accessStatus", "purchasedAt"],
  "sort": [{ "field": "purchasedAt", "direction": "desc" }],
  "chart": { "type": "bar", "dimension": "accessStatus", "measure": "students", "interval": null },
  "page": { "number": 1, "size": 50 },
  "timezone": "Africa/Cairo"
}
```

### Response

```json
{
  "success": true,
  "data": {
    "snapshot": {
      "hash": "sha256hex",
      "asOfUtc": "2026-07-22T11:00:00Z",
      "timezone": "Africa/Cairo",
      "scopeLabel": "محتوى المدرس الحالي",
      "appliedFilterText": "حالة الوصول: فعال أو منتهي ..."
    },
    "availability": { "status": "available", "reasonCode": null, "message": null },
    "summary": [{ "key": "students", "label": "الطلاب", "value": 124, "format": "integer" }],
    "chart": {
      "type": "bar",
      "categories": ["فعال", "منتهي"],
      "series": [{ "key": "students", "label": "الطلاب", "data": [100, 24] }],
      "table": [{ "category": "فعال", "students": 100 }, { "category": "منتهي", "students": 24 }]
    },
    "table": {
      "columns": [{ "key": "studentName", "label": "الطالب", "dataType": "string", "format": null }],
      "rows": [{ "studentName": "...", "studentPhone": "..." }],
      "page": { "number": 1, "size": 50, "totalRows": 124, "totalPages": 3, "isTruncated": false }
    },
    "warnings": []
  }
}
```

إذا المجال غير متاح يعود 200 مع availability `unavailable` وصفوف/metrics فارغة وreasonCode ثابت، لا أصفاراً موهمة. إذا field غير متاح يعود 400/403 قبل أي query.

## GET `/lookups/{domain}/{field}?search=&page=1&pageSize=30`

يعيد قيم الاختيار الديناميكية المصرح بها للحقول المرجعية مثل المدرس/الباقة/الترم/الحصة. يطبق نطاق actor قبل البحث، ويعيد `{items:[{value,label}],page,total}`. لا يسمح بحقل غير معرف كـlookup، وTeacher A لا يستطيع اكتشاف أسماء/معرفات محتوى Teacher B عبر lookup أو total.

## Saved definitions

### POST `/definitions`

```json
{
  "name": "اشترى ولم يشاهد - يوليو",
  "configuration": { "schemaVersion": 1, "domain": "purchases_access", "filters": {}, "columns": [], "sort": [], "chart": {}, "timezone": "Africa/Cairo" }
}
```

Returns 201 with `{id,name,domain,schemaVersion,version,createdAt,updatedAt,configuration}`. Server canonicalizes and strips page number/asOf/scope.

### GET `/definitions?domain=&page=1&pageSize=20`

Owner-only paged list. Configuration may be omitted in list response.

### GET `/definitions/{id}`

Revalidates catalog/scope. Returns warnings for removed fields and a migrated canonical configuration; forbidden domain returns 403 or owner-safe 404.

### PUT `/definitions/{id}`

Request includes `version`, `name`, `configuration`. Returns 409 `REPORT_VERSION_CONFLICT` if stale.

### POST `/definitions/{id}/copy`

Creates an owner copy with unique localized suffix; revalidates before copy.

### DELETE `/definitions/{id}?version=n`

Returns 204; audited. Owner-only.

### POST `/definitions/{id}/run`

Accepts page override only. Same response as `/query` and current authorization.

## Exports

### POST `/exports`

```json
{
  "format": "xlsx",
  "reportDefinitionId": null,
  "configuration": { "schemaVersion": 1, "domain": "students", "filters": {}, "columns": ["fullName"], "sort": [], "chart": {}, "timezone": "Africa/Cairo" }
}
```

- Exactly one of `reportDefinitionId` or `configuration`.
- Server normalizes, authorizes, fixes `asOfUtc`, streams the full authorized row set to a private immutable spool, stores its count/hash, then returns 202 for asynchronous XLSX/PDF rendering. Changes after acceptance cannot alter the export.
- Idempotency supported via `Idempotency-Key` scoped to owner/request hash.

```json
{
  "success": true,
  "data": { "id": "uuid", "status": "queued", "format": "xlsx", "requestedAt": "...", "expiresAt": "..." }
}
```

### GET `/exports/{id}`

Returns owner-only status, rowCount, safe failure, expiry and `downloadReady`; no storage path.

### GET `/exports/{id}/download`

- Owner-only; rechecks actor is still permitted for snapshot domain and fields.
- 409 if not completed، 410 if expired، 403 if permission was revoked.
- Headers: private `Content-Disposition`, accurate content type, `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`.

### DELETE `/exports/{id}`

Deletes owned file/record or marks expired; 204. Cleanup also runs automatically.

## Audit events

Actions: `ReportDefinitionCreated`, `ReportDefinitionUpdated`, `ReportDefinitionCopied`, `ReportDefinitionDeleted`, `ReportExportRequested`, `ReportExportCompleted`, `ReportExportDownloaded`, `ReportExportFailed`.

Audit payload includes domain, ids, format, snapshotHash, rowCount, actor/scope classification؛ excludes raw filter values classified as PII and excludes file path.

## Rate and complexity limits

- Query: 30/min per actor؛ max 20 conditions، depth 3، 100 values، page 200.
- Export: 5 active/actor، 20/day default configurable، 50,000 XLSX rows، 5,000 PDF rows، 30 selected columns.
- String `contains` on high-cardinality fields may require minimum 2 characters and cannot combine with unconstrained exports.
- Unsupported/too expensive plans return 422/429 with actionable reason, never silently broaden.
