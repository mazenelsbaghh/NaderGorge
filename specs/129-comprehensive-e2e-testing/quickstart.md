# Quickstart Guide: Running E2E and API Tests

## 1. Start External Services (Docker)
Ensure Postgres and Redis are running:
```bash
make up
```

## 2. Start Backend (in E2e mode)
Backend MUST run natively in `E2e` environment on port `5245`:
```bash
cd backend/src/NaderGorge.API
ASPNETCORE_ENVIRONMENT=E2e dotnet run --environment E2e --urls "http://localhost:5245"
```

## 3. Start Frontend (on port 3000)
Frontend Next.js server MUST run natively on port `3000` so Playwright tests can target it:
```bash
cd frontend
npx next dev -p 3000
```

## 4. Run Playwright E2E Tests
In another terminal, run Playwright test suite inside `frontend/`:
```bash
cd frontend
npm run test:e2e
```

## 5. Run Python API/E2E Tests
Run the Python test suite:
```bash
cd tests
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
pytest
```
