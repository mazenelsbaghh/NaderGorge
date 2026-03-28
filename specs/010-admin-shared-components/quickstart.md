# Quickstart: Admin Shared Components

**Feature**: `010-admin-shared-components`
**Date**: 2026-03-26

## Overview

After this refactoring, creating a new admin page requires 3 steps:

### Step 1: Import shared components

```tsx
import {
  AdminShellChrome,
  AdminDataTable,
  AdminStatCard,
  AdminModal,
  AdminSearchToolbar,
  AdminTabBar,
} from '@/components/admin';
```

### Step 2: Wrap page in AdminShellChrome

```tsx
export default function AdminNewPage() {
  return (
    <AdminShellChrome
      activePath="/admin/new-page"
      sectionLabel="اسم القسم"
      pageTitle="عنوان الصفحة"
    >
      {/* Page content here */}
    </AdminShellChrome>
  );
}
```

### Step 3: Compose with shared components

```tsx
<AdminShellChrome activePath="/admin/example" sectionLabel="مثال" pageTitle="صفحة مثال">
  {/* Stats row */}
  <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
    <AdminStatCard icon={Users} label="الإجمالي" value={150} variant="light" />
    <AdminStatCard icon={Star} label="النشطون" value={42} variant="accent" />
    <AdminStatCard icon={Shield} label="المعلقون" value={5} variant="muted" />
  </section>

  {/* Search + Table */}
  <AdminSearchToolbar value={search} onChange={setSearch} placeholder="بحث..." />
  <AdminDataTable
    columns={myColumns}
    data={myData}
    loading={isLoading}
    rowKey={(row) => row.id}
  />

  {/* Modal */}
  <AdminModal open={showModal} onClose={() => setShowModal(false)} title="إضافة عنصر">
    <form>{/* fields */}</form>
  </AdminModal>
</AdminShellChrome>
```

## Running the project

```bash
cd frontend
npm run dev
```

Navigate to `http://localhost:3000/admin/*` to see the admin pages.

## Key files

| File | Purpose |
|------|---------|
| `src/components/admin/index.ts` | Barrel exports for all shared components |
| `src/components/admin/AdminShellChrome.tsx` | Layout shell |
| `src/components/admin/AdminDataTable.tsx` | Generic table |
| `src/components/admin/AdminStatCard.tsx` | Stat cards |
| `src/components/admin/AdminModal.tsx` | Modal wrapper |
| `src/components/admin/AdminSearchToolbar.tsx` | Search bar |
| `src/components/admin/AdminTabBar.tsx` | Tab navigation |
| `src/components/admin/useAdminTheme.ts` | Theme hook |
