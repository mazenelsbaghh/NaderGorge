# Tasks: Miscellaneous Fixes and Improvements

### Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

### Tasks Checklist

#### Backend Implementation Tasks

- [x] **Task 1: Add `SuspensionReason` property to User entity**
  - **File**: `backend/src/NaderGorge.Domain/Entities/User.cs` (around line 12)
  - **Instruction**: Add a new public string property:
    ```csharp
    public string? SuspensionReason { get; set; }
    ```

- [x] **Task 2: Save Suspension Reason in `ToggleStudentSystemAccessCommandHandler`**
  - **File**: `backend/src/NaderGorge.Application/Features/Admin/Commands/ToggleStudentSystemAccessCommand.cs` (around line 44)
  - **Instruction**: Store the reason on the user object:
    ```csharp
    user.SuspensionReason = request.IsActive ? null : request.Reason;
    ```

- [x] **Task 3: Update `LoginCommandHandler` to output Arabic disabled message with reason**
  - **File**: `backend/src/NaderGorge.Application/Features/Auth/Commands/LoginCommand.cs` (around lines 48-58)
  - **Instruction**: Replace the database audit log search with a direct check on `user.SuspensionReason`:
    ```csharp
    if (!user.IsActive)
    {
        var reasonText = !string.IsNullOrWhiteSpace(user.SuspensionReason) ? $" السبب: {user.SuspensionReason}." : "";
        throw new UnauthorizedAccessException($"تم تعطيل الحساب.{reasonText} برجاء التواصل مع الدعم الفني: 01272629122");
    }
    ```

- [x] **Task 4: Update `StudentPackageDto` properties**
  - **File**: `backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs` (around line 61)
  - **Instruction**: Replace `StudentPackageDto` properties to match:
    ```csharp
    public class StudentPackageDto
    {
        public Guid Id { get; set; } // PackageId
        public Guid AccessGrantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime EnrolledAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public decimal Progress { get; set; }
        public bool IsActive { get; set; }
        public string PurchaseMethod { get; set; } = string.Empty; // "Code" or "Balance"
        public decimal Price { get; set; }
    }
    ```

- [x] **Task 5: Update `GetStudentProfileDetailQueryHandler` to query all package grants**
  - **File**: `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs` (around lines 51-69)
  - **Instruction**: Query all packages regardless of `IsActive`, select `AccessCodeId`, `IsActive`, and `Id`. Map them correctly to `StudentPackageDto` with purchase method (check if `AccessCodeId != null`).
    ```csharp
    var packageGrants = await _context.StudentAccessGrants
        .Where(g => g.UserId == request.UserId && g.PackageId.HasValue)
        .Select(g => new { g.Id, g.PackageId, g.CreatedAt, g.ExpiresAt, g.IsActive, g.AccessCodeId })
        .ToListAsync(cancellationToken);

    var packages = new List<StudentPackageDto>();
    foreach (var grant in packageGrants)
    {
        var p = await _context.Packages.FindAsync(new object[] { grant.PackageId!.Value }, cancellationToken);
        packages.Add(new StudentPackageDto
        {
            Id = grant.PackageId.Value,
            AccessGrantId = grant.Id,
            Name = p != null ? p.Name : "Unknown",
            EnrolledAt = grant.CreatedAt,
            ExpiresAt = grant.ExpiresAt,
            Progress = 0,
            IsActive = grant.IsActive,
            PurchaseMethod = grant.AccessCodeId.HasValue ? "Code" : "Balance",
            Price = p != null ? p.Price : 0m
        });
    }
    ```

- [x] **Task 6: Create `CancelPackageGrantCommand` and Handler**
  - **File**: `backend/src/NaderGorge.Application/Features/Admin/Commands/CancelPackageGrantCommand.cs` [NEW]
  - **Instruction**: Implement deactivation logic and refund EGP to student's balance if `RefundBalance` is true and transaction was completed.
    - Check if grant exists and is active.
    - Set `IsActive = false`.
    - If `RefundBalance` is true, find `Package` price, add to `StudentBalance.CurrentBalance`, and save `BalanceTransaction` with type "Refund".
    - Save Audit log.

- [x] **Task 7: Register Cancel Package endpoint in `AdminController`**
  - **File**: `backend/src/NaderGorge.API/Controllers/AdminController.cs` (around line 118)
  - **Instruction**: Add endpoint:
    ```csharp
    [HttpPost("users/students/{userId:guid}/packages/{accessGrantId:guid}/cancel")]
    public async Task<IActionResult> CancelPackage(Guid userId, Guid accessGrantId, [FromBody] CancelPackageRequest dto)
    {
        var result = await _mediator.Send(new CancelPackageGrantCommand(accessGrantId, dto.RefundBalance, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }
    ```
    And define:
    ```csharp
    public record CancelPackageRequest(bool RefundBalance);
    ```

- [x] **Task 8: Increase Rate Limit Thresholds**
  - **File**: `backend/src/NaderGorge.API/Configuration/RateLimitingConfig.cs` (around lines 21, 32, 43, 54)
  - **Instruction**: Change `PermitLimit` values:
    - `auth`: increase to 30
    - `codes`: increase to 20
    - `video-session`: increase to 30
    - `Global`: increase to 1000

- [x] **Task 9: Scaffold and Apply Database Migration**
  - **Instruction**: Run commands:
    ```bash
    make migrate-add NAME=AddSuspensionReasonToUser
    make migrate
    ```

#### Frontend Implementation Tasks

- [x] **Task 10: Support `NEXT_PUBLIC_APP_URL` in `QrDisplay.tsx`**
  - **File**: `frontend/src/components/codes/QrDisplay.tsx` (around lines 20-25)
  - **Instruction**: Resolve base URL:
    ```typescript
    const effectiveBaseUrl = process.env.NEXT_PUBLIC_APP_URL || baseUrl || (typeof window !== 'undefined' ? window.location.origin : 'https://nadergeorge.com');
    ```
    Add a warning UI banner if `effectiveBaseUrl` matches `0.0.0.0` or `localhost`.

- [x] **Task 11: Add `cancelStudentPackage` to `adminService`**
  - **File**: `frontend/src/services/admin-service.ts` (around line 675)
  - **Instruction**: Add:
    ```typescript
    cancelStudentPackage: async (userId: string, accessGrantId: string, refundBalance: boolean) => {
      const res = await apiClient.post(`/admin/users/students/${userId}/packages/${accessGrantId}/cancel`, { refundBalance });
      return res.data;
    }
    ```

- [x] **Task 12: Reposition Balance Edit Button inside Card**
  - **File**: `frontend/src/app/admin/users/[id]/page.tsx` (around lines 416-431)
  - **Instruction**: Remove the absolute edit button and pass a clean inline button inside `AdminStatCard` children.

- [x] **Task 13: Redirect Logged-In Users on Login page**
  - **File**: `frontend/src/app/(public)/login/page.tsx` (around lines 38-45)
  - **Instruction**: Check State Store in `useEffect` and redirect authenticated users automatically to dashboard. Hide form layout.

- [x] **Task 14: Sidebar Navigation Expand on Hover (Admin)**
  - **File**: `frontend/src/components/admin/AdminShellChrome.tsx` (around lines 128-195)
  - **Instruction**: Modify classes to support expanded sidebar state and show labels on hover.

- [x] **Task 15: Sidebar Navigation Expand on Hover (Student)**
  - **File**: `frontend/src/components/layout/StudentShellChrome.tsx` (around lines 155-242)
  - **Instruction**: Apply similar classes to expand student sidebar on hover.

- [x] **Task 16: Render Package Cancellation option in student detail page**
  - **File**: `frontend/src/app/admin/users/[id]/page.tsx` (around lines 439-460)
  - **Instruction**: Add columns to table for Purchase Method and Cancel Action. Add warning modals to display original payment type and process deactivations.

- [x] **Task 17: Update Disable account action to prompt suspension reason**
  - **File**: `frontend/src/app/admin/users/[id]/page.tsx` (around lines 101-108, and bottom modals section)
  - **Instruction**: Change the toggle status method to open a modal asking for the suspension reason if the account is being disabled.
