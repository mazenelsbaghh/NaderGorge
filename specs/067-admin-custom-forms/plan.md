# Implementation Plan: Custom Dynamic Forms System

**Branch**: `067-admin-custom-forms` | **Date**: 2026-06-01 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/067-admin-custom-forms/spec.md)
**Input**: Feature specification from `/specs/067-admin-custom-forms/spec.md`

## Summary
Implement a fully customizable and dynamic Forms system that enables admins to build forms (e.g. for booking new secondary classes or recruitment applications), manage submission lifecycles, and view response snapshots. The forms are served on a public route using a slug (e.g. `/forms/recruitment`) and submissions are saved as static JSON payloads to ensure fields modification doesn't corrupt historical records.

## Technical Context

- **Language/Version**: C# 13 (.NET 9.0) for Backend, TypeScript 5.x / Next.js 16.2.1 for Frontend
- **Primary Dependencies**: MediatR, FluentValidation, EF Core 9.0, Axios (Frontend)
- **Storage**: PostgreSQL (via EF Core DbSets: `CustomForms`, `FormSubmissions`)
- **Testing**: Playwright for E2E tests, manual API verification
- **Target Platform**: Docker-compose (net9.0 backend container, nextjs frontend container)
- **Performance Goals**: API response time < 200ms p95 for public retrieval, dynamic form render < 50ms, submissions persist < 500ms.
- **Constraints**: No dynamicity of Tailwind classes without validation, strict layered Clean Architecture, Cairo font, and RTL-first formatting.

## Constitution Check

- **Layer Separation**: The design strictly maintains Domain -> Application -> Infrastructure -> API boundaries. Data mapping and logic happen inside MediatR handlers. Database access is only via `IAppDbContext` injected into handlers.
- **Security & Access Rules**: Admin endpoints are secured with `[Authorize(Roles = "Admin")]` attribute. Public endpoints allow anonymous access and validate field format boundaries.
- **Data Protection/Integrity**: Dynamic fields in form submission are snapshotted in `SubmittedDataJson` text/JSON column. Form field list is stored in `FieldsJson` to prevent data corruption when fields are modified.

## Project Structure

### Documentation

```text
specs/067-admin-custom-forms/
├── plan.md              # This file
├── spec.md              # Feature specification
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code

#### Backend
```text
backend/src/NaderGorge.Domain/
├── Entities/
│   ├── CustomForm.cs         # Form entity
│   └── FormSubmission.cs     # Submission entity
└── Enums/
    └── FormSubmissionStatus.cs # Submission status enum

backend/src/NaderGorge.Application/
├── Features/
│   ├── Admin/
│   │   ├── Commands/
│   │   │   └── AdminFormCommands.cs # Admin CRUD MediatR commands
│   │   └── Queries/
│   │       └── AdminFormQueries.cs  # Admin listing MediatR queries
│   └── Public/
│       ├── Commands/
│       │   └── SubmitPublicFormCommand.cs # Guest submission command
│       └── Queries/
│           └── GetPublicFormQuery.cs     # Retrieve form details query

backend/src/NaderGorge.Infrastructure/
└── Data/
    └── AppDbContext.cs # DbSets and model config mappings

backend/src/NaderGorge.API/
└── Controllers/
    ├── AdminFormsController.cs # Admin endpoints
    └── PublicFormsController.cs # Public endpoints
```

#### Frontend
```text
frontend/src/
├── app/
│   ├── admin/
│   │   └── forms/
│   │       ├── page.tsx               # Admin forms list
│   │       ├── new/
│   │       │   └── page.tsx           # Create form builder
│   │       └── [id]/
│   │           ├── edit/
│   │           │   └── page.tsx       # Edit form builder
│   │           └── submissions/
│   │               └── page.tsx       # Submissions table
│   └── forms/
│       └── [slug]/
│           └── page.tsx               # Public dynamic form submission page
├── services/
│   └── forms-service.ts               # Forms Axios API calls
└── components/
    └── admin/
        └── AdminShellChrome.tsx       # Register /admin/forms menu item
```

## Data Model & Database Migration

### Database Entities

#### `FormSubmissionStatus.cs` (Domain Enum)
```csharp
namespace NaderGorge.Domain.Enums;

public enum FormSubmissionStatus
{
    Pending = 0,
    Reviewed = 1,
    Accepted = 2,
    Rejected = 3
}
```

#### `CustomForm.cs` (Domain Entity)
```csharp
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class CustomForm : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public bool IsActive { get; set; }
    public string FieldsJson { get; set; } = null!; // JSON string containing field schemas
    
    public virtual ICollection<FormSubmission> Submissions { get; set; } = new List<FormSubmission>();
}
```

#### `FormSubmission.cs` (Domain Entity)
```csharp
using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class FormSubmission : BaseEntity
{
    public Guid CustomFormId { get; set; }
    public string SubmittedDataJson { get; set; } = null!; // JSON string of submitted answers mapping field IDs to values
    public FormSubmissionStatus Status { get; set; } = FormSubmissionStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public virtual CustomForm CustomForm { get; set; } = null!;
}
```

### DbContext Mapping configurations in `AppDbContext.cs`
```csharp
// Define DbSets
public DbSet<CustomForm> CustomForms => Set<CustomForm>();
public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

// In OnModelCreating:
modelBuilder.Entity<CustomForm>(e =>
{
    e.ToTable("custom_forms");
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.Slug).IsUnique();
    e.Property(x => x.Title).HasMaxLength(200).IsRequired();
    e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
    e.Property(x => x.Description).HasMaxLength(2000);
    e.Property(x => x.FieldsJson).IsRequired();
});

modelBuilder.Entity<FormSubmission>(e =>
{
    e.ToTable("form_submissions");
    e.HasKey(x => x.Id);
    e.Property(x => x.AdminNotes).HasMaxLength(2000);
    e.Property(x => x.SubmittedDataJson).IsRequired();
    e.Property(x => x.Status).HasConversion<int>();
    e.HasOne(x => x.CustomForm)
     .WithMany(f => f.Submissions)
     .HasForeignKey(x => x.CustomFormId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

## API Contracts & Routing

### Admin Forms Controller (`/api/admin/forms`)
- `GET /api/admin/forms`
  - Response: `ApiResponse<List<AdminFormDto>>`
- `GET /api/admin/forms/{id}`
  - Response: `ApiResponse<AdminFormDetailDto>`
- `POST /api/admin/forms`
  - Request: `CreateFormCommand` (Title, Description, Slug, IsActive, FieldsJson)
  - Response: `ApiResponse<Guid>`
- `PUT /api/admin/forms/{id}`
  - Request: `UpdateFormCommand` (Id, Title, Description, Slug, IsActive, FieldsJson)
  - Response: `ApiResponse`
- `GET /api/admin/forms/{id}/submissions`
  - Response: `ApiResponse<List<AdminSubmissionDto>>`
- `PUT /api/admin/forms/submissions/{submissionId}/status`
  - Request: `UpdateSubmissionStatusCommand` (Status, AdminNotes)
  - Response: `ApiResponse`

### Public Forms Controller (`/api/public/forms`)
- `GET /api/public/forms/{slug}`
  - Response: `ApiResponse<PublicFormDto>` (Returns form data if exists and IsActive)
- `POST /api/public/forms/{slug}/submit`
  - Request: `SubmitPublicFormCommand` (Answers: Dictionary<string, string>)
  - Response: `ApiResponse` (Validates answers against FieldsJson and saves)

## MediatR DTOs, Commands, Queries

### DTOs
```csharp
public record AdminFormDto(Guid Id, string Title, string Description, string Slug, bool IsActive, int SubmissionCount, DateTime CreatedAt);
public record AdminFormDetailDto(Guid Id, string Title, string Description, string Slug, bool IsActive, string FieldsJson, DateTime CreatedAt);
public record AdminSubmissionDto(Guid Id, Guid CustomFormId, string SubmittedDataJson, FormSubmissionStatus Status, string? AdminNotes, DateTime SubmittedAt);
public record PublicFormDto(Guid Id, string Title, string Description, string FieldsJson);
```

### Commands and Handlers

#### `CreateFormCommand` (Admin)
- Validates Slug uniqueness and format. Inserts `CustomForm` entity.

#### `UpdateFormCommand` (Admin)
- Validates that Slug remains unique (excluding current form). Updates properties.

#### `UpdateSubmissionStatusCommand` (Admin)
- Finds `FormSubmission`, updates `Status` and `AdminNotes`.

#### `SubmitPublicFormCommand` (Public)
- Finds form by active slug. Parses `FieldsJson` definitions.
- Loops through fields and enforces rules:
  - If field `isRequired` is true and value is missing/empty, returns failure.
  - If field type is `email`, validates regex structure.
  - If field type is `phone`, validates numeric digit boundaries (Arabic/Western numbers format compatibility).
- Serializes answers into `SubmittedDataJson`.
- Adds `FormSubmission` database record.

## Frontend Services & Dynamic Component UI

### `forms-service.ts`
Axios wrapper communicating with `/api/admin/forms` and `/api/public/forms` endpoints.

### UI Screens

1. **Admin Sidebar Register**: Register `ClipboardList` for route `/admin/forms`.
2. **Forms List Screen (`/admin/forms/page.tsx`)**:
   - Renders a list of custom forms using `AdminDataTable`.
   - Action buttons: "إنشاء نموذج جديد" (Create form), "تعديل" (Edit), "الطلبات المستلمة" (View Submissions with submission count badge).
3. **Form Builder (`/admin/forms/new/page.tsx` & `/admin/forms/[id]/edit/page.tsx`)**:
   - Rich editor to configure metadata: Title, description, slug (URL path validation).
   - Dynamic fields builder layout: Add, remove, and reorder fields.
   - Field config: Field Label, Input Type (Text, Long Text, Number, Email, Phone, Dropdown, Checkbox), Placeholder, Is Required toggler.
   - If dropdown (select) type is picked, shows an input to add comma-separated options list.
4. **Submissions Moderation Board (`/admin/forms/[id]/submissions/page.tsx`)**:
   - Renders `AdminDataTable` showing submissions, grouped by date.
   - Row content shows values parsed from `SubmittedDataJson`.
   - Action: "عرض" opens details side drawer or modal displaying full form layout containing the submission's answers, an editable dropdown for `Status` (Pending, Reviewed, Accepted, Rejected), and a text area for `AdminNotes`.
5. **Public Viewer (`/forms/[slug]/page.tsx`)**:
   - Beautiful, RTL-first public layout tailored with Nader George Gold-Sand palette.
   - Dynamically resolves field types into HTML5 input elements:
     - `text` -> `<input type="text" />`
     - `longtext` -> `<textarea />`
     - `email` -> `<input type="email" />`
     - `phone` -> `<input type="tel" />`
     - `number` -> `<input type="number" />`
     - `select` -> `<select>` dropdown
     - `checkbox` -> `<input type="checkbox" />`
   - Client-side validation: triggers inline errors in Arabic before submission.
   - Success state shows a premium checkout-success animated card with green-gold gradients.

## Verification Plan

### Automated Tests
- Run `npm test` and backend builds.
- Scaffold Playwright script testing the public form path submission and verifying it updates the admin submissions counter.

### Manual Verification
1. Create a form "نموذج توظيف" with slug `recruitment`. Configure fields:
   - Full Name (required text)
   - Email (required email)
   - Phone (required phone)
   - Experience (select: 1 year, 2 years, 3+ years)
   - CV Summary (longtext)
2. Submit from a public window (`/forms/recruitment`) with a blank required field, verify validation fires.
3. Submit a valid form, verify success message appears.
4. Navigate to admin dashboard (`/admin/forms`), check submission details modal, change status to `Reviewed`, add admin note, save, and check persistence.
5. Edit form fields (e.g. delete one field), verify historical submission reads the deleted field's value correctly.
