import Link from 'next/link';
import { ArrowRight } from 'lucide-react';

type EmployeePageShellProps = {
  title: string;
  description: string;
  children: React.ReactNode;
  compact?: boolean;
  home?: boolean;
};

export function EmployeePageShell({
  title,
  description,
  children,
  compact = false,
  home = false,
}: EmployeePageShellProps) {
  return (
    <main className={`hr-page ${compact ? 'hr-page--compact' : ''}`} dir="rtl">
      <header className="hr-page-header">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="hr-brand-mark">مسار · بوابة الموظف</p>
          {!home && (
            <Link
              href="/employee"
              className="admin-btn-ghost min-h-11"
              aria-label="العودة إلى بوابة الموظف"
            >
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
              كل الخدمات
            </Link>
          )}
        </div>
        <h1 className="hr-page-title">{title}</h1>
        <p className="hr-page-description">{description}</p>
      </header>
      {children}
    </main>
  );
}
