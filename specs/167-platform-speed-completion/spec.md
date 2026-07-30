# Feature Specification: Platform Speed Completion

**Feature Branch**: `167-platform-speed-completion`  
**Created**: 2026-07-29  
**Status**: Draft  
**Input**: User-approved request to implement every finding in the platform speed, navigation, UI, backend, and production audit; include all current workspace changes; verify them; and deploy progressively to all three production nodes without downtime.

## Clarifications

### Session 2026-07-29

- Q: ما سياسة الرجوع بعد تطبيق migrations؟ → A: rollback تلقائي لصور التطبيق فقط؛ تظل قاعدة البيانات على النسخة الجديدة المتوافقة، وتُعالج مشكلات المخطط بـforward-fix دون down migration أو restore تلقائي.
- Q: متى تتوقف إضافة تغييرات الـworkspace إلى الإصدار؟ → A: تُضم كل التغييرات الموجودة أو التي تظهر أثناء التنفيذ. يُختم snapshot تقنيًا فقط عند إنشاء مرشح الإصدار؛ وأي تغيير يظهر بعد الختم يبطل المرشح، ويفرض إنشاء artifacts جديدة وإعادة جميع بوابات التحقق قبل النشر.
- Q: هل يلزم انتظار نافذة ثابتة أو عدد محدد من مشاهدات RUM قبل النشر؟ → A: لا؛ تعتمد أهلية النشر على بوابات الأداء والصحة الفورية المتفق عليها. تستمر مراقبة RUM بعد النشر مع إظهار حجم العينة، ولا يُدّعى نجاح إحصائي قبل كفايتها.
- Q: هل يجوز تنزيل حزم أو أدوات أو صور على جهاز التطوير المحلي؟ → A: لا؛ يُستخدم الموجود محليًا فقط. أي dependency أو image غير موجود يُتحقق/يُبنى في بيئة الباني أو الإنتاج البعيدة، أو يُسجّل كحاجز محلي، دون تنزيله على جهاز المستخدم.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fast, Persistent Navigation (Priority: P1)

As an authenticated user on any platform surface, I can move between the pages I use every day without the surrounding application shell restarting, losing its state, jumping my scroll position unexpectedly, or making me wait for avoidable reloads.

**Why this priority**: Navigation is repeated throughout every session and currently amplifies loading, rendering, and data-request costs across all roles.

**Independent Test**: Navigate repeatedly between two frequently used pages on each protected surface and verify that the shell identity and state persist, the destination is prepared before or at intent, duplicate data requests do not occur, and the target content becomes usable within the navigation target.

**Acceptance Scenarios**:

1. **Given** a signed-in user with a collapsed or expanded navigation group, **When** the user opens another page on the same surface, **Then** the shell remains mounted and retains its navigation state.
2. **Given** a user has scrolled a long internal page, **When** the user leaves and returns using browser history, **Then** the applicable scroll container restores a sensible prior position without shifting the shell.
3. **Given** a user focuses, hovers, or is likely to select a high-frequency destination, **When** the destination is safe to prepare, **Then** the system prepares only the resources needed to reduce click-to-content delay.
4. **Given** a protected deep link, **When** the session requires authentication, **Then** the user is sent to sign in and returns to the validated original destination after success.

---

### User Story 2 - Responsive Entry and Student Experience (Priority: P1)

As a visitor or student, I can load, type into, and submit login, registration, and student-dashboard screens smoothly on a typical mobile device without continuous decorative work competing with my input or hidden assets being prioritized.

**Why this priority**: Cloud measurements show interaction responsiveness is the weakest platform indicator, and entry/student routes affect the largest and most device-constrained audience.

**Independent Test**: Use login, registration, and student dashboard on representative mobile and desktop profiles; verify responsive input, stable loading, one session transition, no duplicated shell, no continuous heavy decoration under reduced-motion or constrained-device conditions, and measured targets.

**Acceptance Scenarios**:

1. **Given** a visitor on a typical mobile device, **When** the visitor types in registration fields, **Then** decorative visuals do not cause a long interaction task or visible input delay.
2. **Given** a returning authenticated student, **When** the student opens the student home, **Then** useful content or a matching content skeleton appears without rendering a second shell.
3. **Given** a user selects light or dark mode, **When** the authentication screen loads, **Then** a single appropriate brand asset is prioritized and no alternate hidden asset delays the largest visible content.
4. **Given** the user prefers reduced motion, **When** any entry or student screen appears, **Then** nonessential continuous and scripted motion is disabled.

---

### User Story 3 - Efficient Data-Heavy Workflows (Priority: P1)

As an administrator, teacher, assistant, employee, or support operator, I can search, filter, open, and switch between large records without downloading far more rows than I can see, issuing a request for every keystroke, or waiting for repeated per-record data lookups.

**Why this priority**: Large lists and support dashboards can multiply network, memory, and database work as data grows, directly affecting operational users.

**Independent Test**: Exercise student search, large administration tabs, and live-support history/dashboard flows with representative data volumes; verify bounded page sizes, cancellation of obsolete searches, retained prior results, bounded query counts, and target response times.

**Acceptance Scenarios**:

1. **Given** a large student dataset, **When** an administrator types a search term quickly, **Then** obsolete requests are cancelled or suppressed and only a bounded result page is returned.
2. **Given** an operator opens a support history containing many conversations, **When** the history is returned, **Then** the number of data-store operations remains bounded rather than increasing linearly per displayed record.
3. **Given** a user revisits recently loaded data that is still fresh, **When** the user returns to the screen, **Then** the user sees retained data immediately and the system does not issue an identical duplicate request.
4. **Given** a real-time event changes relevant data, **When** the event is received, **Then** only the affected cached data is updated or invalidated.

---

### User Story 4 - Stable, Accessible Screens and Motion (Priority: P2)

As a keyboard, assistive-technology, mobile, or motion-sensitive user, I can understand loading, errors, dialogs, drawers, and carousels; pause changing content; keep focus in the active interaction; and use controls that perform the action they advertise.

**Why this priority**: Accessibility and stable feedback are required for reliable task completion and prevent visual polish from reducing usability or responsiveness.

**Independent Test**: Run keyboard and automated accessibility journeys through navigation drawers, dialogs, loading, error, empty, and carousel states at mobile and desktop widths, with reduced motion enabled and disabled.

**Acceptance Scenarios**:

1. **Given** a mobile drawer or modal is open, **When** the user presses Tab, Shift+Tab, or Escape, **Then** focus remains inside until close, the background is inactive, and focus returns to the trigger.
2. **Given** content is loading or fails, **When** the state appears, **Then** assistive technology receives a concise status, focus moves only when necessary, and internal error details are not exposed.
3. **Given** content changes automatically, **When** it remains visible beyond the accepted interval, **Then** the user can pause it and it pauses on focus or when reduced motion is preferred.
4. **Given** a visible previous/next control, **When** the user activates it, **Then** the advertised content change occurs and the current item is conveyed accessibly.

---

### User Story 5 - Trustworthy Performance Evidence (Priority: P1)

As the product and operations owner, I can distinguish performance by route, role surface, device class, and connection quality; correlate a slow user experience with server and data-store work; and prevent a release from exceeding agreed budgets.

**Why this priority**: Current aggregate measurements are biased by traffic mix, and existing load evidence does not exercise authenticated workflows or meaningful real-time load.

**Independent Test**: Produce a pre-release and post-release evidence set containing route-level user metrics, workflow response distributions, resource budgets, real-time load, node distribution, and correlated slow-request evidence without storing secrets.

**Acceptance Scenarios**:

1. **Given** real user measurements, **When** the owner filters by route, surface, device, or connection class, **Then** the key responsiveness and stability percentiles are available separately.
2. **Given** a slow interaction or request, **When** an operator follows its safe correlation identifier, **Then** the relevant application and data-store timing can be identified without exposing credentials or personal content.
3. **Given** a candidate release exceeds a route resource budget or workflow threshold, **When** verification runs, **Then** the release gate fails before production deployment.
4. **Given** a three-node load run, **When** authenticated workflows and real-time connections execute, **Then** throughput, error rate, percentiles, node shares, dropped work, and reconnect behavior are recorded.

---

### User Story 6 - Zero-Downtime Complete Release (Priority: P1)

As the operations owner, I can publish the complete verified workspace state, including pre-existing changes, progressively across the three production nodes without planned downtime and with a deterministic stop and rollback path.

**Why this priority**: The user explicitly requires every current change to ship together, so release completeness, data safety, and rollback evidence are part of feature correctness.

**Independent Test**: Build the exact source state, prove migration and release identity, drain and update one node at a time, run health and smoke gates before proceeding, verify all nodes converge on one release, and exercise the documented rollback stop condition.

**Acceptance Scenarios**:

1. **Given** any required build, test, migration, security, or acceptance gate fails, **When** release orchestration evaluates the candidate, **Then** production deployment does not begin.
2. **Given** a healthy candidate, **When** rolling deployment begins, **Then** each node is drained, updated, verified, and returned before the next node is changed.
3. **Given** a node fails health or smoke checks after update, **When** the failure threshold is reached, **Then** rollout stops and the prior verified application release is restored while the backward-compatible database schema remains at its applied version.
4. **Given** deployment succeeds, **When** final acceptance runs, **Then** all three nodes report the same release identity, remain healthy, share traffic, and preserve database, queue, real-time, and file-service correctness.
5. **Given** the workspace contains changes created outside this feature run, **When** the release source is sealed, **Then** those changes are included in the reviewed manifest and are not silently omitted or reset.
6. **Given** another workspace change appears after a candidate has been sealed, **When** release completeness is evaluated, **Then** the candidate is invalidated, the new change is included, and the complete artifact build and verification gates run again before production deployment.

### Edge Cases

- A route is rarely used or carries a very large payload, so eager preparation would waste bandwidth; preparation must remain selective and measurable.
- A cached response becomes unauthorized or stale after a permission, password, security-version, or content-state change; invalidation must preserve the current security boundary.
- A user navigates rapidly while searches, dashboard reads, or imports are still in flight; obsolete work must not overwrite newer state.
- A real-time connection drops or reconnects during a cache update; the client must recover authoritative state without a full-page reload or duplicate visible action.
- A device has reduced motion enabled, low processing capability, a slow connection, data-saving preference, or a backgrounded tab.
- A table, translated label, long Arabic name, or error message expands beyond the expected size at mobile or enlarged-text widths.
- A background event fails after being claimed but before acknowledgement; it must be retried without loss, unauthorized broadcast, or duplicate business mutation.
- A security-state cache is stale when an account is disabled or credentials change; the revocation path must invalidate it immediately.
- A migration is valid on an empty database but conflicts with the current production schema or mixed-version rolling window.
- A post-migration application gate fails after new writes have started; application artifacts roll back automatically, the compatible schema remains applied, and remediation proceeds through a forward fix without automatic down migration or database restore.
- A node, tunnel connector, database writer, Redis master, shared-file mount, or real-time backplane becomes unhealthy during rollout.
- The candidate contains a pre-existing change that fails review or tests; it remains in scope to fix and cannot be bypassed merely because another author created it.
- A workspace change appears at any time before production publication; it remains in scope, and any already sealed release candidate must be rebuilt and requalified from the new complete snapshot.
- Performance measurements lack enough eligible samples after rollout; no success claim may be made until the minimum observation evidence is available.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Visitor/Student**: On landing, login, registration, and student surfaces, verify responsive typing, theme, deep-link return, dashboard loading, packages navigation, history navigation, and reduced motion.
- **Manual QA Admin/Teacher/Assistant/Employee**: Verify persistent shells, high-frequency navigation, permissions, search cancellation, paginated lists, tab loading, dialogs, drawers, and error recovery.
- **Manual QA Live Support**: Verify queue, conversation, student history, timeline, AI/recovery states, reconnect, and targeted real-time refresh with representative history volume.
- **Manual QA Accessibility**: Keyboard-only navigation, focus visibility, skip navigation, drawer/dialog focus containment, status announcements, carousel pause, 200% text, mobile overflow, light/dark contrast, and reduced motion.
- **Manual QA Negative Check**: Verify denied routes and actions remain denied after navigation-policy unification and cache introduction.
- **Docker Acceptance**: Build exact frontend/backend/worker images; validate compose; run migrations against isolated empty and production-like databases; start the complete stack; verify readiness, static cache headers, surfaces, API, queues, real-time connections, and shared assets.
- **Production Acceptance**: Preflight all three nodes, seal source/release identity, deploy one node at a time, verify health and smoke between nodes, verify balanced traffic and unified release identity, and capture rollback-ready evidence.
- **External Dependencies**: Production cluster access, PostgreSQL high availability, Redis/Sentinel, shared file storage, Cloudflare Tunnel credentials, external asset/video services, and representative test accounts must be available; unavailable dependencies block final production acceptance rather than being silently skipped.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST keep each protected surface shell persistent across navigation within that surface.
- **FR-002**: The system MUST retain relevant navigation state and restore applicable history scroll without remounting the entire application shell.
- **FR-003**: The system MUST prepare high-frequency destinations selectively and MUST avoid unconditional preparation of rare or expensive destinations.
- **FR-004**: The system MUST preserve validated deep-link destinations through authentication and MUST avoid a full document reload for same-origin transitions.
- **FR-005**: The system MUST avoid loading public-navigation behavior and resources on protected surfaces that do not render that navigation.
- **FR-006**: The system MUST divide large interactive screens into independently loadable regions without changing their visible business behavior.
- **FR-007**: The system MUST deduplicate identical in-flight reads, retain fresh data across navigation, cancel obsolete reads, and invalidate only data affected by a completed mutation or real-time event.
- **FR-008**: The system MUST provide bounded server-side pagination for large record collections and MUST debounce user-driven search.
- **FR-009**: The system MUST prevent obsolete search or navigation responses from replacing newer results.
- **FR-010**: The system MUST render one appropriate priority brand asset for the active visual theme and MUST not prioritize hidden theme alternatives.
- **FR-011**: The system MUST avoid continuously running nonessential high-cost decoration on data-entry screens and constrained devices.
- **FR-012**: The system MUST honor user motion preferences for scripted and style-driven motion across all surfaces.
- **FR-013**: Automatically changing content MUST provide pause control and MUST pause on focus, hover where applicable, hidden-page state, and reduced-motion preference.
- **FR-014**: Visible interactive controls MUST perform their advertised action and expose state, label, and current-item information accessibly.
- **FR-015**: Drawers and dialogs MUST contain focus, deactivate background interaction, close by Escape when safe, and restore focus to their trigger.
- **FR-016**: Loading, empty, error, and retry states MUST be matched to their content region, announced accessibly, and MUST not expose internal exception details.
- **FR-017**: The system MUST provide a keyboard skip path to primary content and MUST maintain logical focus after client navigation.
- **FR-018**: Responsive screens MUST preserve all critical actions at mobile widths, 200% text scaling, long Arabic content, and both visual themes.
- **FR-019**: Public and versioned static assets MUST receive explicit cache behavior appropriate to their mutability, and release verification MUST confirm effective edge and browser headers.
- **FR-020**: Authenticated request validation MUST preserve immediate account and credential revocation while avoiding an avoidable authoritative-store read on every eligible request.
- **FR-021**: Live-support list, history, dashboard, and timeline reads MUST use bounded data-store operations independent of the number of displayed records.
- **FR-022**: Background event processing MUST separate claim ownership, delivery, retry, and acknowledgement so a slow destination does not hold long authoritative-store locks.
- **FR-023**: Background events MUST remain lossless and idempotent across node concurrency, reconnects, retries, and process termination.
- **FR-024**: Real-user performance evidence MUST be segmentable by route, surface, device class, and connection class while minimizing personal data.
- **FR-025**: Slow user, application, and data-store operations MUST be traceable through safe correlation identifiers and percentile dashboards.
- **FR-026**: Release verification MUST enforce separate resource budgets for initial, shared, and deferred route resources using effective transfer sizes.
- **FR-027**: Load verification MUST exercise authenticated workflows, database-heavy reads, writes, real-time connections, reconnection, and node distribution rather than health endpoints alone.
- **FR-028**: Navigation display and route enforcement MUST use one authoritative permission policy and MUST not broaden any role's current effective access.
- **FR-029**: All workspace changes present before production publication, including changes that appear during implementation or verification, MUST be inventoried, reviewed, tested, and included; no workspace change may be reset or silently excluded.
- **FR-030**: Any defect in an in-scope pre-existing change that blocks verification MUST be fixed and verified as part of this feature.
- **FR-030a**: Sealing a release candidate MUST freeze an exact source snapshot only for reproducible build and verification; any subsequent workspace change MUST invalidate that candidate and trigger a new snapshot, artifact build, and complete verification cycle.
- **FR-031**: Database changes MUST be forward-compatible with mixed-version rolling operation and the prior verified application release, non-destructive, and verified against empty and production-like schemas.
- **FR-032**: The release MUST deploy progressively across three nodes without planned downtime and MUST require health and smoke success before advancing.
- **FR-033**: The rollout MUST stop and restore the prior verified application release when a defined critical gate fails; an applied compatible database migration MUST remain in place and any schema remediation MUST be a forward fix.
- **FR-034**: Final production acceptance MUST verify one release identity, three healthy application nodes, balanced request distribution, one logical database writer, healthy shared services, and intact critical workflows.
- **FR-035**: Performance improvements MUST include before-and-after evidence; unmeasured or statistically insufficient results MUST be reported as pending rather than successful.
- **FR-036**: Logging and measurements MUST exclude access tokens, credentials, private message content, and other sensitive payloads.

### Key Entities

- **Performance Observation**: A privacy-minimized measurement of a user-visible metric associated with route, surface, device class, connection class, release identity, rating, and capture time.
- **Performance Budget**: An accepted threshold for a route or workflow, including metric, percentile or resource class, limit, and blocking policy.
- **Navigation State**: User interface state that legitimately persists within a surface, such as expanded groups and restorable internal scroll position, without persisting sensitive page data.
- **Cached Query Record**: A client-visible result identified by an authoritative key, freshness window, invalidation scope, and current request state.
- **Background Event Claim**: Durable ownership and retry state for an event awaiting external or real-time delivery, separate from business-state mutation.
- **Release Candidate Manifest**: The exact source and image identities, included workspace changes, migrations, verification results, and deployment eligibility.
- **Deployment Evidence**: Per-node drain, update, health, smoke, traffic, release-identity, failure, and rollback observations for the rolling release.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 75% of eligible mobile page loads show the largest primary content within 2.0 seconds after the post-release observation sample is sufficient.
- **SC-002**: At least 75% of eligible mobile interactions produce the next visual response within 200 milliseconds.
- **SC-003**: At least 75% of eligible page views maintain a layout-shift score below 0.1.
- **SC-004**: At least 75% of warm, same-surface navigations make destination content usable within 300 milliseconds.
- **SC-005**: After successful authentication, at least 75% of same-origin transitions to the applicable dashboard become usable within 1.5 seconds without a full document reload.
- **SC-006**: Routine read workflows complete within 250 milliseconds at the 95th percentile, and designated data-heavy workflows complete within 500 milliseconds at the 95th percentile under the accepted workload.
- **SC-007**: Normal navigation produces zero identical duplicate reads within the applicable freshness window.
- **SC-008**: Rapid search produces at most one current active request per search control and returns no more than the accepted bounded page size.
- **SC-009**: Initial transferred route resources for login, registration, and student home are reduced by at least 25% from the sealed pre-change baseline without removing user functionality.
- **SC-010**: Live-support dashboard and history data-store command counts remain within the accepted fixed budget as displayed record count grows.
- **SC-011**: Automated accessibility checks report zero critical violations on the selected core route and interaction matrix, and every manual keyboard journey is completable.
- **SC-012**: A 30-minute accepted workload completes with no unexpected errors or dropped iterations, records usable 95th and 99th percentiles, and distributes eligible requests across all three healthy nodes within the accepted balance tolerance.
- **SC-013**: The rolling production deployment completes with no planned user-visible downtime and all three nodes converge on the exact verified release identity.
- **SC-014**: A simulated or real post-update critical health failure stops advancement and produces successful rollback evidence without data loss.
- **SC-015**: Every workspace change present immediately before production publication is represented in the final sealed candidate manifest and passes the applicable code, test, documentation, build, migration, and runtime gates.
- **SC-016**: No performance log, metric, trace, build artifact, or deployment evidence contains credentials, access tokens, private messages, or secret values.

## Assumptions

- The approved audit at `docs/platform-speed-navigation-ui-audit-2026-07-29.md` is the authoritative finding inventory for this feature.
- The current authorization, financial, entitlement, assessment, HR, and support business rules remain authoritative unless an existing change already intentionally modifies them and its specification confirms that change.
- All tracked and untracked workspace changes present before production publication, including changes that appear during implementation or verification, are intentionally in scope for review, repair, verification, and release.
- Candidate sealing is a reproducibility boundary, not a scope exclusion: later changes require resealing, rebuilding, and rerunning the complete gate set.
- The existing three-node production cluster, rolling deployment, release sealing, migration gates, and rollback tooling remain the operational foundation.
- No destructive database migration, data deletion, automatic down migration, or automatic production database restore is authorized; application rollback keeps the forward-compatible schema in place.
- Performance success requires a sealed baseline and enough eligible post-release observations; production rollout success can be complete while long-window performance outcomes remain in monitored validation.
- No fixed RUM duration or sample-count threshold blocks deployment; immediate synthetic, workflow, health, and resource-budget gates determine release eligibility, while post-release RUM remains explicitly sample-qualified.
- Cloudflare's attached 24-hour report establishes aggregate LCP/INP/CLS evidence, but the secondary-results page is excluded from prioritization and the report is not treated as a route-balanced platform sample.
- External provider incidents and infrastructure resource steal are reported separately and cannot be represented as application-code improvements.
- Local implementation and verification MUST NOT download packages, browser binaries, container images, SDKs, or tools onto the user's device; remote release/build environments remain responsible for any required network retrieval under their reviewed controls.
