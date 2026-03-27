# Quickstart Guide: Development - Phase 2.5 Admin CMS

This brief guide sets up your local environment to begin working on Phase 2.5 UI features.

## Prerequisites
1. Docker Compose setup for Postgres & Redis
2. NaderGorge.API backend running with `.EF Core AddPhase2AcademicOps` migration.
3. Node 18+ and Next.js frontend running `npm run dev` in `frontend/`.

## Running API / Updating Permissions
If you are developing locally on Admin endpoints, ensure you have seeded your own dev account as Admin.
To force creation of a new Admin Assistant token for API testing:

```bash
cd backend/src/NaderGorge.API
dotnet run --launch-profile E2e
```

## Adding Assistant Locally Fast

1. Open `http://localhost:3000/admin/users`.
2. Assuming you implemented the UI, click "Edit User Role" and select "Assistant".
3. Validate by logging in with that user.

## Writing Admin Role Endpoints

If your existing `AdminController` inside `NaderGorge.API` doesn't support generic claim modification yet, create it. Identity's `UserManager.AddToRoleAsync(user, RoleNames.Assistant)` simplifies this heavily over mapping custom RBAC entities.
