# Quickstart Guide: Remove Programs and Link Packages Directly to Subjects

## Database Migration
To apply the database schema changes and migrate existing packages, run:
```bash
make migrate
```

## Running Natively
To start the backend and frontend services natively:
```bash
# In backend directory
dotnet run --project src/NaderGorge.API

# In frontend directory
npm run dev
```

## Running with Docker
To start the entire Docker stack:
```bash
make up
```
Once healthy, verify endpoints at:
- Swagger: `http://localhost:5245/swagger`
- Admin Surface: `http://localhost:8738/admin`
- Teacher Surface: `http://localhost:8738/teacher`
