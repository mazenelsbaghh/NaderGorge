# Data Model & Contracts: Audit Trail and Reports Cockpit

## API Request Contracts

### 1. Get Audit Logs List
* **Route**: `GET /api/admin/reports/audit`
* **Query Parameters**:
  * `startDate` (DateTime?): Filter logs since this date.
  * `endDate` (DateTime?): Filter logs until this date.
  * `performedByUserId` (Guid?): Filter by admin user.
  * `entityType` (string?): Filter by affected table (e.g. PayrollRecord, TaskItem).
  * `page` (int): Page number (default: 1).
  * `pageSize` (int): Size per page (default: 20).
* **Response DTO**: `PagedResult<AuditLogDetailDto>`
  ```csharp
  public record AuditLogDetailDto(
      Guid Id,
      string Action,
      string EntityType,
      Guid? EntityId,
      Guid? PerformedByUserId,
      string? PerformedByUserName,
      string? PerformedByUserPhone,
      string? OldValues,
      string? NewValues,
      string? IpAddress,
      DateTime CreatedAt
  );
  ```

### 2. Get KPI Dashboard Reports
* **Route**: `GET /api/admin/reports/kpi`
* **Query Parameters**:
  * `startDate` (DateTime?): Filter metrics from this date.
  * `endDate` (DateTime?): Filter metrics to this date.
  * `roleName` (string?): Filter metrics for specific roles.
  * `employeeId` (Guid?): Filter metrics for specific employee.
* **Response DTO**: `KpiDashboardDto`
  ```csharp
  public record KpiDashboardDto(
      AttendanceKpiDto Attendance,
      TaskKpiDto Tasks,
      List<CrmOutcomeKpiDto> CrmOutcomes,
      MediaKpiDto Media,
      PaymentKpiDto Payments,
      List<PayrollStatusKpiDto> PayrollStatus
  );

  public record AttendanceKpiDto(
      int TotalLogs,
      int PresentCount,
      int LateCount,
      int AbsentCount,
      decimal PresentRate,
      decimal LateRate,
      decimal AbsentRate
  );

  public record TaskKpiDto(
      int TotalTasks,
      int CompletedCount,
      int PendingCount,
      int OverdueCount,
      decimal CompletionRate
  );

  public record CrmOutcomeKpiDto(
      string Outcome,
      int Count
  );

  public record MediaKpiDto(
      int TotalItems,
      int PublishedCount,
      decimal AverageProductionDays
  );

  public record PaymentKpiDto(
      int TotalTransactions,
      int AutoMatchedCount,
      int CouponActivatedCount,
      decimal AutoMatchRate
  );

  public record PayrollStatusKpiDto(
      string Status,
      int Count
  );
  ```
