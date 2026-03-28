# Development Quickstart: Nested Content Profiles

To get started on `018-nested-content-profiles`:

1.  **Backend Development**: Ensure your DB has some Packages and Terms. Test creating Sections via Swagger (`/swagger/index.html`) using `CreateSectionCommand`.
2.  **API Endpoints**: Add `GetTermByIdQuery` to `NaderGorge.Application` and wire it to `AdminController`. Do the same for `GetSectionByIdQuery`.
3.  **Frontend Setup**: Add `getTermById` to `adminService`. Implement the UI for `TermProfilePage` inside `app/admin/content/terms/[id]/page.tsx` utilizing existing components like `AdminShellChrome`.
4.  **Local Testing**: Ensure you test clicking an "Eye" icon inside the Term List, loading the Term Profile, adding a section, and verifying its order. Same pattern applies for the Section to Lesson workflow.
