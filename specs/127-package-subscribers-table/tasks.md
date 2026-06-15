# 127 — Package Subscribers Table: Tasks

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Implementation Tasks

### Backend: MediatR Query

- [ ] **T01**: Create `GetContentSubscribersQuery.cs` in `backend/src/NaderGorge.Application/Features/Admin/Content/Queries/`
  - Define records: `GetContentSubscribersQuery`, `ContentSubscriberDto`, `ContentSubscribersPagedResult`
  - Implement `GetContentSubscribersQueryHandler` that:
    1. Maps `ContentType` string to `CodeType` enum and filters `StudentAccessGrants` by `GrantType` + appropriate FK
    2. Applies optional `Search` filter (LIKE on `User.FullName` or `User.PhoneNumber`)
    3. Joins `User` → `StudentProfile` for demographic data
    4. Orders by `GrantedAt DESC`, paginates server-side
  - Return `ApiResponse<ContentSubscribersPagedResult>`

- [ ] **T02**: Create `ExportContentSubscribersQuery.cs` in same directory
  - Define `ExportContentSubscribersQuery(string ContentType, Guid ContentId, string? Search)` → `IRequest<byte[]>`
  - Handler reuses same join/filter logic as T01 but loads ALL rows
  - Builds CSV with UTF-8 BOM header, Arabic column headers
  - Columns: الاسم الكامل, رقم الهاتف, المحافظة, المنطقة, المرحلة, الصف, المدرسة, هاتف الأب, هاتف الأم, تاريخ الاشتراك, الحالة

### Backend: API Endpoints

- [ ] **T03**: Add 6 endpoints to `AdminController.cs`
  - `[HttpGet("packages/{id:guid}/subscribers")]` → `GetContentSubscribersQuery("package", id, page, pageSize, search)`
  - `[HttpGet("packages/{id:guid}/subscribers/export")]` → `ExportContentSubscribersQuery("package", id, search)` → return `File(bytes, "text/csv", fileName)`
  - `[HttpGet("terms/{id:guid}/subscribers")]` → same pattern with "term"
  - `[HttpGet("terms/{id:guid}/subscribers/export")]` → same pattern
  - `[HttpGet("sections/{id:guid}/subscribers")]` → same pattern with "section"
  - `[HttpGet("sections/{id:guid}/subscribers/export")]` → same pattern
  - All require `[HasPermission("content.manage")]`

### Frontend: Service Layer

- [ ] **T04**: Add to `frontend/src/services/admin-service.ts`
  - Add `ContentSubscriberDto` interface matching backend DTO
  - Add `getContentSubscribers(contentType: string, id: string, page?: number, pageSize?: number, search?: string)` method
  - Add `exportContentSubscribersCsv(contentType: string, id: string, contentName: string)` method that:
    1. Fetches blob from `GET /admin/{contentType}s/{id}/subscribers/export`
    2. Creates URL.createObjectURL and triggers download
    3. File name: `subscribers_${contentName}_${YYYY-MM-DD}.csv`

### Frontend: Shared Component

- [ ] **T05**: Create `frontend/src/components/admin/ContentSubscribersTab.tsx`
  - Props: `contentType: 'package' | 'term' | 'section'`, `contentId: string`, `contentName: string`
  - State: `subscribers[]`, `loading`, `error`, `page`, `totalCount`, `search`
  - `useEffect` to fetch subscribers when `contentId`, `page`, or `search` changes (debounced search 300ms)
  - Uses `AdminDataTable<ContentSubscriberDto>` with columns:
    - الاسم (FullName + AvatarSlug initials badge)
    - الهاتف (Phone)
    - المحافظة (Governorate)
    - المرحلة (EducationStage mapped to Arabic label)
    - الصف (GradeLevel mapped to Arabic label)
    - تاريخ الاشتراك (EnrolledAt formatted)
    - الحالة (IsActive → badge: "نشط" green / "ملغى" red)
  - Search input at top with placeholder "بحث بالاسم أو رقم الهاتف..."
  - Download CSV button with `Download` icon from lucide-react
  - Empty state: "لا يوجد طلاب مشتركين حالياً"
  - Row click → `router.push('/admin/students/{studentId}')`

- [ ] **T06**: Export `ContentSubscribersTab` from `frontend/src/components/admin/index.ts`

### Frontend: Tab Integration

- [ ] **T07**: Update `PackageProfilePageClient.tsx`
  - Add `'subscribers'` to `ActiveTab` union type
  - Add tab `{ key: 'subscribers', label: 'الطلاب المشتركين', icon: Users }` to `TABS` array
  - Add render block: `{activeTab === 'subscribers' && <ContentSubscribersTab contentType="package" contentId={params.id} contentName={pkg.name} />}`

- [ ] **T08**: Update `TermProfilePageClient.tsx`
  - Add `'subscribers'` to `ActiveTab` union type
  - Add tab to `TABS` array
  - Add render block with `contentType="term"`

- [ ] **T09**: Update `SectionProfilePageClient.tsx`
  - Add `'subscribers'` to `ActiveTab` union type
  - Add tab to `TABS` array
  - Add render block with `contentType="section"`

### Quality Gates

- [ ] **T10**: Run `clean-code-guard` on all changed production files
- [ ] **T11**: Run `test-guard` on any changed test files
- [ ] **T12**: Verify backend builds with `dotnet build` (no warnings)
- [ ] **T13**: Verify frontend builds with `npm run build` (no errors)
