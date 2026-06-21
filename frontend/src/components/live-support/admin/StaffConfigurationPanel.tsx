import type { ReactNode } from 'react';
export function StaffConfigurationPanel({ children }: { children: ReactNode }) { return <section aria-labelledby="staff-config-title"><h2 id="staff-config-title" className="mb-3 text-lg font-bold text-slate-900">الموظفون والسعة والجداول</h2><div className="space-y-4">{children}</div></section>; }
