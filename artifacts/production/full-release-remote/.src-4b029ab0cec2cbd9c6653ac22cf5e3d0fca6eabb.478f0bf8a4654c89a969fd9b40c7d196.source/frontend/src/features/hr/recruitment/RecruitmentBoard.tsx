'use client';

import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Loader2, Plus, UserPlus } from 'lucide-react';
import toast from 'react-hot-toast';
import { hrRecruitmentService, RequisitionDto } from '@/services/hr-recruitment-service';
import { cairoDateAfterDays } from '@/lib/cairo-time';

const stages = [
  { key: 'Applied', label: 'متقدم' },
  { key: 'Screening', label: 'فرز' },
  { key: 'Interview', label: 'مقابلة' },
  { key: 'Offer', label: 'عرض وظيفي' },
  { key: 'Hired', label: 'تم التعيين' },
];

const emptyRequisition = { title: '', openings: 1, requirements: '' };
const emptyCandidate = {
  requisitionId: '',
  fullName: '',
  phoneNumber: '',
  email: '',
  cvAssetReference: '',
};

export function RecruitmentBoard() {
  const [rows, setRows] = useState<RequisitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [requisition, setRequisition] = useState(emptyRequisition);
  const [candidate, setCandidate] = useState(emptyCandidate);
  const [temporaryPasswords, setTemporaryPasswords] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const requisitions = await hrRecruitmentService.board();
      setRows(requisitions);
      setCandidate((current) => ({ ...current, requisitionId: current.requisitionId || requisitions[0]?.id || '' }));
    } catch {
      toast.error('تعذر تحميل التوظيف');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function addRequisition(event: FormEvent) {
    event.preventDefault();
    try {
      await hrRecruitmentService.createRequisition({ ...requisition, organizationUnitId: null });
      toast.success('تم فتح الاحتياج');
      setRequisition(emptyRequisition);
      await load();
    } catch {
      toast.error('تعذر فتح الاحتياج');
    }
  }

  async function addCandidate(event: FormEvent) {
    event.preventDefault();
    try {
      await hrRecruitmentService.addCandidate(candidate.requisitionId, candidate);
      toast.success('تمت إضافة المرشح');
      setCandidate((current) => ({ ...emptyCandidate, requisitionId: current.requisitionId }));
      await load();
    } catch {
      toast.error('تعذر إضافة المرشح');
    }
  }

  async function issueOffer(candidateId: string) {
    try {
      const proposedStartDate = cairoDateAfterDays(14);
      await hrRecruitmentService.createOffer(candidateId, { baseSalary: 7000, currency: 'EGP', proposedStartDate });
      toast.success('تم إصدار العرض');
      await load();
    } catch {
      toast.error('تعذر إصدار العرض');
    }
  }

  async function acceptAndHire(candidateId: string, offerId: string, version: number) {
    const temporaryPassword = temporaryPasswords[candidateId] ?? '';
    if (temporaryPassword.length < 8) {
      toast.error('أدخل كلمة مرور مؤقتة من 8 أحرف على الأقل');
      return;
    }

    try {
      await hrRecruitmentService.acceptOffer(offerId, version);
      await hrRecruitmentService.hire(candidateId, offerId, temporaryPassword);
      setTemporaryPasswords((current) => ({ ...current, [candidateId]: '' }));
      toast.success('تم إنشاء الحساب وملف الموظف معًا');
      await load();
    } catch {
      toast.error('تعذر إتمام التعيين');
    }
  }

  if (loading) {
    return <div className="admin-panel py-16 text-center"><Loader2 className="mx-auto animate-spin" /></div>;
  }

  const candidates = rows.flatMap((row) => row.candidates.map((candidateRow) => ({ ...candidateRow, requisition: row.title })));

  return (
    <div className="space-y-5">
      <div className="grid gap-5 xl:grid-cols-2">
        <form onSubmit={addRequisition} className="admin-panel">
          <h2 className="font-black">احتياج وظيفي</h2>
          <div className="mt-4 grid gap-3">
            <input required aria-label="المسمى الوظيفي" placeholder="المسمى الوظيفي" value={requisition.title} onChange={(event) => setRequisition({ ...requisition, title: event.target.value })} className="admin-input" />
            <input required aria-label="عدد الوظائف" type="number" min="1" value={requisition.openings} onChange={(event) => setRequisition({ ...requisition, openings: Number(event.target.value) })} className="admin-input" />
            <textarea required aria-label="متطلبات الوظيفة" placeholder="المتطلبات" value={requisition.requirements} onChange={(event) => setRequisition({ ...requisition, requirements: event.target.value })} className="admin-input min-h-20" />
          </div>
          <button className="admin-btn-primary mt-4 inline-flex min-h-11 items-center gap-2"><Plus className="h-4 w-4" />فتح الاحتياج</button>
        </form>

        <form onSubmit={addCandidate} className="admin-panel">
          <h2 className="font-black">مرشح جديد</h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            <select aria-label="الاحتياج الوظيفي" value={candidate.requisitionId} onChange={(event) => setCandidate({ ...candidate, requisitionId: event.target.value })} className="admin-input sm:col-span-2">
              {rows.map((row) => <option value={row.id} key={row.id}>{row.title}</option>)}
            </select>
            <input required aria-label="اسم المرشح" placeholder="الاسم" value={candidate.fullName} onChange={(event) => setCandidate({ ...candidate, fullName: event.target.value })} className="admin-input" />
            <input required aria-label="هاتف المرشح" placeholder="رقم الهاتف" value={candidate.phoneNumber} onChange={(event) => setCandidate({ ...candidate, phoneNumber: event.target.value })} className="admin-input" />
            <input aria-label="بريد المرشح" type="email" placeholder="البريد" value={candidate.email} onChange={(event) => setCandidate({ ...candidate, email: event.target.value })} className="admin-input" />
            <input aria-label="مرجع السيرة الذاتية" placeholder="مرجع CV" value={candidate.cvAssetReference} onChange={(event) => setCandidate({ ...candidate, cvAssetReference: event.target.value })} className="admin-input" />
          </div>
          <button className="admin-btn-primary mt-4 inline-flex min-h-11 items-center gap-2"><UserPlus className="h-4 w-4" />إضافة المرشح</button>
        </form>
      </div>

      <div className="grid gap-4 overflow-x-auto pb-2 lg:grid-cols-5">
        {stages.map((stage) => (
          <section key={stage.key} className="min-w-[240px] rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
            <h2 className="px-2 py-2 font-black">{stage.label}</h2>
            <div className="space-y-3">
              {candidates.filter((candidateRow) => candidateRow.stage === stage.key).map((candidateRow) => (
                <article key={candidateRow.id} className="rounded-2xl bg-[var(--admin-card)] p-4 shadow-sm">
                  <b>{candidateRow.fullName}</b>
                  <p className="mt-1 text-xs text-[var(--admin-muted)]">{candidateRow.requisition} · {candidateRow.phoneNumber}</p>
                  {stage.key !== 'Offer' && stage.key !== 'Hired' && (
                    <button onClick={() => void issueOffer(candidateRow.id)} className="admin-btn-secondary mt-3 min-h-11 w-full">إصدار عرض 7000</button>
                  )}
                  {stage.key === 'Offer' && candidateRow.offers.map((offerRow) => (
                    <div key={offerRow.id} className="mt-3 space-y-2">
                      <label className="block text-xs font-bold">
                        كلمة مرور مؤقتة
                        <input type="password" autoComplete="new-password" minLength={8} value={temporaryPasswords[candidateRow.id] ?? ''} onChange={(event) => setTemporaryPasswords((current) => ({ ...current, [candidateRow.id]: event.target.value }))} className="admin-input mt-1" />
                      </label>
                      <button onClick={() => void acceptAndHire(candidateRow.id, offerRow.id, offerRow.version)} className="admin-btn-primary min-h-11 w-full">قبول وتعيين</button>
                    </div>
                  ))}
                </article>
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
