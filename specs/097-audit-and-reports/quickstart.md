# Quickstart Guide: Audit Trail and Reports Cockpit

## Running the Backend

Launch the backend with E2E settings:
```bash
ASPNETCORE_ENVIRONMENT=E2e API_CALLBACK_SECRET=zXcVbNmQweRtyUiOpAsDfGhJkL1234567890ZaSdfGhjKlQwe dotnet run --project backend/src/NaderGorge.API/NaderGorge.API.csproj
```

## Seeding & Testing Data

Use the E2E Testing controller to seed test data (employees, tasks, CRM calls, packages):
```bash
curl -X POST http://localhost:5245/api/e2e/seed
```

## Running Automated Tests

Run the Python integration test suite:
```bash
tests/venv/bin/python -m pytest tests/test_audit_and_reports.py -v
```

## Verifying on the UI

1. Login as an Administrator.
2. Navigate to `/admin/reports`.
3. Switch between:
   * **سجل العمليات والرقابة (Audit Trail)**
   * **لوحة تقارير الأداء (KPI Dashboard)**
4. Modify filters (Date, Role, User) and verify that the data updates.
