'use client';

import { useState } from 'react';
import { ExternalLink, Link2, RefreshCw } from 'lucide-react';
import { mimStudioService, studioError } from './service';
import type { McpConnection } from './contract';

export function HiggsfieldConnection({ connection, onChanged, unsaved = false }: { connection: McpConnection; onChanged: (connection: McpConnection) => void; unsaved?: boolean }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [toolCount, setToolCount] = useState<number | null>(null);
  const run = async (kind: 'connect' | 'disconnect' | 'discover') => {
    setBusy(true); setError('');
    try {
      if (kind === 'connect') {
        const { authorizationUrl } = await mimStudioService.connect();
        const url = new URL(authorizationUrl);
        if (url.origin !== 'https://clerk.higgsfield.ai' || url.pathname !== '/oauth/authorize') throw new Error('Invalid authorization URL');
        sessionStorage.setItem('mim-studio-return', window.location.pathname);
        window.location.assign(url.href);
      } else if (kind === 'disconnect') {
        await mimStudioService.disconnect(); setToolCount(null); onChanged(await mimStudioService.connection());
      } else {
        const tools = await mimStudioService.tools(); setToolCount(tools.length);
      }
    } catch (cause) { setError(studioError(cause)); }
    finally { setBusy(false); }
  };
  return <section className="space-y-3 border-t border-[var(--admin-border)] pt-5" aria-labelledby="mim-higgsfield-heading">
    <h3 id="mim-higgsfield-heading" className="flex items-center gap-2 font-black text-[var(--admin-text)]"><Link2 className="h-4 w-4" />Higgsfield MCP</h3>
    <p className="text-sm leading-7 text-[var(--admin-muted)]">{connection.connected ? 'حسابك مربوط بالاستوديو.' : 'اربط حسابك لاكتشاف أدوات الصور والفيديو المتاحة.'}</p>
    {!connection.configured && <p className="text-sm leading-7 text-amber-800">الربط يحتاج ضبط رابط الرجوع في إعدادات السيرفر.</p>}
    <div className="flex flex-wrap gap-2">
      {connection.connected ? <>
        <button type="button" className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => void run('discover')}><RefreshCw className="h-4 w-4" />فحص الاتصال</button>
        <button type="button" className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => void run('disconnect')}>فصل الربط من المنصة</button>
      </> : <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50" disabled={busy || !connection.configured || unsaved} onClick={() => void run('connect')}><ExternalLink className="h-4 w-4" />{busy ? 'جاري فتح الربط…' : 'ربط حساب Higgsfield'}</button>}
    </div>
    {unsaved && !connection.connected && <p className="text-sm text-[var(--admin-muted)]">احفظ الاسكربت قبل الانتقال لربط حسابك.</p>}
    {toolCount !== null && <p role="status" className="text-sm font-bold text-emerald-800">الاتصال يعمل؛ تم اكتشاف {toolCount} أداة بدون إرسال طلب توليد.</p>}
    {error && <p role="alert" className="text-sm leading-7 text-red-800">{error}</p>}
    <p className="text-xs leading-6 text-[var(--admin-muted)]">بعد حفظ المشهد، اعرض تكلفته من أسفل الاسكربت ثم ابدأ توليده. ربط الحساب وفحصه لا يولّدان فيديوهات.</p>
  </section>;
}
