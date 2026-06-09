# Implementation Plan: Separate User Lists & Remove General Users Page

**Branch**: `104-separate-user-lists` | **Date**: 2026-06-09 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/104-separate-user-lists/spec.md)
**Input**: Feature specification from `/specs/104-separate-user-lists/spec.md`

## Summary

The general "Users" page (`/admin/users`) will be removed/redirected and replaced by a dedicated "Students" (`/admin/students`) list. All user roles will be separated into their own pages:
1. **Students** -> `/admin/students`
2. **Assistants** -> `/admin/assistants`
3. **Admins** -> `/admin/admins`
4. **Teachers** -> `/admin/teachers` (already exists)

We will modify the frontend routes, sidebar navigation config, student profile page, and Playwright tests to align with this separation. The Student Profile page (`/admin/users/[id]`) will remain under `/admin/users/[id]` for URL stability, but it will be visually and behaviorally nested under "الطلاب" (active sidebar highlight and back-button).

---

## Technical Context

- **Language/Version**: TypeScript 5.x / Next.js 16.2.1 / React 19
- **Primary Dependencies**: `lucide-react`, `framer-motion`, `react-hot-toast`
- **Storage**: N/A
- **Testing**: Playwright (`@playwright/test`)
- **Target Platform**: Web application (Admin dashboard)
- **Performance Goals**: Transitions under 300ms, pages load under 1s.
- **Constraints**: Apply the Cairo typography and Umber-sand/gold light theme (or calm dark mode) from the Curated Archive design system.

---

## Constitution Check

### Layer Impact

- **Backend**: None.
- **Frontend**: Modify sidebar configuration, navigation items, page redirects, and create the three new list components.
- **Worker**: None.
- **Database**: None.
- **Docker**: None.

### Automated Tests
- Update Playwright test `frontend/tests/e2e/admin-users.spec.ts` to navigate to `/admin/students` instead of `/admin/users` and verify list filtering.

### Manual QA
- Log in as Admin, click "الطلاب", verify list shows only students and no role tabs.
- Click "المساعدين", verify list shows only assistants.
- Click "المديرين", verify list shows only admins.
- Type `/admin/users` in URL, check redirect to `/admin/students`.

---

## Project Structure

### Documentation (this feature)

```text
specs/104-separate-user-lists/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Technical research
├── data-model.md        # Data models mapping
└── tasks.md             # Tasks list
```

### Source Code (repository root)

```text
frontend/
└── src/
    ├── app/
    │   └── admin/
    │       ├── admins/
    │       │   └── page.tsx        # [NEW] Admins page
    │       ├── assistants/
    │       │   └── page.tsx        # [NEW] Assistants page
    │       ├── students/
    │       │   └── page.tsx        # [NEW] Students page
    │       ├── users/
    │       │   ├── page.tsx        # [MODIFY] Client redirect to /admin/students
    │       │   └── [id]/
    │       │       └── page.tsx    # [MODIFY] Set activePath='/admin/students', back to '/admin/students'
    │       └── layout.tsx
    ├── components/
    │   └── admin/
    │       └── AdminShellChrome.tsx # [MODIFY] Sidebar routes
    └── packages/
        └── admin/
            └── navigation.tsx       # [MODIFY] Sidebar items config
```

---

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Run `npm run lint` inside the `frontend` directory.
- Run Playwright E2E test `npx playwright test frontend/tests/e2e/admin-users.spec.ts`.

**Docker Gate Required**:
- Verify frontend compilation is warning/error free.

**Manual QA Required**:
- Test redirection from `/admin/users`.
- Test the new pages in the sidebar and check correct data loading.
