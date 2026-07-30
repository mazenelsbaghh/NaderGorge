'use client';

import { useEffect, useState } from 'react';
import { adminService } from '@/services/admin-service';
import { teacherService, type SubjectDto } from '@/services/teacher-service';
import { Checkbox, Label as CheckboxLabel } from '@/components/ui/checkbox';
import { NumberField } from '@/components/ui/number-field';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { AcademicScopeSelector } from '@/components/admin/AcademicScopeSelector';
import {
  GRADES_BY_STAGE,
  type AcademicScopePayload,
  type AcademicScopeSummary,
  type EducationStage,
  type GradeLevel,
} from '@/lib/academic-labels';

interface PackageDetailsFormProps {
  pkg: {
    id: string;
    name: string;
    description: string;
    price: number;
    isActive: boolean;
    programId?: string;
    targetGrade?: string;
    subjectId?: string;
    subjectName?: string;
    academicScopes?: AcademicScopeSummary[] | null;
  };
  onSuccess?: () => void;
}

function getStageForGrade(grade: string): EducationStage | null {
  for (const [stage, groups] of Object.entries(GRADES_BY_STAGE) as [EducationStage, typeof GRADES_BY_STAGE[EducationStage]][]) {
    if (groups.some((group) => group.grades.some((item) => item.value === grade))) {
      return stage;
    }
  }

  return null;
}

function getInitialScopes(pkg: PackageDetailsFormProps['pkg']): AcademicScopePayload[] {
  if (pkg.academicScopes?.length) {
    return pkg.academicScopes.map((scope) => ({
      scopeLevel: scope.scopeLevel,
      educationStage: scope.educationStage ?? null,
      gradeLevel: scope.gradeLevel ?? null,
      subjectId: scope.subjectId ?? null,
    }));
  }

  if (pkg.targetGrade && pkg.targetGrade !== 'All') {
    const stage = getStageForGrade(pkg.targetGrade);
    if (stage) {
      return [{
        scopeLevel: pkg.subjectId ? 'Exact' : 'GradeAllSubjects',
        educationStage: stage,
        gradeLevel: pkg.targetGrade as GradeLevel,
        subjectId: pkg.subjectId ?? null,
      }];
    }
  }

  return [{ scopeLevel: 'PlatformWide' }];
}

export function PackageDetailsForm({ pkg, onSuccess }: PackageDetailsFormProps) {
  const [name, setName] = useState(pkg.name || '');
  const [description, setDescription] = useState(pkg.description || '');
  const [price, setPrice] = useState(pkg.price || 0);
  const [isActive, setIsActive] = useState(pkg.isActive !== false);
  const [academicScopes, setAcademicScopes] = useState<AcademicScopePayload[]>(() => getInitialScopes(pkg));
  const [subjects, setSubjects] = useState<SubjectDto[]>(pkg.subjectId ? [{ id: pkg.subjectId, name: pkg.subjectName || 'مادة الباقة', description: '' }] : []);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    teacherService.getSubjects()
      .then((response) => setSubjects(response.data ?? []))
      .catch(() => {
        if (pkg.subjectId) {
          setSubjects([{ id: pkg.subjectId, name: pkg.subjectName || 'مادة الباقة', description: '' }]);
        }
      });
  }, [pkg.subjectId, pkg.subjectName]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    if (academicScopes.length === 0) {
      toast.error('اختر المرحلة والصف للباقة.');
      return;
    }

    try {
      setSaving(true);
      await adminService.updatePackage(pkg.id, {
        name,
        description,
        price,
        isActive,
        academicScopes,
      });
      toast.success('تم تحديث بيانات الباقة بنجاح.');
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ أثناء تحديث الباقة، يرجى المحاولة مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div className="space-y-2">
          <label className="text-sm font-bold text-[var(--admin-text)]">اسم الباقة</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="مثال: الباقة التأسيسية"
            required
            className="admin-input"
          />
        </div>
        <div className="space-y-2">
          <NumberField
            minValue={0}
            step={0.1}
            value={price}
            onChange={setPrice}
          >
            <NumberField.Label className="text-sm font-bold text-[var(--admin-text)] mb-2 block">السعر الإجمالي (جنيه مصري)</NumberField.Label>
            <NumberField.Group className="h-[46px]">
              <NumberField.DecrementButton />
              <NumberField.Input />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
      </div>

      <div className="space-y-2">
        <label className="text-sm font-bold text-[var(--admin-text)]">الوصف المفصل</label>
        <textarea
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="اكتب وصفاً مفصلاً يوضح محتوى ومميزات هذه الباقة للطلاب..."
          className="admin-input"
        />
      </div>

      <div className="flex items-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 transition-all hover:bg-[var(--admin-card-soft)] hover:shadow-sm">
        <Checkbox id="isActive" isSelected={isActive} onChange={setIsActive}>
          <Checkbox.Control>
            <Checkbox.Indicator />
          </Checkbox.Control>
          <Checkbox.Content>
            <CheckboxLabel className="cursor-pointer">تفعيل الباقة (تظهر للطلاب للتسجيل)</CheckboxLabel>
          </Checkbox.Content>
        </Checkbox>
      </div>

      <div className="space-y-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
        <div>
          <h4 className="text-sm font-bold text-[var(--admin-text)]">المرحلة والصف</h4>
        </div>
        <AcademicScopeSelector
          value={academicScopes}
          onChange={setAcademicScopes}
          subjects={subjects}
        />
      </div>

      <div className="flex justify-end border-t border-[var(--admin-border)] pt-6">
        <NeumorphButton
          type="submit"
          disabled={saving}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="px-8"
        >
          حفظ التغييرات
        </NeumorphButton>
      </div>
    </form>
  );
}
