'use client';

import type { TeacherSummaryReport } from '@/services/teacher-course-summary-service';

export function TeacherSummaryResults({ report, busy, onPage }: {
  report: TeacherSummaryReport; busy: boolean; onPage: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(report.totalStudents / report.pageSize));
  const identity = report.columns.filter(column => column.key.startsWith('identity'));
  const lessons = report.columns.filter(column => !column.key.startsWith('identity'));
  return <section aria-label="نتيجة ملخص الطلاب" aria-busy={busy} className="min-w-0 space-y-4 text-[var(--admin-text)]">
    <h3 className="text-lg font-bold">نتيجة التقرير <span className="text-sm font-normal text-[var(--admin-muted)]">{report.totalStudents} طالب</span></h3>
    {!report.rows.length ? <p role="status" className="py-8 text-center">لا يوجد طلاب مطابقون. جرّب كل الصفوف أو كل الكورسات.</p> : <>
      <div className="space-y-4 md:hidden">{report.rows.map(row => <article key={row.id} className="rounded-xl border border-[var(--admin-border)] p-4">
        <h4 className="font-bold">{row.identity0}</h4>
        <dl className="mt-3 space-y-2 text-sm">{identity.slice(1).map(column => <div key={column.key} className="flex flex-wrap justify-between gap-x-4 gap-y-1"><dt className="text-[var(--admin-muted)]">{column.label}</dt><dd className="max-w-full break-words"><bdi>{row[column.key]}</bdi></dd></div>)}</dl>
        {!!lessons.length && <details className="mt-4 border-t border-[var(--admin-border)] pt-3"><summary className="min-h-11 cursor-pointer font-bold">الحصص والمشاهدة والدرجات</summary><dl className="space-y-3 text-sm">{lessons.map(column => <div key={column.key}><dt className="text-[var(--admin-muted)]">{column.label}</dt><dd className="mt-1 whitespace-pre-line font-bold">{row[column.key]}</dd></div>)}</dl></details>}
      </article>)}</div>
      <div role="region" aria-label="جدول بيانات الطلاب، قابل للتمرير أفقيًا" tabIndex={0} className="hidden max-w-full overflow-x-auto rounded-xl border border-[var(--admin-border)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] md:block">
        <table className="w-full text-right text-sm"><caption className="sr-only">بيانات الطلاب كاملة ومتابعة الحصص حسب الاختيارات</caption><thead className="bg-[var(--admin-card-soft)]"><tr>{report.columns.map(column => <th key={column.key} scope="col" className="min-w-40 max-w-72 px-4 py-3 align-top font-bold">{column.label}</th>)}</tr></thead><tbody className="divide-y divide-[var(--admin-border)]">{report.rows.map(row => <tr key={row.id} className="hover:bg-[var(--admin-hover)]">{report.columns.map(column => <td key={column.key} className="max-w-72 whitespace-pre-line break-words px-4 py-3 align-top"><bdi>{row[column.key]}</bdi></td>)}</tr>)}</tbody></table>
      </div>
    </>}
    {pages > 1 && <nav aria-label="صفحات ملخص الطلاب" className="flex items-center justify-between gap-3 text-sm"><button type="button" disabled={busy || report.page <= 1} onClick={() => onPage(report.page - 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-50">السابق</button><span>صفحة {report.page} من {pages}</span><button type="button" disabled={busy || report.page >= pages} onClick={() => onPage(report.page + 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-50">التالي</button></nav>}
  </section>;
}
