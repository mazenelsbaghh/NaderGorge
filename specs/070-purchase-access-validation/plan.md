# Technical Implementation Plan - Purchase Access Validation E2E Tests

## Proposed Changes
We will create a new python integration test file `tests/test_purchases.py` containing three test functions:
1. `test_unpurchased_access_blocked(mock_package)`:
   - Login student.
   - Assert `GET /api/content/lessons/{lesson_id}` fails with status code 400 or 403.
   - Assert `POST /api/exams/{exam_id}/start` fails with status code 400 or 403.
2. `test_package_code_purchase_flow(mock_package)`:
   - Admin generates a Package code for the `package_id`.
   - New student logins.
   - Activates the code via `POST /api/codes/activate`.
   - Gets lesson details -> assert status 200.
   - Starts exam attempt -> assert status 200.
3. `test_balance_purchase_flow(mock_package)`:
   - Admin generates a Balance code for 150 EGP.
   - New student logins.
   - Activates balance code -> wallet balance becomes 150.
   - Purchases package via `POST /api/student/balance/purchase` with package ID.
   - Gets lesson details -> assert status 200.
   - Starts exam attempt -> assert status 200.

## Verification Plan
We will run `pytest tests/test_purchases.py` inside the python testing virtual environment.
To ensure the backend runs and handles the requests, the E2e testing docker setup is used.
We will run `pytest` inside the terminal.
