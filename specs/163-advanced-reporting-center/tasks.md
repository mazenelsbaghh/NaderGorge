# Tasks: مركز التقارير المتقدمة

**Input**: `specs/163-advanced-reporting-center/{spec,plan,research,data-model,contracts/reporting-api,quickstart}.md`  
**Testing policy**: الاختبارات إلزامية لأن الميزة تتعامل مع صلاحيات وPII ومال وتصدير. تكتب اختبارات القصة قبل إغلاق تنفيذها، وتستخدم PostgreSQL لعقود الاستعلام.

## Phase 1: Setup

- [ ] T001 Add ClosedXML and QuestPDF package references with pinned compatible versions in `backend/src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj`
- [ ] T002 [P] Add the licensed Arabic export font asset and attribution in `backend/src/NaderGorge.Infrastructure/Reporting/Exports/Fonts/README.md`
- [ ] T003 [P] Add reporting limits, export TTL, storage path, and worker concurrency settings in `backend/src/NaderGorge.API/appsettings.json`
- [ ] T004 [P] Add typed advanced-report client contracts in `frontend/src/services/advanced-report-service.ts`
- [ ] T005 Create reporting module dependency-registration entry point in `backend/src/NaderGorge.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`
- [ ] T006 Register reporting services and configuration validation in `backend/src/NaderGorge.API/Program.cs`

**Phase gate**: backend restores/builds and `docker compose config -q` passes before foundational work.

---

## Phase 2: Foundational — blocking prerequisites

- [ ] T007 Finalize `ReportDefinition` invariants, configuration-size limit, schema version, and concurrency token in `backend/src/NaderGorge.Domain/Entities/ReportDefinition.cs`
- [ ] T008 [P] Add `ReportExport`, format/status enums, lifecycle methods, and expiry invariants in `backend/src/NaderGorge.Domain/Entities/ReportExport.cs`
- [ ] T009 Add ReportDefinitions and ReportExports sets to the data abstraction in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [ ] T010 Configure report definition/export JSON columns, relationships, constraints, and indexes in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [ ] T011 Generate the report definitions/exports migration with indexes and constraints in `backend/src/NaderGorge.Infrastructure/Migrations/*_AddAdvancedReportingCenter.cs`
- [ ] T012 [P] Define field, operator, filter-group, chart, sort, page, snapshot, result, availability, and catalog DTOs in `backend/src/NaderGorge.Application/Features/Reporting/Contracts/ReportingContracts.cs`
- [ ] T013 [P] Define saved-definition and export DTOs in `backend/src/NaderGorge.Application/Features/Reporting/Contracts/ReportingPersistenceContracts.cs`
- [ ] T014 [P] Define `IReportCatalog`, `IReportDomainProvider`, `IReportScopeResolver`, `IReportExportGenerator`, and private storage interfaces in `backend/src/NaderGorge.Application/Features/Reporting/Services/ReportingInterfaces.cs`
- [ ] T015 Implement canonical JSON normalization, schema-version checking, and stable SHA-256 snapshot hashing in `backend/src/NaderGorge.Application/Features/Reporting/Services/ReportSnapshotNormalizer.cs`
- [ ] T016 Implement FluentValidation rules for depth, count, type/operator/value, columns, sort, chart, timezone, and unknown scope properties in `backend/src/NaderGorge.Application/Features/Reporting/Validation/ReportRequestValidators.cs`
- [ ] T017 Implement server-derived Admin/Teacher/TeacherStaff scope resolution and permission revalidation in `backend/src/NaderGorge.Infrastructure/Reporting/ReportScopeResolver.cs`
- [ ] T018 Implement the typed 13-domain catalog and the 9-domain teacher projection in `backend/src/NaderGorge.Infrastructure/Reporting/Catalog/ReportCatalog.cs`
- [ ] T019 Add centralized field-classification and teacher redaction policy for secrets, support, security, and tracking codes in `backend/src/NaderGorge.Infrastructure/Reporting/Catalog/ReportFieldPolicy.cs`
- [ ] T020 Add reporting rate-limit policies and owner-safe exception mapping in `backend/src/NaderGorge.API/Extensions/ReportingApiExtensions.cs`
- [ ] T021 [P] Add unit tests for canonical hashing, schema versions, nested-filter limits, operator compatibility, and Cairo timezone validation in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportValidationTests.cs`
- [ ] T022 [P] Add authorization tests for Admin, delegated reports.manage, Teacher, TeacherStaff reports/reports.finance, Student, and revoked memberships in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportScopeResolverTests.cs`
- [ ] T023 Add PostgreSQL integration fixture with two isolated teachers and representative facts in `backend/tests/NaderGorge.Integration.Tests/Reporting/PostgresReportingFixture.cs`
- [ ] T024 Run migration/build/tests and record Phase 2 gate evidence in `specs/163-advanced-reporting-center/verification/foundation.md`

**Phase gate**: no story implementation proceeds until schema migration, field policy, scope tests, and PostgreSQL fixture pass.

---

## Phase 3: User Story 1 — تقرير متعدد الفلاتر (P1)

**Goal**: كتالوج + query يعيد summary/chart/table متسقة مع nested filters.

**Independent test**: Admin ينفذ `(Package A OR B) AND bought AND noWatch AND Cairo range` ويطابق summary/chart/table الصفوف المتوقعة.

- [ ] T025 [P] [US1] Add contract tests for GET catalog, scoped lookups, and POST query success/error envelopes in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportingApiContractTests.cs`
- [ ] T026 [P] [US1] Add PostgreSQL tests for all/any nesting, null semantics, escaped contains, stable pagination, and invalid-field fail-closed behavior in `backend/tests/NaderGorge.Integration.Tests/Reporting/ReportFilterTranslationTests.cs`
- [ ] T027 [P] [US1] Add PostgreSQL tests proving summary, chart, and table share one scoped base set/asOf timestamp in `backend/tests/NaderGorge.Integration.Tests/Reporting/ReportResultConsistencyTests.cs`
- [ ] T028 [US1] Implement catalog and scoped lookup query handlers returning only actor-authorized domains/fields/presets/values in `backend/src/NaderGorge.Application/Features/Reporting/Queries/GetReportCatalogQuery.cs`
- [ ] T029 [US1] Implement validated report execution orchestration, cancellation, snapshot creation, and availability handling in `backend/src/NaderGorge.Application/Features/Reporting/Queries/ExecuteReportQuery.cs`
- [ ] T030 [P] [US1] Implement reusable typed filter translation helpers without raw client expressions in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/ReportFilterTranslator.cs`
- [ ] T031 [P] [US1] Implement students provider with academic/profile/contact/activity projections in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/StudentsReportProvider.cs`
- [ ] T032 [P] [US1] Implement purchases/access provider with distinct source and lifecycle semantics in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/PurchasesAccessReportProvider.cs`
- [ ] T033 [P] [US1] Implement engagement provider for watches, progress, completion, and inactivity in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/EngagementReportProvider.cs`
- [ ] T034 [US1] Implement eligible-non-buyer and buyers-not-started cohort builders with fixed asOf semantics in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/CommercialCohortBuilder.cs`
- [ ] T035 [US1] Add GET catalog, scoped lookup, and POST query endpoints with reports authorization/rate limits in `backend/src/NaderGorge.API/Controllers/ReportsController.cs`
- [ ] T036 [P] [US1] Implement API calls, runtime response guards, cancellation, and error mapping in `frontend/src/services/advanced-report-service.ts`
- [ ] T037 [P] [US1] Build accessible nested all/any filter groups with typed controls in `frontend/src/components/reports/ReportFilterBuilder.tsx`
- [ ] T038 [P] [US1] Build domain/preset/column/sort/chart selectors driven only by server catalog in `frontend/src/components/reports/ReportConfigurationPanel.tsx`
- [ ] T039 [P] [US1] Build summary cards and availability/warning/empty/error states in `frontend/src/components/reports/ReportSummary.tsx`
- [ ] T040 [P] [US1] Build chart rendering plus accessible tabular alternative from API series in `frontend/src/components/reports/ReportChart.tsx`
- [ ] T041 [P] [US1] Build typed, sortable, paged, responsive detail table in `frontend/src/components/reports/ReportResultsTable.tsx`
- [ ] T042 [US1] Compose the shared report workspace with abortable execution and snapshot visibility in `frontend/src/components/reports/AdvancedReportWorkspace.tsx`
- [ ] T043 [US1] Replace the admin report center content while preserving legacy audit/KPI access through presets or links in `frontend/src/app/admin/reports/AdminReportsPageClient.tsx`
- [ ] T044 [P] [US1] Add component tests/checks for operator reset, nested groups, pagination, and availability states in `frontend/tests/e2e/advanced-reports-admin.spec.ts`
- [ ] T045 [US1] Run story tests and record correctness/performance/Docker evidence in `specs/163-advanced-reporting-center/verification/us1.md`

---

## Phase 4: User Story 2 — تقارير المدرس المقيدة (P1)

**Goal**: نفس المحرك بكتالوج ونطاق مدرس آمن مفروض من الخادم.

**Independent test**: Teacher A لا يرى أي صف أو total/chart category خاص بـTeacher B حتى مع body مُعدّل، ولا يمكنه فتح support/staff/security/parent-tracking.

- [ ] T046 [P] [US2] Add cross-teacher leakage tests for rows, summary, chart, lookups, validation errors, and totals in `backend/tests/NaderGorge.Integration.Tests/Reporting/TeacherReportIsolationTests.cs`
- [ ] T047 [P] [US2] Add teacher student-data allow/deny tests for contacts versus tokens/devices/support/security/raw parent code in `backend/tests/NaderGorge.Application.Tests/Reporting/TeacherReportFieldPolicyTests.cs`
- [ ] T048 [P] [US2] Add TeacherStaff permission grant/revoke and finance permission tests in `backend/tests/NaderGorge.Integration.Tests/Reporting/TeacherStaffReportingPermissionTests.cs`
- [ ] T049 [US2] Apply effective teacher scope before every provider filter/projection/aggregate in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/ScopedReportProviderBase.cs`
- [ ] T050 [US2] Implement teacher-safe balance/recharge derivation limited to teacher-scoped recharge and content purchases in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/BalanceRechargeReportProvider.cs`
- [ ] T051 [US2] Implement teacher finance projection limited to actor teacher allocations/account/payouts in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/TeacherFinanceReportProvider.cs`
- [ ] T052 [US2] Add explicit teacher permission keys `reports` and `reports.finance` to membership validation and management contracts in `backend/src/NaderGorge.Application/Services/TeacherAuthorizationService.cs`
- [ ] T053 [P] [US2] Add teacher Reports navigation item guarded by the reporting permission in `frontend/src/components/teacher/TeacherShellChrome.tsx`
- [ ] T054 [P] [US2] Add teacher route and server/client bootstrap in `frontend/src/app/teacher/reports/page.tsx`
- [ ] T055 [US2] Compose teacher workspace with scoped labels and forbidden-domain handling in `frontend/src/app/teacher/reports/TeacherReportsPageClient.tsx`
- [ ] T056 [P] [US2] Add E2E tests for Teacher A/B isolation, hidden domains, direct API tampering, and staff revocation in `frontend/tests/e2e/advanced-reports-teacher.spec.ts`
- [ ] T057 [US2] Run story tests and record negative-permission/Docker evidence in `specs/163-advanced-reporting-center/verification/us2.md`

---

## Phase 5: User Story 3 — حفظ وتصدير التقرير (P1)

**Goal**: definitions شخصية versioned، وXLSX/PDF خاصان من نفس snapshot المصرح بها.

**Independent test**: حفظ/إعادة فتح التقرير، ثم تنزيل XLSX/PDF ومقارنة filters/Cairo timestamp/columns/rows مع نفس query؛ منع التنزيل بعد سحب الصلاحية أو expiry.

- [ ] T058 [P] [US3] Add definition CRUD, owner isolation, size, schema migration, and optimistic concurrency tests in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportDefinitionTests.cs`
- [ ] T059 [P] [US3] Add frozen-row snapshot, lifecycle, idempotency, owner isolation, permission-revocation, mutation-after-acceptance, and expiry tests in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportExportLifecycleTests.cs`
- [ ] T060 [P] [US3] Add XLSX workbook parsing tests for sheets, typed cells, Arabic headers, filters, and exact rows in `backend/tests/NaderGorge.Integration.Tests/Reporting/XlsxReportExportTests.cs`
- [ ] T061 [P] [US3] Add PDF parse/render tests for validity, Arabic shaping/RTL, filters, Cairo time, pagination, and exact counts in `backend/tests/NaderGorge.Integration.Tests/Reporting/PdfReportExportTests.cs`
- [ ] T062 [US3] Implement create/list/get/update/copy/delete/run definition handlers with canonical config and audit in `backend/src/NaderGorge.Application/Features/Reporting/Commands/ReportDefinitionCommands.cs`
- [ ] T063 [US3] Implement export request/status/delete/download handlers that stream authorized rows into an immutable private spool at acceptance in `backend/src/NaderGorge.Application/Features/Reporting/Commands/ReportExportCommands.cs`
- [ ] T064 [P] [US3] Implement private random-key filesystem/object abstraction with path traversal prevention in `backend/src/NaderGorge.Infrastructure/Reporting/Exports/PrivateReportExportStorage.cs`
- [ ] T065 [P] [US3] Implement real XLSX Summary/Data generation with typed values and bounded memory in `backend/src/NaderGorge.Infrastructure/Reporting/Exports/XlsxReportExportGenerator.cs`
- [ ] T066 [P] [US3] Implement RTL Arabic PDF title/filter/summary/table generation with embedded font in `backend/src/NaderGorge.Infrastructure/Reporting/Exports/PdfReportExportGenerator.cs`
- [ ] T067 [US3] Implement durable database job claiming, bounded retry, stale-running recovery, and file generation from the frozen spool in `backend/src/NaderGorge.API/BackgroundServices/ReportExportWorker.cs`
- [ ] T068 [US3] Implement expired-file cleanup without public URLs in `backend/src/NaderGorge.API/BackgroundServices/ReportExportCleanupService.cs`
- [ ] T069 [US3] Add definition/export/download endpoints, no-store headers, idempotency, and safe filenames in `backend/src/NaderGorge.API/Controllers/ReportsController.cs`
- [ ] T070 [P] [US3] Build saved reports list/create/rename/copy/delete/version-conflict UI in `frontend/src/components/reports/SavedReportsPanel.tsx`
- [ ] T071 [P] [US3] Build XLSX/PDF request, polling, retry, expiry, and authenticated download UI in `frontend/src/components/reports/ReportExportPanel.tsx`
- [ ] T072 [P] [US3] Add E2E save/restore/version-conflict/export/download/permission-revoke flows in `frontend/tests/e2e/advanced-report-exports.spec.ts`
- [ ] T073 [US3] Run story tests, inspect generated files, and record no-PII audit/Docker evidence in `specs/163-advanced-reporting-center/verification/us3.md`

---

## Phase 6: User Story 4 — جميع مجالات التقارير (P2)

**Goal**: إظهار 13 مجالاً للأدمن و9 للمدرس، مع providers موثوقة أو availability صريحة.

**Independent test**: catalog matrix يطابق الدور، وكل provider يعيد seeded truth أو partial/unavailable موثق بلا أرقام مصطنعة.

- [ ] T074 [P] [US4] Add a catalog completeness contract test for 13 admin domains, 9 teacher domains, Arabic labels, presets, availability, and forbidden fields in `backend/tests/NaderGorge.Application.Tests/Reporting/ReportCatalogCompletenessTests.cs`
- [ ] T075 [P] [US4] Add provider truth-table tests for codes and lifecycle/redaction in `backend/tests/NaderGorge.Integration.Tests/Reporting/CodesReportProviderTests.cs`
- [ ] T076 [P] [US4] Add provider truth-table tests for content hierarchy and shared packages in `backend/tests/NaderGorge.Integration.Tests/Reporting/ContentReportProviderTests.cs`
- [ ] T077 [P] [US4] Add provider truth-table tests for exams/homework/question error rates in `backend/tests/NaderGorge.Integration.Tests/Reporting/AssessmentsReportProviderTests.cs`
- [ ] T078 [P] [US4] Add admin-only provider tests for staff/support/security/parent tracking including redaction in `backend/tests/NaderGorge.Integration.Tests/Reporting/AdminOnlyReportProviderTests.cs`
- [ ] T079 [P] [US4] Add provider truth-table tests for comments/community and teacher scoping in `backend/tests/NaderGorge.Integration.Tests/Reporting/CommentsCommunityReportProviderTests.cs`
- [ ] T080 [US4] Implement codes provider without plaintext/hash leakage in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/CodesReportProvider.cs`
- [ ] T081 [US4] Implement content hierarchy provider including shared ownership rules in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/ContentReportProvider.cs`
- [ ] T082 [US4] Implement assessment provider for exam/homework status, score, pass, and error-rate projections in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/AssessmentsReportProvider.cs`
- [ ] T083 [US4] Implement comments/community provider with scoped bodies and response metrics in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/CommentsCommunityReportProvider.cs`
- [ ] T084 [P] [US4] Implement admin-only staff operations provider with finance.manage field gate for salary details in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/StaffOperationsReportProvider.cs`
- [ ] T085 [P] [US4] Implement admin-only support aggregates without message bodies/attachments in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/SupportReportProvider.cs`
- [ ] T086 [P] [US4] Implement admin-only parent tracking provider with partial telemetry availability in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/ParentTrackingReportProvider.cs`
- [ ] T087 [P] [US4] Implement admin-only security/audit aggregates with token/password/JSON redaction in `backend/src/NaderGorge.Infrastructure/Reporting/Providers/SecurityAuditReportProvider.cs`
- [ ] T088 [US4] Register all providers and presets with deterministic catalog keys in `backend/src/NaderGorge.Infrastructure/Reporting/ReportingServiceCollectionExtensions.cs`
- [ ] T089 [P] [US4] Add E2E catalog/domain availability and representative preset coverage in `frontend/tests/e2e/advanced-report-domains.spec.ts`
- [ ] T090 [US4] Run story tests and record per-domain availability/source/Docker evidence in `specs/163-advanced-reporting-center/verification/us4.md`

---

## Phase 7: Polish and cross-cutting quality

- [ ] T091 [P] Add Cairo midnight and DST boundary tests for query grouping and both exports in `backend/tests/NaderGorge.Integration.Tests/Reporting/CairoReportingTimeTests.cs`
- [ ] T092 [P] Add query-plan assertions/EXPLAIN evidence for representative 100k-row presets in `backend/tests/NaderGorge.Integration.Tests/Reporting/ReportingQueryPlanTests.cs`
- [ ] T093 Add composite indexes justified by measured plans in `backend/src/NaderGorge.Infrastructure/Migrations/*_OptimizeAdvancedReportingQueries.cs`
- [ ] T094 [P] Add worker concurrency, exactly-once claim, restart recovery, cancellation, and five-active-export limit tests in `backend/tests/NaderGorge.Integration.Tests/Reporting/ReportExportConcurrencyTests.cs`
- [ ] T095 [P] Add responsive, RTL, keyboard, chart-alternative, loading/empty/error/403 accessibility checks in `frontend/tests/e2e/advanced-reports-accessibility.spec.ts`
- [ ] T096 Apply Clean Code Guard to changed production code and record findings/fixes in `specs/163-advanced-reporting-center/verification/clean-code-guard.md`
- [ ] T097 Apply Test Guard to reporting tests and record determinism/isolation/fixture findings in `specs/163-advanced-reporting-center/verification/test-guard.md`
- [ ] T098 Run `make verify`, focused PostgreSQL tests, frontend lint/typecheck/build, and E2E suite and record exact results in `specs/163-advanced-reporting-center/verification/final.md`
- [ ] T099 Run `docker compose config -q`, `make up`, `make migrate`, service health checks, and sample export in containers and record evidence in `specs/163-advanced-reporting-center/verification/final.md`
- [ ] T100 Complete product-owner manual QA for Admin, Teacher, TeacherStaff, negative permissions, mobile, and both file formats in `specs/163-advanced-reporting-center/verification/manual-qa.md`

## Dependencies

```text
Setup (T001-T006)
  -> Foundation (T007-T024)
     -> US1 query engine (T025-T045)
        -> US2 teacher surface/isolation (T046-T057)
        -> US3 save/export (T058-T073)
        -> US4 remaining domains (T074-T090)
           -> Polish/final gates (T091-T100)
```

- US2 وUS3 يمكن بدء اختباراتهما بالتوازي بعد اكتمال US1 contract، لكن لا يغلقان قبل foundation scope/authorization.
- US4 providers مستقلة ويمكن تنفيذ T080-T087 بالتوازي بعد ثبات catalog/provider interfaces.
- التصدير يعتمد على query snapshot الثابتة، لذلك لا يسبق T029.

## Parallel execution examples

- **US1**: T031 students، T032 purchases، T033 engagement، T037 filter builder، T039 summary، T040 chart، T041 table.
- **US2**: T046/T047/T048 tests بالتوازي، وT053/T054 frontend بعد ثبات keys.
- **US3**: T060/T061 tests، T064 storage، T065 XLSX، T066 PDF، T070 saved UI، T071 export UI.
- **US4**: أزواج test/provider لكل مجال T075-T087 على ملفات منفصلة.

## Implementation strategy

1. **MVP P1**: foundation + US1 ليعمل admin query لطلاب/شراء/engagement مع nested filters ونتائج كاملة.
2. **Secure teacher increment**: US2؛ لا تُفتح route المدرس قبل اجتياز leakage matrix.
3. **Persistence/export increment**: US3؛ لا يُسمح بتنزيل ملف بلا reauthorization/TTL.
4. **Breadth increment**: US4 لإكمال كل المجالات مع unavailable صريح حيث المصدر غير كافٍ.
5. **Ship gate**: T091-T100 جميعها؛ أي فشل صلاحية أو تناقض export هو no-go.

## Format validation

- المهام: 100، IDs متسلسلة T001–T100.
- كل مهام قصص المستخدم تحمل `[US1]`..`[US4]`.
- `[P]` مستخدم فقط لملفات/أعمال قابلة للتنفيذ بالتوازي.
- كل مهمة إنتاج/اختبار/توثيق تشير إلى مسار ملف محدد.
