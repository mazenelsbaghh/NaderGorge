import apiClient from './api-client';
import { createClientId } from '@/lib/client-id';

export interface ApiResponse<T = any> {
  data: T;
  success: boolean;
  message: string;
}

export interface EmployeeProfileDto {
  id: string;
  employeeNumber: string;
  employmentStatus: string;
  hireDate: string;
  terminationDate?: string | null;
  workMode: string;
  userId: string;
  basicSalary: number;
  standardStartTime: string; // "hh:mm:ss" or "hh:mm"
  targetDailyHours: number;
  dailyBreakAllowanceMinutes?: number;
  shortPermissionMaxMinutes?: number;
  dailyShortPermissionAllowanceMinutes?: number;
  updatedAt?: string | null;
  rowVersion?: string | null;
}

export interface ProvisionEmployeePayload {
  fullName: string;
  phoneNumber: string;
  password: string;
  role: string;
  basicSalary: number;
  standardStartTime: string;
  targetDailyHours: number;
  shiftTemplateId?: string;
  shiftEffectiveFrom?: string;
}

export interface ProvisionEmployeeResult {
  employeeId: string;
  employeeNumber: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  role: string;
  updatedAt?: string | null;
}

export interface EmployeeDto {
  id: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  roles: string[];
  employeeProfile?: EmployeeProfileDto;
  hasProfile?: boolean;
  rowVersion?: string | null;
}

export interface EmployeeProfileMutationResult {
  id: string;
  userId: string;
  updatedAt?: string | null;
  rowVersion?: string | null;
}

export interface OrganizationUnitDto {
  id: string;
  code: string;
  name: string;
  type: string;
  parentId?: string | null;
  managerEmployeeId?: string | null;
  isActive: boolean;
}

export interface EmploymentAssignmentDto {
  id: string;
  organizationUnitId: string;
  organizationUnit: string;
  position?: string | null;
  grade?: string | null;
  manager?: string | null;
  location?: string | null;
  costCenter?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  changeReason: string;
}

export interface EmploymentContractDto {
  id: string;
  contractNumber: string;
  type: string;
  status: string;
  startDate: string;
  endDate?: string | null;
  probationEndDate?: string | null;
  currency: string;
  termsVersion: number;
}

export interface EmployeeDetailDto {
  id: string;
  employeeNumber: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  employmentStatus: string;
  hireDate: string;
  terminationDate?: string | null;
  workMode: string;
  standardStartTime: string;
  targetDailyHours: number;
  assignments: EmploymentAssignmentDto[];
  contracts: EmploymentContractDto[];
}

export interface WorkCalendarDto { id: string; code: string; name: string; timeZoneId: string; workingDaysMask: number; }
export interface ShiftSegmentDto { id?: string; sequence: number; dayOfWeek?: number | null; startsAt: string; endsAt: string; unpaidBreakMinutes: number; workDateRule: string; }
export interface ShiftTemplateDto { id: string; code: string; name: string; mode: string; workCalendarId: string; graceMinutes: number; minimumBreakMinutes: number; overtimeAfterMinutes: number; version: number; segments: ShiftSegmentDto[]; }
export interface ShiftAssignmentDto { id: string; employeeId: string; employee: string; shiftTemplateId: string; shift: string; effectiveFrom: string; effectiveTo?: string | null; status: string; reason: string; segments: ShiftSegmentDto[]; }
export interface ShiftAssignmentPayload { employeeId: string; shiftTemplateId: string; effectiveFrom: string; effectiveTo?: string | null; reason: string; }
export type AttendancePolicyKind = 'Unrestricted' | 'Geofence' | 'TrustedDevice';
export interface AttendancePolicyDto {
  id: string; code: string; name: string; kind: AttendancePolicyKind; latitude?: number | null; longitude?: number | null;
  radiusMeters: number; maximumAccuracyMeters: number; isActive: boolean;
}
export interface AttendancePolicyAssignmentDto {
  id: string; attendancePolicyId: string; policy: string; employeeId?: string | null; employee?: string | null;
  shiftTemplateId?: string | null; shift?: string | null; effectiveFrom: string; effectiveTo?: string | null;
}
export interface AttendancePolicyConfigurationDto {
  policies: AttendancePolicyDto[];
  assignments: AttendancePolicyAssignmentDto[];
}
export type AttendanceBreakKind = 'Regular' | 'ShortPermission';
export interface AttendanceBreakDto { id: string; startedAt: string; endedAt?: string | null; kind: AttendanceBreakKind; allowedMinutes: number; }
export interface AttendanceSessionDto { id: string; workDate: string; clockedInAt: string; clockedOutAt?: string | null; state: string; workedMinutes: number; lateMinutes: number; earlyLeaveMinutes: number; overtimeMinutes: number; breakAllowanceMinutes?: number; shortPermissionMaxMinutes?: number; dailyShortPermissionAllowanceMinutes?: number; breaks?: AttendanceBreakDto[]; }
export interface AdminBreakSessionDto { id: string; employeeId: string; employee: string; employeePhone: string; workDate: string; clockedInAt: string; clockedOutAt?: string | null; state: string; workedMinutes: number; lateMinutes: number; earlyLeaveMinutes: number; overtimeMinutes: number; breakAllowanceMinutes: number; shortPermissionMaxMinutes: number; openBreak?: { id: string; startedAt: string; kind: AttendanceBreakKind; allowedMinutes: number } | null; }
export interface AdminDailyAttendanceReportDto { employeeId: string; employee: string; employeePhone: string; workDate: string; clockedInAt: string; clockedOutAt?: string | null; workedMinutes: number; lateMinutes: number; earlyLeaveMinutes: number; overtimeMinutes: number; hasOpenSession: boolean; }
export interface AttendanceCorrectionDto { id: string; employeeId: string; employee: string; attendanceSessionId: string; proposedClockedInAt?: string | null; proposedClockedOutAt?: string | null; reason: string; evidenceReference?: string | null; state: string; beforeJson: string; appliedJson?: string | null; version: number; }
export interface LeaveTypeDto { id: string; code: string; name: string; isPaid: boolean; requiresAttachment: boolean; allowsHalfDay: boolean; }
export interface LeaveBalanceDto { id: string; leaveTypeId: string; leaveType: string; year: number; granted: number; carried: number; reserved: number; used: number; available: number; }
export interface LeaveRequestDto { id: string; leaveTypeId: string; leaveType: string; startDate: string; endDate: string; dayFraction: number; workdays: number; reason: string; attachmentReference?: string | null; state: string; approvalInstanceId?: string | null; version: number; employee?: string; }
export interface ApprovalInboxDto {
  id: string;
  approvalInstanceId: string;
  order: number;
  dueAt: string;
  escalationLevel: number;
  requestType: string;
  requestId: string;
  instanceVersion: number;
  requester: string;
  step: string;
  leaveType?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  dayFraction?: number | null;
  workdays?: number | null;
  reason?: string | null;
  availableLeaveBalance?: number | null;
}
export interface LeavePolicyDto { id: string; name: string; leaveTypeId: string; leaveType: string; annualEntitlement: number; maximumCarryover: number; allowNegativeBalance: boolean; effectiveFrom: string; effectiveTo?: string | null; workCalendarId: string; }

export interface SaveEmployeeProfilePayload {
  userId: string;
  basicSalary: number;
  standardStartTime: string;
  targetDailyHours: number;
  dailyBreakAllowanceMinutes?: number;
  shortPermissionMaxMinutes?: number;
  dailyShortPermissionAllowanceMinutes?: number;
  expectedUpdatedAt?: string | null;
}

export interface AttendanceLogDto {
  id: string;
  date: string; // "yyyy-MM-dd"
  clockIn: string; // DateTime ISO
  clockOut?: string; // DateTime ISO
  lateMinutes: number;
  status: string; // "Present", "Late", "Absent", "Sick", "Leave"
  ipAddress: string;
  userAgent: string;
  durationMinutes?: number;
}

export interface AdminAttendanceLogDto extends AttendanceLogDto {
  employeeId: string;
  employeeName: string;
  employeePhone: string;
}

export interface MyAttendanceStatusDto {
  hasProfile: boolean;
  logs: AttendanceLogDto[];
  targetDailyHours?: number;
}

export const hrService = {
  // US1: Employee Profile Setup & Management
  listEmployees: async (search?: string): Promise<EmployeeDto[]> => {
    const res = await apiClient.get<ApiResponse<EmployeeDto[]>>(
      '/admin/hr/employees',
      {
        params: search ? { search } : {},
      }
    );
    return res.data?.data ?? [];
  },

  saveEmployeeProfile: async (
    payload: SaveEmployeeProfilePayload
  ): Promise<ApiResponse<EmployeeProfileMutationResult>> => {
    const res = await apiClient.post<ApiResponse<EmployeeProfileMutationResult>>(
      '/admin/hr/employees',
      payload
    );
    return res.data;
  },

  provisionEmployee: async (
    payload: ProvisionEmployeePayload
  ): Promise<ApiResponse<ProvisionEmployeeResult>> => {
    const res = await apiClient.post<ApiResponse<ProvisionEmployeeResult>>(
      '/admin/hr/employees/provision',
      payload,
      { headers: { 'Idempotency-Key': createClientId() } }
    );
    return res.data;
  },

  listOrganizationUnits: async (): Promise<OrganizationUnitDto[]> => {
    const res = await apiClient.get<OrganizationUnitDto[]>('/hr/organization/units');
    return res.data ?? [];
  },

  getEmployeeDetail: async (employeeId: string): Promise<EmployeeDetailDto> => {
    const res = await apiClient.get<EmployeeDetailDto>(`/hr/employees/${employeeId}`);
    return res.data;
  },

  listWorkCalendars: async (): Promise<WorkCalendarDto[]> => {
    const res = await apiClient.get<WorkCalendarDto[]>('/hr/admin/shifts/calendars');
    return res.data ?? [];
  },
  listShiftTemplates: async (): Promise<ShiftTemplateDto[]> => {
    const res = await apiClient.get<ShiftTemplateDto[]>('/hr/admin/shifts/templates');
    return res.data ?? [];
  },
  updateWorkCalendar: async (calendarId: string, workingDaysMask: number): Promise<ApiResponse<string>> => {
    const res = await apiClient.patch<ApiResponse<string>>(
      `/hr/admin/shifts/calendars/${calendarId}`,
      { workingDaysMask },
    );
    return res.data;
  },
  createShiftTemplate: async (payload: Omit<ShiftTemplateDto, 'id' | 'version'>): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>('/hr/admin/shifts/templates', payload);
    return res.data;
  },
  listShiftAssignments: async (): Promise<ShiftAssignmentDto[]> => {
    const res = await apiClient.get<ShiftAssignmentDto[]>('/hr/admin/shifts/assignments');
    return res.data ?? [];
  },
  validateShiftAssignments: async (payload: ShiftAssignmentPayload[]): Promise<{ valid: boolean; conflicts: unknown[] }> => {
    const res = await apiClient.post<{ valid: boolean; conflicts: unknown[] }>('/hr/admin/shifts/assignments/validate', payload);
    return res.data;
  },
  publishShiftAssignments: async (payload: ShiftAssignmentPayload[]): Promise<ApiResponse<string[]>> => {
    const res = await apiClient.post<ApiResponse<string[]>>('/hr/admin/shifts/assignments/publish', payload,
      { headers: { 'Idempotency-Key': createClientId() } });
    return res.data;
  },
  updateShiftAssignment: async (assignmentId: string, payload: {
    effectiveFrom: string; effectiveTo?: string | null; reason: string; segments: ShiftSegmentDto[];
  }): Promise<ApiResponse<string>> => {
    const res = await apiClient.patch<ApiResponse<string>>(`/hr/admin/shifts/assignments/${assignmentId}`, payload);
    return res.data;
  },
  getAttendancePolicyConfiguration: async (): Promise<AttendancePolicyConfigurationDto> => {
    const res = await apiClient.get<AttendancePolicyConfigurationDto>('/hr/admin/attendance/policies');
    return res.data;
  },
  createAttendancePolicy: async (payload: {
    code: string; name: string; kind: AttendancePolicyKind; latitude?: number | null; longitude?: number | null;
    radiusMeters: number; maximumAccuracyMeters: number;
  }): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>('/hr/admin/attendance/policies', payload);
    return res.data;
  },
  assignAttendancePolicy: async (payload: {
    attendancePolicyId: string; employeeId?: string | null; shiftTemplateId?: string | null;
    effectiveFrom: string; effectiveTo?: string | null;
  }): Promise<ApiResponse<void>> => {
    const res = await apiClient.post<ApiResponse<void>>('/hr/admin/attendance/policy-assignments', payload);
    return res.data;
  },
  getAttendanceToday: async (): Promise<AttendanceSessionDto | null> => {
    const res = await apiClient.get<AttendanceSessionDto | null>('/hr/self/attendance/today'); return res.data;
  },
  getAttendanceHistory: async (): Promise<AttendanceSessionDto[]> => {
    const res = await apiClient.get<AttendanceSessionDto[]>('/hr/self/attendance'); return res.data ?? [];
  },
  clockInSecure: async (evidence: { latitude?: number; longitude?: number; accuracy?: number; deviceToken?: string }): Promise<ApiResponse<{ sessionId: string }>> => {
    const res = await apiClient.post<ApiResponse<{ sessionId: string }>>('/hr/self/attendance/clock-in', evidence, { headers: { 'Idempotency-Key': createClientId() } }); return res.data;
  },
  startAttendanceBreak: async (kind: AttendanceBreakKind = 'Regular'): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>('/hr/self/attendance/breaks/start', { kind }, { headers: { 'Idempotency-Key': createClientId() } }); return res.data;
  },
  endAttendanceBreak: async (breakId: string): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(`/hr/self/attendance/breaks/${breakId}/end`, {}, { headers: { 'Idempotency-Key': createClientId() } }); return res.data;
  },
  clockOutSecure: async (): Promise<ApiResponse<{ sessionId: string }>> => {
    const res = await apiClient.post<ApiResponse<{ sessionId: string }>>('/hr/self/attendance/clock-out', {}, { headers: { 'Idempotency-Key': createClientId() } }); return res.data;
  },
  submitAttendanceCorrection: async (payload: { attendanceSessionId: string; proposedClockedInAt?: string | null; proposedClockedOutAt?: string | null; reason: string; evidenceReference?: string | null }): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>('/hr/self/attendance/corrections', payload); return res.data;
  },
  listAttendanceCorrections: async (): Promise<AttendanceCorrectionDto[]> => {
    const res = await apiClient.get<AttendanceCorrectionDto[]>('/hr/admin/attendance/corrections'); return res.data ?? [];
  },
  listAdminBreakSessions: async (from?: string, to?: string): Promise<AdminBreakSessionDto[]> => {
    const res = await apiClient.get<AdminBreakSessionDto[]>('/hr/admin/attendance/sessions', { params: { ...(from ? { from } : {}), ...(to ? { to } : {}) } }); return res.data ?? [];
  },
  getDailyAttendanceReport: async (from?: string, to?: string): Promise<AdminDailyAttendanceReportDto[]> => {
    const res = await apiClient.get<AdminDailyAttendanceReportDto[]>('/hr/admin/attendance/daily-report', { params: { ...(from ? { from } : {}), ...(to ? { to } : {}) } }); return res.data ?? [];
  },
  decideAttendanceCorrection: async (id: string, payload: { approve: boolean; isHrDecision: boolean; reason: string; expectedVersion: number }): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/hr/admin/attendance/corrections/${id}/decision`, payload); return res.data;
  },
  listLeaveTypes: async (): Promise<LeaveTypeDto[]> => (await apiClient.get<LeaveTypeDto[]>('/hr/self/leave/catalog')).data ?? [],
  listLeaveBalances: async (): Promise<LeaveBalanceDto[]> => (await apiClient.get<LeaveBalanceDto[]>('/hr/self/leave/balances')).data ?? [],
  listMyLeaveRequests: async (): Promise<LeaveRequestDto[]> => (await apiClient.get<LeaveRequestDto[]>('/hr/self/leave/requests')).data ?? [],
  submitLeaveRequest: async (payload: { leaveTypeId: string; startDate: string; endDate: string; dayFraction: number; reason: string; attachmentReference?: string | null }): Promise<ApiResponse<string>> =>
    (await apiClient.post<ApiResponse<string>>('/hr/self/leave/requests', payload)).data,
  withdrawLeaveRequest: async (id: string, reason: string): Promise<ApiResponse<boolean>> =>
    (await apiClient.post<ApiResponse<boolean>>(`/hr/self/leave/requests/${id}/withdraw`, { reason })).data,
  listLeaveApprovalInbox: async (): Promise<ApprovalInboxDto[]> => (await apiClient.get<ApprovalInboxDto[]>('/hr/approvals/inbox')).data ?? [],
  decideLeaveApproval: async (instanceId: string, payload: { approve: boolean; reason: string; expectedVersion: number }): Promise<ApiResponse<boolean>> =>
    (await apiClient.post<ApiResponse<boolean>>(`/hr/approvals/${instanceId}/decision`, payload)).data,
  getLeaveConfiguration: async (): Promise<{ types: LeaveTypeDto[]; policies: LeavePolicyDto[] }> =>
    (await apiClient.get<{ types: LeaveTypeDto[]; policies: LeavePolicyDto[] }>('/hr/admin/leave/config')).data,
  createLeaveType: async (payload: { code: string; name: string; isPaid: boolean; requiresAttachment: boolean; allowsHalfDay: boolean }) =>
    (await apiClient.post('/hr/admin/leave/types', payload)).data,
  createLeavePolicy: async (payload: { name: string; leaveTypeId: string; annualEntitlement: number; maximumCarryover: number; allowNegativeBalance: boolean; effectiveFrom: string; effectiveTo?: string | null; workCalendarId: string }) =>
    (await apiClient.post('/hr/admin/leave/policies', payload)).data,
  createApprovalDelegation: async (payload: { delegateUserId: string; scope: string; startsAt: string; endsAt: string; reason: string }) =>
    (await apiClient.post('/hr/approvals/delegations', payload)).data,
  createApprovalDefinition: async (payload: { requestType: string; name: string; steps: Array<{ order: number; name: string; approverKind: 'DirectManager' | 'Permission' | 'SpecificUser'; permission?: string | null; specificUserId?: string | null; slaMinutes: number; escalationPermission?: string | null }> }) =>
    (await apiClient.post('/hr/approvals/definitions', payload)).data,

  // US2: Employee Attendance Logging (Clock-in/out)
  clockIn: async (): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/hr/attendance/clock-in'
    );
    return res.data;
  },

  clockOut: async (): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/hr/attendance/clock-out'
    );
    return res.data;
  },

  getMyAttendance: async (): Promise<MyAttendanceStatusDto> => {
    const res =
      await apiClient.get<ApiResponse<MyAttendanceStatusDto>>('/hr/attendance/my');
    return res.data?.data ?? { hasProfile: false, logs: [] };
  },

  getAttendance: async (
    search?: string,
    startDate?: string,
    endDate?: string
  ): Promise<AdminAttendanceLogDto[]> => {
    const res = await apiClient.get<ApiResponse<AdminAttendanceLogDto[]>>(
      '/admin/hr/attendance',
      {
        params: {
          ...(search ? { search } : {}),
          ...(startDate ? { startDate } : {}),
          ...(endDate ? { endDate } : {}),
        },
      }
    );
    return res.data?.data ?? [];
  },

};
