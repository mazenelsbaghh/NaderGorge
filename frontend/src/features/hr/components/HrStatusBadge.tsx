const labels: Record<string, string> = {
  Pending: 'قيد المراجعة',
  PendingApproval: 'بانتظار الموافقة',
  Submitted: 'تم الإرسال',
  Approved: 'معتمد',
  FinalApproved: 'معتمد نهائيًا',
  Paid: 'مدفوع',
  Applied: 'تم التطبيق',
  Completed: 'مكتمل',
  Active: 'نشط',
  Open: 'مفتوح',
  Rejected: 'مرفوض',
  Declined: 'مرفوض',
  Cancelled: 'ملغي',
  Canceled: 'ملغي',
  Withdrawn: 'مسحوب',
  Closed: 'مغلق',
  Overdue: 'متأخر',
  Due: 'مستحق',
  Draft: 'مسودة',
  DryRun: 'تمت المحاكاة',
  Reconciled: 'تمت المطابقة',
  Activated: 'مفعّل',
  Failed: 'فشل',
};

function statusTone(status: string) {
  if (/Approved|Paid|Applied|Completed|Active|Activated|Reconciled|Closed/i.test(status)) return 'success';
  if (/Rejected|Declined|Cancelled|Canceled|Overdue|Failed/i.test(status)) return 'danger';
  if (/Pending|Submitted|Due|Open/i.test(status)) return 'warning';
  if (/Withdrawn/i.test(status)) return 'neutral';
  return 'info';
}

export function HrStatusBadge({ status }: { status: string }) {
  return (
    <span className={`hr-status hr-status--${statusTone(status)}`}>
      {labels[status] ?? status}
    </span>
  );
}
