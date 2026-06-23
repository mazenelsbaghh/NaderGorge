# Feature Specification: Live Support AI Refinements and Performance Dashboard

**Feature Branch**: `144-ai-live-support-refinements`  
**Created**: 2026-06-23  
**Status**: Draft  
**Input**: User description: "تضيف زرار التشغيل و وتعمل تابه علشان اشوف هو عامل اد اي حل كام مشكله بعت كام مسدج و كل ده ونزعم كمان الفتره الزمنيه وحل مشكلة النشر 500"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - AI Enable/Disable & Publish Fix (Priority: P1)

As an administrator, I want to publish my draft policies without encountering 500 database unique key violations, and I want to toggle the current AI assistant status (Play/Stop) directly on the active policy without having to publish a new draft.

**Why this priority**: Crucial for system reliability and control. If publishing fails or the AI cannot be toggled on/off, the entire AI support system is unusable.

**Independent Test**: Admin can publish a policy, toggle it off (Stop), and then toggle it back on (Play) without creating new drafts, and all operations succeed without database constraint errors.

**Acceptance Scenarios**:

1. **Given** a published and active policy exists, **When** the admin publishes a new policy version, **Then** the database updates succeed without unique constraint violations on `IsEnabled`, marking the old one superseded (and disabled) and the new one published (and enabled).
2. **Given** a published policy exists in "متوقف" (Disabled) state, **When** the admin clicks the "تفعيل" (Play) button, **Then** the backend sets `IsEnabled = true` on the published policy and returns success.
3. **Given** a published policy exists in "مفعّل" (Enabled) state, **When** the admin clicks the "إيقاف وتحويل" (Stop) button, **Then** the backend sets `IsEnabled = false` on the published policy and returns success.

---

### User Story 2 - Visual Activity Indicator (Priority: P2)

As an administrator, I want a clear, responsive visual indicator showing whether the Live Support AI is currently running and monitoring incoming conversations.

**Why this priority**: Essential for immediate operational awareness. Admin needs to know instantly if the AI is actively processing user queries.

**Independent Test**: Admin loads the page and immediately notices a green pulsating badge when the AI is active, or a gray static badge when it is stopped.

**Acceptance Scenarios**:

1. **Given** the AI is enabled (`IsEnabled = true` on the published policy), **When** the page loads, **Then** the status card displays a green badge with a pulsating/glowing micro-animation saying "نشط حالياً".
2. **Given** the AI is disabled (`IsEnabled = false` on the published policy or no policy published), **When** the page loads, **Then** the status card displays a gray badge saying "متوقف".

---

### User Story 3 - AI Performance Dashboard (Priority: P3)

As an administrator, I want to view a tabbed interface showing metrics of the AI's performance (active conversations, resolved issues, handoffs, message counts, executed actions) with quick preset time filters (Last 24 Hours, Last 7 Days, Last 30 Days, Lifetime).

**Why this priority**: Provides business visibility into AI effectiveness, helping the system owner measure the return on investment and identify tuning needs.

**Independent Test**: Admin clicks the "الإحصائيات والأداء" tab, changes the filter from "آخر 24 ساعة" to "آخر 7 أيام", and verifies that the counts update instantly.

**Acceptance Scenarios**:

1. **Given** the admin is on the settings page, **When** they click the "الإحصائيات والأداء" (Statistics & Performance) tab, **Then** a grid of performance metrics is displayed:
   - **المحادثات النشطة حالياً** (Currently Active Conversations with AI)
   - **المشاكل المحلولة بواسطة الذكاء الاصطناعي** (Issues Resolved by AI)
   - **التحويلات إلى الدعم البشري** (Handoffs to Human Staff)
   - **إجمالي الرسائل المرسلة من الذكاء الاصطناعي** (Total Messages Sent by AI)
   - **الإجراءات التلقائية الناجحة** (Successful Automated Actions Executed)
2. **Given** the statistics tab is active, **When** the admin selects a time preset (e.g. "آخر 7 أيام"), **Then** the statistics are refreshed by querying the backend API with the selected time period parameter.

---

### Edge Cases

- **No Published Policy**: If there is no published policy in the database, the play/stop toggles must be disabled, and the status must display "لم يتم نشر أي سياسة بعد".
- **Zero Statistics**: If a time period has no activity, all counters should display `0` cleanly instead of failing or displaying empty blanks.
- **Rapid Clicks**: If the admin clicks the Enable/Disable toggle repeatedly, UI loading state should prevent multiple concurrent requests.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Config Flow**: Log in as an Admin, go to `/admin/live-support/ai`. Publish a policy, verify no 500 error. Disable it, verify it switches to "متوقف". Enable it, verify it switches to "نشط حالياً" with the green pulse indicator.
- **Manual QA Stats Check**: Navigate to the statistics tab, verify all 5 counters display values. Switch between time presets and confirm the data updates.
- **Docker Acceptance**: Verify that compiling the backend and rebuilding the frontend containers works, and that migration tests succeed.
- **External Dependencies**: Requires connection to PostgreSQL to fetch data from live-support tables.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Backend MUST resolve the duplicate key constraint violation on `IX_live_support_ai_policy_versions_IsEnabled` by sequentially calling `SaveChangesAsync` after disabling the old policy and before enabling the new draft policy.
- **FR-002**: Backend MUST provide an API endpoint `POST /api/live-support/admin/ai/enable` to enable the currently published AI policy version.
- **FR-003**: Backend MUST provide an API endpoint `GET /api/live-support/admin/ai/stats` accepting a time period filter (e.g. `last-24h`, `last-7d`, `last-30d`, `lifetime`) and returning aggregated metrics.
- **FR-004**: Frontend MUST implement a tabbed navigation interface containing two tabs: "الإعدادات وقاعدة القرار" (Settings & Policy) and "الإحصائيات والأداء" (Statistics & Performance).
- **FR-005**: Frontend MUST show a glowing/pulsating green indicator badge when the AI status is active.
- **FR-006**: Frontend MUST provide a button to Enable the assistant when it is stopped, and a button to Disable it when it is running.

### Key Entities *(include if feature involves data)*

- **LiveSupportAIConversationState**: Stores the current AI conversation state and mode (Active, Resolved, HandedOff). Used to count active conversations, resolved conversations, and handoffs.
- **LiveSupportMessage**: Stores all conversation messages. Used to count messages sent by `SenderType == AI`.
- **LiveSupportAIPendingAction**: Stores actions proposed by the AI. Used to count successful execution status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin can toggle the AI status (Enable/Disable) and see the UI update in under 1 second.
- **SC-002**: Statistics load and filter in under 1.5 seconds.
- **SC-003**: 100% of policy publish attempts succeed without throwing unique constraint exceptions.

## Assumptions

- **Timezone**: All statistics queries will run using database UTC timestamps.
- **No new tables**: No database schema modifications or migrations are required. The existing schemas contain all the fields needed to satisfy the requirements.
