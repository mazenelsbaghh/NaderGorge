export const hrPermissions = {
  legacyManage: 'hr.manage',
  employeeRead: 'hr.employee.read',
  employeeManage: 'hr.employee.manage',
  attendanceManage: 'hr.attendance.manage',
  leaveManage: 'hr.leave.manage',
  payrollView: 'payroll.view',
  payrollConfigure: 'payroll.configure',
  payrollPrepare: 'payroll.prepare',
  payrollReview: 'payroll.review',
  payrollFinalApprove: 'payroll.final_approve',
  payrollPay: 'payroll.pay',
  legacyFinance: 'finance.manage',
} as const;

export const hrAdminRoutePermissions = {
  '/admin/hr': [
    hrPermissions.legacyManage,
    hrPermissions.employeeRead,
    hrPermissions.employeeManage,
    hrPermissions.attendanceManage,
    hrPermissions.leaveManage,
  ],
  '/admin/finance': [
    hrPermissions.legacyFinance,
    hrPermissions.payrollView,
    hrPermissions.payrollConfigure,
    hrPermissions.payrollPrepare,
    hrPermissions.payrollReview,
    hrPermissions.payrollFinalApprove,
    hrPermissions.payrollPay,
  ],
} as const;
