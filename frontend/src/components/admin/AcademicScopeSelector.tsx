'use client';

import { Plus, Trash2 } from 'lucide-react';
import {
  ACADEMIC_SCOPE_LEVEL_LABELS,
  GRADES_BY_STAGE,
  STAGE_OPTIONS,
  type AcademicScopeLevel,
  type AcademicScopePayload,
  type EducationStage,
  type GradeLevel,
} from '@/lib/academic-labels';

interface SubjectOption {
  id: string;
  name: string;
}

interface SubjectEligibilityOption {
  educationStage: EducationStage;
  gradeLevel: GradeLevel;
  subjectId: string;
}

interface AcademicScopeSelectorProps {
  value: AcademicScopePayload[];
  onChange: (value: AcademicScopePayload[]) => void;
  subjects?: SubjectOption[];
  subjectEligibilities?: SubjectEligibilityOption[];
}

const scopeLevels: AcademicScopeLevel[] = ['PlatformWide', 'StageWide', 'GradeAllSubjects', 'Exact'];

function firstGradeForStage(stage?: EducationStage | null): GradeLevel | null {
  if (!stage) return null;
  return GRADES_BY_STAGE[stage]?.[0]?.grades[0]?.value ?? null;
}

function normalizeScope(scope: AcademicScopePayload): AcademicScopePayload {
  if (scope.scopeLevel === 'PlatformWide') return { scopeLevel: 'PlatformWide' };
  if (scope.scopeLevel === 'StageWide') return { scopeLevel: 'StageWide', educationStage: scope.educationStage ?? 'Secondary' };
  const stage = scope.educationStage ?? 'Secondary';
  const grade = scope.gradeLevel ?? firstGradeForStage(stage);
  return {
    scopeLevel: scope.scopeLevel,
    educationStage: stage,
    gradeLevel: grade,
    subjectId: scope.scopeLevel === 'Exact' ? scope.subjectId ?? null : null,
  };
}

export function AcademicScopeSelector({ value, onChange, subjects = [], subjectEligibilities }: AcademicScopeSelectorProps) {
  const getEligibleSubjects = (scope: AcademicScopePayload) => {
    if (!subjectEligibilities || !scope.educationStage || !scope.gradeLevel) return subjects;
    const allowedIds = new Set(subjectEligibilities
      .filter((eligibility) => eligibility.educationStage === scope.educationStage && eligibility.gradeLevel === scope.gradeLevel)
      .map((eligibility) => eligibility.subjectId));
    return subjects.filter((subject) => allowedIds.has(subject.id));
  };

  const updateAt = (index: number, next: AcademicScopePayload) => {
    const copy = [...value];
    const normalized = normalizeScope(next);
    if (normalized.scopeLevel === 'Exact') {
      const availableSubjects = getEligibleSubjects(normalized);
      const subjectStillAvailable = normalized.subjectId
        ? availableSubjects.some((subject) => subject.id === normalized.subjectId)
        : false;
      copy[index] = {
        ...normalized,
        subjectId: subjectStillAvailable
          ? normalized.subjectId
          : availableSubjects.length === 1 ? availableSubjects[0].id : null,
      };
    } else {
      copy[index] = normalized;
    }
    onChange(copy);
  };

  return (
    <div className="space-y-3">
      {value.map((scope, index) => {
        const normalized = normalizeScope(scope);
        const grades = normalized.educationStage ? GRADES_BY_STAGE[normalized.educationStage] ?? [] : [];
        const availableSubjects = getEligibleSubjects(normalized);
        return (
          <div key={`${index}-${normalized.scopeLevel}`} className="grid gap-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 md:grid-cols-[180px_1fr_1fr_1fr_auto]">
            <select className="admin-input" value={normalized.scopeLevel} onChange={(event) => updateAt(index, { scopeLevel: event.target.value as AcademicScopeLevel })}>
              {scopeLevels.map((level) => <option key={level} value={level}>{ACADEMIC_SCOPE_LEVEL_LABELS[level]}</option>)}
            </select>
            {normalized.scopeLevel !== 'PlatformWide' ? (
              <select className="admin-input" value={normalized.educationStage ?? 'Secondary'} onChange={(event) => updateAt(index, { ...normalized, educationStage: event.target.value as EducationStage, gradeLevel: firstGradeForStage(event.target.value as EducationStage) })}>
                {STAGE_OPTIONS.map((stage) => <option key={stage.value} value={stage.value}>{stage.label}</option>)}
              </select>
            ) : <div />}
            {normalized.scopeLevel === 'GradeAllSubjects' || normalized.scopeLevel === 'Exact' ? (
              <select className="admin-input" value={normalized.gradeLevel ?? ''} onChange={(event) => updateAt(index, { ...normalized, gradeLevel: event.target.value as GradeLevel })}>
                {grades.flatMap((group) => group.grades).map((grade) => <option key={grade.value} value={grade.value}>{grade.label}</option>)}
              </select>
            ) : <div />}
            {normalized.scopeLevel === 'Exact' ? (
              <select className="admin-input" value={normalized.subjectId ?? ''} onChange={(event) => updateAt(index, { ...normalized, subjectId: event.target.value || null })}>
                <option value="">{availableSubjects.length ? 'اختر المادة' : 'لا توجد مواد مفعلة لهذا الصف'}</option>
                {availableSubjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
              </select>
            ) : <div />}
            <button type="button" className="admin-btn-icon" title="حذف النطاق" aria-label="حذف النطاق" onClick={() => onChange(value.filter((_, itemIndex) => itemIndex !== index))}>
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        );
      })}
      <button type="button" className="admin-btn-secondary inline-flex items-center gap-2" onClick={() => onChange([...value, { scopeLevel: 'GradeAllSubjects', educationStage: 'Secondary', gradeLevel: 'FirstSecondary' }])}>
        <Plus className="h-4 w-4" />
        إضافة نطاق
      </button>
    </div>
  );
}
