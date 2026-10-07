'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { AdminPage } from '@/components/admin';
import { mimStudioService, studioError } from '@/features/mim-studio/service';

export default function MimStudioConnectPage() {
  const captured = useRef(false);
  const [callback, setCallback] = useState<{ code: string; state: string; issuer: string | null } | null>(null);
  const [returnPath, setReturnPath] = useState('/admin/content');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [complete, setComplete] = useState(false);
  useEffect(() => {
    if (captured.current) return;
    captured.current = true;
    const params = new URLSearchParams(window.location.search);
    window.history.replaceState(null, '', window.location.pathname);
    const previous = sessionStorage.getItem('mim-studio-return');
    sessionStorage.removeItem('mim-studio-return');
    if (previous && /^\/admin\/content\/lessons\/[a-f0-9-]{36}$/i.test(previous)) setReturnPath(`${previous}?tab=mim-studio`);
    const code = params.get('code'), state = params.get('state');
    if (params.has('error')) setError('ربط الحساب لم يكتمل. ارجع للحصة وحاول مرة أخرى.');
    else if (!code || !state) setError('ابدأ ربط حساب Higgsfield من داخل الحصة.');
    else setCallback({ code, state, issuer: params.get('iss') });
  }, []);
  const finish = async () => {
    if (!callback) return;
    setBusy(true); setError('');
    try { await mimStudioService.complete(callback.code, callback.state, callback.issuer); setComplete(true); setCallback(null); }
    catch (cause) { setError(studioError(cause)); setCallback(null); }
    finally { setBusy(false); }
  };
  return <AdminPage activePath="/admin/content" sectionLabel="استوديو ميم" pageTitle="ربط حساب Higgsfield" subtitle="حساب التوليد الخاص بك">
    <div className="admin-panel mx-auto max-w-xl space-y-5 p-6" dir="rtl">
      <p className="text-base leading-8 text-[var(--admin-text)]">{complete ? 'تم ربط حسابك. ارجع للحصة لفحص الأدوات المتاحة.' : 'أكمل الربط بحساب الإدارة الحالي، ثم ارجع لاسكربت الحصة.'}</p>
      {error && <p role="alert" className="rounded-lg bg-red-50 p-4 text-sm text-red-800">{error}</p>}
      {callback && <button type="button" disabled={busy} onClick={() => void finish()} className="admin-btn-primary min-h-11">{busy ? 'جاري إكمال الربط…' : 'إكمال ربط الحساب'}</button>}
      <Link href={returnPath} className="admin-btn-ghost min-h-11">العودة للحصة</Link>
    </div>
  </AdminPage>;
}
