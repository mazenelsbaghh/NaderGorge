export type TeacherAgreementScopeType = 'Default' | 'Package' | 'Term' | 'ContentSection' | 'Lesson' | 'LessonVideo' | 'PublicExam' | 'SharedPackage' | 'CodeGroup';
export type TeacherAgreementTrigger = 'ContentSale' | 'CodeDelivery' | 'CodeActivation';
export type TeacherAgreementAllocationMode = 'Percentage' | 'FixedPerSale' | 'FixedPerCode' | 'FixedPerBatch';
export type TeacherPriceBasis = 'Gross' | 'NetAfterDiscount';

export interface TeacherAgreement {
  id: string;
  teacherId: string;
  scopeType: TeacherAgreementScopeType;
  scopeId?: string;
  trigger: TeacherAgreementTrigger;
  allocationMode: TeacherAgreementAllocationMode;
  allocationValue: number;
  priceBasis: TeacherPriceBasis;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
  reason: string;
}

export interface TeacherFinanceSummary {
  teacherId: string;
  totalEarned: number;
  available: number;
  reserved: number;
  paid: number;
  debt: number;
  netPayable: number;
}

export type TeacherPayoutStatus = 'Unpaid' | 'Reserved' | 'Paid' | 'Reversed' | 'Debt' | string;

export interface TeacherLedgerLine {
  id: string;
  teacherFinancialEventId: string;
  contentNameSnapshot: string;
  teacherShareAmount: number;
  platformShareAmount: number;
  payoutStatus: TeacherPayoutStatus;
  reviewStatus: string;
  reversedAmount: number;
  agreementId?: string;
  occurredAt: string;
  sourceType: string;
  grossAmount: number;
  discountAmount: number;
  platformDiscountAmount: number;
  teacherDiscountAmount: number;
}

export interface PagedTeacherLedger {
  items: TeacherLedgerLine[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SettlementPreview {
  error?: string | null;
  allocations: TeacherLedgerLine[];
  adjustments: Array<{ id: string; amount: number; reason: string }>;
  grossDueAmount: number;
  debtDeductionAmount: number;
  netPayableAmount: number;
}

export interface TeacherSettlementLine {
  id: string;
  allocationId?: string;
  adjustmentId?: string;
  amount: number;
  descriptionSnapshot: string;
}

export interface TeacherSettlement {
  id: string;
  teacherId: string;
  periodFrom: string;
  periodTo: string;
  currency: string;
  status: 'Draft' | 'Reviewed' | 'Approved' | 'Paid' | 'Cancelled' | string;
  grossDueAmount: number;
  debtDeductionAmount: number;
  netPayableAmount: number;
  note?: string;
  lines: TeacherSettlementLine[];
  payments: Array<{ id: string; amount: number; paymentMethod: string; transferReference: string; attachmentUrl?: string; paidAt?: string }>;
}

export interface CodeGroupFinancialTerms {
  trigger: Extract<TeacherAgreementTrigger, 'CodeDelivery' | 'CodeActivation'>;
  agreementId?: string;
  recipient?: string;
}
