import { CircleCheck, CircleGauge, MessagesSquare, Wifi, WifiOff } from 'lucide-react';
import type { LiveSupportStaffBootstrap } from '@/services/live-support-service';

export function StaffStatusHeader({ state, connected }: { state: LiveSupportStaffBootstrap; connected: boolean }) {
  const items = [
    { label: 'الحضور', value: state.isCheckedIn ? 'مسجل' : 'غير مسجل', good: state.isCheckedIn, icon: CircleCheck },
    { label: 'الاتصال', value: connected ? 'متصل' : 'إعادة اتصال', good: connected, icon: connected ? Wifi : WifiOff },
    { label: 'الحمل', value: `${state.activeLoad} من ${state.capacity} محادثات`, good: state.activeLoad < state.capacity, icon: CircleGauge },
    { label: 'الطابور', value: state.waitingCount ? `${state.waitingCount} بانتظار الدعم` : 'لا أحد ينتظر', good: state.waitingCount === 0, icon: MessagesSquare },
  ];

  return (
    <header aria-label="حالة موظف الدعم" className="flex flex-wrap items-center gap-x-5 gap-y-2 rounded-xl bg-[var(--admin-card-soft)] px-4 py-3">
      {items.map(({ label, value, good, icon: Icon }) => (
        <div key={label} className="flex min-w-fit items-center gap-2 text-sm">
          <Icon aria-hidden="true" size={16} className={good ? 'text-[var(--admin-success)]' : 'text-[var(--admin-warning)]'} />
          <span className="text-[var(--admin-muted)]">{label}</span>
          <strong className="text-[var(--admin-text)]">{value}</strong>
        </div>
      ))}
    </header>
  );
}
