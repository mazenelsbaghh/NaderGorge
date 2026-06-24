'use client';

import { useEffect, useState } from 'react';
import { getLiveSupportApiError, liveSupportService } from '@/services/live-support-service';
import { UserPlus, LoaderCircle, CheckCircle } from 'lucide-react';

interface AISecureRegistrationFormProps {
  conversationId: string;
  decisionId: string;
  onSuccess: () => void;
}

export function AISecureRegistrationForm({ conversationId, decisionId, onSuccess }: AISecureRegistrationFormProps) {
  const [busy, setBusy] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState('');

  // Form Fields
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [gender, setGender] = useState<'Male' | 'Female'>('Male');
  const [governorate, setGovernorate] = useState('');
  const [address, setAddress] = useState('');
  const [educationStage, setEducationStage] = useState('Secondary');
  const [gradeLevel, setGradeLevel] = useState('ThirdSecondary');
  const [schoolName, setSchoolName] = useState('');
  const [parentPhoneNumber, setParentPhoneNumber] = useState('');
  useEffect(() => () => setPassword(''), []);

  const stages = [
    { value: 'Primary', label: 'الابتدائية' },
    { value: 'Preparatory', label: 'الإعدادية' },
    { value: 'Secondary', label: 'الثانوية' }
  ];

  const getGradesForStage = (stage: string) => {
    switch (stage) {
      case 'Primary':
        return [{ value: 'Grade6', label: 'الصف السادس الابتدائي' }];
      case 'Preparatory':
        return [
          { value: 'Grade7', label: 'الصف الأول الإعدادي' },
          { value: 'Grade8', label: 'الصف الثاني الإعدادي' },
          { value: 'Grade9', label: 'الصف الثالث الإعدادي' }
        ];
      case 'Secondary':
      default:
        return [
          { value: 'FirstSecondary', label: 'الصف الأول الثانوي' },
          { value: 'SecondSecondary', label: 'الصف الثاني الثانوي' },
          { value: 'ThirdSecondary', label: 'الصف الثالث الثانوي' }
        ];
    }
  };

  const handleStageChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    setEducationStage(val);
    const grades = getGradesForStage(val);
    if (grades.length > 0) {
      setGradeLevel(grades[0].value);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError('');

    // Validations
    if (!fullName.trim() || fullName.trim().split(/\s+/).length < 4) {
      setError('يرجى إدخال الاسم الرباعي.');
      setBusy(false);
      return;
    }
    if (phoneNumber.trim().length < 10) {
      setError('رقم الهاتف غير صالح.');
      setBusy(false);
      return;
    }
    if (password.length < 8) {
      setError('كلمة المرور يجب أن لا تقل عن 8 أحرف.');
      setBusy(false);
      return;
    }
    if (parentPhoneNumber.trim() === phoneNumber.trim()) {
      setError('رقم هاتف ولي الأمر يجب أن يختلف عن رقم الطالب.');
      setBusy(false);
      return;
    }

    try {
      await liveSupportService.confirmAIRegistration(conversationId, {
        decisionId,
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
        dateOfBirth,
        gender,
        governorate: governorate.trim(),
        address: address.trim(),
        educationStage,
        gradeLevel,
        schoolName: schoolName.trim(),
        parentPhoneNumber: parentPhoneNumber.trim()
      });
      setPassword('');
      setSuccess(true);
      setTimeout(() => {
        onSuccess();
      }, 2000);
    } catch (error) {
      setError(getLiveSupportApiError(error, 'فشل إنشاء الحساب. راجع البيانات وحاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  };

  if (success) {
    return (
      <div dir="rtl" className="my-3 rounded-2xl border border-green-100 bg-green-50/50 p-4 text-center shadow-sm">
        <span className="mx-auto mb-2 grid size-12 place-items-center rounded-full bg-green-100 text-green-700">
          <CheckCircle size={24} />
        </span>
        <h4 className="text-sm font-bold text-green-900">تم تسجيل الحساب بنجاح</h4>
        <p className="mt-1 text-xs text-green-800">
          جاري ربط حسابك الجديد وتحديث المحادثة...
        </p>
      </div>
    );
  }

  return (
    <div dir="rtl" className="my-3 rounded-2xl border border-cyan-100 bg-cyan-50/50 p-4 shadow-sm">
      <div className="flex items-start gap-3 mb-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-100 text-cyan-800">
          <UserPlus size={20} />
        </span>
        <div>
          <h4 className="text-xs font-semibold text-cyan-900">إنشاء حساب جديد</h4>
          <p className="mt-1 text-xs text-slate-600 leading-relaxed">
            املأ بيانات الحساب بالأسفل لإنشاء حساب طالب مسجل وربطه بالمحادثة فوراً.
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-3">
        <label className="block">
          <span className="text-xs font-medium text-slate-700">الاسم ثلاثي أو رباعي</span>
          <input
            required
            disabled={busy}
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="الاسم الكامل باللغة العربية"
            className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
          />
        </label>

        <div className="grid grid-cols-2 gap-2">
          <label className="block">
            <span className="text-xs font-medium text-slate-700">رقم الهاتف</span>
            <input
              required
              disabled={busy}
              type="tel"
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              placeholder="01xxxxxxxxx"
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-slate-700">كلمة المرور</span>
            <input
              required
              disabled={busy}
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="كلمة مرور قوية"
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <label className="block">
            <span className="text-xs font-medium text-slate-700">تاريخ الميلاد</span>
            <input required disabled={busy} type="date" value={dateOfBirth} onChange={(event) => setDateOfBirth(event.target.value)} autoComplete="bday" className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-2 text-xs outline-none focus:border-cyan-600" />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-slate-700">النوع</span>
            <select disabled={busy} value={gender} onChange={(event) => setGender(event.target.value as 'Male' | 'Female')} className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-2 text-xs outline-none focus:border-cyan-600">
              <option value="Male">ذكر</option><option value="Female">أنثى</option>
            </select>
          </label>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <label className="block">
            <span className="text-xs font-medium text-slate-700">المرحلة الدراسية</span>
            <select
              disabled={busy}
              value={educationStage}
              onChange={handleStageChange}
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-2 text-xs outline-none focus:border-cyan-600"
            >
              {stages.map((stage) => (
                <option key={stage.value} value={stage.value}>
                  {stage.label}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="text-xs font-medium text-slate-700">الصف الدراسي</span>
            <select
              disabled={busy}
              value={gradeLevel}
              onChange={(e) => setGradeLevel(e.target.value)}
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-2 text-xs outline-none focus:border-cyan-600"
            >
              {getGradesForStage(educationStage).map((grade) => (
                <option key={grade.value} value={grade.value}>
                  {grade.label}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <label className="block">
            <span className="text-xs font-medium text-slate-700">المحافظة</span>
            <input
              required
              disabled={busy}
              value={governorate}
              onChange={(e) => setGovernorate(e.target.value)}
              placeholder="مثال: القاهرة"
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-slate-700">اسم المدرسة</span>
            <input
              required
              disabled={busy}
              value={schoolName}
              onChange={(e) => setSchoolName(e.target.value)}
              placeholder="اسم المدرسة بالكامل"
              className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
            />
          </label>
        </div>

        <label className="block">
          <span className="text-xs font-medium text-slate-700">العنوان</span>
          <input required disabled={busy} value={address} onChange={(event) => setAddress(event.target.value)} autoComplete="street-address" className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600" />
        </label>

        <label className="block">
          <span className="text-xs font-medium text-slate-700">رقم هاتف ولي الأمر</span>
          <input
            required
            disabled={busy}
            type="tel"
            value={parentPhoneNumber}
            onChange={(e) => setParentPhoneNumber(e.target.value)}
            placeholder="يجب أن يختلف عن رقم هاتفك"
            className="mt-1 h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
          />
        </label>

        {error && (
          <p className="text-xs font-medium text-red-600 leading-normal" role="alert">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={busy}
          className="h-10 w-full rounded-xl bg-cyan-700 text-xs font-bold text-white hover:bg-cyan-800 disabled:opacity-50 transition-colors flex items-center justify-center gap-1.5"
        >
          {busy && <LoaderCircle size={14} className="animate-spin" />}
          <span>إنشاء الحساب وتأكيده</span>
        </button>
      </form>
    </div>
  );
}
