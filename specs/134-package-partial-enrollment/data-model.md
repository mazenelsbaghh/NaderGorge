# Data Model: Package Partial Enrollment Display

No database migrations are required for this feature, as we are leveraging existing relationships:

## Existing Entities and Relations
1. **Package**: Has many Terms.
2. **Term**: Belongs to Package (`t.PackageId`). Has many Sections.
3. **ContentSection**: Belongs to Term (`cs.TermId`). Has many Lessons.
4. **Lesson**: Belongs to ContentSection (`l.ContentSectionId`).
5. **StudentAccessGrant**: Holds user access grants. Has nullable fields `PackageId`, `TermId`, `ContentSectionId`, and `LessonId`, matching their respective `GrantType` values.
