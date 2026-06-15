# Data Model & Seeding Strategy

## Database Schema Changes
This feature does not introduce any new database entities, columns, or migrations. It relies on the existing schema.

## Database Seeding Strategy
Both Playwright E2E tests and Python tests require a well-defined initial database state. This is achieved via the `.NET` backend E2E endpoint:
- **Seeding Endpoint**: `POST /api/e2e/seed`
- **Authentication**: Header `X-E2E-Token` containing the value configured in environment (default: `E2eOnlyTestTokenValue123456789012345`)
- **Seeding Fixtures**:
  - `clearDatabase: true` -> Destroys and recreates database schema
  - `seedAdmin: true` -> Creates admin user `20000000000` with password `password`
  - `seedStudents: true` -> Creates student user `20000000001` with password `password`
  - `seedAssistant: true` -> Creates assistant user `20000000003` with password `password`
  - `seedTeacher: true` -> Creates teacher user `20000000004` with password `password`
