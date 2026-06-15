# 127 — Package Subscribers Table: Technical Plan

## Architecture

This feature follows the existing MediatR CQRS pattern on the backend and the existing `AdminDataTable` component pattern on the frontend.

---

## Backend Changes

### New Query: `GetContentSubscribersQuery`

**File**: `backend/src/NaderGorge.Application/Features/Admin/Content/Queries/GetContentSubscribersQuery.cs`

```csharp
public record GetContentSubscribersQuery(
    string ContentType,  // "package" | "term" | "section"
    Guid ContentId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<ApiResponse<ContentSubscribersPagedResult>>;

public record ContentSubscriberDto(
    Guid StudentId, string FullName, string Phone,
    string Governorate, string? District,
    string EducationStage, string GradeLevel,
    string? SchoolName, string? ParentPhone, string? MotherPhone,
    DateTime EnrolledAt, bool IsActive, string? AvatarSlug
);

public record ContentSubscribersPagedResult(
    List<ContentSubscriberDto> Items, int TotalCount, int Page, int PageSize
);
```

**Handler Logic**:
1. Map `ContentType` → filter `StudentAccessGrants` by `GrantType` + appropriate FK (`PackageId`, `TermId`, `ContentSectionId`).
2. Apply optional `Search` filter (LIKE on `User.FullName` or `User.PhoneNumber`).
3. Join `User` → `StudentProfile` for demographic data.
4. Order by `GrantedAt DESC`, paginate server-side.

### New Query: `ExportContentSubscribersQuery`

**File**: `backend/src/NaderGorge.Application/Features/Admin/Content/Queries/ExportContentSubscribersQuery.cs`

Returns `byte[]` of UTF-8 BOM CSV data. Reuses same filtering logic as `GetContentSubscribersQuery` but loads ALL matching rows (no pagination).

### Controller Endpoints

**File**: `backend/src/NaderGorge.API/Controllers/AdminController.cs`

Add 6 endpoints:
- `GET packages/{id}/subscribers` → `GetContentSubscribersQuery("package", id, ...)`
- `GET packages/{id}/subscribers/export` → `ExportContentSubscribersQuery("package", id, ...)`
- `GET terms/{id}/subscribers` → `GetContentSubscribersQuery("term", id, ...)`
- `GET terms/{id}/subscribers/export` → `ExportContentSubscribersQuery("term", id, ...)`
- `GET sections/{id}/subscribers` → `GetContentSubscribersQuery("section", id, ...)`
- `GET sections/{id}/subscribers/export` → `ExportContentSubscribersQuery("section", id, ...)`

All require `[HasPermission("content.manage")]`.

---

## Frontend Changes

### Service Layer

**File**: `frontend/src/services/admin-service.ts`

Add:
```typescript
getContentSubscribers: async (contentType: string, id: string, page = 1, pageSize = 20, search = '') => { ... }
exportContentSubscribersCsv: async (contentType: string, id: string, name: string) => { ... }
```

The CSV export function fetches the blob from the API and triggers a browser download.

### Shared Component: `ContentSubscribersTab`

**File**: `frontend/src/components/admin/ContentSubscribersTab.tsx`

- Props: `contentType: 'package' | 'term' | 'section'`, `contentId: string`, `contentName: string`
- Uses `AdminDataTable` with columns for all subscriber fields.
- `AdminSearchToolbar` for search.
- Download button uses `Download` icon from lucide-react.
- Loading, error, and empty states.
- Row click navigates to `/admin/students/{id}` (student profile).

### Tab Integration

**Files modified**:
1. `PackageProfilePageClient.tsx` — Add 'subscribers' to `ActiveTab`, add tab config, render `ContentSubscribersTab`.
2. `TermProfilePageClient.tsx` — Same pattern.
3. `SectionProfilePageClient.tsx` — Same pattern.

---

## Data Flow

```
AdminController → MediatR → GetContentSubscribersQueryHandler
                               ├── StudentAccessGrants (filter by GrantType + FK)
                               ├── JOIN User (FullName, Phone)
                               └── JOIN StudentProfile (Governorate, etc.)
```

---

## Verification Plan

### Build Check
- `dotnet build` backend
- `npm run build` frontend (no errors/warnings)

### Manual Testing
- Navigate to Package Profile → "الطلاب المشتركين" tab → verify table loads
- Search by name/phone → verify filtering
- Click "تنزيل CSV" → verify file downloads with correct data
- Test on Term and Section profile pages
- Test empty state (package with 0 subscribers)
