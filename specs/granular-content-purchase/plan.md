# Technical Plan — Granular Content Purchase

## Overview
Enable students to purchase individual Terms, Sections, and Lessons via the existing `PurchaseContentCommand` and `PurchaseContentModal` infrastructure. The main changes are:
1. Backend: Extend price validation in `PurchaseContentCommand`
2. Backend: Fix cascading access checks in `AccessCheckService`
3. Frontend: Pass correct `contentType` and price from each page's sidebar

## Architecture Decision
**No new DB tables, migrations, or API endpoints needed.** The existing `StudentAccessGrant` entity and `CodeType` enum already support granular access. This is purely logic fixes and wiring.

---

## Backend Changes

### Component 1: PurchaseContentCommand

#### [MODIFY] [PurchaseContentCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs)

Extend the price validation switch (lines 35-45) to handle `Term`, `Month`, and `Lesson`:

```csharp
case CodeType.Term:
    var term = await _db.Terms.FirstOrDefaultAsync(t => t.Id == request.ContentId, ct);
    if (term == null) return ApiResponse<bool>.Fail("الترم غير موجود");
    price = term.Price;
    contentName = term.Title;
    break;
case CodeType.Month:
    var section = await _db.ContentSections.FirstOrDefaultAsync(s => s.Id == request.ContentId, ct);
    if (section == null) return ApiResponse<bool>.Fail("القسم غير موجود");
    price = section.Price;
    contentName = section.Title;
    break;
case CodeType.Lesson:
    var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == request.ContentId, ct);
    if (lesson == null) return ApiResponse<bool>.Fail("الحصة غير موجودة");
    price = lesson.Price;
    contentName = lesson.Title;
    break;
```

Remove the `default: return Fail(...)` case.

Add zero-price guard after the switch:
```csharp
if (price <= 0)
    return ApiResponse<bool>.Fail("هذا المحتوى مجاني ولا يحتاج شراء.");
```

---

### Component 2: AccessCheckService

#### [MODIFY] [AccessCheckService.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Services/AccessCheckService.cs)

Fix `HasAccessToLessonAsync` to check cascading grants:

```csharp
public async Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
{
    // Admin/Teacher bypass
    var userRoles = await _db.UserRoles
        .Include(ur => ur.Role)
        .Where(ur => ur.UserId == userId)
        .Select(ur => ur.Role.Name)
        .ToListAsync(ct);

    if (userRoles.Contains("Admin") || userRoles.Contains("Teacher"))
        return true;

    var lesson = await _db.Lessons
        .Include(l => l.ContentSection)
        .ThenInclude(cs => cs.Term)
        .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

    if (lesson == null) return false;

    var sectionId = lesson.ContentSectionId;
    var termId = lesson.ContentSection?.TermId;
    var packageId = lesson.ContentSection?.Term?.PackageId;

    // Check ANY matching grant (lesson, section, term, or package level)
    return await _db.StudentAccessGrants.AnyAsync(g =>
        g.UserId == userId &&
        g.IsActive &&
        (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow) &&
        (g.LessonId == lessonId ||
         g.ContentSectionId == sectionId ||
         (termId != null && g.TermId == termId) ||
         (packageId != null && g.PackageId == packageId)),
        ct);
}
```

---

## Frontend Changes

### Component 3: Term Detail Page Sidebar

#### [MODIFY] [TermDetailPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/terms/%5BtermId%5D/TermDetailPageClient.tsx)

- Change `PurchaseContentModal` props:
  - If `term.price > 0`: `contentType="Term"`, `contentId={termId}`, `price={term.price}`
  - Else: `contentType="Package"`, `contentId={packageId}`, `price={pkg.price}`

### Component 4: Section Detail Page Sidebar

#### [MODIFY] [SectionDetailPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/terms/%5BtermId%5D/sections/%5BsectionId%5D/SectionDetailPageClient.tsx)

- Same pattern: if `section.price > 0` → `contentType="Month"`, else cascade up

### Component 5: Lesson Purchase Button

#### [MODIFY] Lesson cards in [SectionDetailPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/terms/%5BtermId%5D/sections/%5BsectionId%5D/SectionDetailPageClient.tsx)

- For lessons with `price > 0` and no access: show a purchase button that opens PurchaseContentModal with `contentType="Lesson"` and the lesson's price

---

## Verification Plan

### Manual Verification
1. Set a term price > 0 in admin
2. Try purchasing term from student term page → verify lesson access within that term
3. Set a section price > 0 in admin
4. Try purchasing section → verify lesson access within that section
5. Try purchasing individual lesson → verify only that lesson accessible
6. Verify duplicate purchase error works
7. Verify zero-price items don't show purchase buttons
