# Quickstart Guide: Payroll, Teacher Finance, and Activated Code Accounting

## 1. Setting Up the Environment

Ensure the Docker stack is running:
```bash
make up
```

Apply the EF Core migrations to update the database schema:
```bash
make migrate
```
*(Verify that tables `payroll_records`, `payroll_adjustments`, `teacher_accounts`, `teacher_payouts`, and `access_code_activation_logs` have been successfully created).*

---

## 2. Running Automated Tests

### Backend Unit Tests
Run the newly created unit tests for payroll calculation and teacher commission activation rules:
```bash
dotnet test backend/tests/NaderGorge.Application.Tests --filter Category=Finance
```

### Python/API Smoke Tests
Verify endpoints, permissions, and isolation boundaries:
```bash
python3 -m pytest tests/test_teacher_finance.py -v
```

---

## 3. Testing APIs Manually (cURL Examples)

### Admin: Generate Payroll
```bash
curl -X POST http://localhost:5245/api/admin/finance/payroll/generate \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin_jwt_token>" \
  -d '{
    "month": 6,
    "year": 2026
  }'
```

### Admin: Add Payroll Adjustment
```bash
curl -X POST http://localhost:5245/api/admin/finance/payroll/<payroll_id>/adjustments \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin_jwt_token>" \
  -d '{
    "type": 0,
    "amount": 500.00,
    "reason": "Performance Bonus"
  }'
```

### Student: Redeem Access Code
```bash
curl -X POST http://localhost:5245/api/student/codes/activate \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <student_jwt_token>" \
  -d '{
    "code": "12345678"
  }'
```
*(Verify in database that `AccessCodeActivationLog` is recorded and the corresponding teacher's balance in `TeacherAccount` increases).*

### Teacher: Request Payout
```bash
curl -X POST http://localhost:5245/api/teacher/finance/payouts \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <teacher_jwt_token>" \
  -d '{
    "amount": 200.00
  }'
```

### Admin: Resolve Payout (Approve/Pay)
```bash
curl -X POST http://localhost:5245/api/admin/finance/payouts/<payout_id>/resolve \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin_jwt_token>" \
  -d '{
    "status": 1
  }'
```
