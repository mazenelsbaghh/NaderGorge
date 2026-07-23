# Contract: Employee, Organization and Contract

## Employee provisioning

`POST /api/hr/admin/employees`

Headers: `Idempotency-Key` required. Permission: `hr.employee.manage`.

Request includes account `{fullName,phoneNumber,password,roleIds}`, employment `{hireDate,workMode,locationId,organizationUnitId,positionId,gradeId,managerEmployeeId,costCenterId}`, contract `{type,startDate,endDate,probationEndDate,baseSalary,currency}`, initial shift and leave policy ids.

Response `201`: `{ employeeId,userId,employeeNumber,status,version }`. The response exists only after all rows and onboarding tasks commit. Failure leaves no User or EmployeeProfile.

Errors: `PHONE_ALREADY_EXISTS`, `ROLE_NOT_EMPLOYABLE`, `MANAGER_INVALID`, `ASSIGNMENT_OVERLAP`, `CONTRACT_INVALID`, `SHIFT_INVALID`, `EMPLOYEE_PROVISIONING_FAILED`.

## Employee reads and lifecycle

- `GET /api/hr/admin/employees` — explicit EmployeeProfile source, scoped search/filter/page.
- `GET /api/hr/admin/employees/{id}` — sections filtered by independent sensitive permissions.
- `PATCH /api/hr/admin/employees/{id}` — basic non-temporal data only.
- `POST /api/hr/admin/employees/{id}/assignments` — effective-dated transfer/promotion/manager change.
- `POST /api/hr/admin/employees/{id}/contracts` — draft/activate/renew/terminate contract.
- `POST /api/hr/admin/employees/{id}/lifecycle` — suspend/reactivate/terminate/archive with reason.

No employee delete endpoint. Termination disables future access only after offboarding checkpoint or approved exception; records remain.

## Organization

- `GET/POST/PATCH /api/hr/admin/organization/units`
- `POST /api/hr/admin/organization/units/{id}/move`
- `GET/POST/PATCH /api/hr/admin/jobs`, `/grades`, `/locations`, `/cost-centers`
- `GET /api/hr/admin/organization/tree?effectiveOn=`

Moves reject cycles and preserve effective history. One company root is implicit; no `companyId` in public contracts.

## Frontend routes

`/admin/hr/employees`, `/admin/hr/employees/{id}`, `/admin/hr/organization`, `/admin/hr/contracts`. Create employee is one wizard with validation review and one submit, not chained API calls.
