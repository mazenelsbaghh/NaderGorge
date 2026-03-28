# Implementation Tasks: Content Pricing & Currency Update

**Branch**: `019-content-pricing-currency`
**Spec**: [spec.md](./spec.md)
**Plan**: [plan.md](./plan.md)
**Data Model**: [data-model.md](./data-model.md)
**Contracts**: [contracts/api.md](./contracts/api.md)

## Implementation Strategy
- Build the data foundation first (EF Core Migrations) as it is a firm blocker for all backend code.
- Execute US1 (Pricing application) bottom-up: Application Commands -> Queries/DTOs -> Frontend API Services -> Frontend Forms & Lists.
- Execute US2 (Currency display text change) in parallel, as it is just string replacement in React Views.

## Phases

### Phase 1: Setup
*(No pure application setup tasks required since the project is fully bootstrapped)*

### Phase 2: Foundational  
**Goal**: Persist the `Price` column in the database through EF Core.

- [x] T001 Update `Term` entity to add `public decimal Price { get; set; }` in `backend/src/NaderGorge.Domain/Entities/Term.cs`.
- [x] T002 Update `ContentSection` and `Lesson` entities to add `public decimal Price { get; set; }` in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`.
- [x] T003 Generate EF Core migration by running `dotnet ef migrations add AddContentPricing --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API` and update the database with `dotnet ef database update`.

### Phase 3: User Story 1 - Granular Content Pricing (P1)
**Goal:** Allow administrators to set and view prices for Terms, Sections, and Lessons.
**Independent Test:** Admin navigating to Term/Section/Lesson creation will see a Price field, and the list will properly display the saved price.

**Backend Commands & Queries**
- [x] T004 [P] [US1] Update `CreateTermCommand` and `UpdateTermCommand` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Admin/Commands/CreateTermCommand.cs` and `UpdateTermCommand.cs`.
- [x] T005 [P] [US1] Update `CreateSectionCommand` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Admin/Commands/CreateSectionCommand.cs`.
- [x] T006 [P] [US1] Update `CreateLessonCommand` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Admin/Commands/CreateLessonCommand.cs`.
- [x] T007 [P] [US1] Update `TermDto` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageByIdQuery.cs` and `GetTermsQuery.cs`.
- [x] T008 [P] [US1] Update `TermDetailDto` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetTermByIdQuery.cs`.
- [x] T009 [P] [US1] Update `ContentSectionDto` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetSectionsQuery.cs`.
- [x] T010 [P] [US1] Update `SectionDetailDto` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetSectionByIdQuery.cs`.
- [x] T011 [P] [US1] Update `LessonSummaryDto` to include `decimal Price` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs`.

**Frontend API & Services**
- [x] T012 [P] [US1] Add `price: number` to `TermDto`, `ContentSectionDto`, and `LessonSummaryDto` types in `frontend/src/services/content-service.ts`.
- [x] T013 [P] [US1] Update arguments for `createTerm`, `updateTerm`, `createSection`, and `createLesson` to accept `price: number` in `frontend/src/services/admin-service.ts`.

**Frontend React UI**
- [x] T014 [US1] Add a "Price" number input to `AddTermForm` in `frontend/src/components/admin/AddTermForm.tsx` (using `جنيها`).
- [x] T015 [US1] Display Price in `TermListManager` item badge in `frontend/src/components/admin/TermListManager.tsx`.
- [x] T016 [US1] Add a "Price" number input to `AddSectionForm` in `frontend/src/components/admin/AddSectionForm.tsx` (using `جنيها`).
- [x] T017 [US1] Display Price in `SectionListManager` item badge in `frontend/src/components/admin/SectionListManager.tsx`.
- [x] T018 [US1] Add a "Price" number input to `AddLessonForm` in `frontend/src/components/admin/AddLessonForm.tsx` (using `جنيها`).
- [x] T019 [US1] Display Price in `LessonListManager` item badge in `frontend/src/components/admin/LessonListManager.tsx`.

### Phase 4: User Story 2 - Localized Currency Display (P2)
**Goal:** Globally switch UI currency strings from Kuwaiti Dinar to Egyptian Pound.
**Independent Test:** Admin views "packages" and package profiles and sees "جنيها" everywhere.

- [x] T020 [P] [US2] Update `AddPackageForm.tsx` (actually `page.tsx`) in `frontend/src/app/admin/content/page.tsx` to replace `دك` or similar strings with `جنيها`.
- [x] T021 [P] [US2] Update `PackageDetailsForm.tsx` in `frontend/src/components/admin/PackageDetailsForm.tsx` to replace `دك` with `جنيها`.
- [x] T022 [P] [US2] Expand scope to include entityPrice in `page.tsx` creation modsls./packages/[id]/page.tsx` (AdminStatCard explicitly rendering price).

### Phase 5: Polish & Cross-Cutting Concerns

- [x] T023 Compile and test creating a new Package -> Term -> Section -> Lesson structure, passing prices sequentially to all components without any API payload 400 errors.

---

## Dependencies

- **Phase 2** (Database Migration) MUST complete before **Phase 3** since ASP.NET Core will crash retrieving missing columns.
- `T004-T011` (Backend Queries & Commands) and `T020-T022` (Frontend US2 string replacements) are completely independent and can be executed in parallel immediately.
- `T014-T019` depend on `T012` and `T013` (Frontend Services definition).

## Parallel Execution Opportunities

- Updating the frontend "دك" replacement (Phase 4) can occur simultaneously while the backend developer generates EF Core migrations (Phase 2).
- Updating query files (DTO mapping) can be easily executed in parallel chunks.
