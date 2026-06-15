# 127 — Package Subscribers Table with Export

## 1. Overview / نظرة عامة

Add a **"الطلاب المشتركين" (Enrolled Students)** tab to the admin Package Profile page that displays a paginated, searchable table of all students who have an active `StudentAccessGrant` for the package. Include a **CSV download** button that exports all subscriber details.

The same pattern should also be available on the **Term Profile** and **Section Profile** pages, filtering by the relevant `GrantType`.

---

## 2. User Stories / قصص المستخدم

### US-01: Admin views subscribers table on Package Profile
**As** an admin  
**I want** to see a table of all students subscribed to a specific package  
**So that** I can quickly review enrollment data without leaving the content management area.

**Acceptance Criteria:**
- A new tab "الطلاب المشتركين" appears in the Package Profile page alongside existing tabs (نظرة عامة, الأترام, صفحة الأكواد).
- The table displays: Student Name, Phone, Governorate, Education Stage, Grade Level, Enrolled Date, and Status (active/cancelled).
- The table supports client-side search by name or phone.
- The table paginates at 10 rows per page.
- Clicking a student row navigates to the admin student profile page.

### US-02: Admin downloads subscribers as CSV
**As** an admin  
**I want** to download a CSV file of all subscribers to a package  
**So that** I can share the data with team members or import it elsewhere.

**Acceptance Criteria:**
- A "تنزيل CSV" button appears above the table.
- The CSV includes: Full Name, Phone, Governorate, District, Education Stage, Grade Level, School Name, Parent Phone, Mother Phone, Enrolled Date, Status.
- The file name follows the pattern: `subscribers_{packageName}_{date}.csv`.
- Arabic text is properly encoded (UTF-8 BOM).

### US-03: Term and Section profile pages also show subscribers
**As** an admin  
**I want** to see enrolled students on Term and Section profile pages too  
**So that** I have granular visibility into who has access at each content level.

**Acceptance Criteria:**
- Term Profile page shows students with `GrantType=Term` and `TermId=<current term>`.
- Section Profile page shows students with `GrantType=Section` and `ContentSectionId=<current section>`.
- Same table layout, search, pagination, and CSV download as the package level.

---

## 3. Functional Requirements / المتطلبات الوظيفية

### FR-01: Backend Query — GetContentSubscribersQuery
- New MediatR query: `GetContentSubscribersQuery(ContentType, ContentId, Page, PageSize, Search?)`
- Returns `ContentSubscribersPagedResult` with items of type `ContentSubscriberDto`.
- `ContentSubscriberDto` fields:
  - `StudentId` (Guid)
  - `FullName` (string)
  - `Phone` (string)
  - `Governorate` (string)
  - `District` (string?)
  - `EducationStage` (string)
  - `GradeLevel` (string)
  - `SchoolName` (string?)
  - `ParentPhone` (string?)
  - `MotherPhone` (string?)
  - `EnrolledAt` (DateTime)
  - `IsActive` (bool)
  - `AvatarSlug` (string?)

### FR-02: Backend Endpoint
- `GET /api/admin/packages/{id}/subscribers?page=1&pageSize=20&search=`
- `GET /api/admin/terms/{id}/subscribers?page=1&pageSize=20&search=`
- `GET /api/admin/sections/{id}/subscribers?page=1&pageSize=20&search=`
- All require `content.manage` permission.

### FR-03: Backend CSV Export Endpoint
- `GET /api/admin/packages/{id}/subscribers/export`
- `GET /api/admin/terms/{id}/subscribers/export`
- `GET /api/admin/sections/{id}/subscribers/export`
- Returns `text/csv` with UTF-8 BOM.
- All require `content.manage` permission.

### FR-04: Frontend Service Layer
- Add `getContentSubscribers(contentType, id, page, pageSize, search)` to `admin-service.ts`.
- Add `downloadContentSubscribersCsv(contentType, id)` that triggers a file download.

### FR-05: Frontend Component — ContentSubscribersTab
- Reusable component that accepts `contentType` ('package' | 'term' | 'section') and `contentId`.
- Uses `AdminDataTable` with proper columns.
- Search input above the table.
- CSV download button with icon.
- Empty state when no subscribers.

### FR-06: Tab Integration
- Add "الطلاب المشتركين" tab to `PackageProfilePageClient.tsx`.
- Add same tab to `TermProfilePageClient.tsx`.
- Add same tab to `SectionProfilePageClient.tsx`.

---

## 4. Non-Functional Requirements / المتطلبات غير الوظيفية

- **Performance**: Server-side pagination; query must not load all grants into memory.
- **Security**: Endpoints require `content.manage` permission.
- **Accessibility**: Table uses proper `<th scope>`, pagination has aria-labels.
- **Encoding**: CSV export uses UTF-8 BOM for Excel compatibility with Arabic.
- **Design**: Follows existing admin neumorphic design system with `var(--admin-*)` tokens.

---

## 5. Edge Cases / حالات الحافة

- Package with 0 subscribers → show empty state with message "لا يوجد طلاب مشتركين حالياً".
- Cancelled grants → show with a "ملغى" badge in red.
- Search with no results → show "لا توجد نتائج مطابقة للبحث".
- Very long student name → truncate with ellipsis.
- CSV with 0 rows → export headers only.

---

## 6. Out of Scope / خارج النطاق

- Editing student data from the subscribers table.
- Bulk operations (e.g., cancel all grants).
- Real-time updates via SignalR.
- Filtering by date range.
