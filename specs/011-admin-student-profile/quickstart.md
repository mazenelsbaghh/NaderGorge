# Quickstart: Admin Student Profile

This guide provides the boilerplate for the new Next.js page that serves as the entry point to the Student Profile.

## Frontend Steps

1. **Create the dynamic route file** at `frontend/src/app/admin/users/[id]/page.tsx`
2. **Utilize Shared Components**: Use `AdminShellChrome`, `AdminStatCard`, `AdminTabBar`, `AdminDataTable`, and `AdminModal`.
3. **Draft the Data Service**: Add the new endpoints to `frontend/src/services/admin-service.ts`.

## Minimal Page Scaffold (app/admin/users/[id]/page.tsx)

```tsx
'use client';

import { useState, useEffect } from 'react';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard } from '@/components/admin';
import { adminService } from '@/services/admin-service';

export default function AdminStudentProfile({ params }: { params: { id: string } }) {
  const [activeTab, setActiveTab] = useState<'overview' | 'academic' | 'devices' | 'financials' | 'overrides'>('overview');
  const [studentData, setStudentData] = useState<any>(null);

  const TABS: AdminTab<'overview' | 'academic' | 'devices' | 'financials' | 'overrides'>[] = [
     { key: 'overview', label: 'نظرة عامة' },
     { key: 'academic', label: 'الأكاديمية' },
     { key: 'devices', label: 'الأجهزة' },
     { key: 'overrides', label: 'تجاوزات المشاهدة' },
     { key: 'financials', label: 'الماليات' }
  ];

  useEffect(() => {
    // adminService.getStudentProfile(params.id)
  }, [params.id]);

  return (
    <AdminShellChrome
       activePath="/admin/users"
       sectionLabel="إدارة المستخدمين"
       pageTitle="ملف الطالب"
       subtitle="عرض شامل ومفصل لسجل ونشاط الطالب"
    >
       <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
       
       <div className="mt-8">
         {activeTab === 'overview' && (
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
               <AdminStatCard variant="light" label="أجهزة مسجلة" value="2" />
               <AdminStatCard variant="muted" label="باقات نشطة" value="3" />
            </div>
         )}
         {/* Other tabs go here */}
       </div>
    </AdminShellChrome>
  );
}
```

## Backend Steps

1. Create `GetStudentProfileDetailQuery` and handler using MediatR.
2. Return `StudentProfileExtendedDto` populated from EF Core `.Include()` navigation properties.
3. Configure `AdminAuditLog` in `NaderGorgeDbContext` and add the basic MediatR Event handler for logging state changes.
