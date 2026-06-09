# Quickstart: Separate User Lists

## How to Run the Project Locally

To run the Next.js frontend project:

```bash
cd frontend
npm run dev
```

The frontend runs at `http://localhost:8738`.

## Accessing the New Routes

1. Login as an administrator (`01000000000`/`Admin@123`).
2. Go to `/admin/students` to view the list of Students.
3. Go to `/admin/assistants` to view the list of Assistants.
4. Go to `/admin/admins` to view the list of Administrators.
5. Go to `/admin/users` - it should redirect to `/admin/students`.

## Running Tests

To run the Playwright E2E tests:

```bash
cd frontend
npx playwright test tests/e2e/admin-users.spec.ts
```
