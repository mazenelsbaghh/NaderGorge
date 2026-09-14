# Feature Specification: Platform Phase 0 Audit

**Feature Branch**: `[150-platform-phase0-audit]`
**Created**: 2026-06-27
**Status**: Draft
**Input**: User description: "ابدء باول فيس باستخدام speckit-all؛ المقصود المؤكد هو Phase 0 Audit فقط من docs/platform-change-roadmap.md، بدون تنفيذ كود إنتاجي."

## Clarifications

### Session 2026-06-27

- Q: ما تعريف `Completed` في تقرير Phase 0؟ → A: `Completed = implemented evidence`: وجود كود/واجهة/اختبارات أو أثر واضح، مع تسجيل manual QA كـ pending لو لم تُجرّب.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - إدارة المنصة ترى حالة الموجود قبل التنفيذ (Priority: P1)

كمالك أو مدير للمنصة، أريد تقرير Phase 0 يوضح حالة Modules المنصة والـ specs المرتبطة بها، حتى لا يبدأ تنفيذ Phase 1 فوق عمل موجود أو متعارض.

**Why this priority**: هذا هو الغرض الأساسي من Phase 0، وبدونه قد يتم تكرار Features موجودة أو كسر تدفقات الشراء والصلاحيات.

**Independent Test**: يمكن اختباره بفتح تقرير Phase 0 في `docs/` والتأكد أنه يغطي كل بنود roadmap الأساسية ويربطها بحالة واضحة: موجود، ناقص، متعارض، يحتاج Spec، أو يحتاج فحص إضافي.

**Acceptance Scenarios**:

1. **Given** وجود `docs/platform-change-roadmap.md` وملفات `specs/`، **When** يراجع المدير تقرير Phase 0، **Then** يجد ملخصا واضحا لكل Phase وبند رئيسي مع الحالة والمخاطر والـ specs المرتبطة.
2. **Given** بند له spec موجود مثل تطبيق ولي الأمر أو المرتبات، **When** يظهر في التقرير، **Then** يتم ربطه بالـ spec الحالي بدلا من اقتراح إعادة بنائه من الصفر، لكن لا يصنف `Complete` إلا إذا ظهر دليل تنفيذ مثل كود أو واجهة أو اختبارات أو أثر واضح.

---

### User Story 2 - المطور يعرف أثر كل بند قبل تحويله إلى Spec (Priority: P2)

كمطور سيكمل باقي المراحل، أريد أن يحدد التقرير نوع أثر كل بند: بيانات، صلاحيات، دفع/فلوس، واجهة، Worker/Event، أو توثيق فقط، حتى تكون specs التالية دقيقة وقابلة للتنفيذ.

**Why this priority**: كثير من البنود تمس الشراء والحسابات والصلاحيات، وتصنيف الأثر يقلل أخطاء التخطيط والتنفيذ.

**Independent Test**: يمكن اختيار أي بند من التقرير والتحقق أن له تصنيف أثر واحد أو أكثر، ومعه سبب مختصر أو ملاحظة خطر عند الحاجة.

**Acceptance Scenarios**:

1. **Given** بند مثل أكواد الخصم أو حسابات المدرسين، **When** يقرأ المطور التقرير، **Then** يرى أنه عالي المخاطر لأنه يمس الدفع والبيانات والصلاحيات.
2. **Given** بند مثل فيديو تعريفي للمدرس، **When** يقرأ المطور التقرير، **Then** يرى أنه غالبا واجهة وبيانات خفيفة ما لم يرتبط بصلاحيات أو مزود فيديو جديد.

---

### User Story 3 - صاحب القرار يرى أولويات التنفيذ التالية (Priority: P3)

كصاحب قرار، أريد أن ينتهي Phase 0 بترتيب مقترح لما يجب تحويله إلى specs تنفيذية لاحقا، حتى تكون المراحل التالية عملية ومبنية على الاعتماديات الحقيقية.

**Why this priority**: التقرير ليس مجرد جرد؛ يجب أن ينتج عنه قرار واضح عن أول feature تنفيذية وما يجب تأجيله.

**Independent Test**: يمكن مراجعة قسم "Next specs / recommended sequence" في التقرير والتأكد أنه يذكر أولويات واضحة ومبررة.

**Acceptance Scenarios**:

1. **Given** أن التقرير اكتمل، **When** يراجع صاحب القرار قسم التوصيات، **Then** يجد قائمة مرتبة بأول specs التنفيذية المقترحة مع سبب الترتيب.
2. **Given** بند مؤجل مثل الإعلانات أو اللايف، **When** يظهر في التقرير، **Then** يوضح التقرير سبب التأجيل والاعتماديات المطلوبة قبله.

---

### Edge Cases

- إذا كان spec موجودا لكنه ما زال قالبا عاما أو ناقصا، يجب تمييزه كـ `Spec exists but incomplete` وليس `Complete`.
- إذا كان spec موجودا ومكتوبا جيدا لكن لا يوجد دليل تنفيذ، يجب تمييزه كـ `Spec ready / implementation not verified` أو `Partial` وليس `Complete`.
- إذا لم يظهر module في الكود أو في specs، يجب تمييزه كـ `Missing / needs new spec`.
- إذا تعارضت وثائق roadmap مع الكود الحالي، يجب تسجيل التعارض كملاحظة خطر لا كحقيقة محسومة.
- إذا كان البند يمس بيانات مالية أو صلاحيات أو شراء، يجب تصنيفه High Risk حتى لو لم يتم تحديد التنفيذ بعد.
- إذا لم يمكن إثبات حالة بند من القراءة السريعة، يجب تسجيل `Needs deeper inspection` بدل التخمين.
- إذا تعذر تشغيل تجربة يدوية أو أخذ screenshots بسبب بيانات أو credentials أو وقت، يجب تسجيل Manual QA كـ `pending` أو `blocked` مع السبب بدلا من إسقاط البند.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin/Product owner يفتح تقرير Phase 0 في `docs/` ويراجع أن البنود الكبرى من `docs/platform-change-roadmap.md` موجودة ومصنفة.
- **Manual QA Negative Check**: التأكد أن Phase 0 لم تضف endpoints أو migrations أو UI production جديدة، وأن التقرير لا يدعي اكتمال بند بلا دليل.
- **Docker Acceptance**: تشغيل `docker compose config -q` كحد أدنى لإثبات أن التعديلات التوثيقية لم تلمس إعدادات Docker. لا يلزم `make migrate` لأن Phase 0 لا يغير schema.
- **External Dependencies**: لا توجد APIs أو بوابات دفع أو أجهزة خارجية مطلوبة للتحقق من Phase 0.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a Phase 0 audit report under `docs/` that is separate from the roadmap and can be reviewed independently.
- **FR-002**: The report MUST cover the roadmap phases and major items from `docs/platform-change-roadmap.md`.
- **FR-002a**: The report MUST cover every phase and every major subsection, and it MUST expand child checklist items when they affect data, payment/finance, permissions, dependencies, or delivery risk.
- **FR-003**: The report MUST map each reviewed item to existing specs when a likely matching spec exists.
- **FR-004**: The report MUST classify each reviewed item by impact area: data, permissions, payment/finance, UI, worker/event, documentation-only, or needs-new-spec.
- **FR-005**: The report MUST identify each reviewed item status as one of: complete, partial, missing, conflicting, spec-incomplete, spec-ready-implementation-not-verified, or needs-deeper-inspection.
- **FR-005a**: The report MUST define `Complete` as requiring implemented evidence such as production code, a user-facing/admin-facing surface, automated tests, or another concrete artifact; a spec alone MUST NOT be enough for `Complete`.
- **FR-005b**: The report MUST record manual QA as `passed`, `failed`, `blocked`, or `pending`; missing manual QA MUST NOT prevent implemented evidence from being recorded, but it MUST remain visible as pending evidence.
- **FR-006**: The report MUST call out high-risk items touching payment, teacher revenue, access grants, coupons, permissions, parent data, or audit logs.
- **FR-007**: The report MUST identify duplicate or overlapping roadmap items that should reuse existing specs instead of being rebuilt.
- **FR-008**: The report MUST include a recommended next-spec sequence after Phase 0.
- **FR-009**: The report MUST include explicit out-of-scope notes stating that Phase 0 does not implement product code, migrations, endpoints, or UI features.
- **FR-010**: The report MUST include verification notes listing which repository areas were inspected and what remains uncertain.
- **FR-011**: The feature MUST preserve existing product behavior; any production-code change is out of scope unless separately approved by the user.
- **FR-012**: The feature MUST update Spec Kit progress artifacts for this SDD run without silently broadening scope beyond Phase 0 Audit.
- **FR-013**: The report MUST use one Markdown document with these sections at minimum: Executive Summary, Audit Matrix, High-Risk Items, Conflicts/Overlaps, Recommended Next Specs, Manual QA Status, and Verification Notes.
- **FR-014**: Each audited row MUST include an evidence note referencing a file path, spec path, UI surface, test, or explicit `not found / pending` observation.

### Key Entities *(include if feature involves data)*

- **Roadmap Item**: A requested platform change from `docs/platform-change-roadmap.md`, including phase, description, status, impact areas, risk level, and related specs.
- **Existing Spec Reference**: A mapping to a directory under `specs/` that appears to cover or partially cover a roadmap item.
- **Audit Finding**: A documented observation about completion, missing scope, conflict, dependency, or risk.
- **Recommended Next Spec**: A proposed future implementation specification with ordering rationale.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of roadmap phases from Phase 0 through Phase 6 are represented in the audit report.
- **SC-002**: At least 90% of major roadmap items are mapped to an existing spec or explicitly marked as needing a new spec.
- **SC-003**: All payment, access, permissions, and teacher-finance items are marked with high-risk notes and required evidence before implementation.
- **SC-004**: A reviewer can identify the recommended first three post-audit implementation specs in under 5 minutes by reading the report.
- **SC-005**: Final verification confirms no production code, database migration, or runtime configuration was intentionally changed by Phase 0.
- **SC-006**: No reviewed item is marked `Complete` unless the report cites implemented evidence; manual QA that was not executed is visibly marked `pending`.
- **SC-007**: The report contains the required decision sections: Executive Summary, Audit Matrix, High-Risk Items, Conflicts/Overlaps, Recommended Next Specs, Manual QA Status, and Verification Notes.

## Assumptions

- Phase 0 is a discovery and documentation feature; product behavior changes are out of scope.
- Repository context, existing specs, and lightweight source inspection are sufficient to classify the first audit pass.
- Items that cannot be confidently classified will be marked as needing deeper inspection rather than guessed.
- The existing roadmap in `docs/platform-change-roadmap.md` remains the source of truth for requested phase ordering during this audit.
- Screenshot evidence is useful but not required for Phase 0 completion; unavailable manual evidence will be recorded as `pending` or `blocked`.
