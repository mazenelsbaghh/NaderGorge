# Research: Nested Content Profiles

## Technical Context & Unknowns Resolved

### 1. Missing Metadata Queries
- **Decision**: Create `GetTermByIdQuery` and `GetSectionByIdQuery`.
- **Rationale**: The new `TermProfilePage` and `SectionProfilePage` need to display the header/title of the term and section (e.g., "Term: First Semester"). Currently, there are only queries to fetch lists of children (`GetSectionsQuery(termId)`, `GetLessonsQuery(sectionId)`), but no queries to fetch the parent entity itself.
- **Alternatives considered**: Passing the title down via URL params (e.g., `?title=First+Semester`) - rejected because it breaks on direct URL navigation and bookmarking, and doesn't align with the existing `getPackageById` pattern.

### 2. Frontend Component Extensibility
- **Decision**: Duplicate the layout pattern used in `PackageProfilePage` for `TermProfilePage` and `SectionProfilePage` using `AdminShellChrome`.
- **Rationale**: Maintains consistency across all nested content management layers. The user workflow is identical: view parent details, manage list of children.
- **Alternatives considered**: A vast tree-view or drag-and-drop interface for managing nested content on a single page - rejected because it violates the MVP discipline (Phase 3 requirements favor simplicity) and could become slow and cluttered given the amount of content per package.

### 3. API Contract Updates
- **Decision**: Standardize `AdminController.cs` to expose `[HttpGet("terms/{id:guid}")]` and `[HttpGet("sections/{id:guid}")]`.
- **Rationale**: Keeps the administrative dashboard APIs logically grouped under the admin context.

## Integration Best Practices
- Next.js 15 requires unwrapping React Server Components `params` using `React.use()` in client components. We must strictly follow this for `params.id` in the new `page.tsx` files.
- Reusing `AdminStatCard`, `AdminTabBar`, and `AdminShellChrome` strictly adheres to the "Editorial Scholar" UI rules specified in the Constitution.
