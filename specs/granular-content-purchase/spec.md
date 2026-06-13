# Granular Content Purchase — شراء المحتوى الجزئي

## Feature Description
Allow students to purchase individual content items (Term, Section/Month, Lesson) from the student-facing pages, not just the entire Package. When a student purchases a term, only that term's sections and lessons become accessible. When purchasing a section, only that section's lessons become accessible. When purchasing a lesson, only that specific lesson becomes accessible.

## Current State Analysis

### What exists
- `StudentAccessGrant` entity already has granular fields: `PackageId`, `TermId`, `ContentSectionId`, `LessonId`
- `CodeType` enum already has: `Package=0`, `Term=1`, `Month=2`, `Lesson=3`
- `PurchaseContentCommand` already checks for duplicate Term/Month/Lesson grants
- `PurchaseContentModal` component exists and works for Package purchases

### What's broken / missing

1. **Backend `PurchaseContentCommand`** (line 43-44): The switch for validating price/name only handles `CodeType.Package`. All other types return `"شراء الأجزاء الفردية غير متاح بالرصيد حالياً"` — this must be extended to handle Term, Month(Section), and Lesson.

2. **Backend `AccessCheckService.HasAccessToLessonAsync`**: Only checks `g.LessonId == lessonId || g.PackageId == packageId`. Missing cascading grants:
   - If student has TermId grant → should have access to all lessons in that term
   - If student has ContentSectionId grant → should have access to all lessons in that section

3. **Frontend**: Purchase buttons/modals on Term and Section pages always pass `contentType: "Package"` and the package price. They should pass the correct contentType and entity-specific price.

4. **Frontend Lesson Page**: No purchase button exists for individual lessons.

## Requirements

### R1: Backend — Enable granular purchase validation
In `PurchaseContentCommand.Handle()`:
- `CodeType.Term` → look up the `Term` by `ContentId`, get `term.Price` and `term.Title`
- `CodeType.Month` → look up the `ContentSection` by `ContentId`, get `section.Price` and `section.Title`
- `CodeType.Lesson` → look up the `Lesson` by `ContentId`, get `lesson.Price` and `lesson.Title`
- If price is 0 or null → return error "هذا المحتوى مجاني ولا يحتاج شراء"

### R2: Backend — Cascading access checks
In `AccessCheckService.HasAccessToLessonAsync()`:
- After getting the lesson with its section and term, check grants in this order:
  1. Direct lesson grant: `g.LessonId == lessonId`
  2. Section grant: `g.ContentSectionId == lesson.ContentSectionId`
  3. Term grant: `g.TermId == lesson.ContentSection.TermId`
  4. Package grant: `g.PackageId == packageId`
- Any match = access granted

### R3: Frontend — Term page sidebar purchase
- Pass `contentType: "Term"` and `term.price` when the term has a price > 0
- Fall back to `contentType: "Package"` and `pkg.price` when term price is 0/null

### R4: Frontend — Section page sidebar purchase
- Pass `contentType: "Month"` and `section.price` when section has a price > 0
- Fall back to term price → package price cascading

### R5: Frontend — Lesson-level purchase
- If a lesson has `price > 0` and the student doesn't have access, show a purchase button on the lesson card
- On the lesson detail page, show a purchase prompt if no access

### R6: Price labels
- Sidebar should show "سعر الترم" / "سعر القسم" / "سعر الحصة" / "سعر الباقة" accordingly
- Purchase confirmation modal should show the correct entity name and price

## Edge Cases
- If a student buys a Term, and later buys the full Package → both grants coexist, no conflict
- If a student buys a Lesson, then buys the Section → the lesson grant still active but section grant gives broader access
- Zero/null price items should not show purchase buttons (they're free / included)
- `isEnrolled` on Package level is independent of granular access — a student can have Term access without being "enrolled" in the full package

## Acceptance Criteria
- [ ] Student can purchase Term from term detail page sidebar
- [ ] Student can purchase Section from section detail page sidebar  
- [ ] Student can purchase Lesson from lesson list or lesson detail page
- [ ] After purchasing Term → all lessons in that term show hasAccess=true
- [ ] After purchasing Section → all lessons in that section show hasAccess=true
- [ ] After purchasing Lesson → only that lesson shows hasAccess=true
- [ ] Price labels are contextual (سعر الترم / سعر القسم / سعر الحصة)
- [ ] Purchase modal shows correct entity name and price
- [ ] Duplicate purchase returns appropriate error
- [ ] Zero-price entities don't show purchase buttons
