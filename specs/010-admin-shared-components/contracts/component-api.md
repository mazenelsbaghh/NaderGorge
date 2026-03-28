# Component API Contracts: Admin Shared Components

**Feature**: `010-admin-shared-components`
**Date**: 2026-03-26

> These contracts define the public API surface for each shared component. All internal implementation details are excluded — only the inputs, outputs, and behavioral guarantees are documented.

---

## Contract 1: AdminShellChrome

**Import**: `import { AdminShellChrome } from '@/components/admin'`

### Usage Pattern

```tsx
<AdminShellChrome
  activePath="/admin/users"
  sectionLabel="إدارة المستخدمين"
  pageTitle="قائمة الأعضاء"
  subtitle="إدارة وتدقيق الوصول"
  action={<button>دعوة عضو جديد</button>}
  subNav={<AdminTabBar ... />}
>
  {/* Page content */}
</AdminShellChrome>
```

### Behavioral Guarantees

1. Renders sidebar navigation with correct active item highlighting
2. Renders breadcrumb: "الباحث التحريري > {sectionLabel}"
3. Renders page title with optional subtitle and header accessory
4. Renders footer ornament with design-token colors
5. Applies theme CSS custom properties from `useAdminTheme` to root element
6. Theme toggle button in sidebar persists preference to localStorage

---

## Contract 2: AdminDataTable\<T\>

**Import**: `import { AdminDataTable } from '@/components/admin'`

### Usage Pattern

```tsx
const columns: AdminColumn<PackageRow>[] = [
  { key: 'name', label: 'اسم الباقة', render: (row) => <span>{row.name}</span> },
  { key: 'price', label: 'السعر', render: (row) => <span>{row.price} EGP</span>, align: 'left' },
];

<AdminDataTable
  columns={columns}
  data={packages}
  loading={isLoading}
  pageSize={10}
  emptyMessage="لا توجد باقات."
  rowKey={(row) => row.id}
/>
```

### Behavioral Guarantees

1. Renders table header row with column labels using admin theme colors
2. Renders data rows with hover effect `bg-[var(--admin-hover)]`
3. When `loading=true`, renders `pageSize` skeleton rows
4. When `data` is empty and `loading=false`, renders centered empty message
5. Pagination footer shows "عرض X-Y من أصل Z عنصر"
6. Next/Previous buttons disable at boundaries
7. Page resets to 1 when `data` reference changes

---

## Contract 3: AdminStatCard

**Import**: `import { AdminStatCard } from '@/components/admin'`

### Usage Pattern

```tsx
<AdminStatCard icon={Users} label="الإجمالي" value={1500} variant="light" />
<AdminStatCard icon={Video} label="المرفوعة" value={42} variant="accent" subtitle="فيديو مرتبط" />
<AdminStatCard icon={Shield} label="معلق" value={5} variant="muted">
  <AvatarStack users={pendingUsers} />
</AdminStatCard>
```

### Behavioral Guarantees

1. `light` variant: bordered card, `--admin-card-soft` bg, `--admin-text` value
2. `accent` variant: gold `#775a19` bg, white text, `#fcd386` label
3. `muted` variant: bordered card, `--admin-card-strong` bg, `--admin-text` value
4. Numeric values are auto-formatted with `Intl.NumberFormat('ar-EG')`
5. Icon renders in a tinted circle (light/muted) or glass circle (accent)

---

## Contract 4: AdminModal

**Import**: `import { AdminModal } from '@/components/admin'`

### Usage Pattern

```tsx
<AdminModal
  open={showModal}
  onClose={() => setShowModal(false)}
  title="إضافة باقة"
  subtitle="باقة جديدة"
>
  <form onSubmit={handleSubmit}>
    {/* Form fields */}
  </form>
</AdminModal>
```

### Behavioral Guarantees

1. Renders nothing when `open=false`
2. Entry animation: backdrop fades in, card scales from 96% with 14px y-offset
3. Exit animation: reverse of entry
4. Clicking backdrop area calls `onClose`
5. Close button in header calls `onClose`
6. Modal container: `rounded-[2rem]`, `border border-white/50`, `bg-[#fcf9ef]`, max-width configurable

---

## Contract 5: AdminSearchToolbar

**Import**: `import { AdminSearchToolbar } from '@/components/admin'`

### Usage Pattern

```tsx
<AdminSearchToolbar
  value={search}
  onChange={setSearch}
  placeholder="بحث في الباقات..."
  actions={
    <>
      <button>تصفية</button>
      <button>تصدير</button>
    </>
  }
/>
```

### Behavioral Guarantees

1. Renders full-width search input with search icon on the right side
2. Input uses theme tokens: `--admin-search` bg, `--admin-text` color, `--admin-primary` focus ring
3. `actions` slot renders to the left of (before in RTL) the search input
4. Wrapper has `bg-[#f1eee4]/30` tinted background strip

---

## Contract 6: AdminTabBar\<T\>

**Import**: `import { AdminTabBar } from '@/components/admin'`

### Usage Pattern

```tsx
<AdminTabBar
  tabs={[
    { key: 'all', label: 'الكل' },
    { key: 'Admin', label: 'المديرين' },
    { key: 'Student', label: 'الطلاب', icon: GraduationCap },
  ]}
  activeTab={roleFilter}
  onSelect={setRoleFilter}
/>
```

### Behavioral Guarantees

1. Active tab: `bg-[#5d4300] text-white shadow-*`
2. Inactive tab: `bg-[var(--admin-card-soft)] text-[var(--admin-muted)]`
3. Container: `rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]`
4. Optional icon renders before label text
5. `onSelect` fires with the tab's `key` value
