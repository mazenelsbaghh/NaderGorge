export type TeacherAgreementScopeType = 'Default' | 'Package' | 'Term' | 'ContentSection' | 'Lesson' | 'LessonVideo' | 'PublicExam' | 'SharedPackage' | 'CodeGroup';
export type TeacherAgreementTrigger = 'ContentSale' | 'CodeDelivery' | 'CodeActivation' | 'AllSources';
export type TeacherAgreementAllocationMode = 'Percentage' | 'FixedPerSale' | 'FixedPerCode' | 'FixedPerBatch' | 'PlatformFixedPerUnit';
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
  teacherName: string;
  totalEarned: number;
  available: number;
  reserved: number;
  paid: number;
  debt: number;
  netPayable: number;
  retained?: number;
  codeAmountDue?: number;
  codeAmountCollected?: number;
  netBalance: number;
  debtReserved: number;
  unreservedDebt: number;
  todayEarnings: number;
  sourceEarnings: number;
  sourceDifference: number;
  balanceDifference: number;
  sources: Array<{ sourceType: string; count: number; teacherShare: number; platformShare: number }>;
}

export interface TeacherCollection {
  id: string;
  studentName: string;
  amount: number;
  walletLabel: string;
  walletPhoneNumber: string;
  senderPhoneNumber: string;
  resolvedAt: string | null;
  status: 'Matched' | 'Approved';
  transferReference: string | null;
  isVodafoneCash: boolean;
}

export interface TeacherCollections {
  teacherId: string;
  totalAmount: number;
  vodafoneCashAmount: number;
  otherOrUnverifiedAmount: number;
  totalCount: number;
  vodafoneCashCount: number;
  filteredCount: number;
  page: number;
  pageSize: number;
  items: TeacherCollection[];
}

export type TeacherPayoutStatus = 'Unpaid' | 'Reserved' | 'Paid' | 'Reversed' | 'Debt' | string;

export interface TeacherLedgerLine {
  id: string;
  teacherFinancialEventId: string;
  contentNameSnapshot: string;
  teacherShareAmount: number;
  platformShareAmount: number;
  payoutStatus: TeacherPayoutStatus;
  retainedByTeacher?: boolean;
  reviewStatus: string;
  reversedAmount: number;
  agreementId?: string;
  allocationMode: string;
  agreementAllocationMode?: TeacherAgreementAllocationMode;
  allocationValue: number;
  grossBasisAmount: number;
  priceBasis?: TeacherPriceBasis;
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

export interface CodeCollectionInput {
  amount: number;
  treasuryAccountId: string;
  reference: string;
  idempotencyKey: string;
}
export interface CodeBatchAccount {
  id: string;
  teacherId: string;
  name: string;
  totalCodes: number;
  trigger: 'CodeDelivery' | 'CodeActivation';
  recipient?: string;
  started: boolean;
  quote?: {
    unitPrice: number; units: number; gross: number; net: number; teacherShare: number; platformShare: number;
    key: string; agreement: { agreementId?: string; allocationMode: TeacherAgreementAllocationMode; allocationValue: number };
  };
  delivery?: {
    confirmedAt: string; recipient: string; platformAmountDue?: number; teacherRetainedAmount?: number;
    paid: number; remaining?: number;
    payments: Array<{ id: string; amount: number; reference: string; receivedAt: string }>;
  };
}
