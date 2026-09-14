'use client';
import { useState } from 'react';
import { AdminPage } from '@/components/admin';
import { PayrollRuleEditor, PayrollRunWorkspace } from '@/features/hr/payroll';
export default function HrPayrollPageClient() { const [tab, setTab] = useState<'runs' | 'rules'>('runs'); return <AdminPage activePath="/admin/hr/payroll" sectionLabel="الموارد البشرية" pageTitle="رواتب الموظفين" subtitle="محرك مؤرّخ قابل للتفسير ومراجعة مالية واعتماد نهائي منفصل عن حسابات المدرسين." subNav={<div className="flex gap-2"><button onClick={() => setTab('runs')} className={tab === 'runs' ? 'admin-btn-primary min-h-11' : 'admin-btn-secondary min-h-11'}>الدورات</button><button onClick={() => setTab('rules')} className={tab === 'rules' ? 'admin-btn-primary min-h-11' : 'admin-btn-secondary min-h-11'}>القواعد والمكونات</button></div>}>{tab === 'runs' ? <PayrollRunWorkspace /> : <PayrollRuleEditor />}</AdminPage>; }
