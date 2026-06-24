'use client';

import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, LoaderCircle, Play, X } from 'lucide-react';
import {
  liveSupportService,
  type LiveSupportActionDefinition,
} from '@/services/live-support-service';
import { studentActionFields } from './student-action-definitions';

type FieldValue = string | number | boolean;

export function StudentActionsPanel({
  conversationId,
  hasStudent,
  onCompleted,
}: {
  conversationId: string;
  hasStudent: boolean;
  onCompleted: () => void;
}) {
  const [catalog, setCatalog] = useState<LiveSupportActionDefinition[]>([]);
  const [selected, setSelected] = useState<LiveSupportActionDefinition>();
  const [values, setValues] = useState<Record<string, FieldValue>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState('');
  
  const available = useMemo(
    () =>
      catalog.filter((item) =>
        hasStudent
          ? item.key !== 'student.create-and-link'
          : item.key === 'student.create-and-link'
      ),
    [catalog, hasStudent]
  );

  useEffect(() => {
    void liveSupportService.getActionCatalog(conversationId).then(setCatalog);
  }, [conversationId]);

  function choose(action: LiveSupportActionDefinition) {
    setSelected(action);
    setConfirming(false);
    setResult('');
    setFieldErrors({});
    setValues(
      Object.fromEntries(
        (studentActionFields[action.key] ?? []).map((field) => [
          field.key,
          field.type === 'checkbox' ? false : '',
        ])
      )
    );
  }

  function validateForm(): boolean {
    if (!selected) return false;
    const errors: Record<string, string> = {};
    const fields = studentActionFields[selected.key] ?? [];
    
    for (const field of fields) {
      const val = values[field.key];
      if (field.required && (val === undefined || val === null || val === '')) {
        errors[field.key] = 'هذا الحقل مطلوب';
      }
      if (field.type === 'number' && val !== '' && isNaN(Number(val))) {
        errors[field.key] = 'يجب إدخال رقم صحيح';
      }
    }
    
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function execute() {
    if (!selected) return;
    setBusy(true);
    setResult('');
    try {
      const payload = Object.fromEntries(
        Object.entries(values)
          .filter(([, value]) => value !== '')
          .map(([key, value]) => [
            key,
            studentActionFields[selected.key]?.find(
              (field) => field.key === key
            )?.type === 'number'
              ? Number(value)
              : value,
          ])
      );
      const response = await liveSupportService.executeStudentAction<
        Record<string, unknown>,
        { message: string }
      >(
        conversationId,
        selected.key,
        crypto.randomUUID(),
        selected.confirmationVersion,
        payload
      );
      setResult(response.message);
      setConfirming(false);
      onCompleted();
    } catch (cause) {
      setResult(
        (cause as { response?: { data?: { message?: string } } }).response?.data
          ?.message ?? 'تعذر تنفيذ الإجراء.'
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-3">
      <h3 className="font-bold text-slate-900">إجراءات الطالب</h3>
      <p className="mt-1 text-xs text-slate-500">
        كل إجراء يحتاج تأكيدًا ويُسجل باسمك ووقت تنفيذه.
      </p>
      <div className="mt-3 grid grid-cols-2 gap-2">
        {available.map((action) => (
          <button
            key={action.key}
            onClick={() => choose(action)}
            className={`rounded-xl border p-2 text-right text-xs font-semibold ${action.danger === 'financial' ? 'border-amber-300 bg-amber-50 text-amber-900' : action.danger === 'high' ? 'border-red-200 bg-red-50 text-red-800' : 'border-slate-200 text-slate-700 hover:border-cyan-600'}`}
          >
            {action.labelAr}
          </button>
        ))}
      </div>
      {selected && (
        <div
          className="fixed inset-0 z-[120] grid place-items-center bg-slate-950/60 p-4"
          onClick={() => !busy && setSelected(undefined)}
        >
          <div
            role="dialog"
            aria-modal="true"
            onClick={(event) => event.stopPropagation()}
            className="max-h-[90dvh] w-full max-w-lg overflow-y-auto rounded-3xl bg-white p-5"
            dir="rtl"
          >
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-bold">{selected.labelAr}</h3>
                <p className="mt-1 text-xs text-slate-500">{selected.key}</p>
              </div>
              <button
                disabled={busy}
                onClick={() => setSelected(undefined)}
                aria-label="إغلاق"
                className="grid size-10 place-items-center rounded-full hover:bg-slate-100 disabled:opacity-50"
              >
                <X size={18} />
              </button>
            </div>
            {!confirming ? (
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  if (validateForm()) {
                    setConfirming(true);
                  }
                }}
                className="mt-5 space-y-3"
              >
                {(studentActionFields[selected.key] ?? []).map((field) => (
                  <div key={field.key} className="space-y-1">
                    <label
                      className={
                        field.type === 'checkbox'
                          ? 'flex items-center gap-2 text-sm cursor-pointer'
                          : 'block text-sm font-semibold text-slate-700'
                      }
                    >
                      {field.type === 'checkbox' ? (
                        <>
                          <input
                            type="checkbox"
                            disabled={busy}
                            checked={Boolean(values[field.key])}
                            onChange={(event) =>
                              setValues({
                                ...values,
                                [field.key]: event.target.checked,
                              })
                            }
                            className="size-5 rounded border-slate-300 text-cyan-600 focus:ring-cyan-500"
                          />
                          {field.label}
                        </>
                      ) : (
                        <>
                          {field.label}
                          {field.type === 'select' ? (
                            <select
                              disabled={busy}
                              required={field.required}
                              value={String(values[field.key] ?? '')}
                              onChange={(event) =>
                                setValues({
                                  ...values,
                                  [field.key]: event.target.value,
                                })
                              }
                              className={`mt-1 h-11 w-full rounded-xl border px-3 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}
                            >
                              <option value="">اختر</option>
                              {field.options?.map((option) => (
                                <option key={option} value={option}>{option}</option>
                              ))}
                            </select>
                          ) : (
                            <input
                              type={
                                field.type === 'datetime'
                                  ? 'datetime-local'
                                  : field.type
                              }
                              disabled={busy}
                              required={field.required}
                              value={String(values[field.key] ?? '')}
                              onChange={(event) =>
                                setValues({
                                  ...values,
                                  [field.key]: event.target.value,
                                })
                              }
                              className={`mt-1 h-11 w-full rounded-xl border px-3 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}
                            />
                          )}
                        </>
                      )}
                    </label>
                    {fieldErrors[field.key] && (
                      <p className="text-xs font-semibold text-red-600">{fieldErrors[field.key]}</p>
                    )}
                  </div>
                ))}
                <button
                  type="submit"
                  disabled={busy}
                  className="h-11 w-full rounded-xl bg-slate-900 font-semibold text-white hover:bg-slate-800 disabled:bg-slate-300 transition-colors"
                >
                  مراجعة وتأكيد
                </button>
              </form>
            ) : (
              <div className="mt-5">
                <div className={`rounded-2xl border p-4 ${selected.danger === 'financial' ? 'border-amber-200 bg-amber-50 text-amber-900' : selected.danger === 'high' ? 'border-red-200 bg-red-50 text-red-800' : 'border-slate-200 bg-slate-50 text-slate-800'}`}>
                  <AlertTriangle className={`mb-2 ${selected.danger === 'financial' ? 'text-amber-700' : selected.danger === 'high' ? 'text-red-700' : 'text-slate-700'}`} />
                  <h4 className="font-bold">تأكيد التنفيذ: {selected.labelAr}</h4>
                  <p className="mt-2 text-xs">
                    التصنيف: {selected.category}
                  </p>
                  <p className="mt-1 text-xs leading-5">
                    سيتم تنفيذ هذا الإجراء على الطالب المرتبط وتسجيل العملية كاملة. هذا الإجراء ذو خطورة: {selected.danger === 'financial' ? 'مالية' : selected.danger === 'high' ? 'عالية' : 'عادية'}.
                  </p>
                </div>
                <div className="mt-4 flex gap-2">
                  <button
                    disabled={busy}
                    onClick={() => setConfirming(false)}
                    className="h-11 flex-1 rounded-xl border hover:bg-slate-50 disabled:opacity-50"
                  >
                    رجوع
                  </button>
                  <button
                    disabled={busy}
                    onClick={() => void execute()}
                    className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-red-700 font-semibold text-white hover:bg-red-800 disabled:bg-slate-300 transition-colors"
                  >
                    {busy ? (
                      <LoaderCircle className="animate-spin" size={17} />
                    ) : (
                      <Play size={17} />
                    )}
                    {selected.danger === 'financial'
                      ? `تأكيد ${selected.labelAr}`
                      : 'تنفيذ الإجراء'}
                  </button>
                </div>
              </div>
            )}
            {result && (
              <p
                role="status"
                className="mt-3 rounded-xl bg-slate-100 p-3 text-sm font-semibold"
              >
                {result}
              </p>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
