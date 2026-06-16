# Quickstart & Verification: E2E Test Flow for Course Content Creation and Access

## 1. Prerequisites
Ensure the local PostgreSQL database, Redis, and ASP.NET Core backend containers are running:
```bash
make up
make migrate
```

## 2. Execute the E2E Test Suite
To run the newly created integration tests, execute `pytest` targeting the test file:
```bash
.venv/bin/pytest tests/test_e2e_content_flow.py -v
```

The test script:
1. Resets the database state using `/api/e2e/seed`.
2. Sets up a new Subject and Teacher.
3. Creates a Yearly Package.
4. Generates Month 1 and Month 2 Courses (Terms).
5. Populates both courses with Free and Paid lessons containing videos, exams, and homework.
6. Verifies that the student user cannot access paid lessons initially, but can access free lessons after purchasing them for 0 EGP.
7. Recharges the student's wallet via the admin balance adjust API.
8. Purchases the Yearly Package.
9. Verifies that the student can now access all lessons under both Month 1 and Month 2.
