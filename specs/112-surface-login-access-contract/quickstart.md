# Quickstart: Surface Login and Access Contract

This quickstart guides you through running the development environment and verifying the surface-specific login and access control behavior.

## Running the Application Locally

1. Start the backend:
   ```bash
   dotnet run --project backend/src/NaderGorge.API
   ```
2. Start the frontend:
   ```bash
   cd frontend
   npm run dev
   ```

## Verifying Surfaces

In local development, the surfaces map to different ports:
- **Landing Page**: `http://localhost:8738`
- **Student Portal**: `http://localhost:8739`
- **Admin Portal**: `http://localhost:8740`
- **Teacher Portal**: `http://localhost:8741`
- **Assistant Portal**: `http://localhost:8742`

### Scenarios to Test

1. **Gate Verification**: Access `http://localhost:8739/student/packages` while logged out. Verify redirection to `http://localhost:8739/login` displaying the "بوابة الطالب" (Student Gateway) brand.
2. **Access Prevention**: Log in as a student on `http://localhost:8739`. Attempt to navigate to `http://localhost:8739/admin`. Verify you see a branded 404/Forbidden page instead of a redirect.
