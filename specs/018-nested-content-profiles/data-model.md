# Data Model: Nested Content Profiles

The backend schema already supports the core hierarchy (`Package` -> `Term` -> `ContentSection` -> `Lesson`). This feature requires us to expose specific read models for profile pages.

## Database Entities (Existing)

### 1. `Term`
- **Fields**: `Guid Id`, `string Title`, `int Order`, `Guid PackageId`
- **Relationships**: A Package has many Terms. A Term has many Sections.

### 2. `ContentSection`
- **Fields**: `Guid Id`, `string Title`, `int Order`, `Guid TermId`
- **Relationships**: A Term has many Sections. A Section has many Lessons.

### 3. `Lesson`
- **Fields**: `Guid Id`, `string Title`, `string Summary`, `int Order`, `Guid ContentSectionId`, `Guid? ExamId`
- **Relationships**: A Section has many Lessons.

## View Models (New)

### `GetTermByIdDto` (or returned from `GetTermByIdQuery`)
```csharp
public record TermDetailDto(Guid Id, string Title, int Order, Guid PackageId);
```

### `GetSectionByIdDto` (or returned from `GetSectionByIdQuery`)
```csharp
public record SectionDetailDto(Guid Id, string Title, int Order, Guid TermId);
```

These simple DTOs provide enough context to power the breadcrumbs and headers of `TermProfilePage` and `SectionProfilePage`.
