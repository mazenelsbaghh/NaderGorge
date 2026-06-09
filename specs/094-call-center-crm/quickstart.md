# CRM Developer Quickstart

## 1. Database Migrations
Run the following commands in the backend to generate and apply the migrations:
```bash
# Generate the migration
dotnet ef migrations add AddCrmEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API

# Apply the migration to the database
dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API
```

## 2. Running the Backend
From the `backend/src/NaderGorge.API` folder, start the dev API:
```bash
dotnet run
```
The API is available at `http://localhost:5245`.

## 3. Running the Frontend
From the `frontend` folder, start Next.js:
```bash
npm run dev
```
Open `http://localhost:8738/admin/crm` (Admin cockpit) or `http://localhost:8738/assistant/crm` (Agent board).

## 4. Running Backend Tests
To run C# CRM tests:
```bash
dotnet test backend/tests/NaderGorge.Application.Tests --filter "FullyQualifiedName~CRM"
```
